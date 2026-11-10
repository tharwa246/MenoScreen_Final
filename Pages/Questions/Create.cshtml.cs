using System.ComponentModel.DataAnnotations;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages_Questions;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly VersionService _versions;

    public CreateModel(ApplicationDbContext db, VersionService versions)
    {
        _db = db;
        _versions = versions;
    }

    public int VersionId { get; set; }
    public int VersionNumber { get; set; }
    public List<string> Blocks { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Only these fields can come from the form (the version is never posted)
    public class InputModel
    {
        [Range(1, 200)]
        public int Number { get; set; }

        [Range(1, 200)]
        [Display(Name = "Sort order")]
        public int SortOrder { get; set; }

        public string? Block { get; set; }

        [Display(Name = "Or add a new block")]
        [MaxLength(60)]
        public string? NewBlock { get; set; }

        [Required, MaxLength(500)]
        public string Text { get; set; } = string.Empty;

        public QuestionType Type { get; set; } = QuestionType.Scored;

        [MaxLength(300)]
        public string? Options { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int versionId)
    {
        var result = await LoadAsync(versionId);
        if (result != null) return result;

        var next = await _db.Questions
            .Where(q => q.QuestionSetVersionId == versionId)
            .MaxAsync(q => (int?)q.Number) ?? 0;

        Input.Number = next + 1;
        Input.SortOrder = next + 1;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int versionId)
    {
        var result = await LoadAsync(versionId);
        if (result != null) return result;

        // New block name wins over the dropdown
        var block = !string.IsNullOrWhiteSpace(Input.NewBlock)
            ? Input.NewBlock.Trim()
            : Input.Block;

        if (string.IsNullOrWhiteSpace(block))
            ModelState.AddModelError("Input.Block", "Choose a block or type a new one.");

        if (Input.Type == QuestionType.Context && string.IsNullOrWhiteSpace(Input.Options))
            ModelState.AddModelError("Input.Options", "Context questions need answer options, separated by |");

        if (Input.Type == QuestionType.SafetyFlag &&
            await _db.Questions.AnyAsync(q => q.QuestionSetVersionId == versionId && q.Type == QuestionType.SafetyFlag))
            ModelState.AddModelError("Input.Type", "This version already has a safety question. A version can only have one.");

        if (await _db.Questions.AnyAsync(q => q.QuestionSetVersionId == versionId && q.Number == Input.Number))
            ModelState.AddModelError("Input.Number", "That question number is already used in this version.");

        if (!ModelState.IsValid) return Page();

        _db.Questions.Add(new Question
        {
            QuestionSetVersionId = versionId,
            Number = Input.Number,
            SortOrder = Input.SortOrder,
            Block = block!,
            Text = Input.Text.Trim(),
            Type = Input.Type,
            // Only context questions have options
            Options = Input.Type == QuestionType.Context ? Input.Options!.Trim() : null
        });
        await _db.SaveChangesAsync();

        return RedirectToPage("/Versions/Details", new { id = versionId });
    }

    // Returns a redirect/NotFound if the version can't be edited, otherwise null
    private async Task<IActionResult?> LoadAsync(int versionId)
    {
        var version = await _db.QuestionSetVersions.FindAsync(versionId);
        if (version == null) return NotFound();

        if (await _versions.IsLockedAsync(versionId))
            return RedirectToPage("/Versions/Details", new { id = versionId });

        VersionId = versionId;
        VersionNumber = version.VersionNumber;

        Blocks = await _db.Questions
            .Where(q => q.QuestionSetVersionId == versionId)
            .OrderBy(q => q.SortOrder)
            .Select(q => q.Block)
            .Distinct()
            .ToListAsync();

        return null;
    }
}