using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// ALL PATIENTS IN EVERY CLINIC (admins only: the /Admin folder needs AdminOnly)
// Clinicians keep using /Patients, which only ever shows their own clinic.
// From here an admin opens "Manage" to fix her details, move her clinic, or delete her record.
// Admins see record details, not her answers: clinical detail stays with her clinic (POPIA, least access).
public class PatientsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public PatientsModel(ApplicationDbContext db) => _db = db;

    public record PatientRow(int Id, string FullName, string ClinicName, bool ConsentGiven, bool HasPortal,
                             int ScreeningCount, string? LastBand, DateTime? LastScreenedAt);

    public List<PatientRow> Patients { get; set; } = new();
    public List<Clinic> Clinics { get; set; } = new();

    // The filters come from the address bar (?clinicId=2&search=nomsa), so a filtered list can be bookmarked
    [BindProperty(SupportsGet = true)]
    public int? ClinicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        Clinics = await _db.Clinics.OrderBy(c => c.Name).ToListAsync();

        var query = _db.Patients.AsQueryable();

        if (ClinicId != null)
            query = query.Where(p => p.ClinicId == ClinicId);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().ToLower();
            query = query.Where(p => p.FullName.ToLower().Contains(term));
        }

        // One query: each patient with her clinic's name and a summary of her screenings
        Patients = await query
            .OrderBy(p => p.Clinic.Name)
            .ThenBy(p => p.FullName)
            .Select(p => new PatientRow(
                p.Id,
                p.FullName,
                p.Clinic.Name,
                p.ConsentGiven,
                p.UserId != null,
                p.Screenings.Count,
                p.Screenings.OrderByDescending(s => s.ScreenedAt).Select(s => s.Band).FirstOrDefault(),
                p.Screenings.OrderByDescending(s => s.ScreenedAt).Select(s => (DateTime?)s.ScreenedAt).FirstOrDefault()))
            .ToListAsync();
    }
}