<?php
// PHP 8.3 goldens for plan Q1-keel (Super CP auth gate). Portal / demo / MFA parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function keel_norm($value)
{
	if ($value === null) {
		return array('action' => 'continue');
	}
	return $value;
}

function keel_json_body($raw)
{
	return array(
		'action' => 'json',
		'content_type' => 'application/json; charset=utf-8',
		'body' => (string) $raw,
	);
}

function keel_admin($session, $uid)
{
	$_COOKIE = array();
	if ($session !== null) {
		$_COOKIE['admin_session'] = $session;
	}
	if ($uid !== null) {
		$_COOKIE['admin_u_id'] = $uid;
	}
	return epc_cp_auth_gate_is_admin();
}

function keel_run_admin(PDO $pdo)
{
	$saved = $GLOBALS['DP_Config'];
	$GLOBALS['DP_Config'] = null;
	$noCfg = keel_admin('tok-ok', '7');
	$GLOBALS['DP_Config'] = $saved;
	$pdo->exec('DELETE FROM `sessions`');
	$ins = $pdo->prepare('INSERT INTO `sessions` (`session`,`type`,`user_id`) VALUES (?,?,?)');
	$ins->execute(array('tok-ok', 1, 7));
	$ins->execute(array('tok-user', 0, 7));
	$ins->execute(array('tok-dup', 1, 7));
	$ins->execute(array('tok-dup', 1, 7));
	$ok = array(
		$noCfg,
		keel_admin('', '7'),
		keel_admin('tok-ok', '0'),
		keel_admin('tok-ok', ''),
		keel_admin('missing', '7'),
		keel_admin('tok-user', '7'),
		keel_admin('tok-ok', '7'),
		keel_admin('tok-ok', '7abc'),
		keel_admin('tok-ok', '8'),
		keel_admin('tok-dup', '7'),
	);
	$pdo->exec('DROP TABLE `sessions`');
	$ok[] = keel_admin('tok-ok', '7');
	return $ok;
}

function keel_run_landing()
{
	$GLOBALS['KEEL_ERP_ONLY'] = false;
	$a = epc_cp_auth_gate_erp_only_landing();
	$GLOBALS['KEEL_ERP_ONLY'] = true;
	$GLOBALS['DP_Config']->backend_dir = 'cp';
	$b = epc_cp_auth_gate_erp_only_landing();
	$GLOBALS['DP_Config']->backend_dir = '';
	$c = epc_cp_auth_gate_erp_only_landing();
	$GLOBALS['DP_Config']->backend_dir = 'panel';
	$d = epc_cp_auth_gate_erp_only_landing();
	if (!function_exists('epc_portal_erp_cp_shell_url')) {
		eval('function epc_portal_erp_cp_shell_url() { return (string) ($GLOBALS["KEEL_ERP_SHELL"] ?? ""); }');
	}
	$GLOBALS['KEEL_ERP_SHELL'] = '/erp-app';
	$e = epc_cp_auth_gate_erp_only_landing();
	return array($a, $b, $c, $d, $e);
}

function keel_prep_admin(PDO $pdo)
{
	$pdo->exec('DELETE FROM `sessions`');
	$pdo->prepare('INSERT INTO `sessions` (`session`,`type`,`user_id`) VALUES (?,?,?)')->execute(array('tok-ok', 1, 7));
	$_COOKIE = array('admin_session' => 'tok-ok', 'admin_u_id' => '7');
}

function keel_req($method, $uri, $qs = '', $get = array(), $post = array())
{
	$_SERVER['REQUEST_METHOD'] = $method;
	$_SERVER['REQUEST_URI'] = $uri;
	$_SERVER['QUERY_STRING'] = $qs;
	$_GET = $get;
	$_POST = $post;
}

function keel_run_gate(PDO $pdo)
{
	$out = array();
	$_COOKIE = array();
	$GLOBALS['DP_Config']->backend_dir = 'cp';
	keel_req('GET', '/cp/', 'next=1');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/index.php');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('POST', '/cp/', '', array(), array('authentication' => '1'));
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/shop/tenant_hub/x', 'a=1');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/control/portal/tenants');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/shop/finance/erp');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/control');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/client-erp/x');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/demo/x');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('GET', '/cp/control/ajax/ping');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('POST', '/cp/', '', array(), array('authentication' => '0'));
	$out[] = keel_norm(epc_cp_auth_gate_run());
	keel_req('POST', '/cp/', '', array(), array('authentication' => ''));
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['DP_Config']->backend_dir = '';
	keel_req('GET', '/cp/');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['DP_Config']->backend_dir = '/cp/';
	keel_req('GET', '/cp/shop/tenant_hub/x');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['DP_Config']->backend_dir = 'cp';
	keel_prep_admin($pdo);
	keel_req('GET', '/cp/control/portal/tenants');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_ERP_ONLY'] = true;
	if (!function_exists('epc_portal_erp_cp_shell_url')) {
		eval('function epc_portal_erp_cp_shell_url() { return (string) ($GLOBALS["KEEL_ERP_SHELL"] ?? ""); }');
	}
	$GLOBALS['KEEL_ERP_SHELL'] = '/cp/shop/finance/erp?epc_erp_shell=1';
	$GLOBALS['KEEL_PLATFORM_HOST'] = false;
	keel_req('GET', '/cp/');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_PLATFORM_HOST'] = true;
	keel_req('GET', '/cp/', 'tab=1');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_ERP_ONLY'] = false;
	$GLOBALS['KEEL_PLATFORM_ERP'] = true;
	$GLOBALS['KEEL_PLATFORM_ERP_URL'] = '/platform-erp';
	keel_req('GET', '/cp/');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_PLATFORM_ERP'] = false;
	$GLOBALS['KEEL_CLIENT_ERP'] = true;
	$GLOBALS['KEEL_CLIENT_KEY'] = 'acme';
	keel_req('GET', '/cp/');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_CLIENT_ERP'] = false;
	$GLOBALS['KEEL_DEMO'] = true;
	$GLOBALS['KEEL_DEMO_PARSED'] = array('is_login_root' => 1);
	$GLOBALS['KEEL_DEMO_KEY'] = 'demo1';
	$GLOBALS['KEEL_DEMO_ERP_ONLY'] = true;
	keel_req('GET', '/demo/acme/cp/');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_DEMO_ERP_ONLY'] = false;
	keel_req('GET', '/demo/acme/cp/');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	$GLOBALS['KEEL_DEMO'] = true;
	$GLOBALS['KEEL_DEMO_PARSED'] = array('is_login_root' => 0);
	keel_req('GET', '/cp/shop/tenant_hub/x');
	$out[] = keel_norm(epc_cp_auth_gate_run());
	return $out;
}

function keel_mfa_present($on)
{
	$path = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_mfa.php';
	if ($on) {
		if (!is_file($path)) {
			keel_write_mfa($_SERVER['DOCUMENT_ROOT']);
		}
	} else {
		@unlink($path);
	}
}

function keel_run_mfa(PDO $pdo)
{
	$pdo->exec('DELETE FROM `sessions`');
	$pdo->prepare('INSERT INTO `sessions` (`session`,`type`,`user_id`) VALUES (?,?,?)')->execute(array('tok-ok', 1, 7));
	$_COOKIE = array();
	keel_mfa_present(true);
	epc_cp_mfa_route_guard();
	$a = array('called' => false, 'count' => (int) $GLOBALS['KEEL_ENFORCE']);
	$_COOKIE = array('admin_u_id' => '7');
	keel_mfa_present(false);
	epc_cp_mfa_route_guard();
	$b = array('called' => false, 'count' => (int) $GLOBALS['KEEL_ENFORCE']);
	keel_mfa_present(true);
	$_SERVER['REQUEST_URI'] = '/cp/control/portal/x';
	epc_cp_mfa_route_guard();
	$c = array(
		'called' => true,
		'path' => $GLOBALS['KEEL_ENFORCE_LAST']['path'],
		'user_id' => $GLOBALS['KEEL_ENFORCE_LAST']['user_id'],
	);
	$_SERVER['REQUEST_URI'] = '?skip=1';
	epc_cp_mfa_route_guard();
	$c2 = array('called' => false, 'count' => (int) $GLOBALS['KEEL_ENFORCE']);
	$_SERVER['REQUEST_URI'] = '';
	epc_cp_mfa_route_guard();
	$c3 = array(
		'called' => true,
		'path' => $GLOBALS['KEEL_ENFORCE_LAST']['path'],
		'user_id' => $GLOBALS['KEEL_ENFORCE_LAST']['user_id'],
	);
	$_COOKIE = array();
	ob_start();
	epc_cp_auth_gate_mfa_ajax();
	$d = keel_json_body(ob_get_clean());
	$_COOKIE = array('admin_u_id' => '7', 'admin_session' => 'bad');
	ob_start();
	epc_cp_auth_gate_mfa_ajax();
	$e = keel_json_body(ob_get_clean());
	$_COOKIE = array('admin_u_id' => '7', 'admin_session' => 'tok-ok');
	keel_mfa_present(false);
	ob_start();
	epc_cp_auth_gate_mfa_ajax();
	$f = keel_json_body(ob_get_clean());
	keel_mfa_present(true);
	ob_start();
	epc_cp_auth_gate_mfa_ajax();
	$g = keel_json_body(ob_get_clean());
	$_GET['epc_mfa_ajax'] = '1';
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['REQUEST_URI'] = '/cp/control?epc_mfa_ajax=1';
	$_SERVER['QUERY_STRING'] = 'epc_mfa_ajax=1';
	$h = keel_norm(epc_cp_auth_gate_run());
	return array($a, $b, $c, $c2, $c3, $d, $e, $f, $g, $h, (int) $GLOBALS['KEEL_ENFORCE'], (int) $GLOBALS['KEEL_AJAX']);
}

function keel_schema(PDO $pdo)
{
	$pdo->exec('CREATE TABLE `sessions` (
		`session` varchar(64) NOT NULL DEFAULT \'\',
		`type` int NOT NULL DEFAULT 0,
		`user_id` int NOT NULL DEFAULT 0
	) ENGINE=InnoDB DEFAULT CHARSET=utf8');
}

function keel_write_config($doc, $dbName, $password)
{
	file_put_contents($doc . '/config.php', "<?php
class DP_Config {
	public \$host = '127.0.0.1';
	public \$db = '" . $dbName . "';
	public \$user = 'ecomae';
	public \$password = '" . $password . "';
	public \$backend_dir = 'cp';
}
");
}

function keel_write_mfa($doc)
{
	$dir = $doc . '/content/general_pages';
	@mkdir($dir, 0777, true);
	file_put_contents($dir . '/epc_auth_mfa.php', '<?php
function epc_mfa_enforce_route_guard($pdo, $userId, $path): void {
	$GLOBALS["KEEL_ENFORCE"] = ((int) ($GLOBALS["KEEL_ENFORCE"] ?? 0)) + 1;
	$GLOBALS["KEEL_ENFORCE_LAST"] = array("user_id"=>(int)$userId, "path"=>(string)$path);
}
function epc_mfa_handle_ajax($pdo, $userId): array {
	$GLOBALS["KEEL_AJAX"] = ((int) ($GLOBALS["KEEL_AJAX"] ?? 0)) + 1;
	return array("ok" => true, "user_id" => (int) $userId, "next" => "/cp/mfa");
}
');
}

function keel_patch_gate(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace(
		"/header\\('Location: ' \\. (.+), true, 302\\);\\s*exit;/",
		'return array(\'action\' => \'redirect\', \'location\' => $1, \'status\' => 302);',
		$code
	);
	$code = str_replace(
		"\t\tepc_cp_auth_gate_mfa_ajax();\n\t\texit;",
		"\t\tobstart();\n\t\tepc_cp_auth_gate_mfa_ajax();\n\t\treturn array('action'=>'json','content_type'=>'application/json; charset=utf-8','body'=>ob_get_clean());",
		$code
	);
	$code = str_replace('obstart()', 'ob_start()', $code);
	file_put_contents($dest, $code);
}

if (!function_exists('epc_portal_is_erp_only_tenant')) {
	function epc_portal_is_erp_only_tenant() { return !empty($GLOBALS['KEEL_ERP_ONLY']); }
}
if (!function_exists('epc_portal_demo_is_cp_context')) {
	function epc_portal_demo_is_cp_context() { return !empty($GLOBALS['KEEL_DEMO']); }
}
if (!function_exists('epc_portal_demo_parse_cp_path')) {
	function epc_portal_demo_parse_cp_path() { return $GLOBALS['KEEL_DEMO_PARSED'] ?? null; }
}
if (!function_exists('epc_cp_control_url')) {
	function epc_cp_control_url($backend) { return '/' . $backend . '/control'; }
}
if (!function_exists('epc_portal_demo_cp_is_erp_only')) {
	function epc_portal_demo_cp_is_erp_only() { return !empty($GLOBALS['KEEL_DEMO_ERP_ONLY']); }
}
if (!function_exists('epc_portal_demo_erp_shell_url')) {
	function epc_portal_demo_erp_shell_url($key) { return '/demo/' . $key . '/erp'; }
}
if (!function_exists('epc_portal_demo_cp_site_key')) {
	function epc_portal_demo_cp_site_key() { return (string) ($GLOBALS['KEEL_DEMO_KEY'] ?? ''); }
}
if (!function_exists('epc_portal_demo_cp_post_login_url')) {
	function epc_portal_demo_cp_post_login_url($key) { return '/demo/' . $key . '/cp/control'; }
}
if (!function_exists('epc_platform_erp_is_active')) {
	function epc_platform_erp_is_active() { return !empty($GLOBALS['KEEL_PLATFORM_ERP']); }
}
if (!function_exists('epc_platform_erp_shell_url')) {
	function epc_platform_erp_shell_url() { return (string) ($GLOBALS['KEEL_PLATFORM_ERP_URL'] ?? ''); }
}
if (!function_exists('epc_client_erp_is_active')) {
	function epc_client_erp_is_active() { return !empty($GLOBALS['KEEL_CLIENT_ERP']); }
}
if (!function_exists('epc_client_erp_site_key')) {
	function epc_client_erp_site_key() { return (string) ($GLOBALS['KEEL_CLIENT_KEY'] ?? ''); }
}
if (!function_exists('epc_client_erp_shell_url')) {
	function epc_client_erp_shell_url($key) { return '/client-erp/' . $key; }
}
if (!function_exists('epc_portal_is_platform_hostname')) {
	function epc_portal_is_platform_hostname() { return !empty($GLOBALS['KEEL_PLATFORM_HOST']); }
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1k_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	keel_schema($pdo);
	keel_write_config($doc, $dbName, $password);
	keel_write_mfa($doc);
	keel_patch_gate($root . '/cp/epc_cp_auth_gate.php', $doc . '/epc_cp_auth_gate.php');
	$cleanup = function () use ($admin, $dbName, $doc) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		foreach (glob($doc . '/content/general_pages/*') ?: array() as $file) { @unlink($file); }
		@rmdir($doc . '/content/general_pages');
		@rmdir($doc . '/content');
		foreach (glob($doc . '/*') ?: array() as $file) { @unlink($file); }
		@rmdir($doc);
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	require $doc . '/config.php';
	$GLOBALS['DP_Config'] = new DP_Config();
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['KEEL_ERP_ONLY'] = false;
	$GLOBALS['KEEL_ERP_SHELL_FN'] = false;
	$GLOBALS['KEEL_ERP_SHELL'] = '';
	$GLOBALS['KEEL_DEMO'] = false;
	$GLOBALS['KEEL_DEMO_PARSED'] = null;
	$GLOBALS['KEEL_DEMO_KEY'] = '';
	$GLOBALS['KEEL_DEMO_ERP_ONLY'] = false;
	$GLOBALS['KEEL_PLATFORM_ERP'] = false;
	$GLOBALS['KEEL_PLATFORM_ERP_URL'] = '';
	$GLOBALS['KEEL_CLIENT_ERP'] = false;
	$GLOBALS['KEEL_CLIENT_KEY'] = '';
	$GLOBALS['KEEL_PLATFORM_HOST'] = false;
	$GLOBALS['KEEL_MFA_FILE'] = true;
	$GLOBALS['KEEL_ENFORCE'] = 0;
	$GLOBALS['KEEL_AJAX'] = 0;
	$_COOKIE = array();
	$_GET = array();
	$_POST = array();
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['REQUEST_URI'] = '/';
	$_SERVER['QUERY_STRING'] = '';
	if (empty($GLOBALS['KEEL_ERP_SHELL_FN'])) {
		// landing() uses function_exists — always defined here. Toggle via runkit is unavailable;
		// empty URL from the stub plus the flag is handled in keel_run_landing by renaming.
	}
	require $doc . '/epc_cp_auth_gate.php';
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1keel_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1keel_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
