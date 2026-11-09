using System.ComponentModel.DataAnnotations;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// MANAGE ONE PATIENT'S RECORD (admins only)
// Correct her name or language, move her to another clinic, delete a wrongly captured
// screening, or delete her whole record. Every change is written to the audit log.
public class EditPatientModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly PortalService _portal;
    private readonly UserManager<IdentityUser> _users;

    public EditPatientModel(ApplicationDbContext db, AuditService audit, PortalService portal,
                            UserManager<IdentityUser> users)
    {
        _db = db;
        _audit = audit;
        _portal = portal;
        _users = users;
    }

    public Patient Patient { get; set; } = null!;
    public string? PortalUsername { get; set; }
    public List<Screening> History { get; set; } = new();
    public List<Clinic> Clinics { get; set; } = new();

    [BindProperty]
    public PatientInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    // The same four languages as the "Add patient" page
    public static readonly Dictionary<string, string> Languages = new()
    {
        ["en"] = "English",
        ["zu"] = "isiZulu",
        ["xh"] = "isiXhosa",
        ["af"] = "Afrikaans"
    };

    public class PatientInput
    {
        [Required, MaxLength(100), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, Display(Name = "Preferred language")]
        public string PreferredLanguage { get; set; } = "en";

        [Display(Name = "Clinic")]
        public int ClinicId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();

        // Fill the form with what's saved now
        Input = new PatientInput
        {
            FullName = Patient.FullName,
            PreferredLanguage = Patient.PreferredLanguage,
            ClinicId = Patient.ClinicId
        };

        await _audit.LogAsync("AdminViewPatient", "Patient", id);
        return Page();
    }

    // "Save": name, language, and clinic
    public async Task<IActionResult> OnPostSaveAsync(int id)
    {
        var patient = await _db.Patients.Include(p => p.Clinic).FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return NotFound();

        if (!Languages.ContainsKey(Input.PreferredLanguage))
            ModelState.AddModelError("Input.PreferredLanguage", "Choose a language from the list.");

        var newClinicName = await _db.Clinics
            .Where(c => c.Id == Input.ClinicId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();
        if (newClinicName == null)
            ModelState.AddModelError("Input.ClinicId", "Choose a clinic from the list.");

        if (!ModelState.IsValid)
        {
            await LoadAsync(id);
            return Page();
        }

        bool moved = patient.ClinicId != Input.ClinicId;
        string oldClinicName = patient.Clinic.Name;

        patient.FullName = Input.FullName.Trim();
        patient.PreferredLanguage = Input.PreferredLanguage;
        patient.ClinicId = Input.ClinicId;
        await _db.SaveChangesAsync();

        await _audit.LogAsync("AdminUpdatePatient", "Patient", id,
            moved ? $"Moved from {oldClinicName} to {newClinicName}" : "Details corrected");

        StatusMessage = moved
            ? $"Saved. She now belongs to {newClinicName}: only its clinicians can see her, with all her screenings and check-ins."
            : "Saved.";
        return RedirectToPage(new { id });
    }

    // "Delete" on one screening: e.g. captured for the wrong patient
    public async Task<IActionResult> OnPostDeleteScreeningAsync(int id, int screeningId)
    {
        // Only a screening that really belongs to THIS patient
        var screening = await _db.Screenings.FirstOrDefaultAsync(s => s.Id == screeningId && s.PatientId == id);
        if (screening == null) return NotFound();

        _db.Screenings.Remove(screening);   // her answers to it are deleted with it (cascade)
        await _db.SaveChangesAsync();
        await _audit.LogAsync("AdminDeleteScreening", "Screening", screeningId, $"Patient {id}");

        StatusMessage = "Screening deleted.";
        return RedirectToPage(new { id });
    }

    // "Delete her record": POPIA right to deletion, same as the clinician's button
    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirmDelete)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        if (!confirmDelete)
        {
            StatusMessage = "Tick the confirmation box to delete her record.";
            return RedirectToPage(new { id });
        }

        // Her portal login goes too
        if (patient.UserId != null)
        {
            var login = await _users.FindByIdAsync(patient.UserId);
            if (login != null) await _users.DeleteAsync(login);
        }

        _db.Patients.Remove(patient);   // screenings, answers, check-ins and invites cascade
        await _db.SaveChangesAsync();
        // The audit keeps the id only, never her name
        await _audit.LogAsync("AdminDeletePatient", "Patient", id, "Deleted by admin");

        TempData["StatusMessage"] = "Her record and portal login have been deleted.";
        return RedirectToPage("Patients");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var patient = await _db.Patients.Include(p => p.Clinic).FirstOrDefaultAsync(p => p.Id == id);
        if (patient == null) return false;

        Patient = patient;
        PortalUsername = await _portal.GetUsernameAsync(patient);
        History = await _db.Screenings
            .Where(s => s.PatientId == id)
            .OrderByDescending(s => s.ScreenedAt)
            .ToListAsync();
        Clinics = await _db.Clinics.OrderBy(c => c.Name).ToListAsync();
        return true;
    }
}