<?php
/**
 * Boot ERP page like CP (eval path) and report fatal errors.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', '0');
error_reporting(E_ALL);

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
$db_link = new PDO(
	'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
	$DP_Config->user,
	$DP_Config->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db_link->query('SET NAMES utf8');

$backend = $DP_Config->backend_dir;
$root = $_SERVER['DOCUMENT_ROOT'];
$out = array('status' => true, 'steps' => array());

function epc_erp_boot_step(array &$out, $name, callable $fn)
{
	try {
		$result = $fn();
		$out['steps'][$name] = array('ok' => true, 'result' => $result);
		return true;
	} catch (Throwable $e) {
		$out['steps'][$name] = array(
			'ok' => false,
			'error' => $e->getMessage(),
			'file' => $e->getFile(),
			'line' => $e->getLine(),
		);
		$out['status'] = false;
		return false;
	}
}

define('_ASTEXE_', 1);
$GLOBALS['DP_Config'] = $DP_Config;
$GLOBALS['db_link'] = $db_link;

require_once $root . '/content/shop/finance/epc_erp_helpers.php';

epc_erp_boot_step($out, 'schema', function () use ($db_link) {
	epc_erp_full_ensure_schema($db_link);
	return 'ok';
});

$date_from = strtotime(date('Y-m-01 00:00:00'));
$date_to = time();

epc_erp_boot_step($out, 'dashboard', function () use ($db_link, $date_from, $date_to) {
	$d = epc_erp_dashboard($db_link, $date_from, $date_to);
	return array('order_count' => $d['order_count'], 'revenue' => $d['revenue_ex_vat']);
});

epc_erp_boot_step($out, 'pl_report', function () use ($db_link, $date_from, $date_to) {
	$p = epc_erp_gl_pl_report($db_link, $date_from, $date_to);
	return array('net_profit' => $p['net_profit']);
});

epc_erp_boot_step($out, 'include_wrapper', function () use ($root, $backend, $db_link) {
	$user_session = array('user_id' => 1, 'csrf_guard_key' => 'diag');
	ob_start();
	include $root . '/' . $backend . '/content/shop/finance/erp/erp_main_page.php';
	$html = ob_get_clean();
	return array('bytes' => strlen($html), 'has_erp' => (strpos($html, 'epc-erp-kpi') !== false));
});

epc_erp_boot_step($out, 'eval_wrapper', function () use ($root, $backend, $db_link, $DP_Config) {
	$path = $root . '/' . $backend . '/content/shop/finance/erp/erp_main_page.php';
	$code = file_get_contents($path);
	if ($code === false) {
		throw new RuntimeException('Cannot read wrapper');
	}
	$user_session = array('user_id' => 1, 'csrf_guard_key' => 'diag');
	ob_start();
	eval('?>' . $code . '<?php ');
	$html = ob_get_clean();
	return array('bytes' => strlen($html), 'has_erp' => (strpos($html, 'epc-erp-kpi') !== false));
});

epc_erp_boot_step($out, 'eval_body', function () use ($root, $backend, $db_link) {
	$path = $root . '/' . $backend . '/content/shop/finance/erp/erp_main.php';
	$code = file_get_contents($path);
	if ($code === false) {
		throw new RuntimeException('Cannot read body');
	}
	$user_session = array('user_id' => 1, 'csrf_guard_key' => 'diag');
	ob_start();
	eval('?>' . $code . '<?php ');
	$html = ob_get_clean();
	return array('bytes' => strlen($html), 'has_erp' => (strpos($html, 'epc-erp-kpi') !== false));
});

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
