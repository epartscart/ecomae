<?php
/**
 * Emergency single-tenant live switch using host runtime DB override.
 * Example:
 * https://www.ecomae.com/epc-tenant-emergency-live.php?token=...&tenant=taxofinca&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';

$tenantKey = strtolower(trim((string) ($_GET['tenant'] ?? 'taxofinca')));
$apply = !empty($_GET['apply']);
$registryPass = trim((string) ($_GET['db_password'] ?? ''));
$target = array(
	'epartscart' => array('host' => 'www.epartscart.com', 'industry' => 'auto_parts', 'mode' => 'full'),
	'taxofinca' => array('host' => 'www.taxofinca.com', 'industry' => 'tax_advisory', 'mode' => 'consultancy'),
	'electronicae' => array('host' => 'www.electronicae.com', 'industry' => 'electronics', 'mode' => 'full'),
	'stylenlook' => array('host' => 'www.stylenlook.com', 'industry' => 'fashion', 'mode' => 'full'),
	'thejewellerytrend' => array('host' => 'www.thejewellerytrend.com', 'industry' => 'jewellery', 'mode' => 'full'),
);
if (!isset($target[$tenantKey])) {
	exit("Unsupported tenant. Use tenant=epartscart|taxofinca|electronicae|stylenlook|thejewellerytrend\n");
}
$host = $target[$tenantKey]['host'];
$docroot = '/home/ecomae/htdocs/www.ecomae.com';

function epc_tel_probe(string $url): array
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
	return array('code' => $code, 'nodb' => $noDb);
}

echo "=== Tenant emergency live ===\n";
echo "tenant={$tenantKey} host={$host}\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$beforeRoot = epc_tel_probe('https://' . $host . '/');
$beforeEn = epc_tel_probe('https://' . $host . '/en/');
$beforeCp = epc_tel_probe('https://' . $host . '/cp/');
echo "Before: /={$beforeRoot['code']} nodb=" . ($beforeRoot['nodb'] ? 'yes' : 'no')
	. " | /en/={$beforeEn['code']} nodb=" . ($beforeEn['nodb'] ? 'yes' : 'no')
	. " | /cp/={$beforeCp['code']} nodb=" . ($beforeCp['nodb'] ? 'yes' : 'no') . "\n";

if (!$apply) {
	echo "Dry run. Re-run with apply=1.\n";
	exit;
}

$cfgFile = $docroot . '/config.local.php';
if (!is_file($cfgFile)) {
	exit("Missing {$cfgFile}\n");
}
$epc_config_local = null;
include $cfgFile;
$dbName = trim((string) ($epc_config_local['db'] ?? 'ecomae'));
$dbUser = trim((string) ($epc_config_local['user'] ?? 'ecomae'));
$dbPass = trim((string) ($epc_config_local['password'] ?? ''));
if ($registryPass !== '') {
	$dbPass = $registryPass;
}
if ($dbPass === '') {
	exit("Cannot resolve platform DB password from config.local.php\n");
}

$overrideFile = $docroot . '/config.tenant-host-db.php';
$existing = array();
if (is_file($overrideFile)) {
	$epc_tenant_host_db = null;
	include $overrideFile;
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db)) {
		$existing = $epc_tenant_host_db;
	}
}
$existing[$host] = array('db' => $dbName, 'user' => $dbUser, 'password' => $dbPass);
$php = "<?php\n\$epc_tenant_host_db = " . var_export($existing, true) . ";\n";
file_put_contents($overrideFile, $php);
echo "Wrote {$overrideFile} override for {$host}\n";

try {
	$pdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($pdo);
	$tpl = epc_portal_tenant_templates()[$tenantKey];
	$save = epc_portal_save_tenant($pdo, array(
		'site_key' => $tenantKey,
		'hostname' => $host,
		'industry_code' => $target[$tenantKey]['industry'],
		'status' => 'live',
		'trade_name' => $tpl['trade_name'],
		'hub_name' => $tpl['hub_name'],
		'from_email' => $tpl['from_email'],
		'db_name' => $dbName,
		'db_user' => $dbUser,
		'db_password' => $dbPass,
		'notes' => 'epc-tenant-emergency-live.php host override',
	));
	echo 'Registry: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
	$settings = array(
		'host' => $host,
		'industry_code' => $target[$tenantKey]['industry'],
		'access_mode' => $target[$tenantKey]['mode'],
		'theme_template' => epc_portal_default_theme_template($target[$tenantKey]['industry']),
		'domain_path' => 'https://' . $host . '/',
		'theme' => epc_portal_style_template_theme(
			$target[$tenantKey]['industry'],
			epc_portal_default_theme_template($target[$tenantKey]['industry'])
		),
	);
	epc_portal_save_site_settings($pdo, $settings);
	echo "site_settings: OK\n";
} catch (Exception $e) {
	echo 'Registry/site_settings: FAIL — ' . $e->getMessage() . "\n";
}

$afterRoot = epc_tel_probe('https://' . $host . '/');
$afterEn = epc_tel_probe('https://' . $host . '/en/');
$afterCp = epc_tel_probe('https://' . $host . '/cp/');
echo "After: /={$afterRoot['code']} nodb=" . ($afterRoot['nodb'] ? 'yes' : 'no')
	. " | /en/={$afterEn['code']} nodb=" . ($afterEn['nodb'] ? 'yes' : 'no')
	. " | /cp/={$afterCp['code']} nodb=" . ($afterCp['nodb'] ? 'yes' : 'no') . "\n";
