using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Versions;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly VersionService _versions;

    public IndexModel(ApplicationDbContext db, VersionService versions)
    {
        _db = db;
        _versions = versions;
    }

    public record Row(int Id, int VersionNumber, bool IsActive, string? Notes,
                      DateTime CreatedAt, int QuestionCount, int ScoredCount, int ScreeningCount);

    public List<Row> Rows { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Rows = await _db.QuestionSetVersions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new Row(
                v.Id,
                v.VersionNumber,
                v.IsActive,
                v.Notes,
                v.CreatedAt,
                v.Questions.Count,
                v.Questions.Count(q => q.Type == QuestionType.Scored),
                _db.Screenings.Count(s => s.QuestionSetVersionId == v.Id)))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCloneAsync(int id)
    {
        try
        {
            var copy = await _versions.CloneAsync(id);
            Message = $"Created draft version {copy.VersionNumber}. It is not active yet.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(int id)
    {
        try
        {
            await _versions.ActivateAsync(id);
            Message = "Version activated. New screenings will use it.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        return RedirectToPage();
    }
}