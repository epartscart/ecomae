<?php
/**
 * Customer: list quote requests, view detail, submit draft, accept quoted offer.
 */
defined('_ASTEXE_') or die('No access');

require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/dp_user.php');
$user_id = DP_User::getUserId();
$admin_id = DP_User::getAdminId();
$is_admin_viewer = $admin_id > 0;

$lang_href = !empty($multilang_params['lang_href']) ? $multilang_params['lang_href'] : '';

require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/pricing/epc_currency.php');
$epc_currency_records = epc_currency_records($db_link, $DP_Config);
$epc_selected_currency_iso = epc_currency_selected_iso($epc_currency_records, $DP_Config);
function epc_quote_money($amount)
{
	global $epc_currency_records, $epc_selected_currency_iso, $DP_Config;
	return epc_currency_format_amount($amount, $epc_currency_records, $epc_selected_currency_iso, $DP_Config->currency_show_mode);
}

$detail_id = isset($_GET['id']) ? (int) $_GET['id'] : 0;

if ($user_id <= 0 && !$is_admin_viewer) {
	?>
	<div class="epc-quotes-panel epc-quotes-panel--login">
		<div class="epc-quotes-panel__icon"><i class="fa fa-file-text-o" aria-hidden="true"></i></div>
		<div class="epc-quotes-panel__content">
			<span class="epc-quotes-panel__eyebrow">Customer quotes</span>
			<h2><?php echo $detail_id > 0 ? 'Quote #'.(int) $detail_id.' is protected' : 'Login required to view quotes'; ?></h2>
			<p><?php echo translate_str_by_id(4559); ?> Please log in with the customer account that created the quote request. Administrators can open quote details in the control panel.</p>
			<div class="epc-quotes-panel__chips">
				<span><i class="fa fa-lock" aria-hidden="true"></i> Private customer records</span>
				<span><i class="fa fa-shopping-cart" aria-hidden="true"></i> Add quoted items to cart</span>
			</div>
			<?php if ($detail_id > 0) { ?>
				<div class="epc-quotes-panel__actions">
					<a class="btn btn-ar btn-primary" href="/<?php echo htmlspecialchars($DP_Config->backend_dir); ?>/shop/quote-requests?quote_id=<?php echo (int) $detail_id; ?>">Open quote #<?php echo (int) $detail_id; ?> in control panel</a>
				</div>
			<?php } ?>
		</div>
	</div>
	<div class="panel panel-primary epc-quotes-login-form">
	<?php
	$login_form_postfix = 'my_quotes';
	require($_SERVER['DOCUMENT_ROOT'].'/modules/login/login_form_general.php');
	?>
	</div>
	<?php
	return;
}

if ($detail_id > 0) {
	if ($is_admin_viewer) {
		$q = $db_link->prepare('SELECT * FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1');
		$q->execute(array($detail_id));
	} else {
		$q = $db_link->prepare('SELECT * FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1');
		$q->execute(array($detail_id, $user_id));
	}
	$quote = $q->fetch(PDO::FETCH_ASSOC);
	if (!$quote) {
		?>
		<div class="epc-quotes-panel epc-quotes-panel--empty">
			<div class="epc-quotes-panel__icon"><i class="fa fa-search" aria-hidden="true"></i></div>
			<div class="epc-quotes-panel__content">
				<span class="epc-quotes-panel__eyebrow">Quote lookup</span>
				<h2>Quote #<?php echo (int) $detail_id; ?> not found</h2>
				<p>The quote may not exist, or it may belong to another customer account.</p>
				<a class="btn btn-ar btn-primary" href="<?php echo htmlspecialchars($lang_href); ?>/shop/quotes">Back to quotes</a>
			</div>
		</div>
		<?php
		return;
	}

	$iq = $db_link->prepare('SELECT * FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC');
	$iq->execute(array($detail_id));
	$lines = $iq->fetchAll(PDO::FETCH_ASSOC);

	$status_label = htmlspecialchars($quote['status']);
	?>
	<div class="epc-quotes-panel">
		<div class="epc-quotes-panel__icon"><i class="fa fa-file-text-o" aria-hidden="true"></i></div>
		<div class="epc-quotes-panel__content">
			<span class="epc-quotes-panel__eyebrow"><?php echo $is_admin_viewer ? 'Administrator quote view' : 'Customer quote'; ?></span>
			<h2>Quote #<?php echo (int) $quote['id']; ?></h2>
			<p>Status: <strong><?php echo $status_label; ?></strong><?php echo $is_admin_viewer ? ' · Customer user ID: '.(int) $quote['user_id'] : ''; ?></p>
			<div class="epc-quotes-panel__chips">
				<span><i class="fa fa-clock-o" aria-hidden="true"></i> Updated <?php echo $quote['time_updated'] ? date('Y-m-d H:i', (int) $quote['time_updated']) : 'not yet'; ?></span>
				<?php if ($is_admin_viewer) { ?>
					<span><i class="fa fa-user-secret" aria-hidden="true"></i> Admin access enabled</span>
				<?php } ?>
			</div>
		</div>
	</div>
	<div class="row epc-quotes-detail">
		<div class="col-md-12">
			<p><a class="btn btn-ar btn-default" href="<?php echo htmlspecialchars($lang_href); ?>/shop/quotes">&larr; All quotes</a></p>
			<?php if ($quote['customer_note'] !== null && $quote['customer_note'] !== '') { ?>
				<p><strong>Your note:</strong> <?php echo nl2br(htmlspecialchars($quote['customer_note'])); ?></p>
			<?php } ?>
			<?php if ($quote['admin_note'] !== null && $quote['admin_note'] !== '') { ?>
				<p><strong>Staff note:</strong> <?php echo nl2br(htmlspecialchars($quote['admin_note'])); ?></p>
			<?php } ?>

			<table class="table table-bordered table-condensed">
				<thead>
					<tr>
						<th>Manufacturer</th>
						<th>Article</th>
						<th>Name</th>
						<th>Qty</th>
						<th>Quoted price</th>
						<th>Lead time (days)</th>
						<th>Line note</th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($lines as $ln) {
					$po = json_decode($ln['product_object_json'], true);
					$m = is_array($po) && isset($po['manufacturer']) ? htmlspecialchars($po['manufacturer']) : '';
					$a = is_array($po) && isset($po['article_show']) ? htmlspecialchars($po['article_show']) : '';
					$n = is_array($po) && isset($po['name']) ? htmlspecialchars($po['name']) : '';
					$qp = $ln['quoted_price'] !== null ? htmlspecialchars(epc_quote_money((float) $ln['quoted_price'])) : '—';
					$lt = $ln['quoted_time_to_exe'] !== null ? (int) $ln['quoted_time_to_exe'] : '—';
					$lnote = $ln['line_admin_note'] ? htmlspecialchars($ln['line_admin_note']) : '';
					?>
					<tr>
						<td><?php echo $m; ?></td>
						<td><?php echo $a; ?></td>
						<td><?php echo $n; ?></td>
						<td><?php echo (int) $ln['count_need']; ?></td>
						<td><?php echo $qp; ?></td>
						<td><?php echo $lt; ?></td>
						<td><?php echo $lnote; ?></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>

			<?php if (!$is_admin_viewer && $quote['status'] === 'draft' && count($lines) > 0) { ?>
				<div class="form-group">
					<label>Message to sales (optional)</label>
					<textarea class="form-control" id="quote_customer_note" rows="3"><?php echo htmlspecialchars($quote['customer_note']); ?></textarea>
				</div>
				<button type="button" class="btn btn-primary" id="btn_submit_quote">Submit for quote</button>
				<script>
				jQuery('#btn_submit_quote').on('click', function() {
					jQuery.post('/content/shop/order_process/ajax_quote_submit.php', {
						quote_id: <?php echo (int) $detail_id; ?>,
						customer_note: jQuery('#quote_customer_note').val()
					}, function(r) {
						if (r.status) { location.reload(); }
						else { alert(r.message || 'Error'); }
					}, 'json');
				});
				</script>
			<?php } ?>

			<?php if (!$is_admin_viewer && $quote['status'] === 'quoted') { ?>
				<p>Review the prices above, then add everything to your cart and proceed to checkout.</p>
				<button type="button" class="btn btn-success" id="btn_accept_quote">Accept and add to cart</button>
				<script>
				jQuery('#btn_accept_quote').on('click', function() {
					if (!confirm('Add quoted lines to your cart?')) return;
					jQuery.post('/content/shop/order_process/ajax_quote_accept.php', {
						quote_id: <?php echo (int) $detail_id; ?>
					}, function(r) {
						if (r.status) {
							window.location.href = '<?php echo htmlspecialchars($lang_href, ENT_QUOTES); ?>/shop/cart';
						} else {
							alert(r.message || 'Error');
						}
					}, 'json');
				});
				</script>
			<?php } ?>
			<?php if ($is_admin_viewer) { ?>
				<p class="epc-quotes-admin-note">You are viewing this quote with administrator access. Use the control panel to edit prices, lead time, and staff notes.</p>
				<a class="btn btn-ar btn-primary" href="/<?php echo htmlspecialchars($DP_Config->backend_dir); ?>/shop/quote-requests?quote_id=<?php echo (int) $quote['id']; ?>">Open in control panel</a>
			<?php } ?>
		</div>
	</div>
	<?php
	return;
}

if ($is_admin_viewer) {
	$list = $db_link->prepare('SELECT * FROM `shop_quote_requests` ORDER BY `id` DESC');
	$list->execute();
} else {
	$list = $db_link->prepare('SELECT * FROM `shop_quote_requests` WHERE `user_id` = ? ORDER BY `id` DESC');
	$list->execute(array($user_id));
}
$rows = $list->fetchAll(PDO::FETCH_ASSOC);
?>
<div class="epc-quotes-panel">
	<div class="epc-quotes-panel__icon"><i class="fa fa-list-alt" aria-hidden="true"></i></div>
	<div class="epc-quotes-panel__content">
		<span class="epc-quotes-panel__eyebrow"><?php echo $is_admin_viewer ? 'Administrator quote list' : 'Customer quotes'; ?></span>
		<h2><?php echo $is_admin_viewer ? 'All customer quotes' : 'My quotes'; ?></h2>
		<p><?php echo $is_admin_viewer ? 'You are viewing quote requests with backend administrator access.' : 'Track your requested prices, staff replies, and quoted items.'; ?></p>
	</div>
</div>
<div class="row">
	<div class="col-md-12">
		<?php if (count($rows) === 0) { ?>
			<div class="epc-quotes-empty">No quotes yet. Use &quot;Add to quote&quot; on part search results.</div>
		<?php } else { ?>
			<table class="table table-striped">
				<thead>
					<tr>
						<th>ID</th>
						<?php if ($is_admin_viewer) { ?><th>User</th><?php } ?>
						<th>Status</th>
						<th>Updated</th>
						<th></th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($rows as $r) { ?>
					<tr>
						<td><?php echo (int) $r['id']; ?></td>
						<?php if ($is_admin_viewer) { ?><td><?php echo (int) $r['user_id']; ?></td><?php } ?>
						<td><?php echo htmlspecialchars($r['status']); ?></td>
						<td><?php echo $r['time_updated'] ? date('Y-m-d H:i', (int) $r['time_updated']) : ''; ?></td>
						<td><a href="<?php echo htmlspecialchars($lang_href); ?>/shop/quotes?id=<?php echo (int) $r['id']; ?>">View</a></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
		<?php } ?>
	</div>
</div>
