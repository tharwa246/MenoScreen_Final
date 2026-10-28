
namespace MenoScreen.Models;

public static class ScreeningSources
{
    public const string Clinician = "Clinician";   // clinician read the questions aloud
    public const string SelfScreen = "SelfScreen"; // patient answered on her own phone via QR code
}

public class Screening
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    // The Identity user (clinician) who ran this screening, or who issued the QR invite
    public string ClinicianId { get; set; } = string.Empty;

    public DateTime ScreenedAt { get; set; } = DateTime.UtcNow;

    public int QuestionSetVersionId { get; set; }
    public QuestionSetVersion QuestionSetVersion { get; set; } = null!;

    public int RawScore { get; set; }

    public int MenoLikelihood { get; set; }

    public string Band { get; set; } = string.Empty; // final band: same as ScoreBand, or "Complex" if Q23 = Yes

    //ScoreBand is never “Complex”. It only comes from the 0-100 score.
    public string ScoreBand { get; set; } = string.Empty;  // from the score alone: Unlikely, Possible, Likely, Significant

    // How the answers were collected (see ScreeningSources)
    public string Source { get; set; } = ScreeningSources.Clinician;

    public List<Answer> Answers { get; set; } = new();
}
