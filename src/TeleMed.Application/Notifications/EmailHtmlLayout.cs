using System.Net;
using System.Text.RegularExpressions;

namespace TeleMed.Application.Notifications;

public sealed record EmailBrand(string ProductName, string HeaderLogoUrl, string MarkLogoUrl, string AppUrl);

/// <summary>
/// Wraps plain-text notification bodies in a branded HTML shell for SMTP clients.
/// </summary>
public static partial class EmailHtmlLayout
{
    private const string Primary = "#015591";
    private const string Accent = "#50C898";
    private const string Canvas = "#eef4f9";
    private const string Card = "#ffffff";
    private const string Muted = "#5c6b7a";

    public static string Render(EmailBrand brand, string subject, string plainBody)
    {
        var safeSubject = WebUtility.HtmlEncode(subject);
        var paragraphs = BuildParagraphs(plainBody);
        var ctaUrl = ExtractFirstUrl(plainBody);
        var year = DateTime.UtcNow.Year;

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <meta name="color-scheme" content="light" />
              <title>{safeSubject}</title>
            </head>
            <body style="margin:0;padding:0;background:{Canvas};font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#1a2b3c;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:{Canvas};padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;">
                      <tr>
                        <td align="center" style="padding:8px 0 24px;font-size:18px;font-weight:700;letter-spacing:-0.02em;color:{Primary};">
                          {WebUtility.HtmlEncode(brand.ProductName)}
                        </td>
                      </tr>
                      <tr>
                        <td style="background:{Card};border-radius:16px;border:1px solid #d8e6f2;overflow:hidden;box-shadow:0 8px 32px rgba(1,85,145,0.08);">
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                            <tr>
                              <td style="height:4px;background:linear-gradient(90deg,{Primary} 0%,{Accent} 100%);font-size:0;line-height:0;">&nbsp;</td>
                            </tr>
                            <tr>
                              <td style="padding:32px 28px 8px;">
                                <p style="margin:0 0 8px;font-size:12px;font-weight:600;letter-spacing:0.08em;text-transform:uppercase;color:{Accent};">{WebUtility.HtmlEncode(brand.ProductName)}</p>
                                <h1 style="margin:0;font-size:22px;line-height:1.35;font-weight:700;color:{Primary};">{safeSubject}</h1>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:8px 28px 28px;font-size:16px;line-height:1.65;color:#243447;">
                                {paragraphs}
                              </td>
                            </tr>
                            {(ctaUrl is null ? "" : CtaBlock(ctaUrl, "Open link"))}
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:24px 12px 0;font-size:12px;line-height:1.6;color:{Muted};">
                          <p style="margin:0 0 8px;">You received this email because of activity on your {WebUtility.HtmlEncode(brand.ProductName)} account.</p>
                          <p style="margin:0;">&copy; {year} VersaLife Health. Telemedicine for Sri Lanka.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string CtaBlock(string url, string label)
    {
        var safeUrl = WebUtility.HtmlEncode(url);
        var safeLabel = WebUtility.HtmlEncode(label);

        return $"""
          <tr>
            <td align="center" style="padding:0 28px 32px;">
              <table role="presentation" cellspacing="0" cellpadding="0" style="border-collapse:separate;">
                <tr>
                  <td align="center" bgcolor="{Primary}" style="border-radius:999px;background:{Primary};">
                    <a href="{safeUrl}" style="display:inline-block;color:#ffffff;text-decoration:none;font-size:15px;font-weight:600;line-height:1;padding:14px 22px;border-radius:999px;">{safeLabel}</a>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
          """;
    }

    private static string BuildParagraphs(string plainBody)
    {
        var chunks = plainBody.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (chunks.Length == 0)
        {
            return "<p style=\"margin:0;\">&nbsp;</p>";
        }

        return string.Concat(chunks.Select(chunk =>
        {
            var withBreaks = WebUtility.HtmlEncode(chunk).Replace("\n", "<br />", StringComparison.Ordinal);
            return $"<p style=\"margin:0 0 16px;\">{withBreaks}</p>";
        }));
    }

    private static string? ExtractFirstUrl(string text)
    {
        var match = UrlPattern().Match(text);
        return match.Success ? match.Value : null;
    }

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();
}
