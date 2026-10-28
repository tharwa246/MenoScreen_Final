using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Analytics;

// Clinic-level, anonymous figures. Protected by the "StaffOnly" policy in Program.cs.
public class IndexModel : PageModel
{
    private readonly AnalyticsService _analytics;

    public IndexModel(AnalyticsService analytics) => _analytics = analytics;

    public AnalyticsSummary Summary { get; set; } = new();

    public async Task OnGetAsync()
    {
        Summary = await _analytics.GetSummaryAsync();
    }
}
