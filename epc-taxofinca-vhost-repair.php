<?php
/**
 * Fix taxofinca routing: dedicated CLP site + shared docroot; scrub taxofinca off epartscart vhost.
 * https://www.epartscart.com/epc-taxofinca-vhost-repair.php?token=...&clp_pass=...
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '1');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_theme_templates.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';
$platformSite = 'www.epartscart.com';
$sharedRoot = '/home/epartscart/htdocs/www.epartscart.com';
$siteUser = 'epartscart';
$siteUserPass = trim((string) ($_GET['site_user_password'] ?? getenv('EPC_SITE_USER_PASSWORD') ?: 'EpcTaxofinca2026!'));
$aliases = array($hostname, $bare);

if ($clpPass === '') {
	exit("clp_pass required\n");
}

function epc_tf_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	$location = '';
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
			if (stripos($h, 'Location:') === 0) {
				$location = trim(substr($h, 9));
			}
		}
	}
	$hint = '';
	if ($body !== false) {
		if (stripos($body, '403 Forbidden') !== false && stripos($body, 'nginx') !== false) {
			$hint = ' [nginx403]';
		} elseif (stripos($body, '404 Not Found') !== false && stripos($body, 'nginx') !== false) {
			$hint = ' [nginx404]';
		} elseif (stripos($body, 'Taxofin') !== false || stripos($body, 'taxofinca') !== false) {
			$hint = ' [taxofinca]';
		} elseif (stripos($body, 'eParts Cart') !== false || stripos($body, 'epartscart') !== false) {
			$hint = ' [epartscart]';
		}
	}
	$loc = $location !== '' ? (' -> ' . $location) : '';
	return "HTTP {$code}{$hint}{$loc}";
}

echo "=== Taxofinca vhost repair ===\n\n";
echo "Before:\n";
echo '  origin ' . $hostname . ': ' . epc_tf_probe('http://127.0.0.1/', $hostname) . "\n";
echo '  https://' . $hostname . '/: ' . epc_tf_probe('https://' . $hostname . '/') . "\n";
echo '  https://' . $bare . '/: ' . epc_tf_probe('https://' . $bare . '/') . "\n";
echo '  https://' . $hostname . '/cp/: ' . epc_tf_probe('https://' . $hostname . '/cp/') . "\n\n";

$pdo = null;
foreach (array($sharedRoot . '/config.local.php', $sharedRoot . '/config.php', __DIR__ . '/config.local.php') as $path) {
	if (!is_file($path)) {
		continue;
	}
	$epc_config_local = null;
	$cfg = null;
	require $path;
	if (isset($cfg) && is_object($cfg)) {
		try {
			$pdo = new PDO(
				'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
				$cfg->user,
				$cfg->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			echo "PDO: {$cfg->user}@{$cfg->db}\n";
			break;
		} catch (Exception $e) {
			echo 'PDO fail: ' . $e->getMessage() . "\n";
		}
	}
}

if ($pdo instanceof PDO) {
	try {
		epc_portal_db_ensure($pdo);
		$ind = epc_portal_industry('tax_advisory');
		epc_portal_save_site_settings($pdo, array(
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
			'enabled_packs' => isset($ind['cp_packs']) ? $ind['cp_packs'] : array('core', 'erp', 'professional', 'tax_advisory'),
			'theme' => epc_portal_style_template_theme('tax_advisory', 'classic'),
		));
		echo "Site settings saved for {$hostname} in tenant DB\n";
	} catch (Exception $e) {
		echo 'Site settings: ' . $e->getMessage() . "\n";
	}
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] === '' || $vf['token'] === '') {
	exit("Could not read vhost for {$platformSite}\n");
}
$vhost = epc_clp_vhost_scrub_tenant_misroutes($vf['vhost'], $aliases);
if (epc_clp_vhost_save($cookie, $platformSite, $vhost, $vf['token'])) {
	echo "Scrubbed epartscart redirect misroutes, len=" . strlen($vhost) . "\n";
} else {
	echo "WARN: epartscart vhost scrub save failed\n";
}

$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
$tenantSiteReady = epc_clp_web_site_listed($dash, $hostname);

$create = epc_clp_web_create_php_site($cookie, array(
	'domain' => $hostname,
	'site_user' => $siteUser,
	'site_user_password' => $siteUserPass,
	'php_version' => '8.3',
));
echo "Create {$hostname}:\n";
foreach ($create['log'] as $line) {
	echo '  ' . $line . "\n";
}
if (!empty($create['ok'])) {
	$tenantSiteReady = true;
}

if ($tenantSiteReady) {
$repoint = epc_clp_web_set_site_docroot($cookie, $hostname, $sharedRoot);
echo "Docroot {$hostname} -> {$sharedRoot}:\n";
foreach ($repoint['log'] as $line) {
	echo '  ' . $line . "\n";
}

if (!epc_clp_web_site_listed(epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie), $bare)) {
	$createBare = epc_clp_web_create_php_site($cookie, array(
		'domain' => $bare,
		'site_user' => $siteUser,
		'site_user_password' => $siteUserPass,
		'php_version' => '8.3',
	));
	echo "Create {$bare}:\n";
	foreach ($createBare['log'] as $line) {
		echo '  ' . $line . "\n";
	}
	$repointBare = epc_clp_web_set_site_docroot($cookie, $bare, $sharedRoot);
	echo "Docroot {$bare}:\n";
	foreach ($repointBare['log'] as $line) {
		echo '  ' . $line . "\n";
	}
} else {
	echo "Site {$bare} already listed\n";
}

$ssl = epc_clp_web_install_ssl($cookie, $hostname, array($bare));
echo 'SSL ' . $hostname . ': ' . implode(' | ', array_slice($ssl['log'], 0, 3)) . "\n";
} else {
	echo "Dedicated site not ready — Model C via Varnish on {$platformSite}\n";
	$route = epc_clp_vhost_configure_tenant_via_varnish($cookie, $platformSite, $aliases, $platformSite);
	foreach ($route['log'] as $line) {
		echo '  ' . $line . "\n";
	}
	$ssl = epc_clp_web_install_ssl($cookie, $platformSite, $aliases);
	echo 'SSL ' . $platformSite . ': ' . implode(' | ', array_slice($ssl['log'], 0, 3)) . "\n";
}

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($sharedRoot));
echo 'permissions reset code=' . $perm['code'] . ' ' . substr($perm['output'], 0, 120) . "\n";
@exec('chmod -R o+rX ' . escapeshellarg($sharedRoot) . ' 2>&1', $chmodOut, $chmodCode);
echo 'chmod o+rX docroot code=' . $chmodCode . "\n";


$purge = epc_clp_run("varnish-cache:purge --purge='https://{$hostname}/,https://{$bare}/'");
echo 'Varnish purge code=' . $purge['code'] . ' ' . substr($purge['output'], 0, 120) . "\n";

echo "\nAfter:\n";
echo '  origin ' . $hostname . ': ' . epc_tf_probe('http://127.0.0.1/', $hostname) . "\n";
echo '  https://' . $hostname . '/: ' . epc_tf_probe('https://' . $hostname . '/') . "\n";
echo '  https://' . $bare . '/: ' . epc_tf_probe('https://' . $bare . '/') . "\n";
echo '  https://' . $hostname . '/cp/: ' . epc_tf_probe('https://' . $hostname . '/cp/') . "\n";
echo '  https://' . $platformSite . '/: ' . epc_tf_probe('https://' . $platformSite . '/') . "\n";

echo "\nIf apex still redirects wrong: set Cloudflare Page Rule @ -> https://www.taxofinca.com/\$1\n";
echo "If 403 persists: Cloudflare SSL Full + purge cache; confirm DNS points to same IP as epartscart.\n";
