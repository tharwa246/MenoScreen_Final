using MenoScreen.Data;
using Microsoft.AspNetCore.Identity;

namespace MenoScreen.Services;

public class DoctorAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _users;

    public DoctorAccountService(ApplicationDbContext db, UserManager<IdentityUser> users)
    {
        _db = db;
        _users = users;
    }

    // Creates a login in the Doctor role and links it to the doctor record.
    // Returns a list of problems. An empty list means it worked.
    public async Task<List<string>> LinkAccountAsync(int doctorId, string? email, string? password)
    {
        var errors = new List<string>();

        var doctor = await _db.Doctors.FindAsync(doctorId);
        if (doctor == null)
        {
            errors.Add("Doctor not found.");
            return errors;
        }

        if (doctor.UserId != null)
        {
            errors.Add("This doctor already has a login.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            errors.Add("An email and a password are required.");
            return errors;
        }

        email = email.Trim();
        if (await _users.FindByEmailAsync(email) != null)
        {
            errors.Add("That email already has an account.");
            return errors;
        }

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            errors.AddRange(created.Errors.Select(e => e.Description));
            return errors;
        }

        IdentityResult roleResult;
        try
        {
            roleResult = await _users.AddToRoleAsync(user, "Doctor");
        }
        catch (InvalidOperationException)
        {
            // Identity throws, instead of returning an error, when the role does not exist
            roleResult = IdentityResult.Failed(new IdentityError { Description = "The Doctor role does not exist." });
        }

        if (!roleResult.Succeeded)
        {
            await _users.DeleteAsync(user);   // never leave a half-made account behind
            errors.AddRange(roleResult.Errors.Select(e => e.Description));
            return errors;
        }

        doctor.UserId = user.Id;
        await _db.SaveChangesAsync();
        return errors;
    }
}