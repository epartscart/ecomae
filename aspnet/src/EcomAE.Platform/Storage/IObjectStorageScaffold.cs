namespace EcomAE.Platform.Storage;

/// <summary>
/// Unwired object-storage contract for Enterprise BOS scaffolding.
/// Not registered in DI; local/env file paths remain current.
/// </summary>
public interface IObjectStorageScaffold
{
    Task PutAsync(string key, Stream content, CancellationToken cancellationToken = default);

    Task<Stream?> GetAsync(string key, CancellationToken cancellationToken = default);
}
