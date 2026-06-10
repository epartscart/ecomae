<?php
/**
 * Verify Portal CP sidebar has POS Terminal + Visual page editor on live tenants.
 *
 * GET https://www.ecomae.com/epc-cp-portal-menu-verify.php?token=epartscart-deploy-2026
 * GET …&site_key=epartscart
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	http_response_code(403);
	echo json_encode(array('ok' => false, 'error' => 'forbidden'), JSON_UNESCAPED_SLASHES);
	exit;
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_cp_menu.php';
require_once __DIR__ . '/content/general_pages/epc_integrations_helpers.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$filterKey = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
$expectedTenants = array('epartscart', 'electronicae', 'taxofinca', 'stylenlook', 'thejewellerytrend');

$platformPdo = epc_portal_platform_pdo();
$report = array(
	'ok' => true,
	'backend' => $backend,
	'paths' => array(
		'pos' => '/' . $backend . '/shop/pos/terminal',
		'visual_editor' => '/' . $backend . '/control/portal/epc_visual_page_editor',
	),
	'tenants' => array(),
);

function epc_cp_portal_verify_connect(array $cred, DP_Config $cfg): ?PDO
{
	$db = trim((string) ($cred['db'] ?? ''));
	if ($db === '') {
		return null;
	}
	$user = trim((string) ($cred['user'] ?? ''));
	if ($user === '') {
		$user = (string) $cfg->user;
	}
	$pass = (string) ($cred['pass'] ?? '');
	if ($pass === '') {
		$pass = (string) $cfg->password;
	}
	try {
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}

function epc_cp_portal_verify_tenant(PDO $pdo, string $backend, string $siteKey, string $host, ?PDO $platformPdo): array
{
	$portalGroupId = epc_cp_mm_group_id($pdo, 'epc_cp_group_portal');
	$posItem = null;
	$vpeItem = null;
	if ($portalGroupId > 0) {
		$st = $pdo->prepare(
			'SELECT `id`, `caption`, `url`, `items_group`, `show_anyway`
			 FROM `control_items`
			 WHERE `items_group` = ?
			 AND (`url` LIKE ? OR `url` LIKE ?)'
		);
		$st->execute(array($portalGroupId, '%/shop/pos/terminal', '%epc_visual_page_editor%'));
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$url = strtolower((string) ($row['url'] ?? ''));
			if (strpos($url, '/shop/pos/') !== false) {
				$posItem = $row;
			}
			if (strpos($url, 'epc_visual_page_editor') !== false) {
				$vpeItem = $row;
			}
		}
	}

	$content = array('pos' => null, 'visual_editor' => null);
	foreach (array('shop/pos/terminal', 'control/portal/epc_visual_page_editor') as $slug) {
		$key = strpos($slug, 'pos/') !== false ? 'pos' : 'visual_editor';
		$cst = $pdo->prepare('SELECT `id`, `published_flag` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$cst->execute(array($slug));
		$content[$key] = $cst->fetch(PDO::FETCH_ASSOC) ?: null;
	}

	$posUrl = '/' . $backend . '/shop/pos/terminal';
	$vpeUrl = '/' . $backend . '/control/portal/epc_visual_page_editor';
	$posVisible = epc_portal_cp_item_visible($posUrl);
	$vpeVisible = epc_portal_cp_item_visible($vpeUrl);
	$posFeature = epc_integrations_feature_enabled('pos', $siteKey !== '' ? $siteKey : null, $platformPdo);
	$vpeFeature = epc_integrations_feature_enabled('visual_page_editor', $siteKey !== '' ? $siteKey : null, $platformPdo);

	$posMenuBlocked = epc_integrations_menu_blocked_by_feature(str_replace($backend, '<backend>', $posUrl));
	$vpeMenuBlocked = epc_integrations_menu_blocked_by_feature(str_replace($backend, '<backend>', $vpeUrl));

	$ok = $posItem !== null && $vpeItem !== null
		&& !empty($content['pos']) && !empty($content['visual_editor'])
		&& $posVisible && $vpeVisible && $posFeature && $vpeFeature
		&& !$posMenuBlocked && !$vpeMenuBlocked;

	return array(
		'ok' => $ok,
		'hostname' => $host,
		'site_key' => $siteKey,
		'portal_group_id' => $portalGroupId,
		'menu' => array(
			'pos' => $posItem,
			'visual_editor' => $vpeItem,
		),
		'content_routes' => $content,
		'visibility' => array(
			'pos_pack_visible' => $posVisible,
			'visual_pack_visible' => $vpeVisible,
			'pos_feature_enabled' => $posFeature,
			'visual_feature_enabled' => $vpeFeature,
			'pos_menu_blocked' => $posMenuBlocked,
			'visual_menu_blocked' => $vpeMenuBlocked,
		),
		'urls' => array(
			'pos' => ($host !== '' ? 'https://' . $host : '') . $posUrl,
			'visual_editor' => ($host !== '' ? 'https://' . $host : '') . $vpeUrl,
		),
	);
}

if (!$platformPdo instanceof PDO) {
	$report['ok'] = false;
	$report['error'] = 'platform_db_unavailable';
	echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
	exit;
}

epc_portal_db_ensure($platformPdo);
$rows = function_exists('epc_portal_list_tenants') ? epc_portal_list_tenants($platformPdo) : array();

foreach ($rows as $row) {
	if ((string) ($row['status'] ?? '') !== 'live') {
		continue;
	}
	$siteKey = (string) ($row['site_key'] ?? '');
	if ($filterKey !== '' && $siteKey !== $filterKey) {
		continue;
	}
	if ($filterKey === '' && !in_array($siteKey, $expectedTenants, true)) {
		continue;
	}
	$cred = epc_portal_tenant_setup_credentials($row);
	$pdo = epc_cp_portal_verify_connect($cred, $cfg);
	if (!$pdo instanceof PDO) {
		$report['tenants'][$siteKey] = array('ok' => false, 'error' => 'db_connect_failed', 'hostname' => (string) ($row['hostname'] ?? ''));
		$report['ok'] = false;
		continue;
	}
	$entry = epc_cp_portal_verify_tenant(
		$pdo,
		$backend,
		$siteKey,
		(string) ($row['hostname'] ?? ''),
		$platformPdo
	);
	$report['tenants'][$siteKey] = $entry;
	if (!$entry['ok']) {
		$report['ok'] = false;
	}
}

echo json_encode($report, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
