using MenoScreen.Models;

namespace MenoScreen.Pages.Shared;

// What the shared _QuestionFields partial needs to draw the 23 questions.
// Used by the clinician screening (Screenings/New) and the patient self-screen (SelfScreen).
public class QuestionFormModel
{
    public List<Question> Questions { get; init; } = new();
    public Dictionary<int, int> Answers { get; init; } = new();
    public Dictionary<int, string> TextAnswers { get; init; } = new();

    // true = the patient is reading the questions herself on her phone
    public bool SelfScreen { get; init; }

    // The language the questions are shown in: en, zu, xh or af. Answers are always saved in English.
    public string Language { get; init; } = "en";

    // Translated interface wording (rating labels, Yes/No...) from ContentService.GetAllAsync.
    // Empty = English everywhere.
    public Dictionary<string, string> Texts { get; init; } = new();

    // One interface text, falling back to the English given if there is no translation
    public string T(string key, string english) =>
        Texts.TryGetValue("ui." + key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : english;

    public static readonly string[] RatingLabels = { "Not at all", "A little", "Quite a bit", "Extremely" };
}
