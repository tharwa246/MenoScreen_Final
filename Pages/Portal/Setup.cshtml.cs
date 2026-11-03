using System.ComponentModel.DataAnnotations;
using MenoScreen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Portal;

// SET UP YOUR PORTAL ACCOUNT (open to anyone: she isn't signed in yet)
// She types the username and setup code from her clinician, and chooses her own password.
public class SetupModel : PageModel
{
    private readonly PortalService _portal;
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly AuditService _audit;

    public SetupModel(PortalService portal, SignInManager<IdentityUser> signIn, AuditService audit)
    {
        _portal = portal;
        _signIn = signIn;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter your username."), Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your setup code."), Display(Name = "Setup code")]
        public string SetupCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Choose a password."), StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
        [Display(Name = "Choose a password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
        [Display(Name = "Type it again")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    // The QR code on her pack opens /Portal/Setup?u=her-username, so she only types the code
    public void OnGet(string? u) => Input.Username = u ?? string.Empty;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var (user, error) = await _portal.CompleteSetupAsync(Input.Username, Input.SetupCode, Input.Password);
        if (user == null)
        {
            ErrorMessage = error;
            return Page();
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        await _audit.LogAsync("PortalSetupComplete", "User", null, user.UserName);
        return RedirectToPage("/Portal/Index");
    }
}