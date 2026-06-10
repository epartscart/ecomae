<?php
/**
 * Visual Page Editor — register schema, routes, and menu on platform + every live tenant DB.
 *
 * Dry-run:  https://www.ecomae.com/epc-visual-editor-setup-all.php?token=epartscart-deploy-2026
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
		$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($row['db_name'] ?? '')));
		if ($dbName === '') {
			continue;
		}
		$targets[] = array(
			'label' => (string) ($row['site_key'] ?? $dbName),
			'host' => (string) ($row['hostname'] ?? ''),
			'cred' => array(
				'db' => $dbName,
				'user' => (string) ($row['db_user'] ?? ''),
				'pass' => (string) ($row['db_password'] ?? ''),
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

function epc_vpe_setup_connect(array $cred, DP_Config $cfg): ?PDO
{
	$db = (string) ($cred['db'] ?? '');
	$user = (string) ($cred['user'] ?? '');
	$pass = (string) ($cred['pass'] ?? '');
	if ($db === '') {
		return null;
	}
	if ($user === '') {
		$user = (string) $cfg->user;
	}
	if ($pass === '') {
		$pass = (string) $cfg->password;
	}
	$host = trim((string) $cfg->host);
	if ($host === '' || strtolower($host) === 'localhost') {
		$host = '127.0.0.1';
	}
	try {
		return new PDO(
			'mysql:host=' . $host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}

function epc_vpe_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_vpe_setup_register(PDO $pdo, string $backend): array
{
	epc_vpe_ensure_schema($pdo);
	epc_vpe_setup_lang($pdo, 'epc_visual_page_editor', 'Visual page editor', 'Визуальный редактор страниц');

	if (function_exists('epc_cp_super_cp_operator_menu_apply')) {
		$operatorGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_operator');
		if ($operatorGroup <= 0) {
			epc_cp_super_cp_operator_menu_apply($pdo);
		}
	}
	$portalMenu = function_exists('epc_cp_portal_menu_apply')
		? epc_cp_portal_menu_apply($pdo)
		: array('items' => array());
	$itemId = (int) ($portalMenu['items']['visual_editor'] ?? 0);
	if ($itemId <= 0) {
		$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
		$itemId = epc_cp_mm_ensure_item(
			$pdo,
			$portalGroup,
			'epc_visual_page_editor',
			'/' . $backend . '/control/portal/epc_visual_page_editor',
			16,
			'#7c3aed',
			'fas fa-magic',
			1
		);
	}

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		throw new RuntimeException('Parent content not found');
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$contentUrl = 'control/portal/epc_visual_page_editor';
	$phpPath = '/' . $backend . '/content/control/portal/epc_visual_page_editor.php';
	$now = time();

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_visual_page_editor', 'epc_visual_page_editor', $parentId, $level, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, \'epc_visual_page_editor\', \'epc_visual_page_editor\', ?, \'Visual page editor\', 0, \'php\', ?, \'epc_visual_page_editor\', \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 12)'
		)->execute(array($contentUrl, $level, $parentId, $phpPath, $now, $now));
		$contentId = (int) $pdo->lastInsertId();
	}

	return array(
		'menu_item' => $itemId,
		'portal_menu' => $portalMenu,
		'content_id' => $contentId,
	);
}

echo "=== EPC Visual Page Editor — all tenants ===\n";
echo 'mode=' . ($apply ? 'apply' : 'dry-run') . "\n";
echo 'targets=' . count($unique) . "\n\n";

foreach ($unique as $t) {
	$label = (string) $t['label'];
	$db = (string) $t['cred']['db'];
	$host = (string) $t['host'];
	echo "=== {$label} (db={$db}) ===\n";
	if ($host !== '') {
		echo "  vpe_url=https://{$host}/{$backend}/control/portal/epc_visual_page_editor\n";
	}
	$pdo = epc_vpe_setup_connect($t['cred'], $cfg);
	if (!$pdo instanceof PDO) {
		echo "  ERROR: cannot connect\n\n";
		continue;
	}
	if (!$apply) {
		try {
			epc_vpe_ensure_schema($pdo);
			$cnt = (int) $pdo->query('SELECT COUNT(*) FROM `epc_page_builder_layouts`')->fetchColumn();
			echo "  layouts={$cnt} (dry-run)\n\n";
		} catch (Throwable $e) {
			echo "  probe: " . $e->getMessage() . "\n\n";
		}
		continue;
	}
	try {
		$result = epc_vpe_setup_register($pdo, $backend);
		echo '  OK content_id=' . $result['content_id'] . ' menu_item=' . $result['menu_item'] . "\n\n";
	} catch (Throwable $e) {
		echo '  FAIL: ' . $e->getMessage() . "\n\n";
	}
}

echo "Done.\n";
