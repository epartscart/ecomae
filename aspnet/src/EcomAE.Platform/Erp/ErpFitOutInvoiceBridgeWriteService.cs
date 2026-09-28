namespace EcomAE.Platform.Erp;

public interface IErpFitOutInvoiceBridgeWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutInvoiceBridgeSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutInvoiceBridgeSaveRequest(
    long Id = 0,
    long ProjectId = 0,
    long InvoiceId = 0,
    string? Stage = null,
    string? Reference = null,
    bool ConfirmWrites = false);

public sealed class ErpFitOutInvoiceBridgeWriteService : IErpFitOutInvoiceBridgeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutInvoiceBridgeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFitOutInvoiceBridgeSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId <= 0 || request.InvoiceId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project and invoice ids are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var stage = Normalize(request.Stage, "progress_claim");
        var reference = Clip(request.Reference, 120);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var invoiceTableExists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema=DATABASE() AND table_name=?
                """),
            cancellationToken,
            "epc_einvoice_documents").ConfigureAwait(false);
        if (invoiceTableExists == 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "The tenant e-invoice table is unavailable.");
        }

        var invoice = await LoadInvoiceAsync(
            connection,
            request.InvoiceId,
            cancellationToken).ConfigureAwait(false);
        if (invoice is null || invoice.Active == 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "The active invoice was not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            """
            CREATE TABLE IF NOT EXISTS `ecomae_fitout_invoice_links` (
                `id` bigint NOT NULL AUTO_INCREMENT,
                `project_id` bigint NOT NULL,
                `invoice_id` bigint NOT NULL,
                `invoice_number` varchar(64) NOT NULL,
                `stage` varchar(32) NOT NULL,
                `reference` varchar(120) NOT NULL DEFAULT '',
                `subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                `total_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                `total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
                `currency_code` varchar(8) NOT NULL DEFAULT 'AED',
                `invoice_status` varchar(24) NOT NULL DEFAULT '',
                `created_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at_utc` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_ecomae_fitout_invoice_link` (`project_id`,`invoice_id`),
                KEY `ix_ecomae_fitout_invoice_project` (`project_id`,`stage`)
            ) ENGINE=InnoDB
            """,
            cancellationToken).ConfigureAwait(false);

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `ecomae_fitout_invoice_links`
                    SET `project_id`=?,`invoice_id`=?,`invoice_number`=?,`stage`=?,
                        `reference`=?,`subtotal_ex_vat`=?,`total_vat`=?,
                        `total_incl_vat`=?,`currency_code`=?,`invoice_status`=?,
                        `updated_at_utc`=UTC_TIMESTAMP()
                    WHERE `id`=?
                    """),
                cancellationToken,
                request.ProjectId,
                request.InvoiceId,
                invoice.Number,
                stage,
                reference,
                invoice.Subtotal,
                invoice.Vat,
                invoice.Total,
                invoice.Currency,
                invoice.Status,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Fit-out invoice link saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `ecomae_fitout_invoice_links`
                    (`project_id`,`invoice_id`,`invoice_number`,`stage`,`reference`,
                     `subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`currency_code`,
                     `invoice_status`)
                VALUES (?,?,?,?,?,?,?,?,?,?)
                ON DUPLICATE KEY UPDATE
                    `invoice_number`=VALUES(`invoice_number`),
                    `stage`=VALUES(`stage`),
                    `reference`=VALUES(`reference`),
                    `subtotal_ex_vat`=VALUES(`subtotal_ex_vat`),
                    `total_vat`=VALUES(`total_vat`),
                    `total_incl_vat`=VALUES(`total_incl_vat`),
                    `currency_code`=VALUES(`currency_code`),
                    `invoice_status`=VALUES(`invoice_status`),
                    `updated_at_utc`=UTC_TIMESTAMP()
                """),
            cancellationToken,
            request.ProjectId,
            request.InvoiceId,
            invoice.Number,
            stage,
            reference,
            invoice.Subtotal,
            invoice.Vat,
            invoice.Total,
            invoice.Currency,
            invoice.Status).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Fit-out invoice linked", request.InvoiceId);
    }

    private static string Normalize(string? value, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return normalized is "advance" or "progress_claim" or "retention" or "final"
            ? normalized
            : fallback;
    }

    private static string Clip(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static async Task<InvoiceSnapshot?> LoadInvoiceAsync(
        System.Data.Common.DbConnection connection,
        long invoiceId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT `invoice_number`,`subtotal_ex_vat`,`total_vat`,`total_incl_vat`,
                   `currency_code`,`status`,`active`
            FROM `epc_einvoice_documents`
            WHERE `id`=?
            LIMIT 1
            """);
        ErpDb.AddParameters(command, invoiceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new InvoiceSnapshot(
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
            reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
            reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
            reader.IsDBNull(4) ? "AED" : reader.GetString(4),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            reader.IsDBNull(6) ? 0 : reader.GetInt32(6));
    }

    private sealed record InvoiceSnapshot(
        string Number,
        decimal Subtotal,
        decimal Vat,
        decimal Total,
        string Currency,
        string Status,
        int Active);
}
