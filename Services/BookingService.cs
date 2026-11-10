using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public record DoctorAppointmentRow(int Id, string PatientName, DateTime StartsAt, DateTime EndsAt,
                                   string? Note, AppointmentStatus Status, bool CanRecordOutcome);
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

    // Only Booked appointments can be cancelled. Attended and no-show records are history.
    public async Task CancelAsync(int appointmentId)
    {
        var appointment = await _db.Appointments.FindAsync(appointmentId)
            ?? throw new InvalidOperationException("Appointment not found.");

        if (appointment.Status != AppointmentStatus.Booked)
            throw new InvalidOperationException("Only booked appointments can be cancelled.");

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();
    }

    public Task MarkAttendedAsync(int appointmentId, string doctorUserId) =>
        SetOutcomeAsync(appointmentId, doctorUserId, AppointmentStatus.Attended);

    public Task MarkNoShowAsync(int appointmentId, string doctorUserId) =>
        SetOutcomeAsync(appointmentId, doctorUserId, AppointmentStatus.NoShow);

    // the method both of the above call
    private async Task SetOutcomeAsync(int appointmentId, string doctorUserId, AppointmentStatus outcome)
    {
        var appointment = await _db.Appointments
            .Include(a => a.AvailabilitySlot)
                .ThenInclude(s => s.Doctor)
            .FirstOrDefaultAsync(a => a.Id == appointmentId)
            ?? throw new InvalidOperationException("Appointment not found.");

        // A doctor can only change her own appointments
        if (appointment.AvailabilitySlot.Doctor.UserId != doctorUserId)
            throw new UnauthorizedAccessException("This appointment belongs to another doctor.");

        if (appointment.Status != AppointmentStatus.Booked)
            throw new InvalidOperationException("The outcome has already been recorded.");

        if (appointment.AvailabilitySlot.StartsAt > DateTime.UtcNow)
            throw new InvalidOperationException("You can't record an outcome before the appointment time.");

        appointment.Status = outcome;
        appointment.StatusChangedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    
    // Only this doctor's own appointments. The filter is in the query, so another doctor's
    // appointments are never loaded.
    public async Task<List<DoctorAppointmentRow>> GetDoctorAppointmentsAsync(string? doctorUserId)
    {
        // Without this check, a blank id would match every doctor who has no login yet
        if (string.IsNullOrWhiteSpace(doctorUserId))
            return new List<DoctorAppointmentRow>();

        var now = DateTime.UtcNow;

        return await _db.Appointments
            .Where(a => a.AvailabilitySlot.Doctor.UserId == doctorUserId)
            .OrderBy(a => a.AvailabilitySlot.StartsAt)
            .Select(a => new DoctorAppointmentRow(
                a.Id,
                a.Patient.FullName,
                a.AvailabilitySlot.StartsAt,
                a.AvailabilitySlot.EndsAt,
                a.Note,
                a.Status,
                a.Status == AppointmentStatus.Booked && a.AvailabilitySlot.StartsAt <= now))
            .ToListAsync();
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