namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP erp_tabs_* → ASP.NET primary app map (PHP remains write-authoritative).
/// Generated for full tab coverage; unmapped tabs fall through to area hubs or /erp/module-app.
/// </summary>
public static class ErpPhpTabRouteMap
{
    public const string ModuleAppPath = "/erp/module-app";

    private static readonly Dictionary<string, string> TabRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["accounting"] = "/erp/gl-journals-app",
        ["accounting_automation"] = "/erp/fin-advanced-app?tab=accounting_automation",
        ["accounts"] = "/erp/accounts-summary-app",
        ["aftersales"] = "/erp/returns-rma-app",
        ["agenda"] = "/erp/agenda-app",
        ["aging"] = "/erp/aging-app",
        ["ai_advisor"] = "/erp/guide-app?tab=ai_advisor",
        ["ai_assistant"] = "/erp/guide-app?tab=ai_assistant",
        ["aml_compliance"] = "/erp/aml-compliance-app",
        ["ap_aging"] = "/erp/aging-app",
        ["ap_setup"] = "/erp/suppliers-app?tab=ap_setup",
        ["approval"] = "/erp/approvals-app",
        ["approvals"] = "/erp/approvals-app",
        ["ar_aging"] = "/erp/aging-app",
        ["ar_setup"] = "/erp/contacts-app?tab=ar_setup",
        ["assets"] = "/erp/fixed-assets-app",
        ["audit"] = "/erp/audit-trail-app",
        ["balance_sheet"] = "/erp/report-center-app",
        ["bank_entries"] = "/erp/cash-entries-app",
        ["bank_instruments"] = "/erp/bank-reconciliation-app?tab=bank_instruments",
        ["bank_setup"] = "/erp/cash-accounts-app?tab=bank_setup",
        ["bank_recon"] = "/erp/bank-reconciliation-app",
        ["banking"] = "/erp/cash-accounts-app",
        ["barcode_purchase"] = "/erp/purchase-orders-app?tab=barcode_purchase",
        ["blockchain_proofs"] = "/erp/blockchain-proofs-app",
        ["budget_planning"] = "/erp/budgets-app",
        ["budgeting"] = "/erp/budgets-app",
        ["business_units"] = "/erp/consolidations-app?tab=business_units",
        ["card_reader"] = "/erp/pos-overview-app",
        ["cash"] = "/erp/cash-accounts-app",
        ["cash_bank"] = "/erp/cash-accounts-app",
        ["cash_forecast"] = "/erp/cash-accounts-app?tab=cash_forecast",
        ["chart_of_accounts"] = "/erp/coa-accounts-app",
        ["coa"] = "/erp/coa-accounts-app",
        ["collections"] = "/erp/collections-dunning-app",
        ["compliance"] = "/erp/soc2-compliance-app",
        ["consolidation_bu"] = "/erp/consolidations-app",
        ["consolidation_group"] = "/erp/consolidations-app",
        ["consolidation_ic"] = "/erp/consolidations-app",
        ["contacts"] = "/erp/contacts-app",
        ["contracts"] = "/erp/contracts-app",
        ["cost_models"] = "/erp/cost-models-app",
        ["crm"] = "/erp/crm-tickets-app",
        ["crm_integration"] = "/erp/crm-board-app?tab=crm_integration",
        ["custom_shipping"] = "/erp/fulfillment-queue-app?tab=custom_shipping",
        ["customer_groups"] = "/erp/customer-groups-app",
        ["dashboard"] = "/erp",
        ["data_import"] = "/erp/data-migrations-app",
        ["data_migration"] = "/erp/data-migrations-app",
        ["delivery_notes"] = "/erp/delivery-notes-app",
        ["doc_attachment"] = "/erp/doc-attachments-app",
        ["doc_expiry"] = "/erp/doc-expiry-app",
        ["doc_formats"] = "/erp/documents-app?tab=doc_formats",
        ["document_control"] = "/erp/doc-expiry-app",
        ["documents"] = "/erp/documents-app",
        ["drilldown"] = "/erp/report-center-app?tab=drilldown",
        ["ecommerce_integration"] = "/erp/integrations-app",
        ["einvoice"] = "/erp/einvoice-documents-app",
        ["elec_reporting"] = "/erp/electronic-reporting-app",
        ["enterprise_reports"] = "/erp/report-center-app?tab=enterprise_reports",
        ["erp_setup"] = "/erp/tenant-config-app?tab=erp_setup",
        ["exec_dashboard"] = "/erp",
        ["expense_reports"] = "/erp/expense-reports-app",
        ["ext_reports"] = "/erp/tax-external-reporting-app",
        ["external_reports"] = "/erp/tax-external-reporting-app",
        ["favorites"] = "/erp/workspace-favorites-app",
        ["fin_advanced"] = "/erp/fin-advanced-app",
        ["fix_unfix"] = "/erp/jewellery-fixing-app?tab=fix_unfix",
        ["fixed_assets"] = "/erp/fixed-assets-app",
        ["fulfilment"] = "/erp/fulfillment-queue-app",
        ["general_journal"] = "/erp/gl-journals-app",
        ["gl"] = "/erp/gl-journals-app",
        ["gold_rate"] = "/erp/jewellery-masters-app?tab=gold_rate",
        ["gold_scheme"] = "/erp/jewellery-masters-app?tab=gold_scheme",
        ["guide"] = "/erp/guide-app",
        ["hr"] = "/erp/hr-overview-app",
        ["hr_law"] = "/erp/hr-overview-app?tab=hr_law",
        ["hr_ops"] = "/erp/hr-overview-app?tab=hr_ops",
        ["industry_intel"] = "/erp/industry-packs-app",
        ["insurance"] = "/erp/insurance-compliance-app",
        ["integration"] = "/erp/integrations-app",
        ["inv_groups"] = "/erp/stock-transfers-app",
        ["inventory"] = "/erp/inventory-stock-app",
        ["inventory_report"] = "/erp/inventory-report-app",
        ["inventory_stock"] = "/erp/inventory-stock-app",
        ["invoices"] = "/erp/invoices-app",
        ["jewellery"] = "/erp/jewellery-retail-app?tab=jewellery",
        ["jewellery_tag"] = "/erp/jewellery-masters-app?tab=jewellery_tag",
        ["journals"] = "/erp/gl-journals-app",
        ["jw_barcode"] = "/erp/jewellery-masters-app?tab=jw_barcode",
        ["jw_color_stone"] = "/erp/jewellery-masters-app?tab=jw_color_stone",
        ["jw_currency"] = "/erp/jewellery-masters-app?tab=jw_currency",
        ["jw_design"] = "/erp/jewellery-masters-app?tab=jw_design",
        ["jw_diamond"] = "/erp/jewellery-masters-app?tab=jw_diamond",
        ["jw_diamond_purchase"] = "/erp/jewellery-fixing-app?tab=jw_diamond_purchase",
        ["jw_journal_voucher"] = "/erp/gl-journals-app?tab=jw_journal_voucher",
        ["jw_karat"] = "/erp/jewellery-masters-app?tab=jw_karat",
        ["jw_metal_purchase"] = "/erp/jewellery-fixing-app?tab=jw_metal_purchase",
        ["jw_metal_sales"] = "/erp/jewellery-retail-app?tab=jw_metal_sales",
        ["jw_metal_stock"] = "/erp/jewellery-stock-verification-app?tab=jw_metal_stock",
        ["jw_pearl"] = "/erp/jewellery-masters-app?tab=jw_pearl",
        ["jw_petty_cash"] = "/erp/cash-accounts-app?tab=jw_petty_cash",
        ["jw_pos_advance"] = "/erp/pos-overview-app",
        ["jw_purchase_fixing"] = "/erp/jewellery-fixing-app?tab=jw_purchase_fixing",
        ["jw_purchase_window"] = "/erp/jewellery-fixing-app?tab=jw_purchase_window",
        ["jw_rate_type"] = "/erp/jewellery-masters-app?tab=jw_rate_type",
        ["jw_repair_delivery"] = "/erp/jewellery-repairs-app?tab=jw_repair_delivery",
        ["jw_repair_receipt"] = "/erp/jewellery-repairs-app?tab=jw_repair_receipt",
        ["jw_repair_register"] = "/erp/jewellery-repairs-app?tab=jw_repair_register",
        ["jw_repair_sale"] = "/erp/jewellery-repairs-app?tab=jw_repair_sale",
        ["jw_repair_search"] = "/erp/jewellery-repairs-app?tab=jw_repair_search",
        ["jw_repair_transfer"] = "/erp/jewellery-repairs-app?tab=jw_repair_transfer",
        ["jw_repairs"] = "/erp/jewellery-repairs-app?tab=jw_repairs",
        ["jw_retail_sales"] = "/erp/jewellery-retail-app?tab=jw_retail_sales",
        ["jw_sales_analysis"] = "/erp/jewellery-retail-app?tab=jw_sales_analysis",
        ["jw_sales_fixing"] = "/erp/jewellery-fixing-app?tab=jw_sales_fixing",
        ["jw_sales_return"] = "/erp/jewellery-retail-app?tab=jw_sales_return",
        ["jw_seed_data"] = "/erp/jewellery-masters-app?tab=jw_seed_data",
        ["jw_stock_balance"] = "/erp/jewellery-stock-verification-app?tab=jw_stock_balance",
        ["jw_stock_verification"] = "/erp/jewellery-stock-verification-app?tab=jw_stock_verification",
        ["jw_tourist_vat"] = "/erp/uae-tax-compliance-app?tab=jw_tourist_vat",
        ["jw_trial_balance"] = "/erp/report-center-app?tab=jw_trial_balance",
        ["jw_workshop_receive"] = "/erp/jewellery-repairs-app?tab=jw_workshop_receive",
        ["knowledge"] = "/erp/guide-app",
        ["knowledge_base"] = "/erp/guide-app",
        ["landed_cost"] = "/erp/landed-cost-app",
        ["landed_cost_v2"] = "/erp/landed-cost-app",
        ["leads"] = "/erp/crm-board-app",
        ["ledger"] = "/erp/stock-movements-app",
        ["listing"] = "/erp/projects-overview-app?tab=listing",
        ["manufacturing"] = "/erp/production-overview-app",
        ["marketing"] = "/erp/marketing-app",
        ["master_planning"] = "/erp/order-planning-app?tab=master_planning",
        ["mfg_planning"] = "/erp/production-overview-app?tab=mfg_planning",
        ["movements"] = "/erp/stock-movements-app",
        ["multi_entity"] = "/erp/consolidations-app?tab=multi_entity",
        ["on_premises"] = "/erp/on-premises-app",
        ["onpremises"] = "/erp/on-premises-app",
        ["opening"] = "/erp/opening-app",
        ["opening_balances"] = "/erp/opening-app",
        ["opportunities"] = "/erp/crm-opportunities-app",
        ["order_planning"] = "/erp/order-planning-app",
        ["org_admin"] = "/erp/tenant-config-app?tab=org_admin",
        ["overview"] = "/erp",
        ["payables"] = "/erp/payables-app",
        ["payment_batches"] = "/erp/payment-batches-app",
        ["payroll"] = "/erp/payroll-app",
        ["performance"] = "/erp/performance-app",
        ["petty_cash"] = "/erp/cash-accounts-app?tab=petty_cash",
        ["pl"] = "/erp/report-center-app",
        ["platform"] = "/erp/tenant-config-app?tab=platform",
        ["print_designer"] = "/erp/print-designer-app",
        ["process_flow"] = "/erp/process-flow-tasks-app",
        ["processflow"] = "/erp/process-flow-tasks-app",
        ["procurement_categories"] = "/erp/procurement-categories-app",
        ["procurement_link"] = "/erp/purchase-requests-app",
        ["product_info"] = "/erp/product-info-app",
        ["project_accounting"] = "/erp/project-accounting-app",
        ["projects"] = "/erp/projects-overview-app",
        ["proposals"] = "/erp/sales-quotations-app",
        ["purchase_orders"] = "/erp/purchase-orders-app",
        ["purchase_requisitions"] = "/erp/purchase-requests-app",
        ["purchaseorders"] = "/erp/purchase-orders-app",
        ["purchases"] = "/erp/purchases-app",
        ["quality"] = "/erp/quality-app",
        ["quotations"] = "/erp/sales-quotations-app",
        ["revenue"] = "/erp/report-center-app?tab=revenue",
        ["rc_finance"] = "/erp/report-center-app",
        ["receivables"] = "/erp/receivables-app",
        ["reconciliation"] = "/erp/bank-reconciliation-app",
        ["recruitment"] = "/erp/recruitment-app",
        ["report_center"] = "/erp/report-center-app",
        ["report_scheduler"] = "/erp/report-scheduler-app",
        ["reportcenter"] = "/erp/report-center-app",
        ["reports"] = "/erp/report-center-app",
        ["retail_barcode"] = "/erp/jewellery-retail-app?tab=retail_barcode",
        ["retail_commerce"] = "/erp/jewellery-retail-app?tab=retail_commerce",
        ["rfid"] = "/erp/rfid-app",
        ["rfq"] = "/erp/rfq-app",
        ["sales_orders"] = "/erp/sales-orders-app",
        ["sales_quotations"] = "/erp/sales-quotations-app",
        ["salesorders"] = "/erp/sales-orders-app",
        ["security_roles"] = "/erp/tenant-config-app?tab=security_roles",
        ["setup"] = "/erp/tenant-config-app?tab=setup",
        ["shortcut_icons"] = "/erp/workspace-favorites-app",
        ["sla"] = "/erp/crm-tickets-app?tab=sla",
        ["staff"] = "/erp/staff-app",
        ["stock"] = "/erp/inventory-stock-app",
        ["subscriptions"] = "/erp/sales-orders-app?tab=subscriptions",
        ["supplier_portal"] = "/erp/suppliers-app?tab=supplier_portal",
        ["tax_compliance"] = "/erp/uae-tax-compliance-app",
        ["tenant_config"] = "/erp/tenant-config-app",
        ["three_way_match"] = "/erp/three-way-match-app",
        ["tickets"] = "/erp/crm-tickets-app",
        ["tourist_refund"] = "/erp/vat-app?tab=tourist_refund",
        ["transfers"] = "/erp/stock-transfers-app",
        ["vat"] = "/erp/vat-app",
        ["vat_refund"] = "/erp/vat-app?tab=vat_refund",
        ["vat_return"] = "/erp/vat-app?tab=vat_return",
        ["vendors"] = "/erp/suppliers-app",
        ["virtual_warehouse"] = "/erp/warehouses-app?tab=virtual_warehouse",
        ["warehouse"] = "/erp/warehouses-app",
        ["withholding"] = "/erp/withholding-app",
        ["wms"] = "/erp/warehouse-wms-app",
        ["workflow"] = "/erp/workflow-app",
        ["workflow_automation"] = "/erp/workflows-app",
        ["workspace"] = "/erp/workspace-favorites-app",
        ["year_end"] = "/erp/period-close-app",
    };

    public static IReadOnlyDictionary<string, string> All => TabRoutes;

    /// <summary>
    /// ERP chrome clicks must stay on /erp. Dedicated twins keep the same slug
    /// under /erp/{app}; leftovers already use /erp/module-app.
    /// </summary>
    public static string PreferErpSurface(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return "/erp";
        var value = href.Trim();
        if (value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase))
        {
            return "/erp/" + value["/cp/".Length..];
        }

        return value;
    }

    public static bool TryMapTab(string? tab, out string href)
    {
        href = string.Empty;
        if (string.IsNullOrWhiteSpace(tab)) return false;
        var key = tab.Trim();
        if (!TabRoutes.TryGetValue(key, out var mapped) || string.IsNullOrWhiteSpace(mapped))
        {
            return false;
        }
        href = mapped;
        return true;
    }

    public static string MapTabOrModuleApp(string? tab)
    {
        if (TryMapTab(tab, out var href)) return href;
        var key = (tab ?? string.Empty).Trim();
        if (key.Length == 0) return "/erp";
        return ModuleAppPath + "?tab=" + Uri.EscapeDataString(key);
    }
}
