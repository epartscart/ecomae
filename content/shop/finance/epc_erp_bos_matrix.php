<?php
/**
 * BOS Enterprise Matrix — D365 F&O, Oracle EBS/Cloud, SAP S/4HANA coverage map.
 *
 * Returns a structured matrix of every standard module from D365, Oracle,
 * and SAP mapped to the ECOM AE ERP source file that implements it, plus
 * a coverage status (full / partial / planned / gap).
 *
 * Usage:
 *   $matrix = epc_bos_matrix();
 *   $d365   = $matrix['d365'];
 *   $oracle = $matrix['oracle'];
 *   $sap    = $matrix['sap'];
 *   $gaps   = epc_bos_matrix_gaps($matrix);
 */
defined('_ASTEXE_') or die('No access');

function epc_bos_matrix(): array
{
	$d365 = array(
		array('area' => 'Financial', 'module' => 'General Ledger', 'status' => 'full', 'file' => 'epc_erp_gl.php', 'notes' => 'COA, journals, trial balance, B/S, P&L'),
		array('area' => 'Financial', 'module' => 'Accounts Payable', 'status' => 'full', 'file' => 'epc_erp_procurement.php', 'notes' => 'Suppliers, purchase invoices, payments, 3-way match'),
		array('area' => 'Financial', 'module' => 'Accounts Receivable', 'status' => 'full', 'file' => 'epc_erp_aging.php + epc_erp_credit.php', 'notes' => 'Customer ledger, aging, statements, collections'),
		array('area' => 'Financial', 'module' => 'Cash & Bank Management', 'status' => 'full', 'file' => 'epc_erp_cash_treasury.php', 'notes' => 'Bank accounts, cash flow forecast, instruments'),
		array('area' => 'Financial', 'module' => 'Bank Reconciliation', 'status' => 'full', 'file' => 'epc_erp_bank_reconciliation.php', 'notes' => 'Statement import, auto-matching, reconciliation workflow'),
		array('area' => 'Financial', 'module' => 'Fixed Assets', 'status' => 'full', 'file' => 'epc_erp_fixed_assets.php', 'notes' => 'Register, depreciation (SLN + reducing), disposal, GL posting'),
		array('area' => 'Financial', 'module' => 'Budgeting', 'status' => 'full', 'file' => 'epc_erp_budget_planning.php', 'notes' => 'Budget models, lines, variance, transfers, forecast positions'),
		array('area' => 'Financial', 'module' => 'Cost Accounting', 'status' => 'full', 'file' => 'epc_erp_costing.php + epc_erp_cost_models.php', 'notes' => 'Cost centers, elements, allocation, overhead, EAM'),
		array('area' => 'Financial', 'module' => 'Currency Revaluation', 'status' => 'full', 'file' => 'epc_erp_ccy_revaluation.php', 'notes' => 'Period-end revaluation of open monetary balances'),
		array('area' => 'Financial', 'module' => 'Tax / VAT', 'status' => 'full', 'file' => 'epc_uae_vat.php + epc_erp_compliance.php', 'notes' => 'UAE VAT + worldwide tax compliance engine'),
		array('area' => 'Financial', 'module' => 'Withholding Tax', 'status' => 'full', 'file' => 'epc_erp_withholding.php', 'notes' => 'WHT rates, auto-calculation, reporting'),
		array('area' => 'Financial', 'module' => 'Electronic Reporting', 'status' => 'full', 'file' => 'epc_erp_elec_reporting.php', 'notes' => 'Configurable export formats'),
		array('area' => 'Financial', 'module' => 'E-Invoicing', 'status' => 'full', 'file' => 'epc_erp_einvoice.php', 'notes' => 'E-invoice generation and schema'),
		array('area' => 'Financial', 'module' => 'Consolidation', 'status' => 'full', 'file' => 'epc_erp_consolidation.php', 'notes' => 'Group consolidation, IC eliminations, minority interest'),
		array('area' => 'Financial', 'module' => 'Intercompany', 'status' => 'full', 'file' => 'epc_erp_intercompany.php', 'notes' => 'Legal entities, IC transactions, settlements'),
		array('area' => 'Financial', 'module' => 'Fiscal Periods & Closing', 'status' => 'full', 'file' => 'epc_erp_closing.php + epc_erp_fiscal_periods.php', 'notes' => 'Year-end close, period control'),
		array('area' => 'Financial', 'module' => 'Financial Dimensions', 'status' => 'full', 'file' => 'epc_erp_dimensions.php', 'notes' => 'Business unit + sub-modules'),
		array('area' => 'Financial', 'module' => 'Advance Payments', 'status' => 'full', 'file' => 'epc_erp_advances.php', 'notes' => 'Customer receipts & supplier payments'),
		array('area' => 'Financial', 'module' => 'Subscription Billing', 'status' => 'full', 'file' => 'epc_erp_subscriptions.php', 'notes' => 'IFRS 15 / ASC 606 revenue recognition'),
		array('area' => 'Financial', 'module' => 'External Financial Reports', 'status' => 'full', 'file' => 'epc_erp_external_reports.php', 'notes' => 'IFRS-compliant report builder'),

		array('area' => 'Supply Chain', 'module' => 'Inventory Management', 'status' => 'full', 'file' => 'epc_erp_inventory.php', 'notes' => 'Warehouses, items, weighted avg, perishables, period closing'),
		array('area' => 'Supply Chain', 'module' => 'Procurement & Sourcing', 'status' => 'full', 'file' => 'epc_erp_procurement.php + epc_erp_scm.php', 'notes' => 'PO, RFQ, 3-way match, landed cost'),
		array('area' => 'Supply Chain', 'module' => 'Sales Order & Fulfilment', 'status' => 'full', 'file' => 'epc_erp_fulfilment.php + epc_erp_order_fulfillment.php', 'notes' => 'SO, delivery notes, multi-supplier'),
		array('area' => 'Supply Chain', 'module' => 'Warehouse Management', 'status' => 'full', 'file' => 'epc_erp_wms.php', 'notes' => 'Locations, zones, picking waves, work orders, cycle counting'),
		array('area' => 'Supply Chain', 'module' => 'Master Planning / MRP', 'status' => 'full', 'file' => 'epc_erp_enterprise.php + epc_erp_mfg_routing.php', 'notes' => 'Net requirements, planned orders, BOM explosion'),
		array('area' => 'Supply Chain', 'module' => 'Production Control', 'status' => 'full', 'file' => 'epc_erp_manufacturing.php + epc_erp_mfg_routing.php', 'notes' => 'BOMs, production orders, routing, work centers'),
		array('area' => 'Supply Chain', 'module' => 'Quality Management', 'status' => 'full', 'file' => 'epc_erp_quality.php', 'notes' => 'Test plans, quality orders, NCR'),
		array('area' => 'Supply Chain', 'module' => 'Product Information (PIM)', 'status' => 'full', 'file' => 'epc_erp_pim_custom_fields.php', 'notes' => 'Custom attributes, multi-option fields, cross-module'),
		array('area' => 'Supply Chain', 'module' => 'Product Structure', 'status' => 'full', 'file' => 'epc_erp_product_structure.php', 'notes' => 'BOM hierarchy and released products'),
		array('area' => 'Supply Chain', 'module' => 'Pricing', 'status' => 'full', 'file' => 'epc_erp_pricing.php', 'notes' => 'Price lists, trade agreements'),
		array('area' => 'Supply Chain', 'module' => 'Demand Forecasting', 'status' => 'full', 'file' => 'epc_erp_scm.php + epc_erp_syncron_policy.php', 'notes' => 'Moving avg, exponential smoothing, safety stock'),
		array('area' => 'Supply Chain', 'module' => 'Transportation Management', 'status' => 'full', 'file' => 'epc_erp_transport.php', 'notes' => 'Carriers, rate cards, shipments, freight invoices, routing'),
		array('area' => 'Supply Chain', 'module' => 'After-Sales / Service', 'status' => 'full', 'file' => 'epc_erp_aftersales.php', 'notes' => 'RMA, warranty, service & repair jobs'),

		array('area' => 'HR', 'module' => 'Human Resources', 'status' => 'full', 'file' => 'epc_erp_hr.php + epc_erp_staff.php', 'notes' => 'Departments, employees, leave, documents'),
		array('area' => 'HR', 'module' => 'Payroll', 'status' => 'full', 'file' => 'epc_erp_payroll.php', 'notes' => 'Pay runs, deductions, WPS'),
		array('area' => 'HR', 'module' => 'Labour Law Compliance', 'status' => 'full', 'file' => 'epc_erp_hr_law.php', 'notes' => 'Worldwide 30+ countries, end-of-service, statutory cards'),
		array('area' => 'HR', 'module' => 'Talent Management', 'status' => 'full', 'file' => 'epc_erp_hr_talent.php', 'notes' => 'Recruitment + performance management'),

		array('area' => 'Commerce', 'module' => 'Retail & Commerce', 'status' => 'full', 'file' => 'epc_erp_retail.php', 'notes' => 'Channels, assortments, POS, periodic discounts'),
		array('area' => 'Commerce', 'module' => 'E-Commerce Bridge', 'status' => 'full', 'file' => 'epc_erp_ecommerce.php', 'notes' => 'Storefront ↔ ERP data sync'),

		array('area' => 'CRM', 'module' => 'CRM', 'status' => 'full', 'file' => 'epc_crm_modules.php + epc_erp_crm_advanced.php', 'notes' => 'Leads, opportunities, quotes, tickets, projects'),

		array('area' => 'Project', 'module' => 'Project Accounting', 'status' => 'full', 'file' => 'epc_erp_project_accounting.php', 'notes' => 'Budgets, actuals, WIP, revenue recognition'),
		array('area' => 'Project', 'module' => 'Project Management', 'status' => 'full', 'file' => 'epc_erp_projects.php', 'notes' => 'Tasks, milestones, resource allocation'),

		array('area' => 'Platform', 'module' => 'Audit Trail', 'status' => 'full', 'file' => 'epc_erp_audit.php', 'notes' => 'Key action logging'),
		array('area' => 'Platform', 'module' => 'Security / RBAC', 'status' => 'full', 'file' => 'epc_erp_rbac.php + epc_erp_security.php', 'notes' => 'Role-based access, duty segregation'),
		array('area' => 'Platform', 'module' => 'Data Import / Export', 'status' => 'full', 'file' => 'epc_erp_dataio.php', 'notes' => 'Bulk data import/export'),
		array('area' => 'Platform', 'module' => 'Integration / API', 'status' => 'full', 'file' => 'epc_erp_integration.php + epc_erp_datalink.php', 'notes' => 'REST API, commerce data-link'),
		array('area' => 'Platform', 'module' => 'Workflow / Collaboration', 'status' => 'full', 'file' => 'epc_erp_collab.php + epc_bos_workflow.php', 'notes' => 'Approval chains, task collaboration'),
		array('area' => 'Platform', 'module' => 'Organization Admin', 'status' => 'full', 'file' => 'epc_erp_orgadmin.php + epc_erp_org.php', 'notes' => 'Group, legal entity, BU tree'),
		array('area' => 'Platform', 'module' => 'Report Center', 'status' => 'full', 'file' => 'epc_erp_report_center.php', 'notes' => 'Per-module reports, CSV export, live filter'),
		array('area' => 'Platform', 'module' => 'Executive Dashboard', 'status' => 'full', 'file' => 'epc_erp_exec_dashboard.php', 'notes' => 'Cross-module KPI cockpit'),
		array('area' => 'Platform', 'module' => 'Process Flows', 'status' => 'full', 'file' => 'epc_erp_process_flows.php', 'notes' => 'Industry process flow templates'),

		array('area' => 'Compliance', 'module' => 'Customs / Trade', 'status' => 'full', 'file' => 'epc_erp_customs.php', 'notes' => 'Dubai Customs Mirsal-2 style'),
		array('area' => 'Compliance', 'module' => 'Document Expiry Tracker', 'status' => 'full', 'file' => 'epc_erp_doc_expiry.php', 'notes' => 'Multi-threshold auto-reminders'),
		array('area' => 'Compliance', 'module' => 'Insurance Management', 'status' => 'full', 'file' => 'epc_erp_insurance.php', 'notes' => '20+ policy classes, claims, renewals'),
		array('area' => 'Compliance', 'module' => 'Contract Management', 'status' => 'full', 'file' => 'epc_erp_contracts.php', 'notes' => 'Contracts, e-signature, OCR'),
		array('area' => 'Compliance', 'module' => 'GRC / Governance', 'status' => 'full', 'file' => 'epc_erp_governance.php + epc_bos_compliance.php', 'notes' => 'Roles, permissions, notifications'),
	);

	$oracle = array(
		array('oracle_module' => 'Oracle General Ledger', 'status' => 'full', 'epc_module' => 'General Ledger', 'file' => 'epc_erp_gl.php'),
		array('oracle_module' => 'Oracle Payables', 'status' => 'full', 'epc_module' => 'Accounts Payable', 'file' => 'epc_erp_procurement.php'),
		array('oracle_module' => 'Oracle Receivables', 'status' => 'full', 'epc_module' => 'Accounts Receivable', 'file' => 'epc_erp_aging.php + epc_erp_credit.php'),
		array('oracle_module' => 'Oracle Cash Management', 'status' => 'full', 'epc_module' => 'Cash & Bank', 'file' => 'epc_erp_cash_treasury.php'),
		array('oracle_module' => 'Oracle Fixed Assets', 'status' => 'full', 'epc_module' => 'Fixed Assets', 'file' => 'epc_erp_fixed_assets.php'),
		array('oracle_module' => 'Oracle Inventory', 'status' => 'full', 'epc_module' => 'Inventory Management', 'file' => 'epc_erp_inventory.php'),
		array('oracle_module' => 'Oracle Purchasing', 'status' => 'full', 'epc_module' => 'Procurement & Sourcing', 'file' => 'epc_erp_procurement.php + epc_erp_scm.php'),
		array('oracle_module' => 'Oracle Order Management', 'status' => 'full', 'epc_module' => 'Sales & Fulfilment', 'file' => 'epc_erp_fulfilment.php'),
		array('oracle_module' => 'Oracle Advanced Pricing', 'status' => 'full', 'epc_module' => 'Pricing', 'file' => 'epc_erp_pricing.php'),
		array('oracle_module' => 'Oracle Tax (E-Business Tax)', 'status' => 'full', 'epc_module' => 'Tax / VAT', 'file' => 'epc_uae_vat.php + epc_erp_compliance.php'),
		array('oracle_module' => 'Oracle Cost Management', 'status' => 'full', 'epc_module' => 'Cost Accounting', 'file' => 'epc_erp_costing.php + epc_erp_cost_models.php'),
		array('oracle_module' => 'Oracle Project Costing & Billing', 'status' => 'full', 'epc_module' => 'Project Accounting', 'file' => 'epc_erp_project_accounting.php'),
		array('oracle_module' => 'Oracle HRMS', 'status' => 'full', 'epc_module' => 'Human Resources', 'file' => 'epc_erp_hr.php'),
		array('oracle_module' => 'Oracle Payroll', 'status' => 'full', 'epc_module' => 'Payroll', 'file' => 'epc_erp_payroll.php'),
		array('oracle_module' => 'Oracle Talent Management', 'status' => 'full', 'epc_module' => 'Talent Management', 'file' => 'epc_erp_hr_talent.php'),
		array('oracle_module' => 'Oracle Manufacturing', 'status' => 'full', 'epc_module' => 'Production Control', 'file' => 'epc_erp_manufacturing.php'),
		array('oracle_module' => 'Oracle WMS', 'status' => 'full', 'epc_module' => 'Warehouse Management', 'file' => 'epc_erp_wms.php'),
		array('oracle_module' => 'Oracle SCM Cloud', 'status' => 'full', 'epc_module' => 'Supply Chain Management', 'file' => 'epc_erp_scm.php'),
		array('oracle_module' => 'Oracle Quality', 'status' => 'full', 'epc_module' => 'Quality Management', 'file' => 'epc_erp_quality.php'),
		array('oracle_module' => 'Oracle CRM / Sales Cloud', 'status' => 'full', 'epc_module' => 'CRM', 'file' => 'epc_crm_modules.php'),
		array('oracle_module' => 'Oracle Planning Central / ASCP', 'status' => 'full', 'epc_module' => 'Master Planning / MRP', 'file' => 'epc_erp_enterprise.php + epc_erp_mfg_routing.php'),
		array('oracle_module' => 'Oracle Budgetary Control', 'status' => 'full', 'epc_module' => 'Budgeting', 'file' => 'epc_erp_budget_planning.php'),
		array('oracle_module' => 'Oracle Advanced Collections', 'status' => 'full', 'epc_module' => 'Credit & Collections', 'file' => 'epc_erp_collections.php + epc_erp_credit.php'),
		array('oracle_module' => 'Oracle Treasury', 'status' => 'full', 'epc_module' => 'Treasury', 'file' => 'epc_erp_treasury.php + epc_erp_cash_treasury.php'),
		array('oracle_module' => 'Oracle iExpenses', 'status' => 'full', 'epc_module' => 'Expense Reports', 'file' => 'epc_erp_phase8.php'),
		array('oracle_module' => 'Oracle Hyperion / EPM Consolidation', 'status' => 'full', 'epc_module' => 'Consolidation', 'file' => 'epc_erp_consolidation.php'),
		array('oracle_module' => 'Oracle Intercompany', 'status' => 'full', 'epc_module' => 'Intercompany', 'file' => 'epc_erp_intercompany.php'),
		array('oracle_module' => 'Oracle Commerce', 'status' => 'full', 'epc_module' => 'Retail & E-Commerce', 'file' => 'epc_erp_retail.php + epc_erp_ecommerce.php'),
		array('oracle_module' => 'Oracle Global Trade Mgmt', 'status' => 'full', 'epc_module' => 'Customs / Trade', 'file' => 'epc_erp_customs.php'),
		array('oracle_module' => 'Oracle GRC', 'status' => 'full', 'epc_module' => 'GRC / Governance', 'file' => 'epc_erp_governance.php + epc_bos_compliance.php'),
		array('oracle_module' => 'Oracle Enterprise Asset Mgmt', 'status' => 'full', 'epc_module' => 'EAM', 'file' => 'epc_erp_costing.php (EAM section)'),
		array('oracle_module' => 'Oracle Subscription Mgmt', 'status' => 'full', 'epc_module' => 'Subscription Billing', 'file' => 'epc_erp_subscriptions.php'),
		array('oracle_module' => 'Oracle Supplier Portal', 'status' => 'full', 'epc_module' => 'Supplier Portal', 'file' => 'epc_erp_supplier_portal.php'),
		array('oracle_module' => 'Oracle Transportation Mgmt', 'status' => 'full', 'epc_module' => 'Transportation Mgmt', 'file' => 'epc_erp_transport.php'),
		array('oracle_module' => 'Oracle Property Manager', 'status' => 'full', 'epc_module' => 'Real Estate', 'file' => 'epc_erp_real_estate.php'),
		array('oracle_module' => 'Oracle Lease Accounting', 'status' => 'full', 'epc_module' => 'Lease Accounting (IFRS 16)', 'file' => 'epc_erp_real_estate.php'),
		array('oracle_module' => 'Oracle Field Service', 'status' => 'full', 'epc_module' => 'After-Sales + PM', 'file' => 'epc_erp_aftersales.php + epc_erp_plant_maintenance.php'),
		array('oracle_module' => 'Oracle EHS (Health & Safety)', 'status' => 'full', 'epc_module' => 'EHS', 'file' => 'epc_erp_ehs.php'),
	);

	$sap = array(
		array('sap_module' => 'FI — Financial Accounting', 'status' => 'full', 'epc_module' => 'GL + AP + AR', 'file' => 'epc_erp_gl.php + epc_erp_procurement.php + epc_erp_aging.php'),
		array('sap_module' => 'CO — Controlling', 'status' => 'full', 'epc_module' => 'Cost Accounting', 'file' => 'epc_erp_costing.php + epc_erp_cost_models.php'),
		array('sap_module' => 'AA — Asset Accounting', 'status' => 'full', 'epc_module' => 'Fixed Assets', 'file' => 'epc_erp_fixed_assets.php'),
		array('sap_module' => 'MM — Materials Management', 'status' => 'full', 'epc_module' => 'Inventory + Procurement', 'file' => 'epc_erp_inventory.php + epc_erp_procurement.php'),
		array('sap_module' => 'SD — Sales & Distribution', 'status' => 'full', 'epc_module' => 'Sales & Fulfilment', 'file' => 'epc_erp_fulfilment.php + epc_erp_order_fulfillment.php'),
		array('sap_module' => 'PP — Production Planning', 'status' => 'full', 'epc_module' => 'Manufacturing + MRP', 'file' => 'epc_erp_manufacturing.php + epc_erp_mfg_routing.php'),
		array('sap_module' => 'QM — Quality Management', 'status' => 'full', 'epc_module' => 'Quality Management', 'file' => 'epc_erp_quality.php'),
		array('sap_module' => 'PM — Plant Maintenance', 'status' => 'full', 'epc_module' => 'Plant Maintenance', 'file' => 'epc_erp_plant_maintenance.php'),
		array('sap_module' => 'PS — Project System', 'status' => 'full', 'epc_module' => 'Project Accounting', 'file' => 'epc_erp_project_accounting.php + epc_erp_projects.php'),
		array('sap_module' => 'HR / HCM', 'status' => 'full', 'epc_module' => 'HR + Payroll', 'file' => 'epc_erp_hr.php + epc_erp_payroll.php'),
		array('sap_module' => 'WM / EWM — Warehouse Mgmt', 'status' => 'full', 'epc_module' => 'Warehouse Management', 'file' => 'epc_erp_wms.php'),
		array('sap_module' => 'TR — Treasury', 'status' => 'full', 'epc_module' => 'Treasury', 'file' => 'epc_erp_treasury.php + epc_erp_cash_treasury.php'),
		array('sap_module' => 'BW / BPC — BI & Consolidation', 'status' => 'full', 'epc_module' => 'Reports + Consolidation', 'file' => 'epc_erp_report_center.php + epc_erp_consolidation.php'),
		array('sap_module' => 'CRM', 'status' => 'full', 'epc_module' => 'CRM', 'file' => 'epc_crm_modules.php + epc_erp_crm_advanced.php'),
		array('sap_module' => 'SRM — Supplier Relationship', 'status' => 'full', 'epc_module' => 'Supplier Portal', 'file' => 'epc_erp_supplier_portal.php'),
		array('sap_module' => 'GTS — Global Trade Services', 'status' => 'full', 'epc_module' => 'Customs / Trade', 'file' => 'epc_erp_customs.php'),
		array('sap_module' => 'GRC — Governance Risk Compliance', 'status' => 'full', 'epc_module' => 'GRC / Governance', 'file' => 'epc_erp_governance.php + epc_bos_compliance.php'),
		array('sap_module' => 'SuccessFactors — Talent', 'status' => 'full', 'epc_module' => 'Talent Management', 'file' => 'epc_erp_hr_talent.php'),
		array('sap_module' => 'Ariba — Procurement Cloud', 'status' => 'full', 'epc_module' => 'Procurement + SCM', 'file' => 'epc_erp_procurement.php + epc_erp_scm.php'),
		array('sap_module' => 'Concur — Expense Management', 'status' => 'full', 'epc_module' => 'Expense Reports', 'file' => 'epc_erp_phase8.php'),
		array('sap_module' => 'IBP — Integrated Business Planning', 'status' => 'full', 'epc_module' => 'SCM + MRP + Syncron', 'file' => 'epc_erp_scm.php + epc_erp_syncron_policy.php'),
		array('sap_module' => 'S/4 Asset Management', 'status' => 'full', 'epc_module' => 'Fixed Assets', 'file' => 'epc_erp_fixed_assets.php'),
		array('sap_module' => 'Subscription Billing', 'status' => 'full', 'epc_module' => 'Subscription Billing', 'file' => 'epc_erp_subscriptions.php'),
		array('sap_module' => 'Retail / Commerce', 'status' => 'full', 'epc_module' => 'Retail & E-Commerce', 'file' => 'epc_erp_retail.php + epc_erp_ecommerce.php'),
		array('sap_module' => 'TM — Transportation Mgmt', 'status' => 'full', 'epc_module' => 'Transportation Mgmt', 'file' => 'epc_erp_transport.php'),
		array('sap_module' => 'RE — Real Estate', 'status' => 'full', 'epc_module' => 'Real Estate & Lease', 'file' => 'epc_erp_real_estate.php'),
		array('sap_module' => 'PLM — Product Lifecycle', 'status' => 'full', 'epc_module' => 'PLM + PIM', 'file' => 'epc_erp_plm.php + epc_erp_pim_custom_fields.php'),
		array('sap_module' => 'EHS — Environment Health Safety', 'status' => 'full', 'epc_module' => 'EHS', 'file' => 'epc_erp_ehs.php'),
		array('sap_module' => 'Fieldglass — Contingent Workforce', 'status' => 'full', 'epc_module' => 'Contingent Workforce', 'file' => 'epc_erp_contingent_workforce.php'),
	);

	return array('d365' => $d365, 'oracle' => $oracle, 'sap' => $sap);
}

function epc_bos_matrix_gaps(array $matrix): array
{
	$gaps = array();
	foreach (array('d365', 'oracle', 'sap') as $platform) {
		foreach ($matrix[$platform] as $row) {
			$s = $row['status'];
			if ($s === 'gap' || $s === 'partial') {
				$mod = $row['module'] ?? $row['oracle_module'] ?? $row['sap_module'] ?? '';
				$gaps[$mod][] = array('platform' => $platform, 'status' => $s);
			}
		}
	}
	return $gaps;
}

function epc_bos_matrix_stats(array $matrix): array
{
	$stats = array();
	foreach (array('d365', 'oracle', 'sap') as $p) {
		$total = count($matrix[$p]);
		$full = 0;
		$partial = 0;
		$gap = 0;
		foreach ($matrix[$p] as $row) {
			if ($row['status'] === 'full') {
				$full++;
			} elseif ($row['status'] === 'partial') {
				$partial++;
			} else {
				$gap++;
			}
		}
		$stats[$p] = array(
			'total' => $total,
			'full' => $full,
			'partial' => $partial,
			'gap' => $gap,
			'coverage_pct' => $total > 0 ? round(($full + $partial * 0.5) / $total * 100, 1) : 0,
		);
	}
	return $stats;
}

function epc_bos_matrix_render_html(array $matrix): string
{
	$stats = epc_bos_matrix_stats($matrix);
	$html = '<style>
.bos-matrix{border-collapse:collapse;width:100%;margin:16px 0;font-size:13px}
.bos-matrix th,.bos-matrix td{border:1px solid #ddd;padding:6px 10px;text-align:left}
.bos-matrix th{background:#f0f4f8;font-weight:600}
.bos-st-full{color:#16a34a;font-weight:600}.bos-st-partial{color:#ca8a04;font-weight:600}.bos-st-gap{color:#dc2626;font-weight:600}
.bos-summary{display:flex;gap:24px;margin:12px 0}
.bos-card{border:1px solid #ddd;border-radius:8px;padding:16px;flex:1;text-align:center}
.bos-card h4{margin:0 0 8px;font-size:15px}.bos-card .pct{font-size:28px;font-weight:700}
</style>';

	$html .= '<div class="bos-summary">';
	foreach (array('d365' => 'D365 F&O', 'oracle' => 'Oracle EBS/Cloud', 'sap' => 'SAP S/4HANA') as $k => $label) {
		$s = $stats[$k];
		$html .= '<div class="bos-card"><h4>' . $label . '</h4><div class="pct">' . $s['coverage_pct'] . '%</div>';
		$html .= '<div>' . $s['full'] . ' full · ' . $s['partial'] . ' partial · ' . $s['gap'] . ' gap</div></div>';
	}
	$html .= '</div>';

	$html .= '<h3>D365 Finance & Operations</h3>';
	$html .= '<table class="bos-matrix"><thead><tr><th>Area</th><th>Module</th><th>Status</th><th>EPC File</th><th>Notes</th></tr></thead><tbody>';
	foreach ($matrix['d365'] as $r) {
		$cls = 'bos-st-' . $r['status'];
		$html .= '<tr><td>' . htmlspecialchars($r['area']) . '</td><td>' . htmlspecialchars($r['module']) . '</td>';
		$html .= '<td class="' . $cls . '">' . strtoupper($r['status']) . '</td>';
		$html .= '<td>' . htmlspecialchars($r['file']) . '</td><td>' . htmlspecialchars($r['notes']) . '</td></tr>';
	}
	$html .= '</tbody></table>';

	$html .= '<h3>Oracle EBS / Cloud</h3>';
	$html .= '<table class="bos-matrix"><thead><tr><th>Oracle Module</th><th>Status</th><th>EPC Module</th><th>EPC File</th></tr></thead><tbody>';
	foreach ($matrix['oracle'] as $r) {
		$cls = 'bos-st-' . $r['status'];
		$html .= '<tr><td>' . htmlspecialchars($r['oracle_module']) . '</td>';
		$html .= '<td class="' . $cls . '">' . strtoupper($r['status']) . '</td>';
		$html .= '<td>' . htmlspecialchars($r['epc_module']) . '</td>';
		$html .= '<td>' . htmlspecialchars($r['file']) . '</td></tr>';
	}
	$html .= '</tbody></table>';

	$html .= '<h3>SAP S/4HANA</h3>';
	$html .= '<table class="bos-matrix"><thead><tr><th>SAP Module</th><th>Status</th><th>EPC Module</th><th>EPC File</th></tr></thead><tbody>';
	foreach ($matrix['sap'] as $r) {
		$cls = 'bos-st-' . $r['status'];
		$html .= '<tr><td>' . htmlspecialchars($r['sap_module']) . '</td>';
		$html .= '<td class="' . $cls . '">' . strtoupper($r['status']) . '</td>';
		$html .= '<td>' . htmlspecialchars($r['epc_module']) . '</td>';
		$html .= '<td>' . htmlspecialchars($r['file']) . '</td></tr>';
	}
	$html .= '</tbody></table>';

	$gaps = epc_bos_matrix_gaps($matrix);
	if (!empty($gaps)) {
		$html .= '<h3>Gaps to Close</h3><table class="bos-matrix"><thead><tr><th>Module</th><th>Platforms</th><th>Status</th></tr></thead><tbody>';
		foreach ($gaps as $mod => $platforms) {
			$pNames = array_map(function ($p) { return strtoupper($p['platform']); }, $platforms);
			$html .= '<tr><td>' . htmlspecialchars($mod) . '</td><td>' . implode(', ', $pNames) . '</td>';
			$html .= '<td class="bos-st-gap">BUILD NEEDED</td></tr>';
		}
		$html .= '</tbody></table>';
	}

	return $html;
}
