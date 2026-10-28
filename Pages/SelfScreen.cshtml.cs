using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MenoScreen.Pages;

// THE PATIENT'S QR PAGE (address: /SelfScreen/<token>)
// This is the only page anyone can open without signing in. Instead of a login, the long
// random token in the address proves she was invited (see InviteService).
// The page has three states: Ready (show the questions), Invalid (link used/expired),
// and Done (thank you). She never sees her score here: her clinician goes through
// the result with her, so a worrying number is never shown without support.
public class SelfScreenModel : PageModel
{
    private readonly InviteService _invites;
    private readonly ScreeningService _screenings;
    private readonly AuditService _audit;

    public SelfScreenModel(InviteService invites, ScreeningService screenings, AuditService audit)
    {
        _invites = invites;
        _screenings = screenings;
        _audit = audit;
    }

    public enum State { Ready, Invalid, Done }
    public State PageState { get; set; } = State.Ready;

    public string FirstName { get; set; } = string.Empty;
    public List<Question> Questions { get; set; } = new();

    [BindProperty]
    public Dictionary<int, int> Answers { get; set; } = new();

    [BindProperty]
    public Dictionary<int, string> TextAnswers { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string token)
    {
        var invite = await _invites.FindUsableAsync(token);
        if (invite == null)
        {
            PageState = State.Invalid;
            return Page();
        }

        await LoadAsync(invite);
        return Page();
    }

    // Runs when she presses "Send my answers". We check the token AGAIN, because the
    // link could have expired or been used on another phone while she was answering.
    public async Task<IActionResult> OnPostAsync(string token)
    {
        var invite = await _invites.FindUsableAsync(token);
        if (invite == null)
        {
            PageState = State.Invalid;
            return Page();
        }

        try
        {
            var screening = await _screenings.SaveSelfScreeningAsync(invite, Answers, TextAnswers);
            await _audit.LogAsync("SelfScreenSubmitted", "Screening", screening.Id, $"Invite {invite.Id}");
            PageState = State.Done;
            FirstName = FirstNameOf(invite.Patient.FullName);
            return Page();
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
            await LoadAsync(invite);
            return Page();
        }
    }

    private async Task LoadAsync(ScreeningInvite invite)
    {
        // First name only on screen, in case someone looks over her shoulder
        FirstName = FirstNameOf(invite.Patient.FullName);
        Questions = await _screenings.GetActiveQuestionsAsync();
    }

    private static string FirstNameOf(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
}
