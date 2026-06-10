<?php
/**
 * Enable production security lockdown (run once after deploy, then block this script too).
 * GET/POST: token=EPC_DEPLOY_TOKEN
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$flag = __DIR__ . '/.epc-security-lockdown';
$now = gmdate('c');
$payload = array(
	'enabled_at' => $now,
	'enabled_by_ip' => epc_deploy_client_ip(),
	'note' => 'Blocks public access to epc-*.php, extract-zip.php, chunk-receiver.php via .htaccess',
);

if (!file_put_contents($flag, json_encode($payload, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES))) {
	http_response_code(500);
	exit(json_encode(array('status' => false, 'message' => 'Could not write lockdown flag')));
}

echo json_encode(array(
	'status' => true,
	'message' => 'Production lockdown enabled',
	'flag' => '.epc-security-lockdown',
	'enabled_at' => $now,
	'next_steps' => array(
		'Set server env EPC_DEPLOY_TOKEN to a new random value (rotate from default).',
		'Set EPC_DEPLOY_ALLOWED_IPS to your office/VPN IP if you still need setup scripts.',
		'Rotate DB password, tech_key, SMTP password, UMAPI key.',
		'Re-run deploy only via SSH/SFTP or temporarily remove .epc-security-lockdown.',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
