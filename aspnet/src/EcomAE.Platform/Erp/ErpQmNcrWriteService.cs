namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_qm_ncr_update</c> twin. Schema ensure and NCR create stay PHP.
/// </summary>
public interface IErpQmNcrWriteService
{
    Task<ErpSimpleWriteResult> UpdateAsync(
        long id,
        string? status,
        string? disposition,
        string? correctiveAction,
        CancellationToken cancellationToken = default);
}

public sealed class ErpQmNcrWriteService : IErpQmNcrWriteService
{
    internal static readonly string[] AllowedStatus = ["open", "investigate", "action", "closed"];
    internal static readonly string[] AllowedDisposition = ["use_as_is", "rework", "scrap", "return"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpQmNcrWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateAsync(
        long id,
        string? status,
        string? disposition,
        string? correctiveAction,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A non-conformance id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedStatus.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid status");
        }

        var disp = (disposition ?? string.Empty).Trim().ToLowerInvariant();
        if (disp.Length > 0 && !AllowedDisposition.Contains(disp, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid disposition");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var closedAt = next == "closed" ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0L;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_qm_ncr` SET `status` = ?, `disposition` = ?, `corrective_action` = ?, `time_closed` = ? WHERE `id` = ?"),
            cancellationToken,
            next,
            disp,
            (correctiveAction ?? string.Empty).Trim(),
            closedAt,
            id);
        return ErpSimpleWriteResult.Ok("Non-conformance updated", id);
    }
}
