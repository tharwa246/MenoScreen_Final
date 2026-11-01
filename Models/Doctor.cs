using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

public class Doctor
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Practice { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(60)]
    public string Province { get; set; } = string.Empty;

    // Languages spoken, e.g. "en,zu,af"
    [MaxLength(30)]
    public string Languages { get; set; } = "en";

    public bool IsMenopauseTrained { get; set; }

    // Complex-flag patients should only be offered specialists
    public bool IsSpecialist { get; set; }

    public List<AvailabilitySlot> Slots { get; set; } = new();
}