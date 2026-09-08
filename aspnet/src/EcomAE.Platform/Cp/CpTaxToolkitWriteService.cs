using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_tax_toolkit_install</c> / <c>epc_tax_toolkit_assign_tenant</c> twin.
/// Does not CREATE tables or seed the worldwide catalog. Refresh / migrate-all stay PHP.
/// </summary>
public interface ICpTaxToolkitWriteService
{
    Task<CpTaxToolkitWriteResult> InstallAsync(
        CpTaxToolkitInstallWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<CpTaxToolkitWriteResult> AssignTenantAsync(
        CpTaxToolkitAssignWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpTaxToolkitInstallWriteRequest(
    string? KitCode = null,
    bool SetDefault = false,
    int AdminId = 0);

public sealed record CpTaxToolkitAssignWriteRequest(
    string? CountryCode = null,
    string? KitCode = null,
    string? SiteKey = null,
    string? RegNumber = null,
    int AdminId = 0);

public sealed record CpTaxToolkitWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    int Writes,
    long Id,
    string KitCode)
{
    public static CpTaxToolkitWriteResult Fail(string code, string message) =>
        new(false, code, message, 0, 0, "");

    public static CpTaxToolkitWriteResult Ok(string message, int writes, long id, string kitCode) =>
        new(true, "ok", message, writes, id, kitCode);
}

public sealed class CpTaxToolkitWriteService : ICpTaxToolkitWriteService
{
    private static readonly IReadOnlyDictionary<string, string> LegacyKitCodes
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AE"] = "AE-UAE-VAT",
            ["UAE"] = "AE-UAE-VAT",
            ["OM"] = "OM-OMAN-VAT",
            ["SA"] = "SA-KSA-VAT",
            ["IN"] = "IN-INDIA-GST",
            ["PK"] = "PK-PAKISTAN-GST",
            ["GB"] = "GB-UK-VAT",
            ["UK"] = "GB-UK-VAT",
            ["US"] = "US-SALES-TAX",
        };

    private readonly IErpWriteConnectionFactory _connections;

    public CpTaxToolkitWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string KitCodeForCountry(string? country)
    {
        var cc = (country ?? "").Trim().ToUpperInvariant();
        if (cc.Length == 0 || cc == "UAE")
        {
            cc = "AE";
        }

        if (LegacyKitCodes.TryGetValue(cc, out var mapped))
        {
            return mapped;
        }

        if (cc.Length > 3)
        {
            cc = cc[..2];
        }

        return cc + "-" + cc + "-VAT";
    }

    public static string NormalizeSiteKey(string? siteKey)
    {
        var key = Regex.Replace((siteKey ?? "").Trim().ToLowerInvariant(), "[^a-z0-9_]", "");
        return key.Length > 0 ? key : "platform";
    }

    public async Task<CpTaxToolkitWriteResult> InstallAsync(
        CpTaxToolkitInstallWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return CpTaxToolkitWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var kitCode = (request.KitCode ?? "").Trim();
        if (kitCode.Length == 0)
        {
            return CpTaxToolkitWriteResult.Fail("invalid", "Kit code is required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TablesReadyAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return CpTaxToolkitWriteResult.Fail("invalid", "Tax toolkit tables are not provisioned");
        }

        var installed = await InstallCoreAsync(connection, kitCode, request.SetDefault, request.AdminId, cancellationToken).ConfigureAwait(false);
        return installed.Succeeded
            ? CpTaxToolkitWriteResult.Ok(
                request.SetDefault ? "Tax kit installed and set as default." : "Tax kit installed.",
                installed.Writes,
                installed.Id,
                kitCode)
            : installed;
    }

    public async Task<CpTaxToolkitWriteResult> AssignTenantAsync(
        CpTaxToolkitAssignWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return CpTaxToolkitWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var siteKey = NormalizeSiteKey(request.SiteKey);
        var country = (request.CountryCode ?? "").Trim().ToUpperInvariant();
        if (country == "UAE" || country.Length == 0)
        {
            country = "AE";
        }

        var kitCode = (request.KitCode ?? "").Trim();
        if (kitCode.Length == 0)
        {
            kitCode = KitCodeForCountry(country);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TablesReadyAsync(connection, cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tax_toolkit_tenant_profile", "site_key", cancellationToken).ConfigureAwait(false))
        {
            return CpTaxToolkitWriteResult.Fail("invalid", "Tax toolkit tables are not provisioned");
        }

        var installed = await InstallCoreAsync(connection, kitCode, true, request.AdminId, cancellationToken).ConfigureAwait(false);
        if (!installed.Succeeded)
        {
            return installed;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var reg = (request.RegNumber ?? "").Trim();
        var existingId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_tax_toolkit_tenant_profile` WHERE `site_key` = ? LIMIT 1"),
            cancellationToken,
            siteKey).ConfigureAwait(false);
        var writes = installed.Writes;
        if (existingId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_tax_toolkit_tenant_profile` SET `country_code`=?, `kit_code`=?, `reg_number`=?, `installed_kit_id`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                country,
                kitCode,
                reg,
                installed.Id,
                now,
                existingId).ConfigureAwait(false);
            writes++;
            return CpTaxToolkitWriteResult.Ok("Platform tenant kit saved.", writes, existingId, kitCode);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_tax_toolkit_tenant_profile`
                (`site_key`, `country_code`, `kit_code`, `reg_number`, `installed_kit_id`, `time_updated`)
                VALUES (?,?,?,?,?,?)
                """),
            cancellationToken,
            siteKey,
            country,
            kitCode,
            reg,
            installed.Id,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        writes++;
        return CpTaxToolkitWriteResult.Ok("Platform tenant kit saved.", writes, id, kitCode);
    }

    private static async Task<CpTaxToolkitWriteResult> InstallCoreAsync(
        System.Data.Common.DbConnection connection,
        string kitCode,
        bool setDefault,
        int adminId,
        CancellationToken cancellationToken)
    {
        var kitId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_tax_toolkits` WHERE `kit_code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            kitCode).ConfigureAwait(false);
        if (kitId <= 0)
        {
            return CpTaxToolkitWriteResult.Fail(
                "invalid",
                "Tax kit not found: " + kitCode + " — worldwide seed stays on the Classic twin.");
        }

        var writes = 0;
        var installId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_tax_toolkit_installs` WHERE `kit_code` = ? LIMIT 1"),
            cancellationToken,
            kitCode).ConfigureAwait(false);
        if (installId <= 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_tax_toolkit_installs`
                    (`kit_id`, `kit_code`, `is_default`, `installed_by`, `time_installed`)
                    VALUES (?,?,?,?,?)
                    """),
                cancellationToken,
                kitId,
                kitCode,
                setDefault ? 1 : 0,
                adminId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            installId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            writes++;
        }

        if (setDefault)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "UPDATE `epc_tax_toolkit_installs` SET `is_default` = 0",
                cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_tax_toolkit_installs` SET `is_default` = 1 WHERE `id` = ?"),
                cancellationToken,
                installId).ConfigureAwait(false);
            writes += 2;
        }

        return CpTaxToolkitWriteResult.Ok("Tax kit installed.", writes, installId, kitCode);
    }

    private static async Task<bool> TablesReadyAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken) =>
        await ColumnExistsAsync(connection, "epc_tax_toolkits", "kit_code", cancellationToken).ConfigureAwait(false)
        && await ColumnExistsAsync(connection, "epc_tax_toolkit_installs", "kit_code", cancellationToken).ConfigureAwait(false);

    private static async Task<bool> ColumnExistsAsync(
        System.Data.Common.DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
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
