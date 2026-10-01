using System.Text;
using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class FileSignatureTests
{
    public static TheoryData<byte[], string?> Samples() => new()
    {
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01], FileSignature.Jpeg },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D], FileSignature.Png },
        { Encoding.ASCII.GetBytes("RIFF\x10\x00\x00\x00WEBP"), FileSignature.Webp },
        { Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ"), FileSignature.Pdf },
        { Encoding.ASCII.GetBytes("RIFF\x10\x00\x00\x00WAVE"), null },
        { [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D], FileSignature.Mp4 },
        { [0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00], FileSignature.Webm },
        { Encoding.ASCII.GetBytes("<html><body>"), null },
        { [0x89, 0x50, 0x4E], null },
        { [], null },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void Detects_content_type_from_magic_bytes(byte[] header, string? expected)
    {
        FileSignature.DetectContentType(header).ShouldBe(expected);
    }

    [Fact]
    public void Images_exclude_pdf()
    {
        FileSignature.Images.ShouldBe([FileSignature.Jpeg, FileSignature.Png, FileSignature.Webp], ignoreOrder: true);
    }

    [Theory]
    [InlineData(FileSignature.Jpeg)]
    [InlineData(FileSignature.Png)]
    [InlineData(FileSignature.Webp)]
    [InlineData(FileSignature.Pdf)]
    [InlineData(FileSignature.Mp4)]
    [InlineData(FileSignature.Webm)]
    public void Extension_round_trips(string contentType)
    {
        FileSignature.ContentTypeFromExtension(FileSignature.Extension(contentType)).ShouldBe(contentType);
    }
}
