<?php
/**
 * Super CP link audit — menu URLs, content rows, PHP files, HTTP status.
 * https://www.ecomae.com/epc-supercp-link-audit.php?token=epartscart-deploy-2026
 * JSON: add &format=json
 * Fix missing portal routes: add &fix=1 (runs portal setup scripts inline)
 */
declare(strict_types=1);

error_reporting(E_ALL);
ini_set('display_errors', '0');

require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	header('HTTP/1.1 403 Forbidden');
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
$baseHost = 'https://www.ecomae.com';
$format = strtolower((string) ($_GET['format'] ?? 'text'));
$scope = strtolower((string) ($_GET['scope'] ?? 'super'));
$doFix = !empty($_GET['fix']);

$superGroupKeys = array(
	'epc_cp_group_tenant_hub',
	'epc_cp_group_portal',
);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		exit('DB connect failed: ' . $e->getMessage() . "\n");
	}
}

epc_portal_db_ensure($pdo);

$fixesApplied = array();
if ($doFix) {
	$setupUrls = array(
		'ecomae-super-cp-setup.php',
		'epc-portal-cp-setup.php',
		'epc-platform-failover-cp-setup.php',
		'epc-platform-health-checkup-cp-setup.php',
		'epc-platform-governance-setup.php',
		'epc-erp-only-setup.php',
		'epc-api-documentation-cp-setup.php',
		'epc-api-clients-cp-setup.php',
		'epc-custom-shipping-setup.php',
	);
	$token = epc_deploy_token();
	foreach ($setupUrls as $script) {
		$url = $baseHost . '/' . $script . '?token=' . rawurlencode($token);
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_TIMEOUT => 120,
			CURLOPT_SSL_VERIFYPEER => false,
			CURLOPT_FOLLOWLOCATION => true,
		));
		$out = (string) curl_exec($ch);
		$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);
		$fixesApplied[] = array(
			'script' => $script,
			'ok' => ($code >= 200 && $code < 400 && stripos($out, 'Forbidden') === false),
			'http' => $code,
			'output' => trim(substr($out, 0, 400)),
		);
	}
}

function epc_scla_content_url_from_path(string $backend, string $path): string
{
	$path = preg_replace('#\?.*$#', '', $path);
	$prefix = '/' . $backend . '/';
	if (strpos($path, $prefix) === 0) {
		return ltrim(substr($path, strlen($prefix)), '/');
	}
	return ltrim($path, '/');
}

function epc_scla_probe_http(string $url): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => false,
		CURLOPT_TIMEOUT => 25,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_HEADER => true,
		CURLOPT_NOBODY => true,
		CURLOPT_USERAGENT => 'epc-supercp-link-audit/1.0',
	));
	curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$redirect = (string) curl_getinfo($ch, CURLINFO_REDIRECT_URL);
	curl_close($ch);
	return array('http' => $code, 'redirect' => $redirect);
}

function epc_scla_body_has_404(string $url): bool
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 25,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_USERAGENT => 'epc-supercp-link-audit/1.0',
	));
	$body = (string) curl_exec($ch);
	curl_close($ch);
	if ($body === '') {
		return false;
	}
	return (bool) preg_match('/404\s+Page\s+not\s+found|page\s+not\s+available|Страница\s+не\s+найдена/i', $body);
}

$entries = array();

$st = $pdo->query(
	'SELECT i.`id`, i.`caption`, i.`url`, g.`caption` AS group_caption
	 FROM `control_items` i
	 INNER JOIN `control_groups` g ON g.`id` = i.`items_group`
	 ORDER BY g.`order` ASC, i.`order` ASC, i.`id` ASC'
);
while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
	$path = str_replace('<backend>', $backend, (string) $row['url']);
	$entries[] = array(
		'source' => 'sidebar',
		'group' => (string) $row['group_caption'],
		'caption' => (string) $row['caption'],
		'menu_id' => (int) $row['id'],
		'path' => $path,
	);
}

$static = array(
	array('source' => 'topbar', 'group' => 'nav', 'caption' => 'CP home', 'path' => '/' . $backend . '/'),
	array('source' => 'topbar', 'group' => 'nav', 'caption' => 'Platform ERP', 'path' => '/' . $backend . '/platform-erp/'),
	array('source' => 'topbar', 'group' => 'nav', 'caption' => 'Industry settings', 'path' => '/' . $backend . '/control/portal/industry_settings'),
	array('source' => 'tenant_hub', 'group' => 'tabs', 'caption' => 'Onboard', 'path' => '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=onboard'),
	array('source' => 'tenant_hub', 'group' => 'tabs', 'caption' => 'Tenants', 'path' => '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=tenants'),
	array('source' => 'tenant_hub', 'group' => 'tabs', 'caption' => 'DNS', 'path' => '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=dns'),
	array('source' => 'tenant_hub', 'group' => 'tabs', 'caption' => 'Guide', 'path' => '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=guide'),
	array('source' => 'tenant_hub', 'group' => 'tabs', 'caption' => 'Health', 'path' => '/' . $backend . '/shop/tenant_hub/tenant_hub?tab=health'),
	array('source' => 'tenant_hub', 'group' => 'actions', 'caption' => 'Failover guide', 'path' => '/' . $backend . '/control/portal/epc_platform_failover_guide'),
	array('source' => 'tenant_hub', 'group' => 'actions', 'caption' => 'ERP-only guide', 'path' => '/' . $backend . '/control/portal/epc_erp_only_onboard_guide'),
	array('source' => 'tenant_hub', 'group' => 'actions', 'caption' => 'API documentation guide', 'path' => '/' . $backend . '/control/portal/epc_api_documentation_guide'),
	array('source' => 'platform_erp', 'group' => 'erp', 'caption' => 'ERP shell', 'path' => '/' . $backend . '/platform-erp/shop/finance/erp?epc_erp_shell=1'),
	array('source' => 'marketing', 'group' => 'external', 'caption' => 'Marketing site', 'path' => 'https://www.ecomae.com/'),
);
$entries = array_merge($entries, $static);

$seen = array();
$results = array();
$brokenBefore = 0;

foreach ($entries as $entry) {
	$path = (string) $entry['path'];
	$dedupeKey = $path;
	if (strpos($path, 'tenant_hub/tenant_hub?tab=') === false) {
		$dedupeKey = preg_replace('#\?.*$#', '', $path);
	}
	if (isset($seen[$dedupeKey])) {
		continue;
	}
	$seen[$dedupeKey] = true;

	$fullUrl = (strpos($path, 'http') === 0) ? $path : ($baseHost . $path);
	$probe = epc_scla_probe_http($fullUrl);
	$page404 = ($probe['http'] === 404) || (($probe['http'] === 200 || $probe['http'] === 302) && epc_scla_body_has_404($fullUrl));

	$contentUrl = epc_scla_content_url_from_path($backend, $path);
	$cst = $pdo->prepare('SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$cst->execute(array($contentUrl));
	$crow = $cst->fetch(PDO::FETCH_ASSOC);

	$phpOk = null;
	$phpPath = '';
	if ($crow && !empty($crow['content'])) {
		$phpPath = str_replace('<backend_dir>', $backend, (string) $crow['content']);
		$phpOk = is_file($_SERVER['DOCUMENT_ROOT'] . $phpPath);
	}

	$broken = $page404 || $probe['http'] === 404 || $probe['http'] >= 500;

	if ($broken) {
		$brokenBefore++;
	}

	$results[] = array(
		'source' => $entry['source'],
		'group' => $entry['group'],
		'caption' => $entry['caption'],
		'menu_id' => $entry['menu_id'] ?? null,
		'path' => $path,
		'url' => $fullUrl,
		'http' => $probe['http'],
		'redirect' => $probe['redirect'],
		'content_id' => $crow ? (int) $crow['id'] : null,
		'published' => $crow ? (int) $crow['published_flag'] : null,
		'php_path' => $phpPath,
		'php_exists' => $phpOk,
		'page_not_available' => $page404,
		'broken' => $broken,
	);
}

if ($scope === 'super') {
	$results = array_values(array_filter($results, function (array $r) use ($superGroupKeys) {
		if (in_array($r['source'], array('topbar', 'tenant_hub', 'platform_erp', 'marketing'), true)) {
			return true;
		}
		return $r['source'] === 'sidebar' && in_array((string) $r['group'], $superGroupKeys, true);
	}));
	$brokenBefore = 0;
	foreach ($results as $r) {
		if (!empty($r['broken'])) {
			$brokenBefore++;
		}
	}
}

$summary = array(
	'timestamp' => gmdate('c'),
	'host' => $baseHost,
	'backend' => $backend,
	'db' => $cfg->db,
	'total_tested' => count($results),
	'broken_count' => $brokenBefore,
	'fixes_applied' => $fixesApplied,
	'results' => $results,
);

if ($format === 'json') {
	header('Content-Type: application/json; charset=utf-8');
	echo json_encode($summary, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

header('Content-Type: text/plain; charset=utf-8');
echo "=== Super CP link audit ===\n";
echo 'Time: ' . $summary['timestamp'] . "\n";
echo 'Total tested: ' . $summary['total_tested'] . "\n";
echo 'Broken: ' . $summary['broken_count'] . "\n\n";

if ($fixesApplied) {
	echo "=== Fixes applied ===\n";
	foreach ($fixesApplied as $fix) {
		echo ($fix['ok'] ? 'OK' : 'FAIL') . ' ' . $fix['script'] . "\n";
	}
	echo "\n";
}

foreach ($results as $r) {
	$flag = $r['broken'] ? ' BROKEN' : ' OK';
	echo sprintf(
		"[%s] %s %s\n  %s\n  http=%d redirect=%s content=%s pub=%s php=%s p404=%s%s\n\n",
		$r['source'],
		$r['group'],
		$r['caption'],
		$r['path'],
		$r['http'],
		$r['redirect'] !== '' ? $r['redirect'] : '-',
		$r['content_id'] !== null ? (string) $r['content_id'] : '-',
		$r['published'] !== null ? (string) $r['published'] : '-',
		$r['php_exists'] === null ? '-' : ($r['php_exists'] ? 'yes' : 'NO'),
		$r['page_not_available'] ? 'yes' : 'no',
		$flag
	);
}

echo "Done.\n";
