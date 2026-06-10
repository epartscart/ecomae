<?php
/**
 * Set all commerce tenants to United Arab Emirates (AE) and apply country profile.
 *
 * Dry-run:  ?token=epartscart-deploy-2026
 * Apply:    ?token=...&apply=1
 * One key:  &site_key=epartscart
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/tenant_hub/epc_tenant_country_profile.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
$targetCountry = 'AE';

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	exit("Platform DB unavailable\n");
}
epc_portal_db_ensure($platformPdo);

$tenants = epc_portal_list_tenants($platformPdo);
$commerceKeys = array('epartscart', 'electronicae', 'stylenlook', 'taxofinca', 'thejewellerytrend', 'ecomae', 'platform', 'docpart', 'asap');

echo '=== EPC tenant country migrate → UAE (AE) ===' . "\n";
echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'tenants_listed=' . count($tenants) . "\n\n";

$rows = array();
foreach ($tenants as $t) {
	$key = (string) ($t['site_key'] ?? '');
	if ($key === '' || $key === 'ecomae') {
		continue;
	}
	if ($onlyKey !== '' && $key !== $onlyKey) {
		continue;
	}
	$status = (string) ($t['status'] ?? '');
	if (!in_array($status, array('live', 'dns_pending', 'draft'), true) && empty($t['is_demo'])) {
		continue;
	}
	$rows[] = $t;
}

if ($onlyKey === '' && count($rows) === 0) {
	foreach ($commerceKeys as $key) {
		$row = epc_portal_tenant_get($platformPdo, $key);
		if ($row !== null) {
			$rows[] = $row;
		}
	}
}

$summary = array();
foreach ($rows as $t) {
	$key = (string) $t['site_key'];
	$host = (string) ($t['hostname'] ?? '');
	$cur = strtoupper((string) ($t['country_code'] ?? ''));
	echo "=== {$key} ({$host}) ===\n";
	echo '  current_country_code=' . ($cur !== '' ? $cur : '(unset)') . "\n";
	if (!$apply) {
		echo "  (dry-run — would set AE + apply profile)\n\n";
		$summary[] = array('site_key' => $key, 'host' => $host, 'ok' => true, 'dry' => true);
		continue;
	}
	$result = epc_tenant_apply_country_profile($key, $targetCountry, $platformPdo);
	echo '  ok=' . ($result['ok'] ? 'yes' : 'no') . ' country=' . $result['country_code'] . "\n";
	foreach ($result['steps'] as $step => $val) {
		echo "    {$step}={$val}\n";
	}
	foreach ($result['errors'] as $err) {
		echo "    ERROR: {$err}\n";
	}
	echo "\n";
	$summary[] = array(
		'site_key' => $key,
		'host' => $host,
		'ok' => !empty($result['ok']),
		'country' => $result['country_code'],
		'steps' => $result['steps'],
	);
}

echo "--- summary ---\n";
printf("%-22s %-28s %8s %s\n", 'site_key', 'hostname', 'country', 'status');
foreach ($summary as $s) {
	printf(
		"%-22s %-28s %8s %s\n",
		$s['site_key'],
		$s['host'] ?? '',
		$s['country'] ?? 'AE',
		!empty($s['dry']) ? 'dry-run' : (!empty($s['ok']) ? 'OK' : 'FAIL')
	);
}
echo "\nDone.\n";
