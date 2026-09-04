using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_vendor_approvals.php</c> twins for suspend / reject.
/// Approve stays PHP (<c>epc_vendor_approve_account</c> provisions a warehouse).
/// </summary>
public interface ICpVendorApprovalWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long accountId, string? action, CancellationToken cancellationToken = default);
}

public sealed class CpVendorApprovalWriteService : ICpVendorApprovalWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpVendorApprovalWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long accountId,
        string? action,
        CancellationToken cancellationToken = default)
    {
        if (accountId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A vendor account id is required.");
        }

        var next = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (next is not ("suspend" or "reject"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Suspend or reject is required. Approve stays PHP.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var status = next == "suspend" ? "suspended" : "rejected";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_vendor_accounts` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            status, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), accountId);
        return ErpSimpleWriteResult.Ok("Updated", accountId);
    }
}
