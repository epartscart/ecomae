using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpJwIndusDataTable(
    ErpJwIndusTemplate Template,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    long Total,
    string Message);

/// <summary>Live view of where each INDUS CSV lands in the ERP: row counts, every stored field, and master entry for parties / POS customers.</summary>
public interface IErpJwIndusDataService
{
    Task<IReadOnlyDictionary<string, long>> CountsAsync(CancellationToken cancellationToken = default);

    Task<ErpJwIndusDataTable?> TableAsync(string key, int limit = 300, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveMasterAsync(
        string key,
        IReadOnlyDictionary<string, string> values,
        string actor,
        CancellationToken cancellationToken = default);
}

public sealed class ErpJwIndusDataService : IErpJwIndusDataService
{
    public static readonly IReadOnlySet<string> EditableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "parties", "pos_customers" };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwIndusDataService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>ERP screen where the template's data is used day to day.</summary>
    public static string ScreenHref(string key) => key switch
    {
        "coa" => "/erp/coa-accounts-app",
        "parties" => "/erp/jewellery/workspace?tab=indus&key=parties",
        "pos_customers" => "/erp/jewellery/workspace?tab=indus&key=pos_customers",
        "currencies" => "/erp/jewellery-masters-app?tab=jw_currency",
        "karats" => "/erp/jewellery-masters-app?tab=jw_karat",
        "rate_types" => "/erp/jewellery-masters-app?tab=jw_rate_type",
        "daily_rates" => "/erp/jewellery-masters-app?tab=gold_rate",
        "metal_stock" => "/erp/jewellery-stock-verification-app?tab=jw_metal_stock",
        "designs" => "/erp/jewellery-masters-app?tab=jw_design",
        "diamonds" => "/erp/jewellery-masters-app?tab=jw_diamond",
        "color_stones" => "/erp/jewellery-masters-app?tab=jw_color_stone",
        "pearls" => "/erp/jewellery-masters-app?tab=jw_pearl",
        "tags" => "/erp/jewellery-masters-app?tab=jewellery_tag",
        "vouchers" or "voucher_lines" or "voucher_receipts" => "/erp/jewellery-retail-app?tab=jw_retail_sales",
        "voucher_accounts" => "/erp/jewellery-retail-app?tab=jw_journal_voucher",
        "fixing" => "/erp/jewellery-fixing-app?tab=jw_purchase_fixing",
        "petty_cash" => "/erp/jewellery-retail-app?tab=jw_petty_cash",
        "repairs" or "repair_items" => "/erp/jewellery-repairs-app?tab=jw_repairs",
        "stock_verifications" or "stock_verification_lines" => "/erp/jewellery-stock-verification-app?tab=jw_stock_verification",
        _ => "/erp/jewellery/workspace?tab=indus",
    };

    public async Task<IReadOnlyDictionary<string, long>> CountsAsync(CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!_connections.IsConfigured) return counts;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);
        foreach (var t in ErpJwIndusImportSpec.Templates)
        {
            if (!await ErpDb.TableKnownAsync(connection, t.Table, cancellationToken).ConfigureAwait(false))
            {
                counts[t.Key] = -1;
                continue;
            }

            var scoped = await CompanyScopedAsync(connection, t, cancellationToken).ConfigureAwait(false);
            counts[t.Key] = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `" + t.Table + "`" + (scoped ? " WHERE `company_id` IN (0,?)" : "")),
                cancellationToken,
                scoped ? new object?[] { companyId } : Array.Empty<object?>()).ConfigureAwait(false);
        }

        return counts;
    }

    public async Task<ErpJwIndusDataTable?> TableAsync(string key, int limit = 300, CancellationToken cancellationToken = default)
    {
        var t = ErpJwIndusImportSpec.Templates.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        if (t is null) return null;
        if (!_connections.IsConfigured) return new(t, [], [], 0, "Database is not configured.");
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpDb.TableKnownAsync(connection, t.Table, cancellationToken).ConfigureAwait(false))
        {
            return new(t, [], [], 0, "Table " + t.Table + " is not provisioned yet.");
        }

        var cols = new List<ErpJwIndusColumn>();
        foreach (var c in t.Columns.Where(c => c.Column.Length > 0).DistinctBy(c => c.Column, StringComparer.OrdinalIgnoreCase))
        {
            if (await ErpDb.ColumnKnownAsync(connection, t.Table, c.Column, cancellationToken).ConfigureAwait(false)) cols.Add(c);
        }

        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);
        var scoped = await CompanyScopedAsync(connection, t, cancellationToken).ConfigureAwait(false);
        var where = scoped ? " WHERE `company_id` IN (0,?)" : "";
        var total = await ErpDb.LongAsync(
            connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `" + t.Table + "`" + where), cancellationToken,
            scoped ? new object?[] { companyId } : Array.Empty<object?>()).ConfigureAwait(false);

        var rows = new List<IReadOnlyList<string>>();
        await using (var command = connection.CreateCommand())
        {
            var select = string.Join(",", cols.Select(c => "IFNULL(CAST(`" + c.Column + "` AS CHAR),'')"));
            command.CommandText = ErpDb.Positional(
                "SELECT `id`" + (select.Length > 0 ? "," + select : "") + " FROM `" + t.Table + "`" + where
                + " ORDER BY `id` DESC LIMIT " + Math.Clamp(limit, 1, 1000).ToString(CultureInfo.InvariantCulture));
            if (scoped) ErpDb.AddParameters(command, companyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new List<string>(cols.Count + 1);
                for (var i = 0; i <= cols.Count; i++)
                {
                    row.Add(reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "");
                }

                rows.Add(row);
            }
        }

        var headers = new List<string> { "Id" };
        headers.AddRange(cols.Select(c => c.IndusField.Length > 0 ? c.IndusField : c.Header));
        return new(t, headers, rows, total, "");
    }

    public async Task<ErpSimpleWriteResult> SaveMasterAsync(
        string key,
        IReadOnlyDictionary<string, string> values,
        string actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var t = ErpJwIndusImportSpec.Templates.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
        if (t is null || !EditableKeys.Contains(t.Key))
        {
            return ErpSimpleWriteResult.Fail("invalid", "This INDUS master cannot be edited here.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var cols = t.Columns.Where(c => c.Column.Length > 0).ToList();
        var args = new List<object?>();
        foreach (var c in cols)
        {
            var raw = values.TryGetValue(c.Header, out var v) ? (v ?? "").Trim() : "";
            if (c.Required && raw.Length == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", c.IndusField + " is required.");
            }

            args.Add(Parse(c, raw, out var error));
            if (error is not null)
            {
                return ErpSimpleWriteResult.Fail("invalid", c.IndusField + ": " + error);
            }
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await ErpDb.TableKnownAsync(connection, t.Table, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Table " + t.Table + " is not provisioned.");
        }

        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);
        var names = new List<string> { "company_id" };
        names.AddRange(cols.Select(c => c.Column));
        args.Insert(0, companyId);
        if (t.CreatedByColumn is { Length: > 0 } createdBy)
        {
            names.Add(createdBy);
            args.Add(actor.Length > 80 ? actor[..80] : actor);
        }

        var keyColumn = cols.First(c => c.Required).Column;
        var update = string.Join(",", cols.Where(c => c.Column != keyColumn).Select(c => "`" + c.Column + "`=VALUES(`" + c.Column + "`)"));
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `" + t.Table + "` (" + string.Join(",", names.Select(n => "`" + n + "`")) + ") VALUES ("
                + string.Join(",", names.Select(_ => "?")) + ") ON DUPLICATE KEY UPDATE " + update),
            cancellationToken,
            args.ToArray()).ConfigureAwait(false);
        var code = Convert.ToString(args[1], CultureInfo.InvariantCulture) ?? "";
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `" + t.Table + "` WHERE `company_id`=? AND `" + keyColumn + "`=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return id > 0
            ? ErpSimpleWriteResult.Ok(t.Title + " " + code + " saved", id)
            : ErpSimpleWriteResult.Fail("invalid", "Save failed.");
    }

    private static object? Parse(ErpJwIndusColumn c, string raw, out string? error)
    {
        error = null;
        switch (c.Type)
        {
            case ErpJwIndusColType.Decimal:
                if (raw.Length == 0) return 0m;
                if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
                error = "not a number";
                return null;
            case ErpJwIndusColType.Int:
                if (raw.Length == 0) return 0;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
                error = "not a whole number";
                return null;
            case ErpJwIndusColType.Date:
                if (raw.Length == 0) return null;
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt.Date;
                error = "not a date";
                return null;
            case ErpJwIndusColType.Flag:
                return raw is "1" or "true" or "on" or "Y" or "y" ? 1 : 0;
            default:
                return c.MaxLength > 0 && raw.Length > c.MaxLength ? raw[..c.MaxLength] : raw;
        }
    }

    private static async Task<bool> CompanyScopedAsync(DbConnection connection, ErpJwIndusTemplate t, CancellationToken ct)
        => t.HasCompany && await ErpDb.ColumnKnownAsync(connection, t.Table, "company_id", ct).ConfigureAwait(false);
}
