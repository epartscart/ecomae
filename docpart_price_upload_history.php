<?php
/**
 * Price list upload history: archive uploaded files and record import statistics for CP.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

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
		`created_at` DATETIME NOT NULL,
		PRIMARY KEY (`id`),
		KEY `price_id` (`price_id`),
		KEY `created_at` (`created_at`),
		KEY `source_ref` (`upload_source`, `source_ref`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
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
		VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,NOW());'
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

	return (int)$db_link->lastInsertId();
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
	epc_price_history_save($db_link, [
		'price_id' => $priceId,
		'upload_source' => 'pyprices',
		'source_ref' => 'task_' . $taskId,
		'original_filename' => (string)($task['file_name'] ?? 'pyprices'),
		'rows_imported' => $records,
		'rows_in_db' => epc_price_history_count_items($db_link, $priceId),
		'status' => $status,
		'error_text' => count($errors) ? implode("\n", array_slice($errors, 0, 20)) : '',
		'stats_json' => [
			'validation_messages' => $task['validation_messages'] ?? [],
			'error_messages' => $task['error_messages'] ?? [],
			'other_messages' => $task['other_messages'] ?? [],
		],
	]);
}
