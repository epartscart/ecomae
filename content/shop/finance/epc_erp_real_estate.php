<?php
/**
 * Real Estate & Lease Accounting — property register, lease contracts
 * (IFRS 16 / ASC 842), rent schedules, and property maintenance.
 *
 * Closes Oracle Property Manager + Oracle Lease Accounting + SAP RE gap.
 *
 * Tables: epc_erp_properties, epc_erp_leases, epc_erp_lease_schedules,
 *         epc_erp_property_maintenance
 */
defined('_ASTEXE_') or die('No access');

function epc_re_ensure_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_properties` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`code`         VARCHAR(30) NOT NULL DEFAULT "",
		`name`         VARCHAR(200) NOT NULL DEFAULT "",
		`type`         ENUM("office","warehouse","retail","land","residential","industrial","other") NOT NULL DEFAULT "office",
		`ownership`    ENUM("owned","leased","sub_leased") NOT NULL DEFAULT "leased",
		`address`      TEXT,
		`city`         VARCHAR(100) NOT NULL DEFAULT "",
		`country`      CHAR(2) NOT NULL DEFAULT "AE",
		`area_sqm`     DECIMAL(12,2) NOT NULL DEFAULT 0.00,
		`market_value` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`status`       ENUM("active","vacant","under_maintenance","disposed") NOT NULL DEFAULT "active",
		`acquired_date` DATE DEFAULT NULL,
		`disposed_date` DATE DEFAULT NULL,
		`notes`        TEXT,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_code` (`company_id`,`code`),
		INDEX `idx_type` (`type`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_leases` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`company_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`property_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`lease_no`     VARCHAR(40) NOT NULL DEFAULT "",
		`lessor`       VARCHAR(200) NOT NULL DEFAULT "",
		`lessee_type`  ENUM("we_lease","we_sublease") NOT NULL DEFAULT "we_lease",
		`start_date`   DATE NOT NULL,
		`end_date`     DATE NOT NULL,
		`monthly_rent` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`annual_rent`  DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`payment_freq` ENUM("monthly","quarterly","semi_annual","annual","upfront") NOT NULL DEFAULT "monthly",
		`escalation_pct` DECIMAL(5,2) NOT NULL DEFAULT 0.00,
		`deposit`      DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`ifrs16_rou`   DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`ifrs16_liability` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`discount_rate` DECIMAL(6,4) NOT NULL DEFAULT 0.0500,
		`status`       ENUM("active","expired","terminated","renewed") NOT NULL DEFAULT "active",
		`renewal_option` TINYINT(1) NOT NULL DEFAULT 0,
		`renewal_terms` TEXT,
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		`updated_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		UNIQUE KEY `uk_no` (`company_id`,`lease_no`),
		INDEX `idx_property` (`property_id`),
		INDEX `idx_dates` (`start_date`,`end_date`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_lease_schedules` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`lease_id`     INT UNSIGNED NOT NULL DEFAULT 0,
		`period_date`  DATE NOT NULL,
		`rent_amount`  DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`interest`     DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`principal`    DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`balance`      DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`depreciation` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`paid`         TINYINT(1) NOT NULL DEFAULT 0,
		`paid_date`    DATE DEFAULT NULL,
		`gl_posted`    TINYINT(1) NOT NULL DEFAULT 0,
		INDEX `idx_lease` (`lease_id`),
		INDEX `idx_period` (`period_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_property_maintenance` (
		`id`           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`property_id`  INT UNSIGNED NOT NULL DEFAULT 0,
		`type`         ENUM("preventive","corrective","emergency","inspection") NOT NULL DEFAULT "preventive",
		`description`  TEXT,
		`vendor`       VARCHAR(200) NOT NULL DEFAULT "",
		`scheduled_date` DATE DEFAULT NULL,
		`completed_date` DATE DEFAULT NULL,
		`cost`         DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`     CHAR(3) NOT NULL DEFAULT "AED",
		`status`       ENUM("planned","in_progress","completed","cancelled") NOT NULL DEFAULT "planned",
		`created_at`   INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_property` (`property_id`),
		INDEX `idx_status` (`status`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_re_property_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_properties` SET `code`=?,`name`=?,`type`=?,`ownership`=?,`address`=?,`city`=?,`country`=?,`area_sqm`=?,`market_value`=?,`currency`=?,`status`=?,`acquired_date`=?,`disposed_date`=?,`notes`=? WHERE `id`=?')
			->execute(array($data['code'] ?? '', $data['name'] ?? '', $data['type'] ?? 'office', $data['ownership'] ?? 'leased', $data['address'] ?? '', $data['city'] ?? '', $data['country'] ?? 'AE', $data['area_sqm'] ?? 0, $data['market_value'] ?? 0, $data['currency'] ?? 'AED', $data['status'] ?? 'active', $data['acquired_date'] ?? null, $data['disposed_date'] ?? null, $data['notes'] ?? '', $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_properties` (`company_id`,`code`,`name`,`type`,`ownership`,`address`,`city`,`country`,`area_sqm`,`market_value`,`currency`,`status`,`acquired_date`,`notes`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), $data['code'] ?? '', $data['name'] ?? '', $data['type'] ?? 'office', $data['ownership'] ?? 'leased', $data['address'] ?? '', $data['city'] ?? '', $data['country'] ?? 'AE', $data['area_sqm'] ?? 0, $data['market_value'] ?? 0, $data['currency'] ?? 'AED', 'active', $data['acquired_date'] ?? null, $data['notes'] ?? '', $now));
	return (int) $db->lastInsertId();
}

function epc_re_property_list(PDO $db, int $companyId = 0): array
{
	$sql = 'SELECT p.*, (SELECT COUNT(*) FROM `epc_erp_leases` l WHERE l.`property_id` = p.`id` AND l.`status` = "active") AS active_leases FROM `epc_erp_properties` p WHERE 1=1';
	if ($companyId > 0) {
		$sql .= ' AND p.`company_id` = ' . (int) $companyId;
	}
	$sql .= ' ORDER BY p.`name`';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_re_lease_save(PDO $db, array $data, int $id = 0): int
{
	$now = time();
	$annual = (float) ($data['annual_rent'] ?? 0);
	$monthly = (float) ($data['monthly_rent'] ?? 0);
	if ($annual > 0 && $monthly == 0) {
		$monthly = round($annual / 12, 2);
	} elseif ($monthly > 0 && $annual == 0) {
		$annual = round($monthly * 12, 2);
	}

	if ($id > 0) {
		$db->prepare('UPDATE `epc_erp_leases` SET `property_id`=?,`lease_no`=?,`lessor`=?,`lessee_type`=?,`start_date`=?,`end_date`=?,`monthly_rent`=?,`annual_rent`=?,`currency`=?,`payment_freq`=?,`escalation_pct`=?,`deposit`=?,`ifrs16_rou`=?,`ifrs16_liability`=?,`discount_rate`=?,`status`=?,`renewal_option`=?,`renewal_terms`=?,`updated_at`=? WHERE `id`=?')
			->execute(array((int) ($data['property_id'] ?? 0), $data['lease_no'] ?? '', $data['lessor'] ?? '', $data['lessee_type'] ?? 'we_lease', $data['start_date'] ?? date('Y-m-d'), $data['end_date'] ?? date('Y-m-d'), $monthly, $annual, $data['currency'] ?? 'AED', $data['payment_freq'] ?? 'monthly', $data['escalation_pct'] ?? 0, $data['deposit'] ?? 0, $data['ifrs16_rou'] ?? 0, $data['ifrs16_liability'] ?? 0, $data['discount_rate'] ?? 0.05, $data['status'] ?? 'active', isset($data['renewal_option']) ? (int) $data['renewal_option'] : 0, $data['renewal_terms'] ?? '', $now, $id));
		return $id;
	}
	$db->prepare('INSERT INTO `epc_erp_leases` (`company_id`,`property_id`,`lease_no`,`lessor`,`lessee_type`,`start_date`,`end_date`,`monthly_rent`,`annual_rent`,`currency`,`payment_freq`,`escalation_pct`,`deposit`,`ifrs16_rou`,`ifrs16_liability`,`discount_rate`,`status`,`renewal_option`,`renewal_terms`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['company_id'] ?? 0), (int) ($data['property_id'] ?? 0), $data['lease_no'] ?? '', $data['lessor'] ?? '', $data['lessee_type'] ?? 'we_lease', $data['start_date'] ?? date('Y-m-d'), $data['end_date'] ?? date('Y-m-d'), $monthly, $annual, $data['currency'] ?? 'AED', $data['payment_freq'] ?? 'monthly', $data['escalation_pct'] ?? 0, $data['deposit'] ?? 0, $data['ifrs16_rou'] ?? 0, $data['ifrs16_liability'] ?? 0, $data['discount_rate'] ?? 0.05, 'active', isset($data['renewal_option']) ? (int) $data['renewal_option'] : 0, $data['renewal_terms'] ?? '', $now, $now));
	return (int) $db->lastInsertId();
}

function epc_re_ifrs16_calculate(float $monthlyRent, int $termMonths, float $discountRate): array
{
	$monthlyRate = $discountRate / 12;
	if ($monthlyRate <= 0) {
		$pv = $monthlyRent * $termMonths;
	} else {
		$pv = $monthlyRent * (1 - pow(1 + $monthlyRate, -$termMonths)) / $monthlyRate;
	}
	$rou = round($pv, 2);
	$monthlyDepr = round($rou / $termMonths, 2);

	$schedule = array();
	$balance = $rou;
	for ($m = 1; $m <= $termMonths; $m++) {
		$interest = round($balance * $monthlyRate, 2);
		$principal = round($monthlyRent - $interest, 2);
		$balance = round($balance - $principal, 2);
		if ($balance < 0) {
			$balance = 0;
		}
		$schedule[] = array(
			'month' => $m,
			'rent' => $monthlyRent,
			'interest' => $interest,
			'principal' => $principal,
			'balance' => $balance,
			'depreciation' => $monthlyDepr,
		);
	}

	return array(
		'rou_asset' => $rou,
		'lease_liability' => $rou,
		'monthly_depreciation' => $monthlyDepr,
		'schedule' => $schedule,
	);
}

function epc_re_lease_generate_schedule(PDO $db, int $leaseId): int
{
	$st = $db->prepare('SELECT * FROM `epc_erp_leases` WHERE `id` = ?');
	$st->execute(array($leaseId));
	$lease = $st->fetch(PDO::FETCH_ASSOC);
	if (!$lease) {
		return 0;
	}

	$start = new DateTime($lease['start_date']);
	$end = new DateTime($lease['end_date']);
	$termMonths = (int) $start->diff($end)->format('%m') + (int) $start->diff($end)->format('%y') * 12;
	if ($termMonths < 1) {
		$termMonths = 1;
	}

	$calc = epc_re_ifrs16_calculate((float) $lease['monthly_rent'], $termMonths, (float) $lease['discount_rate']);

	$db->prepare('UPDATE `epc_erp_leases` SET `ifrs16_rou` = ?, `ifrs16_liability` = ?, `updated_at` = ? WHERE `id` = ?')
		->execute(array($calc['rou_asset'], $calc['lease_liability'], time(), $leaseId));

	$db->prepare('DELETE FROM `epc_erp_lease_schedules` WHERE `lease_id` = ?')->execute(array($leaseId));

	$count = 0;
	$periodDate = clone $start;
	foreach ($calc['schedule'] as $line) {
		$db->prepare('INSERT INTO `epc_erp_lease_schedules` (`lease_id`,`period_date`,`rent_amount`,`interest`,`principal`,`balance`,`depreciation`) VALUES (?,?,?,?,?,?,?)')
			->execute(array($leaseId, $periodDate->format('Y-m-d'), $line['rent'], $line['interest'], $line['principal'], $line['balance'], $line['depreciation']));
		$periodDate->modify('+1 month');
		$count++;
	}
	return $count;
}

function epc_re_lease_schedule(PDO $db, int $leaseId): array
{
	$st = $db->prepare('SELECT * FROM `epc_erp_lease_schedules` WHERE `lease_id` = ? ORDER BY `period_date`');
	$st->execute(array($leaseId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_re_lease_list(PDO $db, int $companyId = 0, string $status = ''): array
{
	$sql = 'SELECT l.*, p.`name` AS property_name, p.`type` AS property_type FROM `epc_erp_leases` l LEFT JOIN `epc_erp_properties` p ON p.`id` = l.`property_id` WHERE 1=1';
	if ($companyId > 0) {
		$sql .= ' AND l.`company_id` = ' . (int) $companyId;
	}
	if ($status !== '') {
		$sql .= ' AND l.`status` = "' . preg_replace('/[^a-z_]/', '', $status) . '"';
	}
	$sql .= ' ORDER BY l.`end_date` ASC';
	return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_re_maintenance_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_property_maintenance` (`property_id`,`type`,`description`,`vendor`,`scheduled_date`,`cost`,`currency`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['property_id'] ?? 0), $data['type'] ?? 'preventive', $data['description'] ?? '', $data['vendor'] ?? '', $data['scheduled_date'] ?? null, $data['cost'] ?? 0, $data['currency'] ?? 'AED', 'planned', time()));
	return (int) $db->lastInsertId();
}

function epc_re_portfolio_summary(PDO $db, int $companyId): array
{
	$props = $db->prepare('SELECT COUNT(*) AS total, SUM(`area_sqm`) AS total_area, SUM(`market_value`) AS total_value FROM `epc_erp_properties` WHERE `company_id` = ? AND `status` != "disposed"');
	$props->execute(array($companyId));
	$propRow = $props->fetch(PDO::FETCH_ASSOC);

	$leases = $db->prepare('SELECT COUNT(*) AS active_leases, SUM(`annual_rent`) AS annual_rent_total, SUM(`ifrs16_rou`) AS total_rou, SUM(`ifrs16_liability`) AS total_liability FROM `epc_erp_leases` WHERE `company_id` = ? AND `status` = "active"');
	$leases->execute(array($companyId));
	$leaseRow = $leases->fetch(PDO::FETCH_ASSOC);

	return array_merge($propRow, $leaseRow);
}

function epc_re_ensure_depth_schema(PDO $db): void
{
	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_lease_modifications` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`lease_id`      INT UNSIGNED NOT NULL DEFAULT 0,
		`mod_type`      ENUM("term_extension","term_reduction","rent_change","scope_change","discount_rate_change","termination") NOT NULL DEFAULT "rent_change",
		`effective_date` DATE NOT NULL,
		`old_monthly_rent` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`new_monthly_rent` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`old_end_date`  DATE DEFAULT NULL,
		`new_end_date`  DATE DEFAULT NULL,
		`old_discount_rate` DECIMAL(6,4) NOT NULL DEFAULT 0.0000,
		`new_discount_rate` DECIMAL(6,4) NOT NULL DEFAULT 0.0000,
		`remeasured_rou` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`remeasured_liability` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`adjustment_amount` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`reason`        TEXT,
		`approved_by`   VARCHAR(100) NOT NULL DEFAULT "",
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_lease` (`lease_id`),
		INDEX `idx_date` (`effective_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_property_valuations` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`property_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`valuation_date` DATE NOT NULL,
		`method`        ENUM("market_comparison","income_capitalization","cost_approach","discounted_cashflow") NOT NULL DEFAULT "market_comparison",
		`valuer`        VARCHAR(200) NOT NULL DEFAULT "",
		`valuer_license` VARCHAR(60) NOT NULL DEFAULT "",
		`previous_value` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`new_value`     DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`gain_loss`     DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`notes`         TEXT,
		`gl_posted`     TINYINT(1) NOT NULL DEFAULT 0,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_property` (`property_id`),
		INDEX `idx_date` (`valuation_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_property_revenue` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`property_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`lease_id`      INT UNSIGNED DEFAULT NULL,
		`revenue_type`  ENUM("base_rent","cam_charges","parking","utilities_recovery","service_charge","penalty","other") NOT NULL DEFAULT "base_rent",
		`period_from`   DATE NOT NULL,
		`period_to`     DATE NOT NULL,
		`amount`        DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`invoice_ref`   VARCHAR(40) NOT NULL DEFAULT "",
		`received`      TINYINT(1) NOT NULL DEFAULT 0,
		`received_date` DATE DEFAULT NULL,
		`gl_posted`     TINYINT(1) NOT NULL DEFAULT 0,
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_property` (`property_id`),
		INDEX `idx_lease` (`lease_id`),
		INDEX `idx_period` (`period_from`,`period_to`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');

	$db->exec('CREATE TABLE IF NOT EXISTS `epc_erp_property_insurance` (
		`id`            INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
		`property_id`   INT UNSIGNED NOT NULL DEFAULT 0,
		`policy_no`     VARCHAR(60) NOT NULL DEFAULT "",
		`insurer`       VARCHAR(200) NOT NULL DEFAULT "",
		`type`          ENUM("building","contents","liability","fire","flood","earthquake","all_risk") NOT NULL DEFAULT "all_risk",
		`coverage_amount` DECIMAL(16,2) NOT NULL DEFAULT 0.00,
		`premium_annual` DECIMAL(14,2) NOT NULL DEFAULT 0.00,
		`currency`      CHAR(3) NOT NULL DEFAULT "AED",
		`start_date`    DATE DEFAULT NULL,
		`end_date`      DATE DEFAULT NULL,
		`status`        ENUM("active","expired","cancelled","pending_renewal") NOT NULL DEFAULT "active",
		`created_at`    INT UNSIGNED NOT NULL DEFAULT 0,
		INDEX `idx_property` (`property_id`),
		INDEX `idx_expiry` (`end_date`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function epc_re_lease_modify(PDO $db, int $leaseId, array $data): int
{
	$lease = $db->prepare('SELECT * FROM `epc_erp_leases` WHERE `id` = ?');
	$lease->execute(array($leaseId));
	$old = $lease->fetch(PDO::FETCH_ASSOC);
	if (!$old) {
		return 0;
	}

	$newRent = (float) ($data['new_monthly_rent'] ?? $old['monthly_rent']);
	$newEnd = $data['new_end_date'] ?? $old['end_date'];
	$newRate = (float) ($data['new_discount_rate'] ?? $old['discount_rate']);

	$start = new DateTime($data['effective_date'] ?? date('Y-m-d'));
	$end = new DateTime($newEnd);
	$remainMonths = max(1, (int) $start->diff($end)->format('%m') + (int) $start->diff($end)->format('%y') * 12);
	$calc = epc_re_ifrs16_calculate($newRent, $remainMonths, $newRate);

	$oldLiab = (float) $old['ifrs16_liability'];
	$adjustment = round($calc['lease_liability'] - $oldLiab, 2);

	$db->prepare('INSERT INTO `epc_erp_lease_modifications` (`lease_id`,`mod_type`,`effective_date`,`old_monthly_rent`,`new_monthly_rent`,`old_end_date`,`new_end_date`,`old_discount_rate`,`new_discount_rate`,`remeasured_rou`,`remeasured_liability`,`adjustment_amount`,`reason`,`approved_by`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array($leaseId, $data['mod_type'] ?? 'rent_change', $data['effective_date'] ?? date('Y-m-d'), (float) $old['monthly_rent'], $newRent, $old['end_date'], $newEnd, (float) $old['discount_rate'], $newRate, $calc['rou_asset'], $calc['lease_liability'], $adjustment, $data['reason'] ?? '', $data['approved_by'] ?? '', time()));
	$modId = (int) $db->lastInsertId();

	$db->prepare('UPDATE `epc_erp_leases` SET `monthly_rent`=?,`annual_rent`=?,`end_date`=?,`discount_rate`=?,`ifrs16_rou`=?,`ifrs16_liability`=?,`updated_at`=? WHERE `id`=?')
		->execute(array($newRent, round($newRent * 12, 2), $newEnd, $newRate, $calc['rou_asset'], $calc['lease_liability'], time(), $leaseId));

	epc_re_lease_generate_schedule($db, $leaseId);
	return $modId;
}

function epc_re_escalation_process(PDO $db, int $companyId = 0): int
{
	$sql = 'SELECT * FROM `epc_erp_leases` WHERE `status` = "active" AND `escalation_pct` > 0';
	if ($companyId > 0) {
		$sql .= ' AND `company_id` = ' . (int) $companyId;
	}
	$leases = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
	$processed = 0;
	$today = date('Y-m-d');
	foreach ($leases as $lease) {
		$start = new DateTime($lease['start_date']);
		$diff = $start->diff(new DateTime($today));
		$yearsElapsed = (int) $diff->format('%y');
		if ($yearsElapsed < 1) {
			continue;
		}
		$anniversary = clone $start;
		$anniversary->modify('+' . $yearsElapsed . ' years');
		if ($anniversary->format('Y-m-d') !== $today) {
			continue;
		}
		$oldRent = (float) $lease['monthly_rent'];
		$escalation = (float) $lease['escalation_pct'];
		$newRent = round($oldRent * (1 + $escalation / 100), 2);
		epc_re_lease_modify($db, (int) $lease['id'], array(
			'mod_type' => 'rent_change',
			'effective_date' => $today,
			'new_monthly_rent' => $newRent,
			'reason' => 'Annual escalation ' . $escalation . '% (year ' . $yearsElapsed . ')',
		));
		$processed++;
	}
	return $processed;
}

function epc_re_valuation_save(PDO $db, array $data): int
{
	$propId = (int) ($data['property_id'] ?? 0);
	$oldVal = 0;
	if ($propId > 0) {
		$st = $db->prepare('SELECT `market_value` FROM `epc_erp_properties` WHERE `id` = ?');
		$st->execute(array($propId));
		$oldVal = (float) ($st->fetchColumn() ?: 0);
	}
	$newVal = (float) ($data['new_value'] ?? 0);
	$gainLoss = round($newVal - $oldVal, 2);
	$db->prepare('INSERT INTO `epc_erp_property_valuations` (`property_id`,`valuation_date`,`method`,`valuer`,`valuer_license`,`previous_value`,`new_value`,`gain_loss`,`currency`,`notes`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array($propId, $data['valuation_date'] ?? date('Y-m-d'), $data['method'] ?? 'market_comparison', $data['valuer'] ?? '', $data['valuer_license'] ?? '', $oldVal, $newVal, $gainLoss, $data['currency'] ?? 'AED', $data['notes'] ?? '', time()));
	if ($propId > 0) {
		$db->prepare('UPDATE `epc_erp_properties` SET `market_value` = ? WHERE `id` = ?')
			->execute(array($newVal, $propId));
	}
	return (int) $db->lastInsertId();
}

function epc_re_revenue_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_property_revenue` (`property_id`,`lease_id`,`revenue_type`,`period_from`,`period_to`,`amount`,`currency`,`invoice_ref`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['property_id'] ?? 0), $data['lease_id'] ?? null, $data['revenue_type'] ?? 'base_rent', $data['period_from'] ?? date('Y-m-01'), $data['period_to'] ?? date('Y-m-t'), $data['amount'] ?? 0, $data['currency'] ?? 'AED', $data['invoice_ref'] ?? '', time()));
	return (int) $db->lastInsertId();
}

function epc_re_insurance_save(PDO $db, array $data): int
{
	$db->prepare('INSERT INTO `epc_erp_property_insurance` (`property_id`,`policy_no`,`insurer`,`type`,`coverage_amount`,`premium_annual`,`currency`,`start_date`,`end_date`,`status`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?)')
		->execute(array((int) ($data['property_id'] ?? 0), $data['policy_no'] ?? '', $data['insurer'] ?? '', $data['type'] ?? 'all_risk', $data['coverage_amount'] ?? 0, $data['premium_annual'] ?? 0, $data['currency'] ?? 'AED', $data['start_date'] ?? null, $data['end_date'] ?? null, 'active', time()));
	return (int) $db->lastInsertId();
}

function epc_re_lease_expiry_alerts(PDO $db, int $companyId, int $daysBefore = 90): array
{
	$st = $db->prepare('SELECT l.*, p.`name` AS property_name, DATEDIFF(l.`end_date`, CURDATE()) AS days_remaining FROM `epc_erp_leases` l LEFT JOIN `epc_erp_properties` p ON p.`id` = l.`property_id` WHERE l.`company_id` = ? AND l.`status` = "active" AND l.`end_date` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL ? DAY) ORDER BY l.`end_date`');
	$st->execute(array($companyId, $daysBefore));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_re_schedule_mark_paid(PDO $db, int $scheduleId, string $paidDate = ''): void
{
	if ($paidDate === '') {
		$paidDate = date('Y-m-d');
	}
	$db->prepare('UPDATE `epc_erp_lease_schedules` SET `paid` = 1, `paid_date` = ? WHERE `id` = ?')
		->execute(array($paidDate, $scheduleId));
}

function epc_re_occupancy_cost(PDO $db, int $companyId): array
{
	$st = $db->prepare('SELECT p.`id`, p.`name`, p.`area_sqm`, l.`annual_rent`, CASE WHEN p.`area_sqm` > 0 THEN ROUND(l.`annual_rent` / p.`area_sqm`, 2) ELSE 0 END AS cost_per_sqm, (SELECT SUM(m.`cost`) FROM `epc_erp_property_maintenance` m WHERE m.`property_id` = p.`id` AND m.`status` = "completed") AS maintenance_cost FROM `epc_erp_properties` p LEFT JOIN `epc_erp_leases` l ON l.`property_id` = p.`id` AND l.`status` = "active" WHERE p.`company_id` = ? AND p.`status` != "disposed" ORDER BY cost_per_sqm DESC');
	$st->execute(array($companyId));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_re_maintenance_complete(PDO $db, int $maintId, string $completedDate = ''): void
{
	if ($completedDate === '') {
		$completedDate = date('Y-m-d');
	}
	$db->prepare('UPDATE `epc_erp_property_maintenance` SET `status` = "completed", `completed_date` = ? WHERE `id` = ?')
		->execute(array($completedDate, $maintId));
}

function epc_re_dashboard(PDO $db, int $companyId): array
{
	$summary = epc_re_portfolio_summary($db, $companyId);
	$expiring = epc_re_lease_expiry_alerts($db, $companyId, 90);
	$overdueMaint = $db->prepare('SELECT COUNT(*) AS overdue_maintenance FROM `epc_erp_property_maintenance` WHERE `property_id` IN (SELECT `id` FROM `epc_erp_properties` WHERE `company_id` = ?) AND `status` = "planned" AND `scheduled_date` < CURDATE()');
	$overdueMaint->execute(array($companyId));
	$maintRow = $overdueMaint->fetch(PDO::FETCH_ASSOC);
	$ins = $db->prepare('SELECT COUNT(*) AS expiring_insurance FROM `epc_erp_property_insurance` WHERE `property_id` IN (SELECT `id` FROM `epc_erp_properties` WHERE `company_id` = ?) AND `status` = "active" AND `end_date` BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY)');
	$ins->execute(array($companyId));
	$insRow = $ins->fetch(PDO::FETCH_ASSOC);
	$unpaid = $db->prepare('SELECT COUNT(*) AS unpaid_schedules, SUM(`rent_amount`) AS unpaid_rent FROM `epc_erp_lease_schedules` WHERE `lease_id` IN (SELECT `id` FROM `epc_erp_leases` WHERE `company_id` = ?) AND `paid` = 0 AND `period_date` <= CURDATE()');
	$unpaid->execute(array($companyId));
	$unpaidRow = $unpaid->fetch(PDO::FETCH_ASSOC);
	return array_merge($summary, array('expiring_leases' => count($expiring)), $maintRow, $insRow, $unpaidRow);
}
