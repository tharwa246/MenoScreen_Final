using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

// One heading on the portal ("Hot flushes and sleep") with its tips underneath
public record SuggestionGroup(string Title, List<string> Tips);

// PICKS THE TIPS A PATIENT SEES on "My health": the shown tips for her heaviest symptom
// areas, then the general ones. The tips themselves are managed by admins (Admin > Portal tips).
public class SuggestionService
{
    private readonly ApplicationDbContext _db;

    public SuggestionService(ApplicationDbContext db) => _db = db;

    public async Task<List<SuggestionGroup>> ForAsync(IEnumerable<string> areas)
    {
        // Her areas first, then "General" for everyone (Distinct stops it appearing twice)
        var wanted = areas.Append(SuggestionAreas.General).Distinct().ToList();

        // ONE database query for all the tips we need, shown ones only, in order
        var tips = await _db.Suggestions
            .Where(s => s.IsActive && wanted.Contains(s.Area))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();

        // Group them under friendly headings, in the order of her areas; skip empty groups
        return wanted
            .Select(area => new SuggestionGroup(
                SuggestionAreas.Titles.GetValueOrDefault(area, area),
                tips.Where(t => t.Area == area).Select(t => t.Text).ToList()))
            .Where(g => g.Tips.Any())
            .ToList();
    }
}