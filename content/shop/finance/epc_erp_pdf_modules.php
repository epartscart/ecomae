<?php
/**
 * Advanced ERP — D365 F&O-style structural / master-data modules.
 *
 * Implements the module + sub-module set requested by the client (PDF):
 *   - Business Unit (business units, class units, financial dimensions,
 *     legal entities, cost centres, listing)
 *   - Account Payable / Account Receivable setup (methods & terms of payment,
 *     vendor / customer groups)
 *   - Budgeting (account-wise + monthly + master budget)
 *   - Bank parameters & cheque register
 *   - Inventory groups
 *   - Retail barcode formats
 *   - Listing (resource listing attached to vouchers)
 *
 * Everything is additive (`epc_erp_pm_*` tables only) and strictly per-tenant —
 * every row lives in the calling tenant's own database. Nothing is hard-coded:
 * groups, dimensions, payment methods/terms, budgets and barcode formats are
 * all configurable rows, so the same code serves any industry / country / tenant.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_extended.php';

/**
 * Registry of simple "code + name" master tables: table => [columns].
 * Used by the generic list/save/toggle helpers; the table name is always
 * validated against this whitelist so it can never be attacker-controlled.
 *
 * @return array<string, array<int,string>>
 */
function epc_erp_pm_registry(): array
{
	return array(
		'epc_erp_pm_business_units' => array('code', 'name', 'legal_entity_id', 'parent_id', 'manager', 'note'),
		'epc_erp_pm_class_units'    => array('code', 'name', 'class_type', 'note'),
		'epc_erp_pm_legal_entities' => array('code', 'name', 'country_code', 'currency_code', 'trn', 'note'),
		'epc_erp_pm_dimensions'     => array('code', 'name', 'dim_type', 'note'),
		'epc_erp_pm_dimension_values' => array('dimension_id', 'code', 'name', 'note'),
		'epc_erp_pm_vendor_groups'  => array('code', 'name', 'terms_id', 'note'),
		'epc_erp_pm_customer_groups' => array('code', 'name', 'terms_id', 'note'),
		'epc_erp_pm_pay_methods'    => array('code', 'name', 'method_type', 'account_code', 'note'),
		'epc_erp_pm_pay_terms'      => array('code', 'name', 'net_days', 'note'),
		'epc_erp_pm_inv_groups'     => array('code', 'name', 'valuation', 'note'),
		'epc_erp_pm_barcode_formats' => array('code', 'name', 'symbology', 'pattern', 'note'),
	);
}

/** Create all module tables (idempotent). */
function epc_erp_pm_ensure_schema(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_legal_entities` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `country_code` varchar(4) NOT NULL DEFAULT '',
		`currency_code` varchar(8) NOT NULL DEFAULT '', `trn` varchar(40) NOT NULL DEFAULT '',
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_business_units` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `legal_entity_id` int(11) NOT NULL DEFAULT 0,
		`parent_id` int(11) NOT NULL DEFAULT 0, `manager` varchar(120) NOT NULL DEFAULT '',
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_class_units` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `class_type` varchar(60) NOT NULL DEFAULT '',
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_dimensions` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `dim_type` varchar(60) NOT NULL DEFAULT '',
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_dimension_values` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `dimension_id` int(11) NOT NULL DEFAULT 0,
		`code` varchar(40) NOT NULL DEFAULT '', `name` varchar(190) NOT NULL DEFAULT '',
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`), KEY `dim` (`dimension_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_vendor_groups` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `terms_id` int(11) NOT NULL DEFAULT 0,
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_customer_groups` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `terms_id` int(11) NOT NULL DEFAULT 0,
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_pay_methods` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `method_type` varchar(40) NOT NULL DEFAULT '',
		`account_code` varchar(40) NOT NULL DEFAULT '', `note` varchar(255) NOT NULL DEFAULT '',
		`active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_pay_terms` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `net_days` int(11) NOT NULL DEFAULT 0,
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_inv_groups` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `valuation` varchar(30) NOT NULL DEFAULT 'weighted_avg',
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_barcode_formats` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `symbology` varchar(30) NOT NULL DEFAULT 'CODE128',
		`pattern` varchar(120) NOT NULL DEFAULT '', `note` varchar(255) NOT NULL DEFAULT '',
		`active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	// Budgets (header + monthly lines)
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_budgets` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '',
		`name` varchar(190) NOT NULL DEFAULT '', `fiscal_year` varchar(9) NOT NULL DEFAULT '',
		`business_unit_id` int(11) NOT NULL DEFAULT 0, `is_master` tinyint(1) NOT NULL DEFAULT 0,
		`note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_budget_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `budget_id` int(11) NOT NULL DEFAULT 0,
		`account_code` varchar(40) NOT NULL DEFAULT '', `account_name` varchar(190) NOT NULL DEFAULT '',
		`month_no` tinyint(4) NOT NULL DEFAULT 0, `amount` decimal(18,2) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`),
		KEY `b` (`budget_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	// Cheque register
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_cheques` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `bank_account_id` int(11) NOT NULL DEFAULT 0,
		`cheque_no` varchar(40) NOT NULL DEFAULT '', `pay_to` varchar(190) NOT NULL DEFAULT '',
		`amount` decimal(18,2) NOT NULL DEFAULT 0, `cheque_date` int(11) NOT NULL DEFAULT 0,
		`memo` varchar(255) NOT NULL DEFAULT '', `status` varchar(20) NOT NULL DEFAULT 'printed',
		`time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");
	// Listing (resource listing, attachable to a voucher)
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_pm_listings` (
		`id` int(11) NOT NULL AUTO_INCREMENT, `ref_no` varchar(40) NOT NULL DEFAULT '',
		`resource_type` varchar(40) NOT NULL DEFAULT '', `title` varchar(190) NOT NULL DEFAULT '',
		`description` text, `qty` decimal(18,3) NOT NULL DEFAULT 0, `rate` decimal(18,2) NOT NULL DEFAULT 0,
		`amount` decimal(18,2) NOT NULL DEFAULT 0, `voucher_ref` varchar(60) NOT NULL DEFAULT '',
		`status` varchar(20) NOT NULL DEFAULT 'draft', `active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	epc_erp_pm_seed_defaults($db);
	$done = true;
}

/** Seed sensible, editable defaults once (so the screens aren't empty). */
function epc_erp_pm_seed_defaults(PDO $db): void
{
	$now = time();
	$seedIf = function (string $table, string $checkSql, array $rows) use ($db, $now) {
		if ((int) $db->query($checkSql)->fetchColumn() > 0) {
			return;
		}
		foreach ($rows as $r) {
			$cols = array_keys($r);
			$ph = implode(',', array_fill(0, count($cols), '?'));
			$sql = 'INSERT INTO `' . $table . '` (`' . implode('`,`', $cols) . '`,`active`,`time_created`,`time_updated`) VALUES (' . $ph . ',1,?,?)';
			$vals = array_values($r);
			$vals[] = $now;
			$vals[] = $now;
			$db->prepare($sql)->execute($vals);
		}
	};
	$seedIf('epc_erp_pm_pay_terms', "SELECT COUNT(*) FROM `epc_erp_pm_pay_terms`", array(
		array('code' => 'NET0', 'name' => 'Due on receipt', 'net_days' => 0),
		array('code' => 'NET15', 'name' => 'Net 15 days', 'net_days' => 15),
		array('code' => 'NET30', 'name' => 'Net 30 days', 'net_days' => 30),
		array('code' => 'NET60', 'name' => 'Net 60 days', 'net_days' => 60),
	));
	$seedIf('epc_erp_pm_pay_methods', "SELECT COUNT(*) FROM `epc_erp_pm_pay_methods`", array(
		array('code' => 'CASH', 'name' => 'Cash', 'method_type' => 'cash', 'account_code' => '1010'),
		array('code' => 'BANK', 'name' => 'Bank transfer', 'method_type' => 'bank', 'account_code' => '1020'),
		array('code' => 'CHEQUE', 'name' => 'Cheque', 'method_type' => 'cheque', 'account_code' => '1020'),
		array('code' => 'CARD', 'name' => 'Credit card', 'method_type' => 'card', 'account_code' => '1030'),
	));
	$seedIf('epc_erp_pm_vendor_groups', "SELECT COUNT(*) FROM `epc_erp_pm_vendor_groups`", array(
		array('code' => 'LOCAL', 'name' => 'Local suppliers', 'terms_id' => 0),
		array('code' => 'IMPORT', 'name' => 'Import / overseas', 'terms_id' => 0),
	));
	$seedIf('epc_erp_pm_customer_groups', "SELECT COUNT(*) FROM `epc_erp_pm_customer_groups`", array(
		array('code' => 'RETAIL', 'name' => 'Retail customers', 'terms_id' => 0),
		array('code' => 'WHOLESALE', 'name' => 'Wholesale / B2B', 'terms_id' => 0),
		array('code' => 'VIP', 'name' => 'Key accounts', 'terms_id' => 0),
	));
	$seedIf('epc_erp_pm_dimensions', "SELECT COUNT(*) FROM `epc_erp_pm_dimensions`", array(
		array('code' => 'DEPT', 'name' => 'Department', 'dim_type' => 'department'),
		array('code' => 'PROJECT', 'name' => 'Project', 'dim_type' => 'project'),
		array('code' => 'COSTCENTER', 'name' => 'Cost centre', 'dim_type' => 'cost_center'),
	));
	$seedIf('epc_erp_pm_inv_groups', "SELECT COUNT(*) FROM `epc_erp_pm_inv_groups`", array(
		array('code' => 'FG', 'name' => 'Finished goods', 'valuation' => 'weighted_avg'),
		array('code' => 'RM', 'name' => 'Raw materials', 'valuation' => 'weighted_avg'),
		array('code' => 'TRADE', 'name' => 'Trading items', 'valuation' => 'fifo'),
	));
	$seedIf('epc_erp_pm_barcode_formats', "SELECT COUNT(*) FROM `epc_erp_pm_barcode_formats`", array(
		array('code' => 'SKU128', 'name' => 'SKU (Code 128)', 'symbology' => 'CODE128', 'pattern' => '{SKU}'),
		array('code' => 'EAN13', 'name' => 'Retail EAN-13', 'symbology' => 'EAN13', 'pattern' => '{SKU13}'),
		array('code' => 'PRICE', 'name' => 'SKU + price tag', 'symbology' => 'CODE128', 'pattern' => '{SKU}-{PRICE}'),
	));
}

/** Generic list for a registered master table. */
function epc_erp_pm_list(PDO $db, string $table, bool $activeOnly = false): array
{
	epc_erp_pm_ensure_schema($db);
	if (!array_key_exists($table, epc_erp_pm_registry())) {
		throw new Exception('Unknown master table');
	}
	$sql = 'SELECT * FROM `' . $table . '`';
	if ($activeOnly) {
		$sql .= ' WHERE `active` = 1';
	}
	$sql .= ' ORDER BY `code`, `id`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/** Generic insert/update for a registered master table. Returns row id. */
function epc_erp_pm_save(PDO $db, string $table, array $data): int
{
	epc_erp_pm_ensure_schema($db);
	$reg = epc_erp_pm_registry();
	if (!isset($reg[$table])) {
		throw new Exception('Unknown master table');
	}
	$cols = $reg[$table];
	$id = (int) ($data['id'] ?? 0);
	$now = time();
	$set = array();
	$vals = array();
	foreach ($cols as $c) {
		if (array_key_exists($c, $data)) {
			$set[] = '`' . $c . '` = ?';
			$vals[] = is_string($data[$c]) ? trim($data[$c]) : $data[$c];
		}
	}
	if (empty($set)) {
		throw new Exception('Nothing to save');
	}
	if ($id > 0) {
		$set[] = '`time_updated` = ?';
		$vals[] = $now;
		$vals[] = $id;
		$db->prepare('UPDATE `' . $table . '` SET ' . implode(',', $set) . ' WHERE `id` = ?')->execute($vals);
		return $id;
	}
	$set[] = '`active` = 1';
	$set[] = '`time_created` = ' . $now;
	$set[] = '`time_updated` = ' . $now;
	$db->prepare('INSERT INTO `' . $table . '` SET ' . implode(',', $set))->execute($vals);
	return (int) $db->lastInsertId();
}

/** Toggle active flag on a registered master table. */
function epc_erp_pm_toggle(PDO $db, string $table, int $id, int $active): void
{
	epc_erp_pm_ensure_schema($db);
	if (!array_key_exists($table, epc_erp_pm_registry())) {
		throw new Exception('Unknown master table');
	}
	$db->prepare('UPDATE `' . $table . '` SET `active` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array($active ? 1 : 0, time(), $id));
}

/* ----------------------------- Budgeting ----------------------------- */

/** @return array<int,array<string,mixed>> budget headers with line totals. */
function epc_erp_pm_budgets_list(PDO $db): array
{
	epc_erp_pm_ensure_schema($db);
	$sql = 'SELECT b.*, COALESCE(SUM(l.`amount`),0) AS total_amount,
			(SELECT `name` FROM `epc_erp_pm_business_units` WHERE `id` = b.`business_unit_id`) AS bu_name
		FROM `epc_erp_pm_budgets` b
		LEFT JOIN `epc_erp_pm_budget_lines` l ON l.`budget_id` = b.`id`
		WHERE b.`active` = 1 GROUP BY b.`id` ORDER BY b.`fiscal_year` DESC, b.`code`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_erp_pm_budget_save(PDO $db, array $data): int
{
	epc_erp_pm_ensure_schema($db);
	$now = time();
	$id = (int) ($data['id'] ?? 0);
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_pm_budgets` SET `code`=?,`name`=?,`fiscal_year`=?,`business_unit_id`=?,`is_master`=?,`note`=?,`time_updated`=? WHERE `id`=?')
			->execute(array(trim((string) ($data['code'] ?? '')), trim((string) ($data['name'] ?? '')), trim((string) ($data['fiscal_year'] ?? '')), (int) ($data['business_unit_id'] ?? 0), !empty($data['is_master']) ? 1 : 0, trim((string) ($data['note'] ?? '')), $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_pm_budgets` (`code`,`name`,`fiscal_year`,`business_unit_id`,`is_master`,`note`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,1,?,?)')
		->execute(array(trim((string) ($data['code'] ?? '')), trim((string) ($data['name'] ?? '')), trim((string) ($data['fiscal_year'] ?? '')), (int) ($data['business_unit_id'] ?? 0), !empty($data['is_master']) ? 1 : 0, trim((string) ($data['note'] ?? '')), $now, $now));
	return (int) $db->lastInsertId();
}

/** Add/update one account-wise budget line (account × month × amount). */
function epc_erp_pm_budget_line_save(PDO $db, array $data): int
{
	epc_erp_pm_ensure_schema($db);
	$db->prepare('INSERT INTO `epc_erp_pm_budget_lines` (`budget_id`,`account_code`,`account_name`,`month_no`,`amount`,`time_updated`) VALUES (?,?,?,?,?,?)')
		->execute(array((int) ($data['budget_id'] ?? 0), trim((string) ($data['account_code'] ?? '')), trim((string) ($data['account_name'] ?? '')), (int) ($data['month_no'] ?? 0), (float) ($data['amount'] ?? 0), time()));
	return (int) $db->lastInsertId();
}

/** Budget lines for one budget, with monthly pivot. */
function epc_erp_pm_budget_lines(PDO $db, int $budgetId): array
{
	epc_erp_pm_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_erp_pm_budget_lines` WHERE `budget_id` = ? ORDER BY `account_code`, `month_no`');
	$st->execute(array($budgetId));
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ----------------------------- Cheques ----------------------------- */

function epc_erp_pm_cheque_save(PDO $db, array $data): int
{
	epc_erp_pm_ensure_schema($db);
	$date = !empty($data['cheque_date']) ? strtotime((string) $data['cheque_date']) : time();
	$db->prepare('INSERT INTO `epc_erp_pm_cheques` (`bank_account_id`,`cheque_no`,`pay_to`,`amount`,`cheque_date`,`memo`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['bank_account_id'] ?? 0), trim((string) ($data['cheque_no'] ?? '')), trim((string) ($data['pay_to'] ?? '')), (float) ($data['amount'] ?? 0), $date ?: time(), trim((string) ($data['memo'] ?? '')), 'printed', time()));
	return (int) $db->lastInsertId();
}

function epc_erp_pm_cheques_list(PDO $db, int $limit = 100): array
{
	epc_erp_pm_ensure_schema($db);
	return $db->query('SELECT * FROM `epc_erp_pm_cheques` ORDER BY `id` DESC LIMIT ' . (int) $limit)->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ----------------------------- Listing ----------------------------- */

function epc_erp_pm_listing_save(PDO $db, array $data): int
{
	epc_erp_pm_ensure_schema($db);
	$now = time();
	$qty = (float) ($data['qty'] ?? 0);
	$rate = (float) ($data['rate'] ?? 0);
	$amount = $qty * $rate;
	$id = (int) ($data['id'] ?? 0);
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_pm_listings` SET `resource_type`=?,`title`=?,`description`=?,`qty`=?,`rate`=?,`amount`=?,`voucher_ref`=?,`status`=?,`time_updated`=? WHERE `id`=?')
			->execute(array(trim((string) ($data['resource_type'] ?? '')), trim((string) ($data['title'] ?? '')), (string) ($data['description'] ?? ''), $qty, $rate, $amount, trim((string) ($data['voucher_ref'] ?? '')), trim((string) ($data['status'] ?? 'draft')), $now, $id));
		return $id;
	}
	$ref = 'LST-' . date('Y') . '-' . str_pad((string) (epc_erp_pm_next_listing_seq($db)), 5, '0', STR_PAD_LEFT);
	$db->prepare('INSERT INTO `epc_erp_pm_listings` (`ref_no`,`resource_type`,`title`,`description`,`qty`,`rate`,`amount`,`voucher_ref`,`status`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,1,?,?)')
		->execute(array($ref, trim((string) ($data['resource_type'] ?? '')), trim((string) ($data['title'] ?? '')), (string) ($data['description'] ?? ''), $qty, $rate, $amount, trim((string) ($data['voucher_ref'] ?? '')), trim((string) ($data['status'] ?? 'draft')), $now, $now));
	return (int) $db->lastInsertId();
}

function epc_erp_pm_next_listing_seq(PDO $db): int
{
	$n = (int) $db->query("SELECT COUNT(*) FROM `epc_erp_pm_listings` WHERE YEAR(FROM_UNIXTIME(`time_created`)) = " . (int) date('Y'))->fetchColumn();
	return $n + 1;
}

function epc_erp_pm_listings_list(PDO $db, int $limit = 100): array
{
	epc_erp_pm_ensure_schema($db);
	return $db->query('SELECT * FROM `epc_erp_pm_listings` WHERE `active` = 1 ORDER BY `id` DESC LIMIT ' . (int) $limit)->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/** Attach a listing to a voucher reference. */
function epc_erp_pm_listing_attach(PDO $db, int $id, string $voucherRef): void
{
	epc_erp_pm_ensure_schema($db);
	$db->prepare('UPDATE `epc_erp_pm_listings` SET `voucher_ref` = ?, `status` = ?, `time_updated` = ? WHERE `id` = ?')
		->execute(array(trim($voucherRef), 'attached', time(), $id));
}
