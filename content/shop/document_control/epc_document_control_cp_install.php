<?php
/**
 * Document Control — CP content routes, menu, schema (per MySQL database).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_document_control_schema.php';

function epc_document_control_cp_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_document_control_cp_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 88)
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

function epc_document_control_cp_file_checks($docRoot, $backendDir = 'cp')
{
	$backendDir = trim((string) $backendDir, '/');
	if ($backendDir === '') {
		$backendDir = 'cp';
	}
	$root = rtrim((string) $docRoot, '/\\');
	$paths = array(
		'main_page' => $root . '/' . $backendDir . '/content/shop/document_control/document_control_main_page.php',
		'main' => $root . '/' . $backendDir . '/content/shop/document_control/document_control_main.php',
		'hub_page' => $root . '/' . $backendDir . '/content/shop/document_control/document_control_hub_page.php',
		'ajax' => $root . '/' . $backendDir . '/content/shop/document_control/ajax_document_control_endpoint.php',
		'ajax_handler' => $root . '/' . $backendDir . '/content/shop/document_control/ajax_document_control.php',
		'css' => $root . '/' . $backendDir . '/content/shop/document_control/epc_document_control.css',
		'js' => $root . '/' . $backendDir . '/content/shop/document_control/epc_document_control.js',
		'js_config' => $root . '/' . $backendDir . '/content/shop/document_control/epc_document_control_config.php',
		'css_proxy' => $root . '/content/general_pages/epc_document_control_cp_css.php',
		'page_assets' => $root . '/content/general_pages/epc_cp_page_assets.php',
		'helpers' => $root . '/content/shop/document_control/epc_document_control_helpers.php',
		'schema' => $root . '/content/shop/document_control/epc_document_control_schema.php',
		'print' => $root . '/content/shop/document_control/service/print.php',
		'legacy_redirect' => $root . '/' . $backendDir . '/content/shop/print_docs/print_docs_redirect_page.php',
	);
	$out = array();
	foreach ($paths as $key => $path) {
		$out[$key] = is_file($path);
	}
	return $out;
}

function epc_document_control_cp_probe(PDO $pdo, $docRoot, $backendDir = 'cp')
{
	$urls = array('shop/document_control', 'shop/document_control/document_control', 'shop/modul-pechati-dokumentov');
	$content = array();
	foreach ($urls as $url) {
		$st = $pdo->prepare('SELECT `id`, `published_flag`, `content_type`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$st->execute(array($url));
		$content[$url] = $st->fetch(PDO::FETCH_ASSOC) ?: null;
	}
	$menu = array();
	$st = $pdo->query(
		"SELECT ci.`id`, ci.`url`, ci.`items_group`, cg.`caption` AS group_caption
		 FROM `control_items` ci
		 LEFT JOIN `control_groups` cg ON cg.`id` = ci.`items_group`
		 WHERE ci.`url` LIKE '%document_control%' OR ci.`url` LIKE '%modul-pechati%'
		 ORDER BY ci.`id` ASC"
	);
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$menu[] = $row;
	}
	$tables = array();
	foreach (array('epc_document_company', 'epc_document_templates', 'epc_document_attachments') as $tbl) {
		try {
			$tables[$tbl] = (int) $pdo->query("SELECT COUNT(*) FROM `{$tbl}`")->fetchColumn() >= 0;
		} catch (Exception $e) {
			$tables[$tbl] = false;
		}
	}
	return array(
		'content' => $content,
		'menu' => $menu,
		'tables' => $tables,
		'files' => epc_document_control_cp_file_checks($docRoot, $backendDir),
	);
}

/**
 * @return array{hub_content_id:int,content_id:int,legacy_redirect_id:int,menu:array}
 */
function epc_document_control_cp_install(PDO $pdo, $backendDir = 'cp')
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';

	epc_document_control_cp_lang($pdo, 'epc_document_control_cp', 'Document Control', 'Управление документами');
	epc_document_control_cp_lang($pdo, 'epc_cp_group_documents', 'Documents', 'Документы');
	epc_doc_control_ensure_schema($pdo);

	$hubId = epc_document_control_cp_register_content(
		$pdo,
		'shop',
		'shop/document_control',
		'document_control_hub',
		'epc_cp_group_documents',
		'/<backend_dir>/content/shop/document_control/document_control_hub_page.php',
		'Documents',
		87
	);

	$contentId = epc_document_control_cp_register_content(
		$pdo,
		'shop/document_control',
		'shop/document_control/document_control',
		'document_control',
		'epc_document_control_cp',
		'/<backend_dir>/content/shop/document_control/document_control_main_page.php',
		'Document Control',
		88
	);

	$legacy = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$legacy->execute(array('shop/modul-pechati-dokumentov'));
	$legacyId = (int) $legacy->fetchColumn();
	if ($legacyId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `description` = ? WHERE `id` = ?'
		)->execute(array(
			'/<backend_dir>/content/shop/print_docs/print_docs_redirect_page.php',
			'Document Control (legacy redirect)',
			'Redirects legacy Russian print module to English Document Control',
			$legacyId,
		));
	}

	$menu = epc_cp_document_control_menu_apply($pdo);

	return array(
		'hub_content_id' => $hubId,
		'content_id' => $contentId,
		'legacy_redirect_id' => $legacyId,
		'menu' => $menu,
	);
}

function epc_document_control_cp_public_url($host = null, $backendDir = 'cp')
{
	$backendDir = trim((string) $backendDir, '/');
	if ($backendDir === '') {
		$backendDir = 'cp';
	}
	if ($host === null || $host === '') {
		$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? 'localhost');
	}
	$host = strtolower(trim($host));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	$scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
	return $scheme . '://' . $host . '/' . $backendDir . '/shop/document_control/document_control';
}

function epc_document_control_cp_url($tab = '')
{
	global $DP_Config;
	$backend = isset($DP_Config) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	$relative = 'shop/document_control/document_control';
	if ($tab !== '') {
		$relative .= (strpos($relative, '?') !== false ? '&' : '?') . 'tab=' . rawurlencode((string) $tab);
	}

	$prefix = '/' . $backend . '/';
	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active() && function_exists('epc_platform_erp_path_prefix')) {
		$prefix = epc_platform_erp_path_prefix();
	} elseif (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()
		&& function_exists('epc_client_erp_path_prefix') && function_exists('epc_client_erp_site_key')) {
		$siteKey = epc_client_erp_site_key();
		if ($siteKey !== '') {
			$prefix = epc_client_erp_path_prefix() . $siteKey . '/';
		}
	} elseif (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()
		&& function_exists('epc_portal_demo_cp_tenant_base')) {
		$prefix = rtrim(epc_portal_demo_cp_tenant_base(), '/') . '/';
	}

	return rtrim($prefix, '/') . '/' . $relative;
}

function epc_document_control_cp_ajax_url()
{
	global $DP_Config;
	$backend = isset($DP_Config) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	$relative = 'content/shop/document_control/ajax_document_control_endpoint.php';
	$prefix = '/' . $backend . '/';

	if (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active() && function_exists('epc_platform_erp_path_prefix')) {
		$prefix = epc_platform_erp_path_prefix();
	} elseif (function_exists('epc_client_erp_is_active') && epc_client_erp_is_active()
		&& function_exists('epc_client_erp_path_prefix') && function_exists('epc_client_erp_site_key')) {
		$siteKey = epc_client_erp_site_key();
		if ($siteKey !== '') {
			$prefix = epc_client_erp_path_prefix() . $siteKey . '/';
		}
	} elseif (function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context()
		&& function_exists('epc_portal_demo_cp_tenant_base')) {
		$prefix = rtrim(epc_portal_demo_cp_tenant_base(), '/') . '/';
	}

	return rtrim($prefix, '/') . '/' . $relative;
}

/**
 * Rebind $db_link when Model C CP cached PDO points at the wrong tenant database.
 */
function epc_dc_ensure_db_link()
{
	global $db_link;
	$DP_Config = (isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config']))
		? $GLOBALS['DP_Config']
		: null;
	if (!$DP_Config instanceof DP_Config) {
		return null;
	}
	$expectedDb = trim((string) ($DP_Config->db ?? ''));
	if ($expectedDb === '') {
		return isset($db_link) && $db_link instanceof PDO ? $db_link : null;
	}
	if (isset($db_link) && $db_link instanceof PDO) {
		try {
			$linkedDb = strtolower(trim((string) $db_link->query('SELECT DATABASE()')->fetchColumn()));
			if ($linkedDb !== '' && strcasecmp($linkedDb, $expectedDb) !== 0) {
				$db_link = null;
				unset($GLOBALS['epc_db_link']);
			}
		} catch (Throwable $e) {
			$db_link = null;
		}
	}
	if (!isset($db_link) || !($db_link instanceof PDO)) {
		try {
			$db_link = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$db_link->query('SET NAMES utf8;');
			$GLOBALS['epc_db_link'] = $db_link;
		} catch (Throwable $e) {
			echo '<div class="alert alert-danger">Database connection failed: ' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
			return null;
		}
	}
	return $db_link;
}
