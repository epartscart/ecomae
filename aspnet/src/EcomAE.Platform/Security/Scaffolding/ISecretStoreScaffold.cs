namespace EcomAE.Platform.Security.Scaffolding;

/// <summary>
/// Unwired secret-store contract for Enterprise BOS scaffolding.
/// Not registered in DI; CloudPanel env files remain authoritative.
/// </summary>
public interface ISecretStoreScaffold
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}
