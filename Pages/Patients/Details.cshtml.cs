using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Patients;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public DetailsModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Patient Patient { get; set; } = null!;
    public List<Screening> History { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        await _audit.LogAsync("ViewPatient", "Patient", id);
        return Page();
    }

    // The three buttons on this page each have their own "handler": OnPostConsent,
    // OnPostWithdrawConsent and OnPostDelete. The form says which one with asp-page-handler.
    public async Task<IActionResult> OnPostConsentAsync(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.ConsentGiven = true;
        patient.ConsentRecordedAt = DateTime.UtcNow;
        patient.ConsentRecordedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("RecordConsent", "Patient", id);

        StatusMessage = "Consent recorded. You can now screen her.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostWithdrawConsentAsync(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        patient.ConsentGiven = false;
        patient.ConsentRecordedAt = DateTime.UtcNow;
        patient.ConsentRecordedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("WithdrawConsent", "Patient", id);

        StatusMessage = "Consent withdrawn. No new screenings can be saved for her.";
        return RedirectToPage(new { id });
    }

    // POPIA "right to deletion": if she asks, we delete her record completely.
    // Deleting the patient also deletes her screenings, answers and QR invites, because
    // the database is set to "cascade" deletes down to every row that belongs to her.
    // The clinician must tick a confirmation box first, because this can't be undone.
    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirmDelete)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return NotFound();

        if (!confirmDelete)
        {
            StatusMessage = "Tick the confirmation box to delete her record.";
            return RedirectToPage(new { id });
        }

        _db.Patients.Remove(patient);
        await _db.SaveChangesAsync();
        // The audit entry keeps the id only, never the name, so it survives the deletion
        await _audit.LogAsync("DeletePatient", "Patient", id, "Deleted at patient's request");

        return RedirectToPage("Index");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null) return false;

        Patient = patient;
        History = await _db.Screenings
            .Where(s => s.PatientId == id)
            .OrderByDescending(s => s.ScreenedAt)
            .ToListAsync();
        return true;
    }
}
