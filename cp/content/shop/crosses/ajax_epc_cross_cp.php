<?php
/**
 * CP crosses: crossbase lookup, one-click add, bulk sync, verification.
 */
header('Content-Type: application/json;charset=utf-8;');
set_time_limit(300);

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config;

try {
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (PDOException $e) {
	exit(json_encode(array('status' => false, 'message' => 'No DB connect')));
}
$db_link->query('SET NAMES utf8;');

$pages_to_check = array(array('id' => 380, 'url' => 'shop/crosses'));
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/check_admin_access/check_admin_access.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	exit(json_encode(array('status' => false, 'message' => 'Access denied')));
}

require_once __DIR__ . '/epc_cp_cross_helpers.php';

$request_object = array();
if (!empty($_POST['request_object'])) {
	$request_object = json_decode((string) $_POST['request_object'], true);
}
if (!is_array($request_object)) {
	$request_object = array();
}

$action = isset($request_object['action']) ? (string) $request_object['action'] : '';
$anchor_article = isset($request_object['article']) ? urldecode((string) $request_object['article']) : '';
$anchor_brand = isset($request_object['manufacturer']) ? urldecode((string) $request_object['manufacturer']) : '';

$answer = array('status' => false);

switch ($action) {
	case 'lookup_crosses':
		$search = epc_cp_cross_fetch_search($DP_Config, $anchor_article, $anchor_brand);
		if ($search === null || empty($search['status'])) {
			$answer['message'] = isset($search['message']) ? (string) $search['message'] : 'Cross search failed or returned no data';
			break;
		}
		$references = isset($search['references']) && is_array($search['references']) ? $search['references'] : array();
		$references = epc_cp_cross_annotate_references($db_link, $anchor_article, $anchor_brand, $references);
		$linked = 0;
		$missing = 0;
		foreach ($references as $ref) {
			if (!empty($ref['cp_linked'])) {
				$linked++;
			} else {
				$missing++;
			}
		}
		$ref_loaded = count($references);
		$total_catalog = isset($search['total_catalog']) ? (int) $search['total_catalog'] : 0;
		if ($total_catalog < 1 && isset($search['total'])) {
			$total_catalog = (int) $search['total'];
		}
		if ($total_catalog < $ref_loaded) {
			$total_catalog = $ref_loaded;
		}
		$answer = array(
			'status' => true,
			'article' => $anchor_article,
			'manufacturer' => $anchor_brand,
			'source' => isset($search['source']) ? (string) $search['source'] : '',
			'reference_count' => $ref_loaded,
			'references_loaded' => $ref_loaded,
			'total_catalog' => $total_catalog,
			'stock_count' => isset($search['stock_count']) ? (int) $search['stock_count'] : 0,
			'crossbase_count' => isset($search['crossbase_count']) ? (int) $search['crossbase_count'] : 0,
			'local_count' => isset($search['local_count']) ? (int) $search['local_count'] : 0,
			'crossbase_persisted' => isset($search['crossbase_persisted']) ? (int) $search['crossbase_persisted'] : 0,
			'cp_links_for_article' => epc_cp_cross_count_links_for_anchor($db_link, $anchor_article, $anchor_brand),
			'cp_linked_in_results' => $linked,
			'cp_missing_in_results' => $missing,
			'references' => $references,
			'stock' => isset($search['stock']) && is_array($search['stock']) ? $search['stock'] : array(),
		);
		break;

	case 'verify_crosses':
		$search = epc_cp_cross_fetch_search($DP_Config, $anchor_article, $anchor_brand);
		$references = ($search && !empty($search['references']) && is_array($search['references']))
			? $search['references']
			: array();
		$references = epc_cp_cross_annotate_references($db_link, $anchor_article, $anchor_brand, $references);
		$linked = 0;
		$missing = 0;
		$missing_samples = array();
		foreach ($references as $ref) {
			if (!empty($ref['cp_linked'])) {
				$linked++;
			} else {
				$missing++;
				if (count($missing_samples) < 15) {
					$missing_samples[] = array(
						'brand' => isset($ref['brand']) ? (string) $ref['brand'] : '',
						'article' => isset($ref['article']) ? (string) $ref['article'] : '',
						'source' => isset($ref['source']) ? (string) $ref['source'] : '',
					);
				}
			}
		}
		$answer = array(
			'status' => true,
			'article' => $anchor_article,
			'manufacturer' => $anchor_brand,
			'cp_links_in_db' => epc_cp_cross_count_links_for_anchor($db_link, $anchor_article, $anchor_brand),
			'results_total' => count($references),
			'cp_linked_in_results' => $linked,
			'cp_missing_in_results' => $missing,
			'coverage_percent' => count($references) > 0 ? round(100 * $linked / count($references), 1) : 0,
			'missing_samples' => $missing_samples,
			'fully_linked' => ($missing === 0 && count($references) > 0),
		);
		break;

	case 'add_cross_link':
		$ref_article = isset($request_object['ref_article']) ? urldecode((string) $request_object['ref_article']) : '';
		$ref_brand = isset($request_object['ref_brand']) ? urldecode((string) $request_object['ref_brand']) : '';
		$result = epc_cp_cross_add_link($db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand);
		$answer = array(
			'status' => true,
			'inserted' => (int) $result['inserted'],
			'already' => (int) $result['already'],
			'skipped' => (int) $result['skipped'],
			'reason' => isset($result['reason']) ? (string) $result['reason'] : '',
			'cp_links_for_article' => epc_cp_cross_count_links_for_anchor($db_link, $anchor_article, $anchor_brand),
		);
		break;

	case 'add_cross_bulk':
		$refs = isset($request_object['references']) && is_array($request_object['references'])
			? $request_object['references']
			: array();
		$source_filter = isset($request_object['source_filter']) ? (string) $request_object['source_filter'] : '';
		$only_missing = !isset($request_object['only_missing']) || !empty($request_object['only_missing']);
		$inserted = 0;
		$already = 0;
		$skipped = 0;
		foreach ($refs as $ref) {
			if (!is_array($ref)) {
				$skipped++;
				continue;
			}
			if ($source_filter !== '') {
				$src = isset($ref['source']) ? (string) $ref['source'] : '';
				if ($src !== $source_filter && strpos($src, $source_filter) === false) {
					continue;
				}
			}
			$ref_article = isset($ref['article']) ? (string) $ref['article'] : '';
			$ref_brand = isset($ref['brand']) ? (string) $ref['brand'] : '';
			if ($only_missing) {
				$st = epc_cp_cross_pair_status($db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand);
				if (!empty($st['linked'])) {
					$already++;
					continue;
				}
			}
			$res = epc_cp_cross_add_link($db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand);
			$inserted += (int) $res['inserted'];
			$already += (int) $res['already'];
			$skipped += (int) $res['skipped'];
		}
		$answer = array(
			'status' => true,
			'inserted' => $inserted,
			'already' => $already,
			'skipped' => $skipped,
			'cp_links_for_article' => epc_cp_cross_count_links_for_anchor($db_link, $anchor_article, $anchor_brand),
		);
		break;

	case 'sync_from_crossbase':
		$search = epc_cp_cross_fetch_search($DP_Config, $anchor_article, $anchor_brand);
		if ($search === null || empty($search['status'])) {
			$answer['message'] = 'Cross search failed';
			break;
		}
		$references = isset($search['references']) && is_array($search['references']) ? $search['references'] : array();
		$import = epc_cp_cross_import_references($db_link, $anchor_article, $anchor_brand, $references, true, 'crossbase');
		$answer = array(
			'status' => true,
			'inserted' => (int) $import['inserted'],
			'already' => (int) $import['already'],
			'skipped' => (int) $import['skipped'],
			'processed' => (int) $import['processed'],
			'cp_links_for_article' => epc_cp_cross_count_links_for_anchor($db_link, $anchor_article, $anchor_brand),
			'reference_count' => count($references),
		);
		break;

	case 'import_full_catalog':
		$search = epc_cp_cross_fetch_search($DP_Config, $anchor_article, $anchor_brand, true);
		if ($search === null || empty($search['status'])) {
			$answer['message'] = isset($search['message']) ? (string) $search['message'] : 'Full cross catalog fetch failed';
			break;
		}
		$references = isset($search['references']) && is_array($search['references']) ? $search['references'] : array();
		$ref_loaded = count($references);
		$total_catalog = isset($search['total_catalog']) ? (int) $search['total_catalog'] : 0;
		if ($total_catalog < 1 && isset($search['total'])) {
			$total_catalog = (int) $search['total'];
		}
		$only_missing = !isset($request_object['only_missing']) || !empty($request_object['only_missing']);
		$import = epc_cp_cross_import_references($db_link, $anchor_article, $anchor_brand, $references, $only_missing, '');
		$answer = array(
			'status' => true,
			'article' => $anchor_article,
			'manufacturer' => $anchor_brand,
			'inserted' => (int) $import['inserted'],
			'already' => (int) $import['already'],
			'skipped' => (int) $import['skipped'],
			'processed' => (int) $import['processed'],
			'references_loaded' => $ref_loaded,
			'total_catalog' => max($total_catalog, $ref_loaded),
			'cp_links_for_article' => epc_cp_cross_count_links_for_anchor($db_link, $anchor_article, $anchor_brand),
			'catalog_note' => ($total_catalog > $ref_loaded)
				? ('Interchange catalog reports ' . $total_catalog . ' crosses; ' . $ref_loaded . ' were parsed from the catalog page and linked.')
				: '',
		);
		break;

	case 'repair_empty_brands':
		$limit = isset($request_object['limit']) ? (int) $request_object['limit'] : 500;
		$result = docpart_cross_repair_empty_manufacturers($db_link, $anchor_article, $limit);
		$answer = array(
			'status' => true,
			'updated' => (int) ($result['updated'] ?? 0),
			'skipped' => (int) ($result['skipped'] ?? 0),
			'error' => isset($result['error']) ? (string) $result['error'] : '',
		);
		break;

	default:
		$answer['message'] = 'Unknown action';
		break;
}

exit(json_encode($answer, JSON_UNESCAPED_UNICODE));
