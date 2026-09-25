namespace TeleMed.Application.Abstractions;

public interface IOtpCodes
{
    string NewCode();
    string Hash(string destination, string code);
}
