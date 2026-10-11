# ERP finance PHP gap audit (2026-10-12)

Classification of the 166 `content/shop/finance` + `cp/content/shop/finance` files flagged 'gap' by `PHP_REFERENCE_GAP_INVENTORY.md`.

## Verdict

| Bucket | Files | Meaning |
|---|---:|---|
| Entry-reachable, covered | 46 | Named by `erp_nav_areas.php` / `ajax_erp.php` (entry points). All 321 ajax cases + nav areas already have ASP.NET twins. |
| Library behind covered entries | ~58 | Referenced only by live PHP libs/tests (e.g. `epc_erp_modules_ajax.php` 21 cases — every case has ASP.NET coverage: aml, barcode_purchase, card_reader, crm_integration, customer_groups, data_migration, doc_attachment, drilldown, ecommerce_integration, fix_unfix, gold_rate, gold_scheme, inventory_report, jewellery_tag, report_scheduler, rfid, shortcut_icons, sla, tickets, tourist_refund, virtual_warehouse). |
| Catalog-only dormant modules | ~14 | Referenced ONLY by `epc_erp_bos_matrix.php` (capability catalog) — no nav tab, no ajax case, no page route in PHP itself: `epc_erp_transport`, `epc_erp_plant_maintenance`, `epc_erp_real_estate`, `epc_erp_ehs`, `epc_erp_contingent_workforce`, `epc_erp_plm`, `epc_erp_product_structure`(also tests+setup tab), `epc_erp_hr_law`(bos+free-tools), `epc_tax_toolkit_world`, `epc_erp_compliance`, `epc_erp_governance`, `epc_erp_audit_tools`, `epc_erp_collab`, `epc_erp_datalink`. No user-reachable surface exists to parity — treated as deferred module work, not migration gap. |
| Storefront/payment/system | ~10 | `pay_for_order`, `payment_systems/*` (robokassa/cdekpay/alfabank/promsvbank/docpart_emulator), `epc_uae_customer_vat` — storefront checkout scope (Cursor), not ERP. |
| Portal shell/CSS/guide | ~12 | `erp_portal*`, `epc_erp_ui`, `epc_erp_topbar_brand`, `epc_erp_portal_inline_css/body/home/guide`, `erp_guide_portal`, `epc_pwa`, guide pages — PHP chrome superseded by Blazor `PhpErpDesktopChrome`; nothing to port. |
| CP ops pages (all now built) | ~6 | All twinned: `account_operations`/`operations_editor`/`operation_create_edit` → `/erp/account-operations-app` + `/erp/operations/create-entry` (#2138); `payment_systems_wholesaler`/`payment_systems_standart` → `/erp/payment-systems-app` two tabs + save routes (#2139); `custom_shipping_reports` → `/erp/custom-shipping-reports` + CSV export (#2140); `erp_invoice_attachments_panel` → invoice detail attachments + upload form (#2141); `erp_dashboard_netsuite` → already covered by `/erp/app` (built as the erp_main + netsuite variant: hero metrics, reminders, KPI meter, A/R aging chart, planning alerts, top suppliers, task analytics). |

## Reachable (ajax/nav-named) files — all covered via 321-case coverage

- `content/shop/finance/epc_uae_tax_compliance.php` (2943 lines)
- `content/shop/finance/epc_erp_jewellery.php` (1732 lines)
- `cp/content/shop/finance/erp/erp_nav_areas.php` (1418 lines)
- `content/shop/finance/epc_erp_inventory.php` (1351 lines)
- `content/shop/finance/epc_erp_jewellery_integration.php` (913 lines)
- `content/shop/finance/epc_erp_phase8.php` (910 lines)
- `content/shop/finance/epc_erp_order_planning.php` (896 lines)
- `content/shop/finance/epc_erp_aml_compliance.php` (895 lines)
- `content/shop/finance/epc_erp_staff.php` (851 lines)
- `content/shop/finance/epc_erp_concurrency.php` (774 lines)
- `content/shop/finance/epc_erp_integration.php` (700 lines)
- `content/shop/finance/epc_erp_cp_shell.php` (692 lines)
- `content/shop/finance/epc_crm_helpers.php` (655 lines)
- `content/shop/finance/epc_erp_period_close.php` (577 lines)
- `content/shop/finance/epc_erp_wms.php` (570 lines)
- `content/shop/finance/epc_uae_vat.php` (559 lines)
- `content/shop/finance/epc_erp_print_designer.php` (476 lines)
- `content/shop/finance/epc_erp_fin_advanced.php` (451 lines)
- `content/shop/finance/epc_erp_mfg_routing.php` (448 lines)
- `content/shop/finance/epc_erp_payroll.php` (448 lines)
- `content/shop/finance/epc_erp_procurement.php` (436 lines)
- `cp/content/shop/finance/erp/erp_dashboard.php` (421 lines)
- `content/shop/finance/epc_erp_quality.php` (383 lines)
- `content/shop/finance/epc_erp_credit.php` (375 lines)
- `content/shop/finance/epc_bos_workflow.php` (373 lines)
- `content/shop/finance/epc_erp_company.php` (360 lines)
- `content/shop/finance/epc_erp_dashboard.php` (353 lines)
- `content/shop/finance/epc_erp_closing.php` (348 lines)
- `content/shop/finance/epc_erp_dimensions.php` (333 lines)
- `content/shop/finance/epc_erp_currency.php` (329 lines)
- `content/shop/finance/epc_erp_collections.php` (326 lines)
- `content/shop/finance/epc_erp_projects.php` (320 lines)
- `content/shop/finance/epc_erp_budget_planning.php` (284 lines)
- `content/shop/finance/epc_erp_company_context.php` (262 lines)
- `content/shop/finance/epc_erp_org.php` (259 lines)
- `content/shop/finance/epc_erp_elec_reporting.php` (254 lines)
- `content/shop/finance/epc_erp_platform.php` (253 lines)
- `content/shop/finance/epc_erp_advanced.php` (185 lines)
- `content/shop/finance/epc_erp_contracts.php` (178 lines)
- `content/shop/finance/epc_erp_opening.php` (157 lines)
- `content/shop/finance/epc_erp_audit.php` (146 lines)
- `content/shop/finance/epc_erp_fiscal_periods.php` (69 lines)
- `content/shop/finance/epc_crm_access.php` (38 lines)
- `content/shop/finance/payment_systems/cdekpay/success.php` (31 lines)
- `content/shop/finance/payment_systems/robokassa/success.php` (29 lines)
- `content/shop/finance/payment_systems/robokassa/error.php` (28 lines)

## Deferred dormant-module files (no PHP surface)

- `content/shop/finance/epc_erp_transport.php`
- `content/shop/finance/epc_erp_plant_maintenance.php`
- `content/shop/finance/epc_erp_real_estate.php`
- `content/shop/finance/epc_erp_ehs.php`
- `content/shop/finance/epc_erp_contingent_workforce.php`
- `content/shop/finance/epc_erp_plm.php`
- `content/shop/finance/epc_erp_product_structure.php`
- `content/shop/finance/epc_erp_hr_law.php`
- `content/shop/finance/epc_tax_toolkit_world.php`
- `content/shop/finance/epc_erp_compliance.php`
- `content/shop/finance/epc_erp_governance.php`
- `content/shop/finance/epc_erp_audit_tools.php`
- `content/shop/finance/epc_erp_collab.php`
- `content/shop/finance/epc_erp_datalink.php`
