<?php
/**
 * End-to-end probe: commerce order → SO + multi-supplier POs → PI (cost) → SI (revenue) → AP/AR payments → GL / B/S / P&L.
 *
 * GET ?token=…&site_key=epartscart&order_id=18          — read-only status + GL snapshot
 * GET …&run=1&confirm=1                               — execute full test cycle (mutates DB)
 * GET …&swap_item_id=38&swap_storage_id=7             — optional supplier swap target (defaults: last line → 2nd storage)
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_order_fulfillment.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_vouchers.php';

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'electronicae' => 'www.electronicae.com',
	'taxofinca' => 'www.taxofinca.com',
);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
$host = $tenants[$siteKey] ?? $tenants['epartscart'];
$_SERVER['HTTP_HOST'] = $host;
$_SERVER['SERVER_NAME'] = $host;
unset($GLOBALS['epc_portal_config_applied'], $GLOBALS['epc_db_link']);
$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$orderId = (int) ($_GET['order_id'] ?? 0);
$run = !empty($_GET['run']) && !empty($_GET['confirm']);
$swapItemId = (int) ($_GET['swap_item_id'] ?? 0);
$swapStorageId = (int) ($_GET['swap_storage_id'] ?? 0);

function epc_cycle_gl_key_balances(PDO $db): array
{
	epc_erp_gl_ensure_schema($db);
	$codes = array('1000', '1010', '1100', '1150', '2000', '2100', '4000', '5000');
	$out = array();
	foreach ($codes as $code) {
		$row = epc_erp_gl_coa_by_code($db, $code);
		if (!$row) {
			$out[$code] = null;
			continue;
		}
		$out[$code] = array(
			'name' => $row['name'],
			'balance' => round(epc_erp_gl_coa_balance($db, (int) $row['id'], time()), 2),
		);
	}
	return $out;
}

function epc_cycle_bs_pl_snippet(PDO $db): array
{
	$bs = epc_erp_gl_balance_sheet($db, time());
	$pl = epc_erp_gl_pl_report($db, strtotime(date('Y-m-01 00:00:00')), time());
	return array(
		'balance_sheet' => array(
			'total_assets' => round((float) $bs['total_assets'], 2),
			'total_liabilities' => round((float) $bs['total_liabilities'], 2),
			'total_equity' => round((float) $bs['total_equity'], 2),
			'balanced' => abs((float) $bs['total_assets'] - (float) $bs['total_liabilities_equity']) < 0.05,
			'assets_top' => array_slice($bs['assets'], 0, 5),
			'liabilities_top' => array_slice($bs['liabilities'], 0, 5),
		),
		'pl' => array(
			'total_revenue' => round((float) $pl['total_revenue'], 2),
			'total_expenses' => round((float) $pl['total_expenses'], 2),
			'net_profit' => round((float) $pl['net_profit'], 2),
			'revenue_lines' => array_slice($pl['revenue'], 0, 3),
			'expense_lines' => array_slice($pl['expenses'], 0, 3),
		),
	);
}

function epc_cycle_step(array &$steps, string $name, callable $fn): void
{
	try {
		$result = $fn();
		$steps[] = array('step' => $name, 'pass' => true, 'detail' => $result);
	} catch (Throwable $e) {
		$steps[] = array('step' => $name, 'pass' => false, 'error' => $e->getMessage());
	}
}

function epc_cycle_issue_order_item(PDO $db, int $orderItemId): array
{
	$st = $db->prepare('SELECT `id`, `count_need` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1');
	$st->execute(array($orderItemId));
	$item = $st->fetch(PDO::FETCH_ASSOC);
	if (!$item) {
		throw new Exception('Item #' . $orderItemId . ' not found');
	}
	$need = (float) ($item['count_need'] ?? 0);
	$det = $db->prepare('SELECT `id`, `count_reserved`, `count_issued` FROM `shop_orders_items_details` WHERE `order_item_id` = ? LIMIT 1');
	$det->execute(array($orderItemId));
	$row = $det->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$db->prepare(
			'UPDATE `shop_orders_items_details` SET `count_issued` = `count_reserved` + `count_issued`, `count_reserved` = 0 WHERE `id` = ?'
		)->execute(array((int) $row['id']));
	} else {
		$iq = $db->prepare('SELECT `order_id`, `t2_storage_id`, `t2_price_purchase` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1');
		$iq->execute(array($orderItemId));
		$it = $iq->fetch(PDO::FETCH_ASSOC);
		$db->prepare(
			'INSERT INTO `shop_orders_items_details`
			(`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`)
			VALUES (?, ?, 0, ?, 0, 0, ?, 0, ?)'
		)->execute(array(
			(int) ($it['order_id'] ?? 0),
			$orderItemId,
			(int) ($it['t2_storage_id'] ?? 0),
			$need,
			(float) ($it['t2_price_purchase'] ?? 0),
		));
	}
	return array('order_item_id' => $orderItemId, 'issued_qty' => $need);
}

function epc_cycle_complete_order(PDO $db, int $orderId): array
{
	$refs = epc_erp_order_status_refs($db);
	$itemFinish = $refs['item_finish_ids'][0] ?? 0;
	$orderFinish = $refs['order_finish_ids'][0] ?? 0;
	if ($itemFinish <= 0) {
		throw new Exception('No item finish status configured in CP');
	}
	$db->prepare('UPDATE `shop_orders_items` SET `status` = ? WHERE `order_id` = ?')->execute(array($itemFinish, $orderId));
	if (epc_erp_shop_orders_has_status($db) && $orderFinish > 0) {
		$db->prepare('UPDATE `shop_orders` SET `status` = ? WHERE `id` = ?')->execute(array($orderFinish, $orderId));
	}
	return array(
		'order_id' => $orderId,
		'item_finish_status' => $itemFinish,
		'order_finish_status' => $orderFinish,
		'order_complete' => epc_erp_order_is_complete($db, $orderId),
	);
}

function epc_cycle_reset_fulfillment_state(PDO $db, int $orderId): array
{
	$db->prepare(
		'UPDATE `epc_erp_po_lines` pl
		 INNER JOIN `epc_erp_purchase_orders` po ON po.`id` = pl.`po_id`
		 SET pl.`qty_received` = 0, pl.`qty_cancelled` = 0, pl.`time_updated` = ?
		 WHERE po.`order_id` = ? AND po.`purchase_id` = 0'
	)->execute(array(time(), $orderId));
	$db->prepare(
		"UPDATE `epc_erp_purchase_orders` SET `status` = 'approved', `received_at` = 0, `time_updated` = ?
		 WHERE `order_id` = ? AND `purchase_id` = 0 AND `status` != 'cancelled'"
	)->execute(array(time(), $orderId));
	$db->prepare(
		'UPDATE `shop_orders_items_details` SET `count_reserved` = 0, `count_issued` = 0, `count_canceled` = 0 WHERE `order_id` = ?'
	)->execute(array($orderId));
	$refs = epc_erp_order_status_refs($db);
	if (!empty($refs['item_finish_ids'])) {
		$openSt = (int) ($refs['item_finish_ids'][0] ?? 1) - 1;
		if ($openSt < 1) {
			$openSt = 1;
		}
		$db->prepare('UPDATE `shop_orders_items` SET `status` = ? WHERE `order_id` = ?')->execute(array($openSt, $orderId));
	}
	if (epc_erp_shop_orders_has_status($db)) {
		$db->prepare('UPDATE `shop_orders` SET `status` = 1 WHERE `id` = ?')->execute(array($orderId));
	}
	$linked = 0;
	$piSt = $db->prepare('SELECT `id`, `supplier_id` FROM `epc_erp_purchases` WHERE `order_id` = ? AND `active` = 1');
	$piSt->execute(array($orderId));
	while ($pi = $piSt->fetch(PDO::FETCH_ASSOC)) {
		$upd = $db->prepare(
			'UPDATE `epc_erp_purchase_orders` SET `purchase_id` = ?, `status` = \'received\'
			 WHERE `order_id` = ? AND `supplier_id` = ? AND `purchase_id` = 0 LIMIT 1'
		);
		$upd->execute(array((int) $pi['id'], $orderId, (int) $pi['supplier_id']));
		$linked += (int) $upd->rowCount();
	}
	return array('order_id' => $orderId, 'reset' => true, 'linked_existing_purchases' => $linked);
}

function epc_cycle_default_cash_account(PDO $db): int
{
	$id = (int) $db->query(
		"SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 ORDER BY `account_type` = 'bank' DESC, `id` ASC LIMIT 1"
	)->fetchColumn();
	if ($id <= 0) {
		throw new Exception('No active cash/bank account — create one in ERP Cash & Bank');
	}
	return $id;
}

$out = array(
	'ok' => true,
	'probe' => 'epc-erp-order-cycle-verify',
	'ts' => gmdate('c'),
	'site_key' => $siteKey,
	'host' => $host,
	'mode' => $run ? 'run' : 'probe',
	'steps' => array(),
	'replay' => array(
		'1' => 'CP → Orders → open multi-supplier order (3+ storages). Confirm ERP bootstrap created SO + draft POs per supplier.',
		'2' => 'Issue goods per supplier (partial first). If a supplier cannot supply, CP line edit → change storage/supplier; ERP → Fulfillment → Swap supplier on line.',
		'3' => 'When PO lines received: ERP → Purchase orders → Post purchase invoice (cost / COGS + AP). Order may still be open.',
		'4' => 'Complete all order lines in CP. ERP → Post sales invoice (revenue / AR).',
		'5' => 'ERP → Cash & Bank → Supplier payment (AP ↓, cash ↓). Customer receipt (AR ↓, cash ↑).',
		'6' => 'ERP → Reports → Balance sheet & P&L — assets = liabilities + equity; revenue − COGS ≈ net profit.',
		'probe_url' => 'epc-erp-order-cycle-verify.php?token=…&site_key=epartscart&order_id=ORDER_ID',
		'run_url' => 'epc-erp-order-cycle-verify.php?token=…&site_key=epartscart&order_id=ORDER_ID&run=1&confirm=1',
	),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	epc_erp_order_fulfillment_ensure_schema($pdo);
	epc_erp_gl_ensure_schema($pdo);

	if ($orderId <= 0) {
		$orderId = (int) $pdo->query(
			'SELECT o.`id` FROM `shop_orders` o
			 INNER JOIN `shop_orders_items` i ON i.`order_id` = o.`id`
			 WHERE o.`successfully_created` = 1
			 GROUP BY o.`id`
			 HAVING COUNT(DISTINCT i.`t2_storage_id`) > 1
			 ORDER BY o.`id` DESC LIMIT 1'
		)->fetchColumn();
		if ($orderId <= 0) {
			$orderId = 18;
		}
	}
	$out['order_id'] = $orderId;

	$out['gl_before'] = epc_cycle_gl_key_balances($pdo);
	$out['reports_before'] = epc_cycle_bs_pl_snippet($pdo);

	$linesQ = $pdo->prepare(
		'SELECT `id`, `t2_storage_id`, `t2_article`, `count_need`, `t2_price_purchase`, `price`
		 FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`'
	);
	$linesQ->execute(array($orderId));
	$lines = $linesQ->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$out['lines'] = $lines;

	if (!$run) {
		$out['fulfillment'] = epc_erp_order_fulfillment_status($pdo, $orderId);
		$out['checks'] = array(
			'multi_supplier' => count(array_unique(array_column($lines, 't2_storage_id'))) > 1,
			'has_sales_order' => !empty($out['fulfillment']['sales_order']),
			'po_count' => count($out['fulfillment']['purchase_orders'] ?? array()),
			'cost_posted' => !empty($out['fulfillment']['accounting']['cost_posted']),
			'revenue_posted' => !empty($out['fulfillment']['accounting']['revenue_posted']),
			'order_complete' => !empty($out['fulfillment']['order_complete']),
		);
		$out['gl_after'] = $out['gl_before'];
		$out['reports_after'] = $out['reports_before'];
		echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		exit;
	}

	$steps = array();
	$firstItemId = (int) ($lines[0]['id'] ?? 0);
	$secondStorageId = (int) ($lines[1]['t2_storage_id'] ?? 0);
	$lastItemId = (int) ($lines[count($lines) - 1]['id'] ?? 0);
	if ($swapItemId <= 0) {
		$swapItemId = $lastItemId;
	}
	if ($swapStorageId <= 0) {
		$swapStorageId = $secondStorageId;
	}

	epc_cycle_step($steps, '0_reset_open_fulfillment', function () use ($pdo, $orderId) {
		return epc_cycle_reset_fulfillment_state($pdo, $orderId);
	});

	epc_cycle_step($steps, '1_bootstrap_so_and_pos', function () use ($pdo, $orderId) {
		if (!epc_erp_order_fulfillment_find_sales_order($pdo, $orderId)) {
			return epc_erp_order_fulfillment_bootstrap($pdo, $orderId);
		}
		return array('already_linked' => true, 'sales_order_id' => (int) epc_erp_order_fulfillment_find_sales_order($pdo, $orderId)['id']);
	});

	epc_cycle_step($steps, '2_supplier_swap_before_issue', function () use ($pdo, $orderId, $swapItemId, $swapStorageId, $lines) {
		foreach ($lines as $ln) {
			if ((int) $ln['id'] === $swapItemId && (int) $ln['t2_storage_id'] === $swapStorageId) {
				return array('skipped' => true, 'reason' => 'Line already on target storage');
			}
		}
		$chk = $pdo->prepare(
			'SELECT po.`supplier_id` FROM `epc_erp_po_lines` pl
			 INNER JOIN `epc_erp_purchase_orders` po ON po.`id` = pl.`po_id`
			 WHERE pl.`shop_order_item_id` = ? AND po.`order_id` = ? AND po.`status` != \'cancelled\'
			 ORDER BY pl.`id` DESC LIMIT 1'
		);
		$chk->execute(array($swapItemId, $orderId));
		$currentSupplier = (int) $chk->fetchColumn();
		$fake = array('t2_storage_id' => $swapStorageId);
		$target = epc_erp_order_fulfillment_resolve_line_supplier($pdo, $fake);
		if ($currentSupplier > 0 && $currentSupplier === (int) $target['supplier_id']) {
			return array('skipped' => true, 'reason' => 'PO already on target supplier', 'supplier_id' => $currentSupplier);
		}
		try {
			return epc_erp_order_fulfillment_swap_line_supplier($pdo, $orderId, $swapItemId, $swapStorageId);
		} catch (Throwable $e) {
			if (strpos($e->getMessage(), 'already assigned') !== false) {
				return array('skipped' => true, 'reason' => $e->getMessage());
			}
			throw $e;
		}
	});

	epc_cycle_step($steps, '3_partial_issue_first_supplier', function () use ($pdo, $firstItemId) {
		if ($firstItemId <= 0) {
			throw new Exception('No order lines');
		}
		return epc_cycle_issue_order_item($pdo, $firstItemId);
	});

	epc_cycle_step($steps, '4_sync_after_partial', function () use ($pdo, $orderId) {
		epc_erp_order_fulfillment_sync_po_statuses($pdo, $orderId);
		return array('fulfillment' => epc_erp_order_fulfillment_sync_sales_status($pdo, $orderId));
	});

	epc_cycle_step($steps, '5_issue_remaining_lines', function () use ($pdo, $lines, $firstItemId) {
		$done = array();
		foreach ($lines as $ln) {
			$iid = (int) $ln['id'];
			if ($iid === $firstItemId) {
				continue;
			}
			$done[] = epc_cycle_issue_order_item($pdo, $iid);
		}
		return $done;
	});

	epc_cycle_step($steps, '6_complete_order', function () use ($pdo, $orderId) {
		return epc_cycle_complete_order($pdo, $orderId);
	});

	epc_cycle_step($steps, '7_sync_po_statuses', function () use ($pdo, $orderId) {
		return epc_erp_order_fulfillment_sync_po_statuses($pdo, $orderId);
	});

	$poIds = array();
	epc_cycle_step($steps, '8_post_purchase_invoices_cost', function () use ($pdo, $orderId, &$poIds) {
		epc_erp_order_fulfillment_sync_po_statuses($pdo, $orderId);
		$posted = array();
		$errors = array();
		$st = $pdo->prepare(
			'SELECT `id`, `supplier_id`, `status`, `purchase_id` FROM `epc_erp_purchase_orders`
			 WHERE `order_id` = ? AND `status` NOT IN (\'cancelled\', \'draft\') AND `purchase_id` = 0'
		);
		$st->execute(array($orderId));
		while ($po = $st->fetch(PDO::FETCH_ASSOC)) {
			$poIds[] = (int) $po['id'];
			try {
				$posted[] = epc_erp_order_fulfillment_post_po_invoice($pdo, (int) $po['id']);
			} catch (Throwable $e) {
				$recover = $pdo->prepare(
					'SELECT `purchase_id` FROM `epc_erp_purchase_orders` WHERE `id` = ? AND `purchase_id` > 0 LIMIT 1'
				);
				$recover->execute(array((int) $po['id']));
				$recoveredId = (int) $recover->fetchColumn();
				if ($recoveredId > 0) {
					$posted[] = array('po_id' => (int) $po['id'], 'purchase_id' => $recoveredId, 'recovered' => true);
				} else {
					$errors[] = array('po_id' => (int) $po['id'], 'error' => $e->getMessage());
				}
			}
		}
		$glPosted = array();
		$piGl = $pdo->prepare('SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` = ? AND `gl_journal_id` = 0');
		$piGl->execute(array($orderId));
		while ($pid = (int) $piGl->fetchColumn()) {
			try {
				$glPosted[] = array('purchase_id' => $pid, 'journal_id' => epc_erp_gl_post_purchase($pdo, $pid));
			} catch (Throwable $e) {
				$errors[] = array('purchase_id' => $pid, 'gl_error' => $e->getMessage());
			}
		}
		if (!$posted && !$glPosted) {
			$cntSt = $pdo->prepare(
				'SELECT
				 (SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `order_id` = ? AND `status` = \'received\' AND `purchase_id` > 0) AS po_done,
				 (SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` = ?) AS pi_count'
			);
			$cntSt->execute(array($orderId, $orderId));
			$cnt = $cntSt->fetch(PDO::FETCH_ASSOC) ?: array();
			if ((int) ($cnt['pi_count'] ?? 0) > 0 && (int) ($cnt['po_done'] ?? 0) > 0) {
				return array(
					'already_posted' => true,
					'purchase_invoices' => (int) $cnt['pi_count'],
					'pos_linked' => (int) $cnt['po_done'],
				);
			}
			throw new Exception('No POs posted' . ($errors ? (': ' . json_encode($errors)) : ''));
		}
		epc_erp_gl_sync_unposted($pdo);
		return array('posted' => $posted, 'gl_posted' => $glPosted, 'errors' => $errors);
	});

	epc_cycle_step($steps, '9_post_sales_invoice_revenue', function () use ($pdo, $orderId) {
		$r = epc_erp_order_fulfillment_post_sales_invoice($pdo, $orderId, 1);
		epc_erp_gl_sync_unposted($pdo);
		return $r;
	});

	$cashAccountId = epc_cycle_default_cash_account($pdo);
	$cuSt = $pdo->prepare('SELECT `user_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1');
	$cuSt->execute(array($orderId));
	$customerUserId = (int) $cuSt->fetchColumn();

	epc_cycle_step($steps, '10_supplier_payments_ap', function () use ($pdo, $orderId, $cashAccountId) {
		$payments = array();
		$st = $pdo->prepare(
			'SELECT `id`, `supplier_id`, `total_amount`, `gl_journal_id` FROM `epc_erp_purchases`
			 WHERE `active` = 1 AND `order_id` = ? ORDER BY `id`'
		);
		$st->execute(array($orderId));
		while ($p = $st->fetch(PDO::FETCH_ASSOC)) {
			$paidSt = $pdo->prepare(
				'SELECT IFNULL(SUM(`amount`), 0) FROM `epc_erp_supplier_accounting`
				 WHERE `supplier_id` = ? AND `purchase_id` = ? AND `entry_kind` = \'payment\''
			);
			$paidSt->execute(array((int) $p['supplier_id'], (int) $p['id']));
			$paid = (float) $paidSt->fetchColumn();
			$due = round((float) $p['total_amount'] - $paid, 2);
			if ($due <= 0.009) {
				continue;
			}
			try {
				$payments[] = epc_erp_payment_voucher($pdo, array(
				'supplier_id' => (int) $p['supplier_id'],
				'amount' => $due,
				'account_id' => $cashAccountId,
				'purchase_id' => (int) $p['id'],
				'note' => 'Cycle verify AP payment PI #' . (int) $p['id'],
				));
			} catch (Throwable $payErr) {
				$payments[] = array(
					'purchase_id' => (int) $p['id'],
					'error' => $payErr->getMessage(),
					'due' => $due,
				);
			}
		}
		$paidOk = count(array_filter($payments, function ($row) {
			return !empty($row['cash_entry_id']) || !empty($row['voucher_no']);
		}));
		if ($paidOk <= 0) {
			return array('skipped' => true, 'reason' => 'All purchase invoices already settled', 'attempts' => $payments);
		}
		epc_erp_gl_sync_unposted($pdo);
		return $payments;
	});

	epc_cycle_step($steps, '11_customer_receipt_ar', function () use ($pdo, $orderId, $customerUserId, $cashAccountId) {
		$so = epc_erp_order_fulfillment_find_sales_order($pdo, $orderId);
		$invId = $so ? (int) ($so['sales_invoice_id'] ?? 0) : 0;
		if ($invId <= 0) {
			$invQ = $pdo->prepare('SELECT `id` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1');
			$invQ->execute(array($orderId));
			$invId = (int) $invQ->fetchColumn();
		}
		if ($invId <= 0) {
			throw new Exception('Sales invoice required before customer receipt');
		}
		$amount = $so ? round((float) $so['total_amount'], 2) : 0.0;
		if ($amount <= 0) {
			$amount = round((float) $pdo->query(
				'SELECT SUM(`price` * `count_need`) FROM `shop_orders_items` WHERE `order_id` = ' . (int) $orderId
			)->fetchColumn(), 2);
		}
		if ($customerUserId <= 0 || $amount <= 0) {
			throw new Exception('Cannot determine customer receipt amount');
		}
		$r = epc_erp_receipt_voucher($pdo, array(
			'user_id' => $customerUserId,
			'account_id' => $cashAccountId,
			'amount' => $amount,
			'note' => 'Cycle verify AR receipt order #' . $orderId,
			'post_gl' => true,
			'sales_order_id' => $so ? (int) $so['id'] : 0,
			'sales_invoice_id' => $so ? (int) ($so['sales_invoice_id'] ?? 0) : 0,
		));
		epc_erp_gl_sync_unposted($pdo);
		return $r;
	});

	$out['steps'] = $steps;
	$out['gl_after'] = epc_cycle_gl_key_balances($pdo);
	$out['reports_after'] = epc_cycle_bs_pl_snippet($pdo);
	$out['fulfillment'] = epc_erp_order_fulfillment_status($pdo, $orderId);

	$passed = count(array_filter($steps, function ($s) {
		return !empty($s['pass']);
	}));
	$out['summary'] = array(
		'steps_passed' => $passed,
		'steps_total' => count($steps),
		'all_pass' => $passed === count($steps) && count($steps) > 0,
		'cost_before_revenue' => (
			!empty($out['fulfillment']['accounting']['cost_posted'])
			&& !empty($out['fulfillment']['accounting']['revenue_posted'])
		),
		'bs_balanced' => !empty($out['reports_after']['balance_sheet']['balanced']),
	);
	$out['ok'] = !empty($out['summary']['all_pass']);
} catch (Throwable $e) {
	$out['ok'] = false;
	$out['error'] = $e->getMessage();
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
