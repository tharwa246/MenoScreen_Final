namespace MenoScreen.Models;

public class AvailabilitySlot
{
    public int Id { get; set; }

    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    public DateTime StartsAt { get; set; }   // stored in UTC
    public DateTime EndsAt { get; set; }

    // Null means the slot is still free
    public Appointment? Appointment { get; set; }
}
