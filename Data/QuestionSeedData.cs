using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

public static class SeedData
{
    public static async Task SeedQuestionsAsync(ApplicationDbContext db)
    {
        // 1. If questions already exist, stop. This stops duplicates on restart.
        if (await db.QuestionSetVersions.AnyAsync()) return;

        // 2. Create Version 1
        var v1 = new QuestionSetVersion
        {
            VersionNumber = 1,
            IsActive = true,
            Notes = "Initial draft from clinical brief"
        };

        // 3. A small helper so we don't repeat the same long block 23 times
        void Add(int number, string block, string text, QuestionType type, string? options = null)
        {
            v1.Questions.Add(new Question
            {
                Number = number,
                SortOrder = number,
                Block = block,
                Text = text,
                Type = type,
                Options = options
            });
        }

        // 4. Block 1: context (Q1-Q4), not scored
        Add(1, "Patient Context", "How old are you?", QuestionType.Context, "35-44|45-54|55-64|65+");
        Add(2, "Patient Context", "When did your periods last change or stop?", QuestionType.Context,
            "Still regular|Becoming irregular|Stopped <2 yrs|Stopped >2 yrs|Hysterectomy|Not sure");
        Add(3, "Patient Context", "Has a doctor ever spoken to you about menopause or your hormones?", QuestionType.Context,
            "Yes|No|Not sure");
        Add(4, "Patient Context", "Are you currently taking any regular medication?", QuestionType.Context,
            "Yes|No|Not sure");

        // 5. Block 2: Cognitive & Mood (Q5-Q8), scored
        Add(5, "Cognitive & Mood", "Have you been struggling to concentrate, or feeling like your thinking is foggy?", QuestionType.Scored);
        Add(6, "Cognitive & Mood", "Have you been forgetting names, words, or where you put things more than usual?", QuestionType.Scored);
        Add(7, "Cognitive & Mood", "Have you been feeling anxious, low, or tearful for no clear reason?", QuestionType.Scored);
        Add(8, "Cognitive & Mood", "Have you been feeling more irritable than usual, or finding your moods are harder to manage?", QuestionType.Scored);

        // 6. Block 3: Vasomotor & Sleep (Q9-Q12), scored
        Add(9, "Vasomotor & Sleep", "Have you been experiencing sudden waves of heat through your body?", QuestionType.Scored);
        Add(10, "Vasomotor & Sleep", "Have you been waking up drenched in sweat or feeling overheated at night?", QuestionType.Scored);
        Add(11, "Vasomotor & Sleep", "Have you been struggling to fall asleep, or waking up during the night?", QuestionType.Scored);
        Add(12, "Vasomotor & Sleep", "Have you noticed your heart racing or beating irregularly?", QuestionType.Scored);

        // 7. Block 4: Musculoskeletal & Energy (Q13-Q15), scored
        Add(13, "Musculoskeletal & Energy", "Have you been experiencing joint pain or stiffness, especially in the mornings?", QuestionType.Scored);
        Add(14, "Musculoskeletal & Energy", "Do you feel exhausted even after a full night's sleep?", QuestionType.Scored);
        Add(15, "Musculoskeletal & Energy", "Have you been getting new headaches, or are your existing headaches becoming more frequent or severe?", QuestionType.Scored);

        // 8. Block 5: Urogenital & Sexual Health (Q16-Q19), scored
        Add(16, "Urogenital & Sexual Health", "Have you been experiencing any vaginal soreness, irritation, or discomfort, including during sex?", QuestionType.Scored);
        Add(17, "Urogenital & Sexual Health", "Have you been needing to rush to the bathroom urgently, or experiencing any leaking?", QuestionType.Scored);
        Add(18, "Urogenital & Sexual Health", "Have you been getting urinary tract infections repeatedly?", QuestionType.Scored);
        Add(19, "Urogenital & Sexual Health", "Has your interest in sex reduced or disappeared?", QuestionType.Scored);

        // 9. Block 6: Quality of Life (Q20-Q22), scored
        Add(20, "Quality of Life Impact", "Have these symptoms been affecting your ability to work: your concentration, productivity, or confidence?", QuestionType.Scored);
        Add(21, "Quality of Life Impact", "Have these symptoms been affecting your relationships with your partner, family, or friends?", QuestionType.Scored);
        Add(22, "Quality of Life Impact", "Overall, how much are these symptoms getting in the way of your daily life?", QuestionType.Scored);

        // 10. Block 7: safety flag (Q23), not scored
        Add(23, "Safety Flag", "Do you have any health conditions, such as diabetes, heart disease, a history of cancer, or an autoimmune condition, that we should consider before discussing treatment?", QuestionType.SafetyFlag);

        // 11. Save everything. EF saves the version AND its 23 questions together.
        db.QuestionSetVersions.Add(v1);
        await db.SaveChangesAsync();
    }
}