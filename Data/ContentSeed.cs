using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

// THE ENGLISH INTERFACE WORDING that Azure translates into isiZulu, isiXhosa and Afrikaans.
// Safe on every start-up: it only adds what is missing.
public static class ContentSeed
{
    public static readonly Dictionary<string, string> English = new()
    {
        ["ui.ScaleIntro"] = "I'd like you to rate how much each symptom has been affecting you: 0 means not at all, 1 a little, 2 quite a bit, and 3 extremely.",
        ["ui.Scale0"] = "Not at all",
        ["ui.Scale1"] = "A little",
        ["ui.Scale2"] = "Quite a bit",
        ["ui.Scale3"] = "Extremely",
        ["ui.Yes"] = "Yes",
        ["ui.No"] = "No",
        ["ui.Select"] = "Choose an answer"
    };

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var existing = (await db.ContentTexts
            .Where(c => c.Language == "en")
            .Select(c => c.Key)
            .ToListAsync()).ToHashSet();

        foreach (var (key, value) in English)
        {
            if (existing.Contains(key)) continue;
            db.ContentTexts.Add(new ContentText { Key = key, Language = "en", Value = value, IsReviewed = true });
        }

        await db.SaveChangesAsync();
        await SyncHandoutsAsync(db);
    }

    // The English handout paragraph for each band is edited on Admin > Clinical content
    // (BandContent table). This copies it into the translation table as "handout.<Band>",
    // so Azure always translates the CURRENT English wording.
    public static async Task SyncHandoutsAsync(ApplicationDbContext db)
    {
        var bands = await db.BandContents.AsNoTracking().ToListAsync();
        var rows = await db.ContentTexts
            .Where(c => c.Language == "en" && c.Key.StartsWith("handout."))
            .ToListAsync();

        foreach (var b in bands)
        {
            var key = "handout." + b.Band;
            var row = rows.FirstOrDefault(r => r.Key == key);
            if (row == null)
            {
                db.ContentTexts.Add(new ContentText
                {
                    Key = key, Language = "en", Value = b.PatientHandout, IsReviewed = true
                });
            }
            else if (row.Value != b.PatientHandout)
            {
                row.Value = b.PatientHandout;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
    }
}