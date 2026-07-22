<?php
/**
 * CP tab — individual payment accounts (office / vendor receive funds).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_accounts.php';

epc_pay_accounts_ensure_schema($db_link);
epc_pay_accounts_seed_platform($db_link);

$accounts = epc_pay_accounts_list($db_link);
$offices = epc_pay_accounts_list_offices($db_link);
$vendors = epc_pay_accounts_list_vendors($db_link);
$settlements = epc_pay_accounts_list_settlements($db_link, 30);
$handlers = array_keys(epc_payment_gateway_defs());
$ownerTypes = epc_pay_accounts_owner_types();

$editId = isset($_GET['account_id']) ? (int)$_GET['account_id'] : 0;
$edit = $editId > 0 ? epc_pay_accounts_get($db_link, $editId) : null;
$editCreds = $edit ? epc_pay_accounts_decode_credentials($edit['credentials'] ?? '{}') : array();
?>

<div class="epc-pay-hint">
	<strong>Individual accounts:</strong> each shop office or marketplace vendor can hold its own merchant credentials
	(or a connected account ID / payout IBAN). Checkout routes payment to that account; settlements record who received the money.
</div>

<div class="epc-pay-actions">
	<button type="button" class="btn btn-success btn-sm" id="epc_btn_seed_platform_account"><i class="fa fa-database"></i> Ensure platform account</button>
</div>

<div class="row">
	<div class="col-lg-5">
		<div class="epc-pay-section">
			<h4><?php echo $edit ? 'Edit account #' . (int)$edit['id'] : 'Add payment account'; ?></h4>
			<div class="body" style="padding:14px;">
				<form id="epc_pay_account_form" class="form-horizontal" action="javascript:void(0);" onsubmit="return false;">
					<input type="hidden" name="id" value="<?php echo $edit ? (int)$edit['id'] : 0; ?>">
					<div class="form-group">
						<label class="col-sm-4 control-label">Owner type</label>
						<div class="col-sm-8">
							<select name="owner_type" id="epc_acc_owner_type" class="form-control">
								<?php foreach ($ownerTypes as $k => $lbl): ?>
								<option value="<?php echo epc_payment_h($k); ?>" <?php echo ($edit && $edit['owner_type'] === $k) ? 'selected' : ''; ?>><?php echo epc_payment_h($lbl); ?></option>
								<?php endforeach; ?>
							</select>
						</div>
					</div>
					<div class="form-group" id="epc_acc_office_wrap">
						<label class="col-sm-4 control-label">Office</label>
						<div class="col-sm-8">
							<select name="office_id" id="epc_acc_office" class="form-control">
								<option value="0">—</option>
								<?php foreach ($offices as $o): ?>
								<option value="<?php echo (int)$o['id']; ?>" <?php echo ($edit && $edit['owner_type'] === 'office' && (int)$edit['owner_id'] === (int)$o['id']) ? 'selected' : ''; ?>>
									#<?php echo (int)$o['id']; ?> — <?php echo epc_payment_h($o['caption']); ?>
								</option>
								<?php endforeach; ?>
							</select>
						</div>
					</div>
					<div class="form-group" id="epc_acc_vendor_wrap" style="display:none;">
						<label class="col-sm-4 control-label">Vendor</label>
						<div class="col-sm-8">
							<select name="vendor_id" id="epc_acc_vendor" class="form-control">
								<option value="0">—</option>
								<?php foreach ($vendors as $v): ?>
								<option value="<?php echo (int)$v['id']; ?>" <?php echo ($edit && $edit['owner_type'] === 'vendor' && (int)$edit['owner_id'] === (int)$v['id']) ? 'selected' : ''; ?>>
									#<?php echo (int)$v['id']; ?> — <?php echo epc_payment_h($v['vendor_full'] ?: $v['vendor_short']); ?>
								</option>
								<?php endforeach; ?>
							</select>
						</div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Title</label>
						<div class="col-sm-8"><input type="text" name="title" class="form-control" value="<?php echo epc_payment_h($edit['title'] ?? ''); ?>" placeholder="e.g. Dubai warehouse Telr"></div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Gateway</label>
						<div class="col-sm-8">
							<select name="handler" class="form-control">
								<?php foreach ($handlers as $h): ?>
								<option value="<?php echo epc_payment_h($h); ?>" <?php echo ($edit && $edit['handler'] === $h) ? 'selected' : ''; ?>><?php echo epc_payment_h(epc_payment_handler_title($h)); ?> (<?php echo epc_payment_h($h); ?>)</option>
								<?php endforeach; ?>
							</select>
						</div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Mode</label>
						<div class="col-sm-8">
							<select name="mode" class="form-control">
								<option value="direct" <?php echo (!$edit || ($edit['mode'] ?? '') === 'direct') ? 'selected' : ''; ?>>Direct merchant keys (account receives charge)</option>
								<option value="connected" <?php echo ($edit && ($edit['mode'] ?? '') === 'connected') ? 'selected' : ''; ?>>Connected account ID (Stripe Connect / sub-merchant)</option>
								<option value="payout" <?php echo ($edit && ($edit['mode'] ?? '') === 'payout') ? 'selected' : ''; ?>>Platform collects → payout to bank IBAN</option>
							</select>
						</div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Connected account ID</label>
						<div class="col-sm-8"><input type="text" name="connected_account_id" class="form-control" value="<?php echo epc_payment_h($edit['connected_account_id'] ?? ''); ?>" placeholder="acct_… or Telr merchant ref"></div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Payout IBAN</label>
						<div class="col-sm-8"><input type="text" name="payout_iban" class="form-control" value="<?php echo epc_payment_h($edit['payout_iban'] ?? ''); ?>"></div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Bank / account name</label>
						<div class="col-sm-8">
							<input type="text" name="payout_bank" class="form-control" style="margin-bottom:6px;" value="<?php echo epc_payment_h($edit['payout_bank'] ?? ''); ?>" placeholder="Bank name">
							<input type="text" name="payout_name" class="form-control" value="<?php echo epc_payment_h($edit['payout_name'] ?? ''); ?>" placeholder="Account name">
						</div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Platform fee %</label>
						<div class="col-sm-8"><input type="number" step="0.01" min="0" max="100" name="platform_fee_pct" class="form-control" value="<?php echo epc_payment_h($edit['platform_fee_pct'] ?? '0'); ?>"></div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Merchant credentials (JSON)</label>
						<div class="col-sm-8">
							<textarea name="credentials_json" class="form-control" rows="5" placeholder='{"demo_mode":1,"currency":"AED","store_id":"..."}'><?php echo epc_payment_h($edit ? json_encode($editCreds, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE) : "{\"demo_mode\":1,\"currency\":\"AED\"}"); ?></textarea>
							<p class="help-block" style="margin:6px 0 0;font-size:12px;">Paste gateway keys for this account. Office saves also sync to legacy <code>shop_offices.pay_system_*</code>.</p>
						</div>
					</div>
					<div class="form-group">
						<label class="col-sm-4 control-label">Flags</label>
						<div class="col-sm-8">
							<label class="checkbox-inline"><input type="checkbox" name="demo_mode" value="1" <?php echo (!$edit || !empty($edit['demo_mode'])) ? 'checked' : ''; ?>> Demo mode</label>
							<label class="checkbox-inline"><input type="checkbox" name="is_default" value="1" <?php echo ($edit && !empty($edit['is_default'])) ? 'checked' : ''; ?>> Default for owner</label>
							<select name="status" class="form-control" style="margin-top:8px;max-width:180px;">
								<option value="active" <?php echo (!$edit || ($edit['status'] ?? '') === 'active') ? 'selected' : ''; ?>>Active</option>
								<option value="pending" <?php echo ($edit && ($edit['status'] ?? '') === 'pending') ? 'selected' : ''; ?>>Pending</option>
								<option value="disabled" <?php echo ($edit && ($edit['status'] ?? '') === 'disabled') ? 'selected' : ''; ?>>Disabled</option>
							</select>
						</div>
					</div>
					<div class="form-group">
						<div class="col-sm-offset-4 col-sm-8">
							<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save account</button>
							<?php if ($edit): ?>
							<a class="btn btn-default" href="<?php echo epc_payment_h($paymentsUrl . '?tab=accounts'); ?>">Cancel</a>
							<?php endif; ?>
						</div>
					</div>
				</form>
			</div>
		</div>
	</div>

	<div class="col-lg-7">
		<div class="epc-pay-section">
			<h4>Payment accounts</h4>
			<div class="body table-responsive">
				<table class="table table-condensed" style="margin:0;">
					<thead><tr><th>Title</th><th>Owner</th><th>Gateway</th><th>Mode</th><th>Fee</th><th>Status</th><th></th></tr></thead>
					<tbody>
					<?php if (empty($accounts)): ?>
						<tr><td colspan="7" class="text-muted">No accounts yet — save one on the left.</td></tr>
					<?php else: foreach ($accounts as $a): ?>
						<tr>
							<td>
								<strong><?php echo epc_payment_h($a['title']); ?></strong>
								<?php if (!empty($a['is_default'])): ?> <span class="badge-active">Default</span><?php endif; ?>
							</td>
							<td><span class="badge-region"><?php echo epc_payment_h($a['owner_type']); ?></span> #<?php echo (int)$a['owner_id']; ?></td>
							<td><code><?php echo epc_payment_h($a['handler']); ?></code></td>
							<td><?php echo epc_payment_h($a['mode']); ?></td>
							<td><?php echo epc_payment_h($a['platform_fee_pct']); ?>%</td>
							<td><?php echo epc_payment_h($a['status']); ?></td>
							<td>
								<a class="btn btn-xs btn-default" href="<?php echo epc_payment_h($paymentsUrl . '?tab=accounts&account_id=' . (int)$a['id']); ?>">Edit</a>
								<button type="button" class="btn btn-xs btn-danger epc-acc-disable" data-id="<?php echo (int)$a['id']; ?>">Disable</button>
							</td>
						</tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		</div>

		<div class="epc-pay-section">
			<h4>Recent settlements (who received the payment)</h4>
			<div class="body table-responsive">
				<table class="table table-condensed" style="margin:0;">
					<thead><tr><th>ID</th><th>Order</th><th>Account</th><th>Gross</th><th>Fee</th><th>Net</th><th>Status</th><th></th></tr></thead>
					<tbody>
					<?php if (empty($settlements)): ?>
						<tr><td colspan="8" class="text-muted">Settlements appear after a successful online payment.</td></tr>
					<?php else: foreach ($settlements as $s): ?>
						<tr>
							<td><?php echo (int)$s['id']; ?></td>
							<td><?php echo (int)$s['order_id'] ?: '—'; ?></td>
							<td><?php echo epc_payment_h($s['owner_type']); ?> #<?php echo (int)$s['owner_id']; ?> <code><?php echo epc_payment_h($s['handler']); ?></code></td>
							<td><?php echo epc_payment_h($s['gross_amount']); ?> <?php echo epc_payment_h($s['currency']); ?></td>
							<td><?php echo epc_payment_h($s['fee_amount']); ?></td>
							<td><strong><?php echo epc_payment_h($s['net_amount']); ?></strong></td>
							<td><?php echo epc_payment_h($s['status']); ?></td>
							<td>
								<?php if (($s['status'] ?? '') !== 'paid_out'): ?>
								<button type="button" class="btn btn-xs btn-success epc-settle-paid" data-id="<?php echo (int)$s['id']; ?>">Mark paid out</button>
								<?php endif; ?>
							</td>
						</tr>
					<?php endforeach; endif; ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
</div>
