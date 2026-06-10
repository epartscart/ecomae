<?php
/**
 * Recover epartscart storefront DB routing after connection exhaustion or registry drift.
 * https://www.ecomae.com/epc-epartscart-db-recover.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$apply = !empty($_GET['apply']);
$docroot = '/home/ecomae/htdocs/www.ecomae.com';
$host = 'www.epartscart.com';

function epc_edr_probe(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
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
	$noDb = is_string($body) && stripos($body, 'No DB connect') !== false;
	$html = is_string($body) && (stripos($body, '<html') !== false || stripos($body, '<!DOCTYPE') !== false);
	return array('code' => $code, 'nodb' => $noDb, 'html' => $html, 'bytes' => is_string($body) ? strlen($body) : 0);
}

function epc_edr_release_php_fpm(): void
{
	foreach (array(
		'runuser -u root -- systemctl restart php8.4-fpm',
		'runuser -u root -- systemctl restart php8.3-fpm',
		'runuser -u root -- systemctl restart php8.2-fpm',
		'runuser -u root -- systemctl restart php-fpm',
	) as $cmd) {
		$r = epc_clp_run_cmd($cmd . ' 2>&1');
		echo $cmd . ' → ' . trim((string) ($r['output'] ?? '')) . " [exit={$r['code']}]\n";
		if ((int) $r['code'] === 0) {
			return;
		}
	}
}

echo "=== epartscart DB recover ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$before = epc_edr_probe('https://' . $host . '/en/');
echo "Before /en/: HTTP {$before['code']} nodb=" . ($before['nodb'] ? 'yes' : 'no') . " bytes={$before['bytes']}\n\n";

$resolved = epc_portal_resolve_tenant_db_credentials();
echo 'resolve_tenant_db_credentials: db=' . $resolved['db'] . ' user=' . $resolved['user']
	. ' pass_len=' . strlen($resolved['password']) . "\n";

$mysqli = @new mysqli('127.0.0.1', $resolved['user'], $resolved['password'], $resolved['db']);
if ($mysqli->connect_errno) {
	echo 'mysqli: FAIL ' . $mysqli->connect_error . "\n";
} else {
	$tables = $mysqli->query('SHOW TABLES');
	$cnt = $tables instanceof mysqli_result ? $tables->num_rows : 0;
	echo "mysqli: OK tables={$cnt}\n";
	$mysqli->close();
}

if (!$apply) {
	echo "\nDry run. Re-run with apply=1.\n";
	exit;
}

echo "\n=== Release PHP-FPM ===\n";
epc_edr_release_php_fpm();
sleep(2);

$tenantDbFile = $docroot . '/config.tenant-db.php';
$tenantDbPhp = "<?php\n\$epc_tenant_db = array(\n\t'db' => 'docpart',\n\t'user' => 'docpart',\n\t'password' => "
	. var_export($resolved['password'], true) . ",\n);\n";
file_put_contents($tenantDbFile, $tenantDbPhp);
echo "Wrote {$tenantDbFile}\n";

$flag = $docroot . '/epc-opcache-bust-once.flag';
file_put_contents($flag, (string) time());
echo "Wrote opcache bust flag\n";

$ecomaePass = trim((string) ($_GET['db_password'] ?? ''));
if ($ecomaePass === '' && is_file($docroot . '/config.local.php')) {
	$epc_config_local = null;
	include $docroot . '/config.local.php';
	$ecomaePass = trim((string) ($epc_config_local['password'] ?? ''));
}
if ($ecomaePass !== '' && $resolved['password'] !== '') {
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8;connect_timeout=5',
			'ecomae',
			$ecomaePass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 5)
		);
		epc_portal_db_ensure($pdo);
		$tpl = epc_portal_tenant_templates()['epartscart'];
		$save = epc_portal_save_tenant($pdo, array(
			'site_key' => 'epartscart',
			'hostname' => $host,
			'industry_code' => 'auto_parts',
			'status' => 'live',
			'trade_name' => $tpl['trade_name'],
			'hub_name' => $tpl['hub_name'],
			'from_email' => $tpl['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'db_password' => $resolved['password'],
			'notes' => 'epc-epartscart-db-recover.php',
		));
		echo 'Registry: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
	} catch (Exception $e) {
		echo 'Registry: FAIL — ' . $e->getMessage() . "\n";
	}
} else {
	echo "Registry skipped (no ecomae password)\n";
}

echo "\n=== After ===\n";
foreach (array(
	'https://' . $host . '/en/',
	'https://' . $host . '/en/parts/toyota/1780131090',
	'https://' . $host . '/en/apai-root-auto-parts',
) as $url) {
	$p = epc_edr_probe($url);
	echo $url . ": HTTP {$p['code']} nodb=" . ($p['nodb'] ? 'yes' : 'no')
		. ' html=' . ($p['html'] ? 'yes' : 'no') . " bytes={$p['bytes']}\n";
}
