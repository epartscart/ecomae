namespace EcomAE.Platform.Integrations.Scaffolding;

/// <summary>
/// Unwired blockchain integration contract (proof layer only).
/// Not registered in DI; business SoR remains app DB.
/// </summary>
public interface IBlockchainIntegrationScaffold
{
    Task<string?> GetProofAsync(string reference, CancellationToken cancellationToken = default);
}
