<?php
/**
 * Price list upload history: archive uploaded files and record import statistics for CP.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

require_once __DIR__ . '/epc_price_import_helpers.php';

function epc_price_history_ensure_schema(PDO $db_link): void
{
	$db_link->exec("CREATE TABLE IF NOT EXISTS `epc_price_upload_history` (
		`id` INT(11) NOT NULL AUTO_INCREMENT,
		`price_id` INT(11) NOT NULL DEFAULT 0,
		`price_name` VARCHAR(255) NOT NULL DEFAULT '',
		`upload_source` VARCHAR(32) NOT NULL DEFAULT '',
		`source_ref` VARCHAR(64) NOT NULL DEFAULT '',
		`original_filename` VARCHAR(255) NOT NULL DEFAULT '',
		`stored_relpath` VARCHAR(512) NOT NULL DEFAULT '',
		`file_size` BIGINT NOT NULL DEFAULT 0,
		`rows_imported` INT(11) NOT NULL DEFAULT 0,
		`rows_skipped` INT(11) NOT NULL DEFAULT 0,
		`rows_in_db` INT(11) NOT NULL DEFAULT 0,
		`brands_count` INT(11) NOT NULL DEFAULT 0,
		`items_count` INT(11) NOT NULL DEFAULT 0,
		`status` VARCHAR(16) NOT NULL DEFAULT 'ok',
		`error_text` TEXT NULL,
		`stats_json` LONGTEXT NULL,
		`uploaded_by` INT(11) NOT NULL DEFAULT 0,
		`is_active` TINYINT(1) NOT NULL DEFAULT 0,
		`created_at` DATETIME NOT NULL,
		PRIMARY KEY (`id`),
		KEY `price_id` (`price_id`),
		KEY `created_at` (`created_at`),
		KEY `is_active` (`price_id`, `is_active`),
		KEY `source_ref` (`upload_source`, `source_ref`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");

	epc_price_history_ensure_issues_column($db_link);
	epc_price_history_ensure_active_column($db_link);
}

function epc_price_history_ensure_issues_column(PDO $db_link): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	try {
		$db_link->query('SELECT `issues_relpath` FROM `epc_price_upload_history` LIMIT 1');
	} catch (Throwable $e) {
		try {
			$db_link->exec(
				'ALTER TABLE `epc_price_upload_history` ADD COLUMN `issues_relpath` VARCHAR(512) NOT NULL DEFAULT \'\' AFTER `stored_relpath`;'
			);
		} catch (Throwable $e2) {
			// Already exists.
		}
	}
}

function epc_price_history_ensure_active_column(PDO $db_link): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	try {
		$db_link->query('SELECT `is_active` FROM `epc_price_upload_history` LIMIT 1');
	} catch (Throwable $e) {
		try {
			$db_link->exec(
				'ALTER TABLE `epc_price_upload_history` ADD COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 0 AFTER `uploaded_by`;'
			);
			$db_link->exec(
				'ALTER TABLE `epc_price_upload_history` ADD KEY `is_active` (`price_id`, `is_active`);'
			);
		} catch (Throwable $e2) {
			// Column may already exist under a different migration path.
		}
		epc_price_history_backfill_active_flags($db_link);
	}
}

function epc_price_history_backfill_active_flags(PDO $db_link): void
{
	static $backfilled = false;
	if ($backfilled) {
		return;
	}
	$backfilled = true;
	try {
		$db_link->exec(
			"UPDATE `epc_price_upload_history` h
			 INNER JOIN (
				SELECT `price_id`, MAX(`id`) AS `max_id`
				FROM `epc_price_upload_history`
				WHERE TRIM(`stored_relpath`) <> '' AND `status` IN ('ok','partial')
				GROUP BY `price_id`
			 ) t ON h.`id` = t.`max_id`
			 SET h.`is_active` = 1
			 WHERE h.`is_active` = 0;"
		);
	} catch (Throwable $e) {
		// Ignore if column missing.
	}
}

function epc_price_history_set_active(PDO $db_link, int $priceId, int $historyId): void
{
	if ($priceId <= 0 || $historyId <= 0) {
		return;
	}
	epc_price_history_ensure_schema($db_link);
	$db_link->prepare('UPDATE `epc_price_upload_history` SET `is_active` = 0 WHERE `price_id` = ?;')
		->execute([$priceId]);
	$db_link->prepare(
		'UPDATE `epc_price_upload_history` SET `is_active` = 1 WHERE `id` = ? AND `price_id` = ? LIMIT 1;'
	)->execute([$historyId, $priceId]);
}

function epc_price_history_get_active(PDO $db_link, int $priceId): ?array
{
	if ($priceId <= 0) {
		return null;
	}
	epc_price_history_ensure_schema($db_link);
	$q = $db_link->prepare(
		'SELECT * FROM `epc_price_upload_history`
		 WHERE `price_id` = ? AND `is_active` = 1
		 ORDER BY `id` DESC LIMIT 1;'
	);
	$q->execute([$priceId]);
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if ($row && trim((string)$row['stored_relpath']) !== '' && is_file(epc_price_history_file_absolute_path($row))) {
		return $row;
	}
	return epc_price_history_get_latest_with_file($db_link, $priceId);
}

function epc_price_history_get_latest_with_file(PDO $db_link, int $priceId): ?array
{
	if ($priceId <= 0) {
		return null;
	}
	epc_price_history_ensure_schema($db_link);
	$q = $db_link->prepare(
		"SELECT * FROM `epc_price_upload_history`
		 WHERE `price_id` = ? AND TRIM(`stored_relpath`) <> ''
		 ORDER BY `id` DESC LIMIT 20;"
	);
	$q->execute([$priceId]);
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		if (is_file(epc_price_history_file_absolute_path($row))) {
			return $row;
		}
	}
	return null;
}

/**
 * Latest downloadable upload per price list (prefers active row).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_price_history_get_latest_map(PDO $db_link): array
{
	epc_price_history_ensure_schema($db_link);
	$map = [];

	$activeQ = $db_link->query(
		"SELECT * FROM `epc_price_upload_history`
		 WHERE `is_active` = 1 AND TRIM(`stored_relpath`) <> ''
		 ORDER BY `id` DESC;"
	);
	while ($row = $activeQ->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int)$row['price_id'];
		if ($pid > 0 && !isset($map[$pid]) && is_file(epc_price_history_file_absolute_path($row))) {
			$map[$pid] = $row;
		}
	}

	$latestQ = $db_link->query(
		"SELECT h.* FROM `epc_price_upload_history` h
		 INNER JOIN (
			SELECT `price_id`, MAX(`id`) AS `max_id`
			FROM `epc_price_upload_history`
			WHERE TRIM(`stored_relpath`) <> ''
			GROUP BY `price_id`
		 ) t ON h.`id` = t.`max_id`
		 ORDER BY h.`id` DESC;"
	);
	while ($row = $latestQ->fetch(PDO::FETCH_ASSOC)) {
		$pid = (int)$row['price_id'];
		if ($pid > 0 && !isset($map[$pid]) && is_file(epc_price_history_file_absolute_path($row))) {
			$map[$pid] = $row;
		}
	}
	return $map;
}

function epc_price_history_update_by_source_ref(PDO $db_link, string $source, string $sourceRef, array $params): int
{
	if ($sourceRef === '') {
		return 0;
	}
	epc_price_history_ensure_schema($db_link);
	$q = $db_link->prepare(
		'SELECT `id`, `price_id` FROM `epc_price_upload_history`
		 WHERE `upload_source` = ? AND `source_ref` = ? ORDER BY `id` DESC LIMIT 1;'
	);
	$q->execute([$source, $sourceRef]);
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return 0;
	}
	$historyId = (int)$row['id'];
	$priceId = (int)$row['price_id'];

	$sets = [];
	$vals = [];
	foreach ($params as $col => $val) {
		if (in_array($col, ['rows_imported', 'rows_skipped', 'rows_in_db', 'brands_count', 'items_count', 'status', 'error_text', 'stats_json', 'stored_relpath', 'issues_relpath', 'file_size', 'original_filename'], true)) {
			$sets[] = '`' . $col . '` = ?';
			$vals[] = $val;
		}
	}
	if (count($sets) === 0) {
		return $historyId;
	}
	$vals[] = $historyId;
	$db_link->prepare('UPDATE `epc_price_upload_history` SET ' . implode(', ', $sets) . ' WHERE `id` = ? LIMIT 1;')
		->execute($vals);

	$status = (string)($params['status'] ?? '');
	if (in_array($status, ['ok', 'partial'], true)) {
		epc_price_history_set_active($db_link, $priceId, $historyId);
	}

	if (!empty($params['import_issues']) && is_array($params['import_issues'])) {
		epc_price_history_attach_issues($db_link, $historyId, $priceId, $params['import_issues']);
	} elseif (trim((string)($params['error_text'] ?? '')) !== '' || !empty($params['stats_json'])) {
		$statsDecoded = is_array($params['stats_json'] ?? null)
			? $params['stats_json']
			: json_decode((string)($params['stats_json'] ?? ''), true);
		$moduleIssues = epc_price_history_issues_from_module(is_array($statsDecoded) ? $statsDecoded : null, (string)($params['error_text'] ?? ''));
		if (count($moduleIssues) > 0) {
			epc_price_history_attach_issues($db_link, $historyId, $priceId, $moduleIssues);
		}
	}
	return $historyId;
}

/**
 * Update the newest history row for a pyprices client task (any upload_source).
 *
 * @param array<string,mixed> $params
 */
function epc_price_history_update_by_task_ref(PDO $db_link, int $taskId, array $params): int
{
	if ($taskId <= 0) {
		return 0;
	}
	epc_price_history_ensure_schema($db_link);
	$sourceRef = 'task_' . $taskId;
	$q = $db_link->prepare(
		'SELECT `id`, `price_id`, `stored_relpath` FROM `epc_price_upload_history`
		 WHERE `source_ref` = ? ORDER BY `id` DESC LIMIT 1;'
	);
	$q->execute([$sourceRef]);
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return 0;
	}
	$historyId = (int)$row['id'];
	$priceId = (int)$row['price_id'];

	$sets = [];
	$vals = [];
	foreach ($params as $col => $val) {
		if (in_array($col, ['upload_source', 'rows_imported', 'rows_skipped', 'rows_in_db', 'brands_count', 'items_count', 'status', 'error_text', 'stats_json', 'stored_relpath', 'issues_relpath', 'file_size', 'original_filename'], true)) {
			$sets[] = '`' . $col . '` = ?';
			$vals[] = $val;
		}
	}
	if (count($sets) === 0) {
		return $historyId;
	}
	$vals[] = $historyId;
	$db_link->prepare('UPDATE `epc_price_upload_history` SET ' . implode(', ', $sets) . ' WHERE `id` = ? LIMIT 1;')
		->execute($vals);

	$status = (string)($params['status'] ?? '');
	if (in_array($status, ['ok', 'partial'], true)) {
		epc_price_history_set_active($db_link, $priceId, $historyId);
	}

	if (!empty($params['import_issues']) && is_array($params['import_issues'])) {
		epc_price_history_attach_issues($db_link, $historyId, $priceId, $params['import_issues']);
	} elseif (trim((string)($params['error_text'] ?? '')) !== '' || !empty($params['stats_json'])) {
		$statsDecoded = is_array($params['stats_json'] ?? null)
			? $params['stats_json']
			: json_decode((string)($params['stats_json'] ?? ''), true);
		$moduleIssues = epc_price_history_issues_from_module(is_array($statsDecoded) ? $statsDecoded : null, (string)($params['error_text'] ?? ''));
		if (count($moduleIssues) > 0) {
			epc_price_history_attach_issues($db_link, $historyId, $priceId, $moduleIssues);
		}
	}
	return $historyId;
}

/**
 * @param array<string,mixed> $params
 */
function epc_price_history_update_by_id(PDO $db_link, int $historyId, array $params): void
{
	if ($historyId <= 0) {
		return;
	}
	epc_price_history_ensure_schema($db_link);
	$row = epc_price_history_get_row($db_link, $historyId);
	if (!$row) {
		return;
	}
	$priceId = (int)$row['price_id'];

	$sets = [];
	$vals = [];
	foreach ($params as $col => $val) {
		if (in_array($col, ['upload_source', 'rows_imported', 'rows_skipped', 'rows_in_db', 'brands_count', 'items_count', 'status', 'error_text', 'stats_json', 'stored_relpath', 'issues_relpath', 'file_size', 'original_filename'], true)) {
			$sets[] = '`' . $col . '` = ?';
			$vals[] = $val;
		}
	}
	if (count($sets) === 0) {
		return;
	}
	$vals[] = $historyId;
	$db_link->prepare('UPDATE `epc_price_upload_history` SET ' . implode(', ', $sets) . ' WHERE `id` = ? LIMIT 1;')
		->execute($vals);

	$status = (string)($params['status'] ?? '');
	if (in_array($status, ['ok', 'partial'], true)) {
		epc_price_history_set_active($db_link, $priceId, $historyId);
	}

	if (!empty($params['import_issues']) && is_array($params['import_issues'])) {
		epc_price_history_attach_issues($db_link, $historyId, $priceId, $params['import_issues']);
	} elseif (trim((string)($params['error_text'] ?? '')) !== '' || !empty($params['stats_json'])) {
		$statsDecoded = is_array($params['stats_json'] ?? null)
			? $params['stats_json']
			: json_decode((string)($params['stats_json'] ?? ''), true);
		$moduleIssues = epc_price_history_issues_from_module(is_array($statsDecoded) ? $statsDecoded : null, (string)($params['error_text'] ?? ''));
		if (count($moduleIssues) > 0) {
			epc_price_history_attach_issues($db_link, $historyId, $priceId, $moduleIssues);
		}
	}
}

function epc_price_history_storage_root(): string
{
	return $_SERVER['DOCUMENT_ROOT'] . '/content/files/price_upload_history';
}

function epc_price_history_archive_file(string $sourcePath, int $priceId, string $originalFilename): string
{
	if (!is_file($sourcePath) || !is_readable($sourcePath)) {
		return '';
	}
	$safeName = preg_replace('/[^a-zA-Z0-9._-]+/', '_', basename($originalFilename));
	if ($safeName === '' || $safeName === '_') {
		$safeName = 'upload.csv';
	}
	$dir = epc_price_history_storage_root() . '/' . $priceId;
	if (!is_dir($dir) && !mkdir($dir, 0755, true)) {
		return '';
	}
	$dest = $dir . '/' . time() . '_' . $safeName;
	if (!copy($sourcePath, $dest)) {
		return '';
	}
	return '/content/files/price_upload_history/' . $priceId . '/' . basename($dest);
}

function epc_price_history_count_brands(PDO $db_link, int $priceId): int
{
	$q = $db_link->prepare(
		"SELECT COUNT(DISTINCT `manufacturer`) FROM `shop_docpart_prices_data`
		 WHERE `price_id` = ? AND TRIM(`manufacturer`) <> '';"
	);
	$q->execute([$priceId]);
	return (int)$q->fetchColumn();
}

function epc_price_history_count_items(PDO $db_link, int $priceId): int
{
	$q = $db_link->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?;');
	$q->execute([$priceId]);
	return (int)$q->fetchColumn();
}

function epc_price_history_exists_source_ref(PDO $db_link, string $source, string $sourceRef): bool
{
	if ($sourceRef === '') {
		return false;
	}
	epc_price_history_ensure_schema($db_link);
	$q = $db_link->prepare(
		'SELECT COUNT(*) FROM `epc_price_upload_history` WHERE `upload_source` = ? AND `source_ref` = ? LIMIT 1;'
	);
	$q->execute([$source, $sourceRef]);
	return (int)$q->fetchColumn() > 0;
}

/**
 * @param array<string,mixed> $params
 */
function epc_price_history_save(PDO $db_link, array $params): int
{
	epc_price_history_ensure_schema($db_link);

	$priceId = (int)($params['price_id'] ?? 0);
	$priceName = (string)($params['price_name'] ?? '');
	$source = (string)($params['upload_source'] ?? 'unknown');
	$sourceRef = (string)($params['source_ref'] ?? '');

	if ($sourceRef !== '' && epc_price_history_exists_source_ref($db_link, $source, $sourceRef)) {
		return 0;
	}

	if ($priceId > 0) {
		if ($priceName === '') {
			$pn = $db_link->prepare('SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
			$pn->execute([$priceId]);
			$priceName = (string)$pn->fetchColumn();
		}
		$brands = epc_price_history_count_brands($db_link, $priceId);
		$items = epc_price_history_count_items($db_link, $priceId);
	} else {
		$brands = (int)($params['brands_count'] ?? 0);
		$items = (int)($params['items_count'] ?? 0);
	}

	$stats = $params['stats_json'] ?? null;
	if (is_array($stats)) {
		$stats = json_encode($stats, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	}

	$stmt = $db_link->prepare(
		'INSERT INTO `epc_price_upload_history`
		(`price_id`,`price_name`,`upload_source`,`source_ref`,`original_filename`,`stored_relpath`,`file_size`,
		 `rows_imported`,`rows_skipped`,`rows_in_db`,`brands_count`,`items_count`,`status`,`error_text`,`stats_json`,`uploaded_by`,`created_at`)
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,NOW());'
	);
	$stmt->execute([
		$priceId,
		mb_substr($priceName, 0, 255, 'UTF-8'),
		mb_substr($source, 0, 32, 'UTF-8'),
		mb_substr($sourceRef, 0, 64, 'UTF-8'),
		mb_substr((string)($params['original_filename'] ?? ''), 0, 255, 'UTF-8'),
		mb_substr((string)($params['stored_relpath'] ?? ''), 0, 512, 'UTF-8'),
		(int)($params['file_size'] ?? 0),
		(int)($params['rows_imported'] ?? 0),
		(int)($params['rows_skipped'] ?? 0),
		(int)($params['rows_in_db'] ?? 0),
		$brands,
		$items,
		mb_substr((string)($params['status'] ?? 'ok'), 0, 16, 'UTF-8'),
		(string)($params['error_text'] ?? ''),
		$stats,
		(int)($params['uploaded_by'] ?? 0),
	]);

	$historyId = (int)$db_link->lastInsertId();
	$stored = trim((string)($params['stored_relpath'] ?? ''));
	$status = (string)($params['status'] ?? 'ok');
	if ($historyId > 0 && $priceId > 0 && $stored !== '' && in_array($status, ['ok', 'partial'], true)) {
		epc_price_history_set_active($db_link, $priceId, $historyId);
	}

	$importIssues = $params['import_issues'] ?? null;
	if ($historyId > 0 && is_array($importIssues) && count($importIssues) > 0) {
		epc_price_history_attach_issues($db_link, $historyId, $priceId, $importIssues);
	} elseif ($historyId > 0 && trim((string)($params['error_text'] ?? '')) !== '') {
		$statsDecoded = null;
		if (!empty($params['stats_json'])) {
			$statsDecoded = is_array($params['stats_json']) ? $params['stats_json'] : json_decode((string)$params['stats_json'], true);
		}
		$moduleIssues = epc_price_history_issues_from_module(is_array($statsDecoded) ? $statsDecoded : null, (string)$params['error_text']);
		if (count($moduleIssues) > 0) {
			epc_price_history_attach_issues($db_link, $historyId, $priceId, $moduleIssues);
		}
	}

	return $historyId;
}

/**
 * @return array<int,array<string,mixed>>
 */
function epc_price_history_list(PDO $db_link, int $priceId = 0, int $limit = 50): array
{
	epc_price_history_ensure_schema($db_link);
	$limit = max(1, min(200, $limit));
	if ($priceId > 0) {
		$q = $db_link->prepare(
			'SELECT * FROM `epc_price_upload_history` WHERE `price_id` = ? ORDER BY `id` DESC LIMIT ' . $limit . ';'
		);
		$q->execute([$priceId]);
	} else {
		$q = $db_link->query(
			'SELECT * FROM `epc_price_upload_history` ORDER BY `id` DESC LIMIT ' . $limit . ';'
		);
	}
	$rows = [];
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$rows[] = $row;
	}
	return $rows;
}

function epc_price_history_get_row(PDO $db_link, int $historyId): ?array
{
	epc_price_history_ensure_schema($db_link);
	$q = $db_link->prepare('SELECT * FROM `epc_price_upload_history` WHERE `id` = ? LIMIT 1;');
	$q->execute([$historyId]);
	$row = $q->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_price_history_file_absolute_path(array $row): string
{
	$rel = (string)($row['stored_relpath'] ?? '');
	if ($rel === '') {
		return '';
	}
	return $_SERVER['DOCUMENT_ROOT'] . $rel;
}

function epc_price_history_issues_absolute_path(array $row): string
{
	$rel = (string)($row['issues_relpath'] ?? '');
	if ($rel === '') {
		return '';
	}
	return $_SERVER['DOCUMENT_ROOT'] . $rel;
}

/**
 * @param array<string,mixed> $fields
 * @return array<string,mixed>
 */
/**
 * @param array<string,mixed> $fields Pass _csv_row + _column_labels for full per-column detail.
 * @return array<string,mixed>
 */
function epc_price_history_issue(int $lineNo, string $issueType, string $reason, array $fields = []): array
{
	if (isset($fields['_csv_row'], $fields['_column_labels'])) {
		$csvRow = is_array($fields['_csv_row']) ? $fields['_csv_row'] : [];
		$labels = is_array($fields['_column_labels']) ? $fields['_column_labels'] : [];
		unset($fields['_csv_row'], $fields['_column_labels']);
		return epc_price_history_issue_detail($lineNo, $issueType, $reason, $csvRow, $labels, $fields);
	}
	return epc_price_history_issue_module($lineNo, $issueType, $reason, $fields);
}

/**
 * @param array<int,array<string,mixed>> $issues
 */
function epc_price_history_write_issues_file(int $priceId, int $historyId, array $issues): string
{
	if ($priceId <= 0 || $historyId <= 0 || count($issues) === 0) {
		return '';
	}
	$dir = epc_price_history_storage_root() . '/' . $priceId;
	if (!is_dir($dir) && !mkdir($dir, 0755, true)) {
		return '';
	}
	$dest = $dir . '/' . $historyId . '_issues.csv';
	$fh = fopen($dest, 'wb');
	if ($fh === false) {
		return '';
	}
	$csvRows = epc_price_history_issues_to_csv_rows($issues);
	foreach ($csvRows as $csvRow) {
		fputcsv($fh, $csvRow);
	}
	fclose($fh);
	return '/content/files/price_upload_history/' . $priceId . '/' . basename($dest);
}

/**
 * @param array<int,array<string,mixed>> $issues
 */
function epc_price_history_attach_issues(PDO $db_link, int $historyId, int $priceId, array $issues): void
{
	if ($historyId <= 0 || $priceId <= 0) {
		return;
	}
	epc_price_history_ensure_schema($db_link);
	$rel = epc_price_history_write_issues_file($priceId, $historyId, $issues);
	if ($rel === '') {
		return;
	}
	$db_link->prepare('UPDATE `epc_price_upload_history` SET `issues_relpath` = ? WHERE `id` = ? LIMIT 1;')
		->execute([$rel, $historyId]);
}

/**
 * @param array<string,mixed>|null $stats
 * @return array<int,array<string,mixed>>
 */
function epc_price_history_issues_from_module($stats, string $errorText = ''): array
{
	$issues = [];
	$n = 0;
	if ($errorText !== '') {
		$issues[] = epc_price_history_issue_module(0, 'error', 'import_error', ['details' => $errorText]);
	}
	if (!is_array($stats)) {
		return $issues;
	}
	foreach (['validation_messages' => 'validation', 'error_messages' => 'error', 'other_messages' => 'info'] as $key => $reasonPrefix) {
		if (empty($stats[$key]) || !is_array($stats[$key])) {
			continue;
		}
		foreach ($stats[$key] as $msg) {
			$n++;
			$issues[] = epc_price_history_issue_module($n, 'error', $reasonPrefix, ['details' => (string)$msg]);
		}
	}
	if (!empty($stats['errors_general_list']) && is_array($stats['errors_general_list'])) {
		foreach ($stats['errors_general_list'] as $msg) {
			$n++;
			$issues[] = epc_price_history_issue_module($n, 'error', 'general', ['details' => (string)$msg]);
		}
	}
	return $issues;
}

/**
 * @return array<int,array<string,mixed>>
 */
function epc_price_history_collect_issues_for_history(array $row): array
{
	$path = epc_price_history_issues_absolute_path($row);
	if ($path !== '' && is_file($path)) {
		return [];
	}
	$stats = json_decode((string)($row['stats_json'] ?? ''), true);
	return epc_price_history_issues_from_module(is_array($stats) ? $stats : null, (string)($row['error_text'] ?? ''));
}

/**
 * @return array{skipped:int,error:int,total:int}
 */
function epc_price_history_count_issue_types(array $issues): array
{
	$skipped = 0;
	$error = 0;
	foreach ($issues as $issue) {
		if (($issue['issue_type'] ?? '') === 'skipped') {
			$skipped++;
		} else {
			$error++;
		}
	}
	return ['skipped' => $skipped, 'error' => $error, 'total' => count($issues)];
}

/**
 * Stream issues CSV (optionally filtered: skipped | error | all).
 */
function epc_price_history_stream_issues_csv(array $row, string $filter = 'all'): void
{
	$issues = [];
	$path = epc_price_history_issues_absolute_path($row);
	if ($path !== '' && is_file($path)) {
		$fh = fopen($path, 'rb');
		if ($fh !== false) {
			$header = fgetcsv($fh);
			if (!is_array($header)) {
				$header = [];
			}
			$typeIdx = array_search('issue_type', $header, true);
			if ($typeIdx === false) {
				$typeIdx = 1;
			}
			header('Content-Type: text/csv; charset=utf-8');
			$filterLabel = $filter === 'all' ? 'issues' : $filter;
			$safeName = preg_replace('/[^a-zA-Z0-9._-]+/', '_', (string)$row['price_name']);
			$filename = ($safeName !== '' ? $safeName : 'price') . '_' . $filterLabel . '_upload_' . (int)$row['id'] . '.csv';
			header('Content-Disposition: attachment; filename="' . $filename . '"');
			$out = fopen('php://output', 'w');
			fputcsv($out, $header);
			while (($data = fgetcsv($fh)) !== false) {
				if (!is_array($data)) {
					continue;
				}
				$type = (string)($data[$typeIdx] ?? '');
				if ($filter === 'skipped' && $type !== 'skipped') {
					continue;
				}
				if ($filter === 'error' && $type !== 'error') {
					continue;
				}
				fputcsv($out, $data);
			}
			fclose($fh);
			fclose($out);
			return;
		}
	}
	$issues = epc_price_history_collect_issues_for_history($row);
	header('Content-Type: text/csv; charset=utf-8');
	$filterLabel = $filter === 'all' ? 'issues' : $filter;
	$safeName = preg_replace('/[^a-zA-Z0-9._-]+/', '_', (string)$row['price_name']);
	$filename = ($safeName !== '' ? $safeName : 'price') . '_' . $filterLabel . '_upload_' . (int)$row['id'] . '.csv';
	header('Content-Disposition: attachment; filename="' . $filename . '"');
	$out = fopen('php://output', 'w');
	$csvRows = epc_price_history_issues_to_csv_rows($issues);
	$writtenHeader = false;
	foreach ($csvRows as $csvRow) {
		if (!$writtenHeader) {
			fputcsv($out, $csvRow);
			$writtenHeader = true;
			continue;
		}
		$type = (string)($csvRow[1] ?? '');
		if ($filter === 'skipped' && $type !== 'skipped') {
			continue;
		}
		if ($filter === 'error' && $type !== 'error') {
			continue;
		}
		fputcsv($out, $csvRow);
	}
	fclose($out);
}

/**
 * Resolve or create a Docpart price list by name (UAE supplier files).
 *
 * @return array<string,mixed>|null
 */
function epc_price_resolve_or_create_list(PDO $db_link, int $priceId, string $priceName): ?array
{
	$priceName = trim($priceName);
	if ($priceId > 0) {
		$q = $db_link->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
		$q->execute([$priceId]);
		$row = $q->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	if ($priceName !== '') {
		$q = $db_link->prepare('SELECT * FROM `shop_docpart_prices` WHERE `name` = ? LIMIT 1');
		$q->execute([$priceName]);
		$row = $q->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
		$q = $db_link->prepare('SELECT * FROM `shop_docpart_prices` WHERE UPPER(`name`) = UPPER(?) LIMIT 1');
		$q->execute([$priceName]);
		$row = $q->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}

	if ($priceName === '') {
		return null;
	}

	$stmt = $db_link->prepare(
		'INSERT INTO `shop_docpart_prices`
		(`name`,`load_mode`,`strings_to_left`,`manufacturer_col`,`article_col`,`name_col`,`exist_col`,`price_col`,`time_to_exe_col`,`storage_col`,`min_order_col`,`clean_before`,`file_name_substring`,`encoding`,`separator`,`h_time`)
		VALUES (?, 1, 1, 1, 2, 3, 4, 5, 7, 0, 0, 1, ?, ?, ?, ?);'
	);
	$stmt->execute([$priceName, $priceName, 'utf-8', ',', '0']);
	$newId = (int)$db_link->lastInsertId();
	$q = $db_link->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
	$q->execute([$newId]);
	return $q->fetch(PDO::FETCH_ASSOC) ?: null;
}

function epc_price_link_storage_to_list(PDO $db_link, string $storageName, int $priceId): void
{
	if ($storageName === '' || $priceId <= 0) {
		return;
	}
	$q = $db_link->prepare('SELECT `id`, `connection_options` FROM `shop_storages` WHERE UPPER(`name`) = UPPER(?) LIMIT 1');
	$q->execute([$storageName]);
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return;
	}
	$opts = json_decode((string)$row['connection_options'], true);
	if (!is_array($opts)) {
		$opts = [];
	}
	$opts['price_id'] = (string)$priceId;
	if (!isset($opts['probability'])) {
		$opts['probability'] = '100';
	}
	$db_link->prepare('UPDATE `shop_storages` SET `connection_options` = ? WHERE `id` = ? LIMIT 1;')
		->execute([json_encode($opts, JSON_UNESCAPED_UNICODE), (int)$row['id']]);
}

function epc_price_history_channel_upload_source(string $channel): string
{
	$map = [
		'ftp' => 'pyprices_ftp',
		'email' => 'pyprices_email',
		'url' => 'pyprices_url',
		'local_path' => 'pyprices_pc',
	];
	return $map[$channel] ?? 'pyprices';
}

function epc_price_history_channel_label(string $channel): string
{
	$labels = [
		'ftp' => 'FTP',
		'email' => 'Email',
		'url' => 'URL',
		'local_path' => 'PC upload',
	];
	return $labels[$channel] ?? 'Pyprices';
}

function epc_price_history_tmp_upload_root(): string
{
	global $DP_Config;
	if (!isset($DP_Config) || !is_object($DP_Config)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$DP_Config = new DP_Config();
	}
	return $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . $DP_Config->tmp_dir_prices_upload;
}

/**
 * Locate the price file pyprices used (before temp cleanup when possible).
 *
 * @return array{path:string,name:string}
 */
function epc_price_history_find_task_file(array $task): array
{
	$pathKeys = ['local_path', 'downloaded_path', 'file_path', 'source_file_path', 'price_file_path', 'archive_path'];
	foreach ($pathKeys as $key) {
		$p = trim((string)($task[$key] ?? ''));
		if ($p !== '' && is_file($p)) {
			return ['path' => $p, 'name' => basename($p)];
		}
	}

	$tmpFolder = trim((string)($task['tmp_folder_name'] ?? ''));
	if ($tmpFolder !== '') {
		$dir = epc_price_history_tmp_upload_root() . '/' . $tmpFolder;
		if (is_dir($dir)) {
			foreach (scandir($dir) ?: [] as $entry) {
				if ($entry === '.' || $entry === '..') {
					continue;
				}
				$full = $dir . '/' . $entry;
				if (is_file($full)) {
					return ['path' => $full, 'name' => $entry];
				}
			}
		}
	}

	$substr = trim((string)($task['file_name_substring'] ?? ''));
	$root = epc_price_history_tmp_upload_root();
	if ($substr !== '' && is_dir($root)) {
		$bestPath = '';
		$bestMtime = 0;
		try {
			$it = new RecursiveIteratorIterator(
				new RecursiveDirectoryIterator($root, FilesystemIterator::SKIP_DOTS),
				RecursiveIteratorIterator::LEAVES_ONLY
			);
			foreach ($it as $fileInfo) {
				if (!$fileInfo->isFile()) {
					continue;
				}
				$name = $fileInfo->getFilename();
				if (stripos($name, $substr) === false) {
					continue;
				}
				$mtime = $fileInfo->getMTime();
				if ($mtime >= $bestMtime) {
					$bestMtime = $mtime;
					$bestPath = $fileInfo->getPathname();
				}
			}
		} catch (Throwable $e) {
			// Ignore unreadable temp dirs.
		}
		if ($bestPath !== '') {
			return ['path' => $bestPath, 'name' => basename($bestPath)];
		}
	}

	$orig = trim((string)($task['file_name'] ?? ''));
	if ($orig !== '') {
		return ['path' => '', 'name' => $orig];
	}

	return ['path' => '', 'name' => 'price_import.csv'];
}

/**
 * Create a pending history row when a pyprices task is queued (FTP / email / URL / cron).
 */
function epc_price_history_begin_pyprices_task(PDO $db_link, int $priceId, int $taskId, string $channel = ''): void
{
	if ($priceId <= 0 || $taskId <= 0) {
		return;
	}
	if ($channel === 'local_path') {
		return;
	}
	epc_price_history_ensure_schema($db_link);
	$sourceRef = 'task_' . $taskId;
	$exists = $db_link->prepare(
		'SELECT COUNT(*) FROM `epc_price_upload_history` WHERE `source_ref` = ? LIMIT 1;'
	);
	$exists->execute([$sourceRef]);
	if ((int)$exists->fetchColumn() > 0) {
		return;
	}

	$priceName = '';
	$pn = $db_link->prepare('SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1;');
	$pn->execute([$priceId]);
	$priceName = (string)$pn->fetchColumn();

	$via = epc_price_history_channel_label($channel);
	$origName = $via . ' — ' . ($priceName !== '' ? $priceName : ('price #' . $priceId));

	epc_price_history_save($db_link, [
		'price_id' => $priceId,
		'price_name' => $priceName,
		'upload_source' => epc_price_history_channel_upload_source($channel),
		'source_ref' => $sourceRef,
		'original_filename' => $origName,
		'stored_relpath' => '',
		'file_size' => 0,
		'status' => 'pending',
	]);
}

function epc_price_history_log_pyprices_task(PDO $db_link, array $task, int $priceId): void
{
	if ($priceId <= 0) {
		return;
	}
	$taskId = (int)($task['client_task_id'] ?? 0);
	if ($taskId <= 0) {
		return;
	}

	$records = (int)($task['records_handled'] ?? 0);
	$errors = [];
	if (!empty($task['validation_messages']) && is_array($task['validation_messages'])) {
		$errors = array_merge($errors, $task['validation_messages']);
	}
	if (!empty($task['error_messages']) && is_array($task['error_messages'])) {
		$errors = array_merge($errors, $task['error_messages']);
	}
	if (!empty($task['other_error'])) {
		$errors[] = (string)$task['other_error'];
	}
	$status = ($records > 0 && count($errors) === 0) ? 'ok' : (($records > 0) ? 'partial' : 'failed');
	$errorText = count($errors) ? implode("\n", array_slice($errors, 0, 20)) : '';
	$stats = [
		'validation_messages' => $task['validation_messages'] ?? [],
		'error_messages' => $task['error_messages'] ?? [],
		'other_messages' => $task['other_messages'] ?? [],
	];
	$moduleIssues = epc_price_history_issues_from_module($stats, $errorText);
	$channel = (string)($task['source'] ?? '');
	$uploadSource = epc_price_history_channel_upload_source($channel);

	$fileInfo = epc_price_history_find_task_file($task);
	$storedRel = '';
	$fileSize = 0;
	$origName = $fileInfo['name'];
	if ($fileInfo['path'] !== '') {
		$storedRel = epc_price_history_archive_file($fileInfo['path'], $priceId, $origName);
		$fileSize = (int)filesize($fileInfo['path']);
	}

	$updateParams = [
		'upload_source' => $uploadSource,
		'rows_imported' => $records,
		'rows_in_db' => epc_price_history_count_items($db_link, $priceId),
		'brands_count' => epc_price_history_count_brands($db_link, $priceId),
		'items_count' => epc_price_history_count_items($db_link, $priceId),
		'status' => $status,
		'error_text' => $errorText,
		'stats_json' => json_encode($stats, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		'import_issues' => $moduleIssues,
	];
	if ($storedRel !== '') {
		$updateParams['stored_relpath'] = $storedRel;
		$updateParams['file_size'] = $fileSize;
		$updateParams['original_filename'] = $origName;
	}

	$tmpFolder = (string)($task['tmp_folder_name'] ?? '');
	if ($tmpFolder !== '') {
		$pcUpdate = $updateParams;
		unset($pcUpdate['upload_source']);
		$updated = epc_price_history_update_by_source_ref($db_link, 'pyprices_upload', $tmpFolder, $pcUpdate);
		if ($updated > 0) {
			epc_price_history_refresh_article_search($db_link, $priceId, $records);
			return;
		}
	}

	if (epc_price_history_update_by_task_ref($db_link, $taskId, $updateParams) > 0) {
		epc_price_history_refresh_article_search($db_link, $priceId, $records);
		return;
	}

	epc_price_history_save($db_link, [
		'price_id' => $priceId,
		'upload_source' => $uploadSource,
		'source_ref' => 'task_' . $taskId,
		'original_filename' => $origName,
		'stored_relpath' => $storedRel,
		'file_size' => $fileSize,
		'rows_imported' => $records,
		'rows_in_db' => epc_price_history_count_items($db_link, $priceId),
		'status' => $status,
		'error_text' => $errorText,
		'stats_json' => $stats,
		'import_issues' => $moduleIssues,
	]);

	epc_price_history_refresh_article_search($db_link, $priceId, $records);
}

function epc_price_history_refresh_article_search(PDO $db_link, int $priceId, int $records): void
{
	if ($records <= 0 || $priceId <= 0) {
		return;
	}
	if (!is_file(__DIR__ . '/docpart_article_match.php')) {
		return;
	}
	require_once __DIR__ . '/docpart_article_match.php';
	if (function_exists('docpart_price_data_backfill_article_search')) {
		docpart_price_data_backfill_article_search($db_link, $priceId, 100000);
	}
}
