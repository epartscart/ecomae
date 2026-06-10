<?php
/**
 * Bring www.taxofinca.com online: nginx alias on epartscart + tenant registry + probes.
 * https://www.epartscart.com/epc-taxofinca-go-live.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$ecomaeDbPass = trim((string) ($_GET['db_password'] ?? ''));
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';
$storefrontSite = 'www.epartscart.com';
$docroot = '/home/epartscart/htdocs/www.epartscart.com';

if ($clpPass === '') {
	exit("clp_pass required\n");
}

function epc_taxofinca_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$snippet = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 100) : 'no body';
	return "HTTP {$code} — {$snippet}";
}

echo "=== Taxofinca go-live ===\n\n";
echo "Before fix:\n";
echo "  origin Host {$hostname}: " . epc_taxofinca_probe('http://127.0.0.1/', $hostname) . "\n";
echo "  public https://{$hostname}/: " . epc_taxofinca_probe('https://' . $hostname . '/') . "\n\n";

$dbPass = '';
$dbUser = 'docpart';
$dbName = 'docpart';
foreach (array(
	$docroot . '/config.local.php',
	$docroot . '/config.php',
	__DIR__ . '/config.local.php',
) as $path) {
	if (!is_file($path)) {
		continue;
	}
	$epc_config_local = null;
	$cfg = null;
	require $path;
	if (isset($epc_config_local['password']) && (string) $epc_config_local['password'] !== '') {
		$dbPass = (string) $epc_config_local['password'];
	}
	if (isset($epc_config_local['user']) && (string) $epc_config_local['user'] !== '') {
		$dbUser = (string) $epc_config_local['user'];
	}
	if (isset($epc_config_local['db']) && (string) $epc_config_local['db'] !== '') {
		$dbName = (string) $epc_config_local['db'];
	}
	if (isset($cfg) && is_object($cfg) && $dbPass === '') {
		$dbPass = (string) $cfg->password;
		$dbUser = (string) $cfg->user;
		$dbName = (string) $cfg->db;
	}
}
echo "Tenant DB target: {$dbUser}@{$dbName}\n";

if ($ecomaeDbPass === '' && is_file('/home/ecomae/htdocs/www.ecomae.com/config.local.php')) {
	$epc_config_local = null;
	require '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	if (isset($epc_config_local['password'])) {
		$ecomaeDbPass = (string) $epc_config_local['password'];
	}
}

try {
	$tenantPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8',
		$dbUser,
		$dbPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_portal_db_ensure($tenantPdo);
	$ind = epc_portal_industry('tax_advisory');
	require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';
		epc_portal_save_site_settings($tenantPdo, array(
		'host' => $hostname,
		'industry_code' => 'tax_advisory',
		'access_mode' => 'erp_only',
		'theme_template' => 'classic',
		'system_name' => 'Taxofin',
		'hub_name' => 'Taxofin',
		'tagline' => 'Tax & advisory services',
		'domain_path' => 'https://' . $hostname . '/',
		'contact' => epc_portal_default_contact(array(
			'trade_name' => 'Taxofin',
			'hub_name' => 'Taxofin',
			'from_email' => 'info@taxofinca.com',
		)),
		'enabled_packs' => isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core'),
		'theme' => epc_portal_style_template_theme('tax_advisory', 'classic'),
	));
	echo "Site settings for {$hostname} in tenant DB: OK\n";
} catch (Exception $e) {
	echo 'Tenant site settings: ' . $e->getMessage() . "\n";
}

if ($ecomaeDbPass !== '') {
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
			'ecomae',
			$ecomaeDbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		epc_portal_db_ensure($pdo);
		$tpl = epc_portal_tenant_templates()['taxofinca'];
		$save = epc_portal_save_tenant($pdo, array(
			'site_key' => 'taxofinca',
			'hostname' => $hostname,
			'industry_code' => $tpl['industry'],
			'status' => 'live',
			'trade_name' => $tpl['trade_name'],
			'hub_name' => $tpl['hub_name'],
			'from_email' => $tpl['from_email'],
			'db_name' => $dbName,
			'db_user' => $dbUser,
			'db_password' => $dbPass,
			'notes' => 'epc-taxofinca-go-live.php',
		));
		echo 'Super CP registry: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
	} catch (Exception $e) {
		echo 'Super CP registry skipped: ' . $e->getMessage() . "\n";
	}
} else {
	echo "Super CP registry skipped — pass db_password= for ecomae MySQL user\n";
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "\nCloudPanel OK\n";


foreach (array_unique(array($hostname, $bare)) as $orphan) {
	$del = epc_clp_web_delete_site($cookie, $orphan);
	echo "Remove orphan site {$orphan}: " . implode(' ', $del['log']) . "\n";
}

$vh = epc_clp_vhost_configure_tenant_direct_php($cookie, $storefrontSite, array($hostname, $bare), $storefrontSite);
echo implode("\n", $vh['log']) . "\n";

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($docroot));
echo 'permissions reset code=' . $perm['code'] . "\n";
@exec('chmod -R o+rX ' . escapeshellarg($docroot) . ' 2>&1', $chmodOut, $chmodCode);
echo "chmod docroot code={$chmodCode}\n";

$ssl = epc_clp_web_install_ssl($cookie, $storefrontSite);
echo 'SSL ' . $storefrontSite . ': ' . implode(' | ', array_slice($ssl['log'], 0, 3)) . "\n";

echo "\n=== After fix ===\n";
echo "  origin Host {$hostname}: " . epc_taxofinca_probe('http://127.0.0.1/', $hostname) . "\n";
echo "  public https://{$hostname}/: " . epc_taxofinca_probe('https://' . $hostname . '/') . "\n";
echo "  CP https://{$hostname}/cp/: " . epc_taxofinca_probe('https://' . $hostname . '/cp/') . "\n";

echo "\n=== DNS (GoDaddy / Cloudflare) ===\n";
echo "Point www and @ to the SAME target as www.epartscart.com (orange-cloud proxied recommended).\n";
echo "If you use Cloudflare DNS-only (grey cloud) to 31.97.216.247, nginx alias above is required (done).\n";
echo "503 from Cloudflare usually means origin had no vhost — re-test after 2–5 minutes.\n";
