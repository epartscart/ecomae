using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Erp;

public sealed record ErpTrialBalanceSnapshotRequest(
    string? FromDate = null,
    string? ToDate = null,
    string? PostingLayer = null,
    string? OpeningDisplay = null,
    string? IncludeClosing = null,
    string? IncludeClosingAdj = null,
    string? DimensionSet = null,
    string? SeparateDims = null,
    string? TrackingId = null,
    long CreatedBy = 0);

public interface IErpTrialBalanceWriteService
{
    Task<ErpSimpleWriteResult> SaveSnapshotAsync(
        ErpTrialBalanceSnapshotRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ErpTrialBalanceWriteService : IErpTrialBalanceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpTrialBalanceReadService _read;

    public ErpTrialBalanceWriteService(IErpWriteConnectionFactory connections, IErpTrialBalanceReadService read)
    {
        _connections = connections;
        _read = read;
    }

    public async Task<ErpSimpleWriteResult> SaveSnapshotAsync(
        ErpTrialBalanceSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = Validate(request);
        if (check is not null)
        {
            return check;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var parameters = ErpTrialBalanceParameters.FromQuery(
            request.FromDate,
            request.ToDate,
            request.PostingLayer,
            request.OpeningDisplay,
            request.IncludeClosing,
            request.IncludeClosingAdj,
            request.DimensionSet,
            request.SeparateDims,
            null,
            null);
        var result = await _read.ReadAsync(parameters, cancellationToken).ConfigureAwait(false);
        var tracking = string.IsNullOrWhiteSpace(request.TrackingId)
            ? "TB-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
            : request.TrackingId.Trim();
        if (tracking.Length > 64)
        {
            tracking = tracking[..64];
        }

        var payload = JsonSerializer.Serialize(result.Lines.Select(l => new
        {
            l.Code,
            l.Name,
            l.AccountType,
            l.Company,
            l.Opening,
            l.Debit,
            l.Credit,
            l.Closing,
        }));
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureTrialBalanceSnapshotsAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_tb_snapshots` (" +
                "`tracking_id`,`date_from`,`date_to`,`posting_layer`,`opening_display`," +
                "`include_closing`,`include_closing_adj`,`dimension_set`,`separate_dims`," +
                "`row_count`,`opening_total`,`debit_total`,`credit_total`,`closing_total`," +
                "`payload_json`,`created_by`,`time_created`" +
                ") VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            tracking,
            parameters.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            parameters.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ErpTrialBalanceParameters.LayerKey(parameters.PostingLayer),
            ErpTrialBalanceParameters.OpeningKey(parameters.OpeningDisplay),
            parameters.IncludeClosingTransactions ? 1 : 0,
            parameters.IncludeClosingAdjustments ? 1 : 0,
            ErpTrialBalanceParameters.DimensionKey(parameters.DimensionSet),
            parameters.SeparateDimensionColumns ? 1 : 0,
            result.Lines.Count,
            result.OpeningTotal,
            result.DebitTotal,
            result.CreditTotal,
            result.ClosingTotal,
            payload,
            request.CreatedBy,
            now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Trial balance snapshot " + tracking + " saved.", id);
    }

    public static ErpSimpleWriteResult? Validate(ErpTrialBalanceSnapshotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var from = ErpTrialBalanceParameters.ParseDate(request.FromDate);
        var to = ErpTrialBalanceParameters.ParseDate(request.ToDate);
        if (from is null || to is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "From date and to date are required.");
        }

        if (to < from)
        {
            return ErpSimpleWriteResult.Fail("invalid", "To date must be on or after from date.");
        }

        return null;
    }
}
