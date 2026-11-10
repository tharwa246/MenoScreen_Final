using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// EVERY SCREENING IN EVERY CLINIC (admins only), newest first.
// Filter by clinic and band to find one, then delete it if it was captured by mistake.
// Shows the result only (score, band), never her answers.
public class ScreeningsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public ScreeningsModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public record ScreeningRow(int Id, DateTime ScreenedAt, int PatientId, string PatientName, string ClinicName,
                               int MenoLikelihood, string Band, string Source);

    public List<ScreeningRow> Rows { get; set; } = new();
    public List<Clinic> Clinics { get; set; } = new();

    // How many match the filters in total (the table shows at most 200)
    public int Total { get; set; }
    public const int PageSize = 200;

    [BindProperty(SupportsGet = true)]
    public int? ClinicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Band { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        Clinics = await _db.Clinics.OrderBy(c => c.Name).ToListAsync();

        var query = _db.Screenings.AsQueryable();

        if (ClinicId != null)
            query = query.Where(s => s.Patient.ClinicId == ClinicId);

        // Only accept a real band name from the scoring engine's list
        if (!string.IsNullOrEmpty(Band) && ScoringService.Bands.Contains(Band))
            query = query.Where(s => s.Band == Band);

        Total = await query.CountAsync();

        Rows = await query
            .OrderByDescending(s => s.ScreenedAt)
            .Take(PageSize)
            .Select(s => new ScreeningRow(
                s.Id,
                s.ScreenedAt,
                s.PatientId,
                s.Patient.FullName,
                s.Patient.Clinic.Name,
                s.MenoLikelihood,
                s.Band,
                s.Source))
            .ToListAsync();
    }

    // "Delete" on one row. The filters ride along, so the admin lands back on the same list.
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var screening = await _db.Screenings.FindAsync(id);
        if (screening == null) return NotFound();

        _db.Screenings.Remove(screening);   // her answers to it are deleted with it (cascade)
        await _db.SaveChangesAsync();
        await _audit.LogAsync("AdminDeleteScreening", "Screening", id, $"Patient {screening.PatientId}");

        StatusMessage = "Screening deleted.";
        return RedirectToPage(new { clinicId = ClinicId, band = Band });
    }
}