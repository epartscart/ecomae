<?php
/**
 * Marketing & growth — helpers, live metrics, progress.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_marketing_schema.php';
require_once __DIR__ . '/epc_marketing_strategies_data.php';

function epc_marketing_h($v): string
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_marketing_table_exists(PDO $db, string $table): bool
{
	$st = $db->prepare('SHOW TABLES LIKE ?');
	$st->execute(array($table));
	return (bool)$st->fetchColumn();
}

function epc_marketing_live_snapshot(PDO $db): array
{
	$snap = array(
		'generated_at' => gmdate('c'),
		'orders_total' => 0,
		'orders_7d' => 0,
		'orders_30d' => 0,
		'users_total' => 0,
		'marketplace_orders' => 0,
		'whatsapp_api_sent' => 0,
		'whatsapp_api_failed' => 0,
		'price_rows' => 0,
		'brands_count' => 0,
		'ga_property' => 'G-J19D1KHXCG',
		'sitemap_url' => '',
		'domain' => '',
	);

	try {
		if (epc_marketing_table_exists($db, 'shop_orders')) {
			$snap['orders_total'] = (int)$db->query(
				'SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1'
			)->fetchColumn();
			$weekAgo = time() - 7 * 86400;
			$monthAgo = time() - 30 * 86400;
			$st = $db->prepare('SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ?');
			$st->execute(array($weekAgo));
			$snap['orders_7d'] = (int)$st->fetchColumn();
			$st->execute(array($monthAgo));
			$snap['orders_30d'] = (int)$st->fetchColumn();
		}
	} catch (Throwable $e) {
	}

	try {
		if (epc_marketing_table_exists($db, 'users')) {
			$snap['users_total'] = (int)$db->query('SELECT COUNT(*) FROM `users`')->fetchColumn();
		}
	} catch (Throwable $e) {
	}

	try {
		if (epc_marketing_table_exists($db, 'epc_marketplace_orders')) {
			$snap['marketplace_orders'] = (int)$db->query('SELECT COUNT(*) FROM `epc_marketplace_orders`')->fetchColumn();
		}
	} catch (Throwable $e) {
	}

	try {
		if (epc_marketing_table_exists($db, 'epc_whatsapp_notify_log')) {
			$snap['whatsapp_api_sent'] = (int)$db->query(
				"SELECT COUNT(*) FROM `epc_whatsapp_notify_log` WHERE `status` = 'sent'"
			)->fetchColumn();
			$snap['whatsapp_api_failed'] = (int)$db->query(
				"SELECT COUNT(*) FROM `epc_whatsapp_notify_log` WHERE `status` != 'sent'"
			)->fetchColumn();
		}
	} catch (Throwable $e) {
	}

	try {
		if (epc_marketing_table_exists($db, 'shop_docpart_prices_data')) {
			$snap['price_rows'] = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_prices_data`')->fetchColumn();
			$snap['brands_count'] = (int)$db->query(
				'SELECT COUNT(DISTINCT `manufacturer`) FROM `shop_docpart_prices_data` WHERE TRIM(`manufacturer`) != \'\''
			)->fetchColumn();
		}
	} catch (Throwable $e) {
	}

	return $snap;
}

function epc_marketing_load_progress(PDO $db): array
{
	$progress = array();
	if (!epc_marketing_table_exists($db, 'epc_marketing_task_progress')) {
		return $progress;
	}
	$rows = $db->query('SELECT * FROM `epc_marketing_task_progress`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		$sk = (string)$row['strategy_key'];
		$tk = (string)$row['task_key'];
		if (!isset($progress[$sk])) {
			$progress[$sk] = array();
		}
		$progress[$sk][$tk] = array(
			'done' => (int)$row['is_done'] === 1,
			'done_at' => (int)$row['done_at'],
			'note' => (string)$row['note'],
		);
	}
	return $progress;
}

function epc_marketing_completion_stats(array $strategies, array $progress): array
{
	$total = 0;
	$done = 0;
	$byStrategy = array();
	foreach ($strategies as $key => $str) {
		$stTotal = count($str['follow_tasks']);
		$stDone = 0;
		foreach ($str['follow_tasks'] as $taskKey => $label) {
			$total++;
			if (!empty($progress[$key][$taskKey]['done'])) {
				$done++;
				$stDone++;
			}
		}
		$byStrategy[$key] = array(
			'total' => $stTotal,
			'done' => $stDone,
			'pct' => $stTotal > 0 ? (int)round(100 * $stDone / $stTotal) : 0,
		);
	}
	return array(
		'total' => $total,
		'done' => $done,
		'pct' => $total > 0 ? (int)round(100 * $done / $total) : 0,
		'by_strategy' => $byStrategy,
	);
}

function epc_marketing_latest_kpis(PDO $db, array $strategies): array
{
	$latest = array();
	if (!epc_marketing_table_exists($db, 'epc_marketing_kpi_log')) {
		return $latest;
	}
	foreach ($strategies as $str) {
		foreach ($str['kpis'] as $kpiKey => $meta) {
			$st = $db->prepare(
				'SELECT * FROM `epc_marketing_kpi_log` WHERE `kpi_key` = ? ORDER BY `recorded_at` DESC LIMIT 1'
			);
			$st->execute(array($kpiKey));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$latest[$kpiKey] = $row;
			}
		}
	}
	return $latest;
}

function epc_marketing_kpi_history(PDO $db, string $kpiKey, int $limit = 12): array
{
	if (!epc_marketing_table_exists($db, 'epc_marketing_kpi_log')) {
		return array();
	}
	$st = $db->prepare(
		'SELECT * FROM `epc_marketing_kpi_log` WHERE `kpi_key` = ? ORDER BY `recorded_at` DESC LIMIT ' . (int)$limit
	);
	$st->execute(array($kpiKey));
	return $st->fetchAll(PDO::FETCH_ASSOC);
}

function epc_marketing_recent_reviews(PDO $db, int $limit = 20): array
{
	if (!epc_marketing_table_exists($db, 'epc_marketing_reviews')) {
		return array();
	}
	return $db->query(
		'SELECT * FROM `epc_marketing_reviews` ORDER BY `created_at` DESC LIMIT ' . (int)$limit
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_marketing_toggle_task(PDO $db, string $strategyKey, string $taskKey, bool $done, int $userId = 0): void
{
	epc_marketing_ensure_schema($db);
	$now = time();
	$st = $db->prepare(
		'INSERT INTO `epc_marketing_task_progress` (`strategy_key`, `task_key`, `is_done`, `done_at`, `updated_at`)
		 VALUES (?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `is_done` = VALUES(`is_done`), `done_at` = VALUES(`done_at`), `updated_at` = VALUES(`updated_at`)'
	);
	$st->execute(array($strategyKey, $taskKey, $done ? 1 : 0, $done ? $now : null, $now));
}

function epc_marketing_save_kpi(
	PDO $db,
	string $strategyKey,
	string $kpiKey,
	$value,
	string $note,
	int $userId = 0
): void {
	epc_marketing_ensure_schema($db);
	$decimal = is_numeric($value) ? (float)$value : null;
	$text = is_numeric($value) ? (string)$value : trim((string)$value);
	$st = $db->prepare(
		'INSERT INTO `epc_marketing_kpi_log`
		 (`strategy_key`, `kpi_key`, `value_decimal`, `value_text`, `note`, `recorded_at`, `recorded_by`)
		 VALUES (?, ?, ?, ?, ?, ?, ?)'
	);
	$st->execute(array($strategyKey, $kpiKey, $decimal, $text, $note, time(), $userId));
}

function epc_marketing_save_review(
	PDO $db,
	string $strategyKey,
	string $reviewType,
	int $score,
	string $notes,
	int $userId = 0
): void {
	epc_marketing_ensure_schema($db);
	$st = $db->prepare(
		'INSERT INTO `epc_marketing_reviews` (`strategy_key`, `review_type`, `score`, `notes`, `created_at`, `created_by`)
		 VALUES (?, ?, ?, ?, ?, ?)'
	);
	$st->execute(array($strategyKey, $reviewType, max(0, min(5, $score)), $notes, time(), $userId));
}

function epc_marketing_resolve_link(string $url, string $backend, string $domain): string
{
	if (preg_match('#^https?://#i', $url)) {
		return $url;
	}
	if ($url[0] === '/') {
		if (strpos($url, '/cp/') === 0) {
			return rtrim($domain, '/') . preg_replace('#^/cp/#', '/' . $backend . '/', $url);
		}
		return rtrim($domain, '/') . $url;
	}
	return $url;
}

function epc_marketing_demo_report(PDO $db): array
{
	epc_marketing_ensure_schema($db);
	$strategies = epc_marketing_strategies();
	$progress = epc_marketing_load_progress($db);
	return array(
		'status' => true,
		'generated_at' => gmdate('c'),
		'completion' => epc_marketing_completion_stats($strategies, $progress),
		'live' => epc_marketing_live_snapshot($db),
		'strategies' => array_keys($strategies),
	);
}
