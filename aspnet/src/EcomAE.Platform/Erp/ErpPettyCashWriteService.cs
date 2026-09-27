namespace EcomAE.Platform.Erp;

public interface IErpPettyCashWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPettyCashWriteRequest request,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPettyCashWriteRequest(
    string? Name = null,
    long AccountId = 0,
    decimal FloatAmount = 0,
    long CustodianUserId = 0);

public sealed class ErpPettyCashWriteService : IErpPettyCashWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlLedgerWriteService _ledger;

    public ErpPettyCashWriteService(IErpWriteConnectionFactory connections, IErpGlLedgerWriteService ledger)
    {
        _connections = connections;
        _ledger = ledger;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPettyCashWriteRequest request,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || string.IsNullOrWhiteSpace(request.Name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Petty cash name is required.");
        }

        var accountId = request.AccountId;
        if (accountId <= 0)
        {
            accountId = await _ledger.CreateCashAccountAsync(
                new ErpCashAccountInput
                {
                    Name = request.Name.Trim(),
                    AccountType = "cash",
                    OpeningBalance = request.FloatAmount,
                },
                adminId,
                cancellationToken).ConfigureAwait(false);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_petty_cash` (`name`,`account_id`,`float_amount`,`custodian_user_id`,`time_created`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            request.Name.Trim(),
            accountId,
            ErpTaxAmountCalculator.Round2(request.FloatAmount),
            request.CustodianUserId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Petty cash float created", id);
    }
}
