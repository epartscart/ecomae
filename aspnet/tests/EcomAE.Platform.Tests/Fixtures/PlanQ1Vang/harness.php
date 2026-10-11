<?php
// PHP 8.3 goldens for plan Q1-vang (POS CP install). Leftover POS helper parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function vang_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function vang_open(string $schema): array
{
	list($user, $pass, $host, $port) = vang_dsn();
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE lang_text_strings (
		str_key VARCHAR(64) PRIMARY KEY, description TEXT, same VARCHAR(8), is_error INT, is_custom INT, used_found INT
	)');
	$db->exec('CREATE TABLE lang_text_strings_translation (
		str_key VARCHAR(64), lang_code VARCHAR(8), value TEXT,
		PRIMARY KEY (str_key, lang_code)
	)');
	$db->exec('CREATE TABLE content (
		id INT AUTO_INCREMENT PRIMARY KEY, count INT, url VARCHAR(128), level INT, alias VARCHAR(64), value VARCHAR(64),
		parent INT, description VARCHAR(255), is_frontend INT, content_type VARCHAR(16), content VARCHAR(255),
		title_tag VARCHAR(128), description_tag VARCHAR(16), keywords_tag VARCHAR(16), author_tag VARCHAR(16),
		main_flag INT, modules_array TEXT, css_js TEXT, robots_tag VARCHAR(16),
		system_flag INT, published_flag INT, open INT, time_created INT, time_edited INT, `order` INT
	)');
	$db->exec('CREATE TABLE content_access (content_id INT, group_id INT, PRIMARY KEY (content_id, group_id))');
	$db->exec('CREATE TABLE groups (id INT PRIMARY KEY, parent INT, for_backend INT)');
	return array($admin, $db);
}

function vang_run_lang()
{
	$schema = 'ecomae_cpw_vang_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = vang_open($schema);
	try {
		epc_pos_cp_lang($db, 'epc_pos_terminal_cp', 'POS Terminal', 'Касса POS');
		epc_pos_cp_lang($db, 'epc_pos_terminal_cp', "POS O'term", 'Касса');
		$keys = $db->query('SELECT str_key, description FROM lang_text_strings ORDER BY str_key')->fetchAll(PDO::FETCH_ASSOC);
		$tr = $db->query('SELECT str_key, lang_code, value FROM lang_text_strings_translation ORDER BY str_key, lang_code')->fetchAll(PDO::FETCH_ASSOC);
		return array($keys, $tr);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function vang_run_register()
{
	$schema = 'ecomae_cpw_vang_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = vang_open($schema);
	try {
		$db->exec("INSERT INTO groups VALUES (1, 0, 1), (2, 1, 1), (3, 2, 1)");
		$db->exec("INSERT INTO content (id, url, level, is_frontend, published_flag) VALUES (10, 'shop', 1, 0, 1)");
		$first = epc_pos_cp_register_content($db, 'shop', 'shop/pos', 'pos_folder', 'epc_cp_group_pos', '/cp/pos.php', 'Point of Sale', 86);
		$again = epc_pos_cp_register_content($db, 'shop', 'shop/pos', 'pos_folder', 'epc_cp_group_pos', '/cp/pos2.php', "POS O'hub", 86);
		$missing = 0;
		try {
			epc_pos_cp_register_content($db, 'nope', 'shop/x', 'x', 'k', '/x.php', 'X');
		} catch (Exception $e) {
			$missing = strpos($e->getMessage(), 'Parent not found') !== false ? 1 : 0;
		}
		$row = $db->query("SELECT url, content, title_tag, alias, value, parent, level, published_flag FROM content WHERE url='shop/pos'")->fetch(PDO::FETCH_ASSOC);
		$groups = $db->query('SELECT group_id FROM content_access WHERE content_id = ' . (int) $again . ' ORDER BY group_id')->fetchAll(PDO::FETCH_COLUMN);
		return array($first, $again, $missing, $row, array_map('intval', $groups));
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function vang_run_install()
{
	$schema = 'ecomae_cpw_vang_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = vang_open($schema);
	try {
		$db->exec("INSERT INTO groups VALUES (1, 0, 1)");
		$db->exec("INSERT INTO content (id, url, level, is_frontend, published_flag) VALUES (10, 'shop', 1, 0, 1), (20, 'control/config', 1, 0, 1), (21, 'control/portal/epc_tenant_control_center', 2, 0, 1)");
		$db->exec("INSERT INTO content_access VALUES (21, 7), (21, 9)");
		$GLOBALS['VANG_WALKIN'] = 44;
		$GLOBALS['VANG_PORTAL'] = array('portal' => 1);
		$GLOBALS['VANG_POS'] = array('pos' => 1);
		$out = epc_pos_cp_install($db, 'cp');
		$urls = $db->query('SELECT url FROM content WHERE is_frontend=0 ORDER BY url')->fetchAll(PDO::FETCH_COLUMN);
		$superAccess = $db->query('SELECT group_id FROM content_access WHERE content_id = ' . (int) $out['super_content_id'] . ' ORDER BY group_id')->fetchAll(PDO::FETCH_COLUMN);
		return array(
			$out['hub_content_id'] > 0 ? 1 : 0,
			$out['content_id'] > 0 ? 1 : 0,
			$out['super_content_id'] > 0 ? 1 : 0,
			$out['walkin_user_id'],
			$out['menu'],
			$urls,
			array_map('intval', $superAccess),
			$GLOBALS['VANG_SCHEMA'],
		);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function vang_run_connect()
{
	$cfg = new DP_Config();
	$cfg->host = '127.0.0.1';
	$cfg->user = 'ecomae';
	$cfg->password = (string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$empty = epc_pos_setup_connect(array('db' => ''), $cfg);
	$missingUser = epc_pos_setup_connect(array('db' => 'mysql', 'user' => '', 'pass' => ''), $cfg);
	$ok = epc_pos_setup_connect(array('db' => 'mysql', 'user' => 'ecomae', 'pass' => getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')), $cfg);
	$bad = epc_pos_setup_connect(array('db' => 'no_such_db_vang', 'user' => 'ecomae', 'pass' => 'x'), $cfg);
	$okFlag = $ok instanceof PDO ? 1 : 0;
	$ok = null;
	return array($empty === null ? 1 : 0, $missingUser instanceof PDO ? 1 : 0, $okFlag, $bad === null ? 1 : 0);
}

function vang_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/epc_pos_helpers.php';", '// leftover pos helper parent stubbed', $code);
	$code = str_replace(
		"\$mainstream = \$_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';\n\tif (is_file(\$mainstream)) {\n\t\trequire_once \$mainstream;\n\t}",
		'// leftover mainstream include stubbed',
		$code
	);
	file_put_contents($dest, $code);
}

function vang_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	$GLOBALS['VANG_WALKIN'] = 0;
	$GLOBALS['VANG_PORTAL'] = array();
	$GLOBALS['VANG_POS'] = array();
	$GLOBALS['VANG_SCHEMA'] = 0;
	if (!function_exists('epc_pos_ensure_schema')) {
		eval('function epc_pos_ensure_schema($db) { $GLOBALS["VANG_SCHEMA"] = 1; }');
	}
	if (!function_exists('epc_pos_ensure_walkin_user')) {
		eval('function epc_pos_ensure_walkin_user($db) { return (int) ($GLOBALS["VANG_WALKIN"] ?? 0); }');
	}
	if (!function_exists('epc_cp_portal_menu_apply')) {
		eval('function epc_cp_portal_menu_apply($db) { return $GLOBALS["VANG_PORTAL"] ?? array(); }');
	}
	if (!function_exists('epc_cp_pos_menu_apply')) {
		eval('function epc_cp_pos_menu_apply($db) { return $GLOBALS["VANG_POS"] ?? array(); }');
	}
	if (!class_exists('DP_Config', false)) {
		eval('class DP_Config { public $host; public $user; public $password; }');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_vang_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	vang_patch($root . '/content/shop/pos/epc_pos_cp_install.php', $doc . '/install.php');
	vang_boot();
	require $doc . '/install.php';
	$fn = 'vang_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/install.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1vang_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1vang_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
