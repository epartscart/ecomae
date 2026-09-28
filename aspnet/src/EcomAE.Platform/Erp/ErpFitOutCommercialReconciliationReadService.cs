using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutCommercialReconciliationReadService
{
    Task<ErpFitOutCommercialReconciliation> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutCommercialReconciliation(
    long ProjectId,
    decimal ContractValue,
    decimal BudgetCost,
    decimal CertifiedAmount,
    decimal VendorBills,
    decimal PaymentVouchers,
    decimal ClientBilled,
    decimal RetentionHeld,
    decimal RetentionReleased,
    decimal UnbilledClientAmount,
    decimal UnpaidVendorAmount,
    decimal ForecastMargin,
    string Source,
    string Message);

public sealed class ErpFitOutCommercialReconciliationReadService
    : IErpFitOutCommercialReconciliationReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutCommercialReconciliationReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutCommercialReconciliation> ReadAsync(
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
            var contractValue = await SumAsync(
                connection,
                "SELECT COALESCE(`contract_value`,0) FROM `epc_prj_projects` WHERE `id`=? LIMIT 1",
                projectId,
                cancellationToken).ConfigureAwait(false);
            var budgetCost = await SumAsync(
                connection,
                "SELECT COALESCE(SUM(`cost_budget`),0) FROM `epc_prja_budget` WHERE `project_id`=?",
                projectId,
                cancellationToken).ConfigureAwait(false);
            var totals = await ReadRecordTotalsAsync(connection, projectId, cancellationToken).ConfigureAwait(false);
            var clientBilled = await SumAsync(
                connection,
                """
                SELECT COALESCE(SUM(`subtotal_ex_vat`),0)
                FROM `ecomae_fitout_invoice_links`
                WHERE `project_id`=?
                """,
                projectId,
                cancellationToken).ConfigureAwait(false);

            var retentionHeld = Math.Max(
                0m,
                decimal.Round(totals.Certified * totals.RetentionPercent / 100m, 2)
                    - totals.RetentionReleased);
            return new(
                projectId,
                contractValue,
                budgetCost,
                totals.Certified,
                totals.VendorBills,
                totals.PaymentVouchers,
                clientBilled,
                retentionHeld,
                totals.RetentionReleased,
                Math.Max(0m, totals.Certified - clientBilled),
                Math.Max(0m, totals.VendorBills - totals.PaymentVouchers),
                contractValue - Math.Max(budgetCost, totals.VendorBills),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(projectId, exception.Message, "database-error");
        }
    }

    private static async Task<(
        decimal Certified,
        decimal VendorBills,
        decimal PaymentVouchers,
        decimal RetentionReleased,
        decimal RetentionPercent)> ReadRecordTotalsAsync(
        DbConnection connection,
        long projectId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT
                COALESCE(SUM(CASE WHEN `record_type` IN
                    ('subcontract_certification','progress_claim','subcontractor_progress_claim','client_progress_claim')
                    THEN `amount` ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN `record_type`='vendor_bill' THEN `amount` ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN `record_type`='payment_voucher' THEN `amount` ELSE 0 END),0),
                COALESCE(SUM(CASE WHEN `record_type`='retention_release'
                    AND `status`='approved' THEN `amount` ELSE 0 END),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `project_id`=?
            """);
        ErpDb.AddParameters(command, projectId);
        decimal certified;
        decimal vendorBills;
        decimal paymentVouchers;
        decimal retentionReleased;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (0m, 0m, 0m, 0m, 0m);
            }

            certified = reader.GetDecimal(0);
            vendorBills = reader.GetDecimal(1);
            paymentVouchers = reader.GetDecimal(2);
            retentionReleased = reader.GetDecimal(3);
        }

        var retentionPercent = await ReadRetentionPercentAsync(connection, projectId, cancellationToken)
            .ConfigureAwait(false);
        return (certified, vendorBills, paymentVouchers, retentionReleased, retentionPercent);
    }

    private static async Task<decimal> ReadRetentionPercentAsync(
        DbConnection connection,
        long projectId,
        CancellationToken cancellationToken)
        => await SumAsync(
            connection,
            "SELECT COALESCE(`retention_percent`,0) FROM `ecomae_fitout_contract_terms` WHERE `contract_id`=? LIMIT 1",
            projectId,
            cancellationToken).ConfigureAwait(false);

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

    private static ErpFitOutCommercialReconciliation Empty(
        long projectId,
        string message,
        string source = "migration")
        => new(projectId, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, source, message);
}
