<?php
/**
 * Supplier LPO (Local Purchase Order) e-mail notifications.
 * LPO number = customer order number (shop_orders.id).
 */
defined('_ASTEXE_') or die('No access');

function epc_supplier_h($value): string
{
	return htmlspecialchars((string)$value, ENT_QUOTES, 'UTF-8');
}

/**
 * Supplier order inbox for a warehouse (connection_options.order_email, else price list sender_email).
 */
function epc_storage_supplier_order_email($db_link, int $storage_id): string
{
	if ($storage_id <= 0 || !isset($db_link) || !$db_link) {
		return '';
	}
	try {
		$stmt = $db_link->prepare('SELECT `connection_options`, `interface_type` FROM `shop_storages` WHERE `id` = ? LIMIT 1;');
		$stmt->execute(array($storage_id));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			return '';
		}
		$opts = json_decode((string)($row['connection_options'] ?? ''), true);
		if (!is_array($opts)) {
			$opts = array();
		}
		foreach (array('order_email', 'supplier_order_email', 'lpo_email') as $key) {
			$email = strtolower(trim((string)($opts[$key] ?? '')));
			if ($email !== '' && filter_var($email, FILTER_VALIDATE_EMAIL)) {
				return $email;
			}
		}
		$price_id = (int)($opts['price_id'] ?? 0);
		if ($price_id > 0) {
			$pq = $db_link->prepare('SELECT `sender_email` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
			$pq->execute(array($price_id));
			$email = strtolower(trim((string)$pq->fetchColumn()));
			if ($email !== '' && filter_var($email, FILTER_VALIDATE_EMAIL)) {
				return $email;
			}
		}
	} catch (Throwable $e) {
	}
	return '';
}

/**
 * Resolve warehouse id for an order line (t2_storage_id or detail storage).
 */
function epc_order_item_storage_id($db_link, array $item): int
{
	$storage_id = (int)($item['t2_storage_id'] ?? 0);
	if ($storage_id > 0) {
		return $storage_id;
	}
	if (!isset($db_link) || !$db_link) {
		return 0;
	}
	try {
		$q = $db_link->prepare('SELECT `storage_id` FROM `shop_orders_items_details` WHERE `order_item_id` = ? ORDER BY `id` ASC LIMIT 1;');
		$q->execute(array((int)($item['id'] ?? 0)));
		return (int)$q->fetchColumn();
	} catch (Throwable $e) {
		return 0;
	}
}

/**
 * HTML purchase request for one supplier / warehouse.
 */
function epc_build_supplier_lpo_html($db_link, int $order_id, int $storage_id, string $storage_name, array $items): string
{
	global $DP_Config;
	$domain = is_object($DP_Config) ? rtrim((string)$DP_Config->domain_path, '/') : '';

	$html = '<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;color:#111;">';
	$html .= '<p style="margin:0 0 12px;">Dear supplier,</p>';
	$html .= '<p style="margin:0 0 12px;">Please supply the following parts for our customer order. '
		. 'Use <strong>LPO / PO number <span style="color:#b45309;">' . (int)$order_id . '</span></strong> '
		. 'on your invoice and delivery note (this is our customer order number).</p>';
	$html .= '<table style="border-collapse:collapse;margin:0 0 16px;font-size:14px;">';
	$html .= '<tr><td style="padding:4px 16px 4px 0;font-weight:bold;">LPO number</td><td>' . (int)$order_id . '</td></tr>';
	$html .= '<tr><td style="padding:4px 16px 4px 0;font-weight:bold;">Warehouse</td><td>' . epc_supplier_h($storage_name) . '</td></tr>';
	$html .= '</table>';

	$html .= '<table style="border-collapse:collapse;width:100%;max-width:720px;font-size:13px;" border="1" cellpadding="6" cellspacing="0">';
	$html .= '<thead><tr style="background:#f1f5f9;">'
		. '<th align="left">Brand</th><th align="left">Part no.</th><th align="left">Description</th><th align="right">Qty</th>'
		. '</tr></thead><tbody>';

	foreach ($items as $item) {
		$brand = (string)($item['t2_manufacturer'] ?? '');
		$article = (string)($item['t2_article_show'] ?? $item['t2_article'] ?? '');
		$name = (string)($item['t2_name'] ?? '');
		$qty = (int)($item['count_need'] ?? 0);
		if ($qty <= 0) {
			continue;
		}
		$html .= '<tr>'
			. '<td>' . epc_supplier_h($brand) . '</td>'
			. '<td>' . epc_supplier_h($article) . '</td>'
			. '<td>' . epc_supplier_h($name) . '</td>'
			. '<td align="right">' . $qty . '</td>'
			. '</tr>';
	}
	$html .= '</tbody></table>';
	$html .= '<p style="margin:16px 0 0;font-size:13px;color:#475569;">Reply to this e-mail if any line is unavailable. '
		. 'Reference LPO <strong>' . (int)$order_id . '</strong> on all correspondence.</p>';
	if ($domain !== '') {
		$html .= '<p style="margin:8px 0 0;font-size:12px;color:#64748b;">' . epc_supplier_h($domain) . '</p>';
	}
	$html .= '</div>';
	return $html;
}

/**
 * Send LPO e-mails to suppliers (one per warehouse with configured e-mail).
 */
function epc_send_supplier_lpo_notifications($db_link, int $order_id): void
{
	if ($order_id <= 0 || !isset($db_link) || !$db_link) {
		return;
	}
	if (!function_exists('send_notify')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
	}
	if (!function_exists('epc_log_order_notification')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
	}

	$storages = array();
	try {
		$sq = $db_link->query('SELECT `id`, `name` FROM `shop_storages`;');
		while ($s = $sq->fetch(PDO::FETCH_ASSOC)) {
			$storages[(int)$s['id']] = (string)$s['name'];
		}
	} catch (Throwable $e) {
		return;
	}

	$items_by_storage = array();
	try {
		$iq = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC;');
		$iq->execute(array($order_id));
		while ($item = $iq->fetch(PDO::FETCH_ASSOC)) {
			$sid = epc_order_item_storage_id($db_link, $item);
			if ($sid <= 0) {
				continue;
			}
			if (!isset($items_by_storage[$sid])) {
				$items_by_storage[$sid] = array();
			}
			$items_by_storage[$sid][] = $item;
		}
	} catch (Throwable $e) {
		epc_log_order_notification($db_link, $order_id, 'Supplier LPO: failed to load order items');
		return;
	}

	if (empty($items_by_storage)) {
		epc_log_order_notification($db_link, $order_id, 'Supplier LPO: no warehouse lines to notify');
		return;
	}

	$sent = 0;
	$skipped = 0;
	foreach ($items_by_storage as $storage_id => $items) {
		$email = epc_storage_supplier_order_email($db_link, (int)$storage_id);
		$storage_name = isset($storages[$storage_id]) ? $storages[$storage_id] : ('Warehouse #' . $storage_id);
		if ($email === '') {
			$skipped++;
			epc_log_order_notification(
				$db_link,
				$order_id,
				'Supplier LPO skipped (no order e-mail): ' . $storage_name . ' [ID ' . $storage_id . ']'
			);
			continue;
		}

		$order_text = epc_build_supplier_lpo_html($db_link, $order_id, (int)$storage_id, $storage_name, $items);
		$vars = array(
			'order_id' => $order_id,
			'lpo_number' => (string)$order_id,
			'storage_name' => $storage_name,
			'order_text' => $order_text,
		);
		$persons = array(
			array(
				'type' => 'direct_contact',
				'contacts' => array(
					'email' => array('value' => $email),
				),
			),
		);
		$answer = send_notify('lpo_to_supplier', $vars, $persons, true);
		if (function_exists('epc_notify_store_answer')) {
			epc_notify_store_answer($answer);
		}
		$ok = function_exists('epc_notify_email_status') ? epc_notify_email_status($answer, $email) : null;
		if ($ok !== true) {
			$answer = send_notify('lpo_to_supplier', $vars, $persons, true);
			$ok = function_exists('epc_notify_email_status') ? epc_notify_email_status($answer, $email) : null;
		}
		epc_log_order_notification(
			$db_link,
			$order_id,
			'Supplier LPO to ' . $email . ' (' . $storage_name . ', LPO #' . $order_id . '): ' . ($ok ? 'sent' : 'FAILED')
		);
		if ($ok) {
			$sent++;
		}
	}

	if ($sent === 0 && $skipped > 0) {
		epc_log_order_notification(
			$db_link,
			$order_id,
			'Supplier LPO: 0 sent — set "Supplier order email (LPO)" on each warehouse in CP → Logistics → Warehouses'
		);
	}
}
