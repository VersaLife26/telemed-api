namespace TeleMed.Application.Abstractions;

public sealed record EmailAttachment(string FileName, byte[] Content, string ContentType = "application/pdf");
