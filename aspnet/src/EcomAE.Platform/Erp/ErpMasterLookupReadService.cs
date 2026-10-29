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
            "item" or "items" or "sku" or "inventory" or "item-code" or "item-id" => "item",
            "coa" or "chart" or "chart-account" or "chart-of-accounts" or "gl" or "gl-account" => "coa",
            "warehouse" or "warehouses" or "warehouse-id" or "wh" => "warehouse",
            "jw-metal-stock" or "metal-stock" or "jw-item" or "jw-stock" or "jw-stock-code" => "jw-metal-stock",
            "jw-karat" or "karat" or "karats" => "jw-karat",
            "jw-currency" or "jw-curr" => "jw-currency",
            "jw-rate-type" or "rate-type" => "jw-rate-type",
            "jw-type" or "jw-item-type" => "jw-type",
            "jw-brand" or "brand" or "brands" => "jw-brand",
            "jw-category" or "jw-cat" => "jw-category",
            "jw-sub-category" or "jw-subcategory" => "jw-sub-category",
            "jw-country" or "country" or "countries" => "jw-country",
            "jw-cc" or "jw-cost-centre" or "cc-making" or "cc-metal" => "jw-cc",
            "jw-addin" or "addin" or "addins" => "jw-addin",
            "jw-hs" or "hs-code" or "hscode" => "jw-hs",
            "jw-design" or "design" or "designs" => "jw-design",
            "jw-tag" or "tag" or "tags" or "barcode" => "jw-tag",
            "jw-pos-customer" or "pos-customer" => "jw-pos-customer",
            "jw-salesman" or "salesman" or "salesmen" => "jw-salesman",
            "jw-location" or "location" or "locations" => "jw-location",
            "project" or "projects" or "prj" => "project",
            "re-project" or "reproject" or "real-estate-project" => "re-project",
            "account" or "accounts" or "cash" or "bank" or "cash-account" => "account",
            "property" or "properties" => "property",
            "unit" or "units" => "unit",
            "tenant" or "tenants" => "tenant",
            "contractor" or "contractors" => "contractor",
            "business-unit" or "business-units" or "branch" or "branches" or "bu" => "business-unit",
            "legal-entity" or "legal-entities" or "legal-entity-id" => "legal-entity",
            "cost-centre" or "cost-center" or "cost-centres" or "cc" => "cost-centre",
            _ => string.Empty,
        };
    }

    public static string MasterHref(string kind)
        => kind switch
        {
            "customer" => "/erp/receivables-app",
            "supplier" => "/erp/suppliers-app",
            "item" => "/erp/inventory-stock-app",
            "coa" => "/erp/coa-accounts-app",
            "warehouse" => "/erp/warehouses-app",
            "jw-metal-stock" => "/erp/jewellery-stock-verification-app?tab=jw_metal_stock",
            "jw-karat" => "/erp/jewellery-masters-app?tab=jw_karat",
            "jw-currency" => "/erp/jewellery-masters-app?tab=jw_currency",
            "jw-rate-type" => "/erp/jewellery-masters-app?tab=jw_rate_type",
            "jw-type" or "jw-brand" or "jw-category" or "jw-sub-category" or "jw-country" or "jw-cc" or "jw-hs"
                => "/erp/jewellery-stock-verification-app?tab=jw_metal_stock",
            "jw-addin" => "/erp/jewellery-fixing-app?tab=jw_metal_purchase",
            "jw-design" => "/erp/jewellery-masters-app?tab=jw_design",
            "jw-tag" or "jw-location" => "/erp/jewellery-masters-app?tab=jw_barcode",
            "jw-pos-customer" => "/erp/jewellery-retail-app?tab=jw_retail_sales",
            "jw-salesman" => "/erp/jewellery-retail-app?tab=jw_retail_sales",
            "project" => "/erp/project-accounting-app",
            "re-project" => "/erp/real-estate-app?tab=projects",
            "account" => "/erp/cash-accounts-app",
            "property" => "/erp/real-estate-app?tab=properties",
            "unit" => "/erp/real-estate-app?tab=properties",
            "tenant" => "/erp/real-estate-app?tab=leases",
            "contractor" => "/erp/real-estate-app?tab=construction",
            "business-unit" => "/erp/organization-app?tab=business_units",
            "legal-entity" => "/erp/organization-app?tab=legal_entities",
            "cost-centre" => "/erp/organization-app?tab=cost_centres",
            _ => "/erp",
        };

    public static string RecordAppHref(string kind)
        => kind switch
        {
            "item" => "/erp/product-info-app",
            _ => MasterHref(kind),
        };

    /// <summary>
    /// Pages whose entry forms live on a separate ecom-style entry view:
    /// the "add new" shortcut must land there, not on the list page.
    /// </summary>
    public static string MasterEntryHref(string kind)
        => kind switch
        {
            "supplier" => "/erp/suppliers-app?tab=entry",
            "coa" => "/erp/coa-accounts-app?view=entry",
            "warehouse" => "/erp/warehouses-app?tab=entry",
            _ => MasterHref(kind),
        };

    public static string RecordQueryKey(string kind)
        => kind switch
        {
            "customer" => "customer_id",
            "supplier" => "supplier_id",
            "item" => "pm_item_id",
            "coa" => "account_id",
            "warehouse" => "warehouse_id",
            "account" => "account_id",
            _ => "",
        };

    public static string KindLabel(string kind)
        => kind switch
        {
            "customer" => "Customer",
            "supplier" => "Supplier",
            "item" => "Item",
            "coa" => "Chart account",
            "warehouse" => "Warehouse",
            "jw-metal-stock" => "Metal stock",
            "jw-karat" => "Karat",
            "jw-currency" => "Currency",
            "jw-rate-type" => "Rate type",
            "jw-type" => "Type",
            "jw-brand" => "Brand",
            "jw-category" => "Category",
            "jw-sub-category" => "Sub-category",
            "jw-country" => "Country",
            "jw-cc" => "Cost centre",
            "jw-addin" => "Add-in",
            "jw-design" => "Design",
            "jw-tag" => "Tag / barcode",
            "jw-pos-customer" => "POS customer",
            "jw-salesman" => "Salesman",
            "jw-location" => "Location",
            "jw-hs" => "HS code",
            "project" => "Project",
            "re-project" => "Development project",
            "account" => "Cash / bank account",
            "property" => "Property",
            "unit" => "Unit",
            "tenant" => "Tenant",
            "contractor" => "Contractor",
            "business-unit" => "Business unit",
            "legal-entity" => "Legal entity",
            "cost-centre" => "Cost centre",
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
            "supplier" => await LoadApprovedSuppliersAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "item" => await LoadItemsAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "coa" => await LoadChartAccountsAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "warehouse" => await LoadWarehousesAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "jw-metal-stock" => await LoadMetalStockAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "jw-karat" => await LoadKaratsAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "jw-currency" => await LoadSimpleAsync(
                connection, "jw-currency", "epc_jewel_currency", "id", "curr_code", "description", "curr_code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "jw-rate-type" => await LoadSimpleAsync(
                connection, "jw-rate-type", "epc_jewel_rate_type", "id", "rate_type", "currency", "rate_type",
                query, limit, cancellationToken).ConfigureAwait(false),
            "jw-type" => await LoadDistinctAsync(connection, "jw-type", "epc_jewel_metal_stock", "type", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-brand" => await LoadDistinctAsync(connection, "jw-brand", "epc_jewel_metal_stock", "brand", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-category" => await LoadDistinctAsync(connection, "jw-category", "epc_jewel_metal_stock", "category", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-sub-category" => await LoadDistinctAsync(connection, "jw-sub-category", "epc_jewel_metal_stock", "sub_category", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-country" => await LoadDistinctAsync(connection, "jw-country", "epc_jewel_metal_stock", "country", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-cc" => await LoadCostCentresAsync(connection, query, limit, cancellationToken).ConfigureAwait(false),
            "jw-addin" => await LoadDistinctAsync(connection, "jw-addin", "epc_jewel_voucher_addins", "addin_code", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-hs" => await LoadDistinctAsync(connection, "jw-hs", "epc_jewel_metal_stock", "hs_code", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-design" => await LoadSimpleAsync(
                connection, "jw-design", "epc_jewel_design", "id", "design_code", "description", "design_code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "jw-tag" => await LoadSimpleAsync(
                connection, "jw-tag", "epc_jw_tags", "id", "tag_no", "description", "tag_no",
                query, limit, cancellationToken).ConfigureAwait(false),
            "jw-pos-customer" => await LoadSimpleAsync(
                connection, "jw-pos-customer", "epc_jewel_pos_customer", "id", "customer_code", "customer_name", "customer_code",
                query, limit, cancellationToken).ConfigureAwait(false),
            "jw-salesman" => await LoadDistinctAsync(connection, "jw-salesman", "epc_jewel_voucher", "salesman", query, limit, cancellationToken).ConfigureAwait(false),
            "jw-location" => await LoadDistinctAsync(connection, "jw-location", "epc_jw_tags", "location", query, limit, cancellationToken).ConfigureAwait(false),
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
            "business-unit" => await LoadSimpleAsync(
                connection, "business-unit", "epc_erp_pm_business_units", "id", "code", "name", "code",
                query, limit, cancellationToken, requireActive: true).ConfigureAwait(false),
            "legal-entity" => await LoadSimpleAsync(
                connection, "legal-entity", "epc_erp_pm_legal_entities", "id", "code", "name", "code",
                query, limit, cancellationToken, requireActive: true).ConfigureAwait(false),
            "cost-centre" => await LoadSimpleAsync(
                connection, "cost-centre", "epc_cc_centers", "id", "code", "name", "code",
                query, limit, cancellationToken, requireActive: true).ConfigureAwait(false),
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

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadMetalStockAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_jewel_metal_stock", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var extra = "TRIM(CONCAT_WS(' · ', NULLIF(TRIM(`metal`),''), NULLIF(TRIM(CAST(`purity` AS CHAR)),''), NULLIF(TRIM(`karat`),'')))";
        var sql = "SELECT `id`, IFNULL(`item_code`,''), IFNULL(`description`,''), " + extra
            + " FROM `epc_jewel_metal_stock`"
            + WhereLike(query, ["CAST(`id` AS CHAR)", "`item_code`", "`description`", "`karat`", "`metal`"])
            + " ORDER BY `item_code` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, "jw-metal-stock", sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadKaratsAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_jewel_karat_master", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var sql = "SELECT `id`, IFNULL(`karat_code`,''), IFNULL(`description`,''), CAST(`std_purity` AS CHAR)"
            + " FROM `epc_jewel_karat_master`"
            + WhereLike(query, ["CAST(`id` AS CHAR)", "`karat_code`", "`description`"])
            + " ORDER BY `karat_code` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, "jw-karat", sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadCostCentresAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var fromStock = await LoadDistinctAsync(
            connection, "jw-cc", "epc_jewel_metal_stock", "cc_making", query, limit, cancellationToken).ConfigureAwait(false);
        var metal = await LoadDistinctAsync(
            connection, "jw-cc", "epc_jewel_metal_stock", "cc_metal", query, limit, cancellationToken).ConfigureAwait(false);
        var org = await LoadSimpleAsync(
            connection, "jw-cc", "epc_erp_cost_centres", "id", "code", "name", "code",
            query, limit, cancellationToken, requireActive: true).ConfigureAwait(false);
        return fromStock.Concat(metal).Concat(org)
            .GroupBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(row => !string.IsNullOrWhiteSpace(row.Code))
            .Take(limit)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadDistinctAsync(
        DbConnection connection,
        string kind,
        string table,
        string column,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, table, column, cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var sql = "SELECT MIN(`id`), TRIM(`" + column + "`), TRIM(`" + column + "`), '' FROM `" + table + "`"
            + " WHERE TRIM(`" + column + "`) <> ''"
            + AndLike(query, ["TRIM(`" + column + "`)"] )
            + " GROUP BY TRIM(`" + column + "`) ORDER BY TRIM(`" + column + "`) ASC LIMIT "
            + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, kind, sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadChartAccountsAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_erp_coa_accounts", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }

        var sql = "SELECT `id`, IFNULL(`code`,''), IFNULL(`name`,''), IFNULL(`account_type`,'')"
            + " FROM `epc_erp_coa_accounts` WHERE `active` = 1"
            + AndLike(query, ["CAST(`id` AS CHAR)", "`code`", "`name`", "`account_type`"])
            + " ORDER BY `code` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
        return await ReadRowsAsync(connection, "coa", sql, query, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadWarehousesAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpMasterOption>();
        if (await TableExistsAsync(connection, "epc_erp_inv_warehouses", cancellationToken).ConfigureAwait(false))
        {
            var sql = "SELECT `id`, IFNULL(NULLIF(TRIM(`code`), ''), CAST(`id` AS CHAR)), IFNULL(`name`,''), 'Stock'"
                + " FROM `epc_erp_inv_warehouses` WHERE `active` = 1"
                + AndLike(query, ["CAST(`id` AS CHAR)", "`code`", "`name`"])
                + " ORDER BY `code` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
            rows.AddRange(await ReadRowsAsync(connection, "warehouse", sql, query, cancellationToken).ConfigureAwait(false));
        }

        if (await TableExistsAsync(connection, "epc_warehouses", cancellationToken).ConfigureAwait(false))
        {
            var sql = "SELECT `id`, IFNULL(NULLIF(TRIM(`code`), ''), CAST(`id` AS CHAR)), IFNULL(`name`,''), IFNULL(`type`,'')"
                + " FROM `epc_warehouses` WHERE `is_active` = 1"
                + AndLike(query, ["CAST(`id` AS CHAR)", "`code`", "`name`", "`type`"])
                + " ORDER BY `code` ASC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
            var seen = rows.Select(row => row.Id).ToHashSet();
            foreach (var row in await ReadRowsAsync(connection, "warehouse", sql, query, cancellationToken).ConfigureAwait(false))
            {
                if (seen.Add(row.Id))
                {
                    rows.Add(row);
                }
            }
        }

        return rows.Count <= limit ? rows : rows.Take(limit).ToArray();
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

    private static async Task<IReadOnlyList<ErpMasterOption>> LoadApprovedSuppliersAsync(
        DbConnection connection,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var preferred = await ColumnExistsAsync(connection, "epc_erp_suppliers", "supplier_code", cancellationToken).ConfigureAwait(false)
            ? "supplier_code"
            : await PreferColumnAsync(connection, "epc_erp_suppliers", "vendor_account", cancellationToken).ConfigureAwait(false);
        var rows = await LoadSimpleAsync(
            connection, "supplier", "epc_erp_suppliers", "id", "name", "name", preferred,
            query, limit, cancellationToken, requireActive: true).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_suppliers", "approval_status", cancellationToken).ConfigureAwait(false))
        {
            return rows;
        }

        var allowed = new HashSet<long>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id` FROM `epc_erp_suppliers` WHERE IFNULL(`approval_status`,'') IN ('','approved')";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                allowed.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        return rows.Where(r => allowed.Contains(r.Id)).ToArray();
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

    private static Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
        => ErpDb.TableKnownAsync(connection, table, cancellationToken);

    private static Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
        => ErpDb.ColumnKnownAsync(connection, table, column, cancellationToken);
}
