using MenoScreen.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// ALL STAFF ACCOUNTS (admins only: the /Admin folder needs the AdminOnly policy)
// One row per clinician or admin: name, email, role, clinic and whether they can sign in.
public class StaffModel : PageModel
{
    private readonly UserManager<IdentityUser> _users;
    private readonly ApplicationDbContext _db;

    public StaffModel(UserManager<IdentityUser> users, ApplicationDbContext db)
    {
        _users = users;
        _db = db;
    }

    public record StaffRow(string UserId, string Name, string Email, string Role, string Clinic, bool Disabled);
    public List<StaffRow> Staff { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Names and clinics come from StaffProfiles; logins and roles come from Identity
        var profiles = await _db.StaffProfiles.Include(s => s.Clinic).ToDictionaryAsync(s => s.UserId);
        var admins = await _users.GetUsersInRoleAsync("Admin");
        var clinicians = await _users.GetUsersInRoleAsync("Clinician");

        Staff = admins.Select(u => (User: u, Role: "Admin"))
            .Concat(clinicians.Select(u => (User: u, Role: "Clinician")))
            .Select(x =>
            {
                profiles.TryGetValue(x.User.Id, out var p);
                return new StaffRow(
                    x.User.Id,
                    p?.FullName ?? "(no name yet)",
                    x.User.Email ?? "",
                    x.Role,
                    x.Role == "Admin" ? "All clinics" : p?.Clinic?.Name ?? "No clinic",
                    EditStaffModel.IsDisabled(x.User));
            })
            .OrderBy(r => r.Role)
            .ThenBy(r => r.Name)
            .ToList();
    }
}