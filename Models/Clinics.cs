using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// A CLINIC (one practice or consulting site).
// Every patient belongs to one clinic, and every clinician works at one clinic.
// A clinician only ever sees her own clinic's patients (POPIA: least access).
public class Clinic
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Suburb or town, e.g. "Gugulethu"
    [MaxLength(100)]
    public string? Area { get; set; }

    public List<Patient> Patients { get; set; } = new();
}