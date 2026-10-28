using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

public class Patient
{
    public int Id { get; set; }

    // Empty until she registers an account in the patient portal (later)
    public string? UserId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    // en, zu, xh, af (used later for the handout language)
    [MaxLength(5)]
    public string PreferredLanguage { get; set; } = "en";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Screening> Screenings { get; set; } = new();
}