<?php
/**
 * ERP — fixed assets register, depreciation methods, book value.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';

function epc_erp_fa_depreciation_methods()
{
	return array(
		'straight_line' => 'Straight line',
		'declining_balance' => 'Declining balance',
		'double_declining' => 'Double declining balance',
		'units_of_production' => 'Units of production',
	);
}

function epc_erp_fa_ensure_schema(PDO $db)
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fa_categories` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`code` varchar(32) NOT NULL,
		`name` varchar(255) NOT NULL,
		`default_method` varchar(32) NOT NULL DEFAULT 'straight_line',
		`default_life_months` int(11) NOT NULL DEFAULT 60,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_code` (`code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fa_assets` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`asset_code` varchar(64) NOT NULL,
		`name` varchar(255) NOT NULL,
		`category_id` int(11) NOT NULL DEFAULT 0,
		`acquisition_date` date NOT NULL,
		`cost` decimal(14,2) NOT NULL DEFAULT 0.00,
		`salvage_value` decimal(14,2) NOT NULL DEFAULT 0.00,
		`useful_life_months` int(11) NOT NULL DEFAULT 60,
		`depreciation_method` varchar(32) NOT NULL DEFAULT 'straight_line',
		`accumulated_depreciation` decimal(14,2) NOT NULL DEFAULT 0.00,
		`book_value` decimal(14,2) NOT NULL DEFAULT 0.00,
		`location` varchar(255) DEFAULT NULL,
		`tracking_id` varchar(128) DEFAULT NULL,
		`serial_no` varchar(128) DEFAULT NULL,
		`status` enum('active','disposed','fully_depreciated') NOT NULL DEFAULT 'active',
		`opening_batch_id` int(11) NOT NULL DEFAULT 0,
		`note` text,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_code` (`asset_code`),
		KEY `x_cat` (`category_id`),
		KEY `x_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fa_depreciation_runs` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`period_month` char(7) NOT NULL,
		`run_date` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`note` varchar(255) DEFAULT NULL,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_period` (`period_month`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fa_depreciation_lines` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`run_id` int(11) NOT NULL,
		`asset_id` int(11) NOT NULL,
		`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
		`accumulated_after` decimal(14,2) NOT NULL DEFAULT 0.00,
		`book_value_after` decimal(14,2) NOT NULL DEFAULT 0.00,
		PRIMARY KEY (`id`),
		KEY `x_run` (`run_id`),
		KEY `x_asset` (`asset_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_fa_tracking_log` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`asset_id` int(11) NOT NULL,
		`event_type` varchar(32) NOT NULL,
		`location` varchar(255) DEFAULT NULL,
		`note` text,
		`event_date` int(11) NOT NULL DEFAULT 0,
		`admin_id` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_asset` (`asset_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8");

	epc_erp_fa_seed_categories($db);
}

function epc_erp_fa_seed_categories(PDO $db)
{
	if ((int) $db->query('SELECT COUNT(*) FROM `epc_erp_fa_categories`')->fetchColumn() > 0) {
		return;
	}
	$cats = array(
		array('VEH', 'Vehicles', 'straight_line', 60),
		array('IT', 'IT equipment', 'declining_balance', 36),
		array('FURN', 'Furniture & fixtures', 'straight_line', 84),
		array('BLDG', 'Buildings', 'straight_line', 240),
	);
	$ins = $db->prepare('INSERT INTO `epc_erp_fa_categories` (`code`,`name`,`default_method`,`default_life_months`) VALUES (?,?,?,?)');
	foreach ($cats as $c) {
		$ins->execute($c);
	}
}

function epc_erp_fa_refresh_book_value(PDO $db, $assetId)
{
	$st = $db->prepare('SELECT `cost`, `accumulated_depreciation`, `salvage_value` FROM `epc_erp_fa_assets` WHERE `id` = ? LIMIT 1');
	$st->execute(array((int) $assetId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}
	$bv = max((float) $row['salvage_value'], (float) $row['cost'] - (float) $row['accumulated_depreciation']);
	$db->prepare('UPDATE `epc_erp_fa_assets` SET `book_value` = ? WHERE `id` = ?')->execute(array(round($bv, 2), (int) $assetId));
}

function epc_erp_fa_create_asset(PDO $db, array $data)
{
	$code = trim((string) ($data['asset_code'] ?? ''));
	$name = trim((string) ($data['name'] ?? ''));
	$cost = round((float) ($data['cost'] ?? 0), 2);
	if ($code === '' || $name === '' || $cost <= 0) {
		throw new Exception('Asset code, name and cost required');
	}
	$method = (string) ($data['depreciation_method'] ?? 'straight_line');
	if (!isset(epc_erp_fa_depreciation_methods()[$method])) {
		$method = 'straight_line';
	}
	$acq = !empty($data['acquisition_date']) ? date('Y-m-d', strtotime((string) $data['acquisition_date'])) : date('Y-m-d');
	$openAccum = round((float) ($data['accumulated_depreciation'] ?? 0), 2);
	$db->prepare(
		'INSERT INTO `epc_erp_fa_assets`
		(`asset_code`,`name`,`category_id`,`acquisition_date`,`cost`,`salvage_value`,`useful_life_months`,`depreciation_method`,
		`accumulated_depreciation`,`book_value`,`location`,`tracking_id`,`serial_no`,`opening_batch_id`,`note`,`time_created`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$code, $name, (int) ($data['category_id'] ?? 0), $acq, $cost,
		round((float) ($data['salvage_value'] ?? 0), 2),
		max(1, (int) ($data['useful_life_months'] ?? 60)),
		$method, $openAccum, max(0, $cost - $openAccum),
		trim((string) ($data['location'] ?? '')),
		trim((string) ($data['tracking_id'] ?? '')),
		trim((string) ($data['serial_no'] ?? '')),
		(int) ($data['opening_batch_id'] ?? 0),
		trim((string) ($data['note'] ?? '')),
		time(),
	));
	$id = (int) $db->lastInsertId();
	epc_erp_fa_refresh_book_value($db, $id);
	if (!empty($data['location'])) {
		epc_erp_fa_log_tracking($db, $id, 'registered', (string) $data['location'], 'Asset registered');
	}
	return $id;
}

function epc_erp_fa_log_tracking(PDO $db, $assetId, $eventType, $location = '', $note = '')
{
	require_once __DIR__ . '/epc_erp_inventory.php';
	$db->prepare(
		'INSERT INTO `epc_erp_fa_tracking_log` (`asset_id`,`event_type`,`location`,`note`,`event_date`,`admin_id`) VALUES (?,?,?,?,?,?)'
	)->execute(array((int) $assetId, substr($eventType, 0, 32), $location, $note, time(), epc_erp_admin_id()));
}

function epc_erp_fa_period_depreciation_amount(array $asset, $periodMonth)
{
	$cost = (float) $asset['cost'];
	$salvage = (float) $asset['salvage_value'];
	$accum = (float) $asset['accumulated_depreciation'];
	$life = max(1, (int) $asset['useful_life_months']);
	$depreciable = max(0, $cost - $salvage);
	$remaining = max(0, $depreciable - $accum);
	if ($remaining <= 0) {
		return 0.0;
	}
	$method = (string) $asset['depreciation_method'];
	$book = max($salvage, $cost - $accum);

	if ($method === 'straight_line') {
		return min($remaining, round($depreciable / $life, 2));
	}
	if ($method === 'declining_balance') {
		$rate = 2.0 / $life;
		return min($remaining, round($book * $rate, 2));
	}
	if ($method === 'double_declining') {
		$rate = 4.0 / $life;
		return min($remaining, round($book * $rate, 2));
	}
	if ($method === 'units_of_production') {
		return min($remaining, round($depreciable / $life, 2));
	}
	return min($remaining, round($depreciable / $life, 2));
}

function epc_erp_fa_run_depreciation(PDO $db, $periodMonth, $note = '')
{
	$periodMonth = preg_replace('/[^0-9\-]/', '', (string) $periodMonth);
	if (strlen($periodMonth) !== 7) {
		throw new Exception('Period must be YYYY-MM');
	}
	$chk = $db->prepare('SELECT `id` FROM `epc_erp_fa_depreciation_runs` WHERE `period_month` = ? LIMIT 1');
	$chk->execute(array($periodMonth));
	if ($chk->fetch()) {
		throw new Exception('Depreciation already posted for ' . $periodMonth);
	}
	$assets = $db->query("SELECT * FROM `epc_erp_fa_assets` WHERE `status` = 'active'")->fetchAll(PDO::FETCH_ASSOC);
	$total = 0;
	$db->beginTransaction();
	try {
		require_once __DIR__ . '/epc_erp_inventory.php';
		$db->prepare(
			'INSERT INTO `epc_erp_fa_depreciation_runs` (`period_month`,`run_date`,`admin_id`,`note`) VALUES (?,?,?,?)'
		)->execute(array($periodMonth, time(), epc_erp_admin_id(), $note));
		$runId = (int) $db->lastInsertId();
		$lineIns = $db->prepare(
			'INSERT INTO `epc_erp_fa_depreciation_lines` (`run_id`,`asset_id`,`amount`,`accumulated_after`,`book_value_after`) VALUES (?,?,?,?,?)'
		);
		foreach ($assets as $a) {
			$amt = epc_erp_fa_period_depreciation_amount($a, $periodMonth);
			if ($amt <= 0) {
				continue;
			}
			$newAccum = round((float) $a['accumulated_depreciation'] + $amt, 2);
			$newBv = max((float) $a['salvage_value'], (float) $a['cost'] - $newAccum);
			$db->prepare('UPDATE `epc_erp_fa_assets` SET `accumulated_depreciation` = ?, `book_value` = ? WHERE `id` = ?')
				->execute(array($newAccum, $newBv, (int) $a['id']));
			$lineIns->execute(array($runId, (int) $a['id'], $amt, $newAccum, $newBv));
			$total += $amt;
			if ($newBv <= (float) $a['salvage_value'] + 0.01) {
				$db->prepare("UPDATE `epc_erp_fa_assets` SET `status` = 'fully_depreciated' WHERE `id` = ?")->execute(array((int) $a['id']));
			}
		}
		$db->prepare('UPDATE `epc_erp_fa_depreciation_runs` SET `total_amount` = ? WHERE `id` = ?')->execute(array(round($total, 2), $runId));
		$db->commit();
		return array('run_id' => $runId, 'total' => round($total, 2), 'assets' => count($assets));
	} catch (Exception $e) {
		$db->rollBack();
		throw $e;
	}
}

function epc_erp_fa_list_assets(PDO $db, $limit = 500)
{
	$limit = max(1, min(2000, (int) $limit));
	return $db->query(
		'SELECT a.*, c.name AS category_name FROM `epc_erp_fa_assets` a
		LEFT JOIN `epc_erp_fa_categories` c ON c.id = a.category_id
		ORDER BY a.asset_code LIMIT ' . $limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_fa_summary(PDO $db)
{
	$row = $db->query(
		"SELECT COUNT(*) AS cnt, SUM(`cost`) AS total_cost, SUM(`accumulated_depreciation`) AS total_accum, SUM(`book_value`) AS total_book
		FROM `epc_erp_fa_assets` WHERE `status` IN ('active','fully_depreciated')"
	)->fetch(PDO::FETCH_ASSOC);
	return array(
		'count' => (int) ($row['cnt'] ?? 0),
		'total_cost' => round((float) ($row['total_cost'] ?? 0), 2),
		'total_accumulated' => round((float) ($row['total_accum'] ?? 0), 2),
		'total_book_value' => round((float) ($row['total_book'] ?? 0), 2),
	);
}
