<?php
/**
 * Bidirectional cross-reference helpers (price name cluster, local DB persist).
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';

function docpart_load_interchange_partners($db_link, $article_norm, $max_rounds = 6, $row_limit = 5000)
{
	$article_norm = docpart_normalize_article_for_price($article_norm);
	if ($article_norm === '') {
		return array();
	}
	$partners = array();
	$seen = array();
	$known_norms = array($article_norm => true);
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	for ($round = 0; $round < $max_rounds; $round++) {
		$norm_batch = array_keys($known_norms);
		if (empty($norm_batch)) {
			break;
		}
		$discovered_norms = array();
		$chunks = array_chunk($norm_batch, 150);
		$stop_all = false;
		foreach ($chunks as $chunk) {
			if ($stop_all || count($partners) >= $row_limit) {
				$stop_all = true;
				break;
			}
			$placeholders = implode(',', array_fill(0, count($chunk), '?'));
			$params = array_merge($chunk, $chunk);
			try {
				$cross_query = $db_link->prepare(
					'SELECT `article`, `manufacturer_article`, `analog`, `manufacturer_analog` '
					. 'FROM `shop_docpart_articles_analogs_list` '
					. 'WHERE ' . $art_expr . ' IN (' . $placeholders . ') OR ' . $analog_expr . ' IN (' . $placeholders . ') '
					. 'LIMIT ' . (int) $row_limit
				);
				$cross_query->execute($params);
			} catch (Exception $e) {
				$stop_all = true;
				break;
			}
			while ($row = $cross_query->fetch(PDO::FETCH_ASSOC)) {
				$row_article_norm = docpart_normalize_article_for_price($row['article']);
				$row_analog_norm = docpart_normalize_article_for_price($row['analog']);
				if (isset($known_norms[$row_article_norm])) {
					$brand = trim((string) $row['manufacturer_analog']);
					$article = trim((string) $row['analog']);
					if ($article !== '' && $row_analog_norm !== '') {
						$key = strtoupper($brand . '|' . $row_analog_norm);
						if (!isset($seen[$key])) {
							$seen[$key] = true;
							$partners[] = array(
								'brand' => $brand,
								'article' => $article,
								'article_norm' => $row_analog_norm,
							);
						}
						$discovered_norms[$row_analog_norm] = true;
					}
				}
				if (isset($known_norms[$row_analog_norm])) {
					$brand = trim((string) $row['manufacturer_article']);
					$article = trim((string) $row['article']);
					if ($article !== '' && $row_article_norm !== '') {
						$key = strtoupper($brand . '|' . $row_article_norm);
						if (!isset($seen[$key])) {
							$seen[$key] = true;
							$partners[] = array(
								'brand' => $brand,
								'article' => $article,
								'article_norm' => $row_article_norm,
							);
						}
						$discovered_norms[$row_article_norm] = true;
					}
				}
				if (count($partners) >= $row_limit) {
					$stop_all = true;
					break;
				}
			}
		}
		if ($stop_all) {
			break;
		}
		$added_norm = false;
		foreach ($discovered_norms as $norm => $_) {
			if (!isset($known_norms[$norm])) {
				$known_norms[$norm] = true;
				$added_norm = true;
			}
		}
		if (!$added_norm) {
			break;
		}
	}
	return $partners;
}

function docpart_price_name_cluster_core($name)
{
	$name = strtoupper(trim((string) $name));
	$name = preg_replace('/\s+/', ' ', $name);
	$name = preg_replace('/\s+\d{5,}[A-Z0-9\-]*$/', '', $name);
	$name = trim($name);
	if ($name === '' || strlen($name) < 6) {
		return '';
	}
	if (preg_match('/^(OIL FILTER|FILTER|AIR FILTER|FUEL FILTER|CABIN FILTER|SPARK PLUG)/', $name)) {
		return $name;
	}
	return '';
}

function docpart_cross_refs_from_price_name_cluster($db_link, $article_norm, $limit = 100)
{
	$article_norm = docpart_normalize_article_for_price($article_norm);
	if ($article_norm === '') {
		return array();
	}
	$refs = array();
	$seen = array();
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$cores = array();
	try {
		$names_query = $db_link->prepare(
			'SELECT DISTINCT TRIM(`name`) AS `name` FROM `shop_docpart_prices_data` '
			. 'WHERE ' . $art_expr . ' = ? AND IFNULL(`exist`, 0) > 0 AND IFNULL(`price`, 0) > 0 '
			. "AND TRIM(`name`) <> '' LIMIT 15"
		);
		$names_query->execute(array($article_norm));
		while ($row = $names_query->fetch(PDO::FETCH_ASSOC)) {
			$core = docpart_price_name_cluster_core(isset($row['name']) ? $row['name'] : '');
			if ($core !== '') {
				$cores[$core] = true;
			}
		}
	} catch (Exception $e) {
		return $refs;
	}
	if (empty($cores)) {
		return $refs;
	}
	foreach (array_keys($cores) as $core) {
		if (count($refs) >= $limit) {
			break;
		}
		try {
			$like = $core . '%';
			$siblings_query = $db_link->prepare(
				'SELECT `manufacturer`, `article`, MAX(`article_show`) AS `article_show`, MAX(`name`) AS `name` '
				. 'FROM `shop_docpart_prices_data` '
				. 'WHERE UPPER(TRIM(`name`)) LIKE ? AND ' . $art_expr . ' <> ? '
				. 'AND IFNULL(`exist`, 0) > 0 AND IFNULL(`price`, 0) > 0 '
				. 'GROUP BY `manufacturer`, `article` '
				. 'ORDER BY `manufacturer`, `article` '
				. 'LIMIT ' . min(40, (int) $limit)
			);
			$siblings_query->execute(array($like, $article_norm));
			while ($product = $siblings_query->fetch(PDO::FETCH_ASSOC)) {
				$product_norm = docpart_normalize_article_for_price(isset($product['article']) ? $product['article'] : '');
				if ($product_norm === '' || $product_norm === $article_norm) {
					continue;
				}
				$brand = trim((string) (isset($product['manufacturer']) ? $product['manufacturer'] : ''));
				$article = trim((string) (!empty($product['article_show']) ? $product['article_show'] : $product['article']));
				$key = strtoupper($brand . '|' . $product_norm);
				if (isset($seen[$key])) {
					continue;
				}
				$seen[$key] = true;
				$refs[] = array(
					'brand' => $brand,
					'article' => $article,
					'article_norm' => $product_norm,
				);
				if (count($refs) >= $limit) {
					break;
				}
			}
		} catch (Exception $e) {
			continue;
		}
	}
	return $refs;
}

/**
 * Parts whose price-list name cites this OEM number (common for aftermarket oil filters).
 */
function docpart_cross_refs_from_stock_oem_mention($db_link, $article_norm, $limit = 30)
{
	$article_norm = docpart_normalize_article_for_price($article_norm);
	if ($article_norm === '' || strlen($article_norm) < 5) {
		return array();
	}
	$refs = array();
	$seen = array();
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	try {
		$like = '%' . $article_norm . '%';
		$oem_query = $db_link->prepare(
			'SELECT `manufacturer`, `article`, MAX(`article_show`) AS `article_show` '
			. 'FROM `shop_docpart_prices_data` '
			. 'WHERE UPPER(TRIM(`name`)) LIKE ? '
			. 'AND ' . $art_expr . ' <> ? '
			. 'AND IFNULL(`exist`, 0) > 0 AND IFNULL(`price`, 0) > 0 '
			. 'GROUP BY `manufacturer`, `article` '
			. 'ORDER BY `manufacturer`, `article` '
			. 'LIMIT ' . max(5, min(25, (int) $limit))
		);
		$oem_query->execute(array($like, $article_norm));
		while ($product = $oem_query->fetch(PDO::FETCH_ASSOC)) {
			$product_norm = docpart_normalize_article_for_price(isset($product['article']) ? $product['article'] : '');
			if ($product_norm === '' || $product_norm === $article_norm) {
				continue;
			}
			$brand = trim((string) (isset($product['manufacturer']) ? $product['manufacturer'] : ''));
			$article = trim((string) (!empty($product['article_show']) ? $product['article_show'] : $product['article']));
			$key = strtoupper($brand . '|' . $product_norm);
			if (isset($seen[$key])) {
				continue;
			}
			$seen[$key] = true;
			$refs[] = array(
				'brand' => $brand,
				'article' => $article,
				'article_norm' => $product_norm,
			);
		}
	} catch (Exception $e) {
		return $refs;
	}
	return $refs;
}

function docpart_cross_prepare_brand_name($brand)
{
	$sweep = array('#', '`', "\r\n", "\r", "\n", "\t", "'", '"', '\\');
	return trim(str_replace($sweep, '', mb_strtoupper(trim((string) $brand), 'UTF-8')));
}

/**
 * Infer OEM brand from normalized article patterns (Toyota 90915*, etc.).
 */
function docpart_cross_infer_brand_from_article_norm($article_norm)
{
	$article_norm = docpart_normalize_article_for_price($article_norm);
	if ($article_norm === '') {
		return '';
	}
	if (preg_match('/^90915/i', $article_norm)) {
		return 'TOYOTA';
	}
	if (preg_match('/^15400/i', $article_norm)) {
		return 'HONDA';
	}
	if (preg_match('/^15208|^22040/i', $article_norm)) {
		return 'NISSAN';
	}
	if (preg_match('/^26300|^28113/i', $article_norm)) {
		return 'HYUNDAI';
	}
	if (preg_match('/^06[A-Z0-9]|^1K0|^5W0|^8E0/i', $article_norm)) {
		return 'VAG';
	}
	if (preg_match('/^A000|^A[0-9]{9,10}$/i', $article_norm)) {
		return 'MERCEDES-BENZ';
	}
	if (preg_match('/^B6Y1|^PE01|^LF05/i', $article_norm)) {
		return 'MAZDA';
	}
	if (preg_match('/^12279|^12280|^12281|^13101|^13102|^13103|^13104|^13105|^13106/i', $article_norm)) {
		return 'TOYOTA';
	}
	if (preg_match('/^46256|^45114/i', $article_norm)) {
		return 'TEIKIN';
	}
	return '';
}

/**
 * Resolve manufacturer for an article using prices, existing crosses, and pattern rules.
 *
 * @param array<string,string> $hints fallback_brand, partner_brand
 */
function docpart_cross_resolve_brand_for_article(PDO $db_link, $article, array $hints = array())
{
	$fallback = docpart_cross_prepare_brand_name(isset($hints['fallback_brand']) ? $hints['fallback_brand'] : '');
	if ($fallback !== '') {
		return $fallback;
	}
	$article_norm = docpart_normalize_article_for_price($article);
	if ($article_norm === '') {
		return '';
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	try {
		$price_query = $db_link->prepare(
			'SELECT UPPER(TRIM(`manufacturer`)) AS `mfr`, COUNT(*) AS `cnt` '
			. 'FROM `shop_docpart_prices_data` '
			. 'WHERE ' . $art_expr . ' = ? AND TRIM(`manufacturer`) <> "" '
			. 'GROUP BY UPPER(TRIM(`manufacturer`)) '
			. 'ORDER BY `cnt` DESC LIMIT 1'
		);
		$price_query->execute(array($article_norm));
		$price_row = $price_query->fetch(PDO::FETCH_ASSOC);
		if ($price_row && !empty($price_row['mfr'])) {
			return docpart_cross_prepare_brand_name($price_row['mfr']);
		}
	} catch (Exception $e) {
		// continue
	}
	try {
		$cross_query = $db_link->prepare(
			'SELECT `manufacturer_article`, `manufacturer_analog`, `article`, `analog` '
			. 'FROM `shop_docpart_articles_analogs_list` '
			. 'WHERE ' . $art_expr . ' = ? OR ' . $analog_expr . ' = ? '
			. 'ORDER BY `id` DESC LIMIT 40'
		);
		$cross_query->execute(array($article_norm, $article_norm));
		$brand_counts = array();
		while ($row = $cross_query->fetch(PDO::FETCH_ASSOC)) {
			$row_article_norm = docpart_normalize_article_for_price($row['article']);
			$row_analog_norm = docpart_normalize_article_for_price($row['analog']);
			$mfr = '';
			if ($row_article_norm === $article_norm) {
				$mfr = docpart_cross_prepare_brand_name($row['manufacturer_article']);
			} elseif ($row_analog_norm === $article_norm) {
				$mfr = docpart_cross_prepare_brand_name($row['manufacturer_analog']);
			}
			if ($mfr !== '') {
				if (!isset($brand_counts[$mfr])) {
					$brand_counts[$mfr] = 0;
				}
				$brand_counts[$mfr]++;
			}
		}
		if (!empty($brand_counts)) {
			arsort($brand_counts);
			return (string) key($brand_counts);
		}
	} catch (Exception $e) {
		// continue
	}
	$partner = docpart_cross_prepare_brand_name(isset($hints['partner_brand']) ? $hints['partner_brand'] : '');
	if ($partner !== '') {
		return $partner;
	}
	return docpart_cross_infer_brand_from_article_norm($article_norm);
}

/**
 * Fill empty manufacturer_article / manufacturer_analog on existing CP cross rows.
 */
function docpart_cross_repair_empty_manufacturers(PDO $db_link, $article_filter = '', $limit = 500)
{
	$limit = max(1, min(5000, (int) $limit));
	$where = '(`manufacturer_article` = "" OR `manufacturer_analog` = "")';
	$params = array();
	if (trim((string) $article_filter) !== '') {
		$norm = docpart_normalize_article_for_price($article_filter);
		if ($norm !== '') {
			$art_expr = docpart_sql_article_normalized_expr('`article`');
			$analog_expr = docpart_sql_article_normalized_expr('`analog`');
			$where .= ' AND (' . $art_expr . ' = ? OR ' . $analog_expr . ' = ?)';
			$params[] = $norm;
			$params[] = $norm;
		}
	}
	$updated = 0;
	$skipped = 0;
	try {
		$select = $db_link->prepare(
			'SELECT `id`, `article`, `manufacturer_article`, `analog`, `manufacturer_analog` '
			. 'FROM `shop_docpart_articles_analogs_list` WHERE ' . $where . ' ORDER BY `id` DESC LIMIT ' . $limit
		);
		$select->execute($params);
		$update = $db_link->prepare(
			'UPDATE `shop_docpart_articles_analogs_list` '
			. 'SET `manufacturer_article` = ?, `manufacturer_analog` = ? WHERE `id` = ?'
		);
		while ($row = $select->fetch(PDO::FETCH_ASSOC)) {
			$mfr_article = docpart_cross_prepare_brand_name($row['manufacturer_article']);
			$mfr_analog = docpart_cross_prepare_brand_name($row['manufacturer_analog']);
			if ($mfr_article === '') {
				$mfr_article = docpart_cross_resolve_brand_for_article($db_link, $row['article'], array(
					'partner_brand' => $mfr_analog,
				));
			}
			if ($mfr_analog === '') {
				$mfr_analog = docpart_cross_resolve_brand_for_article($db_link, $row['analog'], array(
					'partner_brand' => $mfr_article,
				));
			}
			if ($mfr_article === '' || $mfr_analog === '') {
				$skipped++;
				continue;
			}
			$update->execute(array($mfr_article, $mfr_analog, (int) $row['id']));
			$updated++;
		}
	} catch (Exception $e) {
		return array('updated' => 0, 'skipped' => 0, 'error' => $e->getMessage());
	}
	return array('updated' => $updated, 'skipped' => $skipped, 'error' => '');
}

/**
 * True when this exact article+brand pair is already linked (both directions).
 * Supports same part number with different brands (e.g. VALEO TY37 <-> ASVA TY37).
 *
 * @return array{linked:bool,id:int}
 */
function docpart_cross_pair_exists_with_brands(PDO $db_link, $article_a, $brand_a, $article_b, $brand_b)
{
	$article_a = docpart_normalize_article_for_price($article_a);
	$article_b = docpart_normalize_article_for_price($article_b);
	$brand_a = docpart_cross_prepare_brand_name($brand_a);
	$brand_b = docpart_cross_prepare_brand_name($brand_b);
	if ($article_a === '' || $article_b === '' || $brand_a === '' || $brand_b === '') {
		return array('linked' => false, 'id' => 0);
	}
	if ($article_a === $article_b && $brand_a === $brand_b) {
		return array('linked' => false, 'id' => 0);
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	try {
		$exists_query = $db_link->prepare(
			'SELECT `id` FROM `shop_docpart_articles_analogs_list` '
			. 'WHERE ('
			. $art_expr . ' = ? AND ' . $analog_expr . ' = ? '
			. 'AND UPPER(TRIM(`manufacturer_article`)) = ? AND UPPER(TRIM(`manufacturer_analog`)) = ?'
			. ') OR ('
			. $art_expr . ' = ? AND ' . $analog_expr . ' = ? '
			. 'AND UPPER(TRIM(`manufacturer_article`)) = ? AND UPPER(TRIM(`manufacturer_analog`)) = ?'
			. ') LIMIT 1'
		);
		$exists_query->execute(array(
			$article_a, $article_b, $brand_a, $brand_b,
			$article_b, $article_a, $brand_b, $brand_a,
		));
		$row = $exists_query->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return array('linked' => true, 'id' => (int) $row['id']);
		}
	} catch (Exception $e) {
		return array('linked' => false, 'id' => 0);
	}
	return array('linked' => false, 'id' => 0);
}

function docpart_cross_persist_interchange_pair($db_link, $article, $manufacturer_article, $analog, $manufacturer_analog)
{
	$article = trim((string) $article);
	$analog = trim((string) $analog);
	$manufacturer_article = docpart_cross_prepare_brand_name($manufacturer_article);
	$manufacturer_analog = docpart_cross_prepare_brand_name($manufacturer_analog);
	if ($article === '' || $analog === '') {
		return false;
	}
	if ($manufacturer_article === '') {
		$manufacturer_article = docpart_cross_resolve_brand_for_article($db_link, $article, array(
			'partner_brand' => $manufacturer_analog,
		));
	}
	if ($manufacturer_analog === '') {
		$manufacturer_analog = docpart_cross_resolve_brand_for_article($db_link, $analog, array(
			'partner_brand' => $manufacturer_article,
			'fallback_brand' => $manufacturer_article,
		));
	}
	if ($manufacturer_article === '' || $manufacturer_analog === '') {
		return false;
	}
	$a_norm = docpart_normalize_article_for_price($article);
	$an_norm = docpart_normalize_article_for_price($analog);
	if ($a_norm === '' || $an_norm === '') {
		return false;
	}
	if ($a_norm === $an_norm && $manufacturer_article === $manufacturer_analog) {
		return false;
	}
	if (!empty(docpart_cross_pair_exists_with_brands($db_link, $article, $manufacturer_article, $analog, $manufacturer_analog)['linked'])) {
		return false;
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	try {
		$insert_query = $db_link->prepare(
			'INSERT INTO `shop_docpart_articles_analogs_list` '
			. '(`article`, `manufacturer_article`, `analog`, `manufacturer_analog`) VALUES (?,?,?,?)'
		);
		$insert_query->execute(array($article, $manufacturer_article, $analog, $manufacturer_analog));
		return true;
	} catch (Exception $e) {
		return false;
	}
}

/**
 * Store both directions so a customer can open either cross number and see the same interchange cluster.
 */
function docpart_cross_persist_interchange_pair_bidirectional($db_link, $article, $manufacturer_article, $analog, $manufacturer_analog)
{
	$inserted = 0;
	if (docpart_cross_persist_interchange_pair($db_link, $article, $manufacturer_article, $analog, $manufacturer_analog)) {
		$inserted++;
	}
	if (docpart_cross_persist_interchange_pair($db_link, $analog, $manufacturer_analog, $article, $manufacturer_article)) {
		$inserted++;
	}
	return $inserted;
}
