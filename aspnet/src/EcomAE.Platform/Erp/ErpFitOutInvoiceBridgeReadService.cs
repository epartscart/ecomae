using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutInvoiceBridgeReadService
{
    Task<ErpFitOutInvoiceBridgeResult> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutInvoiceBridgeRow(
    long Id,
    long ProjectId,
    long InvoiceId,
    string InvoiceNumber,
    string Stage,
    string Reference,
    decimal SubtotalExVat,
    decimal TotalVat,
    decimal TotalInclVat,
    string CurrencyCode,
    string InvoiceStatus);

public sealed record ErpFitOutInvoiceBridgeResult(
    long ProjectId,
    IReadOnlyList<ErpFitOutInvoiceBridgeRow> Rows,
    string Source,
    string Message);

public sealed class ErpFitOutInvoiceBridgeReadService : IErpFitOutInvoiceBridgeReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutInvoiceBridgeReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutInvoiceBridgeResult> ReadAsync(
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

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = new List<ErpFitOutInvoiceBridgeRow>();
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `id`,`project_id`,`invoice_id`,`invoice_number`,`stage`,`reference`,
                       `subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`currency_code`,`invoice_status`
                FROM `ecomae_fitout_invoice_links`
                WHERE `project_id`=?
                ORDER BY `id` DESC
                """);
            ErpDb.AddParameters(command, projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetDecimal(6),
                    reader.GetDecimal(7),
                    reader.GetDecimal(8),
                    reader.GetString(9),
                    reader.GetString(10)));
            }

            return new(projectId, rows, "database", string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static ErpFitOutInvoiceBridgeResult Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, Array.Empty<ErpFitOutInvoiceBridgeRow>(), source, message);
}
