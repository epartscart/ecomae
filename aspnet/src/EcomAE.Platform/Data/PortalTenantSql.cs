namespace EcomAE.Platform.Data;

public static class PortalTenantSql
{
    public const string TableName = "epc_portal_tenants";

    public const string SelectActiveTenantByHost = """
        SELECT
            `site_key`,
            `hostname`,
            `db_name`,
            IFNULL(`db_user`, '') AS db_user,
            IFNULL(`db_password`, '') AS db_password,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            IFNULL(`scale_policy`, '') AS scale_policy
        FROM `epc_portal_tenants`
        WHERE `hostname` = @host
          AND `status` IN ('dns_pending', 'live')
          AND COALESCE(`is_active`, 1) = 1
        ORDER BY CASE WHEN IFNULL(TRIM(`db_name`), '') <> '' THEN 0 ELSE 1 END,
                 `is_demo` ASC, `erp_only_shared` ASC, `site_key` ASC
        LIMIT 1
        """;

    /// <summary>
    /// Same as <see cref="SelectActiveTenantByHost"/> but matches primary or www-alias hostname.
    /// Prefers exact <c>@h0</c> (request host) over <c>@h1</c> (www stripped/added).
    /// Do not reference legacy <c>db_pass</c> — MySQL errors if the column is absent and the
    /// registry catch falls through to seed (false <c>tenant_db_unbound</c> while portal has db_name).
    /// </summary>
    public const string SelectActiveTenantByHosts = """
        SELECT
            `site_key`,
            `hostname`,
            `db_name`,
            IFNULL(`db_user`, '') AS db_user,
            IFNULL(`db_password`, '') AS db_password,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            IFNULL(`scale_policy`, '') AS scale_policy
        FROM `epc_portal_tenants`
        WHERE `hostname` IN (@h0, @h1)
          AND `status` IN ('dns_pending', 'live')
          AND COALESCE(`is_active`, 1) = 1
        -- Prefer a row with shop db_name (www erp-only/shared stubs must not starve storefront).
        -- Then prefer exact request host, then non-demo live shop over erp_only_shared.
        ORDER BY CASE WHEN IFNULL(TRIM(`db_name`), '') <> '' THEN 0 ELSE 1 END,
                 CASE WHEN `hostname` = @h0 THEN 0 ELSE 1 END,
                 `is_demo` ASC, `erp_only_shared` ASC, `site_key` ASC
        LIMIT 1
        """;

    /// <summary>
    /// Core columns only — used when <c>dedicated_db</c>/<c>scale_policy</c> migrations are missing.
    /// </summary>
    public const string SelectActiveTenantByHostsMinimal = """
        SELECT
            `site_key`,
            `hostname`,
            `db_name`,
            IFNULL(`db_user`, '') AS db_user,
            IFNULL(`db_password`, '') AS db_password,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            0 AS dedicated_db,
            '' AS scale_policy
        FROM `epc_portal_tenants`
        WHERE `hostname` IN (@h0, @h1)
          AND `status` IN ('dns_pending', 'live')
          AND COALESCE(`is_active`, 1) = 1
        ORDER BY CASE WHEN IFNULL(TRIM(`db_name`), '') <> '' THEN 0 ELSE 1 END,
                 CASE WHEN `hostname` = @h0 THEN 0 ELSE 1 END,
                 `is_demo` ASC, `erp_only_shared` ASC, `site_key` ASC
        LIMIT 1
        """;

    public const string SelectTenantBySiteKey = """
        SELECT
            `site_key`,
            `hostname`,
            `db_name`,
            IFNULL(`db_user`, '') AS db_user,
            IFNULL(`db_password`, '') AS db_password,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            IFNULL(`scale_policy`, '') AS scale_policy
        FROM `epc_portal_tenants`
        WHERE `site_key` = @siteKey
        LIMIT 1
        """;
}
