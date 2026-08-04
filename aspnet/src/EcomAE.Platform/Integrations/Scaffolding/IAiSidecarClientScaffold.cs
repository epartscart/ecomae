namespace EcomAE.Platform.Integrations.Scaffolding;

/// <summary>
/// Unwired AI-sidecar client contract (REST/gRPC to Python FastAPI).
/// Not registered in DI; Python remains AI-only.
/// </summary>
public interface IAiSidecarClientScaffold
{
    Task<string?> InferAsync(string payloadJson, CancellationToken cancellationToken = default);
}
