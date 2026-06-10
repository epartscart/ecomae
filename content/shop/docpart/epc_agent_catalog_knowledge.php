<?php
/**
 * AI agent catalog knowledge — passenger / commercial / motorbike makes
 * and in-stock parts brands from live DB (Epart catalog + UAE price lists).
 */
defined('_ASTEXE_') or define('_ASTEXE_', 1);

require_once __DIR__ . '/epc_stock_brands_helpers.php';
require_once __DIR__ . '/docpart_genuine_manufacturers.php';

function epc_agent_catalog_cache_path(): string
{
	return __DIR__ . '/cache/epc_agent_catalog_index.json';
}

function epc_agent_catalog_norm_key(string $name): string
{
	$key = mb_strtoupper(trim($name), 'UTF-8');
	return preg_replace('/[^A-Z0-9А-ЯЁ]+/u', '', $key);
}

function epc_agent_catalog_match_pattern(string $name): string
{
	$escaped = preg_quote(mb_strtoupper(trim($name), 'UTF-8'), '/');
	$escaped = str_replace(array('\ ', '\-', '\.'), array('\s+', '[-\s]?', '[\.\s]?'), $escaped);
	return '/(?:^|[^A-Z0-9])(' . $escaped . ')(?:[^A-Z0-9]|$)/i';
}

/**
 * @return array<string, mixed>
 */
function epc_agent_catalog_build_index(PDO $db): array
{
	$stock = array();
	$stock_by_key = array();
	$price_ids = epc_stock_brand_price_ids_with_stock($db);
	foreach (epc_stock_brands_with_counts($db, $price_ids) as $row) {
		$name = trim((string)($row['name'] ?? ''));
		if ($name === '') {
			continue;
		}
		$key = epc_agent_catalog_norm_key($name);
		if ($key === '') {
			continue;
		}
		$entry = array(
			'name' => $name,
			'key' => $key,
			'parts_count' => (int)($row['parts_count'] ?? 0),
			'len' => mb_strlen($name, 'UTF-8'),
		);
		$stock[] = $entry;
		$stock_by_key[$key] = $entry;
	}
	usort($stock, function ($a, $b) {
		return ($b['len'] <=> $a['len']) ?: strcasecmp($a['name'], $b['name']);
	});

	$vehicles = array();
	$vehicle_by_key = array();
	$section_counts = array('passenger' => 0, 'commercial' => 0, 'motorbike' => 0);
	try {
		$stmt = $db->query(
			"SELECT `section`, `mfa_id`, `manufacturer`, `type`, `country`
			 FROM `epc_umapi_manufacturers`
			 WHERE `section` IN ('passenger','commercial','motorbike')
			 AND TRIM(`manufacturer`) <> ''
			 ORDER BY `manufacturer` ASC"
		);
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$name = trim((string)($row['manufacturer'] ?? ''));
			$section = (string)($row['section'] ?? '');
			if ($name === '' || !isset($section_counts[$section])) {
				continue;
			}
			$key = epc_agent_catalog_norm_key($name);
			if ($key === '') {
				continue;
			}
			if (!isset($vehicle_by_key[$key])) {
				$vehicle_by_key[$key] = array(
					'name' => $name,
					'key' => $key,
					'sections' => array(),
					'mfa_ids' => array(),
					'type' => trim((string)($row['type'] ?? '')),
					'country' => trim((string)($row['country'] ?? '')),
					'len' => mb_strlen($name, 'UTF-8'),
				);
			}
			if (!in_array($section, $vehicle_by_key[$key]['sections'], true)) {
				$vehicle_by_key[$key]['sections'][] = $section;
				$section_counts[$section]++;
			}
			$vehicle_by_key[$key]['mfa_ids'][$section] = (int)($row['mfa_id'] ?? 0);
		}
		$vehicles = array_values($vehicle_by_key);
		usort($vehicles, function ($a, $b) {
			return ($b['len'] <=> $a['len']) ?: strcasecmp($a['name'], $b['name']);
		});
	} catch (Exception $e) {
	}

	return array(
		'stock' => $stock,
		'stock_by_key' => $stock_by_key,
		'vehicles' => $vehicles,
		'vehicle_by_key' => $vehicle_by_key,
		'section_counts' => $section_counts,
		'updated_at' => time(),
	);
}

/**
 * @return array<string, mixed>
 */
function epc_agent_catalog_index(PDO $db): array
{
	static $mem = null;
	static $mem_at = 0;
	if (is_array($mem) && (time() - $mem_at) < 300) {
		return $mem;
	}

	$path = epc_agent_catalog_cache_path();
	$ttl = 21600;
	if (is_file($path)) {
		$raw = @file_get_contents($path);
		if ($raw !== false && $raw !== '') {
			$data = json_decode($raw, true);
			if (is_array($data) && !empty($data['updated_at']) && (time() - (int)$data['updated_at']) < $ttl) {
				$mem = $data;
				$mem_at = time();
				return $data;
			}
		}
	}

	$data = epc_agent_catalog_build_index($db);
	$dir = dirname($path);
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	@file_put_contents($path, json_encode($data, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), LOCK_EX);
	$mem = $data;
	$mem_at = time();
	return $data;
}

function epc_agent_catalog_section_hint(string $text): string
{
	$lower = mb_strtolower($text, 'UTF-8');
	if (preg_match('/\b(commercial|truck|trucks|lcv|cv\b|bus|buses|fleet|hino|isuzu npr|fuso|scania|man truck|daf truck)\b/i', $lower)) {
		return 'commercial';
	}
	if (preg_match('/\b(motorcycle|motorbike|motorbikes|bike|bikes|scooter|scooters|atv|dirt bike|yamaha r1|honda cb)\b/i', $lower)) {
		return 'motorbike';
	}
	if (preg_match('/\b(passenger|car|cars|sedan|suv|hatchback|saloon)\b/i', $lower)) {
		return 'passenger';
	}
	return '';
}

function epc_agent_catalog_section_label(string $section): string
{
	$map = array(
		'passenger' => 'Passenger cars',
		'commercial' => 'Commercial vehicles',
		'motorbike' => 'Motorcycles',
	);
	return isset($map[$section]) ? $map[$section] : ucfirst($section);
}

function epc_agent_catalog_match_stock(array $index, string $text): ?array
{
	$upper = mb_strtoupper(trim($text), 'UTF-8');
	foreach ($index['stock'] as $row) {
		if (preg_match(epc_agent_catalog_match_pattern($row['name']), $upper)) {
			return $row;
		}
	}
	return null;
}

function epc_agent_catalog_match_vehicle(array $index, string $text, string $section_hint = ''): ?array
{
	$upper = mb_strtoupper(trim($text), 'UTF-8');
	foreach ($index['vehicles'] as $row) {
		if (!preg_match(epc_agent_catalog_match_pattern($row['name']), $upper)) {
			continue;
		}
		if ($section_hint !== '' && !in_array($section_hint, $row['sections'], true)) {
			continue;
		}
		return $row;
	}
	if ($section_hint !== '') {
		return epc_agent_catalog_match_vehicle($index, $text, '');
	}
	return null;
}

/**
 * @return array<string, mixed>|null
 */
function epc_agent_catalog_match_model(PDO $db, array $vehicle, string $text): ?array
{
	if (empty($vehicle['mfa_ids']) || !is_array($vehicle['mfa_ids'])) {
		return null;
	}
	$upper = mb_strtoupper(trim($text), 'UTF-8');
	$best = null;
	foreach ($vehicle['mfa_ids'] as $section => $mfa_id) {
		if ($mfa_id <= 0) {
			continue;
		}
		try {
			$stmt = $db->prepare(
				'SELECT `model_series`, `ms_id`
				 FROM `epc_umapi_models`
				 WHERE `section` = ? AND `mfa_id` = ?
				 ORDER BY CHAR_LENGTH(`model_series`) DESC
				 LIMIT 400'
			);
			$stmt->execute(array($section, $mfa_id));
			while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
				$model = trim((string)($row['model_series'] ?? ''));
				if ($model === '') {
					continue;
				}
				if (!preg_match(epc_agent_catalog_match_pattern($model), $upper)) {
					continue;
				}
				$len = mb_strlen($model, 'UTF-8');
				if ($best === null || $len > $best['len']) {
					$best = array(
						'name' => $model,
						'ms_id' => (int)($row['ms_id'] ?? 0),
						'section' => $section,
						'mfa_id' => $mfa_id,
						'len' => $len,
					);
				}
			}
		} catch (Exception $e) {
		}
	}
	return $best;
}

function epc_agent_catalog_has_inquiry_intent(string $text): bool
{
	return (bool)preg_match(
		'/\b(do you have|have you got|do you carry|do you sell|any|got|stock|available|availability|parts?|spares?|components?|brand|brands|manufacturer|maker|make|makes|vehicle|vehicles|catalog|catalogue|support|carry|sell|list|show|what|which|browse|model|models|commercial|passenger|motorcycle|motorbike|truck|bike)\b/i',
		$text
	);
}

function epc_agent_catalog_is_list_query(string $text): bool
{
	$lower = mb_strtolower(trim($text), 'UTF-8');
	if (!preg_match('/\b(brand|brands|make|makes|manufacturer|manufacturers|vehicle|vehicles|catalog|catalogue|list|what|which|how many|show|available)\b/i', $lower)) {
		return false;
	}
	return (bool)preg_match(
		'/\b(what|which|how many|list|show|all|available|do you have|passenger|commercial|motorcycle|motorbike|truck|car|cars|brand|brands|make|makes|vehicle|vehicles|catalog)\b/i',
		$lower
	);
}

/**
 * Resolve a customer question to catalog entities.
 *
 * @return array<string, mixed>|null
 */
function epc_agent_catalog_resolve_query(PDO $db, string $text): ?array
{
	$text = trim($text);
	if ($text === '') {
		return null;
	}
	if (preg_match('/\b(vin|part number|article)\b/i', $text) && preg_match('/\b([A-HJ-NPR-Z0-9]{11,17})\b/i', strtoupper($text))) {
		return null;
	}

	$index = epc_agent_catalog_index($db);
	$section_hint = epc_agent_catalog_section_hint($text);
	$stock = epc_agent_catalog_match_stock($index, $text);
	$vehicle = epc_agent_catalog_match_vehicle($index, $text, $section_hint);
	$model = ($vehicle !== null) ? epc_agent_catalog_match_model($db, $vehicle, $text) : null;

	if ($stock === null && $vehicle === null && !epc_agent_catalog_is_list_query($text)) {
		return null;
	}
	if ($stock === null && $vehicle === null) {
		return array(
			'action' => 'list',
			'section_hint' => $section_hint,
			'index' => $index,
		);
	}

	$has_intent = epc_agent_catalog_has_inquiry_intent($text);
	$name_only = (bool)preg_match('/^[A-Za-z0-9][A-Za-z0-9\-\.\s]{1,40}$/u', $text);
	if (!$has_intent && !$name_only && $model === null) {
		return null;
	}

	return array(
		'action' => ($stock !== null && $vehicle !== null) ? 'both' : ($stock !== null ? 'stock_brand' : 'vehicle'),
		'stock' => $stock,
		'vehicle' => $vehicle,
		'model' => $model,
		'section_hint' => $section_hint,
		'index' => $index,
	);
}

function epc_agent_catalog_match_name_in_text(PDO $db, string $text, string $prefer = 'any'): string
{
	$index = epc_agent_catalog_index($db);
	$section_hint = epc_agent_catalog_section_hint($text);
	if ($prefer === 'stock' || $prefer === 'any') {
		$stock = epc_agent_catalog_match_stock($index, $text);
		if ($stock !== null) {
			return (string)$stock['name'];
		}
	}
	if ($prefer === 'vehicle' || $prefer === 'any') {
		$vehicle = epc_agent_catalog_match_vehicle($index, $text, $section_hint);
		if ($vehicle !== null) {
			return (string)$vehicle['name'];
		}
	}
	return '';
}
