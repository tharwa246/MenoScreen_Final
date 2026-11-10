using MenoScreen.Data;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Translations;

public class ReviewModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly TranslationAdminService _admin;

    public ReviewModel(ApplicationDbContext db, TranslationAdminService admin)
    {
        _db = db;
        _admin = admin;
    }

    [BindProperty(SupportsGet = true)]
    public int VersionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Lang { get; set; } = "zu";

    [BindProperty]
    public List<ReviewItem> Items { get; set; } = new();

    [BindProperty]
    public List<BlockItem> Blocks { get; set; } = new();

    public Dictionary<int, EnglishQuestion> English { get; set; } = new();
    public int VersionNumber { get; set; }
    public List<string> Errors { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await PrepareAsync()) return RedirectToPage("Index");

        var data = await _admin.LoadReviewAsync(VersionId, Lang);
        Items = data.Items;
        Blocks = data.Blocks;
        English = data.English;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await PrepareAsync()) return RedirectToPage("Index");

        var data = await _admin.LoadReviewAsync(VersionId, Lang);
        English = data.English;

        Errors = await _admin.SaveReviewAsync(VersionId, Lang, Blocks, Items, User.Identity?.Name ?? "admin");

        // On errors, the page shows what was typed so nothing is lost
        if (Errors.Count > 0) return Page();

        Message = "Saved.";
        return RedirectToPage(new { versionId = VersionId, lang = Lang });
    }

    private async Task<bool> PrepareAsync()
    {
        if (Lang == "en" || !TranslationHelper.Languages.ContainsKey(Lang)) return false;

        var version = await _db.QuestionSetVersions.FindAsync(VersionId);
        if (version == null) return false;

        VersionNumber = version.VersionNumber;
        return true;
    }
}
