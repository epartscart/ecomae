<?php
/**
 * Environment, Health & Safety (EHS) — incident reporting, risk assessments,
 * safety inspections, compliance tracking, and training records.
 *
 * Closes Oracle EHS + SAP EHS gap.
 *
 * Tables: epc_erp_ehs_incidents, epc_erp_ehs_risk_assessments,
 *         epc_erp_ehs_inspections, epc_erp_ehs_training,
 *         epc_erp_ehs_corrective_actions
 */
defined('_ASTEXE_') or die('No access');

function epc_ehs_ensure_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_incidents` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`incident_no`    VARCHAR(30) NOT NULL DEFAULT "",
		`type`           ENUM("injury","near_miss","property_damage","environmental","fire","chemical","ergonomic","other") NOT NULL DEFAULT "injury",
		`severity`       ENUM("minor","moderate","major","critical","fatal") NOT NULL DEFAULT "minor",
		`date_occurred`  DATETIME DEFAULT NULL,
		`date_reported`  DATETIME DEFAULT NULL,
		`location`       VARCHAR(200) NOT NULL DEFAULT "",
		`department`     VARCHAR(100) NOT NULL DEFAULT "",
		`reported_by`    VARCHAR(100) NOT NULL DEFAULT "",
		`affected_person` VARCHAR(100) NOT NULL DEFAULT "",
		`description`    TEXT,
		`root_cause`     TEXT,
		`immediate_action` TEXT,
		`lost_days`      SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`cost_estimate`  DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`       CHAR(3) NOT NULL DEFAULT "AED",
		`status`         ENUM("reported","investigating","corrective_action","closed") NOT NULL DEFAULT "reported",
		`closed_date`    DATE DEFAULT NULL,
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_no` (`company_id`,`incident_no`),
		INDEX `idx_type` (`type`),
		INDEX `idx_severity` (`severity`),
		INDEX `idx_status` (`status`),
		INDEX `idx_date` (`date_occurred`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_risk_assessments` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`title`          VARCHAR(200) NOT NULL DEFAULT "",
		`area`           VARCHAR(100) NOT NULL DEFAULT "",
		`hazard`         TEXT,
		`risk_level`     ENUM("low","medium","high","extreme") NOT NULL DEFAULT "medium",
		`likelihood`     TINYINT UNSIGNED NOT NULL DEFAULT 3,
		`consequence`    TINYINT UNSIGNED NOT NULL DEFAULT 3,
		`risk_score`     SMALLINT UNSIGNED NOT NULL DEFAULT 9,
		`existing_controls` TEXT,
		`recommended_controls` TEXT,
		`residual_risk`  ENUM("low","medium","high","extreme") NOT NULL DEFAULT "low",
		`assessor`       VARCHAR(100) NOT NULL DEFAULT "",
		`assessment_date` DATE DEFAULT NULL,
		`review_date`    DATE DEFAULT NULL,
		`status`         ENUM("draft","active","review_due","archived") NOT NULL DEFAULT "draft",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_risk` (`risk_level`),
		INDEX `idx_review` (`review_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_inspections` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`type`           ENUM("safety","fire","electrical","chemical","workplace","vehicle","equipment") NOT NULL DEFAULT "safety",
		`location`       VARCHAR(200) NOT NULL DEFAULT "",
		`inspector`      VARCHAR(100) NOT NULL DEFAULT "",
		`inspection_date` DATE DEFAULT NULL,
		`next_due`       DATE DEFAULT NULL,
		`findings`       TEXT,
		`score`          DECIMAL(5,2) DEFAULT NULL,
		`pass`           TINYINT(1) NOT NULL DEFAULT 1,
		`corrective_actions_count` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
		`status`         ENUM("scheduled","completed","overdue") NOT NULL DEFAULT "scheduled",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_type` (`type`),
		INDEX `idx_due` (`next_due`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_training` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`employee_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`employee_name`  VARCHAR(100) NOT NULL DEFAULT "",
		`course`         VARCHAR(200) NOT NULL DEFAULT "",
		`category`       ENUM("induction","fire_safety","first_aid","hazmat","confined_space","working_at_height","manual_handling","ppe","driving","environmental","general") NOT NULL DEFAULT "general",
		`provider`       VARCHAR(100) NOT NULL DEFAULT "",
		`completion_date` DATE DEFAULT NULL,
		`expiry_date`    DATE DEFAULT NULL,
		`certificate_ref` VARCHAR(100) NOT NULL DEFAULT "",
		`status`         ENUM("scheduled","completed","expired","renewed") NOT NULL DEFAULT "scheduled",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_employee` (`employee_id`),
		INDEX `idx_expiry` (`expiry_date`),
		INDEX `idx_category` (`category`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_corrective_actions` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`source_type`    ENUM("incident","inspection","risk_assessment","audit") NOT NULL DEFAULT "incident",
		`source_id`      INT UNSIGNED NOT NULL DEFAULT 0,
		`description`    TEXT,
		`assigned_to`    VARCHAR(100) NOT NULL DEFAULT "",
		`priority`       ENUM("low","medium","high","critical") NOT NULL DEFAULT "medium",
		`due_date`       DATE DEFAULT NULL,
		`completed_date` DATE DEFAULT NULL,
		`status`         ENUM("open","in_progress","completed","overdue","cancelled") NOT NULL DEFAULT "open",
		`verification`   TEXT,
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_source` (`source_type`,`source_id`),
		INDEX `idx_status` (`status`),
		INDEX `idx_due` (`due_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_ehs_incident_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_ehs_incidents` SET `type`=?,`severity`=?,`date_occurred`=?,`date_reported`=?,`location`=?,`department`=?,`reported_by`=?,`affected_person`=?,`description`=?,`root_cause`=?,`immediate_action`=?,`lost_days`=?,`cost_estimate`=?,`currency`=?,`status`=?,`closed_date`=?,`updated_at`=? WHERE `id`=?')
			->execute(array($data['type'] ?? 'injury', $data['severity'] ?? 'minor', $data['date_occurred'] ?? null, $data['date_reported'] ?? date('Y-m-d H:i:s'), $data['location'] ?? '', $data['department'] ?? '', $data['reported_by'] ?? '', $data['affected_person'] ?? '', $data['description'] ?? '', $data['root_cause'] ?? '', $data['immediate_action'] ?? '', (int) ($data['lost_days'] ?? 0), $data['cost_estimate'] ?? 0, $data['currency'] ?? 'AED', $data['status'] ?? 'reported', $data['closed_date'] ?? null, $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_ehs_incidents` (`company_id`,`incident_no`,`type`,`severity`,`date_occurred`,`date_reported`,`location`,`department`,`reported_by`,`affected_person`,`description`,`immediate_action`,`lost_days`,`cost_estimate`,`currency`,`status`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['incident_no'] ?? '', $data['type'] ?? 'injury', $data['severity'] ?? 'minor', $data['date_occurred'] ?? null, $data['date_reported'] ?? date('Y-m-d H:i:s'), $data['location'] ?? '', $data['department'] ?? '', $data['reported_by'] ?? '', $data['affected_person'] ?? '', $data['description'] ?? '', $data['immediate_action'] ?? '', (int) ($data['lost_days'] ?? 0), $data['cost_estimate'] ?? 0, $data['currency'] ?? 'AED', 'reported', $now, $now));
	return (int) $db->lastInsertId();
}

function epc_ehs_incident_list(PDO $db, int $companyId = 0, string $status = ''): array
{
	$sql = 'SELECT * FROM `epc_erp_ehs_incidents` WHERE 1=1';
	if ($companyId > 0) {
		$sql .= ' AND `company_id` = ' . (int) $companyId;
	}
	if ($status !== '') {
		$sql .= ' AND `status` = "' . preg_replace('/[^a-z_]/', '', $status) . '"';
	}
	$sql .= ' ORDER BY `date_occurred` DESC';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_ehs_risk_save(PDO $db, array $data, int $id = 0): int
{
	$likelihood = (int) ($data['likelihood'] ?? 3);
	$consequence = (int) ($data['consequence'] ?? 3);
	$score = $likelihood * $consequence;
	$level = 'low';
	if ($score >= 20) {
		$level = 'extreme';
	} elseif ($score >= 12) {
		$level = 'high';
	} elseif ($score >= 6) {
		$level = 'medium';
	}

	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_ehs_risk_assessments` SET `title`=?,`area`=?,`hazard`=?,`risk_level`=?,`likelihood`=?,`consequence`=?,`risk_score`=?,`existing_controls`=?,`recommended_controls`=?,`residual_risk`=?,`assessor`=?,`assessment_date`=?,`review_date`=?,`status`=? WHERE `id`=?')
			->execute(array($data['title'] ?? '', $data['area'] ?? '', $data['hazard'] ?? '', $level, $likelihood, $consequence, $score, $data['existing_controls'] ?? '', $data['recommended_controls'] ?? '', $data['residual_risk'] ?? 'low', $data['assessor'] ?? '', $data['assessment_date'] ?? date('Y-m-d'), $data['review_date'] ?? null, $data['status'] ?? 'draft', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_ehs_risk_assessments` (`company_id`,`title`,`area`,`hazard`,`risk_level`,`likelihood`,`consequence`,`risk_score`,`existing_controls`,`recommended_controls`,`residual_risk`,`assessor`,`assessment_date`,`review_date`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['title'] ?? '', $data['area'] ?? '', $data['hazard'] ?? '', $level, $likelihood, $consequence, $score, $data['existing_controls'] ?? '', $data['recommended_controls'] ?? '', $data['residual_risk'] ?? 'low', $data['assessor'] ?? '', $data['assessment_date'] ?? date('Y-m-d'), $data['review_date'] ?? null, 'draft', $now));
	return (int) $db->lastInsertId();
}

function epc_ehs_inspection_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_ehs_inspections` (`company_id`,`type`,`location`,`inspector`,`inspection_date`,`next_due`,`findings`,`score`,`pass`,`corrective_actions_count`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['type'] ?? 'safety', $data['location'] ?? '', $data['inspector'] ?? '', $data['inspection_date'] ?? date('Y-m-d'), $data['next_due'] ?? null, $data['findings'] ?? '', $data['score'] ?? null, isset($data['pass']) ? (int) $data['pass'] : 1, (int) ($data['corrective_actions_count'] ?? 0), $data['status'] ?? 'completed', time()));
	return (int) $db->lastInsertId();
}

function epc_ehs_training_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_ehs_training` (`company_id`,`employee_id`,`employee_name`,`course`,`category`,`provider`,`completion_date`,`expiry_date`,`certificate_ref`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['employee_id'] ?? 0), $data['employee_name'] ?? '', $data['course'] ?? '', $data['category'] ?? 'general', $data['provider'] ?? '', $data['completion_date'] ?? null, $data['expiry_date'] ?? null, $data['certificate_ref'] ?? '', $data['status'] ?? 'completed', time()));
	return (int) $db->lastInsertId();
}

function epc_ehs_corrective_action_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_ehs_corrective_actions` (`company_id`,`source_type`,`source_id`,`description`,`assigned_to`,`priority`,`due_date`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['source_type'] ?? 'incident', (int) ($data['source_id'] ?? 0), $data['description'] ?? '', $data['assigned_to'] ?? '', $data['priority'] ?? 'medium', $data['due_date'] ?? null, 'open', time()));
	return (int) $db->lastInsertId();
}

function epc_ehs_dashboard(PDO $db, int $companyId): array
{
	$incidents = $db->prepare('SELECT COUNT(*) AS total, SUM(CASE WHEN `status` != "closed" THEN 1 ELSE 0 END) AS open_incidents, SUM(`lost_days`) AS total_lost_days, SUM(`cost_estimate`) AS total_cost FROM `epc_erp_ehs_incidents` WHERE `company_id` = ?');
	$incidents->execute(array($companyId));
	$incRow = $incidents->fetch(PDO::FETCH_ASSOC);

	$risks = $db->prepare('SELECT COUNT(*) AS total, SUM(CASE WHEN `risk_level` IN ("high","extreme") THEN 1 ELSE 0 END) AS high_risks FROM `epc_erp_ehs_risk_assessments` WHERE `company_id` = ? AND `status` = "active"');
	$risks->execute(array($companyId));
	$riskRow = $risks->fetch(PDO::FETCH_ASSOC);

	$overdue = $db->prepare('SELECT COUNT(*) AS overdue_actions FROM `epc_erp_ehs_corrective_actions` WHERE `company_id` = ? AND `status` IN ("open","in_progress") AND `due_date` < CURDATE()');
	$overdue->execute(array($companyId));
	$overdueRow = $overdue->fetch(PDO::FETCH_ASSOC);

	$training = $db->prepare('SELECT COUNT(*) AS expiring_soon FROM `epc_erp_ehs_training` WHERE `company_id` = ? AND `status` = "completed" AND `expiry_date` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)');
	$training->execute(array($companyId));
	$trainRow = $training->fetch(PDO::FETCH_ASSOC);

	return array_merge($incRow, $riskRow, $overdueRow, $trainRow);
}
