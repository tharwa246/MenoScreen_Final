using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

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
        ["ui.Select"] = "Select...",

        ["handout.Hello"] = "Hello",
        ["handout.NextStep"] = "Your next step",
        ["handout.ScanOrVisit"] = "Scan the code with your phone, or visit:",
        ["handout.Unlikely"] = "Your results today don't show a strong hormonal pattern, but your body knows best. If you notice changes, come back. JoinMandi.com has information to help you understand what to watch for.",
        ["handout.Possible"] = "What you are feeling has a name. Your results today suggest your hormones may be starting to change. This is perimenopause, and there is support available. JoinMandi.com will help you understand what is happening and what your options are.",
        ["handout.Likely"] = "Your symptoms are real, and they have a cause. Your results today suggest you are likely in perimenopause or menopause. You have options, and you deserve to know about them. JoinMandi.com will walk you through what is happening and how to find the right support.",
        ["handout.Significant"] = "You have been carrying a lot. Your results today show that your symptoms are significant, and you deserve urgent, proper support. Please do not wait. JoinMandi.com can help you find the right doctor now.",
        ["handout.Complex"] = "Your results today suggest your symptoms may be related to your hormones, but because of your health history, your doctor wants to make sure you get the right specialist support. JoinMandi.com has information to help you prepare for that conversation."
    };

    // Safe on every start-up: it only adds what is missing
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
    }
}