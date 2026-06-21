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
