<?php
/**
 * Enterprise Readiness Score — per-tenant health check for the Tenant Hub.
 *
 * Checks: isolation audit, MFA enrollment, backup age, e-invoice status,
 * homepage performance, VAT/compliance, ERP module activation, branding.
 *
 * Score: 0–100. Each check contributes weighted points. Sales shows
 * prospects their path from demo → paid → enterprise.
 */
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

/* ─────────────────── Main Score Calculator ─────────────────── */

function epc_readiness_score(PDO $platformPdo, string $siteKey, array $options = array()): array
{
	$checks = array();
	$totalWeight = 0;
	$earnedWeight = 0;

	// 1. Commerce Isolation (weight: 20)
	$isolation = epc_readiness_check_isolation($platformPdo, $siteKey);
	$checks[] = $isolation;
	$totalWeight += $isolation['weight'];
	$earnedWeight += $isolation['earned'];

	// 2. MFA Enrollment (weight: 15)
	$mfa = epc_readiness_check_mfa($platformPdo, $siteKey);
	$checks[] = $mfa;
	$totalWeight += $mfa['weight'];
	$earnedWeight += $mfa['earned'];

	// 3. Backup Age (weight: 10)
	$backup = epc_readiness_check_backup($platformPdo, $siteKey);
	$checks[] = $backup;
	$totalWeight += $backup['weight'];
	$earnedWeight += $backup['earned'];

	// 4. E-Invoice Status (weight: 15)
	$einvoice = epc_readiness_check_einvoice($platformPdo, $siteKey);
	$checks[] = $einvoice;
	$totalWeight += $einvoice['weight'];
	$earnedWeight += $einvoice['earned'];

	// 5. Homepage Performance (weight: 10)
	$perf = epc_readiness_check_homepage_perf($platformPdo, $siteKey);
	$checks[] = $perf;
	$totalWeight += $perf['weight'];
	$earnedWeight += $perf['earned'];

	// 6. VAT / Compliance (weight: 10)
	$compliance = epc_readiness_check_compliance($platformPdo, $siteKey);
	$checks[] = $compliance;
	$totalWeight += $compliance['weight'];
	$earnedWeight += $compliance['earned'];

	// 7. ERP Module Activation (weight: 10)
	$erp = epc_readiness_check_erp_modules($platformPdo, $siteKey);
	$checks[] = $erp;
	$totalWeight += $erp['weight'];
	$earnedWeight += $erp['earned'];

	// 8. Branding / Design Tokens (weight: 5)
	$branding = epc_readiness_check_branding($platformPdo, $siteKey);
	$checks[] = $branding;
	$totalWeight += $branding['weight'];
	$earnedWeight += $branding['earned'];

	// 9. Webhooks Configured (weight: 5)
	$webhooks = epc_readiness_check_webhooks($platformPdo, $siteKey);
	$checks[] = $webhooks;
	$totalWeight += $webhooks['weight'];
	$earnedWeight += $webhooks['earned'];

	$score = $totalWeight > 0 ? (int) round(($earnedWeight / $totalWeight) * 100) : 0;

	$tier = 'demo';
	if ($score >= 80) { $tier = 'enterprise'; }
	elseif ($score >= 60) { $tier = 'paid'; }
	elseif ($score >= 30) { $tier = 'pilot'; }

	return array(
		'site_key'     => $siteKey,
		'score'        => $score,
		'tier'         => $tier,
		'tier_label'   => epc_readiness_tier_label($tier),
		'total_weight' => $totalWeight,
		'earned_weight' => $earnedWeight,
		'checks'       => $checks,
		'generated_at' => date('c'),
	);
}

function epc_readiness_tier_label(string $tier): string
{
	$labels = array(
		'demo'       => 'Demo',
		'pilot'      => 'Pilot',
		'paid'       => 'Production',
		'enterprise' => 'Enterprise',
	);
	return $labels[$tier] ?? 'Unknown';
}

/* ─────────────────── Individual Checks ─────────────────── */

function epc_readiness_check_isolation(PDO $pdo, string $siteKey): array
{
	$weight = 20;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT `setting_value` FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'isolation_audit_status\' LIMIT 1'
		);
		$st->execute(array($siteKey));
		$val = $st->fetchColumn();

		if ($val === 'pass' || $val === 'ok') {
			$status = 'pass';
			$detail = 'Site_key isolation audit passed.';
			$earned = $weight;
		} elseif ($val === 'warn') {
			$status = 'warn';
			$detail = 'Isolation audit has warnings. Review recommended.';
			$earned = (int) ($weight * 0.5);
		} elseif ($val === 'fail') {
			$status = 'fail';
			$detail = 'Isolation audit failed. Fix cross-tenant leaks.';
		} else {
			$status = 'not_run';
			$detail = 'Isolation audit not yet run for this tenant.';
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'Audit system not available.';
	}

	return array(
		'id' => 'isolation', 'label' => 'Commerce Isolation',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-shield', 'remediation' => 'Run isolation audit from BOS Fleet Command.',
	);
}

function epc_readiness_check_mfa(PDO $pdo, string $siteKey): array
{
	$weight = 15;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT COUNT(*) FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'mfa_enabled\' AND `setting_value` = \'1\''
		);
		$st->execute(array($siteKey));
		$mfaEnabled = (int) $st->fetchColumn() > 0;

		if ($mfaEnabled) {
			$status = 'pass';
			$detail = 'MFA (TOTP) is enabled for finance roles.';
			$earned = $weight;
		} else {
			$status = 'fail';
			$detail = 'MFA not enabled. Required for enterprise tier.';
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'MFA check unavailable.';
	}

	return array(
		'id' => 'mfa', 'label' => 'MFA Enrollment',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-lock', 'remediation' => 'Enable MFA in tenant CP security settings.',
	);
}

function epc_readiness_check_backup(PDO $pdo, string $siteKey): array
{
	$weight = 10;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT `setting_value` FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'last_backup_time\' LIMIT 1'
		);
		$st->execute(array($siteKey));
		$lastBackup = (int) $st->fetchColumn();

		if ($lastBackup <= 0) {
			$status = 'fail';
			$detail = 'No backup recorded. Set up daily backup schedule.';
		} else {
			$age = time() - $lastBackup;
			$ageHours = round($age / 3600);
			if ($age < 86400) {
				$status = 'pass';
				$detail = 'Last backup ' . $ageHours . 'h ago. Within 24h RPO.';
				$earned = $weight;
			} elseif ($age < 172800) {
				$status = 'warn';
				$detail = 'Last backup ' . $ageHours . 'h ago. Exceeds 24h RPO.';
				$earned = (int) ($weight * 0.5);
			} else {
				$status = 'fail';
				$detail = 'Last backup ' . round($age / 86400) . ' days ago. Critical.';
			}
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'Backup tracking not configured.';
	}

	return array(
		'id' => 'backup', 'label' => 'Backup Age',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-database', 'remediation' => 'Configure daily backup via cron or CloudPanel.',
	);
}

function epc_readiness_check_einvoice(PDO $pdo, string $siteKey): array
{
	$weight = 15;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT `setting_value` FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'einvoice_asp_mode\' LIMIT 1'
		);
		$st->execute(array($siteKey));
		$mode = (string) $st->fetchColumn();

		if ($mode === 'api' || $mode === 'live') {
			$status = 'pass';
			$detail = 'E-invoice ASP API integration is live.';
			$earned = $weight;
		} elseif ($mode === 'manual') {
			$status = 'warn';
			$detail = 'E-invoice in manual mode. Switch to API for automation.';
			$earned = (int) ($weight * 0.5);
		} elseif ($mode === 'test') {
			$status = 'warn';
			$detail = 'E-invoice in test mode. Move to live for FTA compliance.';
			$earned = (int) ($weight * 0.3);
		} else {
			$status = 'fail';
			$detail = 'E-invoice not configured. Required for UAE FTA compliance.';
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'E-invoice check unavailable.';
	}

	return array(
		'id' => 'einvoice', 'label' => 'E-Invoice (FTA)',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-paper-plane', 'remediation' => 'Configure ASP API keys in ERP Finance settings.',
	);
}

function epc_readiness_check_homepage_perf(PDO $pdo, string $siteKey): array
{
	$weight = 10;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT `setting_value` FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'homepage_load_ms\' LIMIT 1'
		);
		$st->execute(array($siteKey));
		$loadMs = (int) $st->fetchColumn();

		if ($loadMs <= 0) {
			$status = 'not_run';
			$detail = 'Homepage load time not measured. Run performance test.';
		} elseif ($loadMs < 2000) {
			$status = 'pass';
			$detail = 'Homepage loads in ' . $loadMs . 'ms. Under 2s target.';
			$earned = $weight;
		} elseif ($loadMs < 5000) {
			$status = 'warn';
			$detail = 'Homepage loads in ' . $loadMs . 'ms. Target is under 2s.';
			$earned = (int) ($weight * 0.5);
		} else {
			$status = 'fail';
			$detail = 'Homepage loads in ' . $loadMs . 'ms. Critical performance issue.';
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'Performance check unavailable.';
	}

	return array(
		'id' => 'homepage_perf', 'label' => 'Homepage Performance',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-tachometer', 'remediation' => 'Enable page cache, gzip, and optimize queries.',
	);
}

function epc_readiness_check_compliance(PDO $pdo, string $siteKey): array
{
	$weight = 10;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$checks = 0;
		$passed = 0;

		// VAT configured?
		$st = $pdo->prepare(
			'SELECT COUNT(*) FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'vat_trn\' AND `setting_value` != \'\''
		);
		$st->execute(array($siteKey));
		$checks++;
		if ((int) $st->fetchColumn() > 0) { $passed++; }

		// Trade license?
		$st = $pdo->prepare(
			'SELECT COUNT(*) FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'trade_license\' AND `setting_value` != \'\''
		);
		$st->execute(array($siteKey));
		$checks++;
		if ((int) $st->fetchColumn() > 0) { $passed++; }

		if ($checks === $passed && $checks > 0) {
			$status = 'pass';
			$detail = 'VAT TRN and trade license configured.';
			$earned = $weight;
		} elseif ($passed > 0) {
			$status = 'warn';
			$detail = $passed . '/' . $checks . ' compliance items configured.';
			$earned = (int) ($weight * ($passed / $checks));
		} else {
			$status = 'fail';
			$detail = 'No compliance items configured.';
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'Compliance check unavailable.';
	}

	return array(
		'id' => 'compliance', 'label' => 'VAT / Compliance',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-balance-scale', 'remediation' => 'Add VAT TRN and trade license in tenant settings.',
	);
}

function epc_readiness_check_erp_modules(PDO $pdo, string $siteKey): array
{
	$weight = 10;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT `setting_value` FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` = \'erp_modules_active\' LIMIT 1'
		);
		$st->execute(array($siteKey));
		$val = (string) $st->fetchColumn();

		$modules = $val !== '' ? json_decode($val, true) : null;
		if (is_array($modules) && count($modules) > 0) {
			$count = count($modules);
			if ($count >= 5) {
				$status = 'pass';
				$detail = $count . ' ERP modules activated.';
				$earned = $weight;
			} else {
				$status = 'warn';
				$detail = $count . ' ERP modules activated. Recommend 5+ for full suite.';
				$earned = (int) ($weight * min(1, $count / 5));
			}
		} else {
			// Check tenant type — ERP-only tenants get full credit
			$st2 = $pdo->prepare(
				'SELECT `erp_enabled` FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1'
			);
			$st2->execute(array($siteKey));
			$erpEnabled = (int) $st2->fetchColumn();
			if ($erpEnabled) {
				$status = 'pass';
				$detail = 'ERP enabled for tenant (module list not tracked).';
				$earned = $weight;
			} else {
				$status = 'fail';
				$detail = 'No ERP modules activated for this tenant.';
			}
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'ERP module check unavailable.';
	}

	return array(
		'id' => 'erp_modules', 'label' => 'ERP Modules',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-th-large', 'remediation' => 'Enable ERP modules in Super CP → Tenant Hub.',
	);
}

function epc_readiness_check_branding(PDO $pdo, string $siteKey): array
{
	$weight = 5;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT COUNT(*) FROM `epc_settings` WHERE `site_key` = ? AND `setting_key` LIKE \'brand_%\' AND `setting_value` != \'\''
		);
		$st->execute(array($siteKey));
		$count = (int) $st->fetchColumn();

		// Also check static catalog
		if (function_exists('epc_design_tokens_tenant_catalog')) {
			$catalog = epc_design_tokens_tenant_catalog();
			if (isset($catalog[$siteKey])) {
				$count = max($count, 1);
			}
		}

		if ($count >= 3) {
			$status = 'pass';
			$detail = 'Tenant branding configured (' . $count . ' design tokens).';
			$earned = $weight;
		} elseif ($count >= 1) {
			$status = 'warn';
			$detail = 'Partial branding configured. Add logo and colors.';
			$earned = (int) ($weight * 0.5);
		} else {
			$status = 'fail';
			$detail = 'No tenant branding configured.';
		}
	} catch (\Exception $e) {
		$status = 'not_run';
		$detail = 'Branding check unavailable.';
	}

	return array(
		'id' => 'branding', 'label' => 'Branding / Design Tokens',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-paint-brush', 'remediation' => 'Configure logo, colors, and design tokens in BOS.',
	);
}

function epc_readiness_check_webhooks(PDO $pdo, string $siteKey): array
{
	$weight = 5;
	$status = 'unknown';
	$detail = '';
	$earned = 0;

	try {
		$st = $pdo->prepare(
			'SELECT COUNT(*) FROM `epc_webhooks` WHERE `tenant_key` = ? AND `active` = 1'
		);
		$st->execute(array($siteKey));
		$count = (int) $st->fetchColumn();

		if ($count > 0) {
			$status = 'pass';
			$detail = $count . ' active webhook' . ($count > 1 ? 's' : '') . ' configured.';
			$earned = $weight;
		} else {
			$status = 'warn';
			$detail = 'No webhooks configured. Optional but recommended for integrations.';
			$earned = 0;
		}
	} catch (\Exception $e) {
		// Table may not exist yet — not a failure
		$status = 'not_run';
		$detail = 'Webhook system not deployed yet.';
	}

	return array(
		'id' => 'webhooks', 'label' => 'Webhooks',
		'weight' => $weight, 'earned' => $earned,
		'status' => $status, 'detail' => $detail,
		'icon' => 'fa-plug', 'remediation' => 'Register webhook endpoints in BOS or ERP settings.',
	);
}

/* ─────────────────── Multi-Tenant Summary ─────────────────── */

function epc_readiness_fleet_summary(PDO $platformPdo): array
{
	$tenants = array();
	try {
		$st = $platformPdo->query(
			'SELECT `site_key`, `trade_name`, `status`, `industry_code` FROM `epc_portal_tenants` WHERE `status` = \'live\' ORDER BY `trade_name`'
		);
		$rows = $st->fetchAll(PDO::FETCH_ASSOC);
		foreach ($rows as $row) {
			$score = epc_readiness_score($platformPdo, $row['site_key']);
			$tenants[] = array(
				'site_key'   => $row['site_key'],
				'trade_name' => $row['trade_name'],
				'industry'   => $row['industry_code'],
				'score'      => $score['score'],
				'tier'       => $score['tier'],
				'tier_label' => $score['tier_label'],
				'checks'     => count($score['checks']),
				'passed'     => count(array_filter($score['checks'], function ($c) { return $c['status'] === 'pass'; })),
			);
		}
	} catch (\Exception $e) {
		// Table may not exist
	}

	$avgScore = 0;
	if (count($tenants) > 0) {
		$avgScore = (int) round(array_sum(array_column($tenants, 'score')) / count($tenants));
	}

	return array(
		'tenant_count' => count($tenants),
		'average_score' => $avgScore,
		'tenants'       => $tenants,
		'generated_at'  => date('c'),
	);
}
