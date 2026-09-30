using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwBarcodePurchaseLookupReadService
{
    Task<ErpJwBarcodePurchaseLookupResult> ReadAsync(
        int companyId,
        string barcode,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwBarcodePurchaseLookupRow(
    long Id,
    int CompanyId,
    string Barcode,
    string ItemDescription,
    string SupplierName,
    string PurchaseDate,
    string PurchaseInvoiceNo,
    string MetalType,
    string Karat,
    decimal GrossWeight,
    decimal NetWeight,
    decimal StoneWeight,
    decimal GoldRateAtPurchase,
    decimal MetalValue,
    decimal MakingCharges,
    decimal StoneValue,
    decimal OtherCharges,
    decimal TotalCost,
    decimal MarginPct,
    decimal MarginAmount,
    decimal SellingPrice,
    string Category,
    string DesignNo,
    string HallmarkNo,
    string CertificateNo,
    string Status);

public sealed record ErpJwBarcodePurchaseLookupResult(
    ErpJwBarcodePurchaseLookupRow? Row,
    string Source,
    string Message);

public sealed class ErpJwBarcodePurchaseLookupReadService
    : IErpJwBarcodePurchaseLookupReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwBarcodePurchaseLookupReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpJwBarcodePurchaseLookupResult> ReadAsync(
        int companyId,
        string barcode,
        CancellationToken cancellationToken = default)
    {
        var value = barcode.Trim();
        if (companyId <= 0 || value.Length == 0)
        {
            return Empty("Company and barcode are required.", "invalid");
        }

        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.", "migration");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`company_id`,`barcode`,`item_description`,`supplier_name`,
                   `purchase_date`,`purchase_invoice_no`,`metal_type`,`karat`,
                   `gross_weight`,`net_weight`,`stone_weight`,`gold_rate_at_purchase`,
                   `metal_value`,`making_charges`,`stone_value`,`other_charges`,
                   `total_cost`,`margin_pct`,`margin_amount`,`selling_price`,
                   `category`,`design_no`,`hallmark_no`,`certificate_no`,`status`
            FROM `epc_barcode_purchases`
            WHERE `company_id`=? AND `barcode`=?
            LIMIT 1
            """);
        ErpDb.AddParameters(command, companyId, value);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Empty("Barcode purchase was not found.", "database");
            }

            return new(
                new(
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? string.Empty : reader.GetValue(5).ToString() ?? string.Empty,
                    reader.GetString(6),
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetDecimal(9),
                    reader.GetDecimal(10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12),
                    reader.GetDecimal(13),
                    reader.GetDecimal(14),
                    reader.GetDecimal(15),
                    reader.GetDecimal(16),
                    reader.GetDecimal(17),
                    reader.GetDecimal(18),
                    reader.GetDecimal(19),
                    reader.GetDecimal(20),
                    reader.GetString(21),
                    reader.GetString(22),
                    reader.GetString(23),
                    reader.GetString(24),
                    reader.GetString(25)),
                "database",
                string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }
    }

    private static ErpJwBarcodePurchaseLookupResult Empty(string message, string source)
        => new(null, source, message);
}
