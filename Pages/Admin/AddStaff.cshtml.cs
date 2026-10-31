using System.ComponentModel.DataAnnotations;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// Admins create clinician and admin accounts here. There is no public sign-up.
// Each account also gets a StaffProfile: the person's real name and, for clinicians,
// the clinic they work at (which decides which patients they can see).
[Authorize(Roles = "Admin")]
public class AddStaffModel : PageModel
{
    private readonly UserManager<IdentityUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public AddStaffModel(UserManager<IdentityUser> users, ApplicationDbContext db, AuditService audit)
    {
        _users = users;
        _db = db;
        _audit = audit;
    }

    public static readonly string[] AssignableRoles = { "Clinician", "Admin" };

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<Clinic> Clinics { get; set; } = new();

    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required, MaxLength(100), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
        [Display(Name = "Temporary password")]
        public string Password { get; set; } = string.Empty;

        [Required, Display(Name = "Role")]
        public string Role { get; set; } = "Clinician";

        // Clinicians must have a clinic; admins don't need one
        [Display(Name = "Clinic")]
        public int? ClinicId { get; set; }
    }

    public async Task OnGetAsync() => await LoadClinicsAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadClinicsAsync();

        if (!AssignableRoles.Contains(Input.Role))
            ModelState.AddModelError("Input.Role", "Choose Clinician or Admin.");

        if (Input.Role == "Clinician" && (Input.ClinicId == null || !Clinics.Any(c => c.Id == Input.ClinicId)))
            ModelState.AddModelError("Input.ClinicId", "Choose the clinic this clinician works at.");

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

        // Their real name and clinic
        _db.StaffProfiles.Add(new StaffProfile
        {
            UserId = user.Id,
            FullName = Input.FullName.Trim(),
            ClinicId = Input.Role == "Clinician" ? Input.ClinicId : null
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("CreateStaffAccount", "User", null, $"{Input.FullName} ({Input.Email}) as {Input.Role}");

        StatusMessage = $"Account created for {Input.FullName} as {Input.Role}. Give them the temporary password in person, and ask them to change it under their account after signing in.";
        ModelState.Clear();
        Input = new();
        return Page();
    }

    private async Task LoadClinicsAsync() =>
        Clinics = await _db.Clinics.OrderBy(c => c.Name).ToListAsync();
}