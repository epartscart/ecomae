<?php
/**
 * Genuine (OE) vs aftermarket classification for part search.
 * Genuine = manufacturers in UMAPI Epart Catalog sections: passenger, commercial, motorbike.
 */

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_genuine_cache_path()
{
	return dirname(__FILE__) . '/cache/epc_genuine_manufacturers.json';
}

function epc_genuine_site_base_url($DP_Config)
{
	if (!is_object($DP_Config) || empty($DP_Config->domain_path)) {
		$scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
		$host = isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : 'localhost';
		return $scheme . '://' . $host;
	}
	return rtrim((string)$DP_Config->domain_path, '/');
}

function epc_genuine_sync_umapi_sections($base_url)
{
	$sections = array('passenger', 'commercial', 'motorbike');
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => 30,
			'ignore_errors' => true,
			'header' => "User-Agent: EPC-Genuine-Sync\r\n",
		),
	));
	foreach ($sections as $section) {
		$url = rtrim($base_url, '/') . '/api/umapi_proxy.php?action=manufacturers&section=' . rawurlencode($section) . '&language=en&region=WWW';
		@file_get_contents($url, false, $ctx);
	}
}

function epc_genuine_count_umapi_rows($db_link)
{
	try {
		$q = $db_link->query("SELECT COUNT(DISTINCT `manufacturer`) FROM `epc_umapi_manufacturers` WHERE `section` IN ('passenger','commercial','motorbike');");
		return (int)$q->fetchColumn();
	} catch (Exception $e) {
		return 0;
	}
}

function epc_genuine_section_counts($db_link)
{
	$counts = array('passenger' => 0, 'commercial' => 0, 'motorbike' => 0);
	try {
		$q = $db_link->query("SELECT `section`, COUNT(DISTINCT `manufacturer`) AS `cnt` FROM `epc_umapi_manufacturers` WHERE `section` IN ('passenger','commercial','motorbike') GROUP BY `section`;");
		while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
			$sec = isset($row['section']) ? (string)$row['section'] : '';
			if (isset($counts[$sec])) {
				$counts[$sec] = (int)$row['cnt'];
			}
		}
	} catch (Exception $e) {
	}
	return $counts;
}

function epc_genuine_load_manufacturer_names($db_link)
{
	$names = array();
	try {
		$stmt = $db_link->query("SELECT DISTINCT `manufacturer` FROM `epc_umapi_manufacturers` WHERE `section` IN ('passenger','commercial','motorbike') AND `manufacturer` <> '' ORDER BY `manufacturer`;");
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$name = trim((string)$row['manufacturer']);
			if ($name !== '') {
				$names[] = $name;
			}
		}
	} catch (Exception $e) {
	}
	return $names;
}

function epc_genuine_read_cache()
{
	$path = epc_genuine_cache_path();
	if (!is_file($path)) {
		return null;
	}
	$raw = @file_get_contents($path);
	if ($raw === false || $raw === '') {
		return null;
	}
	$data = json_decode($raw, true);
	return is_array($data) ? $data : null;
}

function epc_genuine_write_cache($payload)
{
	$path = epc_genuine_cache_path();
	$dir = dirname($path);
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	@file_put_contents($path, json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT), LOCK_EX);
}

/**
 * @param PDO $db_link
 * @param object|null $DP_Config
 * @param string $catalog_url
 * @return array{brands: array<string, bool>, meta: array<string, mixed>}
 */
function epc_genuine_build_frontend_index($db_link, $DP_Config, $catalog_url)
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_manufacturer_synonyms.php';

	$min_expected = 50;
	// Never sync UMAPI during storefront page render — remote sync under load causes 524s.
	// Cache miss / empty catalog: return stale cache or empty brands; cron/setup fills data.

	$cached = epc_genuine_read_cache();
	$cache_ttl = 86400;
	if (
		is_array($cached)
		&& !empty($cached['brands'])
		&& isset($cached['meta']['updated_at'])
		&& (time() - (int)$cached['meta']['updated_at']) < $cache_ttl
	) {
		if (!empty($catalog_url)) {
			$cached['meta']['catalog_url'] = $catalog_url;
		}
		return array(
			'brands' => $cached['brands'],
			'meta' => $cached['meta'],
		);
	}
	// Prefer any non-empty cache over rebuilding when load is high.
	if (is_array($cached) && !empty($cached['brands'])) {
		if (!empty($catalog_url)) {
			$cached['meta']['catalog_url'] = $catalog_url;
		}
		return array(
			'brands' => $cached['brands'],
			'meta' => isset($cached['meta']) && is_array($cached['meta']) ? $cached['meta'] : array(),
		);
	}
	if (function_exists('sys_getloadavg')) {
		$load = @sys_getloadavg();
		if (is_array($load) && isset($load[0]) && (float) $load[0] >= 6.0) {
			return array(
				'brands' => array(),
				'meta' => array(
					'source' => 'load_shed',
					'catalog_url' => $catalog_url,
					'updated_at' => time(),
				),
			);
		}
	}

	$synonym_map = docpart_load_manufacturer_synonym_map($db_link);
	$names = epc_genuine_load_manufacturer_names($db_link);
	$brands = array();

	foreach ($names as $name) {
		$equiv = docpart_synonym_names_for_brand($name, $synonym_map);
		foreach ($equiv as $equiv_name) {
			$key = docpart_synonym_normalize_brand($equiv_name);
			if ($key !== '') {
				$brands[$key] = true;
			}
		}
	}

	$section_counts = epc_genuine_section_counts($db_link);
	$meta = array(
		'source' => 'epart_catalog',
		'sections' => $section_counts,
		'total' => count($names),
		'keys' => count($brands),
		'catalog_url' => $catalog_url,
		'updated_at' => time(),
	);

	$payload = array('brands' => $brands, 'meta' => $meta);
	epc_genuine_write_cache($payload);

	return $payload;
}
