<?php
// PHP 8.3 goldens for plan Q1-howe (CP mobile-apps page). Leftover unique
// portal, leftover unique page-frame parent, leftover unique user helper,
// leftover portal-db parent, Super-CP host, and portal host stay injected.
// Real integrations helpers are not included — they pull leftover parents.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function howe_helpers(): void
{
	if (!function_exists('epc_int_h')) {
		function epc_int_h($v): string
		{
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		}
	}
	if (!function_exists('epc_int_backend')) {
		function epc_int_backend(): string
		{
			$dir = 'cp';
			if (isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config'])) {
				$dir = (string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp');
			}
			return trim($dir, '/');
		}
	}
	if (!function_exists('epc_integrations_default_mobile_config')) {
		function epc_integrations_default_mobile_config(): array
		{
			return array(
				'enabled' => false,
				'app_name' => '',
				'bundle_id' => '',
				'deep_link_scheme' => '',
				'deep_link_domain' => '',
				'api_base_url' => '',
				'play_store_url' => '',
				'app_store_url' => '',
				'pwa_enabled' => true,
				'firebase_project_id' => '',
				'push_enabled' => false,
			);
		}
	}
	if (!function_exists('epc_integrations_mobile_config')) {
		function epc_integrations_mobile_config($pdo = null): array
		{
			$override = isset($GLOBALS['HOWE_MOBILE']) && is_array($GLOBALS['HOWE_MOBILE'])
				? $GLOBALS['HOWE_MOBILE']
				: array();
			return array_merge(epc_integrations_default_mobile_config(), $override);
		}
	}
	if (!function_exists('epc_integrations_platform_mobile_defaults')) {
		function epc_integrations_platform_mobile_defaults(): array
		{
			return array(
				'api_base_url' => 'https://www.ecomae.com',
				'firebase_template' => '',
				'capacitor_version' => '6',
				'allow_push' => true,
				'default_deep_link_scheme' => 'epartscart://',
			);
		}
	}
}

function howe_stubs(bool $withSuperFn, bool $withHostFn): void
{
	howe_helpers();
	if ($withSuperFn && !function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['HOWE_SUPER']);
		}
	}
	if ($withHostFn && !function_exists('epc_portal_host')) {
		function epc_portal_host(): string
		{
			return (string) ($GLOBALS['HOWE_PORTAL_HOST'] ?? '');
		}
	}
}

function howe_docroot_files(string $doc, bool $withUserClass): void
{
	$pages = $doc . '/content/general_pages';
	$users = $doc . '/content/users';
	@mkdir($pages, 0777, true);
	@mkdir($users, 0777, true);
	file_put_contents($pages . '/epc_integrations_helpers.php', "<?php\n");
	file_put_contents($pages . '/epc_cp_page_frame.php', <<<'PHP'
<?php
if (!function_exists('epc_cp_page_frame_open')) {
	function epc_cp_page_frame_open($opts): void
	{
		$opts = is_array($opts) ? $opts : array();
		$hero = isset($opts['hero']) && is_array($opts['hero']) ? $opts['hero'] : array();
		echo 'FRAME_OPEN:'
			. (string) ($opts['class'] ?? '') . '|'
			. (string) ($hero['badge'] ?? '') . '|'
			. (string) ($hero['title'] ?? '');
	}
}
if (!function_exists('epc_cp_page_frame_close')) {
	function epc_cp_page_frame_close(): void
	{
		echo 'FRAME_CLOSE';
	}
}
PHP);
	if ($withUserClass) {
		file_put_contents($users . '/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function isAdmin(): bool
	{
		return !empty($GLOBALS['HOWE_ADMIN']);
	}
}
PHP);
	} else {
		file_put_contents($users . '/dp_user.php', "<?php\n");
	}
}

function howe_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	howe_stubs(!empty($GLOBALS['HOWE_SUPER_FN']), !empty($GLOBALS['HOWE_HOST_FN']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['HOWE_DOCROOT'];
	if (array_key_exists('DP_Config', $GLOBALS)) {
		$DP_Config = $GLOBALS['DP_Config'];
	}
	if (array_key_exists('db_link', $GLOBALS)) {
		$db_link = $GLOBALS['db_link'];
	} else {
		unset($db_link);
	}
	require $path;
}

function howe_render(
	mixed $config,
	mixed $super,
	bool $superFn,
	bool $admin,
	bool $userClass,
	mixed $host,
	bool $hostFn,
	mixed $portalHost,
	array $mobile
): string {
	$GLOBALS['HOWE_SUPER'] = $super;
	$GLOBALS['HOWE_SUPER_FN'] = $superFn;
	$GLOBALS['HOWE_ADMIN'] = $admin;
	$GLOBALS['HOWE_HOST_FN'] = $hostFn;
	$GLOBALS['HOWE_MOBILE'] = $mobile;
	if ($config === '__unset__') {
		unset($GLOBALS['DP_Config']);
	} else {
		$GLOBALS['DP_Config'] = $config;
	}
	if ($host === '__unset__') {
		unset($_SERVER['HTTP_HOST']);
	} else {
		$_SERVER['HTTP_HOST'] = $host;
	}
	if ($portalHost === '__unset__') {
		unset($GLOBALS['HOWE_PORTAL_HOST']);
	} else {
		$GLOBALS['HOWE_PORTAL_HOST'] = $portalHost;
	}
	$tmp = sys_get_temp_dir() . '/ecomae_howe_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['HOWE_PAGE'], $tmp);
	ob_start();
	howe_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function howe_run_empty(): array
{
	$none = howe_render('__unset__', 0, false, false, true, '__unset__', false, '__unset__', array());
	$login = howe_render((object) array('backend_dir' => 'acme'), 1, true, false, true, 'acme.example.com', false, '__unset__', array());
	return array($none, $login);
}

function howe_run_gates(): array
{
	$noSuper = howe_render((object) array('backend_dir' => 'cp'), 1, false, true, true, 'cp.example.com', false, '__unset__', array());
	$emptyDir = howe_render((object) array('backend_dir' => ''), 0, true, true, true, 'cp.example.com', false, '__unset__', array());
	$escape = howe_render((object) array('backend_dir' => 'acme"><x'), 0, true, true, true, 'cp.example.com', false, '__unset__', array());
	$tenantGate = howe_render((object) array('backend_dir' => 'cp'), 0, true, true, true, 'cp.example.com', false, '__unset__', array());
	$denied = howe_render((object) array('backend_dir' => 'cp'), 1, true, false, true, 'cp.example.com', false, '__unset__', array());
	return array($noSuper, $emptyDir, $escape, $tenantGate, $denied);
}

function howe_filled_mobile(): array
{
	return array(
		'enabled' => true,
		'app_name' => 'Ada & Co',
		'bundle_id' => 'com.acme.app',
		'deep_link_scheme' => 'acme://',
		'deep_link_domain' => 'www.acme.com',
		'api_base_url' => 'https://shop.acme.com/en/',
		'play_store_url' => 'https://play.google.com/x',
		'app_store_url' => 'https://apps.apple.com/x',
		'pwa_enabled' => false,
		'firebase_project_id' => 'acme-fire',
		'push_enabled' => true,
	);
}

function howe_run_live(): array
{
	$cfg = (object) array('backend_dir' => 'cp');
	$tenantDefault = howe_render($cfg, 0, true, true, true, 'WWW.Example.COM', false, '__unset__', array());
	$superDefault = howe_render($cfg, 1, true, true, true, 'example.com', false, '__unset__', array());
	$filled = howe_render($cfg, 1, true, true, true, 'example.com', false, '__unset__', howe_filled_mobile());
	$wwwHost = howe_render($cfg, 0, true, true, true, 'www.epartscart.com', false, '__unset__', array());
	$apex = howe_render($cfg, 0, true, true, true, 'epartscart.com', false, '__unset__', array('api_base_url' => '0'));
	return array($tenantDefault, $superDefault, $filled, $wwwHost, $apex);
}

function howe_run_tenant(): array
{
	$acme = howe_render((object) array('backend_dir' => 'acme'), 1, true, true, true, 'acme.example.com', false, '__unset__', array());
	$beta = howe_render((object) array('backend_dir' => 'beta'), 0, true, true, true, 'beta.example.com', false, '__unset__', array());
	$portalHost = howe_render((object) array('backend_dir' => 'acme'), 1, true, true, true, 'ignored.example.com', true, 'shop.acme.test', array());
	return array($acme, $beta, $portalHost);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_howe_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$GLOBALS['HOWE_PAGE'] = $root . '/cp/content/control/portal/epc_mobile_apps.php';
	$GLOBALS['HOWE_DOCROOT'] = $tmp;
	howe_docroot_files($tmp, true);
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'howe_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		exit(1);
	}
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1howe_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1howe_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
