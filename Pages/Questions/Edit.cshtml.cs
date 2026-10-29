using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages_Questions;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly VersionService _versions;

    public EditModel(ApplicationDbContext db, VersionService versions)
    {
        _db = db;
        _versions = versions;
    }

    [BindProperty]
    public Question Question { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id == null) return NotFound();

        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id);
        if (question == null) return NotFound();

        // Locked versions can't be edited, even by typing the URL
        if (await _versions.IsLockedAsync(question.QuestionSetVersionId))
            return RedirectToPage("/Versions/Details", new { id = question.QuestionSetVersionId });

        Question = question;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // The form doesn't send the navigation property, so don't validate it
        ModelState.Remove("Question.QuestionSetVersion");

        var existing = await _db.Questions.FirstOrDefaultAsync(q => q.Id == Question.Id);
        if (existing == null) return NotFound();

        if (await _versions.IsLockedAsync(existing.QuestionSetVersionId))
            return RedirectToPage("/Versions/Details", new { id = existing.QuestionSetVersionId });

        if (!ModelState.IsValid) return Page();

        // Only these fields can change. The version can't be moved.
        existing.Number = Question.Number;
        existing.SortOrder = Question.SortOrder;
        existing.Block = Question.Block;
        existing.Text = Question.Text;
        existing.Type = Question.Type;
        existing.Options = Question.Options;

        await _db.SaveChangesAsync();

        return RedirectToPage("/Versions/Details", new { id = existing.QuestionSetVersionId });
    }
}