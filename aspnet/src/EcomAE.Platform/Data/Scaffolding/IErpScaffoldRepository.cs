namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// Unwired repository contract for Enterprise BOS EF Core ERP cutover.
/// Not registered in DI and must not be used for production reads/writes yet.
/// </summary>
public interface IErpScaffoldRepository
{
    Task<IReadOnlyList<ErpCashAccountStub>> ListCashAccountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpCashEntryStub>> ListCashEntriesAsync(long? accountId, CancellationToken cancellationToken = default);
}
