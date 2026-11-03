using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        // Patients have their own home: the portal
        if (User.IsInRole("Patient")) return RedirectToPage("/Portal/Index");
        return Page();
    }
}