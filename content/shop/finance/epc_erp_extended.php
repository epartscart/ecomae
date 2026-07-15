<?php
/**
 * ERP Phases 9–12 — PO workflow, treasury stubs, agenda, KB, multi-entity placeholder.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';

function epc_erp_extended_ensure_schema(PDO $db)
{
	// Called from the top of nearly every function in this file (PO save/list/status,
	// three-way match, payment batches...), so a single Purchase orders page load could
	// re-run this whole body — a dozen CREATE TABLE IF NOT EXISTS + column-add checks —
	// many times over. The schema can't change mid-request, so guard it like the CRM
	// and dimensions modules already do.
	static $doneForConnection = null;
	if ($doneForConnection === $db) {
		return;
	}
	$doneForConnection = $db;

	require_once __DIR__ . '/epc_erp_phase8.php';
	epc_erp_phase8_ensure_schema($db);

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_purchase_orders` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`po_no` varchar(32) NOT NULL,
		`supplier_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL DEFAULT '',
		`amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`status` enum('draft','approved','partial','received','cancelled') NOT NULL DEFAULT 'draft',
		`purchase_id` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`approved_at` int(11) NOT NULL DEFAULT 0,
		`received_at` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_po_no` (`po_no`),
		KEY `x_supplier` (`supplier_id`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase orders with approval workflow';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_po_receipts` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`po_id` int(11) NOT NULL,
		`receipt_no` varchar(32) NOT NULL,
		`qty_received` decimal(14,3) NOT NULL DEFAULT 0.000,
		`purchase_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_po` (`po_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Goods receipt lines for 3-way match';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_payment_batches` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`batch_no` varchar(32) NOT NULL,
		`batch_type` enum('sepa','local','cheque') NOT NULL DEFAULT 'sepa',
		`account_id` int(11) NOT NULL DEFAULT 0,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`line_count` int(11) NOT NULL DEFAULT 0,
		`status` enum('draft','submitted','processed','cancelled') NOT NULL DEFAULT 'draft',
		`execution_date` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_batch_no` (`batch_no`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Outbound payment batches (SEPA stub)';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_petty_cash` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`name` varchar(128) NOT NULL,
		`account_id` int(11) NOT NULL DEFAULT 0,
		`float_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`custodian_user_id` int(11) NOT NULL DEFAULT 0,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Petty cash floats';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_agenda_events` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`title` varchar(255) NOT NULL,
		`event_type` varchar(32) NOT NULL DEFAULT 'meeting',
		`start_at` int(11) NOT NULL DEFAULT 0,
		`end_at` int(11) NOT NULL DEFAULT 0,
		`all_day` tinyint(1) NOT NULL DEFAULT 0,
		`entity_type` varchar(32) DEFAULT NULL,
		`entity_id` int(11) NOT NULL DEFAULT 0,
		`assigned_user_id` int(11) NOT NULL DEFAULT 0,
		`location` varchar(255) DEFAULT NULL,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_start` (`start_at`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Shared ERP agenda / calendar';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_notifications` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`user_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL,
		`body` varchar(512) DEFAULT NULL,
		`link_tab` varchar(64) DEFAULT NULL,
		`is_read` tinyint(1) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_user` (`user_id`,`is_read`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP notification centre stub';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_kb_articles` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`slug` varchar(128) NOT NULL,
		`title` varchar(255) NOT NULL,
		`category` varchar(64) NOT NULL DEFAULT 'general',
		`summary` text,
		`body_html` mediumtext,
		`published` tinyint(1) NOT NULL DEFAULT 1,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_slug` (`slug`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Internal knowledge base';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_platform_settings` (
		`setting_key` varchar(64) NOT NULL,
		`setting_value` text,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`setting_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tenant ERP platform toggles';");

	epc_erp_schema_add_column_if_missing($db, 'epc_crm_quotes', 'quote_kind', "varchar(16) NOT NULL DEFAULT 'quote'");
	epc_erp_schema_add_column_if_missing($db, 'epc_crm_quotes', 'subtotal', 'decimal(14,2) NOT NULL DEFAULT 0.00');
}

function epc_erp_po_list(PDO $db, $status = '', $limit = 100)
{
	epc_erp_extended_ensure_schema($db);
	$sql = 'SELECT p.*, s.`name` AS supplier_name FROM `epc_erp_purchase_orders` p
		LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id` WHERE 1=1';
	$params = array();
	if ($status !== '') {
		$sql .= ' AND p.`status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY p.`time_updated` DESC LIMIT ' . max(50, min(2000, (int) $limit));
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_po_save(PDO $db, array $data)
{
	epc_erp_extended_ensure_schema($db);
	$id = (int) ($data['id'] ?? 0);
	$now = time();
	$supplierId = (int) ($data['supplier_id'] ?? 0);
	$title = trim((string) ($data['title'] ?? ''));
	$amountEx = round((float) ($data['amount_ex_vat'] ?? 0), 2);
	if ($title === '' || $supplierId <= 0) {
		throw new Exception('Supplier and title are required');
	}
	require_once __DIR__ . '/epc_tax_toolkit.php';
	$taxCalc = epc_tax_toolkit_calc_amounts($db, $amountEx, 0, (int) ($data['contact_id'] ?? 0));
	$vat = $taxCalc['vat_amount'];
	$total = $taxCalc['total_amount'];
	$status = in_array($data['status'] ?? '', array('draft', 'approved', 'partial', 'received', 'cancelled'), true)
		? $data['status'] : 'draft';
	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_erp_purchase_orders` SET `supplier_id`=?, `title`=?, `amount_ex_vat`=?, `vat_amount`=?, `total_amount`=?, `status`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array($supplierId, $title, $amountEx, $vat, $total, $status, trim((string) ($data['notes'] ?? '')), $now, $id));
		epc_erp_po_pf_sync($db, $id);
		return $id;
	}
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	$poNo = epc_erp_next_voucher_no($db, 'PO');
	$db->prepare(
		'INSERT INTO `epc_erp_purchase_orders` (`po_no`, `voucher_no`, `supplier_id`, `title`, `amount_ex_vat`, `vat_amount`, `total_amount`, `status`, `notes`, `admin_id`, `time_created`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array($poNo, $poNo, $supplierId, $title, $amountEx, $vat, $total, 'draft', trim((string) ($data['notes'] ?? '')), epc_erp_admin_id(), $now, $now));
	$newPoId = (int) $db->lastInsertId();
	epc_erp_po_pf_sync($db, $newPoId);
	return $newPoId;
}

/** Best-effort: keep the procurement process-flow case in step with a PO. Never throws. */
function epc_erp_po_pf_sync(PDO $db, int $poId): void
{
	try {
		$pf = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
		if ($poId > 0 && is_file($pf)) {
			require_once $pf;
			if (function_exists('epc_pf_sync_po_case')) { epc_pf_sync_po_case($db, $poId); }
		}
	} catch (Exception $e) {
	}
}

function epc_erp_po_set_status(PDO $db, $poId, $status)
{
	epc_erp_extended_ensure_schema($db);
	$poId = (int) $poId;
	$allowed = array('draft', 'approved', 'partial', 'received', 'cancelled');
	if (!in_array($status, $allowed, true)) {
		throw new Exception('Invalid PO status');
	}
	$now = time();
	$extra = '';
	if ($status === 'approved') {
		$extra = ', `approved_at` = ' . $now;
	}
	if ($status === 'received') {
		$extra = ', `received_at` = ' . $now;
	}
	$db->exec('UPDATE `epc_erp_purchase_orders` SET `status` = ' . $db->quote($status) . ', `time_updated` = ' . $now . $extra . ' WHERE `id` = ' . $poId);
	// best-effort: keep the procurement process-flow case in step with the PO status
	try {
		$pf = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_processflow.php';
		if (is_file($pf)) {
			require_once $pf;
			if (function_exists('epc_pf_sync_po_case')) { epc_pf_sync_po_case($db, $poId); }
		}
	} catch (Exception $e) {
	}
	return true;
}

function epc_erp_three_way_match_rows(PDO $db, $limit = 50)
{
	epc_erp_extended_ensure_schema($db);
	$sql = 'SELECT po.`id` AS po_id, po.`po_no`, po.`status` AS po_status, po.`total_amount` AS po_total,
		p.`id` AS purchase_id, p.`invoice_number`, p.`total_amount` AS invoice_total, p.`status` AS purchase_status,
		(SELECT COUNT(*) FROM `epc_erp_po_receipts` r WHERE r.`po_id` = po.`id`) AS receipt_count
		FROM `epc_erp_purchase_orders` po
		LEFT JOIN `epc_erp_purchases` p ON p.`id` = po.`purchase_id` OR (po.`order_id` > 0 AND p.`order_id` = po.`order_id`)
		WHERE po.`status` IN (\'approved\', \'partial\', \'received\')
		ORDER BY po.`id` DESC LIMIT ' . (int) $limit;
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_payment_batches_list(PDO $db, $limit = 50)
{
	epc_erp_extended_ensure_schema($db);
	$st = $db->query('SELECT b.*, a.`name` AS account_name FROM `epc_erp_payment_batches` b
		LEFT JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = b.`account_id`
		ORDER BY b.`time_updated` DESC LIMIT ' . (int) $limit);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_payment_batch_save(PDO $db, array $data)
{
	epc_erp_extended_ensure_schema($db);
	$now = time();
	$batchNo = epc_erp_phase8_next_no($db, 'PAY-', 'epc_erp_payment_batches', 'batch_no');
	$total = round((float) ($data['total_amount'] ?? 0), 2);
	$db->prepare(
		'INSERT INTO `epc_erp_payment_batches` (`batch_no`, `batch_type`, `account_id`, `total_amount`, `line_count`, `status`, `execution_date`, `notes`, `admin_id`, `time_created`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$batchNo,
		in_array($data['batch_type'] ?? '', array('sepa', 'local', 'cheque'), true) ? $data['batch_type'] : 'sepa',
		(int) ($data['account_id'] ?? 0),
		$total,
		max(0, (int) ($data['line_count'] ?? 1)),
		'draft',
		!empty($data['execution_date']) ? strtotime($data['execution_date'] . ' 12:00:00') : $now,
		trim((string) ($data['notes'] ?? '')),
		epc_erp_admin_id(),
		$now,
		$now,
	));
	$batchId = (int) $db->lastInsertId();
	if (function_exists('epc_pf_sync_pay_case')) {
		try { epc_pf_sync_pay_case($db, $batchId); } catch (Exception $e) {}
	}
	return $batchId;
}

function epc_erp_petty_cash_list(PDO $db)
{
	epc_erp_extended_ensure_schema($db);
	return $db->query(
		'SELECT pc.*, a.`name` AS account_name, a.`opening_balance` AS account_balance
		 FROM `epc_erp_petty_cash` pc
		 LEFT JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = pc.`account_id`
		 WHERE pc.`active` = 1 ORDER BY pc.`name`'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_petty_cash_save(PDO $db, array $data)
{
	epc_erp_extended_ensure_schema($db);
	$name = trim((string) ($data['name'] ?? ''));
	if ($name === '') {
		throw new Exception('Petty cash name required');
	}
	$accountId = (int) ($data['account_id'] ?? 0);
	if ($accountId <= 0) {
		$accountId = epc_erp_create_cash_account($db, array(
			'name' => $name,
			'account_type' => 'cash',
			'opening_balance' => (float) ($data['float_amount'] ?? 0),
		));
	}
	$db->prepare(
		'INSERT INTO `epc_erp_petty_cash` (`name`, `account_id`, `float_amount`, `custodian_user_id`, `time_created`)
		 VALUES (?,?,?,?,?)'
	)->execute(array($name, $accountId, round((float) ($data['float_amount'] ?? 0), 2), (int) ($data['custodian_user_id'] ?? 0), time()));
	return (int) $db->lastInsertId();
}

function epc_erp_agenda_list(PDO $db, $monthYm = '', $limit = 200)
{
	epc_erp_extended_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_erp_agenda_events` WHERE 1=1';
	$params = array();
	if ($monthYm !== '' && preg_match('/^\d{4}-\d{2}$/', $monthYm)) {
		$start = strtotime($monthYm . '-01 00:00:00');
		$end = strtotime(date('Y-m-t 23:59:59', $start));
		$sql .= ' AND `start_at` >= ? AND `start_at` <= ?';
		$params = array($start, $end);
	}
	$sql .= ' ORDER BY `start_at` ASC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_agenda_save(PDO $db, array $data)
{
	epc_erp_extended_ensure_schema($db);
	$title = trim((string) ($data['title'] ?? ''));
	if ($title === '') {
		throw new Exception('Event title required');
	}
	$start = !empty($data['start_at']) ? strtotime($data['start_at']) : time();
	$end = !empty($data['end_at']) ? strtotime($data['end_at']) : ($start + 3600);
	$db->prepare(
		'INSERT INTO `epc_erp_agenda_events` (`title`, `event_type`, `start_at`, `end_at`, `entity_type`, `entity_id`, `assigned_user_id`, `location`, `notes`, `admin_id`, `time_created`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$title,
		mb_substr(trim((string) ($data['event_type'] ?? 'meeting')), 0, 32),
		$start,
		$end,
		mb_substr(trim((string) ($data['entity_type'] ?? '')), 0, 32),
		(int) ($data['entity_id'] ?? 0),
		(int) ($data['assigned_user_id'] ?? 0),
		trim((string) ($data['location'] ?? '')),
		trim((string) ($data['notes'] ?? '')),
		epc_erp_admin_id(),
		time(),
	));
	return (int) $db->lastInsertId();
}

function epc_erp_notifications_list(PDO $db, $limit = 20)
{
	epc_erp_extended_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_notifications` ORDER BY `time_created` DESC LIMIT ' . (int) $limit);
	$st->execute();
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_notifications_unread_count(PDO $db)
{
	epc_erp_extended_ensure_schema($db);
	return (int) $db->query('SELECT COUNT(*) FROM `epc_erp_notifications` WHERE `is_read` = 0')->fetchColumn();
}

function epc_erp_notification_seed(PDO $db, $title, $body, $linkTab = '')
{
	epc_erp_extended_ensure_schema($db);
	$db->prepare(
		'INSERT INTO `epc_erp_notifications` (`user_id`, `title`, `body`, `link_tab`, `time_created`) VALUES (0,?,?,?,?)'
	)->execute(array($title, $body, $linkTab, time()));
}

function epc_erp_kb_list(PDO $db, $category = '', $limit = 100)
{
	epc_erp_extended_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_erp_kb_articles` WHERE `published` = 1';
	$params = array();
	if ($category !== '') {
		$sql .= ' AND `category` = ?';
		$params[] = $category;
	}
	$sql .= ' ORDER BY `title` ASC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_kb_save(PDO $db, array $data)
{
	epc_erp_extended_ensure_schema($db);
	$title = trim((string) ($data['title'] ?? ''));
	if ($title === '') {
		throw new Exception('Article title required');
	}
	$slug = preg_replace('/[^a-z0-9-]+/', '-', strtolower($title));
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_kb_articles` (`slug`, `title`, `category`, `summary`, `body_html`, `admin_id`, `time_created`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?)'
	)->execute(array(
		$slug,
		$title,
		mb_substr(trim((string) ($data['category'] ?? 'general')), 0, 64),
		trim((string) ($data['summary'] ?? '')),
		trim((string) ($data['body_html'] ?? '')),
		epc_erp_admin_id(),
		$now,
		$now,
	));
	return (int) $db->lastInsertId();
}

function epc_erp_kb_seed_defaults(PDO $db)
{
	$articles = array(
		array('Month-end close', 'finance', 'Close AR/AP, VAT, and GL before period lock.'),
		array('Fulfilment pipeline', 'operations', 'Payment → stock → delivery → returns.'),
		array('CRM inside ERP', 'sales', 'Leads, opportunities, and quotes live under Sales → CRM tab only.'),
	);
	foreach ($articles as $a) {
		$chk = $db->prepare('SELECT `id` FROM `epc_erp_kb_articles` WHERE `slug` = ? LIMIT 1');
		$slug = preg_replace('/[^a-z0-9-]+/', '-', strtolower($a[0]));
		$chk->execute(array($slug));
		if ($chk->fetchColumn()) {
			continue;
		}
		epc_erp_kb_save($db, array('title' => $a[0], 'category' => $a[1], 'summary' => $a[2], 'body_html' => '<p>' . htmlspecialchars($a[2]) . '</p>'));
	}
}

function epc_erp_multi_entity_enabled(PDO $db)
{
	epc_erp_extended_ensure_schema($db);
	$st = $db->prepare('SELECT `setting_value` FROM `epc_erp_platform_settings` WHERE `setting_key` = ? LIMIT 1');
	$st->execute(array('multi_entity_enabled'));
	return (string) $st->fetchColumn() === '1';
}

function epc_erp_multi_entity_set(PDO $db, $enabled)
{
	epc_erp_extended_ensure_schema($db);
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_platform_settings` (`setting_key`, `setting_value`, `time_updated`) VALUES (?,?,?)
		 ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `time_updated` = VALUES(`time_updated`)'
	)->execute(array('multi_entity_enabled', $enabled ? '1' : '0', $now));
}

/** Generic tenant-scoped key/value setting read (epc_erp_platform_settings). */
function epc_erp_platform_setting_get(PDO $db, $key, $default = '')
{
	epc_erp_extended_ensure_schema($db);
	$st = $db->prepare('SELECT `setting_value` FROM `epc_erp_platform_settings` WHERE `setting_key` = ? LIMIT 1');
	$st->execute(array((string) $key));
	$v = $st->fetchColumn();
	return ($v === false || $v === null) ? $default : (string) $v;
}

/** Generic tenant-scoped key/value setting write (epc_erp_platform_settings). */
function epc_erp_platform_setting_set(PDO $db, $key, $value)
{
	epc_erp_extended_ensure_schema($db);
	$db->prepare(
		'INSERT INTO `epc_erp_platform_settings` (`setting_key`, `setting_value`, `time_updated`) VALUES (?,?,?)
		 ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `time_updated` = VALUES(`time_updated`)'
	)->execute(array((string) $key, (string) $value, time()));
}

function epc_erp_proposals_list(PDO $db, $kind = '', $limit = 100)
{
	epc_erp_extended_ensure_schema($db);
	if (!$db->query("SHOW TABLES LIKE 'epc_crm_quotes'")->fetch()) {
		return array();
	}
	$sql = 'SELECT q.*, o.`title` AS opp_title FROM `epc_crm_quotes` q
		LEFT JOIN `epc_crm_opportunities` o ON o.`id` = q.`opportunity_id`
		WHERE q.`active` = 1';
	$params = array();
	if ($kind !== '') {
		$sql .= ' AND q.`quote_kind` = ?';
		$params[] = $kind;
	}
	$sql .= ' ORDER BY q.`time_updated` DESC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_bom_from_inventory(PDO $db, $limit = 100)
{
	require_once __DIR__ . '/epc_erp_inventory.php';
	epc_erp_inventory_ensure_schema($db);
	$sql = 'SELECT i.`id`, i.`sku`, i.`name`, i.`unit`, i.`reorder_level`,
		(SELECT SUM(s.`qty_on_hand`) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty_on_hand
		FROM `epc_erp_inv_items` i WHERE i.`active` = 1 ORDER BY i.`sku` LIMIT ' . (int) $limit;
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}
