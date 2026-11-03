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
    private readonly ClinicScopeService _scope;
    private readonly PortalService _portal;

    public ResultModel(ScreeningService screenings, DocumentService documents, AuditService audit,
                       ClinicianAssistantService assistant, ClinicScopeService scope, PortalService portal)
    {
        _screenings = screenings;
        _documents = documents;
        _audit = audit;
        _assistant = assistant;
        _scope = scope;
        _portal = portal;
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
    public static readonly Dictionary<string, string> Messages = new()
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
        if (screening == null || await _scope.FindPatientAsync(screening.PatientId) == null) return NotFound();

        await _audit.LogAsync("ExportClinicianPdf", "Screening", id);
        var bytes = _documents.BuildClinicianSummary(screening);
        return File(bytes, "application/pdf", $"clinician-summary-{id}.pdf");
    }

    // "Download her screening pack": ONE PDF. Page 1 is hers (result, JoinMandi and, if ticked,
    // her portal login), page 2 is the clinician summary for her file.
    // It's a POST, not a link, because it can create her portal account or a new setup code.
    public async Task<IActionResult> OnPostPackPdfAsync(int id, bool includePortal)
    {
        var screening = await _screenings.LoadFullAsync(id);
        if (screening == null || await _scope.FindPatientAsync(screening.PatientId) == null) return NotFound();

        PortalLogin? login = null;
        if (includePortal && screening.Patient.ConsentGiven)
        {
            string username;
            string code;
            if (screening.Patient.UserId == null)
            {
                // First time: make her account (generated username + setup code)
                (username, code) = await _portal.CreateAccessAsync(screening.Patient);
                await _audit.LogAsync("CreatePortalAccess", "Patient", screening.PatientId, $"Username {username}");
            }
            else
            {
                // She already has an account: a fresh code (any older printout stops working)
                code = await _portal.NewSetupCodeAsync(screening.Patient);
                username = await _portal.GetUsernameAsync(screening.Patient) ?? string.Empty;
                await _audit.LogAsync("NewPortalSetupCode", "Patient", screening.PatientId);
            }

            // The QR code opens the setup page with her username already filled in
            var setupUrl = Url.Page("/Portal/Setup", pageHandler: null, values: new { u = username }, protocol: Request.Scheme)!;
            login = new PortalLogin(username, code, setupUrl);
        }

        await _audit.LogAsync("ExportScreeningPack", "Screening", id);
        var bytes = _documents.BuildScreeningPack(screening, login);
        return File(bytes, "application/pdf", $"mandi-screening-pack-{id}.pdf");
    }

    public async Task<IActionResult> OnGetHandoutPdfAsync(int id)
    {
        var screening = await _screenings.LoadFullAsync(id);
        if (screening == null || await _scope.FindPatientAsync(screening.PatientId) == null) return NotFound();

        await _audit.LogAsync("ExportHandoutPdf", "Screening", id);
        var bytes = _documents.BuildPatientHandout(screening);
        return File(bytes, "application/pdf", $"mandi-handout-{id}.pdf");
    }

    private async Task<bool> LoadAsync(int id)
    {
        var screening = await _screenings.LoadFullAsync(id);
        // Another clinic's result counts as "not found"
        if (screening == null || await _scope.FindPatientAsync(screening.PatientId) == null) return false;

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
