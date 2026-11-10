using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Versions;

public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly VersionService _versions;

    public DetailsModel(ApplicationDbContext db, VersionService versions)
    {
        _db = db;
        _versions = versions;
    }

    public QuestionSetVersion Version { get; set; } = null!;
    public bool IsLocked { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var version = await _db.QuestionSetVersions
            .Include(v => v.Questions)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (version == null) return NotFound();

        Version = version;
        Version.Questions = Version.Questions.OrderBy(q => q.SortOrder).ToList();
        IsLocked = await _versions.IsLockedAsync(id);
        return Page();
    }
}