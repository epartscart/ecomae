namespace EcomAE.Platform.Erp;

/// <summary>
/// Organization workspace tabs (enterprise area): the legal-entity register,
/// business units, consolidation group membership, and the user access matrix.
/// Feeds the same generic workspace grid as AP/warehouse specs.
/// </summary>
public static class ErpOrgWorkspaceSpecs
{
    private const string Companies = "/erp/companies-app";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["org_entities"] = new(
                ["Code", "Legal entity", "Country", "Currency", "Consolidated", "TRN", "Active"],
                "SELECT `code`, `name`, `country_code`, `currency_code`," +
                " IF(`consolidated`=1,'perimeter','managed'), IFNULL(`trn`,''), IF(`active`=1,'yes','no')" +
                " FROM `epc_erp_pm_legal_entities` ORDER BY `id` LIMIT 40",
                "No legal entities registered.",
                Companies,
                ErpShamFoundationService.EnsureSchemaAsync),

            ["org_business_units"] = new(
                ["Entity", "BU code", "Business unit", "Branch", "Manager", "Cost centre", "Status"],
                "SELECT IFNULL(e.`code`,'(none)'), b.`code`, b.`name`, b.`branch`, b.`manager_name`, b.`cost_centre`, b.`status`" +
                " FROM `epc_erp_org_business_units` b LEFT JOIN `epc_erp_pm_legal_entities` e ON e.`id` = b.`entity_id`" +
                " ORDER BY b.`entity_id`, b.`id` LIMIT 200",
                "No business units defined.",
                Companies,
                ErpShamFoundationService.EnsureSchemaAsync),

            ["org_group_members"] = new(
                ["Group", "Entity", "Name", "Ownership %", "Currency", "Method"],
                "SELECT g.`group_name`, m.`site_key`, m.`entity_name`, m.`ownership_pct`, m.`local_currency`, m.`consolidation`" +
                " FROM `epc_entity_members` m JOIN `epc_entity_groups` g ON g.`id` = m.`group_id`" +
                " ORDER BY g.`id`, m.`id` LIMIT 100",
                "No consolidation group yet.",
                Companies,
                ErpShamFoundationService.EnsureSchemaAsync),

            ["org_entity_accounts"] = new(
                ["Entity", "Account", "Name", "Type", "Active", "Suspense only"],
                "SELECT IFNULL(e.`code`,'?'), a.`coa_code`, c.`name`, c.`account_type`," +
                " IF(a.`active`=1,'yes','no'), IF(a.`suspense_only`=1,'yes','-')" +
                " FROM `epc_erp_org_entity_accounts` a" +
                " LEFT JOIN `epc_erp_pm_legal_entities` e ON e.`id` = a.`entity_id`" +
                " LEFT JOIN `epc_erp_coa_accounts` c ON c.`id` = a.`coa_id`" +
                " ORDER BY e.`code`, a.`coa_code` LIMIT 500",
                "No entity account activations yet.",
                Companies,
                ErpShamFoundationService.EnsureSchemaAsync),

            ["org_user_access"] = new(
                ["User", "Entity", "BU", "Role", "Actions", "Status"],
                "SELECT u.`user_email`, IFNULL(e.`code`,'(group-wide)'), IFNULL(b.`code`,'(entity-wide)'), u.`role`, u.`actions_csv`, u.`status`" +
                " FROM `epc_erp_org_user_access` u" +
                " LEFT JOIN `epc_erp_pm_legal_entities` e ON e.`id` = u.`entity_id`" +
                " LEFT JOIN `epc_erp_org_business_units` b ON b.`id` = u.`bu_id`" +
                " ORDER BY u.`user_email` LIMIT 400",
                "No access grants yet — grant users entity x BU access here.",
                Companies,
                ErpShamFoundationService.EnsureSchemaAsync),
        };
}
