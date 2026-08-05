using System.Data.Common;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP-compatible logout: clear admin/customer cookies and best-effort DELETE from <c>sessions</c>.
/// Mirrors <c>epc_cp_perform_logout</c> + storefront session cookie clear.
/// </summary>
public sealed class LegacyLogoutService
{
    private readonly ITenantDbConnectionFactory _connections;

    public LegacyLogoutService(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task LogoutAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var adminSession = httpContext.Request.Cookies["admin_session"] ?? string.Empty;
        var adminUser = ParseInt(httpContext.Request.Cookies["admin_u_id"]);
        var customerSession = httpContext.Request.Cookies["session"] ?? string.Empty;
        var customerUser = ParseInt(httpContext.Request.Cookies["u_id"]);

        if (_connections.IsConfigured)
        {
            try
            {
                await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(adminSession) && adminUser > 0)
                {
                    await DeleteSessionRowAsync(connection, adminSession, adminUser, cancellationToken).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(customerSession) && customerUser > 0)
                {
                    await DeleteSessionRowAsync(connection, customerSession, customerUser, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // Cookie clear still proceeds (same as PHP).
            }
        }

        LegacyLoginCookieWriter.ClearAll(httpContext.Response);
    }

    public static string RedirectForSurface(string? surface, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.Contains('\\', StringComparison.Ordinal))
        {
            return returnUrl;
        }

        return (surface ?? "cp").Trim().ToLowerInvariant() switch
        {
            "erp" => "/erp/login",
            "bos" => "/bos/login",
            "storefront" or "shop" or "customer" => "/storefront/login",
            _ => "/cp/login",
        };
    }

    private static async Task DeleteSessionRowAsync(
        DbConnection connection,
        string sessionToken,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM `sessions` WHERE `session` = @session AND `user_id` = @userId";
        AddParameter(command, "@session", sessionToken);
        AddParameter(command, "@userId", userId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static int ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : 0;
}
