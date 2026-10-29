using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

// SAVING A SCREENING
// This class takes her answers, checks them, asks ScoringService for the score,
// and saves the screening plus every answer to the database.
// Both ways of screening use it: the clinician-led form and the patient's QR self-screen.
public class ScreeningService
{
    private readonly ApplicationDbContext _db;
    private readonly ScoringService _scoring;

    public ScreeningService(ApplicationDbContext db, ScoringService scoring)
    {
        _db = db;
        _scoring = scoring;
    }

    // Gets the questions to show. Questions belong to a numbered "version" so the medical
    // team can change the wording later without breaking old results. We use the newest
    // version marked IsActive, sorted into the order the patient sees them.
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

    // Clinician-led screening (the clinician reads the questions aloud)
    // numericAnswers: QuestionId -> 0-3 (scored) or 0/1 (safety question)
    // textAnswers:    QuestionId -> text (the context questions)
    public async Task<Screening> SaveScreeningAsync(
        int patientId,
        string clinicianId,
        IDictionary<int, int> numericAnswers,
        IDictionary<int, string> textAnswers,
        DateTime? consultationStartedAt = null)
    {
        var screening = await BuildScreeningAsync(patientId, clinicianId, numericAnswers, textAnswers, ScreeningSources.Clinician);
    if (consultationStartedAt.HasValue)
        {
             screening.ConsultationStartedAt = consultationStartedAt.Value;
             screening.ConsultationEndedAt = DateTime.UtcNow;
             screening.ConsultationDurationSeconds =
        Math.Max(0, (int)(screening.ConsultationEndedAt.Value - screening.ConsultationStartedAt.Value).TotalSeconds);
} 
//The above calculates the duration on the server when the clinician submits the questionnaire and stores the results on the screening.
        _db.Screenings.Add(screening);
        await _db.SaveChangesAsync();   // saves the screening and all its answers together
        return screening;
    }

    // Patient self-screening from a QR code.
    // The important part: we save the screening AND mark the QR link as "used" in the
    // same database save. If one fails, neither happens. So one QR link = one screening,
    // and nobody can reuse a photographed QR code later.
    public async Task<Screening> SaveSelfScreeningAsync(
        ScreeningInvite invite,
        IDictionary<int, int> numericAnswers,
        IDictionary<int, string> textAnswers)
    {
        if (!invite.IsUsable(DateTime.UtcNow))
            throw new InvalidOperationException("This screening link has expired or has already been used.");

        var screening = await BuildScreeningAsync(invite.PatientId, invite.ClinicianId, numericAnswers, textAnswers, ScreeningSources.SelfScreen);

        _db.Screenings.Add(screening);
        invite.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Link the invite to its screening. We can only do this now, because the database
        // gives the screening its Id number during the save above.
        invite.ScreeningId = screening.Id;
        await _db.SaveChangesAsync();
        return screening;
    }

    private async Task<Screening> BuildScreeningAsync(
        int patientId,
        string clinicianId,
        IDictionary<int, int> numericAnswers,
        IDictionary<int, string> textAnswers,
        string source)
    {
        // 1. The patient must exist, and she must have agreed to her answers being recorded.
        //    POPIA treats health information as "special personal information", which we may
        //    only process with her consent. This check is the last line of defence: even if a
        //    page forgot to check, no screening can be saved without consent.
        var patient = await _db.Patients.FindAsync(patientId)
            ?? throw new InvalidOperationException("Patient not found.");
        if (!patient.ConsentGiven)
            throw new InvalidOperationException("This patient's consent has not been recorded. Record consent on her patient record before screening.");

        // 2. Load the questions from the database
        var questions = await GetActiveQuestionsAsync();

        // 3. The safety question must be answered 0 or 1
        var safety = questions.First(q => q.Type == QuestionType.SafetyFlag);
        if (!numericAnswers.TryGetValue(safety.Id, out var safetyValue) || safetyValue is < 0 or > 1)
            throw new InvalidOperationException("The safety question must be answered Yes or No.");

        // 3b. Every patient context question (Q1-Q4) must be answered too, with one of its options
        foreach (var q in questions.Where(q => q.Type == QuestionType.Context))
        {
            var options = (q.Options ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (!textAnswers.TryGetValue(q.Id, out var text) || string.IsNullOrWhiteSpace(text) || !options.Contains(text))
                throw new InvalidOperationException($"Question {q.Number} needs an answer.");
        }

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
            Band = result.Band,
            Source = source
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

        return screening;
    }

    // Loads one screening together with her details and every answer + question.
    // "Include" tells Entity Framework to fetch the linked rows in the same query,
    // otherwise screening.Patient and answer.Question would be empty.
    public Task<Screening?> LoadFullAsync(int id) =>
        _db.Screenings
            .Include(s => s.Patient)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(s => s.Id == id);
}
