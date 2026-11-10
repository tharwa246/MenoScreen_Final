using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Models;

namespace MenoScreen.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Screening> Screenings => Set<Screening>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<QuestionSetVersion> QuestionSetVersions => Set<QuestionSetVersion>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QuestionTranslation> QuestionTranslations => Set<QuestionTranslation>();
    public DbSet<ContentText> ContentTexts => Set<ContentText>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // keeps the Identity tables working

        // One answer per question per screening
        builder.Entity<Answer>()
            .HasIndex(a => new { a.ScreeningId, a.QuestionId })
            .IsUnique();

        // One question number per question-set version
        builder.Entity<Question>()
            .HasIndex(q => new { q.QuestionSetVersionId, q.Number })
            .IsUnique();

        // One appointment per slot: the database-level double-booking guard
        builder.Entity<Appointment>()
            .HasIndex(a => a.AvailabilitySlotId)
            .IsUnique();

        // One translation per question per language
        builder.Entity<QuestionTranslation>()
            .HasIndex(t => new { t.QuestionId, t.Language })
            .IsUnique();

        // One interface text per key per language
        builder.Entity<ContentText>()
            .HasIndex(c => new { c.Key, c.Language })
            .IsUnique();
    }
}
