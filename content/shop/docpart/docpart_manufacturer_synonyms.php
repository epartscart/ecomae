<?php
/**
 * Manufacturer synonym groups from shop_docpart_manufacturers_synonyms (CP).
 * Treats canonical names and synonyms as the same brand for matching.
 */

function docpart_synonym_normalize_brand($brand)
{
	$brand = trim(str_replace('"', "'", (string)$brand));
	if ($brand === '') {
		return '';
	}
	return mb_strtoupper($brand, 'UTF-8');
}

/**
 * @return array<string, array<int, string>> Map: UPPER brand/synonym => list of equivalent UPPER names
 */
function docpart_load_manufacturer_synonym_map($db_link)
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}

	$groups_by_id = array();

	try {
		$mfr_query = $db_link->query('SELECT `id`, `name` FROM `shop_docpart_manufacturers`;');
		while ($mfr_row = $mfr_query->fetch(PDO::FETCH_ASSOC)) {
			$id = (int)$mfr_row['id'];
			$name = docpart_synonym_normalize_brand(isset($mfr_row['name']) ? $mfr_row['name'] : '');
			if ($name === '') {
				continue;
			}
			if (!isset($groups_by_id[$id])) {
				$groups_by_id[$id] = array();
			}
			$groups_by_id[$id][$name] = true;
		}

		$syn_query = $db_link->query('SELECT `manufacturer_id`, `synonym` FROM `shop_docpart_manufacturers_synonyms`;');
		while ($syn_row = $syn_query->fetch(PDO::FETCH_ASSOC)) {
			$id = (int)$syn_row['manufacturer_id'];
			$synonym = docpart_synonym_normalize_brand(isset($syn_row['synonym']) ? $syn_row['synonym'] : '');
			if ($synonym === '') {
				continue;
			}
			if (!isset($groups_by_id[$id])) {
				$groups_by_id[$id] = array();
			}
			$groups_by_id[$id][$synonym] = true;
		}
	} catch (Exception $e) {
		$cache = array();
		return $cache;
	}

	$brand_to_names = array();
	foreach ($groups_by_id as $names_set) {
		$names = array_keys($names_set);
		foreach ($names as $name) {
			$brand_to_names[$name] = $names;
		}
	}

	$cache = $brand_to_names;
	return $cache;
}

/**
 * @return array<int, string>
 */
function docpart_synonym_names_for_brand($brand, $synonym_map)
{
	$normalized = docpart_synonym_normalize_brand($brand);
	if ($normalized === '') {
		return array('');
	}
	if (isset($synonym_map[$normalized]) && is_array($synonym_map[$normalized])) {
		return array_values(array_unique($synonym_map[$normalized]));
	}
	return array($normalized);
}

function docpart_synonym_brands_equivalent($left, $right, $synonym_map)
{
	$left = docpart_synonym_normalize_brand($left);
	$right = docpart_synonym_normalize_brand($right);
	if ($left === '' || $right === '') {
		return $left === $right;
	}
	if ($left === $right) {
		return true;
	}
	$left_names = isset($synonym_map[$left]) ? $synonym_map[$left] : array($left);
	return in_array($right, $left_names, true);
}

/**
 * @return array<string, string> Map: UPPER brand/synonym => canonical UPPER name from shop_docpart_manufacturers.name
 */
function docpart_load_manufacturer_canonical_map($db_link)
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}

	$id_to_canonical = array();
	$brand_to_canonical = array();

	try {
		$mfr_query = $db_link->query('SELECT `id`, `name` FROM `shop_docpart_manufacturers`;');
		while ($mfr_row = $mfr_query->fetch(PDO::FETCH_ASSOC)) {
			$id = (int)$mfr_row['id'];
			$canonical = docpart_synonym_normalize_brand(isset($mfr_row['name']) ? $mfr_row['name'] : '');
			if ($canonical === '') {
				continue;
			}
			$id_to_canonical[$id] = $canonical;
			$brand_to_canonical[$canonical] = $canonical;
		}

		$syn_query = $db_link->query('SELECT `manufacturer_id`, `synonym` FROM `shop_docpart_manufacturers_synonyms`;');
		while ($syn_row = $syn_query->fetch(PDO::FETCH_ASSOC)) {
			$id = (int)$syn_row['manufacturer_id'];
			$synonym = docpart_synonym_normalize_brand(isset($syn_row['synonym']) ? $syn_row['synonym'] : '');
			if ($synonym === '' || !isset($id_to_canonical[$id])) {
				continue;
			}
			$brand_to_canonical[$synonym] = $id_to_canonical[$id];
		}
	} catch (Exception $e) {
		$brand_to_canonical = array();
	}

	$cache = $brand_to_canonical;
	return $cache;
}

function docpart_synonym_canonical_brand($brand, $canonical_map)
{
	$normalized = docpart_synonym_normalize_brand($brand);
	if ($normalized === '') {
		return '';
	}
	if (isset($canonical_map[$normalized])) {
		return $canonical_map[$normalized];
	}
	return $normalized;
}
