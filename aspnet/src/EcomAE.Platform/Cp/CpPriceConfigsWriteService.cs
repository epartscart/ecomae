using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_super_cp_price_configs.php</c> twin of <c>epc_scp_price_config_save</c>
/// and <c>epc_scp_price_config_delete</c>. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpPriceConfigsWriteService
{
    Task<IReadOnlyList<CpPriceConfigRow>> ListAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        CpPriceConfigSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed record CpPriceConfigSaveRequest(
    long Id,
    string? Name,
    string? Scope,
    string? SiteKey,
    string? ClientType,
    string? ClientRef,
    decimal MarkupPercent,
    decimal MarkupFixed,
    string? Currency,
    int Priority,
    bool Active,
    string? Notes);

public sealed record CpPriceConfigRow(
    long Id,
    string Name,
    string Scope,
    string SiteKey,
    string ClientType,
    string ClientRef,
    decimal MarkupPercent,
    decimal MarkupFixed,
    string Currency,
    int Priority,
    bool Active,
    string Notes);

public sealed class CpPriceConfigsWriteService : ICpPriceConfigsWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> ClientTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["all"] = "All clients",
        ["catalog"] = "Built-in catalogue",
        ["api"] = "API / integration",
        ["channel"] = "Sales channel",
        ["price_list"] = "Price list",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpPriceConfigsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeScope(string? raw)
    {
        var scope = (raw ?? string.Empty).Trim();
        return scope is "platform" or "tenant" ? scope : "platform";
    }

    public static string NormalizeClientType(string? raw)
    {
        var clientType = (raw ?? string.Empty).Trim();
        return ClientTypes.ContainsKey(clientType) ? clientType : "all";
    }

    public static string NormalizeCurrency(string? raw)
    {
        var currency = (raw ?? "AED").Trim();
        if (currency.Length > 8)
        {
            currency = currency[..8];
        }

        return currency.ToUpperInvariant();
    }

    public static int NormalizePriority(int raw)
        => raw < 1 ? 1 : raw;

    public async Task<IReadOnlyList<CpPriceConfigRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id`, `name`, `scope`, `site_key`, `client_type`, `client_ref`, `markup_percent`, `markup_fixed`, `currency`, `priority`, `active`, IFNULL(`notes`, '') FROM `epc_platform_price_configs` ORDER BY `active` DESC, `priority` ASC, `name` ASC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpPriceConfigRow>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPriceConfigRow(
                    ReadLong(reader, 0),
                    ReadString(reader, 1),
                    ReadString(reader, 2),
                    ReadString(reader, 3),
                    ReadString(reader, 4),
                    ReadString(reader, 5),
                    ReadDecimal(reader, 6),
                    ReadDecimal(reader, 7),
                    ReadString(reader, 8),
                    (int)ReadLong(reader, 9),
                    ReadLong(reader, 10) != 0,
                    ReadString(reader, 11)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpPriceConfigSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var scope = NormalizeScope(request.Scope);
        var siteKey = NormalizeSiteKey(request.SiteKey);
        var clientType = NormalizeClientType(request.ClientType);
        var clientRef = (request.ClientRef ?? string.Empty).Trim();
        var currency = NormalizeCurrency(request.Currency);
        var priority = NormalizePriority(request.Priority);
        var active = request.Active ? 1 : 0;
        var notes = (request.Notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var existing = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_platform_price_configs` WHERE `id`=? LIMIT 1"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (existing <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Price config not found");
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_platform_price_configs` SET `name`=?, `scope`=?, `site_key`=?, `client_type`=?, `client_ref`=?, `markup_percent`=?, `markup_fixed`=?, `currency`=?, `priority`=?, `active`=?, `notes`=?, `updated_at`=? WHERE `id`=?"),
                    cancellationToken, name, scope, siteKey, clientType, clientRef, request.MarkupPercent, request.MarkupFixed, currency, priority, active, notes, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Price config saved.", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_platform_price_configs` (`name`, `scope`, `site_key`, `client_type`, `client_ref`, `markup_percent`, `markup_fixed`, `currency`, `priority`, `active`, `notes`, `updated_at`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken, name, scope, siteKey, clientType, clientRef, request.MarkupPercent, request.MarkupFixed, currency, priority, active, notes, now, now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Price config saved.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Price-configs table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Price config id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_platform_price_configs` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Price config deleted.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Price-configs table is missing — schema-ensure stays Classic.");
        }
    }

    private static string ReadString(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static decimal ReadDecimal(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
