<?php
/**
 * On-Premises License Admin CLI — issue, list, and revoke on-prem licenses.
 *
 * Usage (run on the platform server, CLI only):
 *   php epc-onprem-license-generate.php --customer "Acme Corp" --tier professional --users 50 --days 365
 *   php epc-onprem-license-generate.php --list
 *   php epc-onprem-license-generate.php --revoke LIC-2026-XXXX-XXXX
 *
 * Requires EPC_LICENSE_SIGNING_KEY_PATH to point at an RSA private key
 * (generate once with: openssl genrsa -out /etc/ecomae/license_signing_key.pem 2048
 * then: openssl rsa -in /etc/ecomae/license_signing_key.pem -pubout -out license_public_key.pem
 * and ship license_public_key.pem inside deploy/on-premises/ to on-prem clients).
 */
define('_ASTEXE_', 1);

if (php_sapi_name() !== 'cli') {
	echo "CLI only.\n";
	exit(1);
}

$_SERVER['DOCUMENT_ROOT'] = $_SERVER['DOCUMENT_ROOT'] ?? __DIR__;
require_once __DIR__ . '/content/general_pages/epc_api_clients.php';
require_once __DIR__ . '/content/general_pages/epc_onprem_licenses.php';

$pdo = epc_api_clients_platform_pdo();
if (!$pdo instanceof PDO) {
	fwrite(STDERR, "Could not connect to the platform database.\n");
	exit(1);
}

function cli_opt(array $args, string $name, $default = null)
{
	$idx = array_search('--' . $name, $args, true);
	if ($idx === false || !isset($args[$idx + 1])) {
		return $default;
	}
	return $args[$idx + 1];
}

$args = $argv;
array_shift($args);

if (in_array('--list', $args, true)) {
	$rows = epc_onprem_license_list($pdo);
	if (empty($rows)) {
		echo "No licenses issued yet.\n";
		exit(0);
	}
	printf("%-24s %-20s %-14s %-10s %-8s %s\n", 'LICENSE KEY', 'CUSTOMER', 'TIER', 'STATUS', 'USERS', 'EXPIRES');
	foreach ($rows as $r) {
		echo sprintf(
			"%-24s %-20s %-14s %-10s %-8s %s\n",
			$r['license_key'],
			substr((string) $r['customer_name'], 0, 20),
			$r['tier'],
			$r['status'],
			$r['users_max'],
			$r['expires_at'] ? date('Y-m-d', (int) $r['expires_at']) : 'never'
		);
	}
	exit(0);
}

$revokeIdx = array_search('--revoke', $args, true);
if ($revokeIdx !== false && isset($args[$revokeIdx + 1])) {
	$key = $args[$revokeIdx + 1];
	$ok = epc_onprem_license_revoke($pdo, $key);
	echo $ok ? "Revoked: {$key}\n" : "License not found: {$key}\n";
	exit($ok ? 0 : 1);
}

$signingKeyPath = epc_onprem_license_signing_key_path();
if (!is_file($signingKeyPath)) {
	fwrite(STDERR, "Warning: no signing key at {$signingKeyPath} — activation will fail until one exists.\n");
	fwrite(STDERR, "Generate one with:\n");
	fwrite(STDERR, "  openssl genrsa -out {$signingKeyPath} 2048\n");
	fwrite(STDERR, "  openssl rsa -in {$signingKeyPath} -pubout -out deploy/on-premises/license_public_key.pem\n\n");
}

$opts = array(
	'customer_name' => cli_opt($args, 'customer', ''),
	'tier' => cli_opt($args, 'tier', 'standard'),
	'users_max' => (int) cli_opt($args, 'users', 25),
	'expires_days' => (int) cli_opt($args, 'days', 365),
	'notes' => cli_opt($args, 'notes', ''),
);

$result = epc_onprem_license_generate($pdo, $opts);

echo "License issued:\n";
echo "  Key:      {$result['license_key']}\n";
echo "  Tier:     {$result['tier']}\n";
echo "  Users:    {$result['users_max']}\n";
echo "  Expires:  " . ($result['expires_at'] ? date('Y-m-d', $result['expires_at']) : 'never') . "\n";
echo "\nGive this key to the customer to run:\n";
echo "  ./install.sh --license {$result['license_key']} --domain erp.customer.com\n";
