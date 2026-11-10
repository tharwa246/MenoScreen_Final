using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// THE WORDING FOR ONE BAND (Unlikely, Possible, Likely, Significant, Complex).
// One row per band, used in several places, so changing it here changes it everywhere:
//  * ClinicianMessage: the message on the Result page (and the "screen without saving" page)
//  * ClinicianSummary: the summary on the clinician PDF
//  * PatientHandout:   the paragraph on her page of the PDF and on "My health"
// The band NAMES stay in code (ScoringService.Bands): scoring and analytics depend on them.
public class BandContent
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Band { get; set; } = string.Empty;

    [Required, MaxLength(600)]
    public string ClinicianMessage { get; set; } = string.Empty;

    [Required, MaxLength(600)]
    public string ClinicianSummary { get; set; } = string.Empty;

    [Required, MaxLength(800)]
    public string PatientHandout { get; set; } = string.Empty;

    // Who changed it last, and when (shown on the admin page)
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}

// ONE SECTION OF THE APPROVED GUIDELINES that the clinician assistant answers from.
// Replaces the old Guidelines/assistant-guidelines.md file. Only shown sections are sent.
public class GuidelineSection
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Heading { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    // Lower numbers come first
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Who signed it off clinically, and when. Empty = not approved yet.
    [MaxLength(120)]
    public string? ApprovedBy { get; set; }

    public DateOnly? ApprovedOn { get; set; }
}

// ONE SUGGESTED QUESTION, shown as a one-tap button under "Ask the clinician assistant"
public class AssistantPrompt
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Text { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}