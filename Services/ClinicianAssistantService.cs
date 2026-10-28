using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MenoScreen.Models;

namespace MenoScreen.Services;

public record AssistantReply(bool Ok, string Text);

// THE AI CLINICIAN ASSISTANT
// The clinician types a question on the result page (e.g. "When should I refer her?").
// We send that question to an AI language model over the internet, and show its answer.
//
// What keeps it safe ("grounding"):
//  * We give the AI the clinic's approved guideline text (Guidelines/assistant-guidelines.md)
//    and this screening's results, and instruct it to answer ONLY from those. If the answer
//    isn't there, it must say so instead of guessing.
//  * It's told never to diagnose, prescribe or give doses. It supports the clinician; it
//    doesn't replace her.
//  * Privacy: her name and record numbers are never sent, only her scores, band and
//    context answers (age range etc.), so the AI provider can't tell who she is.
//  * All provider settings (address, model, key) come from user-secrets, not the code.
public class ClinicianAssistantService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ClinicianAssistantService> _log;

    public ClinicianAssistantService(HttpClient http, IConfiguration config, IWebHostEnvironment env, ILogger<ClinicianAssistantService> log)
    {
        _http = http;
        _config = config;
        _env = env;
        _log = log;
    }

    // Switched on only when every provider setting is present in user-secrets
    public bool IsConfigured =>
        new[] { "Assistant:ApiKey", "Assistant:BaseUrl", "Assistant:Model", "Assistant:VersionHeader", "Assistant:ApiVersion" }
            .All(key => !string.IsNullOrWhiteSpace(_config[key]));

    public async Task<AssistantReply> AskAsync(Screening screening, string question, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new(false, "The assistant isn't switched on yet. Add its settings in user-secrets (see SETUP.md).");

        question = question.Trim();
        if (question.Length is 0 or > 1000)
            return new(false, "Please ask a question of up to 1,000 characters.");

        string guidelines = await LoadGuidelinesAsync(ct);

        // The "system prompt": standing instructions the AI must follow for every answer.
        string system =
            "You are the MenoScreen clinician assistant, used by South African primary-care clinicians " +
            "after a menopause symptom screening.\n\n" +
            "RULES:\n" +
            "1. Answer ONLY from the <guidelines> and <screening> provided. Do not use outside medical knowledge.\n" +
            "2. If the guidelines don't cover the question, say: \"The clinic guidelines don't cover this. Please consult a menopause-trained colleague or specialist.\" and stop.\n" +
            "3. Never diagnose, never prescribe, never give doses. You support the clinician's judgement; you don't replace it.\n" +
            "4. If the band is Complex, always remind the clinician that specialist referral comes before any treatment discussion.\n" +
            "5. Be brief: at most 150 words, plain language, warm and respectful about the patient.\n" +
            "6. When you use the guidelines, name the section heading you relied on.\n\n" +
            $"<guidelines>\n{guidelines}\n</guidelines>";

        // The message: this screening's results (no name) plus the clinician's question.
        string user = $"<screening>\n{DescribeScreening(screening)}\n</screening>\n\nClinician's question: {question}";

        var body = new
        {
            model = _config["Assistant:Model"],
            max_tokens = 600,
            system,
            messages = new[] { new { role = "user", content = user } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", _config["Assistant:ApiKey"]);
        request.Headers.Add(_config["Assistant:VersionHeader"]!, _config["Assistant:ApiVersion"]);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Send it. If the internet is down or the provider has a problem, we catch the error
        // and show a friendly message, so the rest of the result page still works.
        try
        {
            using var response = await _http.SendAsync(request, ct);
            string json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Assistant API returned {Status}: {Body}", (int)response.StatusCode, json);
                return new(false, "The assistant couldn't answer just now. Please try again in a moment.");
            }

            var parsed = JsonSerializer.Deserialize<MessagesResponse>(json);
            string text = string.Join("\n", parsed?.Content?
                .Where(c => c.Type == "text")
                .Select(c => c.Text) ?? Enumerable.Empty<string?>());

            return string.IsNullOrWhiteSpace(text)
                ? new(false, "The assistant returned an empty answer. Please rephrase your question.")
                : new(true, text.Trim());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "Assistant call failed");
            return new(false, "The assistant couldn't be reached. Check the internet connection and try again.");
        }
    }

    private async Task<string> LoadGuidelinesAsync(CancellationToken ct)
    {
        var path = Path.Combine(_env.ContentRootPath, "Guidelines", "assistant-guidelines.md");
        return File.Exists(path)
            ? await File.ReadAllTextAsync(path, ct)
            : "(No guideline file found. Answer every question with rule 2.)";
    }

    // Everything the model needs about the screening, and nothing that identifies her
    private static string DescribeScreening(Screening s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"MenoLikelihood score: {s.MenoLikelihood}/100");
        sb.AppendLine($"Score band: {s.ScoreBand}");
        sb.AppendLine($"Final triage band: {s.Band}{(s.Band == "Complex" ? " (safety question Q23 answered Yes: existing health conditions)" : "")}");

        sb.AppendLine("Symptom blocks (score / max):");
        foreach (var b in ScoringService.BlockBreakdown(s.Answers))
            sb.AppendLine($"- {b.Block}: {b.Score}/{b.Max} ({b.Percent}%)");

        var context = s.Answers
            .Where(a => a.Question.Type == QuestionType.Context && !string.IsNullOrWhiteSpace(a.TextValue))
            .OrderBy(a => a.Question.SortOrder);
        sb.AppendLine("Patient context:");
        foreach (var a in context)
            sb.AppendLine($"- {a.Question.Text} {a.TextValue}");

        return sb.ToString();
    }

    private sealed class MessagesResponse
    {
        [JsonPropertyName("content")] public List<ContentBlock>? Content { get; set; }
    }

    private sealed class ContentBlock
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
