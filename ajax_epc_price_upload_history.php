<?php
header('Content-Type: application/json; charset=utf-8');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		[PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
	);
} catch (PDOException $e) {
	exit(json_encode(['status' => false, 'message' => 'No DB connect']));
}
$db_link->query('SET NAMES utf8;');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

if (!DP_User::isAdmin()) {
	exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

$action = (string)($_POST['action'] ?? $_GET['action'] ?? 'list');
$priceId = (int)($_POST['price_id'] ?? $_GET['price_id'] ?? 0);
$limit = (int)($_POST['limit'] ?? $_GET['limit'] ?? 50);

if ($action === 'download') {
	$historyId = (int)($_GET['history_id'] ?? $_POST['history_id'] ?? 0);
	$row = epc_price_history_get_row($db_link, $historyId);
	if (!$row) {
		http_response_code(404);
		exit('Not found');
	}
	$path = epc_price_history_file_absolute_path($row);
	if ($path === '' || !is_file($path)) {
		http_response_code(404);
		exit('File not available');
	}
	$name = (string)$row['original_filename'];
	if ($name === '') {
		$name = 'price_upload_' . $historyId . '.csv';
	}
	header('Content-Type: application/octet-stream');
	header('Content-Disposition: attachment; filename="' . str_replace('"', '', $name) . '"');
	header('Content-Length: ' . filesize($path));
	readfile($path);
	exit;
}

if ($action === 'export_db') {
	$exportPriceId = (int)($_GET['price_id'] ?? $_POST['price_id'] ?? 0);
	if ($exportPriceId <= 0) {
		exit(json_encode(['status' => false, 'message' => 'price_id required']));
	}
	$pn = $db_link->prepare('SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
	$pn->execute([$exportPriceId]);
	$priceName = (string)$pn->fetchColumn();
	$safe = preg_replace('/[^a-zA-Z0-9._-]+/', '_', $priceName);
	$filename = ($safe !== '' ? $safe : 'price') . '_export_' . date('Y-m-d_His') . '.csv';

	header('Content-Type: text/csv; charset=utf-8');
	header('Content-Disposition: attachment; filename="' . $filename . '"');

	$out = fopen('php://output', 'w');
	fputcsv($out, ['manufacturer', 'article', 'article_show', 'name', 'exist', 'price', 'time_to_exe', 'storage', 'min_order']);

	$q = $db_link->prepare(
		'SELECT `manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`
		 FROM `shop_docpart_prices_data` WHERE `price_id` = ? ORDER BY `manufacturer`,`article`;'
	);
	$q->execute([$exportPriceId]);
	while ($r = $q->fetch(PDO::FETCH_ASSOC)) {
		fputcsv($out, [
			$r['manufacturer'],
			$r['article'],
			$r['article_show'],
			$r['name'],
			$r['exist'],
			$r['price'],
			$r['time_to_exe'],
			$r['storage'],
			$r['min_order'],
		]);
	}
	fclose($out);
	exit;
}

$rows = epc_price_history_list($db_link, $priceId, $limit);
$backend = '/' . $DP_Config->backend_dir;
$downloadBase = $backend . '/content/shop/prices_upload/ajax_epc_price_upload_history.php';

ob_start();
?>
<div class="table-responsive">
	<table class="table table-condensed table-striped table-bordered">
		<thead>
			<tr>
				<th>#</th>
				<th>Date</th>
				<th>Price list</th>
				<th>Source</th>
				<th>File</th>
				<th>Imported</th>
				<th>Skipped</th>
				<th>Brands</th>
				<th>Items in DB</th>
				<th>Status</th>
				<th></th>
			</tr>
		</thead>
		<tbody>
		<?php if (count($rows) === 0): ?>
			<tr><td colspan="11" class="text-center text-muted">No upload history yet.</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $i => $row): ?>
				<?php
				$status = (string)$row['status'];
				$statusClass = $status === 'ok' ? 'success' : ($status === 'partial' ? 'warning' : 'danger');
				$hasFile = trim((string)$row['stored_relpath']) !== '';
				$err = trim((string)$row['error_text']);
				?>
				<tr>
					<td><?php echo (int)$row['id']; ?></td>
					<td><?php echo htmlspecialchars((string)$row['created_at'], ENT_QUOTES, 'UTF-8'); ?></td>
					<td><?php echo htmlspecialchars((string)$row['price_name'] . ' (#' . $row['price_id'] . ')', ENT_QUOTES, 'UTF-8'); ?></td>
					<td><?php echo htmlspecialchars((string)$row['upload_source'], ENT_QUOTES, 'UTF-8'); ?></td>
					<td title="<?php echo htmlspecialchars($err, ENT_QUOTES, 'UTF-8'); ?>">
						<?php echo htmlspecialchars((string)$row['original_filename'], ENT_QUOTES, 'UTF-8'); ?>
						<?php if ($row['file_size'] > 0): ?>
							<br><small><?php echo number_format((int)$row['file_size']); ?> bytes</small>
						<?php endif; ?>
					</td>
					<td><?php echo (int)$row['rows_imported']; ?></td>
					<td><?php echo (int)$row['rows_skipped']; ?></td>
					<td><?php echo (int)$row['brands_count']; ?></td>
					<td><?php echo (int)$row['items_count']; ?></td>
					<td><span class="label label-<?php echo $statusClass; ?>"><?php echo htmlspecialchars($status, ENT_QUOTES, 'UTF-8'); ?></span>
						<?php if ($err !== ''): ?>
							<br><small class="text-danger"><?php echo htmlspecialchars(mb_substr($err, 0, 120, 'UTF-8'), ENT_QUOTES, 'UTF-8'); ?></small>
						<?php endif; ?>
					</td>
					<td class="text-nowrap">
						<?php if ($hasFile): ?>
							<a class="btn btn-xs btn-primary" href="<?php echo $downloadBase; ?>?action=download&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode((string)($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '')); ?>">Download</a>
						<?php endif; ?>
						<a class="btn btn-xs btn-default" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$row['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode((string)($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '')); ?>">Export DB</a>
					</td>
				</tr>
			<?php endforeach; ?>
		<?php endif; ?>
		</tbody>
	</table>
</div>
<?php
$html = ob_get_clean();

echo json_encode([
	'status' => true,
	'html' => $html,
	'count' => count($rows),
], JSON_UNESCAPED_UNICODE);
