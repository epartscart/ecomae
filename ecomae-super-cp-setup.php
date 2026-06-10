<?php
/**
 * Register Super CP: Platform → Tenant hub (cp.ecomae.com).
 * https://www.ecomae.com/ecomae-super-cp-setup.php?token=epartscart-deploy-2026
 */
if (!function_exists('ecomae_super_cp_is_entry')) {
	function ecomae_super_cp_is_entry(): bool
	{
		return realpath((string) ($_SERVER['SCRIPT_FILENAME'] ?? '')) === realpath(__FILE__);
	}
}

if (!function_exists('ecomae_super_cp_file_bundle')) {
	function ecomae_super_cp_file_bundle(): array
	{
		$path = __DIR__ . '/ecomae-super-cp-file-bundle.php';
		if (is_file($path)) {
			$bundle = require $path;
			if (is_array($bundle) && count($bundle) > 0) {
				return $bundle;
			}
		}
		return array();
	}
}

if (!function_exists('ecomae_fix_cp_delegate_index')) {
	function ecomae_fix_cp_delegate_index(string $root): array
	{
		$indexPath = rtrim($root, '/\\') . '/index.php';
		if (!is_file($indexPath)) {
			return array('ok' => false, 'message' => 'index.php missing');
		}
		$marker = 'Nginx try_files sends /cp/* subpaths here';
		$content = (string) file_get_contents($indexPath);
		if (strpos($content, $marker) !== false) {
			return array('ok' => true, 'message' => 'Already patched');
		}
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
		if (strpos($content, $needle) === false) {
			return array('ok' => false, 'message' => 'index.php anchor not found');
		}
		$newContent = str_replace($needle, $insert, $content);
		if ($newContent === $content || file_put_contents($indexPath, $newContent) === false) {
			return array('ok' => false, 'message' => 'Patch write failed');
		}
		return array('ok' => true, 'message' => 'index.php patched');
	}
}

if (!function_exists('ecomae_super_cp_materialize_files')) {
	function ecomae_super_cp_materialize_files(string $root): array
	{
		$written = array();
		$bundle = ecomae_super_cp_file_bundle();
		if (!is_array($bundle) || count($bundle) === 0) {
			return $written;
		}
		foreach ($bundle as $rel => $encoded) {
			$rel = str_replace('\\', '/', (string) $rel);
			if ($rel === '' || strpos($rel, '..') !== false) {
				continue;
			}
			$content = base64_decode((string) $encoded, true);
			if ($content === false || $content === '') {
				continue;
			}
			$dest = rtrim($root, '/\\') . '/' . $rel;
			$dir = dirname($dest);
			if (!is_dir($dir)) {
				mkdir($dir, 0755, true);
			}
			if (file_put_contents($dest, $content) !== false) {
				if (substr($rel, -4) === '.php' && strpos($rel, 'tenant_hub_main_page.php') !== false && substr(rtrim($content), -2) !== '?>') {
					file_put_contents($dest, rtrim($content) . "\n?>\n");
				}
				$written[] = $rel;
			}
		}
		return $written;
	}
}

if (!ecomae_super_cp_is_entry()) {
	return;
}

error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$cpHotfixOnly = !empty($_GET['cp_hotfix']);
if ($cpHotfixOnly && is_file(__DIR__ . '/ecomae-cp-hotfix-mini.php')) {
	require __DIR__ . '/ecomae-cp-hotfix-mini.php';
	exit;
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

epc_portal_db_ensure($pdo);
$materialized = ecomae_super_cp_materialize_files(__DIR__);
$cpDelegateFix = ecomae_fix_cp_delegate_index(__DIR__);

function epc_th_lang($pdo, $key, $en, $ru)
{
	try {
		$pdo->query('SELECT 1 FROM `lang_text_strings` LIMIT 1');
	} catch (Exception $e) {
		return;
	}
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_th_lang($pdo, 'epc_tenant_hub_cp', 'Tenant hub', 'Ð¦ÐµÐ½Ñ‚Ñ€ ÐºÐ»Ð¸ÐµÐ½Ñ‚Ð¾Ð²');
epc_th_lang($pdo, 'epc_cp_group_tenant_hub', 'Platform', 'ÐŸÐ»Ð°Ñ‚Ñ„Ð¾Ñ€Ð¼Ð°');

function epc_th_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 85)
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new Exception('Parent not found: ' . $parentUrl);
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$now = time();
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array($url, $level, $alias, $valueKey, $parentId, $title, $phpPath, $title, $now, $now, $order));
		$contentId = (int) $pdo->lastInsertId();
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = (int) $st->fetchColumn();
	$groups = array($root > 0 ? $root : 1);
	$collect = function ($pid) use ($pdo, &$collect, &$groups) {
		$ch = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($pid));
		while ($r = $ch->fetch(PDO::FETCH_ASSOC)) {
			$groups[] = (int) $r['id'];
			$collect((int) $r['id']);
		}
	};
	if ($root > 0) {
		$collect($root);
	}
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach (array_unique($groups) as $gid) {
		$ins->execute(array($contentId, (int) $gid));
	}
	return $contentId;
}

$hubId = epc_th_register_content(
	$pdo,
	'shop',
	'shop/tenant_hub',
	'tenant_hub',
	'epc_cp_group_tenant_hub',
	'/<backend_dir>/content/shop/tenant_hub/tenant_hub_hub_page.php',
	'Platform',
	84
);

$contentId = epc_th_register_content(
	$pdo,
	'shop/tenant_hub',
	'shop/tenant_hub/tenant_hub',
	'tenant_hub_main',
	'epc_tenant_hub_cp',
	'/<backend_dir>/content/shop/tenant_hub/tenant_hub_main_page.php',
	'Tenant hub',
	85
);

$menu = epc_cp_super_platform_menu_apply($pdo);

$wwwSettings = epc_portal_default_site_settings('www.ecomae.com');
$wwwSettings['host'] = 'www.ecomae.com';
epc_portal_save_site_settings($pdo, $wwwSettings);

$cpSettings = epc_portal_default_site_settings('cp.ecomae.com');
$cpSettings['host'] = 'cp.ecomae.com';
if (!in_array('super_platform', $cpSettings['enabled_packs'], true)) {
	$cpSettings['enabled_packs'][] = 'super_platform';
}
epc_portal_save_site_settings($pdo, $cpSettings);

$base = rtrim($cfg->domain_path, '/');

echo json_encode(array(
	'status' => true,
	'message' => 'ecomae Super CP tenant hub registered',
	'db' => $cfg->db,
	'materialized_files' => $materialized,
	'cp_delegate_fix' => $cpDelegateFix,
	'hub_content_id' => $hubId,
	'content_id' => $contentId,
	'menu_group_id' => $menu['tenant_hub_group'],
	'menu_item_id' => $menu['tenant_hub_item'],
	'hosts_seeded' => array('www.ecomae.com', 'cp.ecomae.com'),
	'urls' => array(
		'super_cp' => 'https://cp.ecomae.com/' . $cfg->backend_dir . '/shop/tenant_hub/tenant_hub',
		'marketing' => 'https://www.ecomae.com/',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
