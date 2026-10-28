using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Screenings;

// "START A SCREENING": the quickest way into the 23 questions.
// The clinician either adds a new patient (name, language, consent) or picks a returning
// patient, and is taken straight to the screening form. No detour via the patient list.
public class StartModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public StartModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // Returning patients who can be screened (consent recorded), for the drop-down
    public List<Patient> ReturningPatients { get; set; } = new();
    public int PatientsWithoutConsent { get; set; }

    [BindProperty]
    public NewPatientInput NewPatient { get; set; } = new();

    [BindProperty]
    public int? ExistingPatientId { get; set; }

    public string? ExistingError { get; set; }

    public class NewPatientInput
    {
        [Required(ErrorMessage = "Enter her full name."), MaxLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Preferred language")]
        public string PreferredLanguage { get; set; } = "en";

        [Display(Name = "She has agreed to her answers being recorded and used for her care")]
        public bool ConsentGiven { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    // Button 1: add a new patient and go straight to her screening
    public async Task<IActionResult> OnPostNewAsync()
    {
        ModelState.Remove(nameof(ExistingPatientId));

        var allowed = new[] { "en", "zu", "xh", "af" };
        if (!allowed.Contains(NewPatient.PreferredLanguage))
            ModelState.AddModelError("NewPatient.PreferredLanguage", "Choose a language from the list.");

        // POPIA: we can't screen her without consent
        if (!NewPatient.ConsentGiven)
            ModelState.AddModelError("NewPatient.ConsentGiven", "Record her consent before starting the screening.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var patient = new Patient
        {
            FullName = NewPatient.FullName.Trim(),
            PreferredLanguage = NewPatient.PreferredLanguage,
            ConsentGiven = true,
            ConsentRecordedAt = DateTime.UtcNow,
            ConsentRecordedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreatePatient", "Patient", patient.Id, "Consent recorded");

        return RedirectToPage("/Screenings/New", new { patientId = patient.Id });
    }

    // Button 2: pick a returning patient and go straight to her screening
    public async Task<IActionResult> OnPostExistingAsync()
    {
        var patient = ExistingPatientId is int id ? await _db.Patients.FindAsync(id) : null;

        if (patient == null || !patient.ConsentGiven)
        {
            ExistingError = "Choose a patient from the list.";
            ModelState.Clear();
            await LoadAsync();
            return Page();
        }

        return RedirectToPage("/Screenings/New", new { patientId = patient.Id });
    }

    private async Task LoadAsync()
    {
        ReturningPatients = await _db.Patients
            .Where(p => p.ConsentGiven)
            .OrderBy(p => p.FullName)
            .ToListAsync();
        PatientsWithoutConsent = await _db.Patients.CountAsync(p => !p.ConsentGiven);
    }
}