
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
    }
}

