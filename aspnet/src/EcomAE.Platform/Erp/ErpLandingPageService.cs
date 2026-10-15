using System.Data.Common;
using EcomAE.Platform.Cp;

namespace EcomAE.Platform.Erp;

public interface IErpLandingPage
{
    Task<string> CurrentAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(string? path, CancellationToken cancellationToken = default);
}

public sealed class ErpLandingPageService : IErpLandingPage
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpLandingPageService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<string> CurrentAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "";
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await TryReadAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return "";
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(string? path, CancellationToken cancellationToken = default)
    {
        var normal = ErpLandingPage.Normalize(path);
        if (string.IsNullOrEmpty(normal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose a page in this company.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "The company database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpOnlyShopBootstrap.CreateCompanySettingsSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
            "INSERT INTO `epc_org_company_settings` (`company_id`,`setting_key`,`setting_value`) VALUES (0,?,?) ON DUPLICATE KEY UPDATE `setting_value`=VALUES(`setting_value`)"),
            cancellationToken, ErpLandingPage.SettingKey, normal).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("After login, this company opens " + normal + ".", 0);
    }

    public static async Task<string> TryReadAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(connection, null, ErpDb.Positional(
                "SELECT `setting_value` FROM `epc_org_company_settings` WHERE `company_id`=0 AND `setting_key`=? LIMIT 1"),
                cancellationToken, ErpLandingPage.SettingKey).ConfigureAwait(false);
            return ErpLandingPage.Normalize(value) ?? "";
        }
        catch (DbException)
        {
            return "";
        }
    }
}
