using System.ComponentModel.DataAnnotations;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages.Portal;

// THE WEEKLY CHECK-IN: five 0-10 sliders, HRT, a note, and "please contact me".
// It's saved against HER record only (the id comes from GetMyPatientAsync, never the form).
public class CheckInModel : PageModel
{
    private readonly PortalService _portal;
    private readonly AuditService _audit;

    public CheckInModel(PortalService portal, AuditService audit)
    {
        _portal = portal;
        _audit = audit;
    }

    [BindProperty]
    public CheckInInput Input { get; set; } = new();

    public bool AlreadyToday { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class CheckInInput
    {
        [Range(0, 10)] public int VasomotorSleep { get; set; }
        [Range(0, 10)] public int CognitiveMood { get; set; }
        [Range(0, 10)] public int MusculoskeletalEnergy { get; set; }
        [Range(0, 10)] public int UrogenitalSexual { get; set; }
        [Range(0, 10)] public int QualityOfLife { get; set; }

        [Required]
        public string HrtStatus { get; set; } = HrtStatuses.No;

        public DateOnly? HrtStartedOn { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public bool WantsContact { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var me = await _portal.GetMyPatientAsync();
        if (me == null) return RedirectToPage("Index");

        AlreadyToday = await _portal.HasCheckedInTodayAsync(me.Id);

        // Start the sliders where she was last time, so she only moves what changed
        var last = (await _portal.GetCheckInsAsync(me.Id)).LastOrDefault();
        if (last != null)
        {
            Input.VasomotorSleep = last.VasomotorSleep;
            Input.CognitiveMood = last.CognitiveMood;
            Input.MusculoskeletalEnergy = last.MusculoskeletalEnergy;
            Input.UrogenitalSexual = last.UrogenitalSexual;
            Input.QualityOfLife = last.QualityOfLife;
            Input.HrtStatus = last.HrtStatus;
            Input.HrtStartedOn = last.HrtStartedOn;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var me = await _portal.GetMyPatientAsync();
        if (me == null) return RedirectToPage("Index");

        if (await _portal.HasCheckedInTodayAsync(me.Id))
        {
            StatusMessage = "You've already checked in today. Thank you! You can check in again tomorrow.";
            return RedirectToPage("Index");
        }

        if (!HrtStatuses.All.Contains(Input.HrtStatus))
            ModelState.AddModelError("Input.HrtStatus", "Choose one of the options.");
        if (Input.HrtStartedOn > DateOnly.FromDateTime(DateTime.Today))
            ModelState.AddModelError("Input.HrtStartedOn", "That date is in the future.");

        if (!ModelState.IsValid) return Page();

        // A start date only makes sense if she is, or was, on HRT
        bool onOrWasOnHrt = Input.HrtStatus is HrtStatuses.Yes or HrtStatuses.Stopped;

        await _portal.SaveCheckInAsync(me.Id, new WeeklyCheckIn
        {
            VasomotorSleep = Input.VasomotorSleep,
            CognitiveMood = Input.CognitiveMood,
            MusculoskeletalEnergy = Input.MusculoskeletalEnergy,
            UrogenitalSexual = Input.UrogenitalSexual,
            QualityOfLife = Input.QualityOfLife,
            HrtStatus = Input.HrtStatus,
            HrtStartedOn = onOrWasOnHrt ? Input.HrtStartedOn : null,
            Note = string.IsNullOrWhiteSpace(Input.Note) ? null : Input.Note.Trim(),
            WantsContact = Input.WantsContact
        });
        await _audit.LogAsync("PortalCheckIn", "Patient", me.Id, Input.WantsContact ? "Asked to be contacted" : null);

        StatusMessage = Input.WantsContact
            ? "Thank you for checking in. Your clinic will see that you'd like them to contact you."
            : "Thank you for checking in. See you next week.";
        return RedirectToPage("Index");
    }
}