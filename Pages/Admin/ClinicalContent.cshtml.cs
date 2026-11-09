using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// MANAGE THE CLINICAL CONTENT (admins only: the /Admin folder needs AdminOnly)
//  1. Band wording: the screen message, clinician summary and patient handout per band
//  2. Guideline sections the clinician assistant answers from, with clinical sign-off
//  3. The suggested questions shown as buttons above the assistant
// Every change is written to the audit log.
public class ClinicalContentModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ContentService _content;
    private readonly AuditService _audit;

    public ClinicalContentModel(ApplicationDbContext db, ContentService content, AuditService audit)
    {
        _db = db;
        _content = content;
        _audit = audit;
    }

    public List<BandContent> Bands { get; set; } = new();
    public List<GuidelineSection> Sections { get; set; } = new();
    public List<AssistantPrompt> Prompts { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    // =============== 1. BAND WORDING ===============

    public async Task<IActionResult> OnPostSaveBandAsync(int id, string? clinicianMessage, string? clinicianSummary, string? patientHandout)
    {
        var row = await _db.BandContents.FindAsync(id);
        if (row == null) return NotFound();

        clinicianMessage = Clean(clinicianMessage);
        clinicianSummary = Clean(clinicianSummary);
        patientHandout = Clean(patientHandout);

        if (clinicianMessage == null || clinicianMessage.Length > 600 ||
            clinicianSummary == null || clinicianSummary.Length > 600 ||
            patientHandout == null || patientHandout.Length > 800)
        {
            StatusMessage = $"{row.Band}: all three texts are needed (screen message and summary up to 600 characters, handout up to 800).";
            return RedirectToPage();
        }

        row.ClinicianMessage = clinicianMessage;
        row.ClinicianSummary = clinicianSummary;
        row.PatientHandout = patientHandout;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = User.Identity?.Name;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UpdateBandContent", "BandContent", id, row.Band);

        StatusMessage = $"{row.Band} wording saved. The Result page, PDFs and \"My health\" use it straight away.";
        return RedirectToPage();
    }

    // =============== 2. GUIDELINE SECTIONS ===============

    public async Task<IActionResult> OnPostAddSectionAsync(string? heading, string? body)
    {
        heading = Clean(heading);
        body = Clean(body);
        if (heading == null || heading.Length > 120 || body == null || body.Length > 4000)
        {
            StatusMessage = "A section needs a heading (up to 120 characters) and text (up to 4,000).";
            return RedirectToPage();
        }

        // New sections go to the end
        var lastOrder = await _db.GuidelineSections.MaxAsync(g => (int?)g.SortOrder) ?? 0;
        var section = new GuidelineSection { Heading = heading, Body = body, SortOrder = lastOrder + 10 };
        _db.GuidelineSections.Add(section);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreateGuideline", "GuidelineSection", section.Id, heading);

        StatusMessage = "Section added. It is not approved yet.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveSectionAsync(int id, string? heading, string? body, int sortOrder,
                                                            bool isActive, string? approvedBy, DateOnly? approvedOn)
    {
        var section = await _db.GuidelineSections.FindAsync(id);
        if (section == null) return NotFound();

        heading = Clean(heading);
        body = Clean(body);
        approvedBy = Clean(approvedBy);
        if (heading == null || heading.Length > 120 || body == null || body.Length > 4000 || approvedBy?.Length > 120)
        {
            StatusMessage = "A section needs a heading (up to 120 characters) and text (up to 4,000); \"Approved by\" is up to 120.";
            return RedirectToPage();
        }

        section.Heading = heading;
        section.Body = body;
        section.SortOrder = sortOrder;
        section.IsActive = isActive;

        // Sign-off: a name means approved. If no date was picked, today is used.
        // No name means not approved, so the date is cleared too.
        section.ApprovedBy = approvedBy;
        section.ApprovedOn = approvedBy == null ? null : approvedOn ?? DateOnly.FromDateTime(DateTime.Today);

        await _db.SaveChangesAsync();
        await _audit.LogAsync("UpdateGuideline", "GuidelineSection", id,
            $"{(isActive ? "Shown" : "Hidden")}, {(approvedBy == null ? "not approved" : "approved by " + approvedBy)}");

        StatusMessage = "Section saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSectionAsync(int id)
    {
        var section = await _db.GuidelineSections.FindAsync(id);
        if (section == null) return NotFound();

        _db.GuidelineSections.Remove(section);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("DeleteGuideline", "GuidelineSection", id, section.Heading);

        StatusMessage = "Section deleted.";
        return RedirectToPage();
    }

    // =============== 3. SUGGESTED QUESTIONS ===============

    public async Task<IActionResult> OnPostAddPromptAsync(string? text)
    {
        text = Clean(text);
        if (text == null || text.Length > 200)
        {
            StatusMessage = "A suggested question needs some text, up to 200 characters.";
            return RedirectToPage();
        }

        var lastOrder = await _db.AssistantPrompts.MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var prompt = new AssistantPrompt { Text = text, SortOrder = lastOrder + 10 };
        _db.AssistantPrompts.Add(prompt);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreatePrompt", "AssistantPrompt", prompt.Id);

        StatusMessage = "Suggested question added.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSavePromptAsync(int id, string? text, int sortOrder, bool isActive)
    {
        var prompt = await _db.AssistantPrompts.FindAsync(id);
        if (prompt == null) return NotFound();

        text = Clean(text);
        if (text == null || text.Length > 200)
        {
            StatusMessage = "A suggested question needs some text, up to 200 characters.";
            return RedirectToPage();
        }

        prompt.Text = text;
        prompt.SortOrder = sortOrder;
        prompt.IsActive = isActive;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UpdatePrompt", "AssistantPrompt", id, isActive ? "Shown" : "Hidden");

        StatusMessage = "Suggested question saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeletePromptAsync(int id)
    {
        var prompt = await _db.AssistantPrompts.FindAsync(id);
        if (prompt == null) return NotFound();

        _db.AssistantPrompts.Remove(prompt);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("DeletePrompt", "AssistantPrompt", id);

        StatusMessage = "Suggested question deleted.";
        return RedirectToPage();
    }

    // =============== HELPERS ===============

    private async Task LoadAsync()
    {
        Bands = await _content.GetAllBandsAsync();
        Sections = await _db.GuidelineSections.OrderBy(g => g.SortOrder).ThenBy(g => g.Id).ToListAsync();
        Prompts = await _db.AssistantPrompts.OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
    }

    // Blank or only spaces counts as "nothing"; otherwise trim the spaces off the ends
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}