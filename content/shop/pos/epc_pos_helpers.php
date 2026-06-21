<?php
/**
 * EPC POS — schema, product search, sessions, checkout → ERP SO + invoice + receipt.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/../finance/epc_erp_schema.php';
require_once __DIR__ . '/../finance/epc_erp_helpers.php';
require_once __DIR__ . '/../finance/epc_erp_vouchers.php';
require_once __DIR__ . '/../finance/epc_tax_toolkit.php';

function epc_pos_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_pos_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pos_settings` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`pos_enabled` tinyint(1) NOT NULL DEFAULT 1,
		`register_name` varchar(64) NOT NULL DEFAULT 'Register 1',
		`default_warehouse_id` int(11) NOT NULL DEFAULT 0,
		`walkin_user_id` int(11) NOT NULL DEFAULT 0,
		`default_cash_account_id` int(11) NOT NULL DEFAULT 0,
		`default_card_account_id` int(11) NOT NULL DEFAULT 0,
		`receipt_header` varchar(512) NOT NULL DEFAULT '',
		`receipt_footer` varchar(512) NOT NULL DEFAULT 'Thank you for your purchase',
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS tenant settings (single row)';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pos_sessions` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`session_no` varchar(32) NOT NULL,
		`register_name` varchar(64) NOT NULL DEFAULT '',
		`opened_by` int(11) NOT NULL DEFAULT 0,
		`opened_at` int(11) NOT NULL DEFAULT 0,
		`closed_at` int(11) NOT NULL DEFAULT 0,
		`opening_float` decimal(14,2) NOT NULL DEFAULT 0.00,
		`closing_cash` decimal(14,2) DEFAULT NULL,
		`expected_cash` decimal(14,2) DEFAULT NULL,
		`sales_count` int(11) NOT NULL DEFAULT 0,
		`sales_total` decimal(14,2) NOT NULL DEFAULT 0.00,
		`status` enum('open','closed') NOT NULL DEFAULT 'open',
		`notes` varchar(512) NOT NULL DEFAULT '',
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_session_no` (`session_no`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS register shifts';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pos_sales` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`session_id` int(11) NOT NULL DEFAULT 0,
		`sale_no` varchar(32) NOT NULL,
		`customer_user_id` int(11) NOT NULL DEFAULT 0,
		`contact_id` int(11) NOT NULL DEFAULT 0,
		`customer_label` varchar(255) NOT NULL DEFAULT 'Walk-in guest',
		`sales_order_id` int(11) NOT NULL DEFAULT 0,
		`sales_invoice_id` int(11) NOT NULL DEFAULT 0,
		`receipt_voucher_no` varchar(32) NOT NULL DEFAULT '',
		`subtotal_ex` decimal(14,2) NOT NULL DEFAULT 0.00,
		`discount_total` decimal(14,2) NOT NULL DEFAULT 0.00,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`payment_method` enum('cash','card','split') NOT NULL DEFAULT 'cash',
		`cash_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`card_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`tax_kit_code` varchar(32) NOT NULL DEFAULT '',
		`tax_rate` decimal(6,3) NOT NULL DEFAULT 0.000,
		`status` enum('completed','void') NOT NULL DEFAULT 'completed',
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_sale_no` (`sale_no`),
		KEY `x_session` (`session_id`),
		KEY `x_time` (`time_created`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS completed sales';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pos_sale_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`sale_id` int(11) NOT NULL,
		`line_no` int(11) NOT NULL DEFAULT 1,
		`product_source` varchar(16) NOT NULL DEFAULT 'manual',
		`product_ref` varchar(64) NOT NULL DEFAULT '',
		`sku` varchar(64) NOT NULL DEFAULT '',
		`barcode` varchar(64) NOT NULL DEFAULT '',
		`name` varchar(255) NOT NULL,
		`qty` decimal(14,3) NOT NULL DEFAULT 1.000,
		`unit_price_ex` decimal(14,4) NOT NULL DEFAULT 0.0000,
		`line_discount_pct` decimal(6,2) NOT NULL DEFAULT 0.00,
		`line_discount_amt` decimal(14,2) NOT NULL DEFAULT 0.00,
		`line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,
		`tax_rate` decimal(6,3) NOT NULL DEFAULT 0.000,
		`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`line_total` decimal(14,2) NOT NULL DEFAULT 0.00,
		PRIMARY KEY (`id`),
		KEY `x_sale` (`sale_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS sale line items';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_pos_sequences` (
		`year` int(11) NOT NULL,
		`last_seq` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`year`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='POS sale number sequence';");

	$cnt = (int) $db->query('SELECT COUNT(*) FROM `epc_pos_settings`')->fetchColumn();
	if ($cnt <= 0) {
		$db->prepare(
			'INSERT INTO `epc_pos_settings` (`pos_enabled`, `register_name`, `receipt_footer`, `time_updated`) VALUES (1, ?, ?, ?)'
		)->execute(array('Register 1', 'Thank you for your purchase', time()));
	}
}

function epc_pos_get_settings(PDO $db): array
{
	epc_pos_ensure_schema($db);
	$row = $db->query('SELECT * FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1')->fetch(PDO::FETCH_ASSOC);
	return $row ?: array('pos_enabled' => 1, 'register_name' => 'Register 1');
}

function epc_pos_save_settings(PDO $db, array $data): void
{
	epc_pos_ensure_schema($db);
	$cur = epc_pos_get_settings($db);
	$id = (int) ($cur['id'] ?? 1);
	$db->prepare(
		'UPDATE `epc_pos_settings` SET
		 `pos_enabled` = ?, `register_name` = ?, `default_warehouse_id` = ?,
		 `default_cash_account_id` = ?, `default_card_account_id` = ?,
		 `receipt_header` = ?, `receipt_footer` = ?, `time_updated` = ?
		 WHERE `id` = ?'
	)->execute(array(
		!empty($data['pos_enabled']) ? 1 : 0,
		substr(trim((string) ($data['register_name'] ?? 'Register 1')), 0, 64),
		(int) ($data['default_warehouse_id'] ?? 0),
		(int) ($data['default_cash_account_id'] ?? 0),
		(int) ($data['default_card_account_id'] ?? 0),
		substr(trim((string) ($data['receipt_header'] ?? '')), 0, 512),
		substr(trim((string) ($data['receipt_footer'] ?? 'Thank you for your purchase')), 0, 512),
		time(),
		$id,
	));
}

function epc_pos_next_sale_no(PDO $db): string
{
	epc_pos_ensure_schema($db);
	$year = (int) date('Y');
	$started = $db->beginTransaction();
	try {
		$sel = $db->prepare('SELECT `last_seq` FROM `epc_pos_sequences` WHERE `year` = ? FOR UPDATE');
		$sel->execute(array($year));
		$seq = (int) $sel->fetchColumn();
		if ($seq <= 0) {
			$db->prepare('INSERT INTO `epc_pos_sequences` (`year`, `last_seq`) VALUES (?, 1) ON DUPLICATE KEY UPDATE `last_seq` = `last_seq` + 1')
				->execute(array($year));
			$seq = 1;
		} else {
			$seq++;
			$db->prepare('UPDATE `epc_pos_sequences` SET `last_seq` = ? WHERE `year` = ?')->execute(array($seq, $year));
		}
		if ($started) {
			$db->commit();
		}
	} catch (Exception $e) {
		if ($started && $db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
	return 'POS-' . $year . '-' . str_pad((string) $seq, 5, '0', STR_PAD_LEFT);
}

function epc_pos_next_session_no(PDO $db): string
{
	return 'REG-' . date('Ymd') . '-' . str_pad((string) random_int(1, 9999), 4, '0', STR_PAD_LEFT);
}

function epc_pos_ensure_walkin_user(PDO $db): int
{
	$settings = epc_pos_get_settings($db);
	$uid = (int) ($settings['walkin_user_id'] ?? 0);
	if ($uid > 0) {
		$chk = $db->prepare('SELECT `user_id` FROM `users` WHERE `user_id` = ? LIMIT 1');
		$chk->execute(array($uid));
		if ($chk->fetchColumn()) {
			return $uid;
		}
	}
	$email = 'pos.walkin@local';
	$st = $db->prepare('SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($email));
	$existing = (int) $st->fetchColumn();
	if ($existing > 0) {
		$db->prepare('UPDATE `epc_pos_settings` SET `walkin_user_id` = ? WHERE `id` = ?')
			->execute(array($existing, (int) $settings['id']));
		return $existing;
	}
	$now = time();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$cfg = new DP_Config();
	$hash = md5(bin2hex(random_bytes(8)) . $cfg->secret_succession);
	try {
		$db->prepare(
			'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
			 VALUES (?, 1, ?, 1, 1, ?, 1)'
		)->execute(array($email, $hash, $now));
		$uid = (int) $db->lastInsertId();
	} catch (Exception $e) {
		$st->execute(array($email));
		$uid = (int) $st->fetchColumn();
	}
	if ($uid > 0) {
		try {
			epc_tax_toolkit_assign_customer($db, $uid, 0, 'AE', 'AE-UAE-VAT', 'retail', '', null, false, true);
		} catch (Exception $e) {
		}
		$db->prepare('UPDATE `epc_pos_settings` SET `walkin_user_id` = ? WHERE `id` = ?')
			->execute(array($uid, (int) $settings['id']));
	}
	return $uid;
}

function epc_pos_default_cash_account(PDO $db): int
{
	$settings = epc_pos_get_settings($db);
	$aid = (int) ($settings['default_cash_account_id'] ?? 0);
	if ($aid > 0) {
		return $aid;
	}
	require_once __DIR__ . '/../finance/epc_erp_helpers.php';
	$accounts = epc_erp_list_cash_accounts($db);
	foreach ($accounts as $a) {
		if (stripos((string) ($a['name'] ?? ''), 'cash') !== false || ($a['account_type'] ?? '') === 'cash') {
			return (int) $a['id'];
		}
	}
	return !empty($accounts[0]['id']) ? (int) $accounts[0]['id'] : 0;
}

function epc_pos_default_card_account(PDO $db): int
{
	$settings = epc_pos_get_settings($db);
	$aid = (int) ($settings['default_card_account_id'] ?? 0);
	if ($aid > 0) {
		return $aid;
	}
	require_once __DIR__ . '/../finance/epc_erp_helpers.php';
	$accounts = epc_erp_list_cash_accounts($db);
	foreach ($accounts as $a) {
		if (stripos((string) ($a['name'] ?? ''), 'card') !== false || stripos((string) ($a['name'] ?? ''), 'bank') !== false) {
			return (int) $a['id'];
		}
	}
	return epc_pos_default_cash_account($db);
}

function epc_pos_get_open_session(PDO $db, int $adminId = 0): ?array
{
	epc_pos_ensure_schema($db);
	$sql = 'SELECT * FROM `epc_pos_sessions` WHERE `status` = \'open\' ORDER BY `opened_at` DESC LIMIT 1';
	$row = $db->query($sql)->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_pos_open_session(PDO $db, float $openingFloat, int $adminId, string $registerName = ''): array
{
	epc_pos_ensure_schema($db);
	$open = epc_pos_get_open_session($db);
	if ($open) {
		throw new Exception('A register session is already open (' . $open['session_no'] . ')');
	}
	$settings = epc_pos_get_settings($db);
	if (empty($settings['pos_enabled'])) {
		throw new Exception('POS is disabled for this tenant');
	}
	$reg = $registerName !== '' ? $registerName : (string) ($settings['register_name'] ?? 'Register 1');
	$now = time();
	$sessionNo = epc_pos_next_session_no($db);
	$db->prepare(
		'INSERT INTO `epc_pos_sessions` (`session_no`, `register_name`, `opened_by`, `opened_at`, `opening_float`, `status`)
		 VALUES (?,?,?,?,?,?)'
	)->execute(array($sessionNo, $reg, $adminId, $now, round($openingFloat, 2), 'open'));
	return array(
		'session_id' => (int) $db->lastInsertId(),
		'session_no' => $sessionNo,
		'register_name' => $reg,
		'opening_float' => round($openingFloat, 2),
		'opened_at' => $now,
	);
}

function epc_pos_close_session(PDO $db, int $sessionId, float $closingCash, string $notes = ''): array
{
	epc_pos_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_pos_sessions` WHERE `id` = ? AND `status` = \'open\' LIMIT 1');
	$st->execute(array($sessionId));
	$sess = $st->fetch(PDO::FETCH_ASSOC);
	if (!$sess) {
		throw new Exception('Open session not found');
	}
	$salesSt = $db->prepare(
		'SELECT COUNT(*) AS cnt, COALESCE(SUM(`cash_amount`),0) AS cash_total, COALESCE(SUM(`total_amount`),0) AS grand_total
		 FROM `epc_pos_sales` WHERE `session_id` = ? AND `status` = \'completed\''
	);
	$salesSt->execute(array($sessionId));
	$sales = $salesSt->fetch(PDO::FETCH_ASSOC);
	$expected = round((float) $sess['opening_float'] + (float) ($sales['cash_total'] ?? 0), 2);
	$now = time();
	$db->prepare(
		'UPDATE `epc_pos_sessions` SET `closed_at` = ?, `closing_cash` = ?, `expected_cash` = ?,
		 `sales_count` = ?, `sales_total` = ?, `status` = \'closed\', `notes` = ? WHERE `id` = ?'
	)->execute(array(
		$now,
		round($closingCash, 2),
		$expected,
		(int) ($sales['cnt'] ?? 0),
		round((float) ($sales['grand_total'] ?? 0), 2),
		substr(trim($notes), 0, 512),
		$sessionId,
	));
	return array(
		'session_id' => $sessionId,
		'expected_cash' => $expected,
		'closing_cash' => round($closingCash, 2),
		'variance' => round($closingCash - $expected, 2),
		'sales_count' => (int) ($sales['cnt'] ?? 0),
	);
}

function epc_pos_search_products(PDO $db, string $q, int $limit = 30): array
{
	$q = trim($q);
	if ($q === '') {
		return array();
	}
	$limit = max(1, min(50, $limit));
	$like = '%' . $q . '%';
	$exact = preg_replace('/\s+/', '', strtoupper($q));
	$out = array();
	$seen = array();

	$priceSql = 'SELECT `id`, `manufacturer`, COALESCE(NULLIF(`article_show`, \'\'), `article`) AS `article`,
		`name`, `price`, `exist`, `storage`
	 FROM `shop_docpart_prices_data`
	 WHERE (`name` LIKE ? OR `article` LIKE ? OR `article_show` LIKE ? OR `manufacturer` LIKE ?
	    OR UPPER(REPLACE(`article`, \' \', \'\')) = ?)
	   AND IFNULL(`price`, 0) > 0
	 ORDER BY `name` ASC LIMIT ' . (int) $limit;
	try {
		$st = $db->prepare($priceSql);
		$st->execute(array($like, $like, $like, $like, $exact));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$key = 'pd_' . (int) $row['id'];
			if (isset($seen[$key])) {
				continue;
			}
			$seen[$key] = true;
			$sku = trim((string) $row['article']);
			$out[] = array(
				'source' => 'price_data',
				'ref' => (string) $row['id'],
				'sku' => $sku,
				'barcode' => $sku,
				'name' => (string) $row['name'],
				'brand' => (string) $row['manufacturer'],
				'price' => round((float) $row['price'], 4),
				'stock' => (int) ($row['exist'] ?? 0),
				'storage' => (string) ($row['storage'] ?? ''),
			);
		}
	} catch (Exception $e) {
	}

	if (count($out) < $limit) {
		try {
			$catSt = $db->prepare(
				'SELECT `id`, `caption`, `alias`, `price` FROM `shop_catalogue_products`
				 WHERE (`caption` LIKE ? OR `alias` LIKE ?) AND `published_flag` = 1
				 ORDER BY `caption` ASC LIMIT ' . (int) ($limit - count($out))
			);
			$catSt->execute(array($like, $like));
			while ($row = $catSt->fetch(PDO::FETCH_ASSOC)) {
				$key = 'cat_' . (int) $row['id'];
				if (isset($seen[$key])) {
					continue;
				}
				$seen[$key] = true;
				$out[] = array(
					'source' => 'catalog',
					'ref' => (string) $row['id'],
					'sku' => (string) $row['alias'],
					'barcode' => (string) $row['alias'],
					'name' => (string) $row['caption'],
					'brand' => '',
					'price' => round((float) $row['price'], 4),
					'stock' => null,
				);
			}
		} catch (Exception $e) {
		}
	}

	if (count($out) < $limit) {
		try {
			require_once __DIR__ . '/../finance/epc_erp_inventory.php';
			epc_erp_inventory_ensure_schema($db);
			$invSt = $db->prepare(
				'SELECT i.`id`, i.`sku`, i.`name`, s.`qty_on_hand`
				 FROM `epc_erp_inv_items` i
				 LEFT JOIN `epc_erp_inv_stock` s ON s.`item_id` = i.`id`
				 WHERE i.`active` = 1 AND (i.`sku` LIKE ? OR i.`name` LIKE ? OR i.`sku` = ?)
				 GROUP BY i.`id`
				 ORDER BY i.`name` ASC LIMIT ' . (int) ($limit - count($out))
			);
			$invSt->execute(array($like, $like, $q));
			while ($row = $invSt->fetch(PDO::FETCH_ASSOC)) {
				$key = 'inv_' . (int) $row['id'];
				if (isset($seen[$key])) {
					continue;
				}
				$seen[$key] = true;
				$out[] = array(
					'source' => 'inventory',
					'ref' => (string) $row['id'],
					'sku' => (string) $row['sku'],
					'barcode' => (string) $row['sku'],
					'name' => (string) $row['name'],
					'brand' => '',
					'price' => 0.0,
					'stock' => (float) ($row['qty_on_hand'] ?? 0),
				);
			}
		} catch (Exception $e) {
		}
	}

	return $out;
}

function epc_pos_search_customers(PDO $db, string $q, int $limit = 20): array
{
	$q = trim($q);
	if ($q === '') {
		return array();
	}
	$like = '%' . $q . '%';
	$out = array();
	$st = $db->prepare(
		'SELECT u.`user_id`, u.`email`, p.`surname`, p.`name`, p.`phone`, p.`country`
		 FROM `users` u
		 LEFT JOIN `users_profile` p ON p.`user_id` = u.`user_id`
		 WHERE u.`email` LIKE ? OR p.`phone` LIKE ? OR p.`name` LIKE ? OR p.`surname` LIKE ?
		 ORDER BY u.`user_id` DESC LIMIT ' . (int) max(1, min(30, $limit))
	);
	try {
		$st->execute(array($like, $like, $like, $like));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$label = trim((string) ($row['name'] ?? '') . ' ' . (string) ($row['surname'] ?? ''));
			if ($label === '') {
				$label = (string) $row['email'];
			}
			$out[] = array(
				'type' => 'user',
				'user_id' => (int) $row['user_id'],
				'contact_id' => 0,
				'label' => $label,
				'email' => (string) $row['email'],
				'phone' => (string) ($row['phone'] ?? ''),
			);
		}
	} catch (Exception $e) {
	}

	try {
		require_once __DIR__ . '/../finance/epc_erp_phase8.php';
		epc_erp_phase8_ensure_schema($db);
		$cst = $db->prepare(
			'SELECT `id`, `name`, `company`, `email`, `phone`, `country_code`
			 FROM `epc_erp_contacts`
			 WHERE `party_type` IN (\'customer\',\'both\') AND (`name` LIKE ? OR `company` LIKE ? OR `email` LIKE ? OR `phone` LIKE ?)
			 ORDER BY `name` ASC LIMIT ' . (int) max(1, min(20, $limit))
		);
		$cst->execute(array($like, $like, $like, $like));
		while ($row = $cst->fetch(PDO::FETCH_ASSOC)) {
			$out[] = array(
				'type' => 'contact',
				'user_id' => 0,
				'contact_id' => (int) $row['id'],
				'label' => trim((string) ($row['name'] ?: $row['company'])),
				'email' => (string) ($row['email'] ?? ''),
				'phone' => (string) ($row['phone'] ?? ''),
			);
		}
	} catch (Exception $e) {
	}

	return $out;
}

function epc_pos_parse_cart_lines(array $lines): array
{
	$parsed = array();
	foreach ($lines as $i => $ln) {
		if (!is_array($ln)) {
			continue;
		}
		$name = trim((string) ($ln['name'] ?? ''));
		if ($name === '') {
			continue;
		}
		$qty = max(0.001, (float) ($ln['qty'] ?? 1));
		$unit = round(max(0, (float) ($ln['unit_price_ex'] ?? $ln['price'] ?? 0)), 4);
		$discPct = min(100, max(0, (float) ($ln['line_discount_pct'] ?? 0)));
		$discAmt = round(max(0, (float) ($ln['line_discount_amt'] ?? 0)), 2);
		$gross = round($qty * $unit, 2);
		if ($discPct > 0) {
			$discAmt = round($gross * $discPct / 100, 2);
		}
		$net = round(max(0, $gross - $discAmt), 2);
		$parsed[] = array(
			'line_no' => count($parsed) + 1,
			'product_source' => substr((string) ($ln['source'] ?? 'manual'), 0, 16),
			'product_ref' => substr((string) ($ln['ref'] ?? ''), 0, 64),
			'sku' => substr((string) ($ln['sku'] ?? ''), 0, 64),
			'barcode' => substr((string) ($ln['barcode'] ?? $ln['sku'] ?? ''), 0, 64),
			'name' => mb_substr($name, 0, 255),
			'qty' => $qty,
			'unit_price_ex' => $unit,
			'line_discount_pct' => $discPct,
			'line_discount_amt' => $discAmt,
			'line_ex_vat' => $net,
		);
	}
	return $parsed;
}

function epc_pos_calc_cart_totals(PDO $db, array $lines, int $userId = 0, int $contactId = 0): array
{
	$subtotal = 0.0;
	$discountTotal = 0.0;
	$lineDetails = array();
	$taxCtx = epc_tax_toolkit_resolve($db, $userId, $contactId);
	$rate = (float) $taxCtx['tax_rate'];

	foreach ($lines as $ln) {
		$gross = round((float) $ln['qty'] * (float) $ln['unit_price_ex'], 2);
		$disc = (float) ($ln['line_discount_amt'] ?? 0);
		$net = round((float) $ln['line_ex_vat'], 2);
		$lineTax = epc_tax_toolkit_calc_line($db, $net, $userId, $contactId);
		$lineDetails[] = array_merge($ln, array(
			'tax_rate' => $lineTax['tax_rate'],
			'vat_amount' => $lineTax['tax_amount'],
			'line_total' => $lineTax['gross_amount'],
		));
		$subtotal += $gross;
		$discountTotal += $disc;
	}

	$amountEx = round($subtotal - $discountTotal, 2);
	$totals = epc_tax_toolkit_calc_amounts($db, $amountEx, $userId, $contactId);

	return array(
		'lines' => $lineDetails,
		'subtotal_ex' => round($subtotal, 2),
		'discount_total' => round($discountTotal, 2),
		'amount_ex_vat' => $amountEx,
		'vat_amount' => $totals['vat_amount'],
		'total_amount' => $totals['total_amount'],
		'tax_rate' => $rate,
		'tax_label' => $taxCtx['tax_label'],
		'kit_code' => $taxCtx['kit_code'],
		'tax_context' => $taxCtx,
	);
}

function epc_pos_complete_sale(PDO $db, array $payload, int $adminId): array
{
	epc_pos_ensure_schema($db);
	epc_erp_vouchers_ensure_schema($db);

	$sessionId = (int) ($payload['session_id'] ?? 0);
	$session = null;
	if ($sessionId > 0) {
		$st = $db->prepare('SELECT * FROM `epc_pos_sessions` WHERE `id` = ? AND `status` = \'open\' LIMIT 1');
		$st->execute(array($sessionId));
		$session = $st->fetch(PDO::FETCH_ASSOC);
	}
	if (!$session) {
		$session = epc_pos_get_open_session($db);
		$sessionId = $session ? (int) $session['id'] : 0;
	}
	if (!$session) {
		throw new Exception('Open a register session before completing a sale');
	}

	$rawLines = $payload['lines'] ?? array();
	if (is_string($rawLines)) {
		$rawLines = json_decode($rawLines, true) ?: array();
	}
	$lines = epc_pos_parse_cart_lines(is_array($rawLines) ? $rawLines : array());
	if (empty($lines)) {
		throw new Exception('Cart is empty');
	}

	$userId = (int) ($payload['customer_user_id'] ?? 0);
	$contactId = (int) ($payload['contact_id'] ?? 0);
	$customerLabel = trim((string) ($payload['customer_label'] ?? ''));
	if ($userId <= 0 && $contactId <= 0) {
		$userId = epc_pos_ensure_walkin_user($db);
		$customerLabel = $customerLabel !== '' ? $customerLabel : 'Walk-in guest';
	} elseif ($userId <= 0 && $contactId > 0) {
		$userId = epc_pos_ensure_walkin_user($db);
		if ($customerLabel === '') {
			$cst = $db->prepare('SELECT `name`, `company` FROM `epc_erp_contacts` WHERE `id` = ? LIMIT 1');
			$cst->execute(array($contactId));
			$cRow = $cst->fetch(PDO::FETCH_ASSOC);
			$customerLabel = trim((string) ($cRow['name'] ?? $cRow['company'] ?? 'Customer'));
		}
	} elseif ($customerLabel === '') {
		if ($userId > 0) {
			$uq = $db->prepare('SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1');
			$uq->execute(array($userId));
			$customerLabel = (string) $uq->fetchColumn();
		} else {
			$customerLabel = 'Customer';
		}
	}

	$calc = epc_pos_calc_cart_totals($db, $lines, $userId, $contactId);
	$paymentMethod = in_array($payload['payment_method'] ?? '', array('cash', 'card', 'split'), true)
		? $payload['payment_method'] : 'cash';
	$total = round((float) $calc['total_amount'], 2);
	$cashAmount = round((float) ($payload['cash_amount'] ?? 0), 2);
	$cardAmount = round((float) ($payload['card_amount'] ?? 0), 2);
	if ($paymentMethod === 'cash') {
		$cashAmount = $total;
		$cardAmount = 0.0;
	} elseif ($paymentMethod === 'card') {
		$cardAmount = $total;
		$cashAmount = 0.0;
	} else {
		if ($cashAmount + $cardAmount <= 0) {
			$cashAmount = $total;
		}
		if (abs($cashAmount + $cardAmount - $total) > 0.02) {
			throw new Exception('Split payment must equal total');
		}
	}

	$saleNo = epc_pos_next_sale_no($db);
	$soData = array(
		'customer_user_id' => $userId,
		'contact_id' => $contactId,
		'title' => 'POS ' . $saleNo,
		'status' => 'confirmed',
		'notes' => 'POS sale ' . $saleNo,
		'lines_json' => json_encode(array_map(function ($ln) {
			return array(
				'description' => $ln['name'],
				'qty' => $ln['qty'],
				'unit_price_ex_vat' => $ln['unit_price_ex'],
				'line_ex_vat' => $ln['line_ex_vat'],
			);
		}, $calc['lines'])),
	);
	$soId = epc_erp_sales_order_save($db, $soData);
	epc_erp_sales_order_set_status($db, $soId, 'confirmed');
	$inv = epc_erp_so_convert_to_invoice($db, $soId);

	$rvNo = '';
	$cashAccount = epc_pos_default_cash_account($db);
	$cardAccount = epc_pos_default_card_account($db);
	if ($cashAmount > 0 && $cashAccount > 0) {
		$rv = epc_erp_receipt_voucher($db, array(
			'user_id' => $userId,
			'account_id' => $cashAccount,
			'amount' => $cashAmount,
			'sales_order_id' => $soId,
			'sales_invoice_id' => (int) $inv['sales_invoice_id'],
			'reference' => $saleNo . '-CASH',
			'note' => 'POS cash payment ' . $saleNo,
			'post_gl' => true,
		));
		$rvNo = $rv['voucher_no'];
	}
	if ($cardAmount > 0 && $cardAccount > 0) {
		$rvCard = epc_erp_receipt_voucher($db, array(
			'user_id' => $userId,
			'account_id' => $cardAccount,
			'amount' => $cardAmount,
			'sales_order_id' => $soId,
			'sales_invoice_id' => (int) $inv['sales_invoice_id'],
			'reference' => $saleNo . '-CARD',
			'note' => 'POS card payment ' . $saleNo,
			'post_gl' => true,
		));
		if ($rvNo === '') {
			$rvNo = $rvCard['voucher_no'];
		}
	}

	$settings = epc_pos_get_settings($db);
	$warehouseId = (int) ($settings['default_warehouse_id'] ?? 0);
	if ($warehouseId <= 0) {
		try {
			require_once __DIR__ . '/../finance/epc_erp_inventory.php';
			epc_erp_inventory_ensure_schema($db);
			$wh = $db->query('SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
			$warehouseId = (int) $wh;
		} catch (Exception $e) {
			$warehouseId = 0;
		}
	}
	if ($warehouseId > 0) {
		require_once __DIR__ . '/../finance/epc_erp_inventory.php';
		foreach ($calc['lines'] as $ln) {
			$sku = trim((string) ($ln['sku'] ?? ''));
			if ($sku === '') {
				continue;
			}
			try {
				$itemId = epc_erp_inventory_resolve_item_id($db, $sku, array(
					'create_if_missing' => true,
					'name' => $ln['name'],
				));
				if ($itemId > 0) {
					epc_erp_inventory_record_movement($db, array(
						'movement_type' => 'sale_out',
						'warehouse_id' => $warehouseId,
						'item_id' => $itemId,
						'qty' => (float) $ln['qty'],
						'reference' => $saleNo,
						'note' => 'POS sale',
						'order_id' => $soId,
					));
				}
			} catch (Exception $e) {
			}
		}
	}

	$now = time();
	$db->prepare(
		'INSERT INTO `epc_pos_sales`
		(`session_id`, `sale_no`, `customer_user_id`, `contact_id`, `customer_label`, `sales_order_id`, `sales_invoice_id`,
		 `receipt_voucher_no`, `subtotal_ex`, `discount_total`, `vat_amount`, `total_amount`, `payment_method`,
		 `cash_amount`, `card_amount`, `tax_kit_code`, `tax_rate`, `admin_id`, `time_created`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$sessionId, $saleNo, $userId, $contactId, $customerLabel,
		$soId, (int) $inv['sales_invoice_id'], $rvNo,
		$calc['subtotal_ex'], $calc['discount_total'], $calc['vat_amount'], $total,
		$paymentMethod, $cashAmount, $cardAmount,
		$calc['kit_code'], $calc['tax_rate'], $adminId, $now,
	));
	$saleId = (int) $db->lastInsertId();

	$insLine = $db->prepare(
		'INSERT INTO `epc_pos_sale_lines`
		(`sale_id`, `line_no`, `product_source`, `product_ref`, `sku`, `barcode`, `name`, `qty`, `unit_price_ex`,
		 `line_discount_pct`, `line_discount_amt`, `line_ex_vat`, `tax_rate`, `vat_amount`, `line_total`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	);
	foreach ($calc['lines'] as $ln) {
		$insLine->execute(array(
			$saleId, (int) $ln['line_no'], $ln['product_source'], $ln['product_ref'], $ln['sku'], $ln['barcode'],
			$ln['name'], $ln['qty'], $ln['unit_price_ex'], $ln['line_discount_pct'], $ln['line_discount_amt'],
			$ln['line_ex_vat'], $ln['tax_rate'], $ln['vat_amount'], $ln['line_total'],
		));
	}

	require_once __DIR__ . '/../finance/epc_erp_audit.php';
	epc_erp_audit_log($db, 'pos_sale', 'pos_sale', $saleId, 'POS sale completed', array('sale_no' => $saleNo));

	return array(
		'sale_id' => $saleId,
		'sale_no' => $saleNo,
		'sales_order_id' => $soId,
		'sales_invoice_id' => (int) $inv['sales_invoice_id'],
		'invoice_number' => (string) $inv['invoice_number'],
		'receipt_voucher_no' => $rvNo,
		'totals' => $calc,
		'payment_method' => $paymentMethod,
		'customer_label' => $customerLabel,
		'time_created' => $now,
	);
}

function epc_pos_get_sale(PDO $db, int $saleId): ?array
{
	epc_pos_ensure_schema($db);
	$st = $db->prepare(
		'SELECT s.*, d.`invoice_number`
		 FROM `epc_pos_sales` s
		 LEFT JOIN `epc_einvoice_documents` d ON d.`id` = s.`sales_invoice_id`
		 WHERE s.`id` = ? LIMIT 1'
	);
	$st->execute(array($saleId));
	$sale = $st->fetch(PDO::FETCH_ASSOC);
	if (!$sale) {
		return null;
	}
	$lnSt = $db->prepare('SELECT * FROM `epc_pos_sale_lines` WHERE `sale_id` = ? ORDER BY `line_no`');
	$lnSt->execute(array($saleId));
	$sale['lines'] = $lnSt->fetchAll(PDO::FETCH_ASSOC);
	return $sale;
}

function epc_pos_receipt_html(PDO $db, int $saleId): string
{
	$sale = epc_pos_get_sale($db, $saleId);
	if (!$sale) {
		return '<p>Sale not found</p>';
	}
	$settings = epc_pos_get_settings($db);
	$header = trim((string) ($settings['receipt_header'] ?? ''));
	$footer = trim((string) ($settings['receipt_footer'] ?? 'Thank you for your purchase'));
	$taxLabel = 'VAT';
	try {
		$ctx = epc_tax_toolkit_resolve($db, (int) $sale['customer_user_id'], (int) $sale['contact_id']);
		$taxLabel = (string) ($ctx['tax_label'] ?? 'VAT');
	} catch (Exception $e) {
	}

	ob_start();
	?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>Receipt <?php echo epc_pos_h($sale['sale_no']); ?></title>
<style>
@media print { .no-print { display: none !important; } body { margin: 0; } }
body { font-family: 'Segoe UI', Arial, sans-serif; max-width: 320px; margin: 16px auto; color: #111; font-size: 13px; }
h1 { font-size: 16px; margin: 0 0 4px; text-align: center; }
.meta { text-align: center; color: #555; margin-bottom: 12px; font-size: 11px; }
table { width: 100%; border-collapse: collapse; }
td { padding: 4px 0; vertical-align: top; }
.qty { width: 28px; text-align: right; padding-right: 6px; color: #555; }
.amt { text-align: right; white-space: nowrap; }
hr { border: none; border-top: 1px dashed #999; margin: 8px 0; }
.total-row td { font-weight: 700; font-size: 14px; padding-top: 6px; }
.btn { display: inline-block; padding: 10px 18px; background: #2563eb; color: #fff; border: none; border-radius: 8px; cursor: pointer; font-size: 14px; }
.center { text-align: center; margin: 12px 0; }
.item-name { font-weight: 600; }
.disc { color: #b45309; font-size: 11px; }
</style>
</head>
<body>
<div class="no-print center">
	<button class="btn" onclick="window.print()">Print receipt</button>
</div>
<?php if ($header !== ''): ?><div class="meta"><?php echo nl2br(epc_pos_h($header)); ?></div><?php endif; ?>
<h1>Receipt</h1>
<div class="meta">
	<strong><?php echo epc_pos_h($sale['sale_no']); ?></strong><br>
	<?php echo epc_pos_h(date('Y-m-d H:i', (int) $sale['time_created'])); ?><br>
	<?php echo epc_pos_h($sale['customer_label']); ?>
</div>
<table>
<?php foreach ($sale['lines'] as $ln): ?>
<tr>
	<td class="qty"><?php echo epc_pos_h(rtrim(rtrim(number_format((float) $ln['qty'], 3, '.', ''), '0'), '.')); ?>×</td>
	<td>
		<div class="item-name"><?php echo epc_pos_h($ln['name']); ?></div>
		<?php if ((float) $ln['line_discount_amt'] > 0): ?>
		<div class="disc">−<?php echo number_format((float) $ln['line_discount_amt'], 2); ?> discount</div>
		<?php endif; ?>
	</td>
	<td class="amt"><?php echo number_format((float) $ln['line_total'], 2); ?></td>
</tr>
<?php endforeach; ?>
</table>
<hr>
<table>
<?php if ((float) $sale['discount_total'] > 0): ?>
<tr><td>Discount</td><td class="amt">−<?php echo number_format((float) $sale['discount_total'], 2); ?></td></tr>
<?php endif; ?>
<tr><td>Subtotal ex <?php echo epc_pos_h($taxLabel); ?></td><td class="amt"><?php echo number_format((float) $sale['subtotal_ex'] - (float) $sale['discount_total'], 2); ?></td></tr>
<tr><td><?php echo epc_pos_h($taxLabel); ?> (<?php echo number_format((float) $sale['tax_rate'], 1); ?>%)</td><td class="amt"><?php echo number_format((float) $sale['vat_amount'], 2); ?></td></tr>
<tr class="total-row"><td>Total</td><td class="amt"><?php echo number_format((float) $sale['total_amount'], 2); ?></td></tr>
<tr><td>Payment</td><td class="amt"><?php echo epc_pos_h(ucfirst((string) $sale['payment_method'])); ?></td></tr>
</table>
<?php if (!empty($sale['invoice_number'])): ?>
<div class="meta">Invoice: <?php echo epc_pos_h($sale['invoice_number'] ?? ''); ?></div>
<?php endif; ?>
<div class="meta"><?php echo nl2br(epc_pos_h($footer)); ?></div>
</body>
</html>
	<?php
	return (string) ob_get_clean();
}

function epc_pos_session_summary(PDO $db, int $sessionId): array
{
	epc_pos_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_pos_sessions` WHERE `id` = ? LIMIT 1');
	$st->execute(array($sessionId));
	$sess = $st->fetch(PDO::FETCH_ASSOC);
	if (!$sess) {
		return array();
	}
	$salesSt = $db->prepare(
		'SELECT COUNT(*) AS cnt, COALESCE(SUM(`total_amount`),0) AS total,
		        COALESCE(SUM(`cash_amount`),0) AS cash, COALESCE(SUM(`card_amount`),0) AS card
		 FROM `epc_pos_sales` WHERE `session_id` = ? AND `status` = \'completed\''
	);
	$salesSt->execute(array($sessionId));
	$s = $salesSt->fetch(PDO::FETCH_ASSOC);
	return array_merge($sess, array(
		'sales_count' => (int) ($s['cnt'] ?? 0),
		'sales_total' => round((float) ($s['total'] ?? 0), 2),
		'cash_total' => round((float) ($s['cash'] ?? 0), 2),
		'card_total' => round((float) ($s['card'] ?? 0), 2),
	));
}

function epc_pos_dashboard_stats(PDO $db): array
{
	epc_pos_ensure_schema($db);
	$today = strtotime('today');
	$week = strtotime('-7 days');
	$stats = array(
		'today_sales' => 0,
		'today_total' => 0.0,
		'week_sales' => 0,
		'week_total' => 0.0,
		'open_session' => epc_pos_get_open_session($db),
	);
	try {
		$st = $db->prepare('SELECT COUNT(*) AS c, COALESCE(SUM(`total_amount`),0) AS t FROM `epc_pos_sales` WHERE `time_created` >= ? AND `status` = \'completed\'');
		$st->execute(array($today));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		$stats['today_sales'] = (int) ($row['c'] ?? 0);
		$stats['today_total'] = round((float) ($row['t'] ?? 0), 2);
		$st->execute(array($week));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		$stats['week_sales'] = (int) ($row['c'] ?? 0);
		$stats['week_total'] = round((float) ($row['t'] ?? 0), 2);
	} catch (Exception $e) {
	}
	return $stats;
}
