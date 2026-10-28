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

    public static readonly string[] RatingLabels = { "Not at all", "A little", "Quite a bit", "Extremely" };
}
