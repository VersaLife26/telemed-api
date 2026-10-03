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
    private const string Watermark = "#55D32F2F";

    private static readonly byte[] Logo = LoadLogo();
    private static readonly TimeZoneInfo Colombo = IanaTimeZone.Find(PlatformPolicy.TimeZoneId);

    public byte[] Render(PrescriptionPdf p)
    {
        var qr = PngByteQRCodeHelper.GetQRCode(p.VerifyUrl, QRCodeGenerator.ECCLevel.M, 10);
        var signature = Decode(p.Signature, p.Id, "signature");
        var seal = Decode(p.Seal, p.Id, "seal");
        var doctorName = "Dr. " + DoctorName(p.DoctorName);
        var issued = TimeZoneInfo.ConvertTime(p.IssuedAt, Colombo).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

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
                        row.ConstantItem(22, Unit.Millimetre).AlignMiddle().Text("Rx").Bold().FontSize(36).FontColor(Colors.White);
                        row.RelativeItem().PaddingLeft(4, Unit.Millimetre).AlignMiddle().Column(doctor =>
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
                content.Item().Background(CardBackground).Padding(4, Unit.Millimetre).Column(card => PatientCard(card, p, issued));
                content.Item().PaddingTop(6, Unit.Millimetre).Text("Medication").Bold().FontSize(11).FontColor(Navy);
                content.Item().PaddingTop(3, Unit.Millimetre);
                for (var i = 0; i < p.Items.Count; i++)
                {
                    content.Item().PaddingBottom(5, Unit.Millimetre).Element(c => Medication(c, p.Items[i], i + 1));
                }

                if (p.Investigations.Count > 0)
                {
                    content.Item().PaddingTop(2, Unit.Millimetre).Text("Investigations").Bold().FontSize(11).FontColor(Navy);
                    content.Item().PaddingTop(3, Unit.Millimetre);
                    for (var i = 0; i < p.Investigations.Count; i++)
                    {
                        var index = i + 1;
                        var name = p.Investigations[i];
                        content.Item().PaddingBottom(2, Unit.Millimetre).Row(row =>
                        {
                            row.ConstantItem(10, Unit.Millimetre).Text($"I{index}").Bold().FontSize(11).FontColor(Navy);
                            row.RelativeItem().Text(name).FontSize(11);
                        });
                    }
                }
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
                        copy.Item().Text("I, the licensed medical practitioner above, confirm this is my intent, and authorise by this writing that the specified medicines are dispensed for use by the designated individual above-named.")
                            .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                        copy.Item().PaddingTop(1, Unit.Millimetre)
                            .Text($"Issued by VersaLife Telemedicine. A pharmacist can confirm this prescription by scanning the code.  ·  {p.Id}")
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

    private static void Medication(IContainer container, PrescriptionPdfItem item, int index)
    {
        var title = string.Join(" ", new[] { item.DrugName, item.Strength, item.Form }.Where(s => !string.IsNullOrWhiteSpace(s)));
        container.Row(row =>
        {
            row.ConstantItem(10, Unit.Millimetre).Text($"M{index}").Bold().FontSize(11).FontColor(Navy);
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(title).Bold().FontSize(11).FontColor(Colors.Grey.Darken4);
                if (item.IsGeneric)
                {
                    col.Item().Text("Generic").Italic().FontSize(8).FontColor(Colors.Grey.Darken1);
                }

                col.Item().PaddingTop(1, Unit.Millimetre).Text("Take").FontSize(8).FontColor(Colors.Grey.Darken1);
                foreach (var bullet in MedicationBullets(item))
                {
                    col.Item().Row(b =>
                    {
                        b.ConstantItem(4, Unit.Millimetre).Text("•").FontSize(9).FontColor(Navy);
                        b.RelativeItem().Text(bullet).FontSize(9);
                    });
                }
            });
        });
    }

    private static IEnumerable<string> MedicationBullets(PrescriptionPdfItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Dosage))
        {
            yield return item.Dosage.Trim();
        }

        if (!string.IsNullOrWhiteSpace(item.Frequency))
        {
            yield return item.Frequency.Trim();
        }

        yield return $"For {item.DurationDays} day{(item.DurationDays == 1 ? "" : "s")}";
        yield return $"Quantity {item.Quantity.ToString(CultureInfo.InvariantCulture)}";

        if (string.IsNullOrWhiteSpace(item.Instructions))
        {
            yield break;
        }

        foreach (var part in item.Instructions.Split(['\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    private static void PatientCard(ColumnDescriptor card, PrescriptionPdf p, string issued)
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
            values.RelativeItem(1).Text(issued).FontSize(11);
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
