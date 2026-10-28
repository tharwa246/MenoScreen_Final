namespace MenoScreen.Models;
public class Answer
{
    public int Id { get; set; }
    public int ScreeningId { get; set; }
    public Screening Screening { get; set; } = null!;

    public int QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    public int? Value { get; set; }          // 0-3 for scored, 0/1 for safety flag
    public string? TextValue { get; set; }   // context answers
}