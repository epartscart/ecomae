<?php
// PHP 8.3 goldens for plan Q1-fjord (marketing broadcast panel). Leftover dp_user stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function fjord_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/shop\/marketing\/epc_marketing_broadcast_helpers\.php[\'"]\s*;/', '// helpers injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/general_pages\/epc_cp_page_frame\.php[\'"]\s*;/', '// frame injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/users\/dp_user\.php[\'"]\s*;/', '// leftover user injected', $code);
	$code = preg_replace('/require_once\s+\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*[\'"]\/content\/notifications\/epc_whatsapp_notify\.php[\'"]\s*;/', '// wa notify injected', $code);
	file_put_contents($dest, $code);
}

function fjord_pdo(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function fjord_stubs(): void
{
	if (!function_exists('epc_mb_h')) {
		function epc_mb_h($v): string
		{
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		}
	}
	if (!function_exists('epc_mb_ensure_schema')) {
		function epc_mb_ensure_schema($pdo): void
		{
		}
	}
	if (!function_exists('epc_mb_shop_context')) {
		function epc_mb_shop_context($cfg): array
		{
			return (array) ($GLOBALS['FJORD_SHOP'] ?? array('shop_name' => "O'Reilly Parts"));
		}
	}
	if (!function_exists('epc_mb_dashboard_stats')) {
		function epc_mb_dashboard_stats($pdo): array
		{
			return (array) ($GLOBALS['FJORD_STATS'] ?? array(
				'email_recipients' => 4, 'whatsapp_recipients' => 3, 'emails_sent' => 2, 'whatsapp_sent' => 1, 'campaigns' => 2,
			));
		}
	}
	if (!function_exists('epc_mb_list_campaigns')) {
		function epc_mb_list_campaigns($pdo, $limit = 15): array
		{
			return (array) ($GLOBALS['FJORD_CAMPAIGNS'] ?? array());
		}
	}
	if (!function_exists('epc_mb_list_groups')) {
		function epc_mb_list_groups($pdo): array
		{
			return (array) ($GLOBALS['FJORD_GROUPS'] ?? array(array('id' => 7, 'name' => "O'Reilly trade")));
		}
	}
	if (!function_exists('epc_mb_email_templates')) {
		function epc_mb_email_templates(): array
		{
			return array('promo_sale' => array('label' => 'Sale'), 'blank' => array('label' => 'Blank'));
		}
	}
	if (!function_exists('epc_mb_whatsapp_templates')) {
		function epc_mb_whatsapp_templates(): array
		{
			return array('promo_bilingual' => array('label' => 'Promo EN+AR'));
		}
	}
	if (!function_exists('epc_mb_csrf_token')) {
		function epc_mb_csrf_token(): string
		{
			return 'tok-1';
		}
	}
	if (!function_exists('epc_mb_verify_csrf')) {
		function epc_mb_verify_csrf(): bool
		{
			return !empty($GLOBALS['FJORD_CSRF_OK']);
		}
	}
	if (!function_exists('epc_mb_hub_url')) {
		function epc_mb_hub_url(string $tab = 'email'): string
		{
			$base = '/cp/control/portal/epc_marketing_broadcast';
			return $tab !== '' && $tab !== 'email' ? $base . '?tab=' . rawurlencode($tab) : $base;
		}
	}
	if (!function_exists('epc_mb_backend')) {
		function epc_mb_backend(): string
		{
			return 'cp';
		}
	}
	if (!function_exists('epc_auth_smtp_diagnose')) {
		function epc_auth_smtp_diagnose(): array
		{
			return (array) ($GLOBALS['FJORD_SMTP'] ?? array('ok' => 1, 'issues' => array()));
		}
	}
	if (!function_exists('epc_wa_api_enabled')) {
		function epc_wa_api_enabled($cfg): bool
		{
			return !empty($GLOBALS['FJORD_WA']);
		}
	}
	if (!function_exists('epc_mb_send_email_campaign')) {
		function epc_mb_send_email_campaign($pdo, $post, $cfg, $op): array
		{
			return array('ok' => true, 'message' => 'email:' . $op);
		}
	}
	if (!function_exists('epc_mb_send_whatsapp_campaign')) {
		function epc_mb_send_whatsapp_campaign($pdo, $post, $cfg, $op): array
		{
			return array(
				'ok' => true,
				'message' => 'wa:' . $op,
				'wa_links' => array(
					array('link' => 'https://wa.me/97150', 'name' => "O'Reilly"),
					array('link' => '', 'name' => 'skip'),
				),
			);
		}
	}
	if (!function_exists('epc_cp_page_frame_open')) {
		function epc_cp_page_frame_open(array $opts = array()): void
		{
			echo 'FRAME_OPEN:' . (string) ($opts['class'] ?? '');
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
			public static function isAdmin(): bool
			{
				return !empty($GLOBALS['FJORD_ADMIN']);
			}

			public static function getUserId()
			{
				return $GLOBALS['FJORD_UID'] ?? 21;
			}
		}
	}
}

function fjord_include(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	fjord_stubs();
	require_once $GLOBALS['FJORD_PAGE'];
}

function fjord_capture(callable $fn): string
{
	ob_start();
	$fn();
	return (string) ob_get_clean();
}

function fjord_reset(): void
{
	$GLOBALS['FJORD_ADMIN'] = 0;
	$GLOBALS['FJORD_UID'] = 21;
	$GLOBALS['FJORD_CSRF_OK'] = 0;
	$GLOBALS['FJORD_WA'] = 0;
	$GLOBALS['FJORD_SMTP'] = array('ok' => 1, 'issues' => array());
	$GLOBALS['FJORD_SHOP'] = array('shop_name' => "O'Reilly Parts");
	$GLOBALS['FJORD_CAMPAIGNS'] = array();
	$GLOBALS['db_link'] = null;
	$GLOBALS['DP_Config'] = (object) array();
	$_GET = array();
	$_POST = array();
	$_SERVER['REQUEST_METHOD'] = 'GET';
}

function fjord_run_gates(): array
{
	fjord_include();
	$noAdmin = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	$GLOBALS['FJORD_ADMIN'] = 1;
	$noDb = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	$GLOBALS['FJORD_ADMIN'] = 1;
	$GLOBALS['db_link'] = fjord_pdo();
	$_GET['tab'] = 'Email!';
	$email = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	return array($noAdmin, $noDb, $email);
}

function fjord_run_hub(): array
{
	fjord_include();
	fjord_reset();
	$GLOBALS['FJORD_ADMIN'] = 1;
	$GLOBALS['db_link'] = fjord_pdo();
	$_GET['tab'] = 'guide';
	$acme = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	$GLOBALS['FJORD_SHOP'] = array('shop_name' => 'Beta Demo');
	$beta = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	$_SERVER['REQUEST_METHOD'] = 'POST';
	$_POST['epc_mb_action'] = 'send_email';
	$GLOBALS['FJORD_CSRF_OK'] = 0;
	$_GET['tab'] = 'email';
	$csrf = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	$GLOBALS['FJORD_CSRF_OK'] = 1;
	$ok = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	$_POST['epc_mb_action'] = 'send_whatsapp';
	$wa = fjord_capture(static function () {
		epc_mb_render_hub();
	});
	return array($acme, $beta, $csrf, $ok, $wa);
}

function fjord_run_compose(): array
{
	fjord_include();
	$pdo = fjord_pdo();
	$groups = array(array('id' => 7, 'name' => "O'Reilly trade"));
	$smtpOk = array('ok' => 1, 'issues' => array());
	$smtpBad = array('ok' => 0, 'issues' => array('host empty', "O'Reilly"));
	$emailOk = fjord_capture(static function () use ($pdo, $groups, $smtpOk) {
		epc_mb_render_email_tab($pdo, epc_mb_email_templates(), $groups, 'tok-1', $smtpOk, '/cp/mail');
	});
	$emailBad = fjord_capture(static function () use ($pdo, $groups, $smtpBad) {
		epc_mb_render_email_tab($pdo, epc_mb_email_templates(), $groups, 'tok-1', $smtpBad, '/cp/mail');
	});
	$waOff = fjord_capture(static function () use ($pdo, $groups) {
		epc_mb_render_whatsapp_tab($pdo, epc_mb_whatsapp_templates(), $groups, 'tok-1', false);
	});
	$waOn = fjord_capture(static function () use ($pdo, $groups) {
		epc_mb_render_whatsapp_tab($pdo, epc_mb_whatsapp_templates(), $groups, 'tok-1', true);
	});
	return array($emailOk, $emailBad, $waOff, $waOn);
}

function fjord_run_history(): array
{
	fjord_include();
	$empty = fjord_capture(static function () {
		epc_mb_render_history_tab(array());
	});
	$rows = fjord_capture(static function () {
		epc_mb_render_history_tab(array(
			array('id' => 9, 'channel' => 'whatsapp', 'audience_mode' => 'all', 'created_at' => 1700000000, 'total_targets' => 3, 'status' => 'done', 'sent_ok' => 2, 'sent_fail' => 1),
			array('id' => 8, 'channel' => 'email', 'audience_mode' => 'group', 'created_at' => 1700000000, 'total_targets' => 4, 'status' => 'sent', 'sent_ok' => 4, 'sent_fail' => 0),
		));
	});
	$guideOff = fjord_capture(static function () {
		epc_mb_render_guide_tab(array('shop_name' => "O'Reilly Parts"), '/cp/mail', '/cp/mb?tab=guide', false, '/cp/int');
	});
	$guideOn = fjord_capture(static function () {
		epc_mb_render_guide_tab(array('shop_name' => 'Beta Demo'), '/cp/mail', '/cp/mb?tab=guide', true, '/cp/int');
	});
	return array($empty, $rows, $guideOff, $guideOn);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_fjord_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	fjord_patch($root . '/cp/content/control/portal/epc_marketing_broadcast_panel.php', $tmp . '/page.php');
	$GLOBALS['FJORD_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	fjord_reset();
	$fn = 'fjord_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1fjord_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1fjord_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
