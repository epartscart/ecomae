<?php
/**
 * CP: quote requests — list, edit line prices, send quote to customer (status -> quoted).
 * Quotes are for registered (logged-in) customers only; guest user_id=0 rows are hidden.
 *
 * Do not use `return` after output: page PHP is eval()'d with the template in dp_core.php;
 * return exits the whole eval and can leave the CP main area blank.
 */
defined('_ASTEXE_') or die('No access');

require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/dp_user.php');
$user_session = DP_User::getAdminSession();

/**
 * Build display fields for a registered quote customer.
 *
 * @param int $user_id
 * @return array{user_id:int,name:string,email:string,phone:string,company:string,label:string,profile_url:string,ok:bool}
 */
function epc_quote_customer_details($user_id)
{
	global $DP_Config;

	$user_id = (int) $user_id;
	$out = array(
		'user_id' => $user_id,
		'name' => '',
		'email' => '',
		'phone' => '',
		'company' => '',
		'label' => '',
		'profile_url' => '',
		'ok' => false,
	);

	if ($user_id <= 0) {
		$out['label'] = 'Guest (not allowed)';
		return $out;
	}

	$profile = DP_User::getUserProfileById($user_id);
	if (!is_array($profile)) {
		$profile = array();
	}

	$parts = array();
	foreach (array('surname', 'name', 'patronymic') as $k) {
		if (!empty($profile[$k])) {
			$parts[] = trim((string) $profile[$k]);
		}
	}
	$out['name'] = trim(implode(' ', $parts));
	$out['email'] = !empty($profile['email']) ? trim((string) $profile['email']) : '';
	$out['phone'] = !empty($profile['phone']) ? trim((string) $profile['phone']) : '';
	if ($out['phone'] === '' && !empty($profile['cellphone'])) {
		$out['phone'] = trim((string) $profile['cellphone']);
	}
	$out['company'] = !empty($profile['company_name']) ? trim((string) $profile['company_name']) : '';
	$out['profile_url'] = '/'.$DP_Config->backend_dir.'/users/usermanager/user?user_id='.$user_id;
	$out['ok'] = true;

	$label_bits = array();
	if ($out['name'] !== '') {
		$label_bits[] = $out['name'];
	}
	if ($out['company'] !== '') {
		$label_bits[] = $out['company'];
	}
	if ($out['email'] !== '') {
		$label_bits[] = $out['email'];
	} elseif ($out['phone'] !== '') {
		$label_bits[] = $out['phone'];
	}
	if (count($label_bits) < 1) {
		$label_bits[] = 'Customer #'.$user_id;
	}
	$out['label'] = implode(' · ', $label_bits);

	return $out;
}

if (!empty($_POST['action'])) {
	require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/stop_csrf.php');

	if ($_POST['action'] === 'save_quote' && !empty($_POST['quote_id'])) {
		$quote_id = (int) $_POST['quote_id'];
		$admin_note = isset($_POST['admin_note']) ? trim($_POST['admin_note']) : '';

		$owner_q = $db_link->prepare('SELECT `user_id` FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1');
		$owner_q->execute(array($quote_id));
		$owner_id = (int) $owner_q->fetchColumn();
		if ($owner_id <= 0) {
			$error_message = 'Quotes are only for registered customers';
			?>
			<script>
			location = "/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}

		$lines = isset($_POST['lines']) && is_array($_POST['lines']) ? $_POST['lines'] : array();
		$upd_line = $db_link->prepare('UPDATE `shop_quote_items` SET `quoted_price` = ?, `quoted_time_to_exe` = ?, `line_admin_note` = ? WHERE `id` = ? AND `quote_id` = ?');
		foreach ($lines as $line_id_key => $row) {
			$line_id = (int) $line_id_key;
			$qp = isset($row['quoted_price']) ? str_replace(',', '.', trim($row['quoted_price'])) : '';
			$qt = isset($row['quoted_time_to_exe']) ? trim($row['quoted_time_to_exe']) : '';
			$ln = isset($row['line_admin_note']) ? trim($row['line_admin_note']) : '';

			$quoted_price = null;
			if ($qp !== '' && is_numeric($qp)) {
				$quoted_price = (float) $qp;
			}
			$quoted_time = null;
			if ($qt !== '' && is_numeric($qt)) {
				$quoted_time = (int) $qt;
			}

			$upd_line->execute(array($quoted_price, $quoted_time, $ln, $line_id, $quote_id));
		}

		$db_link->prepare('UPDATE `shop_quote_requests` SET `admin_note` = ?, `time_updated` = ? WHERE `id` = ?')->execute(array($admin_note, time(), $quote_id));

		$success_message = 'Saved';
		?>
		<script>
		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?quote_id=<?php echo $quote_id; ?>&success_message=<?php echo urlencode($success_message); ?>";
		</script>
		<?php
		exit;
	}

	if ($_POST['action'] === 'send_quote' && !empty($_POST['quote_id'])) {
		$quote_id = (int) $_POST['quote_id'];

		$owner_q = $db_link->prepare('SELECT `user_id` FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1');
		$owner_q->execute(array($quote_id));
		$owner_id = (int) $owner_q->fetchColumn();
		if ($owner_id <= 0) {
			$error_message = 'Quotes are only for registered customers';
			?>
			<script>
			location = "/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}

		$line_count_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?');
		$line_count_query->execute(array($quote_id));
		if ((int) $line_count_query->fetchColumn() < 1) {
			$error_message = 'Add at least one quote line before publishing';
			?>
			<script>
			location = "/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?quote_id=<?php echo $quote_id; ?>&error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}

		$chk = $db_link->prepare('SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ? AND (`quoted_price` IS NULL OR `quoted_price` <= 0)');
		$chk->execute(array($quote_id));
		if ((int) $chk->fetchColumn() > 0) {
			$error_message = 'Set a positive quoted price on every line first';
			?>
			<script>
			location = "/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?quote_id=<?php echo $quote_id; ?>&error_message=<?php echo urlencode($error_message); ?>";
			</script>
			<?php
			exit;
		}

		$db_link->prepare('UPDATE `shop_quote_requests` SET `status` = \'quoted\', `time_updated` = ? WHERE `id` = ? AND `status` IN (\'submitted\',\'quoted\')')->execute(array(time(), $quote_id));

		$success_message = 'Quote sent to customer';
		?>
		<script>
		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?quote_id=<?php echo $quote_id; ?>&success_message=<?php echo urlencode($success_message); ?>";
		</script>
		<?php
		exit;
	}
}

require_once('content/control/actions_alert.php');

$currency_query = $db_link->prepare('SELECT * FROM `shop_currencies` WHERE `iso_code` = ?;');
$currency_query->execute(array($DP_Config->shop_currency));
$currency_record = $currency_query->fetch();
$currency_sign = $currency_record ? $currency_record['sign'] : '';

$edit_id = isset($_GET['quote_id']) ? (int) $_GET['quote_id'] : 0;

if ($edit_id > 0) {
	$q = $db_link->prepare('SELECT * FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` > 0 LIMIT 1');
	$q->execute(array($edit_id));
	$quote = $q->fetch(PDO::FETCH_ASSOC);
	if (!$quote) {
		echo '<div class="col-lg-12"><div class="alert alert-warning">Quote not found, or it is not linked to a registered customer.</div>';
		echo '<p><a href="/'.htmlspecialchars($DP_Config->backend_dir).'/shop/quote-requests">Back to list</a></p></div>';
	} else {
		$customer = epc_quote_customer_details((int) $quote['user_id']);
		$iq = $db_link->prepare('SELECT * FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC');
		$iq->execute(array($edit_id));
		$lines = $iq->fetchAll(PDO::FETCH_ASSOC);
		?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					Quote #<?php echo (int) $quote['id']; ?>
					— <?php echo htmlspecialchars($customer['label'] !== '' ? $customer['label'] : ('Customer #'.$customer['user_id'])); ?>
				</div>
				<div class="panel-body">
					<p><a href="/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests">Back to list</a></p>

					<div class="well" style="margin-bottom:20px;">
						<h4 style="margin-top:0;">Customer details</h4>
						<p class="text-muted" style="margin-bottom:12px;">Quotes are available only for logged-in / registered customers.</p>
						<table class="table table-condensed" style="margin-bottom:0; max-width:720px;">
							<tbody>
								<tr>
									<th style="width:160px;">Customer ID</th>
									<td>
										<?php echo (int) $customer['user_id']; ?>
										<?php if ($customer['profile_url'] !== '') { ?>
											&nbsp;<a href="<?php echo htmlspecialchars($customer['profile_url']); ?>" target="_blank">Open profile</a>
										<?php } ?>
									</td>
								</tr>
								<tr>
									<th>Name</th>
									<td><?php echo $customer['name'] !== '' ? htmlspecialchars($customer['name']) : '—'; ?></td>
								</tr>
								<?php if ($customer['company'] !== '') { ?>
								<tr>
									<th>Company</th>
									<td><?php echo htmlspecialchars($customer['company']); ?></td>
								</tr>
								<?php } ?>
								<tr>
									<th>Email</th>
									<td>
										<?php if ($customer['email'] !== '') { ?>
											<a href="mailto:<?php echo htmlspecialchars($customer['email']); ?>"><?php echo htmlspecialchars($customer['email']); ?></a>
										<?php } else { echo '—'; } ?>
									</td>
								</tr>
								<tr>
									<th>Phone</th>
									<td>
										<?php if ($customer['phone'] !== '') { ?>
											<a href="tel:<?php echo htmlspecialchars(preg_replace('/\s+/', '', $customer['phone'])); ?>"><?php echo htmlspecialchars($customer['phone']); ?></a>
										<?php } else { echo '—'; } ?>
									</td>
								</tr>
								<tr>
									<th>Status</th>
									<td><?php echo htmlspecialchars($quote['status']); ?></td>
								</tr>
								<tr>
									<th>Created</th>
									<td><?php echo !empty($quote['time_created']) ? date('Y-m-d H:i', (int) $quote['time_created']) : '—'; ?></td>
								</tr>
								<tr>
									<th>Updated</th>
									<td><?php echo !empty($quote['time_updated']) ? date('Y-m-d H:i', (int) $quote['time_updated']) : '—'; ?></td>
								</tr>
								<?php if (!empty($quote['time_submitted'])) { ?>
								<tr>
									<th>Submitted</th>
									<td><?php echo date('Y-m-d H:i', (int) $quote['time_submitted']); ?></td>
								</tr>
								<?php } ?>
								<?php if (!empty($quote['accepted_order_id'])) { ?>
								<tr>
									<th>Accepted order</th>
									<td>
										<a href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders?order_id=<?php echo (int) $quote['accepted_order_id']; ?>">
											#<?php echo (int) $quote['accepted_order_id']; ?>
										</a>
									</td>
								</tr>
								<?php } ?>
							</tbody>
						</table>
					</div>

					<form method="post">
						<input type="hidden" name="action" value="save_quote" />
						<input type="hidden" name="quote_id" value="<?php echo (int) $edit_id; ?>" />
						<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key']); ?>" />

						<div class="form-group">
							<label>Customer note</label>
							<div class="well well-sm"><?php echo nl2br(htmlspecialchars((string) $quote['customer_note'])); ?></div>
						</div>
						<div class="form-group">
							<label>Staff note (visible to customer on detail page)</label>
							<textarea class="form-control" name="admin_note" rows="3"><?php echo htmlspecialchars($quote['admin_note']); ?></textarea>
						</div>

						<table class="table table-bordered table-condensed">
							<thead>
								<tr>
									<th>ID</th>
									<th>Part</th>
									<th>Qty</th>
									<th>Quoted price (<?php echo htmlspecialchars($currency_sign); ?>)</th>
									<th>Lead time (days)</th>
									<th>Line note</th>
								</tr>
							</thead>
							<tbody>
							<?php foreach ($lines as $ln) {
								$po = json_decode($ln['product_object_json'], true);
								$label = '';
								if (is_array($po)) {
									$label = htmlspecialchars($po['manufacturer'].' '.$po['article_show'].' — '.$po['name']);
								}
								?>
								<tr>
									<td><?php echo (int) $ln['id']; ?></td>
									<td><?php echo $label; ?></td>
									<td><?php echo (int) $ln['count_need']; ?></td>
									<td>
										<input class="form-control" type="text" name="lines[<?php echo (int) $ln['id']; ?>][quoted_price]" value="<?php echo $ln['quoted_price'] !== null ? htmlspecialchars($ln['quoted_price']) : ''; ?>" />
									</td>
									<td>
										<input class="form-control" type="text" name="lines[<?php echo (int) $ln['id']; ?>][quoted_time_to_exe]" value="<?php echo $ln['quoted_time_to_exe'] !== null ? (int) $ln['quoted_time_to_exe'] : ''; ?>" />
									</td>
									<td>
										<input class="form-control" type="text" name="lines[<?php echo (int) $ln['id']; ?>][line_admin_note]" value="<?php echo htmlspecialchars($ln['line_admin_note']); ?>" />
									</td>
								</tr>
							<?php } ?>
							</tbody>
						</table>

						<button type="submit" class="btn btn-primary">Save</button>
					</form>

					<?php if (in_array($quote['status'], array('submitted', 'quoted'), true)) { ?>
					<form method="post" style="margin-top:15px;" onsubmit="return confirm('Publish this quote? Customer will be able to accept and add lines to cart.');">
						<input type="hidden" name="action" value="send_quote" />
						<input type="hidden" name="quote_id" value="<?php echo (int) $edit_id; ?>" />
						<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key']); ?>" />
						<button type="submit" class="btn btn-success">Publish quote to customer</button>
					</form>
					<?php } ?>
				</div>
			</div>
		</div>
		<?php
	}
} else {
	$status_filter = isset($_GET['status']) ? $_GET['status'] : '';

	// Registered customers only (storefront quotes require login).
	$sql = 'SELECT * FROM `shop_quote_requests` WHERE `user_id` > 0';
	$args = array();
	if ($status_filter !== '') {
		$sql .= ' AND `status` = ?';
		$args[] = $status_filter;
	}
	$sql .= ' ORDER BY `id` DESC LIMIT 200';

	$list = $db_link->prepare($sql);
	$list->execute($args);
	$rows = $list->fetchAll(PDO::FETCH_ASSOC);

	$customer_cache = array();
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">Quote requests</div>
			<div class="panel-body">
				<p class="text-muted">Quotes are for registered (login / register) customers only. Customer name, email, and phone are shown below.</p>
				<form method="get" class="form-inline">
					<label>Status</label>
					<select name="status" class="form-control" onchange="this.form.submit()">
						<option value="">All</option>
						<option value="draft" <?php echo $status_filter === 'draft' ? 'selected' : ''; ?>>draft</option>
						<option value="submitted" <?php echo $status_filter === 'submitted' ? 'selected' : ''; ?>>submitted</option>
						<option value="quoted" <?php echo $status_filter === 'quoted' ? 'selected' : ''; ?>>quoted</option>
						<option value="accepted" <?php echo $status_filter === 'accepted' ? 'selected' : ''; ?>>accepted</option>
					</select>
				</form>

				<table class="table table-striped" style="margin-top:15px;">
					<thead>
						<tr>
							<th>ID</th>
							<th>Customer</th>
							<th>Email</th>
							<th>Phone</th>
							<th>Status</th>
							<th>Updated</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
					<?php
					if (count($rows) < 1) {
						?>
						<tr><td colspan="7" class="text-muted">No quote requests for registered customers.</td></tr>
						<?php
					}
					foreach ($rows as $r) {
						$uid = (int) $r['user_id'];
						if (!isset($customer_cache[$uid])) {
							$customer_cache[$uid] = epc_quote_customer_details($uid);
						}
						$c = $customer_cache[$uid];
						$name_cell = $c['name'] !== '' ? $c['name'] : ('Customer #'.$uid);
						if ($c['company'] !== '') {
							$name_cell .= ' ('.$c['company'].')';
						}
						?>
						<tr>
							<td><?php echo (int) $r['id']; ?></td>
							<td>
								<a href="/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?quote_id=<?php echo (int) $r['id']; ?>">
									<?php echo htmlspecialchars($name_cell); ?>
								</a>
								<div class="text-muted" style="font-size:12px;">ID <?php echo $uid; ?>
									<?php if ($c['profile_url'] !== '') { ?>
										· <a href="<?php echo htmlspecialchars($c['profile_url']); ?>" target="_blank">profile</a>
									<?php } ?>
								</div>
							</td>
							<td><?php echo $c['email'] !== '' ? htmlspecialchars($c['email']) : '—'; ?></td>
							<td><?php echo $c['phone'] !== '' ? htmlspecialchars($c['phone']) : '—'; ?></td>
							<td><?php echo htmlspecialchars($r['status']); ?></td>
							<td><?php echo $r['time_updated'] ? date('Y-m-d H:i', (int) $r['time_updated']) : ''; ?></td>
							<td><a href="/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests?quote_id=<?php echo (int) $r['id']; ?>">Open</a></td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
	<?php
}
?>
