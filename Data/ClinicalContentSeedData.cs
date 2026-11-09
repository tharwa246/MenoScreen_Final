using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

// THE STARTING CLINICAL CONTENT, taken from section 7 of the MenoScreen brief.
// Each table is only filled when it's empty; after that, admins manage the content
// on the Admin > Clinical content page.
public static class ClinicalContentSeedData
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        await SeedBandsAsync(db);
        await SeedGuidelinesAsync(db);
        await SeedPromptsAsync(db);
    }

    // ---- 1. The wording for each band (screen message, clinician summary, her handout) ----
    private static async Task SeedBandsAsync(ApplicationDbContext db)
    {
        if (await db.BandContents.AnyAsync()) return;

        db.BandContents.AddRange(
            new BandContent
            {
                Band = "Unlikely",
                ClinicianMessage = "Her symptom profile does not strongly suggest hormonal change at this time. Reassure her and invite her to return if symptoms persist or worsen.",
                ClinicianSummary = "Low MenoLikelihood score. No significant symptom cluster identified at this time. Recommend: reassurance and routine follow-up.",
                PatientHandout = "Your results today don't show a strong hormonal pattern, but your body knows best. If you notice changes, come back. JoinMandi.com has information to help you understand what to watch for."
            },
            new BandContent
            {
                Band = "Possible",
                ClinicianMessage = "Her symptom profile is consistent with early perimenopause. Begin the menopause conversation. Consider lifestyle support or referral.",
                ClinicianSummary = "Moderate MenoLikelihood score. Symptom cluster consistent with perimenopause. Recommend: menopause conversation, lifestyle support, consider referral.",
                PatientHandout = "What you are feeling has a name. Your results today suggest your hormones may be starting to change. This is perimenopause, and there is support available. JoinMandi.com will help you understand what is happening and what your options are."
            },
            new BandContent
            {
                Band = "Likely",
                ClinicianMessage = "Her symptom profile is strongly consistent with peri/menopause. Discuss HRT options or refer to a menopause-trained GP or gynaecologist.",
                ClinicianSummary = "High MenoLikelihood score. Strong symptom pattern consistent with perimenopause or menopause. Recommend: discussion of HRT options or referral to menopause-trained specialist.",
                PatientHandout = "Your symptoms are real, and they have a cause. Your results today suggest you are likely in perimenopause or menopause. You have options, and you deserve to know about them. JoinMandi.com will walk you through what is happening and how to find the right support."
            },
            new BandContent
            {
                Band = "Significant",
                ClinicianMessage = "She is carrying a significant symptom burden. Urgent referral is recommended. Please use warm, validating language in your conversation and in her summary.",
                ClinicianSummary = "Very high MenoLikelihood score. Severe symptom burden. Urgent referral to menopause-trained specialist recommended. Patient requires compassionate, timely support.",
                PatientHandout = "You have been carrying a lot. Your results today show that your symptoms are significant, and you deserve urgent, proper support. Please do not wait. JoinMandi.com can help you find the right doctor now."
            },
            new BandContent
            {
                Band = "Complex",
                ClinicianMessage = "This patient has existing health conditions that require specialist input before any treatment is considered. Please refer before discussing HRT.",
                ClinicianSummary = "Complex case flag raised. Existing health conditions noted. Specialist referral required before any treatment decision. Do not recommend HRT without specialist input.",
                PatientHandout = "Your results today suggest your symptoms may be related to your hormones, but because of your health history, your doctor wants to make sure you get the right specialist support. JoinMandi.com has information to help you prepare for that conversation."
            });

        await db.SaveChangesAsync();
    }

    // ---- 2. The guideline sections the assistant answers from (was assistant-guidelines.md) ----
    // Not marked as approved: the clinical advisor signs each one off on the admin page.
    private static async Task SeedGuidelinesAsync(ApplicationDbContext db)
    {
        if (await db.GuidelineSections.AnyAsync()) return;

        db.GuidelineSections.AddRange(
            new GuidelineSection
            {
                SortOrder = 10,
                Heading = "About the MenoLikelihood score",
                Body = """
                    - The score runs from 0 to 100. It is the sum of the scored symptom answers (each rated 0 = not at all, 1 = a little, 2 = quite a bit, 3 = extremely), divided by the maximum possible, times 100.
                    - Bands: Unlikely 0-30, Possible 31-55, Likely 56-80, Significant 81-100.
                    - If the safety question (existing conditions such as diabetes, heart disease, a history of cancer or an autoimmune condition) is answered Yes, the final band is Complex, whatever the score.
                    - The score is a screening aid that starts a conversation. It is not a diagnosis.
                    """
            },
            new GuidelineSection
            {
                SortOrder = 20,
                Heading = "Symptom blocks",
                Body = """
                    - Cognitive & Mood: concentration, memory, anxiety or low mood, irritability.
                    - Vasomotor & Sleep: hot flushes, night sweats, sleep disruption, palpitations.
                    - Musculoskeletal & Energy: joint pain or stiffness, exhaustion, headaches.
                    - Urogenital & Sexual Health: vaginal discomfort, urgency or leaking, recurrent UTIs, reduced libido.
                    - Quality of Life Impact: effect on work, relationships and daily life.
                    - A high block score shows where the conversation should focus first.
                    """
            },
            new GuidelineSection
            {
                SortOrder = 30,
                Heading = "Recommended action by band (from the MenoScreen brief, section 7)",
                Body = """
                    - Unlikely: The symptom profile does not strongly suggest hormonal change at this time. Reassure her and invite her to return if symptoms persist or worsen. Routine follow-up.
                    - Possible: Consistent with early perimenopause. Begin the menopause conversation. Consider lifestyle support or referral.
                    - Likely: Strongly consistent with peri/menopause. Discuss HRT options or refer to a menopause-trained GP or gynaecologist.
                    - Significant: Significant symptom burden. Urgent referral to a menopause-trained specialist is recommended. Use warm, validating language.
                    - Complex: Existing health conditions need specialist input before any treatment is considered. Refer before discussing HRT. Do not recommend HRT without specialist input.
                    """
            },
            new GuidelineSection
            {
                SortOrder = 40,
                Heading = "How to talk with her",
                Body = """
                    - Use warm, validating, plain language. Many women have been told their symptoms are "just stress".
                    - Offer the conversation in her preferred language where possible (English, isiZulu, isiXhosa, Afrikaans).
                    - Give her the patient handout; it points her to JoinMandi.com for education, resources and HRT-informed doctors.
                    """
            });

        await db.SaveChangesAsync();
    }

    // ---- 3. The one-tap suggested questions for the assistant ----
    private static async Task SeedPromptsAsync(ApplicationDbContext db)
    {
        if (await db.AssistantPrompts.AnyAsync()) return;

        db.AssistantPrompts.AddRange(
            new AssistantPrompt { SortOrder = 10, Text = "What should I focus on first with her?" },
            new AssistantPrompt { SortOrder = 20, Text = "How do I explain this result to her in plain language?" },
            new AssistantPrompt { SortOrder = 30, Text = "When should I refer her, and to whom?" });

        await db.SaveChangesAsync();
    }
}