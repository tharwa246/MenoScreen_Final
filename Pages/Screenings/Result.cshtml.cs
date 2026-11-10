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
    private readonly ContentService _content;

    public ResultModel(ApplicationDbContext db, DocumentService documents, ContentService content)
    {
        _db = db;
        _documents = documents;
        _content = content;
    }

    public Screening Screening { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
    public int MaxRaw { get; set; }

    // On-screen messages from section 7 of the brief. Clinician-facing, so English only.
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
        var screening = await _db.Screenings
            .Include(s => s.Patient)
            .Include(s => s.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (screening == null) return NotFound();

        Screening = screening;
        MaxRaw = screening.Answers.Count(a => a.Question.Type == QuestionType.Scored) * 3;
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

    // lang is optional. Without it, the patient's preferred language is used.
    public async Task<IActionResult> OnGetHandoutPdfAsync(int id, string? lang)
    {
        var screening = await _db.Screenings
            .Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (screening == null) return NotFound();

        var language = TranslationHelper.Normalize(
            string.IsNullOrWhiteSpace(lang) ? screening.Patient.PreferredLanguage : lang);

        var texts = await _content.GetAllAsync(language);
        var bytes = _documents.BuildPatientHandout(screening, texts);
        return File(bytes, "application/pdf", $"patient-handout-{id}-{language}.pdf");
    }
}