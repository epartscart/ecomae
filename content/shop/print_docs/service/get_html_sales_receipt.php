<?php
/**
 * Sales receipt HTML for order print (legacy print_docs module).
 * Expects: $db_link, $user_id, $order_id, $DP_Config
 */
defined('_INTASK_') or die('No access');

$order_id = (int) $order_id;

if (DP_User::isAdmin() || (method_exists('DP_User', 'isBackendGroup') && DP_User::isBackendGroup())) {
	$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ?;');
	$order_query->execute(array($order_id));
	$order_record = $order_query->fetch(PDO::FETCH_ASSOC);
} else {
	if ($user_id <= 0) {
		http_response_code(403);
		exit('Not authorized');
	}
	$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `user_id` = ? AND `id` = ?;');
	$order_query->execute(array($user_id, $order_id));
	$order_record = $order_query->fetch(PDO::FETCH_ASSOC);
}

if ($order_record === false || !is_array($order_record)) {
	http_response_code(404);
	exit('No such order');
}

$items = array();
$items_query = $db_link->prepare(
	'SELECT `t2_manufacturer`, `t2_article`, `t2_name`, `price`, `count_need`
	 FROM `shop_orders_items`
	 WHERE `order_id` = ?
	 ORDER BY `id` ASC;'
);
$items_query->execute(array($order_id));
while ($row = $items_query->fetch(PDO::FETCH_ASSOC)) {
	$items[] = $row;
}

$currency = 'AED';
try {
	$cur_q = $db_link->prepare('SELECT `sign`, `caption_short` FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1;');
	$cur_q->execute(array($DP_Config->shop_currency));
	$cur = $cur_q->fetch(PDO::FETCH_ASSOC);
	if ($cur) {
		$currency = !empty($cur['caption_short']) ? (string) $cur['caption_short'] : (string) $cur['sign'];
	}
} catch (Throwable $e) {
	// keep AED default
}

$company_name = 'ePartsCart';
$company_phone = '';
$company_address = '';
try {
	if ($db_link->query("SHOW TABLES LIKE 'epc_document_company'")->fetchColumn()) {
		$co = $db_link->query('SELECT `legal_name`, `trade_name`, `phone`, `address_line1`, `city`, `country` FROM `epc_document_company` WHERE `id` = 1 LIMIT 1')->fetch(PDO::FETCH_ASSOC);
		if ($co) {
			$company_name = trim((string) ($co['trade_name'] ?: $co['legal_name'])) ?: $company_name;
			$company_phone = (string) ($co['phone'] ?? '');
			$company_address = trim(implode(', ', array_filter(array(
				(string) ($co['address_line1'] ?? ''),
				(string) ($co['city'] ?? ''),
				(string) ($co['country'] ?? ''),
			))));
		}
	}
} catch (Throwable $e) {
	// defaults
}

$buyer = '';
if (!empty($order_record['user_id'])) {
	try {
		$uq = $db_link->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1;');
		$uq->execute(array((int) $order_record['user_id']));
		$u = $uq->fetch(PDO::FETCH_ASSOC);
		if ($u) {
			$buyer = trim(implode(' / ', array_filter(array((string) $u['email'], (string) $u['phone']))));
		}
	} catch (Throwable $e) {
		// ignore
	}
}
if ($buyer === '') {
	$buyer = trim(implode(' / ', array_filter(array(
		(string) ($order_record['email_not_auth'] ?? ''),
		(string) ($order_record['phone_not_auth'] ?? ''),
	))));
}
if ($buyer === '') {
	$buyer = 'Customer #' . (int) $order_record['user_id'];
}

$order_time = !empty($order_record['time']) ? date('Y-m-d H:i', (int) $order_record['time']) : date('Y-m-d H:i');
$total = 0.0;
$epc_print_doc_title = (!empty($epc_print_doc_title) && is_string($epc_print_doc_title))
	? $epc_print_doc_title
	: 'Sales receipt';

ob_start();
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title><?php echo htmlspecialchars($epc_print_doc_title, ENT_QUOTES, 'UTF-8'); ?> #<?php echo (int) $order_id; ?></title>
<style>
body { font-family: Arial, Helvetica, sans-serif; font-size: 13px; color: #111; margin: 24px; }
h1 { font-size: 22px; margin: 0 0 6px; }
.muted { color: #555; font-size: 12px; }
table { width: 100%; border-collapse: collapse; margin-top: 16px; }
th, td { border: 1px solid #ccc; padding: 8px; vertical-align: top; }
th { background: #f5f5f5; text-align: left; }
.right { text-align: right; }
.totals { width: 280px; margin-left: auto; margin-top: 12px; }
.sign { margin-top: 36px; }
@media print { body { margin: 12px; } .no-print { display: none; } }
</style>
</head>
<body>
<div class="no-print" style="margin-bottom:12px;">
	<button onclick="window.print()">Print</button>
</div>
<h1><?php echo htmlspecialchars($epc_print_doc_title, ENT_QUOTES, 'UTF-8'); ?></h1>
<div class="muted"><?php echo htmlspecialchars($company_name, ENT_QUOTES, 'UTF-8'); ?>
<?php if ($company_address !== '') { ?> — <?php echo htmlspecialchars($company_address, ENT_QUOTES, 'UTF-8'); ?><?php } ?>
<?php if ($company_phone !== '') { ?> — <?php echo htmlspecialchars($company_phone, ENT_QUOTES, 'UTF-8'); ?><?php } ?>
</div>
<p>
	<strong>Receipt / Order No:</strong> <?php echo (int) $order_id; ?><br />
	<strong>Date:</strong> <?php echo htmlspecialchars($order_time, ENT_QUOTES, 'UTF-8'); ?><br />
	<strong>Customer:</strong> <?php echo htmlspecialchars($buyer, ENT_QUOTES, 'UTF-8'); ?>
</p>

<table>
	<thead>
		<tr>
			<th style="width:40px;">#</th>
			<th>Brand</th>
			<th>Part number</th>
			<th>Description</th>
			<th class="right" style="width:70px;">Qty</th>
			<th class="right" style="width:100px;">Price</th>
			<th class="right" style="width:110px;">Amount</th>
		</tr>
	</thead>
	<tbody>
	<?php
	$n = 0;
	foreach ($items as $item) {
		$n++;
		$qty = (float) $item['count_need'];
		$price = (float) $item['price'];
		$amount = $qty * $price;
		$total += $amount;
		?>
		<tr>
			<td><?php echo $n; ?></td>
			<td><?php echo htmlspecialchars((string) $item['t2_manufacturer'], ENT_QUOTES, 'UTF-8'); ?></td>
			<td><?php echo htmlspecialchars((string) $item['t2_article'], ENT_QUOTES, 'UTF-8'); ?></td>
			<td><?php echo htmlspecialchars((string) $item['t2_name'], ENT_QUOTES, 'UTF-8'); ?></td>
			<td class="right"><?php echo rtrim(rtrim(number_format($qty, 2, '.', ''), '0'), '.'); ?></td>
			<td class="right"><?php echo number_format($price, 2, '.', ','); ?></td>
			<td class="right"><?php echo number_format($amount, 2, '.', ','); ?></td>
		</tr>
		<?php
	}
	if ($n === 0) {
		?>
		<tr><td colspan="7">No order lines found.</td></tr>
		<?php
	}
	?>
	</tbody>
</table>

<table class="totals">
	<tr>
		<th>Total (<?php echo htmlspecialchars($currency, ENT_QUOTES, 'UTF-8'); ?>)</th>
		<th class="right"><?php echo number_format($total, 2, '.', ','); ?></th>
	</tr>
</table>

<div class="sign">
	<p><strong>Received by (name &amp; signature):</strong> _______________________________</p>
	<p><strong>Date:</strong> __________</p>
</div>
</body>
</html>
<?php
$HTML = ob_get_clean();
?>
