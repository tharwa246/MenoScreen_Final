using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Patients;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public CreateModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Only these fields can come from the form
    public class InputModel
    {
        [Required, MaxLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Preferred language")]
        public string PreferredLanguage { get; set; } = "en";

        [Display(Name = "She has agreed to her answers being recorded and used for her care")]
        public bool ConsentGiven { get; set; }
    }

    // "save" = save and go back to the list; "screen" = save and start screening
    public async Task<IActionResult> OnPostAsync(string next = "save")
    {
        if (!ModelState.IsValid) return Page();

        var allowed = new[] { "en", "zu", "xh", "af" };
        if (!allowed.Contains(Input.PreferredLanguage))
        {
            ModelState.AddModelError("Input.PreferredLanguage", "Choose a language from the list.");
            return Page();
        }

        if (next == "screen" && !Input.ConsentGiven)
        {
            ModelState.AddModelError("Input.ConsentGiven", "Record her consent before starting a screening.");
            return Page();
        }

        var patient = new Patient
        {
            FullName = Input.FullName.Trim(),
            PreferredLanguage = Input.PreferredLanguage
        };

        if (Input.ConsentGiven)
        {
            patient.ConsentGiven = true;
            patient.ConsentRecordedAt = DateTime.UtcNow;
            patient.ConsentRecordedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreatePatient", "Patient", patient.Id, Input.ConsentGiven ? "Consent recorded" : "No consent yet");

        return next == "screen"
            ? RedirectToPage("/Screenings/New", new { patientId = patient.Id })
            : RedirectToPage("Index");
    }
}
