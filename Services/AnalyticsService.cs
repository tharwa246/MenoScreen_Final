using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public record BandCount(string Band, int Count, int Percent);
public record WeekCount(DateTime WeekStart, int Count);
public record BlockAverage(string Block, int AveragePercent);
public record ClinicTotal(int ClinicId, string Name, int Patients, int Screenings, int AverageMenoLikelihood, int ComplexCount);

public class AnalyticsSummary
{
    public int TotalPatients { get; init; }
    public int TotalScreenings { get; init; }
    public int ScreeningsLast30Days { get; init; }
    public int AverageMenoLikelihood { get; init; }
    public int ComplexCount { get; init; }
    public int SelfScreenCount { get; init; }
    public List<BandCount> Bands { get; init; } = new();
    public List<WeekCount> Weekly { get; init; } = new();
    public List<BlockAverage> BlockAverages { get; init; } = new();
}

// THE ANALYTICS DASHBOARD
// Works out clinic-wide totals: how many screenings, how many in each band, which
// symptom blocks score highest on average, and how many screenings per week.
// Only totals and averages come out of here, never a patient's name (POPIA data minimisation).
public class AnalyticsService
{
    private readonly ApplicationDbContext _db;

    public AnalyticsService(ApplicationDbContext db) => _db = db;

    // clinicId = null means "all clinics together" (admins only; clinicians always pass their own)
    public async Task<AnalyticsSummary> GetSummaryAsync(int? clinicId = null, int weeks = 12)
    {
        var now = DateTime.UtcNow;

        // Which rows count? Everything, or just one clinic's patients and their screenings
        var screeningQuery = _db.Screenings.AsQueryable();
        var answerQuery = _db.Answers.AsQueryable();
        var patientQuery = _db.Patients.AsQueryable();
        if (clinicId != null)
        {
            screeningQuery = screeningQuery.Where(s => s.Patient.ClinicId == clinicId);
            answerQuery = answerQuery.Where(a => a.Screening.Patient.ClinicId == clinicId);
            patientQuery = patientQuery.Where(p => p.ClinicId == clinicId);
        }

        // Load just 4 small fields per screening, then do the counting in C#.
        // A clinic has hundreds of screenings, not millions, so this is fast, and it
        // behaves the same on SQLite and Postgres (date maths differs between databases).
        var screenings = await screeningQuery
            .Select(s => new { s.ScreenedAt, s.Band, s.MenoLikelihood, s.Source })
            .ToListAsync();

        int total = screenings.Count;

        var bands = ScoringService.Bands
            .Select(b =>
            {
                int count = screenings.Count(s => s.Band == b);
                return new BandCount(b, count, total == 0 ? 0 : (int)Math.Round(count * 100.0 / total));
            })
            .ToList();

        // Screenings per week for the last 12 weeks (weeks start on Monday).
        // Weeks with no screenings still appear, with a count of 0, so the chart has no gaps.
        var thisMonday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        var weekly = Enumerable.Range(0, weeks)
            .Select(i => thisMonday.AddDays(-7 * (weeks - 1 - i)))
            .Select(start => new WeekCount(start,
                screenings.Count(s => s.ScreenedAt >= start && s.ScreenedAt < start.AddDays(7))))
            .ToList();

        // For each symptom block: the average answer across every screening, shown as a
        // percentage of the maximum (3). E.g. average answer 2.1 out of 3 = 70%.
        var scoredAnswers = await answerQuery
            .Where(a => a.Question.Type == QuestionType.Scored)
            .Select(a => new { a.Question.Block, a.Question.SortOrder, Value = a.Value ?? 0 })
            .ToListAsync();

        var blockAverages = scoredAnswers
            .GroupBy(a => a.Block)
            .OrderBy(g => g.Min(a => a.SortOrder))
            .Select(g => new BlockAverage(g.Key, (int)Math.Round(g.Average(a => a.Value) * 100.0 / 3)))
            .ToList();

        return new AnalyticsSummary
        {
            TotalPatients = await patientQuery.CountAsync(),
            TotalScreenings = total,
            ScreeningsLast30Days = screenings.Count(s => s.ScreenedAt >= now.AddDays(-30)),
            AverageMenoLikelihood = total == 0 ? 0 : (int)Math.Round(screenings.Average(s => s.MenoLikelihood)),
            ComplexCount = screenings.Count(s => s.Band == "Complex"),
            SelfScreenCount = screenings.Count(s => s.Source == ScreeningSources.SelfScreen),
            Bands = bands,
            Weekly = weekly,
            BlockAverages = blockAverages
        };
    }

    // ADMIN ONLY: one row per clinic, for the pie chart and the comparison table
    public async Task<List<ClinicTotal>> GetClinicTotalsAsync()
    {
        var clinics = await _db.Clinics
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, Patients = c.Patients.Count })
            .ToListAsync();

        var screenings = await _db.Screenings
            .Select(s => new { s.Patient.ClinicId, s.MenoLikelihood, s.Band })
            .ToListAsync();

        return clinics.Select(c =>
        {
            var mine = screenings.Where(s => s.ClinicId == c.Id).ToList();
            return new ClinicTotal(
                c.Id,
                c.Name,
                c.Patients,
                mine.Count,
                mine.Count == 0 ? 0 : (int)Math.Round(mine.Average(s => s.MenoLikelihood)),
                mine.Count(s => s.Band == "Complex"));
        }).ToList();
    }
}
