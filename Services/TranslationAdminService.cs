using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Services;

public record TranslateSummary(int Translated, int SkippedReviewed, int SkippedExisting);
public record EnglishQuestion(int Number, string Block, string Text, string? Options, QuestionType Type);

// These are classes (not records) so the review pages can bind them from a form
public class ReviewItem
{
    public int QuestionId { get; set; }
    public string? Text { get; set; }
    public string? Options { get; set; }
    public bool IsReviewed { get; set; }
}

public class BlockItem
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class ContentItem
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsReviewed { get; set; }
}

public class ReviewData
{
    public List<ReviewItem> Items { get; set; } = new();
    public List<BlockItem> Blocks { get; set; } = new();
    public Dictionary<int, EnglishQuestion> English { get; set; } = new();
}

public class WordingData
{
    public List<ContentItem> Items { get; set; } = new();
    public Dictionary<string, string> English { get; set; } = new();
}

public class TranslationAdminService
{
    private readonly ApplicationDbContext _db;
    private readonly AzureTranslatorService _translator;

    public TranslationAdminService(ApplicationDbContext db, AzureTranslatorService translator)
    {
        _db = db;
        _translator = translator;
    }

    // ---------- Azure: questions ----------

    // Fills in missing translations as unreviewed drafts. Reviewed wording is never touched.
    // Existing drafts are only replaced when replaceDrafts is true.
    // If Azure fails, nothing is saved.
    public async Task<TranslateSummary> TranslateVersionAsync(
        int versionId, string language, string userName, bool replaceDrafts, CancellationToken ct = default)
    {
        RequireTarget(language);

        var questions = await _db.Questions
            .Include(q => q.Translations)
            .Where(q => q.QuestionSetVersionId == versionId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync(ct);

        if (questions.Count == 0)
            throw new InvalidOperationException("That version has no questions.");

        int skippedReviewed = 0, skippedExisting = 0;
        var todo = new List<Question>();
        foreach (var q in questions)
        {
            var row = q.Translations.FirstOrDefault(t => t.Language == language);
            if (row != null && row.IsReviewed) { skippedReviewed++; continue; }
            if (row != null && !string.IsNullOrWhiteSpace(row.Text) && !replaceDrafts) { skippedExisting++; continue; }
            todo.Add(q);
        }

        if (todo.Count == 0)
            return new TranslateSummary(0, skippedReviewed, skippedExisting);

        // Identical wording ("Yes", "No", block names) is sent once, so it is translated the same way everywhere
        var strings = new List<string>();
        var position = new Dictionary<string, int>();
        int Slot(string text)
        {
            if (!position.TryGetValue(text, out var i))
            {
                i = strings.Count;
                strings.Add(text);
                position[text] = i;
            }
            return i;
        }

        var plan = todo.Select(q => new
        {
            Question = q,
            Text = Slot(q.Text),
            Block = string.IsNullOrWhiteSpace(q.Block) ? -1 : Slot(q.Block),
            Options = SplitOptions(q.Options).Select(Slot).ToList()
        }).ToList();

        // One call to Azure. If it throws, we have changed nothing.
        var translated = await _translator.TranslateAsync(strings, language, ct);

        foreach (var p in plan)
        {
            var q = p.Question;
            var row = q.Translations.FirstOrDefault(t => t.Language == language);
            if (row == null)
            {
                row = new QuestionTranslation { Language = language };
                q.Translations.Add(row);
            }

            row.Text = translated[p.Text];
            row.Block = p.Block < 0 ? string.Empty : translated[p.Block];
            // "|" separates options, so it can never appear inside one
            row.Options = p.Options.Count == 0
                ? null
                : string.Join("|", p.Options.Select(i => translated[i].Replace("|", " ").Trim()));
            row.IsReviewed = false;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userName;
        }

        await _db.SaveChangesAsync(ct);
        return new TranslateSummary(todo.Count, skippedReviewed, skippedExisting);
    }

    // ---------- Azure: interface and handout wording ----------

    public async Task<TranslateSummary> TranslateInterfaceAsync(
        string language, string userName, bool replaceDrafts, CancellationToken ct = default)
    {
        RequireTarget(language);

        // Pick up any change to the English handout wording made on Admin > Clinical content
        await ContentSeed.SyncHandoutsAsync(_db);

        var english = await _db.ContentTexts
            .Where(c => c.Language == "en")
            .OrderBy(c => c.Key)
            .ToListAsync(ct);

        var existing = await _db.ContentTexts.Where(c => c.Language == language).ToListAsync(ct);
        var byKey = existing.ToDictionary(c => c.Key);

        int skippedReviewed = 0, skippedExisting = 0;
        var todo = new List<ContentText>();
        foreach (var e in english)
        {
            if (byKey.TryGetValue(e.Key, out var row))
            {
                if (row.IsReviewed) { skippedReviewed++; continue; }
                if (!string.IsNullOrWhiteSpace(row.Value) && !replaceDrafts) { skippedExisting++; continue; }
            }
            todo.Add(e);
        }

        if (todo.Count == 0)
            return new TranslateSummary(0, skippedReviewed, skippedExisting);

        var translated = await _translator.TranslateAsync(todo.Select(e => e.Value).ToList(), language, ct);

        for (int i = 0; i < todo.Count; i++)
        {
            var e = todo[i];
            if (!byKey.TryGetValue(e.Key, out var row))
            {
                row = new ContentText { Key = e.Key, Language = language };
                _db.ContentTexts.Add(row);
            }
            row.Value = translated[i];
            row.IsReviewed = false;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userName;
        }

        await _db.SaveChangesAsync(ct);
        return new TranslateSummary(todo.Count, skippedReviewed, skippedExisting);
    }

    // ---------- Review: questions ----------

    public async Task<ReviewData> LoadReviewAsync(int versionId, string language)
    {
        var questions = await _db.Questions
            .AsNoTracking()
            .Include(q => q.Translations.Where(t => t.Language == language))
            .Where(q => q.QuestionSetVersionId == versionId)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        var data = new ReviewData();
        foreach (var q in questions)
        {
            var t = q.Translations.FirstOrDefault();
            data.English[q.Id] = new EnglishQuestion(q.Number, q.Block, q.Text, q.Options, q.Type);
            data.Items.Add(new ReviewItem
            {
                QuestionId = q.Id,
                Text = t?.Text,
                Options = t?.Options,
                IsReviewed = t?.IsReviewed ?? false
            });
        }

        foreach (var block in questions.Select(q => q.Block).Distinct())
        {
            var value = questions
                .Where(q => q.Block == block)
                .Select(q => q.Translations.FirstOrDefault()?.Block)
                .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            data.Blocks.Add(new BlockItem { Key = block, Value = value ?? string.Empty });
        }

        return data;
    }

    // Returns a list of problems. If there are any, nothing is saved.
    // Blank wording removes the translation, so the form falls back to English.
    public async Task<List<string>> SaveReviewAsync(int versionId, string language,
        IList<BlockItem> blocks, IList<ReviewItem> items, string userName)
    {
        RequireTarget(language);

        var questions = await _db.Questions
            .Include(q => q.Translations)
            .Where(q => q.QuestionSetVersionId == versionId)
            .ToListAsync();
        var byId = questions.ToDictionary(q => q.Id);

        // Ignore question ids from other versions, and duplicates
        var posted = items
            .Where(i => byId.ContainsKey(i.QuestionId))
            .GroupBy(i => i.QuestionId)
            .Select(g => g.First())
            .ToList();

        var errors = new List<string>();
        foreach (var item in posted)
        {
            var q = byId[item.QuestionId];
            var text = item.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            if (text.Length > 1000)
                errors.Add($"Question {q.Number}: the text is too long (1000 characters at most).");

            var english = SplitOptions(q.Options);
            var options = SplitOptions(item.Options);
            if (english.Count > 0 && options.Count > 0 && options.Count != english.Count)
                errors.Add($"Question {q.Number}: it needs {english.Count} answer options separated by | (you entered {options.Count}).");
        }
        if (errors.Count > 0) return errors;

        var removed = new HashSet<int>();
        foreach (var item in posted)
        {
            var q = byId[item.QuestionId];
            var row = q.Translations.FirstOrDefault(t => t.Language == language);
            var text = item.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                if (row != null)
                {
                    _db.QuestionTranslations.Remove(row);
                    removed.Add(row.Id);
                }
                continue;
            }

            if (row == null)
            {
                row = new QuestionTranslation { Language = language };
                q.Translations.Add(row);
            }

            var options = SplitOptions(item.Options);
            row.Text = text;
            row.Options = SplitOptions(q.Options).Count == 0 || options.Count == 0 ? null : string.Join("|", options);
            row.IsReviewed = item.IsReviewed;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userName;
        }

        // A block name applies to every question in that block
        var blockNames = new Dictionary<string, string>();
        foreach (var b in blocks)
            if (!string.IsNullOrWhiteSpace(b.Key) && !string.IsNullOrWhiteSpace(b.Value))
                blockNames[b.Key] = b.Value.Trim();

        foreach (var q in questions)
        {
            var row = q.Translations.FirstOrDefault(t => t.Language == language);
            if (row == null || removed.Contains(row.Id)) continue;
            if (blockNames.TryGetValue(q.Block, out var name)) row.Block = name;
        }

        await _db.SaveChangesAsync();
        return errors;
    }

    // ---------- Review: interface and handout wording ----------

    public async Task<WordingData> LoadWordingAsync(string language)
    {
        var english = await _db.ContentTexts.AsNoTracking()
            .Where(c => c.Language == "en")
            .OrderBy(c => c.Key)
            .ToListAsync();

        var rows = await _db.ContentTexts.AsNoTracking()
            .Where(c => c.Language == language)
            .ToListAsync();
        var byKey = rows.ToDictionary(c => c.Key);

        var data = new WordingData();
        foreach (var e in english)
        {
            data.English[e.Key] = e.Value;
            byKey.TryGetValue(e.Key, out var row);
            data.Items.Add(new ContentItem
            {
                Key = e.Key,
                Value = row?.Value,
                IsReviewed = row?.IsReviewed ?? false
            });
        }
        return data;
    }

    public async Task<List<string>> SaveWordingAsync(string language, IList<ContentItem> items, string userName)
    {
        RequireTarget(language);

        var englishKeys = (await _db.ContentTexts
            .Where(c => c.Language == "en")
            .Select(c => c.Key)
            .ToListAsync()).ToHashSet();

        var posted = items
            .Where(i => englishKeys.Contains(i.Key))
            .GroupBy(i => i.Key)
            .Select(g => g.First())
            .ToList();

        var errors = new List<string>();
        foreach (var item in posted)
            if ((item.Value?.Trim().Length ?? 0) > 2000)
                errors.Add($"{item.Key}: the text is too long (2000 characters at most).");
        if (errors.Count > 0) return errors;

        var rows = await _db.ContentTexts.Where(c => c.Language == language).ToListAsync();
        var byKey = rows.ToDictionary(c => c.Key);

        foreach (var item in posted)
        {
            var value = item.Value?.Trim();
            byKey.TryGetValue(item.Key, out var row);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (row != null) _db.ContentTexts.Remove(row);
                continue;
            }

            if (row == null)
            {
                row = new ContentText { Key = item.Key, Language = language };
                _db.ContentTexts.Add(row);
            }
            row.Value = value;
            row.IsReviewed = item.IsReviewed;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = userName;
        }

        await _db.SaveChangesAsync();
        return errors;
    }

    // ---------- helpers ----------

    private static void RequireTarget(string language)
    {
        if (language == "en" || !TranslationHelper.Languages.ContainsKey(language))
            throw new InvalidOperationException($"Unsupported language: {language}");
    }

    private static List<string> SplitOptions(string? options) =>
        string.IsNullOrWhiteSpace(options)
            ? new List<string>()
            : options.Split('|').Select(o => o.Trim()).ToList();
}