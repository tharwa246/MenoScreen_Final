using MenoScreen.Data;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Translations;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly TranslationAdminService _admin;
    private readonly AzureTranslatorService _translator;

    public IndexModel(ApplicationDbContext db, TranslationAdminService admin, AzureTranslatorService translator)
    {
        _db = db;
        _admin = admin;
        _translator = translator;
    }

    public record VersionOption(int Id, int VersionNumber, bool IsActive);
    public record CoverageRow(string Code, string Name, int Translated, int Reviewed);

    [BindProperty(SupportsGet = true)]
    public int? VersionId { get; set; }

    public List<VersionOption> Versions { get; set; } = new();
    public int SelectedVersionId { get; set; }
    public int QuestionTotal { get; set; }
    public List<CoverageRow> QuestionCoverage { get; set; } = new();
    public int WordingTotal { get; set; }
    public List<CoverageRow> WordingCoverage { get; set; } = new();

    public bool TranslatorConfigured => _translator.IsConfigured;

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Versions = await _db.QuestionSetVersions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new VersionOption(v.Id, v.VersionNumber, v.IsActive))
            .ToListAsync();

        if (Versions.Count == 0) return;

        SelectedVersionId = VersionId ?? Versions.FirstOrDefault(v => v.IsActive)?.Id ?? Versions[0].Id;
        if (!Versions.Any(v => v.Id == SelectedVersionId))
            SelectedVersionId = Versions[0].Id;

        QuestionTotal = await _db.Questions.CountAsync(q => q.QuestionSetVersionId == SelectedVersionId);
        WordingTotal = await _db.ContentTexts.CountAsync(c => c.Language == "en");

        foreach (var (code, name) in TranslationHelper.Languages.Where(l => l.Key != "en"))
        {
            var qTranslated = await _db.QuestionTranslations.CountAsync(t =>
                t.Question.QuestionSetVersionId == SelectedVersionId && t.Language == code && t.Text != "");
            var qReviewed = await _db.QuestionTranslations.CountAsync(t =>
                t.Question.QuestionSetVersionId == SelectedVersionId && t.Language == code && t.IsReviewed);
            QuestionCoverage.Add(new CoverageRow(code, name, qTranslated, qReviewed));

            var wTranslated = await _db.ContentTexts.CountAsync(c => c.Language == code && c.Value != "");
            var wReviewed = await _db.ContentTexts.CountAsync(c => c.Language == code && c.IsReviewed);
            WordingCoverage.Add(new CoverageRow(code, name, wTranslated, wReviewed));
        }
    }

    public async Task<IActionResult> OnPostTranslateQuestionsAsync(int versionId, string language, bool replaceDrafts)
    {
        try
        {
            var r = await _admin.TranslateVersionAsync(
                versionId, language, User.Identity?.Name ?? "admin", replaceDrafts);

            Message = $"Translated {r.Translated} question(s) into {TranslationHelper.Languages[language]} as drafts " +
                      $"(skipped {r.SkippedReviewed} reviewed and {r.SkippedExisting} existing). Please review them.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            Message = "Translation failed, nothing was changed. " + ex.Message;
        }
        return RedirectToPage(new { versionId });
    }

    public async Task<IActionResult> OnPostTranslateInterfaceAsync(int versionId, string language, bool replaceDrafts)
    {
        try
        {
            var r = await _admin.TranslateInterfaceAsync(
                language, User.Identity?.Name ?? "admin", replaceDrafts);

            Message = $"Translated {r.Translated} interface text(s) into {TranslationHelper.Languages[language]} as drafts " +
                      $"(skipped {r.SkippedReviewed} reviewed and {r.SkippedExisting} existing). Please review them.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            Message = "Translation failed, nothing was changed. " + ex.Message;
        }
        return RedirectToPage(new { versionId });
    }
}