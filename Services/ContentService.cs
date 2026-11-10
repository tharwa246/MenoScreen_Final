using System.Text;
using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

// READS THE CLINICAL CONTENT from the database for the pages, the PDFs and the assistant.
// Everything that shows band wording, guideline text or suggested questions asks this
// service, so there is ONE place the app reads it from.
public class ContentService
{
    private readonly ApplicationDbContext _db;

    public ContentService(ApplicationDbContext db) => _db = db;

    // The wording for one band. If the row is missing (it shouldn't be), we return empty
    // wording rather than crash, so the page and PDFs still work.
    public async Task<BandContent> GetBandAsync(string band) =>
        await _db.BandContents.AsNoTracking().FirstOrDefaultAsync(b => b.Band == band)
        ?? new BandContent { Band = band };

    // All five bands, in the same order as the scoring engine (for the admin page)
    public async Task<List<BandContent>> GetAllBandsAsync()
    {
        var rows = await _db.BandContents.ToListAsync();
        return rows.OrderBy(b => Array.IndexOf(ScoringService.Bands, b.Band)).ToList();
    }

    // The shown suggested questions, in order (the buttons on the Result page)
    public Task<List<string>> GetPromptsAsync() =>
        _db.AssistantPrompts
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => p.Text)
            .ToListAsync();

    // The shown guideline sections joined into one text for the assistant, each under its
    // own "## Heading" so the assistant can name the section it relied on.
    public async Task<string> GetGuidelinesTextAsync()
    {
        var sections = await _db.GuidelineSections
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder).ThenBy(g => g.Id)
            .ToListAsync();

        if (sections.Count == 0)
            return "(No guideline sections are switched on. Answer every question with rule 2.)";

        var sb = new StringBuilder();
        foreach (var g in sections)
        {
            sb.AppendLine($"## {g.Heading}");
            sb.AppendLine(g.Body.Trim());
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ===== TRANSLATED INTERFACE WORDING (added from Tharwa's branch) =====

    // All interface texts in one language. English is loaded first and the chosen language
    // is laid on top, so a missing or blank translation falls back to English.
    public async Task<Dictionary<string, string>> GetAllAsync(string? language)
    {
        var lang = TranslationHelper.Normalize(language);

        var rows = await _db.ContentTexts
            .AsNoTracking()
            .Where(c => c.Language == "en" || c.Language == lang)
            .ToListAsync();

        var result = rows.Where(c => c.Language == "en").ToDictionary(c => c.Key, c => c.Value);

        foreach (var c in rows.Where(c => c.Language == lang && !string.IsNullOrWhiteSpace(c.Value)))
            result[c.Key] = c.Value;

        return result;
    }

    // If a key is missing everywhere, show the key itself so the gap is obvious
    public static string Pick(IDictionary<string, string> texts, string key) =>
        texts.TryGetValue(key, out var value) ? value : key;

    // Her handout paragraph in her language. English (and any language without a
    // translation yet) comes from the band wording on Admin > Clinical content.
    public async Task<string> GetHandoutAsync(string band, string? language)
    {
        var english = (await GetBandAsync(band)).PatientHandout;

        var lang = TranslationHelper.Normalize(language);
        if (lang == "en") return english;

        var translated = await _db.ContentTexts
            .AsNoTracking()
            .Where(c => c.Key == "handout." + band && c.Language == lang)
            .Select(c => c.Value)
            .FirstOrDefaultAsync();

        return string.IsNullOrWhiteSpace(translated) ? english : translated;
    }
}