<?php
/**
 * Auto Price AI — CP menu + shell verification for all live tenants.
 * GET ?token=…&site_key=epartscart  (omit site_key for all)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
require_once __DIR__ . '/content/general_pages/epc_integrations_helpers.php';
require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_cp_install.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) ($cfg->backend_dir ?? 'cp'), '/');
if ($backend === '') {
	$backend = 'cp';
}

$onlySite = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));

$tenantHosts = array(
	'ecomae' => 'www.ecomae.com',
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);

$docroots = array(
	'ecomae' => '/home/ecomae/htdocs/www.ecomae.com/',
	'epartscart' => '/home/epartscart/htdocs/www.epartscart.com/',
	'electronicae' => '/home/electronicae/htdocs/www.electronicae.com/',
	'taxofinca' => '/home/taxofinca/htdocs/www.taxofinca.com/',
	'stylenlook' => '/home/stylenlook/htdocs/www.stylenlook.com/',
	'thejewellerytrend' => '/home/thejewellerytrend/htdocs/www.thejewellerytrend.com/',
);

function epc_apai_tv_curl(string $url, string $host): array
{
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_TIMEOUT => 20,
		CURLOPT_HTTPHEADER => array('Host: ' . $host),
	));
	$body = (string) curl_exec($ch);
	$out = array(
		'http' => (int) curl_getinfo($ch, CURLINFO_HTTP_CODE),
		'bytes' => strlen($body),
		'has_menu_label' => stripos($body, 'Auto Price AI') !== false || stripos($body, 'epc_portal_auto_price_engine') !== false,
		'has_hero' => stripos($body, 'Auto Price AI') !== false,
		'has_shell' => stripos($body, 'epc-apai-tab-body') !== false || stripos($body, 'epc-ape-panel--shell') !== false,
		'login_page' => stripos($body, 'Log in form') !== false,
	);
	curl_close($ch);
	return $out;
}

function epc_apai_tv_menu_check(PDO $pdo, string $backend): array
{
	$menuUrl = '/<backend>/control/portal/epc_auto_price_engine';
	$st = $pdo->prepare('SELECT `id`, `items_group`, `caption`, `url`, `show_anyway`, `order` FROM `control_items` WHERE `url` = ? OR `url` LIKE ? LIMIT 1');
	$st->execute(array($menuUrl, $menuUrl . '?%'));
	$item = $st->fetch(PDO::FETCH_ASSOC) ?: null;

	$portalGroupId = 0;
	$portalGroupKey = '';
	$gst = $pdo->prepare('SELECT `id`, `caption` FROM `control_groups` WHERE `caption` = ? LIMIT 1');
	$gst->execute(array('epc_cp_group_portal'));
	$gRow = $gst->fetch(PDO::FETCH_ASSOC);
	if ($gRow) {
		$portalGroupId = (int) $gRow['id'];
		$portalGroupKey = (string) $gRow['caption'];
	}

	$content = null;
	$cst = $pdo->prepare('SELECT `id`, `url`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$cst->execute(array('control/portal/epc_auto_price_engine'));
	$content = $cst->fetch(PDO::FETCH_ASSOC) ?: null;

	$menuVisible = false;
	$featureBlocked = false;
	if ($item) {
		$item['url'] = str_replace('<backend>', $backend, (string) $item['url']);
		$menuVisible = function_exists('epc_portal_cp_item_visible_enhanced')
			? epc_portal_cp_item_visible_enhanced($item)
			: true;
		$featureBlocked = function_exists('epc_integrations_menu_blocked_by_feature')
			? epc_integrations_menu_blocked_by_feature((string) $item['url'])
			: false;
	}

	return array(
		'menu_item' => $item ? array(
			'id' => (int) $item['id'],
			'group_id' => (int) $item['items_group'],
			'caption' => (string) $item['caption'],
			'url' => (string) $item['url'],
			'show_anyway' => (int) ($item['show_anyway'] ?? 0),
			'in_portal_group' => $portalGroupId > 0 && (int) $item['items_group'] === $portalGroupId,
		) : null,
		'portal_group' => array('id' => $portalGroupId, 'caption' => $portalGroupKey),
		'content' => $content ? array(
			'id' => (int) $content['id'],
			'published' => (int) ($content['published_flag'] ?? 0),
			'php_path' => (string) ($content['content'] ?? ''),
		) : null,
		'menu_visible_enhanced' => $menuVisible,
		'feature_blocked' => $featureBlocked,
	);
}

$platformPdo = epc_portal_platform_pdo();
if (!$platformPdo instanceof PDO) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'platform_db_unavailable')));
}

epc_portal_db_ensure($platformPdo);
epc_integrations_ensure_schema($platformPdo);

$out = array('ok' => true, 'tenants' => array(), 'summary' => array('pass' => 0, 'fail' => 0));

$targets = array();
foreach (epc_portal_list_tenants($platformPdo) as $row) {
	if ((string) ($row['status'] ?? '') !== 'live') {
		continue;
	}
	$sk = (string) ($row['site_key'] ?? '');
	if ($onlySite !== '' && $sk !== $onlySite) {
		continue;
	}
	$targets[] = $row;
}
if ($onlySite === 'ecomae' || ($onlySite === '' && !count($targets))) {
	$targets[] = array('site_key' => 'ecomae', 'hostname' => 'www.ecomae.com', 'db_name' => $cfg->db, 'db_user' => $cfg->user, 'db_password' => $cfg->password, 'status' => 'live');
}

foreach ($targets as $row) {
	$siteKey = (string) ($row['site_key'] ?? '');
	$host = (string) ($row['hostname'] ?? ($tenantHosts[$siteKey] ?? ''));
	if ($host === '') {
		$host = $tenantHosts[$siteKey] ?? ('www.' . $siteKey . '.com');
	}

	$pdo = null;
	if ($siteKey === 'ecomae' || $siteKey === 'platform') {
		try {
			$pdo = new PDO(
				'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
				$cfg->user,
				$cfg->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		} catch (Throwable $e) {
			$pdo = null;
		}
	} else {
		$pdo = epc_auto_price_setup_connect(array(
			'db' => (string) ($row['db_name'] ?? ''),
			'user' => (string) ($row['db_user'] ?? ''),
			'pass' => (string) ($row['db_password'] ?? ''),
		), $cfg);
	}

	$rootKey = $siteKey === 'platform' ? 'ecomae' : $siteKey;
	$root = $docroots[$rootKey] ?? $docroots['ecomae'];
	$stubRel = $backend . '/content/control/portal/epc_auto_price_engine.php';
	$stubPath = $root . $stubRel;
	$sharedStub = $docroots['ecomae'] . $stubRel;
	$stubExists = is_file($stubPath) || is_file($sharedStub);
	$stubBytes = 0;
	if (is_file($stubPath)) {
		$stubBytes = (int) filesize($stubPath);
	} elseif (is_file($sharedStub)) {
		$stubBytes = (int) filesize($sharedStub);
		$stubPath = $sharedStub;
	}

	$menu = $pdo instanceof PDO ? epc_apai_tv_menu_check($pdo, $backend) : array('error' => 'db_connect_failed');
	$featureEnabled = epc_integrations_feature_enabled('auto_price_ai', $siteKey, $platformPdo);

	$cpUrl = 'https://' . $host . '/' . $backend . '/control/portal/epc_auto_price_engine?tab=discover';
	if ($siteKey !== 'ecomae' && $siteKey !== 'platform') {
		$cpUrl .= '&site_key=' . rawurlencode($siteKey);
	}
	$fetch = epc_apai_tv_curl($cpUrl, $host);

	$pass = $pdo instanceof PDO
		&& !empty($menu['menu_item'])
		&& !empty($menu['content'])
		&& (int) ($menu['content']['published'] ?? 0) === 1
		&& !empty($menu['menu_visible_enhanced'])
		&& empty($menu['feature_blocked'])
		&& $featureEnabled
		&& $stubExists
		&& $fetch['http'] === 200;

	$out['tenants'][$siteKey] = array(
		'host' => $host,
		'db' => (string) ($row['db_name'] ?? $cfg->db),
		'cp_url' => $cpUrl,
		'pass' => $pass,
		'feature_auto_price_ai' => $featureEnabled,
		'menu' => $menu,
		'files' => array(
			'docroot' => $root,
			'shared_docroot' => $docroots['ecomae'],
			'stub' => array('path' => $stubPath, 'exists' => $stubExists, 'bytes' => $stubBytes),
		),
		'cp_fetch' => $fetch,
	);
	$pass ? $out['summary']['pass']++ : $out['summary']['fail']++;
}

$out['ok'] = $out['summary']['fail'] === 0;
echo json_encode($out, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
