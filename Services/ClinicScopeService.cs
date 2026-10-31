using System.Security.Claims;
using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

// WHICH PATIENTS MAY THIS PERSON SEE?
// Every page that lists or opens patients asks this class instead of using _db.Patients
// directly. A clinician gets only her own clinic's patients. If she types another
// clinic's patient number into the address bar, the page simply says "Not found".
public class ClinicScopeService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;

    // Remembered for the rest of this web request, so we only look it up once
    private int? _clinicId;
    private bool _loaded;

    public ClinicScopeService(ApplicationDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    // The signed-in staff member's clinic, or null (admins, or a login with no profile yet)
    public async Task<int?> GetClinicIdAsync()
    {
        if (_loaded) return _clinicId;

        var userId = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        _clinicId = userId == null
            ? null
            : await _db.StaffProfiles
                .Where(s => s.UserId == userId)
                .Select(s => s.ClinicId)
                .FirstOrDefaultAsync();
        _loaded = true;
        return _clinicId;
    }

    public async Task<string?> GetClinicNameAsync()
    {
        var clinicId = await GetClinicIdAsync();
        if (clinicId == null) return null;
        return await _db.Clinics.Where(c => c.Id == clinicId).Select(c => c.Name).FirstOrDefaultAsync();
    }

    // The patients this person may see. A clinician with no clinic sees nobody
    // (safer than everybody) until an admin assigns her to one.
    public async Task<IQueryable<Patient>> PatientsAsync()
    {
        var clinicId = await GetClinicIdAsync();
        return clinicId == null
            ? _db.Patients.Where(p => false)
            : _db.Patients.Where(p => p.ClinicId == clinicId);
    }

    // One patient, but only if she belongs to this person's clinic
    public async Task<Patient?> FindPatientAsync(int id) =>
        await (await PatientsAsync()).FirstOrDefaultAsync(p => p.Id == id);
}