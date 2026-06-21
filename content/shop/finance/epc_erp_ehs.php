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

function epc_ehs_ensure_depth_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_permits` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`permit_no`      VARCHAR(30) NOT NULL DEFAULT "",
		`type`           ENUM("hot_work","confined_space","working_at_height","excavation","electrical","chemical","demolition","crane","general") NOT NULL DEFAULT "general",
		`location`       VARCHAR(200) NOT NULL DEFAULT "",
		`description`    TEXT,
		`hazards`        TEXT,
		`precautions`    TEXT,
		`requested_by`   VARCHAR(100) NOT NULL DEFAULT "",
		`approved_by`    VARCHAR(100) NOT NULL DEFAULT "",
		`valid_from`     DATETIME DEFAULT NULL,
		`valid_to`       DATETIME DEFAULT NULL,
		`status`         ENUM("requested","approved","active","expired","cancelled","suspended") NOT NULL DEFAULT "requested",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_no` (`company_id`,`permit_no`),
		INDEX `idx_type` (`type`),
		INDEX `idx_status` (`status`),
		INDEX `idx_validity` (`valid_from`,`valid_to`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_chemicals` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`chemical_name`  VARCHAR(200) NOT NULL DEFAULT "",
		`cas_number`     VARCHAR(30) NOT NULL DEFAULT "",
		`un_number`      VARCHAR(20) NOT NULL DEFAULT "",
		`ghs_class`      VARCHAR(100) NOT NULL DEFAULT "",
		`hazard_statements` TEXT,
		`precautionary_statements` TEXT,
		`storage_requirements` TEXT,
		`ppe_required`   TEXT,
		`msds_file`      VARCHAR(500) NOT NULL DEFAULT "",
		`max_quantity`   DECIMAL(12,3) NOT NULL DEFAULT 0.000,
		`current_qty`    DECIMAL(12,3) NOT NULL DEFAULT 0.000,
		`unit`           VARCHAR(20) NOT NULL DEFAULT "kg",
		`location`       VARCHAR(200) NOT NULL DEFAULT "",
		`supplier`       VARCHAR(200) NOT NULL DEFAULT "",
		`expiry_date`    DATE DEFAULT NULL,
		`status`         ENUM("approved","restricted","banned","pending_review") NOT NULL DEFAULT "approved",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_cas` (`cas_number`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_observations` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`observation_no` VARCHAR(30) NOT NULL DEFAULT "",
		`category`       ENUM("safe_act","unsafe_act","safe_condition","unsafe_condition","good_practice","suggestion") NOT NULL DEFAULT "unsafe_condition",
		`location`       VARCHAR(200) NOT NULL DEFAULT "",
		`department`     VARCHAR(100) NOT NULL DEFAULT "",
		`observed_by`    VARCHAR(100) NOT NULL DEFAULT "",
		`description`    TEXT,
		`action_taken`   TEXT,
		`severity`       ENUM("low","medium","high") NOT NULL DEFAULT "medium",
		`status`         ENUM("open","acknowledged","resolved") NOT NULL DEFAULT "open",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_category` (`category`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_emergency_plans` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`name`           VARCHAR(200) NOT NULL DEFAULT "",
		`scenario`       ENUM("fire","chemical_spill","medical","earthquake","flood","explosion","gas_leak","power_failure","active_threat","pandemic","other") NOT NULL DEFAULT "fire",
		`location`       VARCHAR(200) NOT NULL DEFAULT "",
		`procedures`     TEXT,
		`assembly_points` TEXT,
		`key_contacts`   TEXT,
		`equipment_list` TEXT,
		`last_drill_date` DATE DEFAULT NULL,
		`next_drill_date` DATE DEFAULT NULL,
		`drill_frequency_days` SMALLINT UNSIGNED NOT NULL DEFAULT 180,
		`version`        VARCHAR(10) NOT NULL DEFAULT "1.0",
		`status`         ENUM("draft","active","under_review","archived") NOT NULL DEFAULT "draft",
		`created_at`     INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_company` (`company_id`),
		INDEX `idx_scenario` (`scenario`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_ehs_ppe_tracking` (
		`id`             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`employee_id`    INT UNSIGNED NOT NULL DEFAULT 0,
		`ppe_type`       ENUM("helmet","safety_glasses","gloves","safety_shoes","high_vis","ear_protection","respirator","face_shield","harness","coverall","other") NOT NULL DEFAULT "helmet",
		`item_code`      VARCHAR(40) NOT NULL DEFAULT "",
		`issued_date`    DATE DEFAULT NULL,
		`expiry_date`    DATE DEFAULT NULL,
		`condition`      ENUM("new","good","fair","worn","damaged","replaced") NOT NULL DEFAULT "new",
		`returned_date`  DATE DEFAULT NULL,
		`notes`          TEXT,
		INDEX `idx_employee` (`employee_id`),
		INDEX `idx_type` (`ppe_type`),
		INDEX `idx_expiry` (`expiry_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_ehs_permit_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_ehs_permits` SET `type`=?,`location`=?,`description`=?,`hazards`=?,`precautions`=?,`approved_by`=?,`valid_from`=?,`valid_to`=?,`status`=? WHERE `id`=?')
			->execute(array($data['type'] ?? 'general', $data['location'] ?? '', $data['description'] ?? '', $data['hazards'] ?? '', $data['precautions'] ?? '', $data['approved_by'] ?? '', $data['valid_from'] ?? null, $data['valid_to'] ?? null, $data['status'] ?? 'requested', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_ehs_permits` (`company_id`,`permit_no`,`type`,`location`,`description`,`hazards`,`precautions`,`requested_by`,`valid_from`,`valid_to`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['permit_no'] ?? '', $data['type'] ?? 'general', $data['location'] ?? '', $data['description'] ?? '', $data['hazards'] ?? '', $data['precautions'] ?? '', $data['requested_by'] ?? '', $data['valid_from'] ?? null, $data['valid_to'] ?? null, 'requested', $now));
	return (int) $db->lastInsertId();
}

function epc_ehs_permit_approve(PDO $db, int $permitId, string $approver): void
{
	$db->prepare('UPDATE `epc_erp_ehs_permits` SET `status` = "approved", `approved_by` = ? WHERE `id` = ? AND `status` = "requested"')
		->execute(array($approver, $permitId));
}

function epc_ehs_chemical_save(PDO $db, array $data, int $id = 0): int
{
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_ehs_chemicals` SET `chemical_name`=?,`cas_number`=?,`un_number`=?,`ghs_class`=?,`hazard_statements`=?,`precautionary_statements`=?,`storage_requirements`=?,`ppe_required`=?,`msds_file`=?,`max_quantity`=?,`current_qty`=?,`unit`=?,`location`=?,`supplier`=?,`expiry_date`=?,`status`=? WHERE `id`=?')
			->execute(array($data['chemical_name'] ?? '', $data['cas_number'] ?? '', $data['un_number'] ?? '', $data['ghs_class'] ?? '', $data['hazard_statements'] ?? '', $data['precautionary_statements'] ?? '', $data['storage_requirements'] ?? '', $data['ppe_required'] ?? '', $data['msds_file'] ?? '', $data['max_quantity'] ?? 0, $data['current_qty'] ?? 0, $data['unit'] ?? 'kg', $data['location'] ?? '', $data['supplier'] ?? '', $data['expiry_date'] ?? null, $data['status'] ?? 'approved', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_ehs_chemicals` (`company_id`,`chemical_name`,`cas_number`,`un_number`,`ghs_class`,`hazard_statements`,`precautionary_statements`,`storage_requirements`,`ppe_required`,`msds_file`,`max_quantity`,`current_qty`,`unit`,`location`,`supplier`,`expiry_date`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['chemical_name'] ?? '', $data['cas_number'] ?? '', $data['un_number'] ?? '', $data['ghs_class'] ?? '', $data['hazard_statements'] ?? '', $data['precautionary_statements'] ?? '', $data['storage_requirements'] ?? '', $data['ppe_required'] ?? '', $data['msds_file'] ?? '', $data['max_quantity'] ?? 0, $data['current_qty'] ?? 0, $data['unit'] ?? 'kg', $data['location'] ?? '', $data['supplier'] ?? '', $data['expiry_date'] ?? null, 'approved', time()));
	return (int) $db->lastInsertId();
}

function epc_ehs_observation_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_ehs_observations` (`company_id`,`observation_no`,`category`,`location`,`department`,`observed_by`,`description`,`action_taken`,`severity`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['observation_no'] ?? '', $data['category'] ?? 'unsafe_condition', $data['location'] ?? '', $data['department'] ?? '', $data['observed_by'] ?? '', $data['description'] ?? '', $data['action_taken'] ?? '', $data['severity'] ?? 'medium', 'open', time()));
	return (int) $db->lastInsertId();
}

function epc_ehs_emergency_plan_save(PDO $db, array $data, int $id = 0): int
{
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_ehs_emergency_plans` SET `name`=?,`scenario`=?,`location`=?,`procedures`=?,`assembly_points`=?,`key_contacts`=?,`equipment_list`=?,`next_drill_date`=?,`drill_frequency_days`=?,`version`=?,`status`=? WHERE `id`=?')
			->execute(array($data['name'] ?? '', $data['scenario'] ?? 'fire', $data['location'] ?? '', $data['procedures'] ?? '', $data['assembly_points'] ?? '', $data['key_contacts'] ?? '', $data['equipment_list'] ?? '', $data['next_drill_date'] ?? null, (int) ($data['drill_frequency_days'] ?? 180), $data['version'] ?? '1.0', $data['status'] ?? 'draft', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_ehs_emergency_plans` (`company_id`,`name`,`scenario`,`location`,`procedures`,`assembly_points`,`key_contacts`,`equipment_list`,`next_drill_date`,`drill_frequency_days`,`version`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['name'] ?? '', $data['scenario'] ?? 'fire', $data['location'] ?? '', $data['procedures'] ?? '', $data['assembly_points'] ?? '', $data['key_contacts'] ?? '', $data['equipment_list'] ?? '', $data['next_drill_date'] ?? null, (int) ($data['drill_frequency_days'] ?? 180), $data['version'] ?? '1.0', 'draft', time()));
	return (int) $db->lastInsertId();
}

function epc_ehs_ppe_issue(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_ehs_ppe_tracking` (`company_id`,`employee_id`,`ppe_type`,`item_code`,`issued_date`,`expiry_date`,`condition`,`notes`) VALUES (?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['employee_id'] ?? 0), $data['ppe_type'] ?? 'helmet', $data['item_code'] ?? '', $data['issued_date'] ?? date('Y-m-d'), $data['expiry_date'] ?? null, 'new', $data['notes'] ?? ''));
	return (int) $db->lastInsertId();
}

function epc_ehs_ppe_expiring(PDO $db, int $companyId, int $days = 30): array
{
	$st = $db->prepare('SELECT p.*, e.`employee_name` FROM `epc_erp_ehs_ppe_tracking` p LEFT JOIN `epc_erp_ehs_training` e ON e.`employee_id` = p.`employee_id` AND e.`company_id` = p.`company_id` WHERE p.`company_id` = ? AND p.`returned_date` IS NULL AND p.`expiry_date` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL ? DAY) ORDER BY p.`expiry_date`');
	$st->execute(array($companyId, $days));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_ehs_corrective_action_complete(PDO $db, int $actionId, string $verification = ''): void
{
	$db->prepare('UPDATE `epc_erp_ehs_corrective_actions` SET `status` = "completed", `completed_date` = CURDATE(), `verification` = ? WHERE `id` = ?')
		->execute(array($verification, $actionId));
}

function epc_ehs_incident_close(PDO $db, int $incidentId, string $rootCause = ''): void
{
	$db->prepare('UPDATE `epc_erp_ehs_incidents` SET `status` = "closed", `closed_date` = CURDATE(), `root_cause` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($rootCause, time(), $incidentId));
}

function epc_ehs_ltifr(PDO $db, int $companyId, int $periodDays = 365): float
{
	$inc = $db->prepare('SELECT COUNT(*) AS lti FROM `epc_erp_ehs_incidents` WHERE `company_id` = ? AND `lost_days` > 0 AND `date_occurred` >= DATE_SUB(CURDATE(), INTERVAL ? DAY)');
	$inc->execute(array($companyId, $periodDays));
	$lti = (int) $inc->fetchColumn();
	$hours = $db->prepare('SELECT COUNT(*) * 2000 AS man_hours FROM `epc_erp_ehs_training` WHERE `company_id` = ?');
	$hours->execute(array($companyId));
	$manHours = max(1, (int) $hours->fetchColumn());
	return round($lti * 1000000 / $manHours, 2);
}

function epc_ehs_risk_matrix_render(PDO $db, int $companyId): array
{
	$st = $db->prepare('SELECT `likelihood`, `consequence`, COUNT(*) AS cnt, `risk_level` FROM `epc_erp_ehs_risk_assessments` WHERE `company_id` = ? AND `status` = "active" GROUP BY `likelihood`, `consequence`, `risk_level`');
	$st->execute(array($companyId));
	$cells = $st->fetchAll(PDO::FETCH_ASSOC);
	$matrix = array();
	for ($l = 1; $l <= 5; $l++) {
		for ($c = 1; $c <= 5; $c++) {
			$matrix[$l][$c] = array('count' => 0, 'level' => 'low');
		}
	}
	foreach ($cells as $cell) {
		$matrix[(int) $cell['likelihood']][(int) $cell['consequence']] = array('count' => (int) $cell['cnt'], 'level' => $cell['risk_level']);
	}
	return $matrix;
}

function epc_ehs_inspection_list(PDO $db, int $companyId, string $status = ''): array
{
	$sql = 'SELECT * FROM `epc_erp_ehs_inspections` WHERE `company_id` = ?';
	$params = array($companyId);
	if ($status !== '') {
		$sql .= ' AND `status` = ?';
		$params[] = $status;
	}
	$sql .= ' ORDER BY `inspection_date` DESC';
	$st = $db->prepare($sql);
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_ehs_training_matrix(PDO $db, int $companyId): array
{
	$st = $db->prepare('SELECT `employee_id`, `employee_name`, `category`, MAX(`completion_date`) AS last_completed, MIN(`expiry_date`) AS earliest_expiry, CASE WHEN MIN(`expiry_date`) < CURDATE() THEN "expired" WHEN MIN(`expiry_date`) < DATE_ADD(CURDATE(), INTERVAL 30 DAY) THEN "expiring" ELSE "valid" END AS cert_status FROM `epc_erp_ehs_training` WHERE `company_id` = ? GROUP BY `employee_id`, `employee_name`, `category` ORDER BY `employee_name`, `category`');
	$st->execute(array($companyId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}
