using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
namespace MenoScreen.Pages.Screenings;

public class NewModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ScreeningService _screenings;
    private readonly AuditService _audit;
    public DateTime ConsultationStartedAt { get; set; }

    public NewModel(ApplicationDbContext db, ScreeningService screenings, AuditService audit)
    {
        _db = db;
        _screenings = screenings;
        _audit = audit;
    }

    public Patient Patient { get; set; } = null!;
    public List<Question> Questions { get; set; } = new();

    // QuestionId -> 0-3 (scored) or 0/1 (safety question)
    [BindProperty]
    public Dictionary<int, int> Answers { get; set; } = new();

    // QuestionId -> text (context questions)
    [BindProperty]
    public Dictionary<int, string> TextAnswers { get; set; } = new();

    public string? ErrorMessage { get; set; }

    [TempData]
    public DateTime? ConsultationStartedAtValue { get; set; }

    public async Task<IActionResult> OnGetAsync(int patientId)
    {
        if (!await LoadAsync(patientId)) return NotFound();

        // POPIA: no consent, no screening
        if (!Patient.ConsentGiven)
            return RedirectToPage("/Patients/Details", new { id = patientId });

        ConsultationStartedAtValue = DateTime.UtcNow;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int patientId)
    {
        if (!await LoadAsync(patientId)) return NotFound();

        var clinicianId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var screening = await _screenings.SaveScreeningAsync(
                patientId, clinicianId, Answers, TextAnswers, ConsultationStartedAtValue);

            await _audit.LogAsync("CreateScreening", "Screening", screening.Id, $"Patient {patientId}");
            return RedirectToPage("Result", new { id = screening.Id });
        }
        catch (InvalidOperationException ex)
        {
            // e.g. a question was left unanswered
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient == null) return false;

        Patient = patient;
        Questions = await _screenings.GetActiveQuestionsAsync();
        return true;
    }
}
