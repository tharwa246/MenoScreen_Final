using MenoScreen.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MenoScreen.Services;

public class DocumentService
{
    public DocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string LandingUrl = "https://joinmandi.com";

    // Clinician summary wording from section 7 of the brief.
    // Temporary: later this moves into the database so the medical team can edit it.
    private static readonly Dictionary<string, string> SummaryText = new()
    {
        ["Unlikely"] = "Low MenoLikelihood score. No significant symptom cluster identified at this time. Recommend: reassurance and routine follow-up.",
        ["Possible"] = "Moderate MenoLikelihood score. Symptom cluster consistent with perimenopause. Recommend: menopause conversation, lifestyle support, consider referral.",
        ["Likely"] = "High MenoLikelihood score. Strong symptom pattern consistent with perimenopause or menopause. Recommend: discussion of HRT options or referral to menopause-trained specialist.",
        ["Significant"] = "Very high MenoLikelihood score. Severe symptom burden. Urgent referral to menopause-trained specialist recommended. Patient requires compassionate, timely support.",
        ["Complex"] = "Complex case flag raised. Existing health conditions noted. Specialist referral required before any treatment decision. Do not recommend HRT without specialist input."
    };

    // Patient handout wording from section 7 of the brief.
    // Temporary: later this moves into the database so the medical team can edit it.
    private static readonly Dictionary<string, string> HandoutText = new()
    {
        ["Unlikely"] = "Your results today don't show a strong hormonal pattern, but your body knows best. If you notice changes, come back. JoinMandi.com has information to help you understand what to watch for.",
        ["Possible"] = "What you are feeling has a name. Your results today suggest your hormones may be starting to change. This is perimenopause, and there is support available. JoinMandi.com will help you understand what is happening and what your options are.",
        ["Likely"] = "Your symptoms are real, and they have a cause. Your results today suggest you are likely in perimenopause or menopause. You have options, and you deserve to know about them. JoinMandi.com will walk you through what is happening and how to find the right support.",
        ["Significant"] = "You have been carrying a lot. Your results today show that your symptoms are significant, and you deserve urgent, proper support. Please do not wait. JoinMandi.com can help you find the right doctor now.",
        ["Complex"] = "Your results today suggest your symptoms may be related to your hormones, but because of your health history, your doctor wants to make sure you get the right specialist support. JoinMandi.com has information to help you prepare for that conversation."
    };

    // The screening must be loaded with Patient, and Answers including each Question
    public byte[] BuildClinicianSummary(Screening s)
    {
        // Subtotal per block, from the scored answers only
        var blocks = s.Answers
            .Where(a => a.Question.Type == QuestionType.Scored)
            .GroupBy(a => a.Question.Block)
            .Select(g => new
            {
                Name = g.Key,
                Score = g.Sum(a => a.Value ?? 0),
                Max = g.Count() * 3
            })
            .ToList();

        // Context answers (Q1-Q4)
        var context = s.Answers
            .Where(a => a.Question.Type == QuestionType.Context)
            .OrderBy(a => a.Question.SortOrder)
            .ToList();

        var summary = SummaryText.GetValueOrDefault(s.Band, string.Empty);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Text("MenoScreen by Mandi: Clinician Summary").FontSize(18).Bold();

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"Patient: {s.Patient.FullName}");
                    col.Item().Text($"Screened: {s.ScreenedAt:yyyy-MM-dd HH:mm} (UTC)");

                    col.Item().Text("Patient context").Bold().FontSize(13);
                    foreach (var a in context)
                        col.Item().Text($"{a.Question.Text}  {a.TextValue}");

                    col.Item().Text("Result").Bold().FontSize(13);
                    col.Item().Text($"MenoLikelihood score: {s.MenoLikelihood} / 100");
                    col.Item().Text($"Score band: {s.ScoreBand}");
                    col.Item().Text($"Final triage band: {s.Band}").Bold();

                    col.Item().Text("Symptom profile").Bold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(1);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Symptom block").Bold();
                            h.Cell().Text("Score").Bold();
                        });

                        foreach (var b in blocks)
                        {
                            table.Cell().Text(b.Name);
                            table.Cell().Text($"{b.Score} / {b.Max}");
                        }
                    });

                    col.Item().Text("Recommended action").Bold().FontSize(13);
                    col.Item().Text(summary);
                });

                page.Footer().Text("For clinician records only. Prototype: wording awaiting medical review.")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    // Only needs the screening with its Patient loaded
    public byte[] BuildPatientHandout(Screening s)
    {
        var text = HandoutText.GetValueOrDefault(s.Band, string.Empty);

        // QR code pointing at the JoinMandi site (no band or score in the link)
        using var generator = new QRCodeGenerator();
        var qrData = generator.CreateQrCode(LandingUrl, QRCodeGenerator.ECCLevel.Q);
        byte[] qrPng = new PngByteQRCode(qrData).GetGraphic(10);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(13));

                page.Header().Text("MenoScreen by Mandi").FontSize(22).Bold();

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text($"Hello {s.Patient.FullName}").FontSize(16).Bold();
                    col.Item().Text(text);

                    col.Item().PaddingTop(10).Text("Your next step").Bold();
                    col.Item().Text("Scan the code with your phone, or visit:");
                    col.Item().Text(LandingUrl).Bold();
                    col.Item().Width(140).Image(qrPng);

                    col.Item().PaddingTop(20).Text("Talk to Mandi · Khuluma noMandi · Thetha noMandi · Praat met Mandi")
                        .Italic();
                    col.Item().Text("You are not alone · Awuhleli wedwa · Awukho wedwa · Jy is nie alleen nie")
                        .Italic();
                });

                page.Footer().Text("Prototype: wording awaiting medical review.")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }
}