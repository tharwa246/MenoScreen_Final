using MenoScreen.Data;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public class ContentService
{
    private readonly ApplicationDbContext _db;

    public ContentService(ApplicationDbContext db)
    {
        _db = db;
    }

    // English is loaded first and the chosen language is laid on top,
    // so a missing or blank translation falls back to English.
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
}