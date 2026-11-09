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

    [MaxLength(60)]
    public string Block { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Text { get; set; } = string.Empty;

    // Same choices in the same order as Question.Options, e.g. "Ja|Nee|Nie seker nie"
    [MaxLength(300)]
    public string? Options { get; set; }
}