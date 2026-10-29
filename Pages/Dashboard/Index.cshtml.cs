using MenoScreen.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public record BandRow(string Band, int Count);
    public record MonthRow(string Month, int Count);

    public int Total { get; set; }
    public double AverageScore { get; set; }
    public List<BandRow> ByBand { get; set; } = new();
    public List<BandRow> ComplexByScoreBand { get; set; } = new();
    public List<MonthRow> ByMonth { get; set; } = new();

    // Shown in this order, so bands with zero screenings still appear
    private static readonly string[] FinalBands = { "Unlikely", "Possible", "Likely", "Significant", "Complex" };
    private static readonly string[] ScoreBands = { "Unlikely", "Possible", "Likely", "Significant" };

    public async Task OnGetAsync()
    {
        Total = await _db.Screenings.CountAsync();

        // The cast to double? stops Average from crashing when there are no rows
        AverageScore = await _db.Screenings
            .Select(s => (double?)s.MenoLikelihood)
            .AverageAsync() ?? 0;

        // Count per final band (done in the database with GROUP BY)
        var finalCounts = await _db.Screenings
            .GroupBy(s => s.Band)
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync();

        ByBand = FinalBands
            .Select(b => new BandRow(b, finalCounts.FirstOrDefault(x => x.Band == b)?.Count ?? 0))
            .ToList();

        // Of the Complex screenings, what would the score alone have said?
        var complexCounts = await _db.Screenings
            .Where(s => s.Band == "Complex")
            .GroupBy(s => s.ScoreBand)
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync();

        ComplexByScoreBand = ScoreBands
            .Select(b => new BandRow(b, complexCounts.FirstOrDefault(x => x.Band == b)?.Count ?? 0))
            .ToList();

        // Screenings per month
        var monthCounts = await _db.Screenings
            .GroupBy(s => new { s.ScreenedAt.Year, s.ScreenedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        ByMonth = monthCounts
            .Select(x => new MonthRow($"{x.Year}-{x.Month:00}", x.Count))
            .ToList();
    }
}
