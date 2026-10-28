using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;

namespace MenoScreen.Services;

// THE AUDIT LOG (POPIA accountability)
// Every time someone views a result, downloads a PDF, records consent or deletes a
// patient, we write one line: who, what, which record, and when.
// Admins can read the log on the Admin page. The app never edits or deletes these lines.
// Call it from any page with: await _audit.LogAsync("ViewResult", "Screening", id);
public class AuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public async Task LogAsync(string action, string entityType, int? entityId, string? details = null)
    {
        // Who is doing this? If nobody is signed in (a patient on the QR page),
        // UserId stays empty and the Admin page shows "Patient (QR link)".
        var user = _http.HttpContext?.User;
        bool signedIn = user?.Identity?.IsAuthenticated == true;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = signedIn ? user!.FindFirstValue(ClaimTypes.NameIdentifier) : null,
            UserName = signedIn ? user!.Identity!.Name : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details is { Length: > 500 } ? details[..500] : details
        });
        await _db.SaveChangesAsync();
    }
}
