using System.ComponentModel.DataAnnotations;

namespace TeleMed.Infrastructure.Options;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    [Required]
    public string ConnectionString { get; set; } = "";

    public bool MigrateOnStartup { get; set; }
}
