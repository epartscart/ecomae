namespace EcomAE.Platform.Erp;

public interface IErpFitOutContractTermsWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutContractTermsSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutContractTermsSaveRequest(
    long ContractId,
    decimal AdvancePercent,
    decimal RetentionPercent,
    int WarrantyMonths);

public sealed class ErpFitOutContractTermsWriteService : IErpFitOutContractTermsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutContractTermsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutContractTermsSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ContractId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var advance = decimal.Round(Math.Clamp(request.AdvancePercent, 0, 100), 4, MidpointRounding.AwayFromZero);
        var retention = decimal.Round(Math.Clamp(request.RetentionPercent, 0, 100), 4, MidpointRounding.AwayFromZero);
        var warranty = Math.Clamp(request.WarrantyMonths, 0, 1_200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_contract_terms` (
                `contract_id` bigint NOT NULL,
                `advance_percent` decimal(8,4) NOT NULL DEFAULT 0,
                `retention_percent` decimal(8,4) NOT NULL DEFAULT 0,
                `warranty_months` int NOT NULL DEFAULT 0,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`contract_id`)
            ) ENGINE=InnoDB
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO `ecomae_fitout_contract_terms`
                (`contract_id`,`advance_percent`,`retention_percent`,`warranty_months`)
            VALUES (?,?,?,?)
            ON DUPLICATE KEY UPDATE
                `advance_percent`=VALUES(`advance_percent`),
                `retention_percent`=VALUES(`retention_percent`),
                `warranty_months`=VALUES(`warranty_months`),
                `updated_at_utc`=CURRENT_TIMESTAMP
            """,
            cancellationToken,
            request.ContractId, advance, retention, warranty).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fit-out contract terms saved", request.ContractId);
    }
}
