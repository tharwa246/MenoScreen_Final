using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Screenings;

public class ResultModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly DocumentService _documents;

    public ResultModel(ApplicationDbContext db, DocumentService documents)
    {
        _db = db;
        _documents = documents;
    }

    public Screening Screening { get; set; } = null!;
    public string Message { get; set; } = string.Empty;

    // NEW: the maximum raw score, worked out from the questions actually answered
    public int MaxRaw { get; set; }

    // On-screen messages from section 7 of the brief.
    // Temporary: later these move into the database so the medical team can edit them.
    private static readonly Dictionary<string, string> Messages = new()
    {
        ["Unlikely"] = "Her symptom profile does not strongly suggest hormonal change at this time. Reassure her and invite her to return if symptoms persist or worsen.",
        ["Possible"] = "Her symptom profile is consistent with early perimenopause. Begin the menopause conversation. Consider lifestyle support or referral.",
        ["Likely"] = "Her symptom profile is strongly consistent with peri/menopause. Discuss HRT options or refer to a menopause-trained GP or gynaecologist.",
        ["Significant"] = "She is carrying a significant symptom burden. Urgent referral is recommended. Please use warm, validating language in your conversation and in her summary.",
        ["Complex"] = "This patient has existing health conditions that require specialist input before any treatment is considered. Please refer before discussing HRT."
    };

    public async Task<IActionResult> OnGetAsync(int id)
    {
        // CHANGED: also load the answers and their questions, to count the scored ones
        var screening = await _db.Screenings
            .Include(s => s.Patient)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (screening == null) return NotFound();

        Screening = screening;
        MaxRaw = screening.Answers.Count(a => a.Question.Type == QuestionType.Scored) * 3;   // NEW
        Message = Messages.GetValueOrDefault(screening.Band, string.Empty);
        return Page();
    }

    public async Task<IActionResult> OnGetClinicianPdfAsync(int id)
    {
        var screening = await _db.Screenings
            .Include(s => s.Patient)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (screening == null) return NotFound();

        var bytes = _documents.BuildClinicianSummary(screening);
        return File(bytes, "application/pdf", $"clinician-summary-{id}.pdf");
    }

    public async Task<IActionResult> OnGetHandoutPdfAsync(int id)
    {
        var screening = await _db.Screenings
            .Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (screening == null) return NotFound();

        var bytes = _documents.BuildPatientHandout(screening);
        return File(bytes, "application/pdf", $"patient-handout-{id}.pdf");
    }
}