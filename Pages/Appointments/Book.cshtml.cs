using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Appointments;

public class BookModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly BookingService _booking;

    public BookModel(ApplicationDbContext db, BookingService booking)
    {
        _db = db;
        _booking = booking;
    }

    public Patient Patient { get; set; } = null!;
    public List<AvailabilitySlot> Slots { get; set; } = new();
    public bool SpecialistsOnly { get; set; }

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(int patientId)
    {
        if (!await LoadAsync(patientId)) return NotFound();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int patientId, int slotId, string? note)
    {
        if (slotId == 0)
        {
            Message = "Choose a time slot first.";
            return RedirectToPage(new { patientId });
        }

        try
        {
            var clinicianId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await _booking.BookAsync(slotId, patientId, clinicianId, note);
            return RedirectToPage("/Appointments/Index");
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
            return RedirectToPage(new { patientId });
        }
    }

    private async Task<bool> LoadAsync(int patientId)
    {
        var patient = await _db.Patients.FindAsync(patientId);
        if (patient == null) return false;

        Patient = patient;
        Slots = await _booking.GetOpenSlotsAsync(patientId);

        // Only used to show a notice. The real rule is enforced in BookingService.
        var latestBand = await _db.Screenings
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.ScreenedAt)
            .Select(s => s.Band)
            .FirstOrDefaultAsync();
        SpecialistsOnly = latestBand == "Complex";

        return true;
    }
}