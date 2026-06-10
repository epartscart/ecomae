<?php
/**
 * Probe UAE advance payments + AR/AP statement summaries.
 * GET: token=epartscart-deploy-2026&user_id=&supplier_id=&simulate=1
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_advances.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_vouchers.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_helpers.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

epc_erp_advances_ensure_schema($pdo);
epc_erp_vouchers_ensure_schema($pdo);

$user_id = (int)($_GET['user_id'] ?? 0);
$supplier_id = (int)($_GET['supplier_id'] ?? 0);
$simulate = !empty($_GET['simulate']);
$out = array('status' => true, 'schema' => 'ok', 'steps' => array());

$run = function ($name, callable $fn) use (&$out) {
	$t0 = microtime(true);
	try {
		$result = $fn();
		$out['steps'][$name] = array('ok' => true, 'ms' => round((microtime(true) - $t0) * 1000, 1), 'result' => $result);
	} catch (Throwable $e) {
		$out['steps'][$name] = array('ok' => false, 'ms' => round((microtime(true) - $t0) * 1000, 1), 'error' => $e->getMessage());
	}
};

$run('coa_advance_accounts', function () use ($pdo) {
	$a = epc_erp_gl_coa_by_code($pdo, '2050');
	$b = epc_erp_gl_coa_by_code($pdo, '2060');
	return array('2050' => $a ? $a['name'] : null, '2060' => $b ? $b['name'] : null);
});

if ($user_id > 0) {
	$run('customer_summary', function () use ($pdo, $user_id) {
		return epc_erp_customer_statement_summary($pdo, $user_id);
	});
	$run('customer_aggregate_count', function () use ($pdo, $user_id) {
		return count(epc_erp_customer_statement_aggregate($pdo, $user_id));
	});
}

if ($supplier_id > 0) {
	$run('supplier_summary', function () use ($pdo, $supplier_id) {
		return epc_erp_supplier_statement_summary($pdo, $supplier_id);
	});
	$run('supplier_aggregate_count', function () use ($pdo, $supplier_id) {
		return count(epc_erp_supplier_statement_aggregate($pdo, $supplier_id));
	});
}

if ($simulate) {
	$run('formula_customer_10k_12k', function () {
		$advance = 10000.0;
		$open_so = 12000.0;
		$net = max(0, $open_so - $advance);
		return array(
			'advance_received' => $advance,
			'open_so_value' => $open_so,
			'net_receivable' => $net,
			'expected' => 2000.0,
			'pass' => abs($net - 2000.0) < 0.01,
		);
	});
	$run('formula_supplier_10k_8k', function () {
		$advance = 10000.0;
		$open_po = 8000.0;
		$net = $advance - $open_po;
		return array(
			'advance_paid' => $advance,
			'open_po_value' => $open_po,
			'net_advance_with_supplier' => $net,
			'expected' => 2000.0,
			'pass' => abs($net - 2000.0) < 0.01,
		);
	});
	$run('formula_invoice_close', function () {
		$advance = 10000.0;
		$invoiced = 12000.0;
		$paid_on_invoice = 2000.0;
		$net_before = max(0, $invoiced - $advance);
		$net_after = max(0, $invoiced - $advance - $paid_on_invoice);
		return array(
			'net_receivable_before_extra_payment' => $net_before,
			'closing_after_settlement' => $net_after,
			'pass' => abs($net_after) < 0.01,
		);
	});
}

$out['vat_advance_rows'] = (int)$pdo->query('SELECT COUNT(*) FROM `epc_uae_vat_advance`')->fetchColumn();
$out['vat_supplier_advance_rows'] = (int)$pdo->query('SELECT COUNT(*) FROM `epc_uae_vat_supplier_advance`')->fetchColumn();

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
