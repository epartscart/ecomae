using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Guarded PHP <c>epc_scm_rfq_add_response</c> twin. The PHP SCM table remains
/// Classic-owned: this service only inserts when the existing table is present.
/// </summary>
public interface IErpRfqResponseWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpRfqResponseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRfqResponseWriteRequest(
    long RfqId = 0,
    long RfqLineId = 0,
    long SupplierId = 0,
    decimal UnitPrice = 0m,
    int LeadTimeDays = 0,
    string? Notes = null,
    long AdminId = 0);

public sealed class ErpRfqResponseWriteService : IErpRfqResponseWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRfqResponseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpRfqResponseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RfqId <= 0 || request.RfqLineId <= 0 || request.SupplierId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFQ, line, and supplier ids are required.");
        }

        if (request.UnitPrice < 0m || request.LeadTimeDays < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unit price and lead time must not be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_scm_rfq_lines", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_scm_rfq_responses", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("schema", "PHP SCM response tables are not provisioned.");
        }

        var lineExists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_scm_rfq_lines` WHERE `id`=? AND `rfq_id`=?"),
            cancellationToken,
            request.RfqLineId,
            request.RfqId).ConfigureAwait(false);
        if (lineExists == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFQ line was not found.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_scm_rfq_responses`
                    (`rfq_id`,`rfq_line_id`,`supplier_id`,`unit_price`,`lead_time_days`,`notes`,`time_created`)
                VALUES (?,?,?,?,?,?,?)
                """),
            cancellationToken,
            request.RfqId,
            request.RfqLineId,
            request.SupplierId,
            Math.Round(request.UnitPrice, 4, MidpointRounding.AwayFromZero),
            request.LeadTimeDays,
            Clip(request.Notes, 1000),
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        return ErpSimpleWriteResult.Ok("Supplier response saved.", id);
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
