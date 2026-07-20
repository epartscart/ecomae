<?php
/**
 * ERP module — main CP page (dashboard + tabs).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
if (isset($db_link) && $db_link instanceof PDO && function_exists('epc_erp_assert_tenant_db_context')) {
	epc_erp_assert_tenant_db_context($db_link);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_fulfilment.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_staff.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_crm_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
require __DIR__ . '/erp_nav_areas.php';

// Public client demo (/erp-demo) renders the full Super ERP read-only with no
// login. Block every write so the shared demo workspace can never be mutated.
$epc_erp_demo_mirror = !empty($GLOBALS['epc_erp_demo_mirror']);
if ($epc_erp_demo_mirror && $_SERVER['REQUEST_METHOD'] === 'POST') {
	http_response_code(403);
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode(array('status' => false, 'message' => 'Demo is read-only. Sign in to make changes.'));
	exit;
}

if ($_SERVER['REQUEST_METHOD'] === 'GET' && isset($_GET['action'])) {
	$getAction = (string)$_GET['action'];
	if ($getAction === 'invoice_print' || $getAction === 'invoice_download_json') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_invoices.php';
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
		if (!epc_erp_user_can_access($db_link)) {
			http_response_code(403);
			exit('Access denied');
		}
		$invId = (int)($_GET['invoice_id'] ?? $_GET['document_id'] ?? 0);
		$doc = epc_einvoice_get_document($db_link, $invId);
		if (!$doc) {
			http_response_code(404);
			exit('Not found');
		}
		if ($getAction === 'invoice_print') {
			header('Content-Type: text/html; charset=utf-8');
			echo epc_erp_invoice_print_html($doc);
			exit;
		}
		header('Content-Type: application/json; charset=utf-8');
		header('Content-Disposition: attachment; filename="' . preg_replace('/[^a-zA-Z0-9._-]/', '_', $doc['invoice_number']) . '.json"');
		echo epc_erp_invoice_peppol_json($doc);
		exit;
	}
}
if ($_SERVER['REQUEST_METHOD'] === 'GET' && isset($_GET['action']) && $_GET['action'] === 'einvoice_download_xml') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_einvoice.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';
	if (!epc_erp_user_can_access($db_link)) {
		http_response_code(403);
		exit('Access denied');
	}
	$docId = (int)($_GET['document_id'] ?? 0);
	$doc = epc_einvoice_get_document($db_link, $docId);
	if (!$doc || empty($doc['xml_content'])) {
		http_response_code(404);
		exit('Not found');
	}
	header('Content-Type: application/xml; charset=utf-8');
	header('Content-Disposition: attachment; filename="' . preg_replace('/[^a-zA-Z0-9._-]/', '_', $doc['invoice_number']) . '.xml"');
	echo $doc['xml_content'];
	exit;
}

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	$crmPostActions = array(
		'save_lead', 'delete_lead', 'save_opportunity', 'update_stage', 'convert_lead',
		'won_hint', 'save_activity', 'toggle_activity', 'dashboard', 'pipeline',
	);
	$postAction = (string) $_POST['action'];
	if (in_array($postAction, $crmPostActions, true)) {
		require dirname(__DIR__, 2) . '/crm/ajax_crm.php';
		exit;
	}
	require __DIR__ . '/ajax_erp.php';
	exit;
}

epc_erp_full_ensure_schema($db_link);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
epc_erp_extended_ensure_schema($db_link);

if ($epc_erp_demo_mirror) {
	// Mirror the complete Super ERP so prospects can browse every module.
	$userAllowedTabs = epc_erp_staff_all_tabs();
} else {
	$userAllowedTabs = epc_erp_user_allowed_tabs($db_link);
	$modFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
	if (is_file($modFile)) {
		require_once $modFile;
		if (function_exists('epc_erp_filter_tabs_by_tenant_modules')) {
			$userAllowedTabs = epc_erp_filter_tabs_by_tenant_modules($userAllowedTabs);
		}
	}
}
if (function_exists('epc_crm_pack_enabled') && epc_crm_pack_enabled() && epc_crm_user_can_access($db_link) && !in_array('crm', $userAllowedTabs, true)) {
	$userAllowedTabs[] = 'crm';
}
if (!in_array('bank_recon', $userAllowedTabs, true) && in_array('cash_bank', $userAllowedTabs, true)) {
	$userAllowedTabs[] = 'bank_recon';
}

$tab = isset($_GET['tab']) ? (string) $_GET['tab'] : 'dashboard';
// Executive dashboard and Industry intelligence are now folded into the main
// dashboard; alias their old links so existing bookmarks keep working.
if ($tab === 'exec_dashboard' || $tab === 'industry_intel') {
	$tab = 'dashboard';
}
$erpArea = isset($_GET['area']) ? (string) $_GET['area'] : epc_erp_tab_to_area($tab);
if (!isset(epc_erp_nav_areas_config()[$erpArea])) {
	$erpArea = epc_erp_tab_to_area($tab);
}
if (!in_array($tab, $userAllowedTabs, true)) {
	$tab = in_array('dashboard', $userAllowedTabs, true) ? 'dashboard' : $userAllowedTabs[0];
	$erpArea = epc_erp_tab_to_area($tab);
}
if ($tab === 'procurement_link') {
	$tab = 'purchases';
}
if (epc_erp_is_shell_request() || !empty($GLOBALS['epc_erp_standalone'])) {
	$GLOBALS['epc_erp_shell_mode'] = true;
}
$epc_erp_shell_mode = epc_erp_is_shell_request() || !empty($GLOBALS['epc_erp_standalone']);

$date_from_str = isset($_GET['from']) ? (string)$_GET['from'] : date('Y-m-01');
$date_to_str = isset($_GET['to']) ? (string)$_GET['to'] : date('Y-m-d');
$date_from = strtotime($date_from_str . ' 00:00:00') ?: strtotime(date('Y-m-01'));
$date_to = strtotime($date_to_str . ' 23:59:59') ?: time();

if (!isset($epc_erp_portal)) {
	extract(epc_erp_configure_portal_urls('cp'));
} else {
	extract(epc_erp_configure_portal_urls($epc_erp_portal));
}
if ($epc_erp_demo_mirror) {
	// Keep all in-workspace navigation inside the public /erp-demo mirror so a
	// client browsing without login is never bounced to the /erp sign-in page.
	$erpUrl = '/erp-demo';
}
if (!isset($DP_Config) && isset($GLOBALS['DP_Config'])) {
	$DP_Config = $GLOBALS['DP_Config'];
}
if (!isset($erpAjaxEndpoint) || $erpAjaxEndpoint === '') {
	$backendPrefix = '/' . (isset($DP_Config->backend_dir) ? (string) $DP_Config->backend_dir : 'cp') . '/';
	$erpAjaxEndpoint = function_exists('epc_erp_resolve_ajax_endpoint')
		? epc_erp_resolve_ajax_endpoint($backendPrefix)
		: $backendPrefix . 'content/shop/finance/erp/ajax_erp_endpoint.php';
}

$dashboard = ($tab === 'dashboard')
	? epc_erp_dashboard($db_link, $date_from, $date_to)
	: array(
		'revenue_ex_vat' => 0,
		'purchase_ex_vat' => 0,
		'profit_ex_vat' => 0,
		'order_count' => 0,
		'receivable_due_orders' => 0,
		'customer_ledger_balance' => 0,
		'payable_balance' => 0,
		'cash_bank_total' => 0,
		'vat_5_on_revenue' => 0,
		'vat_output' => 0,
		'vat_input' => 0,
		'vat_net_payable' => 0,
	);
$fulfilment_summary = array('total_orders' => 0, 'pipeline' => array('delivery_done' => 0, 'returns_open' => 0));
$crmDash = array();
$crmPackOn = function_exists('epc_crm_pack_enabled') && epc_crm_pack_enabled();
if ($tab === 'dashboard') {
	$fulfilment_summary = epc_erp_fulfilment_summary_light($db_link, $date_from, $date_to);
	if ($crmPackOn && epc_crm_user_can_access($db_link)) {
		$crmDash = epc_crm_dashboard_extended($db_link);
	}
}
$crmErpUrl = epc_erp_tab_url($erpUrl, 'crm', $date_from_str, $date_to_str);
$dashboard_pl = ($tab === 'dashboard' || $tab === 'pl') ? epc_erp_gl_pl_report($db_link, $date_from, $date_to) : array('net_profit' => 0);
$suppliers = in_array($tab, array('payables', 'purchases', 'rfq', 'purchase_orders'), true) ? epc_erp_list_suppliers($db_link) : array();
$invWarehouses = array();
if ($tab === 'purchases') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_inventory.php';
	epc_erp_inventory_ensure_schema($db_link);
	$invWarehouses = epc_erp_inventory_list_warehouses($db_link);
}
$accounts = in_array($tab, array('payables', 'cash_bank', 'payment_batches', 'petty_cash'), true) ? epc_erp_list_cash_accounts($db_link) : array();
if (!isset($user_session) || !is_array($user_session)) {
	$user_session = epc_erp_resolve_user_session();
}
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
// On the standalone /erp portal the AJAX CSRF guard validates against the guest
// storefront session (stop_csrf uses the user session for /erp referer requests),
// so render that token even when an admin (CP) session cookie is also present —
// otherwise forms fail with "CSRF 4" once the operator is logged into CP.
if (!empty($epc_erp_shell_mode)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$erpPortalUserSess = DP_User::getUserSession();
	if (is_array($erpPortalUserSess) && !empty($erpPortalUserSess['csrf_guard_key'])) {
		$csrf = (string) $erpPortalUserSess['csrf_guard_key'];
	}
}
$userDeptCode = epc_erp_staff_primary_department($db_link);
$userDeptName = $userDeptCode !== '' ? epc_erp_staff_department_name($userDeptCode) : '';

$erpTabIncludes = array(
	'sales_orders' => 'erp_tabs_sales_orders.php',
	'leads' => 'erp_tabs_leads.php',
	'opportunities' => 'erp_tabs_opportunities.php',
	'proposals' => 'erp_tabs_proposals.php',
	'delivery_notes' => 'erp_tabs_delivery_notes.php',
	'rfq' => 'erp_tabs_rfq.php',
	'purchase_orders' => 'erp_tabs_purchase_orders.php',
	'three_way_match' => 'erp_tabs_three_way_match.php',
	'cash_bank' => 'erp_tabs_cash_bank.php',
	'payment_batches' => 'erp_tabs_payment_batches.php',
	'petty_cash' => 'erp_tabs_petty_cash.php',
	'fulfilment' => 'erp_tabs_fulfilment.php',
	'aftersales' => 'erp_tabs_aftersales.php',
	'staff' => 'erp_tabs_staff.php',
	'workflow' => 'erp_tabs_workflow.php',
	'processflow' => 'erp_tabs_processflow.php',
	'approvals' => 'erp_tabs_approvals.php',
	'compliance' => 'erp_tabs_compliance.php',
	'industry_intel' => 'erp_tabs_industry_intel.php',
	'ai_advisor' => 'erp_tabs_ai_advisor.php',
	'marketing' => 'erp_tabs_marketing.php',
	'crm' => 'erp_tabs_crm.php',
	'hr' => 'erp_tabs_hr.php',
	'payroll' => 'erp_tabs_payroll.php',
	'vat_return' => 'erp_tabs_vat.php',
	'tax_compliance' => 'erp_tabs_tax_compliance.php',
	'vat_refund' => 'erp_tabs_vat_refund.php',
	'einvoice' => 'erp_tabs_einvoice.php',
	'blockchain_proofs' => 'erp_tabs_blockchain_proofs.php',
	'invoices' => 'erp_tabs_invoices.php',
	'inventory' => 'erp_tabs_inventory.php',
	'fixed_assets' => 'erp_tabs_fixed_assets.php',
	'opening_balances' => 'erp_tabs_opening.php',
	'contacts' => 'erp_tabs_contacts.php',
	'documents' => 'erp_tabs_documents.php',
	'contracts' => 'erp_tabs_contracts.php',
	'document_control' => 'erp_tabs_document_control.php',
	'audit' => 'erp_tabs_audit.php',
	'reports' => 'erp_tabs_reports.php',
	'expense_reports' => 'erp_tabs_expense_reports.php',
	'hr_ops' => 'erp_tabs_hr_ops.php',
	'hr_law' => 'erp_tabs_hr_law.php',
	'subscriptions' => 'erp_tabs_subscriptions.php',
	'manufacturing' => 'erp_tabs_manufacturing.php',
	'order_planning' => 'erp_tabs_order_planning.php',
	'supplier_portal' => 'erp_tabs_supplier_portal.php',
	'exec_dashboard' => 'erp_tabs_exec_dashboard.php',
	'agenda' => 'erp_tabs_agenda.php',
	'projects' => 'erp_tabs_projects.php',
	'knowledge_base' => 'erp_tabs_knowledge_base.php',
	'multi_entity' => 'erp_tabs_multi_entity.php',
	'custom_shipping' => 'erp_tabs_custom_shipping.php',
	'erp_setup' => 'erp_tabs_setup.php',
	'data_import' => 'erp_tabs_data_import.php',
	'aging' => 'erp_tabs_aging.php',
	// Enterprise structural / master-data modules
	'business_units' => 'erp_tabs_business_units.php',
	'listing' => 'erp_tabs_listing.php',
	'product_info' => 'erp_tabs_product_info.php',
	'inv_groups' => 'erp_tabs_inv_groups.php',
	'ap_setup' => 'erp_tabs_ap_setup.php',
	'ar_setup' => 'erp_tabs_ar_setup.php',
	'budgeting' => 'erp_tabs_budgeting.php',
	'bank_setup' => 'erp_tabs_bank_setup.php',
	'consolidation_bu' => 'erp_tabs_consolidation_bu.php',
	'enterprise_reports' => 'erp_tabs_enterprise_reports.php',
	'landed_cost' => 'erp_tabs_landed_cost.php',
	'master_planning' => 'erp_tabs_master_planning.php',
	'retail_barcode' => 'erp_tabs_retail_barcode.php',
	'doc_formats' => 'erp_tabs_doc_formats.php',
	'ext_reports' => 'erp_tabs_external_reports.php',
	// Risk & Insurance
	'insurance' => 'erp_tabs_insurance.php',
	'doc_expiry' => 'erp_tabs_doc_expiry.php',
	// Advanced WMS
	'wms' => 'erp_tabs_wms.php',
	// Manufacturing depth
	'mfg_planning' => 'erp_tabs_mfg_planning.php',
	// Financial depth
	'fin_advanced' => 'erp_tabs_fin_advanced.php',
	// Collections & credit management
	'collections' => 'erp_tabs_collections.php',
	// Project accounting depth
	'project_accounting' => 'erp_tabs_project_accounting.php',
	// Costing value-models
	'cost_models' => 'erp_tabs_cost_models.php',
	// Data & integration framework
	'integration' => 'erp_tabs_integration.php',
	// Quality management
	'quality' => 'erp_tabs_quality.php',
	// Retail & Commerce
	'retail_commerce' => 'erp_tabs_retail_commerce.php',
	// Platform — security roles
	'security_roles' => 'erp_tabs_security_roles.php',
	// Organization administration / Enterprise
	'org_admin' => 'erp_tabs_org_admin.php',
	// Platform / cross-cutting services
	'platform' => 'erp_tabs_platform.php',
	// Year-end closing
	'year_end' => 'erp_tabs_year_end.php',
	// Procurement & sourcing depth
	'purchase_requisitions' => 'erp_tabs_purchase_requisitions.php',
	'procurement_categories' => 'erp_tabs_procurement_categories.php',
	// Budgeting depth
	'budget_planning' => 'erp_tabs_budget_planning.php',
	// HR depth — talent
	'recruitment' => 'erp_tabs_recruitment.php',
	'performance' => 'erp_tabs_performance.php',
	// Cash & treasury depth
	'cash_forecast' => 'erp_tabs_cash_forecast.php',
	'bank_instruments' => 'erp_tabs_bank_instruments.php',
	// Tax depth
	'withholding' => 'erp_tabs_withholding.php',
	'elec_reporting' => 'erp_tabs_elec_reporting.php',
	// Jewellery ERP — master data
	'jewellery' => 'erp_tabs_jewellery.php',
	'jw_karat' => 'erp_tabs_jw_karat.php',
	'jw_rate_type' => 'erp_tabs_jw_rate_type.php',
	'jw_currency' => 'erp_tabs_jw_currency.php',
	'jw_metal_stock' => 'erp_tabs_jw_metal_stock.php',
	'jw_design' => 'erp_tabs_jw_design.php',
	'jw_diamond' => 'erp_tabs_jw_diamond.php',
	'jw_pearl' => 'erp_tabs_jw_pearl.php',
	'jw_color_stone' => 'erp_tabs_jw_color_stone.php',
	// Jewellery ERP — purchase
	'jw_metal_purchase' => 'erp_tabs_jw_metal_purchase.php',
	'jw_diamond_purchase' => 'erp_tabs_jw_diamond_purchase.php',
	'jw_purchase_fixing' => 'erp_tabs_jw_purchase_fixing.php',
	'jw_purchase_window' => 'erp_tabs_jw_purchase_window.php',
	// Jewellery ERP — sales
	'jw_retail_sales' => 'erp_tabs_jw_retail_sales.php',
	'jw_metal_sales' => 'erp_tabs_jw_metal_sales.php',
	'jw_sales_fixing' => 'erp_tabs_jw_sales_fixing.php',
	'jw_sales_return' => 'erp_tabs_jw_sales_return.php',
	'jw_pos_advance' => 'erp_tabs_jw_pos_advance.php',
	// Jewellery ERP — repair & workshop
	'jw_repair_receipt' => 'erp_tabs_jw_repair_receipt.php',
	'jw_repair_transfer' => 'erp_tabs_jw_repair_transfer.php',
	'jw_workshop_receive' => 'erp_tabs_jw_workshop_receive.php',
	'jw_repair_delivery' => 'erp_tabs_jw_repair_delivery.php',
	'jw_repair_sale' => 'erp_tabs_jw_repair_sale.php',
	'jw_repair_register' => 'erp_tabs_jw_repair_register.php',
	'jw_repair_search' => 'erp_tabs_jw_repair_search.php',
	// Jewellery ERP — stock & analysis
	'jw_stock_verification' => 'erp_tabs_jw_stock_verification.php',
	'jw_stock_balance' => 'erp_tabs_jw_stock_balance.php',
	'jw_sales_analysis' => 'erp_tabs_jw_sales_analysis.php',
	'jw_barcode' => 'erp_tabs_jw_barcode.php',
	// Jewellery ERP — finance
	'jw_petty_cash' => 'erp_tabs_jw_petty_cash.php',
	'jw_journal_voucher' => 'erp_tabs_jw_journal_voucher.php',
	'jw_tourist_vat' => 'erp_tabs_jw_tourist_vat.php',
	// Jewellery integrated tabs (cross-module)
	'jw_trial_balance' => 'erp_tabs_jw_trial_balance.php',
	'jw_repairs' => 'erp_tabs_jw_repairs.php',
	'jw_seed_data' => 'erp_tabs_jw_seed_data.php',
	'ai_assistant' => 'erp_tabs_ai_assistant.php',
	'tenant_config' => 'erp_tabs_tenant_config.php',
	'print_designer' => 'erp_tabs_print_designer.php',
	'workflow_automation' => 'erp_tabs_workflow_automation.php',
	// ── New ERP modules (22-feature batch 2) ──
	'sla' => 'erp_tabs_sla.php',
	'tickets' => 'erp_tabs_tickets.php',
	'doc_attachment' => 'erp_tabs_doc_attachment.php',
	'customer_groups' => 'erp_tabs_customer_groups.php',
	'drilldown' => 'erp_tabs_drilldown.php',
	'shortcut_icons' => 'erp_tabs_shortcut_icons.php',
	'gold_scheme' => 'erp_tabs_gold_scheme.php',
	'gold_rate' => 'erp_tabs_gold_rate.php',
	'jewellery_tag' => 'erp_tabs_jewellery_tag.php',
	'barcode_purchase' => 'erp_tabs_barcode_purchase.php',
	'fix_unfix' => 'erp_tabs_fix_unfix.php',
	'tourist_refund' => 'erp_tabs_tourist_refund.php',
	'card_reader' => 'erp_tabs_card_reader.php',
	'aml_compliance' => 'erp_tabs_aml_compliance.php',
	'report_scheduler' => 'erp_tabs_report_scheduler.php',
	'inventory_report' => 'erp_tabs_inventory_report.php',
	'ecommerce_integration' => 'erp_tabs_ecommerce_integration.php',
	'crm_integration' => 'erp_tabs_crm_integration.php',
	'data_migration' => 'erp_tabs_data_migration.php',
	'virtual_warehouse' => 'erp_tabs_virtual_warehouse.php',
	'rfid' => 'erp_tabs_rfid.php',
	'landed_cost_v2' => 'erp_tabs_landed_cost_v2.php',
	'on_premises' => 'erp_tabs_on_premises.php',
);

?>

<?php if (!$epc_erp_shell_mode): ?>
<?php $epcErpCssVer = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260720erp-topnav'; ?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=<?php echo htmlspecialchars($epcErpCssVer, ENT_QUOTES, 'UTF-8'); ?>">
<link rel="stylesheet" href="/content/shop/finance/epc_erp_professional.css?v=<?php echo htmlspecialchars($epcErpCssVer, ENT_QUOTES, 'UTF-8'); ?>">
<?php endif; ?>
<style>
.epc-erp-msg { display:none; margin:10px 0; }
/* Favourites section */
.epc-erp-sidebar-favourites { border-bottom:1px solid rgba(120,160,220,0.15); margin-bottom:4px; padding-bottom:4px; }
.epc-erp-sidebar-fav-head { padding:8px 14px 4px; font-size:11px; font-weight:700; text-transform:uppercase; letter-spacing:.06em; color:#f59e0b; display:flex; align-items:center; gap:6px; }
.epc-erp-sidebar-fav-list { list-style:none; margin:0; padding:0; }
.epc-erp-sidebar-fav-list .epc-erp-sidebar-item { position:relative; }
.epc-erp-sidebar-fav-list .epc-erp-sidebar-item a { padding:5px 14px 5px 30px; }
/* Star buttons */
.epc-erp-fav-star, .epc-erp-fav-unstar { background:none; border:none; cursor:pointer; position:absolute; right:6px; top:50%; transform:translateY(-50%); opacity:0; transition:opacity .2s; font-size:12px; padding:2px 4px; color:#8aa0c4; }
.epc-erp-sidebar-item:hover .epc-erp-fav-star { opacity:.6; }
.epc-erp-fav-star:hover { opacity:1!important; color:#f59e0b; }
.epc-erp-fav-unstar { opacity:.7; }
.epc-erp-fav-unstar:hover { opacity:1; }
.epc-erp-sidebar-item { position:relative; }

/* ══════════════════════════════════════════════════════════════════════
   Suntech ef-window global override — applies the professional blue
   gradient box/grid/toolbar style to ALL ERP tabs (not just jewellery).
   Matches the Suntech jewellery screenshot aesthetic system-wide.
   ══════════════════════════════════════════════════════════════════════ */

/* ef-window core (loaded globally so all tabs can use it) */
.ef-window{border:1px solid #8faabc;border-radius:3px;background:#f0f4f7;margin-bottom:16px;box-shadow:0 1px 4px rgba(0,0,0,.08)}
.ef-title{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);color:#fff;font-size:13px;font-weight:600;padding:5px 12px;border-bottom:1px solid #3d6a7d;letter-spacing:.3px}
.ef-toolbar{background:#d8e4ec;padding:4px 8px;border-bottom:1px solid #b8c8d4;display:flex;gap:4px;flex-wrap:wrap;align-items:center}
.ef-toolbar .btn{padding:2px 8px;font-size:11px}
.ef-body{padding:10px 12px}
.ef-section{border:1px solid #a8bcc8;border-radius:3px;padding:10px 12px;margin-bottom:10px;background:#fff;position:relative}
.ef-section-title{position:absolute;top:-9px;left:10px;background:#f0f4f7;padding:0 6px;font-size:11px;font-weight:600;color:#4a6a7a}
.ef-row{display:flex;flex-wrap:wrap;gap:6px 14px;margin-bottom:6px;align-items:center}
.ef-field{display:flex;align-items:center;gap:4px;font-size:12px}
.ef-field label{font-weight:600;color:#2c4a5a;white-space:nowrap;margin:0;min-width:auto;font-size:11px}
.ef-field input,.ef-field select,.ef-field textarea{border:1px solid #8fb8cc;background:#eaf6fb;padding:2px 6px;font-size:12px;border-radius:2px;min-width:80px}
.ef-field input:focus,.ef-field select:focus{background:#fff;border-color:#3498db;outline:none}
.ef-field input[type="checkbox"]{min-width:auto;width:14px;height:14px}
.ef-field-wide input,.ef-field-wide select{min-width:180px}
.ef-grid{width:100%;border-collapse:collapse;font-size:11px;margin:6px 0}
.ef-grid th{background:#c8dce6;color:#2c4a5a;font-weight:600;padding:4px 6px;border:1px solid #a8c0cc;text-align:left;font-size:11px;white-space:nowrap}
.ef-grid td{padding:3px 6px;border:1px solid #c8d8e0;background:#fff}
.ef-grid td input,.ef-grid td select{border:1px solid #b8d0dc;background:#eaf6fb;padding:1px 4px;font-size:11px;width:100%;box-sizing:border-box}
.ef-grid tbody tr:hover td{background:#f0f8ff}
.ef-grid tfoot td{background:#e0ecf2;font-weight:600}
.ef-totals{display:flex;flex-wrap:wrap;gap:4px 0;justify-content:flex-end;margin-top:8px}
.ef-actions{display:flex;gap:6px;margin-top:10px;justify-content:flex-end}
.ef-actions .btn{font-size:12px;padding:4px 14px}
.ef-status{background:#e0ecf2;border-top:1px solid #b8c8d4;padding:3px 12px;font-size:11px;color:#4a6a7a;display:flex;justify-content:space-between}
.ef-tabs{margin-top:8px}
.ef-tab{background:#d8e4ec;border:1px solid #b8c8d4;padding:3px 12px;font-size:11px;font-weight:600;color:#4a6a7a;cursor:pointer;border-bottom:none}
.ef-tab.active{background:#fff;color:#2c4a5a;border-bottom:1px solid #fff}
.ef-tab-pane{border:1px solid #b8c8d4;padding:10px 12px;background:#fff;margin-top:-1px}
.ef-price-matrix{border-collapse:collapse;font-size:11px}
.ef-price-matrix th,.ef-price-matrix td{border:1px solid #a8c0cc;padding:2px 6px}
.ef-price-matrix th{background:#c8dce6;font-weight:600}
.ef-narration{width:100%;min-height:50px;border:1px solid #a8bcc8;background:#fff;padding:4px 6px;font-size:12px;resize:vertical}
.ef-image-box{border:1px solid #a8bcc8;background:#f8f8f8;width:100px;height:80px;display:flex;align-items:center;justify-content:center;font-size:10px;color:#999}
@media(max-width:768px){.ef-row{flex-direction:column;gap:4px}.ef-field{flex-direction:column;align-items:flex-start}}

/* ── Restyle D365 elements to Suntech look ── */

/* Hero → Suntech title bar style */
.epc-erp-hero{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);color:#fff;padding:8px 14px;border:1px solid #3d6a7d;border-radius:3px 3px 0 0;margin-bottom:0}
.epc-erp-hero h3{color:#fff;font-size:14px;font-weight:600;margin:0 0 2px;letter-spacing:.3px}
.epc-erp-hero p{color:rgba(255,255,255,.85);font-size:11px;margin:0}
.epc-erp-hero p strong{color:#fff}

/* KPI cards → Suntech bordered stat boxes */
.epc-erp-kpi{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:12px;padding:8px 0}
.epc-erp-kpi .kpi{flex:1;min-width:120px;padding:8px 12px;background:#f0f4f7;border:1px solid #8faabc;border-radius:3px;text-align:center}
.epc-erp-kpi .kpi .lbl{font-size:10px;font-weight:600;color:#4a6a7a;text-transform:uppercase;letter-spacing:.04em}
.epc-erp-kpi .kpi .val{font-size:18px;font-weight:700;color:#2c4a5a;margin:2px 0}
.epc-erp-kpi .kpi .val.green{color:#2e7d32}
.epc-erp-kpi .kpi .hint{font-size:10px;color:#6a8a9a}

/* Section cards → Suntech bordered sections */
.epc-erp-section-card{border:1px solid #8faabc;border-radius:3px;margin-bottom:14px;background:#f0f4f7;box-shadow:0 1px 4px rgba(0,0,0,.08)}
.epc-erp-section-card-hd{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);padding:5px 12px;border-bottom:1px solid #3d6a7d}
.epc-erp-section-card-hd h4{color:#fff;font-size:13px;font-weight:600;margin:0;letter-spacing:.3px}
.epc-erp-section-card-bd{padding:10px 12px;background:#fff}

/* Tables → Suntech grid style */
.epc-erp-shell .table,.epc-erp-table,.table-epc{border-collapse:collapse;font-size:11px}
.epc-erp-shell .table>thead>tr>th,.epc-erp-table>thead>tr>th,.table-epc>thead>tr>th{background:#c8dce6;color:#2c4a5a;font-weight:600;padding:4px 6px;border:1px solid #a8c0cc;font-size:11px;white-space:nowrap}
.epc-erp-shell .table>tbody>tr>td,.epc-erp-table>tbody>tr>td,.table-epc>tbody>tr>td{padding:3px 6px;border:1px solid #c8d8e0;background:#fff;color:#1e293b}
.epc-erp-content,.epc-erp-content-body{color:#1e293b}
.epc-erp-content-body .text-muted,.epc-erp-content-body .help-block{color:#475569!important}
.epc-erp-content-body .form-control{color:#1e293b}
.epc-erp-shell .table>tbody>tr:hover>td,.epc-erp-table>tbody>tr:hover>td,.table-epc>tbody>tr:hover>td{background:#f0f8ff}
.epc-erp-shell .table>tfoot>tr>td,.epc-erp-table>tfoot>tr>td,.table-epc>tfoot>tr>td{background:#e0ecf2;font-weight:600;border:1px solid #a8c0cc}
.epc-d365-sumrow td{background:#e0ecf2!important;font-weight:600}

/* Action Pane ribbon → Suntech toolbar style */
.epc-d365-ribbon{background:#d8e4ec;border:1px solid #b8c8d4;border-radius:3px;margin-bottom:12px;overflow:hidden}
.epc-d365-aptabs{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);display:flex;flex-wrap:wrap;padding:0;border-bottom:1px solid #3d6a7d}
.epc-d365-aptab{color:rgba(255,255,255,.8);font-size:12px;font-weight:600;padding:5px 14px;border:none;background:transparent;cursor:pointer}
.epc-d365-aptab.is-active{color:#fff;background:rgba(255,255,255,.15);border-bottom:2px solid #fff}
.epc-d365-aptab:hover{color:#fff;background:rgba(255,255,255,.1)}
.epc-d365-ap-row{display:none;padding:4px 8px}
.epc-d365-ap-row.is-active{display:block}
.epc-d365-actionpane{display:flex;flex-wrap:wrap;gap:6px;align-items:flex-start}
.epc-d365-ap-group{display:flex;flex-direction:column;align-items:center;gap:2px;padding:4px 8px;border-right:1px solid #b8c8d4}
.epc-d365-ap-group:last-child{border-right:none}
.epc-d365-ap-buttons{display:flex;gap:3px;flex-wrap:wrap}
.epc-d365-ap-btn{display:flex;flex-direction:column;align-items:center;gap:1px;padding:4px 8px;border:1px solid transparent;border-radius:2px;font-size:10px;color:#2c4a5a;cursor:pointer;text-decoration:none;background:transparent;min-width:44px;text-align:center}
.epc-d365-ap-btn .fa{font-size:16px;color:#4a7a8f}
.epc-d365-ap-btn:hover{background:#eaf6fb;border-color:#8fb8cc}
.epc-d365-ap-btn.is-primary{background:#eaf6fb;border-color:#8fb8cc}
.epc-d365-ap-btn.is-primary .fa{color:#2c4a5a}
.epc-d365-ap-btn.is-disabled{opacity:.45;cursor:default}
.epc-d365-ap-label{font-size:9px;font-weight:600;color:#6a8a9a;text-transform:uppercase;letter-spacing:.04em;margin-top:2px}

/* FastTabs → Suntech bordered sections */
.epc-d365-fasttab{border:1px solid #a8bcc8;border-radius:3px;margin-bottom:10px;background:#fff}
.epc-d365-ft-hd{display:flex;align-items:center;gap:8px;padding:6px 12px;cursor:pointer;background:#d8e4ec;border-bottom:1px solid #b8c8d4}
.epc-d365-ft-hd:hover{background:#c8dce6}
.epc-d365-fasttab.is-open>.epc-d365-ft-hd{background:linear-gradient(180deg,#6b8fa3 0%,#4a7a8f 100%);color:#fff;border-bottom:1px solid #3d6a7d}
.epc-d365-fasttab.is-open>.epc-d365-ft-hd .epc-d365-ft-title,.epc-d365-fasttab.is-open>.epc-d365-ft-hd .fa{color:#fff}
.epc-d365-fasttab.is-open>.epc-d365-ft-hd .epc-d365-ft-summary{color:rgba(255,255,255,.7)}
.epc-d365-fasttab.is-open>.epc-d365-ft-hd .epc-d365-ft-caret{color:#fff}
.epc-d365-ft-title{font-size:12px;font-weight:600;color:#2c4a5a}
.epc-d365-ft-title .fa{color:#4a7a8f;margin-right:4px}
.epc-d365-ft-caret{font-size:10px;color:#6a8a9a}
.epc-d365-ft-summary{font-size:10px;color:#6a8a9a;margin-left:auto}
.epc-d365-ft-bd{display:none;padding:10px 12px;border-top:none}
.epc-d365-fasttab.is-open>.epc-d365-ft-bd{display:block}

/* Filter bar → Suntech toolbar style */
.epc-erp-filter-bar{background:#d8e4ec;border:1px solid #b8c8d4;border-radius:3px;padding:6px 10px;margin-bottom:12px}
.epc-erp-filter-bar label{font-size:11px;font-weight:600;color:#4a6a7a;margin:0 4px}
.epc-erp-filter-bar .form-control{border:1px solid #8fb8cc;background:#eaf6fb;font-size:11px;padding:2px 6px;border-radius:2px}
.epc-erp-filter-bar .btn{font-size:11px;padding:2px 8px}

/* List toolbar → Suntech style */
.epc-d365-listtoolbar{background:#d8e4ec;border:1px solid #b8c8d4;border-radius:3px;padding:4px 8px;margin-bottom:8px}

/* Tab strips → Suntech tab style */
.epc-d365-tabstrip{display:flex;flex-wrap:wrap;gap:0;border-bottom:1px solid #b8c8d4;margin-bottom:0}
.epc-d365-tab{padding:4px 14px;font-size:12px;font-weight:600;color:#4a6a7a;background:#d8e4ec;border:1px solid #b8c8d4;border-bottom:none;cursor:pointer;text-decoration:none;border-radius:3px 3px 0 0;margin-right:2px}
.epc-d365-tab.is-active{background:#fff;color:#2c4a5a;border-bottom:1px solid #fff;margin-bottom:-1px}
.epc-d365-tab:hover{background:#c8dce6;color:#2c4a5a}
.epc-d365-tabpanel{display:none;border:1px solid #b8c8d4;border-top:none;padding:10px 12px;background:#fff}
.epc-d365-tabpanel.is-active{display:block}

/* Form inputs within ERP → Suntech light blue inputs */
.epc-erp-shell .form-control{border:1px solid #8fb8cc;background:#eaf6fb;font-size:12px;border-radius:2px}
.epc-erp-shell .form-control:focus{background:#fff;border-color:#3498db;box-shadow:none}

/* Status dots / pills → cleaner with Suntech palette */
.epc-d365-statcol{width:6px;padding:0 4px!important}

/* Empty state → Suntech bordered */
.epc-erp-empty{background:#f0f4f7;border:1px solid #a8bcc8;border-radius:3px;color:#6a8a9a}

/* Page header → tighter Suntech spacing */
.epc-erp-page-hd{margin-bottom:10px}
.epc-erp-page-title{font-size:16px;color:#2c4a5a}
.epc-erp-page-sub{font-size:11px;color:#6a8a9a}
</style>
<?php
if (!$epc_erp_shell_mode) {
	echo epc_cp_sidebar_early_init_script();
	echo epc_cp_menu_sections_script();
}
// Enterprise-styled entry modules: Sales order, Purchase order, Inventory,
// Receivables, Payables and General journal adopt the enterprise look (action
// pane, FastTabs, dense grids). The theme is scoped under `.epc-erp-d365`.
$epcErpD365Tabs = array('sales_orders', 'purchase_orders', 'inventory', 'receivables', 'payables', 'gl');
$epcErpD365Tab = in_array($tab, $epcErpD365Tabs, true);
?>

<div class="col-lg-12 epc-erp-shell epc-erp-shell--layout epc-erp-shell--topnav epc-erp-shell--topnav-only<?php echo $epc_erp_shell_mode ? ' epc-erp-shell--pro' : ''; ?><?php echo $epcErpD365Tab ? ' epc-erp-d365' : ''; ?>">
	<?php epc_erp_render_top_nav($erpUrl, $erpArea, $tab, $date_from_str, $date_to_str, $userAllowedTabs); ?>
	<div class="epc-erp-layout epc-erp-layout--topnav epc-erp-layout--full">
		<?php /* Left rail removed: top process mega-menu is the sole primary nav. */ ?>

		<div class="epc-erp-content">
			<div class="epc-erp-content-toolbar">
				<div class="epc-erp-content-toolbar-main">
					<?php epc_erp_render_content_header($erpUrl, $erpArea, $tab, $date_from_str, $date_to_str); ?>
				</div>
				<div class="epc-erp-global-search" id="epc_erp_global_search_wrap">
					<div class="epc-erp-gs-input-wrap">
						<i class="fa fa-search epc-erp-gs-icon"></i>
						<input type="text" id="epc_erp_gs_input" class="epc-erp-gs-input" placeholder="Search modules &amp; records…" autocomplete="off" aria-label="Global search" aria-expanded="false" aria-controls="epc_erp_gs_results">
					</div>
					<div class="epc-erp-gs-results" id="epc_erp_gs_results" hidden></div>
				</div>
				<div class="epc-erp-content-actions">
					<?php
					if (function_exists('epc_erp_render_company_picker_toolbar')) {
						epc_erp_render_company_picker_toolbar();
					}
					?>
					<?php epc_erp_render_notifications_stub($db_link); ?>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h(epc_erp_shell_append_query($guideUrl)); ?>"><i class="fa fa-book"></i> Guide</a>
					<?php if (!empty($epc_erp_cp_links) && !$epc_erp_shell_mode): ?>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($financeOpsUrl); ?>"><i class="fa fa-exchange"></i> Operations</a>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
					<?php endif; ?>
				</div>
			</div>

			<?php if ($epc_erp_demo_mirror): ?>
			<div class="alert alert-warning epc-erp-demo-banner" style="margin-bottom:12px;">
				<i class="fa fa-eye"></i> <strong>Live Super ERP demo (read-only).</strong>
				You're browsing the complete Business OS with every module enabled — sample data, no sign-in. Changes are disabled.
				<a class="btn btn-primary btn-xs" style="margin-left:8px;" href="<?php echo epc_erp_h($portal_home ?? '/erp'); ?>"><i class="fa fa-sign-in"></i> Sign in to your workspace</a>
			</div>
			<?php endif; ?>

			<?php if (!$epc_erp_shell_mode && !$epc_erp_demo_mirror): ?>
			<div class="alert alert-info epc-erp-context-banner">
				<strong>Fulfilment:</strong> customer/supplier payment → stock → delivery → returns.
				<strong>Finance:</strong> revenue &amp; AP when order <strong>Completed</strong>.
				<?php if ($userDeptName !== ''): ?><strong>Your department:</strong> <?php echo epc_erp_h($userDeptName); ?>. <?php endif; ?>
				<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'workflow', $date_from_str, $date_to_str, 'overview')); ?>">Workflow board</a> ·
				<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'fulfilment', $date_from_str, $date_to_str, 'sales')); ?>">Fulfilment</a>
			</div>
			<?php endif; ?>

			<div class="epc-erp-content-body">
			<?php
			// The From/To range only makes sense on transactional lists and reports
			// (ledgers, P&L, sales/purchase docs, aging). Master/setup screens
			// (inventory, COA, contacts, HR, opening balances, etc.) show a current
			// snapshot and must NOT carry a date filter — it confused users who saw
			// a date bar on every screen. Whitelist the date-aware tabs only.
			$epcErpDateFilterTabs = array(
				'proposals', 'sales_orders', 'revenue', 'receivables',
				'delivery_notes', 'invoices', 'purchases', 'payables', 'rfq',
				'purchase_orders', 'three_way_match', 'cash_bank', 'bank_recon',
				'payment_batches', 'petty_cash', 'gl', 'vat_return', 'tax_compliance', 'vat_refund',
				'einvoice', 'pl', 'balance_sheet', 'reports', 'audit',
				'expense_reports', 'marketing',
				// Period reports that filter by the From/To range.
				// (consolidation_bu / master_planning are as-of snapshots, and the
				// master-data modules are excluded, so they carry no date bar.)
				'enterprise_reports', 'bank_setup',
				// BOS pillars: compliance filing calendar (as-at due date) and
				// industry intelligence KPIs both read the From/To range.
				'compliance', 'industry_intel',
			);
			$epcErpShowDateFilter = in_array($tab, $epcErpDateFilterTabs, true);
			?>
			<?php if ($epcErpShowDateFilter): ?>
			<form method="get" class="form-inline epc-erp-filter-bar">
				<input type="hidden" name="area" value="<?php echo epc_erp_h($erpArea); ?>">
				<input type="hidden" name="tab" value="<?php echo epc_erp_h($tab); ?>">
				<?php if ($tab === 'custom_shipping'): ?>
				<?php foreach (array('cs_view', 'cs_category', 'cs_type', 'cs_id', 'cs_report') as $csKeepKey): ?>
				<?php if (!empty($_GET[$csKeepKey])): ?>
				<input type="hidden" name="<?php echo epc_erp_h($csKeepKey); ?>" value="<?php echo epc_erp_h((string) $_GET[$csKeepKey]); ?>">
				<?php endif; ?>
				<?php endforeach; ?>
				<?php endif; ?>
				<?php if ($shellQ = epc_erp_shell_url_query()): ?>
				<input type="hidden" name="epc_erp_shell" value="1">
				<?php endif; ?>
				<label>From</label>
				<input type="date" name="from" class="form-control input-sm" value="<?php echo epc_erp_h($date_from_str); ?>">
				<label>To</label>
				<input type="date" name="to" class="form-control input-sm" value="<?php echo epc_erp_h($date_to_str); ?>">
				<button type="submit" class="btn btn-default btn-sm">Apply dates</button>
			</form>
			<?php endif; ?>

			<div id="epc_erp_msg" class="alert epc-erp-msg"></div>
			<?php
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
			$ftaBannerTabs = array('purchases', 'revenue', 'sales_orders', 'vat_return', 'tax_compliance', 'einvoice', 'invoices', 'payables');
			if (in_array($tab, $ftaBannerTabs, true)) {
				echo epc_uae_fta_erp_banner_html($db_link, $erpUrl);
			}
			?>

			<?php if ($tab === 'dashboard'): ?>
				<?php require __DIR__ . '/erp_dashboard_netsuite.php'; ?>
				<?php if (!empty($crmDash)): ?>
				<div class="alert alert-info" style="margin-top:12px;">
					<strong><i class="fa fa-address-book"></i> CRM pipeline:</strong>
					<?php echo (int)$crmDash['opportunities_open']; ?> open deals ·
					weighted <?php echo epc_erp_money($crmDash['pipeline_weighted']); ?> AED ·
					won MTD <?php echo epc_erp_money($crmDash['won_mtd']); ?> AED ·
					<?php echo (int)$crmDash['leads_new']; ?> new leads ·
					<a href="<?php echo epc_erp_h($crmErpUrl . '&crm_tab=pipeline'); ?>">Open pipeline</a>
				</div>
				<?php endif; ?>
				<?php
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
				if (epc_erp_is_erp_only_context()):
				?>
				<div class="epc-erp-section" style="margin-top:14px;">
					<h4><i class="fa fa-briefcase"></i> ERP-only accounting shortcuts</h4>
					<p class="text-muted">Manual workflow: purchase orders → purchase invoices (PI-), sales orders → sales invoices (SI-), receipt (RV-) and payment (PV-) vouchers, general journals (GV-), transfers (TV-).</p>
					<div class="btn-group" style="flex-wrap:wrap;">
					<?php foreach (epc_erp_erp_only_dashboard_links($erpUrl, $date_from_str, $date_to_str) as $lnk): ?>
						<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, $lnk['tab'], $date_from_str, $date_to_str, $lnk['area'])); ?>"><i class="fa <?php echo epc_erp_h($lnk['icon']); ?>"></i> <?php echo epc_erp_h($lnk['label']); ?></a>
					<?php endforeach; ?>
					</div>
				</div>
				<?php endif; ?>
				<p class="text-muted">Period: <?php echo epc_erp_h(date('d M Y', $date_from)); ?> — <?php echo epc_erp_h(date('d M Y', $date_to)); ?>.
					Revenue, purchase cost, order receivable and order-linked payable apply only when the order is in <strong>Completed</strong> status in CP (all lines finished).
					UAE VAT: prices are <strong>ex VAT</strong>; customer pays ex + 5% output VAT. UAE supplier purchases carry 5% input VAT.
					<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'vat_return', $date_from_str, $date_to_str)); ?>">UAE VAT return</a> ·
					<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'tax_compliance', $date_from_str, $date_to_str, 'finance')); ?>">Tax compliance</a> ·
					<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str)); ?>">E-Invoicing</a> ·
					Operational KPIs from <strong>completed</strong> orders; fulfilment pipeline on the <a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'fulfilment', $date_from_str, $date_to_str, 'sales')); ?>">Fulfilment</a> tab.</p>

			<?php elseif (isset($erpTabIncludes[$tab])): ?>
				<?php require __DIR__ . '/' . $erpTabIncludes[$tab]; ?>

			<?php elseif (strpos($tab, 'rc_') === 0): ?>
				<?php require __DIR__ . '/erp_tabs_reports.php'; ?>

			<?php elseif ($tab === 'revenue'): ?>
				<?php $rows = epc_erp_revenue_report($db_link, $date_from, $date_to); ?>
				<div class="epc-erp-section">
					<h4><i class="fa fa-line-chart"></i> Sales revenue by order</h4>
					<p class="text-muted">Sale, purchase, margin and due amounts appear only after the order reaches <strong>Completed</strong> status in CP. In-progress orders are listed with zero financial columns.</p>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr>
							<th>Order</th><th>Date</th><th>Customer</th><th>ERP status</th><th>Sale ex VAT</th><th>Output VAT</th><th>Incl. VAT</th><th>Purchase</th><th>Margin</th><th>Paid</th><th>Due (incl. VAT)</th><th></th>
						</tr></thead>
						<tbody>
						<?php foreach ($rows as $r): ?>
							<?php $is_complete = !empty($r['order_complete']); ?>
							<tr>
								<td><?php if (!empty($ordersUrl)): ?><a href="<?php echo epc_erp_h($ordersUrl . '?order_id=' . (int)$r['id']); ?>">#<?php echo (int)$r['id']; ?></a><?php else: ?>#<?php echo (int)$r['id']; ?><?php endif; ?></td>
								<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)$r['time'])); ?></td>
								<td><?php echo epc_erp_h($r['customer_email'] ?: ('User ' . (int)$r['user_id'])); ?></td>
								<td><?php echo $is_complete ? '<span class="label label-success">Complete</span>' : '<span class="label label-default">' . epc_erp_h($r['order_status_name'] ?: 'In progress') . '</span>'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['sale_ex_vat']) : '—'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['sale_vat'] ?? 0) : '—'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['sale_incl_vat'] ?? 0) : '—'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['purchase_ex_vat']) : '—'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['profit_ex_vat']) : '—'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['paid_amount']) : '—'; ?></td>
								<td><?php echo $is_complete ? epc_erp_money($r['due_amount']) : '—'; ?></td>
								<td><?php if ($is_complete): ?><?php echo ((int)$r['paid'] === 1) ? '<span class="label label-success">Paid</span>' : '<span class="label label-warning">Due</span>'; ?><?php else: ?><span class="label label-default">Pending</span><?php endif; ?></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>

					<h4>Order revenue adjustment / settlement</h4>
					<p class="text-muted">Correct customer balance for a specific order (discount, write-off, net settlement). Resolves the order&apos;s customer automatically.</p>
					<form id="epc_erp_form_order_settle" class="form-inline epc-erp-form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID" required>
						<select name="entry_kind" class="form-control input-sm">
							<?php foreach (epc_erp_settlement_kinds() as $k => $lbl): ?>
								<option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($lbl); ?></option>
							<?php endforeach; ?>
						</select>
						<select name="direction" class="form-control input-sm">
							<option value="credit">Credit (+ balance)</option>
							<option value="debit">Debit (− balance / write-off)</option>
						</select>
						<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
						<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
						<input type="text" name="note" class="form-control input-sm" placeholder="Note">
						<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1"> Post to GL</label>
						<button type="submit" class="btn btn-sm btn-warning">Post entry</button>
					</form>
				</div>

			<?php elseif ($tab === 'receivables'): ?>
				<?php
				$customers = epc_erp_receivables($db_link);
				$view_user = isset($_GET['user_id']) ? (int)$_GET['user_id'] : 0;
				?>
				<div class="epc-erp-section">
					<h4><i class="fa fa-users"></i> Customer receivable balances</h4>
					<?php
					erp_d365_assets();
					erp_action_pane_ribbon(array(
						array('label' => 'Customer', 'key' => 'ar', 'active' => true, 'groups' => array(
							array('label' => 'View', 'buttons' => array(
								array('label' => 'Refresh', 'icon' => 'fa-refresh', 'url' => epc_erp_tab_url($erpUrl, 'receivables', $date_from_str, $date_to_str)),
							)),
						)),
						array('label' => 'Collect', 'key' => 'collect', 'groups' => array(
							array('label' => 'Collections', 'buttons' => array(
								array('label' => 'Statement', 'icon' => 'fa-file-text-o', 'disabled' => true),
								array('label' => 'Settlement', 'icon' => 'fa-balance-scale', 'disabled' => true),
							)),
						)),
					));
					erp_list_toolbar(array(
						'views' => array('All customers', 'Open balances'),
						'search' => array('placeholder' => 'Filter customers', 'target' => '#epc_erp_ar_tbl'),
					));
					?>
					<p class="text-muted">Ledger balance = customer account credits minus debits (top-ups, payments). <strong>Order due</strong> counts only completed orders. Receivable/settlement entries for an order require Completed status.</p>
					<table class="table table-striped table-bordered table-condensed epc-erp-table" id="epc_erp_ar_tbl">
						<thead><tr><th class="epc-d365-statcol"></th><th data-sort="text">Customer</th><th class="num" data-sort="num">Orders</th><th class="num" data-sort="num">Completed</th><th class="num" data-sort="num">Ledger balance</th><th class="num" data-sort="num">Order due (complete)</th><th></th></tr></thead>
						<tbody>
						<?php $epcArBal = 0.0; $epcArDue = 0.0; foreach ($customers as $c): $epcArBal += (float)$c['balance']; $epcArDue += (float)$c['order_receivable_due']; ?>
							<tr>
								<td class="epc-d365-statcol"><?php echo erp_status_dot((float)$c['order_receivable_due'] > 0 ? 'warn' : 'ok'); ?></td>
								<td><?php echo epc_erp_h($c['email'] ?: ('User #' . (int)$c['user_id'])); ?></td>
								<td class="num"><?php echo (int)$c['order_count']; ?></td>
								<td class="num"><?php echo (int)$c['complete_order_count']; ?></td>
								<td class="num"><strong><?php echo epc_erp_money($c['balance']); ?></strong></td>
								<td class="num"><?php echo epc_erp_money($c['order_receivable_due']); ?></td>
								<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'receivables', $date_from_str, $date_to_str) . '&user_id=' . (int)$c['user_id']); ?>">Statement</a></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
						<?php if (!empty($customers)): ?>
						<tfoot><tr class="epc-d365-sumrow"><td class="epc-d365-statcol"></td><td colspan="3">Sum (<?php echo count($customers); ?> customers)</td><td class="num"><?php echo epc_erp_money($epcArBal); ?></td><td class="num"><?php echo epc_erp_money($epcArDue); ?></td><td></td></tr></tfoot>
						<?php endif; ?>
					</table>
					<?php if ($view_user > 0): ?>
						<h4>Customer statement — user #<?php echo (int)$view_user; ?></h4>
						<p class="text-muted">SO, SI, RV, and ledger activity for <?php echo epc_erp_h(date('d M Y', $date_from)); ?> — <?php echo epc_erp_h(date('d M Y', $date_to)); ?>.</p>
						<table class="table table-bordered table-condensed">
							<thead><tr><th>Date</th><th>Voucher</th><th>Type</th><th>Description</th><th>Debit</th><th>Credit</th></tr></thead>
							<tbody>
							<?php foreach (epc_erp_customer_statement($db_link, $view_user, 200, $date_from, $date_to) as $line): ?>
								<tr>
									<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)($line['time'] ?? 0))); ?></td>
									<td><?php echo epc_erp_h($line['voucher_no'] ?? '—'); ?></td>
									<td><?php echo epc_erp_h($line['voucher_type'] ?? ''); ?></td>
									<td><?php echo epc_erp_h($line['description'] ?? ''); ?></td>
									<td><?php echo !empty($line['debit']) ? epc_erp_money($line['debit']) : '—'; ?></td>
									<td><?php echo !empty($line['credit']) ? epc_erp_money($line['credit']) : '—'; ?></td>
								</tr>
							<?php endforeach; ?>
							</tbody>
						</table>
						<h4>Customer adjustment / settlement</h4>
						<p class="text-muted">Post a non-cash correction to this customer&apos;s ledger (credit increases balance, debit decreases). Optional link to an order for revenue settlement.</p>
						<form id="epc_erp_form_customer_settle" class="form-inline epc-erp-form-inline">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
							<input type="hidden" name="user_id" value="<?php echo (int)$view_user; ?>">
							<select name="entry_kind" class="form-control input-sm">
								<?php foreach (epc_erp_settlement_kinds() as $k => $lbl): ?>
									<option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($lbl); ?></option>
								<?php endforeach; ?>
							</select>
							<select name="direction" class="form-control input-sm">
								<option value="credit">Credit (+ balance)</option>
								<option value="debit">Debit (− balance)</option>
							</select>
							<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
							<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID (opt.)" value="">
							<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
							<input type="text" name="note" class="form-control input-sm" placeholder="Note">
							<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1"> Post to GL</label>
							<button type="submit" class="btn btn-sm btn-warning">Post entry</button>
						</form>
						<?php if (!empty($financeOpsUrl)): ?><p><a class="btn btn-sm btn-primary" href="<?php echo epc_erp_h($financeOpsUrl); ?>">Post customer operation</a></p><?php endif; ?>
					<?php endif; ?>
				</div>

			<?php elseif ($tab === 'payables'): ?>
				<div class="epc-erp-section">
					<h4><i class="fa fa-truck"></i> Supplier payable balances</h4>
					<?php
					erp_d365_assets();
					erp_action_pane_ribbon(array(
						array('label' => 'Vendor', 'key' => 'ap', 'active' => true, 'groups' => array(
							array('label' => 'New', 'buttons' => array(
								array('label' => 'Supplier', 'icon' => 'fa-plus', 'class' => 'is-primary', 'target' => '#epc_erp_form_supplier'),
							)),
							array('label' => 'Data', 'buttons' => array(
								array('label' => 'Sync from warehouses', 'icon' => 'fa-refresh', 'target' => '#epc_erp_sync_suppliers'),
							)),
						)),
						array('label' => 'Pay', 'key' => 'pay', 'groups' => array(
							array('label' => 'Payments', 'buttons' => array(
								array('label' => 'Record payment', 'icon' => 'fa-money', 'class' => 'is-primary', 'target' => '#epc_erp_form_supplier_pay'),
							)),
						)),
					));
					erp_list_toolbar(array(
						'views' => array('All suppliers', 'Open balances'),
						'search' => array('placeholder' => 'Filter suppliers', 'target' => '#epc_erp_ap_tbl'),
					));
					?>
					<p class="text-muted">Payable excludes purchase/AP entries linked to orders that are not yet <strong>Completed</strong> in CP.</p>
					<p>
						<button type="button" class="btn btn-sm btn-default" id="epc_erp_sync_suppliers"><i class="fa fa-refresh"></i> Sync from warehouses</button>
					</p>
					<table class="table table-striped table-bordered table-condensed epc-erp-table" id="epc_erp_ap_tbl">
						<thead><tr><th class="epc-d365-statcol"></th><th data-sort="text">Supplier</th><th data-sort="text">Country</th><th data-sort="text">TRN</th><th data-sort="text">Storage ID</th><th class="num" data-sort="num">Payable balance (AED)</th><th></th></tr></thead>
						<tbody>
						<?php $epcApBal = 0.0; foreach ($suppliers as $s): $epcApBal += (float)$s['balance']; ?>
							<tr>
								<td class="epc-d365-statcol"><?php echo erp_status_dot((float)$s['balance'] > 0 ? 'warn' : 'ok'); ?></td>
								<td><?php echo epc_erp_h($s['name']); ?></td>
								<td><?php echo epc_erp_h(isset($s['country_code']) ? $s['country_code'] : 'AE'); ?>
									<?php if (!empty($s['vat_registered'])): ?><span class="label label-success">VAT</span><?php else: ?><span class="label label-default">No VAT</span><?php endif; ?>
								</td>
								<td><?php echo epc_erp_h($s['trn'] ?: '—'); ?></td>
								<td><?php echo $s['storage_id'] ? (int)$s['storage_id'] : '—'; ?></td>
								<td class="num"><strong><?php echo epc_erp_money($s['balance']); ?></strong></td>
								<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'payables', $date_from_str, $date_to_str) . '&supplier_id=' . (int)$s['id']); ?>">Ledger</a></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
						<?php if (!empty($suppliers)): ?>
						<tfoot><tr class="epc-d365-sumrow"><td class="epc-d365-statcol"></td><td colspan="4">Sum (<?php echo count($suppliers); ?> suppliers)</td><td class="num"><?php echo epc_erp_money($epcApBal); ?></td><td></td></tr></tfoot>
						<?php endif; ?>
					</table>
					<?php
					$view_sup = isset($_GET['supplier_id']) ? (int)$_GET['supplier_id'] : 0;
					if ($view_sup > 0):
					?>
						<h4>Supplier ledger #<?php echo (int)$view_sup; ?></h4>
						<table class="table table-bordered table-condensed">
							<thead><tr><th>Date</th><th>Type</th><th>Amount</th><th>Ref</th><th>Note</th></tr></thead>
							<tbody>
							<?php foreach (epc_erp_supplier_statement($db_link, $view_sup) as $line): ?>
								<tr>
									<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)$line['time'])); ?></td>
									<td><?php echo epc_erp_h(epc_erp_supplier_entry_kind_label(isset($line['entry_kind']) ? $line['entry_kind'] : (((int)$line['is_credit'] === 1) ? 'invoice' : 'payment'))); ?></td>
									<td><?php echo epc_erp_money($line['amount']); ?></td>
									<td><?php echo epc_erp_h($line['reference']); ?></td>
									<td><?php echo epc_erp_h($line['note']); ?></td>
								</tr>
							<?php endforeach; ?>
							</tbody>
						</table>

						<h4>Supplier adjustment / settlement (non-cash)</h4>
						<p class="text-muted">Increase or decrease payable without a cash payment — e.g. credit note, net-off, write-off.</p>
						<form id="epc_erp_form_supplier_settle" class="form-inline epc-erp-form-inline">
							<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
							<input type="hidden" name="supplier_id" value="<?php echo (int)$view_sup; ?>">
							<select name="entry_kind" class="form-control input-sm">
								<?php foreach (epc_erp_settlement_kinds() as $k => $lbl): ?>
									<option value="<?php echo epc_erp_h($k); ?>"><?php echo epc_erp_h($lbl); ?></option>
								<?php endforeach; ?>
							</select>
							<select name="direction" class="form-control input-sm">
								<option value="decrease">Decrease payable</option>
								<option value="increase">Increase payable</option>
							</select>
							<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
							<input type="number" name="purchase_id" class="form-control input-sm" placeholder="Purchase ID (opt.)">
							<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
							<input type="text" name="note" class="form-control input-sm" placeholder="Note">
							<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1"> Post to GL</label>
							<button type="submit" class="btn btn-sm btn-warning">Post entry</button>
						</form>
					<?php endif; ?>

					<?php
						// Legal entity / business unit options for the vendor master form.
						$leOptsVnd = array();
						$buOptsVnd = array();
						try {
							foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_pm_legal_entities` WHERE `active` = 1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $le) {
								$leOptsVnd[(int) $le['id']] = $le['code'] . ' · ' . $le['name'];
							}
						} catch (Exception $e) {
						}
						try {
							foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_pm_business_units` WHERE `active` = 1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $bu) {
								$buOptsVnd[(int) $bu['id']] = $bu['code'] . ' · ' . $bu['name'];
							}
						} catch (Exception $e) {
						}
					?>
					<h4>Add vendor</h4>
					<form id="epc_erp_form_supplier" class="form-horizontal epc-erp-form-inline" style="max-width:960px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<div class="row">
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Vendor name *</label><div class="col-sm-8"><input type="text" name="name" class="form-control input-sm" placeholder="Vendor name" required></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Vendor account</label><div class="col-sm-8"><input type="text" name="vendor_account" class="form-control input-sm" placeholder="e.g. V-0001"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Vendor group</label><div class="col-sm-8"><input type="text" name="vendor_group" class="form-control input-sm" placeholder="e.g. Local / Import / Service"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Legal entity</label><div class="col-sm-8"><select name="legal_entity_id" class="form-control input-sm"><option value="0">— none —</option>
								<?php foreach ($leOptsVnd as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
							</select></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Business unit</label><div class="col-sm-8"><select name="business_unit_id" class="form-control input-sm"><option value="0">— none —</option>
								<?php foreach ($buOptsVnd as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
							</select></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Currency</label><div class="col-sm-8"><input type="text" name="currency_code" class="form-control input-sm" value="AED"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Country</label><div class="col-sm-8"><input type="text" name="country_code" class="form-control input-sm" placeholder="Country (AE)" value="AE"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">TRN / Tax reg.</label><div class="col-sm-8"><input type="text" name="trn" class="form-control input-sm" placeholder="TRN (UAE VAT)"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Registration no.</label><div class="col-sm-8"><input type="text" name="registration_number" class="form-control input-sm" placeholder="Commercial / company reg."></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Payment terms</label><div class="col-sm-8"><input type="text" name="payment_terms" class="form-control input-sm" placeholder="e.g. Net 30"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Payment method</label><div class="col-sm-8"><input type="text" name="payment_method" class="form-control input-sm" placeholder="e.g. Bank transfer / Cheque"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Delivery terms</label><div class="col-sm-8"><input type="text" name="delivery_terms" class="form-control input-sm" placeholder="Incoterms e.g. CIF / FOB"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Delivery mode</label><div class="col-sm-8"><input type="text" name="delivery_mode" class="form-control input-sm" placeholder="e.g. Sea / Air / Road"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Credit limit</label><div class="col-sm-8"><input type="number" step="0.01" name="credit_limit" class="form-control input-sm" placeholder="0.00"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">On hold</label><div class="col-sm-8"><select name="on_hold" class="form-control input-sm"><option value="no">No</option><option value="invoice">Invoice</option><option value="payment">Payment</option><option value="all">All</option></select></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Bank name</label><div class="col-sm-8"><input type="text" name="bank_name" class="form-control input-sm" placeholder="e.g. Emirates NBD"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Bank account no.</label><div class="col-sm-8"><input type="text" name="bank_account_number" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">IBAN</label><div class="col-sm-8"><input type="text" name="iban" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">SWIFT / BIC</label><div class="col-sm-8"><input type="text" name="swift_bic" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Contact person</label><div class="col-sm-8"><input type="text" name="contact_person" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">E-mail</label><div class="col-sm-8"><input type="email" name="contact_email" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Phone</label><div class="col-sm-8"><input type="text" name="contact_phone" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Website</label><div class="col-sm-8"><input type="text" name="website" class="form-control input-sm" placeholder="https://"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Address</label><div class="col-sm-8"><input type="text" name="address" class="form-control input-sm" placeholder="Street, building"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">City</label><div class="col-sm-8"><input type="text" name="city" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">State / region</label><div class="col-sm-8"><input type="text" name="state_region" class="form-control input-sm"></div></div>
							<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Postal code</label><div class="col-sm-8"><input type="text" name="postal_code" class="form-control input-sm"></div></div>
							<div class="col-sm-12 form-group"><label class="col-sm-2 control-label">Notes</label><div class="col-sm-10"><input type="text" name="notes" class="form-control input-sm"></div></div>
						</div>
						<label class="checkbox-inline"><input type="checkbox" name="vat_registered" value="1" checked> UAE VAT registered (5% input)</label>
						<label class="checkbox-inline"><input type="checkbox" name="tax_exempt" value="1"> Tax exempt</label>
						<?php echo epc_erp_dim_render_fields($db_link, array(), array('layout' => 'inline')); ?>
						<div style="margin-top:8px;"><button type="submit" class="btn btn-sm btn-primary">Save vendor</button></div>
					</form>

					<h4>Record supplier payment</h4>
					<form id="epc_erp_form_supplier_pay" class="form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<select name="supplier_id" class="form-control input-sm" required>
							<option value="">Supplier</option>
							<?php foreach ($suppliers as $s): ?>
								<option value="<?php echo (int)$s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option>
							<?php endforeach; ?>
						</select>
						<select name="account_id" class="form-control input-sm" required>
							<option value="">Pay from account</option>
							<?php foreach ($accounts as $a): ?>
								<option value="<?php echo (int)$a['id']; ?>"><?php echo epc_erp_h($a['name']); ?> (<?php echo epc_erp_money($a['balance']); ?>)</option>
							<?php endforeach; ?>
						</select>
						<input type="number" step="0.01" name="amount" class="form-control input-sm" placeholder="Amount AED" required>
						<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
						<?php echo epc_erp_dim_render_fields($db_link, array(), array('layout' => 'inline')); ?>
						<button type="submit" class="btn btn-sm btn-warning">Post payment</button>
					</form>
				</div>

			<?php elseif ($tab === 'purchases'): ?>
				<?php
				$purchases = epc_erp_list_purchases($db_link);
				$bcBosFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
				if (is_file($bcBosFile)) {
					require_once $bcBosFile;
				}
				$viewPurchaseId = isset($_GET['purchase_id']) ? (int) $_GET['purchase_id'] : 0;
				$viewPurchase = null;
				if ($viewPurchaseId > 0) {
					foreach ($purchases as $pp) {
						if ((int) ($pp['id'] ?? 0) === $viewPurchaseId) {
							$viewPurchase = $pp;
							break;
						}
					}
				}
				?>
				<div class="epc-erp-section">
					<h4><i class="fa fa-file-text-o"></i> Purchase invoices (supplier payable)</h4>
					<?php if ($viewPurchase):
						$grnBadge = function_exists('epc_bc_bos_grn_badge_html')
							? epc_bc_bos_grn_badge_html($viewPurchase, array('show_uid' => true))
							: '';
						$grnRef = function_exists('epc_bc_bos_grn_record_id') ? epc_bc_bos_grn_record_id($viewPurchase) : '';
						?>
					<p><a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'purchases', $date_from_str, $date_to_str)); ?>"><i class="fa fa-arrow-left"></i> All purchases</a></p>
					<?php if ($grnBadge !== ''): ?>
					<div class="alert alert-info" style="margin-bottom:14px">
						<strong><i class="fa fa-link"></i> Blockchain BOS GRN proof</strong>
						<span style="margin-left:10px"><?php echo $grnBadge; ?></span>
						<?php if ($grnRef !== ''): ?><small class="text-muted" style="margin-left:8px">Record <code><?php echo epc_erp_h($grnRef); ?></code></small><?php endif; ?>
						<a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'blockchain_proofs', $date_from_str, $date_to_str) . '&bc_type=grn'); ?>" class="btn btn-default btn-xs" style="margin-left:8px">All GRN proofs</a>
					</div>
					<?php elseif (!empty($viewPurchase['inv_receipt_posted'])): ?>
					<div class="alert alert-warning">Stock received — no blockchain proof found yet (mode off, or proof pending create).</div>
					<?php else: ?>
					<div class="alert alert-default" style="border:1px solid #e2e8f0">No inventory receipt posted for this purchase — GRN proof is created when stock is received.</div>
					<?php endif; ?>
					<div class="well" style="background:#fff">
						<p><strong>Purchase #<?php echo (int) $viewPurchase['id']; ?></strong>
						· <?php echo epc_erp_h((string) ($viewPurchase['invoice_number'] ?? '')); ?>
						· <?php echo epc_erp_h((string) ($viewPurchase['supplier_name'] ?? '')); ?></p>
						<p>Total <?php echo epc_erp_money($viewPurchase['total_amount'] ?? 0); ?>
						· Receipt <?php echo !empty($viewPurchase['inv_receipt_posted']) ? '<span class="label label-success">posted</span>' : '<span class="label label-default">not posted</span>'; ?></p>
					</div>
					<?php endif; ?>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>ID</th><th>Date</th><th>Supplier</th><th>Invoice</th><th>Order</th><th>Ex VAT</th><th>VAT</th><th>Total</th><th>Status</th><th>Receipt</th><th>Blockchain</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($purchases as $p):
							$pBadge = (function_exists('epc_bc_bos_grn_badge_html') && !empty($p['inv_receipt_posted']))
								? epc_bc_bos_grn_badge_html($p)
								: '';
							$pView = epc_erp_tab_url($erpUrl, 'purchases', $date_from_str, $date_to_str) . '&purchase_id=' . (int) $p['id'];
							?>
							<tr>
								<td><?php echo (int)$p['id']; ?></td>
								<td><?php echo epc_erp_h(date('Y-m-d', (int)$p['purchase_date'])); ?></td>
								<td><?php echo epc_erp_h($p['supplier_name']); ?></td>
								<td><?php echo epc_erp_h($p['invoice_number']); ?></td>
								<td><?php echo (int)$p['order_id'] ? ('#' . (int)$p['order_id']) : '—'; ?></td>
								<td><?php echo epc_erp_money($p['amount_ex_vat']); ?></td>
								<td><?php echo epc_erp_money($p['vat_amount']); ?></td>
								<td><?php echo epc_erp_money($p['total_amount']); ?></td>
								<td><?php echo epc_erp_h($p['status']); ?></td>
								<td><?php echo !empty($p['inv_receipt_posted']) ? '<span class="label label-success">GRN</span>' : '<span class="text-muted">—</span>'; ?></td>
								<td><?php echo $pBadge !== '' ? $pBadge : '<span class="text-muted">—</span>'; ?></td>
								<td><a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($pView); ?>">View</a></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
					</table>

					<p class="text-muted">Amount is <strong>ex VAT</strong>. VAT <?php echo epc_erp_h(number_format(epc_uae_vat_rate_percent($db_link), 2)); ?>% added automatically for <strong>UAE (AE)</strong> VAT-registered suppliers only.</p>
					<form id="epc_erp_form_purchase" class="form-horizontal" style="max-width:920px;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<div class="form-group form-inline">
							<select name="supplier_id" class="form-control input-sm" required>
								<option value="">Supplier</option>
								<?php foreach ($suppliers as $s): ?>
									<option value="<?php echo (int)$s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option>
								<?php endforeach; ?>
							</select>
							<input type="text" name="invoice_number" class="form-control input-sm" placeholder="Invoice no.">
							<input type="number" step="0.01" name="amount_ex_vat" class="form-control input-sm" placeholder="Amount ex VAT" required>
							<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID (opt.)">
							<input type="text" name="note" class="form-control input-sm" placeholder="Note">
						</div>
						<div class="well well-sm" style="margin-bottom:10px;">
							<label><input type="checkbox" name="receive_inventory" value="1" id="epc_purchase_recv_inv"> <strong>Receive into inventory</strong> (purchase in on save)</label>
							<div id="epc_purchase_inv_block" style="margin-top:10px;display:none;">
								<select name="warehouse_id" class="form-control input-sm">
									<option value="">Warehouse (or map from supplier storage)</option>
									<?php foreach ($invWarehouses as $w): ?>
									<option value="<?php echo (int)$w['id']; ?>"><?php echo epc_erp_h($w['code'] . ' — ' . $w['name']); ?></option>
									<?php endforeach; ?>
								</select>
								<p class="text-muted" style="margin:8px 0 4px;font-size:12px;">Receipt lines (CSV): <code>sku,qty,unit_cost,batch_no,expiry_date</code> — one row per SKU. Unit cost optional (uses invoice amount ÷ qty for single line).</p>
								<textarea name="inventory_csv" class="form-control input-sm" rows="4" placeholder="sku,qty,unit_cost&#10;PART-001,10,25.50&#10;PART-002,5,12.00"></textarea>
							</div>
						</div>
						<div class="form-inline" style="margin-bottom:10px;"><?php echo epc_erp_dim_render_fields($db_link, array(), array('layout' => 'inline')); ?></div>
						<button type="submit" class="btn btn-sm btn-primary">Record purchase + optional stock receipt</button>
					</form>
					<script>
					(function(){
						var cb = document.getElementById('epc_purchase_recv_inv');
						var blk = document.getElementById('epc_purchase_inv_block');
						if (cb && blk) {
							cb.addEventListener('change', function(){ blk.style.display = cb.checked ? 'block' : 'none'; });
						}
					})();
					</script>

					<h4>Create purchase from order cost</h4>
					<p class="text-muted">Allowed only when the order is in <strong>Completed</strong> status. Posts supplier bill from order purchase cost and, when order lines have article codes, auto-receives <strong>purchase_in</strong> stock lines (creates ERP SKUs if missing).<?php if (!empty($epc_erp_cp_links)): ?> Same action is available in <a href="/<?php echo epc_erp_h((string)$DP_Config->backend_dir); ?>/shop/procurement/procurement">Procurement</a>.<?php endif; ?></p>
					<form id="epc_erp_form_purchase_order" class="form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<input type="number" name="order_id" class="form-control input-sm" placeholder="Order ID" required>
						<select name="supplier_id" class="form-control input-sm" required>
							<option value="">Supplier</option>
							<?php foreach ($suppliers as $s): ?>
								<option value="<?php echo (int)$s['id']; ?>"><?php echo epc_erp_h($s['name']); ?></option>
							<?php endforeach; ?>
						</select>
						<button type="submit" class="btn btn-sm btn-default">Generate from order</button>
					</form>

					<h4>Purchase cost adjustment</h4>
					<p class="text-muted">Change purchase ex-VAT amount and post matching supplier ledger entry (positive = increase payable, negative = credit note).</p>
					<form id="epc_erp_form_purchase_adj" class="form-inline epc-erp-form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<input type="number" name="purchase_id" class="form-control input-sm" placeholder="Purchase ID" required>
						<input type="number" step="0.01" name="delta_ex_vat" class="form-control input-sm" placeholder="Δ ex VAT (+/−)" required>
						<input type="text" name="reference" class="form-control input-sm" placeholder="Reference">
						<input type="text" name="note" class="form-control input-sm" placeholder="Note">
						<label class="checkbox-inline"><input type="checkbox" name="post_gl" value="1"> Post to GL</label>
						<button type="submit" class="btn btn-sm btn-warning">Adjust purchase</button>
					</form>
				</div>

			<?php elseif (in_array($tab, array('coa', 'gl', 'pl', 'balance_sheet'), true)): ?>
				<?php require __DIR__ . '/erp_tabs_accounting.php'; ?>
			<?php endif; ?>

			</div><!-- .epc-erp-content-body -->
		</div><!-- .epc-erp-content -->
	</div><!-- .epc-erp-layout -->
</div>

<script>
(function(){
	var erpPostUrl = <?php echo json_encode($erpAjaxEndpoint); ?>;
	var erpDoorBase = <?php echo json_encode($erpUrl); ?>;
	var erpIsFrontend = <?php echo (isset($epc_erp_portal) && $epc_erp_portal === 'frontend') ? 'true' : 'false'; ?>;
	var msgEl = document.getElementById('epc_erp_msg');
	function showMsg(ok, text) {
		if (!msgEl) return;
		msgEl.className = 'alert epc-erp-msg ' + (ok ? 'alert-success' : 'alert-danger');
		msgEl.textContent = text;
		msgEl.style.display = 'block';
	}
	function parseJsonResponse(r) {
		return r.text().then(function(t) {
			try { return JSON.parse(t); }
			catch (e) { throw new Error('Server returned invalid JSON (HTTP ' + r.status + '). Try refreshing the page.'); }
		});
	}
	function postAction(action, form) {
		var fd = new FormData(form);
		fd.append('action', action);
		return fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
			.then(parseJsonResponse)
			.then(function(j){
				showMsg(!!j.status, j.message || (j.status ? 'OK' : 'Error'));
				if (j.status && j.redirect) {
					var red = j.redirect;
					if (red.indexOf('/shop/finance/erp') !== -1) {
						if (erpIsFrontend) {
							// Keep ERP-only tenants on the standalone /erp/ door — never
							// bounce a CP /cp/shop/finance/erp redirect into the control panel.
							var qpos = red.indexOf('?');
							var qs = qpos >= 0 ? red.substring(qpos + 1) : '';
							red = erpDoorBase + (qs ? ((erpDoorBase.indexOf('?') >= 0 ? '&' : '?') + qs) : '');
						} else if (red.indexOf('epc_erp_shell=') === -1) {
							red += (red.indexOf('?') >= 0 ? '&' : '?') + 'epc_erp_shell=1';
						}
					}
					setTimeout(function(){ location.href = red; }, 600);
				} else if (j.status) {
					setTimeout(function(){ location.reload(); }, 800);
				}
				return j;
			})
			.catch(function(){ showMsg(false, 'Request failed'); });
	}
	function bindForm(id, action) {
		var f = document.getElementById(id);
		if (!f) return;
		f.addEventListener('submit', function(ev){
			ev.preventDefault();
			postAction(action, f);
		});
	}
	bindForm('epc_erp_form_supplier', 'create_supplier');
	bindForm('epc_erp_form_supplier_pay', 'supplier_payment');
	bindForm('epc_erp_form_purchase', 'create_purchase');
	bindForm('epc_erp_form_purchase_order', 'purchase_from_order');
	bindForm('epc_erp_form_purchase_adj', 'purchase_adjustment');
	bindForm('epc_erp_form_as_rma', 'as_rma_create');
	bindForm('epc_erp_form_account', 'create_account');
	bindForm('epc_erp_form_entry', 'cash_entry');
	bindForm('epc_erp_form_customer_settle', 'customer_settlement');
	bindForm('epc_erp_form_supplier_settle', 'supplier_settlement');
	bindForm('epc_erp_form_order_settle', 'order_settlement');
	bindForm('epc_erp_form_workflow_create', 'workflow_create');
	// BOS pillars (compliance, approvals, industry intelligence) — generic binder.
	document.querySelectorAll('form[data-bos-action]').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction(f.getAttribute('data-bos-action'), f); });
	});
	bindForm('epc_erp_form_marketing_create', 'marketing_create');
	bindForm('epc_erp_form_rfq', 'save_rfq');
	bindForm('epc_erp_form_delivery_note', 'delivery_note_create');
	bindForm('epc_erp_form_po', 'po_save');
	bindForm('epc_erp_form_so', 'so_save');
	bindForm('epc_erp_form_customer', 'customer_create');
	bindForm('epc_erp_form_receipt_voucher', 'receipt_voucher');
	bindForm('epc_erp_form_payment_voucher', 'payment_voucher');
	bindForm('epc_erp_form_transfer_voucher', 'transfer_voucher');
	document.querySelectorAll('.epc-erp-po-invoice').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('po_to_invoice', f); });
	});
	document.querySelectorAll('.epc-erp-so-status').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('so_status', f); });
	});
	document.querySelectorAll('.epc-erp-so-invoice').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('so_to_invoice', f); });
	});
	document.querySelectorAll('.epc-erp-so-delete').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); if (window.confirm('Delete this draft sales order? This cannot be undone.')) { postAction('so_delete', f); } });
	});
	document.querySelectorAll('.epc-erp-pm-form').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('pm_save', f); });
	});
	document.querySelectorAll('.epc-erp-pm-budget-form').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('pm_budget_save', f); });
	});
	document.querySelectorAll('.epc-erp-pm-budgetline-form').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('pm_budget_line_save', f); });
	});
	document.querySelectorAll('.epc-erp-pm-listing-form').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('pm_listing_save', f); });
	});
	document.querySelectorAll('.epc-erp-pm-cheque-form').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('pm_cheque_save', f); });
	});
	bindForm('epc_erp_form_payment_batch', 'payment_batch_save');
	bindForm('epc_erp_form_petty_cash', 'petty_cash_save');
	bindForm('epc_erp_form_agenda', 'agenda_save');
	bindForm('epc_erp_form_kb', 'kb_save');
	bindForm('epc_erp_form_multi_entity', 'multi_entity_save');
	bindForm('epc_erp_form_bank_import', 'bank_import');
	bindForm('epc_erp_form_invoice', 'invoice_save');
	bindForm('epc_erp_form_doc_upload', 'document_upload');
	bindForm('epc_erp_form_inv_attachment', 'document_upload');
	document.querySelectorAll('.epc-erp-recon-match').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('bank_reconcile', f); });
	});
	document.querySelectorAll('.epc-erp-po-status').forEach(function(f){
		f.addEventListener('submit', function(ev){ ev.preventDefault(); postAction('po_status', f); });
	});
	document.querySelectorAll('.epc-erp-po-lines-toggle').forEach(function(el){
		el.addEventListener('click', function(ev){
			ev.preventDefault();
			var row = document.getElementById('epc_erp_po_lines_row_' + el.getAttribute('data-po'));
			if (row) row.style.display = (row.style.display === 'none' || !row.style.display) ? '' : 'none';
		});
	});
	document.querySelectorAll('.epc-erp-po-receive').forEach(function(f){
		f.addEventListener('submit', function(ev){
			ev.preventDefault();
			var received = {};
			f.querySelectorAll('.epc-erp-po-recv-input').forEach(function(inp){
				received[inp.getAttribute('data-line')] = parseFloat(inp.value || '0');
			});
			f.querySelector('.epc-erp-po-received-json').value = JSON.stringify(received);
			postAction('po_receive_lines', f);
		});
	});
	var syncBtn = document.getElementById('epc_erp_sync_suppliers');
	if (syncBtn) {
		syncBtn.addEventListener('click', function(){
			var fd = new FormData();
			fd.append('action', 'sync_suppliers');
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){
					showMsg(!!j.status, j.message || 'Done');
					if (j.status) setTimeout(function(){ location.reload(); }, 800);
				});
		});
	}
	// Expose the door-aware AJAX endpoint + poster so per-tab scripts (e.g. the
	// receipt/payment voucher settlement grids) can call the same endpoint.
	window.epcErpPostUrl = erpPostUrl;
	window.epcErpPost = postAction;

	// Favourites: star/unstar handlers
	document.querySelectorAll('.epc-erp-fav-star').forEach(function(btn){
		btn.addEventListener('click', function(e){
			e.preventDefault(); e.stopPropagation();
			var tab = this.getAttribute('data-tab');
			var area = this.getAttribute('data-area');
			var fd = new FormData();
			fd.append('action', 'erp_fav_add');
			fd.append('tab_key', tab);
			fd.append('area_key', area);
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, {method:'POST', body:fd, credentials:'same-origin'})
				.then(function(r){return r.json()})
				.then(function(j){ if(j.status) location.reload(); });
		});
	});
	document.querySelectorAll('.epc-erp-fav-unstar').forEach(function(btn){
		btn.addEventListener('click', function(e){
			e.preventDefault(); e.stopPropagation();
			var tab = this.getAttribute('data-tab');
			var fd = new FormData();
			fd.append('action', 'erp_fav_remove');
			fd.append('tab_key', tab);
			var csrf = document.querySelector('input[name="csrf_guard_key"]');
			if (csrf) fd.append('csrf_guard_key', csrf.value);
			fetch(erpPostUrl, {method:'POST', body:fd, credentials:'same-origin'})
				.then(function(r){return r.json()})
				.then(function(j){ if(j.status) location.reload(); });
		});
	});
})();
</script>
<script>
/* ── ERP Global Search ───────────────────────────────────────────── */
(function(){
	var inp   = document.getElementById('epc_erp_gs_input');
	var wrap  = document.getElementById('epc_erp_gs_results');
	if (!inp || !wrap) return;
	var timer = null;
	var erpPostUrl = <?php echo json_encode($erpAjaxEndpoint); ?>;
	var erpBase    = <?php echo json_encode($erpUrl); ?>;
	var from       = <?php echo json_encode($date_from_str); ?>;
	var to         = <?php echo json_encode($date_to_str); ?>;
	var csrf       = (document.querySelector('input[name="csrf_guard_key"]') || {}).value || '';

	function buildUrl(res) {
		var base = erpBase + '?area=' + encodeURIComponent(res.area) + '&tab=' + encodeURIComponent(res.tab)
			+ '&from=' + encodeURIComponent(from) + '&to=' + encodeURIComponent(to);
		if (res.param) base += '&' + res.param;
		return base;
	}

	function render(results) {
		if (!results || results.length === 0) {
			wrap.innerHTML = '<div class="epc-erp-gs-empty"><i class="fa fa-search"></i> No results</div>';
			wrap.hidden = false;
			inp.setAttribute('aria-expanded', 'true');
			return;
		}
		var modules = results.filter(function(r){ return r.type === 'module'; });
		var records = results.filter(function(r){ return r.type === 'record'; });
		var html = '';

		if (modules.length) {
			html += '<div class="epc-erp-gs-group-hd">Modules</div>';
			modules.forEach(function(r){
				html += '<a class="epc-erp-gs-item" href="' + buildUrl(r) + '">'
					+ '<i class="fa ' + (r.icon || 'fa-circle-o') + ' epc-erp-gs-item-icon"></i>'
					+ '<span class="epc-erp-gs-item-label">' + escHtml(r.label) + '</span>'
					+ '<span class="epc-erp-gs-item-sub">' + escHtml(r.sub || '') + '</span></a>';
			});
		}
		if (records.length) {
			var groups = {};
			records.forEach(function(r){ (groups[r.group] = groups[r.group] || []).push(r); });
			Object.keys(groups).forEach(function(g){
				html += '<div class="epc-erp-gs-group-hd">Records · ' + escHtml(g) + '</div>';
				groups[g].forEach(function(r){
					html += '<a class="epc-erp-gs-item" href="' + buildUrl(r) + '">'
						+ '<i class="fa ' + (r.icon || 'fa-circle-o') + ' epc-erp-gs-item-icon"></i>'
						+ '<span class="epc-erp-gs-item-label">' + escHtml(r.label) + '</span>'
						+ '<span class="epc-erp-gs-item-sub">' + escHtml(r.sub || '') + '</span></a>';
				});
			});
		}
		wrap.innerHTML = html;
		wrap.hidden = false;
		inp.setAttribute('aria-expanded', 'true');
	}

	function escHtml(s) {
		return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
	}

	function doSearch(q) {
		var fd = new FormData();
		fd.append('action', 'erp_global_search');
		fd.append('q', q);
		fd.append('csrf_guard_key', csrf);
		fetch(erpPostUrl, {method:'POST', body:fd, credentials:'same-origin'})
			.then(function(r){ return r.json(); })
			.then(function(j){ if (j.status) render(j.results || []); })
			.catch(function(){});
	}

	inp.addEventListener('input', function(){
		var q = inp.value.trim();
		clearTimeout(timer);
		if (q.length < 2) { wrap.hidden = true; inp.setAttribute('aria-expanded','false'); return; }
		timer = setTimeout(function(){ doSearch(q); }, 280);
	});

	inp.addEventListener('keydown', function(e){
		if (e.key === 'Escape') { wrap.hidden = true; inp.setAttribute('aria-expanded','false'); inp.value = ''; }
		if (e.key === 'ArrowDown') {
			var first = wrap.querySelector('.epc-erp-gs-item');
			if (first) { e.preventDefault(); first.focus(); }
		}
	});

	wrap.addEventListener('keydown', function(e){
		if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
			e.preventDefault();
			var items = Array.from(wrap.querySelectorAll('.epc-erp-gs-item'));
			var idx = items.indexOf(document.activeElement);
			if (e.key === 'ArrowDown') idx = Math.min(idx + 1, items.length - 1);
			else idx = Math.max(idx - 1, 0);
			if (idx >= 0 && items[idx]) items[idx].focus();
		}
		if (e.key === 'Escape') { wrap.hidden = true; inp.setAttribute('aria-expanded','false'); inp.focus(); }
	});

	document.addEventListener('click', function(e){
		var searchWrap = document.getElementById('epc_erp_global_search_wrap');
		if (searchWrap && !searchWrap.contains(e.target)) {
			wrap.hidden = true;
			inp.setAttribute('aria-expanded','false');
		}
	});
})();
</script>
<?php
// The /erp/ portal door renders this shell without the CP desktop template that
// normally emits the sidebar navigation JS, so the left-menu module groups had
// no click handler and never expanded (dead clicks). Inline the same idempotent
// accordion + mobile-nav script here so navigation works on every door.
$epcErpNavJsFile = $_SERVER['DOCUMENT_ROOT'] . '/cp/js/epc_erp_shell_nav.js';
if (is_file($epcErpNavJsFile)) {
	echo '<script id="epc-erp-shell-nav-js-inline">' . "\n";
	echo file_get_contents($epcErpNavJsFile);
	echo "\n" . '</script>' . "\n";
} elseif (function_exists('epc_erp_shell_nav_script_tag')) {
	echo epc_erp_shell_nav_script_tag();
}

// AI Voice Command Widget — floating mic button on every ERP page
include __DIR__ . '/erp_voice_command.php';

// Voice Command JS — external via PHP proxy (same proven pattern as nav JS above)
if (function_exists('epc_erp_voice_command_js_script_tag')) {
	echo epc_erp_voice_command_js_script_tag();
}
?>
