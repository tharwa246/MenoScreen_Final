using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;

namespace MenoScreen.Pages.Admin;
//This page is only accessible to users in the Admin role. 
//The Authorize attribute is what enforces that.
[Authorize(Roles = "Admin")] 
public class AdminHomeModel : PageModel
{
    public void OnGet()
    {
         
    }
}

