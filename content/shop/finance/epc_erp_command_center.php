<?php
/**
 * ERP Command Center — Module 0 KPI tiles, approval queue, role-based widgets.
 *
 * The first screen a user sees after ERP login. Fiori/Redwood-style launchpad
 * with live KPI tiles, pending approvals, and role-appropriate quick actions.
 */
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

/* ─────────────────── KPI Tiles ─────────────────── */

function epc_erp_cc_kpi_tiles(PDO $db, int $dateFrom = 0, int $dateTo = 0): array
{
	if ($dateTo <= 0) { $dateTo = time(); }
	if ($dateFrom <= 0) { $dateFrom = (int) strtotime(date('Y-m-01 00:00:00')); }

	$tiles = array();

	// Revenue tile
	$rev = epc_erp_cc_safe_query($db,
		'SELECT IFNULL(SUM(CASE WHEN `successfully_created` = 1 THEN `price_total_wt` - `price_total_wt_vat` ELSE 0 END), 0) AS val FROM `shop_orders` WHERE `time` >= ? AND `time` <= ?',
		array($dateFrom, $dateTo)
	);
	$tiles[] = array(
		'id'    => 'revenue',
		'label' => 'Revenue (ex. VAT)',
		'value' => number_format((float) $rev, 2),
		'icon'  => 'fa-line-chart',
		'color' => '#28a745',
		'unit'  => 'AED',
	);

	// Orders tile
	$orders = epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?',
		array($dateFrom, $dateTo)
	);
	$tiles[] = array(
		'id'    => 'orders',
		'label' => 'Orders',
		'value' => number_format((int) $orders),
		'icon'  => 'fa-shopping-cart',
		'color' => '#007bff',
		'unit'  => '',
	);

	// Accounts Receivable
	$ar = epc_erp_cc_safe_query($db,
		'SELECT IFNULL(SUM(CASE WHEN `income` = 1 THEN `amount` ELSE -`amount` END), 0) AS val FROM `shop_users_accounting` WHERE `active` = 1',
		array()
	);
	$tiles[] = array(
		'id'    => 'ar_balance',
		'label' => 'AR Balance',
		'value' => number_format((float) $ar, 2),
		'icon'  => 'fa-file-text-o',
		'color' => (float) $ar > 0 ? '#fd7e14' : '#28a745',
		'unit'  => 'AED',
	);

	// Accounts Payable
	$ap = epc_erp_cc_safe_query($db,
		'SELECT IFNULL(SUM(`balance`), 0) AS val FROM `epc_erp_suppliers` WHERE `active` = 1',
		array()
	);
	$tiles[] = array(
		'id'    => 'ap_balance',
		'label' => 'AP Balance',
		'value' => number_format(abs((float) $ap), 2),
		'icon'  => 'fa-credit-card',
		'color' => '#dc3545',
		'unit'  => 'AED',
	);

	// Cash & Bank
	$cash = epc_erp_cc_safe_query($db,
		'SELECT IFNULL(SUM(`balance`), 0) AS val FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1',
		array()
	);
	$tiles[] = array(
		'id'    => 'cash_bank',
		'label' => 'Cash & Bank',
		'value' => number_format((float) $cash, 2),
		'icon'  => 'fa-university',
		'color' => '#6f42c1',
		'unit'  => 'AED',
	);

	// VAT Net
	$vatOut = epc_erp_cc_safe_query($db,
		'SELECT IFNULL(SUM(`price_total_wt_vat`), 0) AS val FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?',
		array($dateFrom, $dateTo)
	);
	$vatIn = epc_erp_cc_safe_query($db,
		'SELECT IFNULL(SUM(`vat_amount`), 0) AS val FROM `epc_erp_purchases` WHERE `active` = 1 AND `purchase_date` >= ? AND `purchase_date` <= ?',
		array($dateFrom, $dateTo)
	);
	$vatNet = (float) $vatOut - (float) $vatIn;
	$tiles[] = array(
		'id'    => 'vat_net',
		'label' => 'VAT Net Payable',
		'value' => number_format(abs($vatNet), 2),
		'icon'  => 'fa-balance-scale',
		'color' => $vatNet >= 0 ? '#e83e8c' : '#20c997',
		'unit'  => 'AED',
	);

	// Period Status
	$periodStatus = 'open';
	try {
		require_once __DIR__ . '/epc_erp_period_close.php';
		$currentPeriod = epc_erp_period_get($db, date('Y-m'));
		$periodStatus = $currentPeriod['status'] ?? 'open';
	} catch (\Exception $e) { /* period close not deployed yet */ }

	$tiles[] = array(
		'id'    => 'period_status',
		'label' => 'Period ' . date('M Y'),
		'value' => ucfirst(str_replace('_', ' ', $periodStatus)),
		'icon'  => 'fa-calendar-check-o',
		'color' => $periodStatus === 'locked' ? '#dc3545' : ($periodStatus === 'soft_close' ? '#fd7e14' : '#28a745'),
		'unit'  => '',
	);

	// Inventory Items
	$invCount = epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_erp_items` WHERE `active` = 1',
		array()
	);
	$tiles[] = array(
		'id'    => 'inventory_items',
		'label' => 'Active Items',
		'value' => number_format((int) $invCount),
		'icon'  => 'fa-cubes',
		'color' => '#17a2b8',
		'unit'  => '',
	);

	return $tiles;
}

/* ─────────────────── Approval Queue ─────────────────── */

function epc_erp_cc_approval_queue(PDO $db): array
{
	$queue = array();

	// Draft Sales Orders needing confirmation
	$draftSO = (int) epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_erp_sales_orders` WHERE `status` = \'draft\'', array());
	if ($draftSO > 0) {
		$queue[] = array(
			'id'       => 'draft_so',
			'category' => 'Sales',
			'label'    => $draftSO . ' draft sales order' . ($draftSO > 1 ? 's' : '') . ' awaiting confirmation',
			'count'    => $draftSO,
			'action'   => 'Open Sales Orders',
			'link'     => '/erp/?area=sales&tab=sales_orders',
			'severity' => 'warning',
			'icon'     => 'fa-file-text',
		);
	}

	// Pending Purchase Orders
	$pendingPO = (int) epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_erp_purchase_orders` WHERE `status` IN (\'draft\', \'pending\')', array());
	if ($pendingPO > 0) {
		$queue[] = array(
			'id'       => 'pending_po',
			'category' => 'Procurement',
			'label'    => $pendingPO . ' purchase order' . ($pendingPO > 1 ? 's' : '') . ' pending approval',
			'count'    => $pendingPO,
			'action'   => 'Open Purchase Orders',
			'link'     => '/erp/?area=procurement&tab=purchase_orders',
			'severity' => 'warning',
			'icon'     => 'fa-truck',
		);
	}

	// Unposted GL Journals
	$unpostedGL = (int) epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_erp_gl_journals` WHERE `status` = \'draft\' AND `active` = 1', array());
	if ($unpostedGL > 0) {
		$queue[] = array(
			'id'       => 'unposted_gl',
			'category' => 'Finance',
			'label'    => $unpostedGL . ' unposted GL journal' . ($unpostedGL > 1 ? 's' : ''),
			'count'    => $unpostedGL,
			'action'   => 'Open General Ledger',
			'link'     => '/erp/?area=finance&tab=gl',
			'severity' => 'info',
			'icon'     => 'fa-book',
		);
	}

	// Overdue Invoices (30+ days)
	$overdue = (int) epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_erp_sales_invoices` WHERE `status` = \'unpaid\' AND `due_date` < ?',
		array(time() - 86400 * 30));
	if ($overdue > 0) {
		$queue[] = array(
			'id'       => 'overdue_invoices',
			'category' => 'Finance',
			'label'    => $overdue . ' overdue invoice' . ($overdue > 1 ? 's' : '') . ' (30+ days)',
			'count'    => $overdue,
			'action'   => 'Open Aging Report',
			'link'     => '/erp/?area=finance&tab=aging',
			'severity' => 'danger',
			'icon'     => 'fa-exclamation-triangle',
		);
	}

	// Low Stock Items
	$lowStock = (int) epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_erp_items` WHERE `active` = 1 AND `qty_on_hand` > 0 AND `qty_on_hand` <= `reorder_level`',
		array());
	if ($lowStock > 0) {
		$queue[] = array(
			'id'       => 'low_stock',
			'category' => 'Inventory',
			'label'    => $lowStock . ' item' . ($lowStock > 1 ? 's' : '') . ' at or below reorder level',
			'count'    => $lowStock,
			'action'   => 'Open Inventory',
			'link'     => '/erp/?area=inventory&tab=items',
			'severity' => 'warning',
			'icon'     => 'fa-archive',
		);
	}

	// Pending E-Invoices
	$pendingEinv = (int) epc_erp_cc_safe_query($db,
		'SELECT COUNT(*) AS val FROM `epc_einvoice_documents` WHERE `status` IN (\'draft\', \'queued\')', array());
	if ($pendingEinv > 0) {
		$queue[] = array(
			'id'       => 'pending_einvoice',
			'category' => 'Compliance',
			'label'    => $pendingEinv . ' e-invoice' . ($pendingEinv > 1 ? 's' : '') . ' pending submission',
			'count'    => $pendingEinv,
			'action'   => 'Open E-Invoicing',
			'link'     => '/erp/?area=finance&tab=einvoice',
			'severity' => 'info',
			'icon'     => 'fa-paper-plane',
		);
	}

	return $queue;
}

/* ─────────────────── Role-Based Widget Config ─────────────────── */

function epc_erp_cc_role_widgets(string $role = ''): array
{
	$widgets = array(
		'finance' => array(
			array('id' => 'revenue_chart', 'label' => 'Revenue Trend',       'size' => 'half', 'icon' => 'fa-line-chart'),
			array('id' => 'ar_aging',      'label' => 'AR Aging Summary',    'size' => 'half', 'icon' => 'fa-clock-o'),
			array('id' => 'cash_flow',     'label' => 'Cash Flow',           'size' => 'half', 'icon' => 'fa-exchange'),
			array('id' => 'vat_status',    'label' => 'VAT Return Status',   'size' => 'half', 'icon' => 'fa-balance-scale'),
			array('id' => 'period_close',  'label' => 'Period Close Status', 'size' => 'full', 'icon' => 'fa-calendar-check-o'),
		),
		'operations' => array(
			array('id' => 'order_pipeline', 'label' => 'Order Pipeline',     'size' => 'half', 'icon' => 'fa-shopping-cart'),
			array('id' => 'inventory_val',  'label' => 'Inventory Value',    'size' => 'half', 'icon' => 'fa-cubes'),
			array('id' => 'low_stock',      'label' => 'Low Stock Alerts',   'size' => 'half', 'icon' => 'fa-exclamation-triangle'),
			array('id' => 'po_status',      'label' => 'PO Status',          'size' => 'half', 'icon' => 'fa-truck'),
		),
		'executive' => array(
			array('id' => 'pl_summary',     'label' => 'P&L Summary',        'size' => 'half', 'icon' => 'fa-bar-chart'),
			array('id' => 'revenue_chart',  'label' => 'Revenue Trend',      'size' => 'half', 'icon' => 'fa-line-chart'),
			array('id' => 'cash_flow',      'label' => 'Cash Position',      'size' => 'half', 'icon' => 'fa-university'),
			array('id' => 'compliance',     'label' => 'Compliance Status',  'size' => 'half', 'icon' => 'fa-shield'),
		),
		'sales' => array(
			array('id' => 'order_pipeline', 'label' => 'Order Pipeline',     'size' => 'half', 'icon' => 'fa-shopping-cart'),
			array('id' => 'so_status',      'label' => 'Sales Orders',       'size' => 'half', 'icon' => 'fa-file-text'),
			array('id' => 'ar_aging',       'label' => 'Customer Aging',     'size' => 'half', 'icon' => 'fa-clock-o'),
			array('id' => 'revenue_chart',  'label' => 'Sales Trend',        'size' => 'half', 'icon' => 'fa-line-chart'),
		),
	);

	// Map ERP roles to widget sets
	$roleMap = array(
		'super_admin'        => 'executive',
		'finance_admin'      => 'finance',
		'finance_controller' => 'finance',
		'finance_user'       => 'finance',
		'operations_admin'   => 'operations',
		'warehouse_user'     => 'operations',
		'sales_admin'        => 'sales',
		'sales_user'         => 'sales',
	);

	$widgetSet = $roleMap[$role] ?? 'executive';
	return array(
		'role'    => $role,
		'layout'  => $widgetSet,
		'widgets' => $widgets[$widgetSet] ?? $widgets['executive'],
	);
}

/* ─────────────────── Quick Actions ─────────────────── */

function epc_erp_cc_quick_actions(string $role = ''): array
{
	$actions = array(
		array('id' => 'new_invoice',  'label' => 'New Invoice',       'icon' => 'fa-plus-circle',   'link' => '/erp/?area=sales&tab=invoices&action=create', 'roles' => array('super_admin', 'finance_admin', 'finance_user', 'sales_admin')),
		array('id' => 'new_payment',  'label' => 'Record Payment',    'icon' => 'fa-money',         'link' => '/erp/?area=finance&tab=cash_bank&action=entry', 'roles' => array('super_admin', 'finance_admin', 'finance_user')),
		array('id' => 'new_po',       'label' => 'New Purchase Order', 'icon' => 'fa-cart-plus',    'link' => '/erp/?area=procurement&tab=purchase_orders&action=create', 'roles' => array('super_admin', 'finance_admin', 'operations_admin')),
		array('id' => 'new_journal',  'label' => 'GL Journal Entry',  'icon' => 'fa-pencil-square', 'link' => '/erp/?area=finance&tab=gl&action=manual', 'roles' => array('super_admin', 'finance_admin', 'finance_controller')),
		array('id' => 'vat_return',   'label' => 'VAT Return',        'icon' => 'fa-balance-scale', 'link' => '/erp/?area=finance&tab=vat_return', 'roles' => array('super_admin', 'finance_admin')),
		array('id' => 'aging_report', 'label' => 'Aging Report',      'icon' => 'fa-clock-o',       'link' => '/erp/?area=finance&tab=aging', 'roles' => array('super_admin', 'finance_admin', 'finance_user', 'sales_admin')),
		array('id' => 'inv_movement', 'label' => 'Inventory Movement', 'icon' => 'fa-exchange',     'link' => '/erp/?area=inventory&tab=movements&action=create', 'roles' => array('super_admin', 'operations_admin', 'warehouse_user')),
		array('id' => 'einvoice',     'label' => 'E-Invoice',         'icon' => 'fa-paper-plane',   'link' => '/erp/?area=finance&tab=einvoice', 'roles' => array('super_admin', 'finance_admin')),
	);

	if ($role === '') {
		return $actions;
	}

	return array_values(array_filter($actions, function ($a) use ($role) {
		return in_array($role, $a['roles'], true);
	}));
}

/* ─────────────────── Full Command Center Data ─────────────────── */

function epc_erp_command_center(PDO $db, string $role = '', int $dateFrom = 0, int $dateTo = 0): array
{
	return array(
		'kpi_tiles'     => epc_erp_cc_kpi_tiles($db, $dateFrom, $dateTo),
		'approval_queue' => epc_erp_cc_approval_queue($db),
		'widgets'       => epc_erp_cc_role_widgets($role),
		'quick_actions' => epc_erp_cc_quick_actions($role),
		'period'        => date('Y-m'),
		'generated_at'  => date('c'),
	);
}

/* ─────────────────── Helper ─────────────────── */

function epc_erp_cc_safe_query(PDO $db, string $sql, array $params): string
{
	try {
		$st = $db->prepare($sql);
		$st->execute($params);
		$row = $st->fetch(PDO::FETCH_ASSOC);
		return (string) ($row['val'] ?? '0');
	} catch (\PDOException $e) {
		return '0';
	}
}
