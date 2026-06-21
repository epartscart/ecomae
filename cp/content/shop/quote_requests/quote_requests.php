<?php
/**
 * CP: quote requests — list, edit line prices, send quote to customer (status -> quoted).
 *
 * Do not use `return` after output: page PHP is eval()'d with the template in dp_core.php;
 * return exits the whole eval and can leave the CP main area blank.
 */
defined('_ASTEXE_') or die('No access');

require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/dp_user.php');
$user_session = DP_User::getAdminSession();

if (!empty($_POST['action'])) {
	require_once($_SERVER['DOCUMENT_ROOT'].'/content/users/stop_csrf.php');

	if ($_POST['action'] === 'save_quote' && !empty($_POST['quote_id'])) {
		$quote_id = (int) $_POST['quote_id'];
		$admin_note = isset($_POST['admin_note']) ? trim($_POST['admin_note']) : '';

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
	$q = $db_link->prepare('SELECT * FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1');
	$q->execute(array($edit_id));
	$quote = $q->fetch(PDO::FETCH_ASSOC);
	if (!$quote) {
		echo '<p>Not found</p>';
	} else {
		$iq = $db_link->prepare('SELECT * FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC');
		$iq->execute(array($edit_id));
		$lines = $iq->fetchAll(PDO::FETCH_ASSOC);
		?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">Quote #<?php echo (int) $quote['id']; ?> — user <?php echo (int) $quote['user_id']; ?></div>
				<div class="panel-body">
					<p><strong>Status:</strong> <?php echo htmlspecialchars($quote['status']); ?></p>
					<p><a href="/<?php echo $DP_Config->backend_dir; ?>/shop/quote-requests">Back to list</a></p>

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

	$sql = 'SELECT * FROM `shop_quote_requests` WHERE 1=1';
	$args = array();
	if ($status_filter !== '') {
		$sql .= ' AND `status` = ?';
		$args[] = $status_filter;
	}
	$sql .= ' ORDER BY `id` DESC LIMIT 200';

	$list = $db_link->prepare($sql);
	$list->execute($args);
	$rows = $list->fetchAll(PDO::FETCH_ASSOC);
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">Quote requests</div>
			<div class="panel-body">
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
							<th>User</th>
							<th>Status</th>
							<th>Updated</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
					<?php foreach ($rows as $r) { ?>
						<tr>
							<td><?php echo (int) $r['id']; ?></td>
							<td><?php echo (int) $r['user_id']; ?></td>
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