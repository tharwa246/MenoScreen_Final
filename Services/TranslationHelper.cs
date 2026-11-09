using MenoScreen.Models;

namespace MenoScreen.Services;

// Value is what gets saved (always the English text), Label is what the clinician sees
public record OptionView(string Value, string Label);

public record QuestionView(int Id, int Number, string BlockKey, string Block,
                           string Text, QuestionType Type, List<OptionView> Options);

public static class TranslationHelper
{
    public static readonly Dictionary<string, string> Languages = new()
    {
        ["en"] = "English",
        ["zu"] = "isiZulu",
        ["xh"] = "isiXhosa",
        ["af"] = "Afrikaans"
    };

    public static string Normalize(string? language) =>
        language != null && Languages.ContainsKey(language) ? language : "en";

    // The question must be loaded with its Translations
    public static QuestionView Localize(Question q, string? language)
    {
        var lang = Normalize(language);
        var t = lang == "en" ? null : q.Translations.FirstOrDefault(x => x.Language == lang);

        var text = !string.IsNullOrWhiteSpace(t?.Text) ? t!.Text : q.Text;
        var block = !string.IsNullOrWhiteSpace(t?.Block) ? t!.Block : q.Block;

        // Saved answers stay in English, so the clinician summary and the
        // dashboard never mix languages. Only the label shown on screen changes.
        var values = Split(q.Options);
        var labels = Split(t?.Options);
        if (labels.Count != values.Count) labels = values;

        var options = values.Select((v, i) => new OptionView(v, labels[i])).ToList();

        return new QuestionView(q.Id, q.Number, q.Block, block, text, q.Type, options);
    }

    private static List<string> Split(string? options) =>
        string.IsNullOrWhiteSpace(options)
            ? new List<string>()
            : options.Split('|').Select(o => o.Trim()).ToList();
}