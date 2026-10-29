using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public class VersionService
{
    private readonly ApplicationDbContext _db;

    public VersionService(ApplicationDbContext db)
    {
        _db = db;
    }

    // A version is locked once any screening has used it
    public Task<bool> IsLockedAsync(int versionId) =>
        _db.Screenings.AnyAsync(s => s.QuestionSetVersionId == versionId);

    // Copies a version and all its questions into a new inactive draft
    public async Task<QuestionSetVersion> CloneAsync(int sourceId)
    {
        var source = await _db.QuestionSetVersions
            .Include(v => v.Questions)
            .FirstOrDefaultAsync(v => v.Id == sourceId)
            ?? throw new InvalidOperationException("Version not found.");

        var nextNumber = await _db.QuestionSetVersions.MaxAsync(v => v.VersionNumber) + 1;

        var copy = new QuestionSetVersion
        {
            VersionNumber = nextNumber,
            IsActive = false,
            Notes = $"Copy of version {source.VersionNumber}",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var q in source.Questions)
        {
            copy.Questions.Add(new Question
            {
                Number = q.Number,
                SortOrder = q.SortOrder,
                Block = q.Block,
                Text = q.Text,
                Type = q.Type,
                Options = q.Options
            });
        }

        _db.QuestionSetVersions.Add(copy);
        await _db.SaveChangesAsync();
        return copy;
    }

    // Makes one version active and every other version inactive
    public async Task ActivateAsync(int versionId)
    {
        var target = await _db.QuestionSetVersions
            .Include(v => v.Questions)
            .FirstOrDefaultAsync(v => v.Id == versionId)
            ?? throw new InvalidOperationException("Version not found.");

        // A broken version must never go live
        if (!target.Questions.Any(q => q.Type == QuestionType.Scored))
            throw new InvalidOperationException("This version has no scored questions.");

        if (target.Questions.Count(q => q.Type == QuestionType.SafetyFlag) != 1)
            throw new InvalidOperationException("This version must have exactly one safety question.");

        var all = await _db.QuestionSetVersions.ToListAsync();
        foreach (var v in all)
            v.IsActive = v.Id == versionId;

        await _db.SaveChangesAsync();
    }
}