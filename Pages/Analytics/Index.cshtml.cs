using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Analytics;

// CLINIC ANALYTICS (anonymous totals only; protected by "StaffOnly" in Program.cs)
// - A clinician always sees her OWN clinic. She can't pick another one.
// - An admin sees all clinics together, or one clinic picked from the pie chart or
//   drop-down (the address then becomes /Analytics?clinicId=2).
public class IndexModel : PageModel
{
    private readonly AnalyticsService _analytics;
    private readonly ClinicScopeService _scope;

    public IndexModel(AnalyticsService analytics, ClinicScopeService scope)
    {
        _analytics = analytics;
        _scope = scope;
    }

    // From the address bar: ?clinicId=2 (only used for admins)
    [BindProperty(SupportsGet = true)]
    public int? ClinicId { get; set; }

    public AnalyticsSummary Summary { get; set; } = new();
    public List<ClinicTotal> ClinicTotals { get; set; } = new();
    public string ScopeName { get; set; } = "All clinics";
    public bool IsAdmin { get; set; }

    // Screenings done without consent: not stored, so not in any figure on this page
    public int UnsavedCount { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        IsAdmin = User.IsInRole("Admin");

        if (IsAdmin)
        {
            ClinicTotals = await _analytics.GetClinicTotalsAsync();

            // Ignore a clinic number that doesn't exist
            var picked = ClinicTotals.FirstOrDefault(c => c.ClinicId == ClinicId);
            if (picked == null) ClinicId = null;
            ScopeName = picked?.Name ?? "All clinics";
        }
        else
        {
            // Clinicians: always their own clinic, whatever the address bar says
            ClinicId = await _scope.GetClinicIdAsync();
            if (ClinicId == null) return Page();   // not linked to a clinic: shows the empty message
            ScopeName = await _scope.GetClinicNameAsync() ?? "Your clinic";
        }

        Summary = await _analytics.GetSummaryAsync(ClinicId);
        UnsavedCount = await _analytics.CountUnsavedScreeningsAsync(ClinicId);

        // Tells the banner at the top of the page which clinic is being viewed
        ViewData["ViewingClinic"] = ScopeName;
        return Page();
    }
}