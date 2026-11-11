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

    
    // Ids of patients with an open "contact me" request
    public HashSet<int> WantsContact { get; set; } = new();

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
            
        // Who asked to be contacted in a check-in, and hasn't been marked as contacted since?
        var ids = Patients.Select(p => p.Id).ToList();
        var requests = await _db.WeeklyCheckIns
            .Where(c => c.WantsContact && ids.Contains(c.PatientId))
            .GroupBy(c => c.PatientId)
            .Select(g => new { PatientId = g.Key, At = g.Max(c => c.CheckedInAt) })
            .ToListAsync();
        var handled = await _db.AuditLogs
            .Where(a => a.Action == "ContactHandled" && a.EntityType == "Patient"
                        && a.EntityId != null && ids.Contains(a.EntityId.Value))
            .GroupBy(a => a.EntityId!.Value)
            .Select(g => new { PatientId = g.Key, At = g.Max(a => a.At) })
            .ToDictionaryAsync(x => x.PatientId, x => x.At);
        WantsContact = requests
            .Where(r => !handled.TryGetValue(r.PatientId, out var at) || at < r.At)
            .Select(r => r.PatientId)
            .ToHashSet();

        // Patients waiting for a call go to the top of the list
        Patients = Patients.OrderByDescending(p => WantsContact.Contains(p.Id)).ToList();
    }

    public static string LanguageName(string code) => code switch
    {
        "zu" => "isiZulu",
        "xh" => "isiXhosa",
        "af" => "Afrikaans",
        _ => "English"
    };
}
