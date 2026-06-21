<?php
/**
 * Contingent Workforce Management — contractor/temporary staff, SOW-based
 * engagements, time & expense capture, rate cards, and compliance tracking.
 *
 * Closes SAP Fieldglass gap.
 *
 * Tables: epc_erp_cw_workers, epc_erp_cw_engagements,
 *         epc_erp_cw_timesheets, epc_erp_cw_rate_cards
 */
defined('_ASTEXE_') or die('No access');

function epc_cw_ensure_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_workers` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`worker_code`  VARCHAR(30) NOT NULL DEFAULT "",
		`first_name`   VARCHAR(80) NOT NULL DEFAULT "",
		`last_name`    VARCHAR(80) NOT NULL DEFAULT "",
		`email`        VARCHAR(120) NOT NULL DEFAULT "",
		`phone`        VARCHAR(40) NOT NULL DEFAULT "",
		`type`         ENUM("contractor","temp","freelancer","consultant","agency") NOT NULL DEFAULT "contractor",
		`agency_name`  VARCHAR(200) NOT NULL DEFAULT "",
		`skills`       TEXT,
		`visa_status`  VARCHAR(40) NOT NULL DEFAULT "",
		`visa_expiry`  DATE DEFAULT NULL,
		`tax_id`       VARCHAR(40) NOT NULL DEFAULT "",
		`bank_account` VARCHAR(40) NOT NULL DEFAULT "",
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`status`       ENUM("active","onboarding","offboarded","suspended") NOT NULL DEFAULT "onboarding",
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_code` (`company_id`,`worker_code`),
		INDEX `idx_type` (`type`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_engagements` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`worker_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`engagement_no` VARCHAR(30) NOT NULL DEFAULT "",
		`type`         ENUM("time_materials","fixed_price","sow","milestone") NOT NULL DEFAULT "time_materials",
		`title`        VARCHAR(200) NOT NULL DEFAULT "",
		`department`   VARCHAR(100) NOT NULL DEFAULT "",
		`manager`      VARCHAR(100) NOT NULL DEFAULT "",
		`start_date`   DATE NOT NULL,
		`end_date`     DATE DEFAULT NULL,
		`rate_card_id` INT UNSIGNED DEFAULT NULL,
		`bill_rate`    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`pay_rate`     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`rate_unit`    ENUM("hourly","daily","weekly","monthly","fixed") NOT NULL DEFAULT "hourly",
		`budget`       DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`spent`        DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`sow_ref`      VARCHAR(100) NOT NULL DEFAULT "",
		`status`       ENUM("draft","active","extended","completed","terminated") NOT NULL DEFAULT "draft",
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_no` (`company_id`,`engagement_no`),
		INDEX `idx_worker` (`worker_id`),
		INDEX `idx_status` (`status`),
		INDEX `idx_dates` (`start_date`,`end_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_timesheets` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`engagement_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`worker_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`week_start`   DATE NOT NULL,
		`mon_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`tue_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`wed_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`thu_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`fri_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`sat_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`sun_hrs`      DECIMAL(4,2) NOT NULL DEFAULT 0.00,
		`total_hrs`    DECIMAL(6,2) NOT NULL DEFAULT 0.00,
		`bill_amount`  DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`pay_amount`   DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`expenses`     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`status`       ENUM("draft","submitted","approved","rejected","invoiced") NOT NULL DEFAULT "draft",
		`submitted_at` INT UNSIGNED DEFAULT NULL,
		`approved_by`  VARCHAR(100) NOT NULL DEFAULT "",
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_engagement` (`engagement_id`),
		INDEX `idx_worker` (`worker_id`),
		INDEX `idx_week` (`week_start`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_rate_cards` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`name`         VARCHAR(200) NOT NULL DEFAULT "",
		`job_category` VARCHAR(100) NOT NULL DEFAULT "",
		`skill_level`  ENUM("junior","mid","senior","lead","expert") NOT NULL DEFAULT "mid",
		`bill_rate`    DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`pay_rate`     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`rate_unit`    ENUM("hourly","daily") NOT NULL DEFAULT "hourly",
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`valid_from`   DATE DEFAULT NULL,
		`valid_to`     DATE DEFAULT NULL,
		`active`       TINYINT(1) NOT NULL DEFAULT 1,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_category` (`job_category`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_cw_worker_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_cw_workers` SET `first_name`=?,`last_name`=?,`email`=?,`phone`=?,`type`=?,`agency_name`=?,`skills`=?,`visa_status`=?,`visa_expiry`=?,`tax_id`=?,`currency`=?,`status`=? WHERE `id`=?')
			->execute(array($data['first_name'] ?? '', $data['last_name'] ?? '', $data['email'] ?? '', $data['phone'] ?? '', $data['type'] ?? 'contractor', $data['agency_name'] ?? '', $data['skills'] ?? '', $data['visa_status'] ?? '', $data['visa_expiry'] ?? null, $data['tax_id'] ?? '', $data['currency'] ?? 'AED', $data['status'] ?? 'active', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_cw_workers` (`company_id`,`worker_code`,`first_name`,`last_name`,`email`,`phone`,`type`,`agency_name`,`skills`,`visa_status`,`visa_expiry`,`tax_id`,`currency`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['worker_code'] ?? '', $data['first_name'] ?? '', $data['last_name'] ?? '', $data['email'] ?? '', $data['phone'] ?? '', $data['type'] ?? 'contractor', $data['agency_name'] ?? '', $data['skills'] ?? '', $data['visa_status'] ?? '', $data['visa_expiry'] ?? null, $data['tax_id'] ?? '', $data['currency'] ?? 'AED', 'onboarding', $now));
	return (int) $db->lastInsertId();
}

function epc_cw_worker_list(PDO $db, int $companyId = 0): array
{
	$sql = 'SELECT w.*, (SELECT COUNT(*) FROM `epc_erp_cw_engagements` e WHERE e.`worker_id` = w.`id` AND e.`status` = "active") AS active_engagements FROM `epc_erp_cw_workers` w WHERE w.`status` != "offboarded"';
	if ($companyId > 0) {
		$sql .= ' AND w.`company_id` = ' . (int) $companyId;
	}
	$sql .= ' ORDER BY w.`last_name`, w.`first_name`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_cw_engagement_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_cw_engagements` SET `worker_id`=?,`type`=?,`title`=?,`department`=?,`manager`=?,`start_date`=?,`end_date`=?,`rate_card_id`=?,`bill_rate`=?,`pay_rate`=?,`rate_unit`=?,`budget`=?,`currency`=?,`sow_ref`=?,`status`=?,`updated_at`=? WHERE `id`=?')
			->execute(array((int) ($data['worker_id'] ?? 0), $data['type'] ?? 'time_materials', $data['title'] ?? '', $data['department'] ?? '', $data['manager'] ?? '', $data['start_date'] ?? date('Y-m-d'), $data['end_date'] ?? null, $data['rate_card_id'] ?? null, $data['bill_rate'] ?? 0, $data['pay_rate'] ?? 0, $data['rate_unit'] ?? 'hourly', $data['budget'] ?? 0, $data['currency'] ?? 'AED', $data['sow_ref'] ?? '', $data['status'] ?? 'active', $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_cw_engagements` (`company_id`,`worker_id`,`engagement_no`,`type`,`title`,`department`,`manager`,`start_date`,`end_date`,`rate_card_id`,`bill_rate`,`pay_rate`,`rate_unit`,`budget`,`currency`,`sow_ref`,`status`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['worker_id'] ?? 0), $data['engagement_no'] ?? '', $data['type'] ?? 'time_materials', $data['title'] ?? '', $data['department'] ?? '', $data['manager'] ?? '', $data['start_date'] ?? date('Y-m-d'), $data['end_date'] ?? null, $data['rate_card_id'] ?? null, $data['bill_rate'] ?? 0, $data['pay_rate'] ?? 0, $data['rate_unit'] ?? 'hourly', $data['budget'] ?? 0, $data['currency'] ?? 'AED', $data['sow_ref'] ?? '', 'draft', $now, $now));
	return (int) $db->lastInsertId();
}

function epc_cw_timesheet_save(PDO $db, array $data): int
{
	$total = (float) ($data['mon_hrs'] ?? 0) + (float) ($data['tue_hrs'] ?? 0) + (float) ($data['wed_hrs'] ?? 0) + (float) ($data['thu_hrs'] ?? 0) + (float) ($data['fri_hrs'] ?? 0) + (float) ($data['sat_hrs'] ?? 0) + (float) ($data['sun_hrs'] ?? 0);
	$engId = (int) ($data['engagement_id'] ?? 0);
	$billRate = 0;
	$payRate = 0;
	if ($engId > 0) {
		$eng = $db->prepare('SELECT `bill_rate`,`pay_rate`,`rate_unit` FROM `epc_erp_cw_engagements` WHERE `id` = ?');
		$eng->execute(array($engId));
		$engRow = $eng->fetch(PDO::FETCH_ASSOC);
		if ($engRow) {
			$billRate = (float) $engRow['bill_rate'];
			$payRate = (float) $engRow['pay_rate'];
			if ($engRow['rate_unit'] === 'daily') {
				$billRate = $billRate / 8;
				$payRate = $payRate / 8;
			}
		}
	}
	$billAmount = round($total * $billRate, 2);
	$payAmount = round($total * $payRate, 2);
	$db->prepare('INSERT INTO `epc_erp_cw_timesheets` (`company_id`,`engagement_id`,`worker_id`,`week_start`,`mon_hrs`,`tue_hrs`,`wed_hrs`,`thu_hrs`,`fri_hrs`,`sat_hrs`,`sun_hrs`,`total_hrs`,`bill_amount`,`pay_amount`,`expenses`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $engId, (int) ($data['worker_id'] ?? 0), $data['week_start'] ?? date('Y-m-d'), $data['mon_hrs'] ?? 0, $data['tue_hrs'] ?? 0, $data['wed_hrs'] ?? 0, $data['thu_hrs'] ?? 0, $data['fri_hrs'] ?? 0, $data['sat_hrs'] ?? 0, $data['sun_hrs'] ?? 0, $total, $billAmount, $payAmount, $data['expenses'] ?? 0, 'draft', time()));
	return (int) $db->lastInsertId();
}

function epc_cw_timesheet_approve(PDO $db, int $timesheetId, string $approver): void
{
	$db->prepare('UPDATE `epc_erp_cw_timesheets` SET `status` = "approved", `approved_by` = ? WHERE `id` = ? AND `status` = "submitted"')
		->execute(array($approver, $timesheetId));
	$ts = $db->prepare('SELECT `engagement_id`, `bill_amount` FROM `epc_erp_cw_timesheets` WHERE `id` = ?');
	$ts->execute(array($timesheetId));
	$row = $ts->fetch(PDO::FETCH_ASSOC);
	if ($row && (int) $row['engagement_id'] > 0) {
		$db->prepare('UPDATE `epc_erp_cw_engagements` SET `spent` = `spent` + ?, `updated_at` = ? WHERE `id` = ?')
			->execute(array($row['bill_amount'], time(), (int) $row['engagement_id']));
	}
}

function epc_cw_dashboard(PDO $db, int $companyId): array
{
	$workers = $db->prepare('SELECT COUNT(*) AS total_workers, SUM(CASE WHEN `type` = "contractor" THEN 1 ELSE 0 END) AS contractors, SUM(CASE WHEN `type` = "temp" THEN 1 ELSE 0 END) AS temps FROM `epc_erp_cw_workers` WHERE `company_id` = ? AND `status` = "active"');
	$workers->execute(array($companyId));
	$wRow = $workers->fetch(PDO::FETCH_ASSOC);

	$engagements = $db->prepare('SELECT COUNT(*) AS active_engagements, SUM(`budget`) AS total_budget, SUM(`spent`) AS total_spent FROM `epc_erp_cw_engagements` WHERE `company_id` = ? AND `status` = "active"');
	$engagements->execute(array($companyId));
	$eRow = $engagements->fetch(PDO::FETCH_ASSOC);

	$pending = $db->prepare('SELECT COUNT(*) AS pending_timesheets FROM `epc_erp_cw_timesheets` WHERE `company_id` = ? AND `status` = "submitted"');
	$pending->execute(array($companyId));
	$pRow = $pending->fetch(PDO::FETCH_ASSOC);

	$visa = $db->prepare('SELECT COUNT(*) AS visa_expiring FROM `epc_erp_cw_workers` WHERE `company_id` = ? AND `status` = "active" AND `visa_expiry` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)');
	$visa->execute(array($companyId));
	$vRow = $visa->fetch(PDO::FETCH_ASSOC);

	return array_merge($wRow, $eRow, $pRow, $vRow);
}
