using TeleMed.Domain.Rules;

namespace TeleMed.Domain.Tests;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("0771234567", "+94771234567")]
    [InlineData("771234567", "+94771234567")]
    [InlineData("94771234567", "+94771234567")]
    [InlineData("+94771234567", "+94771234567")]
    [InlineData("+94 77 123 4567", "+94771234567")]
    [InlineData("077-123-4567", "+94771234567")]
    [InlineData("077 123 4567", "+94771234567")]
    [InlineData("(0771) 234 567", "+94771234567")]
    [InlineData("0701234567", "+94701234567")]
    [InlineData("0751234567", "+94751234567")]
    [InlineData("0761234567", "+94761234567")]
    [InlineData("0781234567", "+94781234567")]
    public void Normalizes_sri_lankan_mobile_shapes(string input, string expected)
    {
        PhoneNumber.NormalizeSriLankanMobile(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("0771234567890")]
    [InlineData("0112345678")]
    [InlineData("abcdefghij")]
    [InlineData("0881234567")]
    public void Rejects_non_mobile_input(string? input)
    {
        PhoneNumber.NormalizeSriLankanMobile(input).ShouldBeNull();
    }

    [Theory]
    [InlineData("0771234567")]
    [InlineData("771234567")]
    [InlineData("94771234567")]
    public void Normalization_is_idempotent(string input)
    {
        var once = PhoneNumber.NormalizeSriLankanMobile(input);

        PhoneNumber.NormalizeSriLankanMobile(once).ShouldBe(once);
    }
}
