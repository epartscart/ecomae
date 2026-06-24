<?php
/**
 * P2 #37 — Isolation Anomaly AI
 *
 * Analyzes audit logs to detect cross-tenant data access anomalies,
 * unusual query patterns, and potential isolation breaches. Scores risk
 * and generates alerts.
 *
 * Feeds from: epc_ci_violations, epc_ci_audit_runs, epc_mfa_audit_log
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

define('EPC_ISOLATION_ANOMALY_VERSION', '1.0.0');

/* ─── Schema ─── */

function epc_anomaly_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) return;
	$done = true;

	$pdo->exec("
		CREATE TABLE IF NOT EXISTS `epc_isolation_anomalies` (
			`id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
			`site_key`        VARCHAR(64)    NOT NULL,
			`anomaly_type`    VARCHAR(64)    NOT NULL,
			`severity`        ENUM('low','medium','high','critical') NOT NULL DEFAULT 'medium',
			`description`     TEXT           NOT NULL,
			`evidence_json`   TEXT           NULL,
			`risk_score`      DECIMAL(5,2)   NOT NULL DEFAULT 0.00,
			`resolved`        TINYINT(1)     NOT NULL DEFAULT 0,
			`resolved_by`     INT UNSIGNED   NULL,
			`resolved_at`     DATETIME       NULL,
			`resolution_note` VARCHAR(512)   NULL,
			`created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
			INDEX `idx_site` (`site_key`, `created_at`),
			INDEX `idx_severity` (`severity`),
			INDEX `idx_resolved` (`resolved`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
	");
}

/* ─── Anomaly Detection Engine ─── */

/**
 * Run anomaly scan across all tenants.
 */
function epc_anomaly_scan(PDO $pdo): array
{
	epc_anomaly_ensure_schema($pdo);
	$results = array('scanned' => 0, 'anomalies_found' => 0, 'details' => array());

	// Check 1: Cross-tenant violation spikes
	$violations = epc_anomaly_check_violation_spikes($pdo);
	$results['details']['violation_spikes'] = $violations;
	$results['anomalies_found'] += count($violations['anomalies']);

	// Check 2: Unusual access patterns (same IP accessing multiple tenants)
	$multiTenant = epc_anomaly_check_multi_tenant_ip($pdo);
	$results['details']['multi_tenant_ip'] = $multiTenant;
	$results['anomalies_found'] += count($multiTenant['anomalies']);

	// Check 3: After-hours access to sensitive modules
	$afterHours = epc_anomaly_check_after_hours($pdo);
	$results['details']['after_hours'] = $afterHours;
	$results['anomalies_found'] += count($afterHours['anomalies']);

	// Check 4: MFA bypass attempts
	$mfaBypass = epc_anomaly_check_mfa_bypass($pdo);
	$results['details']['mfa_bypass'] = $mfaBypass;
	$results['anomalies_found'] += count($mfaBypass['anomalies']);

	$results['scanned'] = 4;
	$results['scan_time'] = date('c');
	return $results;
}

function epc_anomaly_check_violation_spikes(PDO $pdo): array
{
	$anomalies = array();
	try {
		$st = $pdo->query("
			SELECT `site_key`, COUNT(*) AS `count`, DATE(`created_at`) AS `day`
			FROM `epc_ci_violations`
			WHERE `created_at` >= DATE_SUB(NOW(), INTERVAL 7 DAY)
			GROUP BY `site_key`, DATE(`created_at`)
			HAVING `count` > 10
			ORDER BY `count` DESC
		");
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($rows as $row) {
			$anomalies[] = array(
				'type' => 'violation_spike',
				'site_key' => $row['site_key'],
				'severity' => $row['count'] > 50 ? 'critical' : ($row['count'] > 25 ? 'high' : 'medium'),
				'description' => $row['count'] . ' isolation violations on ' . $row['day'],
				'risk_score' => min(100, $row['count'] * 2),
			);
			epc_anomaly_record($pdo, $row['site_key'], 'violation_spike', $anomalies[count($anomalies) - 1]);
		}
	} catch (\Exception $e) { /* table may not exist */ }
	return array('check' => 'violation_spikes', 'anomalies' => $anomalies);
}

function epc_anomaly_check_multi_tenant_ip(PDO $pdo): array
{
	$anomalies = array();
	try {
		$st = $pdo->query("
			SELECT `ip`, COUNT(DISTINCT `site_key`) AS `tenant_count`, GROUP_CONCAT(DISTINCT `site_key`) AS `tenants`
			FROM `epc_ci_violations`
			WHERE `created_at` >= DATE_SUB(NOW(), INTERVAL 24 HOUR) AND `ip` != ''
			GROUP BY `ip`
			HAVING `tenant_count` > 1
		");
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($rows as $row) {
			$anomalies[] = array(
				'type' => 'multi_tenant_ip',
				'ip' => $row['ip'],
				'severity' => $row['tenant_count'] > 3 ? 'critical' : 'high',
				'description' => 'IP ' . $row['ip'] . ' accessed ' . $row['tenant_count'] . ' tenants: ' . $row['tenants'],
				'risk_score' => min(100, $row['tenant_count'] * 25),
			);
		}
	} catch (\Exception $e) { /* table may not exist */ }
	return array('check' => 'multi_tenant_ip', 'anomalies' => $anomalies);
}

function epc_anomaly_check_after_hours(PDO $pdo): array
{
	$anomalies = array();
	try {
		$st = $pdo->query("
			SELECT `site_key`, COUNT(*) AS `count`
			FROM `epc_ci_violations`
			WHERE `created_at` >= DATE_SUB(NOW(), INTERVAL 7 DAY)
			AND (HOUR(`created_at`) < 6 OR HOUR(`created_at`) > 22)
			GROUP BY `site_key`
			HAVING `count` > 5
		");
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($rows as $row) {
			$anomalies[] = array(
				'type' => 'after_hours',
				'site_key' => $row['site_key'],
				'severity' => 'medium',
				'description' => $row['count'] . ' after-hours access events for ' . $row['site_key'],
				'risk_score' => min(50, $row['count'] * 5),
			);
		}
	} catch (\Exception $e) { /* table may not exist */ }
	return array('check' => 'after_hours', 'anomalies' => $anomalies);
}

function epc_anomaly_check_mfa_bypass(PDO $pdo): array
{
	$anomalies = array();
	try {
		$st = $pdo->query("
			SELECT `user_id`, COUNT(*) AS `failed`
			FROM `epc_mfa_audit_log`
			WHERE `action` IN ('verify_failed','verify') AND `success` = 0
			AND `created_at` >= DATE_SUB(NOW(), INTERVAL 24 HOUR)
			GROUP BY `user_id`
			HAVING `failed` > 5
		");
		$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
		foreach ($rows as $row) {
			$anomalies[] = array(
				'type' => 'mfa_brute_force',
				'user_id' => $row['user_id'],
				'severity' => $row['failed'] > 20 ? 'critical' : 'high',
				'description' => 'User ' . $row['user_id'] . ' had ' . $row['failed'] . ' failed MFA attempts in 24h',
				'risk_score' => min(100, $row['failed'] * 5),
			);
		}
	} catch (\Exception $e) { /* table may not exist */ }
	return array('check' => 'mfa_bypass', 'anomalies' => $anomalies);
}

/* ─── Record & Manage ─── */

function epc_anomaly_record(PDO $pdo, string $siteKey, string $type, array $data): int
{
	epc_anomaly_ensure_schema($pdo);
	$pdo->prepare("INSERT INTO `epc_isolation_anomalies` (`site_key`,`anomaly_type`,`severity`,`description`,`evidence_json`,`risk_score`) VALUES (?,?,?,?,?,?)")
		->execute(array($siteKey, $type, $data['severity'] ?? 'medium', $data['description'] ?? '', json_encode($data), $data['risk_score'] ?? 0));
	return (int) $pdo->lastInsertId();
}

function epc_anomaly_list(PDO $pdo, bool $unresolvedOnly = true, string $siteKey = '', int $limit = 100): array
{
	epc_anomaly_ensure_schema($pdo);
	$where = array('1=1');
	$params = array();
	if ($unresolvedOnly) { $where[] = '`resolved` = 0'; }
	if ($siteKey !== '') { $where[] = '`site_key` = ?'; $params[] = $siteKey; }
	$params[] = $limit;
	$st = $pdo->prepare("SELECT * FROM `epc_isolation_anomalies` WHERE " . implode(' AND ', $where) . " ORDER BY `risk_score` DESC, `created_at` DESC LIMIT ?");
	$st->execute($params);
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_anomaly_resolve(PDO $pdo, int $anomalyId, int $userId, string $note = ''): array
{
	$pdo->prepare("UPDATE `epc_isolation_anomalies` SET `resolved`=1, `resolved_by`=?, `resolved_at`=NOW(), `resolution_note`=? WHERE `id`=? AND `resolved`=0")
		->execute(array($userId, $note, $anomalyId));
	return array('ok' => true);
}

function epc_anomaly_fleet_stats(PDO $pdo): array
{
	epc_anomaly_ensure_schema($pdo);
	$st = $pdo->query("SELECT `severity`, COUNT(*) AS `count`, SUM(CASE WHEN `resolved`=0 THEN 1 ELSE 0 END) AS `unresolved` FROM `epc_isolation_anomalies` GROUP BY `severity`");
	$bySeverity = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	$total = $pdo->query("SELECT COUNT(*) FROM `epc_isolation_anomalies` WHERE `resolved`=0")->fetchColumn();
	return array('unresolved_total' => (int) $total, 'by_severity' => $bySeverity);
}
