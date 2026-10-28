using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Screenings;

// THE CLINICIAN'S "CREATE QR CODE" PAGE
// GET shows a button; pressing it (POST) creates a new invite and displays its QR code.
// We create the invite on POST, not on page load, so refreshing the page doesn't
// quietly create extra links.
public class InviteModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly InviteService _invites;
    private readonly AuditService _audit;

    public InviteModel(ApplicationDbContext db, InviteService invites, AuditService audit)
    {
        _db = db;
        _invites = invites;
        _audit = audit;
    }

    public Patient Patient { get; set; } = null!;
    public string? Link { get; set; }
    public string? QrImage { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient == null) return NotFound();
        Patient = patient;

        if (!patient.ConsentGiven)
            return RedirectToPage("/Patients/Details", new { id = patientId });

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient == null) return NotFound();
        Patient = patient;

        try
        {
            var invite = await _invites.CreateAsync(patientId, User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // Build the full web address (e.g. http://192.168.1.5:5018/SelfScreen/abc123...).
            // It uses the address the clinician opened the app with, so the phone can reach it.
            Link = Url.Page("/SelfScreen", pageHandler: null, values: new { token = invite.Token }, protocol: Request.Scheme);
            QrImage = InviteService.QrDataUri(Link!);
            ExpiresAt = invite.ExpiresAt;

            await _audit.LogAsync("CreateInvite", "Patient", patientId, $"Invite {invite.Id}");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }
}
