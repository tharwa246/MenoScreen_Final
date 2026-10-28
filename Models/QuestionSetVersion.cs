namespace MenoScreen.Models;
public class QuestionSetVersion
{
    public int Id {get;set;}
    public int VersionNumber {get;set;}
    public bool IsActive {get;set;}
    public string? Notes {get;set;}
    public DateTime CreatedAt {get;set;}
    public List<Question> Questions { get; set; } = new();
}