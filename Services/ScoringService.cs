using MenoScreen.Models;

namespace MenoScreen.Services;

public record ScoreResult(int RawScore, int MenoLikelihood, string ScoreBand, string Band);

//Add up the answers to every question marked Scored
//The maximum is the number of scored questions × 3. It comes from the database and isn’t hard-coded as 54
//Convert to a score out of 100 and round it to a whole number
//Pick the band: 0-30 Unlikely, 31-55 Possible, 56-80 Likely, 81-100 Significant.
//If the safety question (Q23) is answered Yes, the final band becomes “Complex”, whatever the score was.
public class ScoringService
{
    // questions = the questions from the database for this version
    // answers   = QuestionId -> value (0-3 for scored, 1 = Yes / 0 = No for the safety flag)
    public ScoreResult Calculate(IEnumerable<Question> questions, IDictionary<int, int> answers)
    {
        var scored = questions.Where(q => q.Type == QuestionType.Scored).ToList();
        if (scored.Count == 0)
            throw new InvalidOperationException("No scored questions found.");

        // Every scored question must have an answer between 0 and 3
        foreach (var q in scored)
        {
            if (!answers.TryGetValue(q.Id, out var v) || v < 0 || v > 3)
                throw new InvalidOperationException($"Question {q.Number} needs an answer from 0 to 3.");
        }

        int raw = scored.Sum(q => answers[q.Id]);
        int max = scored.Count * 3;
        int pct = (int)Math.Round(raw * 100.0 / max, MidpointRounding.AwayFromZero);

        string scoreBand = pct switch
        {
            <= 30 => "Unlikely",
            <= 55 => "Possible",
            <= 80 => "Likely",
            _ => "Significant"
        };

        // Q23: if answered Yes (1), the final band is Complex at any score
        var safety = questions.FirstOrDefault(q => q.Type == QuestionType.SafetyFlag);
        bool isComplex = safety != null
                         && answers.TryGetValue(safety.Id, out var s)
                         && s == 1;

        string band = isComplex ? "Complex" : scoreBand;

        return new ScoreResult(raw, pct, scoreBand, band);
    }
}