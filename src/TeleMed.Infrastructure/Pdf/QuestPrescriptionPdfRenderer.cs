using System.Globalization;
using Microsoft.Extensions.Logging;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TeleMed.Application.Abstractions;
using TeleMed.Domain.Rules;

namespace TeleMed.Infrastructure.Pdf;

internal sealed class QuestPrescriptionPdfRenderer(ILogger<QuestPrescriptionPdfRenderer> logger) : IPrescriptionPdfRenderer
{
    private const string Navy = "#015591";
    private const string Mint = "#50C898";
    private const string CardBackground = "#E8F2FA";
    private const string StripedRow = "#F5F8FC";
    private const string Watermark = "#55D32F2F";

    private static readonly byte[] Logo = LoadLogo();
    private static readonly TimeZoneInfo Colombo = IanaTimeZone.Find(PlatformPolicy.TimeZoneId);

    public byte[] Render(PrescriptionPdf p)
    {
        var qr = PngByteQRCodeHelper.GetQRCode(p.VerifyUrl, QRCodeGenerator.ECCLevel.M, 10);
        var signature = Decode(p.Signature, p.Id, "signature");
        var seal = Decode(p.Seal, p.Id, "seal");
        var doctorName = "Dr. " + DoctorName(p.DoctorName);

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(14, Unit.Millimetre);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Column(header =>
            {
                header.Item().Row(row =>
                {
                    row.ConstantItem(16, Unit.Millimetre).Image(Logo);
                    row.ConstantItem(4, Unit.Millimetre);
                    row.RelativeItem().Column(brand =>
                    {
                        brand.Item().Text("VersaLife").Bold().FontSize(16).FontColor(Navy);
                        brand.Item().Text("Telemedicine").FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    row.RelativeItem().AlignRight().Column(title =>
                    {
                        title.Item().AlignRight().Text("PRESCRIPTION").Bold().FontSize(11).FontColor(Navy);
                        title.Item().AlignRight().Text("Sri Lanka").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
                header.Item().PaddingTop(3, Unit.Millimetre).LineHorizontal(1.6f, Unit.Millimetre).LineColor(Mint);
            });

            page.Content().PaddingTop(6, Unit.Millimetre).Column(content =>
            {
                content.Spacing(2, Unit.Millimetre);
                content.Item().Text(doctorName).Bold().FontSize(13).FontColor(Navy);
                foreach (var line in p.DoctorQualifications.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    content.Item().Text(line);
                }

                content.Item().Text($"SLMC Registration No. {p.DoctorSlmc}").Bold().FontColor(Navy);
                content.Item().PaddingTop(2, Unit.Millimetre).Background(CardBackground).Padding(4, Unit.Millimetre).Column(card => PatientCard(card, p));
                content.Item().PaddingTop(4, Unit.Millimetre).Text("Prescription").Bold().FontSize(14).FontColor(Navy);
                content.Item().Element(c => Items(c, p.Items));
                content.Item().PaddingTop(12, Unit.Millimetre).Row(row =>
                {
                    row.ConstantItem(62, Unit.Millimetre).Column(sign =>
                    {
                        var box = sign.Item().Height(20, Unit.Millimetre);
                        if (signature is not null)
                        {
                            box.AlignCenter().AlignMiddle().Image(signature).FitArea();
                        }

                        sign.Item().LineHorizontal(0.3f, Unit.Millimetre).LineColor(Navy);
                        sign.Item().PaddingTop(1.5f, Unit.Millimetre).Text("Doctor's signature").FontSize(8).FontColor(Colors.Grey.Darken2);
                        sign.Item().Text(doctorName).Bold().FontSize(8).FontColor(Navy);
                    });
                    row.ConstantItem(8, Unit.Millimetre);
                    row.ConstantItem(28, Unit.Millimetre).Column(stamp =>
                    {
                        if (seal is not null)
                        {
                            stamp.Item().Height(28, Unit.Millimetre).AlignCenter().AlignMiddle().Image(seal).FitArea();
                            stamp.Item().AlignCenter().Text("Stamp").FontSize(7).FontColor(Colors.Grey.Darken1);
                        }
                    });
                    row.RelativeItem();
                    row.ConstantItem(36, Unit.Millimetre).Column(code =>
                    {
                        code.Item().AlignCenter().Width(28, Unit.Millimetre).Image(qr);
                        code.Item().AlignCenter().Text("Scan to verify").FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            page.Footer().Column(footer =>
            {
                footer.Item().LineHorizontal(0.6f, Unit.Millimetre).LineColor(Mint);
                footer.Item().PaddingTop(1.5f, Unit.Millimetre).AlignCenter()
                    .Text("Issued by VersaLife Telemedicine. A pharmacist can confirm this prescription by scanning the code.")
                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                footer.Item().AlignCenter().Text($"Prescription {p.Id}").FontSize(7).FontColor(Colors.Grey.Darken1);
            });

            if (p.IsTest)
            {
                page.Foreground().AlignCenter().AlignMiddle().Text("TEST – NOT VALID").Bold().FontSize(48).FontColor(Watermark);
            }
        })).GeneratePdf();
    }

    private static void PatientCard(ColumnDescriptor card, PrescriptionPdf p)
    {
        card.Item().Row(labels =>
        {
            labels.RelativeItem(3).Text("PATIENT").FontSize(7).FontColor(Navy);
            labels.RelativeItem(1).Text("AGE").FontSize(7).FontColor(Navy);
            labels.RelativeItem(1).Text("DATE").FontSize(7).FontColor(Navy);
        });
        card.Item().Row(values =>
        {
            values.RelativeItem(3).Text(p.PatientName).Bold().FontSize(11);
            values.RelativeItem(1).Text(p.PatientAgeYears is { } age ? $"{age} years" : "-").FontSize(11);
            values.RelativeItem(1).Text(TimeZoneInfo.ConvertTime(p.IssuedAt, Colombo).ToString("dd MMM yyyy", CultureInfo.InvariantCulture)).FontSize(11);
        });

        if (p.PatientSex is not null || p.PatientWeightKg is not null)
        {
            card.Item().PaddingTop(2, Unit.Millimetre).Row(labels =>
            {
                labels.RelativeItem(3).Text("SEX").FontSize(7).FontColor(Navy);
                labels.RelativeItem(2).Text("WEIGHT").FontSize(7).FontColor(Navy);
            });
            card.Item().Row(values =>
            {
                values.RelativeItem(3).Text(p.PatientSex ?? "-").FontSize(11);
                values.RelativeItem(2).Text(p.PatientWeightKg is { } kg ? $"{kg.ToString("0.#", CultureInfo.InvariantCulture)} kg" : "-").FontSize(11);
            });
        }

        if (!string.IsNullOrWhiteSpace(p.PatientAllergies))
        {
            card.Item().PaddingTop(2, Unit.Millimetre).Text("ALLERGIES").FontSize(7).FontColor(Navy);
            card.Item().Text(p.PatientAllergies.Trim());
        }
    }

    private static void Items(IContainer container, IReadOnlyList<PrescriptionPdfItem> items) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(48);
                columns.RelativeColumn(22);
                columns.RelativeColumn(20);
                columns.RelativeColumn(36);
                columns.RelativeColumn(22);
                columns.RelativeColumn(16);
                columns.RelativeColumn(14);
            });

            table.Header(header =>
            {
                foreach (var title in new[] { "Medicine", "Strength", "Form", "Dosage / Frequency", "Duration", "Qty", "Generic" })
                {
                    header.Cell().Background(Navy).Padding(2, Unit.Millimetre).AlignCenter().Text(title).Bold().FontSize(7).FontColor(Colors.White);
                }
            });

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string background = i % 2 == 0 ? StripedRow : "#FFFFFF";
                IContainer Cell() => table.Cell().Background(background).BorderBottom(0.5f).BorderColor("#D2DCE6").Padding(1.5f, Unit.Millimetre);

                Cell().Text(item.DrugName).FontSize(9);
                Cell().AlignCenter().Text(item.Strength).FontSize(9);
                Cell().AlignCenter().Text(item.Form).FontSize(9);
                Cell().Text($"{item.Dosage} / {item.Frequency}").FontSize(9);
                Cell().AlignCenter().Text($"{item.DurationDays} days").FontSize(9);
                Cell().AlignCenter().Text(item.Quantity.ToString(CultureInfo.InvariantCulture)).FontSize(9);
                Cell().AlignCenter().Text(item.IsGeneric ? "Yes" : "").FontSize(9);
                if (!string.IsNullOrWhiteSpace(item.Instructions))
                {
                    table.Cell().ColumnSpan(7).Background(background).PaddingHorizontal(4, Unit.Millimetre).PaddingBottom(1.5f, Unit.Millimetre)
                        .Text(item.Instructions).Italic().FontSize(8).FontColor(Colors.Grey.Darken2);
                }
            }
        });

    // A stamp that passed upload sniffing can still be corrupt; the prescription is rendered without it rather than not at all.
    private Image? Decode(byte[]? bytes, Guid prescriptionId, string kind)
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
            logger.LogWarning(ex, "Rendering prescription {PrescriptionId} without the doctor's {Kind}: the image could not be decoded.", prescriptionId, kind);
            return null;
        }
    }

    // Names stored in lower case ("amara perera") print title-cased; anything with capitals is left alone.
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
