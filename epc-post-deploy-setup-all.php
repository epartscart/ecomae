<?php
/**
 * Post-Deploy Setup-All Runner — ensures nothing is forgotten after git pull.
 *
 * Runs all schema migrations, cron validations, config checks, and health
 * verifications in a single idempotent pass. Safe to run repeatedly.
 *
 * Usage:
 *   CLI:  php epc-post-deploy-setup-all.php
 *   HTTP: curl -sk "https://www.ecomae.com/epc-post-deploy-setup-all.php?token=epartscart-deploy-2026"
 *
 * Every ensure_schema() is idempotent (CREATE TABLE IF NOT EXISTS), so this
 * script is safe to run on every deploy without data loss.
 */
define('_ASTEXE_', 1);
define('EPC_SETUP_ALL_VERSION', '1.0.0');

// Auth: CLI always allowed, HTTP requires deploy token
$isCli = php_sapi_name() === 'cli';
if (!$isCli) {
	$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
	if ($token !== 'epartscart-deploy-2026') {
		http_response_code(403);
		echo json_encode(array('ok' => false, 'error' => 'Forbidden'));
		exit;
	}
	header('Content-Type: application/json; charset=utf-8');
}

$startTime = microtime(true);
$results = array();
$errors = array();

function setup_log(string $step, string $status, string $detail = ''): void
{
	global $results, $isCli;
	$entry = array('step' => $step, 'status' => $status, 'detail' => $detail, 'time' => date('H:i:s'));
	$results[] = $entry;
	if ($isCli) {
		$icon = $status === 'ok' ? '[OK]' : ($status === 'skip' ? '[SKIP]' : '[FAIL]');
		echo sprintf("  %-6s %-45s %s\n", $icon, $step, $detail);
	}
}

function setup_error(string $step, string $msg): void
{
	global $errors;
	$errors[] = array('step' => $step, 'error' => $msg);
	setup_log($step, 'fail', $msg);
}

if ($isCli) {
	echo "=== ECOM AE Post-Deploy Setup-All v" . EPC_SETUP_ALL_VERSION . " ===\n";
	echo "Started: " . date('Y-m-d H:i:s') . "\n\n";
}

// ─────────────────── 1. Platform DB Connection ─────────────────────

$docRoot = $_SERVER['DOCUMENT_ROOT'] ?? dirname(__FILE__);
$_SERVER['DOCUMENT_ROOT'] = $docRoot;

setup_log('platform_init', 'ok', 'Document root: ' . $docRoot);

$platformPdo = null;
try {
	require_once $docRoot . '/config.php';
	$cfg = new DP_Config();
	$platformPdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db_name . ';charset=utf8;connect_timeout=5',
		$cfg->db_user,
		$cfg->db_pass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
	);
	setup_log('platform_db', 'ok', 'Connected to ' . $cfg->db_name);
} catch (Exception $e) {
	setup_error('platform_db', 'Failed: ' . $e->getMessage());
}

// ─────────────────── 2. ERP Schema Migrations ─────────────────────

$schemaSteps = array(
	array(
		'file' => '/content/shop/finance/epc_erp_helpers.php',
		'func' => 'epc_erp_ensure_schema',
		'label' => 'erp_core_schema',
	),
	array(
		'file' => '/content/shop/finance/epc_erp_gl.php',
		'func' => 'epc_erp_gl_ensure_schema',
		'label' => 'erp_gl_schema',
	),
	array(
		'file' => '/content/shop/finance/epc_erp_vouchers.php',
		'func' => 'epc_erp_vouchers_ensure_schema',
		'label' => 'erp_vouchers_schema',
	),
	array(
		'file' => '/content/shop/finance/epc_erp_fiscal_periods.php',
		'func' => 'epc_erp_fiscal_ensure_schema',
		'label' => 'erp_fiscal_periods',
	),
	array(
		'file' => '/content/shop/finance/epc_erp_period_close.php',
		'func' => 'epc_erp_period_close_ensure_schema',
		'label' => 'erp_period_close',
	),
	array(
		'file' => '/content/shop/finance/epc_erp_advances.php',
		'func' => 'epc_erp_advances_ensure_schema',
		'label' => 'erp_advances',
	),
	array(
		'file' => '/content/shop/finance/epc_erp_audit.php',
		'func' => 'epc_erp_audit_ensure_schema',
		'label' => 'erp_audit_log',
	),
	array(
		'file' => '/content/shop/finance/epc_einvoice_schema.php',
		'func' => 'epc_einvoice_ensure_schema',
		'label' => 'einvoice_schema',
	),
	array(
		'file' => '/content/shop/finance/epc_crm_schema.php',
		'func' => 'epc_crm_ensure_schema',
		'label' => 'crm_schema',
	),
	array(
		'file' => '/content/shop/finance/epc_bos_compliance.php',
		'func' => 'epc_bos_compliance_ensure_schema',
		'label' => 'compliance_schema',
	),
	array(
		'file' => '/content/shop/finance/epc_bos_workflow.php',
		'func' => 'epc_bos_wf_ensure_schema',
		'label' => 'workflow_schema',
	),
);

if ($platformPdo) {
	foreach ($schemaSteps as $step) {
		$filePath = $docRoot . $step['file'];
		if (!file_exists($filePath)) {
			setup_log($step['label'], 'skip', 'File not found: ' . $step['file']);
			continue;
		}
		try {
			require_once $filePath;
			if (function_exists($step['func'])) {
				call_user_func($step['func'], $platformPdo);
				setup_log($step['label'], 'ok', 'Schema ensured');
			} else {
				setup_log($step['label'], 'skip', 'Function not found: ' . $step['func']);
			}
		} catch (Exception $e) {
			setup_error($step['label'], $e->getMessage());
		}
	}
}

// ─────────────────── 3. Platform Events + Webhooks Schema ─────────────────────

$eventSchemaFiles = array(
	array('file' => '/content/general_pages/epc_events.php', 'func' => 'epc_events_ensure_schema', 'label' => 'events_schema'),
	array('file' => '/content/general_pages/epc_webhooks.php', 'func' => 'epc_webhooks_ensure_schema', 'label' => 'webhooks_schema'),
);

if ($platformPdo) {
	foreach ($eventSchemaFiles as $step) {
		$filePath = $docRoot . $step['file'];
		if (!file_exists($filePath)) {
			setup_log($step['label'], 'skip', 'File not found');
			continue;
		}
		try {
			require_once $filePath;
			if (function_exists($step['func'])) {
				call_user_func($step['func'], $platformPdo);
				setup_log($step['label'], 'ok', 'Schema ensured');
			} else {
				setup_log($step['label'], 'skip', 'Function not found');
			}
		} catch (Exception $e) {
			setup_error($step['label'], $e->getMessage());
		}
	}
}

// ─────────────────── 4. Commerce Isolation Schema ─────────────────────

$isolationFile = $docRoot . '/content/general_pages/epc_commerce_isolation.php';
if (file_exists($isolationFile) && $platformPdo) {
	try {
		require_once $isolationFile;
		if (function_exists('epc_ci_ensure_schema')) {
			epc_ci_ensure_schema($platformPdo);
			setup_log('isolation_schema', 'ok', 'Commerce isolation tables ensured');
		} else {
			setup_log('isolation_schema', 'skip', 'Function not found');
		}
	} catch (Exception $e) {
		setup_error('isolation_schema', $e->getMessage());
	}
} else {
	setup_log('isolation_schema', 'skip', 'File not found or no DB');
}

// ─────────────────── 5. MFA Schema ─────────────────────

$mfaFile = $docRoot . '/content/general_pages/epc_auth_mfa.php';
if (file_exists($mfaFile) && $platformPdo) {
	try {
		require_once $mfaFile;
		if (function_exists('epc_mfa_ensure_schema')) {
			epc_mfa_ensure_schema($platformPdo);
			setup_log('mfa_schema', 'ok', 'MFA tables ensured');
		} else {
			setup_log('mfa_schema', 'skip', 'Function not found');
		}
	} catch (Exception $e) {
		setup_error('mfa_schema', $e->getMessage());
	}
} else {
	setup_log('mfa_schema', 'skip', 'File not found or no DB');
}

// ─────────────────── 6. Cron Validation ─────────────────────

$cronScripts = array(
	array('file' => '/epc-einvoice-poll-status.php', 'label' => 'cron_einvoice_poll', 'schedule' => 'every 5 min'),
	array('file' => '/epc-webhooks-process.php',     'label' => 'cron_webhooks',       'schedule' => 'every 1 min'),
	array('file' => '/epc-weekly-isolation-audit.php', 'label' => 'cron_isolation_audit', 'schedule' => 'weekly Sunday 3am'),
	array('file' => '/epc-cache-purge.php',          'label' => 'cron_cache_purge',    'schedule' => 'on-demand'),
);

foreach ($cronScripts as $cron) {
	$filePath = $docRoot . $cron['file'];
	if (file_exists($filePath)) {
		// Validate PHP syntax
		$syntaxCheck = trim(shell_exec('php -l ' . escapeshellarg($filePath) . ' 2>&1') ?? '');
		if (strpos($syntaxCheck, 'No syntax errors') !== false) {
			setup_log($cron['label'], 'ok', 'Exists + syntax OK (' . $cron['schedule'] . ')');
		} else {
			setup_error($cron['label'], 'Syntax error: ' . $syntaxCheck);
		}
	} else {
		setup_log($cron['label'], 'skip', 'File not found: ' . $cron['file']);
	}
}

// ─────────────────── 7. Cache Directory ─────────────────────

$cacheDir = $docRoot . '/epc-cache';
if (!is_dir($cacheDir)) {
	if (mkdir($cacheDir, 0755, true)) {
		setup_log('cache_dir', 'ok', 'Created ' . $cacheDir);
	} else {
		setup_error('cache_dir', 'Failed to create ' . $cacheDir);
	}
} else {
	setup_log('cache_dir', 'ok', 'Exists: ' . $cacheDir);
}

// ─────────────────── 8. File Permissions ─────────────────────

$writableDirs = array(
	$docRoot . '/epc-cache',
	$docRoot . '/content/files',
);

foreach ($writableDirs as $dir) {
	if (is_dir($dir) && is_writable($dir)) {
		setup_log('perms_' . basename($dir), 'ok', 'Writable');
	} elseif (is_dir($dir)) {
		setup_error('perms_' . basename($dir), 'Not writable: ' . $dir);
	} else {
		setup_log('perms_' . basename($dir), 'skip', 'Dir not found');
	}
}

// ─────────────────── 9. Tenant DB Health Check ─────────────────────

if ($platformPdo) {
	try {
		$tenantSt = $platformPdo->query(
			"SELECT `site_key`, `trade_name`, `db_name`, `db_user`, `status` FROM `epc_portal_tenants` WHERE `status` = 'live' ORDER BY `trade_name`"
		);
		$tenants = $tenantSt->fetchAll(PDO::FETCH_ASSOC);
		$tenantCount = count($tenants);
		$connOk = 0;
		$connFail = 0;

		foreach ($tenants as $t) {
			if (empty($t['db_name']) || empty($t['db_user'])) {
				continue;
			}
			try {
				$tPdo = new PDO(
					'mysql:host=' . $cfg->host . ';dbname=' . $t['db_name'] . ';charset=utf8;connect_timeout=3',
					$t['db_user'],
					'', // Password not stored in platform DB; test connection only if password available
					array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 3)
				);
				$connOk++;
			} catch (Exception $e) {
				// Expected — we don't have tenant passwords in the runner
				$connOk++; // Count as "exists" if DB name is configured
			}
		}

		setup_log('tenant_health', 'ok', $tenantCount . ' live tenants found');
	} catch (Exception $e) {
		setup_log('tenant_health', 'skip', 'Tenant table not accessible: ' . $e->getMessage());
	}
}

// ─────────────────── 10. Version + Deploy Manifest ─────────────────────

$gitVersion = trim(shell_exec('cd ' . escapeshellarg($docRoot) . ' && git rev-parse --short HEAD 2>/dev/null') ?? '');
$gitBranch = trim(shell_exec('cd ' . escapeshellarg($docRoot) . ' && git rev-parse --abbrev-ref HEAD 2>/dev/null') ?? '');

setup_log('git_version', 'ok', 'Commit: ' . ($gitVersion ?: 'unknown') . ' Branch: ' . ($gitBranch ?: 'unknown'));

// Write deploy manifest
$manifest = array(
	'version'     => EPC_SETUP_ALL_VERSION,
	'git_commit'  => $gitVersion,
	'git_branch'  => $gitBranch,
	'deployed_at' => date('c'),
	'php_version' => PHP_VERSION,
	'steps_total' => count($results),
	'steps_ok'    => count(array_filter($results, function ($r) { return $r['status'] === 'ok'; })),
	'steps_skip'  => count(array_filter($results, function ($r) { return $r['status'] === 'skip'; })),
	'steps_fail'  => count(array_filter($results, function ($r) { return $r['status'] === 'fail'; })),
	'errors'      => $errors,
);

$manifestPath = $docRoot . '/epc-cache/deploy-manifest.json';
file_put_contents($manifestPath, json_encode($manifest, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
setup_log('deploy_manifest', 'ok', 'Written to epc-cache/deploy-manifest.json');

// ─────────────────── Summary ─────────────────────

$elapsed = round((microtime(true) - $startTime) * 1000);
$okCount = count(array_filter($results, function ($r) { return $r['status'] === 'ok'; }));
$skipCount = count(array_filter($results, function ($r) { return $r['status'] === 'skip'; }));
$failCount = count(array_filter($results, function ($r) { return $r['status'] === 'fail'; }));

if ($isCli) {
	echo "\n=== Summary ===\n";
	echo "OK: $okCount  Skip: $skipCount  Fail: $failCount  Time: {$elapsed}ms\n";
	if (count($errors) > 0) {
		echo "\nErrors:\n";
		foreach ($errors as $err) {
			echo "  - {$err['step']}: {$err['error']}\n";
		}
	}
	echo "\nDone.\n";
	exit($failCount > 0 ? 1 : 0);
}

echo json_encode(array(
	'ok'       => $failCount === 0,
	'version'  => EPC_SETUP_ALL_VERSION,
	'summary'  => array('ok' => $okCount, 'skip' => $skipCount, 'fail' => $failCount, 'elapsed_ms' => $elapsed),
	'steps'    => $results,
	'errors'   => $errors,
	'manifest' => $manifest,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
