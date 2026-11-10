using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

// TEST DATA FOR DEMOS (Development only, never on a live site)
// Creates 3 made-up clinics, a test clinician at each, and 30 made-up patients per clinic
// with screenings spread over the last 12 weeks, so the analytics have something to show.
// Every name is invented. It only runs when the database has no clinics yet.
public static class DemoDataSeeder
{
    // Name, area, test clinician's login and name, and how severe that clinic's
    // patients tend to be (0 = no symptoms, 1 = severe), so the clinics look different
    private static readonly (string Name, string Area, string Email, string Clinician, double Severity)[] DemoClinics =
    {
        ("Gugulethu Community Clinic", "Gugulethu", "clinician@menoscreen.test",  "Thandi Nkosi",   0.55),
        ("Sea Point Women's Health",   "Sea Point", "clinician2@menoscreen.test", "Anna van Wyk",   0.38),
        ("Soweto Family Practice",     "Soweto",    "clinician3@menoscreen.test", "Lerato Mokoena", 0.68),
    };

    // How many made-up patients each clinic gets (keep this at 12 or less, so names never repeat)
    private const int PatientsPerClinic = 12;

    // Each clinic has its OWN 12 first names and 12 surnames, in the same order as DemoClinics,
    // so no name ever appears at two clinics
    private static readonly string[][] FirstNames =
    {
        new[] { "Nomsa", "Lindiwe", "Zanele", "Thembeka", "Noluthando", "Busisiwe", "Siphokazi", "Nandipha", "Ayanda", "Zoleka", "Lulama", "Nobuhle" },
        new[] { "Marlene", "Ayesha", "Charmaine", "Elmarie", "Fatima", "Rukhsana", "Bronwyn", "Gail", "Sharon", "Yolanda", "Natasha", "Leigh" },
        new[] { "Palesa", "Mpho", "Dineo", "Refilwe", "Boitumelo", "Nthabiseng", "Tshepiso", "Karabo", "Masego", "Lesedi", "Puleng", "Keabetswe" }
    };

    private static readonly string[][] Surnames =
    {
        new[] { "Mabandla", "Ntshona", "Qwabe", "Mavuso", "Sityana", "Makhaya", "Bikitsha", "Tyali", "Dyantyi", "Mgudlwa", "Ngcobo", "Mfeka" },
        new[] { "Jacobs", "Botha", "Adams", "Petersen", "Daniels", "Hendricks", "Fortuin", "Abrahams", "Van der Merwe", "Isaacs", "Solomons", "Steyn" },
        new[] { "Mahlangu", "Sithole", "Molefe", "Radebe", "Masilo", "Tau", "Motaung", "Shabalala", "Maseko", "Phiri", "Mathebula", "Seabi" }
    };

    private static readonly string[] Languages = { "en", "en", "zu", "xh", "af" };

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<IdentityUser> users, ScreeningService screenings)
    {
        // Already done? Then stop, so a restart doesn't add duplicates
        if (await db.Clinics.AnyAsync()) return;

        // A fixed "seed" number gives the same made-up data every time, so demos are repeatable
        var rng = new Random(2026);
        var questions = await screenings.GetActiveQuestionsAsync();

        // The test admin gets a name too (no clinic: admins see every clinic)
        var admin = await users.FindByEmailAsync("admin@menoscreen.test");
        if (admin != null && !await db.StaffProfiles.AnyAsync(s => s.UserId == admin.Id))
            db.StaffProfiles.Add(new StaffProfile { UserId = admin.Id, FullName = "Test Admin" });

          for (int ci = 0; ci < DemoClinics.Length; ci++)
        {
            var c = DemoClinics[ci];
            var clinic = new Clinic { Name = c.Name, Area = c.Area };
            db.Clinics.Add(clinic);
            await db.SaveChangesAsync();

            // A test clinician for this clinic (password Clinician@1234)
            var clinician = await users.FindByEmailAsync(c.Email);
            if (clinician == null)
            {
                clinician = new IdentityUser { UserName = c.Email, Email = c.Email, EmailConfirmed = true };
                await users.CreateAsync(clinician, "Clinician@1234");
                await users.AddToRoleAsync(clinician, "Clinician");
            }
            db.StaffProfiles.Add(new StaffProfile { UserId = clinician.Id, FullName = c.Clinician, ClinicId = clinic.Id });
            await db.SaveChangesAsync();

            for (int i = 0; i < PatientsPerClinic; i++)
            {
                // Her first visit was some time in the last 12 weeks
                var firstVisit = DateTime.UtcNow.AddDays(-rng.Next(7, 84)).AddHours(-rng.Next(0, 8));
                bool consent = i >= 1;   // the first one has no consent yet, to show that message

                var patient = new Patient
                {
                    // First name i, paired with a shuffled surname. Stepping by 5 through 12 surnames
                    // visits each one exactly once, so no two patients share a name.
                    FullName = $"{FirstNames[ci][i]} {Surnames[ci][(i * 5) % Surnames[ci].Length]}",
                    PreferredLanguage = Languages[rng.Next(Languages.Length)],
                    ClinicId = clinic.Id,
                    CreatedAt = firstVisit,
                    ConsentGiven = consent,
                    ConsentRecordedAt = consent ? firstVisit : null,
                    ConsentRecordedBy = consent ? clinician.Id : null
                };
                db.Patients.Add(patient);
                await db.SaveChangesAsync();

                if (!consent) continue;

                // About 4 in 10 came back for a second screening a few weeks later
                var visits = new List<DateTime> { firstVisit };
                var secondVisit = firstVisit.AddDays(rng.Next(14, 42));
                if (rng.NextDouble() < 0.4 && secondVisit < DateTime.UtcNow)
                    visits.Add(secondVisit);

                // How strong her symptoms are: near her clinic's average, but each woman differs
                double herSeverity = Math.Clamp(c.Severity + (rng.NextDouble() - 0.5) * 0.5, 0.05, 0.95);

                foreach (var when in visits)
                {
                    var (numeric, text) = MakeAnswers(questions, herSeverity, rng);

                    // Saved through the real ScreeningService, so the scores are calculated
                    // exactly like a real screening
                    var screening = await screenings.SaveScreeningAsync(patient.Id, clinician.Id, numeric, text);

                    screening.ScreenedAt = when;
                    if (rng.NextDouble() < 0.25)
                        screening.Source = ScreeningSources.SelfScreen;
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    // Makes up one set of answers to the 23 questions
    private static (Dictionary<int, int> Numeric, Dictionary<int, string> Text) MakeAnswers(
        List<Question> questions, double severity, Random rng)
    {
        var numeric = new Dictionary<int, int>();
        var text = new Dictionary<int, string>();

        foreach (var q in questions)
        {
            if (q.Type == QuestionType.Context)
            {
                // Pick one of the question's own options
                var options = (q.Options ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
                text[q.Id] = options[rng.Next(options.Length)];
            }
            else if (q.Type == QuestionType.Scored)
            {
                // Each answer lands near her severity (0-3), with some natural variation
                var value = severity * 3 + (rng.NextDouble() - 0.5) * 2;
                numeric[q.Id] = (int)Math.Clamp(Math.Round(value), 0, 3);
            }
            else
            {
                // Safety question: about 1 in 12 have an existing condition (band becomes Complex)
                numeric[q.Id] = rng.NextDouble() < 0.08 ? 1 : 0;
            }
        }

        return (numeric, text);
    }
}