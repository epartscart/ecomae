namespace EcomAE.Platform.Search;

/// <summary>
/// Unwired OpenSearch contract for Enterprise BOS scaffolding.
/// Not registered in DI; PHP/UMAPI search remains authoritative.
/// </summary>
public interface IEnterpriseSearchScaffold
{
    Task<IReadOnlyList<string>> SearchAsync(string index, string query, CancellationToken cancellationToken = default);
}
