using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Screenings;

public class ResultModel : PageModel
{
    private readonly ScreeningService _screenings;
    private readonly DocumentService _documents;
    private readonly AuditService _audit;
    private readonly ClinicianAssistantService _assistant;

    public ResultModel(ScreeningService screenings, DocumentService documents, AuditService audit, ClinicianAssistantService assistant)
    {
        _screenings = screenings;
        _documents = documents;
        _audit = audit;
        _assistant = assistant;
    }

    public Screening Screening { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public List<BlockScore> Blocks { get; set; } = new();

    // Her scored answers grouped by symptom block. Each answer becomes one coloured
    // "patch" on the page (0 = palest, 3 = darkest), like JoinMandi's Mandi Blanket.
    public List<IGrouping<string, Answer>> PatchRows { get; set; } = new();
    public List<Answer> ContextAnswers { get; set; } = new();

    public bool AssistantAvailable => _assistant.IsConfigured;

    // A suggestion button posts AssistantQuestion; the free-text box posts CustomQuestion
    [BindProperty]
    public string? AssistantQuestion { get; set; }
    [BindProperty]
    public string? CustomQuestion { get; set; }
    public AssistantReply? AssistantReply { get; set; }

    // On-screen messages from section 7 of the brief.
    // Temporary: later these move into the database so the medical team can edit them.
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Unlikely"] = "Her symptom profile does not strongly suggest hormonal change at this time. Reassure her and invite her to return if symptoms persist or worsen.",
        ["Possible"] = "Her symptom profile is consistent with early perimenopause. Begin the menopause conversation. Consider lifestyle support or referral.",
        ["Likely"] = "Her symptom profile is strongly consistent with peri/menopause. Discuss HRT options or refer to a menopause-trained GP or gynaecologist.",
        ["Significant"] = "She is carrying a significant symptom burden. Urgent referral is recommended. Please use warm, validating language in your conversation and in her summary.",
        ["Complex"] = "This patient has existing health conditions that require specialist input before any treatment is considered. Please refer before discussing HRT."
    };

    // Suggested questions shown as one-tap buttons
    public static readonly string[] SuggestedQuestions =
    {
        "What should I focus on first with her?",
        "How do I explain this result to her in plain language?",
        "When should I refer her, and to whom?"
    };

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadAsync(id)) return NotFound();
        // Every view of a result is written to the audit log (POPIA)
        await _audit.LogAsync("ViewResult", "Screening", id);
        return Page();
    }

    // AI clinician assistant
    public async Task<IActionResult> OnPostAskAsync(int id, CancellationToken ct)
    {
        if (!await LoadAsync(id)) return NotFound();

        if (string.IsNullOrWhiteSpace(AssistantQuestion))
            AssistantQuestion = CustomQuestion;

        if (string.IsNullOrWhiteSpace(AssistantQuestion))
        {
            AssistantReply = new AssistantReply(false, "Type a question, or tap one of the suggestions.");
            return Page();
        }

        AssistantReply = await _assistant.AskAsync(Screening, AssistantQuestion, ct);
        // The question text isn't logged: it may contain clinical detail
        await _audit.LogAsync("AskAssistant", "Screening", id, AssistantReply.Ok ? "Answered" : "Not answered");
        return Page();
    }

    public async Task<IActionResult> OnGetClinicianPdfAsync(int id)
    {
        var screening = await _screenings.LoadFullAsync(id);
        if (screening == null) return NotFound();

        await _audit.LogAsync("ExportClinicianPdf", "Screening", id);
        var bytes = _documents.BuildClinicianSummary(screening);
        return File(bytes, "application/pdf", $"clinician-summary-{id}.pdf");
    }

    public async Task<IActionResult> OnGetHandoutPdfAsync(int id)
    {
        var screening = await _screenings.LoadFullAsync(id);
        if (screening == null) return NotFound();

        await _audit.LogAsync("ExportHandoutPdf", "Screening", id);
        var bytes = _documents.BuildPatientHandout(screening);
        return File(bytes, "application/pdf", $"mandi-handout-{id}.pdf");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var screening = await _screenings.LoadFullAsync(id);
        if (screening == null) return false;

        Screening = screening;
        Message = Messages.GetValueOrDefault(screening.Band, string.Empty);
        Blocks = ScoringService.BlockBreakdown(screening.Answers);

        PatchRows = screening.Answers
            .Where(a => a.Question.Type == QuestionType.Scored)
            .OrderBy(a => a.Question.SortOrder)
            .GroupBy(a => a.Question.Block)
            .ToList();

        ContextAnswers = screening.Answers
            .Where(a => a.Question.Type == QuestionType.Context)
            .OrderBy(a => a.Question.SortOrder)
            .ToList();
        return true;
    }
}
