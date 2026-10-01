using TeleMed.Application.Notifications;

namespace TeleMed.Domain.Tests;

public class EmailHtmlLayoutTests
{
    private static readonly EmailBrand Brand = new(
        "VersaLife Health",
        "https://app.example/assets/logo.svg",
        "https://app.example/assets/logo-small.svg",
        "https://app.example");

    [Fact]
    public void Render_includes_logo_subject_and_escapes_html()
    {
        var html = EmailHtmlLayout.Render(Brand, "Test & subject", "Hello <world>\n\nFee: Rs. 100");

        html.ShouldContain("https://app.example/assets/logo.svg");
        html.ShouldContain("https://app.example/assets/logo-small.svg");
        html.ShouldContain("linear-gradient(90deg,#015591 0%,#50C898 100%)");
        html.ShouldContain("Test &amp; subject");
        html.ShouldContain("Hello &lt;world&gt;");
        html.ShouldContain("Fee: Rs. 100");
        html.ShouldContain("Open VersaLife");
    }

    [Fact]
    public void Render_uses_link_cta_when_body_contains_url()
    {
        var html = EmailHtmlLayout.Render(
            Brand,
            "Prescription ready",
            "Download: https://app.example/prescriptions/1");

        html.ShouldContain("Open link");
        html.ShouldContain("https://app.example/prescriptions/1");
    }
}
