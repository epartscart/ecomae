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

$userAllowedTabs = epc_erp_user_allowed_tabs($db_link);
$modFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
if (is_file($modFile)) {
	require_once $modFile;
	if (function_exists('epc_erp_filter_tabs_by_tenant_modules')) {
		$userAllowedTabs = epc_erp_filter_tabs_by_tenant_modules($userAllowedTabs);
	}
}
if (function_exists('epc_crm_pack_enabled') && epc_crm_pack_enabled() && epc_crm_user_can_access($db_link) && !in_array('crm', $userAllowedTabs, true)) {
	$userAllowedTabs[] = 'crm';
}
if (!in_array('bank_recon', $userAllowedTabs, true) && in_array('cash_bank', $userAllowedTabs, true)) {
	$userAllowedTabs[] = 'bank_recon';
}

$tab = isset($_GET['tab']) ? (string) $_GET['tab'] : 'dashboard';
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
$userDeptCode = epc_erp_staff_primary_department($db_link);
$userDeptName = $userDeptCode !== '' ? epc_erp_staff_department_name($userDeptCode) : '';

$erpTabIncludes = array(
	'sales_orders' => 'erp_tabs_sales_orders.php',
	'proposals' => 'erp_tabs_proposals.php',
	'delivery_notes' => 'erp_tabs_delivery_notes.php',
	'rfq' => 'erp_tabs_rfq.php',
	'purchase_orders' => 'erp_tabs_purchase_orders.php',
	'three_way_match' => 'erp_tabs_three_way_match.php',
	'cash_bank' => 'erp_tabs_cash_bank.php',
	'payment_batches' => 'erp_tabs_payment_batches.php',
	'petty_cash' => 'erp_tabs_petty_cash.php',
	'fulfilment' => 'erp_tabs_fulfilment.php',
	'staff' => 'erp_tabs_staff.php',
	'workflow' => 'erp_tabs_workflow.php',
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
	// D365 F&O-style structural / master-data modules
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
);

?>

<?php if (!$epc_erp_shell_mode): ?>
<?php $epcErpCssVer = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260530erp1'; ?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=<?php echo htmlspecialchars($epcErpCssVer, ENT_QUOTES, 'UTF-8'); ?>">
<link rel="stylesheet" href="/content/shop/finance/epc_erp_professional.css?v=<?php echo htmlspecialchars($epcErpCssVer, ENT_QUOTES, 'UTF-8'); ?>">
<?php endif; ?>
<style>
.epc-erp-msg { display:none; margin:10px 0; }
</style>
<?php
if (!$epc_erp_shell_mode) {
	echo epc_cp_sidebar_early_init_script();
	echo epc_cp_menu_sections_script();
}
?>

<div class="col-lg-12 epc-erp-shell epc-erp-shell--layout<?php echo $epc_erp_shell_mode ? ' epc-erp-shell--pro' : ''; ?>">
	<div class="epc-erp-layout">
		<aside class="epc-erp-sidebar" id="epc_erp_sidebar" aria-label="ERP navigation">
			<div class="epc-erp-sidebar-head">
				<span class="epc-erp-sidebar-brand"><i class="fa fa-cubes"></i> Ecom BOS</span>
				<button type="button" class="epc-erp-sidebar-collapse-toggle" id="epc_erp_sidebar_collapse_toggle" aria-expanded="true" aria-label="Collapse sidebar"><i class="fa fa-chevron-left"></i></button>
				<button type="button" class="epc-erp-sidebar-close" id="epc_erp_sidebar_close" aria-label="Close menu"><i class="fa fa-times"></i></button>
			</div>
			<?php epc_erp_render_sidebar_nav($erpUrl, $erpArea, $tab, $date_from_str, $date_to_str, $userAllowedTabs); ?>
		</aside>
		<div class="epc-erp-sidebar-backdrop" id="epc_erp_sidebar_backdrop" aria-hidden="true"></div>

		<div class="epc-erp-content">
			<div class="epc-erp-content-toolbar">
				<button type="button" class="epc-erp-sidebar-toggle btn btn-default btn-sm" id="epc_erp_sidebar_toggle" aria-label="Open menu" aria-expanded="false"><i class="fa fa-bars"></i></button>
				<div class="epc-erp-content-toolbar-main">
					<?php epc_erp_render_content_header($erpUrl, $erpArea, $tab, $date_from_str, $date_to_str); ?>
				</div>
				<div class="epc-erp-content-actions">
					<?php epc_erp_render_notifications_stub($db_link); ?>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h(epc_erp_shell_append_query($guideUrl)); ?>"><i class="fa fa-book"></i> Guide</a>
					<?php if (!empty($epc_erp_cp_links) && !$epc_erp_shell_mode): ?>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($financeOpsUrl); ?>"><i class="fa fa-exchange"></i> Operations</a>
					<a class="btn btn-default btn-xs" href="<?php echo epc_erp_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
					<?php endif; ?>
				</div>
			</div>

			<?php if (!$epc_erp_shell_mode): ?>
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
				// D365 F&O period reports that filter by the From/To range.
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
					<p class="text-muted">Ledger balance = customer account credits minus debits (top-ups, payments). <strong>Order due</strong> counts only completed orders. Receivable/settlement entries for an order require Completed status.</p>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Customer</th><th>Orders</th><th>Completed</th><th>Ledger balance</th><th>Order due (complete)</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($customers as $c): ?>
							<tr>
								<td><?php echo epc_erp_h($c['email'] ?: ('User #' . (int)$c['user_id'])); ?></td>
								<td><?php echo (int)$c['order_count']; ?></td>
								<td><?php echo (int)$c['complete_order_count']; ?></td>
								<td><strong><?php echo epc_erp_money($c['balance']); ?></strong></td>
								<td><?php echo epc_erp_money($c['order_receivable_due']); ?></td>
								<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'receivables', $date_from_str, $date_to_str) . '&user_id=' . (int)$c['user_id']); ?>">Statement</a></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
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
					<p class="text-muted">Payable excludes purchase/AP entries linked to orders that are not yet <strong>Completed</strong> in CP.</p>
					<p>
						<button type="button" class="btn btn-sm btn-default" id="epc_erp_sync_suppliers"><i class="fa fa-refresh"></i> Sync from warehouses</button>
					</p>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>Supplier</th><th>Country</th><th>TRN</th><th>Storage ID</th><th>Payable balance (AED)</th><th></th></tr></thead>
						<tbody>
						<?php foreach ($suppliers as $s): ?>
							<tr>
								<td><?php echo epc_erp_h($s['name']); ?></td>
								<td><?php echo epc_erp_h(isset($s['country_code']) ? $s['country_code'] : 'AE'); ?>
									<?php if (!empty($s['vat_registered'])): ?><span class="label label-success">VAT</span><?php else: ?><span class="label label-default">No VAT</span><?php endif; ?>
								</td>
								<td><?php echo epc_erp_h($s['trn'] ?: '—'); ?></td>
								<td><?php echo $s['storage_id'] ? (int)$s['storage_id'] : '—'; ?></td>
								<td><strong><?php echo epc_erp_money($s['balance']); ?></strong></td>
								<td><a class="btn btn-xs btn-default" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'payables', $date_from_str, $date_to_str) . '&supplier_id=' . (int)$s['id']); ?>">Ledger</a></td>
							</tr>
						<?php endforeach; ?>
						</tbody>
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

					<h4>Add supplier</h4>
					<form id="epc_erp_form_supplier" class="epc-erp-form-inline">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<div class="form-group"><input type="text" name="name" class="form-control input-sm" placeholder="Supplier name" required></div>
						<div class="form-group"><input type="text" name="country_code" class="form-control input-sm" placeholder="Country (AE)" value="AE"></div>
						<div class="form-group"><input type="email" name="contact_email" class="form-control input-sm" placeholder="E-mail"></div>
						<div class="form-group"><input type="text" name="trn" class="form-control input-sm" placeholder="TRN (UAE VAT)"></div>
						<label class="checkbox-inline"><input type="checkbox" name="vat_registered" value="1" checked> UAE VAT registered (5% input)</label>
						<?php echo epc_erp_dim_render_fields($db_link, array(), array('layout' => 'inline')); ?>
						<button type="submit" class="btn btn-sm btn-primary">Save supplier</button>
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
				<?php $purchases = epc_erp_list_purchases($db_link); ?>
				<div class="epc-erp-section">
					<h4><i class="fa fa-file-text-o"></i> Purchase invoices (supplier payable)</h4>
					<table class="table table-striped table-bordered table-condensed">
						<thead><tr><th>ID</th><th>Date</th><th>Supplier</th><th>Invoice</th><th>Order</th><th>Ex VAT</th><th>VAT</th><th>Total</th><th>Status</th></tr></thead>
						<tbody>
						<?php foreach ($purchases as $p): ?>
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
?>
