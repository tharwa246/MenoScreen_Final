
namespace MenoScreen.Models;

public class Screening
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    // The Identity user (clinician) who ran this screening
    public string ClinicianId { get; set; } = string.Empty;

    public DateTime ScreenedAt { get; set; } = DateTime.UtcNow;

    public int QuestionSetVersionId { get; set; }
    public QuestionSetVersion QuestionSetVersion { get; set; } = null!;

    public int RawScore { get; set; }

    public int MenoLikelihood { get; set; }
     
    public string Band { get; set; } = string.Empty; // final band: same as ScoreBand, or "Complex" if Q23 = Yes

    //ScoreBand is never “Complex”. It only comes from the 0-100 score.
    public string ScoreBand { get; set; } = string.Empty;  // from the score alone: Unlikely, Possible, Likely, Significant

    public List<Answer> Answers { get; set; } = new();
}
