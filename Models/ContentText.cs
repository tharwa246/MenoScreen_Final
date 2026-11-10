using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// One piece of interface wording in one language, e.g. Key "ui.Yes", Language "zu"
public class ContentText
{
    public int Id { get; set; }

    [Required, MaxLength(60)]
    public string Key { get; set; } = string.Empty;

    [Required, MaxLength(5)]
    public string Language { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Value { get; set; } = string.Empty;

    public bool IsReviewed { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}