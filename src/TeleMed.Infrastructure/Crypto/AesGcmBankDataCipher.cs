using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TeleMed.Application.Abstractions;
using TeleMed.Infrastructure.Options;

namespace TeleMed.Infrastructure.Crypto;

// Ciphertext layout: base64(nonce[12] || tag[16] || ciphertext).
internal sealed class AesGcmBankDataCipher(IOptions<CryptoOptions> options) : IBankDataCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key = options.Value.BankDataKeyBytes()!;

    public string Encrypt(string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[NonceSize + TagSize + plain.Length];
        var nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        var input = Convert.FromBase64String(ciphertext);
        var plain = new byte[input.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(input.AsSpan(0, NonceSize), input.AsSpan(NonceSize + TagSize), input.AsSpan(NonceSize, TagSize), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
