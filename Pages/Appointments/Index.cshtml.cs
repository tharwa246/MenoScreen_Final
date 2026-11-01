using MenoScreen.Data;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Appointments;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BookingService _booking;

    public IndexModel(ApplicationDbContext db, BookingService booking)
    {
        _db = db;
        _booking = booking;
    }

    public record Row(int Id, string PatientName, string DoctorName, string Practice,
                      DateTime StartsAt, string? Note);

    public List<Row> Rows { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Rows = await _db.Appointments
            .OrderBy(a => a.AvailabilitySlot.StartsAt)
            .Select(a => new Row(a.Id, a.Patient.FullName, a.AvailabilitySlot.Doctor.FullName,
                                 a.AvailabilitySlot.Doctor.Practice, a.AvailabilitySlot.StartsAt, a.Note))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        try
        {
            await _booking.CancelAsync(id);
            Message = "Appointment cancelled. The slot is free again.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        return RedirectToPage();
    }
}