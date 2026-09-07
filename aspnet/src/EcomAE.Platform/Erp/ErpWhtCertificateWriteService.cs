using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wht_certificate_issue</c> twin. UPDATE
/// <c>epc_wht_txn.certificate_no</c>. Schema ensure stays PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpWhtCertificateWriteService
{
    Task<ErpSimpleWriteResult> IssueAsync(
        ErpWhtCertificateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpWhtCertificateWriteRequest(
    long Id = 0,
    string? CertificateNo = null);

public sealed class ErpWhtCertificateWriteService : IErpWhtCertificateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWhtCertificateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> IssueAsync(
        ErpWhtCertificateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Transaction not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_wht_txn", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_wht_txn", "certificate_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Withholding transaction table is not provisioned");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_wht_txn` WHERE `id`=?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Transaction not found");
        }

        var existing = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `certificate_no` FROM `epc_wht_txn` WHERE `id`=?"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(existing))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Certificate already issued");
        }

        var certNo = Clip((request.CertificateNo ?? string.Empty).Trim(), 60);
        if (certNo.Length == 0)
        {
            certNo = DefaultCertNo(request.Id, DateTime.Now.Year);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_wht_txn` SET `certificate_no`=? WHERE `id`=?"),
            cancellationToken,
            certNo,
            request.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Certificate issued: " + certNo, request.Id);
    }

    public static string DefaultCertNo(long txnId, int year)
        => "WHT-" + year.ToString(CultureInfo.InvariantCulture)
           + "-" + txnId.ToString("D5", CultureInfo.InvariantCulture);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
}
