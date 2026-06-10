<?php
/**
 * Register a DNS-only tenant and point its CloudPanel site to the ecomae docroot.
 * https://www.epartscart.com/epc-ecomae-register-tenant.php?token=...&clp_pass=...&tenant=epartscart
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

function epc_reg_clp_put(string &$cookie, string $site, string $rel, string $content): void
{
	$panel = epc_clp_panel_url();
	$remoteDir = '/htdocs/' . $site;
	epc_clp_web_request($panel . '/site/' . rawurlencode($site) . '/file-manager', array(), $cookie);
	$parts = explode('/', trim(str_replace('\\', '/', $rel), '/'));
	$name = array_pop($parts);
	$parent = $remoteDir;
	foreach ($parts as $part) {
		epc_clp_web_request($panel . '/file-manager/backend/mkdir', array(
			'method' => 'POST',
			'body' => http_build_query(array('id' => $parent, 'name' => $part)),
		), $cookie);
		$parent .= '/' . $part;
	}
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent, 'name' => $name)),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent . '/' . $name, 'content' => $content)),
	), $cookie);
}

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$clpPutRel = str_replace('\\', '/', trim((string) ($_GET['clp_put'] ?? '')));
$clpPutB64 = (string) ($_POST['b64'] ?? '');
if (!empty($_GET['patch_index_delegate']) && $clpPass !== '') {
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
		exit("CloudPanel login failed\n");
	}
	$roots = array(
		'www.ecomae.com' => '/home/ecomae/htdocs/www.ecomae.com',
		'cp.ecomae.com' => '/home/ecomae/htdocs/cp.ecomae.com',
	);
	$marker = 'Nginx try_files sends /cp/* subpaths here';
	$needle = "epc_portal_apply_config(\$DP_Config);\n\nrequire_once \$_SERVER['DOCUMENT_ROOT']";
	$insert = <<<'PHP'
epc_portal_apply_config($DP_Config);

// Nginx try_files sends /cp/* subpaths here — hand off to backend CP (cp/index.php).
$epcBackendDir = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($epcBackendDir !== '' && isset($_SERVER['REQUEST_URI'])) {
	$epcPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if (!is_string($epcPath) || $epcPath === '') {
		$epcPath = '/';
	}
	$epcCpBase = '/' . $epcBackendDir;
	if ($epcPath === $epcCpBase || $epcPath === $epcCpBase . '/'
		|| (strlen($epcPath) > strlen($epcCpBase) && strpos($epcPath, $epcCpBase . '/') === 0)) {
		$cpEntry = $_SERVER['DOCUMENT_ROOT'] . '/' . $epcBackendDir . '/index.php';
		if (is_file($cpEntry)) {
			require $cpEntry;
			exit;
		}
	}
}

require_once $_SERVER['DOCUMENT_ROOT']
PHP;
	foreach ($roots as $site => $root) {
		$indexPath = $root . '/index.php';
		if (!is_file($indexPath)) {
			echo "skip {$site} — index missing\n";
			continue;
		}
		$content = (string) file_get_contents($indexPath);
		if (strpos($content, $marker) !== false) {
			echo "{$site}: already patched\n";
			continue;
		}
		if (strpos($content, $needle) === false) {
			echo "{$site}: anchor not found\n";
			continue;
		}
		$newContent = str_replace($needle, $insert, $content);
		epc_reg_clp_put($cookie, $site, 'index.php', $newContent);
		echo "{$site}: patched index.php (" . strlen($newContent) . " bytes)\n";
	}
	exit("patch_index_delegate done.\n");
}
if (!empty($_GET['self_update']) && $clpPutB64 !== '') {
	$content = base64_decode($clpPutB64, true);
	if ($content === false || $content === '') {
		exit("Bad self b64\n");
	}
	if (file_put_contents(__FILE__, $content) === false) {
		exit("Self update write failed\n");
	}
	exit("Self update ok bytes=" . strlen($content) . "\n");
}
if ($clpPass !== '' && $clpPutRel !== '' && $clpPutB64 !== '' && strpos($clpPutRel, '..') === false && $clpPutRel[0] !== '/') {
	$cookie = '';
	if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
		exit("CloudPanel login failed\n");
	}
	$content = base64_decode($clpPutB64, true);
	if ($content === false || $content === '') {
		exit("Bad b64\n");
	}
	foreach (array('www.ecomae.com', 'cp.ecomae.com') as $site) {
		epc_reg_clp_put($cookie, $site, $clpPutRel, $content);
		echo "Pushed {$clpPutRel} to {$site}\n";
	}
	exit("Push done.\n");
}
$tenantKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['tenant'] ?? 'epartscart')));
$ecomaeDbPass = trim((string) ($_GET['db_password'] ?? ''));
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';

if ($clpPass === '') {
	exit("clp_pass required\n");
}

$templates = epc_portal_tenant_templates();
if (!isset($templates[$tenantKey])) {
	exit("Unknown tenant template: {$tenantKey}\n");
}
$tpl = $templates[$tenantKey];
$hostname = (string) $tpl['hostname'];

echo "=== Register tenant: {$tenantKey} ({$hostname}) ===\n";

$dbPass = '';
$dbUser = 'docpart';
$dbName = 'docpart';
$configPaths = array(
	'/home/epartscart/htdocs/www.epartscart.com/config.local.php',
	__DIR__ . '/config.local.php',
);
foreach ($configPaths as $path) {
	if (!is_file($path)) {
		continue;
	}
	$epc_config_local = null;
	require $path;
	if (isset($epc_config_local['password']) && $epc_config_local['password'] !== '') {
		$dbPass = (string) $epc_config_local['password'];
	}
	if (isset($epc_config_local['db']) && $epc_config_local['db'] !== '') {
		$dbName = (string) $epc_config_local['db'];
	}
	if (isset($epc_config_local['user']) && $epc_config_local['user'] !== '') {
		$dbUser = (string) $epc_config_local['user'];
	}
}
if ($dbPass === '') {
	define('_ASTEXE_', 1);
	require_once __DIR__ . '/config.php';
	$cfg = new DP_Config();
	$dbPass = (string) $cfg->password;
	$dbName = (string) $cfg->db;
	$dbUser = (string) $cfg->user;
}
echo "Tenant DB: {$dbUser}@{$dbName}\n";

if ($ecomaeDbPass === '') {
	$platformCfgPath = $platformDocroot . '/config.local.php';
	if (is_file($platformCfgPath)) {
		$epc_config_local = null;
		require $platformCfgPath;
		if (isset($epc_config_local['password']) && $epc_config_local['password'] !== '') {
			$ecomaeDbPass = (string) $epc_config_local['password'];
		}
	}
}

$pdo = null;
if ($ecomaeDbPass !== '') {
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=ecomae;charset=utf8',
			'ecomae',
			$ecomaeDbPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		epc_portal_db_ensure($pdo);
	} catch (Exception $e) {
		echo "Registry DB warning: " . $e->getMessage() . " (continuing with vhost fix)\n";
		$pdo = null;
	}
} else {
	echo "Registry DB skipped — no ecomae db_password\n";
}

if ($pdo instanceof PDO) {
$save = epc_portal_save_tenant($pdo, array(
	'site_key' => $tenantKey,
	'hostname' => $hostname,
	'industry_code' => $tpl['industry'],
	'status' => 'live',
	'trade_name' => $tpl['trade_name'],
	'hub_name' => $tpl['hub_name'],
	'from_email' => $tpl['from_email'],
	'db_name' => $dbName,
	'db_user' => $dbUser,
	'db_password' => $dbPass,
	'notes' => 'Registered via epc-ecomae-register-tenant.php',
));
echo "Registry: " . ($save['ok'] ? 'OK' : 'FAIL') . ' — ' . ($save['message'] ?? '') . "\n";

$count = (int) $pdo->query('SELECT COUNT(*) FROM `epc_portal_tenants` WHERE `site_key` = ' . $pdo->quote($tenantKey))->fetchColumn();
echo "Tenants in registry: {$count}\n";
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

function clp_update_docroot(string &$cookie, string $domain, string $docroot): array
{
	$panel = epc_clp_panel_url();
	$sitePath = '/site/' . rawurlencode($domain);
	$html = epc_clp_web_request($panel . $sitePath . '/settings', array(), $cookie);
	if (!preg_match('/name="site_domain_settings\[_token\]" value="([^"]+)"/', $html, $m)) {
		return array('ok' => false, 'log' => array('CSRF not found on settings'));
	}
	$body = http_build_query(array(
		'site_domain_settings' => array(
			'domainName' => $domain,
			'rootDirectory' => $docroot,
			'submit' => 'Save',
			'_token' => $m[1],
		),
	));
	epc_clp_web_request($panel . $sitePath . '/settings', array(
		'method' => 'POST',
		'body' => $body,
	), $cookie);
	return array('ok' => true, 'log' => array("docroot={$docroot}"));
}

function clp_restore_live_index(string &$cookie, string $domain, string $indexContent): void
{
	$panel = epc_clp_panel_url();
	$remoteDir = '/htdocs/' . $domain;
	epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/file-manager', array(), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $remoteDir, 'name' => 'index.php')),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $remoteDir . '/index.php', 'content' => $indexContent)),
	), $cookie);
}

$tar = '/tmp/ecomae-full-site.tar.gz';
$indexBody = '';
if (is_file($tar)) {
	$tmp = '/tmp/epc-tenant-index.php';
	@unlink($tmp);
	exec('tar -xOzf ' . escapeshellarg($tar) . ' www.epartscart.com/index.php > ' . escapeshellarg($tmp) . ' 2>&1', $o, $c);
	if ($c === 0 && is_file($tmp) && filesize($tmp) > 500 && stripos((string) file_get_contents($tmp), 'Temporarily offline') === false) {
		$indexBody = (string) file_get_contents($tmp);
	}
}
if ($indexBody === '' && is_file($platformDocroot . '/index.php')) {
	$indexBody = (string) file_get_contents($platformDocroot . '/index.php');
}

$docrootResult = array('ok' => true, 'log' => array('skipped — using vhost alias'));
echo "Docroot: using ecomae vhost alias (no shared-docroot pointer)\n";

$panel = epc_clp_panel_url();
$vhSite = is_file('/home/epartscart/htdocs/www.epartscart.com/index.php') ? 'www.epartscart.com' : 'www.ecomae.com';
echo "Vhost base site: {$vhSite}\n";
$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($vhSite) . '/vhost', array(), $cookie);
$vhToken = '';
$vhost = '';
if (preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
	$vhToken = $vt[1];
}
if (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $vm)) {
	$vhost = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}
$bare = preg_replace('/^www\./', '', $hostname);
foreach (array_unique(array($hostname, $bare)) as $aliasHost) {
	if ($aliasHost === '' || $vhost === '' || stripos($vhost, $aliasHost) !== false) {
		continue;
	}
	if (preg_match('/^\s*server_name\s+(.+);/m', $vhost, $sm)) {
		$vhost = preg_replace('/^\s*server_name\s+.+;/m', '  server_name ' . trim($sm[1]) . ' ' . $aliasHost . ';', $vhost, 1);
		echo "Added vhost alias {$aliasHost} on {$vhSite}\n";
	}
}
if ($vhost !== '' && $vhToken !== '' && stripos($vhHtml, $hostname) === false) {
	epc_clp_web_request($panel . '/site/' . rawurlencode($vhSite) . '/vhost', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'vhost-update' => '1',
			'vhost-template' => $vhost,
			'token' => $vhToken,
		)),
	), $cookie);
	echo "Saved vhost on {$vhSite}\n";
}

$del = epc_clp_web_delete_site($cookie, $hostname);
echo "Remove standalone site {$hostname}: " . implode(' ', $del['log']) . "\n";

$ssl = epc_clp_web_install_ssl($cookie, $vhSite);
echo "SSL {$vhSite} (incl. aliases): " . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

if ($indexBody !== '' && $vhSite === 'www.ecomae.com') {
	clp_restore_live_index($cookie, 'www.ecomae.com', $indexBody);
	echo "Ensured live index.php on platform docroot\n";
}

$sslTenant = epc_clp_web_install_ssl($cookie, $hostname);
echo "SSL tenant host: " . implode(' | ', array_slice($sslTenant['log'], 0, 2)) . "\n";

$token = epc_deploy_token();
@file_get_contents('https://www.ecomae.com/ecomae-super-cp-setup.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 120),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "Super CP setup refreshed\n";

function probe_url(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$snippet = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 100) : '';
	return "HTTP {$code} — {$snippet}";
}

echo "\n=== Results ===\n";
echo "Storefront {$hostname}: " . probe_url('https://' . $hostname . '/') . "\n";
echo "Client CP https://{$hostname}/cp/: " . probe_url('https://' . $hostname . '/cp/') . "\n";
echo "Super CP https://cp.ecomae.com/cp/: " . probe_url('https://cp.ecomae.com/cp/') . "\n";
echo "Tenant hub https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub: " . probe_url('https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub') . "\n";

if ($pdo instanceof PDO) {
	$row = $pdo->query('SELECT hostname, status, industry_code, db_name FROM `epc_portal_tenants` WHERE `site_key` = ' . $pdo->quote($tenantKey))->fetch(PDO::FETCH_ASSOC);
	echo "\nTenant row: " . json_encode($row, JSON_UNESCAPED_UNICODE) . "\n";
}
echo "\nOpen:\n";
echo "  Front: https://{$hostname}/\n";
echo "  Client CP: https://{$hostname}/cp/\n";
echo "  Super CP: https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub\n";
