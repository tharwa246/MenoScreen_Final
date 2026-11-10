using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Screenings;

// SCREENING WITHOUT CONSENT ("not saved")
// Only used when she says NO to consent. The clinician can still screen her: the 23
// questions, the score, the band and her handout. But NOTHING is saved anywhere: no patient
// record, no screening, no answers. The result only lives on this page and is gone when the
// page closes. The audit log records THAT an unsaved screening happened, with no name and
// no answers in it.
public class UnsavedModel : PageModel
{
    private readonly ScreeningService _screenings;
    private readonly DocumentService _documents;
    private readonly AuditService _audit;
    private readonly ContentService _content;

    public UnsavedModel(ScreeningService screenings, DocumentService documents, AuditService audit,
                        ContentService content)
    {
        _screenings = screenings;
        _documents = documents;
        _audit = audit;
        _content = content;
    }

    public List<Question> Questions { get; set; } = new();

    [BindProperty]
    public Dictionary<int, int> Answers { get; set; } = new();

    [BindProperty]
    public Dictionary<int, string> TextAnswers { get; set; } = new();

    // Only used for "Hello ..." on her handout. Never stored.
    [BindProperty]
    public string? FirstName { get; set; }

    public Screening? Result { get; set; }
    public List<BlockScore> Blocks { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync() => Questions = await _screenings.GetActiveQuestionsAsync();

    // "See her result": score it in memory and show it on this same page
    public async Task<IActionResult> OnPostAsync()
    {
        Questions = await _screenings.GetActiveQuestionsAsync();
        try
        {
            Result = await _screenings.BuildUnsavedAsync(Answers, TextAnswers, FirstName);
            Blocks = ScoringService.BlockBreakdown(Result.Answers);
            Message = (await _content.GetBandAsync(Result.Band)).ClinicianMessage;
            await _audit.LogAsync("UnsavedScreening", "Screening", null, "No consent: result shown, nothing stored");
        }
        catch (InvalidOperationException ex)
        {
            // e.g. a question was left unanswered
            ErrorMessage = ex.Message;
        }
        return Page();
    }

    // "Download her handout": the answers come back from hidden fields on the page (they were
    // never saved), so the PDF is built in memory as well
    public async Task<IActionResult> OnPostHandoutAsync()
    {
        try
        {
            var result = await _screenings.BuildUnsavedAsync(Answers, TextAnswers, FirstName);
            var wording = await _content.GetBandAsync(result.Band);
            return File(_documents.BuildPatientHandout(result, wording), "application/pdf", "mandi-handout.pdf");
        }
        catch (InvalidOperationException)
        {
            return RedirectToPage();
        }
    }
}