using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_payments.php</c> <c>activate</c> twin of <c>epc_payment_set_active</c>.
/// Clears every <c>shop_payment_systems.active</c> then sets the named handler.
/// <c>save_config</c>, accounts, seed, and settlement stay Classic (credentials / filesystem).
/// Does not invent a send.
/// </summary>
public interface ICpPaymentsWriteService
{
    Task<ErpSimpleWriteResult> ActivateAsync(string? handler, CancellationToken cancellationToken = default);
}

public sealed class CpPaymentsWriteService : ICpPaymentsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPaymentsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ActivateAsync(
        string? handler,
        CancellationToken cancellationToken = default)
    {
        var key = SanitizeHandler(handler);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Handler required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1"),
            cancellationToken,
            key);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Gateway not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            "UPDATE `shop_payment_systems` SET `active` = 0",
            cancellationToken);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_payment_systems` SET `active` = 1 WHERE `handler` = ? LIMIT 1"),
            cancellationToken,
            key);
        return ErpSimpleWriteResult.Ok("Activated: " + HandlerTitle(key), id);
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', $handler)</c>.</summary>
    public static string SanitizeHandler(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if ((ch is >= 'a' and <= 'z') || (ch is >= '0' and <= '9') || ch == '_')
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString();
    }

    /// <summary>PHP <c>epc_payment_handler_title</c> fallback when the handler is not in the defs table.</summary>
    public static string HandlerTitle(string handler)
    {
        if (string.IsNullOrEmpty(handler))
        {
            return string.Empty;
        }

        var spaced = handler.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
