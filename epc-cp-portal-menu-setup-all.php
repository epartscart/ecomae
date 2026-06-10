<?php
/**
 * Portal CP menu — POS Terminal, Visual page editor, Social media hub on every live tenant DB.
 * Enables feature flags (pos, visual_page_editor) on platform registry.
 *
 * Dry-run:  https://www.ecomae.com/epc-cp-portal-menu-setup-all.php?token=epartscart-deploy-2026
 * Apply:    …&apply=1
 * One DB:   …&apply=1&db=docpart
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_integrations_helpers.php';
require_once __DIR__ . '/content/shop/pos/epc_pos_cp_install.php';
require_once __DIR__ . '/content/general_pages/epc_visual_page_editor.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$onlyDb = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? '')));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$backend = trim((string) $cfg->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}

$targets = array();
$targets[] = array(
	'label' => 'current_config',
	'site_key' => 'platform',
	'host' => function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? ''),
	'cred' => array('db' => $cfg->db, 'user' => $cfg->user, 'pass' => $cfg->password),
);

$platformPdo = epc_portal_platform_pdo();
if ($platformPdo instanceof PDO && function_exists('epc_portal_list_tenants')) {
	epc_portal_db_ensure($platformPdo);
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		if ((string) ($row['status'] ?? '') !== 'live') {
			continue;
		}
		$cred = epc_portal_tenant_setup_credentials($row);
		if ($cred['db'] === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $cred['db']),
			'site_key' => (string) ($row['site_key'] ?? ''),
			'host' => (string) ($row['hostname'] ?? ''),
			'cred' => array(
				'db' => $cred['db'],
				'user' => $cred['user'],
				'pass' => $cred['pass'],
			),
		);
	}
}

$seenDb = array();
$unique = array();
foreach ($targets as $t) {
	$db = (string) ($t['cred']['db'] ?? '');
	if ($db === '' || isset($seenDb[$db])) {
		continue;
	}
	if ($onlyDb !== '' && $db !== $onlyDb) {
		continue;
	}
	$seenDb[$db] = true;
	$unique[] = $t;
}

function epc_cp_portal_setup_connect(array $cred, DP_Config $cfg): ?PDO
{
	return epc_pos_setup_connect($cred, $cfg);
}

function epc_cp_portal_menu_rows(PDO $pdo): array
{
	$portalGroupId = epc_cp_mm_group_id($pdo, 'epc_cp_group_portal');
	if ($portalGroupId <= 0) {
		return array('portal_group' => 0, 'pos' => null, 'visual' => null, 'social' => null);
	}
	$st = $pdo->prepare(
		'SELECT `id`, `caption`, `url`, `items_group`, `show_anyway`
		 FROM `control_items`
		 WHERE `items_group` = ?
		 AND (`url` LIKE ? OR `url` LIKE ? OR `url` LIKE ? OR `caption` IN (?, ?, ?))'
	);
	$st->execute(array(
		$portalGroupId,
		'%/shop/pos/terminal',
		'%epc_visual_page_editor%',
		'%epc_social_media_hub%',
		'epc_pos_terminal_cp',
		'epc_visual_page_editor',
		'epc_social_media_hub_cp',
	));
	$pos = null;
	$visual = null;
	$social = null;
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$url = strtolower((string) ($row['url'] ?? ''));
		if (strpos($url, '/shop/pos/') !== false) {
			$pos = $row;
		}
		if (strpos($url, 'epc_visual_page_editor') !== false) {
			$visual = $row;
		}
		if (strpos($url, 'epc_social_media_hub') !== false) {
			$social = $row;
		}
	}
	return array(
		'portal_group' => $portalGroupId,
		'pos' => $pos,
		'visual' => $visual,
		'social' => $social,
	);
}

function epc_cp_portal_register_visual_content(PDO $pdo, string $backend): int
{
	$contentUrl = 'control/portal/epc_visual_page_editor';
	$phpPath = '/' . $backend . '/content/control/portal/epc_visual_page_editor.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		return 0;
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_visual_page_editor', 'epc_visual_page_editor', $parentId, $level, $contentId));
		return $contentId;
	}
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, \'epc_visual_page_editor\', \'epc_visual_page_editor\', ?, \'Visual page editor\', 0, \'php\', ?, \'epc_visual_page_editor\', \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 12)'
	)->execute(array($contentUrl, $level, $parentId, $phpPath, $now, $now));
	return (int) $pdo->lastInsertId();
}

echo "=== EPC Portal menu (POS + Visual editor + Social media hub) — all tenants ===\n";
echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'targets=' . count($unique) . "\n\n";

if ($apply && $platformPdo instanceof PDO) {
	epc_integrations_ensure_schema($platformPdo);
	$flags = array('pos' => true, 'visual_page_editor' => true);
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		$siteKey = trim((string) ($row['site_key'] ?? ''));
		if ($siteKey === '') {
			continue;
		}
		epc_integrations_save_feature_flags($platformPdo, $siteKey, $flags);
		echo "feature_flags enabled for {$siteKey}\n";
	}
	echo "\n";
}

foreach ($unique as $t) {
	$label = (string) $t['label'];
	$db = (string) $t['cred']['db'];
	$host = (string) $t['host'];
	echo "=== {$label} (db={$db}) ===\n";
	if ($host !== '') {
		echo "  pos_url=https://{$host}/{$backend}/shop/pos/terminal\n";
		echo "  vpe_url=https://{$host}/{$backend}/control/portal/epc_visual_page_editor\n";
	}

	$pdo = epc_cp_portal_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  ERROR: cannot connect\n\n";
		continue;
	}

	if (!$apply) {
		$rows = epc_cp_portal_menu_rows($pdo);
		echo '  portal_group=' . (int) $rows['portal_group'];
		echo ' pos=' . ($rows['pos'] ? 'yes' : 'NO');
		echo ' visual=' . ($rows['visual'] ? 'yes' : 'NO');
		echo ' social=' . ($rows['social'] ? 'yes' : 'NO') . "\n\n";
		continue;
	}

	try {
		epc_vpe_ensure_schema($pdo);
		$posResult = epc_pos_cp_install($pdo, $backend);
		$portalMenu = epc_cp_portal_menu_apply($pdo);
		$vpeContentId = epc_cp_portal_register_visual_content($pdo, $backend);
		$rows = epc_cp_portal_menu_rows($pdo);
		echo '  OK pos_content=' . (int) ($posResult['content_id'] ?? 0);
		echo ' portal_pos_item=' . (int) ($portalMenu['items']['pos_terminal'] ?? 0);
		echo ' portal_vpe_item=' . (int) ($portalMenu['items']['visual_editor'] ?? 0);
		echo ' portal_social_item=' . (int) ($portalMenu['items']['social_media_hub'] ?? 0);
		echo ' vpe_content=' . $vpeContentId;
		echo ' portal_pos_menu=' . ($rows['pos'] ? 'yes' : 'NO');
		echo ' portal_vpe_menu=' . ($rows['visual'] ? 'yes' : 'NO');
		echo ' portal_social_menu=' . ($rows['social'] ? 'yes' : 'NO') . "\n\n";
	} catch (Throwable $e) {
		echo '  FAIL: ' . $e->getMessage() . "\n\n";
	}
}

echo "Done.\n";
