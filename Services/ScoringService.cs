using MenoScreen.Models;

namespace MenoScreen.Services;

// The answer the scoring engine gives back.
// A "record" is a small read-only class: once made, its values can't change.
public record ScoreResult(int RawScore, int MenoLikelihood, string ScoreBand, string Band);

// One row of the score breakdown, e.g. "Vasomotor & Sleep: 9 out of 12 (75%)".
public record BlockScore(string Block, int Score, int Max, int Percent, int QuestionCount);

// THE SCORING ENGINE (runs on the server, never in the browser, so it can't be tampered with)
//
// How a score is worked out, step by step:
//  1. Add up her answers (0-3) to every question marked "Scored". That's the RAW score.
//  2. The MAXIMUM is (number of scored questions) x 3. We count the questions from the
//     database instead of typing 54, so if the medical team adds a question it still works.
//  3. MenoLikelihood = raw / maximum x 100, rounded to a whole number (0-100).
//  4. Pick the BAND from that number: 0-30 Unlikely, 31-55 Possible, 56-80 Likely, 81-100 Significant.
//  5. SAFETY OVERRIDE: if she answered Yes to the safety question (Q23: existing conditions),
//     the final band becomes "Complex", no matter what the score was.
public class ScoringService
{
    // All five bands, in order. Analytics uses this list so every band shows up, even with 0 screenings.
    public static readonly string[] Bands = { "Unlikely", "Possible", "Likely", "Significant", "Complex" };

    // questions = the questions from the database for this version
    // answers   = a lookup of QuestionId -> her answer (0-3 for scored questions, 1 = Yes / 0 = No for Q23)
    public ScoreResult Calculate(IEnumerable<Question> questions, IDictionary<int, int> answers)
    {
        var scored = questions.Where(q => q.Type == QuestionType.Scored).ToList();
        if (scored.Count == 0)
            throw new InvalidOperationException("No scored questions found.");

        // Refuse to score an incomplete screening: a skipped question would wrongly lower her score.
        foreach (var q in scored)
        {
            if (!answers.TryGetValue(q.Id, out var v) || v < 0 || v > 3)
                throw new InvalidOperationException($"Question {q.Number} needs an answer from 0 to 3.");
        }

        int raw = scored.Sum(q => answers[q.Id]);
        int max = scored.Count * 3;
        int pct = ToPercent(raw, max);

        string scoreBand = BandFor(pct);

        // Step 5, the safety override. We keep BOTH bands: ScoreBand (from the number alone)
        // and Band (the final one), so the clinician can see "Complex, but score alone says Likely".
        var safety = questions.FirstOrDefault(q => q.Type == QuestionType.SafetyFlag);
        bool isComplex = safety != null
                         && answers.TryGetValue(safety.Id, out var s)
                         && s == 1;

        string band = isComplex ? "Complex" : scoreBand;

        return new ScoreResult(raw, pct, scoreBand, band);
    }

    public static string BandFor(int percent) => percent switch
    {
        <= 30 => "Unlikely",
        <= 55 => "Possible",
        <= 80 => "Likely",
        _ => "Significant"
    };

    // SCORE BREAKDOWN: one subtotal per symptom block (Cognitive & Mood, Vasomotor & Sleep, ...).
    // This is what draws the "symptom blanket" on the result page and the table in the PDF.
    // It groups her saved answers by block, adds each group up, and keeps the blocks in
    // questionnaire order. "static" means you can call it without creating a ScoringService.
    public static List<BlockScore> BlockBreakdown(IEnumerable<Answer> answers)
    {
        return answers
            .Where(a => a.Question.Type == QuestionType.Scored)
            .GroupBy(a => a.Question.Block)
            .OrderBy(g => g.Min(a => a.Question.SortOrder))
            .Select(g =>
            {
                int score = g.Sum(a => a.Value ?? 0);
                int max = g.Count() * 3;
                return new BlockScore(g.Key, score, max, ToPercent(score, max), g.Count());
            })
            .ToList();
    }

    private static int ToPercent(int score, int max) =>
        max == 0 ? 0 : (int)Math.Round(score * 100.0 / max, MidpointRounding.AwayFromZero);
}
