using System.Security.Cryptography;
using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace MenoScreen.Services;

// QR SELF-SCREENING LINKS
// The clinician creates an "invite" for a patient. The invite has a long random code (token).
// The link /SelfScreen/<token> is turned into a QR code she scans with her phone camera.
// Rules: the link works ONCE, expires after 24 hours, and contains no name or results,
// so a photo of the QR code reveals nothing about her.
public class InviteService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public InviteService(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<ScreeningInvite> CreateAsync(int patientId, string clinicianId)
    {
        var patient = await _db.Patients.FindAsync(patientId)
            ?? throw new InvalidOperationException("Patient not found.");

        if (!patient.ConsentGiven)
            throw new InvalidOperationException("Record the patient's consent before sending her a screening link.");

        int hours = _config.GetValue("SelfScreen:InviteLifetimeHours", 24);

        var invite = new ScreeningInvite
        {
            // The token: 32 random bytes from a cryptographically secure generator, written as
            // 64 letters/digits. There are so many possible values that guessing one is impossible,
            // so knowing the link is enough proof she's the right person (she has no login).
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            PatientId = patientId,
            ClinicianId = clinicianId,
            ExpiresAt = DateTime.UtcNow.AddHours(hours)
        };

        _db.ScreeningInvites.Add(invite);
        await _db.SaveChangesAsync();
        return invite;
    }

    // Checks a scanned link. Returns the invite only if the token exists, hasn't expired
    // and hasn't been used yet. Otherwise returns null and the page says "link expired".
    public async Task<ScreeningInvite?> FindUsableAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return null;

        var invite = await _db.ScreeningInvites
            .Include(i => i.Patient)
            .FirstOrDefaultAsync(i => i.Token == token);

        return invite != null && invite.IsUsable(DateTime.UtcNow) ? invite : null;
    }

    // Turns the link into a QR code picture (using the QRCoder package).
    // We return it as text ("data:image/png;base64,...") that can go straight into
    // an <img src="...">, so we never have to save image files to disk.
    public static string QrDataUri(string url)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        byte[] png = new PngByteQRCode(data).GetGraphic(10);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
