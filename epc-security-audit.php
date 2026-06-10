<?php
/**
 * Security posture report (token required). Does not expose secrets.
 * GET: token=EPC_DEPLOY_TOKEN
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$root = __DIR__;
$lockdown = is_file($root . '/.epc-security-lockdown');
$htaccess = is_file($root . '/.htaccess') ? file_get_contents($root . '/.htaccess') : '';

$criticalScripts = array(
	'epc-production-backup.php',
	'epc-erp-500-debug.php',
	'epc-set-email-config-raw.php',
	'epc-cp-route-content-dump.php',
	'epc-cp-guide-eval-test.php',
	'extract-zip.php',
	'chunk-receiver.php',
	'deploy-remote.php',
	'import-db.php',
);

$exposed = array();
foreach ($criticalScripts as $script) {
	$path = ($script === 'extract-zip.php' || $script === 'chunk-receiver.php' || $script === 'deploy-remote.php' || $script === 'import-db.php')
		? dirname($root) . '/' . $script
		: $root . '/' . $script;
	if (is_file($path)) {
		$exposed[] = $script;
	}
}

$noAuthSetup = array();
foreach (glob($root . '/epc-*.php') ?: array() as $file) {
	$base = basename($file);
	if ($base === 'epc-security-audit.php' || $base === 'epc-security-lockdown.php' || $base === 'epc_deploy_auth.php') {
		continue;
	}
	$src = file_get_contents($file);
	if (strpos($src, 'epartscart-deploy-2026') === false
		&& strpos($src, 'epc_deploy_require_token') === false
		&& strpos($src, 'epc_deploy_auth.php') === false) {
		$noAuthSetup[] = $base;
	}
}

$checks = array(
	array(
		'id' => 'lockdown_flag',
		'severity' => $lockdown ? 'ok' : 'critical',
		'title' => 'Production lockdown flag',
		'status' => $lockdown ? 'enabled' : 'disabled',
		'fix' => 'Run epc-security-lockdown.php?token=... after each deploy',
	),
	array(
		'id' => 'deploy_token_env',
		'severity' => (getenv('EPC_DEPLOY_TOKEN') !== false && getenv('EPC_DEPLOY_TOKEN') !== '') ? 'ok' : 'high',
		'title' => 'Deploy token from environment',
		'status' => (getenv('EPC_DEPLOY_TOKEN') !== false && getenv('EPC_DEPLOY_TOKEN') !== '') ? 'custom env set' : 'using default token in code',
		'fix' => 'Set EPC_DEPLOY_TOKEN on server to a long random string',
	),
	array(
		'id' => 'htaccess_security',
		'severity' => (strpos($htaccess, 'epc-security-lockdown') !== false) ? 'ok' : 'high',
		'title' => '.htaccess security rules',
		'status' => (strpos($htaccess, 'epc-security-lockdown') !== false) ? 'present' : 'missing or outdated',
		'fix' => 'Deploy latest .htaccess from repo',
	),
	array(
		'id' => 'config_web',
		'severity' => 'info',
		'title' => 'config.php in web root',
		'status' => is_file($root . '/config.php') ? 'exists (blocked by htaccess if rules deployed)' : 'missing',
		'fix' => 'Never commit real secrets; deny HTTP access',
	),
	array(
		'id' => 'create_license',
		'severity' => is_file($root . '/api/create_license.php') ? 'high' : 'ok',
		'title' => 'api/create_license.php',
		'status' => is_file($root . '/api/create_license.php') ? 'present' : 'removed',
		'fix' => 'Should return 403 (patched in this release)',
	),
	array(
		'id' => 'critical_scripts_present',
		'severity' => empty($exposed) ? 'ok' : ($lockdown ? 'medium' : 'critical'),
		'title' => 'Dangerous scripts on disk',
		'status' => empty($exposed) ? 'none found' : count($exposed) . ' files',
		'files' => $exposed,
		'fix' => 'Delete from production or enable lockdown',
	),
	array(
		'id' => 'unauth_epc_scripts',
		'severity' => empty($noAuthSetup) ? 'ok' : 'critical',
		'title' => 'epc-*.php without token auth',
		'status' => empty($noAuthSetup) ? 'none' : count($noAuthSetup) . ' files',
		'files' => $noAuthSetup,
		'fix' => 'Add epc_deploy_require_token() to each',
	),
);

$score = 100;
foreach ($checks as $c) {
	if ($c['severity'] === 'critical') {
		$score -= 25;
	} elseif ($c['severity'] === 'high') {
		$score -= 10;
	} elseif ($c['severity'] === 'medium') {
		$score -= 5;
	}
}
if ($score < 0) {
	$score = 0;
}

echo json_encode(array(
	'status' => true,
	'site' => 'epartscart.com',
	'audited_at' => gmdate('c'),
	'security_score' => $score,
	'grade' => $score >= 85 ? 'A' : ($score >= 70 ? 'B' : ($score >= 50 ? 'C' : 'F')),
	'lockdown_active' => $lockdown,
	'client_ip' => epc_deploy_client_ip(),
	'checks' => $checks,
	'immediate_actions' => array(
		'Run epc-security-lockdown.php after deploy',
		'Rotate EPC_DEPLOY_TOKEN, tech_key, DB password, SMTP password',
		'Restrict /cp/ to VPN or office IP at firewall/nginx',
		'Enable HTTPS HSTS and CSP at server level',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
