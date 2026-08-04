namespace EcomAE.Platform.Security.Scaffolding;

/// <summary>
/// Vault / Key Vault scaffolding options for future secret materialization.
/// Not bound in <c>Program.cs</c>; CloudPanel env files remain current secret source.
/// Never commit credentials into this options type or PR comments.
/// </summary>
public sealed class EcomAeVaultScaffoldOptions
{
    public const string SectionName = "EcomAe:Vault";

    public string Provider { get; set; } = "hashicorp-vault";

    public string Address { get; set; } = string.Empty;

    public string SecretsMount { get; set; } = "secret";

    public bool Enabled { get; set; }

    /// <summary>Always false until staging secret-parity evidence exists.</summary>
    public bool ReplaceEnvFileSecrets { get; set; }
}
