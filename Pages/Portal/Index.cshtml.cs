using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Portal;

// THE PATIENT'S HOME. Everything here comes from GetMyPatientAsync, so only her own data.
public class IndexModel : PageModel
{
    private readonly PortalService _portal;

    public IndexModel(PortalService portal) => _portal = portal;

    public Patient? Me { get; set; }
    public string FirstName => Me?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    public async Task OnGetAsync() => Me = await _portal.GetMyPatientAsync();
}