using System.Globalization;
using Microsoft.Extensions.Logging;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Enums;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Pdf;

internal sealed class QuestMedicalReportPdfRenderer(ILogger<QuestMedicalReportPdfRenderer> logger) : IMedicalReportPdfRenderer
{
    private const string Navy = "#015591";
    private const string Mint = "#50C898";
    private const string CardBackground = "#E8F2FA";
    private const string Watermark = "#55D32F2F";

    private static readonly byte[] Logo = LoadLogo();
    private static readonly TimeZoneInfo Colombo = IanaTimeZone.Find(PlatformPolicy.TimeZoneId);

    public byte[] Render(MedicalReportPdf p)
    {
        var qr = PngByteQRCodeHelper.GetQRCode(p.VerifyUrl, QRCodeGenerator.ECCLevel.M, 10);
        var signature = Decode(p.Signature, p.Id, "signature");
        var seal = Decode(p.Seal, p.Id, "seal");
        var doctorName = "Dr. " + DoctorName(p.DoctorName);
        var issued = TimeZoneInfo.ConvertTime(p.IssuedAt, Colombo).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        var visit = p.VisitDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Column(header =>
            {
                header.Item().Background(Navy).PaddingHorizontal(14, Unit.Millimetre).PaddingVertical(10, Unit.Millimetre)
                    .Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Column(doctor =>
                        {
                            doctor.Item().Text(doctorName).Bold().FontSize(16).FontColor(Colors.White);
                            foreach (var line in p.DoctorQualifications.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                doctor.Item().Text(line).FontSize(9).FontColor("#D7E8F4");
                            }

                            doctor.Item().PaddingTop(1, Unit.Millimetre)
                                .Text($"SLMC {p.DoctorSlmc}  ·  Issued {issued}")
                                .FontSize(9).FontColor("#E8F6F0");
                            doctor.Item().Text("VersaLife Telemedicine · Sri Lanka").FontSize(8).FontColor("#B8D4E8");
                        });
                        row.ConstantItem(32, Unit.Millimetre).AlignRight().AlignMiddle().Column(code =>
                        {
                            code.Item().Background(Colors.White).Padding(2, Unit.Millimetre).Width(26, Unit.Millimetre).Image(qr);
                            code.Item().AlignCenter().PaddingTop(1, Unit.Millimetre)
                                .Text("Scan to verify").FontSize(7).FontColor("#D7E8F4");
                        });
                    });
                header.Item().Height(2.2f, Unit.Millimetre).Background(Mint);
            });

            page.Content().PaddingHorizontal(14, Unit.Millimetre).PaddingTop(6, Unit.Millimetre).Column(content =>
            {
                content.Item().Background(CardBackground).Padding(4, Unit.Millimetre).Column(card =>
                {
                    card.Item().Row(labels =>
                    {
                        labels.RelativeItem(3).Text("PATIENT").FontSize(7).FontColor(Navy);
                        labels.RelativeItem(1).Text("AGE").FontSize(7).FontColor(Navy);
                        labels.RelativeItem(1).Text("VISIT").FontSize(7).FontColor(Navy);
                    });
                    card.Item().Row(values =>
                    {
                        values.RelativeItem(3).Text(p.PatientName).Bold().FontSize(11);
                        values.RelativeItem(1).Text(p.PatientAgeYears is { } age ? $"{age} years" : "-").FontSize(11);
                        values.RelativeItem(1).Text(visit).FontSize(11);
                    });
                    if (p.PatientSex is not null)
                    {
                        card.Item().PaddingTop(2, Unit.Millimetre).Text("SEX").FontSize(7).FontColor(Navy);
                        card.Item().Text(p.PatientSex).FontSize(11);
                    }
                });

                content.Item().PaddingTop(7, Unit.Millimetre).Text("Medical report").Bold().FontSize(16).FontColor(Navy);
                content.Item().PaddingTop(1, Unit.Millimetre)
                    .Text("Issued after a VersaLife video consultation.")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);

                content.Item().PaddingTop(5, Unit.Millimetre).Text(p.Addressee + ",").Italic().FontSize(11);

                content.Item().PaddingTop(3, Unit.Millimetre)
                    .Text($"{p.PatientName} was examined by me on {visit} by video consultation. I confirm that this person is under my care for the findings below.")
                    .FontSize(10.5f).LineHeight(1.45f);

                Section(content, "Clinical impression", p.ClinicalImpression);

                if (!string.IsNullOrWhiteSpace(p.Findings))
                {
                    Section(content, "Findings", p.Findings);
                }

                if (!string.IsNullOrWhiteSpace(p.Advice))
                {
                    Section(content, "Advice", p.Advice);
                }

                if (p.Fitness != FitnessForWork.NotAssessed)
                {
                    content.Item().PaddingTop(5, Unit.Millimetre)
                        .Background(CardBackground)
                        .Padding(4, Unit.Millimetre)
                        .Column(box =>
                        {
                            box.Item().Text("FITNESS FOR WORK").FontSize(7).FontColor(Navy);
                            box.Item().PaddingTop(1, Unit.Millimetre).Text(FitnessHeadline(p)).Bold().FontSize(11).FontColor(Navy);
                            foreach (var line in FitnessLines(p))
                            {
                                box.Item().PaddingTop(1, Unit.Millimetre).Text(line).FontSize(10).LineHeight(1.4f);
                            }
                        });
                }

                content.Item().PaddingTop(6, Unit.Millimetre).Text("Yours truly,").FontSize(10);
            });

            page.Footer().PaddingHorizontal(14, Unit.Millimetre).PaddingBottom(10, Unit.Millimetre).Column(footer =>
            {
                footer.Item().PaddingBottom(4, Unit.Millimetre).Row(row =>
                {
                    row.ConstantItem(62, Unit.Millimetre).Column(sign =>
                    {
                        var box = sign.Item().Height(18, Unit.Millimetre);
                        if (signature is not null)
                        {
                            box.AlignLeft().AlignBottom().Image(signature).FitArea();
                        }

                        sign.Item().LineHorizontal(0.3f, Unit.Millimetre).LineColor(Navy);
                        sign.Item().PaddingTop(1.2f, Unit.Millimetre).Text("Doctor's signature").FontSize(8).FontColor(Colors.Grey.Darken2);
                        sign.Item().Text(doctorName).Bold().FontSize(8).FontColor(Navy);
                    });
                    row.RelativeItem();
                    row.ConstantItem(32, Unit.Millimetre).AlignRight().Column(stamp =>
                    {
                        var box = stamp.Item().AlignRight().Height(24, Unit.Millimetre);
                        if (seal is not null)
                        {
                            box.AlignRight().AlignBottom().Image(seal).FitArea();
                        }

                        stamp.Item().AlignRight().Text("Seal").FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                });
                footer.Item().LineHorizontal(0.4f, Unit.Millimetre).LineColor("#D2DCE6");
                footer.Item().PaddingTop(3, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Column(copy =>
                    {
                        copy.Item().Text("I, the licensed medical practitioner above, confirm that this report is a true record of the consultation named, and authorise its use by the patient for the purpose issued.")
                            .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                        copy.Item().PaddingTop(1, Unit.Millimetre)
                            .Text($"Issued by VersaLife Telemedicine. Confirm this report by scanning the code.  ·  {p.Id}")
                            .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(22, Unit.Millimetre).AlignRight().AlignMiddle().Height(16, Unit.Millimetre).Image(Logo).FitArea();
                });
            });

            if (p.IsTest)
            {
                page.Foreground().AlignCenter().AlignMiddle().Text("TEST – NOT VALID").Bold().FontSize(48).FontColor(Watermark);
            }
        })).GeneratePdf();
    }

    private static void Section(ColumnDescriptor content, string heading, string body)
    {
        content.Item().PaddingTop(5, Unit.Millimetre).Text(heading.ToUpperInvariant()).FontSize(7).FontColor(Navy);
        content.Item().PaddingTop(1, Unit.Millimetre).Text(body).FontSize(10.5f).LineHeight(1.45f);
    }

    private static string FitnessHeadline(MedicalReportPdf p) => p.Fitness switch
    {
        FitnessForWork.Fit => "Fit for usual work",
        FitnessForWork.Unfit => "Unfit for work and routine duties",
        FitnessForWork.Restricted => "Fit for work with restrictions",
        _ => "Fitness for work",
    };

    private static IEnumerable<string> FitnessLines(MedicalReportPdf p)
    {
        if (p.Fitness == FitnessForWork.Unfit)
        {
            yield return $"I have requested {p.PatientName} to refrain from commitments and routine work.";
        }

        if (p.LeaveFrom is { } from && p.LeaveUntil is { } until)
        {
            yield return $"Medical leave: {Fmt(from)} until {Fmt(until)} inclusive.";
        }

        if (p.ReturnToWorkOn is { } resume)
        {
            yield return $"Expected to resume work on {Fmt(resume)}.";
        }

        if (!string.IsNullOrWhiteSpace(p.FitnessNotes))
        {
            yield return p.FitnessNotes.Trim();
        }
    }

    private static string Fmt(DateOnly date) => date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    private Image? Decode(byte[]? bytes, Guid reportId, string kind)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return Image.FromBinaryData(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rendering medical report {ReportId} without the doctor's {Kind}: the image could not be decoded.", reportId, kind);
            return null;
        }
    }

    private static string DoctorName(string name)
    {
        name = name.Trim();
        foreach (var prefix in new[] { "dr. ", "dr " })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..].Trim();
                break;
            }
        }

        return name != name.ToLowerInvariant()
            ? name
            : string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static byte[] LoadLogo()
    {
        using var stream = typeof(QuestPrescriptionPdfRenderer).Assembly.GetManifestResourceStream("Pdf.versalife-logo.png")!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
