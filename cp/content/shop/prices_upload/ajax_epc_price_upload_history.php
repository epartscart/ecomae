<?php
header('Content-Type: application/json; charset=utf-8');

require_once __DIR__ . '/../epc_prices_ajax_init.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

if (!DP_User::isAdmin()) {
	exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

$action = (string)($_POST['action'] ?? $_GET['action'] ?? 'list');
$priceId = (int)($_POST['price_id'] ?? $_GET['price_id'] ?? 0);
$limit = (int)($_POST['limit'] ?? $_GET['limit'] ?? 100);
$csrfKey = (string)($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '');
$backend = '/' . $DP_Config->backend_dir;
$downloadBase = $backend . '/content/shop/prices_upload/ajax_epc_price_upload_history.php';

function epc_history_download_row(array $row, string $downloadBase, string $csrfKey): void
{
	$name = (string)$row['original_filename'];
	if ($name === '') {
		$name = 'price_upload_' . (int)$row['id'] . '.csv';
	}
	header('Content-Type: application/octet-stream');
	header('Content-Disposition: attachment; filename="' . str_replace('"', '', $name) . '"');
	header('Content-Length: ' . filesize(epc_price_history_file_absolute_path($row)));
	readfile(epc_price_history_file_absolute_path($row));
	exit;
}

if (in_array($action, ['download_issues', 'download_skipped', 'download_errors'], true)) {
	$historyId = (int)($_GET['history_id'] ?? $_POST['history_id'] ?? 0);
	$filter = 'all';
	if ($action === 'download_skipped') {
		$filter = 'skipped';
	} elseif ($action === 'download_errors') {
		$filter = 'error';
	}
	$row = epc_price_history_get_row($db_link, $historyId);
	if (!$row) {
		http_response_code(404);
		exit('Not found');
	}
	epc_price_history_stream_issues_csv($row, $filter);
}

if ($action === 'download' || $action === 'download_latest') {
	$row = null;
	if ($action === 'download_latest') {
		$dlPriceId = (int)($_GET['price_id'] ?? $_POST['price_id'] ?? 0);
		$row = epc_price_history_get_active($db_link, $dlPriceId);
	} else {
		$row = epc_price_history_get_row($db_link, (int)($_GET['history_id'] ?? $_POST['history_id'] ?? 0));
	}
	if (!$row) {
		http_response_code(404);
		exit('Not found');
	}
	$path = epc_price_history_file_absolute_path($row);
	if ($path === '' || !is_file($path)) {
		http_response_code(404);
		exit('File not available');
	}
	epc_history_download_row($row, $downloadBase, $csrfKey);
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

$activeRow = ($priceId > 0) ? epc_price_history_get_active($db_link, $priceId) : null;
$rows = epc_price_history_list($db_link, $priceId, $limit);

ob_start();
if ($activeRow) {
	$activeHasFile = trim((string)$activeRow['stored_relpath']) !== '' && is_file(epc_price_history_file_absolute_path($activeRow));
	$activeHasIssues = trim((string)($activeRow['issues_relpath'] ?? '')) !== '' && is_file(epc_price_history_issues_absolute_path($activeRow));
	$activeSkipped = (int)($activeRow['rows_skipped'] ?? 0);
	?>
	<div class="alert alert-success" style="margin-bottom:12px;">
		<strong><i class="fas fa-check-circle"></i> Active upload file</strong> (latest successful import for this price list)<br>
		<span><?php echo htmlspecialchars((string)$activeRow['original_filename'], ENT_QUOTES, 'UTF-8'); ?></span>
		&middot; <?php echo htmlspecialchars((string)$activeRow['created_at'], ENT_QUOTES, 'UTF-8'); ?>
		&middot; <?php echo number_format((int)$activeRow['rows_imported']); ?> rows
		&middot; <?php echo (int)$activeRow['brands_count']; ?> brands
		&middot; <?php echo number_format((int)$activeRow['items_count']); ?> items in DB
		<div style="margin-top:8px;">
			<?php if ($activeHasFile): ?>
				<a class="btn btn-sm btn-primary" href="<?php echo $downloadBase; ?>?action=download_latest&amp;price_id=<?php echo (int)$activeRow['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-download"></i> Download active file
				</a>
			<?php endif; ?>
			<a class="btn btn-sm btn-default" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$activeRow['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
				<i class="fas fa-database"></i> Export current DB
			</a>
			<?php if ($activeHasIssues || $activeSkipped > 0): ?>
				<a class="btn btn-sm btn-warning" href="<?php echo $downloadBase; ?>?action=download_skipped&amp;history_id=<?php echo (int)$activeRow['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-download"></i> Skipped lines (<?php echo $activeSkipped; ?>)
				</a>
				<a class="btn btn-sm btn-danger" href="<?php echo $downloadBase; ?>?action=download_errors&amp;history_id=<?php echo (int)$activeRow['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-download"></i> Errors
				</a>
				<a class="btn btn-sm btn-default" href="<?php echo $downloadBase; ?>?action=download_issues&amp;history_id=<?php echo (int)$activeRow['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-download"></i> All issues report
				</a>
			<?php endif; ?>
		</div>
	</div>
	<?php
} elseif ($priceId > 0) {
	?>
	<div class="alert alert-warning" style="margin-bottom:12px;">No archived upload file for this price list yet. Upload a file to store it here for download.</div>
	<?php
}
?>
<p class="text-muted" style="margin-bottom:8px;">Every price update is logged here with status and a downloadable copy of the source file when available. The row marked <span class="label label-success">ACTIVE</span> is the file currently used in the shop for this price list.</p>
<div class="table-responsive">
	<table class="table table-condensed table-striped table-bordered">
		<thead>
			<tr>
				<th>#</th>
				<th>Date</th>
				<?php if ($priceId <= 0): ?><th>Price list</th><?php endif; ?>
				<th>Source</th>
				<th>File</th>
				<th>Imported</th>
				<th>Skipped</th>
				<th>Brands</th>
				<th>Items in DB</th>
				<th>Status</th>
				<th>Download</th>
			</tr>
		</thead>
		<tbody>
		<?php if (count($rows) === 0): ?>
			<tr><td colspan="<?php echo $priceId > 0 ? 10 : 11; ?>" class="text-center text-muted">No upload history yet.</td></tr>
		<?php else: ?>
			<?php foreach ($rows as $row): ?>
				<?php
				$status = (string)$row['status'];
				$statusClass = $status === 'ok' ? 'success' : ($status === 'partial' ? 'warning' : ($status === 'pending' ? 'info' : 'danger'));
				$hasFile = trim((string)$row['stored_relpath']) !== '' && is_file(epc_price_history_file_absolute_path($row));
				$err = trim((string)$row['error_text']);
				$isActive = (int)($row['is_active'] ?? 0) === 1;
				$rowStyle = $isActive ? 'background:#e8f5e9;' : '';
				$rowSkipped = (int)$row['rows_skipped'];
				$hasIssuesFile = trim((string)($row['issues_relpath'] ?? '')) !== '' && is_file(epc_price_history_issues_absolute_path($row));
				$hasErrorText = trim((string)$row['error_text']) !== '';
				$canIssues = $hasIssuesFile || $hasErrorText || $rowSkipped > 0;
				?>
				<tr style="<?php echo $rowStyle; ?>">
					<td><?php echo (int)$row['id']; ?></td>
					<td><?php echo htmlspecialchars((string)$row['created_at'], ENT_QUOTES, 'UTF-8'); ?></td>
					<?php if ($priceId <= 0): ?>
						<td><?php echo htmlspecialchars((string)$row['price_name'] . ' (#' . $row['price_id'] . ')', ENT_QUOTES, 'UTF-8'); ?></td>
					<?php endif; ?>
					<td><?php echo htmlspecialchars(epc_price_history_source_label((string)$row['upload_source']), ENT_QUOTES, 'UTF-8'); ?></td>
					<td title="<?php echo htmlspecialchars($err, ENT_QUOTES, 'UTF-8'); ?>">
						<?php echo htmlspecialchars((string)$row['original_filename'], ENT_QUOTES, 'UTF-8'); ?>
						<?php if ($isActive): ?><br><span class="label label-success">ACTIVE</span><?php endif; ?>
						<?php if ($row['file_size'] > 0): ?>
							<br><small><?php echo number_format((int)$row['file_size']); ?> bytes</small>
						<?php endif; ?>
					</td>
					<td><?php echo (int)$row['rows_imported']; ?></td>
					<td><?php echo (int)$row['rows_skipped']; ?></td>
					<td><?php echo (int)$row['brands_count']; ?></td>
					<td><?php echo (int)$row['items_count']; ?></td>
					<td>
						<span class="label label-<?php echo $statusClass; ?>"><?php echo htmlspecialchars($status, ENT_QUOTES, 'UTF-8'); ?></span>
						<?php if ($err !== ''): ?>
							<br><small class="text-danger"><?php echo htmlspecialchars(mb_substr($err, 0, 120, 'UTF-8'), ENT_QUOTES, 'UTF-8'); ?></small>
						<?php endif; ?>
					</td>
					<td class="text-nowrap">
						<?php if ($hasFile): ?>
							<a class="btn btn-xs btn-primary" href="<?php echo $downloadBase; ?>?action=download&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Download original upload file"><i class="fas fa-download"></i></a>
						<?php elseif ($status === 'pending'): ?>
							<span class="text-muted" title="Import in progress"><i class="fas fa-spinner fa-pulse"></i></span>
						<?php else: ?>
							<span class="text-muted" title="Source file was not archived for this run"><i class="fas fa-exclamation-circle"></i></span>
						<?php endif; ?>
						<?php if ($canIssues): ?>
							<a class="btn btn-xs btn-warning" href="<?php echo $downloadBase; ?>?action=download_skipped&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Skipped lines with details"><i class="fas fa-exclamation-triangle"></i> <?php echo $rowSkipped; ?></a>
							<a class="btn btn-xs btn-danger" href="<?php echo $downloadBase; ?>?action=download_errors&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Error messages"><i class="fas fa-times-circle"></i></a>
							<a class="btn btn-xs btn-default" href="<?php echo $downloadBase; ?>?action=download_issues&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Full issues CSV"><i class="fas fa-list"></i></a>
						<?php endif; ?>
						<a class="btn btn-xs btn-default" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$row['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Current DB export">DB</a>
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
	'active' => $activeRow,
], JSON_UNESCAPED_UNICODE);
