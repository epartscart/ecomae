<?php
/**
 * Transportation Management (TMS) — fleet routing, freight, carrier rates,
 * shipment consolidation, and tracking.
 *
 * Closes Oracle TMS + SAP TM + D365 Transportation gap.
 *
 * Tables: epc_erp_carriers, epc_erp_carrier_rates, epc_erp_shipments,
 *         epc_erp_shipment_lines, epc_erp_freight_invoices, epc_erp_routes
 */
defined('_ASTEXE_') or die('No access');

function epc_tms_ensure_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_carriers` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`code`         VARCHAR(20) NOT NULL DEFAULT "",
		`name`         VARCHAR(200) NOT NULL DEFAULT "",
		`mode`         ENUM("road","sea","air","rail","courier","multimodal") NOT NULL DEFAULT "road",
		`contact_name` VARCHAR(100) NOT NULL DEFAULT "",
		`contact_phone` VARCHAR(40) NOT NULL DEFAULT "",
		`contact_email` VARCHAR(120) NOT NULL DEFAULT "",
		`tax_id`       VARCHAR(40) NOT NULL DEFAULT "",
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`rating`       DECIMAL(3,2) NOT NULL DEFAULT 0.00,
		`active`       TINYINT(1) NOT NULL DEFAULT 1,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_code` (`company_id`,`code`),
		INDEX `idx_mode` (`mode`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_carrier_rates` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`carrier_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`origin`       VARCHAR(100) NOT NULL DEFAULT "",
		`destination`  VARCHAR(100) NOT NULL DEFAULT "",
		`mode`         ENUM("road","sea","air","rail","courier") NOT NULL DEFAULT "road",
		`rate_type`    ENUM("per_kg","per_cbm","per_unit","flat","per_km") NOT NULL DEFAULT "flat",
		`rate_amount`  DECIMAL(12,4) NOT NULL DEFAULT 0.0000,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`min_charge`   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`transit_days` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`valid_from`   DATE DEFAULT NULL,
		`valid_to`     DATE DEFAULT NULL,
		`active`       TINYINT(1) NOT NULL DEFAULT 1,
		INDEX `idx_carrier` (`carrier_id`),
		INDEX `idx_route` (`origin`,`destination`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_routes` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`name`         VARCHAR(200) NOT NULL DEFAULT "",
		`origin`       VARCHAR(100) NOT NULL DEFAULT "",
		`destination`  VARCHAR(100) NOT NULL DEFAULT "",
		`waypoints`    TEXT,
		`distance_km`  DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`est_hours`    DECIMAL(6,2) NOT NULL DEFAULT 0.00,
		`preferred_carrier_id` INT UNSIGNED DEFAULT NULL,
		`active`       TINYINT(1) NOT NULL DEFAULT 1,
		INDEX `idx_company` (`company_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_shipments` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`shipment_no`  VARCHAR(30) NOT NULL DEFAULT "",
		`carrier_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`route_id`     INT UNSIGNED DEFAULT NULL,
		`direction`    ENUM("inbound","outbound","transfer") NOT NULL DEFAULT "outbound",
		`mode`         ENUM("road","sea","air","rail","courier","multimodal") NOT NULL DEFAULT "road",
		`status`       ENUM("planned","dispatched","in_transit","delivered","cancelled") NOT NULL DEFAULT "planned",
		`origin`       VARCHAR(200) NOT NULL DEFAULT "",
		`destination`  VARCHAR(200) NOT NULL DEFAULT "",
		`ship_date`    DATE DEFAULT NULL,
		`eta`          DATE DEFAULT NULL,
		`actual_delivery` DATE DEFAULT NULL,
		`total_weight_kg` DECIMAL(12,3) NOT NULL DEFAULT 0.000,
		`total_volume_cbm` DECIMAL(12,4) NOT NULL DEFAULT 0.0000,
		`total_packages` INT UNSIGNED NOT NULL DEFAULT 0,
		`tracking_ref` VARCHAR(100) NOT NULL DEFAULT "",
		`freight_cost` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`notes`        TEXT,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_no` (`company_id`,`shipment_no`),
		INDEX `idx_status` (`status`),
		INDEX `idx_carrier` (`carrier_id`),
		INDEX `idx_dates` (`ship_date`,`eta`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_shipment_lines` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`shipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`source_type`  ENUM("sales_order","purchase_order","transfer","return") NOT NULL DEFAULT "sales_order",
		`source_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`item_id`      INT UNSIGNED NOT NULL DEFAULT 0,
		`item_name`    VARCHAR(200) NOT NULL DEFAULT "",
		`qty`          DECIMAL(12,3) NOT NULL DEFAULT 0.000,
		`weight_kg`    DECIMAL(10,3) NOT NULL DEFAULT 0.000,
		`volume_cbm`   DECIMAL(10,4) NOT NULL DEFAULT 0.0000,
		`packages`     INT UNSIGNED NOT NULL DEFAULT 1,
		INDEX `idx_shipment` (`shipment_id`),
		INDEX `idx_source` (`source_type`,`source_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_freight_invoices` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`shipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`carrier_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`invoice_no`   VARCHAR(40) NOT NULL DEFAULT "",
		`invoice_date` DATE DEFAULT NULL,
		`amount`       DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`tax_amount`   DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`status`       ENUM("draft","approved","paid","disputed") NOT NULL DEFAULT "draft",
		`gl_posted`    TINYINT(1) NOT NULL DEFAULT 0,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_shipment` (`shipment_id`),
		INDEX `idx_carrier` (`carrier_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_tms_carrier_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_carriers` SET `code`=?,`name`=?,`mode`=?,`contact_name`=?,`contact_phone`=?,`contact_email`=?,`tax_id`=?,`currency`=?,`rating`=?,`active`=? WHERE `id`=?')
			->execute(array($data['code'] ?? '', $data['name'] ?? '', $data['mode'] ?? 'road', $data['contact_name'] ?? '', $data['contact_phone'] ?? '', $data['contact_email'] ?? '', $data['tax_id'] ?? '', $data['currency'] ?? 'AED', $data['rating'] ?? 0, isset($data['active']) ? (int) $data['active'] : 1, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_carriers` (`company_id`,`code`,`name`,`mode`,`contact_name`,`contact_phone`,`contact_email`,`tax_id`,`currency`,`rating`,`active`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['code'] ?? '', $data['name'] ?? '', $data['mode'] ?? 'road', $data['contact_name'] ?? '', $data['contact_phone'] ?? '', $data['contact_email'] ?? '', $data['tax_id'] ?? '', $data['currency'] ?? 'AED', $data['rating'] ?? 0, 1, $now));
	return (int) $db->lastInsertId();
}

function epc_tms_carrier_list(PDO $db, int $companyId = 0): array
{
	$sql = 'SELECT * FROM `epc_erp_carriers` WHERE `active` = 1';
	if ($companyId > 0) {
		$sql .= ' AND `company_id` = ' . (int) $companyId;
	}
	$sql .= ' ORDER BY `name`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_tms_rate_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_carrier_rates` (`carrier_id`,`origin`,`destination`,`mode`,`rate_type`,`rate_amount`,`currency`,`min_charge`,`transit_days`,`valid_from`,`valid_to`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['carrier_id'] ?? 0), $data['origin'] ?? '', $data['destination'] ?? '', $data['mode'] ?? 'road', $data['rate_type'] ?? 'flat', $data['rate_amount'] ?? 0, $data['currency'] ?? 'AED', $data['min_charge'] ?? 0, (int) ($data['transit_days'] ?? 0), $data['valid_from'] ?? null, $data['valid_to'] ?? null));
	return (int) $db->lastInsertId();
}

function epc_tms_rate_find(PDO $db, int $carrierId, string $origin, string $dest, string $mode = ''): ?array
{
	$sql = 'SELECT * FROM `epc_erp_carrier_rates` WHERE `carrier_id` = ? AND `origin` = ? AND `destination` = ? AND `active` = 1 AND (`valid_to` IS NULL OR `valid_to` >= CURDATE())';
	$params = array($carrierId, $origin, $dest);
	if ($mode !== '') {
		$sql .= ' AND `mode` = ?';
		$params[] = $mode;
	}
	$sql .= ' ORDER BY `rate_amount` ASC LIMIT 1';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_tms_rate_calculate(array $rate, float $weight = 0, float $volume = 0, int $qty = 0, float $distance = 0): float
{
	$base = (float) $rate['rate_amount'];
	switch ($rate['rate_type']) {
		case 'per_kg':
			$cost = $base * $weight;
			break;
		case 'per_cbm':
			$cost = $base * $volume;
			break;
		case 'per_unit':
			$cost = $base * $qty;
			break;
		case 'per_km':
			$cost = $base * $distance;
			break;
		default:
			$cost = $base;
	}
	$min = (float) ($rate['min_charge'] ?? 0);
	return max($cost, $min);
}

function epc_tms_shipment_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_shipments` SET `carrier_id`=?,`route_id`=?,`direction`=?,`mode`=?,`status`=?,`origin`=?,`destination`=?,`ship_date`=?,`eta`=?,`actual_delivery`=?,`total_weight_kg`=?,`total_volume_cbm`=?,`total_packages`=?,`tracking_ref`=?,`freight_cost`=?,`currency`=?,`notes`=?,`updated_at`=? WHERE `id`=?')
			->execute(array((int) ($data['carrier_id'] ?? 0), $data['route_id'] ?? null, $data['direction'] ?? 'outbound', $data['mode'] ?? 'road', $data['status'] ?? 'planned', $data['origin'] ?? '', $data['destination'] ?? '', $data['ship_date'] ?? null, $data['eta'] ?? null, $data['actual_delivery'] ?? null, $data['total_weight_kg'] ?? 0, $data['total_volume_cbm'] ?? 0, (int) ($data['total_packages'] ?? 0), $data['tracking_ref'] ?? '', $data['freight_cost'] ?? 0, $data['currency'] ?? 'AED', $data['notes'] ?? '', $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_shipments` (`company_id`,`shipment_no`,`carrier_id`,`route_id`,`direction`,`mode`,`status`,`origin`,`destination`,`ship_date`,`eta`,`total_weight_kg`,`total_volume_cbm`,`total_packages`,`tracking_ref`,`freight_cost`,`currency`,`notes`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['shipment_no'] ?? '', (int) ($data['carrier_id'] ?? 0), $data['route_id'] ?? null, $data['direction'] ?? 'outbound', $data['mode'] ?? 'road', 'planned', $data['origin'] ?? '', $data['destination'] ?? '', $data['ship_date'] ?? null, $data['eta'] ?? null, $data['total_weight_kg'] ?? 0, $data['total_volume_cbm'] ?? 0, (int) ($data['total_packages'] ?? 0), $data['tracking_ref'] ?? '', $data['freight_cost'] ?? 0, $data['currency'] ?? 'AED', $data['notes'] ?? '', $now, $now));
	return (int) $db->lastInsertId();
}

function epc_tms_shipment_line_add(PDO $db, int $shipmentId, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_shipment_lines` (`shipment_id`,`source_type`,`source_id`,`item_id`,`item_name`,`qty`,`weight_kg`,`volume_cbm`,`packages`) VALUES (?,?,?,?,?,?,?,?,?)')
		->execute(array($shipmentId, $data['source_type'] ?? 'sales_order', (int) ($data['source_id'] ?? 0), (int) ($data['item_id'] ?? 0), $data['item_name'] ?? '', $data['qty'] ?? 0, $data['weight_kg'] ?? 0, $data['volume_cbm'] ?? 0, (int) ($data['packages'] ?? 1)));
	return (int) $db->lastInsertId();
}

function epc_tms_shipment_dispatch(PDO $db, int $shipmentId): void
{
	$db->prepare('UPDATE `epc_erp_shipments` SET `status` = "dispatched", `updated_at` = ? WHERE `id` = ? AND `status` = "planned"')
		->execute(array(time(), $shipmentId));
}

function epc_tms_shipment_deliver(PDO $db, int $shipmentId, string $deliveryDate = ''): void
{
	if ($deliveryDate === '') {
		$deliveryDate = date('Y-m-d');
	}
	$db->prepare('UPDATE `epc_erp_shipments` SET `status` = "delivered", `actual_delivery` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($deliveryDate, time(), $shipmentId));
}

function epc_tms_shipment_list(PDO $db, int $companyId = 0, string $status = ''): array
{
	$sql = 'SELECT s.*, c.`name` AS carrier_name FROM `epc_erp_shipments` s LEFT JOIN `epc_erp_carriers` c ON c.`id` = s.`carrier_id` WHERE 1=1';
	if ($companyId > 0) {
		$sql .= ' AND s.`company_id` = ' . (int) $companyId;
	}
	if ($status !== '') {
		$sql .= ' AND s.`status` = "' . preg_replace('/[^a-z_]/', '', $status) . '"';
	}
	$sql .= ' ORDER BY s.`created_at` DESC';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_tms_freight_invoice_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_freight_invoices` (`company_id`,`shipment_id`,`carrier_id`,`invoice_no`,`invoice_date`,`amount`,`tax_amount`,`currency`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['shipment_id'] ?? 0), (int) ($data['carrier_id'] ?? 0), $data['invoice_no'] ?? '', $data['invoice_date'] ?? date('Y-m-d'), $data['amount'] ?? 0, $data['tax_amount'] ?? 0, $data['currency'] ?? 'AED', 'draft', time()));
	return (int) $db->lastInsertId();
}

function epc_tms_carrier_performance(PDO $db, int $carrierId): array
{
	$st = $db->prepare('SELECT COUNT(*) AS total, SUM(CASE WHEN `status`="delivered" THEN 1 ELSE 0 END) AS delivered, SUM(CASE WHEN `status`="delivered" AND `actual_delivery` <= `eta` THEN 1 ELSE 0 END) AS on_time, AVG(`freight_cost`) AS avg_cost FROM `epc_erp_shipments` WHERE `carrier_id` = ?');
	$st->execute(array($carrierId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$row['on_time_pct'] = ($row['delivered'] > 0) ? round($row['on_time'] / $row['delivered'] * 100, 1) : 0;
	return $row;
}

function epc_tms_ensure_depth_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_shipment_events` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`shipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`event_type`   ENUM("created","picked_up","departed","arrived_hub","customs_hold","customs_cleared","out_for_delivery","delivered","exception","returned") NOT NULL DEFAULT "created",
		`location`     VARCHAR(200) NOT NULL DEFAULT "",
		`description`  TEXT,
		`event_time`   DATETIME NOT NULL,
		`recorded_by`  VARCHAR(100) NOT NULL DEFAULT "",
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_shipment` (`shipment_id`),
		INDEX `idx_time` (`event_time`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_tms_claims` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`shipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`carrier_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`claim_no`     VARCHAR(30) NOT NULL DEFAULT "",
		`type`         ENUM("damage","loss","delay","overcharge","shortage","other") NOT NULL DEFAULT "damage",
		`description`  TEXT,
		`amount_claimed` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`amount_settled` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`evidence`     TEXT,
		`status`       ENUM("filed","under_review","accepted","rejected","settled") NOT NULL DEFAULT "filed",
		`filed_date`   DATE DEFAULT NULL,
		`settled_date` DATE DEFAULT NULL,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_shipment` (`shipment_id`),
		INDEX `idx_carrier` (`carrier_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_tms_load_plans` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`plan_no`      VARCHAR(30) NOT NULL DEFAULT "",
		`vehicle_type` VARCHAR(60) NOT NULL DEFAULT "",
		`max_weight_kg` DECIMAL(12,3) NOT NULL DEFAULT 0.000,
		`max_volume_cbm` DECIMAL(12,4) NOT NULL DEFAULT 0.0000,
		`max_pallets`  SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`used_weight_kg` DECIMAL(12,3) NOT NULL DEFAULT 0.000,
		`used_volume_cbm` DECIMAL(12,4) NOT NULL DEFAULT 0.0000,
		`used_pallets` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`utilization_pct` DECIMAL(5,2) NOT NULL DEFAULT 0.00,
		`shipment_ids` TEXT,
		`status`       ENUM("planning","optimized","dispatched","completed") NOT NULL DEFAULT "planning",
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_tms_tenders` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`tender_no`    VARCHAR(30) NOT NULL DEFAULT "",
		`title`        VARCHAR(200) NOT NULL DEFAULT "",
		`route_origin` VARCHAR(100) NOT NULL DEFAULT "",
		`route_dest`   VARCHAR(100) NOT NULL DEFAULT "",
		`mode`         ENUM("road","sea","air","rail","courier","multimodal") NOT NULL DEFAULT "road",
		`volume_estimate` VARCHAR(100) NOT NULL DEFAULT "",
		`valid_from`   DATE DEFAULT NULL,
		`valid_to`     DATE DEFAULT NULL,
		`status`       ENUM("draft","published","evaluation","awarded","cancelled") NOT NULL DEFAULT "draft",
		`awarded_carrier_id` INT UNSIGNED DEFAULT NULL,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_tms_tender_bids` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`tender_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`carrier_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`rate_amount`  DECIMAL(12,4) NOT NULL DEFAULT 0.0000,
		`rate_type`    ENUM("per_kg","per_cbm","per_unit","flat","per_km") NOT NULL DEFAULT "flat",
		`transit_days` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`notes`        TEXT,
		`score`        DECIMAL(5,2) DEFAULT NULL,
		`status`       ENUM("submitted","shortlisted","awarded","rejected") NOT NULL DEFAULT "submitted",
		`submitted_at` INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_tender` (`tender_id`),
		INDEX `idx_carrier` (`carrier_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_tms_shipment_event_add(PDO $db, int $shipmentId, string $eventType, string $location, string $description = ''): int
{
	$db->prepare('INSERT INTO `epc_erp_shipment_events` (`shipment_id`,`event_type`,`location`,`description`,`event_time`,`created_at`) VALUES (?,?,?,?,NOW(),?)')
		->execute(array($shipmentId, $eventType, $location, $description, time()));
	$statusMap = array('picked_up' => 'dispatched', 'departed' => 'in_transit', 'arrived_hub' => 'in_transit', 'out_for_delivery' => 'in_transit', 'delivered' => 'delivered');
	if (isset($statusMap[$eventType])) {
		$db->prepare('UPDATE `epc_erp_shipments` SET `status` = ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array($statusMap[$eventType], time(), $shipmentId));
	}
	return (int) $db->lastInsertId();
}

function epc_tms_shipment_events(PDO $db, int $shipmentId): array
{
	$st = $db->prepare('SELECT * FROM `epc_erp_shipment_events` WHERE `shipment_id` = ? ORDER BY `event_time`');
	$st->execute(array($shipmentId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_tms_consolidate_shipments(PDO $db, array $shipmentIds, array $masterData): int
{
	$masterId = epc_tms_shipment_save($db, $masterData);
	foreach ($shipmentIds as $sid) {
		$lines = $db->prepare('SELECT * FROM `epc_erp_shipment_lines` WHERE `shipment_id` = ?');
		$lines->execute(array((int) $sid));
		foreach ($lines->fetchAll(PDO::FETCH_ASSOC) as $line) {
			unset($line['id']);
			$line['shipment_id'] = $masterId;
			epc_tms_shipment_line_add($db, $masterId, $line);
		}
		$db->prepare('UPDATE `epc_erp_shipments` SET `status` = "cancelled", `notes` = CONCAT(IFNULL(`notes`,""), " [consolidated into #' . (int) $masterId . ']"), `updated_at` = ? WHERE `id` = ?')
			->execute(array(time(), (int) $sid));
	}
	$totals = $db->prepare('SELECT SUM(`weight_kg`) AS w, SUM(`volume_cbm`) AS v, SUM(`packages`) AS p FROM `epc_erp_shipment_lines` WHERE `shipment_id` = ?');
	$totals->execute(array($masterId));
	$t = $totals->fetch(PDO::FETCH_ASSOC);
	$db->prepare('UPDATE `epc_erp_shipments` SET `total_weight_kg` = ?, `total_volume_cbm` = ?, `total_packages` = ? WHERE `id` = ?')
		->execute(array($t['w'] ?? 0, $t['v'] ?? 0, (int) ($t['p'] ?? 0), $masterId));
	return $masterId;
}

function epc_tms_claim_save(PDO $db, array $data, int $id = 0): int
{
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_tms_claims` SET `type`=?,`description`=?,`amount_claimed`=?,`amount_settled`=?,`evidence`=?,`status`=?,`settled_date`=? WHERE `id`=?')
			->execute(array($data['type'] ?? 'damage', $data['description'] ?? '', $data['amount_claimed'] ?? 0, $data['amount_settled'] ?? 0, $data['evidence'] ?? '', $data['status'] ?? 'filed', $data['settled_date'] ?? null, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_tms_claims` (`company_id`,`shipment_id`,`carrier_id`,`claim_no`,`type`,`description`,`amount_claimed`,`currency`,`evidence`,`status`,`filed_date`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['shipment_id'] ?? 0), (int) ($data['carrier_id'] ?? 0), $data['claim_no'] ?? '', $data['type'] ?? 'damage', $data['description'] ?? '', $data['amount_claimed'] ?? 0, $data['currency'] ?? 'AED', $data['evidence'] ?? '', 'filed', date('Y-m-d'), time()));
	return (int) $db->lastInsertId();
}

function epc_tms_load_plan_create(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_tms_load_plans` (`company_id`,`plan_no`,`vehicle_type`,`max_weight_kg`,`max_volume_cbm`,`max_pallets`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['plan_no'] ?? '', $data['vehicle_type'] ?? '', $data['max_weight_kg'] ?? 0, $data['max_volume_cbm'] ?? 0, (int) ($data['max_pallets'] ?? 0), 'planning', time()));
	return (int) $db->lastInsertId();
}

function epc_tms_load_plan_optimize(PDO $db, int $planId, array $shipmentIds): array
{
	$plan = $db->prepare('SELECT * FROM `epc_erp_tms_load_plans` WHERE `id` = ?');
	$plan->execute(array($planId));
	$p = $plan->fetch(PDO::FETCH_ASSOC);
	if (!$p) {
		return array('error' => 'Plan not found');
	}
	$totalW = 0;
	$totalV = 0;
	$totalP = 0;
	$fitted = array();
	foreach ($shipmentIds as $sid) {
		$sh = $db->prepare('SELECT `total_weight_kg`,`total_volume_cbm`,`total_packages` FROM `epc_erp_shipments` WHERE `id` = ?');
		$sh->execute(array((int) $sid));
		$s = $sh->fetch(PDO::FETCH_ASSOC);
		if (!$s) {
			continue;
		}
		$newW = $totalW + (float) $s['total_weight_kg'];
		$newV = $totalV + (float) $s['total_volume_cbm'];
		$newP = $totalP + (int) $s['total_packages'];
		if ($newW > (float) $p['max_weight_kg'] || $newV > (float) $p['max_volume_cbm']) {
			continue;
		}
		$totalW = $newW;
		$totalV = $newV;
		$totalP = $newP;
		$fitted[] = (int) $sid;
	}
	$maxDim = max((float) $p['max_weight_kg'], 1);
	$util = round($totalW / $maxDim * 100, 2);
	$db->prepare('UPDATE `epc_erp_tms_load_plans` SET `used_weight_kg`=?,`used_volume_cbm`=?,`used_pallets`=?,`utilization_pct`=?,`shipment_ids`=?,`status`="optimized" WHERE `id`=?')
		->execute(array($totalW, $totalV, $totalP, $util, implode(',', $fitted), $planId));
	return array('fitted' => $fitted, 'weight' => $totalW, 'volume' => $totalV, 'utilization_pct' => $util);
}

function epc_tms_tender_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_tms_tenders` (`company_id`,`tender_no`,`title`,`route_origin`,`route_dest`,`mode`,`volume_estimate`,`valid_from`,`valid_to`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['tender_no'] ?? '', $data['title'] ?? '', $data['route_origin'] ?? '', $data['route_dest'] ?? '', $data['mode'] ?? 'road', $data['volume_estimate'] ?? '', $data['valid_from'] ?? null, $data['valid_to'] ?? null, 'draft', time()));
	return (int) $db->lastInsertId();
}

function epc_tms_tender_bid_submit(PDO $db, int $tenderId, int $carrierId, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_tms_tender_bids` (`tender_id`,`carrier_id`,`rate_amount`,`rate_type`,`transit_days`,`notes`,`status`,`submitted_at`) VALUES (?,?,?,?,?,?,?,?)')
		->execute(array($tenderId, $carrierId, $data['rate_amount'] ?? 0, $data['rate_type'] ?? 'flat', (int) ($data['transit_days'] ?? 0), $data['notes'] ?? '', 'submitted', time()));
	return (int) $db->lastInsertId();
}

function epc_tms_tender_award(PDO $db, int $tenderId, int $bidId): void
{
	$bid = $db->prepare('SELECT * FROM `epc_erp_tms_tender_bids` WHERE `id` = ?');
	$bid->execute(array($bidId));
	$b = $bid->fetch(PDO::FETCH_ASSOC);
	if (!$b) {
		return;
	}
	$db->prepare('UPDATE `epc_erp_tms_tender_bids` SET `status` = "rejected" WHERE `tender_id` = ? AND `id` != ?')
		->execute(array($tenderId, $bidId));
	$db->prepare('UPDATE `epc_erp_tms_tender_bids` SET `status` = "awarded" WHERE `id` = ?')
		->execute(array($bidId));
	$db->prepare('UPDATE `epc_erp_tms_tenders` SET `status` = "awarded", `awarded_carrier_id` = ? WHERE `id` = ?')
		->execute(array((int) $b['carrier_id'], $tenderId));
}

function epc_tms_freight_invoice_approve(PDO $db, int $invoiceId): void
{
	$db->prepare('UPDATE `epc_erp_freight_invoices` SET `status` = "approved" WHERE `id` = ? AND `status` = "draft"')
		->execute(array($invoiceId));
}

function epc_tms_freight_gl_post(PDO $db, int $invoiceId, int $glJournalId): void
{
	$db->prepare('UPDATE `epc_erp_freight_invoices` SET `gl_posted` = 1 WHERE `id` = ?')
		->execute(array($invoiceId));
}

function epc_tms_dashboard(PDO $db, int $companyId): array
{
	$shipments = $db->prepare('SELECT COUNT(*) AS total, SUM(CASE WHEN `status`="in_transit" THEN 1 ELSE 0 END) AS in_transit, SUM(CASE WHEN `status`="delivered" THEN 1 ELSE 0 END) AS delivered, SUM(CASE WHEN `status`="planned" THEN 1 ELSE 0 END) AS planned, SUM(`freight_cost`) AS total_freight FROM `epc_erp_shipments` WHERE `company_id` = ?');
	$shipments->execute(array($companyId));
	$shRow = $shipments->fetch(PDO::FETCH_ASSOC);

	$onTime = $db->prepare('SELECT COUNT(*) AS delivered, SUM(CASE WHEN `actual_delivery` <= `eta` THEN 1 ELSE 0 END) AS on_time FROM `epc_erp_shipments` WHERE `company_id` = ? AND `status` = "delivered" AND `actual_delivery` IS NOT NULL');
	$onTime->execute(array($companyId));
	$otRow = $onTime->fetch(PDO::FETCH_ASSOC);
	$otRow['on_time_pct'] = ((int) $otRow['delivered'] > 0) ? round((int) $otRow['on_time'] / (int) $otRow['delivered'] * 100, 1) : 0;

	$claims = $db->prepare('SELECT COUNT(*) AS open_claims, SUM(`amount_claimed`) AS claims_value FROM `epc_erp_tms_claims` WHERE `company_id` = ? AND `status` IN ("filed","under_review")');
	$claims->execute(array($companyId));
	$clRow = $claims->fetch(PDO::FETCH_ASSOC);

	$unpaid = $db->prepare('SELECT COUNT(*) AS unpaid_invoices, SUM(`amount`) AS unpaid_amount FROM `epc_erp_freight_invoices` WHERE `company_id` = ? AND `status` IN ("draft","approved")');
	$unpaid->execute(array($companyId));
	$invRow = $unpaid->fetch(PDO::FETCH_ASSOC);

	return array_merge($shRow, $otRow, $clRow ?: array(), $invRow ?: array());
}
