using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Translations;

public class WordingModel : PageModel
{
    private readonly TranslationAdminService _admin;

    public WordingModel(TranslationAdminService admin)
    {
        _admin = admin;
    }

    [BindProperty(SupportsGet = true)]
    public string Lang { get; set; } = "zu";

    [BindProperty]
    public List<ContentItem> Items { get; set; } = new();

    public Dictionary<string, string> English { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Valid()) return RedirectToPage("Index");

        var data = await _admin.LoadWordingAsync(Lang);
        Items = data.Items;
        English = data.English;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Valid()) return RedirectToPage("Index");

        var data = await _admin.LoadWordingAsync(Lang);
        English = data.English;

        Errors = await _admin.SaveWordingAsync(Lang, Items, User.Identity?.Name ?? "admin");
        if (Errors.Count > 0) return Page();

        Message = "Saved.";
        return RedirectToPage(new { lang = Lang });
    }

    private bool Valid() => Lang != "en" && TranslationHelper.Languages.ContainsKey(Lang);
}