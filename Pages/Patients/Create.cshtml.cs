using System.ComponentModel.DataAnnotations;
using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Patients;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public CreateModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    // Only these two fields can come from the form
    public class InputModel
    {
        [Required, MaxLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Preferred language")]
        public string PreferredLanguage { get; set; } = "en";
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var allowed = new[] { "en", "zu", "xh", "af" };
        if (!allowed.Contains(Input.PreferredLanguage))
        {
            ModelState.AddModelError("Input.PreferredLanguage", "Choose a language from the list.");
            return Page();
        }

        _db.Patients.Add(new Patient
        {
            FullName = Input.FullName.Trim(),
            PreferredLanguage = Input.PreferredLanguage
        });
        await _db.SaveChangesAsync();

        return RedirectToPage("Index");
    }
}