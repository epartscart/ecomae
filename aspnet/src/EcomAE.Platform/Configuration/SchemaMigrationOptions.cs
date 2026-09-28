namespace EcomAE.Platform.Configuration;

public sealed class SchemaMigrationOptions
{
    public const string SectionName = "EcomAE:SchemaMigrations";

    public bool ApplyOnStartup { get; set; }

    public int LockTimeoutSeconds { get; set; } = 30;
}
