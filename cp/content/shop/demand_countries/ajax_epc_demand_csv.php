<?php
/**
 * Preview / import demand-countries CSV + overview stats for AI Parts module.
 */
header('Content-Type: application/json; charset=utf-8');
ini_set('display_errors', '0');
set_time_limit(180);
if (ob_get_level()) {
	@ob_end_clean();
}

try {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
	$db_link->query('SET NAMES utf8mb4');
} catch (Throwable $e) {
	http_response_code(503);
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'forbidden')));
}

$csrf_check_admin = true;
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php';

$action = isset($_POST['action']) ? (string) $_POST['action'] : (isset($_GET['action']) ? (string) $_GET['action'] : '');
$mode = isset($_POST['mode']) ? (string) $_POST['mode'] : 'merge';
$file_path = isset($_POST['file_full_path']) ? (string) $_POST['file_full_path'] : '';

try {
	epc_demand_ensure_schema($db_link);
} catch (Throwable $e) {
	exit(json_encode(array('status' => false, 'message' => 'Schema error')));
}

if ($action === 'stats') {
	$registry = epc_demand_country_registry();
	$byCountry = array();
	try {
		$st = $db_link->query(
			'SELECT `country_code`, COUNT(*) AS `cnt`, COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), \'|\', `article_norm`)) AS `parts`
			 FROM `epc_article_demand` GROUP BY `country_code` ORDER BY `cnt` DESC'
		);
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$code = (string) $row['country_code'];
			$byCountry[] = array(
				'code' => $code,
				'name' => isset($registry[$code]['name']) ? (string) $registry[$code]['name'] : $code,
				'tags' => (int) $row['cnt'],
				'parts' => (int) $row['parts'],
			);
		}
	} catch (Throwable $e) {
	}
	$totalTags = 0;
	$totalParts = 0;
	try {
		$totalTags = (int) $db_link->query('SELECT COUNT(*) FROM `epc_article_demand`')->fetchColumn();
		$totalParts = (int) $db_link->query('SELECT COUNT(DISTINCT CONCAT(UPPER(`manufacturer`), \'|\', `article_norm`)) FROM `epc_article_demand`')->fetchColumn();
	} catch (Throwable $e) {
	}
	$demandMarkets = array();
	foreach ($registry as $code => $meta) {
		if (epc_demand_is_stock_pool_country_code($code)) {
			continue;
		}
		$demandMarkets[] = array('code' => $code, 'name' => (string) $meta['name']);
	}
	exit(json_encode(array(
		'status' => true,
		'stats' => array(
			'total_tags' => $totalTags,
			'total_parts' => $totalParts,
			'by_country' => $byCountry,
			'markets' => $demandMarkets,
		),
	), JSON_UNESCAPED_UNICODE));
}

// List brand + article rows tagged for one demand country (CP click-through).
if ($action === 'country_parts') {
	$registry = epc_demand_country_registry();
	$code = epc_demand_normalize_country_code((string) ($_POST['country'] ?? $_GET['country'] ?? ''));
	if ($code === '' || !isset($registry[$code])) {
		exit(json_encode(array('status' => false, 'message' => 'Unknown country code')));
	}
	if (epc_demand_is_stock_pool_country_code($code)) {
		exit(json_encode(array('status' => false, 'message' => 'ARE is UAE stock pool — not a demand market')));
	}
	$q = trim((string) ($_POST['q'] ?? $_GET['q'] ?? ''));
	$page = max(1, (int) ($_POST['page'] ?? $_GET['page'] ?? 1));
	$perPage = (int) ($_POST['per_page'] ?? $_GET['per_page'] ?? 50);
	if ($perPage < 10) {
		$perPage = 10;
	}
	if ($perPage > 200) {
		$perPage = 200;
	}
	$where = '`country_code` = ?';
	$bind = array($code);
	if ($q !== '') {
		$like = '%' . $q . '%';
		$where .= ' AND (`manufacturer` LIKE ? OR `article_norm` LIKE ? OR `notes` LIKE ?)';
		$bind[] = $like;
		$bind[] = $like;
		$bind[] = $like;
	}
	$total = 0;
	try {
		$cst = $db_link->prepare(
			'SELECT COUNT(*) FROM (
				SELECT 1 FROM `epc_article_demand` WHERE ' . $where . '
				GROUP BY UPPER(`manufacturer`), `article_norm`
			) AS t'
		);
		$cst->execute($bind);
		$total = (int) $cst->fetchColumn();
	} catch (Throwable $e) {
		exit(json_encode(array('status' => false, 'message' => 'Count failed')));
	}
	$pages = max(1, (int) ceil($total / $perPage));
	if ($page > $pages) {
		$page = $pages;
	}
	$offset = ($page - 1) * $perPage;
	$parts = array();
	try {
		$st = $db_link->prepare(
			'SELECT UPPER(`manufacturer`) AS `manufacturer`, `article_norm`,
				MAX(`source`) AS `source`, MAX(`notes`) AS `notes`, MAX(`created_at`) AS `created_at`
			 FROM `epc_article_demand`
			 WHERE ' . $where . '
			 GROUP BY UPPER(`manufacturer`), `article_norm`
			 ORDER BY `manufacturer` ASC, `article_norm` ASC
			 LIMIT ' . (int) $perPage . ' OFFSET ' . (int) $offset
		);
		$st->execute($bind);
		$keyPairs = array();
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$brand = trim((string) ($row['manufacturer'] ?? ''));
			$article = trim((string) ($row['article_norm'] ?? ''));
			if ($brand === '' || $article === '') {
				continue;
			}
			$key = $brand . '|' . $article;
			$keyPairs[$key] = array($brand, $article);
			$parts[] = array(
				'brand' => $brand,
				'article' => $article,
				'source' => (string) ($row['source'] ?? ''),
				'notes' => (string) ($row['notes'] ?? ''),
				'created_at' => (int) ($row['created_at'] ?? 0),
				'other_countries' => array(),
				'search_url' => '/en/shop/part_search?article=' . rawurlencode($article)
					. '&manufacturer=' . rawurlencode($brand),
			);
		}
		// Batch-load other demand markets for this page (avoid N+1).
		if ($keyPairs !== array()) {
			$or = array();
			$obind = array();
			foreach ($keyPairs as $pair) {
				$or[] = '(UPPER(`manufacturer`) = ? AND `article_norm` = ?)';
				$obind[] = $pair[0];
				$obind[] = $pair[1];
			}
			$ost = $db_link->prepare(
				'SELECT UPPER(`manufacturer`) AS `manufacturer`, `article_norm`, `country_code`
				 FROM `epc_article_demand`
				 WHERE `country_code` <> ? AND (' . implode(' OR ', $or) . ')'
			);
			$ost->execute(array_merge(array($code), $obind));
			$otherMap = array();
			while ($orow = $ost->fetch(PDO::FETCH_ASSOC)) {
				$k = trim((string) $orow['manufacturer']) . '|' . trim((string) $orow['article_norm']);
				$norm = epc_demand_normalize_country_code((string) ($orow['country_code'] ?? ''));
				if ($norm === '' || epc_demand_is_stock_pool_country_code($norm)) {
					continue;
				}
				if (!isset($otherMap[$k])) {
					$otherMap[$k] = array();
				}
				$otherMap[$k][$norm] = true;
			}
			foreach ($parts as &$p) {
				$k = $p['brand'] . '|' . $p['article'];
				if (!empty($otherMap[$k])) {
					$codes = array_keys($otherMap[$k]);
					sort($codes);
					$p['other_countries'] = $codes;
				}
			}
			unset($p);
		}
	} catch (Throwable $e) {
		exit(json_encode(array('status' => false, 'message' => 'Query failed')));
	}
	exit(json_encode(array(
		'status' => true,
		'country' => array(
			'code' => $code,
			'name' => (string) ($registry[$code]['name'] ?? $code),
		),
		'q' => $q,
		'page' => $page,
		'per_page' => $perPage,
		'pages' => $pages,
		'total' => $total,
		'parts' => $parts,
	), JSON_UNESCAPED_UNICODE));
}

if ($file_path === '') {
	exit(json_encode(array('status' => false, 'message' => 'Missing file path')));
}

$file_name = basename($file_path);
$tmpRoot = realpath($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp');
$fileReal = realpath($file_path);
if ($tmpRoot === false || $fileReal === false || strpos($fileReal, $tmpRoot) !== 0 || !is_file($fileReal)) {
	// Legacy equality check used by older uploads
	$expected = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/tmp/' . $file_name;
	if ($expected !== $file_path || strpos($file_path, '..') !== false || !is_readable($file_path)) {
		exit(json_encode(array('status' => false, 'message' => 'Invalid file path')));
	}
	$fileReal = $file_path;
}

if ($action === 'preview') {
	$result = epc_demand_csv_preview_file($fileReal, 40);
	// Flag UAE/ARE stock-pool codes in preview rows
	if (!empty($result['rows']) && is_array($result['rows'])) {
		foreach ($result['rows'] as &$row) {
			$codes = epc_demand_parse_country_codes_string((string) ($row['countries'] ?? ''));
			foreach ($codes as $code) {
				if (epc_demand_is_stock_pool_country_code($code)) {
					if (!isset($row['errors']) || !is_array($row['errors'])) {
						$row['errors'] = array();
					}
					$row['errors'][] = $code . ' is UAE stock pool — not a demand market';
				}
			}
		}
		unset($row);
	}
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

if ($action === 'import') {
	$result = epc_demand_csv_import_file($db_link, $fileReal, $mode === 'replace' ? 'replace' : 'merge');
	@unlink($fileReal);
	exit(json_encode($result, JSON_UNESCAPED_UNICODE));
}

exit(json_encode(array('status' => false, 'message' => 'Unknown action')));
