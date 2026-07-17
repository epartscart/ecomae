<?php
/**
 * Price upload history list + file downloads (CP admin).
 * Binary download actions must not emit Content-Type: application/json first.
 */

require_once __DIR__ . '/epc_prices_ajax_init.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_price_upload_history.php';

if (!DP_User::isAdmin()) {
	header('Content-Type: application/json; charset=utf-8');
	exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

$action = (string)($_POST['action'] ?? $_GET['action'] ?? 'list');
$priceId = (int)($_POST['price_id'] ?? $_GET['price_id'] ?? 0);
$limit = (int)($_POST['limit'] ?? $_GET['limit'] ?? 100);
$csrfKey = (string)($_POST['csrf_guard_key'] ?? $_GET['csrf_guard_key'] ?? '');
$backend = '/' . $DP_Config->backend_dir;
$downloadBase = $backend . '/content/shop/prices_upload/ajax_epc_price_upload_history.php';

function epc_history_download_row(array $row): void
{
	$name = (string)$row['original_filename'];
	if ($name === '') {
		$name = 'price_upload_' . (int)$row['id'] . '.csv';
	}
	$path = epc_price_history_file_absolute_path($row);
	header('Content-Type: application/octet-stream');
	header('Content-Disposition: attachment; filename="' . str_replace('"', '', $name) . '"');
	header('Content-Length: ' . filesize($path));
	header('X-Content-Type-Options: nosniff');
	readfile($path);
	exit;
}

function epc_history_stream_db_export(PDO $db_link, int $exportPriceId): void
{
	if ($exportPriceId <= 0) {
		header('Content-Type: application/json; charset=utf-8');
		exit(json_encode(['status' => false, 'message' => 'price_id required']));
	}
	$pn = $db_link->prepare('SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
	$pn->execute([$exportPriceId]);
	$priceName = (string)$pn->fetchColumn();
	$safe = preg_replace('/[^a-zA-Z0-9._-]+/', '_', $priceName);
	$filename = ($safe !== '' ? $safe : 'price') . '_export_' . date('Y-m-d_His') . '.csv';

	header('Content-Type: text/csv; charset=utf-8');
	header('Content-Disposition: attachment; filename="' . $filename . '"');
	header('X-Content-Type-Options: nosniff');

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
		header('Content-Type: text/plain; charset=utf-8');
		exit('Not found');
	}
	epc_price_history_stream_issues_csv($row, $filter);
}

if ($action === 'download' || $action === 'download_latest') {
	$row = null;
	$dlPriceId = 0;
	if ($action === 'download_latest') {
		$dlPriceId = (int)($_GET['price_id'] ?? $_POST['price_id'] ?? 0);
		$row = epc_price_history_get_active($db_link, $dlPriceId);
	} else {
		$row = epc_price_history_get_row($db_link, (int)($_GET['history_id'] ?? $_POST['history_id'] ?? 0));
		if ($row) {
			$dlPriceId = (int)$row['price_id'];
		}
	}
	if ($row) {
		$path = epc_price_history_file_absolute_path($row);
		if ($path !== '' && is_file($path)) {
			epc_history_download_row($row);
		}
	}
	// "Last / active file" button: if archive is gone, export live DB prices instead of a dead 404
	if ($action === 'download_latest' && $dlPriceId > 0) {
		epc_history_stream_db_export($db_link, $dlPriceId);
	}
	http_response_code(404);
	header('Content-Type: text/html; charset=utf-8');
	$exportUrl = $downloadBase . '?action=export_db&price_id=' . (int)$dlPriceId . '&csrf_guard_key=' . rawurlencode($csrfKey);
	echo '<!DOCTYPE html><html><body style="font-family:sans-serif;padding:24px">';
	echo '<h2>Upload file not available</h2>';
	echo '<p>The archived source file for this upload is missing on disk (common if the import engine cleaned temp files before archive).</p>';
	if ($dlPriceId > 0) {
		echo '<p><a href="' . htmlspecialchars($exportUrl, ENT_QUOTES, 'UTF-8') . '">Download current prices from database (CSV)</a></p>';
	}
	echo '</body></html>';
	exit;
}

if ($action === 'export_db') {
	epc_history_stream_db_export($db_link, (int)($_GET['price_id'] ?? $_POST['price_id'] ?? 0));
}

header('Content-Type: application/json; charset=utf-8');

$activeRow = ($priceId > 0) ? epc_price_history_get_active($db_link, $priceId) : null;
$rows = epc_price_history_list($db_link, $priceId, $limit);

ob_start();

$dbItemsCount = 0;
$priceListName = '';
if ($priceId > 0) {
	try {
		$metaQ = $db_link->prepare('SELECT `name`, `records_count` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
		$metaQ->execute([$priceId]);
		$meta = $metaQ->fetch(PDO::FETCH_ASSOC);
		if ($meta) {
			$priceListName = (string)$meta['name'];
			$dbItemsCount = (int)$meta['records_count'];
		}
	} catch (Throwable $e) {
		$dbItemsCount = 0;
	}
}

$rowCount = count($rows);
?>
<div class="epc-hist-shell">
<?php if ($activeRow): ?>
	<?php
	$activeHasFile = trim((string)$activeRow['stored_relpath']) !== '';
	$activeHasIssues = trim((string)($activeRow['issues_relpath'] ?? '')) !== '';
	$activeSkipped = (int)($activeRow['rows_skipped'] ?? 0);
	?>
	<div class="epc-hist-active">
		<div>
			<p class="epc-hist-active__title"><i class="fas fa-check-circle"></i> Active upload file</p>
			<p class="epc-hist-active__meta">
				<strong><?php echo htmlspecialchars((string)$activeRow['original_filename'], ENT_QUOTES, 'UTF-8'); ?></strong><br>
				<?php echo htmlspecialchars((string)$activeRow['created_at'], ENT_QUOTES, 'UTF-8'); ?>
				&middot; <?php echo number_format((int)$activeRow['rows_imported']); ?> imported
				&middot; <?php echo number_format((int)$activeRow['brands_count']); ?> brands
				&middot; <?php echo number_format((int)$activeRow['items_count']); ?> items in DB
			</p>
		</div>
		<div class="epc-hist-active__actions">
			<?php if ($activeHasFile): ?>
				<a class="btn btn-sm btn-primary" href="<?php echo $downloadBase; ?>?action=download_latest&amp;price_id=<?php echo (int)$activeRow['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-download"></i> Download active
				</a>
			<?php else: ?>
				<a class="btn btn-sm btn-primary" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$activeRow['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-download"></i> Download from DB
				</a>
			<?php endif; ?>
			<a class="btn btn-sm btn-default" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$activeRow['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
				<i class="fas fa-database"></i> Export DB
			</a>
			<?php if ($activeHasIssues || $activeSkipped > 0): ?>
				<a class="btn btn-sm btn-warning" href="<?php echo $downloadBase; ?>?action=download_skipped&amp;history_id=<?php echo (int)$activeRow['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-exclamation-triangle"></i> Skipped (<?php echo number_format($activeSkipped); ?>)
				</a>
				<a class="btn btn-sm btn-danger" href="<?php echo $downloadBase; ?>?action=download_errors&amp;history_id=<?php echo (int)$activeRow['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-times-circle"></i> Errors
				</a>
			<?php endif; ?>
		</div>
	</div>
<?php elseif ($priceId > 0): ?>
	<div class="epc-hist-warn">
		No archived active file for this list yet.
		You can still <a href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$priceId; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">export current DB prices</a><?php if ($dbItemsCount > 0): ?> (<?php echo number_format($dbItemsCount); ?> rows)<?php endif; ?>.
	</div>
<?php endif; ?>

<?php if ($rowCount === 0): ?>
	<div class="epc-hist-empty">
		<div class="epc-hist-empty__icon"><i class="fas fa-history"></i></div>
		<h5>No upload history yet</h5>
		<p>
			<?php if ($priceId > 0 && $dbItemsCount > 0): ?>
				This price list already has <strong><?php echo number_format($dbItemsCount); ?></strong> rows in the database, but no archived upload files are stored yet.
				New uploads will appear here with downloadable source files.
			<?php elseif ($priceId > 0): ?>
				Upload or update this price list to start building downloadable history.
			<?php else: ?>
				No archived uploads across price lists yet.
			<?php endif; ?>
		</p>
		<div>
			<?php if ($priceId > 0): ?>
				<a class="btn btn-sm btn-primary" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$priceId; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
					<i class="fas fa-database"></i> Export current DB
				</a>
				<button type="button" class="btn btn-sm btn-default" data-epc-open-pyprices-history="1">
					<i class="fas fa-clock"></i> Open task history
				</button>
			<?php endif; ?>
		</div>
	</div>
<?php else: ?>
	<div class="epc-hist-toolbar">
		<div class="epc-hist-toolbar__left">
			<input type="search" class="epc-hist-search" placeholder="Filter by file, source, status…" autocomplete="off" />
			<span class="epc-hist-count"><?php echo (int)$rowCount; ?> record<?php echo $rowCount === 1 ? '' : 's'; ?></span>
		</div>
		<?php if ($priceId > 0): ?>
			<a class="btn btn-xs btn-default" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$priceId; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>">
				<i class="fas fa-database"></i> Export DB
			</a>
		<?php endif; ?>
	</div>
	<p class="epc-hist-note">Row marked <span class="label label-success">ACTIVE</span> is the latest successful import for the shop. Download links open instantly; missing archives fall back to DB export.</p>
	<div class="epc-hist-table-wrap">
		<table class="table table-condensed epc-hist-table">
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
					<th>Items</th>
					<th>Status</th>
					<th>Actions</th>
				</tr>
			</thead>
			<tbody>
			<?php foreach ($rows as $row): ?>
				<?php
				$status = (string)$row['status'];
				$statusClass = $status === 'ok' ? 'success' : ($status === 'partial' ? 'warning' : ($status === 'pending' ? 'info' : 'danger'));
				$hasFile = trim((string)$row['stored_relpath']) !== '';
				$err = trim((string)$row['error_text']);
				$isActive = (int)($row['is_active'] ?? 0) === 1;
				$rowSkipped = (int)$row['rows_skipped'];
				$hasIssuesFile = trim((string)($row['issues_relpath'] ?? '')) !== '';
				$canIssues = $hasIssuesFile || $err !== '' || $rowSkipped > 0;
				$filterHay = strtolower(trim(
					(string)$row['id'] . ' ' .
					(string)$row['created_at'] . ' ' .
					(string)$row['price_name'] . ' ' .
					(string)$row['price_id'] . ' ' .
					epc_price_history_source_label((string)$row['upload_source']) . ' ' .
					(string)$row['original_filename'] . ' ' .
					$status . ' ' .
					$err
				));
				?>
				<tr class="<?php echo $isActive ? 'epc-hist-row-active' : ''; ?>" data-hist-filter="<?php echo htmlspecialchars($filterHay, ENT_QUOTES, 'UTF-8'); ?>">
					<td class="epc-hist-num"><?php echo (int)$row['id']; ?></td>
					<td class="epc-hist-num"><?php echo htmlspecialchars((string)$row['created_at'], ENT_QUOTES, 'UTF-8'); ?></td>
					<?php if ($priceId <= 0): ?>
						<td><?php echo htmlspecialchars((string)$row['price_name'] . ' (#' . $row['price_id'] . ')', ENT_QUOTES, 'UTF-8'); ?></td>
					<?php endif; ?>
					<td><?php echo htmlspecialchars(epc_price_history_source_label((string)$row['upload_source']), ENT_QUOTES, 'UTF-8'); ?></td>
					<td class="epc-hist-file" title="<?php echo htmlspecialchars($err, ENT_QUOTES, 'UTF-8'); ?>">
						<?php echo htmlspecialchars((string)$row['original_filename'], ENT_QUOTES, 'UTF-8'); ?>
						<?php if ($isActive): ?> <span class="label label-success">ACTIVE</span><?php endif; ?>
						<?php if ((int)$row['file_size'] > 0): ?>
							<br><small class="text-muted"><?php echo number_format((int)$row['file_size']); ?> bytes</small>
						<?php endif; ?>
					</td>
					<td class="epc-hist-num"><?php echo number_format((int)$row['rows_imported']); ?></td>
					<td class="epc-hist-num"><?php echo number_format($rowSkipped); ?></td>
					<td class="epc-hist-num"><?php echo number_format((int)$row['brands_count']); ?></td>
					<td class="epc-hist-num"><?php echo number_format((int)$row['items_count']); ?></td>
					<td>
						<span class="label label-<?php echo $statusClass; ?>"><?php echo htmlspecialchars($status, ENT_QUOTES, 'UTF-8'); ?></span>
						<?php if ($err !== ''): ?>
							<br><small class="text-danger"><?php echo htmlspecialchars(mb_substr($err, 0, 100, 'UTF-8'), ENT_QUOTES, 'UTF-8'); ?></small>
						<?php endif; ?>
					</td>
					<td class="epc-hist-actions">
						<?php if ($hasFile): ?>
							<a class="btn btn-xs btn-primary" href="<?php echo $downloadBase; ?>?action=download&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Download original upload file"><i class="fas fa-download"></i></a>
						<?php elseif ($status === 'pending'): ?>
							<span class="text-muted" title="Import in progress"><i class="fas fa-spinner fa-pulse"></i></span>
						<?php else: ?>
							<a class="btn btn-xs btn-default" href="<?php echo $downloadBase; ?>?action=export_db&amp;price_id=<?php echo (int)$row['price_id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Source archive missing — export current DB"><i class="fas fa-database"></i></a>
						<?php endif; ?>
						<?php if ($canIssues): ?>
							<a class="btn btn-xs btn-warning" href="<?php echo $downloadBase; ?>?action=download_skipped&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Skipped lines"><i class="fas fa-exclamation-triangle"></i></a>
							<a class="btn btn-xs btn-danger" href="<?php echo $downloadBase; ?>?action=download_errors&amp;history_id=<?php echo (int)$row['id']; ?>&amp;csrf_guard_key=<?php echo urlencode($csrfKey); ?>" title="Errors"><i class="fas fa-times-circle"></i></a>
						<?php endif; ?>
					</td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	</div>
<?php endif; ?>
</div>
<?php
$html = ob_get_clean();

echo json_encode([
	'status' => true,
	'html' => $html,
	'count' => count($rows),
	'active' => $activeRow,
], JSON_UNESCAPED_UNICODE);
