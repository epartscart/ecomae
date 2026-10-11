<?php
// PHP 8.3 goldens for plan Q1-hunt (tenant email/SMTP page). Leftover unique
// portal, leftover unique page-frame parent, leftover unique user helper,
// leftover portal-db parent, and Super-CP host stay injected.
// Real integrations helpers and auth-smtp parents are not included.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hunt_helpers(): void
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
	if (!function_exists('epc_integrations_feature_enabled')) {
		function epc_integrations_feature_enabled(string $featureKey, $siteKey = null, $platformPdo = null): bool
		{
			return !empty($GLOBALS['HUNT_FEATURE']);
		}
	}
	if (!function_exists('epc_integrations_load_tenant_config')) {
		function epc_integrations_load_tenant_config($pdo = null): array
		{
			$cfg = isset($GLOBALS['HUNT_CONFIG']) && is_array($GLOBALS['HUNT_CONFIG'])
				? $GLOBALS['HUNT_CONFIG']
				: array();
			return $cfg;
		}
	}
	if (!function_exists('epc_auth_smtp_diagnose')) {
		function epc_auth_smtp_diagnose(): array
		{
			$diag = isset($GLOBALS['HUNT_DIAG']) && is_array($GLOBALS['HUNT_DIAG'])
				? $GLOBALS['HUNT_DIAG']
				: array();
			return $diag;
		}
	}
}

function hunt_stubs(bool $withSuperFn): void
{
	hunt_helpers();
	if ($withSuperFn && !function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['HUNT_SUPER']);
		}
	}
}

function hunt_docroot_files(string $doc): void
{
	$pages = $doc . '/content/general_pages';
	$users = $doc . '/content/users';
	@mkdir($pages, 0777, true);
	@mkdir($users, 0777, true);
	file_put_contents($pages . '/epc_integrations_helpers.php', "<?php\n");
	file_put_contents($pages . '/epc_auth_smtp.php', "<?php\n");
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
	file_put_contents($users . '/dp_user.php', <<<'PHP'
<?php
class DP_User {
	public static function isAdmin(): bool
	{
		return !empty($GLOBALS['HUNT_ADMIN']);
	}
	public static function getAdminEmail(): string
	{
		return (string) ($GLOBALS['HUNT_ADMIN_EMAIL'] ?? '');
	}
}
PHP);
}

function hunt_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	hunt_stubs(!empty($GLOBALS['HUNT_SUPER_FN']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['HUNT_DOCROOT'];
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

function hunt_render(
	mixed $config,
	mixed $super,
	bool $superFn,
	bool $admin,
	bool $feature,
	array $integrations,
	array $diag,
	string $adminEmail
): string {
	$GLOBALS['HUNT_SUPER'] = $super;
	$GLOBALS['HUNT_SUPER_FN'] = $superFn;
	$GLOBALS['HUNT_ADMIN'] = $admin;
	$GLOBALS['HUNT_FEATURE'] = $feature;
	$GLOBALS['HUNT_CONFIG'] = $integrations;
	$GLOBALS['HUNT_DIAG'] = $diag;
	$GLOBALS['HUNT_ADMIN_EMAIL'] = $adminEmail;
	if ($config === '__unset__') {
		unset($GLOBALS['DP_Config']);
	} else {
		$GLOBALS['DP_Config'] = $config;
	}
	$tmp = sys_get_temp_dir() . '/ecomae_hunt_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['HUNT_PAGE'], $tmp);
	ob_start();
	hunt_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function hunt_run_empty(): array
{
	$none = hunt_render('__unset__', 0, false, false, true, array(), array(), '');
	$login = hunt_render((object) array('backend_dir' => 'acme'), 1, true, false, true, array(), array(), '');
	return array($none, $login);
}

function hunt_run_gates(): array
{
	$noSuper = hunt_render((object) array('backend_dir' => 'cp'), 1, false, true, false, array(), array(), '');
	$emptyDir = hunt_render((object) array('backend_dir' => ''), 0, true, true, true, array(), array(), 'ops@example.com');
	$escape = hunt_render((object) array('backend_dir' => 'acme"><x'), 0, true, true, true, array(), array(), 'ops@example.com');
	$tenantGate = hunt_render((object) array('backend_dir' => 'cp'), 0, true, true, true, array(), array(), 'ops@example.com');
	$denied = hunt_render((object) array('backend_dir' => 'cp'), 1, true, false, true, array(), array(), '');
	return array($noSuper, $emptyDir, $escape, $tenantGate, $denied);
}

function hunt_run_live(): array
{
	$cfg = (object) array('backend_dir' => 'cp');
	$defaults = hunt_render($cfg, 0, true, true, true, array(), array(), '');
	$diag = hunt_render($cfg, 0, true, true, true, array(), array(
		'source' => 'config.php',
		'host' => 'smtp.example.com',
		'port' => '465',
		'encryption' => 'ssl',
		'from_name' => 'Ada & Co',
		'from_email' => 'ada@example.com',
	), 'ops@example.com');
	$filled = hunt_render($cfg, 0, true, true, true, array(
		'smtp' => array(
			'use_tenant_smtp' => true,
			'smtp_host' => 'mail.acme.com',
			'smtp_port' => '587',
			'smtp_encryption' => 'none',
			'smtp_username' => 'acme & ops',
			'smtp_password' => 'secret',
			'from_name' => 'Acme',
			'from_email' => 'noreply@acme.com',
		),
	), array('source' => 'tenant'), 'Ada <ada@acme.com>');
	$blankEnc = hunt_render($cfg, 0, true, true, true, array(
		'smtp' => array('smtp_encryption' => ''),
	), array(), '');
	$featureOff = hunt_render($cfg, 1, true, true, false, array(), array(), '');
	return array($defaults, $diag, $filled, $blankEnc, $featureOff);
}

function hunt_run_tenant(): array
{
	$acme = hunt_render((object) array('backend_dir' => 'acme'), 1, true, true, true, array(), array('source' => 'acme'), 'ada@acme.test');
	$beta = hunt_render((object) array('backend_dir' => 'beta'), 0, true, true, true, array(), array('source' => 'beta'), 'bea@beta.test');
	$denied = hunt_render((object) array('backend_dir' => 'acme'), 1, true, false, true, array(), array(), '');
	return array($acme, $beta, $denied);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_hunt_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$GLOBALS['HUNT_PAGE'] = $root . '/cp/content/control/portal/epc_tenant_email_settings.php';
	$GLOBALS['HUNT_DOCROOT'] = $tmp;
	hunt_docroot_files($tmp);
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'hunt_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hunt_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hunt_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
