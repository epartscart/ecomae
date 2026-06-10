<?php
/**
 * epartscart.com — ECOM AE platform tenant cutover (Model C nginx alias, Super CP registry).
 * epartscart is NOT separate Hostinger hosting; it shares www.ecomae.com docroot.
 * https://www.ecomae.com/epc-epartscart-supercp-cutover.php?token=...&clp_pass=...&apply=1
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

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$ecomaeDbPass = trim((string) ($_GET['db_password'] ?? ''));
$apply = !empty($_GET['apply']);
$hostname = 'www.epartscart.com';
$bare = 'epartscart.com';
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$legacyDocroot = '/home/epartscart/htdocs/www.epartscart.com';
$aliasHosts = array($hostname, $bare);

function epc_epc_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$snippet = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 90) : 'no body';
	$hint = '';
	if (stripos((string) $body, 'eParts Cart') !== false || stripos((string) $body, 'epartscart') !== false) {
		$hint = ' [epartscart]';
	} elseif (stripos((string) $body, 'ECOM AE') !== false || stripos((string) $body, 'ecomae') !== false) {
		$hint = ' [ecomae]';
	}
	return "HTTP {$code}{$hint} — {$snippet}";
}

echo "=== epartscart.com Super CP cutover ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'platform_site=' . $platformSite . "\n";
echo 'platform_docroot=' . $platformDocroot . "\n";
echo 'platform_ip=' . epc_portal_platform_ip() . "\n\n";

echo "=== BEFORE (origin + public) ===\n";
echo "  origin / Host {$hostname}: " . epc_epc_probe('http://127.0.0.1/', $hostname) . "\n";
echo "  origin /cp/ Host {$hostname}: " . epc_epc_probe('http://127.0.0.1/cp/', $hostname) . "\n";
echo "  origin /erp Host {$hostname}: " . epc_epc_probe('http://127.0.0.1/erp', $hostname) . "\n";
echo "  public https://{$hostname}/: " . epc_epc_probe('https://' . $hostname . '/') . "\n";
echo "  public https://{$hostname}/cp/: " . epc_epc_probe('https://' . $hostname . '/cp/') . "\n";
echo "  public https://{$hostname}/erp: " . epc_epc_probe('https://' . $hostname . '/erp') . "\n";
echo "  public https://{$hostname}/en/: " . epc_epc_probe('https://' . $hostname . '/en/') . "\n";
echo "  public https://{$bare}/ (apex): " . epc_epc_probe('https://' . $bare . '/') . "\n\n";

$dbPass = '';
$dbUser = 'docpart';
$dbName = 'docpart';
// Tenant storefront uses docpart — do not use platform config.local (ecomae operator overrides).
foreach (array(
	$legacyDocroot . '/config.local.php',
	$legacyDocroot . '/config.php',
	$platformDocroot . '/config.php',
	__DIR__ . '/config.php',
) as $path) {
	if (!is_file($path)) {
		continue;
	}
	if (substr($path, -13) === 'config.local.php') {
		$epc_config_local = null;
		include $path;
		if (isset($epc_config_local['password']) && (string) $epc_config_local['password'] !== '') {
			$dbPass = (string) $epc_config_local['password'];
		}
		if (isset($epc_config_local['user']) && (string) $epc_config_local['user'] !== '') {
			$dbUser = (string) $epc_config_local['user'];
		}
		if (isset($epc_config_local['db']) && (string) $epc_config_local['db'] !== '') {
			$dbName = (string) $epc_config_local['db'];
		}
		continue;
	}
	if (!class_exists('DP_Config', false)) {
		include_once $path;
	}
	$cfg = new DP_Config();
	$dbPass = (string) $cfg->password;
	$dbUser = (string) $cfg->user;
	$dbName = (string) $cfg->db;
	break;
}
if ($dbName !== 'docpart') {
	$dbName = 'docpart';
	$dbUser = 'docpart';
}
echo "Tenant DB: {$dbUser}@{$dbName}\n";

if ($ecomaeDbPass === '' && is_file($platformDocroot . '/config.local.php')) {
	$epc_config_local = null;
	include $platformDocroot . '/config.local.php';
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
	$ind = epc_portal_industry('auto_parts');
	epc_portal_save_site_settings($tenantPdo, array(
		'host' => $hostname,
		'industry_code' => 'auto_parts',
		'access_mode' => 'full',
		'enabled_packs' => array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing'),
		'theme_template' => 'nero',
		'system_name' => 'eParts Cart',
		'hub_name' => 'Electronic World Group',
		'tagline' => 'Auto parts & commerce',
		'domain_path' => 'https://' . $hostname . '/',
		'contact' => epc_portal_default_contact(array(
			'trade_name' => 'eParts Cart',
			'hub_name' => 'Electronic World Group',
			'from_email' => 'partsdoc2025@gmail.com',
		)),
		'theme' => epc_portal_style_template_theme('auto_parts', 'nero'),
	));
	echo "Tenant site_settings (docpart): OK — domain_path=https://{$hostname}/\n";
} catch (Exception $e) {
	echo 'Tenant site_settings: FAIL — ' . $e->getMessage() . "\n";
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
		$tpl = epc_portal_tenant_templates()['epartscart'];
		$save = epc_portal_save_tenant($pdo, array(
			'site_key' => 'epartscart',
			'hostname' => $hostname,
			'industry_code' => $tpl['industry'],
			'status' => 'live',
			'trade_name' => $tpl['trade_name'],
			'hub_name' => $tpl['hub_name'],
			'from_email' => $tpl['from_email'],
			'db_name' => 'docpart',
			'db_user' => 'docpart',
			'db_password' => $dbPass,
			'notes' => 'epc-epartscart-supercp-cutover.php',
		));
		echo 'Super CP tenant registry: ' . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";
		if (!empty($save['client_sync']['message'])) {
			echo '  tenant_hub pack sync: ' . ($save['client_sync']['ok'] ? 'OK' : 'WARN') . ' — ' . $save['client_sync']['message'] . "\n";
		}
		$row = $pdo->prepare('SELECT hostname, status, db_name FROM `epc_portal_tenants` WHERE site_key = ? LIMIT 1');
		$row->execute(array('epartscart'));
		$t = $row->fetch(PDO::FETCH_ASSOC);
		if ($t) {
			echo '  registry: ' . $t['hostname'] . ' | ' . $t['status'] . ' | db=' . $t['db_name'] . "\n";
		}
	} catch (Exception $e) {
		echo 'Super CP registry: FAIL — ' . $e->getMessage() . "\n";
	}
} else {
	echo "Super CP registry skipped — pass db_password= for ecomae MySQL\n";
}

echo "\n=== Portal apply (tenant hostname) ===\n";
$_SERVER['HTTP_HOST'] = $hostname;
$_SERVER['SERVER_NAME'] = $hostname;
if (!class_exists('DP_Config', false)) {
	$cfgPath = $platformDocroot . '/config.php';
	if (is_file($cfgPath)) {
		require_once $cfgPath;
	}
}
if (class_exists('DP_Config', false)) {
	$cfgProbe = new DP_Config();
	epc_portal_apply_config($cfgProbe);
	$domainHost = parse_url((string) $cfgProbe->domain_path, PHP_URL_HOST);
	echo '  is_client=' . (epc_portal_is_client_hostname($hostname) ? 'yes' : 'no') . "\n";
	echo '  domain_path=' . $cfgProbe->domain_path . "\n";
	echo '  license_host_match=' . ((is_string($domainHost) && $domainHost === $hostname) ? 'ok' : 'FAIL') . "\n";
} else {
	echo "  SKIP — DP_Config not loaded\n";
}

if (!$apply) {
	echo "\nDry run complete. Re-run with apply=1&clp_pass=... to patch nginx + SSL.\n";
	echo "DNS guide: epc-tenant-dns-epartscart.php?token=" . epc_deploy_token() . "\n";
	exit;
}

if ($clpPass === '') {
	exit("\napply=1 requires clp_pass=\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "\nCloudPanel login: OK\n";

if (is_dir($legacyDocroot) && is_dir($platformDocroot)) {
	// Do not copy config.local.php — platform operator overrides must stay on ecomae docroot.
	$syncFiles = array('config.php');
	foreach ($syncFiles as $rel) {
		$from = $legacyDocroot . '/' . $rel;
		$to = $platformDocroot . '/' . $rel;
		if (is_file($from) && (!is_file($to) || md5_file($from) !== md5_file($to))) {
			echo "Sync {$rel}: legacy → platform\n";
			@copy($from, $to);
		}
	}
}

$vh = epc_clp_vhost_configure_tenant_direct_php($cookie, $platformSite, $aliasHosts, $platformSite);
echo implode("\n", $vh['log']) . "\n";
if (empty($vh['ok'])) {
	echo "WARN: vhost configure returned not-ok\n";
}

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] !== '' && $vf['token'] !== '') {
	$patched = epc_clp_vhost_patch_tenant_direct_root($vf['vhost'], $platformDocroot);
	if ($patched !== $vf['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $patched, $vf['token']);
		echo "Patched tenant direct root → {$platformDocroot}\n";
	}
}

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($platformDocroot));
echo 'permissions reset code=' . $perm['code'] . "\n";

$ssl = epc_clp_web_install_ssl($cookie, $platformSite);
echo 'SSL ' . $platformSite . ': ' . implode(' | ', array_slice($ssl['log'], 0, 4)) . "\n";

echo "\n=== AFTER (origin + public) ===\n";
echo "  origin / Host {$hostname}: " . epc_epc_probe('http://127.0.0.1/', $hostname) . "\n";
echo "  origin /cp/ Host {$hostname}: " . epc_epc_probe('http://127.0.0.1/cp/', $hostname) . "\n";
echo "  origin /erp Host {$hostname}: " . epc_epc_probe('http://127.0.0.1/erp', $hostname) . "\n";
echo "  public https://{$hostname}/: " . epc_epc_probe('https://' . $hostname . '/') . "\n";
echo "  public https://{$hostname}/cp/: " . epc_epc_probe('https://' . $hostname . '/cp/') . "\n";
echo "  public https://{$hostname}/erp: " . epc_epc_probe('https://' . $hostname . '/erp') . "\n";

echo "\n=== GoDaddy DNS (direct — NO Cloudflare) ===\n";
echo "A  @   → " . epc_portal_platform_ip() . "   TTL 600\n";
echo "A  www → " . epc_portal_platform_ip() . "   TTL 600\n";
echo "Remove Cloudflare CNAME/A (104.x / 172.x). Do NOT orange-cloud / proxy.\n";
echo "If external HTTPS times out: open platform VPS firewall TCP 80+443 (all direct-DNS tenants).\n";
echo "Script: epc-hostinger-firewall-open-web.php?token=" . epc_deploy_token() . "&apply=1\n";
echo "Probe: epc-epartscart-connectivity-probe.php?token=" . epc_deploy_token() . "\n";
echo "Guide: epc-tenant-dns-epartscart.php?token=" . epc_deploy_token() . "\n";
echo "\nDO NOT delete old VPS until public probes PASS after DNS propagation.\n";
