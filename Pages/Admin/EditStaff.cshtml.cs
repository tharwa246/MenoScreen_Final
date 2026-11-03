using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// EDIT ONE STAFF ACCOUNT (admins only). Address: /Admin/EditStaff?id=<their login id>
// Change name, role and clinic; disable or enable the account; or set a new temporary password.
// Guardrails: you can't disable or demote yourself, and the last active admin can't be
// removed, so the clinic can never be locked out of its own system.
public class EditStaffModel : PageModel
{
    private readonly UserManager<IdentityUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public EditStaffModel(UserManager<IdentityUser> users, ApplicationDbContext db, AuditService audit)
    {
        _users = users;
        _db = db;
        _audit = audit;
    }

    // "Disabled" = locked for 100 years. A normal lockout (5 wrong passwords) only lasts minutes,
    // so anything locked more than 50 years ahead must have been disabled by an admin.
    public static bool IsDisabled(IdentityUser user) =>
        user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow.AddYears(50);

    public IdentityUser StaffUser { get; set; } = null!;
    public bool Disabled { get; set; }
    public bool IsMe { get; set; }
    public List<Clinic> Clinics { get; set; } = new();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public string? NewPassword { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required, MaxLength(100), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, Display(Name = "Role")]
        public string Role { get; set; } = "Clinician";

        [Display(Name = "Clinic")]
        public int? ClinicId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (!await LoadAsync(id)) return NotFound();

        var profile = await _db.StaffProfiles.FindAsync(id);
        var roles = await _users.GetRolesAsync(StaffUser);
        Input = new InputModel
        {
            FullName = profile?.FullName ?? "",
            Role = roles.Contains("Admin") ? "Admin" : "Clinician",
            ClinicId = profile?.ClinicId
        };
        return Page();
    }

    // "Save changes": name, role and clinic
    public async Task<IActionResult> OnPostSaveAsync(string id)
    {
        if (!await LoadAsync(id)) return NotFound();

        if (!AddStaffModel.AssignableRoles.Contains(Input.Role))
            ModelState.AddModelError("Input.Role", "Choose Clinician or Admin.");

        if (Input.Role == "Clinician" && (Input.ClinicId == null || !Clinics.Any(c => c.Id == Input.ClinicId)))
            ModelState.AddModelError("Input.ClinicId", "Choose the clinic this clinician works at.");

        var roles = await _users.GetRolesAsync(StaffUser);
        if (roles.Contains("Admin") && Input.Role != "Admin")
        {
            if (IsMe)
                ModelState.AddModelError("Input.Role", "You can't remove your own admin role.");
            else if (await ActiveAdminCountAsync() <= 1)
                ModelState.AddModelError("Input.Role", "This is the last admin. Make someone else an admin first.");
        }

        if (!ModelState.IsValid) return Page();

        // Role: swap the old staff role for the new one
        if (!roles.Contains(Input.Role))
        {
            await _users.RemoveFromRolesAsync(StaffUser, roles.Where(r => AddStaffModel.AssignableRoles.Contains(r)));
            await _users.AddToRoleAsync(StaffUser, Input.Role);
            // Makes their next click pick up the new role, instead of waiting for them to sign in again
            await _users.UpdateSecurityStampAsync(StaffUser);
        }

        // Name and clinic (creates the profile if this account never had one)
        var profile = await _db.StaffProfiles.FindAsync(id);
        if (profile == null)
        {
            profile = new StaffProfile { UserId = id };
            _db.StaffProfiles.Add(profile);
        }
        profile.FullName = Input.FullName.Trim();
        profile.ClinicId = Input.Role == "Clinician" ? Input.ClinicId : null;
        await _db.SaveChangesAsync();

        var clinicName = Clinics.FirstOrDefault(c => c.Id == profile.ClinicId)?.Name ?? "all clinics";
        await _audit.LogAsync("UpdateStaffAccount", "User", null, $"{profile.FullName} ({StaffUser.Email}): {Input.Role}, {clinicName}");

        StatusMessage = $"{profile.FullName}'s account saved.";
        return RedirectToPage(new { id });
    }

    // "Disable account": they can't sign in, but nothing is deleted
    public async Task<IActionResult> OnPostDisableAsync(string id)
    {
        if (!await LoadAsync(id)) return NotFound();

        if (IsMe)
        {
            StatusMessage = "You can't disable your own account.";
            return RedirectToPage(new { id });
        }
        if (await _users.IsInRoleAsync(StaffUser, "Admin") && await ActiveAdminCountAsync() <= 1)
        {
            StatusMessage = "This is the last active admin, so it can't be disabled.";
            return RedirectToPage(new { id });
        }

        await _users.SetLockoutEnabledAsync(StaffUser, true);
        await _users.SetLockoutEndDateAsync(StaffUser, DateTimeOffset.UtcNow.AddYears(100));
        // Signs them out if they're signed in right now, not just at their next login
        await _users.UpdateSecurityStampAsync(StaffUser);
        await _audit.LogAsync("DisableStaffAccount", "User", null, StaffUser.Email);

        StatusMessage = "Account disabled. They can't sign in, and their history is kept.";
        return RedirectToPage(new { id });
    }

    // "Enable account": lets them sign in again
    public async Task<IActionResult> OnPostEnableAsync(string id)
    {
        if (!await LoadAsync(id)) return NotFound();

        await _users.SetLockoutEndDateAsync(StaffUser, null);
        await _users.ResetAccessFailedCountAsync(StaffUser);
        await _audit.LogAsync("EnableStaffAccount", "User", null, StaffUser.Email);

        StatusMessage = "Account enabled. They can sign in again.";
        return RedirectToPage(new { id });
    }

    // "Set password": a new temporary password, for someone who forgot theirs
    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        if (!await LoadAsync(id)) return NotFound();

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            StatusMessage = "The new password needs at least 8 characters.";
            return RedirectToPage(new { id });
        }

        // Identity's built-in reset: a one-time token, then the new password is hashed and saved
        var token = await _users.GeneratePasswordResetTokenAsync(StaffUser);
        var result = await _users.ResetPasswordAsync(StaffUser, token, NewPassword);
        if (!result.Succeeded)
        {
            StatusMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToPage(new { id });
        }

        // The audit log records THAT it was reset, never the password itself
        await _audit.LogAsync("ResetStaffPassword", "User", null, StaffUser.Email);

        StatusMessage = "Password reset. Give them the new temporary password in person, and ask them to change it after signing in.";
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return false;

        // Only staff accounts are managed here (patient logins will have their own pages)
        if (!await _users.IsInRoleAsync(user, "Admin") && !await _users.IsInRoleAsync(user, "Clinician"))
            return false;

        StaffUser = user;
        Disabled = IsDisabled(user);
        IsMe = user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier);
        Clinics = await _db.Clinics.OrderBy(c => c.Name).ToListAsync();
        return true;
    }

    private async Task<int> ActiveAdminCountAsync() =>
        (await _users.GetUsersInRoleAsync("Admin")).Count(u => !IsDisabled(u));
}