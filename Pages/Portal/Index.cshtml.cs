using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Portal;

// "MY HEALTH": the patient's home. Everything comes from GetMyPatientAsync, so only her data.
public class IndexModel : PageModel
{
    private readonly PortalService _portal;

    public IndexModel(PortalService portal) => _portal = portal;

    public Patient? Me { get; set; }
    public List<Screening> Screenings { get; set; } = new();
    public List<WeeklyCheckIn> CheckIns { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string FirstName => Me?.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    public Screening? Latest => Screenings.FirstOrDefault();
    public string LatestMessage => Latest == null ? "" : DocumentService.HandoutText.GetValueOrDefault(Latest.Band, "");

    // A check-in is due if she has never done one, or her last one was 7 or more days ago
    public bool CheckInDue => !CheckIns.Any() || CheckIns[^1].CheckedInAt <= DateTime.UtcNow.AddDays(-7);
    public DateTime? NextCheckIn => CheckIns.Any() ? CheckIns[^1].CheckedInAt.AddDays(7) : null;

    public async Task OnGetAsync()
    {
        Me = await _portal.GetMyPatientAsync();
        if (Me == null) return;

        Screenings = await _portal.GetScreeningsAsync(Me.Id);
        CheckIns = await _portal.GetCheckInsAsync(Me.Id);
    }
}