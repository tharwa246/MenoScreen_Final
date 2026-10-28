using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Areas.Identity.Pages.Account;

// OUR OWN SIGN-IN PAGE
// ASP.NET Identity comes with a built-in login page. Putting a page at this exact path
// (Areas/Identity/Pages/Account/Login) replaces the built-in one with ours, so every
// "please sign in" redirect in the app lands here automatically.
public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(SignInManager<IdentityUser> signInManager, ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Where to go after signing in (e.g. the page she was trying to open)
    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter your email address.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Keep me signed in on this computer")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
            ModelState.AddModelError(string.Empty, ErrorMessage);

        // Clear any half-finished external sign-in, so we start clean
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        ReturnUrl = returnUrl ?? Url.Content("~/");
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        if (!ModelState.IsValid) return Page();

        // lockoutOnFailure: true = after 5 wrong passwords the account is locked for a few
        // minutes. This stops someone guessing a clinician's password (POPIA safeguard).
        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User signed in.");
            // LocalRedirect only allows addresses inside this app, so a fake link can't
            // send someone to another website after signing in.
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Too many wrong passwords. This account is locked for a few minutes. Try again later, or ask your clinic admin.");
            return Page();
        }

        // Same message whether the email or the password was wrong, so nobody can use
        // this page to find out which emails have accounts.
        ModelState.AddModelError(string.Empty, "That email and password don't match. Check them and try again.");
        return Page();
    }
}
