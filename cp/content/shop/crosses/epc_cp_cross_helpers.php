<?php
/**
 * CP crosses: crossbase lookup, link verification, persist to shop_docpart_articles_analogs_list.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_cross_interchange.php';

function epc_cp_cross_normalize_article($article)
{
	$sweep = array(' ', '-', '_', '`', '/', "'", '"', '\\', '.', ',', '#', "\r\n", "\r", "\n", "\t");
	$article = mb_strtoupper(trim((string) $article), 'UTF-8');
	return str_replace($sweep, '', $article);
}

function epc_cp_cross_prepare_brand($brand)
{
	$sweep = array('#', '`', "\r\n", "\r", "\n", "\t", "'", '"', '\\');
	return trim(str_replace($sweep, '', mb_strtoupper(trim((string) $brand), 'UTF-8')));
}

/**
 * @return array{linked:bool,id:int}
 */
function epc_cp_cross_pair_status(PDO $db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand)
{
	$anchor_article = epc_cp_cross_normalize_article($anchor_article);
	$ref_article = epc_cp_cross_normalize_article($ref_article);
	$anchor_brand = epc_cp_cross_prepare_brand($anchor_brand);
	$ref_brand = epc_cp_cross_prepare_brand($ref_brand);
	if ($anchor_article === '' || $ref_article === '' || $anchor_brand === '' || $ref_brand === '') {
		return array('linked' => false, 'id' => 0);
	}
	if ($anchor_article === $ref_article && $anchor_brand === $ref_brand) {
		return array('linked' => false, 'id' => 0);
	}
	if (function_exists('docpart_cross_pair_exists_with_brands')) {
		return docpart_cross_pair_exists_with_brands($db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand);
	}
	return array('linked' => false, 'id' => 0);
}

function epc_cp_cross_count_links_for_anchor(PDO $db_link, $anchor_article, $anchor_brand = '')
{
	$anchor_article = epc_cp_cross_normalize_article($anchor_article);
	if ($anchor_article === '') {
		return 0;
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	try {
		$query = $db_link->prepare(
			'SELECT COUNT(*) FROM `shop_docpart_articles_analogs_list` '
			. 'WHERE ' . $art_expr . ' = ? OR ' . $analog_expr . ' = ?'
		);
		$query->execute(array($anchor_article, $anchor_article));
		return (int) $query->fetchColumn();
	} catch (Exception $e) {
		return 0;
	}
}

/**
 * Run storefront cross search (same JSON as ajax_epc_cross_search.php).
 *
 * @return array<string,mixed>|null
 */
function epc_cp_cross_fetch_search($DP_Config, $article_input, $anchor_brand, $cp_bulk = false)
{
	$article_input = trim((string) $article_input);
	if ($article_input === '') {
		return null;
	}
	$base = rtrim((string) $DP_Config->domain_path, '/');
	$url = $base . '/content/shop/docpart/ajax_epc_cross_search.php?article=' . rawurlencode($article_input);
	if (trim((string) $anchor_brand) !== '') {
		$url .= '&brand=' . rawurlencode(trim((string) $anchor_brand));
		$url .= '&manufacturer=' . rawurlencode(trim((string) $anchor_brand));
	}
	if ($cp_bulk) {
		$url .= '&cp_bulk=1&tech_key=' . rawurlencode((string) $DP_Config->tech_key);
	}
	$timeout = $cp_bulk ? 180 : 90;
	$html = '';
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($ch, CURLOPT_FOLLOWLOCATION, true);
		curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 8);
		curl_setopt($ch, CURLOPT_TIMEOUT, $timeout);
		curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, false);
		curl_setopt($ch, CURLOPT_USERAGENT, 'ePartsCart CP crosses');
		$html = curl_exec($ch);
		curl_close($ch);
	} else {
		$context = stream_context_create(array(
			'http' => array(
				'timeout' => $timeout,
				'header' => "User-Agent: ePartsCart CP crosses\r\n",
			),
		));
		$html = @file_get_contents($url, false, $context);
	}
	if (!is_string($html) || $html === '') {
		return null;
	}
	$data = json_decode($html, true);
	return is_array($data) ? $data : null;
}

/**
 * @param array<int,array<string,mixed>> $references
 * @return array<int,array<string,mixed>>
 */
function epc_cp_cross_enrich_reference_brand(PDO $db_link, array &$ref, $anchor_brand = '')
{
	$ref_brand = isset($ref['brand']) ? trim((string) $ref['brand']) : '';
	if ($ref_brand !== '') {
		$ref['brand'] = epc_cp_cross_prepare_brand($ref_brand);
		$ref['brand_complete'] = true;
		return;
	}
	$ref_article = isset($ref['article']) ? (string) $ref['article'] : '';
	if (function_exists('docpart_cross_resolve_brand_for_article')) {
		$resolved = docpart_cross_resolve_brand_for_article($db_link, $ref_article, array(
			'fallback_brand' => $anchor_brand,
		));
		if ($resolved !== '') {
			$ref['brand'] = $resolved;
			$ref['brand_inferred'] = true;
			$ref['brand_complete'] = true;
			return;
		}
	}
	$ref['brand_complete'] = false;
}

function epc_cp_cross_annotate_references(PDO $db_link, $anchor_article, $anchor_brand, array $references)
{
	$anchor_brand = epc_cp_cross_prepare_brand($anchor_brand);
	if ($anchor_brand === '' && function_exists('docpart_cross_resolve_brand_for_article')) {
		$anchor_brand = docpart_cross_resolve_brand_for_article($db_link, $anchor_article, array());
	}
	$out = array();
	foreach ($references as $ref) {
		if (!is_array($ref)) {
			continue;
		}
		epc_cp_cross_enrich_reference_brand($db_link, $ref, $anchor_brand);
		$ref_article = isset($ref['article']) ? (string) $ref['article'] : '';
		$ref_brand = isset($ref['brand']) ? (string) $ref['brand'] : '';
		$status = epc_cp_cross_pair_status($db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand);
		$ref['cp_linked'] = !empty($status['linked']);
		$ref['cp_link_id'] = (int) $status['id'];
		$out[] = $ref;
	}
	return $out;
}

/**
 * @return array{inserted:int,skipped:int,already:int}
 */
function epc_cp_cross_add_link(PDO $db_link, $anchor_article, $anchor_brand, $ref_article, $ref_brand)
{
	$anchor_article_show = trim((string) $anchor_article);
	$ref_article_show = trim((string) $ref_article);
	$anchor_article_db = epc_cp_cross_normalize_article($anchor_article_show);
	$ref_article_db = epc_cp_cross_normalize_article($ref_article_show);

	if ($anchor_article_db === '' || $ref_article_db === '') {
		return array('inserted' => 0, 'skipped' => 1, 'already' => 0, 'reason' => 'invalid_article');
	}
	if ($anchor_article_db === $ref_article_db && $anchor_brand === $ref_brand) {
		return array('inserted' => 0, 'skipped' => 1, 'already' => 0, 'reason' => 'same_part_same_brand');
	}
	if (function_exists('docpart_cross_resolve_brand_for_article')) {
		$anchor_brand = docpart_cross_resolve_brand_for_article($db_link, $anchor_article_db, array(
			'fallback_brand' => epc_cp_cross_prepare_brand($anchor_brand),
		));
		$ref_brand = docpart_cross_resolve_brand_for_article($db_link, $ref_article_db, array(
			'fallback_brand' => epc_cp_cross_prepare_brand($ref_brand),
			'partner_brand' => $anchor_brand,
		));
	} else {
		$anchor_brand = epc_cp_cross_prepare_brand($anchor_brand);
		$ref_brand = epc_cp_cross_prepare_brand($ref_brand);
	}
	if ($anchor_brand === '' || $ref_brand === '') {
		return array('inserted' => 0, 'skipped' => 1, 'already' => 0, 'reason' => 'missing_brand');
	}
	$status = epc_cp_cross_pair_status($db_link, $anchor_article_db, $anchor_brand, $ref_article_db, $ref_brand);
	if (!empty($status['linked'])) {
		return array('inserted' => 0, 'skipped' => 0, 'already' => 1, 'reason' => 'already_linked');
	}
	if (!function_exists('docpart_cross_persist_interchange_pair_bidirectional')) {
		return array('inserted' => 0, 'skipped' => 1, 'already' => 0);
	}
	$inserted = (int) docpart_cross_persist_interchange_pair_bidirectional(
		$db_link,
		$anchor_article_db,
		$anchor_brand,
		$ref_article_db,
		$ref_brand
	);
	if ($inserted > 0) {
		return array('inserted' => 1, 'skipped' => 0, 'already' => 0, 'reason' => '');
	}
	return array('inserted' => 0, 'skipped' => 1, 'already' => 0, 'reason' => 'insert_failed');
}

/**
 * Bulk link cross references to CP (shop_docpart_articles_analogs_list).
 *
 * @param array<int,array<string,mixed>> $references
 * @return array{inserted:int,already:int,skipped:int,processed:int}
 */
function epc_cp_cross_import_references(PDO $db_link, $anchor_article, $anchor_brand, array $references, $only_missing = true, $source_filter = '')
{
	$inserted = 0;
	$already = 0;
	$skipped = 0;
	$processed = 0;
	$anchor_brand_prepared = epc_cp_cross_prepare_brand($anchor_brand);
	if ($anchor_brand_prepared === '' && function_exists('docpart_cross_resolve_brand_for_article')) {
		$anchor_brand_prepared = docpart_cross_resolve_brand_for_article($db_link, $anchor_article, array());
	}
	foreach ($references as $ref) {
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
		epc_cp_cross_enrich_reference_brand($db_link, $ref, $anchor_brand_prepared);
		$ref_brand = isset($ref['brand']) ? (string) $ref['brand'] : '';
		if ($ref_brand === '' || $ref_article === '') {
			$skipped++;
			continue;
		}
		$processed++;
		if ($only_missing) {
			$st = epc_cp_cross_pair_status($db_link, $anchor_article, $anchor_brand_prepared, $ref_article, $ref_brand);
			if (!empty($st['linked'])) {
				$already++;
				continue;
			}
		}
		$res = epc_cp_cross_add_link($db_link, $anchor_article, $anchor_brand_prepared, $ref_article, $ref_brand);
		$inserted += (int) $res['inserted'];
		$already += (int) $res['already'];
		$skipped += (int) $res['skipped'];
	}
	return array(
		'inserted' => $inserted,
		'already' => $already,
		'skipped' => $skipped,
		'processed' => $processed,
	);
}
