namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// Unwired PostgreSQL migration contract for Enterprise BOS scaffolding.
/// Not registered in DI; MySQL/MariaDB bridge remains the current SoR.
/// </summary>
public interface IPostgresMigrationScaffold
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListPendingMigrationsAsync(CancellationToken cancellationToken = default);
}
