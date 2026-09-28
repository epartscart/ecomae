using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutProformaReceiptReadService
{
    Task<ErpFitOutProformaReceiptResult> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutProformaReceiptRow(
    string Stage,
    int InvoiceCount,
    decimal InvoicedExVat,
    decimal InvoicedVat,
    decimal InvoicedInclVat,
    decimal Receipts,
    int ReceiptCount,
    long LastReceiptUnixTime,
    decimal Outstanding);

public sealed record ErpFitOutProformaReceiptResult(
    long ProjectId,
    IReadOnlyList<ErpFitOutProformaReceiptRow> Rows,
    decimal TotalInvoiced,
    decimal TotalReceipts,
    decimal TotalOutstanding,
    string Source,
    string Message);

public sealed class ErpFitOutProformaReceiptReadService
    : IErpFitOutProformaReceiptReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutProformaReceiptReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutProformaReceiptResult> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
        {
            return Empty(projectId, "Project id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return Empty(projectId, "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT `stage`,
                       COUNT(*),
                       COALESCE(SUM(`subtotal_ex_vat`),0),
                       COALESCE(SUM(`total_vat`),0),
                       COALESCE(SUM(`total_incl_vat`),0),
                       COALESCE(SUM(`receipts`),0)
                FROM (
                    SELECT links.`stage`,
                           links.`subtotal_ex_vat`,
                           links.`total_vat`,
                           links.`total_incl_vat`,
                           COALESCE(receipts.`receipt_amount`,0) AS `receipts`,
                           COALESCE(receipts.`receipt_count`,0) AS `receipt_count`,
                           COALESCE(receipts.`last_receipt_time`,0) AS `last_receipt_time`
                    FROM `ecomae_fitout_invoice_links` links
                    LEFT JOIN (
                        SELECT `sales_invoice_id`,
                               SUM(CASE WHEN `direction`=1 THEN `amount` ELSE 0 END) AS `receipt_amount`
                               ,COUNT(CASE WHEN `direction`=1 THEN 1 END) AS `receipt_count`
                               ,MAX(CASE WHEN `direction`=1 THEN `time` ELSE 0 END) AS `last_receipt_time`
                        FROM `epc_erp_cash_bank_entries`
                        WHERE `active`=1 AND `sales_invoice_id`>0
                        GROUP BY `sales_invoice_id`
                    ) receipts ON receipts.`sales_invoice_id`=links.`invoice_id`
                    WHERE links.`project_id`=?
                ) invoice_totals
                GROUP BY `stage`
                ORDER BY `stage`
                """);
            ErpDb.AddParameters(command, projectId);
            var rows = new List<ErpFitOutProformaReceiptRow>();
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var invoiced = reader.GetDecimal(4);
                var receipts = reader.GetDecimal(5);
                rows.Add(new(
                    reader.GetString(0),
                    Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
                    reader.GetDecimal(2),
                    reader.GetDecimal(3),
                    invoiced,
                    receipts,
                    Convert.ToInt32(reader.GetValue(6), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(7), System.Globalization.CultureInfo.InvariantCulture),
                    Math.Max(0m, invoiced - receipts)));
            }

            return new(
                projectId,
                rows,
                rows.Sum(row => row.InvoicedInclVat),
                rows.Sum(row => row.Receipts),
                rows.Sum(row => row.Outstanding),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static ErpFitOutProformaReceiptResult Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, [], 0m, 0m, 0m, source, message);
}
