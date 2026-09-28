namespace EcomAE.Platform.Configuration;

public sealed class DataProtectionOptions
{
    public const string SectionName = "EcomAE:DataProtection";

    public string KeyDirectory { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = "EcomAE.Platform";
}
