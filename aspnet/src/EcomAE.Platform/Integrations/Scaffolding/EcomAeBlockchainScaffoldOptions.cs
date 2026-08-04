namespace EcomAE.Platform.Integrations.Scaffolding;

/// <summary>
/// Blockchain integration scaffolding options (proof/integration layer only).
/// Not bound in <c>Program.cs</c>. Business SoR remains app DB — never blockchain-primary.
/// </summary>
public sealed class EcomAeBlockchainScaffoldOptions
{
    public const string SectionName = "EcomAe:Blockchain";

    public string Network { get; set; } = string.Empty;

    public string RpcUrl { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    /// <summary>Always false — blockchain must not become the business SoR.</summary>
    public bool UseAsBusinessSourceOfRecord { get; set; }
}
