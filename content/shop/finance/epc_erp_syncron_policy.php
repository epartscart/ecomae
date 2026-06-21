<?php
/**
 * Syncron-style inventory policy settings engine.
 *
 * Safety stock, reorder point, service level, lead time, demand forecast —
 * per-item or per-category policy driven by tenant country compliance.
 */
defined('_ASTEXE_') or die('No access');

/* ── schema ── */
function epc_syncron_policy_ensure_schema(PDO $db)
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_inv_policies` (
		`id`                INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`scope`             ENUM("global","category","item") NOT NULL DEFAULT "global",
		`scope_ref`         VARCHAR(120) NOT NULL DEFAULT "",
		`policy_name`       VARCHAR(180) NOT NULL DEFAULT "",
		`safety_stock_qty`  DECIMAL(14,4) NOT NULL DEFAULT 0,
		`reorder_point`     DECIMAL(14,4) NOT NULL DEFAULT 0,
		`reorder_qty`       DECIMAL(14,4) NOT NULL DEFAULT 0,
		`max_stock_qty`     DECIMAL(14,4) NOT NULL DEFAULT 0,
		`service_level_pct` DECIMAL(5,2) NOT NULL DEFAULT 95.00,
		`lead_time_days`    SMALLINT UNSIGNED NOT NULL DEFAULT 7,
		`review_period_days` SMALLINT UNSIGNED NOT NULL DEFAULT 30,
		`demand_method`     ENUM("moving_avg","exponential","manual") NOT NULL DEFAULT "moving_avg",
		`demand_window_days` SMALLINT UNSIGNED NOT NULL DEFAULT 90,
		`demand_alpha`      DECIMAL(4,3) NOT NULL DEFAULT 0.300,
		`active`            TINYINT(1) NOT NULL DEFAULT 1,
		`created_at`        INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`        INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_scope` (`scope`, `scope_ref`),
		INDEX `idx_active` (`active`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_inv_demand_forecast` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`item_id`         INT UNSIGNED NOT NULL DEFAULT 0,
		`warehouse_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`period_start`    DATE NOT NULL,
		`period_end`      DATE NOT NULL,
		`forecast_qty`    DECIMAL(14,4) NOT NULL DEFAULT 0,
		`actual_qty`      DECIMAL(14,4) NOT NULL DEFAULT 0,
		`variance_pct`    DECIMAL(6,2) NOT NULL DEFAULT 0,
		`method`          VARCHAR(40) NOT NULL DEFAULT "moving_avg",
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_item_wh` (`item_id`, `warehouse_id`),
		INDEX `idx_period` (`period_start`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_inv_service_levels` (
		`id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`item_id`         INT UNSIGNED NOT NULL DEFAULT 0,
		`warehouse_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`period_month`    CHAR(7) NOT NULL DEFAULT "",
		`demand_qty`      DECIMAL(14,4) NOT NULL DEFAULT 0,
		`fulfilled_qty`   DECIMAL(14,4) NOT NULL DEFAULT 0,
		`stockout_events` INT UNSIGNED NOT NULL DEFAULT 0,
		`service_level`   DECIMAL(5,2) NOT NULL DEFAULT 0,
		`created_at`      INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_item_wh_month` (`item_id`, `warehouse_id`, `period_month`),
		INDEX `idx_period` (`period_month`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

/* ── policy CRUD ── */
function epc_syncron_policy_save(PDO $db, array $data)
{
	$now = time();
	$id = isset($data['id']) ? (int) $data['id'] : 0;
	if ($id > 0) {
		$sql = 'UPDATE `epc_erp_inv_policies` SET
			`scope`=?, `scope_ref`=?, `policy_name`=?,
			`safety_stock_qty`=?, `reorder_point`=?, `reorder_qty`=?, `max_stock_qty`=?,
			`service_level_pct`=?, `lead_time_days`=?, `review_period_days`=?,
			`demand_method`=?, `demand_window_days`=?, `demand_alpha`=?,
			`active`=?, `updated_at`=?
			WHERE `id`=?';
		$db->prepare($sql)->execute(array(
			$data['scope'] ?? 'global', $data['scope_ref'] ?? '', $data['policy_name'] ?? '',
			$data['safety_stock_qty'] ?? 0, $data['reorder_point'] ?? 0,
			$data['reorder_qty'] ?? 0, $data['max_stock_qty'] ?? 0,
			$data['service_level_pct'] ?? 95, $data['lead_time_days'] ?? 7,
			$data['review_period_days'] ?? 30,
			$data['demand_method'] ?? 'moving_avg', $data['demand_window_days'] ?? 90,
			$data['demand_alpha'] ?? 0.3,
			isset($data['active']) ? (int) $data['active'] : 1, $now,
			$id,
		));
		return $id;
	}
	$sql = 'INSERT INTO `epc_erp_inv_policies`
		(`scope`,`scope_ref`,`policy_name`,
		 `safety_stock_qty`,`reorder_point`,`reorder_qty`,`max_stock_qty`,
		 `service_level_pct`,`lead_time_days`,`review_period_days`,
		 `demand_method`,`demand_window_days`,`demand_alpha`,
		 `active`,`created_at`,`updated_at`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)';
	$db->prepare($sql)->execute(array(
		$data['scope'] ?? 'global', $data['scope_ref'] ?? '', $data['policy_name'] ?? '',
		$data['safety_stock_qty'] ?? 0, $data['reorder_point'] ?? 0,
		$data['reorder_qty'] ?? 0, $data['max_stock_qty'] ?? 0,
		$data['service_level_pct'] ?? 95, $data['lead_time_days'] ?? 7,
		$data['review_period_days'] ?? 30,
		$data['demand_method'] ?? 'moving_avg', $data['demand_window_days'] ?? 90,
		$data['demand_alpha'] ?? 0.3,
		isset($data['active']) ? (int) $data['active'] : 1, $now, $now,
	));
	return (int) $db->lastInsertId();
}

function epc_syncron_policy_list(PDO $db)
{
	return $db->query(
		'SELECT * FROM `epc_erp_inv_policies` WHERE `active` = 1 ORDER BY `scope` ASC, `policy_name` ASC'
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_syncron_policy_get(PDO $db, int $id)
{
	$st = $db->prepare('SELECT * FROM `epc_erp_inv_policies` WHERE `id` = ?');
	$st->execute(array($id));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_syncron_policy_for_item(PDO $db, string $sku)
{
	$st = $db->prepare(
		'SELECT * FROM `epc_erp_inv_policies`
		 WHERE `active` = 1 AND (
			(`scope` = "item" AND `scope_ref` = ?) OR
			`scope` = "global"
		 )
		 ORDER BY FIELD(`scope`, "item", "category", "global") ASC
		 LIMIT 1'
	);
	$st->execute(array($sku));
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

/* ── demand forecast engine ── */
function epc_syncron_demand_moving_avg(PDO $db, int $itemId, int $warehouseId, int $windowDays)
{
	$cutoff = date('Y-m-d', time() - ($windowDays * 86400));
	$st = $db->prepare(
		'SELECT COALESCE(SUM(ABS(`qty`)), 0) AS total_demand,
		        COUNT(DISTINCT DATE(`created_at`)) AS active_days
		 FROM `epc_erp_inv_movements`
		 WHERE `item_id` = ? AND `warehouse_id` = ? AND `movement_type` IN ("sale_out","adjustment")
		   AND `created_at` >= UNIX_TIMESTAMP(?)
		 LIMIT 1'
	);
	$st->execute(array($itemId, $warehouseId, $cutoff));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$totalDemand = (float) ($row['total_demand'] ?? 0);
	$activeDays = max((int) ($row['active_days'] ?? 1), 1);
	return round($totalDemand / $activeDays, 4);
}

function epc_syncron_demand_exponential(PDO $db, int $itemId, int $warehouseId, float $alpha, int $windowDays)
{
	$cutoff = date('Y-m-d', time() - ($windowDays * 86400));
	$st = $db->prepare(
		'SELECT DATE(FROM_UNIXTIME(`created_at`)) AS d, COALESCE(SUM(ABS(`qty`)), 0) AS qty
		 FROM `epc_erp_inv_movements`
		 WHERE `item_id` = ? AND `warehouse_id` = ? AND `movement_type` IN ("sale_out","adjustment")
		   AND `created_at` >= UNIX_TIMESTAMP(?)
		 GROUP BY d ORDER BY d ASC'
	);
	$st->execute(array($itemId, $warehouseId, $cutoff));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	if (empty($rows)) {
		return 0;
	}
	$forecast = (float) $rows[0]['qty'];
	for ($i = 1; $i < count($rows); $i++) {
		$actual = (float) $rows[$i]['qty'];
		$forecast = $alpha * $actual + (1 - $alpha) * $forecast;
	}
	return round($forecast, 4);
}

function epc_syncron_calculate_reorder_point(float $dailyDemand, int $leadTimeDays, float $safetyStock)
{
	return round($dailyDemand * $leadTimeDays + $safetyStock, 4);
}

function epc_syncron_calculate_safety_stock(float $dailyDemand, int $leadTimeDays, float $serviceLevelZ)
{
	$demandStdDev = $dailyDemand * 0.25;
	return round($serviceLevelZ * $demandStdDev * sqrt((float) $leadTimeDays), 4);
}

function epc_syncron_service_level_z(float $pct)
{
	if ($pct >= 99.9) return 3.09;
	if ($pct >= 99) return 2.33;
	if ($pct >= 98) return 2.05;
	if ($pct >= 97) return 1.88;
	if ($pct >= 96) return 1.75;
	if ($pct >= 95) return 1.65;
	if ($pct >= 90) return 1.28;
	if ($pct >= 85) return 1.04;
	if ($pct >= 80) return 0.84;
	return 0.67;
}

/* ── service level tracking ── */
function epc_syncron_record_service_level(PDO $db, int $itemId, int $warehouseId, float $demandQty, float $fulfilledQty, int $stockout)
{
	$month = date('Y-m');
	$level = $demandQty > 0 ? round(($fulfilledQty / $demandQty) * 100, 2) : 100;
	$now = time();
	$db->prepare(
		'INSERT INTO `epc_erp_inv_service_levels`
		 (`item_id`,`warehouse_id`,`period_month`,`demand_qty`,`fulfilled_qty`,`stockout_events`,`service_level`,`created_at`)
		 VALUES (?,?,?,?,?,?,?,?)
		 ON DUPLICATE KEY UPDATE
		 `demand_qty` = `demand_qty` + VALUES(`demand_qty`),
		 `fulfilled_qty` = `fulfilled_qty` + VALUES(`fulfilled_qty`),
		 `stockout_events` = `stockout_events` + VALUES(`stockout_events`),
		 `service_level` = IF(`demand_qty` + VALUES(`demand_qty`) > 0,
			ROUND((`fulfilled_qty` + VALUES(`fulfilled_qty`)) / (`demand_qty` + VALUES(`demand_qty`)) * 100, 2), 100)'
	)->execute(array($itemId, $warehouseId, $month, $demandQty, $fulfilledQty, $stockout, $level, $now));
}

function epc_syncron_service_level_report(PDO $db, string $fromMonth, string $toMonth)
{
	$st = $db->prepare(
		'SELECT `item_id`, `warehouse_id`, `period_month`,
		        `demand_qty`, `fulfilled_qty`, `stockout_events`, `service_level`
		 FROM `epc_erp_inv_service_levels`
		 WHERE `period_month` >= ? AND `period_month` <= ?
		 ORDER BY `period_month` DESC, `item_id` ASC'
	);
	$st->execute(array($fromMonth, $toMonth));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

/* ── policy recommendation engine ── */
function epc_syncron_recommend_policies(PDO $db, int $warehouseId = 0)
{
	$wFilter = $warehouseId > 0 ? ' AND wh.`id` = ' . (int) $warehouseId : '';
	$items = $db->query(
		'SELECT it.`id`, it.`sku`, it.`name`, wh.`id` AS wh_id, wh.`name` AS wh_name,
		        COALESCE(st.`qty_on_hand`, 0) AS on_hand
		 FROM `epc_erp_inv_items` it
		 CROSS JOIN `epc_erp_inv_warehouses` wh
		 LEFT JOIN `epc_erp_inv_stock` st ON st.`item_id` = it.`id` AND st.`warehouse_id` = wh.`id`
		 WHERE it.`active` = 1' . $wFilter . '
		 ORDER BY it.`sku`'
	)->fetchAll(PDO::FETCH_ASSOC);

	$recommendations = array();
	foreach ($items as $row) {
		$itemId = (int) $row['id'];
		$whId = (int) $row['wh_id'];
		$onHand = (float) $row['on_hand'];

		$policy = epc_syncron_policy_for_item($db, (string) $row['sku']);
		$leadTime = $policy ? (int) $policy['lead_time_days'] : 7;
		$slPct = $policy ? (float) $policy['service_level_pct'] : 95;
		$window = $policy ? (int) $policy['demand_window_days'] : 90;

		$dailyDemand = epc_syncron_demand_moving_avg($db, $itemId, $whId, $window);
		$slZ = epc_syncron_service_level_z($slPct);
		$safetySt = epc_syncron_calculate_safety_stock($dailyDemand, $leadTime, $slZ);
		$rop = epc_syncron_calculate_reorder_point($dailyDemand, $leadTime, $safetySt);

		$action = 'ok';
		if ($onHand <= 0 && $dailyDemand > 0) {
			$action = 'stockout';
		} elseif ($onHand <= $rop) {
			$action = 'reorder';
		} elseif ($policy && $policy['max_stock_qty'] > 0 && $onHand > (float) $policy['max_stock_qty']) {
			$action = 'overstock';
		}

		$recommendations[] = array(
			'item_id' => $itemId,
			'sku' => $row['sku'],
			'item_name' => $row['name'],
			'warehouse_id' => $whId,
			'warehouse_name' => $row['wh_name'],
			'on_hand' => $onHand,
			'daily_demand' => $dailyDemand,
			'safety_stock' => $safetySt,
			'reorder_point' => $rop,
			'lead_time_days' => $leadTime,
			'service_level_target' => $slPct,
			'action' => $action,
		);
	}
	return $recommendations;
}

/* ── demand forecast batch ── */
function epc_syncron_run_forecast(PDO $db, int $warehouseId = 0)
{
	epc_syncron_policy_ensure_schema($db);
	$wFilter = $warehouseId > 0 ? ' WHERE wh.`id` = ' . (int) $warehouseId : '';
	$items = $db->query(
		'SELECT it.`id`, it.`sku`, wh.`id` AS wh_id
		 FROM `epc_erp_inv_items` it
		 CROSS JOIN `epc_erp_inv_warehouses` wh' . $wFilter . '
		 WHERE it.`active` = 1'
	)->fetchAll(PDO::FETCH_ASSOC);

	$now = time();
	$periodStart = date('Y-m-d');
	$periodEnd = date('Y-m-d', $now + 30 * 86400);

	$results = array();
	foreach ($items as $row) {
		$itemId = (int) $row['id'];
		$whId = (int) $row['wh_id'];
		$policy = epc_syncron_policy_for_item($db, (string) $row['sku']);
		$method = $policy ? $policy['demand_method'] : 'moving_avg';
		$window = $policy ? (int) $policy['demand_window_days'] : 90;
		$alpha = $policy ? (float) $policy['demand_alpha'] : 0.3;

		if ($method === 'exponential') {
			$dailyForecast = epc_syncron_demand_exponential($db, $itemId, $whId, $alpha, $window);
		} else {
			$dailyForecast = epc_syncron_demand_moving_avg($db, $itemId, $whId, $window);
		}
		$forecastQty = round($dailyForecast * 30, 4);

		$db->prepare(
			'INSERT INTO `epc_erp_inv_demand_forecast`
			 (`item_id`,`warehouse_id`,`period_start`,`period_end`,`forecast_qty`,`method`,`created_at`)
			 VALUES (?,?,?,?,?,?,?)'
		)->execute(array($itemId, $whId, $periodStart, $periodEnd, $forecastQty, $method, $now));
		$results[] = array('item_id' => $itemId, 'warehouse_id' => $whId, 'forecast_30d' => $forecastQty, 'method' => $method);
	}
	return $results;
}
