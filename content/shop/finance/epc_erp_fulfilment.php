<?php
/**
 * ERP — order fulfilment pipeline (payments, stock movement, delivery, returns).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';

function epc_erp_fulfilment_item_refs(PDO $db)
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}
	$finish = array();
	$return = array();
	$issue = array();
	try {
		$q = $db->query('SELECT `id`, `for_finish`, `for_return`, `issue_flag` FROM `shop_orders_items_statuses_ref`');
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$id = (int)$row['id'];
			if ((int)$row['for_finish'] === 1) {
				$finish[] = $id;
			}
			if ((int)$row['for_return'] === 1) {
				$return[] = $id;
			}
			if ((int)$row['issue_flag'] === 1) {
				$issue[] = $id;
			}
		}
	} catch (Exception $e) {
	}
	$cache = array(
		'finish_ids' => $finish,
		'return_ids' => $return,
		'issue_ids' => $issue,
	);
	return $cache;
}

function epc_erp_fulfilment_in_list(array $ids, $value)
{
	return !empty($ids) && in_array((int)$value, $ids, true);
}

function epc_erp_fulfilment_derive_row_metrics(array $row, $purchase_total, $supplier_paid)
{
	$purchase_total = (float)$purchase_total;
	$supplier_paid = (float)$supplier_paid;
	$paid_amount = (float)$row['paid_amount'];
	$due = (float)$row['due_amount'];
	$complete = !empty($row['order_complete']);
	$qty_total = (float)$row['qty_total'];
	$qty_issued = (float)$row['qty_issued'];
	$qty_reserved = (float)$row['qty_reserved'];

	if ($paid_amount > 0.01 && ((int)$row['paid'] === 1 || ($complete && $due < 0.01))) {
		$customer_pay = 'paid';
	} elseif ($paid_amount > 0.01) {
		$customer_pay = 'advance';
	} elseif ($complete && $due > 0.01) {
		$customer_pay = 'credit';
	} else {
		$customer_pay = 'pending';
	}

	if ($purchase_total <= 0) {
		$supplier_pay = 'none';
	} elseif ($supplier_paid >= $purchase_total - 0.01) {
		$supplier_pay = 'paid';
	} elseif ($supplier_paid > 0.01) {
		$supplier_pay = 'advance';
	} else {
		$supplier_pay = 'credit';
	}

	if ($qty_total <= 0) {
		$stock_state = 'none';
	} elseif ($qty_issued >= $qty_total - 0.001) {
		$stock_state = 'in_stock';
	} elseif ($qty_issued > 0 || $qty_reserved > 0) {
		$stock_state = 'partial';
	} else {
		$stock_state = 'awaiting';
	}

	$lines_total = (int)$row['lines_total'];
	$lines_delivered = (int)$row['lines_delivered'];
	if ($complete || ($lines_total > 0 && $lines_delivered >= $lines_total)) {
		$delivery = 'delivered';
	} elseif ($lines_delivered > 0) {
		$delivery = 'partial';
	} else {
		$delivery = 'pending';
	}

	$returns_count = (int)$row['returns_count'];
	$return_state = $returns_count > 0 ? 'open' : 'none';

	$pipeline_step = 1;
	if ($customer_pay !== 'pending') {
		$pipeline_step = 2;
	}
	if ($supplier_pay !== 'none' && $supplier_pay !== 'pending') {
		$pipeline_step = max($pipeline_step, 3);
	}
	if ($stock_state === 'in_stock' || $stock_state === 'partial') {
		$pipeline_step = max($pipeline_step, 4);
	}
	if ($delivery === 'delivered') {
		$pipeline_step = 5;
	}
	if ($return_state === 'open') {
		$pipeline_step = 6;
	}

	return array_merge($row, array(
		'customer_pay' => $customer_pay,
		'supplier_pay' => $supplier_pay,
		'stock_state' => $stock_state,
		'delivery' => $delivery,
		'return_state' => $return_state,
		'pipeline_step' => $pipeline_step,
		'purchase_total' => $purchase_total,
		'supplier_paid' => $supplier_paid,
	));
}

function epc_erp_fulfilment_orders_batch(PDO $db, array $order_ids)
{
	$order_ids = array_values(array_unique(array_filter(array_map('intval', $order_ids))));
	if (empty($order_ids)) {
		return array();
	}
	$sql = epc_erp_order_sum_sql($db);
	$refs = epc_erp_fulfilment_item_refs($db);
	$finish_in = empty($refs['finish_ids']) ? '0' : implode(',', array_map('intval', $refs['finish_ids']));
	$return_in = empty($refs['return_ids']) ? '0' : implode(',', array_map('intval', $refs['return_ids']));
	$in = implode(',', $order_ids);

	$q = $db->query(
		'SELECT `shop_orders`.*, ' . $sql['order_complete_expr'] . ' AS order_complete,
			' . $sql['sale'] . ' AS sale_ex_vat,
			' . $sql['purchase'] . ' AS purchase_ex_vat,
			' . $sql['paid'] . ' AS paid_amount,
			' . $sql['due'] . ' AS due_amount,
			' . epc_erp_order_status_name_sql($db) . ' AS order_status_name,
			(SELECT `email` FROM `users` WHERE `user_id` = `shop_orders`.`user_id` LIMIT 1) AS customer_email,
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`) AS lines_total,
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND `status` IN (' . $finish_in . ')) AS lines_delivered,
			(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND `status` IN (' . $return_in . ')) AS lines_return,
			(SELECT IFNULL(SUM(`count_need`),0) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`) AS qty_total,
			(SELECT IFNULL(SUM(d.`count_issued`),0) FROM `shop_orders_items_details` d
				INNER JOIN `shop_orders_items` i ON i.`id` = d.`order_item_id` WHERE i.`order_id` = `shop_orders`.`id`) AS qty_issued,
			(SELECT IFNULL(SUM(d.`count_reserved`),0) FROM `shop_orders_items_details` d
				INNER JOIN `shop_orders_items` i ON i.`id` = d.`order_item_id` WHERE i.`order_id` = `shop_orders`.`id`) AS qty_reserved,
			(SELECT COUNT(*) FROM `shop_orders_returns` r
				INNER JOIN `shop_orders_returns_items` ri ON ri.`return_id` = r.`id`
				INNER JOIN `shop_orders_items` i ON i.`id` = ri.`item_id`
				WHERE i.`order_id` = `shop_orders`.`id`) AS returns_count
		FROM `shop_orders`
		WHERE `shop_orders`.`id` IN (' . $in . ') AND `shop_orders`.`successfully_created` = 1
		ORDER BY `shop_orders`.`time` DESC'
	);
	$rows = array();
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$rows[(int)$row['id']] = $row;
	}

	$purchase_totals = array();
	$pq = $db->query(
		'SELECT `order_id`, IFNULL(SUM(`total_amount`),0) AS total
		FROM `epc_erp_purchases`
		WHERE `active` = 1 AND `order_id` IN (' . $in . ')
		GROUP BY `order_id`'
	);
	while ($pr = $pq->fetch(PDO::FETCH_ASSOC)) {
		$purchase_totals[(int)$pr['order_id']] = (float)$pr['total'];
	}

	$supplier_paid = array_fill_keys($order_ids, 0.0);
	$sq = $db->query(
		'SELECT `order_id`, IFNULL(SUM(`amount`),0) AS total
		FROM `epc_erp_supplier_accounting`
		WHERE `active` = 1 AND `is_credit` = 0 AND `order_id` IN (' . $in . ')
		GROUP BY `order_id`'
	);
	while ($sr = $sq->fetch(PDO::FETCH_ASSOC)) {
		$supplier_paid[(int)$sr['order_id']] += (float)$sr['total'];
	}
	$sq2 = $db->query(
		'SELECT p.`order_id`, IFNULL(SUM(sa.`amount`),0) AS total
		FROM `epc_erp_supplier_accounting` sa
		INNER JOIN `epc_erp_purchases` p ON p.`id` = sa.`purchase_id` AND p.`active` = 1
		WHERE sa.`active` = 1 AND sa.`is_credit` = 0 AND p.`order_id` IN (' . $in . ')
		GROUP BY p.`order_id`'
	);
	while ($sr = $sq2->fetch(PDO::FETCH_ASSOC)) {
		$supplier_paid[(int)$sr['order_id']] += (float)$sr['total'];
	}

	$out = array();
	foreach ($order_ids as $oid) {
		if (!isset($rows[$oid])) {
			continue;
		}
		$pt = isset($purchase_totals[$oid]) ? $purchase_totals[$oid] : 0.0;
		$sp = isset($supplier_paid[$oid]) ? $supplier_paid[$oid] : 0.0;
		$out[] = epc_erp_fulfilment_derive_row_metrics($rows[$oid], $pt, $sp);
	}
	return $out;
}

function epc_erp_fulfilment_order_metrics(PDO $db, $order_id)
{
	$batch = epc_erp_fulfilment_orders_batch($db, array((int)$order_id));
	return !empty($batch) ? $batch[0] : null;
}

function epc_erp_fulfilment_pipeline_labels()
{
	return array(
		1 => array('key' => 'ordered', 'label' => 'Order placed', 'icon' => 'fa-shopping-cart', 'color' => '#64748b'),
		2 => array('key' => 'customer_pay', 'label' => 'Customer payment', 'icon' => 'fa-credit-card', 'color' => '#2563eb'),
		3 => array('key' => 'supplier_pay', 'label' => 'Supplier payment', 'icon' => 'fa-truck', 'color' => '#7c3aed'),
		4 => array('key' => 'stock', 'label' => 'Goods in stock', 'icon' => 'fa-cubes', 'color' => '#0891b2'),
		5 => array('key' => 'deliver', 'label' => 'Delivered to customer', 'icon' => 'fa-check-circle', 'color' => '#16a34a'),
		6 => array('key' => 'return', 'label' => 'Return in progress', 'icon' => 'fa-reply', 'color' => '#dc2626'),
	);
}

function epc_erp_fulfilment_dashboard(PDO $db, $date_from, $date_to, $max_orders = 40)
{
	$st = $db->prepare(
		'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?'
	);
	$st->execute(array((int)$date_from, (int)$date_to));
	$period_total = (int)$st->fetchColumn();

	$q = $db->prepare(
		'SELECT `id` FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ? ORDER BY `time` DESC LIMIT ' . (int)$max_orders
	);
	$q->execute(array((int)$date_from, (int)$date_to));
	$order_ids = array();
	while ($id = $q->fetchColumn()) {
		$order_ids[] = (int)$id;
	}
	$pipeline = array(
		'ordered' => 0,
		'customer_advance' => 0,
		'customer_credit' => 0,
		'customer_paid' => 0,
		'customer_pending' => 0,
		'supplier_advance' => 0,
		'supplier_credit' => 0,
		'supplier_paid' => 0,
		'supplier_none' => 0,
		'stock_awaiting' => 0,
		'stock_partial' => 0,
		'stock_ready' => 0,
		'delivery_pending' => 0,
		'delivery_partial' => 0,
		'delivery_done' => 0,
		'returns_open' => 0,
	);
	$step_counts = array(1 => 0, 2 => 0, 3 => 0, 4 => 0, 5 => 0, 6 => 0);
	$at_least = array(1 => 0, 2 => 0, 3 => 0, 4 => 0, 5 => 0, 6 => 0);
	$orders_sample = array();
	$total_orders = 0;

	foreach (epc_erp_fulfilment_orders_batch($db, $order_ids) as $m) {
		$total_orders++;
		$step_counts[$m['pipeline_step']]++;
		for ($s = 1; $s <= (int)$m['pipeline_step']; $s++) {
			$at_least[$s]++;
		}
		switch ($m['customer_pay']) {
			case 'advance': $pipeline['customer_advance']++; break;
			case 'credit': $pipeline['customer_credit']++; break;
			case 'paid': $pipeline['customer_paid']++; break;
			default: $pipeline['customer_pending']++; break;
		}
		switch ($m['supplier_pay']) {
			case 'advance': $pipeline['supplier_advance']++; break;
			case 'credit': $pipeline['supplier_credit']++; break;
			case 'paid': $pipeline['supplier_paid']++; break;
			default: $pipeline['supplier_none']++; break;
		}
		switch ($m['stock_state']) {
			case 'in_stock': $pipeline['stock_ready']++; break;
			case 'partial': $pipeline['stock_partial']++; break;
			default: $pipeline['stock_awaiting']++; break;
		}
		switch ($m['delivery']) {
			case 'delivered': $pipeline['delivery_done']++; break;
			case 'partial': $pipeline['delivery_partial']++; break;
			default: $pipeline['delivery_pending']++; break;
		}
		if ($m['return_state'] === 'open') {
			$pipeline['returns_open']++;
		}
		if (count($orders_sample) < 80) {
			$orders_sample[] = $m;
		}
	}

	$pipeline['ordered'] = $period_total;
	$labels = epc_erp_fulfilment_pipeline_labels();

	return array(
		'pipeline' => $pipeline,
		'step_counts' => $step_counts,
		'orders' => $orders_sample,
		'total_orders' => $period_total,
		'funnel' => array(
			'values' => array(
				(int)$at_least[1],
				(int)$at_least[2],
				(int)$at_least[3],
				(int)$at_least[4],
				(int)$at_least[5],
				(int)$at_least[6],
			),
			'labels' => array_values(array_map(function ($l) { return $l['label']; }, $labels)),
			'colors' => array_values(array_map(function ($l) { return $l['color']; }, $labels)),
		),
	);
}

function epc_erp_fulfilment_summary_light(PDO $db, $date_from, $date_to)
{
	$cmp = epc_erp_order_complete_sql($db);
	$st = $db->prepare(
		'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?'
	);
	$st->execute(array((int)$date_from, (int)$date_to));
	$total = (int)$st->fetchColumn();
	$delivered = 0;
	if ($cmp['order_complete_expr'] !== '0') {
		$st = $db->prepare(
			'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ? AND ' . $cmp['order_complete_expr']
		);
		$st->execute(array((int)$date_from, (int)$date_to));
		$delivered = (int)$st->fetchColumn();
	}
	$returns_open = 0;
	try {
		$returns_open = (int)$db->query('SELECT COUNT(*) FROM `shop_orders_returns`')->fetchColumn();
	} catch (Exception $e) {
	}
	return array(
		'total_orders' => $total,
		'pipeline' => array(
			'delivery_done' => $delivered,
			'returns_open' => $returns_open,
		),
	);
}

function epc_erp_fulfilment_stock_movements(PDO $db, $date_from, $date_to, $limit = 50)
{
	$refs = epc_erp_fulfilment_item_refs($db);
	$finish_in = empty($refs['finish_ids']) ? '0' : implode(',', array_map('intval', $refs['finish_ids']));
	$q = $db->prepare(
		'SELECT o.`id` AS order_id, o.`time`, i.`id` AS item_id, i.`t2_article` AS `article`, i.`t2_manufacturer` AS `manufacturer`, i.`count_need`,
			d.`storage_id`, d.`count_reserved`, d.`count_issued`, d.`count_canceled`,
			s.`name` AS storage_name, i.`status`,
			(SELECT `name` FROM `shop_orders_items_statuses_ref` WHERE `id` = i.`status` LIMIT 1) AS status_name,
			(SELECT `email` FROM `users` WHERE `user_id` = o.`user_id` LIMIT 1) AS customer_email
		FROM `shop_orders_items_details` d
		INNER JOIN `shop_orders_items` i ON i.`id` = d.`order_item_id`
		INNER JOIN `shop_orders` o ON o.`id` = i.`order_id`
		LEFT JOIN `shop_storages` s ON s.`id` = d.`storage_id`
		WHERE o.`successfully_created` = 1 AND o.`time` >= ? AND o.`time` <= ?
		ORDER BY o.`time` DESC, i.`id` DESC
		LIMIT ' . (int)$limit
	);
	$q->execute(array((int)$date_from, (int)$date_to));
	$rows = $q->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$row) {
		$issued = (float)$row['count_issued'];
		$need = (float)$row['count_need'];
		if (epc_erp_fulfilment_in_list($refs['finish_ids'], $row['status'])) {
			$row['movement'] = 'Delivered to customer';
			$row['movement_kind'] = 'deliver';
		} elseif ($issued >= $need && $need > 0) {
			$row['movement'] = 'Issued from stock';
			$row['movement_kind'] = 'issue';
		} elseif ((float)$row['count_reserved'] > 0) {
			$row['movement'] = 'Reserved in stock';
			$row['movement_kind'] = 'reserve';
		} elseif ((float)$row['count_canceled'] > 0) {
			$row['movement'] = 'Returned to stock / cancelled';
			$row['movement_kind'] = 'return';
		} else {
			$row['movement'] = 'Awaiting goods';
			$row['movement_kind'] = 'pending';
		}
	}
	unset($row);
	return $rows;
}

function epc_erp_fulfilment_returns(PDO $db, $limit = 40)
{
	try {
		$q = $db->query(
			'SELECT r.`id` AS return_id, r.`time`, r.`sum`, rs.`caption` AS status_name,
				GROUP_CONCAT(DISTINCT i.`order_id` ORDER BY i.`order_id` SEPARATOR \', \') AS order_ids,
				COUNT(DISTINCT ri.`id`) AS items_count,
				(SELECT `email` FROM `users` WHERE `user_id` = r.`user_id` LIMIT 1) AS customer_email
			FROM `shop_orders_returns` r
			INNER JOIN `shop_orders_returns_statuses` rs ON rs.`id` = r.`status_id`
			INNER JOIN `shop_orders_returns_items` ri ON ri.`return_id` = r.`id`
			INNER JOIN `shop_orders_items` i ON i.`id` = ri.`item_id`
			GROUP BY r.`id`
			ORDER BY r.`time` DESC
			LIMIT ' . (int)$limit
		);
		return $q->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array();
	}
}

function epc_erp_fulfilment_pay_badge($kind, $value)
{
	$map = array(
		'customer' => array(
			'paid' => array('Paid', 'success'),
			'advance' => array('Advance paid', 'info'),
			'credit' => array('On credit', 'warning'),
			'pending' => array('Awaiting payment', 'default'),
		),
		'supplier' => array(
			'paid' => array('Supplier paid', 'success'),
			'advance' => array('Advance to supplier', 'info'),
			'credit' => array('Supplier credit', 'warning'),
			'none' => array('No purchase yet', 'default'),
		),
		'stock' => array(
			'in_stock' => array('In stock', 'success'),
			'partial' => array('Partial stock', 'info'),
			'awaiting' => array('Awaiting delivery', 'default'),
			'none' => array('—', 'default'),
		),
		'delivery' => array(
			'delivered' => array('Delivered', 'success'),
			'partial' => array('Partial delivery', 'info'),
			'pending' => array('Not delivered', 'default'),
		),
	);
	if (!isset($map[$kind][$value])) {
		return array('—', 'default');
	}
	return $map[$kind][$value];
}
