using System.ComponentModel.DataAnnotations;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// MANAGE THE PATIENT PORTAL'S SELF-CARE TIPS (admins only: the /Admin folder needs AdminOnly)
// Add a tip, edit its wording or order, hide it, or delete it. Every change is audited.
public class SuggestionsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public SuggestionsModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public List<Suggestion> All { get; set; } = new();

    [BindProperty]
    public NewTipInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class NewTipInput
    {
        [Required, Display(Name = "Symptom area")]
        public string Area { get; set; } = SuggestionAreas.General;

        [Required(ErrorMessage = "Write the tip."), MaxLength(300), Display(Name = "Tip")]
        public string Text { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => await LoadAsync();

    // "Add tip"
    public async Task<IActionResult> OnPostAddAsync()
    {
        // Only the areas in our list are allowed (someone could edit the form in the browser)
        if (!SuggestionAreas.Titles.ContainsKey(Input.Area))
            ModelState.AddModelError("Input.Area", "Choose an area from the list.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        // New tips go to the end of their area
        var lastOrder = await _db.Suggestions
            .Where(s => s.Area == Input.Area)
            .MaxAsync(s => (int?)s.SortOrder) ?? 0;

        var tip = new Suggestion { Area = Input.Area, Text = Input.Text.Trim(), SortOrder = lastOrder + 10 };
        _db.Suggestions.Add(tip);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreateSuggestion", "Suggestion", tip.Id, tip.Area);

        StatusMessage = "Tip added.";
        return RedirectToPage();
    }

    // "Save" on one tip: wording, order, and shown/hidden
    public async Task<IActionResult> OnPostSaveAsync(int id, string? text, int sortOrder, bool isActive)
    {
        var tip = await _db.Suggestions.FindAsync(id);
        if (tip == null) return NotFound();

        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 300)
        {
            StatusMessage = "A tip needs some text, up to 300 characters.";
            return RedirectToPage();
        }

        tip.Text = text;
        tip.SortOrder = sortOrder;
        tip.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UpdateSuggestion", "Suggestion", id, isActive ? "Shown" : "Hidden");

        StatusMessage = "Tip saved.";
        return RedirectToPage();
    }

    // "Delete": removes it completely (untick "Shown" instead to keep it but hide it)
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var tip = await _db.Suggestions.FindAsync(id);
        if (tip == null) return NotFound();

        _db.Suggestions.Remove(tip);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("DeleteSuggestion", "Suggestion", id, tip.Area);

        StatusMessage = "Tip deleted.";
        return RedirectToPage();
    }

    private async Task LoadAsync() =>
        All = await _db.Suggestions
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToListAsync();
}