<?php
/**
 * ERP Accounting + Business Process Automation catalogue.
 *
 * Single source of truth for every automation available in ERP:
 * status detection, enable/disable flags, visual pipeline metadata,
 * template seeding into the workflow builder, and scheduled tick runner.
 */
declare(strict_types=1);

if (!defined('EPC_ERP_AUTOMATION_CATALOGUE_VERSION')) {
	define('EPC_ERP_AUTOMATION_CATALOGUE_VERSION', '1.0.0');
}

/**
 * @return array<string,array<string,mixed>>
 */
function epc_erp_automation_catalogue(): array
{
	return array(
		/* ── Accounting automations ── */
		'order_to_erp' => array(
			'id' => 'order_to_erp',
			'category' => 'accounting',
			'name' => 'Order → ERP posting',
			'icon' => 'fa-shopping-cart',
			'desc' => 'When an order is placed: create AR invoice, post GL journal, deduct inventory, calculate VAT.',
			'pipeline' => array('Validate', 'AR Invoice', 'GL Journal', 'Inventory', 'VAT', 'Notify'),
			'tab' => 'receivables',
			'area' => 'ar',
			'engine' => 'epc_order_erp_pipeline.php',
			'setting_key' => 'auto_order_to_erp',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),
		'period_close' => array(
			'id' => 'period_close',
			'category' => 'accounting',
			'name' => 'Period close checklist',
			'icon' => 'fa-lock',
			'desc' => 'Soft-close → lock fiscal periods; block backdated journals after month-end.',
			'pipeline' => array('Checklist', 'Soft close', 'Lock period', 'Fiscal lock'),
			'tab' => 'year_end',
			'area' => 'finance',
			'engine' => 'epc_erp_period_close.php',
			'setting_key' => 'auto_period_close',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),
		'year_end_close' => array(
			'id' => 'year_end_close',
			'category' => 'accounting',
			'name' => 'Year-end P&L close',
			'icon' => 'fa-calendar-check-o',
			'desc' => 'Close revenue/expense to retained earnings and roll opening balances.',
			'pipeline' => array('Trial balance', 'P&L close', 'RE posting', 'Open next year'),
			'tab' => 'year_end',
			'area' => 'finance',
			'engine' => 'erp_tabs_year_end.php',
			'setting_key' => 'auto_year_end',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),
		'bank_recon' => array(
			'id' => 'bank_recon',
			'category' => 'accounting',
			'name' => 'Bank reconciliation assist',
			'icon' => 'fa-university',
			'desc' => 'Import bank CSV, match statement lines to ledger, flag unmatched.',
			'pipeline' => array('Import CSV', 'Match lines', 'Unmatched queue', 'Reconcile'),
			'tab' => 'bank_recon',
			'area' => 'banking',
			'engine' => 'epc_erp_phase8.php',
			'setting_key' => 'auto_bank_recon',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),
		'collections_dunning' => array(
			'id' => 'collections_dunning',
			'category' => 'accounting',
			'name' => 'Collections & dunning',
			'icon' => 'fa-gavel',
			'desc' => '7-step overdue reminder sequence with escalation and credit holds.',
			'pipeline' => array('Aging scan', 'Day 7', 'Day 30', 'Day 60', 'Day 90', 'Escalate', 'Hold'),
			'tab' => 'collections',
			'area' => 'credit_coll',
			'engine' => 'epc_collections_dunning.php',
			'setting_key' => 'auto_collections_dunning',
			'default_on' => true,
			'guide' => 'collections',
		),
		'report_scheduler' => array(
			'id' => 'report_scheduler',
			'category' => 'accounting',
			'name' => 'Report scheduler',
			'icon' => 'fa-clock-o',
			'desc' => 'Daily/weekly/monthly financial reports emailed to recipients.',
			'pipeline' => array('Schedule', 'Generate', 'Format', 'Email'),
			'tab' => 'report_scheduler',
			'area' => 'finance',
			'engine' => 'epc_erp_report_scheduler.php',
			'setting_key' => 'auto_report_scheduler',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),
		'vat_reminder' => array(
			'id' => 'vat_reminder',
			'category' => 'accounting',
			'name' => 'VAT filing reminder',
			'icon' => 'fa-percent',
			'desc' => 'Remind tax owners 7 days before VAT return deadline.',
			'pipeline' => array('Calendar', '7-day alert', 'Notify', 'Open return'),
			'tab' => 'vat_return',
			'area' => 'tax',
			'engine' => 'workflow',
			'setting_key' => 'auto_vat_reminder',
			'default_on' => false,
			'guide' => 'accounting_automation',
			'workflow_template' => 'vat_filing_reminder',
		),
		'gl_auto_post' => array(
			'id' => 'gl_auto_post',
			'category' => 'accounting',
			'name' => 'Document → GL auto-post',
			'icon' => 'fa-book',
			'desc' => 'Balanced journals from invoices, payments, receipts and stock moves.',
			'pipeline' => array('Document', 'Validate', 'Balance check', 'Post GL'),
			'tab' => 'gl',
			'area' => 'finance',
			'engine' => 'epc_erp_gl.php',
			'setting_key' => 'auto_gl_post',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),
		'payment_reminder' => array(
			'id' => 'payment_reminder',
			'category' => 'accounting',
			'name' => 'AP payment due reminder',
			'icon' => 'fa-credit-card',
			'desc' => 'Alert treasury when supplier invoices approach due date.',
			'pipeline' => array('Scan AP due', 'Notify treasury', 'Payment batch'),
			'tab' => 'payables',
			'area' => 'ap',
			'engine' => 'workflow',
			'setting_key' => 'auto_ap_payment_reminder',
			'default_on' => false,
			'guide' => 'accounting_automation',
			'workflow_template' => 'ap_payment_due',
		),
		'depreciation_run' => array(
			'id' => 'depreciation_run',
			'category' => 'accounting',
			'name' => 'Fixed-asset depreciation',
			'icon' => 'fa-building',
			'desc' => 'Monthly depreciation schedules posting to GL.',
			'pipeline' => array('Schedule', 'Compute', 'Post GL', 'Register update'),
			'tab' => 'fixed_assets',
			'area' => 'asset_mgmt',
			'engine' => 'fixed_assets',
			'setting_key' => 'auto_depreciation',
			'default_on' => true,
			'guide' => 'accounting_automation',
		),

		/* ── Business process automations ── */
		'po_approval' => array(
			'id' => 'po_approval',
			'category' => 'process',
			'name' => 'PO approval chain',
			'icon' => 'fa-check-circle',
			'desc' => 'Manager → Finance → Director thresholds; auto-approve small POs.',
			'pipeline' => array('PO created', 'Amount check', 'Manager', 'Finance', 'Director', 'Approved'),
			'tab' => 'approvals',
			'area' => 'overview',
			'engine' => 'epc_po_approval.php',
			'setting_key' => 'auto_po_approval',
			'default_on' => true,
			'guide' => 'workflow',
			'workflow_template' => 'po_approval_chain',
		),
		'invoice_autosend' => array(
			'id' => 'invoice_autosend',
			'category' => 'process',
			'name' => 'Invoice auto-send',
			'icon' => 'fa-paper-plane',
			'desc' => 'Email tax invoices to customers when order/invoice is completed.',
			'pipeline' => array('Invoice posted', 'Template', 'Email', 'Log'),
			'tab' => 'invoices',
			'area' => 'sales',
			'engine' => 'workflow',
			'setting_key' => 'auto_invoice_send',
			'default_on' => false,
			'guide' => 'bpa_automation',
			'workflow_template' => 'invoice_auto_send',
		),
		'low_stock_alert' => array(
			'id' => 'low_stock_alert',
			'category' => 'process',
			'name' => 'Low stock alert',
			'icon' => 'fa-exclamation-triangle',
			'desc' => 'Notify procurement and create reorder tasks below ROP.',
			'pipeline' => array('Stock event', 'Below ROP?', 'Notify', 'Create task'),
			'tab' => 'order_planning',
			'area' => 'inventory_mgmt',
			'engine' => 'workflow',
			'setting_key' => 'auto_low_stock',
			'default_on' => false,
			'guide' => 'bpa_automation',
			'workflow_template' => 'low_stock_alert',
		),
		'employee_onboarding' => array(
			'id' => 'employee_onboarding',
			'category' => 'process',
			'name' => 'Employee onboarding',
			'icon' => 'fa-user-plus',
			'desc' => 'Create onboarding tasks when a new employee record is created.',
			'pipeline' => array('Hire', 'IT setup', 'HR docs', 'Manager intro', 'Done'),
			'tab' => 'hr',
			'area' => 'people',
			'engine' => 'workflow',
			'setting_key' => 'auto_employee_onboarding',
			'default_on' => false,
			'guide' => 'bpa_automation',
			'workflow_template' => 'employee_onboarding',
		),
		'daily_sales_summary' => array(
			'id' => 'daily_sales_summary',
			'category' => 'process',
			'name' => 'Daily sales summary',
			'icon' => 'fa-bar-chart',
			'desc' => 'Email daily sales KPIs to management at end of day.',
			'pipeline' => array('Schedule 18:00', 'Aggregate', 'Email'),
			'tab' => 'report_scheduler',
			'area' => 'finance',
			'engine' => 'workflow',
			'setting_key' => 'auto_daily_sales',
			'default_on' => false,
			'guide' => 'bpa_automation',
			'workflow_template' => 'daily_sales_summary',
		),
		'aml_alert' => array(
			'id' => 'aml_alert',
			'category' => 'process',
			'name' => 'AML compliance alert',
			'icon' => 'fa-shield',
			'desc' => 'Flag high-value transactions for compliance review.',
			'pipeline' => array('Txn event', 'Threshold', 'Flag case', 'Notify compliance'),
			'tab' => 'aml_compliance',
			'area' => 'tax',
			'engine' => 'epc_erp_aml_compliance.php',
			'setting_key' => 'auto_aml_alert',
			'default_on' => true,
			'guide' => 'bpa_automation',
			'workflow_template' => 'aml_compliance_alert',
		),
		'process_flow_routing' => array(
			'id' => 'process_flow_routing',
			'category' => 'process',
			'name' => 'Process flow routing',
			'icon' => 'fa-sitemap',
			'desc' => 'GPS-style chained task routing across departments with SLA tracking.',
			'pipeline' => array('Start case', 'Route step', 'SLA', 'Approve', 'Next', 'Done'),
			'tab' => 'processflow',
			'area' => 'overview',
			'engine' => 'epc_erp_processflow.php',
			'setting_key' => 'auto_process_flow',
			'default_on' => true,
			'guide' => 'process_flow',
		),
		'three_way_match' => array(
			'id' => 'three_way_match',
			'category' => 'process',
			'name' => '3-way match (PO/GRN/Bill)',
			'icon' => 'fa-exchange',
			'desc' => 'Match purchase order, goods receipt and supplier bill before payment.',
			'pipeline' => array('PO', 'GRN', 'Bill', 'Match', 'Pay'),
			'tab' => 'three_way_match',
			'area' => 'procurement',
			'engine' => 'three_way_match',
			'setting_key' => 'auto_three_way_match',
			'default_on' => true,
			'guide' => 'bpa_automation',
		),
		'subscription_billing' => array(
			'id' => 'subscription_billing',
			'category' => 'process',
			'name' => 'Subscription recurring billing',
			'icon' => 'fa-refresh',
			'desc' => 'Auto-generate recurring invoices on billing cycles.',
			'pipeline' => array('Cycle due', 'Invoice', 'Collect', 'Renew'),
			'tab' => 'subscriptions',
			'area' => 'sales',
			'engine' => 'subscriptions',
			'setting_key' => 'auto_subscription_billing',
			'default_on' => true,
			'guide' => 'subscription',
		),
		'rma_warranty' => array(
			'id' => 'rma_warranty',
			'category' => 'process',
			'name' => 'Warranty / RMA workflow',
			'icon' => 'fa-wrench',
			'desc' => 'RMA request → approve → receive → inspect → refund/replace.',
			'pipeline' => array('Request', 'Approve', 'Receive', 'Inspect', 'Resolve'),
			'tab' => 'aftersales',
			'area' => 'service',
			'engine' => 'aftersales',
			'setting_key' => 'auto_rma_warranty',
			'default_on' => true,
			'guide' => 'warranty',
		),
		'credit_check' => array(
			'id' => 'credit_check',
			'category' => 'process',
			'name' => 'Credit limit gate',
			'icon' => 'fa-ban',
			'desc' => 'Block or warn when order would exceed customer credit limit.',
			'pipeline' => array('Order', 'Credit check', 'Allow/Hold', 'Notify'),
			'tab' => 'collections',
			'area' => 'credit_coll',
			'engine' => 'workflow',
			'setting_key' => 'auto_credit_check',
			'default_on' => true,
			'guide' => 'bpa_automation',
			'workflow_template' => 'credit_limit_gate',
		),
		'goods_receipt_notify' => array(
			'id' => 'goods_receipt_notify',
			'category' => 'process',
			'name' => 'Goods receipt notify',
			'icon' => 'fa-truck',
			'desc' => 'Notify buyer and AP when GRN is posted against a PO.',
			'pipeline' => array('GRN posted', 'Notify buyer', 'Notify AP'),
			'tab' => 'purchase_orders',
			'area' => 'procurement',
			'engine' => 'workflow',
			'setting_key' => 'auto_grn_notify',
			'default_on' => false,
			'guide' => 'bpa_automation',
			'workflow_template' => 'grn_notify',
		),
	);
}

/**
 * @return array<string,array<string,mixed>>
 */
function epc_erp_automation_by_category(string $category): array
{
	$out = array();
	foreach (epc_erp_automation_catalogue() as $id => $row) {
		if (($row['category'] ?? '') === $category) {
			$out[$id] = $row;
		}
	}
	return $out;
}

function epc_erp_automation_is_enabled(PDO $db, string $id): bool
{
	$cat = epc_erp_automation_catalogue();
	if (!isset($cat[$id])) {
		return false;
	}
	$key = 'erp_auto_' . (string) ($cat[$id]['setting_key'] ?? $id);
	$default = !empty($cat[$id]['default_on']) ? '1' : '0';
	if (!function_exists('epc_erp_adv_get_setting')) {
		require_once __DIR__ . '/epc_erp_advanced.php';
	}
	$val = epc_erp_adv_get_setting($db, $key, $default);
	return $val === '1' || $val === 'true' || $val === 'yes';
}

function epc_erp_automation_set_enabled(PDO $db, string $id, bool $on): bool
{
	$cat = epc_erp_automation_catalogue();
	if (!isset($cat[$id])) {
		return false;
	}
	$key = 'erp_auto_' . (string) ($cat[$id]['setting_key'] ?? $id);
	if (!function_exists('epc_erp_adv_set_setting')) {
		require_once __DIR__ . '/epc_erp_advanced.php';
	}
	epc_erp_adv_set_setting($db, $key, $on ? '1' : '0');
	return true;
}

/**
 * Detect whether the underlying engine/module is present on disk.
 */
function epc_erp_automation_engine_present(array $item): bool
{
	$engine = (string) ($item['engine'] ?? '');
	if ($engine === '' || $engine === 'workflow' || $engine === 'fixed_assets'
		|| $engine === 'three_way_match' || $engine === 'subscriptions' || $engine === 'aftersales') {
		return true;
	}
	$doc = isset($_SERVER['DOCUMENT_ROOT']) ? (string) $_SERVER['DOCUMENT_ROOT'] : dirname(__DIR__, 2);
	$path = $doc . '/content/shop/finance/' . ltrim($engine, '/');
	if (is_file($path)) {
		return true;
	}
	$path2 = $doc . '/content/general_pages/' . ltrim($engine, '/');
	return is_file($path2);
}

/**
 * Enrich catalogue with live status for UI.
 *
 * @return array<int,array<string,mixed>>
 */
function epc_erp_automation_status_list(PDO $db, string $siteKey = ''): array
{
	$list = array();
	$wfCounts = array('active' => 0, 'total' => 0, 'runs' => 0);
	$wfFile = __DIR__ . '/../../general_pages/epc_workflow_builder.php';
	if (is_file($wfFile)) {
		require_once $wfFile;
		if (function_exists('epc_workflow_ensure_schema') && $siteKey !== '') {
			try {
				epc_workflow_ensure_schema($db);
				$workflows = epc_workflow_list($db, $siteKey);
				$wfCounts['total'] = count($workflows);
				foreach ($workflows as $w) {
					if (!empty($w['active'])) {
						$wfCounts['active']++;
					}
					$wfCounts['runs'] += (int) ($w['run_count'] ?? 0);
				}
			} catch (Throwable $e) {
				/* ignore */
			}
		}
	}

	foreach (epc_erp_automation_catalogue() as $id => $item) {
		$present = epc_erp_automation_engine_present($item);
		$enabled = $present && epc_erp_automation_is_enabled($db, $id);
		$status = 'missing';
		if ($present && $enabled) {
			$status = 'active';
		} elseif ($present) {
			$status = 'available';
		}
		$item['status'] = $status;
		$item['enabled'] = $enabled;
		$item['present'] = $present;
		$list[] = $item;
	}

	return array(
		'items' => $list,
		'kpis' => array(
			'total' => count($list),
			'active' => count(array_filter($list, static function ($i) { return ($i['status'] ?? '') === 'active'; })),
			'available' => count(array_filter($list, static function ($i) { return ($i['status'] ?? '') === 'available'; })),
			'accounting' => count(array_filter($list, static function ($i) { return ($i['category'] ?? '') === 'accounting'; })),
			'process' => count(array_filter($list, static function ($i) { return ($i['category'] ?? '') === 'process'; })),
			'workflows_active' => $wfCounts['active'],
			'workflows_total' => $wfCounts['total'],
			'workflow_runs' => $wfCounts['runs'],
		),
	);
}

/**
 * Full BPA workflow template library (installable into epc_workflows).
 *
 * @return array<string,array<string,mixed>>
 */
function epc_erp_automation_workflow_templates(): array
{
	return array(
		'po_approval_chain' => array(
			'name' => 'PO Approval Chain',
			'description' => 'Route POs through manager → finance → director by amount',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'po.created'),
			'steps' => array(
				array('step_type' => 'condition', 'action_type' => '', 'label' => 'Under auto-approve threshold?', 'config' => array('field' => 'total', 'operator' => '<', 'value' => 500)),
				array('step_type' => 'action', 'action_type' => 'update_status', 'label' => 'Auto-approve', 'config' => array('new_status' => 'approved')),
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Notify requester', 'config' => array('title' => 'PO Auto-Approved', 'message' => 'PO #{{po_number}} auto-approved')),
			),
		),
		'invoice_auto_send' => array(
			'name' => 'Invoice Auto-Send',
			'description' => 'Email invoice when order completes',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'invoice.posted'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_email', 'label' => 'Email customer', 'config' => array('to' => '{{customer_email}}', 'subject' => 'Invoice #{{invoice_number}}', 'body' => 'Please find your invoice attached.')),
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Log send', 'config' => array('title' => 'Invoice sent', 'message' => 'Invoice #{{invoice_number}} emailed')),
			),
		),
		'low_stock_alert' => array(
			'name' => 'Low Stock Alert',
			'description' => 'Notify procurement below reorder point',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'stock.below'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Alert procurement', 'config' => array('title' => 'Low stock', 'message' => '{{sku}} below ROP')),
				array('step_type' => 'action', 'action_type' => 'create_task', 'label' => 'Reorder task', 'config' => array('title' => 'Reorder {{sku}}', 'assignee' => 'procurement', 'due_days' => 2)),
			),
		),
		'vat_filing_reminder' => array(
			'name' => 'VAT Filing Reminder',
			'description' => 'Remind 7 days before VAT deadline',
			'trigger_type' => 'schedule',
			'trigger_config' => array('cron_expression' => '0 9 * * 1', 'timezone' => 'Asia/Dubai'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'VAT reminder', 'config' => array('title' => 'VAT filing due', 'message' => 'VAT return deadline approaching')),
				array('step_type' => 'action', 'action_type' => 'send_email', 'label' => 'Email tax owner', 'config' => array('to' => '{{tax_email}}', 'subject' => 'VAT filing reminder', 'body' => 'Please prepare the VAT return.')),
			),
		),
		'overdue_escalation' => array(
			'name' => 'Overdue Invoice Escalation',
			'description' => 'Dunning sequence at 30/60/90 days',
			'trigger_type' => 'schedule',
			'trigger_config' => array('cron_expression' => '0 10 * * *', 'timezone' => 'Asia/Dubai'),
			'steps' => array(
				array('step_type' => 'condition', 'action_type' => '', 'label' => 'Days overdue ≥ 7', 'config' => array('field' => 'days_overdue', 'operator' => '>=', 'value' => 7)),
				array('step_type' => 'action', 'action_type' => 'send_email', 'label' => 'Payment reminder', 'config' => array('to' => '{{customer_email}}', 'subject' => 'Payment reminder', 'body' => 'Invoice #{{invoice_number}} is overdue')),
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Collections queue', 'config' => array('title' => 'Overdue escalation', 'message' => 'Invoice #{{invoice_number}} escalated')),
			),
		),
		'employee_onboarding' => array(
			'name' => 'Employee Onboarding',
			'description' => 'Onboarding tasks for new hires',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'employee.created'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'create_task', 'label' => 'IT account', 'config' => array('title' => 'Provision accounts for {{employee_name}}', 'assignee' => 'it', 'due_days' => 1)),
				array('step_type' => 'action', 'action_type' => 'create_task', 'label' => 'HR docs', 'config' => array('title' => 'Collect onboarding docs', 'assignee' => 'hr', 'due_days' => 3)),
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Notify manager', 'config' => array('title' => 'New hire', 'message' => '{{employee_name}} onboarding started')),
			),
		),
		'daily_sales_summary' => array(
			'name' => 'Daily Sales Summary',
			'description' => 'Email daily sales to management',
			'trigger_type' => 'schedule',
			'trigger_config' => array('cron_expression' => '0 18 * * *', 'timezone' => 'Asia/Dubai'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_email', 'label' => 'Email summary', 'config' => array('to' => '{{mgmt_email}}', 'subject' => 'Daily sales summary', 'body' => 'Sales for today are ready in the ERP dashboard.')),
			),
		),
		'aml_compliance_alert' => array(
			'name' => 'AML Compliance Alert',
			'description' => 'Flag transactions above AML threshold',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'payment.posted'),
			'steps' => array(
				array('step_type' => 'condition', 'action_type' => '', 'label' => 'Above AML threshold', 'config' => array('field' => 'amount', 'operator' => '>=', 'value' => 55000)),
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Flag compliance', 'config' => array('title' => 'AML review required', 'message' => 'Payment {{payment_id}} exceeds threshold')),
				array('step_type' => 'action', 'action_type' => 'create_task', 'label' => 'Compliance case', 'config' => array('title' => 'AML review {{payment_id}}', 'assignee' => 'compliance', 'due_days' => 1)),
			),
		),
		'ap_payment_due' => array(
			'name' => 'AP Payment Due Reminder',
			'description' => 'Alert treasury before supplier due dates',
			'trigger_type' => 'schedule',
			'trigger_config' => array('cron_expression' => '0 8 * * *', 'timezone' => 'Asia/Dubai'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Treasury alert', 'config' => array('title' => 'AP payments due', 'message' => 'Supplier invoices approaching due date')),
			),
		),
		'credit_limit_gate' => array(
			'name' => 'Credit Limit Gate',
			'description' => 'Hold orders that exceed credit limit',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'order.placed'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'credit_check', 'label' => 'Credit check', 'config' => array('action_on_exceed' => 'hold')),
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Notify credit', 'config' => array('title' => 'Credit hold', 'message' => 'Order #{{order_number}} held for credit review')),
			),
		),
		'grn_notify' => array(
			'name' => 'Goods Receipt Notify',
			'description' => 'Notify buyer and AP on GRN',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'grn.posted'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_notification', 'label' => 'Notify buyer', 'config' => array('title' => 'GRN posted', 'message' => 'Goods received for PO #{{po_number}}')),
			),
		),
		'order_confirmation' => array(
			'name' => 'Order Confirmation Email',
			'description' => 'Confirm to customer when order is placed',
			'trigger_type' => 'event',
			'trigger_config' => array('event_type' => 'order.placed'),
			'steps' => array(
				array('step_type' => 'action', 'action_type' => 'send_email', 'label' => 'Confirm email', 'config' => array('to' => '{{customer_email}}', 'subject' => 'Order Confirmed #{{order_number}}', 'body' => 'Thank you for your order!')),
				array('step_type' => 'action', 'action_type' => 'gl_journal', 'label' => 'Optional memo', 'config' => array('debit_account' => '1100', 'credit_account' => '4000', 'amount' => 0)),
			),
		),
	);
}

/**
 * Install a named workflow template for a site (idempotent by name).
 *
 * @return array{ok:bool,workflow_id?:int,created?:bool,error?:string}
 */
function epc_erp_automation_install_template(PDO $db, string $siteKey, string $templateId, int $createdBy = 0): array
{
	$templates = epc_erp_automation_workflow_templates();
	if (!isset($templates[$templateId])) {
		return array('ok' => false, 'error' => 'Unknown template: ' . $templateId);
	}
	require_once dirname(__DIR__, 2) . '/general_pages/epc_workflow_builder.php';
	epc_workflow_ensure_schema($db);

	$tpl = $templates[$templateId];
	$existing = epc_workflow_list($db, $siteKey);
	foreach ($existing as $w) {
		if (strcasecmp((string) ($w['name'] ?? ''), (string) $tpl['name']) === 0) {
			return array('ok' => true, 'workflow_id' => (int) $w['id'], 'created' => false);
		}
	}

	$data = $tpl;
	$data['active'] = 1;
	$data['created_by'] = $createdBy;
	$res = epc_workflow_create($db, $siteKey, $data);
	$res['created'] = !empty($res['ok']);
	return $res;
}

/**
 * Enable an automation and optionally install its workflow template.
 *
 * @return array{ok:bool,message:string,workflow_id?:int}
 */
function epc_erp_automation_activate(PDO $db, string $siteKey, string $id, int $userId = 0): array
{
	$cat = epc_erp_automation_catalogue();
	if (!isset($cat[$id])) {
		return array('ok' => false, 'message' => 'Unknown automation');
	}
	epc_erp_automation_set_enabled($db, $id, true);
	$wfId = 0;
	$tpl = (string) ($cat[$id]['workflow_template'] ?? '');
	if ($tpl !== '') {
		$inst = epc_erp_automation_install_template($db, $siteKey, $tpl, $userId);
		if (!empty($inst['ok'])) {
			$wfId = (int) ($inst['workflow_id'] ?? 0);
		}
	}
	return array('ok' => true, 'message' => 'Automation enabled', 'workflow_id' => $wfId);
}

/**
 * Run due scheduled workflows for a site (called from cron / platform job).
 *
 * @return array{ok:bool,ran:int,results:array}
 */
function epc_erp_automation_tick(PDO $db, string $siteKey): array
{
	require_once dirname(__DIR__, 2) . '/general_pages/epc_workflow_builder.php';
	epc_workflow_ensure_schema($db);
	$workflows = epc_workflow_list($db, $siteKey, array('active' => 1, 'trigger_type' => 'schedule'));
	$ran = 0;
	$results = array();
	foreach ($workflows as $w) {
		$cfg = is_array($w['trigger_config'] ?? null) ? $w['trigger_config'] : array();
		if (!epc_erp_automation_schedule_due($cfg, $w['last_run_at'] ?? null)) {
			continue;
		}
		$res = epc_workflow_execute($db, (int) $w['id'], array('source' => 'schedule_tick', 'site_key' => $siteKey));
		$results[] = array('workflow_id' => (int) $w['id'], 'name' => $w['name'], 'result' => $res);
		$ran++;
	}

	// Also advance collections/dunning when enabled
	if (epc_erp_automation_is_enabled($db, 'collections_dunning')) {
		$dunningFile = __DIR__ . '/epc_collections_dunning.php';
		if (is_file($dunningFile)) {
			require_once $dunningFile;
			if (function_exists('epc_dunning_process')) {
				try {
					$d = epc_dunning_process($db, $siteKey);
					$results[] = array('automation' => 'collections_dunning', 'result' => $d);
				} catch (Throwable $e) {
					$results[] = array('automation' => 'collections_dunning', 'error' => $e->getMessage());
				}
			}
		}
	}

	return array('ok' => true, 'ran' => $ran, 'results' => $results);
}

/**
 * Simple due check: if never run, or last run was before today for daily-ish crons.
 */
function epc_erp_automation_schedule_due(array $cfg, $lastRunAt): bool
{
	$cron = trim((string) ($cfg['cron_expression'] ?? '0 9 * * *'));
	$parts = preg_split('/\s+/', $cron);
	$hour = isset($parts[1]) && is_numeric($parts[1]) ? (int) $parts[1] : 9;
	$nowH = (int) date('G');
	if ($nowH < $hour) {
		return false;
	}
	if ($lastRunAt === null || $lastRunAt === '') {
		return true;
	}
	$last = strtotime((string) $lastRunAt);
	if ($last === false) {
		return true;
	}
	return date('Y-m-d', $last) !== date('Y-m-d');
}
