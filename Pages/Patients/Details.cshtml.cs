using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Patients;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;
    private readonly ClinicScopeService _scope;
    private readonly PortalService _portal;
    private readonly UserManager<IdentityUser> _users;

    public DetailsModel(ApplicationDbContext db, AuditService audit, ClinicScopeService scope,
                        PortalService portal, UserManager<IdentityUser> users)
    {
        _db = db;
        _audit = audit;
        _scope = scope;
        _portal = portal;
        _users = users;
    }

    public Patient Patient { get; set; } = null!;
    public List<Screening> History { get; set; } = new();

    // Her portal username, if she has portal access
    public string? PortalUsername { get; set; }

    // A brand-new setup code. TempData shows it ONCE (straight after it's made) and then forgets it.
    [TempData]
    public string? NewSetupCode { get; set; }

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
        var patient = await _scope.FindPatientAsync(id);
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
        var patient = await _scope.FindPatientAsync(id);
        if (patient == null) return NotFound();

        patient.ConsentGiven = false;
        patient.ConsentRecordedAt = DateTime.UtcNow;
        patient.ConsentRecordedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("WithdrawConsent", "Patient", id);

        StatusMessage = "Consent withdrawn. No new screenings can be saved for her.";
        return RedirectToPage(new { id });
    }

    // "Create portal access": a generated username and a one-time setup code
    public async Task<IActionResult> OnPostCreatePortalAsync(int id)
    {
        var patient = await _scope.FindPatientAsync(id);
        if (patient == null) return NotFound();

        try
        {
            var (username, code) = await _portal.CreateAccessAsync(patient);
            NewSetupCode = code;
            await _audit.LogAsync("CreatePortalAccess", "Patient", id, $"Username {username}");
            StatusMessage = "Portal access created. Give her the username and setup code below.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    // "New setup code": lost code or forgotten password. The old code stops working.
    public async Task<IActionResult> OnPostNewSetupCodeAsync(int id)
    {
        var patient = await _scope.FindPatientAsync(id);
        if (patient == null) return NotFound();

        try
        {
            NewSetupCode = await _portal.NewSetupCodeAsync(patient);
            await _audit.LogAsync("NewPortalSetupCode", "Patient", id);
            StatusMessage = "New setup code made. Any older code no longer works.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    // POPIA "right to deletion": if she asks, we delete her record completely.
    // Deleting the patient also deletes her screenings, answers and QR invites, because
    // the database is set to "cascade" deletes down to every row that belongs to her.
    // The clinician must tick a confirmation box first, because this can't be undone.
    public async Task<IActionResult> OnPostDeleteAsync(int id, bool confirmDelete)
    {
        var patient = await _scope.FindPatientAsync(id);
        if (patient == null) return NotFound();

        if (!confirmDelete)
        {
            StatusMessage = "Tick the confirmation box to delete her record.";
            return RedirectToPage(new { id });
        }

        // Her portal login is deleted too
        if (patient.UserId != null)
        {
            var login = await _users.FindByIdAsync(patient.UserId);
            if (login != null) await _users.DeleteAsync(login);
        }

        _db.Patients.Remove(patient);
        await _db.SaveChangesAsync();
        // The audit entry keeps the id only, never the name, so it survives the deletion
        await _audit.LogAsync("DeletePatient", "Patient", id, "Deleted at patient's request");

        return RedirectToPage("Index");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var patient = await _scope.FindPatientAsync(id);
        if (patient == null) return false;

        Patient = patient;
        PortalUsername = await _portal.GetUsernameAsync(patient);
        History = await _db.Screenings
            .Where(s => s.PatientId == id)
            .OrderByDescending(s => s.ScreenedAt)
            .ToListAsync();
        return true;
    }
}
