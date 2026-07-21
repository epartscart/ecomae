<?php
/**
 * CP: Review pending retail/wholesale registrations — approve currency + price profile.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

function epc_ca_h($value)
{
	return htmlspecialchars((string)$value, ENT_QUOTES, 'UTF-8');
}

$message = '';
$error = '';
$admin_id = (int)DP_User::getAdminId();

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
	$action = isset($_POST['action']) ? (string)$_POST['action'] : '';
	$user_id = (int)($_POST['user_id'] ?? 0);
	try {
		if ($user_id <= 0) {
			throw new Exception('Invalid customer');
		}
		if ($action === 'approve') {
			$currency = preg_replace('/[^0-9]/', '', (string)($_POST['currency_iso'] ?? ''));
			$profile = epc_trade_normalize_customer_type((string)($_POST['profile_code'] ?? ''));
			if ($profile === '') {
				$profile = epc_trade_normalize_customer_type(epc_trade_profile_get($db_link, $user_id, 'epc_customer_type', 'retail'));
			}
			if ($currency === '') {
				throw new Exception('Select dealing currency');
			}
			if (!epc_trade_approve_customer($db_link, $user_id, $currency, $profile, $admin_id)) {
				throw new Exception('Could not approve customer (check price profiles exist)');
			}
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
			$cur_rows = epc_trade_currency_options($db_link, $DP_Config);
			$cur_label = isset($cur_rows[$currency]['caption_short']) ? $cur_rows[$currency]['caption_short'] : $currency;
			$body = '<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;">';
			$body .= '<p>Your ' . epc_ca_h(epc_brand_trade_name()) . ' trade account has been <strong>approved</strong>.</p>';
			$body .= '<p><strong>Account type:</strong> ' . epc_ca_h(epc_trade_customer_type_label($profile)) . '<br>';
			$body .= '<strong>Dealing currency:</strong> ' . epc_ca_h($cur_label) . '</p>';
			$body .= '<p>You can now place orders on the website. Prices will be shown in your assigned currency.</p>';
			$body .= '<p>To request a currency change later, open <strong>My account → My data</strong>.</p></div>';
			send_notify('new_order_to_user', array('order_id' => 0, 'order_text' => $body), array(array('type' => 'user_id', 'user_id' => $user_id)), true);
			$message = 'Customer #' . $user_id . ' approved.';
		} elseif ($action === 'reject') {
			$note = trim((string)($_POST['reject_note'] ?? ''));
			epc_trade_reject_customer($db_link, $user_id, $note, $admin_id);
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/notifications/notify_helper.php';
			$body = '<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;"><p>Your trade account registration was reviewed and could not be approved at this time.</p>';
			if ($note !== '') {
				$body .= '<p>' . epc_ca_h($note) . '</p>';
			}
			$body .= '<p>Please contact us if you have questions.</p></div>';
			send_notify('new_order_to_user', array('order_id' => 0, 'order_text' => $body), array(array('type' => 'user_id', 'user_id' => $user_id)), true);
			$message = 'Customer #' . $user_id . ' rejected.';
		} elseif ($action === 'change_currency') {
			$currency = preg_replace('/[^0-9]/', '', (string)($_POST['currency_iso'] ?? ''));
			if ($currency === '' || !epc_trade_is_approved($db_link, $user_id)) {
				throw new Exception('Invalid request');
			}
			epc_trade_profile_set($db_link, $user_id, 'epc_dealing_currency', $currency);
			epc_trade_profile_delete($db_link, $user_id, 'epc_currency_change_requested');
			epc_trade_profile_delete($db_link, $user_id, 'epc_currency_change_requested_iso');
			epc_trade_profile_delete($db_link, $user_id, 'epc_currency_change_note');
			$message = 'Dealing currency updated for customer #' . $user_id . '.';
		} else {
			throw new Exception('Unknown action');
		}
	} catch (Throwable $e) {
		$error = $e->getMessage();
	}
}

$pending = epc_trade_pending_customers($db_link, 300);
$currencies = epc_trade_currency_options($db_link, $DP_Config);
$review_id = (int)($_GET['user_id'] ?? 0);
$review_user = null;
if ($review_id > 0) {
	$q = $db_link->prepare('SELECT * FROM `users` WHERE `user_id` = ? LIMIT 1');
	$q->execute(array($review_id));
	$review_user = $q->fetch(PDO::FETCH_ASSOC);
}
?>

<div class="row">
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">Customer trade approvals</div>
			<div class="panel-body">
				<p>New registrations choose <strong>Retail</strong> (auto-approved — prices visible immediately) or <strong>Wholesale</strong> (pending). Wholesale customers see availability qty, term, info, and price as <strong>***</strong> until you approve and assign a <strong>dealing currency</strong> + price profile. Checkout stays blocked while pending.</p>
				<?php if ($message !== '') { ?><div class="alert alert-success"><?=epc_ca_h($message);?></div><?php } ?>
				<?php if ($error !== '') { ?><div class="alert alert-danger"><?=epc_ca_h($error);?></div><?php } ?>
			</div>
		</div>
	</div>
</div>

<div class="row">
	<div class="col-md-7">
		<div class="hpanel">
			<div class="panel-heading hbuilt">Pending registrations (<?=count($pending);?>)</div>
			<div class="panel-body" style="padding:0;">
				<table class="table table-striped" style="margin:0;">
					<thead>
						<tr>
							<th>ID</th>
							<th>Customer</th>
							<th>Type</th>
							<th>Registered</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						<?php if (empty($pending)) { ?>
						<tr><td colspan="5" class="text-muted">No pending registrations.</td></tr>
						<?php } else { foreach ($pending as $row) {
							$name = trim((string)($row['name'] ?? '') . ' ' . (string)($row['surname'] ?? ''));
							if ($name === '' && !empty($row['company'])) {
								$name = (string)$row['company'];
							}
							?>
						<tr>
							<td><?=(int)$row['user_id'];?></td>
							<td>
								<strong><?=epc_ca_h($name !== '' ? $name : '—');?></strong><br>
								<small><?=epc_ca_h($row['email'] ?: $row['phone']);?></small>
							</td>
							<td><?=epc_ca_h(epc_trade_customer_type_label((string)($row['customer_type'] ?? 'retail')));?></td>
							<td><?=!empty($row['time_registered']) ? date('Y-m-d H:i', (int)$row['time_registered']) : '—';?></td>
							<td><a class="btn btn-xs btn-primary" href="?user_id=<?=(int)$row['user_id'];?>">Review</a></td>
						</tr>
						<?php } } ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>

	<div class="col-md-5">
		<div class="hpanel">
			<div class="panel-heading hbuilt">Review <?php if ($review_user) { ?>#<?=(int)$review_user['user_id'];?><?php } ?></div>
			<div class="panel-body">
				<?php if (!$review_user) { ?>
				<p class="text-muted">Select a pending customer from the list.</p>
				<?php } else {
					$uid = (int)$review_user['user_id'];
					$ctype = epc_trade_profile_get($db_link, $uid, 'epc_customer_type', 'retail');
					$status = epc_trade_approval_status($db_link, $uid);
					require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
					?>
				<p><a class="btn btn-xs btn-default" target="_blank" href="/<?=epc_ca_h($DP_Config->backend_dir);?>/users/usermanager/user?user_id=<?=$uid;?>">Open full customer card</a></p>
				<?=epc_build_customer_profile_html($uid);?>
				<p><strong>Requested type:</strong> <?=epc_ca_h(epc_trade_customer_type_label($ctype));?><br>
				<strong>Status:</strong> <?=epc_ca_h($status);?></p>

				<?php if ($status === 'pending') { ?>
				<form method="post" style="margin-top:16px;">
					<input type="hidden" name="csrf_guard_key" value="<?=$user_session['csrf_guard_key'];?>" />
					<input type="hidden" name="user_id" value="<?=$uid;?>" />
					<input type="hidden" name="action" value="approve" />
					<label>Price profile</label>
					<select class="form-control" name="profile_code">
						<option value="retail" <?=$ctype === 'retail' ? 'selected' : '';?>>Retail</option>
						<option value="wholesale" <?=$ctype === 'wholesale' ? 'selected' : '';?>>Wholesale</option>
					</select>
					<br>
					<label>Dealing currency (fixed for this customer)</label>
					<select class="form-control" name="currency_iso" required>
						<option value="">— Select —</option>
						<?php foreach ($currencies as $iso => $crow) { ?>
						<option value="<?=epc_ca_h($iso);?>"><?=epc_ca_h($crow['caption_short'] . ' (' . $crow['iso_name'] . ')');?></option>
						<?php } ?>
					</select>
					<br>
					<button class="btn btn-success" type="submit">Approve & enable checkout</button>
				</form>
				<form method="post" style="margin-top:20px;border-top:1px solid #eee;padding-top:12px;">
					<input type="hidden" name="csrf_guard_key" value="<?=$user_session['csrf_guard_key'];?>" />
					<input type="hidden" name="user_id" value="<?=$uid;?>" />
					<input type="hidden" name="action" value="reject" />
					<label>Reject (optional note to customer)</label>
					<textarea class="form-control" name="reject_note" rows="2"></textarea>
					<br>
					<button class="btn btn-danger" type="submit" onclick="return confirm('Reject this registration?');">Reject</button>
				</form>
				<?php } elseif ($status === 'approved') {
					$fixed = epc_trade_user_currency_iso($db_link, $uid);
					$fixed_label = isset($currencies[$fixed]['caption_short']) ? $currencies[$fixed]['caption_short'] : $fixed;
					$change_req = epc_trade_profile_get($db_link, $uid, 'epc_currency_change_requested', '') === '1';
					$req_iso = epc_trade_profile_get($db_link, $uid, 'epc_currency_change_requested_iso', '');
					$req_label = ($req_iso !== '' && isset($currencies[$req_iso]['caption_short'])) ? $currencies[$req_iso]['caption_short'] : '';
					$change_note = epc_trade_profile_get($db_link, $uid, 'epc_currency_change_note', '');
					?>
				<p><strong>Dealing currency:</strong> <?=epc_ca_h($fixed_label);?>
					<?php if ($change_req) { ?><span class="label label-warning">Change requested</span><?php } ?>
				</p>
				<?php if ($change_req && $req_label !== '') { ?>
				<p><strong>Requested currency:</strong> <?=epc_ca_h($req_label);?></p>
				<?php } ?>
				<?php if ($change_req && $change_note !== '') { ?>
				<p><strong>Customer note:</strong> <?=epc_ca_h($change_note);?></p>
				<?php } ?>
				<form method="post">
					<input type="hidden" name="csrf_guard_key" value="<?=$user_session['csrf_guard_key'];?>" />
					<input type="hidden" name="user_id" value="<?=$uid;?>" />
					<input type="hidden" name="action" value="change_currency" />
					<label>Update dealing currency</label>
					<select class="form-control" name="currency_iso">
						<?php foreach ($currencies as $iso => $crow) { ?>
						<option value="<?=epc_ca_h($iso);?>" <?=$iso === $fixed ? 'selected' : '';?>><?=epc_ca_h($crow['caption_short']);?></option>
						<?php } ?>
					</select>
					<br>
					<button class="btn btn-primary" type="submit">Save currency</button>
				</form>
				<?php } else { ?>
				<p class="text-muted">This registration was rejected.</p>
				<?php } ?>
				<?php } ?>
			</div>
		</div>
	</div>
</div>
