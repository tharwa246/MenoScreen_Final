using MenoScreen.Data;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Patients;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ClinicScopeService _scope;

    public IndexModel(ApplicationDbContext db, ClinicScopeService scope)
    {
        _db = db;
        _scope = scope;
    }

    public string? ClinicName { get; set; }

    public record PatientRow(int Id, string FullName, string Language, DateTime CreatedAt, bool ConsentGiven,
                             int ScreeningCount, string? LastBand, DateTime? LastScreenedAt, int? LastScreeningId);

    public List<PatientRow> Patients { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public async Task OnGetAsync()
    {
        // Only their own clinic's patients
        ClinicName = await _scope.GetClinicNameAsync();
        var query = await _scope.PatientsAsync();


        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim().ToLower();
            query = query.Where(p => p.FullName.ToLower().Contains(term));
        }

        Patients = await query
            .OrderBy(p => p.FullName)
            .Select(p => new PatientRow(
                p.Id,
                p.FullName,
                p.PreferredLanguage,
                p.CreatedAt,
                p.ConsentGiven,
                p.Screenings.Count,
                p.Screenings.OrderByDescending(s => s.ScreenedAt).Select(s => s.Band).FirstOrDefault(),
                p.Screenings.OrderByDescending(s => s.ScreenedAt).Select(s => (DateTime?)s.ScreenedAt).FirstOrDefault(),
                p.Screenings.OrderByDescending(s => s.ScreenedAt).Select(s => (int?)s.Id).FirstOrDefault()))
            .ToListAsync();
    }

    public static string LanguageName(string code) => code switch
    {
        "zu" => "isiZulu",
        "xh" => "isiXhosa",
        "af" => "Afrikaans",
        _ => "English"
    };
}
