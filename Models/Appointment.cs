using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;
//after an appointment time has passed, the doctor marks it as Attended or No-show
//Doctors only see and change their own appointments.
//Once an outcome is recorded, it can’t be cancelled or changed, so the history stays.

public enum AppointmentStatus
{
    Booked,
    Attended,
    NoShow
}
public class Appointment
{
    public int Id { get; set; }

    public int AvailabilitySlotId { get; set; }
    public AvailabilitySlot AvailabilitySlot { get; set; } = null!;

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    // The Identity user (clinician) who made the booking
    public string BookedById { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Note { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Booked;
    public DateTime? StatusChangedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}