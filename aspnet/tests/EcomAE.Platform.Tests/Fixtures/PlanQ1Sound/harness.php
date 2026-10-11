<?php
// PHP 8.3 goldens for plan Q1-sound (CP guideline). Leftover unique user helper stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['SOUND_DBS'] = array();

function sound_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/users\/dp_user\.php[\'"]\s*;/', '// leftover user injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/[\'"]\s*\.\s*\$DP_Config->backend_dir\s*\.\s*[\'"]\/content\/control\/control_helper\.php[\'"]\s*;/', '// helper injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_cp_page_frame\.php[\'"]\s*;/', '// frame injected', $code);
	$code = str_replace("date('Y-m-d')", "(\$GLOBALS['SOUND_DATE'] ?? '2026-10-10')", $code);
	$code = preg_replace(
		'/if \(!isset\(\$user_session\) \|\| !is_array\(\$user_session\)\) \{.*?return;\n\}\n\n/s',
		"function epc_cpg_require_session()\n{\n\t\$user_session = \$GLOBALS['user_session'] ?? null;\n\tif (!isset(\$user_session) || !is_array(\$user_session)) {\n\t\t\$user_session = DP_User::getAdminSession();\n\t\t\$GLOBALS['user_session'] = \$user_session;\n\t}\n\tif (empty(\$user_session) || !is_array(\$user_session)) {\n\t\t\$epc_cp_login = '/' . htmlspecialchars(\$GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8') . '/';\n\t\techo '<div class=\"alert alert-warning\">Please <a href=\"' . \$epc_cp_login\n\t\t\t. '\">log in to the control panel</a> to view this guide.</div>';\n\t\treturn false;\n\t}\n\treturn true;\n}\n\n",
		$code
	);
	$code = preg_replace(
		'/^\$backend = \$DP_Config->backend_dir;/m',
		"function epc_cpg_render_page()\n{\n\tglobal \$DP_Config, \$db_link;\n\t\$backend = \$DP_Config->backend_dir;",
		$code
	);
	$code = preg_replace(
		'/epc_cp_page_frame_close\(\);\s*\?>\s*$/',
		"epc_cp_page_frame_close();\n}\n",
		$code
	);
	file_put_contents($dest, $code);
}

function sound_pdo(string $suffix, array $groups, array $items): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_sound_' . $suffix . '_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['SOUND_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `control_groups` (`id` INT NOT NULL, `caption` VARCHAR(255) NOT NULL, `order` INT NOT NULL)');
	$pdo->exec('CREATE TABLE `control_items` (`id` INT NOT NULL, `items_group` INT NOT NULL, `caption` VARCHAR(255) NOT NULL, `url` VARCHAR(255) NOT NULL, `order` INT NOT NULL, `fontawesome_class` VARCHAR(64) NOT NULL, `show_anyway` INT NOT NULL)');
	$g = $pdo->prepare('INSERT INTO `control_groups` (`id`,`caption`,`order`) VALUES (?,?,?)');
	foreach ($groups as $row) {
		$g->execute(array($row[0], $row[1], $row[2]));
	}
	$i = $pdo->prepare('INSERT INTO `control_items` (`id`,`items_group`,`caption`,`url`,`order`,`fontawesome_class`,`show_anyway`) VALUES (?,?,?,?,?,?,?)');
	foreach ($items as $row) {
		$i->execute($row);
	}
	return $pdo;
}

function sound_drop(): void
{
	if (empty($GLOBALS['SOUND_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['SOUND_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['SOUND_DBS'] = array();
}

function sound_stubs(): void
{
	if (!function_exists('is_anable')) {
		function is_anable($item): bool
		{
			$id = (int) ($item['id'] ?? 0);
			return empty($GLOBALS['SOUND_DENY_IDS'][$id]);
		}
	}
	if (!function_exists('translate_str_by_key')) {
		function translate_str_by_key($key)
		{
			$map = $GLOBALS['SOUND_KEYS'] ?? array();
			return $map[(string) $key] ?? (string) $key;
		}
	}
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			$map = $GLOBALS['SOUND_IDS'] ?? array();
			return $map[(int) $id] ?? (string) $id;
		}
	}
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['SOUND_SUPER']);
		}
	}
	if (!function_exists('epc_cp_page_frame_open')) {
		function epc_cp_page_frame_open(array $opts = array()): void
		{
			$hero = isset($opts['hero']) && is_array($opts['hero']) ? $opts['hero'] : array();
			echo 'FRAME_OPEN:' . (string) ($opts['class'] ?? '') . ':' . (string) ($hero['badge'] ?? '');
		}
	}
	if (!function_exists('epc_cp_page_frame_close')) {
		function epc_cp_page_frame_close(): void
		{
			echo 'FRAME_CLOSE';
		}
	}
	if (!class_exists('DP_User', false)) {
		class DP_User
		{
			public static function getAdminSession()
			{
				return $GLOBALS['SOUND_LEFTOVER_SESSION'] ?? array();
			}
		}
	}
}

function sound_include(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	sound_stubs();
	if (array_key_exists('user_session', $GLOBALS)) {
		$user_session = $GLOBALS['user_session'];
	}
	$DP_Config = $GLOBALS['DP_Config'];
	$db_link = $GLOBALS['db_link'];
	require_once $GLOBALS['SOUND_PAGE'];
}

function sound_capture(callable $fn): string
{
	ob_start();
	$fn();
	return (string) ob_get_clean();
}

function sound_reset(): void
{
	$GLOBALS['SOUND_LEFTOVER_SESSION'] = array();
	$GLOBALS['SOUND_DENY_IDS'] = array();
	$GLOBALS['SOUND_KEYS'] = array('price_mgmt' => "O'Reilly prices", 'SHOP' => 'Shop desk');
	$GLOBALS['SOUND_IDS'] = array(2070 => 'Orders desk');
	$GLOBALS['SOUND_SUPER'] = 0;
	$GLOBALS['SOUND_DATE'] = '2026-10-10';
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => 'cp');
	$GLOBALS['db_link'] = null;
	unset($GLOBALS['user_session']);
}

function sound_tabs_dump(array $tabs): array
{
	$out = array();
	foreach ($tabs as $gid => $tab) {
		$items = array();
		foreach ($tab['items'] as $item) {
			$items[] = array(
				'id' => (int) $item['id'],
				'caption' => epc_cpg_item_label($item['caption']),
				'url' => (string) $item['url'],
				'hint' => epc_cpg_hint_for_url((string) $item['url'], epc_cpg_page_hints()),
				'icon' => (string) ($item['fontawesome_class'] ?? ''),
			);
		}
		$out[] = array(
			'gid' => (string) $gid,
			'caption' => (string) $tab['caption'],
			'count' => count($items),
			'items' => $items,
		);
	}
	return $out;
}

function sound_run_gates(): array
{
	sound_reset();
	sound_include();
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => 'cp');
	unset($GLOBALS['user_session']);
	$GLOBALS['SOUND_LEFTOVER_SESSION'] = array();
	$cp = sound_capture(static function () {
		epc_cpg_require_session();
	});
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => "O'Reilly");
	unset($GLOBALS['user_session']);
	$quoted = sound_capture(static function () {
		epc_cpg_require_session();
	});
	$GLOBALS['user_session'] = array('user_id' => 21);
	$ok = sound_capture(static function () {
		epc_cpg_require_session();
	});
	return array($cp, $quoted, $ok);
}

function sound_run_helpers(): array
{
	sound_reset();
	$GLOBALS['user_session'] = array('user_id' => 21);
	sound_include();
	$hints = epc_cpg_page_hints();
	return array(
		count($hints),
		array_key_exists('/shop/price-management', $hints) ? $hints['/shop/price-management'] : '',
		epc_cpg_hint_for_url('/cp/shop/price-management?x=1', $hints),
		epc_cpg_hint_for_url('/cp/unknown', $hints),
		epc_cpg_item_label('price_mgmt'),
		epc_cpg_item_label(' 2070'),
		epc_cpg_item_label("O'Reilly"),
		epc_cpg_h("O'Reilly <x>"),
	);
}

function sound_seed_menu(): PDO
{
	return sound_pdo('menu', array(
		array(1, 'SHOP', 10),
		array(2, 'USERS', 20),
	), array(
		array(11, 1, 'price_mgmt', '/<backend>/shop/price-management', 1, 'fa-tags', 0),
		array(12, 1, 'hidden_row', '/<backend>/shop/hidden', 2, 'fa-ban', 0),
		array(13, 1, 'forced_row', '/<backend>/shop/orders/orders', 3, 'fa-list', 1),
		array(14, 9, "O'Reilly extra", '/<backend>/users/customer_mgmt', 4, 'fa-users', 0),
	));
}

function sound_run_menu(): array
{
	sound_reset();
	$GLOBALS['user_session'] = array('user_id' => 21);
	$GLOBALS['SOUND_DENY_IDS'] = array(12 => 1, 13 => 1);
	$GLOBALS['db_link'] = sound_seed_menu();
	sound_include();
	$tabs = epc_cpg_load_menu_tabs($GLOBALS['db_link'], 'cp');
	return sound_tabs_dump($tabs);
}

function sound_run_page(): array
{
	sound_reset();
	$GLOBALS['user_session'] = array('user_id' => 21);
	$GLOBALS['SOUND_SUPER'] = 1;
	$GLOBALS['SOUND_KEYS'] = array('price_mgmt' => 'Acme prices');
	$GLOBALS['SOUND_DENY_IDS'] = array();
	$GLOBALS['db_link'] = sound_pdo('acme', array(
		array(1, 'Acme Shop', 1),
	), array(
		array(21, 1, 'price_mgmt', '/<backend>/shop/price-management', 1, 'fa-tags', 0),
	));
	sound_include();
	$acme = sound_capture(static function () {
		epc_cpg_render_page();
	});
	$GLOBALS['SOUND_SUPER'] = 0;
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => 'beta');
	$GLOBALS['SOUND_KEYS'] = array();
	$GLOBALS['db_link'] = sound_pdo('beta', array(
		array(1, 'Beta Users', 1),
	), array(
		array(31, 1, 'Other page', '/<backend>/users/other', 1, 'fa-user', 0),
	));
	$beta = sound_capture(static function () {
		epc_cpg_render_page();
	});
	return array($acme, $beta);
}

register_shutdown_function('sound_drop');

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_sound_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	sound_patch($root . '/cp/content/control/cp_guideline.php', $tmp . '/page.php');
	$GLOBALS['SOUND_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	sound_reset();
	$fn = 'sound_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		sound_drop();
		exit(1);
	}
	sound_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1sound_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1sound_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
