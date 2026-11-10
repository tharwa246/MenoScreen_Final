using System.Security.Claims;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.MyAppointments;

public class IndexModel : PageModel
{
    private readonly BookingService _booking;

    public IndexModel(BookingService booking)
    {
        _booking = booking;
    }

    public List<DoctorAppointmentRow> Rows { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Rows = await _booking.GetDoctorAppointmentsAsync(CurrentUserId());
    }

    public Task<IActionResult> OnPostAttendedAsync(int id) => RecordAsync(id, attended: true);

    public Task<IActionResult> OnPostNoShowAsync(int id) => RecordAsync(id, attended: false);

    private async Task<IActionResult> RecordAsync(int id, bool attended)
    {
        try
        {
            // The service refuses another doctor's appointment, even if someone edits the form
            if (attended) await _booking.MarkAttendedAsync(id, CurrentUserId());
            else await _booking.MarkNoShowAsync(id, CurrentUserId());

            Message = "Saved.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            Message = ex.Message;
        }
        return RedirectToPage();
    }

    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}