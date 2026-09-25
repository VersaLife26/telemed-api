namespace TeleMed.Infrastructure.Persistence.Seed;

internal static class SeedSql
{
    public static string Read(string fileName)
    {
        using var stream = typeof(SeedSql).Assembly.GetManifestResourceStream($"Seed.{fileName}")
            ?? throw new InvalidOperationException($"Seed script {fileName} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
