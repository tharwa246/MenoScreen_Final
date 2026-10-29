namespace MenoScreen.Models;
public class Answer
{
    public int Id { get; set; }
    //give each answer record its own unique id, so that we can track changes to answers over time. 
    //This is important for safety flag questions,
    //where a clinician may want to see the history of answers for a patient.

    public int ScreeningId { get; set; }
    //stores the ID of the screening that this answer belongs to.

    public Screening Screening { get; set; } = null!;
    // Connects this answer to the actual screening record.
    // ScreeningId stores the ID, while Screening provides a reference to the screening.

    public int QuestionId { get; set; }
    // Stores the ID of the question that was answered.

    public Question Question { get; set; } = null!;
    // Connects this answer to the actual question.
    // This lets the application access information about the question.

    public int? Value { get; set; }         
    // 0-3 for scored, 0/1 for safety flag
    
    public string? TextValue { get; set; }   
    // context answers
}