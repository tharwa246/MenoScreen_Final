using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// ONE SELF-CARE TIP shown on the patient portal ("Ideas that may help").
// Stored in the database so an admin can add, edit, hide or reorder tips without a programmer.
// Area matches the question blocks exactly (e.g. "Vasomotor & Sleep"), plus "General"
// for tips everyone sees. General self-care only: no diagnosis, no medicines or doses.
public class Suggestion
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Area { get; set; } = SuggestionAreas.General;

    [Required, MaxLength(300)]
    public string Text { get; set; } = string.Empty;

    // Lower numbers show first
    public int SortOrder { get; set; }

    // Hidden tips stay in the database, but patients don't see them
    public bool IsActive { get; set; } = true;
}

// The symptom areas a tip can belong to, and the friendly heading the patient sees for each.
// The keys must match the question blocks in QuestionSeedData exactly.
public static class SuggestionAreas
{
    public const string General = "General";

    public static readonly Dictionary<string, string> Titles = new()
    {
        ["Vasomotor & Sleep"] = "Hot flushes and sleep",
        ["Cognitive & Mood"] = "Mood, memory and concentration",
        ["Musculoskeletal & Energy"] = "Joints, energy and headaches",
        ["Urogenital & Sexual Health"] = "Bladder, vaginal comfort and intimacy",
        ["Quality of Life Impact"] = "Work, relationships and daily life",
        [General] = "For everyone"
    };
}