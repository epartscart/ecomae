using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_jw_tag_create</c> / <c>epc_jw_tag_sell</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwTagWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwTagCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SellAsync(
        ErpJwTagSellRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwTagCreateRequest(
    int CompanyId = 0,
    string? TagNo = null,
    string? Barcode = null,
    string? ItemType = null,
    string? Karat = null,
    decimal GrossWeight = 0,
    decimal NetWeight = 0,
    decimal StoneWeight = 0,
    int StoneCount = 0,
    decimal MakingCharges = 0,
    string? MakingType = null,
    decimal CostPrice = 0,
    decimal SellPrice = 0,
    decimal MarginPct = 0,
    string? DesignNo = null,
    string? Category = null,
    string? Subcategory = null,
    int SupplierId = 0,
    int PurchaseId = 0,
    string? PurchaseDate = null,
    string? Location = null,
    string? Description = null);

public sealed record ErpJwTagSellRequest(
    long TagId = 0,
    int InvoiceId = 0,
    int SalesmanId = 0);

public sealed class ErpJwTagWriteService : IErpJwTagWriteService
{
    private static readonly HashSet<string> ItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "gold", "diamond", "silver", "platinum", "gemstone", "mixed"
    };

    private static readonly HashSet<string> MakingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "per_gram", "lumpsum", "percentage"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwTagWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwTagCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tagNo = (request.TagNo ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        if (tagNo.Length == 0 && description.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tag number or description is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jw_tags", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_jw_tags", "tag_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery tag tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var location = Clip((request.Location ?? string.Empty).Trim(), 100);
        if (location.Length == 0)
        {
            location = "showroom";
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (tagNo.Length == 0)
            {
                tagNo = await NextTagNoAsync(connection, transaction, companyId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                tagNo = Clip(tagNo, 50);
            }

            var barcode = Clip((request.Barcode ?? string.Empty).Trim(), 100);
            if (barcode.Length == 0)
            {
                barcode = tagNo;
            }

            var itemType = NormalizeItemType(request.ItemType);
            var karat = Clip((request.Karat ?? string.Empty).Trim(), 10);
            if (karat.Length == 0)
            {
                karat = "22K";
            }

            var purchaseDate = NormalizeDate(request.PurchaseDate);
            var now = UnixNow();

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_jw_tags` (`company_id`,`tag_no`,`barcode`,`item_type`,`karat`,`gross_weight`,`net_weight`,`stone_weight`,`stone_count`,`making_charges`,`making_type`,`cost_price`,`sell_price`,`margin_pct`,`design_no`,`category`,`subcategory`,`supplier_id`,`purchase_id`,`purchase_date`,`location`,`status`,`description`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'in_stock', ?, ?)"),
                cancellationToken,
                companyId,
                tagNo,
                barcode,
                itemType,
                karat,
                RoundNonNeg(request.GrossWeight, 3),
                RoundNonNeg(request.NetWeight, 3),
                RoundNonNeg(request.StoneWeight, 3),
                request.StoneCount < 0 ? 0 : request.StoneCount,
                RoundNonNeg(request.MakingCharges, 2),
                NormalizeMakingType(request.MakingType),
                RoundNonNeg(request.CostPrice, 2),
                RoundNonNeg(request.SellPrice, 2),
                RoundNonNeg(request.MarginPct, 2),
                Clip((request.DesignNo ?? string.Empty).Trim(), 50),
                Clip((request.Category ?? string.Empty).Trim(), 100),
                Clip((request.Subcategory ?? string.Empty).Trim(), 100),
                request.SupplierId < 0 ? 0 : request.SupplierId,
                request.PurchaseId < 0 ? 0 : request.PurchaseId,
                purchaseDate,
                location,
                Clip(description, 500),
                now).ConfigureAwait(false);

            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Failed");
            }

            if (await TableExistsAsync(connection, "epc_jw_tag_history", cancellationToken).ConfigureAwait(false))
            {
                var purchaseId = request.PurchaseId < 0 ? 0 : request.PurchaseId;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_jw_tag_history` (`tag_id`,`action`,`to_location`,`reference`,`time_created`) VALUES (?, 'created', ?, ?, ?)"),
                    cancellationToken,
                    id,
                    location,
                    "Purchase: " + purchaseId.ToString(CultureInfo.InvariantCulture),
                    now).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Tag " + tagNo + " created", id);
        }
        catch (DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Tag number already exists.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ErpSimpleWriteResult> SellAsync(
        ErpJwTagSellRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TagId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tag id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jw_tags", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_jw_tags", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery tag tables are not provisioned");
        }

        var invoiceId = request.InvoiceId < 0 ? 0 : request.InvoiceId;
        var salesmanId = request.SalesmanId < 0 ? 0 : request.SalesmanId;
        var now = UnixNow();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_jw_tags` SET `status` = 'sold', `sold_invoice_id` = ?, `sold_date` = CURDATE(), `salesman_id` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                invoiceId,
                salesmanId,
                now,
                request.TagId).ConfigureAwait(false);
            if (updated <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Tag is missing.");
            }

            if (await TableExistsAsync(connection, "epc_jw_tag_history", cancellationToken).ConfigureAwait(false))
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_jw_tag_history` (`tag_id`,`action`,`reference`,`actor_id`,`time_created`) VALUES (?, 'sold', ?, ?, ?)"),
                    cancellationToken,
                    request.TagId,
                    "Invoice #" + invoiceId.ToString(CultureInfo.InvariantCulture),
                    salesmanId,
                    now).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Tag sold", request.TagId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<string> NextTagNoAsync(
        DbConnection connection,
        DbTransaction transaction,
        int companyId,
        CancellationToken cancellationToken)
    {
        var year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
        if (await TableExistsAsync(connection, "epc_jw_tag_sequences", cancellationToken).ConfigureAwait(false))
        {
            var prefix = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `prefix` FROM `epc_jw_tag_sequences` WHERE `company_id` = ? LIMIT 1 FOR UPDATE"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            var last = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `last_number` FROM `epc_jw_tag_sequences` WHERE `company_id` = ? LIMIT 1"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "T";
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `epc_jw_tag_sequences` (`company_id`,`prefix`,`last_number`) VALUES (?, 'T', 0)"),
                    cancellationToken,
                    companyId).ConfigureAwait(false);
                last = 0;
            }

            var next = last + 1;
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_jw_tag_sequences` SET `last_number` = ? WHERE `company_id` = ?"),
                cancellationToken,
                next,
                companyId).ConfigureAwait(false);
            return Clip(prefix.Trim(), 10) + year + next.ToString("000000", CultureInfo.InvariantCulture);
        }

        var seq = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_jw_tags` WHERE `company_id` = ?"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        return "T" + year + (seq + 1).ToString("000000", CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    private static string NormalizeItemType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ItemTypes.Contains(value) ? value : "gold";
    }

    private static string NormalizeMakingType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        if (value is "lump_sum" or "lump-sum")
        {
            value = "lumpsum";
        }

        if (value is "pct" or "%" or "percent")
        {
            value = "percentage";
        }

        return MakingTypes.Contains(value) ? value : "per_gram";
    }

    private static string NormalizeDate(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
