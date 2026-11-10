using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Portal;

// "MY HEALTH": the patient's home. Everything comes from GetMyPatientAsync, so only her data.
public class IndexModel : PageModel
{
    private readonly PortalService _portal;
    private readonly ScreeningService _screenings;
    private readonly SuggestionService _suggestions;
    private readonly ContentService _content;

    public IndexModel(PortalService portal, ScreeningService screenings, SuggestionService suggestions,
                      ContentService content)
    {
        _portal = portal;
        _screenings = screenings;
        _suggestions = suggestions;
        _content = content;
    }

    // The tips she sees under "Ideas that may help"
    public List<SuggestionGroup> Suggestions { get; set; } = new();
    public Patient? Me { get; set; }
    public List<Screening> Screenings { get; set; } = new();
    public List<WeeklyCheckIn> CheckIns { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string FirstName => Me?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    public Screening? Latest => Screenings.FirstOrDefault();
    // Her band's handout wording (BandContent table), filled in OnGetAsync
    public string LatestMessage { get; set; } = string.Empty;

    // A check-in is due if she has never done one, or her last one was 7 or more days ago
    public bool CheckInDue => !CheckIns.Any() || CheckIns[^1].CheckedInAt <= DateTime.UtcNow.AddDays(-7);
    public DateTime? NextCheckIn => CheckIns.Any() ? CheckIns[^1].CheckedInAt.AddDays(7) : null;

    public async Task OnGetAsync()
    {
        Me = await _portal.GetMyPatientAsync();
        if (Me == null) return;

        Screenings = await _portal.GetScreeningsAsync(Me.Id);
        CheckIns = await _portal.GetCheckInsAsync(Me.Id);
        Suggestions = await _suggestions.ForAsync(await HeaviestAreasAsync());

        if (Latest != null)
            LatestMessage = (await _content.GetBandAsync(Latest.Band)).PatientHandout;
    }

    // Her two heaviest symptom areas: from her latest check-in if she has one,
    // otherwise from her latest screening. Only areas that really bother her count.
    private async Task<List<string>> HeaviestAreasAsync()
    {
        if (CheckIns.Any())
        {
            var last = CheckIns[^1];   // check-ins are in date order, so the last one is the newest
            var scores = new Dictionary<string, int>
            {
                ["Vasomotor & Sleep"] = last.VasomotorSleep,
                ["Cognitive & Mood"] = last.CognitiveMood,
                ["Musculoskeletal & Energy"] = last.MusculoskeletalEnergy,
                ["Urogenital & Sexual Health"] = last.UrogenitalSexual,
                ["Quality of Life Impact"] = last.QualityOfLife
            };
            return scores
                .Where(s => s.Value >= 4)              // 4 or more out of 10
                .OrderByDescending(s => s.Value)
                .Take(2)
                .Select(s => s.Key)
                .ToList();
        }

        if (Latest != null)
        {
            var full = await _screenings.LoadFullAsync(Latest.Id);
            if (full != null)
                return ScoringService.BlockBreakdown(full.Answers)
                    .Where(b => b.Percent >= 34)       // about a third of the maximum or more
                    .OrderByDescending(b => b.Percent)
                    .Take(2)
                    .Select(b => b.Block)
                    .ToList();
        }

        return new();
    }
}