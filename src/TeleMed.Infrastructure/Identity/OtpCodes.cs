using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Identity;

internal sealed class OtpCodes(IOptions<OtpOptions> options) : IOtpCodes
{
    public string NewCode() =>
        options.Value.FixedCode is { Length: > 0 } fixedCode
            ? fixedCode
            : RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public string Hash(string destination, string code) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(options.Value.HmacKey),
            Encoding.UTF8.GetBytes($"{destination}\n{code}")));
}
