using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public class BookingService
{
    private readonly ApplicationDbContext _db;

    public BookingService(ApplicationDbContext db)
    {
        _db = db;
    }

    // Free, future slots. If the patient's latest result is Complex,
    // only specialists are offered (rule from the clinical brief).
    public async Task<List<AvailabilitySlot>> GetOpenSlotsAsync(int patientId)
    {
        var specialistsOnly = await IsComplexAsync(patientId);
        var now = DateTime.UtcNow;

        return await _db.AvailabilitySlots
            .Include(s => s.Doctor)
            .Where(s => s.StartsAt > now && s.Appointment == null)
            .Where(s => !specialistsOnly || s.Doctor.IsSpecialist)
            .OrderBy(s => s.StartsAt)
            .ToListAsync();
    }

    public async Task<Appointment> BookAsync(int slotId, int patientId, string bookedById, string? note)
    {
        var slot = await _db.AvailabilitySlots
            .Include(s => s.Doctor)
            .Include(s => s.Appointment)
            .FirstOrDefaultAsync(s => s.Id == slotId)
            ?? throw new InvalidOperationException("Slot not found.");

        if (slot.StartsAt <= DateTime.UtcNow)
            throw new InvalidOperationException("That slot is in the past.");

        if (slot.Appointment != null)
            throw new InvalidOperationException("That slot is already booked.");

        if (!await _db.Patients.AnyAsync(p => p.Id == patientId))
            throw new InvalidOperationException("Patient not found.");

        if (!slot.Doctor.IsSpecialist && await IsComplexAsync(patientId))
            throw new InvalidOperationException("This patient needs a specialist before any treatment discussion.");

        var appointment = new Appointment
        {
            AvailabilitySlotId = slotId,
            PatientId = patientId,
            BookedById = bookedById,
            Note = note?.Trim()
        };
        _db.Appointments.Add(appointment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index on AvailabilitySlotId fired: someone booked it a moment ago
            throw new InvalidOperationException("That slot was just booked by someone else.");
        }

        return appointment;
    }

    // Cancelling deletes the appointment, which frees the slot
    public async Task CancelAsync(int appointmentId)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId)
            ?? throw new InvalidOperationException("Appointment not found.");

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();
    }

    private async Task<bool> IsComplexAsync(int patientId)
    {
        var latestBand = await _db.Screenings
            .Where(s => s.PatientId == patientId)
            .OrderByDescending(s => s.ScreenedAt)
            .Select(s => s.Band)
            .FirstOrDefaultAsync();

        return latestBand == "Complex";
    }
}