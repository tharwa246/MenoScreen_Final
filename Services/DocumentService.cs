using MenoScreen.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MenoScreen.Services;

// THE PDFs: the clinician summary and the patient handout.
// They're designed to look like the Result page: the same JoinMandi gradient band, fonts,
// band colours and "symptom blanket" patches, so the app and its paperwork feel like one product.
public class DocumentService
{
    public DocumentService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const string LandingUrl = "https://joinmandi.com";

    // ---- Brand colours, the same values as the :root block in wwwroot/css/site.css ----
    private const string Cream = "#FDF7ED";
    private const string CreamLight = "#FFFCF6";
    private const string Sand = "#F6E8D3";
    private const string Line = "#EDDCC6";
    private const string Orange = "#E08D1F";
    private const string Sun = "#F2C14E";
    private const string Terracotta = "#C94A2B";
    private const string Brown = "#3B2A22";
    private const string Text = "#4A3A32";
    private const string Muted = "#7A685D";

    // Severity colours for the blanket patches (0 = not at all ... 3 = extremely)
    private static readonly string[] SeverityFill = { "#F7E9D6", "#F2C14E", "#DC8248", "#C94A2B" };
    private static readonly string[] SeverityInk = { Brown, "#452A15", "#3A1D0C", "#FFFFFF" };
    private static readonly string[] RatingLabels = { "Not at all", "A little", "Quite a bit", "Extremely" };

    // Band pill colours: (fill, text)
    private static readonly Dictionary<string, (string Fill, string Ink)> BandColours = new()
    {
        ["Unlikely"] = ("#5E8C6A", "#FFFFFF"),
        ["Possible"] = ("#F2C14E", "#452A15"),
        ["Likely"] = ("#DC8248", "#3A1D0C"),
        ["Significant"] = ("#C94A2B", "#FFFFFF"),
        ["Complex"] = ("#7B4A8C", "#FFFFFF")
    };

    // ---- Fonts ----
    // The website's fonts, loaded from the project's Fonts folder at start-up (see Program.cs).
    // If the font files are missing, these stay null and the PDF falls back to QuestPDF's
    // built-in font, so the PDF still works.
    private static string? BodyFont;
    private static string? DisplayFont;

    public static void RegisterFonts(string fontsFolder)
    {
        if (!Directory.Exists(fontsFolder))
        {
            Console.WriteLine($"PDF fonts: folder not found at {fontsFolder}");
            return;
        }

        foreach (var file in Directory.GetFiles(fontsFolder, "*.ttf"))
        {
            var name = Path.GetFileName(file);

            // Register each file under the exact name we ask for later, so the names always match.
            // Regular and Bold share one name; QuestPDF picks the right one when we use .Bold().
            if (name.StartsWith("Montserrat", StringComparison.OrdinalIgnoreCase))
            {
                QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Montserrat", File.OpenRead(file));
                BodyFont = "Montserrat";
            }
            else if (name.StartsWith("SourGummy", StringComparison.OrdinalIgnoreCase))
            {
                QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("SourGummy", File.OpenRead(file));
                DisplayFont = "SourGummy";
            }
            Console.WriteLine($"PDF fonts: loaded {name}");
        }
    }

    // Use the rounded JoinMandi headline font for a piece of text (bold either way)
    private static TextSpanDescriptor Display(TextSpanDescriptor text) =>
        DisplayFont == null ? text.Bold() : text.FontFamily(DisplayFont).Bold();

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

    // =====================================================================================
    // CLINICIAN SUMMARY (same layout as the Result page)
    // The screening must be loaded with Patient, and Answers including each Question.
    // =====================================================================================
    public byte[] BuildClinicianSummary(Screening s)
    {
        var blocks = ScoringService.BlockBreakdown(s.Answers);
        var patchRows = s.Answers
            .Where(a => a.Question.Type == QuestionType.Scored)
            .OrderBy(a => a.Question.SortOrder)
            .GroupBy(a => a.Question.Block)
            .ToList();
        var context = s.Answers
            .Where(a => a.Question.Type == QuestionType.Context)
            .OrderBy(a => a.Question.SortOrder)
            .ToList();
        var summary = SummaryText.GetValueOrDefault(s.Band, string.Empty);
               (string Fill, string Ink) band = BandColours.GetValueOrDefault(s.Band, (Muted, "#FFFFFF"));
        string how = s.Source == ScreeningSources.SelfScreen ? "QR self-screen" : "with a clinician";

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(Cream);
                page.DefaultTextStyle(t => (BodyFont == null ? t : t.FontFamily(BodyFont)).FontSize(10).FontColor(Text).LineHeight(1.35f));

                // 1. Gradient band, like the top of joinmandi.com
                page.Header().Element(h => GradientBand(h, "Clinician summary"));

                page.Content().PaddingHorizontal(36).PaddingVertical(24).Column(col =>
                {
                    col.Spacing(16);

                    // 2. Who and when
                    col.Item().Column(c =>
                    {
                        Display(c.Item().Text($"Result for {s.Patient.FullName}")).FontSize(22).FontColor(Brown);
                        c.Item().Text($"Screened {s.ScreenedAt:d MMM yyyy, HH:mm} (UTC), {how}.").FontColor(Muted);
                    });

                    // 3. Score card: big number left, band + message right
                    col.Item().Background(CreamLight).Border(1).BorderColor(Line).Padding(18).Row(row =>
                    {
                        row.Spacing(20);
                        row.ConstantItem(120).Column(c =>
                        {
                            c.Item().Text(t =>
                            {
                                Display(t.Span(s.MenoLikelihood.ToString())).FontSize(40).FontColor(Terracotta);
                                t.Span(" / 100").FontSize(12).FontColor(Muted);
                            });
                            c.Item().Text("MenoLikelihood score").FontSize(8.5f).FontColor(Muted);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Spacing(6);
                            c.Item().Row(r =>
                            {
                                r.AutoItem().Background(band.Fill).PaddingVertical(3).PaddingHorizontal(10)
                                    .Text(s.Band).Bold().FontSize(10).FontColor(band.Ink);
                                if (s.Band != s.ScoreBand)
                                    r.AutoItem().PaddingLeft(8).AlignMiddle().Text($"Score alone: {s.ScoreBand}").FontSize(9).FontColor(Muted);
                            });
                            c.Item().Text($"Raw score {s.RawScore} of {blocks.Sum(b => b.Max)}.").FontSize(9).FontColor(Muted);
                        });
                    });

                    // 4. Safety flag, only when Q23 was answered Yes
                    if (s.Band == "Complex")
                    {
                        col.Item().Background("#F1E7F4").Border(1).BorderColor("#D8C3E0").Padding(12).Text(t =>
                        {
                            t.Span("Safety flag: existing health conditions. ").Bold().FontColor("#3F2449");
                            t.Span("She answered yes to question 23. Refer to a specialist before any treatment conversation, whatever her score.").FontColor("#3F2449");
                        });
                    }

                    // 5. The symptom blanket: one coloured patch per answer
                    col.Item().Background(CreamLight).Border(1).BorderColor(Line).Padding(18).Column(c =>
                    {
                        c.Spacing(9);
                        Display(c.Item().Text("Her symptom blanket")).FontSize(15).FontColor(Brown);
                        c.Item().PaddingBottom(4).Text("Each patch is one answer. The darker the patch, the more that symptom is affecting her.")
                            .FontSize(9).FontColor(Muted);

                        foreach (var rowAnswers in patchRows)
                        {
                            var total = blocks.First(b => b.Block == rowAnswers.Key);
                            c.Item().Row(r =>
                            {
                                r.ConstantItem(160).AlignMiddle().Text(rowAnswers.Key).Bold().FontColor(Brown);
                                r.RelativeItem().Row(p =>
                                {
                                    p.Spacing(4);
                                    foreach (var a in rowAnswers)
                                    {
                                        int v = Math.Clamp(a.Value ?? 0, 0, 3);
                                        p.ConstantItem(22).Height(22).Background(SeverityFill[v])
                                            .Border(0.5f).BorderColor("#CBB8A6")
                                            .AlignCenter().AlignMiddle()
                                            .Text(v.ToString()).FontSize(8).Bold().FontColor(SeverityInk[v]);
                                    }
                                });
                                r.ConstantItem(70).AlignRight().AlignMiddle().Text($"{total.Score} / {total.Max}").FontColor(Muted);
                            });
                        }

                        // Key
                        c.Item().PaddingTop(6).Row(r =>
                        {
                            r.Spacing(14);
                            for (int i = 0; i <= 3; i++)
                            {
                                int level = i;
                                r.AutoItem().Row(k =>
                                {
                                    k.ConstantItem(10).Height(10).Background(SeverityFill[level]).Border(0.5f).BorderColor("#CBB8A6");
                                    k.AutoItem().PaddingLeft(4).Text($"{level} {RatingLabels[level]}").FontSize(8).FontColor(Muted);
                                });
                            }
                        });
                    });

                    // 6. Two columns: about her | recommended action
                    col.Item().Row(row =>
                    {
                        row.Spacing(16);

                        row.RelativeItem().Background(CreamLight).Border(1).BorderColor(Line).Padding(16).Column(c =>
                        {
                            c.Spacing(6);
                            Display(c.Item().Text("About her")).FontSize(13).FontColor(Brown);
                            foreach (var a in context)
                            {
                                c.Item().BorderBottom(0.5f).BorderColor(Line).PaddingBottom(5).Row(r =>
                                {
                                    r.RelativeItem().Text(a.Question.Text).FontSize(9);
                                    r.ConstantItem(90).AlignRight().Text(a.TextValue ?? "-").Bold().FontSize(9).FontColor(Brown);
                                });
                            }
                        });

                        row.RelativeItem().Background(Sand).Padding(16).Column(c =>
                        {
                            c.Spacing(6);
                            Display(c.Item().Text("Recommended action")).FontSize(13).FontColor(Brown);
                            c.Item().Text(summary);
                        });
                    });
                });

                page.Footer().PaddingHorizontal(36).PaddingBottom(18).Row(r =>
                {
                    r.RelativeItem().Text("For clinician records only. Prototype: wording awaiting medical review.").FontSize(8).FontColor(Muted);
                    r.AutoItem().Text(t =>
                    {
                        t.Span("Page ").FontSize(8).FontColor(Muted);
                        t.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    });
                });
            });
        }).GeneratePdf();
    }

    // =====================================================================================
    // PATIENT HANDOUT (only needs the screening with its Patient loaded)
    // =====================================================================================
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
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(Cream);
                page.DefaultTextStyle(t => (BodyFont == null ? t : t.FontFamily(BodyFont)).FontSize(12).FontColor(Text).LineHeight(1.45f));

                page.Header().Element(h => GradientBand(h, "Your health journey home starts here."));

                page.Content().PaddingHorizontal(48).PaddingVertical(32).Column(col =>
                {
                    col.Spacing(18);

                    Display(col.Item().Text($"Hello {s.Patient.FullName}")).FontSize(24).FontColor(Brown);
                    col.Item().Text(text).FontSize(13);

                    // "Your next step" card with the QR code
                    col.Item().Background(CreamLight).Border(1).BorderColor(Line).Padding(20).Row(row =>
                    {
                        row.Spacing(20);
                        row.ConstantItem(130).Image(qrPng);
                        row.RelativeItem().AlignMiddle().Column(c =>
                        {
                            c.Spacing(6);
                            Display(c.Item().Text("Your next step")).FontSize(16).FontColor(Brown);
                            c.Item().Text("Scan the code with your phone camera, or visit:");
                            c.Item().Text("joinmandi.com").Bold().FontSize(14).FontColor(Terracotta);
                        });
                    });

                    col.Item().PaddingTop(8).Column(c =>
                    {
                        c.Spacing(4);
                        Display(c.Item().Text("Talk to Mandi  ·  Khuluma noMandi  ·  Thetha noMandi  ·  Praat met Mandi")).FontSize(12).FontColor(Orange);
                        c.Item().Text("You are not alone  ·  Awuhleli wedwa  ·  Awukho wedwa  ·  Jy is nie alleen nie").Italic().FontSize(11).FontColor(Muted);
                    });
                });

                page.Footer().PaddingHorizontal(48).PaddingBottom(18)
                    .Text("Prototype: wording awaiting medical review.").FontSize(8).FontColor(Muted);
            });
        }).GeneratePdf();
    }

    // =====================================================================================
    // THE JOINMANDI GRADIENT BAND
    // QuestPDF can't draw gradients, so we fake one: 60 thin strips side by side, each a
    // slightly different colour between amber and rose. At normal size it looks smooth.
    // =====================================================================================
    private static void GradientBand(IContainer container, string subtitle)
    {
        container.Height(88).Layers(layers =>
        {
            layers.Layer().Row(row =>
            {
                const int strips = 60;
                for (int i = 0; i < strips; i++)
                    row.RelativeItem().Height(88).Background(GradientColourAt(i / (double)(strips - 1)));
            });

            layers.PrimaryLayer().PaddingHorizontal(36).AlignMiddle().Row(row =>
            {
                row.AutoItem().AlignMiddle().Text(t => Display(t.Span("Mandi")).FontSize(30).FontColor(Cream));
                row.RelativeItem().PaddingLeft(14).AlignMiddle().Column(c =>
                {
                    c.Item().Text("MenoScreen").Bold().FontSize(10).FontColor(Cream);
                    c.Item().Text(subtitle).FontSize(10).FontColor(Sun);
                });
            });
        });
    }

    // The same colour stops as --mandi-gradient in site.css: amber, orange, coral, rose
    private static readonly (double Pos, int R, int G, int B)[] Stops =
    {
        (0.00, 0xDE, 0xA5, 0x39),
        (0.35, 0xDC, 0x82, 0x48),
        (0.65, 0xD7, 0x6A, 0x5F),
        (1.00, 0xD3, 0x64, 0x75)
    };

    private static string GradientColourAt(double t)
    {
        for (int i = 0; i < Stops.Length - 1; i++)
        {
            var a = Stops[i];
            var b = Stops[i + 1];
            if (t <= b.Pos)
            {
                double f = (t - a.Pos) / (b.Pos - a.Pos);
                int r = (int)Math.Round(a.R + (b.R - a.R) * f);
                int g = (int)Math.Round(a.G + (b.G - a.G) * f);
                int bl = (int)Math.Round(a.B + (b.B - a.B) * f);
                return $"#{r:X2}{g:X2}{bl:X2}";
            }
        }
        return "#D36475";
    }
}