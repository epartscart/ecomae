<?php
/**
 * CP Return detail — process lines against the linked order.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_returns_process.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
$user_session = DP_User::getAdminSession();
$automation = epc_returns_ensure_automation($db_link);

$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');
$return_id = isset($_GET['return_id']) ? (int) $_GET['return_id'] : 0;

if ($return_id < 1) {
	echo '<div class="col-lg-12"><div class="alert alert-warning">Missing return_id.</div></div>';
	return;
}

$q = $db_link->prepare('SELECT r.*, s.`caption` AS `status_caption`, s.`color` AS `status_color` FROM `shop_orders_returns` r LEFT JOIN `shop_orders_returns_statuses` s ON s.`id` = r.`status_id` WHERE r.`id` = ? LIMIT 1');
$q->execute(array($return_id));
$return = $q->fetch(PDO::FETCH_ASSOC);
if (!$return) {
	echo '<div class="col-lg-12"><div class="alert alert-warning">Return #'.(int) $return_id.' not found.</div></div>';
	return;
}

// Mark customer messages read for staff.
$db_link->prepare('UPDATE `shop_orders_messages` SET `read` = 1 WHERE `return_id` = ? AND `is_customer` = 1')->execute(array($return_id));

$orderIds = epc_returns_order_ids($db_link, $return_id);
$primaryOrder = $orderIds ? $orderIds[0] : 0;

$custQ = $db_link->prepare('SELECT `user_id`,`email`,`phone` FROM `users` WHERE `user_id` = ? LIMIT 1');
$custQ->execute(array((int) $return['user_id']));
$customer = $custQ->fetch(PDO::FETCH_ASSOC) ?: array();

$linesQ = $db_link->prepare(
	'SELECT ri.`id`, ri.`comment`, ri.`reason_id`, ri.`return_id`, ri.`item_id`, ri.`return_success`, ri.`count_need` AS `return_qty`,
		rr.`caption` AS `reason_caption`,
		oi.`order_id`, oi.`price`, oi.`count_need` AS `order_qty`, oi.`t2_manufacturer`, oi.`t2_article`, oi.`t2_name`, oi.`status` AS `item_status`
	 FROM `shop_orders_returns_items` ri
	 LEFT JOIN `shop_orders_returns_reasons` rr ON rr.`id` = ri.`reason_id`
	 LEFT JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
	 WHERE ri.`return_id` = ?
	 ORDER BY ri.`id` ASC'
);
$linesQ->execute(array($return_id));
$lines = $linesQ->fetchAll(PDO::FETCH_ASSOC);

$statuses = $db_link->query('SELECT * FROM `shop_orders_returns_statuses` ORDER BY `id` ASC')->fetchAll(PDO::FETCH_ASSOC);
$csrf = htmlspecialchars((string) ($user_session['csrf_guard_key'] ?? ''), ENT_QUOTES, 'UTF-8');
?>
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Return #<?php echo (int) $return_id; ?>
			<?php if ($primaryOrder > 0) { ?>
				— against Order #<?php echo (int) $primaryOrder; ?>
			<?php } ?>
		</div>
		<div class="panel-body">
			<p><a href="/<?php echo $backend; ?>/shop/returns-manager">&larr; Back to list</a></p>

			<div class="well" style="margin-bottom:18px;">
				<table class="table table-condensed" style="margin:0; max-width:820px;">
					<tbody>
						<tr>
							<th style="width:180px;">Against order</th>
							<td>
								<?php if (!$orderIds) { ?>
									<span class="text-danger">No order linked — line items may be missing.</span>
								<?php } else {
									foreach ($orderIds as $oid) { ?>
										<a class="btn btn-sm btn-default" style="margin-right:6px;" href="/<?php echo $backend; ?>/shop/orders/order?order_id=<?php echo $oid; ?>" target="_blank">
											<i class="fa fa-file-text-o"></i> Order #<?php echo $oid; ?>
										</a>
									<?php }
								} ?>
							</td>
						</tr>
						<tr>
							<th>Customer</th>
							<td>
								<a href="/<?php echo $backend; ?>/users/usermanager/user?user_id=<?php echo (int) $return['user_id']; ?>" target="_blank">
									ID <?php echo (int) $return['user_id']; ?>
									<?php if (!empty($customer['email'])) { echo ' · '.htmlspecialchars($customer['email'], ENT_QUOTES, 'UTF-8'); } ?>
									<?php if (!empty($customer['phone'])) { echo ' · '.htmlspecialchars($customer['phone'], ENT_QUOTES, 'UTF-8'); } ?>
								</a>
							</td>
						</tr>
						<tr>
							<th>Status</th>
							<td>
								<span class="label" style="background:<?php echo htmlspecialchars((string) $return['status_color'], ENT_QUOTES, 'UTF-8'); ?>; color:#222;">
									<?php echo htmlspecialchars(epc_returns_label((string) $return['status_caption']), ENT_QUOTES, 'UTF-8'); ?>
								</span>
								<?php if ((int) $return['return_complete'] === 1) { ?>
									<span class="label label-success">Complete</span>
								<?php } ?>
							</td>
						</tr>
						<tr>
							<th>Declared sum</th>
							<td><?php echo number_format((float) $return['sum'], 2, '.', ' '); ?></td>
						</tr>
					</tbody>
				</table>
			</div>

			<form method="post" action="/<?php echo $backend; ?>/content/shop/returns/ajax/ajax_return_action.php" id="epc-return-status-form" class="form-inline" style="margin-bottom:18px;">
				<input type="hidden" name="action" value="set_return_status" />
				<input type="hidden" name="return_id" value="<?php echo (int) $return_id; ?>" />
				<input type="hidden" name="csrf_guard_key" value="<?php echo $csrf; ?>" />
				<label>Set return status</label>
				<select name="status_id" class="form-control">
					<?php foreach ($statuses as $st) { ?>
						<option value="<?php echo (int) $st['id']; ?>" <?php echo (int) $return['status_id'] === (int) $st['id'] ? 'selected' : ''; ?>>
							<?php echo htmlspecialchars(epc_returns_label($st['caption']), ENT_QUOTES, 'UTF-8'); ?>
						</option>
					<?php } ?>
				</select>
				<button type="submit" class="btn btn-primary">Update status</button>
			</form>

			<div class="table-responsive">
				<table class="table table-bordered table-condensed">
					<thead>
						<tr>
							<th>Line</th>
							<th>Order</th>
							<th>Part</th>
							<th>Qty</th>
							<th>Price</th>
							<th>Reason</th>
							<th>Decision</th>
							<th>Actions</th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ($lines as $ln) {
						$decision = $ln['return_success'];
						$decisionLabel = 'Pending';
						if ((string) $decision === '1') {
							$decisionLabel = 'Approved';
						} elseif ((string) $decision === '0') {
							$decisionLabel = 'Denied';
						}
						$part = trim(($ln['t2_manufacturer'] ?? '').' '.($ln['t2_article'] ?? '').' — '.($ln['t2_name'] ?? ''));
						?>
						<tr>
							<td>#<?php echo (int) $ln['id']; ?><div class="text-muted" style="font-size:11px;">item <?php echo (int) $ln['item_id']; ?></div></td>
							<td>
								<?php if ((int) $ln['order_id'] > 0) { ?>
									<a href="/<?php echo $backend; ?>/shop/orders/order?order_id=<?php echo (int) $ln['order_id']; ?>" target="_blank">#<?php echo (int) $ln['order_id']; ?></a>
								<?php } else { ?>
									<span class="text-danger">—</span>
								<?php } ?>
							</td>
							<td><?php echo htmlspecialchars($part !== ' — ' ? $part : ('Item #'.(int) $ln['item_id']), ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo (int) ($ln['return_qty'] ?? $ln['order_qty'] ?? 0); ?></td>
							<td><?php echo number_format((float) $ln['price'] * max(1, (int) ($ln['return_qty'] ?? $ln['order_qty'] ?? 1)), 2, '.', ' '); ?></td>
							<td>
								<?php echo htmlspecialchars(epc_returns_label((string) $ln['reason_caption']), ENT_QUOTES, 'UTF-8'); ?>
								<?php if (trim((string) $ln['comment']) !== '') { ?>
									<div class="text-muted" style="font-size:12px;"><?php echo nl2br(htmlspecialchars($ln['comment'], ENT_QUOTES, 'UTF-8')); ?></div>
								<?php } ?>
							</td>
							<td><strong><?php echo htmlspecialchars($decisionLabel, ENT_QUOTES, 'UTF-8'); ?></strong></td>
							<td style="white-space:nowrap;">
								<button type="button" class="btn btn-xs btn-success epc-ret-decide" data-line="<?php echo (int) $ln['id']; ?>" data-decide="1" <?php echo (string) $decision === '1' ? 'disabled' : ''; ?>>Approve</button>
								<button type="button" class="btn btn-xs btn-danger epc-ret-decide" data-line="<?php echo (int) $ln['id']; ?>" data-decide="0" <?php echo (string) $decision === '0' ? 'disabled' : ''; ?>>Deny</button>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>

			<button type="button" class="btn btn-success" id="epc-ret-finalize">Close return (all lines decided)</button>
		</div>
	</div>
</div>
<script>
(function(){
	var csrf = <?php echo json_encode($csrf); ?>;
	var returnId = <?php echo (int) $return_id; ?>;
	var url = <?php echo json_encode('/'.$DP_Config->backend_dir.'/content/shop/returns/ajax/ajax_return_action.php'); ?>;
	function post(data, cb){
		data.csrf_guard_key = csrf;
		data.return_id = returnId;
		jQuery.ajax({
			type: 'POST',
			url: url,
			dataType: 'json',
			data: data,
			success: function(ans){
				if(!ans || !ans.status){
					alert((ans && ans.message) ? ans.message : 'Error');
					return;
				}
				if(cb){ cb(ans); } else { location.reload(); }
			},
			error: function(){ alert('Request failed'); }
		});
	}
	jQuery('.epc-ret-decide').on('click', function(){
		var btn = jQuery(this);
		post({ action: 'decide_line', line_id: btn.data('line'), decide: btn.data('decide') });
	});
	jQuery('#epc-ret-finalize').on('click', function(){
		if(!confirm('Close this return? Approved lines move to Return approved; denied lines to Return rejected.')){ return; }
		post({ action: 'finalize_return' });
	});
	jQuery('#epc-return-status-form').on('submit', function(e){
		e.preventDefault();
		var statusId = jQuery(this).find('[name=status_id]').val();
		post({ action: 'set_return_status', status_id: statusId });
	});
})();
</script>
