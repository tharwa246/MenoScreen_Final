namespace MenoScreen.Models;
public class QuestionSetVersion
{
    public int Id {get;set;}
    public int VersionNumber {get;set;}
    public bool IsActive {get;set;}
    public string? Notes {get;set;}
    // Default to "now" in UTC. Without this the date is unset, which Postgres rejects.
    public DateTime CreatedAt {get;set;} = DateTime.UtcNow;
    public List<Question> Questions { get; set; } = new();
}
