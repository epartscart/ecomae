<?php
/**
 * ERP module — revenue, receivables, payables, cash/bank helpers.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';
require_once __DIR__ . '/epc_erp_gl.php';
require_once __DIR__ . '/epc_uae_vat.php';

function epc_erp_h($v)
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_erp_money($amount, $decimals = 2)
{
	return number_format((float)$amount, $decimals, '.', ',');
}

function epc_erp_admin_id()
{
	if (class_exists('DP_User')) {
		if (method_exists('DP_User', 'getAdminId')) {
			$aid = (int)DP_User::getAdminId();
			if ($aid > 0) {
				return $aid;
			}
		}
		if (method_exists('DP_User', 'getUserId')) {
			return (int)DP_User::getUserId();
		}
	}
	return 0;
}

function epc_erp_settlement_kinds()
{
	return array(
		'adjustment' => 'Adjustment / correction',
		'settlement' => 'Settlement (non-cash close-off)',
		'write_off' => 'Write-off',
	);
}

function epc_erp_supplier_entry_kind_label($kind)
{
	$labels = array(
		'invoice' => 'Purchase invoice (+payable)',
		'payment' => 'Payment (−payable)',
		'adjustment' => 'Adjustment',
		'settlement' => 'Settlement',
		'write_off' => 'Write-off',
	);
	$kind = (string)$kind;
	return isset($labels[$kind]) ? $labels[$kind] : $kind;
}

function epc_erp_ensure_accounting_codes(PDO $db)
{
	$codes = array(
		array('key' => 'epc_erp_ar_credit', 'income' => 1, 'name' => 'ERP — customer credit (settlement/adjustment)'),
		array('key' => 'epc_erp_ar_debit', 'income' => 0, 'name' => 'ERP — customer debit (settlement/adjustment)'),
	);
	foreach ($codes as $c) {
		$st = $db->prepare('SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1');
		$st->execute(array($c['key']));
		if ((int)$st->fetchColumn() > 0) {
			continue;
		}
		$db->prepare('INSERT INTO `shop_accounting_codes` (`income`, `name`, `manual_available`, `key`) VALUES (?, ?, 1, ?)')
			->execute(array((int)$c['income'], $c['name'], $c['key']));
	}
}

function epc_erp_accounting_code_id(PDO $db, $key)
{
	$st = $db->prepare('SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1');
	$st->execute(array((string)$key));
	return (int)$st->fetchColumn();
}

function epc_erp_item_status_exclusion(PDO $db)
{
	$not_count = array();
	try {
		$q = $db->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0;');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$not_count[] = (int)$row['id'];
		}
	} catch (Exception $e) {
	}
	$where_and = '';
	$where_plain = '1=1';
	foreach ($not_count as $sid) {
		$where_and .= ' AND `status` != ' . (int)$sid;
		$where_plain .= ' AND `status` != ' . (int)$sid;
	}
	return array(
		'not_count_ids' => $not_count,
		'where_and' => $where_and,
		'where_plain' => $where_plain,
	);
}

function epc_erp_shop_orders_has_status(PDO $db)
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}
	try {
		$st = $db->query("SHOW COLUMNS FROM `shop_orders` LIKE 'status'");
		$cache = (bool)$st->fetch(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		$cache = false;
	}
	return $cache;
}

function epc_erp_order_status_name_sql(PDO $db)
{
	if (epc_erp_shop_orders_has_status($db)) {
		return '(SELECT `name` FROM `shop_orders_statuses_ref` WHERE `id` = `shop_orders`.`status` LIMIT 1)';
	}
	return '(SELECT `name` FROM `shop_orders_items_statuses_ref` WHERE `id` = (SELECT `status` FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` ORDER BY `id` DESC LIMIT 1) LIMIT 1)';
}

function epc_erp_incomplete_order_exclude_sql(PDO $db)
{
	$cmp = epc_erp_order_complete_sql($db);
	if ($cmp['order_complete_expr'] === '0') {
		return '';
	}
	$incomplete = '(SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 AND NOT (' . $cmp['order_complete_expr'] . '))';
	return ' AND NOT (
		(`order_id` > 0 AND `order_id` IN ' . $incomplete . ')
		OR (`purchase_id` > 0 AND `purchase_id` IN (SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` > 0 AND `order_id` IN ' . $incomplete . '))
	)';
}

function epc_erp_order_status_refs(PDO $db)
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}
	$order_finish = array();
	$item_finish = array();
	try {
		$q = $db->query('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$order_finish[] = (int)$row['id'];
		}
		$q = $db->query('SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$item_finish[] = (int)$row['id'];
		}
	} catch (Exception $e) {
	}
	$cache = array(
		'order_finish_ids' => $order_finish,
		'item_finish_ids' => $item_finish,
	);
	return $cache;
}

function epc_erp_order_is_complete(PDO $db, $order_id)
{
	$order_id = (int)$order_id;
	if ($order_id <= 0) {
		return false;
	}
	$refs = epc_erp_order_status_refs($db);
	if (epc_erp_shop_orders_has_status($db) && !empty($refs['order_finish_ids'])) {
		$in = implode(',', array_map('intval', $refs['order_finish_ids']));
		$q = $db->prepare('SELECT `id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 AND `status` IN (' . $in . ') LIMIT 1');
		$q->execute(array($order_id));
		return (bool)$q->fetchColumn();
	}
	if (empty($refs['item_finish_ids'])) {
		return false;
	}
	$ex = epc_erp_item_status_exclusion($db);
	$item_finish_in = implode(',', array_map('intval', $refs['item_finish_ids']));
	$q = $db->prepare(
		'SELECT
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ?' . $ex['where_and'] . ') AS total_items,
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ?' . $ex['where_and'] . ' AND `status` NOT IN (' . $item_finish_in . ')) AS open_items'
	);
	$q->execute(array($order_id, $order_id));
	$row = $q->fetch(PDO::FETCH_ASSOC);
	return $row && (int)$row['total_items'] > 0 && (int)$row['open_items'] === 0;
}

function epc_erp_order_complete_sql(PDO $db)
{
	$refs = epc_erp_order_status_refs($db);
	$order_finish = $refs['order_finish_ids'];
	$item_finish = $refs['item_finish_ids'];
	$item_finish_where = empty($item_finish)
		? ' AND 1=0'
		: (' AND `status` IN (' . implode(',', array_map('intval', $item_finish)) . ')');

	if (epc_erp_shop_orders_has_status($db) && !empty($order_finish)) {
		$order_in = implode(',', array_map('intval', $order_finish));
		return array(
			'order_complete_expr' => '`shop_orders`.`status` IN (' . $order_in . ')',
			'item_finish_where' => $item_finish_where,
			'order_where' => ' AND `shop_orders`.`status` IN (' . $order_in . ')',
			'order_finish_ids' => $order_finish,
		);
	}

	if (empty($item_finish)) {
		return array(
			'order_complete_expr' => '0',
			'item_finish_where' => ' AND 1=0',
			'order_where' => ' AND 1=0',
			'order_finish_ids' => array(),
		);
	}

	$ex = epc_erp_item_status_exclusion($db);
	$item_finish_in = implode(',', array_map('intval', $item_finish));
	$order_complete_expr = '(
		(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`' . $ex['where_and'] . ') > 0
		AND (SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`' . $ex['where_and'] . ' AND `status` NOT IN (' . $item_finish_in . ')) = 0
	)';
	return array(
		'order_complete_expr' => $order_complete_expr,
		'item_finish_where' => $item_finish_where,
		'order_where' => '',
		'order_finish_ids' => $item_finish,
	);
}

function epc_erp_assert_order_complete(PDO $db, $order_id, $context = 'ERP posting')
{
	if (!epc_erp_order_is_complete($db, (int)$order_id)) {
		throw new Exception($context . ' requires order #' . (int)$order_id . ' to be in Completed status (all lines finished in CP).');
	}
}

function epc_erp_order_sum_sql(PDO $db)
{
	$ex = epc_erp_item_status_exclusion($db);
	$cmp = epc_erp_order_complete_sql($db);
	$item_where = $ex['where_and'] . $cmp['item_finish_where'];
	$item_where_purchase = $ex['where_plain'] . $cmp['item_finish_where'];
	$sale_sub = '(SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`' . $item_where . ')';
	$purchase_sub = '(SELECT SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND ' . $item_where_purchase . ')';
	$sale = 'CAST(IF(' . $cmp['order_complete_expr'] . ', IFNULL(' . $sale_sub . ', 0), 0) AS DECIMAL(20,2))';
	$purchase = 'CAST(IF(' . $cmp['order_complete_expr'] . ', IFNULL(' . $purchase_sub . ', 0), 0) AS DECIMAL(20,2))';
	$paid_issue = 'IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = `shop_orders`.`id`), 0)';
	$paid_income = 'IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = `shop_orders`.`id`), 0)';
	$paid = 'CAST(IF(' . $cmp['order_complete_expr'] . ', (' . $paid_issue . ' - ' . $paid_income . '), 0) AS DECIMAL(20,2))';
	// Do NOT multiply stored line prices by VAT here.
	// B2C carts already store VAT-inclusive unit prices; a SQL *1.05 would double-tax.
	// Exact FTA split (ex VAT / VAT / incl) is applied in PHP via epc_uae_customer_vat_shop_order_totals().
	$due = 'CAST(IF(' . $cmp['order_complete_expr'] . ', GREATEST((' . $sale . ') - (' . $paid . '), 0), 0) AS DECIMAL(20,2))';
	$sale_vat = 'CAST(0 AS DECIMAL(20,2))';
	$sale_incl = 'CAST(IF(' . $cmp['order_complete_expr'] . ', (' . $sale . '), 0) AS DECIMAL(20,2))';
	return array(
		'sale' => $sale,
		'purchase' => $purchase,
		'paid' => $paid,
		'due' => $due,
		'sale_vat' => $sale_vat,
		'sale_incl_vat' => $sale_incl,
		'profit' => 'CAST((' . $sale . ' - IFNULL(' . $purchase . ', 0)) AS DECIMAL(20,2))',
		'order_complete_expr' => $cmp['order_complete_expr'],
		'order_where' => $cmp['order_where'],
	);
}

function epc_erp_payable_balance(PDO $db)
{
	epc_erp_ensure_schema($db);
	$exclude = epc_erp_incomplete_order_exclude_sql($db);
	$payable_up = (float)$db->query(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 1' . $exclude
	)->fetchColumn();
	$payable_down = (float)$db->query(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_supplier_accounting` WHERE `active` = 1 AND `is_credit` = 0' . $exclude
	)->fetchColumn();
	return $payable_up - $payable_down;
}

function epc_erp_supplier_balance_sql(PDO $db)
{
	return epc_erp_incomplete_order_exclude_sql($db);
}

function epc_erp_dashboard(PDO $db, $date_from = 0, $date_to = 0, $light = false)
{
	// Per-request memo — erp_main + intel KPIs + previous-period trends all call this.
	static $memo = array();
	$lightFlag = $light ? '1' : '0';
	$fromKey = (int) $date_from;
	$toKey = (int) $date_to;
	$mkey = spl_object_id($db) . ':' . $fromKey . ':' . $toKey . ':' . $lightFlag;
	if (isset($memo[$mkey])) {
		return $memo[$mkey];
	}

	epc_erp_ensure_schema($db);
	if ($date_to <= 0) {
		$date_to = time();
	}
	if ($date_from <= 0) {
		$date_from = strtotime(date('Y-m-01 00:00:00'));
	}
	// Re-key after normalising defaults so later callers with 0,0 hit the cache.
	$mkey = spl_object_id($db) . ':' . (int) $date_from . ':' . (int) $date_to . ':' . $lightFlag;
	if (isset($memo[$mkey])) {
		return $memo[$mkey];
	}
	$sql = epc_erp_order_sum_sql($db);
	$q = $db->prepare(
		'SELECT
			SUM(IF(' . $sql['order_complete_expr'] . ', 1, 0)) AS order_count,
			IFNULL(SUM(' . $sql['purchase'] . '), 0) AS purchase_ex_vat
		FROM `shop_orders`
		WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?'
	);
	$q->execute(array($date_from, $date_to));
	$row = $q->fetch(PDO::FETCH_ASSOC) ?: array();

	// FTA totals (ex VAT / VAT / incl) — never treat inclusive B2C line prices as ex-VAT then *1.05.
	$revRows = epc_erp_revenue_report($db, $date_from, $date_to, 5000);
	$revenueEx = 0.0;
	$receivableDue = 0.0;
	foreach ($revRows as $rr) {
		if (empty($rr['order_complete'])) {
			continue;
		}
		$revenueEx += (float) ($rr['sale_ex_vat'] ?? 0);
		$receivableDue += (float) ($rr['due_amount'] ?? 0);
	}
	$purchaseEx = (float) ($row['purchase_ex_vat'] ?? 0);
	$profitEx = round($revenueEx - $purchaseEx, 2);

	$receivable = (float)$db->query(
		'SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1'
	)->fetchColumn();
	$customer_paid_out = (float)$db->query(
		'SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0'
	)->fetchColumn();
	$customer_balance = $receivable - $customer_paid_out;

	$payable_balance = epc_erp_payable_balance($db);

	$cash_total = epc_erp_cash_bank_total($db);

	$result = array(
		'date_from' => $date_from,
		'date_to' => $date_to,
		'order_count' => (int)($row['order_count'] ?? 0),
		'revenue_ex_vat' => round($revenueEx, 2),
		'purchase_ex_vat' => $purchaseEx,
		'profit_ex_vat' => $profitEx,
		'receivable_due_orders' => round($receivableDue, 2),
		'customer_ledger_balance' => $customer_balance,
		'payable_balance' => $payable_balance,
		'cash_bank_total' => $cash_total,
		'vat_5_on_revenue' => 0.0,
		'vat_output' => 0.0,
		'vat_input' => 0.0,
		'vat_net_payable' => 0.0,
		'vat_net_status' => '',
		'sales_incl_vat' => 0.0,
	);

	// Light mode (previous-period trend arrows): skip VAT return + command-center tiles.
	if (!$light) {
		$vat_report = epc_uae_vat_return_report($db, $date_from, $date_to);
		$result['vat_5_on_revenue'] = (float)$vat_report['output_vat'];
		$result['vat_output'] = (float)$vat_report['output_vat'];
		$result['vat_input'] = (float)$vat_report['input_vat'];
		$result['vat_net_payable'] = (float)$vat_report['net_vat_payable'];
		$result['vat_net_status'] = (string)$vat_report['net_status'];
		$result['sales_incl_vat'] = (float)$vat_report['sales_incl_vat'];

		// Merge command center KPI tiles into dashboard (P0 #7 — correct layer)
		$ccFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_command_center.php';
		if (file_exists($ccFile)) {
			require_once $ccFile;
			if (function_exists('epc_erp_cc_kpi_tiles')) {
				$result['kpi_tiles'] = epc_erp_cc_kpi_tiles($db, $date_from, $date_to);
			}
			if (function_exists('epc_erp_cc_approval_queue')) {
				$result['approval_queue'] = epc_erp_cc_approval_queue($db);
			}
		}
	}

	$memo[$mkey] = $result;
	return $result;
}

function epc_erp_cash_bank_total(PDO $db)
{
	epc_erp_ensure_schema($db);
	// Single batched balance query instead of N+1 per cash/bank account.
	try {
		$sql = "SELECT IFNULL(SUM(a.`opening_balance`
				+ IFNULL(x.in_amt, 0) - IFNULL(x.out_amt, 0)), 0) AS total
			FROM `epc_erp_cash_bank_accounts` a
			LEFT JOIN (
				SELECT `account_id`,
					SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END) AS in_amt,
					SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END) AS out_amt
				FROM `epc_erp_cash_bank_entries`
				WHERE `active` = 1
				GROUP BY `account_id`
			) x ON x.`account_id` = a.`id`
			WHERE a.`active` = 1";
		return (float) $db->query($sql)->fetchColumn();
	} catch (Throwable $e) {
		$total = 0.0;
		$accounts = epc_erp_list_cash_accounts($db);
		foreach ($accounts as $acc) {
			$total += (float)$acc['balance'];
		}
		return $total;
	}
}

function epc_erp_list_cash_accounts(PDO $db)
{
	epc_erp_ensure_schema($db);
	$rows = $db->query(
		'SELECT * FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1 ORDER BY `account_type`, `name`'
	)->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$row['balance'] = epc_erp_account_balance($db, (int)$row['id']);
	}
	unset($row);
	return $rows;
}

function epc_erp_account_balance(PDO $db, $account_id)
{
	$acc = $db->prepare('SELECT `opening_balance` FROM `epc_erp_cash_bank_accounts` WHERE `id` = ? LIMIT 1');
	$acc->execute(array((int)$account_id));
	$opening = (float)$acc->fetchColumn();
	$stmt_in = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_bank_entries` WHERE `account_id` = ? AND `active` = 1 AND `direction` = 1'
	);
	$stmt_in->execute(array((int)$account_id));
	$in = (float)$stmt_in->fetchColumn();
	$stmt_out = $db->prepare(
		'SELECT IFNULL(SUM(`amount`),0) FROM `epc_erp_cash_bank_entries` WHERE `account_id` = ? AND `active` = 1 AND `direction` = 0'
	);
	$stmt_out->execute(array((int)$account_id));
	$out = (float)$stmt_out->fetchColumn();
	return $opening + $in - $out;
}

function epc_erp_revenue_report(PDO $db, $date_from, $date_to, $limit = 200)
{
	$sql = epc_erp_order_sum_sql($db);
	$status_col = epc_erp_shop_orders_has_status($db) ? '`shop_orders`.`status` AS order_status,' : '0 AS order_status,';
	$q = $db->prepare(
		'SELECT `shop_orders`.`id`, `shop_orders`.`time`, `shop_orders`.`user_id`, `shop_orders`.`paid`, `shop_orders`.`paid_type`, ' . $status_col . '
			' . $sql['order_complete_expr'] . ' AS order_complete,
			' . $sql['sale'] . ' AS sale_ex_vat,
			' . $sql['purchase'] . ' AS purchase_ex_vat,
			' . $sql['profit'] . ' AS profit_ex_vat,
			' . $sql['sale_vat'] . ' AS sale_vat,
			' . $sql['sale_incl_vat'] . ' AS sale_incl_vat,
			' . $sql['paid'] . ' AS paid_amount,
			' . $sql['due'] . ' AS due_amount,
			(SELECT `email` FROM `users` WHERE `users`.`user_id` = `shop_orders`.`user_id` LIMIT 1) AS customer_email,
			' . epc_erp_order_status_name_sql($db) . ' AS order_status_name
		FROM `shop_orders`
		WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?
		ORDER BY `time` DESC
		LIMIT ' . (int)$limit
	);
	$q->execute(array($date_from, $date_to));
	$rows = $q->fetchAll(PDO::FETCH_ASSOC);
	return epc_erp_revenue_report_apply_customer_vat($db, $rows);
}

/**
 * Replace blind sale*VAT with FTA e-invoice totals (no double VAT).
 *
 * @param list<array<string,mixed>> $rows
 * @return list<array<string,mixed>>
 */
function epc_erp_revenue_report_apply_customer_vat(PDO $db, array $rows): array
{
	require_once __DIR__ . '/epc_uae_customer_vat.php';
	foreach ($rows as &$row) {
		$orderId = (int) ($row['id'] ?? 0);
		if ($orderId <= 0 || empty($row['order_complete'])) {
			continue;
		}
		$paid = round((float) ($row['paid_amount'] ?? 0), 2);
		$totals = epc_uae_customer_vat_shop_order_totals($db, $orderId);
		$row['sale_ex_vat'] = (float) $totals['line_net'];
		$row['sale_vat'] = (float) $totals['vat_amount'];
		$row['sale_incl_vat'] = (float) $totals['gross'];
		$row['due_amount'] = max(0, round(((float) $totals['amount_due_base']) - $paid, 2));
		$purchase = round((float) ($row['purchase_ex_vat'] ?? 0), 2);
		$row['profit_ex_vat'] = round(((float) $row['sale_ex_vat']) - $purchase, 2);
	}
	unset($row);
	return $rows;
}

function epc_erp_receivables(PDO $db, $limit = 300)
{
	$sql = epc_erp_order_sum_sql($db);
	$cmp = epc_erp_order_complete_sql($db);
	$order_due_sub = 'IFNULL((SELECT SUM(' . $sql['due'] . ') FROM `shop_orders` WHERE `user_id` = `users`.`user_id` AND `successfully_created` = 1), 0)';
	$complete_count_sub = 'IFNULL((SELECT COUNT(*) FROM `shop_orders` WHERE `user_id` = `users`.`user_id` AND `successfully_created` = 1' . $cmp['order_where'] . '), 0)';
	$q = $db->query(
		'SELECT
			`users`.`user_id`,
			`users`.`email`,
			IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = `users`.`user_id` AND `active` = 1 AND `income` = 1), 0)
			- IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = `users`.`user_id` AND `active` = 1 AND `income` = 0), 0) AS balance,
			' . $order_due_sub . ' AS order_receivable_due,
			(SELECT COUNT(*) FROM `shop_orders` WHERE `user_id` = `users`.`user_id` AND `successfully_created` = 1) AS order_count,
			' . $complete_count_sub . ' AS complete_order_count
		FROM `users`
		HAVING balance != 0 OR order_count > 0 OR order_receivable_due != 0
		ORDER BY order_receivable_due DESC, balance DESC
		LIMIT ' . (int)$limit
	);
	return $q->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_customer_statement(PDO $db, $user_id, $limit = 100, $from = 0, $to = 0)
{
	require_once __DIR__ . '/epc_erp_vouchers.php';
	return epc_erp_customer_statement_aggregate($db, (int) $user_id, (int) $from, (int) $to, (int) $limit);
}

function epc_erp_list_suppliers(PDO $db)
{
	epc_erp_ensure_schema($db);
	$exclude = epc_erp_supplier_balance_sql($db);
	$rows = $db->query(
		'SELECT `epc_erp_suppliers`.*,
			IFNULL((SELECT SUM(`amount`) FROM `epc_erp_supplier_accounting` WHERE `supplier_id` = `epc_erp_suppliers`.`id` AND `active` = 1 AND `is_credit` = 1' . $exclude . '), 0)
			- IFNULL((SELECT SUM(`amount`) FROM `epc_erp_supplier_accounting` WHERE `supplier_id` = `epc_erp_suppliers`.`id` AND `active` = 1 AND `is_credit` = 0' . $exclude . '), 0) AS balance
		FROM `epc_erp_suppliers`
		WHERE `active` = 1
		ORDER BY `name`'
	)->fetchAll(PDO::FETCH_ASSOC);
	return $rows;
}

function epc_erp_list_purchases(PDO $db, $limit = 200)
{
	epc_erp_ensure_schema($db);
	$q = $db->query(
		'SELECT p.*, s.name AS supplier_name
		FROM `epc_erp_purchases` p
		INNER JOIN `epc_erp_suppliers` s ON s.id = p.supplier_id
		WHERE p.active = 1
		ORDER BY p.purchase_date DESC, p.id DESC
		LIMIT ' . (int)$limit
	);
	return $q->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_supplier_statement(PDO $db, $supplier_id, $limit = 100, $from = 0, $to = 0)
{
	require_once __DIR__ . '/epc_erp_advances.php';
	return epc_erp_supplier_statement_aggregate($db, (int) $supplier_id, (int) $from, (int) $to, (int) $limit);
}

function epc_erp_list_cash_entries(PDO $db, $account_id = 0, $limit = 200)
{
	epc_erp_ensure_schema($db);
	$sql = 'SELECT e.*, a.name AS account_name, a.account_type
		FROM `epc_erp_cash_bank_entries` e
		INNER JOIN `epc_erp_cash_bank_accounts` a ON a.id = e.account_id
		WHERE e.active = 1';
	$params = array();
	if ($account_id > 0) {
		$sql .= ' AND e.account_id = ?';
		$params[] = (int)$account_id;
	}
	$sql .= ' ORDER BY e.time DESC, e.id DESC LIMIT ' . (int)$limit;
	$stmt = $db->prepare($sql);
	$stmt->execute($params);
	return $stmt->fetchAll(PDO::FETCH_ASSOC);
}

function epc_erp_sync_suppliers_from_storages(PDO $db)
{
	epc_erp_ensure_schema($db);
	$storages = $db->query('SELECT `id`, `name`, `short_name` FROM `shop_storages`')->fetchAll(PDO::FETCH_ASSOC);
	$created = 0;
	$ins = $db->prepare(
		'INSERT INTO `epc_erp_suppliers` (`storage_id`, `name`, `time_created`)
		SELECT ?, ?, ?
		FROM DUAL
		WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_suppliers` WHERE `storage_id` = ? AND `active` = 1)'
	);
	$now = time();
	foreach ($storages as $s) {
		$name = trim((string)$s['name']);
		if ($name === '') {
			$name = trim((string)$s['short_name']);
		}
		if ($name === '') {
			continue;
		}
		$ins->execute(array((int)$s['id'], $name, $now, (int)$s['id']));
		$created += (int)$ins->rowCount();
	}
	return $created;
}

function epc_erp_create_supplier(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	$country = epc_uae_vat_normalize_country($data['country_code'] ?? 'AE');
	$vatReg = !isset($data['vat_registered']) || $data['vat_registered'] === '' || $data['vat_registered'] === '1' || $data['vat_registered'] === 1;
	$onHold = trim((string)($data['on_hold'] ?? 'no'));
	if (!in_array($onHold, array('no', 'invoice', 'payment', 'all'), true)) {
		$onHold = 'no';
	}
	$stmt = $db->prepare(
		'INSERT INTO `epc_erp_suppliers`
		(`storage_id`, `name`, `contact_email`, `contact_phone`, `trn`, `currency_code`, `country_code`, `vat_registered`,
		 `vendor_account`, `vendor_group`, `legal_entity_id`, `business_unit_id`, `registration_number`,
		 `payment_terms`, `payment_method`, `delivery_terms`, `delivery_mode`, `credit_limit`, `on_hold`, `tax_exempt`,
		 `bank_name`, `bank_account_number`, `iban`, `swift_bic`, `contact_person`, `website`,
		 `address`, `city`, `state_region`, `postal_code`, `notes`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$stmt->execute(array(
		!empty($data['storage_id']) ? (int)$data['storage_id'] : null,
		trim((string)$data['name']),
		trim((string)($data['contact_email'] ?? '')),
		trim((string)($data['contact_phone'] ?? '')),
		trim((string)($data['trn'] ?? '')),
		trim((string)($data['currency_code'] ?? 'AED')),
		$country,
		$vatReg ? 1 : 0,
		trim((string)($data['vendor_account'] ?? '')),
		trim((string)($data['vendor_group'] ?? '')),
		(int)($data['legal_entity_id'] ?? 0),
		(int)($data['business_unit_id'] ?? 0),
		trim((string)($data['registration_number'] ?? '')),
		trim((string)($data['payment_terms'] ?? '')),
		trim((string)($data['payment_method'] ?? '')),
		trim((string)($data['delivery_terms'] ?? '')),
		trim((string)($data['delivery_mode'] ?? '')),
		round((float)($data['credit_limit'] ?? 0), 2),
		$onHold,
		!empty($data['tax_exempt']) ? 1 : 0,
		trim((string)($data['bank_name'] ?? '')),
		trim((string)($data['bank_account_number'] ?? '')),
		trim((string)($data['iban'] ?? '')),
		trim((string)($data['swift_bic'] ?? '')),
		trim((string)($data['contact_person'] ?? '')),
		trim((string)($data['website'] ?? '')),
		trim((string)($data['address'] ?? '')),
		trim((string)($data['city'] ?? '')),
		trim((string)($data['state_region'] ?? '')),
		trim((string)($data['postal_code'] ?? '')),
		trim((string)($data['notes'] ?? '')),
		time(),
	));
	return (int)$db->lastInsertId();
}

function epc_erp_create_purchase(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	// DDL (CREATE TABLE) implicitly commits in MySQL — run all schema ensures
	// before beginTransaction() or commit() throws "There is no active transaction".
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	if (!empty($data['receive_inventory']) && (!empty($data['inventory_lines']) || !empty($data['inventory_csv']))) {
		require_once __DIR__ . '/epc_erp_inventory.php';
		epc_erp_inventory_ensure_schema($db);
	}
	$linked_order_id = (int)($data['order_id'] ?? 0);
	if ($linked_order_id > 0 && empty($data['allow_open_order'])) {
		epc_erp_assert_order_complete($db, $linked_order_id, 'Purchase invoice for order');
	}
	$supplier_id = (int)($data['supplier_id'] ?? 0);
	if ($supplier_id <= 0) {
		throw new Exception('Supplier is required');
	}
	if (!$db->beginTransaction()) {
		throw new Exception('Transaction start failed');
	}
	try {
		$amount_ex = round((float)$data['amount_ex_vat'], 2);
		require_once __DIR__ . '/epc_tax_toolkit.php';
		$vatCalc = epc_tax_toolkit_purchase_amounts($db, $amount_ex, $supplier_id, array(
			'import' => !empty($data['is_import']) || !empty($data['cross_border']),
		));
		$vat = round((float)$vatCalc['vat_amount'], 2);
		$total = round((float)$vatCalc['total_amount'], 2);
		if (!empty($vatCalc['import_duty'])) {
			$total = round((float)($vatCalc['total_with_duty'] ?? $total), 2);
		}
		require_once __DIR__ . '/epc_uae_tax_compliance.php';
		$vatTreatment = epc_uae_vat_purchase_treatment($db, $supplier_id, trim((string)($data['uae_vat_treatment'] ?? 'standard')));
		$supplierRow = epc_uae_vat_get_supplier($db, $supplier_id);
		$purchaseVatCheck = epc_uae_vat_apply_to_voucher($db, 'purchase', array(
			'amount_ex_vat' => $amount_ex,
			'vat_amount' => $vat,
			'total_amount' => $total,
			'vat_applicable' => !empty($vatCalc['vat_applicable']),
			'uae_vat_treatment' => $vatTreatment,
			'supplier_id' => $supplier_id,
			'supplier' => $supplierRow,
		));
		if (!$purchaseVatCheck['ok']) {
			throw new Exception(implode('; ', $purchaseVatCheck['errors']));
		}
		$pdate = !empty($data['purchase_date']) ? (int)$data['purchase_date'] : time();
		$stmt = $db->prepare(
			'INSERT INTO `epc_erp_purchases`
			(`supplier_id`, `order_id`, `storage_id`, `invoice_number`, `purchase_date`, `amount_ex_vat`, `vat_amount`, `total_amount`, `vat_applicable`, `vat_rate`, `uae_vat_treatment`, `uae_tax_legislation_ref`, `status`, `note`, `admin_id`, `time_created`)
			VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
		);
		$invNo = trim((string)($data['invoice_number'] ?? ''));
		if ($invNo === '') {
			$invNo = epc_erp_next_voucher_no($db, 'PI');
		}
		$stmt->execute(array(
			$supplier_id,
			(int)($data['order_id'] ?? 0),
			(int)($data['storage_id'] ?? 0),
			$invNo,
			$pdate,
			$amount_ex,
			$vat,
			$total,
			!empty($vatCalc['vat_applicable']) ? 1 : 0,
			(float)$vatCalc['vat_rate'],
			$vatTreatment,
			(string)($purchaseVatCheck['legislation_ref'] ?? 'vat-decree-8-2017'),
			trim((string)($data['status'] ?? 'confirmed')),
			trim((string)($data['note'] ?? '')),
			epc_erp_admin_id(),
			time(),
		));
		$purchase_id = (int)$db->lastInsertId();
		$db->prepare('UPDATE `epc_erp_purchases` SET `voucher_no` = ? WHERE `id` = ?')->execute(array($invNo, $purchase_id));
		$invPosted = 0;
		if (!empty($data['receive_inventory']) && (!empty($data['inventory_lines']) || !empty($data['inventory_csv']))) {
			require_once __DIR__ . '/epc_erp_inventory.php';
			$invRes = epc_erp_inventory_receive_purchase($db, $purchase_id, $data);
			$invPosted = (int)($invRes['posted'] ?? 0);
		}
		$acc = $db->prepare(
			'INSERT INTO `epc_erp_supplier_accounting`
			(`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`, `order_id`, `reference`, `note`, `admin_id`, `entry_kind`)
			VALUES (?, ?, 1, ?, ?, ?, ?, ?, ?, ?)'
		);
		$acc->execute(array(
			$supplier_id,
			$pdate,
			$total,
			$purchase_id,
			(int)($data['order_id'] ?? 0),
			$invNo,
			'Purchase invoice',
			epc_erp_admin_id(),
			'invoice',
		));
		if ($db->inTransaction()) {
			$db->commit();
		}
		$jid = 0;
		try {
			$jid = epc_erp_gl_post_purchase($db, $purchase_id);
		} catch (Exception $e) {
		}
		return $purchase_id;
	} catch (Exception $e) {
		if ($db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
}

function epc_erp_supplier_payment(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_advances.php';
	epc_erp_advances_ensure_schema($db);
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	if (!$db->beginTransaction()) {
		throw new Exception('Transaction start failed');
	}
	try {
		$supplier_id = (int)$data['supplier_id'];
		$amount = round((float)$data['amount'], 2);
		$account_id = (int)$data['account_id'];
		$time = !empty($data['time']) ? (int)$data['time'] : time();
		$isAdvance = !empty($data['is_advance']);
		$purchaseOrderId = (int)($data['purchase_order_id'] ?? 0);
		if ($amount <= 0 || $supplier_id <= 0 || $account_id <= 0) {
			throw new Exception('Invalid payment data');
		}
		$pvNo = trim((string)($data['reference'] ?? ''));
		if ($pvNo === '' || strpos($pvNo, 'PV-') !== 0) {
			$pvNo = epc_erp_next_voucher_no($db, 'PV');
		}
		$note = trim((string)($data['note'] ?? ($isAdvance ? 'Supplier advance payment' : 'Supplier payment')));
		$entry = $db->prepare(
			'INSERT INTO `epc_erp_cash_bank_entries`
			(`account_id`, `time`, `entry_type`, `direction`, `amount`, `counterparty_type`, `counterparty_id`, `purchase_id`, `purchase_order_id`, `reference`, `note`, `voucher_no`, `is_advance`, `admin_id`)
			VALUES (?, ?, \'payment\', 0, ?, \'supplier\', ?, ?, ?, ?, ?, ?, ?, ?)'
		);
		$entry->execute(array(
			$account_id,
			$time,
			$amount,
			$supplier_id,
			(int)($data['purchase_id'] ?? 0),
			$purchaseOrderId,
			$pvNo,
			$note,
			$pvNo,
			$isAdvance ? 1 : 0,
			epc_erp_admin_id(),
		));
		$cash_entry_id = (int)$db->lastInsertId();
		$acc = $db->prepare(
			'INSERT INTO `epc_erp_supplier_accounting`
			(`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`, `cash_entry_id`, `reference`, `note`, `admin_id`, `entry_kind`)
			VALUES (?, ?, 0, ?, ?, ?, ?, ?, ?, ?)'
		);
		$acc->execute(array(
			$supplier_id,
			$time,
			$amount,
			(int)($data['purchase_id'] ?? 0),
			$cash_entry_id,
			$pvNo,
			$note,
			epc_erp_admin_id(),
			$isAdvance ? 'settlement' : 'payment',
		));
		$db->commit();
		if ($isAdvance) {
			epc_uae_vat_record_supplier_advance_on_payment($db, $cash_entry_id, $supplier_id, $amount, $purchaseOrderId, $time);
			try {
				epc_erp_gl_post_advance_payment($db, $cash_entry_id);
			} catch (Exception $e) {
			}
		} else {
			try {
				epc_erp_gl_post_cash_entry($db, $cash_entry_id);
			} catch (Exception $e) {
			}
		}
		return $cash_entry_id;
	} catch (Exception $e) {
		if ($db->inTransaction()) {
			$db->rollBack();
		}
		throw $e;
	}
}

function epc_erp_cash_entry(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	$account_id = (int)$data['account_id'];
	$amount = round((float)$data['amount'], 2);
	$direction = !empty($data['direction']) ? 1 : 0;
	$entry_type = $direction ? 'receipt' : 'payment';
	if (!empty($data['entry_type'])) {
		$entry_type = (string)$data['entry_type'];
	}
	$time = !empty($data['time']) ? (int)$data['time'] : time();
	if ($amount <= 0 || $account_id <= 0) {
		throw new Exception('Invalid entry');
	}
	require_once __DIR__ . '/epc_erp_vouchers.php';
	epc_erp_vouchers_ensure_schema($db);
	$voucherNo = trim((string)($data['voucher_no'] ?? ''));
	$cpType = (string)($data['counterparty_type'] ?? 'none');
	if ($voucherNo === '' && $direction && $cpType === 'customer') {
		$voucherNo = epc_erp_next_voucher_no($db, 'RV');
	} elseif ($voucherNo === '' && !$direction && $cpType === 'supplier') {
		$voucherNo = epc_erp_next_voucher_no($db, 'PV');
	}
	$reference = trim((string)($data['reference'] ?? ''));
	if ($reference === '' && $voucherNo !== '') {
		$reference = $voucherNo;
	}
	$stmt = $db->prepare(
		'INSERT INTO `epc_erp_cash_bank_entries`
		(`account_id`, `time`, `entry_type`, `direction`, `amount`, `counterparty_type`, `counterparty_id`, `order_id`, `reference`, `note`, `voucher_no`, `admin_id`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$stmt->execute(array(
		$account_id,
		$time,
		$entry_type,
		$direction,
		$amount,
		$cpType,
		(int)($data['counterparty_id'] ?? 0),
		(int)($data['order_id'] ?? 0),
		$reference,
		trim((string)($data['note'] ?? '')),
		$voucherNo !== '' ? $voucherNo : null,
		epc_erp_admin_id(),
	));
	$entry_id = (int)$db->lastInsertId();
	try {
		epc_erp_gl_post_cash_entry($db, $entry_id);
	} catch (Exception $e) {
	}
	return $entry_id;
}

function epc_erp_create_cash_account(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	$type = ($data['account_type'] ?? 'cash') === 'bank' ? 'bank' : 'cash';
	$status = trim((string)($data['status'] ?? 'active'));
	if (!in_array($status, array('active', 'inactive', 'closed'), true)) {
		$status = 'active';
	}
	$stmt = $db->prepare(
		'INSERT INTO `epc_erp_cash_bank_accounts`
		(`name`, `account_type`, `bank_name`, `account_number`, `currency_code`, `opening_balance`, `office_id`,
		 `legal_entity_id`, `business_unit_id`, `gl_account_id`, `iban`, `swift_bic`, `bank_branch`, `routing_code`,
		 `address`, `contact_name`, `contact_phone`, `contact_email`, `status`, `notes`, `time_created`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$stmt->execute(array(
		trim((string)$data['name']),
		$type,
		trim((string)($data['bank_name'] ?? '')),
		trim((string)($data['account_number'] ?? '')),
		trim((string)($data['currency_code'] ?? 'AED')),
		round((float)($data['opening_balance'] ?? 0), 2),
		(int)($data['office_id'] ?? 0),
		(int)($data['legal_entity_id'] ?? 0),
		(int)($data['business_unit_id'] ?? 0),
		(int)($data['gl_account_id'] ?? 0),
		trim((string)($data['iban'] ?? '')),
		trim((string)($data['swift_bic'] ?? '')),
		trim((string)($data['bank_branch'] ?? '')),
		trim((string)($data['routing_code'] ?? '')),
		trim((string)($data['address'] ?? '')),
		trim((string)($data['contact_name'] ?? '')),
		trim((string)($data['contact_phone'] ?? '')),
		trim((string)($data['contact_email'] ?? '')),
		$status,
		trim((string)($data['notes'] ?? '')),
		time(),
	));
	return (int)$db->lastInsertId();
}

function epc_erp_purchase_from_order(PDO $db, $order_id, $supplier_id)
{
	require_once __DIR__ . '/epc_erp_inventory.php';
	epc_erp_assert_order_complete($db, (int)$order_id, 'Generate purchase from order');
	$sql = epc_erp_order_sum_sql($db);
	$q = $db->prepare(
		'SELECT `id`, ' . $sql['purchase'] . ' AS purchase_ex_vat FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1'
	);
	$q->execute(array((int)$order_id));
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		throw new Exception('Order not found');
	}
	$amount = (float)$row['purchase_ex_vat'];
	if ($amount <= 0) {
		throw new Exception('Order has no purchase cost');
	}
	$inventoryLines = array();
	$itemsQ = $db->prepare(
		'SELECT `t2_article`, `t2_name`, `t2_storage_id`, `count_need`, `t2_price_purchase`
		FROM `shop_orders_items`
		WHERE `order_id` = ?'
	);
	$itemsQ->execute(array((int)$order_id));
	$storageFromOrder = 0;
	epc_erp_inventory_ensure_schema($db);
	while ($itemRow = $itemsQ->fetch(PDO::FETCH_ASSOC)) {
		$sku = trim((string)($itemRow['t2_article'] ?? ''));
		if ($sku === '') {
			continue;
		}
		$qty = (float)($itemRow['count_need'] ?? 0);
		if ($qty <= 0) {
			continue;
		}
		$lineStorage = (int)($itemRow['t2_storage_id'] ?? 0);
		if ($storageFromOrder <= 0 && $lineStorage > 0) {
			$storageFromOrder = $lineStorage;
		}
		$lineWh = $lineStorage > 0 ? epc_erp_inventory_warehouse_by_storage($db, $lineStorage) : 0;
		$inventoryLines[] = array(
			'sku' => $sku,
			'name' => trim((string)($itemRow['t2_name'] ?? '')),
			'qty' => $qty,
			'unit_cost' => round((float)($itemRow['t2_price_purchase'] ?? 0), 4),
			'warehouse_id' => $lineWh,
			'warehouse_code' => '',
			'reference' => 'ORD-' . (int)$order_id,
		);
	}
	require_once __DIR__ . '/epc_tax_toolkit.php';
	$vatCalc = epc_tax_toolkit_purchase_amounts($db, $amount, (int)$supplier_id);
	$data = array(
		'supplier_id' => (int)$supplier_id,
		'order_id' => (int)$order_id,
		'storage_id' => $storageFromOrder,
		'invoice_number' => 'ORD-' . (int)$order_id,
		'amount_ex_vat' => $amount,
		'note' => 'Auto from order #' . (int)$order_id . ($vatCalc['vat_applicable'] ? '' : ' (non-UAE supplier — no input VAT)'),
	);
	if (!empty($inventoryLines)) {
		$data['receive_inventory'] = 1;
		$data['create_items_if_missing'] = 1;
		$data['inventory_lines'] = $inventoryLines;
	}
	$purchaseId = epc_erp_create_purchase($db, $data);
	$invPosted = 0;
	$chk = $db->prepare('SELECT `inv_receipt_posted` FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1');
	$chk->execute(array($purchaseId));
	$invPosted = (int)$chk->fetchColumn();
	return array(
		'purchase_id' => $purchaseId,
		'order_id' => (int)$order_id,
		'inventory_line_count' => count($inventoryLines),
		'inventory_receipt_posted' => $invPosted > 0,
	);
}

function epc_erp_customer_settlement(PDO $db, array $data)
{
	epc_erp_ensure_accounting_codes($db);
	$user_id = (int)($data['user_id'] ?? 0);
	$amount = round((float)($data['amount'] ?? 0), 2);
	$direction = (string)($data['direction'] ?? 'credit');
	$entry_kind = (string)($data['entry_kind'] ?? 'adjustment');
	$order_id = (int)($data['order_id'] ?? 0);
	$reference = trim((string)($data['reference'] ?? ''));
	$note = trim((string)($data['note'] ?? ''));
	$post_gl = !empty($data['post_gl']);
	$time = !empty($data['time']) ? (int)$data['time'] : time();

	if ($user_id <= 0 || $amount <= 0) {
		throw new Exception('Customer and positive amount required');
	}
	if ($order_id > 0) {
		epc_erp_assert_order_complete($db, $order_id, 'Customer settlement linked to order');
	}
	if (!in_array($entry_kind, array('adjustment', 'settlement', 'write_off'), true)) {
		$entry_kind = 'adjustment';
	}
	if ($direction !== 'credit' && $direction !== 'debit') {
		throw new Exception('Direction must be credit or debit');
	}
	if ($entry_kind === 'write_off' && $direction === 'credit') {
		throw new Exception('Write-off must reduce customer balance (debit direction)');
	}

	$uq = $db->prepare('SELECT `user_id` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$uq->execute(array($user_id));
	if (!$uq->fetch()) {
		throw new Exception('Customer not found');
	}

	$income = ($direction === 'credit') ? 1 : 0;
	$code_key = $income ? 'epc_erp_ar_credit' : 'epc_erp_ar_debit';
	$code_id = epc_erp_accounting_code_id($db, $code_key);
	if ($code_id <= 0) {
		throw new Exception('ERP accounting code missing — run ERP setup');
	}

	$label = epc_erp_settlement_kinds()[$entry_kind] ?? $entry_kind;
	if ($note === '') {
		$note = $label . ($reference !== '' ? (' — ' . $reference) : '');
	}

	$cols = array('user_id', 'time', 'income', 'amount', 'operation_code', 'active', 'office_id');
	$vals = array($user_id, $time, $income, $amount, $code_id, 1, 0);
	$has_order = false;
	$has_tech = false;
	try {
		$c = $db->query("SHOW COLUMNS FROM `shop_users_accounting` LIKE 'order_id'");
		$has_order = (bool)$c->fetch();
		$c = $db->query("SHOW COLUMNS FROM `shop_users_accounting` LIKE 'tech_value_text'");
		$has_tech = (bool)$c->fetch();
	} catch (Exception $e) {
	}
	if ($has_order) {
		$cols[] = 'order_id';
		$vals[] = $order_id;
	}
	if ($has_tech) {
		$cols[] = 'tech_value_text';
		$vals[] = json_encode(array('entry_kind' => $entry_kind, 'reference' => $reference, 'erp' => 1));
	}

	$ph = implode(',', array_fill(0, count($cols), '?'));
	$db->prepare('INSERT INTO `shop_users_accounting` (`' . implode('`,`', $cols) . '`) VALUES (' . $ph . ')')->execute($vals);
	$ledger_id = (int)$db->lastInsertId();

	if ($income === 0 && $order_id > 0 && $amount > 0) {
		require_once __DIR__ . '/epc_uae_tax_compliance.php';
		epc_uae_vat_record_advance_on_payment($db, $ledger_id, $order_id, $amount, $time);
	}

	$gl_journal_id = 0;
	if ($post_gl) {
		try {
			$gl_journal_id = epc_erp_gl_post_ar_settlement($db, array(
				'ledger_id' => $ledger_id,
				'user_id' => $user_id,
				'order_id' => $order_id,
				'amount' => $amount,
				'income' => $income,
				'entry_kind' => $entry_kind,
				'reference' => $reference,
				'note' => $note,
				'time' => $time,
			));
		} catch (Exception $e) {
		}
	}

	return array('ledger_id' => $ledger_id, 'gl_journal_id' => $gl_journal_id);
}

function epc_erp_supplier_settlement(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	$supplier_id = (int)($data['supplier_id'] ?? 0);
	$amount = round((float)($data['amount'] ?? 0), 2);
	$direction = (string)($data['direction'] ?? 'decrease');
	$entry_kind = (string)($data['entry_kind'] ?? 'adjustment');
	$purchase_id = (int)($data['purchase_id'] ?? 0);
	$order_id = (int)($data['order_id'] ?? 0);
	$reference = trim((string)($data['reference'] ?? ''));
	$note = trim((string)($data['note'] ?? ''));
	$post_gl = !empty($data['post_gl']);
	$time = !empty($data['time']) ? (int)$data['time'] : time();

	if ($supplier_id <= 0 || $amount <= 0) {
		throw new Exception('Supplier and positive amount required');
	}
	if ($order_id > 0) {
		epc_erp_assert_order_complete($db, $order_id, 'Supplier settlement linked to order');
	}
	if ($purchase_id > 0) {
		$pq = $db->prepare('SELECT `order_id` FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1');
		$pq->execute(array($purchase_id));
		$linked_order = (int)$pq->fetchColumn();
		if ($linked_order > 0) {
			epc_erp_assert_order_complete($db, $linked_order, 'Supplier settlement linked to purchase order');
		}
	}
	if (!in_array($entry_kind, array('adjustment', 'settlement', 'write_off'), true)) {
		$entry_kind = 'adjustment';
	}
	if ($direction !== 'increase' && $direction !== 'decrease') {
		throw new Exception('Direction must be increase or decrease payable');
	}
	if ($entry_kind === 'write_off' && $direction === 'increase') {
		throw new Exception('Write-off must decrease payable');
	}

	$sq = $db->prepare('SELECT `id` FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$sq->execute(array($supplier_id));
	if (!$sq->fetch()) {
		throw new Exception('Supplier not found');
	}

	$is_credit = ($direction === 'increase') ? 1 : 0;
	if ($note === '') {
		$label = epc_erp_settlement_kinds()[$entry_kind] ?? $entry_kind;
		$note = $label . ($reference !== '' ? (' — ' . $reference) : '');
	}

	$acc = $db->prepare(
		'INSERT INTO `epc_erp_supplier_accounting`
		(`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`, `order_id`, `reference`, `note`, `admin_id`, `entry_kind`)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)'
	);
	$acc->execute(array(
		$supplier_id,
		$time,
		$is_credit,
		$amount,
		$purchase_id,
		$order_id,
		$reference,
		$note,
		epc_erp_admin_id(),
		$entry_kind,
	));
	$ledger_id = (int)$db->lastInsertId();

	$gl_journal_id = 0;
	if ($post_gl) {
		try {
			$gl_journal_id = epc_erp_gl_post_ap_settlement($db, array(
				'ledger_id' => $ledger_id,
				'supplier_id' => $supplier_id,
				'purchase_id' => $purchase_id,
				'amount' => $amount,
				'is_credit' => $is_credit,
				'entry_kind' => $entry_kind,
				'reference' => $reference,
				'note' => $note,
				'time' => $time,
			));
			$db->prepare('UPDATE `epc_erp_supplier_accounting` SET `gl_journal_id` = ? WHERE `id` = ?')->execute(array($gl_journal_id, $ledger_id));
		} catch (Exception $e) {
		}
	}

	return array('ledger_id' => $ledger_id, 'gl_journal_id' => $gl_journal_id);
}

function epc_erp_purchase_adjustment(PDO $db, array $data)
{
	epc_erp_ensure_schema($db);
	$purchase_id = (int)($data['purchase_id'] ?? 0);
	$delta_ex = round((float)($data['delta_ex_vat'] ?? 0), 2);
	$note = trim((string)($data['note'] ?? ''));
	$reference = trim((string)($data['reference'] ?? ''));
	$post_gl = !empty($data['post_gl']);

	if ($purchase_id <= 0 || abs($delta_ex) < 0.01) {
		throw new Exception('Purchase ID and non-zero adjustment amount required');
	}

	$q = $db->prepare('SELECT * FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1');
	$q->execute(array($purchase_id));
	$p = $q->fetch(PDO::FETCH_ASSOC);
	if (!$p) {
		throw new Exception('Purchase not found');
	}
	if ((int)$p['order_id'] > 0) {
		epc_erp_assert_order_complete($db, (int)$p['order_id'], 'Purchase adjustment linked to order');
	}

	$new_ex = round((float)$p['amount_ex_vat'] + $delta_ex, 2);
	if ($new_ex < 0) {
		throw new Exception('Adjustment would make purchase amount negative');
	}
	require_once __DIR__ . '/epc_tax_toolkit.php';
	$vatCalc = epc_tax_toolkit_purchase_amounts($db, $new_ex, (int)$p['supplier_id']);
	$new_vat = round((float)$vatCalc['vat_amount'], 2);
	$new_total = round((float)$vatCalc['total_amount'], 2);
	$delta_total = round($new_total - (float)$p['total_amount'], 2);

	$db->prepare(
		'UPDATE `epc_erp_purchases` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `vat_applicable` = ?, `vat_rate` = ?, `note` = CONCAT(IFNULL(`note`,\'\'), ?) WHERE `id` = ?'
	)->execute(array(
		$new_ex,
		$new_vat,
		$new_total,
		!empty($vatCalc['vat_applicable']) ? 1 : 0,
		(float)$vatCalc['vat_rate'],
		"\n[" . date('Y-m-d') . ' adjustment ' . $delta_ex . '] ' . $note,
		$purchase_id,
	));

	$direction = ($delta_total >= 0) ? 'increase' : 'decrease';
	$settle = epc_erp_supplier_settlement($db, array(
		'supplier_id' => (int)$p['supplier_id'],
		'amount' => abs($delta_total),
		'direction' => $direction,
		'entry_kind' => 'adjustment',
		'purchase_id' => $purchase_id,
		'order_id' => (int)$p['order_id'],
		'reference' => $reference !== '' ? $reference : ('PUR-ADJ-' . $purchase_id),
		'note' => $note !== '' ? $note : ('Purchase #' . $purchase_id . ' cost adjustment'),
		'post_gl' => $post_gl,
	));

	return array(
		'purchase_id' => $purchase_id,
		'delta_ex_vat' => $delta_ex,
		'new_total' => $new_total,
		'ledger_id' => $settle['ledger_id'],
		'gl_journal_id' => $settle['gl_journal_id'],
	);
}

function epc_erp_order_revenue_settlement(PDO $db, array $data)
{
	$order_id = (int)($data['order_id'] ?? 0);
	if ($order_id <= 0) {
		throw new Exception('Order ID required');
	}
	epc_erp_assert_order_complete($db, $order_id, 'Order revenue settlement');
	$q = $db->prepare('SELECT `user_id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1');
	$q->execute(array($order_id));
	$user_id = (int)$q->fetchColumn();
	if ($user_id <= 0) {
		throw new Exception('Order not found');
	}
	$data['user_id'] = $user_id;
	$data['order_id'] = $order_id;
	if (empty($data['reference'])) {
		$data['reference'] = 'ORD-' . $order_id;
	}
	return epc_erp_customer_settlement($db, $data);
}

function epc_erp_guide_snapshot(PDO $db)
{
	epc_erp_full_ensure_schema($db);
	$dash = epc_erp_dashboard($db);
	return array(
		'generated_at' => date('Y-m-d H:i:s'),
		'dashboard' => $dash,
		'supplier_count' => (int)$db->query('SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1')->fetchColumn(),
		'purchase_count' => (int)$db->query('SELECT COUNT(*) FROM `epc_erp_purchases` WHERE `active` = 1')->fetchColumn(),
		'cash_account_count' => (int)$db->query('SELECT COUNT(*) FROM `epc_erp_cash_bank_accounts` WHERE `active` = 1')->fetchColumn(),
		'entry_count' => (int)$db->query('SELECT COUNT(*) FROM `epc_erp_cash_bank_entries` WHERE `active` = 1')->fetchColumn(),
		'storage_count' => (int)$db->query('SELECT COUNT(*) FROM `shop_storages`')->fetchColumn(),
		'coa_count' => (int)$db->query('SELECT COUNT(*) FROM `epc_erp_coa_accounts` WHERE `active` = 1')->fetchColumn(),
		'gl_journal_count' => (int)$db->query('SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `active` = 1')->fetchColumn(),
		'pl' => epc_erp_gl_pl_report($db, strtotime(date('Y-m-01 00:00:00')), time()),
		'balance_sheet' => epc_erp_gl_balance_sheet($db, time()),
	);
}
