using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// Her answer to "Are you using HRT (hormone replacement therapy)?"
public static class HrtStatuses
{
    public const string No = "No";
    public const string Yes = "Yes";
    public const string Stopped = "Stopped";
    public const string NotSure = "Not sure";
    public static readonly string[] All = { No, Yes, Stopped, NotSure };
}

// ONE WEEKLY CHECK-IN from the patient portal
// Five quick 0-10 sliders, one per symptom area (the same areas as the screening), so she
// and her clinician can see on a chart whether things are getting better or worse.
public class WeeklyCheckIn
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;

    // 0 = no problem at all, 10 = as bad as it gets
    [Range(0, 10)] public int VasomotorSleep { get; set; }
    [Range(0, 10)] public int CognitiveMood { get; set; }
    [Range(0, 10)] public int MusculoskeletalEnergy { get; set; }
    [Range(0, 10)] public int UrogenitalSexual { get; set; }
    [Range(0, 10)] public int QualityOfLife { get; set; }

    [MaxLength(20)]
    public string HrtStatus { get; set; } = HrtStatuses.No;

    // A date only (no time), so it's stored as a plain date in the database
    public DateOnly? HrtStartedOn { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    // She ticked "I'd like my clinician to contact me"
    public bool WantsContact { get; set; }
}