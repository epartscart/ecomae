<?php
/**
 * Plant Maintenance (PM) / Enterprise Asset Management — work orders,
 * preventive maintenance schedules, equipment register, spare parts,
 * and downtime tracking.
 *
 * Closes SAP PM + Oracle EAM full-depth gap (our epc_erp_costing.php
 * had a lightweight EAM section; this provides the dedicated module).
 *
 * Tables: epc_erp_pm_equipment, epc_erp_pm_work_orders,
 *         epc_erp_pm_schedules, epc_erp_pm_downtime, epc_erp_pm_spare_parts
 */
defined('_ASTEXE_') or die('No access');

function epc_pm_ensure_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_equipment` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`code`          VARCHAR(30) NOT NULL DEFAULT "",
		`name`          VARCHAR(200) NOT NULL DEFAULT "",
		`category`      ENUM("production","hvac","electrical","plumbing","vehicle","it","building","other") NOT NULL DEFAULT "other",
		`location`      VARCHAR(200) NOT NULL DEFAULT "",
		`manufacturer`  VARCHAR(100) NOT NULL DEFAULT "",
		`model`         VARCHAR(100) NOT NULL DEFAULT "",
		`serial_no`     VARCHAR(60) NOT NULL DEFAULT "",
		`purchase_date` DATE DEFAULT NULL,
		`purchase_cost` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`warranty_end`  DATE DEFAULT NULL,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`criticality`   ENUM("low","medium","high","critical") NOT NULL DEFAULT "medium",
		`status`        ENUM("operational","under_maintenance","breakdown","decommissioned") NOT NULL DEFAULT "operational",
		`parent_id`     INT UNSIGNED DEFAULT NULL,
		`notes`         TEXT,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_code` (`company_id`,`code`),
		INDEX `idx_category` (`category`),
		INDEX `idx_status` (`status`),
		INDEX `idx_parent` (`parent_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_work_orders` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`wo_number`     VARCHAR(30) NOT NULL DEFAULT "",
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`type`          ENUM("corrective","preventive","predictive","emergency","inspection","calibration") NOT NULL DEFAULT "corrective",
		`priority`      ENUM("low","medium","high","critical") NOT NULL DEFAULT "medium",
		`description`   TEXT,
		`assigned_to`   VARCHAR(100) NOT NULL DEFAULT "",
		`requested_by`  VARCHAR(100) NOT NULL DEFAULT "",
		`planned_start` DATETIME DEFAULT NULL,
		`planned_end`   DATETIME DEFAULT NULL,
		`actual_start`  DATETIME DEFAULT NULL,
		`actual_end`    DATETIME DEFAULT NULL,
		`labour_hours`  DECIMAL(8,2) NOT NULL DEFAULT 0.00,
		`labour_cost`   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`material_cost` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`total_cost`    DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`failure_code`  VARCHAR(40) NOT NULL DEFAULT "",
		`root_cause`    TEXT,
		`status`        ENUM("planned","released","in_progress","completed","cancelled") NOT NULL DEFAULT "planned",
		`schedule_id`   INT UNSIGNED DEFAULT NULL,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_wo` (`company_id`,`wo_number`),
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_type` (`type`),
		INDEX `idx_status` (`status`),
		INDEX `idx_planned` (`planned_start`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_schedules` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`name`          VARCHAR(200) NOT NULL DEFAULT "",
		`type`          ENUM("time_based","meter_based","condition_based") NOT NULL DEFAULT "time_based",
		`interval_days` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`meter_trigger` DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`meter_unit`    VARCHAR(30) NOT NULL DEFAULT "",
		`task_list`     TEXT,
		`estimated_hours` DECIMAL(6,2) NOT NULL DEFAULT 0.00,
		`last_executed` DATE DEFAULT NULL,
		`next_due`      DATE DEFAULT NULL,
		`auto_generate_wo` TINYINT(1) NOT NULL DEFAULT 1,
		`active`        TINYINT(1) NOT NULL DEFAULT 1,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_next` (`next_due`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_downtime` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`wo_id`         INT UNSIGNED DEFAULT NULL,
		`start_time`    DATETIME NOT NULL,
		`end_time`      DATETIME DEFAULT NULL,
		`duration_hours` DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`type`          ENUM("planned","unplanned") NOT NULL DEFAULT "unplanned",
		`reason`        TEXT,
		`production_loss` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_dates` (`start_time`,`end_time`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_spare_parts` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`item_id`       INT UNSIGNED NOT NULL DEFAULT 0,
		`part_name`     VARCHAR(200) NOT NULL DEFAULT "",
		`part_number`   VARCHAR(60) NOT NULL DEFAULT "",
		`qty_required`  DECIMAL(10,3) NOT NULL DEFAULT 1.000,
		`reorder_level` DECIMAL(10,3) NOT NULL DEFAULT 0.000,
		`lead_time_days` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`critical`      TINYINT(1) NOT NULL DEFAULT 0,
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_item` (`item_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_pm_equipment_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_pm_equipment` SET `code`=?,`name`=?,`category`=?,`location`=?,`manufacturer`=?,`model`=?,`serial_no`=?,`purchase_date`=?,`purchase_cost`=?,`warranty_end`=?,`currency`=?,`criticality`=?,`status`=?,`parent_id`=?,`notes`=? WHERE `id`=?')
			->execute(array($data['code'] ?? '', $data['name'] ?? '', $data['category'] ?? 'other', $data['location'] ?? '', $data['manufacturer'] ?? '', $data['model'] ?? '', $data['serial_no'] ?? '', $data['purchase_date'] ?? null, $data['purchase_cost'] ?? 0, $data['warranty_end'] ?? null, $data['currency'] ?? 'AED', $data['criticality'] ?? 'medium', $data['status'] ?? 'operational', $data['parent_id'] ?? null, $data['notes'] ?? '', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_pm_equipment` (`company_id`,`code`,`name`,`category`,`location`,`manufacturer`,`model`,`serial_no`,`purchase_date`,`purchase_cost`,`warranty_end`,`currency`,`criticality`,`status`,`parent_id`,`notes`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['code'] ?? '', $data['name'] ?? '', $data['category'] ?? 'other', $data['location'] ?? '', $data['manufacturer'] ?? '', $data['model'] ?? '', $data['serial_no'] ?? '', $data['purchase_date'] ?? null, $data['purchase_cost'] ?? 0, $data['warranty_end'] ?? null, $data['currency'] ?? 'AED', $data['criticality'] ?? 'medium', 'operational', $data['parent_id'] ?? null, $data['notes'] ?? '', $now));
	return (int) $db->lastInsertId();
}

function epc_pm_equipment_list(PDO $db, int $companyId = 0): array
{
	$sql = 'SELECT e.*, (SELECT COUNT(*) FROM `epc_erp_pm_work_orders` w WHERE w.`equipment_id` = e.`id` AND w.`status` NOT IN ("completed","cancelled")) AS open_wo_count FROM `epc_erp_pm_equipment` e WHERE e.`status` != "decommissioned"';
	if ($companyId > 0) {
		$sql .= ' AND e.`company_id` = ' . (int) $companyId;
	}
	$sql .= ' ORDER BY e.`criticality` DESC, e.`name`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pm_wo_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$total = (float) ($data['labour_cost'] ?? 0) + (float) ($data['material_cost'] ?? 0);
		$db->prepare('UPDATE `epc_erp_pm_work_orders` SET `equipment_id`=?,`type`=?,`priority`=?,`description`=?,`assigned_to`=?,`planned_start`=?,`planned_end`=?,`actual_start`=?,`actual_end`=?,`labour_hours`=?,`labour_cost`=?,`material_cost`=?,`total_cost`=?,`currency`=?,`failure_code`=?,`root_cause`=?,`status`=?,`updated_at`=? WHERE `id`=?')
			->execute(array((int) ($data['equipment_id'] ?? 0), $data['type'] ?? 'corrective', $data['priority'] ?? 'medium', $data['description'] ?? '', $data['assigned_to'] ?? '', $data['planned_start'] ?? null, $data['planned_end'] ?? null, $data['actual_start'] ?? null, $data['actual_end'] ?? null, $data['labour_hours'] ?? 0, $data['labour_cost'] ?? 0, $data['material_cost'] ?? 0, $total, $data['currency'] ?? 'AED', $data['failure_code'] ?? '', $data['root_cause'] ?? '', $data['status'] ?? 'planned', $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_pm_work_orders` (`company_id`,`wo_number`,`equipment_id`,`type`,`priority`,`description`,`assigned_to`,`requested_by`,`planned_start`,`planned_end`,`currency`,`status`,`schedule_id`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['wo_number'] ?? '', (int) ($data['equipment_id'] ?? 0), $data['type'] ?? 'corrective', $data['priority'] ?? 'medium', $data['description'] ?? '', $data['assigned_to'] ?? '', $data['requested_by'] ?? '', $data['planned_start'] ?? null, $data['planned_end'] ?? null, $data['currency'] ?? 'AED', 'planned', $data['schedule_id'] ?? null, $now, $now));
	return (int) $db->lastInsertId();
}

function epc_pm_wo_complete(PDO $db, int $woId, array $data = array()): void
{
	$total = (float) ($data['labour_cost'] ?? 0) + (float) ($data['material_cost'] ?? 0);
	$db->prepare('UPDATE `epc_erp_pm_work_orders` SET `status` = "completed", `actual_end` = ?, `labour_hours` = ?, `labour_cost` = ?, `material_cost` = ?, `total_cost` = ?, `failure_code` = ?, `root_cause` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($data['actual_end'] ?? date('Y-m-d H:i:s'), $data['labour_hours'] ?? 0, $data['labour_cost'] ?? 0, $data['material_cost'] ?? 0, $total, $data['failure_code'] ?? '', $data['root_cause'] ?? '', time(), $woId));
}

function epc_pm_wo_list(PDO $db, int $companyId = 0, string $status = ''): array
{
	$sql = 'SELECT w.*, eq.`name` AS equipment_name, eq.`code` AS equipment_code FROM `epc_erp_pm_work_orders` w LEFT JOIN `epc_erp_pm_equipment` eq ON eq.`id` = w.`equipment_id` WHERE 1=1';
	if ($companyId > 0) {
		$sql .= ' AND w.`company_id` = ' . (int) $companyId;
	}
	if ($status !== '') {
		$sql .= ' AND w.`status` = "' . preg_replace('/[^a-z_]/', '', $status) . '"';
	}
	$sql .= ' ORDER BY FIELD(w.`priority`,"critical","high","medium","low"), w.`planned_start`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pm_schedule_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_pm_schedules` (`company_id`,`equipment_id`,`name`,`type`,`interval_days`,`meter_trigger`,`meter_unit`,`task_list`,`estimated_hours`,`next_due`,`auto_generate_wo`,`active`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['equipment_id'] ?? 0), $data['name'] ?? '', $data['type'] ?? 'time_based', (int) ($data['interval_days'] ?? 0), $data['meter_trigger'] ?? 0, $data['meter_unit'] ?? '', $data['task_list'] ?? '', $data['estimated_hours'] ?? 0, $data['next_due'] ?? null, isset($data['auto_generate_wo']) ? (int) $data['auto_generate_wo'] : 1, 1, time()));
	return (int) $db->lastInsertId();
}

function epc_pm_generate_preventive_wo(PDO $db, int $companyId = 0): int
{
	$sql = 'SELECT * FROM `epc_erp_pm_schedules` WHERE `active` = 1 AND `auto_generate_wo` = 1 AND (`next_due` IS NULL OR `next_due` <= CURDATE())';
	if ($companyId > 0) {
		$sql .= ' AND `company_id` = ' . (int) $companyId;
	}
	$rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
	$created = 0;
	foreach ($rows as $sched) {
		$woNum = 'PM-' . str_pad((string) $sched['equipment_id'], 4, '0', STR_PAD_LEFT) . '-' . date('Ymd');
		$existing = $db->prepare('SELECT COUNT(*) FROM `epc_erp_pm_work_orders` WHERE `wo_number` = ? AND `company_id` = ?');
		$existing->execute(array($woNum, (int) $sched['company_id']));
		if ((int) $existing->fetchColumn() > 0) {
			continue;
		}
		epc_pm_wo_save($db, array(
			'company_id' => $sched['company_id'],
			'wo_number' => $woNum,
			'equipment_id' => $sched['equipment_id'],
			'type' => 'preventive',
			'priority' => 'medium',
			'description' => $sched['name'] . ' — ' . ($sched['task_list'] ?? ''),
			'planned_start' => date('Y-m-d 08:00:00'),
			'schedule_id' => $sched['id'],
		));
		$nextDue = date('Y-m-d', strtotime('+' . max(1, (int) $sched['interval_days']) . ' days'));
		$db->prepare('UPDATE `epc_erp_pm_schedules` SET `last_executed` = CURDATE(), `next_due` = ? WHERE `id` = ?')
			->execute(array($nextDue, $sched['id']));
		$created++;
	}
	return $created;
}

function epc_pm_downtime_log(PDO $db, array $data): int
{
	$start = $data['start_time'] ?? date('Y-m-d H:i:s');
	$end = $data['end_time'] ?? null;
	$hours = 0;
	if ($end !== null) {
		$hours = round((strtotime($end) - strtotime($start)) / 3600, 2);
	}
	$db->prepare('INSERT INTO `epc_erp_pm_downtime` (`company_id`,`equipment_id`,`wo_id`,`start_time`,`end_time`,`duration_hours`,`type`,`reason`,`production_loss`,`currency`) VALUES (?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['equipment_id'] ?? 0), $data['wo_id'] ?? null, $start, $end, $hours, $data['type'] ?? 'unplanned', $data['reason'] ?? '', $data['production_loss'] ?? 0, $data['currency'] ?? 'AED'));
	return (int) $db->lastInsertId();
}

function epc_pm_spare_part_link(PDO $db, int $equipmentId, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_pm_spare_parts` (`company_id`,`equipment_id`,`item_id`,`part_name`,`part_number`,`qty_required`,`reorder_level`,`lead_time_days`,`critical`) VALUES (?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $equipmentId, (int) ($data['item_id'] ?? 0), $data['part_name'] ?? '', $data['part_number'] ?? '', $data['qty_required'] ?? 1, $data['reorder_level'] ?? 0, (int) ($data['lead_time_days'] ?? 0), isset($data['critical']) ? (int) $data['critical'] : 0));
	return (int) $db->lastInsertId();
}

function epc_pm_dashboard(PDO $db, int $companyId): array
{
	$eq = $db->prepare('SELECT COUNT(*) AS total, SUM(CASE WHEN `status` = "breakdown" THEN 1 ELSE 0 END) AS breakdowns, SUM(CASE WHEN `status` = "under_maintenance" THEN 1 ELSE 0 END) AS in_maintenance FROM `epc_erp_pm_equipment` WHERE `company_id` = ? AND `status` != "decommissioned"');
	$eq->execute(array($companyId));
	$eqRow = $eq->fetch(PDO::FETCH_ASSOC);

	$wo = $db->prepare('SELECT COUNT(*) AS open_wo, SUM(CASE WHEN `priority` IN ("high","critical") THEN 1 ELSE 0 END) AS urgent_wo FROM `epc_erp_pm_work_orders` WHERE `company_id` = ? AND `status` NOT IN ("completed","cancelled")');
	$wo->execute(array($companyId));
	$woRow = $wo->fetch(PDO::FETCH_ASSOC);

	$overdue = $db->prepare('SELECT COUNT(*) AS overdue_pm FROM `epc_erp_pm_schedules` WHERE `company_id` = ? AND `active` = 1 AND `next_due` < CURDATE()');
	$overdue->execute(array($companyId));
	$overdueRow = $overdue->fetch(PDO::FETCH_ASSOC);

	$dt = $db->prepare('SELECT SUM(`duration_hours`) AS total_downtime_hours, SUM(`production_loss`) AS total_production_loss FROM `epc_erp_pm_downtime` WHERE `company_id` = ? AND `start_time` >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)');
	$dt->execute(array($companyId));
	$dtRow = $dt->fetch(PDO::FETCH_ASSOC);

	$mtbf = $db->prepare('SELECT AVG(TIMESTAMPDIFF(HOUR, d1.`end_time`, d2.`start_time`)) AS mtbf_hours FROM `epc_erp_pm_downtime` d1 INNER JOIN `epc_erp_pm_downtime` d2 ON d2.`equipment_id` = d1.`equipment_id` AND d2.`start_time` > d1.`end_time` AND d2.`type` = "unplanned" WHERE d1.`company_id` = ? AND d1.`type` = "unplanned" AND d1.`end_time` IS NOT NULL');
	$mtbf->execute(array($companyId));
	$mtbfRow = $mtbf->fetch(PDO::FETCH_ASSOC);

	return array_merge($eqRow, $woRow, $overdueRow, $dtRow, array('mtbf_hours' => round((float) ($mtbfRow['mtbf_hours'] ?? 0), 1)));
}

function epc_pm_ensure_depth_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_meter_readings` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`meter_type`    ENUM("hours","km","cycles","units","temperature","pressure","vibration") NOT NULL DEFAULT "hours",
		`reading_value` DECIMAL(14,3) NOT NULL DEFAULT 0.000,
		`reading_date`  DATETIME NOT NULL,
		`recorded_by`   VARCHAR(100) NOT NULL DEFAULT "",
		`notes`         TEXT,
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_date` (`reading_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_calibrations` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`calibration_no` VARCHAR(30) NOT NULL DEFAULT "",
		`standard_ref`  VARCHAR(100) NOT NULL DEFAULT "",
		`tolerance`     VARCHAR(60) NOT NULL DEFAULT "",
		`measured_value` VARCHAR(60) NOT NULL DEFAULT "",
		`result`        ENUM("pass","fail","adjusted","out_of_range") NOT NULL DEFAULT "pass",
		`calibrated_by` VARCHAR(100) NOT NULL DEFAULT "",
		`lab_name`      VARCHAR(200) NOT NULL DEFAULT "",
		`certificate_no` VARCHAR(60) NOT NULL DEFAULT "",
		`calibration_date` DATE DEFAULT NULL,
		`next_due`      DATE DEFAULT NULL,
		`cost`          DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_due` (`next_due`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_wo_materials` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`wo_id`         INT UNSIGNED NOT NULL DEFAULT 0,
		`item_id`       INT UNSIGNED NOT NULL DEFAULT 0,
		`part_name`     VARCHAR(200) NOT NULL DEFAULT "",
		`part_number`   VARCHAR(60) NOT NULL DEFAULT "",
		`qty_required`  DECIMAL(10,3) NOT NULL DEFAULT 0.000,
		`qty_issued`    DECIMAL(10,3) NOT NULL DEFAULT 0.000,
		`unit_cost`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`total_cost`    DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`warehouse_id`  INT UNSIGNED DEFAULT NULL,
		INDEX `idx_wo` (`wo_id`),
		INDEX `idx_item` (`item_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_failure_codes` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`code`          VARCHAR(20) NOT NULL DEFAULT "",
		`description`   VARCHAR(200) NOT NULL DEFAULT "",
		`category`      ENUM("mechanical","electrical","hydraulic","pneumatic","electronic","structural","software","operator_error","wear","corrosion","other") NOT NULL DEFAULT "mechanical",
		`active`        TINYINT(1) NOT NULL DEFAULT 1,
		UNIQUE KEY `uk_code` (`company_id`,`code`),
		INDEX `idx_category` (`category`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_pm_warranty_claims` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`equipment_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`wo_id`         INT UNSIGNED DEFAULT NULL,
		`claim_no`      VARCHAR(30) NOT NULL DEFAULT "",
		`manufacturer`  VARCHAR(200) NOT NULL DEFAULT "",
		`failure_description` TEXT,
		`claim_amount`  DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`settled_amount` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`status`        ENUM("draft","submitted","under_review","approved","rejected","settled") NOT NULL DEFAULT "draft",
		`submitted_date` DATE DEFAULT NULL,
		`settled_date`  DATE DEFAULT NULL,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_equipment` (`equipment_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_pm_meter_record(PDO $db, int $equipmentId, string $meterType, float $value, string $recordedBy = ''): int
{
	$db->prepare('INSERT INTO `epc_erp_pm_meter_readings` (`equipment_id`,`meter_type`,`reading_value`,`reading_date`,`recorded_by`) VALUES (?,?,?,NOW(),?)')
		->execute(array($equipmentId, $meterType, $value, $recordedBy));
	$readingId = (int) $db->lastInsertId();
	$schedules = $db->prepare('SELECT * FROM `epc_erp_pm_schedules` WHERE `equipment_id` = ? AND `type` = "meter_based" AND `active` = 1 AND `meter_unit` = ?');
	$schedules->execute(array($equipmentId, $meterType));
	foreach ($schedules->fetchAll(PDO::FETCH_ASSOC) as $sched) {
		if ((float) $sched['meter_trigger'] > 0 && $value >= (float) $sched['meter_trigger']) {
			$db->prepare('UPDATE `epc_erp_pm_schedules` SET `next_due` = CURDATE() WHERE `id` = ?')
				->execute(array($sched['id']));
		}
	}
	return $readingId;
}

function epc_pm_meter_history(PDO $db, int $equipmentId, string $meterType = '', int $limit = 100): array
{
	$sql = 'SELECT * FROM `epc_erp_pm_meter_readings` WHERE `equipment_id` = ?';
	$params = array($equipmentId);
	if ($meterType !== '') {
		$sql .= ' AND `meter_type` = ?';
		$params[] = $meterType;
	}
	$sql .= ' ORDER BY `reading_date` DESC LIMIT ' . (int) $limit;
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pm_calibration_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_pm_calibrations` (`company_id`,`equipment_id`,`calibration_no`,`standard_ref`,`tolerance`,`measured_value`,`result`,`calibrated_by`,`lab_name`,`certificate_no`,`calibration_date`,`next_due`,`cost`,`currency`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['equipment_id'] ?? 0), $data['calibration_no'] ?? '', $data['standard_ref'] ?? '', $data['tolerance'] ?? '', $data['measured_value'] ?? '', $data['result'] ?? 'pass', $data['calibrated_by'] ?? '', $data['lab_name'] ?? '', $data['certificate_no'] ?? '', $data['calibration_date'] ?? date('Y-m-d'), $data['next_due'] ?? null, $data['cost'] ?? 0, $data['currency'] ?? 'AED', time()));
	return (int) $db->lastInsertId();
}

function epc_pm_calibration_due(PDO $db, int $companyId, int $days = 30): array
{
	$st = $db->prepare('SELECT c.*, eq.`name` AS equipment_name, eq.`code` AS equipment_code FROM `epc_erp_pm_calibrations` c INNER JOIN `epc_erp_pm_equipment` eq ON eq.`id` = c.`equipment_id` WHERE c.`company_id` = ? AND c.`next_due` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL ? DAY) ORDER BY c.`next_due`');
	$st->execute(array($companyId, $days));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pm_wo_material_add(PDO $db, int $woId, array $data): int
{
	$totalCost = round((float) ($data['qty_required'] ?? 0) * (float) ($data['unit_cost'] ?? 0), 2);
	$db->prepare('INSERT INTO `epc_erp_pm_wo_materials` (`wo_id`,`item_id`,`part_name`,`part_number`,`qty_required`,`unit_cost`,`total_cost`,`currency`,`warehouse_id`) VALUES (?,?,?,?,?,?,?,?,?)')
		->execute(array($woId, (int) ($data['item_id'] ?? 0), $data['part_name'] ?? '', $data['part_number'] ?? '', $data['qty_required'] ?? 0, $data['unit_cost'] ?? 0, $totalCost, $data['currency'] ?? 'AED', $data['warehouse_id'] ?? null));
	return (int) $db->lastInsertId();
}

function epc_pm_wo_material_issue(PDO $db, int $materialId, float $qtyIssued): void
{
	$db->prepare('UPDATE `epc_erp_pm_wo_materials` SET `qty_issued` = `qty_issued` + ?, `total_cost` = `qty_issued` * `unit_cost` WHERE `id` = ?')
		->execute(array($qtyIssued, $materialId));
	$mat = $db->prepare('SELECT `wo_id` FROM `epc_erp_pm_wo_materials` WHERE `id` = ?');
	$mat->execute(array($materialId));
	$woId = (int) $mat->fetchColumn();
	if ($woId > 0) {
		$total = $db->prepare('SELECT SUM(`total_cost`) FROM `epc_erp_pm_wo_materials` WHERE `wo_id` = ?');
		$total->execute(array($woId));
		$db->prepare('UPDATE `epc_erp_pm_work_orders` SET `material_cost` = ?, `total_cost` = `labour_cost` + ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array((float) $total->fetchColumn(), (float) $total->fetchColumn(), time(), $woId));
	}
}

function epc_pm_failure_code_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_pm_failure_codes` (`company_id`,`code`,`description`,`category`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `description`=VALUES(`description`),`category`=VALUES(`category`)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['code'] ?? '', $data['description'] ?? '', $data['category'] ?? 'mechanical'));
	return (int) $db->lastInsertId();
}

function epc_pm_warranty_claim_save(PDO $db, array $data, int $id = 0): int
{
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_pm_warranty_claims` SET `failure_description`=?,`claim_amount`=?,`settled_amount`=?,`status`=?,`settled_date`=? WHERE `id`=?')
			->execute(array($data['failure_description'] ?? '', $data['claim_amount'] ?? 0, $data['settled_amount'] ?? 0, $data['status'] ?? 'draft', $data['settled_date'] ?? null, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_pm_warranty_claims` (`company_id`,`equipment_id`,`wo_id`,`claim_no`,`manufacturer`,`failure_description`,`claim_amount`,`currency`,`status`,`submitted_date`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['equipment_id'] ?? 0), $data['wo_id'] ?? null, $data['claim_no'] ?? '', $data['manufacturer'] ?? '', $data['failure_description'] ?? '', $data['claim_amount'] ?? 0, $data['currency'] ?? 'AED', 'draft', null, time()));
	return (int) $db->lastInsertId();
}

function epc_pm_equipment_hierarchy(PDO $db, int $companyId, int $parentId = 0): array
{
	$sql = 'SELECT * FROM `epc_erp_pm_equipment` WHERE `company_id` = ? AND `status` != "decommissioned"';
	if ($parentId > 0) {
		$sql .= ' AND `parent_id` = ' . (int) $parentId;
	} else {
		$sql .= ' AND (`parent_id` IS NULL OR `parent_id` = 0)';
	}
	$sql .= ' ORDER BY `name`';
	$st = $db->prepare($sql);
	$st->execute(array($companyId));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$row['children'] = epc_pm_equipment_hierarchy($db, $companyId, (int) $row['id']);
	}
	return $rows;
}

function epc_pm_downtime_end(PDO $db, int $downtimeId): void
{
	$db->prepare('UPDATE `epc_erp_pm_downtime` SET `end_time` = NOW(), `duration_hours` = TIMESTAMPDIFF(MINUTE, `start_time`, NOW()) / 60.0 WHERE `id` = ? AND `end_time` IS NULL')
		->execute(array($downtimeId));
}

function epc_pm_equipment_cost_history(PDO $db, int $equipmentId, int $months = 12): array
{
	$st = $db->prepare('SELECT DATE_FORMAT(`planned_start`, "%Y-%m") AS period, COUNT(*) AS wo_count, SUM(`total_cost`) AS total_cost, SUM(`labour_hours`) AS total_hours FROM `epc_erp_pm_work_orders` WHERE `equipment_id` = ? AND `status` = "completed" AND `planned_start` >= DATE_SUB(CURDATE(), INTERVAL ? MONTH) GROUP BY period ORDER BY period');
	$st->execute(array($equipmentId, $months));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_pm_reliability_metrics(PDO $db, int $equipmentId): array
{
	$mtbf = $db->prepare('SELECT AVG(TIMESTAMPDIFF(HOUR, d1.`end_time`, d2.`start_time`)) AS mtbf FROM `epc_erp_pm_downtime` d1 INNER JOIN `epc_erp_pm_downtime` d2 ON d2.`equipment_id` = d1.`equipment_id` AND d2.`start_time` > d1.`end_time` AND d2.`type` = "unplanned" WHERE d1.`equipment_id` = ? AND d1.`type` = "unplanned" AND d1.`end_time` IS NOT NULL');
	$mtbf->execute(array($equipmentId));
	$mtbfVal = round((float) ($mtbf->fetchColumn() ?: 0), 1);

	$mttr = $db->prepare('SELECT AVG(`duration_hours`) AS mttr FROM `epc_erp_pm_downtime` WHERE `equipment_id` = ? AND `type` = "unplanned" AND `end_time` IS NOT NULL');
	$mttr->execute(array($equipmentId));
	$mttrVal = round((float) ($mttr->fetchColumn() ?: 0), 1);

	$availability = ($mtbfVal + $mttrVal > 0) ? round($mtbfVal / ($mtbfVal + $mttrVal) * 100, 1) : 100;

	$totalCost = $db->prepare('SELECT SUM(`total_cost`) FROM `epc_erp_pm_work_orders` WHERE `equipment_id` = ? AND `status` = "completed"');
	$totalCost->execute(array($equipmentId));

	return array(
		'mtbf_hours' => $mtbfVal,
		'mttr_hours' => $mttrVal,
		'availability_pct' => $availability,
		'total_maintenance_cost' => round((float) ($totalCost->fetchColumn() ?: 0), 2),
	);
}
