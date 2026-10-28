using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// ONE ROW = ONE QR CODE. The clinician creates it; the patient scans it to answer the
// screening on her own phone (e.g. in the waiting room).
// The link only contains the random Token, never her name, score or band.
public class ScreeningInvite
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Token { get; set; } = string.Empty;

    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    // The clinician who issued the invite; the finished screening is filed under them
    public string ClinicianId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Set once the patient submits; a used invite can never be reused
    public DateTime? UsedAt { get; set; }
    public int? ScreeningId { get; set; }

    // A QR link works only if nobody has used it yet AND it hasn't expired.
    public bool IsUsable(DateTime nowUtc) => UsedAt == null && nowUtc < ExpiresAt;
}
