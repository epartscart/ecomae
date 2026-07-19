<?php
/**
 * One-window OMS console — daily ops: summary, items, customer, payment, docs, timeline, messages.
 * Expects: $order_id, $db_link, $DP_Config, $orders_statuses, $orders_items_statuses,
 *          $offices_list, $orders_items_statuses_not_count, $shop_orders_paid_type (optional),
 *          $storages_list (optional).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_orders_ws_h')) {
	$epc_orders_ws_helpers = $_SERVER['DOCUMENT_ROOT'] . '/'
		. ($GLOBALS['DP_Config']->backend_dir ?? 'cp')
		. '/content/shop/order_process/epc_orders_workspace_helpers.php';
	if (is_file($epc_orders_ws_helpers)) {
		require_once $epc_orders_ws_helpers;
	}
}

$order_id = (int) ($order_id ?? 0);
if ($order_id <= 0) {
	echo '<div class="epc-scp-orders-detail__empty"><i class="fa fa-hand-pointer-o"></i><p>Select an order to manage</p><span class="text-muted small">One-window OMS: summary, items, customer, payment, invoices &amp; documents</span></div>';
	return;
}

$WHERE_statuses_not_count = '';
for ($i = 0; $i < count($orders_items_statuses_not_count); $i++) {
	$WHERE_statuses_not_count .= ' AND `status` != ' . (int) $orders_items_statuses_not_count[$i];
}

$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = `shop_orders`.`id`), 0)";
$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = `shop_orders`.`id`), 0)";
$sub_balance_SQL = '';
if (isset($DP_Config->wholesaler) && !empty($DP_Config->wholesaler)) {
	$sub_balance_SQL = ' AND `office_id` = `shop_orders`.`office_id` ';
}
$INCOME_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `user_id` = `shop_orders`.`user_id` $sub_balance_SQL), 0)";
$ISSUE_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `user_id` = `shop_orders`.`user_id` $sub_balance_SQL), 0)";

$order_query = $db_link->prepare(
	"SELECT *,
		(SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`,
		CAST((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2)) AS `price_sum`,
		CAST((SELECT SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2)) AS `purchase_sum`,
		CAST(($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(20,2)) AS `paid_sum`,
		CAST(($INCOME_USER_SQL - $ISSUE_USER_SQL) AS DECIMAL(20,2)) AS `customer_balance`,
		CAST((
			(SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count)
			- ($ISSUE_SQL - $INCOME_SQL)
		) AS DECIMAL(20,2)) AS `paid_left`,
		GREATEST(
			IFNULL((SELECT MAX(`time`) FROM `shop_orders_logs` WHERE `order_id` = `shop_orders`.`id`), 0),
			IFNULL((SELECT MAX(`time`) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id`), 0),
			`shop_orders`.`time`
		) AS `last_modified`
	FROM `shop_orders` WHERE `id` = ? LIMIT 1"
);
$order_query->execute(array($order_id));
$order = $order_query->fetch(PDO::FETCH_ASSOC);

if (!$order || !isset($offices_list[$order['office_id']])) {
	echo '<div class="epc-scp-orders-detail__empty"><i class="fa fa-exclamation-triangle"></i><p>Order not found or access denied</p></div>';
	return;
}

if (empty($shop_orders_paid_type)) {
	$shop_orders_paid_type = array();
	$q = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`');
	$q->execute();
	while ($rov = $q->fetch()) {
		$shop_orders_paid_type[$rov['id']] = $rov['name'];
	}
}
if (!isset($storages_list) || !is_array($storages_list) || $storages_list === array()) {
	$storages_list = array();
}
// Prefer short warehouse codes (S-UAE) for staff UI; keep full name as fallback.
try {
	$sq = $db_link->query('SELECT `id`, `name`, `short_name` FROM `shop_storages`');
	while ($s = $sq->fetch(PDO::FETCH_ASSOC)) {
		$sid = (int) $s['id'];
		$short = trim((string) ($s['short_name'] ?? ''));
		$full = trim((string) ($s['name'] ?? ''));
		$storages_list[$sid] = $short !== '' ? $short : ($full !== '' ? $full : ('#' . $sid));
	}
} catch (Throwable $e) {
	try {
		$sq = $db_link->query('SELECT `id`,`name` FROM `shop_storages`');
		while ($s = $sq->fetch(PDO::FETCH_ASSOC)) {
			$storages_list[(int) $s['id']] = (string) $s['name'];
		}
	} catch (Throwable $e2) {
	}
}

$db_link->prepare('UPDATE `shop_orders_viewed` SET `viewed_flag` = 1 WHERE `order_id` = ?')->execute(array($order_id));
$db_link->prepare('UPDATE `shop_orders_messages` SET `read` = 1 WHERE `order_id` = ? AND `is_customer` = 1')->execute(array($order_id));

$status = (int) $order['status'];
$paid = (int) $order['paid'];
$paid_type = (int) ($order['paid_type'] ?? 0);
$customer_id = (int) ($order['user_id'] ?? 0);
$backend = trim((string) $DP_Config->backend_dir, '/');
if ($backend === '') {
	$backend = 'cp';
}
$fullUrl = '/' . $backend . '/shop/orders/order?order_id=' . $order_id;
$canEditItems = ($paid === 0);
$csrf = '';
if (!empty($GLOBALS['user_session']['csrf_guard_key'])) {
	$csrf = (string) $GLOBALS['user_session']['csrf_guard_key'];
} elseif (isset($user_session) && !empty($user_session['csrf_guard_key'])) {
	$csrf = (string) $user_session['csrf_guard_key'];
}

$items_query = $db_link->prepare('SELECT * FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`');
$items_query->execute(array($order_id));
$items = $items_query->fetchAll(PDO::FETCH_ASSOC);

$logs = array();
try {
	$lq = $db_link->prepare('SELECT `time`, `text`, `is_manager`, `is_robot` FROM `shop_orders_logs` WHERE `order_id` = ? ORDER BY `id` DESC LIMIT 40');
	$lq->execute(array($order_id));
	$logs = $lq->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$logs = array();
}

$messages = array();
try {
	$mq = $db_link->prepare('SELECT `id`, `text`, `time`, `is_customer` FROM `shop_orders_messages` WHERE `order_id` = ? AND `return_id` = 0 ORDER BY `id` ASC LIMIT 80');
	$mq->execute(array($order_id));
	$messages = $mq->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	$messages = array();
}

$customer_label = '';
$customer_name = '';
$customer_email = '';
$customer_phone = '';
if ($customer_id > 0) {
	$u = $db_link->prepare('SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
	$u->execute(array($customer_id));
	$ur = $u->fetch(PDO::FETCH_ASSOC) ?: array();
	$customer_email = (string) ($ur['email'] ?? '');
	$customer_phone = (string) ($ur['phone'] ?? '');
	$customer_label = 'ID ' . $customer_id;
	if ($customer_email !== '') {
		$customer_label .= ' · ' . $customer_email;
	}
	if ($customer_phone !== '') {
		$customer_label .= ' · ' . $customer_phone;
	}
	try {
		$pn = $db_link->prepare("SELECT MAX(CASE WHEN `data_key` IN ('name','first_name') THEN `value` END) AS n, MAX(CASE WHEN `data_key` IN ('surname','last_name') THEN `value` END) AS s FROM `users_profiles` WHERE `user_id` = ?");
		$pn->execute(array($customer_id));
		$pr = $pn->fetch(PDO::FETCH_ASSOC);
		$customer_name = trim((string) ($pr['n'] ?? '') . ' ' . (string) ($pr['s'] ?? ''));
	} catch (Throwable $e) {
	}
} else {
	$customer_label = translate_str_by_id(3549) . ' (guest)';
	$customer_phone = (string) ($order['phone_not_auth'] ?? '');
	$customer_email = (string) ($order['email_not_auth'] ?? '');
	if ($customer_phone !== '') {
		$customer_label .= ' · ' . $customer_phone;
	}
	if ($customer_email !== '') {
		$customer_label .= ' · ' . $customer_email;
	}
}

$priceSum = (float) $order['price_sum'];
$purchaseSum = (float) ($order['purchase_sum'] ?? 0);
$benefit = $priceSum - $purchaseSum;
$paidSum = (float) ($order['paid_sum'] ?? 0);
$paidLeft = (float) ($order['paid_left'] ?? max(0, $priceSum - $paidSum));
$customerBalance = (float) ($order['customer_balance'] ?? 0);
$lastMod = (int) ($order['last_modified'] ?? $order['time']);
$userMgrUrl = '/' . $backend . '/users/usermanager?user_id=' . $customer_id;

$usdRate = function_exists('epc_orders_ws_usd_rate')
	? epc_orders_ws_usd_rate($db_link, $DP_Config)
	: 3.6725;

// VAT from stored line prices (B2C inclusive → split; never add 5% again).
$vatNet = 0.0;
$vatAmt = 0.0;
$vatGross = 0.0;
$vatInclusive = false;
$vatRateLabel = 5.0;
$vatFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
if (is_file($vatFile)) {
	require_once $vatFile;
	if (function_exists('epc_uae_customer_vat_order_line')) {
		foreach ($items as $vatItem) {
			$line = epc_uae_customer_vat_order_line(
				$db_link,
				$customer_id,
				(float) ($vatItem['price'] ?? 0),
				(float) ($vatItem['count_need'] ?? 0)
			);
			$vatNet += (float) ($line['line_net'] ?? 0);
			$vatAmt += (float) ($line['vat_amount'] ?? 0);
			$vatGross += (float) ($line['gross'] ?? 0);
			$vatInclusive = $vatInclusive || !empty($line['prices_inclusive']);
			if (!empty($line['tax_rate'])) {
				$vatRateLabel = (float) $line['tax_rate'];
			}
		}
	}
}
if ($vatGross <= 0) {
	$vatGross = $priceSum;
}
$vatDueLabel = $vatInclusive
	? ('incl. VAT ' . number_format($vatRateLabel, 0) . '%')
	: ('excl. + VAT ' . number_format($vatRateLabel, 0) . '%');

$itemIds = array();
foreach ($items as $it) {
	$itemIds[] = (int) $it['id'];
}
$itemIdsJson = htmlspecialchars(json_encode($itemIds), ENT_QUOTES, 'UTF-8');

$dcBase = '/content/shop/document_control/service/print.php?order_id=' . $order_id . '&doc=';
$legacyPrintBase = '/content/shop/print_docs/service/print.php?order_id=' . $order_id
	. '&csrf_admin=1&csrf_guard_key=' . rawurlencode($csrf)
	. '&order_items=' . rawurlencode(json_encode($itemIds))
	. '&doc_name=';
?>
<div class="epc-od epc-od--oms" data-order-id="<?php echo (int) $order_id; ?>" data-can-edit="<?php echo $canEditItems ? '1' : '0'; ?>" data-paid-left="<?php echo epc_orders_ws_h(number_format($paidLeft, 2, '.', '')); ?>" data-customer-balance="<?php echo epc_orders_ws_h(number_format($customerBalance, 2, '.', '')); ?>" data-customer-id="<?php echo (int) $customer_id; ?>" data-item-ids="<?php echo $itemIdsJson; ?>">
	<div class="epc-od__head">
		<div class="epc-od__title-row">
			<h3 class="epc-od__title">Order #<?php echo (int) $order_id; ?> <span class="epc-od__oms-tag">OMS</span></h3>
			<span id="epc_od_status_badge"><?php echo epc_orders_ws_status_badge($status, $orders_statuses, $db_link); ?></span>
		</div>
		<div class="epc-od__meta">
			<span><i class="fa fa-clock-o"></i> Created <?php echo epc_orders_ws_h(date('d.m.Y H:i', (int) $order['time'])); ?></span>
			<span><i class="fa fa-refresh"></i> Modified <?php echo epc_orders_ws_h(date('d.m.Y H:i', $lastMod)); ?></span>
			<span><i class="fa fa-building-o"></i> <?php echo epc_orders_ws_h(translate_str_by_id($offices_list[$order['office_id']])); ?></span>
			<span><i class="fa fa-truck"></i> <?php echo epc_orders_ws_h(translate_str_by_id($order['obtain_caption'])); ?></span>
		</div>
		<div class="epc-od__chips">
			<?php echo epc_orders_ws_paid_badge($paid); ?>
			<?php if (!empty($shop_orders_paid_type[$paid_type])) { ?>
			<span class="epc-scp-badge epc-scp-badge--normal"><?php echo epc_orders_ws_h(translate_str_by_id($shop_orders_paid_type[$paid_type])); ?></span>
			<?php } ?>
			<span class="epc-scp-badge epc-scp-badge--normal"><?php echo count($items); ?> items</span>
		</div>
		<div class="epc-od__customer">
			<?php if ($customer_name !== '') { ?><strong><?php echo epc_orders_ws_h($customer_name); ?></strong> · <?php } ?>
			<?php echo epc_orders_ws_h($customer_label); ?>
		</div>
		<div class="epc-od__totals epc-od__totals--primary">
			<div><span>Amount due</span><strong><?php echo epc_orders_ws_h(number_format($vatGross, 2, '.', ' ')); ?></strong>
				<small><?php echo epc_orders_ws_h($vatDueLabel); ?></small></div>
			<div><span>Paid</span><strong class="is-ok"><?php echo epc_orders_ws_h(number_format($paidSum, 2, '.', ' ')); ?></strong></div>
			<div><span>Balance</span><strong class="<?php echo $paidLeft > 0 ? 'is-bad' : 'is-ok'; ?>"><?php echo epc_orders_ws_h(number_format($paidLeft, 2, '.', ' ')); ?></strong></div>
		</div>
		<div class="epc-od__totals epc-od__totals--secondary">
			<div><span>Net (ex VAT)</span><strong><?php echo epc_orders_ws_h(number_format($vatNet > 0 ? $vatNet : $priceSum, 2, '.', ' ')); ?></strong></div>
			<div><span>VAT <?php echo epc_orders_ws_h(number_format($vatRateLabel, 0)); ?>%</span><strong><?php echo epc_orders_ws_h(number_format($vatAmt, 2, '.', ' ')); ?></strong></div>
			<div><span>Purchase</span><strong><?php echo epc_orders_ws_h(number_format($purchaseSum, 2, '.', ' ')); ?></strong></div>
			<div><span>Margin</span><strong class="<?php echo $benefit >= 0 ? 'is-ok' : 'is-bad'; ?>"><?php echo epc_orders_ws_h(number_format($benefit, 2, '.', ' ')); ?></strong>
				<small><?php echo epc_orders_ws_h(function_exists('epc_orders_ws_aed_usd') ? epc_orders_ws_aed_usd($benefit, $usdRate) : ''); ?></small></div>
		</div>
	</div>

	<div id="epc_od_toast" class="epc-od__toast" role="status"></div>

	<nav class="epc-od__tabs" role="tablist">
		<button type="button" class="is-active" data-epc-od-tab="manage">Manage</button>
		<button type="button" data-epc-od-tab="items">Items (<?php echo count($items); ?>)</button>
		<button type="button" data-epc-od-tab="customer">Customer</button>
		<button type="button" data-epc-od-tab="payment">Payment</button>
		<button type="button" data-epc-od-tab="docs">Invoice / docs</button>
		<button type="button" data-epc-od-tab="timeline">Status / time</button>
		<button type="button" data-epc-od-tab="messages">Messages (<?php echo count($messages); ?>)</button>
	</nav>

	<section class="epc-od__panel is-active" data-epc-od-panel="manage">
		<div class="epc-od__edit">
			<div class="epc-od__edit-title">Update order</div>
			<div class="epc-od__edit-row">
				<label for="epc_od_status">Order status</label>
				<select id="epc_od_status" class="form-control">
					<?php foreach ($orders_statuses as $sid => $sdata) { ?>
					<option value="<?php echo (int) $sid; ?>"<?php echo ((int) $sid === $status) ? ' selected' : ''; ?>><?php echo epc_orders_ws_h(translate_str_by_id($sdata['name'])); ?></option>
					<?php } ?>
				</select>
				<button type="button" class="btn btn-primary btn-sm" onclick="epcApplyOrderStatus(<?php echo (int) $order_id; ?>);"><i class="fa fa-check"></i> Update status</button>
			</div>
			<div class="epc-od__edit-row">
				<label for="epc_od_comment">Internal note (status log)</label>
				<textarea id="epc_od_comment" class="form-control" rows="3" placeholder="Internal note for staff…"></textarea>
				<button type="button" class="btn btn-default btn-sm" onclick="epcAddOrderComment(<?php echo (int) $order_id; ?>);"><i class="fa fa-sticky-note-o"></i> Save note</button>
			</div>
		</div>
		<div class="epc-od__actions">
			<button type="button" class="btn btn-default btn-sm" onclick="epcOmsGotoTab('payment');"><i class="fa fa-credit-card"></i> Payment</button>
			<button type="button" class="btn btn-default btn-sm" onclick="epcOmsGotoTab('docs');"><i class="fa fa-file-text-o"></i> Invoice / docs</button>
			<button type="button" class="btn btn-default btn-sm" onclick="epcOmsGotoTab('customer');"><i class="fa fa-user"></i> Customer</button>
			<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($fullUrl); ?>"><i class="fa fa-external-link"></i> Classic full card</a>
		</div>
		<div id="epc-order-fulfillment-panel-<?php echo (int) $order_id; ?>" class="epc-scp-orders-detail__erp-fulfillment"></div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="items">
		<div class="epc-od__items">
			<div class="epc-od__items-head">
				<h4 class="epc-od__items-title">Line items</h4>
				<span class="text-muted small"><?php echo $canEditItems ? 'Invoice-style lines · sell / purchase / margin / USD · warehouse' : 'Paid order — prices locked'; ?></span>
			</div>
			<?php if (!$items) { ?>
			<p class="text-muted">No line items</p>
			<?php } else { ?>
			<div class="epc-od__items-scroll">
			<table class="epc-od__lines">
				<thead>
					<tr>
						<th>#</th>
						<th>Brand</th>
						<th>Part</th>
						<th>Description</th>
						<th>Warehouse</th>
						<th>Qty</th>
						<th>Sell</th>
						<th>Purchase</th>
						<th>Margin</th>
						<th>Amount</th>
						<th>USD</th>
						<th>Status</th>
						<th></th>
					</tr>
				</thead>
				<tbody>
			<?php
			$lineNo = 0;
			foreach ($items as $item) {
				$lineNo++;
				$itemId = (int) $item['id'];
				$itemStatus = (int) $item['status'];
				$storageId = (int) ($item['t2_storage_id'] ?? 0);
				if ($storageId <= 0 && !empty($item['t2_storage']) && ctype_digit((string) $item['t2_storage'])) {
					$storageId = (int) $item['t2_storage'];
				}
				$storageLabel = $storages_list[$storageId] ?? (string) ($item['t2_storage'] ?? '');
				$storageLabel = function_exists('epc_orders_ws_storage_label')
					? epc_orders_ws_storage_label($storageLabel)
					: ($storageLabel !== '' ? $storageLabel : '—');
				$sell = (float) $item['price'];
				$purchase = (float) $item['t2_price_purchase'];
				$qty = max(1, (int) $item['count_need']);
				$lineTotal = $sell * $qty;
				$lineMargin = ($sell - $purchase) * $qty;
				$lineUsd = $usdRate > 0 ? ($lineTotal / $usdRate) : 0.0;
				$statusLabel = translate_str_by_id($orders_items_statuses[$itemStatus]['name'] ?? '');
				?>
					<tr class="epc-od__line" data-item-id="<?php echo $itemId; ?>">
						<td class="epc-od__num"><?php echo (int) $lineNo; ?></td>
						<td class="epc-od__brand"><?php echo epc_orders_ws_h($item['t2_manufacturer']); ?></td>
						<td class="epc-od__part"><code><?php echo epc_orders_ws_h($item['t2_article']); ?></code></td>
						<td class="epc-od__desc" title="<?php echo epc_orders_ws_h($item['t2_name']); ?>">
							<input type="text" class="form-control input-sm" data-field="t2_name" value="<?php echo epc_orders_ws_h($item['t2_name']); ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
						</td>
						<td class="epc-od__wh">
							<select class="form-control input-sm" data-field="t2_storage_id" <?php echo $canEditItems ? '' : 'disabled'; ?>>
								<option value="0">—</option>
								<?php foreach ($storages_list as $sid => $sname) { ?>
								<option value="<?php echo (int) $sid; ?>"<?php echo ((int) $sid === $storageId) ? ' selected' : ''; ?>><?php echo epc_orders_ws_h(epc_orders_ws_storage_label($sname)); ?></option>
								<?php } ?>
							</select>
							<span class="epc-od__wh-label"><?php echo epc_orders_ws_h($storageLabel); ?></span>
						</td>
						<td class="epc-od__qty">
							<input type="number" step="1" min="1" class="form-control input-sm" data-field="count_need" value="<?php echo (int) $qty; ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
						</td>
						<td class="epc-od__sell">
							<input type="number" step="0.01" min="0" class="form-control input-sm" data-field="price" value="<?php echo epc_orders_ws_h(number_format($sell, 2, '.', '')); ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
						</td>
						<td class="epc-od__buy">
							<input type="number" step="0.01" min="0" class="form-control input-sm" data-field="t2_price_purchase" value="<?php echo epc_orders_ws_h(number_format($purchase, 2, '.', '')); ?>" <?php echo $canEditItems ? '' : 'disabled'; ?> />
						</td>
						<td class="epc-od__margin <?php echo $lineMargin >= 0 ? 'is-ok' : 'is-bad'; ?>"><?php echo epc_orders_ws_h(number_format($lineMargin, 2, '.', ',')); ?></td>
						<td class="epc-od__amt"><?php echo epc_orders_ws_h(number_format($lineTotal, 2, '.', ',')); ?></td>
						<td class="epc-od__usd"><?php echo epc_orders_ws_h(number_format($lineUsd, 2, '.', ',')); ?></td>
						<td class="epc-od__status">
							<select class="form-control input-sm" data-field="item_status">
								<?php foreach ($orders_items_statuses as $isid => $isdata) { ?>
								<option value="<?php echo (int) $isid; ?>"<?php echo ((int) $isid === $itemStatus) ? ' selected' : ''; ?>><?php echo epc_orders_ws_h(translate_str_by_id($isdata['name'])); ?></option>
								<?php } ?>
							</select>
						</td>
						<td class="epc-od__acts">
							<?php if ($canEditItems) { ?>
							<button type="button" class="btn btn-primary btn-xs" title="Save" onclick="epcOmsSaveItem(<?php echo (int) $order_id; ?>, <?php echo $itemId; ?>);"><i class="fa fa-save"></i></button>
							<?php } ?>
							<button type="button" class="btn btn-default btn-xs" title="Update status" onclick="epcOmsSetItemStatus(<?php echo (int) $order_id; ?>, <?php echo $itemId; ?>);"><i class="fa fa-flag"></i></button>
							<button type="button" class="btn btn-warning btn-xs" title="Message customer" onclick="epcOmsMessageItem(<?php echo (int) $order_id; ?>, <?php echo $itemId; ?>, <?php echo htmlspecialchars(json_encode((string) $item['t2_article']), ENT_QUOTES, 'UTF-8'); ?>, <?php echo htmlspecialchars(json_encode(number_format($sell, 2, '.', '')), ENT_QUOTES, 'UTF-8'); ?>);"><i class="fa fa-envelope"></i></button>
						</td>
					</tr>
			<?php } ?>
				</tbody>
				<tfoot>
					<tr>
						<td colspan="5" class="epc-od__foot-label">Order summary</td>
						<td></td>
						<td colspan="2" class="text-muted small"><?php echo epc_orders_ws_h($vatDueLabel); ?></td>
						<td class="<?php echo $benefit >= 0 ? 'is-ok' : 'is-bad'; ?>"><?php echo epc_orders_ws_h(number_format($benefit, 2, '.', ',')); ?></td>
						<td><strong><?php echo epc_orders_ws_h(number_format($vatGross, 2, '.', ',')); ?></strong></td>
						<td><?php echo epc_orders_ws_h(number_format($usdRate > 0 ? $vatGross / $usdRate : 0, 2, '.', ',')); ?></td>
						<td colspan="2" class="text-muted small">VAT <?php echo epc_orders_ws_h(number_format($vatAmt, 2, '.', ',')); ?></td>
					</tr>
				</tfoot>
			</table>
			</div>
			<?php } ?>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="customer">
		<div class="epc-od__customer-panel">
			<div class="epc-od__edit-title">Customer</div>
			<div class="epc-od__kv">
				<div><span>Name</span><strong><?php echo epc_orders_ws_h($customer_name !== '' ? $customer_name : '—'); ?></strong></div>
				<div><span>Type</span><strong><?php echo $customer_id > 0 ? 'Registered #' . $customer_id : 'Guest'; ?></strong></div>
				<div><span>Email</span><strong><?php echo epc_orders_ws_h($customer_email !== '' ? $customer_email : '—'); ?></strong></div>
				<div><span>Phone</span><strong><?php echo epc_orders_ws_h($customer_phone !== '' ? $customer_phone : '—'); ?></strong></div>
				<?php if ($customer_id > 0) { ?>
				<div><span>Account balance</span><strong><?php echo epc_orders_ws_h(number_format($customerBalance, 2, '.', ' ')); ?></strong></div>
				<?php } ?>
			</div>
			<div class="epc-od__actions">
				<?php if ($customer_id > 0) { ?>
				<a class="btn btn-default btn-sm" href="<?php echo epc_orders_ws_h($userMgrUrl); ?>"><i class="fa fa-user"></i> Open customer account</a>
				<button type="button" class="btn btn-default btn-sm" onclick="showCustomerModalInfo(<?php echo (int) $customer_id; ?>);"><i class="fa fa-info-circle"></i> Quick profile</button>
				<?php } ?>
				<button type="button" class="btn btn-primary btn-sm" onclick="epcOmsGotoTab('messages');"><i class="fa fa-envelope"></i> Message customer</button>
			</div>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="payment">
		<div class="epc-od__pay">
			<div class="epc-od__edit-title">Payment</div>
			<div class="epc-od__kv">
				<div><span>Order amount</span><strong><?php echo epc_orders_ws_h(number_format($priceSum, 2, '.', ' ')); ?></strong></div>
				<div><span>Paid</span><strong class="is-ok"><?php echo epc_orders_ws_h(number_format($paidSum, 2, '.', ' ')); ?></strong></div>
				<div><span>Balance due</span><strong class="<?php echo $paidLeft > 0 ? 'is-bad' : 'is-ok'; ?>"><?php echo epc_orders_ws_h(number_format($paidLeft, 2, '.', ' ')); ?></strong></div>
				<div><span>Paid flag</span><strong><?php echo (int) $paid; ?></strong></div>
				<?php if (!empty($shop_orders_paid_type[$paid_type])) { ?>
				<div><span>Method</span><strong><?php echo epc_orders_ws_h(translate_str_by_id($shop_orders_paid_type[$paid_type])); ?></strong></div>
				<?php } ?>
			</div>
			<?php if ($paidLeft > 0.0001) { ?>
			<div class="epc-od__edit" style="margin-top:12px;">
				<div class="epc-od__edit-title">Record payment</div>
				<div class="epc-od__edit-row">
					<label for="epc_od_pay_value">Amount</label>
					<input type="number" step="0.01" min="0.01" id="epc_od_pay_value" class="form-control" value="<?php echo epc_orders_ws_h(number_format($paidLeft, 2, '.', '')); ?>" />
				</div>
				<?php if ($customer_id > 0) { ?>
				<div class="epc-od__pay-source">
					<label><input type="radio" name="epc_od_pay_source" value="1" checked /> Direct payment (cash / card / transfer)</label>
					<label><input type="radio" name="epc_od_pay_source" value="0" /> From customer balance (<?php echo epc_orders_ws_h(number_format($customerBalance, 2, '.', ' ')); ?>)</label>
				</div>
				<?php } else { ?>
				<input type="hidden" name="epc_od_pay_source" value="1" />
				<?php } ?>
				<button type="button" class="btn btn-success btn-sm" onclick="epcOmsPayOrder(<?php echo (int) $order_id; ?>);"><i class="fa fa-check"></i> Apply payment</button>
			</div>
			<?php } else { ?>
			<p class="text-success" style="margin-top:10px;"><i class="fa fa-check-circle"></i> Order is fully paid.</p>
			<?php } ?>
			<?php if ($paidSum > 0) { ?>
			<div class="epc-od__actions" style="margin-top:12px;">
				<button type="button" class="btn btn-default btn-sm" onclick="epcOmsRefundOrder(<?php echo (int) $order_id; ?>, 0);"><i class="fa fa-undo"></i> Refund to balance</button>
				<button type="button" class="btn btn-warning btn-sm" onclick="epcOmsRefundOrder(<?php echo (int) $order_id; ?>, 1);"><i class="fa fa-money"></i> Direct refund</button>
			</div>
			<?php } ?>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="docs">
		<div class="epc-od__docs">
			<div class="epc-od__edit-title">Invoice &amp; documents</div>
			<p class="text-muted small">Open documents for this order in a new tab. Document Control templates + classic print docs.</p>
			<div class="epc-od__doc-grid">
				<a class="epc-od__doc-btn" target="_blank" rel="noopener" href="<?php echo epc_orders_ws_h($dcBase . 'fta_tax_invoice'); ?>"><i class="fa fa-file-text"></i> UAE tax invoice</a>
				<a class="epc-od__doc-btn" target="_blank" rel="noopener" href="<?php echo epc_orders_ws_h($dcBase . 'packing_slip'); ?>"><i class="fa fa-truck"></i> Packing slip</a>
				<a class="epc-od__doc-btn" target="_blank" rel="noopener" href="<?php echo epc_orders_ws_h($dcBase . 'delivery_note'); ?>"><i class="fa fa-file-o"></i> Delivery note</a>
				<a class="epc-od__doc-btn" target="_blank" rel="noopener" href="<?php echo epc_orders_ws_h($dcBase . 'payment_receipt'); ?>"><i class="fa fa-receipt"></i> Payment receipt</a>
				<a class="epc-od__doc-btn" target="_blank" rel="noopener" href="<?php echo epc_orders_ws_h($legacyPrintBase . 'invoice_for_payment'); ?>"><i class="fa fa-print"></i> Invoice for payment</a>
				<a class="epc-od__doc-btn" target="_blank" rel="noopener" href="<?php echo epc_orders_ws_h($legacyPrintBase . 'sales_receipt'); ?>"><i class="fa fa-print"></i> Sales receipt</a>
			</div>
			<div class="epc-od__actions" style="margin-top:12px;">
				<a class="btn btn-default btn-sm" href="/<?php echo epc_orders_ws_h($backend); ?>/shop/document_control/document_control?order_id=<?php echo (int) $order_id; ?>"><i class="fa fa-folder-open"></i> Document Control module</a>
			</div>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="timeline">
		<div class="epc-od__logs epc-od__logs--full">
			<h5><i class="fa fa-history"></i> Status &amp; activity by time</h5>
			<?php if (!$logs) { ?>
			<p class="text-muted">No log entries yet</p>
			<?php } else { ?>
			<ol class="epc-od__timeline">
				<?php foreach ($logs as $log) {
					$who = !empty($log['is_robot']) ? 'robot' : (!empty($log['is_manager']) ? 'staff' : 'system');
					?>
				<li>
					<time><?php echo epc_orders_ws_h(date('d.m.Y H:i:s', (int) $log['time'])); ?></time>
					<span class="epc-od__timeline-who"><?php echo epc_orders_ws_h($who); ?></span>
					<div><?php echo $log['text']; ?></div>
				</li>
				<?php } ?>
			</ol>
			<?php } ?>
		</div>
	</section>

	<section class="epc-od__panel" data-epc-od-panel="messages">
		<div class="epc-od__chat">
			<div class="epc-od__edit-title">Message to customer</div>
			<p class="text-muted small">Order-wide message, or use “Message customer about item” on a line for price-change / item-specific notes.</p>
			<div id="epc_od_chat_thread" class="epc-od__chat-thread">
				<?php if (!$messages) { ?>
				<div class="text-muted">No messages yet</div>
				<?php } else { foreach ($messages as $m) {
					$cls = !empty($m['is_customer']) ? 'is-customer' : 'is-staff';
					$text = html_entity_decode((string) $m['text'], ENT_QUOTES, 'UTF-8');
					?>
				<div class="epc-od__chat-bubble <?php echo $cls; ?>">
					<time><?php echo epc_orders_ws_h(date('d.m.Y H:i', (int) $m['time'])); ?> · <?php echo !empty($m['is_customer']) ? 'Customer' : 'Staff'; ?></time>
					<div><?php echo nl2br(epc_orders_ws_h($text)); ?></div>
				</div>
				<?php } } ?>
			</div>
			<input type="hidden" id="epc_od_msg_item_id" value="0" />
			<div id="epc_od_msg_item_hint" class="epc-od__msg-hint" style="display:none;"></div>
			<textarea id="epc_od_msg_text" class="form-control" rows="3" placeholder="Write a message to the customer…"></textarea>
			<div class="epc-od__chat-actions">
				<button type="button" class="btn btn-primary btn-sm" onclick="epcOmsSendMessage(<?php echo (int) $order_id; ?>);"><i class="fa fa-paper-plane"></i> Send to customer</button>
				<button type="button" class="btn btn-default btn-sm" onclick="epcOmsClearItemMsg();">Clear item context</button>
			</div>
		</div>
	</section>
</div>
