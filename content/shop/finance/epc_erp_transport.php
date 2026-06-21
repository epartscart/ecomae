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
