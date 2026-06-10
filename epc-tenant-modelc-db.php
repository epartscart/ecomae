<?php
/**
 * Model C: point tenant storefronts at shared docpart DB.
 * https://www.ecomae.com/epc-tenant-modelc-db.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$apply = !empty($_GET['apply']);
$docroot = '/home/ecomae/htdocs/www.ecomae.com';
$docpartPass = trim((string) ($_GET['db_password'] ?? ''));

if ($docpartPass === '' && is_file($docroot . '/config.tenant-db.php')) {
	$epc_tenant_db = null;
	require $docroot . '/config.tenant-db.php';
	if (isset($epc_tenant_db['password']) && (string) $epc_tenant_db['password'] !== '') {
		$docpartPass = (string) $epc_tenant_db['password'];
	}
}
if ($docpartPass === '' && is_file($docroot . '/config.local.php')) {
	$epc_config_local = null;
	require $docroot . '/config.local.php';
	$docpartPass = (string) ($epc_config_local['password'] ?? '');
}
if ($docpartPass === '') {
	$docpartPass = 'EpC4rt_Db_2026_xK9mQ2';
}

echo "=== epc-tenant-modelc-db ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=docpart;charset=utf8', 'docpart', $docpartPass);
	$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$tables = (int) $pdo->query('SHOW TABLES')->rowCount();
	echo "docpart connect ok tables={$tables}\n";
} catch (Exception $e) {
	exit('docpart connect FAIL: ' . $e->getMessage() . "\n");
}

$tenantCfg = "<?php\n\$epc_tenant_db = array(\n"
	. "\t'db' => 'docpart',\n"
	. "\t'user' => 'docpart',\n"
	. "\t'password' => " . var_export($docpartPass, true) . ",\n"
	. ");\n";

if ($apply) {
	file_put_contents($docroot . '/config.tenant-db.php', $tenantCfg);
	echo "Wrote {$docroot}/config.tenant-db.php\n";
	epc_portal_db_ensure($pdo);
	$tenantTemplates = epc_portal_tenant_templates();
	foreach (array_keys($tenantTemplates) as $siteKey) {
		$tpl = $tenantTemplates[$siteKey];
		if (!empty($tpl['erp_only_shared']) || (string) ($tpl['hosted_on'] ?? '') === 'platform') {
			echo "registry {$siteKey}: SKIP (shared ERP — dedicated DB required)\n";
			continue;
		}
		$save = epc_portal_save_tenant($pdo, array(
			'site_key' => $siteKey,
			'hostname' => $tpl['hostname'],
			'industry_code' => $tpl['industry'],
			'status' => 'live',
			'trade_name' => $tpl['trade_name'],
			'hub_name' => $tpl['hub_name'],
			'from_email' => $tpl['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'db_password' => $docpartPass,
			'notes' => 'epc-tenant-modelc-db.php Model C',
		));
		echo "registry {$siteKey}: " . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
	}
}

function epc_tmdb_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = '';
	if (is_string($body) && stripos($body, 'No DB connect') !== false) {
		$hint = ' [no-db]';
	} elseif (is_string($body) && stripos($body, 'eParts Cart') !== false) {
		$hint = ' [epartscart]';
	} elseif (is_string($body) && stripos($body, 'Taxofin') !== false) {
		$hint = ' [taxofinca]';
	} elseif (is_string($body) && stripos($body, '<html') !== false) {
		$hint = ' [html]';
	}
	return "HTTP {$code}{$hint}";
}

echo "\n=== Probes (from server) ===\n";
$tenantTemplates = epc_portal_tenant_templates();
foreach ($tenantTemplates as $tpl) {
	$host = (string) ($tpl['hostname'] ?? '');
	if ($host === '') {
		continue;
	}
	echo "  https://{$host}/: " . epc_tmdb_probe("https://{$host}/") . "\n";
	echo "  https://{$host}/cp/: " . epc_tmdb_probe("https://{$host}/cp/") . "\n";
}
echo "\nDone.\n";
