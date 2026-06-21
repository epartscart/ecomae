<?php
/**
 * Staff e-mail / CP-style order layout (included from get_order_info_html_for_manager.php).
 */
if (empty($order) || empty($order_id)) {
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';

if (!isset($epc_currency_records) && isset($db_link, $DP_Config)) {
	$epc_currency_records = epc_currency_records($db_link, $DP_Config);
}
if (empty($epc_currency_records)) {
	$epc_currency_records = epc_currency_supported_defaults();
}
$epc_usd_rate = 3.6725;
if (isset($epc_currency_records['840']['rate'])) {
	$epc_usd_rate = (float)$epc_currency_records['840']['rate'];
}
if ($epc_usd_rate <= 0) {
	$epc_usd_rate = 3.6725;
}

$epc_price_dual = static function ($aed) use ($epc_usd_rate) {
	$aed = (float)$aed;
	$usd = $aed / $epc_usd_rate;
	return number_format($aed, 2, '.', ',') . ' AED<br><span style="font-size:11px;color:#555;">('
		. number_format($usd, 2, '.', ',') . ' USD)</span>';
};

$main_color = !empty($templates['main_color']) ? $templates['main_color'] : '#2b78d6';
$backend = $DP_Config->backend_dir;
$cp_order_url = rtrim($DP_Config->domain_path, '/') . '/' . $backend . '/shop/orders/order?order_id=' . (int)$order_id;

$customer_id = (int)$order['user_id'];
$how_get = (int)$order['how_get'];
$paid_type = (int)$order['paid_type'];
$status_id = (int)$order['status'];

$delivery_address = '';
$delivery_type = '';
$obtain_query = $db_link->prepare('SELECT * FROM `shop_obtaining_modes` WHERE `id` = ? LIMIT 1');
$obtain_query->execute(array($how_get));
$obtain_mode = $obtain_query->fetch(PDO::FETCH_ASSOC);
if ($obtain_mode) {
	$delivery_type = function_exists('translate_str_by_id') ? translate_str_by_id($obtain_mode['caption']) : (string)$obtain_mode['caption'];
}
if (!empty($how_get_json) && is_array($how_get_json)) {
	foreach (array('address', 'delivery_address', 'street', 'city', 'office', 'point', 'name') as $k) {
		if (!empty($how_get_json[$k])) {
			$delivery_address = trim((string)$how_get_json[$k]);
			break;
		}
	}
	if ($delivery_address === '') {
		$delivery_address = trim(json_encode($how_get_json, JSON_UNESCAPED_UNICODE));
	}
}

$payment_label = !empty($shop_orders_paid_type[$paid_type]) ? $shop_orders_paid_type[$paid_type] : '';
$cart_label = 'Cart';
try {
	$cart_q = $db_link->prepare('SELECT `name` FROM `shop_carts` WHERE `user_id` = ? ORDER BY `id` DESC LIMIT 1');
	$cart_q->execute(array($customer_id > 0 ? $customer_id : 0));
	$cn = (string)$cart_q->fetchColumn();
	if ($cn !== '') {
		$cart_label = $cn;
	}
} catch (Throwable $e) {
}

?>
<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;color:#222;">
<p style="margin:0 0 12px;">
	<a style="background:<?php echo htmlspecialchars($main_color, ENT_QUOTES, 'UTF-8'); ?>;color:#fff;text-decoration:none;padding:8px 14px;border-radius:4px;display:inline-block;font-weight:bold;"
		href="<?php echo htmlspecialchars($cp_order_url, ENT_QUOTES, 'UTF-8'); ?>" target="_blank">View order in your Control Panel</a>
</p>
<p style="margin:0 0 16px;"><strong>New order #<?php echo (int)$order_id; ?></strong>
	· <?php echo date('d.m.Y H:i', (int)$order['time']); ?>
	· <?php echo htmlspecialchars($orders_statuses[$status_id]['name'] ?? '', ENT_QUOTES, 'UTF-8'); ?>
</p>

<?php echo epc_build_customer_profile_html($customer_id, $order); ?>

<table style="font-family:Calibri,Arial,sans-serif;font-size:14px;margin:12px 0 18px;border-collapse:collapse;">
	<?php if ($delivery_address !== '') { ?>
	<tr><td style="padding:3px 12px 3px 0;font-weight:bold;">Delivery address:</td><td><?php echo htmlspecialchars($delivery_address, ENT_QUOTES, 'UTF-8'); ?></td></tr>
	<?php } ?>
	<?php if ($delivery_type !== '') { ?>
	<tr><td style="padding:3px 12px 3px 0;font-weight:bold;">Delivery type:</td><td><?php echo htmlspecialchars($delivery_type, ENT_QUOTES, 'UTF-8'); ?></td></tr>
	<?php } ?>
	<?php if ($payment_label !== '') { ?>
	<tr><td style="padding:3px 12px 3px 0;font-weight:bold;">Payment method:</td><td><?php echo htmlspecialchars($payment_label, ENT_QUOTES, 'UTF-8'); ?></td></tr>
	<?php } ?>
	<tr><td style="padding:3px 12px 3px 0;font-weight:bold;">Cart:</td><td><?php echo htmlspecialchars($cart_label, ENT_QUOTES, 'UTF-8'); ?></td></tr>
</table>

<?php
$SELECT_price_purchase_sum = "IFNULL((SELECT SUM(`price_purchase`*`count_reserved`) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), CAST(`t2_price_purchase`*`count_need` AS DECIMAL(8,2)))";
$SELECT_item_price_sum = "CAST(`price`*`count_need` AS DECIMAL(8,2))";
$SELECT_item_profit = "CAST(`price`*`count_need` - $SELECT_price_purchase_sum AS DECIMAL(8,2))";
$SELECT_ORDER_ITEMS = "SELECT *, $SELECT_price_purchase_sum AS `price_purchase_sum`, $SELECT_item_price_sum AS `price_sum`, $SELECT_item_profit AS `profit` FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`";
$order_items_query = $db_link->prepare($SELECT_ORDER_ITEMS);
$order_items_query->execute(array($order_id));

$count_need_total = 0;
$price_sum_total = 0.0;
$price_purchase_sum_total = 0.0;
$profit_total = 0.0;
$weight_total = 0.0;
$rows_html = '';

while ($order_item = $order_items_query->fetch(PDO::FETCH_ASSOC)) {
	$item_status = (int)$order_item['status'];
	$item_count = (int)$order_item['count_need'];
	$item_price = (float)$order_item['price'];
	$item_price_sum = (float)$order_item['price_sum'];
	$item_purchase_sum = (float)$order_item['price_purchase_sum'];
	$item_purchase_unit = $item_count > 0 ? $item_purchase_sum / $item_count : (float)$order_item['t2_price_purchase'];
	$warehouse = '';
	if ((int)$order_item['product_type'] === 2 && !empty($order_item['t2_storage_id'])) {
		$warehouse = $storages_list[$order_item['t2_storage_id']] ?? (string)$order_item['t2_storage'];
	}
	$t2_min = (int)($order_item['t2_time_to_exe'] ?? 0);
	$t2_max = (int)($order_item['t2_time_to_exe_guaranteed'] ?? 0);
	$term = $t2_min > 0 ? ($t2_max > $t2_min ? "from {$t2_min} to {$t2_max} days" : "{$t2_min} days") : '—';

	$weight = 0.0;
	if (!empty($order_item['t2_product_json'])) {
		$pj = json_decode((string)$order_item['t2_product_json'], true);
		if (is_array($pj)) {
			$weight = (float)($pj['weight'] ?? $pj['mass'] ?? $pj['Weight'] ?? 0);
		}
	}
	$line_weight = $weight * $item_count;

	if (array_search($item_status, $orders_items_statuses_not_count, true) === false) {
		$count_need_total += $item_count;
		$price_sum_total += $item_price_sum;
		$price_purchase_sum_total += $item_purchase_sum;
		$profit_total += (float)$order_item['profit'];
		$weight_total += $line_weight;
	}

	$rows_html .= '<tr style="vertical-align:top;">'
		. '<td style="border:1px solid #ccc;padding:6px;">' . htmlspecialchars($warehouse, ENT_QUOTES, 'UTF-8') . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;">' . htmlspecialchars((string)$order_item['t2_manufacturer'], ENT_QUOTES, 'UTF-8') . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;">' . htmlspecialchars((string)$order_item['t2_article'], ENT_QUOTES, 'UTF-8') . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;">' . htmlspecialchars((string)$order_item['t2_name'], ENT_QUOTES, 'UTF-8') . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;text-align:right;">' . ($line_weight > 0 ? number_format($line_weight, 3, '.', '') : '—') . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;">' . htmlspecialchars($term, ENT_QUOTES, 'UTF-8') . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;text-align:center;">' . $item_count . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;text-align:right;">' . $epc_price_dual($item_price) . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;text-align:right;">' . $epc_price_dual($item_purchase_unit) . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;text-align:right;">' . $epc_price_dual($item_price_sum) . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;text-align:right;">' . $epc_price_dual($item_purchase_sum) . '</td>'
		. '<td style="border:1px solid #ccc;padding:6px;"></td>'
		. '</tr>';
}

$gross = $price_sum_total;
$vat = round($gross * 0.05, 2);
$net = $gross + $vat;
$margin = $gross - $price_purchase_sum_total;
$delivery_cost = 0.0;
if (is_array($how_get_json) && isset($how_get_json['delivery_price'])) {
	$delivery_cost = (float)$how_get_json['delivery_price'];
}
$total = $net + $delivery_cost;

$order_message_query = $db_link->prepare('SELECT `text` FROM `shop_orders_messages` WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` ASC LIMIT 1');
$order_message_query->execute(array($order_id));
$order_message = $order_message_query->fetch(PDO::FETCH_ASSOC);
?>

<h4 style="font-family:Calibri,Arial,sans-serif;margin:20px 0 8px;">Additional information for employees</h4>
<div style="overflow-x:auto;">
<table style="font-family:Calibri,Arial,sans-serif;font-size:12px;border-collapse:collapse;width:100%;min-width:900px;">
<thead>
<tr style="background:#f0f4f8;">
<th style="border:1px solid #ccc;padding:6px;">Warehouse</th>
<th style="border:1px solid #ccc;padding:6px;">Brand</th>
<th style="border:1px solid #ccc;padding:6px;">Part number</th>
<th style="border:1px solid #ccc;padding:6px;">Description</th>
<th style="border:1px solid #ccc;padding:6px;">Weight</th>
<th style="border:1px solid #ccc;padding:6px;">Term</th>
<th style="border:1px solid #ccc;padding:6px;">Qty</th>
<th style="border:1px solid #ccc;padding:6px;">Price, AED</th>
<th style="border:1px solid #ccc;padding:6px;">Purchase price</th>
<th style="border:1px solid #ccc;padding:6px;">Amount, AED</th>
<th style="border:1px solid #ccc;padding:6px;">Purchase amount</th>
<th style="border:1px solid #ccc;padding:6px;">Note</th>
</tr>
</thead>
<tbody>
<?php echo $rows_html; ?>
</tbody>
</table>
</div>

<table style="font-family:Calibri,Arial,sans-serif;font-size:14px;margin:16px 0 0 auto;border-collapse:collapse;min-width:320px;float:right;">
<tr><td style="padding:4px 16px 4px 0;text-align:right;">Gross Amount:</td><td style="padding:4px 0;text-align:right;font-weight:bold;"><?php echo $epc_price_dual($gross); ?></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;">VAT 5%:</td><td style="padding:4px 0;text-align:right;font-weight:bold;"><?php echo $epc_price_dual($vat); ?></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;">Net Amount (Including VAT @ 5%):</td><td style="padding:4px 0;text-align:right;font-weight:bold;"><?php echo $epc_price_dual($net); ?></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;">Purchase amount:</td><td style="padding:4px 0;text-align:right;"><?php echo $epc_price_dual($price_purchase_sum_total); ?></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;">Your margin for this order:</td><td style="padding:4px 0;text-align:right;font-weight:bold;color:#1a7f37;"><?php echo $epc_price_dual($margin); ?></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;">Delivery cost:</td><td style="padding:4px 0;text-align:right;"><?php echo $epc_price_dual($delivery_cost); ?></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;font-size:16px;"><strong>TOTAL:</strong></td><td style="padding:4px 0;text-align:right;font-size:16px;"><strong><?php echo $epc_price_dual($total); ?></strong></td></tr>
<tr><td style="padding:4px 16px 4px 0;text-align:right;">Weight:</td><td style="padding:4px 0;text-align:right;"><?php echo $weight_total > 0 ? number_format($weight_total, 3, '.', '') : '—'; ?></td></tr>
</table>
<div style="clear:both;"></div>

<?php if (!empty($order_message['text'])) { ?>
<p style="margin:18px 0 6px;"><strong>Comment:</strong></p>
<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;padding:8px 12px;background:#f9f9f9;border:1px solid #e0e0e0;border-radius:4px;">
<?php echo nl2br(htmlspecialchars((string)$order_message['text'], ENT_QUOTES, 'UTF-8')); ?>
</div>
<?php } ?>

<p style="margin:24px 0 0;">
	<a style="color:<?php echo htmlspecialchars($main_color, ENT_QUOTES, 'UTF-8'); ?>;" href="<?php echo htmlspecialchars($cp_order_url, ENT_QUOTES, 'UTF-8'); ?>" target="_blank">View order in your Control Panel</a>
</p>
</div>
