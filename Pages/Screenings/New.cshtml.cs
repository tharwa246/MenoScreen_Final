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
    private readonly ClinicScopeService _scope;
    private readonly ContentService _content;

    public NewModel(ApplicationDbContext db, ScreeningService screenings, AuditService audit, ClinicScopeService scope,
                    ContentService content)
    {
        _db = db;
        _screenings = screenings;
        _audit = audit;
        _scope = scope;
        _content = content;
    }

    // Optional ?lang=zu in the address. If empty, her preferred language is used.
    [BindProperty(SupportsGet = true)]
    public string? Lang { get; set; }

    // The language actually used (en, zu, xh or af)
    public string Language { get; set; } = "en";

    // Interface wording (rating labels, Yes/No...) in that language
    public Dictionary<string, string> Texts { get; set; } = new();

    public Patient Patient { get; set; } = null!;
    public List<Question> Questions { get; set; } = new();

    // QuestionId -> 0-3 (scored) or 0/1 (safety question)
    [BindProperty]
    public Dictionary<int, int> Answers { get; set; } = new();

    // QuestionId -> text (context questions)
    [BindProperty]
    public Dictionary<int, string> TextAnswers { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int patientId)
    {
        if (!await LoadAsync(patientId)) return NotFound();

        // POPIA: no consent, no screening
        if (!Patient.ConsentGiven)
            return RedirectToPage("/Patients/Details", new { id = patientId });

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int patientId)
    {
        if (!await LoadAsync(patientId)) return NotFound();

        var clinicianId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        try
        {
            var screening = await _screenings.SaveScreeningAsync(
                patientId, clinicianId, Answers, TextAnswers);

            await _audit.LogAsync("CreateScreening", "Screening", screening.Id, $"Patient {patientId}, language {Language}");
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
        var patient = await _scope.FindPatientAsync(patientId);
        if (patient == null) return false;

        Patient = patient;
        Questions = await _screenings.GetActiveQuestionsAsync();

        // The language picked on the page, otherwise her preferred language from her record
        Language = TranslationHelper.Normalize(string.IsNullOrWhiteSpace(Lang) ? patient.PreferredLanguage : Lang);
        Texts = await _content.GetAllAsync(Language);
        return true;
    }
}
