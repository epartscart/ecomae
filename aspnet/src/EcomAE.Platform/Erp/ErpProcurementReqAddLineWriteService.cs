using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_proc_req_add_line</c> twin. Recalc total / requires_approval.
/// Schema ensure stays PHP. Does not CREATE tables.
/// </summary>
public interface IErpProcurementReqAddLineWriteService
{
    Task<ErpSimpleWriteResult> AddLineAsync(
        ErpProcurementReqAddLineWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpProcurementReqAddLineWriteRequest(
    long ReqId = 0,
    long CategoryId = 0,
    string? ItemCode = null,
    string? Description = null,
    decimal Qty = 0,
    decimal UnitPrice = 0,
    string? PreferredVendor = null);

public sealed class ErpProcurementReqAddLineWriteService : IErpProcurementReqAddLineWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpProcurementReqAddLineWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddLineAsync(
        ErpProcurementReqAddLineWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReqId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "id must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_proc_req", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_proc_req", "status", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_proc_req_line", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_proc_req_line", "line_total", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Procurement requisition tables are not provisioned");
        }

        var policyReady = await TableExistsAsync(connection, "epc_proc_policy", cancellationToken).ConfigureAwait(false)
                          && await ColumnExistsAsync(connection, "epc_proc_policy", "preferred_vendor", cancellationToken).ConfigureAwait(false)
                          && await ColumnExistsAsync(connection, "epc_proc_policy", "approval_threshold", cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var status = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `status` FROM `epc_proc_req` WHERE `id`=?"),
                cancellationToken,
                request.ReqId).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `epc_proc_req` WHERE `id`=?"),
                cancellationToken,
                request.ReqId).ConfigureAwait(false);
            if (exists <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Requisition not found");
            }

            if (!string.Equals(status, "draft", StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Lines can only be added while the requisition is a draft");
            }

            var companyId = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `company_id` FROM `epc_proc_req` WHERE `id`=?"),
                cancellationToken,
                request.ReqId).ConfigureAwait(false);
            var categoryId = request.CategoryId < 0 ? 0 : request.CategoryId;
            var qty = request.Qty;
            var price = request.UnitPrice;
            var lineTotal = Math.Round(qty * price, 2, MidpointRounding.AwayFromZero);
            var pref = Clip(request.PreferredVendor, 160);
            if (pref.Length == 0 && policyReady)
            {
                pref = await PreferredVendorAsync(connection, transaction, companyId, categoryId, cancellationToken)
                    .ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_proc_req_line` (`req_id`,`category_id`,`item_code`,`description`,`qty`,`unit_price`,`line_total`,`preferred_vendor`) VALUES (?,?,?,?,?,?,?,?)"),
                cancellationToken,
                request.ReqId,
                categoryId,
                Clip(request.ItemCode, 80),
                Clip(request.Description, 255),
                qty,
                price,
                lineTotal,
                pref).ConfigureAwait(false);
            var lineId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            await RecalcAsync(connection, transaction, request.ReqId, companyId, policyReady, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Line added", lineId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task RecalcAsync(
        DbConnection connection,
        DbTransaction transaction,
        long reqId,
        long companyId,
        bool policyReady,
        CancellationToken cancellationToken)
    {
        var total = await ErpDb.DecimalAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COALESCE(SUM(`line_total`),0) FROM `epc_proc_req_line` WHERE `req_id`=?"),
            cancellationToken,
            reqId).ConfigureAwait(false);
        var firstCategory = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `category_id` FROM `epc_proc_req_line` WHERE `req_id`=? ORDER BY `id` ASC LIMIT 1"),
            cancellationToken,
            reqId).ConfigureAwait(false);
        var needs = await RequiresApprovalAsync(connection, transaction, companyId, firstCategory, total, policyReady, cancellationToken)
            .ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `epc_proc_req` SET `total`=?, `requires_approval`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            total,
            needs ? 1 : 0,
            now,
            reqId).ConfigureAwait(false);
    }

    private static async Task<bool> RequiresApprovalAsync(
        DbConnection connection,
        DbTransaction transaction,
        long companyId,
        long categoryId,
        decimal amount,
        bool policyReady,
        CancellationToken cancellationToken)
    {
        if (!policyReady)
        {
            return amount > 0;
        }

        var thresholdRaw = await PolicyScalarAsync(
            connection, transaction, companyId, categoryId, "approval_threshold", cancellationToken).ConfigureAwait(false);
        if (thresholdRaw is null)
        {
            return amount > 0;
        }

        var threshold = decimal.TryParse(thresholdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
        if (threshold <= 0)
        {
            return false;
        }

        return amount > threshold;
    }

    private static async Task<string> PreferredVendorAsync(
        DbConnection connection,
        DbTransaction transaction,
        long companyId,
        long categoryId,
        CancellationToken cancellationToken)
        => Clip(await PolicyScalarAsync(connection, transaction, companyId, categoryId, "preferred_vendor", cancellationToken)
            .ConfigureAwait(false), 160);

    private static async Task<string?> PolicyScalarAsync(
        DbConnection connection,
        DbTransaction transaction,
        long companyId,
        long categoryId,
        string column,
        CancellationToken cancellationToken)
    {
        var sql = ErpDb.Positional(
            "SELECT `" + column + "` FROM `epc_proc_policy` WHERE `company_id`=? AND `active`=1 AND `category_id`=? ORDER BY `id` DESC LIMIT 1");
        var value = await ErpDb.StringAsync(connection, transaction, sql, cancellationToken, companyId, categoryId)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        return await ErpDb.StringAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT `" + column + "` FROM `epc_proc_policy` WHERE `company_id`=? AND `active`=1 AND `category_id`=0 ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            companyId).ConfigureAwait(false);
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

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
