using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}