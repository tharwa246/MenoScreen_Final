using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Doctors;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public record DoctorRow(int Id, string FullName, string Practice, string Location,
                            bool IsSpecialist, bool IsMenopauseTrained);
    public record SlotRow(string DoctorName, DateTime StartsAt, DateTime EndsAt, bool IsBooked);

    public List<DoctorRow> DoctorList { get; set; } = new();
    public List<SlotRow> SlotList { get; set; } = new();

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        DoctorList = await _db.Doctors
            .OrderBy(d => d.FullName)
            .Select(d => new DoctorRow(d.Id, d.FullName, d.Practice, d.City + ", " + d.Province,
                                       d.IsSpecialist, d.IsMenopauseTrained))
            .ToListAsync();

        var now = DateTime.UtcNow;
        SlotList = await _db.AvailabilitySlots
            .Where(s => s.EndsAt > now)
            .OrderBy(s => s.StartsAt)
            .Take(50)
            .Select(s => new SlotRow(s.Doctor.FullName, s.StartsAt, s.EndsAt, s.Appointment != null))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddDoctorAsync(
        string? fullName, string? practice, string? city, string? province,
        bool isSpecialist, bool isMenopauseTrained)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(province))
        {
            Message = "Name, city and province are required.";
            return RedirectToPage();
        }

        _db.Doctors.Add(new Doctor
        {
            FullName = fullName.Trim(),
            Practice = practice?.Trim() ?? "",
            City = city.Trim(),
            Province = province.Trim(),
            IsSpecialist = isSpecialist,
            IsMenopauseTrained = isMenopauseTrained
        });
        await _db.SaveChangesAsync();

        Message = "Doctor added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddSlotAsync(int doctorId, DateTime? startsAt, int minutes)
    {
        if (!await _db.Doctors.AnyAsync(d => d.Id == doctorId))
        {
            Message = "Choose a doctor.";
            return RedirectToPage();
        }
        if (startsAt == null)
        {
            Message = "Choose a start time.";
            return RedirectToPage();
        }
        if (minutes < 10 || minutes > 240)
        {
            Message = "A slot must be between 10 and 240 minutes.";
            return RedirectToPage();
        }

        // The form gives local time, the database stores UTC
        var start = startsAt.Value.ToUniversalTime();
        var end = start.AddMinutes(minutes);

        if (start <= DateTime.UtcNow)
        {
            Message = "The slot must be in the future.";
            return RedirectToPage();
        }

        // Rule: one doctor can't have two overlapping slots
        var overlaps = await _db.AvailabilitySlots
            .AnyAsync(s => s.DoctorId == doctorId && s.StartsAt < end && s.EndsAt > start);
        if (overlaps)
        {
            Message = "That doctor already has a slot overlapping that time.";
            return RedirectToPage();
        }

        _db.AvailabilitySlots.Add(new AvailabilitySlot { DoctorId = doctorId, StartsAt = start, EndsAt = end });
        await _db.SaveChangesAsync();

        Message = "Slot added.";
        return RedirectToPage();
    }
}
