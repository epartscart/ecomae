using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cft_instrument_save</c> / ajax <c>cft_instrument_save</c> twin.
/// UPDATE <c>epc_cft_instrument</c> when <c>id</c> &gt; 0, else INSERT draft and log created.
/// Status transitions, forecast, projection, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCftInstrumentSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpCftInstrumentSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCftInstrumentSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Ref = null,
    string? Type = null,
    string? Beneficiary = null,
    string? Applicant = null,
    string? Bank = null,
    decimal Amount = 0,
    string? Currency = null,
    string? IssueDate = null,
    string? ExpiryDate = null,
    string? Notes = null);

public sealed class ErpCftInstrumentSaveWriteService : IErpCftInstrumentSaveWriteService
{
    public static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "lc", "bg", "sblc",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCftInstrumentSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpCftInstrumentSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = request.Type ?? "lc";
        if (!Types.Contains(type))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Type must be lc, bg or sblc");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var instrumentRef = (request.Ref ?? string.Empty).Trim();
        var beneficiary = request.Beneficiary ?? string.Empty;
        var applicant = request.Applicant ?? string.Empty;
        var bank = request.Bank ?? string.Empty;
        var currency = request.Currency ?? string.Empty;
        var issueDate = request.IssueDate ?? string.Empty;
        var expiryDate = request.ExpiryDate ?? string.Empty;
        var notes = request.Notes ?? string.Empty;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cft_instrument", "ref", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_cft_instr_event", "event_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bank instrument table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_cft_instrument` SET `ref`=?, `type`=?, `beneficiary`=?, `applicant`=?, `bank`=?, `amount`=?, `currency`=?, `issue_date`=?, `expiry_date`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                instrumentRef,
                type,
                beneficiary,
                applicant,
                bank,
                request.Amount,
                currency,
                issueDate,
                expiryDate,
                notes,
                now,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Instrument saved", request.Id);
        }

        if (instrumentRef.Length == 0)
        {
            var ymd = DateTimeOffset.FromUnixTimeSeconds(now).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var tail = now.ToString(CultureInfo.InvariantCulture);
            instrumentRef = type.ToUpperInvariant() + "-" + ymd + "-" + (tail.Length >= 4 ? tail[^4..] : tail.PadLeft(4, '0'));
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cft_instrument` (`company_id`,`ref`,`type`,`beneficiary`,`applicant`,`bank`,`amount`,`currency`,`issue_date`,`expiry_date`,`status`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,'draft',?,?,?)"),
            cancellationToken,
            companyId,
            instrumentRef,
            type,
            beneficiary,
            applicant,
            bank,
            request.Amount,
            currency,
            issueDate,
            expiryDate,
            notes,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cft_instr_event` (`instrument_id`,`event_type`,`detail`,`amount`,`time_created`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            id,
            "created",
            "Instrument drafted",
            request.Amount,
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Instrument saved", id);
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
}
