using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

public class QuestionTranslation
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    // zu, xh or af. English lives on the Question itself.
    [Required, MaxLength(5)]
    public string Language { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Block { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    // Same choices, in the same order, as Question.Options, separated by |
    [MaxLength(500)]
    public string? Options { get; set; }

    // Has a fluent speaker approved this wording? Azure drafts start as false.
    public bool IsReviewed { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}