using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Screenings;

public class NewModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ScreeningService _screenings;
    private readonly ContentService _content;

    public NewModel(ApplicationDbContext db, ScreeningService screenings, ContentService content)
    {
        _db = db;
        _screenings = screenings;
        _content = content;
    }

    public Patient Patient { get; set; } = null!;
    public List<QuestionView> Questions { get; set; } = new();

    // Optional ?lang=zu in the address. If empty, the patient's preferred language is used.
    [BindProperty(SupportsGet = true)]
    public string? Lang { get; set; }

    public string Language { get; set; } = "en";

    // Labels from the database, in the chosen language
    public Dictionary<string, string> Texts { get; set; } = new();

    // QuestionId -> 0-3 (scored) or 0/1 (safety question)
    [BindProperty]
    public Dictionary<int, int> Answers { get; set; } = new();

    // QuestionId -> English text of the chosen option (context questions)
    [BindProperty]
    public Dictionary<int, string> TextAnswers { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int patientId)
    {
        if (!await LoadAsync(patientId)) return NotFound();
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

            return RedirectToPage("Result", new { id = screening.Id });
        }
        catch (InvalidOperationException ex)
        {
            // e.g. a question was left unanswered (this message is still in English)
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private async Task<bool> LoadAsync(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient == null) return false;

        Patient = patient;
        Language = TranslationHelper.Normalize(
            string.IsNullOrWhiteSpace(Lang) ? patient.PreferredLanguage : Lang);

        Texts = await _content.GetAllAsync(Language);

        var questions = await _screenings.GetActiveQuestionsAsync();
        Questions = questions.Select(q => TranslationHelper.Localize(q, Language)).ToList();
        return true;
    }
}