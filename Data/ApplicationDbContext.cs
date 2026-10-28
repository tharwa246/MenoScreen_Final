
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
    }
}

