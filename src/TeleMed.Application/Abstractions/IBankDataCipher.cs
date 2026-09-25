namespace TeleMed.Application.Abstractions;

public interface IBankDataCipher
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
