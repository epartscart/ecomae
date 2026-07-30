namespace EcomAE.Platform.Data;

public static class PortalTenantSql
{
    public const string TableName = "epc_portal_tenants";

    public const string SelectActiveTenantByHost = """
        SELECT
            `site_key`,
            `hostname`,
            `db_name`,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            `scale_policy`
        FROM `epc_portal_tenants`
        WHERE `hostname` = @host
          AND `status` IN ('dns_pending', 'live')
          AND COALESCE(`is_active`, 1) = 1
        ORDER BY `is_demo` ASC, `erp_only_shared` DESC, `site_key` ASC
        LIMIT 1
        """;

    public const string SelectTenantBySiteKey = """
        SELECT
            `site_key`,
            `hostname`,
            `db_name`,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            `scale_policy`
        FROM `epc_portal_tenants`
        WHERE `site_key` = @siteKey
        LIMIT 1
        """;
}
