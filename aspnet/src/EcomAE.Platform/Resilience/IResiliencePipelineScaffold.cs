namespace EcomAE.Platform.Resilience;

/// <summary>
/// Unwired Polly resilience pipeline contract for Enterprise BOS scaffolding.
/// Not registered in DI.
/// </summary>
public interface IResiliencePipelineScaffold
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
