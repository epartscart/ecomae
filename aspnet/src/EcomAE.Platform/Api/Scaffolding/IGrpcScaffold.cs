namespace EcomAE.Platform.Api.Scaffolding;

/// <summary>
/// Unwired gRPC contract for Enterprise BOS scaffolding.
/// Not registered in DI.
/// </summary>
public interface IGrpcScaffold
{
    Task PingAsync(CancellationToken cancellationToken = default);
}
