using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// POPIA accountability: a record of who viewed, created or exported patient health
// information, and when. Entries are only ever added, never edited or deleted by the app.
public class AuditLog
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    // Null for patient self-screening (no signed-in user)
    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    [Required, MaxLength(64)]
    public string Action { get; set; } = string.Empty;   // e.g. "ViewResult", "ExportClinicianPdf"

    [Required, MaxLength(64)]
    public string EntityType { get; set; } = string.Empty; // e.g. "Screening", "Patient"

    public int? EntityId { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }
}
