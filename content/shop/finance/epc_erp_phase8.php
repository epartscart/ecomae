<?php
/**
 * ERP Phase 8 — contacts, RFQ, delivery notes, bank recon, reports export, expenses.
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_phase8_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_contacts` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`party_type` enum('customer','supplier','both','staff','other') NOT NULL DEFAULT 'customer',
		`name` varchar(255) NOT NULL,
		`company` varchar(255) DEFAULT NULL,
		`email` varchar(255) DEFAULT NULL,
		`phone` varchar(64) DEFAULT NULL,
		`trn` varchar(64) DEFAULT NULL,
		`address` text,
		`city` varchar(128) DEFAULT NULL,
		`country_code` varchar(8) NOT NULL DEFAULT 'AE',
		`linked_user_id` int(11) NOT NULL DEFAULT 0,
		`linked_supplier_id` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_party` (`party_type`,`active`),
		KEY `x_user` (`linked_user_id`),
		KEY `x_supplier` (`linked_supplier_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP unified contacts / third parties';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_rfq` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`rfq_no` varchar(32) NOT NULL,
		`supplier_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL,
		`description` text,
		`amount_est` decimal(14,2) NOT NULL DEFAULT 0.00,
		`currency_code` varchar(8) NOT NULL DEFAULT 'AED',
		`status` enum('draft','sent','quoted','accepted','rejected','cancelled') NOT NULL DEFAULT 'draft',
		`due_date` int(11) NOT NULL DEFAULT 0,
		`order_id` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_rfq_no` (`rfq_no`),
		KEY `x_supplier` (`supplier_id`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier RFQ / proposals';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_delivery_notes` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`note_no` varchar(32) NOT NULL,
		`order_id` int(11) NOT NULL,
		`carrier` varchar(128) DEFAULT NULL,
		`tracking_no` varchar(128) DEFAULT NULL,
		`shipped_at` int(11) NOT NULL DEFAULT 0,
		`delivered_at` int(11) NOT NULL DEFAULT 0,
		`status` enum('draft','shipped','delivered','cancelled') NOT NULL DEFAULT 'draft',
		`pdf_path` varchar(512) DEFAULT NULL,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_note_no` (`note_no`),
		KEY `x_order` (`order_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP delivery notes / shipments';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_bank_statement_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`account_id` int(11) NOT NULL,
		`line_date` int(11) NOT NULL,
		`description` varchar(255) DEFAULT NULL,
		`reference` varchar(128) DEFAULT NULL,
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`direction` tinyint(1) NOT NULL COMMENT '1=credit in, 0=debit out',
		`matched_entry_id` int(11) NOT NULL DEFAULT 0,
		`import_batch` varchar(64) DEFAULT NULL,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_account` (`account_id`,`matched_entry_id`),
		KEY `x_date` (`line_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Bank statement lines for reconciliation';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_expense_reports` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`report_no` varchar(32) NOT NULL,
		`staff_user_id` int(11) NOT NULL DEFAULT 0,
		`title` varchar(255) NOT NULL,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`status` enum('draft','submitted','approved','paid','rejected') NOT NULL DEFAULT 'draft',
		`period_from` int(11) NOT NULL DEFAULT 0,
		`period_to` int(11) NOT NULL DEFAULT 0,
		`notes` text,
		`cash_entry_id` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_report_no` (`report_no`),
		KEY `x_staff` (`staff_user_id`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Staff expense reports';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_documents` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`entity_type` varchar(32) NOT NULL,
		`entity_id` int(11) NOT NULL DEFAULT 0,
		`doc_category` varchar(64) NOT NULL DEFAULT 'general',
		`file_name` varchar(255) NOT NULL,
		`file_path` varchar(512) NOT NULL,
		`file_size` int(11) NOT NULL DEFAULT 0,
		`mime_type` varchar(128) DEFAULT NULL,
		`notes` text,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_entity` (`entity_type`, `entity_id`),
		KEY `x_cat` (`doc_category`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP document attachments (ECM)';");

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_documents', 'version_note', 'varchar(255) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_documents', 'active', 'tinyint(1) NOT NULL DEFAULT 1');

	epc_erp_schema_add_column_if_missing($db, 'epc_erp_contacts', 'currency_code', "varchar(8) NOT NULL DEFAULT 'AED'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'currency_code', "varchar(8) NOT NULL DEFAULT 'AED'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchases', 'fx_rate', 'decimal(12,6) NOT NULL DEFAULT 1.000000');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'currency_code', "varchar(8) NOT NULL DEFAULT 'AED'");
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'fx_rate', 'decimal(12,6) NOT NULL DEFAULT 1.000000');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_cash_bank_entries', 'reconciled', 'tinyint(1) NOT NULL DEFAULT 0');
	epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'reorder_level', 'decimal(14,3) NOT NULL DEFAULT 0.000');
}

function epc_erp_documents_list(PDO $db, $entityType = '', $entityId = 0, $limit = 100, $category = '', $dateFrom = 0, $dateTo = 0)
{
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_erp_documents` WHERE `active` = 1';
	$params = array();
	if ($entityType !== '') {
		$sql .= ' AND `entity_type` = ?';
		$params[] = $entityType;
	}
	if ($entityId > 0) {
		$sql .= ' AND `entity_id` = ?';
		$params[] = $entityId;
	}
	if ($category !== '') {
		$sql .= ' AND `doc_category` = ?';
		$params[] = $category;
	}
	if ($dateFrom > 0) {
		$sql .= ' AND `time_created` >= ?';
		$params[] = $dateFrom;
	}
	if ($dateTo > 0) {
		$sql .= ' AND `time_created` <= ?';
		$params[] = $dateTo;
	}
	$sql .= ' ORDER BY `time_created` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_document_upload(PDO $db, array $data, array $file)
{
	epc_erp_phase8_ensure_schema($db);
	$entityType = mb_substr(trim((string)($data['entity_type'] ?? 'purchase')), 0, 32);
	$entityId = (int)($data['entity_id'] ?? 0);
	$category = mb_substr(trim((string)($data['doc_category'] ?? 'general')), 0, 64);
	if (empty($file['tmp_name']) && !empty($_FILES['file']['tmp_name'])) {
		$file = $_FILES['file'];
	}
	if (empty($file['tmp_name']) || !is_uploaded_file($file['tmp_name'])) {
		throw new Exception('No file uploaded');
	}
	$orig = basename((string)($file['name'] ?? 'file'));
	$safe = preg_replace('/[^a-zA-Z0-9._-]/', '_', $orig);
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_erp_documents';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	$rel = '/content/files/epc_erp_documents/' . date('Ymd') . '_' . uniqid('', true) . '_' . $safe;
	$full = $_SERVER['DOCUMENT_ROOT'] . $rel;
	if (!move_uploaded_file($file['tmp_name'], $full)) {
		throw new Exception('Could not save file');
	}
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_documents` (`entity_type`, `entity_id`, `doc_category`, `file_name`, `file_path`, `file_size`, `mime_type`, `notes`, `version_note`, `admin_id`, `time_created`, `active`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?,1)'
	)->execute(array(
		$entityType,
		$entityId,
		$category,
		$orig,
		$rel,
		(int)($file['size'] ?? 0),
		(string)($file['type'] ?? ''),
		trim((string)($data['notes'] ?? '')),
		mb_substr(trim((string)($data['version_note'] ?? '')), 0, 255),
		epc_erp_admin_id(),
		$now,
	));
	$id = (int)$db->lastInsertId();
	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'document_upload', $entityType, $entityId, 'Uploaded ' . $orig, array('doc_id' => $id));
	return $id;
}

function epc_erp_document_delete(PDO $db, int $docId, bool $canDelete = true)
{
	epc_erp_phase8_ensure_schema($db);
	if (!$canDelete) {
		throw new Exception('You do not have permission to delete documents');
	}
	$st = $db->prepare('SELECT * FROM `epc_erp_documents` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($docId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Document not found');
	}
	$db->prepare('UPDATE `epc_erp_documents` SET `active` = 0 WHERE `id` = ?')->execute(array($docId));
	$path = $_SERVER['DOCUMENT_ROOT'] . (string)$row['file_path'];
	if (is_file($path)) {
		@unlink($path);
	}
	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'document_delete', $row['entity_type'], (int)$row['entity_id'], 'Deleted ' . $row['file_name'], array('doc_id' => $docId));
	return true;
}

function epc_erp_mark_entry_reconciled(PDO $db, $entryId, $reconciled = 1)
{
	epc_erp_phase8_ensure_schema($db);
	$db->prepare('UPDATE `epc_erp_cash_bank_entries` SET `reconciled` = ? WHERE `id` = ? AND `active` = 1')
		->execute(array($reconciled ? 1 : 0, (int)$entryId));
	require_once __DIR__ . '/epc_erp_audit.php';
	epc_erp_audit_log($db, 'bank_reconcile', 'cash_entry', (int)$entryId, $reconciled ? 'Marked reconciled' : 'Unmarked reconciled');
	return true;
}

function epc_erp_phase8_next_no(PDO $db, $prefix, $table, $col)
{
	$map = array('PO-' => 'PO', 'SO-' => 'SO', 'PI-' => 'PI', 'SI-' => 'SI', 'RV-' => 'RV', 'PV-' => 'PV', 'GV-' => 'GV', 'TV-' => 'TV');
	if (isset($map[$prefix])) {
		require_once __DIR__ . '/epc_erp_vouchers.php';
		return epc_erp_next_voucher_no($db, $map[$prefix]);
	}
	$st = $db->query('SELECT `' . $col . '` FROM `' . $table . '` ORDER BY `id` DESC LIMIT 1');
	$last = (string)$st->fetchColumn();
	$n = 1;
	if ($last !== '' && preg_match('/(\d+)$/', $last, $m)) {
		$n = (int)$m[1] + 1;
	}
	return $prefix . str_pad((string)$n, 5, '0', STR_PAD_LEFT);
}

function epc_erp_contacts_list(PDO $db, $partyType = '', $q = '', $limit = 200)
{
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT c.*, u.`email` AS user_email, s.`name` AS supplier_name
		FROM `epc_erp_contacts` c
		LEFT JOIN `users` u ON u.`user_id` = c.`linked_user_id`
		LEFT JOIN `epc_erp_suppliers` s ON s.`id` = c.`linked_supplier_id`
		WHERE c.`active` = 1';
	$params = array();
	if ($partyType !== '') {
		$sql .= ' AND (c.`party_type` = ? OR c.`party_type` = \'both\')';
		$params[] = $partyType;
	}
	if ($q !== '') {
		$sql .= ' AND (c.`name` LIKE ? OR c.`email` LIKE ? OR c.`company` LIKE ? OR c.`phone` LIKE ?)';
		$like = '%' . $q . '%';
		$params = array_merge($params, array($like, $like, $like, $like));
	}
	$sql .= ' ORDER BY c.`name` ASC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_contact_save(PDO $db, array $data)
{
	epc_erp_phase8_ensure_schema($db);
	$id = (int)($data['id'] ?? 0);
	$now = time();
	$fields = array(
		'party_type' => in_array($data['party_type'] ?? '', array('customer', 'supplier', 'both', 'staff', 'other'), true)
			? $data['party_type'] : 'customer',
		'name' => trim((string)($data['name'] ?? '')),
		'company' => trim((string)($data['company'] ?? '')),
		'email' => trim((string)($data['email'] ?? '')),
		'phone' => trim((string)($data['phone'] ?? '')),
		'trn' => trim((string)($data['trn'] ?? '')),
		'address' => trim((string)($data['address'] ?? '')),
		'city' => trim((string)($data['city'] ?? '')),
		'country_code' => strtoupper(substr(trim((string)($data['country_code'] ?? 'AE')), 0, 8)),
		'currency_code' => strtoupper(substr(trim((string)($data['currency_code'] ?? 'AED')), 0, 8)),
		'linked_user_id' => (int)($data['linked_user_id'] ?? 0),
		'linked_supplier_id' => (int)($data['linked_supplier_id'] ?? 0),
		'notes' => trim((string)($data['notes'] ?? '')),
	);
	if ($fields['name'] === '') {
		throw new Exception('Contact name is required');
	}
	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_erp_contacts` SET `party_type`=?, `name`=?, `company`=?, `email`=?, `phone`=?, `trn`=?, `address`=?, `city`=?, `country_code`=?, `currency_code`=?, `linked_user_id`=?, `linked_supplier_id`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array_merge(array_values($fields), array($now, $id)));
		return $id;
	}
	$db->prepare(
		'INSERT INTO `epc_erp_contacts` (`party_type`, `name`, `company`, `email`, `phone`, `trn`, `address`, `city`, `country_code`, `currency_code`, `linked_user_id`, `linked_supplier_id`, `notes`, `time_created`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array_merge(array_values($fields), array($now, $now)));
	$newId = (int)$db->lastInsertId();
	return $newId;
}

function epc_erp_contacts_sync_from_masters(PDO $db)
{
	epc_erp_phase8_ensure_schema($db);
	$n = 0;
	$sups = $db->query('SELECT `id`, `name`, `contact_email`, `contact_phone`, `trn` FROM `epc_erp_suppliers` WHERE `active` = 1')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($sups as $s) {
		$chk = $db->prepare('SELECT `id` FROM `epc_erp_contacts` WHERE `linked_supplier_id` = ? LIMIT 1');
		$chk->execute(array((int)$s['id']));
		if ($chk->fetchColumn()) {
			continue;
		}
		epc_erp_contact_save($db, array(
			'party_type' => 'supplier',
			'name' => $s['name'],
			'email' => $s['contact_email'] ?? '',
			'phone' => $s['contact_phone'] ?? '',
			'trn' => $s['trn'] ?? '',
			'linked_supplier_id' => (int)$s['id'],
		));
		$n++;
	}
	if (epc_erp_shop_orders_has_status($db)) {
		$users = $db->query(
			'SELECT DISTINCT o.`user_id`, u.`email`, u.`phone`
			 FROM `shop_orders` o
			 INNER JOIN `users` u ON u.`user_id` = o.`user_id`
			 WHERE o.`user_id` > 0 LIMIT 500'
		)->fetchAll(PDO::FETCH_ASSOC);
		foreach ($users as $u) {
			$uid = (int)$u['user_id'];
			$chk = $db->prepare('SELECT `id` FROM `epc_erp_contacts` WHERE `linked_user_id` = ? LIMIT 1');
			$chk->execute(array($uid));
			if ($chk->fetchColumn()) {
				continue;
			}
			epc_erp_contact_save($db, array(
				'party_type' => 'customer',
				'name' => $u['email'] ?: ('Customer #' . $uid),
				'email' => $u['email'] ?? '',
				'phone' => $u['phone'] ?? '',
				'linked_user_id' => $uid,
			));
			$n++;
		}
	}
	return $n;
}

function epc_erp_sales_orders_list(PDO $db, $date_from, $date_to, array $filters = array(), $limit = 150)
{
	require_once __DIR__ . '/epc_erp_vouchers.php';
	if (epc_erp_is_erp_only_context()) {
		return epc_erp_manual_sales_orders_list($db, (int) $date_from, (int) $date_to, $filters, (int) $limit);
	}
	$rows = epc_erp_revenue_report($db, $date_from, $date_to, $limit);
	$out = array();
	foreach ($rows as $r) {
		if (!empty($filters['status']) && $filters['status'] === 'complete' && empty($r['order_complete'])) {
			continue;
		}
		if (!empty($filters['status']) && $filters['status'] === 'open' && !empty($r['order_complete'])) {
			continue;
		}
		if (!empty($filters['paid']) && $filters['paid'] === 'due') {
			if ((int)($r['paid'] ?? 0) === 1 || (float)($r['due_amount'] ?? 0) <= 0) {
				continue;
			}
		}
		if (!empty($filters['q'])) {
			$q = (string)$filters['q'];
			$match = (ctype_digit($q) && (int)$r['id'] === (int)$q)
				|| stripos((string)($r['customer_email'] ?? ''), $q) !== false;
			if (!$match) {
				continue;
			}
		}
		$out[] = $r;
	}
	return $out;
}

function epc_erp_rfq_list(PDO $db, $status = '', $limit = 100)
{
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT r.*, s.`name` AS supplier_name FROM `epc_erp_rfq` r
		LEFT JOIN `epc_erp_suppliers` s ON s.`id` = r.`supplier_id` WHERE 1=1';
	$params = array();
	if ($status !== '') {
		$sql .= ' AND r.`status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY r.`time_updated` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_rfq_save(PDO $db, array $data)
{
	epc_erp_phase8_ensure_schema($db);
	$id = (int)($data['id'] ?? 0);
	$now = time();
	$title = trim((string)($data['title'] ?? ''));
	if ($title === '') {
		throw new Exception('RFQ title is required');
	}
	$row = array(
		'supplier_id' => (int)($data['supplier_id'] ?? 0),
		'title' => $title,
		'description' => trim((string)($data['description'] ?? '')),
		'amount_est' => round((float)($data['amount_est'] ?? 0), 2),
		'status' => in_array($data['status'] ?? '', array('draft', 'sent', 'quoted', 'accepted', 'rejected', 'cancelled'), true)
			? $data['status'] : 'draft',
		'due_date' => !empty($data['due_date']) ? strtotime($data['due_date'] . ' 23:59:59') : 0,
		'order_id' => (int)($data['order_id'] ?? 0),
		'admin_id' => epc_erp_admin_id(),
		'time_updated' => $now,
	);
	if ($id > 0) {
		$db->prepare(
			'UPDATE `epc_erp_rfq` SET `supplier_id`=?, `title`=?, `description`=?, `amount_est`=?, `status`=?, `due_date`=?, `order_id`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array(
			$row['supplier_id'], $row['title'], $row['description'], $row['amount_est'],
			$row['status'], $row['due_date'], $row['order_id'], $row['time_updated'], $id,
		));
		return $id;
	}
	$rfqNo = epc_erp_phase8_next_no($db, 'RFQ-', 'epc_erp_rfq', 'rfq_no');
	$db->prepare(
		'INSERT INTO `epc_erp_rfq` (`rfq_no`, `supplier_id`, `title`, `description`, `amount_est`, `status`, `due_date`, `order_id`, `admin_id`, `time_created`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$rfqNo, $row['supplier_id'], $row['title'], $row['description'], $row['amount_est'],
		$row['status'], $row['due_date'], $row['order_id'], $row['admin_id'], $now, $now,
	));
	return (int)$db->lastInsertId();
}

function epc_erp_delivery_notes_list(PDO $db, $date_from = 0, $date_to = 0, $limit = 100)
{
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT d.*, u.`email` AS customer_email FROM `epc_erp_delivery_notes` d
		LEFT JOIN `shop_orders` o ON o.`id` = d.`order_id`
		LEFT JOIN `users` u ON u.`user_id` = o.`user_id` WHERE 1=1';
	$params = array();
	if ($date_from > 0) {
		$sql .= ' AND d.`time_created` >= ?';
		$params[] = $date_from;
	}
	if ($date_to > 0) {
		$sql .= ' AND d.`time_created` <= ?';
		$params[] = $date_to;
	}
	$sql .= ' ORDER BY d.`id` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_delivery_note_create(PDO $db, array $data)
{
	epc_erp_phase8_ensure_schema($db);
	$orderId = (int)($data['order_id'] ?? 0);
	if ($orderId <= 0) {
		throw new Exception('Order ID is required');
	}
	$now = time();
	$noteNo = epc_erp_phase8_next_no($db, 'DN-', 'epc_erp_delivery_notes', 'note_no');
	$status = !empty($data['mark_shipped']) ? 'shipped' : 'draft';
	$shipped = !empty($data['mark_shipped']) ? $now : 0;
	$db->prepare(
		'INSERT INTO `epc_erp_delivery_notes` (`note_no`, `order_id`, `carrier`, `tracking_no`, `shipped_at`, `status`, `notes`, `admin_id`, `time_created`)
		 VALUES (?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$noteNo,
		$orderId,
		trim((string)($data['carrier'] ?? '')),
		trim((string)($data['tracking_no'] ?? '')),
		$shipped,
		$status,
		trim((string)($data['notes'] ?? '')),
		epc_erp_admin_id(),
		$now,
	));
	$id = (int)$db->lastInsertId();
	$pdfRel = epc_erp_delivery_note_pdf_path($db, $id);
	$db->prepare('UPDATE `epc_erp_delivery_notes` SET `pdf_path` = ? WHERE `id` = ?')->execute(array($pdfRel, $id));
	return array('id' => $id, 'note_no' => $noteNo, 'pdf_path' => $pdfRel);
}

function epc_erp_delivery_note_pdf_path(PDO $db, $noteId)
{
	$st = $db->prepare('SELECT d.*, u.`email` FROM `epc_erp_delivery_notes` d
		LEFT JOIN `shop_orders` o ON o.`id` = d.`order_id`
		LEFT JOIN `users` u ON u.`user_id` = o.`user_id` WHERE d.`id` = ? LIMIT 1');
	$st->execute(array($noteId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return '';
	}
	$dir = $_SERVER['DOCUMENT_ROOT'] . '/content/files/epc_erp_delivery_notes';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	$file = $dir . '/dn_' . preg_replace('/[^a-zA-Z0-9_-]/', '_', $row['note_no']) . '.html';
	$html = '<!DOCTYPE html><html><head><meta charset="utf-8"><title>Delivery ' . htmlspecialchars($row['note_no']) . '</title>
	<style>body{font-family:Arial,sans-serif;margin:40px;} h1{font-size:20px;} table{border-collapse:collapse;width:100%;margin-top:20px;} td,th{border:1px solid #ccc;padding:8px;}</style></head><body>';
	$html .= '<h1>Delivery note ' . htmlspecialchars($row['note_no']) . '</h1>';
	$html .= '<p><strong>Order:</strong> #' . (int)$row['order_id'] . '<br>';
	$html .= '<strong>Customer:</strong> ' . htmlspecialchars($row['email'] ?: '—') . '<br>';
	$html .= '<strong>Carrier:</strong> ' . htmlspecialchars($row['carrier'] ?: '—') . '<br>';
	$html .= '<strong>Tracking:</strong> ' . htmlspecialchars($row['tracking_no'] ?: '—') . '<br>';
	$html .= '<strong>Status:</strong> ' . htmlspecialchars($row['status']) . '</p>';
	if (!empty($row['notes'])) {
		$html .= '<p>' . nl2br(htmlspecialchars($row['notes'])) . '</p>';
	}
	$html .= '<p style="margin-top:40px;font-size:12px;color:#666;">Generated ' . date('Y-m-d H:i') . ' — ECOM AE ERP</p></body></html>';
	file_put_contents($file, $html);
	return '/content/files/epc_erp_delivery_notes/' . basename($file);
}

function epc_erp_bank_statement_import(PDO $db, $accountId, $csvText)
{
	epc_erp_phase8_ensure_schema($db);
	if ($accountId <= 0) {
		throw new Exception('Account required');
	}
	$batch = 'IMP-' . date('Ymd-His');
	$lines = preg_split('/\r\n|\r|\n/', trim($csvText));
	$n = 0;
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_bank_statement_lines` (`account_id`, `line_date`, `description`, `reference`, `amount`, `direction`, `import_batch`, `time_created`)
		 VALUES (?,?,?,?,?,?,?,?)'
	);
	foreach ($lines as $i => $line) {
		if ($i === 0 && stripos($line, 'date') !== false) {
			continue;
		}
		$line = trim($line);
		if ($line === '') {
			continue;
		}
		$cols = str_getcsv($line);
		if (count($cols) < 2) {
			continue;
		}
		$dt = strtotime($cols[0]) ?: time();
		$desc = isset($cols[1]) ? trim($cols[1]) : '';
		$ref = isset($cols[2]) ? trim($cols[2]) : '';
		$amt = isset($cols[3]) ? (float)str_replace(',', '', $cols[3]) : 0;
		if ($amt == 0 && isset($cols[2]) && is_numeric(str_replace(',', '', $cols[2]))) {
			$amt = (float)str_replace(',', '', $cols[2]);
			$ref = '';
		}
		$dir = $amt >= 0 ? 1 : 0;
		$ins->execute(array($accountId, $dt, $desc, $ref, abs($amt), $dir, $batch, time()));
		$n++;
	}
	return array('imported' => $n, 'batch' => $batch);
}

function epc_erp_bank_unmatched_lines(PDO $db, $accountId = 0)
{
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_erp_bank_statement_lines` WHERE `matched_entry_id` = 0';
	$params = array();
	if ($accountId > 0) {
		$sql .= ' AND `account_id` = ?';
		$params[] = $accountId;
	}
	$sql .= ' ORDER BY `line_date` DESC LIMIT 200';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_bank_unmatched_entries(PDO $db, $accountId)
{
	$sql = 'SELECT e.* FROM `epc_erp_cash_bank_entries` e
		WHERE e.`account_id` = ? AND e.`active` = 1
		AND e.`id` NOT IN (SELECT `matched_entry_id` FROM `epc_erp_bank_statement_lines` WHERE `matched_entry_id` > 0)
		ORDER BY e.`time` DESC LIMIT 100';
	$st = $db->prepare($sql);
	$st->execute(array($accountId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_bank_reconcile_match(PDO $db, $lineId, $entryId)
{
	epc_erp_phase8_ensure_schema($db);
	$lineId = (int)$lineId;
	$entryId = (int)$entryId;
	if ($lineId <= 0 || $entryId <= 0) {
		throw new Exception('Invalid match');
	}
	$db->prepare('UPDATE `epc_erp_bank_statement_lines` SET `matched_entry_id` = ? WHERE `id` = ?')
		->execute(array($entryId, $lineId));
	return true;
}

function epc_erp_expense_reports_list(PDO $db, $status = '', $limit = 80)
{
	epc_erp_phase8_ensure_schema($db);
	$sql = 'SELECT e.*, p.`display_name` FROM `epc_erp_expense_reports` e
		LEFT JOIN `epc_erp_staff_profiles` p ON p.`user_id` = e.`staff_user_id` WHERE 1=1';
	$params = array();
	if ($status !== '') {
		$sql .= ' AND e.`status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY e.`time_updated` DESC LIMIT ' . (int)$limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_expense_report_save(PDO $db, array $data)
{
	epc_erp_phase8_ensure_schema($db);
	$title = trim((string)($data['title'] ?? ''));
	$amount = round((float)($data['total_amount'] ?? 0), 2);
	if ($title === '' || $amount <= 0) {
		throw new Exception('Title and amount required');
	}
	$now = time();
	$reportNo = epc_erp_phase8_next_no($db, 'EXP-', 'epc_erp_expense_reports', 'report_no');
	$db->prepare(
		'INSERT INTO `epc_erp_expense_reports` (`report_no`, `staff_user_id`, `title`, `total_amount`, `status`, `period_from`, `period_to`, `notes`, `admin_id`, `time_created`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$reportNo,
		(int)($data['staff_user_id'] ?? 0),
		$title,
		$amount,
		'submitted',
		!empty($data['period_from']) ? strtotime($data['period_from'] . ' 00:00:00') : $now,
		!empty($data['period_to']) ? strtotime($data['period_to'] . ' 23:59:59') : $now,
		trim((string)($data['notes'] ?? '')),
		epc_erp_admin_id(),
		$now,
		$now,
	));
	return (int)$db->lastInsertId();
}

function epc_erp_dashboard_dept_widgets(PDO $db, $date_from, $date_to)
{
	$dash = epc_erp_dashboard($db, $date_from, $date_to);
	$ful = epc_erp_fulfilment_summary_light($db, $date_from, $date_to);
	require_once __DIR__ . '/epc_erp_inventory.php';
	epc_erp_inventory_ensure_schema($db);
	$invVal = epc_erp_inventory_valuation_total($db, 0);
	$widgets = array(
		array('dept' => 'Finance', 'icon' => 'fa-money', 'color' => '#16a34a', 'cards' => array(
			array('label' => 'Revenue ex VAT', 'value' => epc_erp_money($dash['revenue_ex_vat']) . ' AED', 'class' => 'blue'),
			array('label' => 'Net VAT', 'value' => epc_erp_money($dash['vat_net_payable']) . ' AED', 'class' => ($dash['vat_net_payable'] ?? 0) >= 0 ? 'red' : 'green'),
			array('label' => 'Payables', 'value' => epc_erp_money($dash['payable_balance']) . ' AED', 'class' => 'red'),
		)),
		array('dept' => 'Sales', 'icon' => 'fa-line-chart', 'color' => '#2563eb', 'cards' => array(
			array('label' => 'Completed orders', 'value' => (string)(int)$dash['order_count']),
			array('label' => 'Receivable due', 'value' => epc_erp_money($dash['receivable_due_orders']) . ' AED', 'class' => 'red'),
			array('label' => 'Margin', 'value' => epc_erp_money($dash['profit_ex_vat']) . ' AED', 'class' => 'green'),
		)),
		array('dept' => 'Operations', 'icon' => 'fa-truck', 'color' => '#0d9488', 'cards' => array(
			array('label' => 'Orders in period', 'value' => (string)(int)$ful['total_orders']),
			array('label' => 'Delivered', 'value' => (string)(int)$ful['pipeline']['delivery_done']),
			array('label' => 'Stock value', 'value' => epc_erp_money($invVal) . ' AED', 'class' => 'green'),
		)),
		array('dept' => 'Treasury', 'icon' => 'fa-university', 'color' => '#7c3aed', 'cards' => array(
			array('label' => 'Cash & bank', 'value' => epc_erp_money($dash['cash_bank_total']) . ' AED', 'class' => 'green'),
			array('label' => 'Customer ledger', 'value' => epc_erp_money($dash['customer_ledger_balance']) . ' AED'),
		)),
	);
	return $widgets;
}

function epc_erp_reports_export(PDO $db, $type, $date_from, $date_to)
{
	$rows = array();
	$headers = array();
	switch ($type) {
		case 'gl':
			require_once __DIR__ . '/epc_erp_gl.php';
			$tb = epc_erp_gl_trial_balance($db, $date_to > 0 ? $date_to : time());
			$headers = array('Code', 'Account', 'Type', 'Debit', 'Credit', 'Balance');
			foreach ($tb['rows'] as $ln) {
				$rows[] = array($ln['code'], $ln['name'], $ln['account_type'], $ln['debit'], $ln['credit'], $ln['balance']);
			}
			break;
		case 'sales':
			$headers = array('Order', 'Date', 'Customer', 'Sale ex VAT', 'Paid', 'Due');
			foreach (epc_erp_revenue_report($db, $date_from, $date_to, 5000) as $r) {
				if (empty($r['order_complete'])) {
					continue;
				}
				$rows[] = array(
					$r['id'], date('Y-m-d', (int)$r['time']), $r['customer_email'],
					$r['sale_ex_vat'], $r['paid_amount'], $r['due_amount'],
				);
			}
			break;
		case 'stock':
			require_once __DIR__ . '/epc_erp_inventory.php';
			epc_erp_inventory_ensure_schema($db);
			$headers = array('SKU', 'Name', 'Warehouse', 'Qty', 'Avg cost', 'Value');
			foreach (epc_erp_inventory_stock_report($db, 0) as $s) {
				$rows[] = array(
					$s['sku'], $s['name'], $s['warehouse_name'],
					$s['qty_on_hand'], $s['avg_unit_cost'],
					round((float)$s['qty_on_hand'] * (float)$s['avg_unit_cost'], 2),
				);
			}
			break;
		default:
			throw new Exception('Unknown export type');
	}
	return array('headers' => $headers, 'rows' => $rows);
}

function epc_erp_csv_output($filename, array $headers, array $rows)
{
	header('Content-Type: text/csv; charset=utf-8');
	header('Content-Disposition: attachment; filename="' . preg_replace('/[^a-zA-Z0-9._-]/', '_', $filename) . '"');
	$out = fopen('php://output', 'w');
	fputcsv($out, $headers);
	foreach ($rows as $row) {
		fputcsv($out, $row);
	}
	fclose($out);
	exit;
}
