using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public class ScreeningService
{
    private readonly ApplicationDbContext _db;
    private readonly ScoringService _scoring;

    public ScreeningService(ApplicationDbContext db, ScoringService scoring)
    {
        _db = db;
        _scoring = scoring;
    }

    // Gets the questions of the active question-set version, in display order
    public async Task<List<Question>> GetActiveQuestionsAsync()
    {
        var version = await _db.QuestionSetVersions
            .Include(v => v.Questions)
            .Where(v => v.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync();

        if (version == null)
            throw new InvalidOperationException("No active question set found.");

        return version.Questions.OrderBy(q => q.SortOrder).ToList();
    }

    // numericAnswers: QuestionId -> 0-3 (scored) or 0/1 (safety question)
    // textAnswers:    QuestionId -> text (the context questions)
    public async Task<Screening> SaveScreeningAsync(
        int patientId,
        string clinicianId,
        IDictionary<int, int> numericAnswers,
        IDictionary<int, string> textAnswers)
    {
        // 1. The patient must exist
        if (!await _db.Patients.AnyAsync(p => p.Id == patientId))
            throw new InvalidOperationException("Patient not found.");

        // 2. Load the questions from the database
        var questions = await GetActiveQuestionsAsync();

        // 3. The safety question must be answered 0 or 1
        var safety = questions.First(q => q.Type == QuestionType.SafetyFlag);
        if (!numericAnswers.TryGetValue(safety.Id, out var safetyValue) || safetyValue is < 0 or > 1)
            throw new InvalidOperationException("The safety question must be answered Yes or No.");

        // 4. Score it (this also checks every scored question has a 0-3 answer)
        var result = _scoring.Calculate(questions, numericAnswers);

        // 5. Build the screening and its answers
        var screening = new Screening
        {
            PatientId = patientId,
            ClinicianId = clinicianId,
            QuestionSetVersionId = questions[0].QuestionSetVersionId,
            RawScore = result.RawScore,
            MenoLikelihood = result.MenoLikelihood,
            ScoreBand = result.ScoreBand,
            Band = result.Band
        };

        foreach (var q in questions)
        {
            if (q.Type == QuestionType.Context)
            {
                if (textAnswers.TryGetValue(q.Id, out var text) && !string.IsNullOrWhiteSpace(text))
                    screening.Answers.Add(new Answer { QuestionId = q.Id, TextValue = text });
            }
            else if (numericAnswers.TryGetValue(q.Id, out var value))
            {
                screening.Answers.Add(new Answer { QuestionId = q.Id, Value = value });
            }
        }

        _db.Screenings.Add(screening);
        await _db.SaveChangesAsync();   // saves the screening and all its answers together
        return screening;
    }
}