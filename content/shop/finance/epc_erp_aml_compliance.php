<?php
/**
 * AML Compliance Module — KYC, monitoring, STR/CTR, legislation, reports.
 * UAE DNFBP / goAML oriented (Federal Decree-Law 20/2018 & successors) with FATF baseline.
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_aml_ensure_schema')) {
	function epc_aml_ensure_schema(PDO $db): void
	{
		static $done = false;
		if ($done) {
			return;
		}
		$db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_kyc` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`company_id` int(11) NOT NULL DEFAULT 0,
			`customer_id` int(11) NOT NULL DEFAULT 0,
			`customer_name` varchar(200) NOT NULL DEFAULT '',
			`id_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'passport,emirates_id,national_id,driving_license,trade_license',
			`id_number` varchar(100) NOT NULL DEFAULT '',
			`id_expiry` date DEFAULT NULL,
			`id_document_path` varchar(500) NOT NULL DEFAULT '',
			`nationality` varchar(50) NOT NULL DEFAULT '',
			`dob` date DEFAULT NULL,
			`risk_level` enum('low','medium','high','very_high') NOT NULL DEFAULT 'low',
			`pep_status` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'Politically Exposed Person',
			`sanctions_checked` tinyint(1) NOT NULL DEFAULT 0,
			`sanctions_match` tinyint(1) NOT NULL DEFAULT 0,
			`verification_status` enum('pending','verified','rejected','expired') NOT NULL DEFAULT 'pending',
			`verified_by` int(11) NOT NULL DEFAULT 0,
			`verified_at` datetime DEFAULT NULL,
			`next_review` date DEFAULT NULL,
			`notes` text,
			`time_created` int(11) NOT NULL DEFAULT 0,
			`time_updated` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_company` (`company_id`),
			KEY `x_customer` (`customer_id`),
			KEY `x_risk` (`risk_level`),
			KEY `x_status` (`verification_status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='KYC records'");

		$db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_transactions` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`company_id` int(11) NOT NULL DEFAULT 0,
			`customer_id` int(11) NOT NULL DEFAULT 0,
			`customer_name` varchar(200) NOT NULL DEFAULT '',
			`transaction_type` varchar(30) NOT NULL DEFAULT '' COMMENT 'cash_sale,wire_transfer,card_payment,crypto',
			`amount` decimal(14,2) NOT NULL DEFAULT 0.00,
			`currency` varchar(3) NOT NULL DEFAULT 'AED',
			`reference` varchar(100) NOT NULL DEFAULT '',
			`risk_score` int(11) NOT NULL DEFAULT 0 COMMENT '0-100',
			`flagged` tinyint(1) NOT NULL DEFAULT 0,
			`flag_reason` varchar(300) NOT NULL DEFAULT '',
			`review_status` varchar(30) NOT NULL DEFAULT 'open' COMMENT 'open,reviewed,escalated,closed',
			`sar_filed` tinyint(1) NOT NULL DEFAULT 0 COMMENT 'Suspicious Activity Report filed',
			`sar_reference` varchar(64) NOT NULL DEFAULT '',
			`reviewed_by` int(11) NOT NULL DEFAULT 0,
			`reviewed_at` datetime DEFAULT NULL,
			`time_created` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_company` (`company_id`),
			KEY `x_customer` (`customer_id`),
			KEY `x_flagged` (`flagged`),
			KEY `x_amount` (`amount`),
			KEY `x_review` (`review_status`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AML transaction monitoring'");

		$db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_rules` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`company_id` int(11) NOT NULL DEFAULT 0,
			`rule_name` varchar(200) NOT NULL DEFAULT '',
			`rule_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'threshold,pattern,frequency,country,pep',
			`threshold_amount` decimal(14,2) NOT NULL DEFAULT 0.00,
			`threshold_currency` varchar(3) NOT NULL DEFAULT 'AED',
			`frequency_count` int(11) NOT NULL DEFAULT 0,
			`frequency_period_days` int(11) NOT NULL DEFAULT 1,
			`countries_list` text COMMENT 'JSON array of high-risk countries',
			`action` varchar(50) NOT NULL DEFAULT 'flag' COMMENT 'flag,block,alert,report',
			`is_active` tinyint(1) NOT NULL DEFAULT 1,
			`time_created` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_company` (`company_id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AML detection rules'");

		$db->exec("CREATE TABLE IF NOT EXISTS `epc_aml_reports` (
			`id` int(11) NOT NULL AUTO_INCREMENT,
			`company_id` int(11) NOT NULL DEFAULT 0,
			`report_type` varchar(50) NOT NULL DEFAULT '' COMMENT 'sar,ctr,goaml,compliance_summary',
			`title` varchar(200) NOT NULL DEFAULT '',
			`period_from` date DEFAULT NULL,
			`period_to` date DEFAULT NULL,
			`total_transactions` int(11) NOT NULL DEFAULT 0,
			`flagged_transactions` int(11) NOT NULL DEFAULT 0,
			`sar_count` int(11) NOT NULL DEFAULT 0,
			`summary_json` mediumtext,
			`body_html` mediumtext,
			`filed_to` varchar(200) NOT NULL DEFAULT '' COMMENT 'FIU, CBUAE, etc.',
			`file_reference` varchar(100) NOT NULL DEFAULT '',
			`generated_by` int(11) NOT NULL DEFAULT 0,
			`time_created` int(11) NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `x_company` (`company_id`),
			KEY `x_type` (`report_type`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='AML compliance reports'");

		// Best-effort column upgrades for older installs.
		foreach (array(
			array('epc_aml_kyc', 'next_review', "date DEFAULT NULL"),
			array('epc_aml_transactions', 'customer_name', "varchar(200) NOT NULL DEFAULT ''"),
			array('epc_aml_transactions', 'review_status', "varchar(30) NOT NULL DEFAULT 'open'"),
			array('epc_aml_reports', 'title', "varchar(200) NOT NULL DEFAULT ''"),
			array('epc_aml_reports', 'summary_json', 'mediumtext'),
			array('epc_aml_reports', 'body_html', 'mediumtext'),
		) as $col) {
			try {
				$chk = $db->query("SHOW COLUMNS FROM `{$col[0]}` LIKE " . $db->quote($col[1]));
				if ($chk && !$chk->fetchColumn()) {
					$db->exec("ALTER TABLE `{$col[0]}` ADD COLUMN `{$col[1]}` {$col[2]}");
				}
			} catch (Throwable $e) {
				// ignore
			}
		}
		$done = true;
	}
}

if (!function_exists('epc_aml_company_id')) {
	function epc_aml_company_id(PDO $db): int
	{
		if (!function_exists('epc_erp_active_company_id')) {
			$ctx = __DIR__ . '/epc_erp_company_context.php';
			if (is_file($ctx)) {
				require_once $ctx;
			}
		}
		if (function_exists('epc_erp_active_company_id')) {
			return (int) epc_erp_active_company_id($db);
		}
		return 0;
	}
}

if (!function_exists('epc_aml_setting_get')) {
	function epc_aml_setting_get(PDO $db, string $key, string $default = ''): string
	{
		if (!function_exists('epc_erp_adv_get_setting')) {
			require_once __DIR__ . '/epc_erp_advanced.php';
		}
		return epc_erp_adv_get_setting($db, 'aml_' . $key, $default);
	}
}

if (!function_exists('epc_aml_setting_set')) {
	function epc_aml_setting_set(PDO $db, string $key, string $value): void
	{
		if (!function_exists('epc_erp_adv_set_setting')) {
			require_once __DIR__ . '/epc_erp_advanced.php';
		}
		epc_erp_adv_set_setting($db, 'aml_' . $key, $value);
	}
}

if (!function_exists('epc_aml_default_rules')) {
	function epc_aml_default_rules(int $companyId): array
	{
		return array(
			array('rule_name' => 'Cash transaction ≥ 55,000 AED (DPMS / CTR)', 'rule_type' => 'threshold', 'threshold_amount' => 55000, 'threshold_currency' => 'AED', 'action' => 'report', 'frequency_count' => 0, 'frequency_period_days' => 1),
			array('rule_name' => 'Wire transfer ≥ 100,000 AED', 'rule_type' => 'threshold', 'threshold_amount' => 100000, 'threshold_currency' => 'AED', 'action' => 'flag', 'frequency_count' => 0, 'frequency_period_days' => 1),
			array('rule_name' => 'More than 5 cash transactions in 7 days', 'rule_type' => 'frequency', 'threshold_amount' => 0, 'threshold_currency' => 'AED', 'action' => 'flag', 'frequency_count' => 5, 'frequency_period_days' => 7),
			array('rule_name' => 'Structuring — 3+ payments in 24h near cash threshold', 'rule_type' => 'frequency', 'threshold_amount' => 0, 'threshold_currency' => 'AED', 'action' => 'flag', 'frequency_count' => 3, 'frequency_period_days' => 1),
			array('rule_name' => 'High-risk country origin', 'rule_type' => 'country', 'threshold_amount' => 0, 'threshold_currency' => 'AED', 'action' => 'flag', 'frequency_count' => 0, 'frequency_period_days' => 1),
		);
	}
}

if (!function_exists('epc_aml_seed_rules')) {
	/** @return array{ok:bool,created:int,message:string} */
	function epc_aml_seed_rules(PDO $db, int $companyId = 0): array
	{
		epc_aml_ensure_schema($db);
		if ($companyId <= 0) {
			$companyId = epc_aml_company_id($db);
		}
		$st = $db->prepare('SELECT COUNT(*) FROM `epc_aml_rules` WHERE `company_id` = ?');
		$st->execute(array($companyId));
		if ((int) $st->fetchColumn() > 0) {
			return array('ok' => true, 'created' => 0, 'message' => 'Rules already configured');
		}
		$now = time();
		$ins = $db->prepare(
			'INSERT INTO `epc_aml_rules`
			(`company_id`,`rule_name`,`rule_type`,`threshold_amount`,`threshold_currency`,`frequency_count`,`frequency_period_days`,`action`,`is_active`,`time_created`)
			VALUES (?,?,?,?,?,?,?,?,1,?)'
		);
		$n = 0;
		foreach (epc_aml_default_rules($companyId) as $r) {
			$ins->execute(array(
				$companyId,
				$r['rule_name'],
				$r['rule_type'],
				(float) $r['threshold_amount'],
				$r['threshold_currency'],
				(int) $r['frequency_count'],
				(int) $r['frequency_period_days'],
				$r['action'],
				$now,
			));
			$n++;
		}
		if (epc_aml_setting_get($db, 'cash_threshold', '') === '') {
			epc_aml_setting_set($db, 'cash_threshold', '55000');
		}
		if (epc_aml_setting_get($db, 'authority', '') === '') {
			epc_aml_setting_set($db, 'authority', 'UAE FIU (goAML)');
		}
		return array('ok' => true, 'created' => $n, 'message' => 'Seeded ' . $n . ' default AML rules');
	}
}

if (!function_exists('epc_aml_check_transaction')) {
	function epc_aml_check_transaction(PDO $db, int $companyId, int $customerId, float $amount, string $currency = 'AED', array $extra = array()): array
	{
		epc_aml_ensure_schema($db);
		epc_aml_seed_rules($db, $companyId);
		$flags = array();
		$riskScore = 0;
		$currency = strtoupper(substr($currency !== '' ? $currency : 'AED', 0, 3));

		$st = $db->prepare('SELECT * FROM `epc_aml_rules` WHERE `company_id` = ? AND `is_active` = 1');
		$st->execute(array($companyId));
		$rules = $st->fetchAll(PDO::FETCH_ASSOC);
		if (!$rules) {
			$thr = (float) epc_aml_setting_get($db, 'cash_threshold', '55000');
			if ($amount >= $thr && $currency === 'AED') {
				$flags[] = 'Exceeds cash reporting threshold (' . number_format($thr, 0) . ' AED)';
				$riskScore += 40;
			}
		}
		foreach ($rules as $rule) {
			if ($rule['rule_type'] === 'threshold' && $amount >= (float) $rule['threshold_amount'] && $currency === $rule['threshold_currency']) {
				$flags[] = 'Exceeds threshold: ' . $rule['rule_name'];
				$riskScore += ($rule['action'] === 'report') ? 40 : 30;
			}
			if ($rule['rule_type'] === 'frequency') {
				$since = time() - (max(1, (int) $rule['frequency_period_days']) * 86400);
				$countStmt = $db->prepare(
					'SELECT COUNT(*) FROM `epc_aml_transactions`
					 WHERE `company_id` = ? AND `customer_id` = ? AND `time_created` > ?'
				);
				$countStmt->execute(array($companyId, $customerId, $since));
				if ((int) $countStmt->fetchColumn() >= (int) $rule['frequency_count']) {
					$flags[] = 'Frequency exceeded: ' . $rule['rule_name'];
					$riskScore += 25;
				}
			}
		}

		$kyc = $db->prepare(
			'SELECT `risk_level`, `pep_status`, `sanctions_match`, `verification_status`, `customer_name`
			 FROM `epc_aml_kyc` WHERE `company_id` = ? AND `customer_id` = ? ORDER BY `time_created` DESC LIMIT 1'
		);
		$kyc->execute(array($companyId, $customerId));
		$kycRow = $kyc->fetch(PDO::FETCH_ASSOC) ?: array();
		if ($kycRow) {
			if (($kycRow['risk_level'] ?? '') === 'high') {
				$riskScore += 20;
			}
			if (($kycRow['risk_level'] ?? '') === 'very_high') {
				$riskScore += 40;
			}
			if (!empty($kycRow['pep_status'])) {
				$flags[] = 'Customer is marked PEP';
				$riskScore += 15;
			}
			if (!empty($kycRow['sanctions_match'])) {
				$flags[] = 'Sanctions match on KYC file';
				$riskScore += 50;
			}
			if (($kycRow['verification_status'] ?? '') === 'pending' || ($kycRow['verification_status'] ?? '') === 'expired') {
				$flags[] = 'KYC not verified / expired';
				$riskScore += 15;
			}
		} elseif ($customerId > 0 && $amount >= 10000) {
			$flags[] = 'No KYC record for customer';
			$riskScore += 20;
		}

		$flagged = $riskScore >= 50 || !empty($flags);
		$customerName = trim((string) ($extra['customer_name'] ?? ($kycRow['customer_name'] ?? '')));
		$txType = trim((string) ($extra['transaction_type'] ?? 'cash_sale'));
		$ref = trim((string) ($extra['reference'] ?? ''));
		$now = time();
		$db->prepare(
			'INSERT INTO `epc_aml_transactions`
			(`company_id`,`customer_id`,`customer_name`,`transaction_type`,`amount`,`currency`,`reference`,`risk_score`,`flagged`,`flag_reason`,`review_status`,`time_created`)
			VALUES (?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array(
			$companyId,
			$customerId,
			$customerName,
			$txType !== '' ? $txType : 'cash_sale',
			$amount,
			$currency,
			$ref,
			min(100, $riskScore),
			$flagged ? 1 : 0,
			implode('; ', $flags),
			$flagged ? 'open' : 'closed',
			$now,
		));
		$txId = (int) $db->lastInsertId();

		return array(
			'ok' => true,
			'flagged' => $flagged,
			'risk_score' => min(100, $riskScore),
			'flags' => $flags,
			'transaction_id' => $txId,
			'message' => $flagged ? 'FLAGGED — review required' : 'CLEAR',
		);
	}
}

if (!function_exists('epc_aml_dashboard')) {
	/** @return array<string,mixed> */
	function epc_aml_dashboard(PDO $db, int $companyId = 0, int $fromTs = 0, int $toTs = 0): array
	{
		epc_aml_ensure_schema($db);
		if ($companyId <= 0) {
			$companyId = epc_aml_company_id($db);
		}
		if ($toTs <= 0) {
			$toTs = time();
		}
		if ($fromTs <= 0) {
			$fromTs = $toTs - (90 * 86400);
		}

		$st = $db->prepare('SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ?');
		$st->execute(array($companyId));
		$kycTotal = (int) $st->fetchColumn();
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `verification_status` = 'verified'");
		$st->execute(array($companyId));
		$kycVerified = (int) $st->fetchColumn();
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `risk_level` IN ('high','very_high')");
		$st->execute(array($companyId));
		$highRisk = (int) $st->fetchColumn();
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `pep_status` = 1");
		$st->execute(array($companyId));
		$pepCount = (int) $st->fetchColumn();
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_aml_kyc` WHERE `company_id` = ? AND `verification_status` IN ('pending','expired')");
		$st->execute(array($companyId));
		$kycPending = (int) $st->fetchColumn();

		$st = $db->prepare('SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `time_created` BETWEEN ? AND ?');
		$st->execute(array($companyId, $fromTs, $toTs));
		$txTotal = (int) $st->fetchColumn();
		$st = $db->prepare('SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `flagged` = 1 AND `time_created` BETWEEN ? AND ?');
		$st->execute(array($companyId, $fromTs, $toTs));
		$flagged = (int) $st->fetchColumn();
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `flagged` = 1 AND `review_status` = 'open'");
		$st->execute(array($companyId));
		$openAlerts = (int) $st->fetchColumn();
		$st = $db->prepare('SELECT COUNT(*) FROM `epc_aml_transactions` WHERE `company_id` = ? AND `sar_filed` = 1 AND `time_created` BETWEEN ? AND ?');
		$st->execute(array($companyId, $fromTs, $toTs));
		$sarFiled = (int) $st->fetchColumn();
		$st = $db->prepare("SELECT COUNT(*) FROM `epc_aml_reports` WHERE `company_id` = ? AND `report_type` = 'ctr' AND `time_created` BETWEEN ? AND ?");
		$st->execute(array($companyId, $fromTs, $toTs));
		$ctrFiled = (int) $st->fetchColumn();
		$st = $db->prepare('SELECT COUNT(*) FROM `epc_aml_rules` WHERE `company_id` = ? AND `is_active` = 1');
		$st->execute(array($companyId));
		$rulesActive = (int) $st->fetchColumn();

		$pct = $kycTotal > 0 ? (int) round(($kycVerified / $kycTotal) * 100) : 0;

		return array(
			'kyc_total' => $kycTotal,
			'kyc_verified' => $kycVerified,
			'kyc_pending' => $kycPending,
			'kyc_pct' => $pct,
			'high_risk' => $highRisk,
			'pep_count' => $pepCount,
			'tx_total' => $txTotal,
			'flagged' => $flagged,
			'open_alerts' => $openAlerts,
			'sar_filed' => $sarFiled,
			'ctr_filed' => $ctrFiled,
			'rules_active' => $rulesActive,
			'cash_threshold' => (float) epc_aml_setting_get($db, 'cash_threshold', '55000'),
			'authority' => epc_aml_setting_get($db, 'authority', 'UAE FIU (goAML)'),
		);
	}
}

if (!function_exists('epc_aml_list_alerts')) {
	/** @return list<array<string,mixed>> */
	function epc_aml_list_alerts(PDO $db, int $companyId = 0, int $limit = 50): array
	{
		epc_aml_ensure_schema($db);
		if ($companyId <= 0) {
			$companyId = epc_aml_company_id($db);
		}
		$st = $db->prepare(
			'SELECT * FROM `epc_aml_transactions`
			 WHERE `company_id` = ? AND `flagged` = 1
			 ORDER BY `time_created` DESC LIMIT ' . max(1, min(200, $limit))
		);
		$st->execute(array($companyId));
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}
}

if (!function_exists('epc_aml_list_kyc')) {
	/** @return list<array<string,mixed>> */
	function epc_aml_list_kyc(PDO $db, int $companyId = 0, int $limit = 100): array
	{
		epc_aml_ensure_schema($db);
		if ($companyId <= 0) {
			$companyId = epc_aml_company_id($db);
		}
		$st = $db->prepare(
			'SELECT * FROM `epc_aml_kyc` WHERE `company_id` = ?
			 ORDER BY FIELD(`risk_level`,"very_high","high","medium","low"), `time_updated` DESC
			 LIMIT ' . max(1, min(500, $limit))
		);
		$st->execute(array($companyId));
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}
}

if (!function_exists('epc_aml_kyc_save')) {
	/** @return array{ok:bool,id:int,message:string} */
	function epc_aml_kyc_save(PDO $db, array $data): array
	{
		epc_aml_ensure_schema($db);
		$companyId = (int) ($data['company_id'] ?? epc_aml_company_id($db));
		$id = (int) ($data['id'] ?? 0);
		$name = trim((string) ($data['customer_name'] ?? ''));
		if ($name === '') {
			return array('ok' => false, 'id' => 0, 'message' => 'Customer name is required');
		}
		$risk = (string) ($data['risk_level'] ?? 'low');
		if (!in_array($risk, array('low', 'medium', 'high', 'very_high'), true)) {
			$risk = 'low';
		}
		$status = (string) ($data['verification_status'] ?? 'pending');
		if (!in_array($status, array('pending', 'verified', 'rejected', 'expired'), true)) {
			$status = 'pending';
		}
		$now = time();
		$fields = array(
			$companyId,
			(int) ($data['customer_id'] ?? 0),
			$name,
			trim((string) ($data['id_type'] ?? '')),
			trim((string) ($data['id_number'] ?? '')),
			($data['id_expiry'] ?? '') !== '' ? (string) $data['id_expiry'] : null,
			trim((string) ($data['nationality'] ?? '')),
			$risk,
			!empty($data['pep_status']) ? 1 : 0,
			!empty($data['sanctions_checked']) ? 1 : 0,
			!empty($data['sanctions_match']) ? 1 : 0,
			$status,
			($data['next_review'] ?? '') !== '' ? (string) $data['next_review'] : null,
			trim((string) ($data['notes'] ?? '')),
			$now,
		);
		if ($id > 0) {
			$db->prepare(
				'UPDATE `epc_aml_kyc` SET `company_id`=?, `customer_id`=?, `customer_name`=?, `id_type`=?, `id_number`=?,
				 `id_expiry`=?, `nationality`=?, `risk_level`=?, `pep_status`=?, `sanctions_checked`=?, `sanctions_match`=?,
				 `verification_status`=?, `next_review`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
			)->execute(array_merge($fields, array($id)));
			return array('ok' => true, 'id' => $id, 'message' => 'KYC record updated');
		}
		$db->prepare(
			'INSERT INTO `epc_aml_kyc`
			(`company_id`,`customer_id`,`customer_name`,`id_type`,`id_number`,`id_expiry`,`nationality`,`risk_level`,
			 `pep_status`,`sanctions_checked`,`sanctions_match`,`verification_status`,`next_review`,`notes`,`time_created`,`time_updated`)
			 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array_merge($fields, array($now)));
		return array('ok' => true, 'id' => (int) $db->lastInsertId(), 'message' => 'KYC record created');
	}
}

if (!function_exists('epc_aml_alert_set_status')) {
	function epc_aml_alert_set_status(PDO $db, int $txId, string $status, int $userId = 0, bool $fileSar = false, string $sarRef = ''): array
	{
		epc_aml_ensure_schema($db);
		$allowed = array('open', 'reviewed', 'escalated', 'closed');
		if (!in_array($status, $allowed, true)) {
			return array('ok' => false, 'message' => 'Invalid status');
		}
		$db->prepare(
			'UPDATE `epc_aml_transactions` SET `review_status`=?, `reviewed_by`=?, `reviewed_at`=?,
			 `sar_filed`=IF(?,1,`sar_filed`), `sar_reference`=IF(?<>"",?,`sar_reference`)
			 WHERE `id`=?'
		)->execute(array(
			$status,
			$userId,
			date('Y-m-d H:i:s'),
			$fileSar ? 1 : 0,
			$sarRef,
			$sarRef,
			$txId,
		));
		return array('ok' => true, 'message' => 'Alert updated');
	}
}

if (!function_exists('epc_aml_settings_save')) {
	function epc_aml_settings_save(PDO $db, array $data): array
	{
		$map = array(
			'cash_threshold' => preg_replace('/[^\d.]/', '', (string) ($data['cash_threshold'] ?? '55000')) ?: '55000',
			'structuring_enabled' => !empty($data['structuring_enabled']) ? '1' : '0',
			'pep_screening' => !empty($data['pep_screening']) ? '1' : '0',
			'authority' => trim((string) ($data['authority'] ?? 'UAE FIU (goAML)')),
			'kyc_low_months' => (string) max(1, (int) ($data['kyc_low_months'] ?? 12)),
			'kyc_medium_months' => (string) max(1, (int) ($data['kyc_medium_months'] ?? 6)),
			'kyc_high_months' => (string) max(1, (int) ($data['kyc_high_months'] ?? 3)),
			'mlro_name' => trim((string) ($data['mlro_name'] ?? '')),
			'goaml_reg' => trim((string) ($data['goaml_reg'] ?? '')),
		);
		foreach ($map as $k => $v) {
			epc_aml_setting_set($db, $k, $v);
		}
		// Keep cash threshold rule in sync.
		$companyId = epc_aml_company_id($db);
		epc_aml_seed_rules($db, $companyId);
		$db->prepare(
			"UPDATE `epc_aml_rules` SET `threshold_amount` = ?
			 WHERE `company_id` = ? AND `rule_type` = 'threshold' AND `rule_name` LIKE '%55,000%'"
		)->execute(array((float) $map['cash_threshold'], $companyId));
		return array('ok' => true, 'message' => 'AML settings saved');
	}
}

if (!function_exists('epc_aml_list_reports')) {
	/** @return list<array<string,mixed>> */
	function epc_aml_list_reports(PDO $db, int $companyId = 0, int $limit = 30): array
	{
		epc_aml_ensure_schema($db);
		if ($companyId <= 0) {
			$companyId = epc_aml_company_id($db);
		}
		$st = $db->prepare(
			'SELECT `id`,`report_type`,`title`,`period_from`,`period_to`,`total_transactions`,`flagged_transactions`,
			 `sar_count`,`filed_to`,`file_reference`,`time_created`
			 FROM `epc_aml_reports` WHERE `company_id` = ? ORDER BY `time_created` DESC LIMIT ' . max(1, min(100, $limit))
		);
		$st->execute(array($companyId));
		return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	}
}

if (!function_exists('epc_aml_get_report')) {
	function epc_aml_get_report(PDO $db, int $id): ?array
	{
		epc_aml_ensure_schema($db);
		$st = $db->prepare('SELECT * FROM `epc_aml_reports` WHERE `id` = ? LIMIT 1');
		$st->execute(array($id));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		return $row ?: null;
	}
}

if (!function_exists('epc_aml_generate_report')) {
	/**
	 * Build an in-ERP AML report (compliance summary / CTR period pack) and store it.
	 * @return array{ok:bool,id:int,message:string,title?:string}
	 */
	function epc_aml_generate_report(PDO $db, string $type, string $from, string $to, int $userId = 0): array
	{
		epc_aml_ensure_schema($db);
		$companyId = epc_aml_company_id($db);
		$type = preg_replace('/[^a-z_]/', '', strtolower($type)) ?: 'compliance_summary';
		$allowed = array('compliance_summary', 'ctr', 'monitoring', 'kyc_register');
		if (!in_array($type, $allowed, true)) {
			$type = 'compliance_summary';
		}
		$fromTs = strtotime($from . ' 00:00:00') ?: (time() - 30 * 86400);
		$toTs = strtotime($to . ' 23:59:59') ?: time();
		$dash = epc_aml_dashboard($db, $companyId, $fromTs, $toTs);
		$alerts = epc_aml_list_alerts($db, $companyId, 100);
		$kyc = epc_aml_list_kyc($db, $companyId, 200);

		$titles = array(
			'compliance_summary' => 'AML compliance summary',
			'ctr' => 'Cash transaction report pack (CTR)',
			'monitoring' => 'Transaction monitoring log',
			'kyc_register' => 'KYC / CDD register extract',
		);
		$title = $titles[$type] . ' · ' . $from . ' → ' . $to;
		$authority = $dash['authority'];

		$rowsHtml = '';
		if ($type === 'kyc_register') {
			foreach ($kyc as $r) {
				$rowsHtml .= '<tr><td>' . htmlspecialchars((string) $r['customer_name']) . '</td><td>'
					. htmlspecialchars((string) $r['id_type']) . '</td><td>'
					. htmlspecialchars((string) $r['verification_status']) . '</td><td>'
					. htmlspecialchars((string) $r['risk_level']) . '</td><td>'
					. (!empty($r['pep_status']) ? 'PEP' : '—') . '</td></tr>';
			}
			$table = '<table class="table table-bordered table-condensed"><thead><tr><th>Customer</th><th>ID type</th><th>Status</th><th>Risk</th><th>PEP</th></tr></thead><tbody>'
				. ($rowsHtml !== '' ? $rowsHtml : '<tr><td colspan="5">No KYC records</td></tr>') . '</tbody></table>';
		} else {
			foreach ($alerts as $r) {
				if ($type === 'ctr' && (float) $r['amount'] < (float) $dash['cash_threshold']) {
					continue;
				}
				$rowsHtml .= '<tr><td>' . date('Y-m-d H:i', (int) $r['time_created']) . '</td><td>'
					. htmlspecialchars((string) ($r['customer_name'] ?: ('#' . $r['customer_id']))) . '</td><td>'
					. number_format((float) $r['amount'], 2) . ' ' . htmlspecialchars((string) $r['currency']) . '</td><td>'
					. (int) $r['risk_score'] . '</td><td>'
					. htmlspecialchars((string) $r['flag_reason']) . '</td><td>'
					. htmlspecialchars((string) $r['review_status']) . '</td></tr>';
			}
			$table = '<table class="table table-bordered table-condensed"><thead><tr><th>When</th><th>Customer</th><th>Amount</th><th>Score</th><th>Flags</th><th>Status</th></tr></thead><tbody>'
				. ($rowsHtml !== '' ? $rowsHtml : '<tr><td colspan="6">No matching alerts in period</td></tr>') . '</tbody></table>';
		}

		$body = '<div class="epc-aml-report-body">'
			. '<p><strong>' . htmlspecialchars($title) . '</strong><br>'
			. 'Authority: ' . htmlspecialchars($authority) . ' · Cash threshold: '
			. number_format((float) $dash['cash_threshold'], 0) . ' AED</p>'
			. '<div class="row">'
			. '<div class="col-sm-3"><div class="well well-sm">KYC verified<br><b>' . (int) $dash['kyc_pct'] . '%</b></div></div>'
			. '<div class="col-sm-3"><div class="well well-sm">Checks<br><b>' . (int) $dash['tx_total'] . '</b></div></div>'
			. '<div class="col-sm-3"><div class="well well-sm">Flagged<br><b>' . (int) $dash['flagged'] . '</b></div></div>'
			. '<div class="col-sm-3"><div class="well well-sm">STR filed<br><b>' . (int) $dash['sar_filed'] . '</b></div></div>'
			. '</div>'
			. $table
			. '<p class="text-muted small">Generated in ERP AML module. File STR/SAR on the official goAML portal when suspicion is confirmed. Do not tip off the subject.</p>'
			. '</div>';

		$summary = array(
			'dashboard' => $dash,
			'alert_count' => count($alerts),
			'kyc_count' => count($kyc),
			'type' => $type,
		);
		$ref = 'AML-' . strtoupper(substr($type, 0, 3)) . '-' . date('Ymd-His');
		$db->prepare(
			'INSERT INTO `epc_aml_reports`
			(`company_id`,`report_type`,`title`,`period_from`,`period_to`,`total_transactions`,`flagged_transactions`,
			 `sar_count`,`summary_json`,`body_html`,`filed_to`,`file_reference`,`generated_by`,`time_created`)
			 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		)->execute(array(
			$companyId,
			$type,
			$title,
			date('Y-m-d', $fromTs),
			date('Y-m-d', $toTs),
			(int) $dash['tx_total'],
			(int) $dash['flagged'],
			(int) $dash['sar_filed'],
			json_encode($summary, JSON_UNESCAPED_UNICODE),
			$body,
			$authority,
			$ref,
			$userId,
			time(),
		));
		return array(
			'ok' => true,
			'id' => (int) $db->lastInsertId(),
			'title' => $title,
			'file_reference' => $ref,
			'message' => 'Report generated',
		);
	}
}

if (!function_exists('epc_aml_curated_laws')) {
	/** Static UAE / FATF AML law anchors (always shown; fetch adds FTA-matched items). */
	function epc_aml_curated_laws(): array
	{
		return array(
			array(
				'title' => 'Federal Decree-Law No. 20 of 2018 on Anti-Money Laundering & CFT',
				'href' => 'https://www.uaefiu.gov.ae',
				'source' => 'UAE FIU / MoE',
				'summary' => 'Primary UAE AML/CFT statute for FIs and DNFBPs — CDD, STR, record-keeping, tipping-off ban.',
				'badge' => 'core',
			),
			array(
				'title' => 'Cabinet Decision No. 10 of 2019 (AML implementing regulations)',
				'href' => 'https://www.uaefiu.gov.ae',
				'source' => 'Cabinet / MoE',
				'summary' => 'Detailed CDD, PEP, correspondent, and reporting duties under Decree-Law 20/2018.',
				'badge' => 'core',
			),
			array(
				'title' => 'Federal Decree-Law No. 10 of 2025 & Cabinet Resolution 134/2025 (AML update)',
				'href' => 'https://www.moet.gov.ae',
				'source' => 'MoET',
				'summary' => 'Updated DNFBP supervisory framework — goAML registration, risk assessment, independent audit.',
				'badge' => 'update',
			),
			array(
				'title' => 'UAE FIU goAML portal — SAR / STR filing',
				'href' => 'https://services.uaefiu.gov.ae',
				'source' => 'UAE FIU',
				'summary' => 'Official channel to file suspicious activity / transaction reports without tipping off.',
				'badge' => 'portal',
			),
			array(
				'title' => 'FATF 40 Recommendations',
				'href' => 'https://www.fatf-gafi.org/en/topics/fatf-recommendations.html',
				'source' => 'FATF',
				'summary' => 'International AML/CFT standard underpinning UAE obligations for DNFBPs and dealers in precious metals.',
				'badge' => 'intl',
			),
		);
	}
}

if (!function_exists('epc_aml_legislation_items')) {
	/** @return list<array<string,mixed>> */
	function epc_aml_legislation_items(PDO $db): array
	{
		$out = array();
		foreach (epc_aml_curated_laws() as $law) {
			$out[] = array_merge($law, array('_curated' => true, '_is_new' => false, '_is_changed' => false));
		}
		if (!function_exists('epc_uae_fta_get_cached_legislation')) {
			$ftaFile = __DIR__ . '/epc_uae_tax_compliance.php';
			if (is_file($ftaFile)) {
				require_once $ftaFile;
			}
		}
		if (!function_exists('epc_uae_fta_get_cached_legislation')) {
			return $out;
		}
		$cached = epc_uae_fta_get_cached_legislation($db);
		foreach (($cached['legislation'] ?? $cached['items'] ?? array()) as $leg) {
			if (!is_array($leg)) {
				continue;
			}
			if (function_exists('epc_uae_tax_legislation_enrich_item')) {
				$leg = epc_uae_tax_legislation_enrich_item($leg, $db);
			}
			$hay = strtolower(
				(string) ($leg['title'] ?? '') . ' '
				. (string) ($leg['category'] ?? '') . ' '
				. (string) ($leg['pattern_key'] ?? '') . ' '
				. (string) ($leg['erp_summary'] ?? '')
			);
			$mods = function_exists('epc_uae_tax_legislation_erp_modules')
				? epc_uae_tax_legislation_erp_modules($leg)
				: array();
			$isAml = in_array('aml', $mods, true)
				|| (bool) preg_match('/aml|anti.?money|launder|goaml|fiu|sanctions|cft|terrorist\s*financ|dnfbp|pep|suspicious\s*transaction/i', $hay);
			if (!$isAml) {
				continue;
			}
			$out[] = array(
				'title' => (string) ($leg['title'] ?? 'Legislation item'),
				'href' => (string) ($leg['url'] ?? $leg['href'] ?? ''),
				'source' => 'FTA cache',
				'summary' => (string) ($leg['erp_summary'] ?? $leg['summary'] ?? ''),
				'badge' => !empty($leg['is_new']) ? 'new' : (!empty($leg['is_changed']) ? 'upd' : 'fta'),
				'_is_new' => !empty($leg['is_new']) || !empty($leg['new_since_last']),
				'_is_changed' => !empty($leg['is_changed']) || !empty($leg['changed_since_last']),
				'_curated' => false,
			);
		}
		return $out;
	}
}

if (!function_exists('epc_aml_dnfbp_context')) {
	/** @return array{is_dnfbp:bool,is_precious_metals:bool,label:string,obligations:array} */
	function epc_aml_dnfbp_context(PDO $db): array
	{
		$profile = array('is_dnfbp' => false, 'is_precious_metals' => false, 'label' => '');
		if (is_file(__DIR__ . '/epc_bos_compliance.php')) {
			require_once __DIR__ . '/epc_bos_compliance.php';
			if (function_exists('epc_bos_compliance_dnfbp_profile')) {
				$profile = epc_bos_compliance_dnfbp_profile($db);
			}
		}
		$country = 'AE';
		if (function_exists('epc_bos_compliance_company_country')) {
			$country = strtoupper((string) epc_bos_compliance_company_country($db));
		}
		$obs = array();
		if (function_exists('epc_bos_compliance_aml_obligations')) {
			$obs = epc_bos_compliance_aml_obligations($country, !empty($profile['is_precious_metals']));
		}
		return array_merge($profile, array('country' => $country, 'obligations' => $obs));
	}
}

if (!function_exists('epc_aml_journey_steps')) {
	/** @return list<array<string,mixed>> */
	function epc_aml_journey_steps(PDO $db): array
	{
		$dash = epc_aml_dashboard($db);
		$dnfbp = epc_aml_dnfbp_context($db);
		$hasMlro = epc_aml_setting_get($db, 'mlro_name', '') !== '';
		$hasGoaml = epc_aml_setting_get($db, 'goaml_reg', '') !== '';
		$hasRules = (int) $dash['rules_active'] > 0;
		$hasKyc = (int) $dash['kyc_total'] > 0;
		$hasCheck = (int) $dash['tx_total'] > 0;
		return array(
			array('key' => 'learn', 'n' => 1, 'title' => 'Learn AML duties', 'blurb' => 'Decree-Law 20/2018, goAML, DNFBP checklist.', 'icon' => 'fa-book', 'section' => 'guide', 'done' => true, 'cta' => 'Open guide'),
			array('key' => 'register', 'n' => 2, 'title' => 'Register & MLRO', 'blurb' => 'goAML enrolment and Money Laundering Reporting Officer.', 'icon' => 'fa-id-badge', 'section' => 'settings', 'done' => $hasMlro || $hasGoaml, 'cta' => 'Set MLRO'),
			array('key' => 'rules', 'n' => 3, 'title' => 'Monitoring rules', 'blurb' => 'Cash threshold, structuring, PEP / sanctions flags.', 'icon' => 'fa-cogs', 'section' => 'settings', 'done' => $hasRules, 'cta' => 'Configure rules'),
			array('key' => 'kyc', 'n' => 4, 'title' => 'KYC / CDD', 'blurb' => 'Identify customers, risk-rate, renew on schedule.', 'icon' => 'fa-user', 'section' => 'kyc', 'done' => $hasKyc, 'cta' => 'KYC register'),
			array('key' => 'monitor', 'n' => 5, 'title' => 'Monitor & alert', 'blurb' => 'Run checks; review open alerts before tipping-off risk.', 'icon' => 'fa-eye', 'section' => 'monitoring', 'done' => $hasCheck, 'cta' => 'Open monitoring'),
			array('key' => 'report', 'n' => 6, 'title' => 'Report (goAML)', 'blurb' => 'STR/SAR + period compliance packs for the FIU.', 'icon' => 'fa-file-text', 'section' => 'reports', 'done' => (int) $dash['sar_filed'] > 0 || count(epc_aml_list_reports($db, 0, 1)) > 0, 'cta' => 'Reports', 'dnfbp' => !empty($dnfbp['is_dnfbp'])),
		);
	}
}

if (!function_exists('epc_aml_guide_sections')) {
	/** @return list<array{title:string,points:list<string>}> */
	function epc_aml_guide_sections(): array
	{
		return array(
			array(
				'title' => 'Who must comply (DNFBP)',
				'points' => array(
					'Dealers in precious metals and stones (jewellery / gold) are Designated Non-Financial Businesses and Professions (DNFBPs) in the UAE.',
					'Real-estate brokers, auditors, corporate service providers and similar professions also carry AML/CFT duties.',
					'Obligations sit with the Ministry of Economy & Tourism (supervisor) and the UAE FIU (goAML) for reporting.',
				),
			),
			array(
				'title' => 'Core programme',
				'points' => array(
					'Appoint an MLRO (Money Laundering Reporting Officer) and register on goAML.',
					'Complete an enterprise-wide risk assessment (customer, geography, product, channel).',
					'Perform CDD / KYC before establishing a business relationship; apply EDD for high-risk and PEPs.',
					'Monitor transactions against rules (cash ≥ AED 55,000 for DPMS, structuring, unusual patterns).',
					'File STR/SAR promptly when suspicion arises — never tip off the customer.',
					'Train staff annually and retain records (typically 5 years).',
					'Commission an independent AML/CFT audit at least annually.',
				),
			),
			array(
				'title' => 'How to use this ERP module',
				'points' => array(
					'Overview — live KPIs from your KYC register and monitoring log.',
					'Monitoring — run real-time checks; open alerts stay until reviewed or escalated.',
					'KYC — maintain identity, risk level, PEP/sanctions flags and next review date.',
					'Reports — generate period packs here; open goAML SAR/STR builders under External reporting.',
					'Legislation — curated UAE AML laws plus any FTA-cached items matching AML keywords. Use Fetch for updates.',
					'Settings — cash threshold, MLRO name, goAML registration number, screening toggles.',
				),
			),
			array(
				'title' => 'Reporting path',
				'points' => array(
					'Internal: escalate an alert → MLRO decides whether to file.',
					'External: UAE FIU goAML portal for SAR/STR; keep the ERP reference on the alert.',
					'Period: generate Compliance summary / CTR pack for board or supervisor evidence.',
					'DPMS: cash/precious-metals reports when ≥ local cash threshold (AED 55,000 in UAE).',
				),
			),
		);
	}
}

if (!function_exists('epc_aml_module_completeness')) {
	/** @return array{percent:int,items:list<array{id:string,label:string,done:bool}>} */
	function epc_aml_module_completeness(PDO $db): array
	{
		$dash = epc_aml_dashboard($db);
		$items = array(
			array('id' => 'rules', 'label' => 'Active monitoring rules', 'done' => (int) $dash['rules_active'] > 0),
			array('id' => 'mlro', 'label' => 'MLRO named in settings', 'done' => epc_aml_setting_get($db, 'mlro_name', '') !== ''),
			array('id' => 'goaml', 'label' => 'goAML registration recorded', 'done' => epc_aml_setting_get($db, 'goaml_reg', '') !== ''),
			array('id' => 'kyc', 'label' => 'At least one KYC record', 'done' => (int) $dash['kyc_total'] > 0),
			array('id' => 'check', 'label' => 'At least one transaction check logged', 'done' => (int) $dash['tx_total'] > 0),
			array('id' => 'review', 'label' => 'No open high-priority backlog (or zero alerts)', 'done' => (int) $dash['open_alerts'] === 0),
		);
		$done = 0;
		foreach ($items as $it) {
			if (!empty($it['done'])) {
				$done++;
			}
		}
		$n = count($items);
		return array('percent' => $n ? (int) round(($done / $n) * 100) : 0, 'items' => $items);
	}
}
