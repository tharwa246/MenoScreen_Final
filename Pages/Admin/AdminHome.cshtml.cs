using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;
//This page is only accessible to users in the Admin role.
//The Authorize attribute is what enforces that.
[Authorize(Roles = "Admin")]
public class AdminHomeModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public AdminHomeModel(ApplicationDbContext db) => _db = db;

    public List<AuditLog> Entries { get; set; } = new();
    public int ActiveQuestionCount { get; set; }
    public int? ActiveVersion { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }

    public List<string> Actions { get; set; } = new();

    public async Task OnGetAsync()
    {
        var active = await _db.QuestionSetVersions
            .Where(v => v.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new { v.VersionNumber, Count = v.Questions.Count })
            .FirstOrDefaultAsync();
        ActiveVersion = active?.VersionNumber;
        ActiveQuestionCount = active?.Count ?? 0;

        Actions = await _db.AuditLogs.Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();

        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Filter))
            query = query.Where(a => a.Action == Filter);

        // Newest 200 entries is plenty for a demo; add paging if the clinic grows
        Entries = await query.OrderByDescending(a => a.At).Take(200).ToListAsync();
    }
}
