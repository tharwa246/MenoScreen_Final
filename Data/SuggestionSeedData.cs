using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

// THE STARTING SET OF PORTAL TIPS. Only runs when the Suggestions table is empty;
// after that, admins manage the tips on the Admin > Portal tips page.
public static class SuggestionSeedData
{
    private static readonly (string Area, string Text)[] Defaults =
    {
        ("Vasomotor & Sleep", "Dress in light layers you can take off quickly, and keep a fan or a cool drink nearby."),
        ("Vasomotor & Sleep", "Keep your bedroom cool and dark, and try to go to bed and wake up at the same time each day."),
        ("Vasomotor & Sleep", "Notice what sets off your flushes. Alcohol, caffeine, spicy food and stress are common triggers."),
        ("Vasomotor & Sleep", "If night sweats wake you, keep a spare cotton top and a glass of water next to the bed."),

        ("Cognitive & Mood", "Brain fog is common in perimenopause. Lists, phone reminders and a calendar can take the pressure off."),
        ("Cognitive & Mood", "Regular movement, even a 20-minute walk, can lift your mood and help you sleep."),
        ("Cognitive & Mood", "Talk to someone you trust about how you've been feeling. You don't have to manage this alone."),
        ("Cognitive & Mood", "If you feel low most days for two weeks or more, please tell your clinic. Help is available."),

        ("Musculoskeletal & Energy", "Gentle stretching in the morning can ease stiffness. Many women find it worst after waking."),
        ("Musculoskeletal & Energy", "Strength exercises and walking help keep your muscles and bones strong as hormones change."),
        ("Musculoskeletal & Energy", "Drink water through the day and try to eat regular meals, which can help with tiredness and headaches."),
        ("Musculoskeletal & Energy", "Foods with calcium, like dairy, leafy greens and tinned fish with bones, support your bones."),

        ("Urogenital & Sexual Health", "Vaginal dryness and discomfort are very common and very treatable. You don't have to put up with them."),
        ("Urogenital & Sexual Health", "Pharmacies stock vaginal moisturisers and lubricants that you can buy without a prescription."),
        ("Urogenital & Sexual Health", "Pelvic floor exercises can help with leaking or a sudden need to rush to the toilet."),
        ("Urogenital & Sexual Health", "If you have pain, bleeding, or keep getting bladder infections, please tell your clinic."),

        ("Quality of Life Impact", "Keep a short note of your symptoms and when they happen. It makes conversations with your clinic easier."),
        ("Quality of Life Impact", "Consider telling your partner, family or manager what you're going through. Small changes can help a lot."),
        ("Quality of Life Impact", "Be kind to yourself. What you're feeling is real, and it is not a weakness."),
        ("Quality of Life Impact", "JoinMandi.com has information and stories from other women going through the same thing."),

        ("General", "Keep doing your weekly check-in. It helps you and your clinic see what's changing."),
        ("General", "There are treatments that can help. Ask your clinic about the options that suit you."),
        ("General", "If something feels very wrong or frightening, don't wait. Contact your clinic, or call 10177 in an emergency.")
    };

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        // Already has tips? Leave them alone: the admin may have edited or deleted some.
        if (await db.Suggestions.AnyAsync()) return;

        int order = 0;
        foreach (var (area, text) in Defaults)
        {
            order += 10;   // gaps of 10 make it easy to slot a new tip in between later
            db.Suggestions.Add(new Suggestion { Area = area, Text = text, SortOrder = order });
        }
        await db.SaveChangesAsync();
    }
}