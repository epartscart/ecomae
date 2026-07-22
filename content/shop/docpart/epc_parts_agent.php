<?php
/**
 * AI Parts Expert — conversational agent (rule-based + live data tools).
 * Uses UAE stock, demand-country tags, VIN catalog, and cross references.
 */

defined('_ASTEXE_') or define('_ASTEXE_', 1);

require_once __DIR__ . '/docpart_article_match.php';
require_once __DIR__ . '/epc_demand_intelligence.php';
require_once __DIR__ . '/epc_stock_brands_helpers.php';
require_once __DIR__ . '/epc_agent_catalog_knowledge.php';
require_once __DIR__ . '/epc_storefront_storage_flags.php';
require_once __DIR__ . '/epc_storefront_prices_helpers.php';

/** Internal-only rules for availability replies (never shown to customers). */
function epc_agent_storefront_toggle_internal_prompt(): string
{
	return "When answering customers about parts availability/pricing:\n"
		. "- Only use storages enabled for storefront (storefront_temp_disabled = 0)\n"
		. "- NEVER mention temporary disable toggles, hidden warehouses, or CP admin controls\n"
		. "- NEVER say a price list was turned off by the shop\n"
		. "- Present unavailable items neutrally: \"We don't have this in stock right now\" or offer alternatives";
}

/**
 * Operator custom prompt + mandatory internal availability rules.
 */
function epc_agent_effective_system_prompt(array $config): string
{
	$custom = trim((string) ($config['system_prompt'] ?? ''));
	$internal = epc_agent_storefront_toggle_internal_prompt();
	$guestRules = epc_storefront_prices_agent_guest_rules();
	if ($guestRules !== '') {
		$internal .= "\n\n" . $guestRules;
	}
	if ($custom === '') {
		return $internal;
	}
	return $custom . "\n\n" . $internal;
}

function epc_agent_enabled($DP_Config): bool
{
	if (!is_object($DP_Config)) {
		return true;
	}
	if (isset($DP_Config->epc_parts_agent_enabled) && (string)$DP_Config->epc_parts_agent_enabled === '0') {
		return false;
	}
	return true;
}

function epc_agent_lang_href($DP_Config): string
{
	if (!empty($GLOBALS['multilang_params']['lang_href'])) {
		return rtrim((string)$GLOBALS['multilang_params']['lang_href'], '/');
	}
	return '/en';
}

function epc_agent_session_dir(): string
{
	$dir = rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'epc_parts_agent_sessions';
	if (!is_dir($dir)) {
		@mkdir($dir, 0700, true);
	}
	return $dir;
}

function epc_agent_session_path(string $session_id): string
{
	$safe = preg_replace('/[^a-zA-Z0-9_\-]/', '', $session_id);
	if ($safe === '' || strlen($safe) > 64) {
		return '';
	}
	return epc_agent_session_dir() . DIRECTORY_SEPARATOR . $safe . '.json';
}

/**
 * @return array{country_code:string, country_name:string, messages:array, created:int}
 */
function epc_agent_session_load(string $session_id): array
{
	$path = epc_agent_session_path($session_id);
	$default = array(
		'country_code' => '',
		'country_name' => '',
		'messages' => array(),
		'created' => time(),
	);
	if ($path === '' || !is_file($path)) {
		return $default;
	}
	$raw = @file_get_contents($path);
	if ($raw === false || $raw === '') {
		return $default;
	}
	$data = json_decode($raw, true);
	return is_array($data) ? array_merge($default, $data) : $default;
}

function epc_agent_session_save(string $session_id, array $session): void
{
	$path = epc_agent_session_path($session_id);
	if ($path === '') {
		return;
	}
	$session['updated'] = time();
	@file_put_contents($path, json_encode($session, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), LOCK_EX);
}

function epc_agent_country_aliases(): array
{
	return array(
		'sudan' => 'SDN', 'sudanese' => 'SDN',
		'algeria' => 'DZA', 'algerian' => 'DZA',
		'kenya' => 'KEN', 'kenyan' => 'KEN',
		'egypt' => 'EGY', 'egyptian' => 'EGY',
		'nigeria' => 'NGA', 'nigerian' => 'NGA',
		'saudi' => 'SAU', 'saudi arabia' => 'SAU', 'ksa' => 'SAU',
		'uae' => 'ARE', 'dubai' => 'ARE', 'emirates' => 'ARE',
		'usa' => 'USA', 'united states' => 'USA', 'america' => 'USA', 'us market' => 'USA',
		'canada' => 'CAN', 'canadian' => 'CAN',
	);
}

function epc_agent_detect_country(string $text): array
{
	$lower = mb_strtolower($text, 'UTF-8');
	$aliases = epc_agent_country_aliases();
	$best_code = '';
	$best_name = '';
	foreach ($aliases as $phrase => $code) {
		if (strpos($lower, $phrase) !== false) {
			$best_code = $code;
			$best_name = ucwords($phrase);
			break;
		}
	}
	if ($best_code === '') {
		return array('code' => '', 'name' => '');
	}
	$registry = epc_demand_country_registry();
	if (isset($registry[$best_code])) {
		return array('code' => $best_code, 'name' => (string)$registry[$best_code]['name']);
	}
	$extra = array('USA' => 'United States', 'CAN' => 'Canada');
	if (isset($extra[$best_code])) {
		return array('code' => $best_code, 'name' => $extra[$best_code]);
	}
	return array('code' => $best_code, 'name' => $best_name);
}

function epc_agent_extract_vin(string $text): string
{
	if (preg_match('/\b([A-HJ-NPR-Z0-9]{11,17})\b/i', strtoupper($text), $m)) {
		return strtoupper($m[1]);
	}
	return '';
}

function epc_agent_known_brands(): array
{
	return array(
		'TOYOTA', 'NISSAN', 'HONDA', 'MAZDA', 'MITSUBISHI', 'SUBARU', 'SUZUKI', 'LEXUS', 'INFINITI',
		'BMW', 'MERCEDES', 'MERCEDES-BENZ', 'AUDI', 'VOLKSWAGEN', 'VW', 'PORSCHE', 'MINI',
		'FORD', 'CHEVROLET', 'GMC', 'DODGE', 'JEEP', 'CHRYSLER',
		'HYUNDAI', 'KIA', 'GENUINE', 'NGK', 'DENSO', 'BOSCH', 'MANN', 'MAHLE', 'VALEO', 'SKF',
		'FEBI', 'TRW', 'TEXTAR', 'NIBK', 'AISIN', 'KOYO', 'NTN', 'NSK', 'GATES', 'DAYCO',
		'CONTINENTAL', 'DELPHI', 'MOBIL', 'CASTROL', 'ACDELCO', 'MOPAR',
		'555', 'GMB', 'NPW', 'FEBEST', 'CTR', 'KYB', 'MONROE', 'BREMBO', 'TOKICO', 'LUK', 'SACHS',
		'BENTLEY', 'ROLLS-ROYCE', 'ROLLS ROYCE', 'LAMBORGHINI', 'FERRARI', 'MASERATI', 'ASTON MARTIN',
		'JAGUAR', 'LAND ROVER', 'RANGE ROVER', 'VOLVO', 'SAAB', 'PEUGEOT', 'CITROEN', 'RENAULT',
		'FIAT', 'ALFA ROMEO', 'OPEL', 'VAUXHALL', 'SEAT', 'SKODA', 'DACIA', 'ISUZU', 'HINO', 'UD',
		'DAIHATSU', 'SSANGYONG', 'GEELY', 'BYD', 'CHERY', 'GREAT WALL', 'MG', 'PROTON', 'PERODUA',
	);
}

function epc_agent_manufacturer_stopwords(): array
{
	return array(
		'any', 'some', 'good', 'the', 'a', 'an', 'do', 'you', 'have', 'got', 'for', 'in', 'stock',
		'available', 'availability', 'sell', 'carry', 'specific', 'certain', 'all', 'your', 'our',
		'what', 'which', 'about', 'please', 'need', 'want', 'are', 'there', 'is', 'it', 'that', 'this',
		'brand', 'brands', 'manufacturer', 'maker', 'marque',
	);
}

function epc_agent_clean_manufacturer_candidate(string $raw): string
{
	$raw = trim(preg_replace('/\s+/', ' ', $raw));
	if ($raw === '') {
		return '';
	}
	$words = preg_split('/\s+/', $raw);
	$keep = array();
	foreach ($words as $word) {
		if ($word === '' || in_array(mb_strtolower($word, 'UTF-8'), epc_agent_manufacturer_stopwords(), true)) {
			continue;
		}
		$keep[] = $word;
	}
	return trim(implode(' ', $keep));
}

function epc_agent_match_known_manufacturer(string $text): string
{
	global $db_link;
	$from_catalog = '';
	if (isset($db_link) && $db_link instanceof PDO) {
		$from_catalog = epc_agent_catalog_match_name_in_text($db_link, $text, 'any');
		if ($from_catalog !== '') {
			return mb_strtoupper($from_catalog, 'UTF-8');
		}
	}

	$upper = mb_strtoupper(trim($text), 'UTF-8');
	$brands = epc_agent_known_brands();
	usort($brands, function ($a, $b) {
		return strlen($b) <=> strlen($a);
	});
	foreach ($brands as $brand) {
		if (preg_match('/\b' . preg_quote($brand, '/') . '\b/i', $upper)) {
			return $brand === 'VW' ? 'VOLKSWAGEN' : ($brand === 'MERCEDES' ? 'MERCEDES-BENZ' : $brand);
		}
	}
	return '';
}

function epc_agent_extract_manufacturer_parts_query(string $text): string
{
	$lower = mb_strtolower(trim($text), 'UTF-8');
	$has_stock_intent = (bool)preg_match(
		'/\b(do you have|have you got|do you carry|do you sell|any|got|stock|available|availability|parts|spares|components|part numbers|brand|brands)\b/i',
		$lower
	);
	if (!$has_stock_intent) {
		return '';
	}

	$candidates = array();
	if (preg_match('/\b([a-z0-9][a-z0-9\-\.\/]{1,40}?)\s+brand(?:\s+(?:available|availability|in\s+stock|parts?|spares?))?\b/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}
	if (preg_match('/\bbrand\s*:?\s*([a-z0-9][a-z0-9\-\.\/]{1,40})\b/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}
	if (preg_match('/\b(?:do you have|have you got|do you carry|do you sell|got|any|is|are)\s+(?:any\s+)?([a-z0-9][a-z0-9\-\.\/]{1,40}?)\s+(?:available|availability|in\s+stock)\b/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}
	if (preg_match('/\b(?:do you have|have you got|do you carry|do you sell|got|any)\s+(?:any\s+)?([a-z0-9][a-z0-9\-\s]{1,40}?)\s+(?:parts|spares|components|products|part numbers)\b/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}
	if (preg_match('/\b(?:parts|spares|components|products|part numbers)\s+(?:for\s+)?([a-z0-9][a-z0-9\-\s]{1,40}?)\s*\??\s*$/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}
	if (preg_match('/\b([a-z0-9][a-z0-9\-\s]{1,40}?)\s+(?:parts|spares|components|products|part numbers)\b/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}
	if (preg_match('/\b(?:do you have|have you got|do you carry|do you sell|any)\s+(?:any\s+)?([a-z0-9][a-z0-9\-\s]{1,40}?)\s*\??\s*$/i', $text, $m)) {
		$candidates[] = epc_agent_clean_manufacturer_candidate($m[1]);
	}

	$known = epc_agent_match_known_manufacturer($text);
	if ($known !== '') {
		$candidates[] = $known;
	}

	foreach ($candidates as $candidate) {
		if ($candidate !== '' && mb_strlen($candidate, 'UTF-8') >= 2) {
			return $candidate;
		}
	}
	return '';
}

function epc_agent_manufacturer_browse_url($DP_Config, string $manufacturer): string
{
	$lang = epc_agent_lang_href($DP_Config);
	$parts_seg = 'parts';
	$slash = '---';
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['level_1']['url'])) {
		$parts_seg = (string)$DP_Config->chpu_search_config['level_1']['url'];
	}
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config['slash_code'])) {
		$slash = (string)$DP_Config->chpu_search_config['slash_code'];
	}
	$mfr = str_replace('/', $slash, trim($manufacturer));
	return $lang . '/' . $parts_seg . '/' . rawurlencode($mfr);
}

function epc_agent_find_manufacturer_in_db(PDO $db, string $query): string
{
	$query = trim($query);
	if ($query === '') {
		return '';
	}
	$rows = epc_stock_brand_parts_for_manufacturer($db, $query, 1);
	if (!empty($rows[0]['manufacturer'])) {
		return trim((string)$rows[0]['manufacturer']);
	}
	list($upper,) = epc_stock_brand_match_params($query);
	if ($upper === '') {
		return '';
	}
	try {
		$stmt = $db->prepare(
			'SELECT MIN(TRIM(`manufacturer`)) AS `name`, COUNT(*) AS `cnt`
			 FROM `shop_docpart_prices_data`
			 WHERE UPPER(TRIM(`manufacturer`)) LIKE ?
			 AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
			 AND ' . epc_ssf_price_data_active_sql() . '
			 GROUP BY UPPER(TRIM(`manufacturer`))
			 ORDER BY `cnt` DESC
			 LIMIT 1'
		);
		$stmt->execute(array($upper . '%'));
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if ($row && !empty($row['name'])) {
			return trim((string)$row['name']);
		}
	} catch (Exception $e) {
	}
	return '';
}

/**
 * @return array{text:string, links?:array, suggestions?:array}
 */
function epc_agent_reply_manufacturer_stock(PDO $db, $DP_Config, string $manufacturer_query): array
{
	global $db_link;
	if (!isset($db_link) || !$db_link) {
		$db_link = $db;
	}

	$resolved = epc_agent_find_manufacturer_in_db($db, $manufacturer_query);
	$lang = epc_agent_lang_href($DP_Config);
	$parts_index = $lang . '/' . (!empty($DP_Config->chpu_search_config['level_1']['url']) ? $DP_Config->chpu_search_config['level_1']['url'] : 'parts');

	if ($resolved === '') {
		return array(
			'text' => "I couldn't find **" . trim($manufacturer_query) . "** in our current UAE in-stock price lists.\n\n"
				. "We stock **many manufacturers** — browse the full list or send a **part number / VIN** and I'll check availability."
				. epc_agent_contact_text_block($DP_Config),
			'links' => array_merge(
				array(array('label' => 'All brands in stock', 'url' => $parts_index)),
				epc_agent_standard_contact_links($DP_Config, $lang)
			),
			'suggestions' => array('Do you have BMW parts', 'NGK 4195', 'Decode VIN'),
		);
	}

	$rows = epc_stock_brand_parts_for_manufacturer($db, $resolved, 5000);
	$part_count = count($rows);
	$total_qty = 0;
	foreach ($rows as $row) {
		$total_qty += (float)($row['exist'] ?? 0);
	}

	$brand_url = epc_agent_manufacturer_browse_url($DP_Config, $resolved);
	$lines = array();
	$samples = array_slice($rows, 0, 6);
	usort($samples, function ($a, $b) {
		return (float)($b['exist'] ?? 0) <=> (float)($a['exist'] ?? 0);
	});
	$showPrices = epc_storefront_prices_visible_for_user();
	$mask = function_exists('epc_storefront_sensitive_mask') ? epc_storefront_sensitive_mask() : '**';
	foreach ($samples as $row) {
		$art = !empty($row['article_show']) ? (string)$row['article_show'] : (string)($row['article'] ?? '');
		$name = trim((string)($row['name'] ?? ''));
		$qty = (int)round((float)($row['exist'] ?? 0));
		$price = trim((string)($row['price'] ?? ''));
		$name_bit = $name !== '' ? ' — ' . $name : '';
		if ($showPrices) {
			$price_bit = ($price !== '') ? ' @ ' . $price : '';
			$lines[] = '• **' . $resolved . ' ' . $art . '**' . $name_bit . ' — ' . number_format($qty) . ' pcs' . $price_bit;
		} else {
			$lines[] = '• **' . $resolved . ' ' . $art . '**' . $name_bit . ' — ' . $mask;
		}
	}

	$pricingNote = $showPrices
		? 'search all part numbers, fitment, and pricing.'
		: 'search all part numbers and fitment — **log in to see prices, stock, terms, and warehouses**.';

	$qtyLine = $showPrices
		? ("• **" . number_format($total_qty) . " pcs** total quantity\n\n")
		: ("• Availability qty: **" . $mask . "**\n\n");

	return array(
		'text' => "**Yes — we stock " . $resolved . " parts** in UAE warehouses.\n\n"
			. "• **" . number_format($part_count) . " part numbers** in stock\n"
			. $qtyLine
			. "**Sample lines:**\n" . implode("\n", $lines)
			. "\n\nOpen the **" . $resolved . " brand catalog** to " . $pricingNote,
		'links' => array(
			array('label' => 'Browse all ' . $resolved . ' parts', 'url' => $brand_url),
			array('label' => 'All brands in stock', 'url' => $parts_index),
		),
		'suggestions' => array($resolved . ' part number search', 'Decode VIN', 'WhatsApp contact'),
	);
}

function epc_agent_known_models(): array
{
	return array(
		'CIVIC', 'ACCORD', 'CRV', 'CR-V', 'FIT', 'CITY', 'JAZZ', 'PILOT', 'ODYSSEY', 'HR-V', 'HRV',
		'CAMRY', 'COROLLA', 'HILUX', 'LAND CRUISER', 'RAV4', 'YARIS', 'FORTUNER', 'PRADO', 'HIACE',
		'ALTIMA', 'SENTRA', 'PATROL', 'SUNNY', 'X-TRAIL', 'NAVARA',
		'LANCER', 'PAJERO', 'OUTLANDER', 'L200',
		'FOCUS', 'FIESTA', 'RANGER', 'EXPLORER', 'MUSTANG',
		'SONATA', 'ELANTRA', 'TUCSON', 'SANTA FE',
		'SPORTAGE', 'SORENTO', 'CERATO',
		'3 SERIES', '5 SERIES', 'X5', 'X3',
		'C CLASS', 'E CLASS', 'GLC', 'GLE',
	);
}

function epc_agent_part_type_keywords(): array
{
	return array(
		'piston', 'oil filter', 'air filter', 'cabin filter', 'fuel filter', 'brake pad', 'brake disc',
		'brake rotor', 'timing belt', 'timing chain', 'spark plug', 'glow plug', 'gasket', 'head gasket',
		'water pump', 'oil pump', 'clutch', 'radiator', 'thermostat', 'shock absorber', 'strut',
		'control arm', 'tie rod', 'ball joint', 'wheel bearing', 'alternator', 'starter motor', 'starter',
		'fuel pump', 'injector', 'turbo', 'turbocharger', 'ring set', 'conrod', 'connecting rod',
		'engine mount', 'cv joint', 'drive shaft', 'wiper blade', 'serpentine belt', 'fan belt',
		'ignition coil', 'lambda sensor', 'oxygen sensor', 'abs sensor', 'wheel hub', 'master cylinder',
	);
}

function epc_agent_stopwords(): array
{
	return array(
		'FROM', 'PART', 'NUMBER', 'VIN', 'HELP', 'STOCK', 'PRICE', 'USA', 'CAN', 'UAE', 'THE', 'AND', 'FOR',
		'YOU', 'YOUR', 'HAVE', 'HAS', 'WANT', 'NEED', 'LOOKING', 'SEARCH', 'FIND', 'MODEL', 'YEAR', 'TYPE',
		'DO', 'ANY', 'ARE', 'IS', 'AM', 'I', 'A', 'AN', 'MY', 'ME', 'WE', 'PLEASE', 'CAN', 'COULD', 'WOULD',
		'WHAT', 'WHICH', 'WHERE', 'WHEN', 'HOW', 'ABOUT', 'WITH', 'THIS', 'THAT', 'THESE', 'THOSE', 'GET',
		'BUY', 'ORDER', 'AVAILABLE', 'AVAILABILITY', 'SHOW', 'GIVE', 'TELL', 'SOME', 'ANY', 'ALL',
		'BRAND', 'BRANDS', 'MANUFACTURER', 'MANUFACTURERS', 'MAKER', 'MAKERS', 'MARQUE',
	);
}

/**
 * When a short numeric token is really a manufacturer name (e.g. "555"), not a part number.
 */
function epc_agent_try_manufacturer_instead_of_article(PDO $db, string $message, string $article, string $brand): string
{
	if ($brand !== '' || $article === '') {
		return '';
	}
	$upper = mb_strtoupper(trim($message), 'UTF-8');
	$article_u = mb_strtoupper(trim($article), 'UTF-8');
	if (preg_match('/\b' . preg_quote($article_u, '/') . '\s+brand\b/i', $upper)) {
		return $article_u;
	}
	if (preg_match('/\bbrand\s*:?\s*' . preg_quote($article_u, '/') . '\b/i', $upper)) {
		return $article_u;
	}
	if (epc_agent_match_known_manufacturer($article_u) !== '') {
		return $article_u;
	}
	if (preg_match('/^\d{2,5}$/', $article_u)
		&& preg_match('/\b(available|availability|stock|parts?|spares?|carry|have|brand)\b/i', $upper)
		&& epc_agent_find_manufacturer_in_db($db, $article_u) !== '') {
		return $article_u;
	}
	return '';
}

function epc_agent_is_plausible_article(string $token): bool
{
	$token = mb_strtoupper(trim($token), 'UTF-8');
	$token = preg_replace('/\.+$/', '', $token);
	if ($token === '' || strlen($token) < 3) {
		return false;
	}
	if (in_array($token, epc_agent_stopwords(), true)) {
		return false;
	}
	if (preg_match('/^(19|20)\d{2}$/', $token)) {
		return false;
	}
	foreach (epc_agent_known_models() as $model) {
		if ($token === mb_strtoupper(str_replace('-', '', $model), 'UTF-8')) {
			return false;
		}
	}
	foreach (epc_agent_part_type_keywords() as $kw) {
		$kw_tok = mb_strtoupper(str_replace(' ', '', $kw), 'UTF-8');
		if ($token === $kw_tok || strpos($token, $kw_tok) === 0) {
			return false;
		}
	}
	// Real part numbers almost always contain at least one digit.
	if (!preg_match('/\d/', $token)) {
		return false;
	}
	if (preg_match('/\d/', $token)) {
		return true;
	}
	return false;
}

function epc_agent_extract_part_type(string $text): string
{
	$lower = mb_strtolower($text, 'UTF-8');
	$best = '';
	$best_len = 0;
	foreach (epc_agent_part_type_keywords() as $kw) {
		if (strpos($lower, $kw) !== false && strlen($kw) > $best_len) {
			$best = $kw;
			$best_len = strlen($kw);
		}
	}
	return $best;
}

/**
 * Customer asks which manufacturer/brand is good for a part category (not export country).
 */
function epc_agent_is_part_brand_question(string $text): bool
{
	$part_type = epc_agent_extract_part_type($text);
	if ($part_type === '') {
		return false;
	}
	if (epc_agent_detect_country($text)['code'] !== '') {
		return false;
	}
	$lower = mb_strtolower($text, 'UTF-8');
	return (bool)preg_match(
		'/\b(brand|brands|manufacturer|maker|good|quality|which|what|any|recommend|suggest|top|trusted|reliable)\b/i',
		$lower
	);
}

/**
 * @return array<int, array{brand:string, lines:int, total_qty:float, samples:array}>
 */
function epc_agent_aggregate_part_type_brands(PDO $db, $DP_Config, array $vq, int $limit = 8): array
{
	$search_vq = $vq;
	$search_vq['model'] = '';
	$hits = epc_agent_search_vehicle_part($db, $DP_Config, $search_vq, 100);
	$by_brand = array();
	foreach ($hits as $h) {
		$b = trim((string)($h['brand'] ?? ''));
		if ($b === '') {
			continue;
		}
		$key = mb_strtoupper($b, 'UTF-8');
		if (!isset($by_brand[$key])) {
			$by_brand[$key] = array(
				'brand' => $b,
				'lines' => 0,
				'total_qty' => 0,
				'samples' => array(),
			);
		}
		$by_brand[$key]['lines']++;
		$by_brand[$key]['total_qty'] += (float)($h['total_qty'] ?? 0);
		if (count($by_brand[$key]['samples']) < 2) {
			$by_brand[$key]['samples'][] = $h;
		}
	}
	$rows = array_values($by_brand);
	usort($rows, function ($a, $b) {
		if ($b['total_qty'] !== $a['total_qty']) {
			return $b['total_qty'] <=> $a['total_qty'];
		}
		return $b['lines'] <=> $a['lines'];
	});
	if (count($rows) > $limit) {
		$rows = array_slice($rows, 0, $limit);
	}
	return $rows;
}

function epc_agent_premium_part_brands(string $part_type): array
{
	$common = array('GMB', 'NPW', 'AISIN', 'DENSO', 'NGK', 'BOSCH', 'MANN', 'MAHLE', 'GATES', 'DAYCO', 'SKF', 'FEBI', 'TRW', 'TEXTAR', 'NIBK', 'KOYO');
	if (strpos($part_type, 'pump') !== false) {
		return array('GMB', 'NPW', 'AISIN', 'DENSO', 'GATES');
	}
	if (strpos($part_type, 'filter') !== false) {
		return array('MANN', 'MAHLE', 'BOSCH', 'DENSO', 'NGK');
	}
	if (strpos($part_type, 'brake') !== false) {
		return array('TEXTAR', 'NIBK', 'TRW', 'FEBI', 'BOSCH');
	}
	if (strpos($part_type, 'belt') !== false || strpos($part_type, 'timing') !== false) {
		return array('GATES', 'DAYCO', 'AISIN', 'CONTINENTAL');
	}
	return $common;
}

function epc_agent_extract_year(string $text): int
{
	if (preg_match('/\b(19[89]\d|20[0-3]\d)\b/', $text, $m)) {
		return (int)$m[1];
	}
	return 0;
}

/**
 * @return array{is_query:bool, brand:string, model:string, year:int, part_type:string, label:string}
 */
function epc_agent_extract_vehicle_query(string $text): array
{
	global $db_link;
	$upper = mb_strtoupper(trim($text), 'UTF-8');
	$lower = mb_strtolower($text, 'UTF-8');
	$brand = '';
	$model = '';
	if (isset($db_link) && $db_link instanceof PDO) {
		$index = epc_agent_catalog_index($db_link);
		$section_hint = epc_agent_catalog_section_hint($text);
		$vehicle = epc_agent_catalog_match_vehicle($index, $text, $section_hint);
		if ($vehicle !== null) {
			$brand = (string)$vehicle['name'];
		}
		$model_row = ($vehicle !== null) ? epc_agent_catalog_match_model($db_link, $vehicle, $text) : null;
		if ($model_row !== null) {
			$model = mb_strtoupper(str_replace(' ', '-', trim((string)$model_row['name'])), 'UTF-8');
		}
	}
	if ($brand === '') {
		foreach (epc_agent_known_brands() as $b) {
			if (preg_match('/\b' . preg_quote($b, '/') . '\b/i', $upper)) {
				$brand = $b === 'VW' ? 'VOLKSWAGEN' : ($b === 'MERCEDES' ? 'MERCEDES-BENZ' : $b);
				break;
			}
		}
	}
	if ($model === '') {
		foreach (epc_agent_known_models() as $m) {
			$pattern = '/\b' . preg_quote(str_replace('-', '[- ]?', $m), '/') . '\b/i';
			if (preg_match($pattern, $upper)) {
				$model = mb_strtoupper(str_replace(' ', '-', trim($m)), 'UTF-8');
				break;
			}
		}
	}
	$year = epc_agent_extract_year($text);
	$part_type = epc_agent_extract_part_type($text);
	$looks_natural = (bool)preg_match(
		'/\b(i want|i need|looking for|do you have|have you got|any stock|available|in stock|any good|good brand|which brand|what brand)\b/i',
		$lower
	);
	$wants_brands = epc_agent_is_part_brand_question($text);
	$is_query = ($brand !== '' && $part_type !== '')
		|| ($brand !== '' && $model !== '' && ($part_type !== '' || $year > 0))
		|| ($looks_natural && $brand !== '' && ($model !== '' || $part_type !== ''))
		|| ($looks_natural && $part_type !== '')
		|| ($part_type !== '' && $wants_brands)
		|| ($part_type !== '' && (bool)preg_match('/\?\s*$/', trim($text)));
	$label_parts = array_filter(array(
		$brand,
		$model !== '' ? str_replace('-', ' ', $model) : '',
		$year > 0 ? (string)$year : '',
		$part_type !== '' ? $part_type : '',
	));
	return array(
		'is_query' => $is_query,
		'brand' => $brand,
		'model' => $model,
		'year' => $year,
		'part_type' => $part_type,
		'label' => implode(' ', $label_parts),
		'wants_brands' => $wants_brands,
	);
}

function epc_agent_extract_brand_article(string $text): array
{
	global $db_link;
	$upper = mb_strtoupper(trim($text), 'UTF-8');
	$brand = '';
	$article = '';
	if (isset($db_link) && $db_link instanceof PDO) {
		$stock = epc_agent_catalog_match_stock(epc_agent_catalog_index($db_link), $text);
		if ($stock !== null) {
			$brand = (string)$stock['name'];
		}
	}
	if ($brand === '') {
		foreach (epc_agent_known_brands() as $b) {
			$pattern = '/\b' . preg_quote($b, '/') . '\b/i';
			if (preg_match($pattern, $upper)) {
				$brand = $b === 'VW' ? 'VOLKSWAGEN' : ($b === 'MERCEDES' ? 'MERCEDES-BENZ' : $b);
				break;
			}
		}
	}
	if (preg_match_all('/\b([A-Z0-9][A-Z0-9\-\/\.]{2,20})\b/', $upper, $matches)) {
		foreach ($matches[1] as $token) {
			$token = preg_replace('/\.+$/', '', $token);
			if (strlen($token) >= 11 && strlen($token) <= 17 && !preg_match('/[IOQ]/', $token)) {
				continue;
			}
			if (!epc_agent_is_plausible_article($token)) {
				continue;
			}
			if ($brand !== '' && $token === str_replace('-', '', $brand)) {
				continue;
			}
			$article = $token;
			break;
		}
	}
	return array('brand' => $brand, 'article' => $article);
}

/**
 * Search UAE price list by vehicle context + part type (name/manufacturer).
 *
 * @return array<int, array{brand:string, article:string, name:string, lines:array, total_qty:float, url:string}>
 */
function epc_agent_search_vehicle_part(PDO $db, $DP_Config, array $vq, int $limit = 8): array
{
	$brand = trim((string)($vq['brand'] ?? ''));
	$model = trim((string)($vq['model'] ?? ''));
	$part_type = trim((string)($vq['part_type'] ?? ''));
	if ($part_type === '' && $brand === '') {
		return array();
	}

	$conditions = array('IFNULL(`price`, 0) > 0', 'IFNULL(`exist`, 0) > 0', epc_ssf_price_data_active_sql());
	$params = array();

	if ($part_type !== '') {
		$like_terms = array($part_type);
		if (substr($part_type, -1) !== 's') {
			$like_terms[] = $part_type . 's';
		}
		if ($part_type === 'piston') {
			$like_terms[] = 'piston ring';
			$like_terms[] = 'piston set';
		}
		$like_terms = array_values(array_unique($like_terms));
		$name_parts = array();
		foreach ($like_terms as $term) {
			$name_parts[] = 'LOWER(`name`) LIKE ?';
			$params[] = '%' . mb_strtolower($term, 'UTF-8') . '%';
		}
		$conditions[] = '(' . implode(' OR ', $name_parts) . ')';
	}
	if ($brand !== '') {
		$conditions[] = 'UPPER(`manufacturer`) LIKE ?';
		$params[] = mb_strtoupper($brand, 'UTF-8') . '%';
	}
	if ($model !== '') {
		$model_plain = str_replace('-', ' ', $model);
		$conditions[] = '(UPPER(`name`) LIKE ? OR UPPER(`name`) LIKE ? OR UPPER(`article`) LIKE ?)';
		$params[] = '%' . $model . '%';
		$params[] = '%' . str_replace('-', ' ', $model) . '%';
		$params[] = '%' . $model . '%';
	}

	$sql = 'SELECT `manufacturer`, `article`, `article_show`, `name`, `price`, `exist`, `storage`, `price_id`
		 FROM `shop_docpart_prices_data`
		 WHERE ' . implode(' AND ', $conditions) . '
		 ORDER BY `exist` DESC, `price` ASC
		 LIMIT 120';

	$rows = array();
	try {
		$stmt = $db->prepare($sql);
		$stmt->execute($params);
		$grouped = array();
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$b = trim((string)($row['manufacturer'] ?? ''));
			$art = !empty($row['article_show']) ? (string)$row['article_show'] : (string)$row['article'];
			$norm = docpart_normalize_article_for_price((string)$row['article']);
			$key = mb_strtoupper($b, 'UTF-8') . '|' . $norm;
			if (!isset($grouped[$key])) {
				$grouped[$key] = array(
					'brand' => $b,
					'article' => $art,
					'name' => (string)($row['name'] ?? ''),
					'lines' => array(),
					'total_qty' => 0,
				);
			}
			$qty = (float)($row['exist'] ?? 0);
			$line = array(
				'warehouse' => (string)($row['storage'] ?? ''),
				'qty' => $qty,
				'price' => (string)($row['price'] ?? ''),
				'price_id' => (int)($row['price_id'] ?? 0),
			);
			$line = epc_ssf_filter_agent_stock_lines($db, array($line));
			if ($line === array()) {
				continue;
			}
			$grouped[$key]['lines'][] = $line[0];
			$grouped[$key]['total_qty'] += $qty;
		}
		$rows = array_values($grouped);
		usort($rows, function ($a, $b) {
			return ($b['total_qty'] <=> $a['total_qty']);
		});
		if (count($rows) > $limit) {
			$rows = array_slice($rows, 0, $limit);
		}
		foreach ($rows as &$r) {
			$r['lines'] = epc_ssf_filter_agent_stock_lines($db, $r['lines'] ?? array());
			$r['total_qty'] = 0;
			foreach ($r['lines'] as $l) {
				$r['total_qty'] += (float)($l['qty'] ?? 0);
			}
			if ($r['total_qty'] <= 0) {
				continue;
			}
			$r['url'] = epc_demand_chpu_part_url($DP_Config, $r['brand'], $r['article']);
		}
		unset($r);
		$rows = array_values(array_filter($rows, function ($r) {
			return !empty($r['lines']) && (float)($r['total_qty'] ?? 0) > 0;
		}));
	} catch (Exception $e) {
	}

	if (empty($rows) && $brand !== '' && $model !== '' && $part_type !== '') {
		$fallback = $vq;
		$fallback['model'] = '';
		return epc_agent_search_vehicle_part($db, $DP_Config, $fallback, $limit);
	}
	return $rows;
}

/**
 * @return array{text:string, links?:array, suggestions?:array}
 */
function epc_agent_reply_vehicle_part(PDO $db, $DP_Config, array $vq, array &$session): array
{
	$lang = epc_agent_lang_href($DP_Config);
	$label = trim((string)($vq['label'] ?? ''));
	if ($label === '') {
		$label = 'your vehicle part';
	}
	$session['vehicle_query'] = $vq;

	$wants_brands = !empty($vq['wants_brands']);
	$part_type = trim((string)($vq['part_type'] ?? ''));
	$hits = epc_agent_search_vehicle_part($db, $DP_Config, $vq, $wants_brands ? 100 : 8);
	$catalog_url = $lang . '/vehicle-catalog';
	$request_url = $lang . '/zapros-prodavczu';

	if (!empty($hits) && $wants_brands && $part_type !== '') {
		$brand_rows = epc_agent_aggregate_part_type_brands($db, $DP_Config, $vq, 8);
		$premium = epc_agent_premium_part_brands($part_type);
		$lines = array();
		$rank = 1;
		$in_stock_premium = array();
		foreach ($brand_rows as $row) {
			$is_premium = in_array(mb_strtoupper($row['brand'], 'UTF-8'), $premium, true);
			if ($is_premium) {
				$in_stock_premium[] = $row['brand'];
			}
			$badge = $is_premium ? ' ⭐' : '';
			$sample_bits = array();
			foreach ($row['samples'] as $sample) {
				$sample_bits[] = $sample['article'] . ' (' . number_format((float)$sample['total_qty']) . ' pcs)';
			}
			$lines[] = $rank . '. **' . $row['brand'] . '**' . $badge
				. ' — ' . (int)$row['lines'] . ' lines, **' . number_format((float)$row['total_qty']) . ' pcs**'
				. (empty($sample_bits) ? '' : "\n   e.g. " . implode(', ', $sample_bits));
			$rank++;
		}
		$premium_note = '';
		if (!empty($in_stock_premium)) {
			$premium_note = "\n\n**Trusted brands we stock now:** " . implode(', ', array_unique($in_stock_premium))
				. " — ranked by UAE warehouse availability (S-UAE / R-UAE).";
		}
		$vehicle_note = ($vq['brand'] !== '' || $vq['model'] !== '')
			? "\n\n*(Tell me **vehicle + engine/VIN** to narrow to the exact " . $part_type . " for your car.)*"
			: "\n\n*(Send **make + model + year** or **VIN** to confirm exact fitment.)*";
		$first_url = !empty($brand_rows[0]['samples'][0]['url']) ? $brand_rows[0]['samples'][0]['url'] : '';
		$links = array(array('label' => 'Vehicle catalog', 'url' => $catalog_url));
		if ($first_url !== '') {
			array_unshift($links, array('label' => 'Open top ' . $part_type . ' line', 'url' => $first_url));
		}
		return array(
			'text' => "**Good " . $part_type . " brands in UAE stock** (by volume & availability):\n\n"
				. implode("\n", $lines) . $premium_note . $vehicle_note,
			'links' => $links,
			'suggestions' => array('Honda Civic 2014 water pump', 'Decode VIN', 'NGK 4195'),
		);
	}

	if (!empty($hits)) {
		$lines = array();
		foreach ($hits as $h) {
			$name = trim((string)$h['name']);
			$name_bit = $name !== '' ? ' — ' . $name : '';
			$lines[] = '• **' . $h['brand'] . ' ' . $h['article'] . '**' . $name_bit
				. ' — ' . number_format((float)$h['total_qty']) . ' pcs (' . epc_agent_format_stock_lines($h['lines']) . ')';
		}
		$year_note = !empty($vq['year']) ? "\n\n*(Year **" . (int)$vq['year'] . "** — confirm exact engine/fitment on the part page or catalog.)*" : '';
		return array(
			'text' => "**" . $label . "** — yes, we have matching lines in **UAE stock**:\n\n" . implode("\n", $lines) . $year_note
				. "\n\nOpen a line for crosses & fitment, or use the **Vehicle Catalog** to pick the exact engine variant.",
			'links' => array(
				array('label' => 'Open top match', 'url' => $hits[0]['url']),
				array('label' => 'Vehicle catalog', 'url' => $catalog_url),
			),
			'suggestions' => array('Send VIN for exact fitment', 'Best for my country'),
		);
	}

	$part_word = !empty($vq['part_type']) ? (string)$vq['part_type'] : 'part';
	$vehicle_hint = trim(($vq['brand'] !== '' ? $vq['brand'] . ' ' : '')
		. ($vq['model'] !== '' ? str_replace('-', ' ', $vq['model']) . ' ' : '')
		. ($vq['year'] > 0 ? (string)$vq['year'] . ' ' : '')
		. $part_word);
	return array(
		'text' => "I couldn't find **" . $label . "** in our current UAE price lists.\n\n"
			. "Engine parts like **" . $part_word . "s** usually need the **exact engine code** — best confirmed by **VIN** or our **Vehicle Catalog**.\n\n"
			. "**Next steps:**\n"
			. "1. **Vehicle Catalog** → **" . ($vq['brand'] !== '' ? $vq['brand'] : 'Make') . " → "
			. ($vq['model'] !== '' ? str_replace('-', ' ', $vq['model']) : 'Model')
			. ($vq['year'] > 0 ? ' → ' . $vq['year'] : '') . " → Engine / " . ucfirst($part_word) . "**\n"
			. "2. Paste your **VIN** here (I will decode it)\n"
			. "3. [Send part request](" . $request_url . ") with: *" . $vehicle_hint . "* — or **WhatsApp** our team (fastest for quotes)"
			. epc_agent_contact_text_block($DP_Config),
		'links' => array_merge(
			epc_agent_standard_contact_links($DP_Config, $lang),
			array(array('label' => 'Vehicle catalog', 'url' => $catalog_url))
		),
		'suggestions' => array('Decode VIN', 'NGK 4195'),
	);
}

function epc_agent_search_by_article(PDO $db, $DP_Config, string $article, int $limit = 10): array
{
	$norm = docpart_normalize_article_for_price($article);
	if ($norm === '') {
		return array();
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$rows = array();
	try {
		$stmt = $db->prepare(
			'SELECT `manufacturer`, `article`, `article_show`, `name`, `price`, `exist`, `storage`, `price_id`
			 FROM `shop_docpart_prices_data`
			 WHERE ' . $art_expr . ' = ?
			 AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
			 AND ' . epc_ssf_price_data_active_sql() . '
			 ORDER BY `exist` DESC, `price` ASC
			 LIMIT 80'
		);
		$stmt->execute(array($norm));
		$grouped = array();
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$b = trim((string)($row['manufacturer'] ?? ''));
			$key = mb_strtoupper($b, 'UTF-8') . '|' . $norm;
			if (!isset($grouped[$key])) {
				$grouped[$key] = array(
					'brand' => $b,
					'article' => !empty($row['article_show']) ? (string)$row['article_show'] : (string)$row['article'],
					'article_norm' => $norm,
					'name' => (string)($row['name'] ?? ''),
					'lines' => array(),
				);
			}
			$line = array(
				'warehouse' => (string)($row['storage'] ?? ''),
				'qty' => (float)($row['exist'] ?? 0),
				'price' => (string)($row['price'] ?? ''),
				'price_id' => (int)($row['price_id'] ?? 0),
			);
			$line = epc_ssf_filter_agent_stock_lines($db, array($line));
			if ($line === array()) {
				continue;
			}
			$grouped[$key]['lines'][] = $line[0];
		}
		$rows = array_values($grouped);
		usort($rows, function ($a, $b) {
			$qa = 0;
			$qb = 0;
			foreach ($a['lines'] as $l) { $qa += (float)$l['qty']; }
			foreach ($b['lines'] as $l) { $qb += (float)$l['qty']; }
			return $qb <=> $qa;
		});
		if (count($rows) > $limit) {
			$rows = array_slice($rows, 0, $limit);
		}
		foreach ($rows as &$r) {
			$r['lines'] = epc_ssf_filter_agent_stock_lines($db, $r['lines'] ?? array());
			$r['url'] = epc_demand_chpu_part_url($DP_Config, $r['brand'], $r['article']);
			$r['total_qty'] = 0;
			foreach ($r['lines'] as $l) {
				$r['total_qty'] += (float)$l['qty'];
			}
		}
		unset($r);
		$rows = array_values(array_filter($rows, function ($r) {
			return !empty($r['lines']) && (float)($r['total_qty'] ?? 0) > 0;
		}));
	} catch (Exception $e) {
	}
	return $rows;
}

function epc_agent_format_stock_lines(array $lines): string
{
	if (empty($lines)) {
		return '';
	}
	$showPrices = epc_storefront_prices_visible_for_user();
	$mask = function_exists('epc_storefront_sensitive_mask') ? epc_storefront_sensitive_mask() : '**';
	$parts = array();
	foreach ($lines as $line) {
		if (!$showPrices) {
			$parts[] = $mask;
			continue;
		}
		$wh = trim((string)($line['warehouse'] ?? ''));
		$qty = (int)round((float)($line['qty'] ?? 0));
		$price = trim((string)($line['price'] ?? ''));
		$seg = ($wh !== '' ? $wh . ': ' : '') . number_format($qty) . ' pcs';
		if ($price !== '') {
			$seg .= ' @ ' . $price;
		}
		$parts[] = $seg;
	}
	return implode('; ', $parts);
}

function epc_agent_lookup_vin($DP_Config, string $vin): array
{
	$base = epc_demand_site_base($DP_Config);
	if ($base === '') {
		return array('ok' => false, 'message' => 'Catalog unavailable.');
	}
	$url = $base . '/api/umapi_proxy.php?' . http_build_query(array(
		'action' => 'vin',
		'vin' => $vin,
		'language' => 'en',
	));
	$data = epc_demand_http_json($url, 25);
	if (!is_array($data)) {
		return array('ok' => false, 'message' => 'VIN lookup failed. Try again or send a part request.');
	}
	if (!empty($data['message']) && empty($data['data'])) {
		return array('ok' => false, 'message' => (string)$data['message']);
	}
	$payload = isset($data['data']) && is_array($data['data']) ? $data['data'] : $data;
	$vehicles = array();
	foreach (array('matchingVehicles', 'matchingVehicleIds') as $k) {
		if (!empty($payload[$k]) && is_array($payload[$k])) {
			$vehicles = $payload[$k];
			break;
		}
	}
	if (empty($vehicles) && !empty($payload['matchingManufacturers'])) {
		return array('ok' => false, 'message' => 'VIN decoded partially — open Vehicle Catalog to finish selection.');
	}
	if (empty($vehicles)) {
		return array('ok' => false, 'message' => 'No vehicle found for this VIN in our catalog.');
	}
	$v = is_array($vehicles[0]) ? $vehicles[0] : array();
	$manu = (string)($v['manuName'] ?? $v['manufacturerName'] ?? '');
	$model = (string)($v['modelName'] ?? $v['model'] ?? '');
	$year = (string)($v['yearOfConstruction'] ?? $v['year'] ?? '');
	$engine = (string)($v['motorType'] ?? $v['engineName'] ?? $v['description'] ?? '');
	$label = trim(implode(' ', array_filter(array($manu, $model, $year, $engine))));
	$lang = epc_agent_lang_href($DP_Config);
	return array(
		'ok' => true,
		'vehicle' => $label,
		'vin' => $vin,
		'catalog_url' => $lang . '/vehicle-catalog?vin=' . rawurlencode($vin),
		'count' => count($vehicles),
	);
}

function epc_agent_market_for_part(PDO $db, $DP_Config, string $brand, string $article, string $country_code): array
{
	$country_code = epc_demand_normalize_country_code($country_code);
	$registry = epc_demand_country_registry();
	$card = epc_demand_build_card($db, $DP_Config, $brand, $article, null, $country_code);
	$stats = epc_demand_get_demand_statistics($db, $brand, docpart_normalize_article_for_price($article));
	$tagged_for_country = false;
	$country_name = '';
	if ($country_code !== '' && isset($registry[$country_code])) {
		$country_name = (string)$registry[$country_code]['name'];
		foreach ($card['demand_countries'] ?? array() as $c) {
			if (!empty($c['code']) && $c['code'] === $country_code) {
				$tagged_for_country = true;
				break;
			}
		}
	}
	return array(
		'card' => $card,
		'stats' => $stats,
		'tagged_for_country' => $tagged_for_country,
		'country_code' => $country_code,
		'country_name' => $country_name,
	);
}

function epc_agent_market_overview(PDO $db, $DP_Config, string $country_code, int $limit = 6): array
{
	$view = epc_demand_build_country_view($db, $DP_Config, $country_code, $limit);
	if (empty($view['status'])) {
		return array('ok' => false, 'message' => (string)($view['message'] ?? 'Unknown country'));
	}
	$in_stock = array();
	foreach ($view['parts'] as $p) {
		if (!empty($p['anchor_in_stock'])) {
			$in_stock[] = $p;
		}
	}
	return array('ok' => true, 'country' => $view['country'], 'parts' => $in_stock, 'total' => count($in_stock));
}

function epc_agent_whatsapp_href($DP_Config): string
{
	$num = '';
	if (is_object($DP_Config) && !empty($DP_Config->epc_whatsapp_number)) {
		$num = preg_replace('/[^0-9]/', '', (string)$DP_Config->epc_whatsapp_number);
	} elseif (is_object($DP_Config) && !empty($DP_Config->epc_contact_phone)) {
		$num = preg_replace('/[^0-9]/', '', (string)$DP_Config->epc_contact_phone);
	}
	return $num !== '' ? 'https://wa.me/' . $num : '';
}

/**
 * @return array{person:string, phone:string, whatsapp:string, whatsapp_href:string, email:string, office:string}
 */
function epc_agent_contact_info($DP_Config): array
{
	$phone = is_object($DP_Config) && !empty($DP_Config->epc_contact_phone)
		? trim((string)$DP_Config->epc_contact_phone) : '';
	$whatsapp = is_object($DP_Config) && !empty($DP_Config->epc_whatsapp_number)
		? trim((string)$DP_Config->epc_whatsapp_number) : $phone;
	$email = is_object($DP_Config) && !empty($DP_Config->epc_head_office_email)
		? trim((string)$DP_Config->epc_head_office_email) : '';
	$office = is_object($DP_Config) && !empty($DP_Config->epc_head_office_title)
		? trim((string)$DP_Config->epc_head_office_title) : 'Head Office';
	$person = '';
	$locations = is_object($DP_Config) && !empty($DP_Config->epc_global_locations_countries)
		? str_replace('\\n', "\n", (string)$DP_Config->epc_global_locations_countries) : '';
	if ($locations !== '') {
		$blocks = preg_split("/\n\s*\n/", trim($locations));
		$head_block = trim((string)($blocks[0] ?? ''));
		if ($head_block !== '' && preg_match('/Contact person:\s*(.+?)(?:\r?\n|$)/i', $head_block, $m)) {
			$person = trim($m[1]);
		}
	}
	if ($person === '') {
		$person = 'Sales team';
	}
	return array(
		'person' => $person,
		'phone' => $phone,
		'whatsapp' => $whatsapp,
		'whatsapp_href' => epc_agent_whatsapp_href($DP_Config),
		'email' => $email,
		'office' => $office,
	);
}

function epc_agent_contact_text_block($DP_Config): string
{
	$c = epc_agent_contact_info($DP_Config);
	$lang = epc_agent_lang_href($DP_Config);
	$lines = array("\n**Contact our team (" . $c['office'] . ")**");
	$lines[] = '• **Contact person:** ' . $c['person'];
	if ($c['whatsapp'] !== '' && $c['whatsapp_href'] !== '') {
		$lines[] = '• **WhatsApp:** [' . $c['whatsapp'] . '](' . $c['whatsapp_href'] . ')';
	} elseif ($c['whatsapp'] !== '') {
		$lines[] = '• **WhatsApp:** ' . $c['whatsapp'];
	}
	if ($c['phone'] !== '' && $c['phone'] !== $c['whatsapp']) {
		$lines[] = '• **Phone:** ' . $c['phone'];
	} elseif ($c['phone'] !== '' && $c['whatsapp'] === '') {
		$lines[] = '• **Phone:** ' . $c['phone'];
	}
	if ($c['email'] !== '') {
		$lines[] = '• **Email:** ' . $c['email'];
	}
	$lines[] = '• **Part request:** [Send part request online](' . $lang . '/zapros-prodavczu)';
	return implode("\n", $lines);
}

/**
 * @return array<int, array{label:string, url:string}>
 */
function epc_agent_standard_contact_links($DP_Config, ?string $lang = null): array
{
	$lang = $lang !== null ? $lang : epc_agent_lang_href($DP_Config);
	$c = epc_agent_contact_info($DP_Config);
	$links = array();
	if ($c['whatsapp_href'] !== '') {
		$label = 'WhatsApp';
		if ($c['whatsapp'] !== '') {
			$label .= ' ' . $c['whatsapp'];
		}
		$links[] = array('label' => $label, 'url' => $c['whatsapp_href']);
	}
	$links[] = array('label' => 'Send part request', 'url' => $lang . '/zapros-prodavczu');
	$links[] = array('label' => 'About delivery', 'url' => $lang . '/o-dostavke');
	return $links;
}

function epc_agent_is_marketing_platform(): bool
{
	if (!function_exists('epc_portal_is_platform_hostname')) {
		$tenantFile = dirname(__DIR__, 2) . '/general_pages/epc_portal_tenant.php';
		if (is_file($tenantFile)) {
			require_once $tenantFile;
		}
	}
	$host = isset($_SERVER['HTTP_HOST']) ? strtolower((string) $_SERVER['HTTP_HOST']) : '';
	if ($host !== '' && strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname($host !== '' ? $host : null)) {
		return $host !== 'cp.ecomae.com';
	}
	if (function_exists('epc_ecomae_is_marketing_platform_host')) {
		return epc_ecomae_is_marketing_platform_host($host !== '' ? $host : null);
	}
	return in_array($host, array('www.ecomae.com', 'ecomae.com'), true);
}

/**
 * @return array{enabled:int, agent_name:string, subtitle:string, greeting:string, system_prompt:string, teaser_text:string, placeholder:string, logo_url:string, domain:string}
 */
function epc_agent_default_config_row(): array
{
	return array(
		'enabled' => 1,
		'agent_name' => '',
		'subtitle' => '',
		'greeting' => '',
		'system_prompt' => '',
		'teaser_text' => '',
		'placeholder' => '',
		'logo_url' => '',
		'domain' => '',
	);
}

function epc_agent_config_ensure_schema(PDO $db): void
{
	epc_agent_ensure_db_schema($db);
	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_parts_agent_config` (
			`id` TINYINT UNSIGNED NOT NULL DEFAULT 1,
			`enabled` TINYINT NOT NULL DEFAULT 1,
			`agent_name` VARCHAR(128) NOT NULL DEFAULT \'\',
			`subtitle` VARCHAR(255) NOT NULL DEFAULT \'\',
			`greeting` TEXT,
			`system_prompt` TEXT,
			`teaser_text` VARCHAR(255) NOT NULL DEFAULT \'\',
			`placeholder` VARCHAR(255) NOT NULL DEFAULT \'\',
			`logo_url` VARCHAR(512) NOT NULL DEFAULT \'\',
			`domain` VARCHAR(255) NOT NULL DEFAULT \'\',
			`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

/**
 * @return array<string, mixed>
 */
function epc_agent_load_config($DP_Config, ?PDO $db = null): array
{
	$row = epc_agent_default_config_row();
	$pdo = $db;
	if (!$pdo instanceof PDO) {
		try {
			if (!is_object($DP_Config) || empty($DP_Config->host) || empty($DP_Config->db)) {
				return $row;
			}
			$pdo = new PDO(
				'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
				$DP_Config->user,
				$DP_Config->password,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
		} catch (Throwable $e) {
			return $row;
		}
	}
	try {
		epc_agent_config_ensure_schema($pdo);
		$st = $pdo->query('SELECT * FROM `epc_parts_agent_config` WHERE `id` = 1 LIMIT 1');
		$dbRow = $st ? $st->fetch(PDO::FETCH_ASSOC) : false;
		if (is_array($dbRow)) {
			foreach (array_keys($row) as $key) {
				if (array_key_exists($key, $dbRow) && (string) $dbRow[$key] !== '') {
					$row[$key] = $dbRow[$key];
				}
			}
			$row['enabled'] = (int) ($dbRow['enabled'] ?? 1);
		}
	} catch (Throwable $e) {
		// Fall back to defaults.
	}
	return $row;
}

/**
 * @param array<string, mixed> $input
 */
function epc_agent_save_config(PDO $db, array $input): array
{
	epc_agent_config_ensure_schema($db);
	$defaults = epc_agent_default_config_row();
	$data = array();
	foreach (array_keys($defaults) as $key) {
		if ($key === 'enabled') {
			$data[$key] = !empty($input[$key]) ? 1 : 0;
			continue;
		}
		$data[$key] = isset($input[$key]) ? trim((string) $input[$key]) : '';
	}
	$data['updated_at'] = time();
	$st = $db->prepare(
		'INSERT INTO `epc_parts_agent_config`
		 (`id`, `enabled`, `agent_name`, `subtitle`, `greeting`, `system_prompt`, `teaser_text`, `placeholder`, `logo_url`, `domain`, `updated_at`)
		 VALUES (1, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE
		 `enabled` = VALUES(`enabled`), `agent_name` = VALUES(`agent_name`), `subtitle` = VALUES(`subtitle`),
		 `greeting` = VALUES(`greeting`), `system_prompt` = VALUES(`system_prompt`), `teaser_text` = VALUES(`teaser_text`),
		 `placeholder` = VALUES(`placeholder`), `logo_url` = VALUES(`logo_url`), `domain` = VALUES(`domain`), `updated_at` = VALUES(`updated_at`)'
	);
	$st->execute(array(
		$data['enabled'],
		$data['agent_name'],
		$data['subtitle'],
		$data['greeting'],
		$data['system_prompt'],
		$data['teaser_text'],
		$data['placeholder'],
		$data['logo_url'],
		$data['domain'],
		$data['updated_at'],
	));
	return $data;
}

/**
 * Tenant / marketing branding for the floating chat widget.
 *
 * @return array<string, mixed>
 */
function epc_agent_widget_branding($DP_Config): array
{
	$config = epc_agent_load_config($DP_Config);
	$contact = epc_agent_contact_info($DP_Config);
	$isMarketing = epc_agent_is_marketing_platform();
	$siteSettings = array();
	$siteProfile = array();
	if (function_exists('epc_portal_load_site_settings')) {
		require_once dirname(__DIR__, 2) . '/general_pages/epc_portal_db.php';
		$siteSettings = epc_portal_load_site_settings();
	}
	if (function_exists('epc_portal_site_profile')) {
		require_once dirname(__DIR__, 2) . '/general_pages/epc_portal.php';
		$siteProfile = epc_portal_site_profile();
	}
	$tenantBrand = null;
	if (!$isMarketing) {
		$brandFile = dirname(__DIR__, 2) . '/general_pages/epc_portal_tenant_brand.php';
		if (is_file($brandFile)) {
			require_once $brandFile;
			if (function_exists('epc_portal_tenant_brand_config')) {
				$tenantBrand = epc_portal_tenant_brand_config();
			}
		}
	}

	$tradeName = '';
	if ($isMarketing) {
		$tradeName = 'ECOM AE';
	} elseif (!empty($siteProfile['trade_name'])) {
		$tradeName = trim((string) $siteProfile['trade_name']);
	} elseif (!empty($siteSettings['system_name'])) {
		$tradeName = trim((string) $siteSettings['system_name']);
	} elseif (is_object($DP_Config) && !empty($DP_Config->site_name)) {
		$tradeName = trim((string) $DP_Config->site_name);
	}
	if ($tradeName === '') {
		$tradeName = 'Store';
	}

	$domain = '';
	if (!empty($config['domain'])) {
		$domain = trim((string) $config['domain']);
	} elseif (!empty($siteProfile['domain_path'])) {
		$domain = rtrim((string) $siteProfile['domain_path'], '/');
	} elseif (!empty($siteSettings['domain_path'])) {
		$domain = rtrim((string) $siteSettings['domain_path'], '/');
	} elseif (is_object($DP_Config) && !empty($DP_Config->domain_path)) {
		$domain = rtrim((string) $DP_Config->domain_path, '/');
	}
	if ($domain === '' && !empty($_SERVER['HTTP_HOST'])) {
		$scheme = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
		$domain = $scheme . '://' . $_SERVER['HTTP_HOST'];
	}

	$logoUrl = '';
	if (!empty($config['logo_url'])) {
		$logoUrl = trim((string) $config['logo_url']);
	} elseif ($isMarketing) {
		$logoUrl = '/content/files/images/ecomae-logo.png';
	} elseif (is_array($tenantBrand) && !empty($tenantBrand['logo_url'])) {
		$logoUrl = (string) $tenantBrand['logo_url'];
	}

	$industry = (string) ($siteProfile['industry'] ?? $siteSettings['industry_code'] ?? 'auto_parts');
	$isAutoParts = ($industry === 'auto_parts');

	if ($isMarketing) {
		$agentName = 'Ecom Expert';
		$subtitle = 'E-commerce · ERP · Demo · 24/7';
		$greeting = "Hi! I'm **Ecom Expert**, your ECOM AE platform assistant.\n\nAsk about multi-tenant storefronts, Super CP, industry templates, pricing, or booking a demo.";
		$teaser = 'Questions about ECOM AE cloud? Tap to chat with Ecom Expert.';
		$placeholder = 'Ask about platform, pricing, or demo…';
		$quickActions = array(
			array('id' => 'platform', 'label' => 'Platform overview'),
			array('id' => 'industries', 'label' => 'Industry templates'),
			array('id' => 'pricing', 'label' => 'Pricing'),
			array('id' => 'demo', 'label' => 'Book a demo'),
			array('id' => 'contact', 'label' => 'Contact sales'),
		);
	} elseif ($isAutoParts) {
		$agentName = 'AI Parts Expert';
		$subtitle = $tradeName . ' · VIN · Part numbers · Export markets · 24/7';
		$greeting = "Hi! I'm **" . $tradeName . "'s AI Parts Expert**.\n\nHow can I help you? **Type your question below.**";
		$teaser = 'Need a part? Ask me — stock, VIN & export markets.';
		$placeholder = 'Part number, VIN, or "I\'m from Sudan…"';
		$quickActions = array(
			array('id' => 'part', 'label' => 'Search part number'),
			array('id' => 'vin', 'label' => 'Decode VIN'),
			array('id' => 'market', 'label' => 'Best for my country'),
			array('id' => 'vehicle', 'label' => 'Browse by vehicle'),
			array('id' => 'contact', 'label' => 'WhatsApp / contact'),
		);
	} else {
		$agentName = $tradeName . ' Assistant';
		$subtitle = $tradeName . ' · Product help · Orders · Contact';
		$greeting = "Hi! I'm **" . $tradeName . "'s assistant**.\n\nAsk about products, orders, delivery, or how to reach our team.";
		$teaser = 'Questions about ' . $tradeName . '? Tap to chat.';
		$placeholder = 'Ask about products, orders, or contact…';
		$quickActions = array(
			array('id' => 'products', 'label' => 'Browse products'),
			array('id' => 'orders', 'label' => 'Order help'),
			array('id' => 'delivery', 'label' => 'Delivery info'),
			array('id' => 'contact', 'label' => 'Contact us'),
		);
	}

	if (!$isMarketing) {
		if (!empty($config['agent_name'])) {
			$agentName = trim((string) $config['agent_name']);
		}
		if (!empty($config['subtitle'])) {
			$subtitle = trim((string) $config['subtitle']);
		}
		if (!empty($config['greeting'])) {
			$greeting = trim((string) $config['greeting']);
		}
		if (!empty($config['teaser_text'])) {
			$teaser = trim((string) $config['teaser_text']);
		}
		if (!empty($config['placeholder'])) {
			$placeholder = trim((string) $config['placeholder']);
		}
	}

	return array(
		'enabled' => (int) ($config['enabled'] ?? 1),
		'agent_name' => $agentName,
		'subtitle' => $subtitle,
		'greeting' => $greeting,
		'system_prompt' => trim((string) ($config['system_prompt'] ?? '')),
		'teaser_text' => $teaser,
		'placeholder' => $placeholder,
		'logo_url' => $logoUrl,
		'domain' => $domain,
		'trade_name' => $tradeName,
		'contact_phone' => (string) ($contact['phone'] ?? ''),
		'contact_email' => (string) ($contact['email'] ?? ''),
		'contact_whatsapp' => (string) ($contact['whatsapp_href'] ?? ''),
		'is_marketing' => $isMarketing,
		'is_auto_parts' => $isAutoParts,
		'industry' => $industry,
		'quick_actions' => $quickActions,
		'theme' => 'blue',
	);
}

function epc_agent_bootstrap($DP_Config): array
{
	$lang = epc_agent_lang_href($DP_Config);
	$branding = epc_agent_widget_branding($DP_Config);
	$registry = epc_demand_country_registry();
	$countries = array();
	foreach ($registry as $code => $meta) {
		if (epc_demand_is_stock_pool_country_code($code)) {
			continue;
		}
		$countries[] = array('code' => $code, 'name' => (string)$meta['name']);
	}
	$links = array(
		'part_request' => $lang . '/zapros-prodavczu',
		'whatsapp' => epc_agent_whatsapp_href($DP_Config),
	);
	if (!empty($branding['is_marketing'])) {
		$base = rtrim((string) ($branding['domain'] ?? '/'), '/');
		if ($base === '') {
			$base = '';
		}
		$links = array(
			'platform' => $base . '/platform',
			'industries' => $base . '/platform/industries',
			'pricing' => $base . '/platform/pricing',
			'demo' => $base . '/platform/demo',
			'contact' => $base . '/platform/contact',
		);
	} elseif (!empty($branding['is_auto_parts'])) {
		$links['vehicle_catalog'] = $lang . '/vehicle-catalog';
	}
	return array_merge($branding, array(
		'ok' => true,
		'proactive_delay_ms' => 1200,
		'links' => $links,
		'countries' => $countries,
	));
}

function epc_agent_is_faq(string $text): bool
{
	$lower = mb_strtolower($text, 'UTF-8');
	$keys = array(
		'delivery', 'shipping', 'payment', 'pay', 'return', 'refund', 'warranty', 'hours',
		'contact', 'where are you', 'location', 'office', 'whatsapp', 'whats app',
		'part request', 'send request', 'contact person', 'phone', 'email', 'call us', 'reach you',
	);
	foreach ($keys as $k) {
		if (strpos($lower, $k) !== false) {
			return true;
		}
	}
	return false;
}

function epc_agent_is_contact_only(string $text): bool
{
	$lower = mb_strtolower(trim($text), 'UTF-8');
	if (!epc_agent_is_faq($text)) {
		return false;
	}
	$contact_keys = array('whatsapp', 'whats app', 'contact', 'phone', 'email', 'call', 'part request', 'send request', 'contact person', 'reach');
	foreach ($contact_keys as $k) {
		if (strpos($lower, $k) !== false) {
			return true;
		}
	}
	return false;
}

function epc_agent_contact_reply($DP_Config): array
{
	$lang = epc_agent_lang_href($DP_Config);
	return array(
		'text' => "**How to reach EpartsCart sales**" . epc_agent_contact_text_block($DP_Config)
			. "\n\nFor **part numbers, VIN decode, or stock checks**, type your question here and I will search live UAE availability.",
		'links' => epc_agent_standard_contact_links($DP_Config, $lang),
		'suggestions' => array('NGK 4195', 'Decode VIN', 'About delivery'),
	);
}

function epc_agent_faq_reply($DP_Config): array
{
	$lang = epc_agent_lang_href($DP_Config);
	return array(
		'text' => "**Delivery & payment (quick guide)**\n\n"
			. "• We ship from **UAE warehouses** (S-UAE / R-UAE) worldwide.\n"
			. "• Payment & delivery details: [About delivery](" . $lang . "/o-dostavke) · [About payment](" . $lang . "/ob-oplate) · [Returns](" . $lang . "/o-vozvrate)\n"
			. "• Need a quote or hard-to-find part? Use the links below — **WhatsApp** is fastest for export quotes."
			. epc_agent_contact_text_block($DP_Config)
			. "\n\nFor **part fitment or stock**, send me a **part number** or **VIN** and I'll check live availability.",
		'links' => epc_agent_standard_contact_links($DP_Config, $lang),
	);
}

/**
 * @param array<string, mixed> $match
 * @return array{text:string, links?:array, suggestions?:array}
 */
function epc_agent_reply_catalog_list($DP_Config, array $match): array
{
	$lang = epc_agent_lang_href($DP_Config);
	$index = isset($match['index']) && is_array($match['index']) ? $match['index'] : array();
	$counts = isset($index['section_counts']) && is_array($index['section_counts']) ? $index['section_counts'] : array();
	$stock_total = isset($index['stock']) && is_array($index['stock']) ? count($index['stock']) : 0;
	$section_hint = (string)($match['section_hint'] ?? '');

	$lines = array();
	$lines[] = '**Our catalogs cover:**';
	$lines[] = '• **Passenger cars** — ' . number_format((int)($counts['passenger'] ?? 0)) . ' makes';
	$lines[] = '• **Commercial vehicles** — ' . number_format((int)($counts['commercial'] ?? 0)) . ' makes';
	$lines[] = '• **Motorcycles** — ' . number_format((int)($counts['motorbike'] ?? 0)) . ' makes';
	$lines[] = '• **Parts brands in UAE stock** — ' . number_format($stock_total) . ' brands';

	if ($section_hint !== '' && isset($counts[$section_hint])) {
		$sample = array();
		if (isset($index['vehicles']) && is_array($index['vehicles'])) {
			foreach ($index['vehicles'] as $row) {
				if (!in_array($section_hint, $row['sections'] ?? array(), true)) {
					continue;
				}
				$sample[] = (string)$row['name'];
				if (count($sample) >= 12) {
					break;
				}
			}
		}
		if (!empty($sample)) {
			$lines[] = '';
			$lines[] = '**Sample ' . epc_agent_catalog_section_label($section_hint) . ' makes:** ' . implode(', ', $sample) . '…';
		}
	} else {
		$popular = array('TOYOTA', 'NISSAN', 'HONDA', 'BMW', 'MERCEDES-BENZ', 'HINO', 'ISUZU', 'YAMAHA', 'KAWASAKI', '555', 'NGK', 'DENSO');
		$found = array();
		foreach ($popular as $name) {
			if (epc_agent_catalog_match_stock($index, $name) !== null || epc_agent_catalog_match_vehicle($index, $name) !== null) {
				$found[] = $name;
			}
		}
		if (!empty($found)) {
			$lines[] = '';
			$lines[] = '**Examples you can ask about:** ' . implode(', ', $found) . '.';
		}
	}

	$lines[] = '';
	$lines[] = 'Ask me any **make** or **brand** by name — e.g. *"Do you have Hino commercial parts?"* or *"555 brand available"*';

	return array(
		'text' => implode("\n", $lines),
		'links' => array(
			array('label' => 'Vehicle catalog', 'url' => $lang . '/vehicle-catalog'),
			array('label' => 'Available brands', 'url' => $lang . '/available-brands'),
			array('label' => 'Parts brands in stock', 'url' => $lang . '/parts'),
		),
		'suggestions' => array('Toyota passenger', 'Hino commercial', 'Yamaha motorcycle', '555 brand'),
	);
}

/**
 * @param array<string, mixed> $match
 * @return array{text:string, links?:array, suggestions?:array}
 */
function epc_agent_reply_catalog_match(PDO $db, $DP_Config, array $match, string $original_query): array
{
	$lang = epc_agent_lang_href($DP_Config);
	$action = (string)($match['action'] ?? 'none');

	if ($action === 'list') {
		return epc_agent_reply_catalog_list($DP_Config, $match);
	}

	$stock = isset($match['stock']) && is_array($match['stock']) ? $match['stock'] : null;
	$vehicle = isset($match['vehicle']) && is_array($match['vehicle']) ? $match['vehicle'] : null;
	$model = isset($match['model']) && is_array($match['model']) ? $match['model'] : null;

	if ($stock !== null && $vehicle === null && $action === 'stock_brand') {
		return epc_agent_reply_manufacturer_stock($db, $DP_Config, (string)$stock['name']);
	}

	if ($vehicle === null) {
		return array(
			'text' => "I couldn't match that to our catalog. Try a **make** (Toyota, Hino, Yamaha) or **parts brand** (555, NGK, Denso).",
			'links' => array(
				array('label' => 'Vehicle catalog', 'url' => $lang . '/vehicle-catalog'),
				array('label' => 'All parts brands', 'url' => $lang . '/parts'),
			),
		);
	}

	$sections = isset($vehicle['sections']) && is_array($vehicle['sections']) ? $vehicle['sections'] : array();
	$section_labels = array();
	foreach ($sections as $sec) {
		$section_labels[] = epc_agent_catalog_section_label($sec);
	}
	$section_text = implode(', ', $section_labels);

	$lines = array();
	$lines[] = '**Yes — ' . $vehicle['name'] . '** is in our **Epart vehicle catalog**.';
	$lines[] = '**Category:** ' . $section_text . '.';
	if (!empty($vehicle['country'])) {
		$lines[] = '**Origin:** ' . $vehicle['country'] . '.';
	}
	if ($model !== null) {
		$lines[] = '**Model matched:** ' . $model['name'] . ' — browse engines/modifications in the catalog.';
	} else {
		$lines[] = 'Browse **year → make → model → engine** to find exact fitment parts.';
	}

	$stock_name = '';
	if ($stock !== null) {
		$stock_name = (string)$stock['name'];
	} else {
		$stock_name = epc_agent_catalog_match_name_in_text($db, $vehicle['name'], 'stock');
	}
	if ($stock_name !== '') {
		$all = epc_stock_brand_parts_for_manufacturer($db, $stock_name, 5000);
		if (!empty($all)) {
			$total_qty = 0;
			foreach ($all as $row) {
				$total_qty += (float)($row['exist'] ?? 0);
			}
			$lines[] = '';
			$lines[] = '**UAE stock:** we also carry **' . $stock_name . '** parts — **' . number_format(count($all)) . ' part numbers**, **' . number_format((int)$total_qty) . ' pcs** in warehouses.';
		}
	}

	$links = array(
		array('label' => 'Open vehicle catalog — ' . $vehicle['name'], 'url' => $lang . '/vehicle-catalog'),
		array('label' => 'Epart catalog (OE)', 'url' => $lang . '/umapi_catalog'),
	);
	if ($stock_name !== '') {
		$links[] = array('label' => 'Browse ' . $stock_name . ' parts in stock', 'url' => epc_agent_manufacturer_browse_url($DP_Config, $stock_name));
	}

	return array(
		'text' => implode("\n", $lines),
		'links' => $links,
		'suggestions' => array(
			$vehicle['name'] . ' parts in stock',
			'Decode VIN for ' . $vehicle['name'],
			'WhatsApp contact',
		),
	);
}

/**
 * @return array{text:string, links?:array, suggestions?:array, country_code?:string}
 */
function epc_agent_marketing_reply($DP_Config, string $message): ?array
{
	if (!epc_agent_is_marketing_platform()) {
		return null;
	}
	$branding = epc_agent_widget_branding($DP_Config);
	$base = rtrim((string) ($branding['domain'] ?? ''), '/');
	if ($base === '') {
		$base = 'https://www.ecomae.com';
	}
	$lower = mb_strtolower(trim($message), 'UTF-8');
	$links = array(
		array('label' => 'Platform overview', 'url' => $base . '/platform'),
		array('label' => 'Industries', 'url' => $base . '/platform/industries'),
		array('label' => 'Pricing', 'url' => $base . '/platform/pricing'),
		array('label' => 'Book demo', 'url' => $base . '/platform/demo'),
		array('label' => 'Contact', 'url' => $base . '/platform/contact'),
	);
	if (preg_match('/\b(pric(e|ing)|cost|tier|plan)\b/u', $lower)) {
		return array(
			'text' => "**ECOM AE pricing** — hosted multi-tenant cloud with storefront, CP, and ERP modules.\n\nSee [Pricing](" . $base . "/platform/pricing) for rental tiers, or [book a demo](" . $base . "/platform/demo) for a sandbox.",
			'links' => $links,
			'suggestions' => array('Book a demo', 'Industry templates', 'Contact sales'),
		);
	}
	if (preg_match('/\b(demo|sandbox|trial|test)\b/u', $lower)) {
		return array(
			'text' => "**Demo sandbox** — spin up an isolated tenant in minutes on ECOM AE.\n\nStart at [Demo](" . $base . "/platform/demo) — choose auto parts, fashion, or ERP-only.",
			'links' => $links,
			'suggestions' => array('Platform overview', 'Pricing', 'Contact sales'),
		);
	}
	if (preg_match('/\b(industr(y|ies)|vertical|template|auto|fashion|tax|erp)\b/u', $lower)) {
		return array(
			'text' => "**Industry templates** — auto spare parts, fashion, tax advisory, electronics, medical, and more.\n\nBrowse [Industries](" . $base . "/platform/industries) for vertical-specific storefront + CP packs.",
			'links' => $links,
			'suggestions' => array('Book a demo', 'Pricing', 'Platform overview'),
		);
	}
	if (preg_match('/\b(platform|ecom ae|ecomae|super cp|tenant|cloud|erp|crm)\b/u', $lower)) {
		return array(
			'text' => "**ECOM AE** is a hosted cloud for **e-commerce + ERP + CRM** — multi-tenant storefronts, operator Super CP, and UAE e-invoice (Peppol).\n\nExplore the [Platform](" . $base . "/platform) page or [Super CP guides](" . $base . "/platform/platform-guides).",
			'links' => $links,
			'suggestions' => array('Book a demo', 'Pricing', 'Industry templates'),
		);
	}
	if (preg_match('/\b(contact|sales|email|phone|whatsapp|reach)\b/u', $lower)) {
		return array(
			'text' => "**Contact ECOM AE sales** — Dubai, UAE.\n\nUse the [Contact](" . $base . "/platform/contact) page or book a [demo](" . $base . "/platform/demo) to speak with our team.",
			'links' => $links,
		);
	}
	return array(
		'text' => "I'm **Ecom Expert**, your ECOM AE platform assistant. I can help with platform capabilities, industry templates, pricing, and booking a demo.\n\nWhat would you like to know?",
		'links' => $links,
		'suggestions' => array('Platform overview', 'Book a demo', 'Pricing', 'Industry templates'),
	);
}

function epc_agent_handle_message(PDO $db, $DP_Config, string $message, array &$session): array
{
	global $db_link;
	$db_link = $db;

	$message = trim($message);
	if ($message === '') {
		if (epc_agent_is_marketing_platform()) {
			return array('text' => 'Ask about ECOM AE platform, industries, pricing, or booking a demo.');
		}
		return array('text' => 'Please type a part number, VIN, or tell me your country and vehicle.');
	}

	if (epc_agent_is_marketing_platform()) {
		$marketing = epc_agent_marketing_reply($DP_Config, $message);
		if ($marketing !== null) {
			return $marketing;
		}
	}

	$lower = mb_strtolower($message, 'UTF-8');

	if (preg_match('/^(hi|hello|hey|good morning|good evening|salam|assalam|marhaba)\b/u', $lower)) {
		return array(
			'text' => "Hello! How can I help you? **Type your question** — part number, brand, VIN, or vehicle.",
			'suggestions' => array('Search NGK 4195', 'Decode VIN', '555 brand'),
		);
	}

	$country_hit = epc_agent_detect_country($message);
	if ($country_hit['code'] !== '') {
		$session['country_code'] = $country_hit['code'];
		$session['country_name'] = $country_hit['name'];
	}

	$country_code = (string)($session['country_code'] ?? '');
	$country_name = (string)($session['country_name'] ?? '');

	if (epc_agent_is_faq($message)) {
		if (epc_agent_is_contact_only($message)
			&& !preg_match('/\b(delivery|shipping|payment|pay|return|refund|warranty)\b/i', $lower)) {
			return epc_agent_contact_reply($DP_Config);
		}
		return epc_agent_faq_reply($DP_Config);
	}

	$catalog_match = epc_agent_catalog_resolve_query($db, $message);
	if ($catalog_match !== null) {
		$ba_skip = epc_agent_extract_brand_article($message);
		$has_explicit_part = ($ba_skip['brand'] !== '' && $ba_skip['article'] !== ''
			&& mb_strlen($ba_skip['article'], 'UTF-8') >= 5);
		$vq_early = epc_agent_extract_vehicle_query($message);
		$has_vehicle_part_query = !empty($vq_early['is_query'])
			&& (($vq_early['part_type'] ?? '') !== '' || ($vq_early['model'] ?? '') !== '');
		if (!$has_explicit_part && !$has_vehicle_part_query) {
			return epc_agent_reply_catalog_match($db, $DP_Config, $catalog_match, $message);
		}
	}

	$mfr_query = epc_agent_extract_manufacturer_parts_query($message);
	if ($mfr_query !== '') {
		return epc_agent_reply_manufacturer_stock($db, $DP_Config, $mfr_query);
	}

	$vin = epc_agent_extract_vin($message);
	if ($vin !== '' || preg_match('/\bvin\b/i', $message)) {
		if ($vin === '') {
			return array('text' => 'Please paste your full **VIN** (11–17 characters) and I will decode it.');
		}
		$result = epc_agent_lookup_vin($DP_Config, $vin);
		if (empty($result['ok'])) {
			return array(
				'text' => $result['message'],
				'links' => array(
					array('label' => 'Send VIN request', 'url' => epc_agent_lang_href($DP_Config) . '/zapros-prodavczu'),
				),
			);
		}
		$extra = '';
		if (!empty($result['count']) && (int)$result['count'] > 1) {
			$extra = "\n\n(" . (int)$result['count'] . " variants found — pick the exact engine in the catalog.)";
		}
		return array(
			'text' => "**VIN decoded**\n\n**Vehicle:** " . $result['vehicle'] . "\n**VIN:** " . $result['vin'] . $extra
				. "\n\nOpen the **Vehicle Catalog** to browse categories (filters, brakes, engine…) for this car.",
			'links' => array(
				array('label' => 'Open vehicle catalog', 'url' => $result['catalog_url']),
			),
		);
	}

	$ba = epc_agent_extract_brand_article($message);
	$brand = $ba['brand'];
	$article = $ba['article'];

	$vehicle_query = epc_agent_extract_vehicle_query($message);
	if (!empty($vehicle_query['is_query']) && !($brand !== '' && $article !== '')) {
		return epc_agent_reply_vehicle_part($db, $DP_Config, $vehicle_query, $session);
	}

	$wants_market = (bool)preg_match('/\b(best|market|country|export|recommend|suitable|popular)\b/i', $message)
		&& !epc_agent_is_part_brand_question($message);
	if ($wants_market && $country_code === '' && $country_name === '') {
		return array(
			'text' => "Which **country / market** are you buying for?\n\nWe track export demand for: **Sudan, Algeria, Kenya, Egypt, Nigeria, Saudi Arabia**.\n\nExample: *\"I'm from Sudan — best option for K20PRU?\"*",
			'suggestions' => array('I am from Sudan', 'I am from Kenya', 'I am from USA'),
		);
	}

	if ($wants_market && $country_code !== '') {
		$registry = epc_demand_country_registry();
		$known_market = isset($registry[$country_code]);
		if (!$known_market && in_array($country_code, array('USA', 'CAN'), true)) {
			if ($article !== '') {
				$hits = epc_agent_search_by_article($db, $DP_Config, $article, 5);
				if (!empty($hits)) {
					$lines = array();
					foreach ($hits as $h) {
						$lines[] = '• **' . $h['brand'] . ' ' . $h['article'] . '** — ' . epc_agent_format_stock_lines($h['lines']);
					}
					return array(
						'text' => "**" . $country_name . "** market tags are not set up yet in our system.\n\n"
							. "Here is what we **stock in UAE** for article **" . $article . "** (you can export to " . $country_name . "):\n\n"
							. implode("\n", $lines)
							. "\n\nConfirm export rules with our team before ordering.",
						'links' => epc_agent_standard_contact_links($DP_Config),
					);
				}
			}
			return array(
				'text' => "We don't have dedicated **" . $country_name . "** demand tags yet.\n\n"
					. "I can still check **UAE stock** by part number or VIN. Send the **brand + article** or ask our team about export to " . $country_name . ".",
				'links' => epc_agent_standard_contact_links($DP_Config),
			);
		}
		if ($brand !== '' && $article !== '') {
			$m = epc_agent_market_for_part($db, $DP_Config, $brand, $article, $country_code);
			$stock = epc_demand_anchor_stock_from_db($db, $brand, docpart_normalize_article_for_price($article));
			$url = epc_demand_chpu_part_url($DP_Config, $brand, $article);
			$tag_line = !empty($m['tagged_for_country'])
				? "✅ Tagged for **" . $country_name . "** export demand."
				: "⚠ Not tagged for **" . $country_name . "** yet — showing UAE stock anyway.";
			$stock_line = empty($stock) ? 'No UAE stock for this exact brand line.' : epc_agent_format_stock_lines($stock);
			$alt_lines = array();
			if (!empty($m['stats']['brand_rows'])) {
				foreach ($m['stats']['brand_rows'] as $row) {
					if (empty($row['country_codes']) || !in_array($country_code, $row['country_codes'], true)) {
						continue;
					}
					$alt_lines[] = '• **' . $row['brand'] . '** (also tagged ' . $country_name . ')';
					if (count($alt_lines) >= 4) {
						break;
					}
				}
			}
			$alt_text = empty($alt_lines) ? '' : "\n\n**Other brands** for same article tagged " . $country_name . ":\n" . implode("\n", $alt_lines);
			return array(
				'text' => "**Market: " . $country_name . "**\n**Part:** " . $brand . ' ' . $article . "\n\n" . $tag_line
					. "\n**UAE stock:** " . $stock_line . $alt_text,
				'links' => array(array('label' => 'View part & crosses', 'url' => $url)),
				'country_code' => $country_code,
			);
		}
		if ($known_market) {
			$overview = epc_agent_market_overview($db, $DP_Config, $country_code, 6);
			if (empty($overview['ok']) || empty($overview['parts'])) {
				return array(
					'text' => "No tagged in-stock lines for **" . $country_name . "** yet.\n\nSend a **part number** or **vehicle + part type** and I'll search UAE stock.",
				);
			}
			$lines = array();
			foreach ($overview['parts'] as $p) {
				$lines[] = '• **' . $p['brand'] . ' ' . $p['article'] . '** — ' . epc_agent_format_stock_lines(array(
					array('warehouse' => $p['warehouse'] ?? '', 'qty' => $p['qty'] ?? 0, 'price' => $p['price'] ?? ''),
				));
			}
			return array(
				'text' => "**Top in-stock parts tagged for " . $country_name . ":**\n\n" . implode("\n", $lines)
					. "\n\nSend a specific **part number** for a detailed cross-reference check.",
				'country_code' => $country_code,
			);
		}
	}

	if ($brand !== '' && $article !== '') {
		$stock = epc_demand_anchor_stock_from_db($db, $brand, docpart_normalize_article_for_price($article));
		$url = epc_demand_chpu_part_url($DP_Config, $brand, $article);
		if (empty($stock)) {
			$hits = epc_agent_search_by_article($db, $DP_Config, $article, 5);
			if (!empty($hits)) {
				$lines = array();
				foreach ($hits as $h) {
					$lines[] = '• **' . $h['brand'] . ' ' . $h['article'] . '** — ' . epc_agent_format_stock_lines($h['lines']);
				}
				$country_note = $country_name !== '' ? "\n\n*(Your market: " . $country_name . " — say \"best for my country\" for export tags.)*" : '';
				return array(
					'text' => "No stock for **" . $brand . " " . $article . "**, but same article is available under other brands:\n\n" . implode("\n", $lines) . $country_note,
					'links' => array(array('label' => 'Open ' . $hits[0]['brand'] . ' ' . $hits[0]['article'], 'url' => $hits[0]['url'])),
				);
			}
			return array(
				'text' => "No UAE stock found for **" . $brand . " " . $article . "**.\n\nTry a cross reference on the part page or contact our team to source it."
					. epc_agent_contact_text_block($DP_Config),
				'links' => epc_agent_standard_contact_links($DP_Config),
			);
		}
		$country_note = '';
		if ($country_code !== '' && isset(epc_demand_country_registry()[$country_code])) {
			$m = epc_agent_market_for_part($db, $DP_Config, $brand, $article, $country_code);
			$country_note = !empty($m['tagged_for_country'])
				? "\n\n✅ Tagged for export to **" . $country_name . "**."
				: "\n\nℹ Not yet tagged for **" . $country_name . "** — confirm with sales for your market.";
		}
		return array(
			'text' => "**" . $brand . " " . $article . "** — in stock:\n\n" . epc_agent_format_stock_lines($stock) . $country_note,
			'links' => array(array('label' => 'View part, crosses & fitment', 'url' => $url)),
		);
	}

	if ($article !== '') {
		$mfr_from_brand_query = epc_agent_try_manufacturer_instead_of_article($db, $message, $article, $brand);
		if ($mfr_from_brand_query !== '') {
			return epc_agent_reply_manufacturer_stock($db, $DP_Config, $mfr_from_brand_query);
		}

		$hits = epc_agent_search_by_article($db, $DP_Config, $article, 8);
		if (empty($hits)) {
			$mfr_fallback = epc_agent_find_manufacturer_in_db($db, $article);
			if ($mfr_fallback !== '' && preg_match('/\b(available|availability|stock|brand|parts?|spares?|have|carry)\b/i', $message)) {
				return epc_agent_reply_manufacturer_stock($db, $DP_Config, $article);
			}
			return array(
				'text' => "No stock for article **" . $article . "** in our UAE price lists.\n\nTry alternate spelling, a **VIN**, or contact our sales team."
					. epc_agent_contact_text_block($DP_Config),
				'links' => epc_agent_standard_contact_links($DP_Config),
			);
		}
		$lines = array();
		foreach ($hits as $h) {
			$lines[] = '• **' . $h['brand'] . ' ' . $h['article'] . '** — ' . number_format((float)$h['total_qty']) . ' pcs (' . epc_agent_format_stock_lines($h['lines']) . ')';
		}
		return array(
			'text' => "**Article " . $article . "** — brands in UAE stock:\n\n" . implode("\n", $lines),
			'links' => array(array('label' => 'Open top result', 'url' => $hits[0]['url'])),
		);
	}

	if (preg_match('/\b(vehicle|catalog|year|make|model|engine|browse)\b/i', $message)) {
		$lang = epc_agent_lang_href($DP_Config);
		return array(
			'text' => "Browse parts by **year → make → model → engine** in our Vehicle Catalog.\n\nOr send a **VIN** here and I'll decode it for you.",
			'links' => array(array('label' => 'Open vehicle catalog', 'url' => $lang . '/vehicle-catalog')),
		);
	}

	if ($country_name !== '') {
		return array(
			'text' => "Got it — your market is **" . $country_name . "**.\n\nNow send a **part number** (e.g. DENSO K20PRU) or **VIN / vehicle + part type** (e.g. oil filter Toyota Hilux 2018).",
			'country_code' => $country_code,
		);
	}

	return array(
		'text' => "I can help you find parts. Try one of these:\n\n"
			. "• **Part search:** `NGK 4195` or `DENSO K20PRU`\n"
			. "• **VIN:** paste 17-character VIN\n"
			. "• **Market:** \"I'm from Sudan — best brand for …\"\n"
			. "• **Vehicle catalog** for year/make/model browsing"
			. epc_agent_contact_text_block($DP_Config),
		'suggestions' => array('Do you have Bentley parts', 'NGK 4195', 'Decode VIN', 'WhatsApp contact'),
		'links' => epc_agent_standard_contact_links($DP_Config),
	);
}

function epc_agent_quick_action_message(string $action_id): string
{
	$map = array(
		'part' => 'I want to search by part number',
		'vin' => 'I want to decode a VIN',
		'market' => 'Which brand is best for my country?',
		'vehicle' => 'Help me browse parts by vehicle',
		'contact' => 'WhatsApp contact and part request',
		'platform' => 'Tell me about the ECOM AE platform',
		'industries' => 'Which industry templates do you support?',
		'pricing' => 'What are your pricing tiers?',
		'demo' => 'I want to book a demo sandbox',
		'products' => 'Help me find products',
		'orders' => 'I need help with an order',
		'delivery' => 'Tell me about delivery options',
	);
	return isset($map[$action_id]) ? $map[$action_id] : '';
}

function epc_agent_ensure_db_schema(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_parts_agent_session` (
			`session_id` VARCHAR(64) NOT NULL,
			`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`message_count` INT UNSIGNED NOT NULL DEFAULT 0,
			`country_code` VARCHAR(8) NOT NULL DEFAULT \'\',
			`country_name` VARCHAR(64) NOT NULL DEFAULT \'\',
			`last_user_text` TEXT,
			`last_agent_text` TEXT,
			`user_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`ip_hash` VARCHAR(64) NOT NULL DEFAULT \'\',
			`user_agent` VARCHAR(255) NOT NULL DEFAULT \'\',
			PRIMARY KEY (`session_id`),
			KEY `updated_at` (`updated_at`),
			KEY `country_code` (`country_code`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$db->exec(
		'CREATE TABLE IF NOT EXISTS `epc_parts_agent_message` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`session_id` VARCHAR(64) NOT NULL,
			`role` ENUM(\'user\',\'agent\') NOT NULL,
			`message_text` MEDIUMTEXT NOT NULL,
			`reply_links_json` TEXT,
			`created_at` INT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `session_id` (`session_id`),
			KEY `created_at` (`created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	epc_agent_schema_migrate($db);
	$done = true;
}

function epc_agent_schema_migrate(PDO $db): void
{
	static $migrated = false;
	if ($migrated) {
		return;
	}
	$columns = array(
		'client_ip' => "VARCHAR(45) NOT NULL DEFAULT ''",
		'ip_country_code' => "VARCHAR(8) NOT NULL DEFAULT ''",
		'ip_country_name' => "VARCHAR(64) NOT NULL DEFAULT ''",
	);
	foreach ($columns as $col => $def) {
		try {
			$db->exec('ALTER TABLE `epc_parts_agent_session` ADD COLUMN `' . $col . '` ' . $def);
		} catch (Throwable $e) {
			// Column already exists.
		}
	}
	$migrated = true;
}

function epc_agent_request_client_ip(): string
{
	$ip = '';
	if (!empty($_SERVER['HTTP_X_FORWARDED_FOR'])) {
		$parts = explode(',', (string)$_SERVER['HTTP_X_FORWARDED_FOR']);
		$ip = trim($parts[0]);
	} elseif (!empty($_SERVER['REMOTE_ADDR'])) {
		$ip = (string)$_SERVER['REMOTE_ADDR'];
	}
	if ($ip === '' || !filter_var($ip, FILTER_VALIDATE_IP)) {
		return '';
	}
	return substr($ip, 0, 45);
}

function epc_agent_request_ip_hash(): string
{
	$ip = epc_agent_request_client_ip();
	if ($ip === '') {
		return '';
	}
	return substr(hash('sha256', $ip . '|epc_agent'), 0, 32);
}

/**
 * @return array{code:string, name:string}
 */
function epc_agent_iso_country_names(): array
{
	return array(
		'AE' => 'United Arab Emirates', 'ARE' => 'United Arab Emirates',
		'SA' => 'Saudi Arabia', 'SAU' => 'Saudi Arabia',
		'SD' => 'Sudan', 'SDN' => 'Sudan',
		'DZ' => 'Algeria', 'DZA' => 'Algeria',
		'EG' => 'Egypt', 'EGY' => 'Egypt',
		'KE' => 'Kenya', 'KEN' => 'Kenya',
		'NG' => 'Nigeria', 'NGA' => 'Nigeria',
		'US' => 'United States', 'USA' => 'United States',
		'CA' => 'Canada', 'CAN' => 'Canada',
		'GB' => 'United Kingdom', 'UK' => 'United Kingdom',
		'IN' => 'India', 'PK' => 'Pakistan', 'BD' => 'Bangladesh',
		'QA' => 'Qatar', 'KW' => 'Kuwait', 'OM' => 'Oman', 'BH' => 'Bahrain',
		'JO' => 'Jordan', 'LB' => 'Lebanon', 'IQ' => 'Iraq', 'IR' => 'Iran',
		'TR' => 'Turkey', 'DE' => 'Germany', 'FR' => 'France', 'IT' => 'Italy',
		'RU' => 'Russia', 'CN' => 'China', 'JP' => 'Japan', 'AU' => 'Australia',
	);
}

function epc_agent_iso_country_name(string $code): string
{
	$code = strtoupper(trim($code));
	if ($code === '') {
		return '';
	}
	$map = epc_agent_iso_country_names();
	if (isset($map[$code])) {
		return (string)$map[$code];
	}
	$registry = epc_demand_country_registry();
	if (isset($registry[$code]['name'])) {
		return (string)$registry[$code]['name'];
	}
	return '';
}

/**
 * @return array{code:string, name:string}
 */
function epc_agent_request_ip_geo(): array
{
	$code = '';
	if (!empty($_SERVER['HTTP_CF_IPCOUNTRY'])) {
		$code = strtoupper(substr(trim((string)$_SERVER['HTTP_CF_IPCOUNTRY']), 0, 8));
	}
	if ($code === '' && !empty($_COOKIE['epc_country'])) {
		$code = strtoupper(substr(trim((string)$_COOKIE['epc_country']), 0, 8));
	}
	if ($code === 'XX' || $code === 'T1') {
		$code = '';
	}
	$name = epc_agent_iso_country_name($code);
	return array('code' => $code, 'name' => $name);
}

/**
 * Resolve visitor country from IP (cached). Used when cookie / CF headers are missing.
 *
 * @return array{code:string, name:string}
 */
function epc_agent_lookup_ip_country(string $ip): array
{
	static $memory = array();
	$ip = trim($ip);
	if ($ip === '') {
		return array('code' => '', 'name' => '');
	}
	if (isset($memory[$ip])) {
		return $memory[$ip];
	}
	if (!filter_var($ip, FILTER_VALIDATE_IP)) {
		return $memory[$ip] = array('code' => '', 'name' => '');
	}
	if (filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE) === false) {
		return $memory[$ip] = array('code' => '', 'name' => '');
	}

	$cache_dir = epc_agent_session_dir() . DIRECTORY_SEPARATOR . 'ip_geo_cache';
	if (!is_dir($cache_dir)) {
		@mkdir($cache_dir, 0700, true);
	}
	$cache_file = $cache_dir . DIRECTORY_SEPARATOR . hash('sha256', $ip) . '.json';
	if (is_file($cache_file) && (time() - (int)@filemtime($cache_file)) < 2592000) {
		$cached = json_decode((string)@file_get_contents($cache_file), true);
		if (is_array($cached) && !empty($cached['code'])) {
			return $memory[$ip] = array(
				'code' => strtoupper((string)$cached['code']),
				'name' => (string)($cached['name'] ?? ''),
			);
		}
	}

	$url = 'https://ipapi.co/' . rawurlencode($ip) . '/json/';
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => 2,
			'ignore_errors' => true,
			'header' => "User-Agent: ePartsCart-Agent/1.0\r\nAccept: application/json\r\n",
		),
		'ssl' => array(
			'verify_peer' => true,
			'verify_peer_name' => true,
		),
	));
	$raw = @file_get_contents($url, false, $ctx);
	$code = '';
	$name = '';
	if ($raw !== false && $raw !== '') {
		$data = json_decode($raw, true);
		if (is_array($data) && empty($data['error']) && !empty($data['country_code'])) {
			$code = strtoupper(substr((string)$data['country_code'], 0, 8));
			$name = trim((string)($data['country_name'] ?? ''));
		}
	}
	if ($code !== '' && $name === '') {
		$name = epc_agent_iso_country_name($code);
	}
	$result = array('code' => $code, 'name' => $name);
	if ($code !== '') {
		@file_put_contents($cache_file, json_encode($result, JSON_UNESCAPED_UNICODE), LOCK_EX);
	}
	return $memory[$ip] = $result;
}

/**
 * @param array<string, mixed> $session
 * @param array<string, mixed>|null $user_extra
 * @return array{primary:string, visitor:string, profile:string, market:string, parts:array<int,string>}
 */
function epc_agent_cp_country_info(array $session, ?array $user_extra = null): array
{
	$market_name = trim((string)($session['country_name'] ?? ''));
	$market_code = trim((string)($session['country_code'] ?? ''));
	if ($market_name === '' && $market_code !== '') {
		$registry = epc_demand_country_registry();
		$market_name = isset($registry[$market_code]['name'])
			? (string)$registry[$market_code]['name']
			: epc_agent_iso_country_name($market_code);
		if ($market_name === '') {
			$market_name = $market_code;
		}
	}

	$profile_name = '';
	if ($user_extra && !empty($user_extra['demand_country_name'])) {
		$profile_name = (string)$user_extra['demand_country_name'];
	}

	$visitor_name = trim((string)($session['ip_country_name'] ?? ''));
	$visitor_code = trim((string)($session['ip_country_code'] ?? ''));
	if ($visitor_name === '' && $visitor_code !== '') {
		$visitor_name = epc_agent_iso_country_name($visitor_code);
		if ($visitor_name === '') {
			$visitor_name = $visitor_code;
		}
	}

	$user_id = (int)($session['user_id'] ?? 0);
	$primary = '';
	if ($user_id > 0) {
		if ($profile_name !== '') {
			$primary = $profile_name;
		} elseif ($market_name !== '') {
			$primary = $market_name;
		} elseif ($visitor_name !== '') {
			$primary = $visitor_name;
		}
	} else {
		if ($visitor_name !== '') {
			$primary = $visitor_name;
		} elseif ($market_name !== '') {
			$primary = $market_name;
		}
	}

	$parts = array();
	if ($visitor_name !== '') {
		$parts[] = $visitor_name . ' (visitor IP)';
	}
	if ($profile_name !== '' && $profile_name !== $visitor_name) {
		$parts[] = $profile_name . ' (account)';
	}
	if ($market_name !== '' && $market_name !== $profile_name && $market_name !== $visitor_name) {
		$parts[] = $market_name . ' (chat market)';
	}
	if (!$parts && $primary !== '') {
		$parts[] = $primary;
	}

	return array(
		'primary' => $primary,
		'visitor' => $visitor_name,
		'profile' => $profile_name,
		'market' => $market_name,
		'parts' => $parts,
	);
}

/**
 * Fill missing geo on CP rows (updates DB when resolved).
 *
 * @param array<int, array<string, mixed>> $sessions
 */
function epc_agent_cp_backfill_guest_geo(PDO $db, array &$sessions, int $max_lookups = 15): void
{
	if (!$sessions) {
		return;
	}
	$update = $db->prepare(
		'UPDATE `epc_parts_agent_session`
		 SET `ip_country_code` = ?, `ip_country_name` = ?
		 WHERE `session_id` = ? AND (`ip_country_code` = \'\' OR `ip_country_name` = \'\')'
	);
	$lookups = 0;
	foreach ($sessions as &$row) {
		$code = trim((string)($row['ip_country_code'] ?? ''));
		$name = trim((string)($row['ip_country_name'] ?? ''));
		if ($code !== '' && $name !== '') {
			continue;
		}
		if ($code !== '' && $name === '') {
			$row['ip_country_name'] = epc_agent_iso_country_name($code);
			if ($row['ip_country_name'] !== '') {
				$update->execute(array($code, $row['ip_country_name'], $row['session_id']));
			}
			continue;
		}
		$ip = trim((string)($row['client_ip'] ?? ''));
		if ($ip === '' || $lookups >= $max_lookups) {
			continue;
		}
		$geo = epc_agent_lookup_ip_country($ip);
		$lookups++;
		if ($geo['code'] === '') {
			continue;
		}
		$row['ip_country_code'] = $geo['code'];
		$row['ip_country_name'] = $geo['name'];
		$update->execute(array($geo['code'], $geo['name'], $row['session_id']));
	}
	unset($row);
}

function epc_agent_cp_json_encode($payload): string
{
	$flags = JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES;
	if (defined('JSON_INVALID_UTF8_SUBSTITUTE')) {
		$flags |= JSON_INVALID_UTF8_SUBSTITUTE;
	}
	$json = json_encode($payload, $flags);
	if ($json === false) {
		return json_encode(array('status' => false, 'message' => 'JSON encode failed'), JSON_UNESCAPED_UNICODE);
	}
	return $json;
}

/**
 * Persist one chat turn to MySQL for CP review.
 *
 * @param array<string, mixed> $reply
 * @param array<string, mixed> $meta
 */
function epc_agent_persist_turn(PDO $db, string $session_id, array $session, string $user_message, array $reply, array $meta = array()): void
{
	if ($session_id === '') {
		return;
	}
	epc_agent_ensure_db_schema($db);

	$now = time();
	$created = (int)($session['created'] ?? $now);
	$country_code = (string)($session['country_code'] ?? '');
	$country_name = (string)($session['country_name'] ?? '');
	if (!empty($reply['country_code'])) {
		$country_code = (string)$reply['country_code'];
	}
	if (!empty($reply['country_name'])) {
		$country_name = (string)$reply['country_name'];
	}
	$user_id = (int)($meta['user_id'] ?? 0);
	$ip_hash = (string)($meta['ip_hash'] ?? epc_agent_request_ip_hash());
	$client_ip = (string)($meta['client_ip'] ?? epc_agent_request_client_ip());
	$ip_country_code = (string)($meta['ip_country_code'] ?? '');
	$ip_country_name = (string)($meta['ip_country_name'] ?? '');
	if ($ip_country_code === '') {
		$geo = epc_agent_request_ip_geo();
		$ip_country_code = (string)$geo['code'];
		$ip_country_name = (string)$geo['name'];
	} elseif ($ip_country_name === '') {
		$ip_country_name = epc_agent_iso_country_name($ip_country_code);
	}
	if ($ip_country_code === '' && $client_ip !== '') {
		$lookup = epc_agent_lookup_ip_country($client_ip);
		$ip_country_code = (string)$lookup['code'];
		$ip_country_name = (string)$lookup['name'];
	}
	$user_agent = substr((string)($meta['user_agent'] ?? (isset($_SERVER['HTTP_USER_AGENT']) ? $_SERVER['HTTP_USER_AGENT'] : '')), 0, 255);
	$message_count = count($session['messages']);
	$last_user = $user_message;
	if ($last_user === '') {
		foreach (array_reverse($session['messages']) as $m) {
			if (($m['role'] ?? '') === 'user' && !empty($m['text'])) {
				$last_user = (string)$m['text'];
				break;
			}
		}
	}
	$last_agent = (string)($reply['text'] ?? '');

	$db->prepare(
		'INSERT INTO `epc_parts_agent_session`
		(`session_id`, `created_at`, `updated_at`, `message_count`, `country_code`, `country_name`,
		 `last_user_text`, `last_agent_text`, `user_id`, `ip_hash`, `user_agent`,
		 `client_ip`, `ip_country_code`, `ip_country_name`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE
		 `updated_at` = VALUES(`updated_at`),
		 `message_count` = VALUES(`message_count`),
		 `country_code` = VALUES(`country_code`),
		 `country_name` = VALUES(`country_name`),
		 `last_user_text` = IF(VALUES(`last_user_text`) <> \'\', VALUES(`last_user_text`), `last_user_text`),
		 `last_agent_text` = VALUES(`last_agent_text`),
		 `user_id` = IF(VALUES(`user_id`) > 0, VALUES(`user_id`), `user_id`),
		 `ip_hash` = IF(VALUES(`ip_hash`) <> \'\', VALUES(`ip_hash`), `ip_hash`),
		 `user_agent` = IF(VALUES(`user_agent`) <> \'\', VALUES(`user_agent`), `user_agent`),
		 `client_ip` = IF(VALUES(`client_ip`) <> \'\', VALUES(`client_ip`), `client_ip`),
		 `ip_country_code` = IF(VALUES(`ip_country_code`) <> \'\', VALUES(`ip_country_code`), `ip_country_code`),
		 `ip_country_name` = IF(VALUES(`ip_country_name`) <> \'\', VALUES(`ip_country_name`), `ip_country_name`)'
	)->execute(array(
		$session_id,
		$created,
		$now,
		$message_count,
		$country_code,
		$country_name,
		$last_user,
		$last_agent,
		$user_id,
		$ip_hash,
		$user_agent,
		$client_ip,
		$ip_country_code,
		$ip_country_name,
	));

	if ($user_message !== '') {
		$db->prepare(
			'INSERT INTO `epc_parts_agent_message` (`session_id`, `role`, `message_text`, `reply_links_json`, `created_at`)
			 VALUES (?, \'user\', ?, \'\', ?)'
		)->execute(array($session_id, $user_message, $now));
	}

	$links_json = '';
	if (!empty($reply['links']) && is_array($reply['links'])) {
		$links_json = json_encode($reply['links'], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	}
	$db->prepare(
		'INSERT INTO `epc_parts_agent_message` (`session_id`, `role`, `message_text`, `reply_links_json`, `created_at`)
		 VALUES (?, \'agent\', ?, ?, ?)'
	)->execute(array($session_id, $last_agent, $links_json, time()));
}

function epc_agent_cp_sync_file_sessions(PDO $db, int $max_files = 80): int
{
	epc_agent_ensure_db_schema($db);
	$dir = epc_agent_session_dir();
	if (!is_dir($dir)) {
		return 0;
	}
	$files = glob($dir . DIRECTORY_SEPARATOR . '*.json');
	if (!$files) {
		return 0;
	}
	usort($files, function ($a, $b) {
		return (int)@filemtime($b) - (int)@filemtime($a);
	});

	$count_stmt = $db->prepare('SELECT COUNT(*) FROM `epc_parts_agent_message` WHERE `session_id` = ?');
	$synced = 0;

	foreach (array_slice($files, 0, $max_files) as $file) {
		$session_id = preg_replace('/[^a-zA-Z0-9_\-]/', '', basename($file, '.json'));
		if ($session_id === '') {
			continue;
		}
		$raw = @file_get_contents($file);
		if ($raw === false || $raw === '') {
			continue;
		}
		$data = json_decode($raw, true);
		if (!is_array($data) || empty($data['messages']) || !is_array($data['messages'])) {
			continue;
		}
		$file_count = count($data['messages']);
		$count_stmt->execute(array($session_id));
		$db_count = (int)$count_stmt->fetchColumn();
		if ($db_count >= $file_count) {
			continue;
		}

		$session = array_merge(array(
			'country_code' => '',
			'country_name' => '',
			'messages' => array(),
			'created' => (int)@filemtime($file),
		), $data);

		if ($db_count === 0) {
			$db->prepare(
				'INSERT INTO `epc_parts_agent_session`
				(`session_id`, `created_at`, `updated_at`, `message_count`, `country_code`, `country_name`,
				 `last_user_text`, `last_agent_text`, `user_id`, `ip_hash`, `user_agent`)
				 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0, \'\', \'\')'
			)->execute(array(
				$session_id,
				(int)($session['created'] ?? time()),
				(int)($session['updated'] ?? time()),
				$file_count,
				(string)($session['country_code'] ?? ''),
				(string)($session['country_name'] ?? ''),
				'',
				'',
			));
		}

		$existing = $db_count;
		$slice = array_slice($data['messages'], $existing);
		foreach ($slice as $m) {
			if (!is_array($m) || empty($m['text'])) {
				continue;
			}
			$role = (($m['role'] ?? '') === 'user') ? 'user' : 'agent';
			$t = (int)($m['t'] ?? time());
			$db->prepare(
				'INSERT INTO `epc_parts_agent_message` (`session_id`, `role`, `message_text`, `reply_links_json`, `created_at`)
				 VALUES (?, ?, ?, \'\', ?)'
			)->execute(array($session_id, $role, (string)$m['text'], $t));
		}

		$last_user = '';
		$last_agent = '';
		foreach (array_reverse($data['messages']) as $m) {
			if (!is_array($m) || empty($m['text'])) {
				continue;
			}
			if (($m['role'] ?? '') === 'user' && $last_user === '') {
				$last_user = (string)$m['text'];
			}
			if (($m['role'] ?? '') === 'agent' && $last_agent === '') {
				$last_agent = (string)$m['text'];
			}
		}
		$db->prepare(
			'UPDATE `epc_parts_agent_session` SET
			 `updated_at` = ?, `message_count` = ?, `country_code` = ?, `country_name` = ?,
			 `last_user_text` = ?, `last_agent_text` = ?
			 WHERE `session_id` = ?'
		)->execute(array(
			(int)($session['updated'] ?? time()),
			$file_count,
			(string)($session['country_code'] ?? ''),
			(string)($session['country_name'] ?? ''),
			$last_user,
			$last_agent,
			$session_id,
		));
		$synced++;
	}

	return $synced;
}

/**
 * @param array<string, mixed> $filters
 * @return array{sessions:array, total:int}
 */
function epc_agent_cp_list_sessions(PDO $db, array $filters = array(), int $limit = 50, int $offset = 0): array
{
	epc_agent_ensure_db_schema($db);
	$limit = max(1, min(200, $limit));
	$offset = max(0, $offset);

	$where = array('1=1');
	$params = array();

	if (!empty($filters['date_from'])) {
		$where[] = '`updated_at` >= ?';
		$params[] = (int)$filters['date_from'];
	}
	if (!empty($filters['date_to'])) {
		$where[] = '`updated_at` <= ?';
		$params[] = (int)$filters['date_to'];
	}
	if (!empty($filters['q'])) {
		$q = '%' . (string)$filters['q'] . '%';
		$where[] = '(`session_id` LIKE ? OR `last_user_text` LIKE ? OR `last_agent_text` LIKE ? OR `client_ip` LIKE ?)';
		$params[] = $q;
		$params[] = $q;
		$params[] = $q;
		$params[] = $q;
	}

	$where_sql = implode(' AND ', $where);
	$count_stmt = $db->prepare('SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE ' . $where_sql);
	$count_stmt->execute($params);
	$total = (int)$count_stmt->fetchColumn();

	$list_stmt = $db->prepare(
		'SELECT `session_id`, `created_at`, `updated_at`, `message_count`, `country_code`, `country_name`,
		 `last_user_text`, `last_agent_text`, `user_id`, `ip_hash`, `user_agent`,
		 `client_ip`, `ip_country_code`, `ip_country_name`
		 FROM `epc_parts_agent_session`
		 WHERE ' . $where_sql . '
		 ORDER BY `updated_at` DESC
		 LIMIT ' . (int)$limit . ' OFFSET ' . (int)$offset
	);
	$list_stmt->execute($params);
	$sessions = $list_stmt->fetchAll(PDO::FETCH_ASSOC);
	$sessions = is_array($sessions) ? $sessions : array();
	epc_agent_cp_backfill_guest_geo($db, $sessions);
	$sessions = epc_agent_cp_enrich_sessions($db, $sessions);

	return array('sessions' => $sessions, 'total' => $total);
}

/**
 * @param array<int, array<string, mixed>> $sessions
 * @return array<int, array<string, mixed>>
 */
function epc_agent_cp_enrich_sessions(PDO $db, array $sessions): array
{
	if (!$sessions) {
		return array();
	}

	$user_ids = array();
	foreach ($sessions as $row) {
		$uid = (int)($row['user_id'] ?? 0);
		if ($uid > 0) {
			$user_ids[$uid] = $uid;
		}
	}

	$users_map = array();
	if ($user_ids) {
		$ids = array_values($user_ids);
		$placeholders = implode(',', array_fill(0, count($ids), '?'));
		$user_stmt = $db->prepare(
			'SELECT `user_id`, `email`, `phone` FROM `users` WHERE `user_id` IN (' . $placeholders . ')'
		);
		$user_stmt->execute($ids);
		while ($u = $user_stmt->fetch(PDO::FETCH_ASSOC)) {
			$users_map[(int)$u['user_id']] = $u;
		}

		$name_stmt = $db->prepare(
			'SELECT `user_id`, `data_key`, `data_value`
			 FROM `users_profiles`
			 WHERE `user_id` IN (' . $placeholders . ')
			 AND `data_key` IN (\'name\', \'fio\', \'company\', \'surname\', \'firstname\')'
		);
		$name_stmt->execute($ids);
		$names = array();
		$priority = array('name' => 1, 'fio' => 2, 'company' => 3, 'firstname' => 4, 'surname' => 5);
		while ($p = $name_stmt->fetch(PDO::FETCH_ASSOC)) {
			$uid = (int)$p['user_id'];
			$key = (string)$p['data_key'];
			$val = trim(html_entity_decode(strip_tags((string)$p['data_value']), ENT_QUOTES, 'UTF-8'));
			if ($val === '') {
				continue;
			}
			if (!isset($names[$uid]) || ($priority[$key] ?? 99) < ($priority[$names[$uid]['key']] ?? 99)) {
				$names[$uid] = array('key' => $key, 'value' => $val);
			}
		}
		foreach ($names as $uid => $name_row) {
			if (isset($users_map[$uid])) {
				$users_map[$uid]['display_name'] = $name_row['value'];
			}
		}

		$registry = epc_demand_country_registry();
		try {
			epc_demand_ensure_schema($db);
			$demand_stmt = $db->prepare(
				'SELECT `user_id`, `country_code` FROM `epc_user_demand_country` WHERE `user_id` IN (' . $placeholders . ')'
			);
			$demand_stmt->execute($ids);
			while ($d = $demand_stmt->fetch(PDO::FETCH_ASSOC)) {
				$uid = (int)$d['user_id'];
				$dcode = strtoupper(trim((string)$d['country_code']));
				if ($dcode === '' || !isset($users_map[$uid])) {
					continue;
				}
				$dname = isset($registry[$dcode]['name'])
					? (string)$registry[$dcode]['name']
					: epc_agent_iso_country_name($dcode);
				if ($dname === '') {
					$dname = $dcode;
				}
				$users_map[$uid]['demand_country_code'] = $dcode;
				$users_map[$uid]['demand_country_name'] = $dname;
			}
		} catch (Throwable $e) {
		}

		$profile_keys = epc_demand_user_country_profile_keys();
		if ($profile_keys) {
			$key_ph = implode(',', array_fill(0, count($profile_keys), '?'));
			$profile_country_stmt = $db->prepare(
				'SELECT `user_id`, `data_key`, `data_value`
				 FROM `users_profiles`
				 WHERE `user_id` IN (' . $placeholders . ') AND `data_key` IN (' . $key_ph . ')'
			);
			$profile_country_stmt->execute(array_merge($ids, $profile_keys));
			$key_rank = array_flip($profile_keys);
			$profile_codes = array();
			while ($pc = $profile_country_stmt->fetch(PDO::FETCH_ASSOC)) {
				$uid = (int)$pc['user_id'];
				if (!isset($users_map[$uid]) || !empty($users_map[$uid]['demand_country_name'])) {
					continue;
				}
				$pkey = (string)$pc['data_key'];
				$code = epc_demand_normalize_user_country_value((string)$pc['data_value']);
				if ($code === '') {
					continue;
				}
				if (!isset($profile_codes[$uid]) || ($key_rank[$pkey] ?? 99) < ($key_rank[$profile_codes[$uid]['key']] ?? 99)) {
					$profile_codes[$uid] = array('key' => $pkey, 'code' => $code);
				}
			}
			foreach ($profile_codes as $uid => $pc_row) {
				if (!empty($users_map[$uid]['demand_country_name'])) {
					continue;
				}
				$dcode = $pc_row['code'];
				$dname = isset($registry[$dcode]['name'])
					? (string)$registry[$dcode]['name']
					: epc_agent_iso_country_name($dcode);
				if ($dname === '') {
					$dname = $dcode;
				}
				$users_map[$uid]['demand_country_code'] = $dcode;
				$users_map[$uid]['demand_country_name'] = $dname;
			}
		}
	}

	foreach ($sessions as &$row) {
		$user_extra = null;
		$uid = (int)($row['user_id'] ?? 0);
		if ($uid > 0 && isset($users_map[$uid])) {
			$user_extra = $users_map[$uid];
		}
		$row['country'] = epc_agent_cp_country_info($row, $user_extra);
		$row['customer'] = epc_agent_cp_format_customer($row, $users_map);
	}
	unset($row);

	return $sessions;
}

/**
 * @param array<string, mixed> $session
 * @param array<int, array<string, mixed>> $users_map
 */
function epc_agent_cp_format_customer(array $session, array $users_map = array()): array
{
	$user_id = (int)($session['user_id'] ?? 0);
	$user_extra = ($user_id > 0 && isset($users_map[$user_id])) ? $users_map[$user_id] : null;
	$country = isset($session['country']) && is_array($session['country'])
		? $session['country']
		: epc_agent_cp_country_info($session, $user_extra);

	if ($user_id > 0) {
		$user = $user_extra;
		$name = ($user && !empty($user['display_name'])) ? (string)$user['display_name'] : '';
		$email = ($user && !empty($user['email'])) ? (string)$user['email'] : '';
		$phone = ($user && !empty($user['phone'])) ? (string)$user['phone'] : '';
		$parts = array();
		if ($name !== '') {
			$parts[] = $name;
		}
		$parts[] = 'ID ' . $user_id;
		if ($email !== '') {
			$parts[] = $email;
		}
		if ($phone !== '') {
			$parts[] = $phone;
		}
		if (!empty($country['primary'])) {
			$parts[] = 'Country: ' . $country['primary'];
		}
		return array(
			'type' => 'user',
			'label' => implode(' · ', $parts),
			'name' => $name,
			'email' => $email,
			'phone' => $phone,
			'user_id' => $user_id,
			'country_label' => (string)($country['primary'] ?? ''),
			'country_parts' => isset($country['parts']) ? $country['parts'] : array(),
			'ip_country_name' => (string)($country['visitor'] ?? ''),
			'profile_country_name' => (string)($country['profile'] ?? ''),
			'market_country_name' => (string)($country['market'] ?? ''),
		);
	}

	$ip = trim((string)($session['client_ip'] ?? ''));
	$ip_name = (string)($country['visitor'] ?? '');
	$ip_code = trim((string)($session['ip_country_code'] ?? ''));
	$parts = array('Guest');
	if (!empty($country['primary'])) {
		$parts[] = $country['primary'];
	} elseif ($ip_name !== '') {
		$parts[] = $ip_name;
	} elseif ($ip_code !== '') {
		$parts[] = $ip_code;
	}
	if ($ip !== '') {
		$parts[] = 'IP ' . $ip;
	}
	if (count($parts) === 1) {
		$parts[] = 'Unknown location';
	}
	return array(
		'type' => 'guest',
		'label' => implode(' · ', $parts),
		'ip' => $ip,
		'ip_country_code' => $ip_code,
		'ip_country_name' => $ip_name,
		'country_label' => (string)($country['primary'] ?? ''),
		'country_parts' => isset($country['parts']) ? $country['parts'] : array(),
		'profile_country_name' => '',
		'market_country_name' => (string)($country['market'] ?? ''),
		'user_id' => 0,
	);
}

/**
 * @return array{session:array|null, messages:array}
 */
function epc_agent_cp_get_session(PDO $db, string $session_id): array
{
	epc_agent_ensure_db_schema($db);
	$safe = preg_replace('/[^a-zA-Z0-9_\-]/', '', $session_id);
	if ($safe === '') {
		return array('session' => null, 'messages' => array());
	}

	$stmt = $db->prepare(
		'SELECT `session_id`, `created_at`, `updated_at`, `message_count`, `country_code`, `country_name`,
		 `last_user_text`, `last_agent_text`, `user_id`, `ip_hash`, `user_agent`,
		 `client_ip`, `ip_country_code`, `ip_country_name`
		 FROM `epc_parts_agent_session` WHERE `session_id` = ? LIMIT 1'
	);
	$stmt->execute(array($safe));
	$session = $stmt->fetch(PDO::FETCH_ASSOC);

	$msg_stmt = $db->prepare(
		'SELECT `id`, `role`, `message_text`, `reply_links_json`, `created_at`
		 FROM `epc_parts_agent_message`
		 WHERE `session_id` = ?
		 ORDER BY `created_at` ASC, `id` ASC'
	);
	$msg_stmt->execute(array($safe));
	$messages = $msg_stmt->fetchAll(PDO::FETCH_ASSOC);

	if (!$session || empty($messages)) {
		$file_session = epc_agent_session_load($safe);
		if (!empty($file_session['messages']) && is_array($file_session['messages'])) {
			try {
				epc_agent_cp_sync_file_sessions($db, 200);
				$stmt->execute(array($safe));
				$session = $stmt->fetch(PDO::FETCH_ASSOC);
				$msg_stmt->execute(array($safe));
				$messages = $msg_stmt->fetchAll(PDO::FETCH_ASSOC);
			} catch (Throwable $e) {
			}
			if (!$session || empty($messages)) {
				$now = time();
				$session = array(
					'session_id' => $safe,
					'created_at' => (int) ($file_session['created'] ?? $now),
					'updated_at' => $now,
					'message_count' => count($file_session['messages']),
					'country_code' => (string) ($file_session['country_code'] ?? ''),
					'country_name' => (string) ($file_session['country_name'] ?? ''),
					'last_user_text' => '',
					'last_agent_text' => '',
					'user_id' => 0,
					'ip_hash' => '',
					'user_agent' => '',
					'client_ip' => (string) ($file_session['client_ip'] ?? ''),
					'ip_country_code' => (string) ($file_session['ip_country_code'] ?? ''),
					'ip_country_name' => (string) ($file_session['ip_country_name'] ?? ''),
				);
				$messages = array();
				foreach ($file_session['messages'] as $m) {
					$messages[] = array(
						'id' => 0,
						'role' => (($m['role'] ?? '') === 'user') ? 'user' : 'agent',
						'message_text' => (string) ($m['text'] ?? ''),
						'reply_links_json' => !empty($m['links']) ? json_encode($m['links'], JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) : '',
						'created_at' => (int) ($m['t'] ?? $now),
					);
				}
			}
		}
	}

	$session_row = $session ?: null;
	if ($session_row) {
		$rows = array($session_row);
		epc_agent_cp_backfill_guest_geo($db, $rows);
		$session_row = $rows[0];
		$enriched = epc_agent_cp_enrich_sessions($db, array($session_row));
		$session_row = $enriched[0];
	}

	return array(
		'session' => $session_row,
		'messages' => is_array($messages) ? $messages : array(),
	);
}

/**
 * @return array<string, mixed>
 */
function epc_agent_cp_stats(PDO $db): array
{
	epc_agent_ensure_db_schema($db);
	$today_start = strtotime('today');

	$total = (int)$db->query('SELECT COUNT(*) FROM `epc_parts_agent_session`')->fetchColumn();
	$sessions_today = (int)$db->query('SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE `updated_at` >= ' . (int)$today_start)->fetchColumn();
	$messages_today = (int)$db->query('SELECT COUNT(*) FROM `epc_parts_agent_message` WHERE `created_at` >= ' . (int)$today_start)->fetchColumn();
	$logged_in = (int)$db->query('SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE `user_id` > 0')->fetchColumn();
	$guests = max(0, $total - $logged_in);

	return array(
		'total_sessions' => $total,
		'sessions_today' => $sessions_today,
		'messages_today' => $messages_today,
		'logged_in_sessions' => $logged_in,
		'guest_sessions' => $guests,
	);
}

/**
 * Chat transcript for widget restore (DB first, then temp file session).
 *
 * @return array{ok:bool, session_id:string, messages:array, country_code:string, country_name:string}
 */
function epc_agent_get_session_history(PDO $db, string $session_id): array
{
	$safe = preg_replace('/[^a-zA-Z0-9_\-]/', '', $session_id);
	if ($safe === '') {
		return array('ok' => false, 'session_id' => '', 'messages' => array(), 'country_code' => '', 'country_name' => '');
	}

	$file = epc_agent_session_load($safe);
	$detail = epc_agent_cp_get_session($db, $safe);
	$out = array();

	if (!empty($detail['messages']) && is_array($detail['messages'])) {
		foreach ($detail['messages'] as $m) {
			if (!is_array($m) || empty($m['message_text'])) {
				continue;
			}
			$links = array();
			if (!empty($m['reply_links_json'])) {
				$decoded = json_decode((string)$m['reply_links_json'], true);
				if (is_array($decoded)) {
					$links = $decoded;
				}
			}
			$out[] = array(
				'role' => (($m['role'] ?? '') === 'user') ? 'user' : 'agent',
				'text' => (string)$m['message_text'],
				'links' => $links,
			);
		}
	}

	if (empty($out) && !empty($file['messages']) && is_array($file['messages'])) {
		foreach ($file['messages'] as $m) {
			if (!is_array($m) || empty($m['text'])) {
				continue;
			}
			$out[] = array(
				'role' => (($m['role'] ?? '') === 'user') ? 'user' : 'agent',
				'text' => (string)$m['text'],
				'links' => (isset($m['links']) && is_array($m['links'])) ? $m['links'] : array(),
			);
		}
	}

	$country_code = (string)($detail['session']['country_code'] ?? $file['country_code'] ?? '');
	$country_name = (string)($detail['session']['country_name'] ?? $file['country_name'] ?? '');

	return array(
		'ok' => true,
		'session_id' => $safe,
		'messages' => $out,
		'country_code' => $country_code,
		'country_name' => $country_name,
	);
}
