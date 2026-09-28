using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutInvoiceReconciliationReadService
{
    Task<ErpFitOutInvoiceReconciliation> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutInvoiceReconciliation(
    long ProjectId,
    decimal CertifiedAmount,
    decimal InvoicedExVat,
    decimal InvoicedVat,
    decimal InvoicedInclVat,
    decimal UnbilledCertifiedAmount,
    string Source,
    string Message);

public sealed class ErpFitOutInvoiceReconciliationReadService
    : IErpFitOutInvoiceReconciliationReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutInvoiceReconciliationReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutInvoiceReconciliation> ReadAsync(
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
            var certified = await SumAsync(
                connection,
                """
                SELECT COALESCE(SUM(`amount`),0)
                FROM `ecomae_fitout_delivery_records`
                WHERE `project_id`=?
                  AND `record_type` IN ('subcontract_certification','progress_claim')
                """,
                projectId,
                cancellationToken).ConfigureAwait(false);
            var invoiced = await SumInvoiceAsync(connection, projectId, cancellationToken).ConfigureAwait(false);
            return new(
                projectId,
                certified,
                invoiced.ExVat,
                invoiced.Vat,
                invoiced.InclVat,
                Math.Max(0m, certified - invoiced.ExVat),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static async Task<decimal> SumAsync(
        DbConnection connection,
        string sql,
        long projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, projectId);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null || value is DBNull
            ? 0m
            : Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<(decimal ExVat, decimal Vat, decimal InclVat)> SumInvoiceAsync(
        DbConnection connection,
        long projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT COALESCE(SUM(`subtotal_ex_vat`),0),
                   COALESCE(SUM(`total_vat`),0),
                   COALESCE(SUM(`total_incl_vat`),0)
            FROM `ecomae_fitout_invoice_links`
            WHERE `project_id`=?
            """);
        ErpDb.AddParameters(command, projectId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0m, 0m, 0m);
        }

        return (
            reader.GetDecimal(0),
            reader.GetDecimal(1),
            reader.GetDecimal(2));
    }

    private static ErpFitOutInvoiceReconciliation Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, 0m, 0m, 0m, 0m, 0m, source, message);
}
