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

function epc_cw_ensure_depth_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_invoices` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`engagement_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`worker_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`invoice_no`   VARCHAR(40) NOT NULL DEFAULT "",
		`period_from`  DATE NOT NULL,
		`period_to`    DATE NOT NULL,
		`timesheet_ids` TEXT,
		`hours_total`  DECIMAL(8,2) NOT NULL DEFAULT 0.00,
		`bill_amount`  DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`expenses`     DECIMAL(10,2) NOT NULL DEFAULT 0.00,
		`tax_amount`   DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`total_amount` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`status`       ENUM("draft","sent","approved","paid","disputed") NOT NULL DEFAULT "draft",
		`gl_posted`    TINYINT(1) NOT NULL DEFAULT 0,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_engagement` (`engagement_id`),
		INDEX `idx_worker` (`worker_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_onboarding` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`worker_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`step_name`    VARCHAR(200) NOT NULL DEFAULT "",
		`category`     ENUM("documents","it_access","security","training","equipment","compliance","other") NOT NULL DEFAULT "documents",
		`required`     TINYINT(1) NOT NULL DEFAULT 1,
		`completed`    TINYINT(1) NOT NULL DEFAULT 0,
		`completed_date` DATE DEFAULT NULL,
		`completed_by` VARCHAR(100) NOT NULL DEFAULT "",
		`notes`        TEXT,
		INDEX `idx_worker` (`worker_id`),
		INDEX `idx_completed` (`completed`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_compliance` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`worker_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`type`         ENUM("work_permit","visa","insurance","medical","background_check","nda","ip_agreement","safety_cert","tax_clearance","other") NOT NULL DEFAULT "work_permit",
		`document_ref` VARCHAR(100) NOT NULL DEFAULT "",
		`issue_date`   DATE DEFAULT NULL,
		`expiry_date`  DATE DEFAULT NULL,
		`status`       ENUM("valid","expired","pending","not_required") NOT NULL DEFAULT "pending",
		`notes`        TEXT,
		INDEX `idx_worker` (`worker_id`),
		INDEX `idx_type` (`type`),
		INDEX `idx_expiry` (`expiry_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_cw_evaluations` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`worker_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`engagement_id` INT UNSIGNED NOT NULL DEFAULT 0,
		`evaluator`    VARCHAR(100) NOT NULL DEFAULT "",
		`period`       VARCHAR(40) NOT NULL DEFAULT "",
		`quality_score` TINYINT UNSIGNED NOT NULL DEFAULT 0,
		`reliability_score` TINYINT UNSIGNED NOT NULL DEFAULT 0,
		`communication_score` TINYINT UNSIGNED NOT NULL DEFAULT 0,
		`technical_score` TINYINT UNSIGNED NOT NULL DEFAULT 0,
		`overall_score` DECIMAL(3,1) NOT NULL DEFAULT 0.0,
		`strengths`    TEXT,
		`improvements` TEXT,
		`recommend_rehire` TINYINT(1) NOT NULL DEFAULT 1,
		`evaluation_date` DATE DEFAULT NULL,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_worker` (`worker_id`),
		INDEX `idx_engagement` (`engagement_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_cw_invoice_generate(PDO $db, int $engagementId, string $periodFrom, string $periodTo): int
{
	$ts = $db->prepare('SELECT * FROM `epc_erp_cw_timesheets` WHERE `engagement_id` = ? AND `status` = "approved" AND `week_start` BETWEEN ? AND ?');
	$ts->execute(array($engagementId, $periodFrom, $periodTo));
	$rows = $ts->fetchAll(PDO::FETCH_ASSOC);
	if (empty($rows)) {
		return 0;
	}
	$hours = 0;
	$bill = 0;
	$expenses = 0;
	$tsIds = array();
	foreach ($rows as $r) {
		$hours += (float) $r['total_hrs'];
		$bill += (float) $r['bill_amount'];
		$expenses += (float) $r['expenses'];
		$tsIds[] = (int) $r['id'];
	}
	$eng = $db->prepare('SELECT `company_id`, `worker_id`, `currency` FROM `epc_erp_cw_engagements` WHERE `id` = ?');
	$eng->execute(array($engagementId));
	$e = $eng->fetch(PDO::FETCH_ASSOC);
	$total = round($bill + $expenses, 2);
	$invoiceNo = 'CW-' . str_pad((string) $engagementId, 4, '0', STR_PAD_LEFT) . '-' . date('Ymd');
	$db->prepare('INSERT INTO `epc_erp_cw_invoices` (`company_id`,`engagement_id`,`worker_id`,`invoice_no`,`period_from`,`period_to`,`timesheet_ids`,`hours_total`,`bill_amount`,`expenses`,`total_amount`,`currency`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($e['company_id'] ?? 0), $engagementId, (int) ($e['worker_id'] ?? 0), $invoiceNo, $periodFrom, $periodTo, implode(',', $tsIds), $hours, $bill, $expenses, $total, $e['currency'] ?? 'AED', 'draft', time()));
	$invId = (int) $db->lastInsertId();
	foreach ($tsIds as $tid) {
		$db->prepare('UPDATE `epc_erp_cw_timesheets` SET `status` = "invoiced" WHERE `id` = ?')
			->execute(array($tid));
	}
	return $invId;
}

function epc_cw_onboarding_setup(PDO $db, int $workerId, string $workerType = 'contractor'): void
{
	$steps = array(
		array('ID/passport copy', 'documents'),
		array('Work permit verification', 'compliance'),
		array('NDA / confidentiality agreement', 'compliance'),
		array('IP assignment agreement', 'compliance'),
		array('Background check', 'compliance'),
		array('Safety induction', 'training'),
		array('IT access provisioning', 'it_access'),
		array('Building access card', 'security'),
		array('Equipment issuance', 'equipment'),
		array('Emergency contact details', 'documents'),
	);
	if ($workerType === 'contractor' || $workerType === 'consultant') {
		$steps[] = array('Insurance certificate', 'compliance');
		$steps[] = array('Tax registration / clearance', 'compliance');
	}
	foreach ($steps as $step) {
		$db->prepare('INSERT INTO `epc_erp_cw_onboarding` (`worker_id`,`step_name`,`category`,`required`) VALUES (?,?,?,1)')
			->execute(array($workerId, $step[0], $step[1]));
	}
}

function epc_cw_onboarding_complete_step(PDO $db, int $stepId, string $completedBy = ''): void
{
	$db->prepare('UPDATE `epc_erp_cw_onboarding` SET `completed` = 1, `completed_date` = CURDATE(), `completed_by` = ? WHERE `id` = ?')
		->execute(array($completedBy, $stepId));
}

function epc_cw_onboarding_progress(PDO $db, int $workerId): array
{
	$st = $db->prepare('SELECT COUNT(*) AS total, SUM(`completed`) AS done, SUM(CASE WHEN `required` = 1 AND `completed` = 0 THEN 1 ELSE 0 END) AS pending_required FROM `epc_erp_cw_onboarding` WHERE `worker_id` = ?');
	$st->execute(array($workerId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$row['pct'] = ((int) $row['total'] > 0) ? round((int) $row['done'] / (int) $row['total'] * 100, 1) : 0;
	return $row;
}

function epc_cw_compliance_save(PDO $db, int $workerId, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_cw_compliance` (`worker_id`,`type`,`document_ref`,`issue_date`,`expiry_date`,`status`,`notes`) VALUES (?,?,?,?,?,?,?)')
		->execute(array($workerId, $data['type'] ?? 'work_permit', $data['document_ref'] ?? '', $data['issue_date'] ?? null, $data['expiry_date'] ?? null, $data['status'] ?? 'pending', $data['notes'] ?? ''));
	return (int) $db->lastInsertId();
}

function epc_cw_compliance_matrix(PDO $db, int $companyId): array
{
	$st = $db->prepare('SELECT w.`id`, w.`worker_code`, CONCAT(w.`first_name`," ",w.`last_name`) AS name, w.`type`, c.`type` AS doc_type, c.`expiry_date`, c.`status` AS doc_status FROM `epc_erp_cw_workers` w LEFT JOIN `epc_erp_cw_compliance` c ON c.`worker_id` = w.`id` WHERE w.`company_id` = ? AND w.`status` IN ("active","onboarding") ORDER BY w.`last_name`, c.`type`');
	$st->execute(array($companyId));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	$matrix = array();
	foreach ($rows as $r) {
		$wid = (int) $r['id'];
		if (!isset($matrix[$wid])) {
			$matrix[$wid] = array('worker_code' => $r['worker_code'], 'name' => $r['name'], 'type' => $r['type'], 'docs' => array());
		}
		if ($r['doc_type'] !== null) {
			$matrix[$wid]['docs'][] = array('type' => $r['doc_type'], 'expiry' => $r['expiry_date'], 'status' => $r['doc_status']);
		}
	}
	return array_values($matrix);
}

function epc_cw_evaluation_save(PDO $db, array $data): int
{
	$q = (int) ($data['quality_score'] ?? 0);
	$r = (int) ($data['reliability_score'] ?? 0);
	$c = (int) ($data['communication_score'] ?? 0);
	$t = (int) ($data['technical_score'] ?? 0);
	$overall = round(($q + $r + $c + $t) / 4, 1);
	$db->prepare('INSERT INTO `epc_erp_cw_evaluations` (`company_id`,`worker_id`,`engagement_id`,`evaluator`,`period`,`quality_score`,`reliability_score`,`communication_score`,`technical_score`,`overall_score`,`strengths`,`improvements`,`recommend_rehire`,`evaluation_date`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['worker_id'] ?? 0), (int) ($data['engagement_id'] ?? 0), $data['evaluator'] ?? '', $data['period'] ?? '', $q, $r, $c, $t, $overall, $data['strengths'] ?? '', $data['improvements'] ?? '', isset($data['recommend_rehire']) ? (int) $data['recommend_rehire'] : 1, $data['evaluation_date'] ?? date('Y-m-d'), time()));
	return (int) $db->lastInsertId();
}

function epc_cw_timesheet_submit(PDO $db, int $timesheetId): void
{
	$db->prepare('UPDATE `epc_erp_cw_timesheets` SET `status` = "submitted", `submitted_at` = ? WHERE `id` = ? AND `status` = "draft"')
		->execute(array(time(), $timesheetId));
}

function epc_cw_timesheet_reject(PDO $db, int $timesheetId, string $approver): void
{
	$db->prepare('UPDATE `epc_erp_cw_timesheets` SET `status` = "rejected", `approved_by` = ? WHERE `id` = ? AND `status` = "submitted"')
		->execute(array($approver, $timesheetId));
}

function epc_cw_engagement_extend(PDO $db, int $engagementId, string $newEndDate, float $newBudget = 0): void
{
	$upd = 'UPDATE `epc_erp_cw_engagements` SET `end_date` = ?, `status` = "extended", `updated_at` = ?';
	$params = array($newEndDate, time());
	if ($newBudget > 0) {
		$upd .= ', `budget` = ?';
		$params[] = $newBudget;
	}
	$upd .= ' WHERE `id` = ?';
	$params[] = $engagementId;
	$db->prepare($upd)->execute($params);
}

function epc_cw_budget_forecast(PDO $db, int $companyId): array
{
	$st = $db->prepare('SELECT e.`id`, e.`engagement_no`, e.`title`, CONCAT(w.`first_name`," ",w.`last_name`) AS worker, e.`budget`, e.`spent`, ROUND(e.`budget` - e.`spent`, 2) AS remaining, CASE WHEN e.`budget` > 0 THEN ROUND(e.`spent` / e.`budget` * 100, 1) ELSE 0 END AS utilization_pct, e.`end_date`, DATEDIFF(e.`end_date`, CURDATE()) AS days_remaining FROM `epc_erp_cw_engagements` e LEFT JOIN `epc_erp_cw_workers` w ON w.`id` = e.`worker_id` WHERE e.`company_id` = ? AND e.`status` IN ("active","extended") ORDER BY utilization_pct DESC');
	$st->execute(array($companyId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_cw_offboard(PDO $db, int $workerId): void
{
	$db->prepare('UPDATE `epc_erp_cw_workers` SET `status` = "offboarded" WHERE `id` = ?')
		->execute(array($workerId));
	$db->prepare('UPDATE `epc_erp_cw_engagements` SET `status` = "completed", `updated_at` = ? WHERE `worker_id` = ? AND `status` IN ("active","extended")')
		->execute(array(time(), $workerId));
}
