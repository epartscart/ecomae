using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>sso_saml</c> <c>provider_toggle</c> / <c>epc_sso_provider_toggle</c>.
/// Create, initiate, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosSsoWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        long providerId,
        bool active,
        CancellationToken cancellationToken = default);
}

public sealed class BosSsoWriteService : IBosSsoWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosSsoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(bool)($_POST['active'] ?? false)</c> — any non-empty string is true, including <c>0</c>.</summary>
    public static bool PhpPostedBool(string? raw)
        => !string.IsNullOrEmpty(raw);

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        long providerId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_sso_providers` SET `active` = ? WHERE `id` = ?
                    """),
                cancellationToken, active ? 1 : 0, providerId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("SSO provider toggled", providerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "SSO providers table is missing — schema-ensure stays Classic.");
        }
    }
}
