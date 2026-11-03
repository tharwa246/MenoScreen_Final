
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Models;

namespace MenoScreen.Data;

// THE DATABASE, AS C# SEES IT
// Each DbSet below is one table. Entity Framework turns our C# classes (Models folder)
// into tables, and turns LINQ like _db.Patients.Where(...) into SQL for us.
// It inherits from IdentityDbContext, which adds the login tables (users, roles, passwords).
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Screening> Screenings => Set<Screening>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<QuestionSetVersion> QuestionSetVersions => Set<QuestionSetVersion>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<ScreeningInvite> ScreeningInvites => Set<ScreeningInvite>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<WeeklyCheckIn> WeeklyCheckIns => Set<WeeklyCheckIn>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); //this preserves the Identity model configuration for the existing authentication system.
        // One answer per question per screening
        builder.Entity<Answer>()
            .HasIndex(a => new { a.ScreeningId, a.QuestionId })
            .IsUnique(); //prevent duplicate answers to the same question within one screening and duplicate question numbers within the same version.
        // One question number per question-set version
        builder.Entity<Question>()
            .HasIndex(q => new { q.QuestionSetVersionId, q.Number })
            .IsUnique();

        builder.Entity<Screening>()
            .Property(s => s.Source)
            .HasMaxLength(20);

        // An index is like a book's index: it makes look-ups fast.
        // "Unique" also makes the database refuse two invites with the same token.
        builder.Entity<ScreeningInvite>()
            .HasIndex(i => i.Token)
            .IsUnique();

        // The audit log is read newest-first
        builder.Entity<AuditLog>()
            .HasIndex(a => a.At);

        // One portal login belongs to exactly one patient
        builder.Entity<Patient>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        // Her check-ins are always read in date order
        builder.Entity<WeeklyCheckIn>()
            .HasIndex(c => new { c.PatientId, c.CheckedInAt });

        // Deleting a clinic must never silently delete its patients
        builder.Entity<Patient>()
            .HasOne(p => p.Clinic)
            .WithMany(c => c.Patients)
            .HasForeignKey(p => p.ClinicId)
            .OnDelete(DeleteBehavior.Restrict);

        // If a clinic is deleted, its staff simply have no clinic until reassigned
        builder.Entity<StaffProfile>()
            .HasOne(s => s.Clinic)
            .WithMany()
            .HasForeignKey(s => s.ClinicId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

