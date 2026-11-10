using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

// THE PATIENT PORTAL'S ACCOUNTS
// 1. Her clinician clicks "Create portal access": we make her a login with a generated
//    username (e.g. nomsa.m4821) and a one-time setup code (e.g. K7P2-QX9M).
// 2. She opens /Portal/Setup, types both, and chooses her own password.
// No password is ever printed or shown to staff, and the code itself is never stored:
// only a one-way "hash" of it, which can check a code but can't be turned back into one.
public class PortalService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly IHttpContextAccessor _http;

    public PortalService(ApplicationDbContext db, UserManager<IdentityUser> users, IHttpContextAccessor http)
    {
        _db = db;
        _users = users;
        _http = http;
    }

    // The signed-in patient's own record, and ONLY hers. Every portal page starts here,
    // so she can never see anyone else's data. Returns null if her consent was withdrawn.
    public async Task<Patient?> GetMyPatientAsync()
    {
        var userId = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;
        return await _db.Patients.FirstOrDefaultAsync(p => p.UserId == userId && p.ConsentGiven);
    }

    // "Create portal access" on her patient page
    public async Task<(string Username, string SetupCode)> CreateAccessAsync(Patient patient)
    {
        if (!patient.ConsentGiven)
            throw new InvalidOperationException("Record her consent before giving her portal access.");
        if (patient.UserId != null)
            throw new InvalidOperationException("She already has portal access.");

        var username = await GenerateUsernameAsync(patient.FullName);
        var user = new IdentityUser { UserName = username };

        // No password yet: she chooses her own on the setup page
        var created = await _users.CreateAsync(user);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join(" ", created.Errors.Select(e => e.Description)));
        await _users.AddToRoleAsync(user, "Patient");

        patient.UserId = user.Id;
        var code = SetNewSetupCode(patient);
        await _db.SaveChangesAsync();
        return (username, code);
    }

    // "New setup code": for a lost code or a forgotten password. The old code stops working.
    public async Task<string> NewSetupCodeAsync(Patient patient)
    {
        if (patient.UserId == null)
            throw new InvalidOperationException("She doesn't have portal access yet.");

        var code = SetNewSetupCode(patient);
        await _db.SaveChangesAsync();
        return code;
    }

    public async Task<string?> GetUsernameAsync(Patient patient) =>
        patient.UserId == null ? null : (await _users.FindByIdAsync(patient.UserId))?.UserName;

    // The setup page: check the username + code, then set the password she chose
    public async Task<(IdentityUser? User, string? Error)> CompleteSetupAsync(string username, string code, string password)
    {
        // The same message for every kind of mistake, so nobody can use this page to find out
        // which usernames exist
        const string mismatch = "That username and setup code don't match, or the code has expired. Ask your clinic for a new code.";

        var user = await _users.FindByNameAsync(username.Trim());
        if (user == null || !await _users.IsInRoleAsync(user, "Patient")) return (null, mismatch);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (patient == null || !patient.ConsentGiven || patient.PortalSetupCodeHash == null
            || patient.PortalSetupCodeExpiresAt == null || patient.PortalSetupCodeExpiresAt < DateTime.UtcNow)
            return (null, mismatch);

        // FixedTimeEquals compares in a way that doesn't leak how many characters were right
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(code)), Encoding.UTF8.GetBytes(patient.PortalSetupCodeHash)))
        {
            // 5 wrong tries cancels the code, so it can't be guessed
            patient.PortalSetupFailedAttempts++;
            if (patient.PortalSetupFailedAttempts >= 5)
            {
                patient.PortalSetupCodeHash = null;
                patient.PortalSetupCodeExpiresAt = null;
            }
            await _db.SaveChangesAsync();
            return (null, mismatch);
        }

        // Check the password meets the rules BEFORE removing any old one
        foreach (var validator in _users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(_users, user, password);
            if (!check.Succeeded)
                return (null, string.Join(" ", check.Errors.Select(e => e.Description)));
        }

        if (await _users.HasPasswordAsync(user))
            await _users.RemovePasswordAsync(user);
        var result = await _users.AddPasswordAsync(user, password);
        if (!result.Succeeded)
            return (null, string.Join(" ", result.Errors.Select(e => e.Description)));

        // The code works once only
        patient.PortalSetupCodeHash = null;
        patient.PortalSetupCodeExpiresAt = null;
        patient.PortalSetupFailedAttempts = 0;
        await _db.SaveChangesAsync();
        await _users.SetLockoutEndDateAsync(user, null);
        return (user, null);
    }

    // e.g. "Nomsa Mabandla" -> "nomsa.m4821" (letters only, plus 4 random digits)
    private async Task<string> GenerateUsernameAsync(string fullName)
    {
        var parts = fullName.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = new string((parts.FirstOrDefault() ?? "").Where(c => c is >= 'a' and <= 'z').ToArray());
        var initial = parts.Length > 1 ? new string(parts[^1].Where(c => c is >= 'a' and <= 'z').Take(1).ToArray()) : "";
        if (first.Length == 0) first = "patient";

        while (true)
        {
            var candidate = $"{first}.{initial}{RandomNumberGenerator.GetInt32(1000, 10000)}";
            if (await _users.FindByNameAsync(candidate) == null) return candidate;
        }
    }

    // Makes a new 8-character code, stores only its hash, and returns it as "ABCD-EFGH"
    private static string SetNewSetupCode(Patient patient)
    {
        // No 0/O or 1/I, so it can't be misread on paper
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[8];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        var code = new string(chars);

        patient.PortalSetupCodeHash = Hash(code);
        patient.PortalSetupCodeExpiresAt = DateTime.UtcNow.AddDays(7);
        patient.PortalSetupFailedAttempts = 0;
        return $"{code[..4]}-{code[4..]}";
    }

    // One-way scramble (SHA-256). Ignores dashes, spaces and capital letters.
    private static string Hash(string code)
    {
        var normal = code.Replace("-", "").Replace(" ", "").ToUpperInvariant();
           return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normal)));
       }

       // ---- HER OWN DATA (pages always pass the id from GetMyPatientAsync, so only hers) ----

       public Task<List<Screening>> GetScreeningsAsync(int patientId) =>
           _db.Screenings
               .Where(s => s.PatientId == patientId)
               .OrderByDescending(s => s.ScreenedAt)
               .ToListAsync();

       // Oldest first, so the chart reads left to right
       public Task<List<WeeklyCheckIn>> GetCheckInsAsync(int patientId) =>
           _db.WeeklyCheckIns
               .Where(c => c.PatientId == patientId)
               .OrderBy(c => c.CheckedInAt)
               .ToListAsync();

       // One check-in a day at most, so a double tap doesn't make two
       public Task<bool> HasCheckedInTodayAsync(int patientId)
       {
           var startOfToday = DateTime.UtcNow.Date;
           return _db.WeeklyCheckIns.AnyAsync(c => c.PatientId == patientId && c.CheckedInAt >= startOfToday);
       }

       public async Task SaveCheckInAsync(int patientId, WeeklyCheckIn checkIn)
       {
           checkIn.PatientId = patientId;
           checkIn.CheckedInAt = DateTime.UtcNow;
           _db.WeeklyCheckIns.Add(checkIn);
           await _db.SaveChangesAsync();
       }
   }