using System.Net.Http.Json;

namespace MenoScreen.Services;

public class AzureTranslatorService
{
    private const int BatchSize = 100;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public AzureTranslatorService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public record TranslationPart(string Text, string To);
    public record TranslationItem(List<TranslationPart> Translations);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config["Translator:Key"]);

    // Translates English wording into one language. The result is in the same order as the input.
    // Only wording is ever sent: never a patient name or answer.
    public async Task<List<string>> TranslateAsync(
        IReadOnlyList<string> texts, string toLanguage, CancellationToken ct = default)
    {
        if (toLanguage == "en" || !TranslationHelper.Languages.ContainsKey(toLanguage))
            throw new InvalidOperationException($"Unsupported target language: {toLanguage}");

        var key = _config["Translator:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "The translator is not configured. Set Translator:Key and Translator:Region in user-secrets.");

        var region = _config["Translator:Region"];
        var results = new List<string>();

        foreach (var batch in texts.Chunk(BatchSize))
        {
            var body = batch.Select(t => new Dictionary<string, string> { ["Text"] = t }).ToList();

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"translate?api-version=3.0&from=en&to={toLanguage}")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", key);
            if (!string.IsNullOrWhiteSpace(region))
                request.Headers.Add("Ocp-Apim-Subscription-Region", region);

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"The translator returned {(int)response.StatusCode}. Check the key, region and quota.");

            var items = await response.Content.ReadFromJsonAsync<List<TranslationItem>>(cancellationToken: ct);

            // Never accept a partial or shuffled result
            if (items == null || items.Count != batch.Length || items.Any(i => i.Translations.Count == 0))
                throw new InvalidOperationException("The translator returned an unexpected reply.");

            results.AddRange(items.Select(i => i.Translations[0].Text));
        }

        return results;
    }
}