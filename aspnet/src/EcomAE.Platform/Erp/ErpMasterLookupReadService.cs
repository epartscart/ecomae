using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

/// <summary>One master row offered by ERP pickers (customer, supplier, item, project, cash account, real-estate).</summary>
public sealed record ErpMasterOption(
    long Id,
    string Kind,
    string Code,
    string Name,
    string Extra);

public sealed record ErpMasterLookupResult(
    string Kind,
    string Query,
    IReadOnlyList<ErpMasterOption> Options,
    int Count,
    string Source,
    string Message);

public interface IErpMasterLookupReadService
{
    Task<ErpMasterLookupResult> SearchAsync(
        string? kind,
        string? query,
        int limit = ErpFirstPaint.PickerLimit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant-scoped master catalog for write forms. Once a customer, supplier, item,
/// project, cash account, or real-estate master exists, later documents pick it
/// instead of re-typing codes and names.
/// </summary>
public sealed class ErpMasterLookupReadService : IErpMasterLookupReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly Dictionary<string, IReadOnlyList<ErpMasterOption>> _emptyQueryCache = new(StringComparer.OrdinalIgnoreCase);

    public ErpMasterLookupReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpMasterLookupResult> SearchAsync(
        string? kind,
        string? query,
        int limit = ErpFirstPaint.PickerLimit,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveKind(kind);
        if (resolved.Length == 0)
        {
            return new("", query ?? string.Empty, [], 0, "invalid", "Unknown master kind.");
        }

        var q = (query ?? string.Empty).Trim();
        var safeLimit = Math.Clamp(limit <= 0 ? ErpFirstPaint.PickerLimit : limit, 1, 200);
        if (string.IsNullOrWhiteSpace(q)
            && _emptyQueryCache.TryGetValue(resolved, out var cached))
        {
            return new(resolved, string.Empty, Take(cached, safeLimit), Math.Min(cached.Count, safeLimit), "database", string.Empty);
        }

        if (!_connections.IsConfigured)
        {
            return new(resolved, q, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = await LoadKindAsync(connection, resolved, q, safeLimit, cancellationToken).ConfigureAwait(false);
            if (q.Length == 0)
            {
                _emptyQueryCache[resolved] = rows;
            }

            return new(resolved, q, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(resolved, q, [], 0, "database-error", ex.Message);
        }
    }

    public static string ResolveKind(string? kind)
    {
        var value = (kind ?? string.Empty).Trim().ToLowerInvariant().Replace('_', '-');
        return value switch
        {
            "customer" or "customers" or "user" or "users" or "user-id" or "customer-user-id" => "customer",
            "supplier" or "suppliers" or "vendor" or "vendors" => "supplier",
            "item" or "items" or "sku" or "inventory" or "item-code" => "item",
            "project" or "projects" or "prj" => "project",
            "re-project" or "reproject" or "real-estate-project" => "re-project",
            "account" or "accounts" or "cash" or "bank" or "cash-account" => "account",
            "property" or "properties" => "property",
            "unit" or "units" => "unit",
            "tenant" or "tenants" => "tenant",
            "contractor" or "contractors" => "contractor",
            _ => string.Empty,
        };
    }

    public static string MasterHref(string kind)
        => kind switch
        {
            "customer" => "/erp/receivables-app",
            "supplier" => "/erp/suppliers-app",
            "item" => "/erp/inventory-stock-app",
            "project" => "/erp/project-accounting-app",
            "re-project" => "/erp/real-estate-app?tab=projects",
            "account" => "/erp/cash-accounts-app",
            "property" => "/erp/real-estate-app?tab=properties",
            "unit" => "/erp/real-estate-app?tab=properties",
            "tenant" => "/erp/real-estate-app?tab=leases",
            "contractor" => "/erp/real-estate-app?tab=construction",
            _ => "/erp",
        };

    public static string KindLabel(string kind)
        => kind switch
        {
            "customer" => "Customer",
            "supplier" => "Supplier",
            "item" => "Item",
            "project" => "Project",
            "re-project" => "Development project",
            "account" => "Cash / bank account",
            "property" => "Property",
            "unit" => "Unit",
            "tenant" => "Tenant",
            "contractor" => "Contractor",
            _ => "Master",
        };

    public static bool Matches(ErpMasterOption option, string query)
    {
        ArgumentNullException.ThrowIfNull(option);
        var needle = (query ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            return true;
        }

        if (long.TryParse(needle, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && option.Id == id)
        {
            return true;
        }

        return Contains(option.Code, needle)
            || Contains(option.Name, needle)
            || Contains(option.Extra, needle)
            || Contains(option.Id.ToString(CultureInfo.InvariantCulture), needle);
    }

    public static IReadOnlyList<ErpMasterOption> Filter(
        IReadOnlyList<ErpMasterOption> options,
        string? query,
        int limit = ErpFirstPaint.PickerLimit)
    {
        ArgumentNullException.ThrowIfNull(options);
        var needle = (query ?? string.Empty).Trim();
        var cap = Math.Clamp(limit <= 0 ? ErpFirstPaint.PickerLimit : limit, 1, 200);
        if (needle.Length == 0)
        {
            return Take(options, cap);
        }

        var exact = new List<ErpMasterOption>();
        var rest = new List<ErpMasterOption>();
        foreach (var option in options)
        {
            if (!Matches(option, needle))
            {
                continue;
            }

            if (string.Equals(option.Code, needle, StringComparison.OrdinalIgnoreCase)
                || option.Id.ToString(CultureInfo.InvariantCulture) == needle)
            {
                exact.Add(option);
            }
            else
            {
                rest.Add(option);
            }
        }

        return exact.Concat(rest).Take(cap).ToArray();
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadKindAsync(
        DbConnection connection,
        string kind,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        return kind switch
        {
            "customer" => await LoadCustomersAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "supplier" => await LoadSimpleAsync(
                connection, "supplier", "epc_erp_suppliers", "id", "name", "name",
                await PreferColumnAsync(connection, "epc_erp_suppliers", "code", cancellationToken).ConfigureAwait(false),
                query, limit, cancellationToken, requireActive: true).ConfigureAwait(false),
            "item" => await LoadItemsAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "project" => await LoadSimpleAsync(
                connection, "project", "epc_prj_projects", "id", "code", "name", "code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "re-project" => await LoadSimpleAsync(
                connection, "re-project", "epc_erp_re_projects", "id", "code", "name", "code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "account" => await LoadAccountsAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "property" => await LoadSimpleAsync(
                connection, "property", "epc_erp_properties", "id", "code", "name", "code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "unit" => await LoadSimpleAsync(
                connection, "unit", "epc_erp_re_units", "id", "code", "name", "code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "tenant" => await LoadSimpleAsync(
                connection, "tenant", "epc_erp_re_tenants", "id", "code", "name", "code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "contractor" => await LoadSimpleAsync(
                connection, "contractor", "epc_erp_re_contractors", "id", "code", "name", "code",
                query, limit, cancellationToken).ConfigureAwait(false),
            _ => [],
        };
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadCustomersAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var hasEmail = await ColumnExistsAsync(connection, "users", "email", cancellationToken).ConfigureAwait(false);
        var hasPhone = await ColumnExistsAsync(connection, "users", "phone", cancellationToken).ConfigureAwait(false);
        var hasName = await ColumnExistsAsync(connection, "users", "name", cancellationToken).ConfigureAwait(false);
        var hasProfiles = await TableExistsAsync(connection, "epc_credit_profiles", cancellationToken).ConfigureAwait(false);
        var email = hasEmail ? "IFNULL(`u`.`email`,'')" : "''";
        var phone = hasPhone ? "IFNULL(`u`.`phone`,'')" : "''";
        var userName = hasName ? "IFNULL(`u`.`name`,'')" : "''";
        var account = hasProfiles ? "IFNULL(`p`.`customer_account`,'')" : "''";
        var profileName = hasProfiles ? "IFNULL(`p`.`customer_name`,'')" : "''";
        var join = hasProfiles ? " LEFT JOIN `epc_credit_profiles` `p` ON `p`.`customer_id` = `u`.`user_id`" : "";
        var codeExpr = "NULLIF(TRIM(" + account + "), '')";
        var nameExpr = "COALESCE(NULLIF(TRIM(" + profileName + "), ''), NULLIF(TRIM(" + userName + "), ''), NULLIF(TRIM(" + email + "), ''), NULLIF(TRIM(" + phone + "), ''), CONCAT('Customer #', `u`.`user_id`))";
        var extraExpr = "TRIM(CONCAT_WS(' · ', NULLIF(TRIM(" + email + "), ''), NULLIF(TRIM(" + phone + "), '')))";
        var sql = "SELECT `u`.`user_id`, IFNULL(" + codeExpr + ", CAST(`u`.`user_id` AS CHAR)), " + nameExpr + ", " + extraExpr
            + " FROM `users` `u`" + join
            + WhereLike(query, ["CAST(`u`.`user_id` AS CHAR)", codeExpr, nameExpr, email, phone])
            + " ORDER BY `u`.`user_id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, "customer", sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadItemsAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_erp_inv_items", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var sql = "SELECT `id`, IFNULL(`sku`,''), IFNULL(`name`,''), CONCAT('sale ', IFNULL(`sales_price`,0), ' / buy ', IFNULL(`purchase_price`,0))"
            + " FROM `epc_erp_inv_items` WHERE `active` = 1"
            + AndLike(query, ["CAST(`id` AS CHAR)", "`sku`", "`name`"])
            + " ORDER BY `sku` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, "item", sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadAccountsAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_erp_cash_bank_accounts", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var number = await ColumnExistsAsync(connection, "epc_erp_cash_bank_accounts", "account_number", cancellationToken).ConfigureAwait(false)
            ? "IFNULL(`account_number`,'')"
            : "''";
        var type = await ColumnExistsAsync(connection, "epc_erp_cash_bank_accounts", "account_type", cancellationToken).ConfigureAwait(false)
            ? "IFNULL(`account_type`,'')"
            : "''";
        var sql = "SELECT `id`, IFNULL(NULLIF(TRIM(" + number + "), ''), CAST(`id` AS CHAR)), IFNULL(`name`,''), " + type
            + " FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1"
            + AndLike(query, ["CAST(`id` AS CHAR)", number, "`name`", type])
            + " ORDER BY `name` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, "account", sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadSimpleAsync(
        DbConnection connection,
        string kind,
        string table,
        string idColumn,
        string codeColumn,
        string nameColumn,
        string preferredCodeColumn,
        string query,
        int limit,
        CancellationToken cancellationToken,
        bool requireActive = false)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var code = await ColumnExistsAsync(connection, table, preferredCodeColumn, cancellationToken).ConfigureAwait(false)
            ? preferredCodeColumn
            : codeColumn;
        var hasCode = await ColumnExistsAsync(connection, table, code, cancellationToken).ConfigureAwait(false);
        var hasName = await ColumnExistsAsync(connection, table, nameColumn, cancellationToken).ConfigureAwait(false);
        var codeExpr = hasCode
            ? "IFNULL(NULLIF(TRIM(`" + code + "`), ''), CAST(`" + idColumn + "` AS CHAR))"
            : "CAST(`" + idColumn + "` AS CHAR)";
        var nameExpr = hasName ? "IFNULL(`" + nameColumn + "`,'')" : "''";
        var where = requireActive && await ColumnExistsAsync(connection, table, "active", cancellationToken).ConfigureAwait(false)
            ? " WHERE `active` = 1"
            : "";
        var likeCols = new List<string> { "CAST(`" + idColumn + "` AS CHAR)", nameExpr };
        if (hasCode)
        {
            likeCols.Insert(1, "`" + code + "`");
        }

        var sql = "SELECT `" + idColumn + "`, " + codeExpr + ", " + nameExpr + ", '' FROM `" + table + "`" + where
            + (where.Length == 0 ? WhereLike(query, likeCols) : AndLike(query, likeCols))
            + " ORDER BY `" + idColumn + "` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, kind, sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> ReadRowsAsync(
        DbConnection connection,
        string kind,
        string sql,
        string query,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(query) ? sql : ErpDb.Positional(sql);
        ErpFirstPaint.ApplyIfUnset(command);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var like = Like(query);
            var args = new object?[sql.Count(ch => ch == '?')];
            Array.Fill(args, like);
            ErpDb.AddParameters(command, args);
        }

        var rows = new List<ErpMasterOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                kind,
                Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return rows;
    }

    private static string WhereLike(string query, IReadOnlyList<string> columns)
        => string.IsNullOrWhiteSpace(query) ? "" : " WHERE " + LikeOr(columns);

    private static string AndLike(string query, IReadOnlyList<string> columns)
        => string.IsNullOrWhiteSpace(query) ? "" : " AND (" + LikeOr(columns) + ")";

    private static string LikeOr(IReadOnlyList<string> columns)
        => string.Join(" OR ", columns.Select(column => "CAST(" + column + " AS CHAR) LIKE ? ESCAPE '\\\\'"));

    private static string Like(string query)
        => "%" + query.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal) + "%";

    private static IReadOnlyList<ErpMasterOption> Take(IReadOnlyList<ErpMasterOption> rows, int limit)
        => rows.Count <= limit ? rows : rows.Take(limit).ToArray();

    private static bool Contains(string value, string needle)
        => !string.IsNullOrWhiteSpace(value)
            && value.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static async Task<string> PreferColumnAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
        => await ColumnExistsAsync(connection, table, column, cancellationToken).ConfigureAwait(false)
            ? column
            : "name";

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var value = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return value > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var value = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return value > 0;
    }
}
