namespace EcomAE.Platform.Api.Scaffolding;

/// <summary>
/// Unwired GraphQL contract for Enterprise BOS scaffolding.
/// Not registered in DI; REST remains the default API surface.
/// </summary>
public interface IGraphQlScaffold
{
    Task<string> ExecuteAsync(string query, CancellationToken cancellationToken = default);
}
