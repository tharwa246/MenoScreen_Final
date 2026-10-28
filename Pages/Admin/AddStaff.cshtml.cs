using System.ComponentModel.DataAnnotations;
using MenoScreen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Admin;

// Admins create clinician and admin accounts here. There is no public sign-up.
[Authorize(Roles = "Admin")]
public class AddStaffModel : PageModel
{
    private readonly UserManager<IdentityUser> _users;
    private readonly AuditService _audit;

    public AddStaffModel(UserManager<IdentityUser> users, AuditService audit)
    {
        _users = users;
        _audit = audit;
    }

    public static readonly string[] AssignableRoles = { "Clinician", "Admin" };

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
        [Display(Name = "Temporary password")]
        public string Password { get; set; } = string.Empty;

        [Required, Display(Name = "Role")]
        public string Role { get; set; } = "Clinician";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!AssignableRoles.Contains(Input.Role))
            ModelState.AddModelError("Input.Role", "Choose Clinician or Admin.");

        if (!ModelState.IsValid) return Page();

        if (await _users.FindByEmailAsync(Input.Email) != null)
        {
            ModelState.AddModelError("Input.Email", "There's already an account with this email.");
            return Page();
        }

        var user = new IdentityUser { UserName = Input.Email, Email = Input.Email, EmailConfirmed = true };
        var created = await _users.CreateAsync(user, Input.Password);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        await _users.AddToRoleAsync(user, Input.Role);
        await _audit.LogAsync("CreateStaffAccount", "User", null, $"{Input.Email} as {Input.Role}");

        StatusMessage = $"Account created for {Input.Email} as {Input.Role}. Give them the temporary password in person, and ask them to change it under their account after signing in.";
        ModelState.Clear();
        Input = new();
        return Page();
    }
}
