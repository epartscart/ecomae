<?php
/**
 * CP AJAX — live FX rates for currency settings page.
 * Actions: preview | apply | schedule_get | schedule_save | schedule_run_now
 */
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');
if (ob_get_level()) {
	ob_end_clean();
}

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? ''), '/\\');
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}
global $db_link;
try {
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('ok' => false, 'error' => 'db')));
}

require_once $docRoot . '/content/users/dp_user.php';
require_once $docRoot . '/content/shop/finance/epc_currency_live_rates.php';

if ((int) DP_User::getAdminId() <= 0) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'forbidden')));
}

$action = (string) ($_GET['action'] ?? $_POST['action'] ?? 'preview');
$csrf = (string) ($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '');

try {
	if ($action === 'preview') {
		$out = epc_currency_live_preview($db_link, $DP_Config);
		exit(json_encode($out));
	}

	if ($action === 'schedule_get') {
		exit(json_encode(array('ok' => true, 'schedule' => epc_currency_live_schedule_get($db_link))));
	}

	if ($action === 'apply' || $action === 'schedule_save' || $action === 'schedule_run_now') {
		// CSRF for write
		$session = DP_User::getAdminSession();
		$expected = (string) ($session['csrf_guard_key'] ?? '');
		if ($expected === '' || !hash_equals($expected, $csrf)) {
			http_response_code(403);
			exit(json_encode(array('ok' => false, 'error' => 'csrf')));
		}
	}

	if ($action === 'apply') {
		$only = null;
		if (!empty($_POST['iso_codes'])) {
			$decoded = json_decode((string) $_POST['iso_codes'], true);
			if (is_array($decoded)) {
				$only = array();
				foreach ($decoded as $c) {
					$only[] = (string) $c;
				}
			}
		}
		$out = epc_currency_live_apply($db_link, $DP_Config, $only);
		exit(json_encode($out));
	}

	if ($action === 'schedule_save') {
		$out = epc_currency_live_schedule_save($db_link, array(
			'enabled' => !empty($_POST['enabled']) ? 1 : 0,
			'timezone' => (string) ($_POST['timezone'] ?? 'Asia/Dubai'),
			'hour' => (int) ($_POST['hour'] ?? 2),
		));
		exit(json_encode($out));
	}

	if ($action === 'schedule_run_now') {
		$tick = epc_currency_live_schedule_tick($db_link, $DP_Config, true);
		exit(json_encode($tick));
	}

	http_response_code(400);
	exit(json_encode(array('ok' => false, 'error' => 'bad_action')));
} catch (Throwable $e) {
	http_response_code(500);
	exit(json_encode(array('ok' => false, 'error' => 'query_failed', 'message' => $e->getMessage())));
}
