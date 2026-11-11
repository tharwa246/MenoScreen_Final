using MenoScreen.Models;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MenoScreen.Services;

// Her portal login, printed on her page of the pack. Never a password: a one-time setup
// code she uses to choose her own password.
public record PortalLogin(string Username, string SetupCode, string SetupUrl);

// THE PDFs: the patient page, the clinician summary, and the "screening pack" (both in one).
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
         // No fonts folder: the PDFs fall back to QuestPDF's built-in font
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
        }
    }

    // Use the rounded JoinMandi headline font for a piece of text (bold either way)
    private static TextSpanDescriptor Display(TextSpanDescriptor text) =>
        DisplayFont == null ? text.Bold() : text.FontFamily(DisplayFont).Bold();

    // The band wording (clinician summary, her handout) now lives in the database
    // (BandContent table). Callers fetch it with ContentService and pass it in.


    // =====================================================================================
    // THE DOCUMENTS YOU CAN DOWNLOAD
    // Each page is drawn by one "Compose..." method below, so a page looks the same
    // whether it's printed on its own or as part of the pack.
    // =====================================================================================

    // Clinician summary on its own
    public byte[] BuildClinicianSummary(Screening s, BandContent wording) =>
        Document.Create(doc => ComposeClinicianPage(doc, s, wording.ClinicianSummary)).GeneratePdf();

    // Her page on its own (also used by "screen without saving", which has no portal login)
    public byte[] BuildPatientHandout(Screening s, BandContent wording, PortalLogin? portal = null) =>
        Document.Create(doc => ComposePatientPage(doc, s, wording.PatientHandout, portal)).GeneratePdf();

    // THE SCREENING PACK: everything in one PDF.
    // Page 1 is hers to take home: her result, JoinMandi, and (if given) her portal login.
    // Page 2 is the clinician summary for her file.
    public byte[] BuildScreeningPack(Screening s, BandContent wording, PortalLogin? portal) =>
        Document.Create(doc =>
        {
            ComposePatientPage(doc, s, wording.PatientHandout, portal);
            ComposeClinicianPage(doc, s, wording.ClinicianSummary);
        }).GeneratePdf();

    // =====================================================================================
    // CLINICIAN SUMMARY PAGE (same layout as the Result page)
    // The screening must be loaded with Patient, and Answers including each Question.
    // =====================================================================================
    private static void ComposeClinicianPage(IDocumentContainer doc, Screening s, string summary)
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

        (string Fill, string Ink) band = BandColours.GetValueOrDefault(s.Band, (Muted, "#FFFFFF"));
        string how = s.Source == ScreeningSources.SelfScreen ? "QR self-screen" : "with a clinician";

        doc.Page(page =>
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
    }

    // =====================================================================================
    // HER PAGE: her result, her portal login (if given) and JoinMandi
    // Needs the screening with its Patient, and Answers including each Question.
    // =====================================================================================
    private static void ComposePatientPage(IDocumentContainer doc, Screening s, string text, PortalLogin? portal)
    {
        (string Fill, string Ink) band = BandColours.GetValueOrDefault(s.Band, (Muted, "#FFFFFF"));

        // The two symptom areas that affect her most (by percentage of the maximum)
        var topAreas = ScoringService.BlockBreakdown(s.Answers)
            .Where(b => b.Score > 0)
            .OrderByDescending(b => b.Percent)
            .Take(2)
            .Select(b => b.Block)
            .ToList();

        // QR codes: one to JoinMandi, one to the portal setup page. Neither link contains
        // her score, band or any health information.
        byte[] joinMandiQr = QrPng(LandingUrl);
        byte[]? portalQr = portal == null ? null : QrPng(portal.SetupUrl);

        doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.PageColor(Cream);
            page.DefaultTextStyle(t => (BodyFont == null ? t : t.FontFamily(BodyFont)).FontSize(11).FontColor(Text).LineHeight(1.4f));

            page.Header().Element(h => GradientBand(h, "Your health journey home starts here."));

            page.Content().PaddingHorizontal(44).PaddingVertical(24).Column(col =>
            {
                col.Spacing(14);

                Display(col.Item().Text($"Hello {s.Patient.FullName}")).FontSize(22).FontColor(Brown);
                col.Item().Text(text).FontSize(12);

                // 1. Her result
                col.Item().Background(CreamLight).Border(1).BorderColor(Line).Padding(16).Row(row =>
                {
                    row.Spacing(18);
                    row.ConstantItem(110).Column(c =>
                    {
                        c.Item().Text(t =>
                        {
                            Display(t.Span(s.MenoLikelihood.ToString())).FontSize(32).FontColor(Terracotta);
                            t.Span(" / 100").FontSize(11).FontColor(Muted);
                        });
                        c.Item().Text("Your score today").FontSize(8.5f).FontColor(Muted);
                    });
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Spacing(6);
                        c.Item().Row(r => r.AutoItem().Background(band.Fill).PaddingVertical(3).PaddingHorizontal(10)
                            .Text(s.Band).Bold().FontSize(10).FontColor(band.Ink));
                        if (topAreas.Any())
                            c.Item().Text($"What affects you most: {string.Join(" and ", topAreas)}.").FontSize(10);
                        c.Item().Text("Your clinician has gone through this result with you.").FontSize(9).FontColor(Muted);
                    });
                });

                // 2. Her portal login (only when the clinician included it)
                if (portal != null && portalQr != null)
                {
                    col.Item().Background(Sand).Padding(16).Row(row =>
                    {
                        row.Spacing(18);
                        row.ConstantItem(110).Image(portalQr);
                        row.RelativeItem().AlignMiddle().Column(c =>
                        {
                            c.Spacing(4);
                            Display(c.Item().Text("See your results online")).FontSize(15).FontColor(Brown);
                            c.Item().Text("Scan the code, or go to:").FontSize(10);
                            c.Item().Text(portal.SetupUrl).FontSize(8.5f).FontColor(Terracotta);
                            c.Item().PaddingTop(4).Text(t =>
                            {
                                t.Span("Username: ").FontSize(11);
                                t.Span(portal.Username).Bold().FontSize(12).FontColor(Brown);
                            });
                            c.Item().Text(t =>
                            {
                                t.Span("Setup code: ").FontSize(11);
                                t.Span(portal.SetupCode).Bold().FontSize(14).FontColor(Brown);
                            });
                            c.Item().PaddingTop(2).Text("The code works once and expires in 7 days. You'll choose your own password. Keep this page private.")
                                .FontSize(8.5f).FontColor(Muted);
                        });
                    });
                }

                // 3. JoinMandi
                col.Item().Background(CreamLight).Border(1).BorderColor(Line).Padding(16).Row(row =>
                {
                    row.Spacing(18);
                    row.ConstantItem(110).Image(joinMandiQr);
                    row.RelativeItem().AlignMiddle().Column(c =>
                    {
                        c.Spacing(6);
                        Display(c.Item().Text("Learn more with Mandi")).FontSize(15).FontColor(Brown);
                        c.Item().Text("Scan the code with your phone camera, or visit:");
                        c.Item().Text("joinmandi.com").Bold().FontSize(14).FontColor(Terracotta);
                    });
                });

                col.Item().PaddingTop(4).Column(c =>
                {
                    c.Spacing(4);
                    Display(c.Item().Text("Talk to Mandi  ·  Khuluma noMandi  ·  Thetha noMandi  ·  Praat met Mandi")).FontSize(12).FontColor(Orange);
                    c.Item().Text("You are not alone  ·  Awuhleli wedwa  ·  Awukho wedwa  ·  Jy is nie alleen nie").Italic().FontSize(10).FontColor(Muted);
                });
            });

            page.Footer().PaddingHorizontal(44).PaddingBottom(18)
                .Text("Prototype: wording awaiting medical review.").FontSize(8).FontColor(Muted);
        });
    }

    // A QR code as a PNG image
    private static byte[] QrPng(string text)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(10);
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