<?php
/**
 * Platform PartsAPI (TECDOC) config — API key server-side only.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_partsapi_file_config(): array
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}
	$cached = array();
	$file = $_SERVER['DOCUMENT_ROOT'] . '/config.epc-partsapi.php';
	if (is_file($file)) {
		$cfg = require $file;
		if (is_array($cfg)) {
			$cached = $cfg;
		}
	}
	return $cached;
}

function epc_partsapi_api_base_url(): string
{
	$file = epc_partsapi_file_config();
	$base = trim((string) ($file['api_base_url'] ?? ''));
	return $base !== '' ? rtrim($base, '/') : 'https://api.partsapi.ru';
}

function epc_partsapi_resolve_key(): string
{
	return epc_partsapi_resolve_key_for_method('');
}

function epc_partsapi_resolve_key_for_method(string $method = ''): string
{
	$file = epc_partsapi_file_config();
	$method = trim($method);
	if ($method !== '') {
		$methodKeys = $file['method_keys'] ?? array();
		if (is_array($methodKeys) && !empty($methodKeys[$method])) {
			$key = trim((string) $methodKeys[$method]);
			if ($key !== '') {
				return $key;
			}
		}
	}
	return trim((string) ($file['api_key'] ?? ''));
}

function epc_partsapi_method_catalog(): array
{
	return array(
		'getMakes' => array('label' => 'Vehicle makes', 'shop_path' => '/account/shop?method=getMakes', 'probe' => array('carType' => 'PC', 'lang' => 'en')),
		'getModels' => array('label' => 'Vehicle models', 'shop_path' => '/account/shop?method=getModels', 'probe' => array('carType' => 'PC', 'makeId' => 16, 'lang' => 'en')),
		'getCars' => array('label' => 'Vehicle modifications', 'shop_path' => '/account/shop?method=getCars', 'probe' => array('carType' => 'PC', 'makeId' => 16, 'modelId' => 5114, 'lang' => 'en')),
		'getSearchTree' => array('label' => 'Product categories', 'shop_path' => '/account/shop?method=getSearchTree', 'probe' => array('carType' => 'PC', 'carId' => 58963, 'lang' => 'en')),
		'getArticles' => array('label' => 'Catalog articles', 'shop_path' => '/account/shop?method=getArticles', 'probe' => array('carType' => 'PC', 'carId' => 58963, 'strId' => 100260, 'lang' => 'en')),
		'searchArticles' => array('label' => 'Part number search', 'shop_path' => '/account/shop?method=searchArticles', 'probe' => array('SEARCH_NUMBER' => '1J0971972', 'LANG' => 'en')),
		'PartSuggest' => array('label' => 'Part suggestions', 'shop_path' => '/account/shop?method=PartSuggest', 'probe' => array('oem' => '1J0971972')),
		'getCrosses' => array('label' => 'Cross references', 'shop_path' => '/account/shop?method=getCrosses', 'probe' => array('number' => '1J0971972')),
		'getCrossesWithBrand' => array('label' => 'Cross references (with brand)', 'shop_path' => '/account/shop?method=getCrossesWithBrand', 'probe' => array('number' => '1J0971972', 'brand' => 'VAG')),
		'tecdocCrosses' => array('label' => 'TecDoc crosses', 'shop_path' => '/account/shop?method=tecdocCrosses', 'probe' => array('number' => '1J0971972', 'brand' => 'VAG')),
		'VINdecode' => array('label' => 'VIN decode', 'shop_path' => '/account/shop?method=VINdecode', 'probe' => array('vin' => 'WVWZZZ1KZAW123456', 'lang' => 'en')),
	);
}

function epc_partsapi_method_shop_path(string $method = ''): string
{
	$method = trim($method);
	if ($method === '') {
		return '/account/shop';
	}
	$catalog = epc_partsapi_method_catalog();
	$path = trim((string) ($catalog[$method]['shop_path'] ?? ''));
	if ($path !== '') {
		return $path;
	}
	return '/account/shop?method=' . rawurlencode($method);
}

function epc_partsapi_proxy_action_method(string $action): string
{
	$action = strtolower($action);
	$map = array(
		'manufacturers' => 'getMakes',
		'models' => 'getModels',
		'modifications' => 'getCars',
		'cars' => 'getCars',
		'categories' => 'getSearchTree',
		'articles' => 'getArticles',
		'products' => 'getArticles',
		'part_search' => 'searchArticles',
		'search' => 'searchArticles',
		'crosses' => 'getCrosses',
		'analogs' => 'getCrosses',
		'vin' => 'VINdecode',
	);
	return $map[$action] ?? '';
}

function epc_partsapi_shop_url(string $method = ''): string
{
	$file = epc_partsapi_file_config();
	$method = trim($method);
	if ($method !== '') {
		$overrides = $file['method_shop_urls'] ?? array();
		if (is_array($overrides) && !empty($overrides[$method])) {
			$url = trim((string) $overrides[$method]);
			if ($url !== '') {
				return $url;
			}
		}
	}
	// Per-method deep links: partsapi.ru login preserves ?method= in next= redirect.
	$path = epc_partsapi_method_shop_path($method);
	if (preg_match('/^https?:\/\//i', $path)) {
		return $path;
	}
	return 'https://partsapi.ru' . ($path !== '' ? $path : '/account/shop');
}

function epc_partsapi_method_shop_client_map(): array
{
	$out = array();
	foreach (epc_partsapi_method_catalog() as $method => $meta) {
		$out[$method] = array(
			'label' => (string) ($meta['label'] ?? $method),
			'shop_url' => epc_partsapi_shop_url($method),
		);
	}
	return $out;
}

function epc_partsapi_action_shop_client_map(): array
{
	$out = array();
	$actions = array('manufacturers', 'models', 'modifications', 'cars', 'categories', 'articles', 'products', 'part_search', 'search', 'crosses', 'analogs', 'vin');
	foreach ($actions as $action) {
		$method = epc_partsapi_proxy_action_method($action);
		if ($method === '') {
			continue;
		}
		$catalog = epc_partsapi_method_catalog();
		$out[$action] = array(
			'method' => $method,
			'label' => (string) ($catalog[$method]['label'] ?? $method),
			'shop_url' => epc_partsapi_shop_url($method),
		);
	}
	return $out;
}

function epc_partsapi_error_code(array $result): int
{
	$data = $result['data'] ?? null;
	if (is_array($data) && isset($data['error_code'])) {
		return (int) $data['error_code'];
	}
	return 0;
}

function epc_partsapi_error_message(array $result): string
{
	$data = $result['data'] ?? null;
	if (is_array($data) && !empty($data['message'])) {
		return is_array($data['message']) ? implode('; ', $data['message']) : (string) $data['message'];
	}
	return (string) ($result['error'] ?? '');
}

function epc_partsapi_message_has_rate_limit(string $message): bool
{
	$message = strtolower($message);
	return $message !== '' && (strpos($message, 'exceeded the number of requests') !== false || strpos($message, 'rate limit') !== false);
}

function epc_partsapi_is_rate_limit_error(array $result): bool
{
	if (epc_partsapi_error_code($result) === 5000) {
		return true;
	}
	return epc_partsapi_message_has_rate_limit(epc_partsapi_error_message($result));
}

function epc_partsapi_is_service_error(array $result): bool
{
	$code = epc_partsapi_error_code($result);
	return in_array($code, array(5005, 5007), true);
}

function epc_partsapi_is_auth_key_error(array $result): bool
{
	if (epc_partsapi_is_rate_limit_error($result) || epc_partsapi_is_service_error($result)) {
		return false;
	}
	$code = epc_partsapi_error_code($result);
	if ($code === 5002) {
		return true;
	}
	if ($code === 5000 || $code === 5005 || $code === 5007) {
		return false;
	}
	$message = strtolower(epc_partsapi_error_message($result));
	if ($message !== '' && (strpos($message, 'authorization key') !== false || strpos($message, 'авторизац') !== false)) {
		return true;
	}
	$status = (int) ($result['http_status'] ?? 0);
	if ($status === 401 || $status === 403) {
		$method = trim((string) ($result['method'] ?? ''));
		if ($method !== '' && epc_partsapi_resolve_key_for_method($method) !== '') {
			return false;
		}
		return true;
	}
	return false;
}

function epc_partsapi_probe_status_meta(array $result, string $method = ''): array
{
	$method = trim($method);
	if ($method === '' && !empty($result['method'])) {
		$method = (string) $result['method'];
	}
	$keyConfigured = $method !== '' && epc_partsapi_resolve_key_for_method($method) !== '';
	$rateLimited = epc_partsapi_is_rate_limit_error($result);
	$serviceError = epc_partsapi_is_service_error($result);
	$authError = epc_partsapi_is_auth_key_error($result);
	return array(
		'error_code' => epc_partsapi_error_code($result),
		'rate_limited' => $rateLimited,
		'service_error' => $serviceError,
		'key_authenticated' => $keyConfigured && !$authError,
		'subscription_required' => !$result['ok'] && $authError,
	);
}

function epc_partsapi_subscription_message(string $action = ''): string
{
	$method = epc_partsapi_proxy_action_method($action);
	if ($method === '') {
		$method = trim($action);
	}
	$catalog = epc_partsapi_method_catalog();
	$label = $catalog[$method]['label'] ?? ($method !== '' ? $method : 'this API method');
	$shop = epc_partsapi_shop_url($method);
	return 'Subscribe to ' . $label . ' (' . ($method !== '' ? $method : 'method') . ') at ' . $shop . ' and add the method key to config.epc-partsapi.php under method_keys.';
}

function epc_partsapi_fail_payload(array $result, string $action = ''): array
{
	$method = epc_partsapi_proxy_action_method($action);
	if ($method === '' && !empty($result['method'])) {
		$method = (string) $result['method'];
	}
	$payload = array(
		'ok' => false,
		'error' => (string) ($result['error'] ?? 'Request failed.'),
		'http_status' => (int) ($result['http_status'] ?? 502),
		'elapsed_ms' => (int) ($result['elapsed_ms'] ?? 0),
	);
	if ($method !== '') {
		$payload['method'] = $method;
	}
	$meta = epc_partsapi_probe_status_meta($result, $method);
	$payload['error_code'] = $meta['error_code'];
	$payload['rate_limited'] = $meta['rate_limited'];
	$payload['service_error'] = $meta['service_error'];
	if (!empty($meta['subscription_required'])) {
		$payload['subscription_required'] = true;
		$payload['error'] = epc_partsapi_subscription_message($action);
		$payload['shop_url'] = epc_partsapi_shop_url($method);
	}
	return $payload;
}

function epc_partsapi_default_lang(): string
{
	$lang = strtolower(trim((string) (epc_partsapi_file_config()['default_lang'] ?? 'en')));
	return preg_match('/^[a-z]{2}$/', $lang) ? $lang : 'en';
}

function epc_partsapi_umapi_fallback_enabled(): bool
{
	$file = epc_partsapi_file_config();
	return !empty($file['umapi_fallback']) && epc_partsapi_resolve_key() !== '';
}

function epc_partsapi_enabled_for_request(): bool
{
	if (!function_exists('epc_portal_is_epartscart_hostname')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
	}
	if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
		return true;
	}
	if (function_exists('epc_portal_is_auto_parts_site') && epc_portal_is_auto_parts_site()) {
		return !empty(epc_partsapi_file_config()['allow_auto_parts_tenants']);
	}
	return false;
}

function epc_partsapi_credentials_configured(): bool
{
	if (epc_partsapi_resolve_key() !== '') {
		return true;
	}
	$methodKeys = epc_partsapi_file_config()['method_keys'] ?? array();
	if (!is_array($methodKeys)) {
		return false;
	}
	foreach ($methodKeys as $key) {
		if (trim((string) $key) !== '') {
			return true;
		}
	}
	return false;
}

function epc_partsapi_car_type_from_section(string $section): string
{
	$section = strtolower($section);
	if ($section === 'commercial') {
		return 'CV';
	}
	if ($section === 'motorbike') {
		return 'Motorcycle';
	}
	return 'PC';
}

function epc_partsapi_section_from_car_type(string $carType): string
{
	if ($carType === 'CV') {
		return 'commercial';
	}
	if ($carType === 'Motorcycle') {
		return 'motorbike';
	}
	return 'passenger';
}

function epc_partsapi_call(string $method, array $params = array(), int $timeout = 25): array
{
	$key = epc_partsapi_resolve_key_for_method($method);
	if ($key === '') {
		return array('ok' => false, 'http_status' => 503, 'error' => 'PartsAPI key is not configured for ' . $method . '.', 'data' => null, 'elapsed_ms' => 0, 'method' => $method);
	}
	$query = array_merge(array('method' => $method, 'key' => $key), $params);
	$url = epc_partsapi_api_base_url() . '?' . http_build_query($query);
	$started = microtime(true);
	$body = false;
	$status = 0;
	$curlError = '';
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_CONNECTTIMEOUT => 10,
			CURLOPT_TIMEOUT => $timeout,
			CURLOPT_HTTPHEADER => array('Accept: application/json'),
			CURLOPT_SSL_VERIFYPEER => true,
			CURLOPT_SSL_VERIFYHOST => 2,
		));
		$body = curl_exec($ch);
		$curlError = curl_error($ch);
		$status = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
		curl_close($ch);
	} else {
		$ctx = stream_context_create(array('http' => array('method' => 'GET', 'header' => "Accept: application/json\r\n", 'timeout' => $timeout, 'ignore_errors' => true)));
		$body = @file_get_contents($url, false, $ctx);
		if (!empty($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
			$status = (int) $m[1];
		}
	}
	$elapsedMs = (int) round((microtime(true) - $started) * 1000);
	if ($body === false || $body === '') {
		return array('ok' => false, 'http_status' => $status ?: 502, 'error' => $curlError !== '' ? $curlError : 'Empty response from PartsAPI.', 'data' => null, 'elapsed_ms' => $elapsedMs);
	}
	$data = json_decode($body, true);
	if (json_last_error() !== JSON_ERROR_NONE) {
		return array('ok' => false, 'http_status' => $status ?: 502, 'error' => 'Non-JSON response from PartsAPI.', 'data' => null, 'elapsed_ms' => $elapsedMs);
	}
	if ($status >= 400) {
		$msg = epc_partsapi_error_message(array('error' => 'PartsAPI HTTP ' . $status, 'data' => $data));
		if ($msg === 'PartsAPI HTTP ' . $status) {
			$msg = 'PartsAPI HTTP ' . $status;
		}
		return array('ok' => false, 'http_status' => $status, 'error' => $msg, 'data' => $data, 'elapsed_ms' => $elapsedMs, 'method' => $method);
	}
	if (is_array($data) && isset($data['error_code']) && (int) $data['error_code'] > 0) {
		return array(
			'ok' => false,
			'http_status' => $status ?: 502,
			'error' => epc_partsapi_error_message(array('data' => $data)),
			'data' => $data,
			'elapsed_ms' => $elapsedMs,
			'method' => $method,
		);
	}
	return array('ok' => true, 'http_status' => $status, 'error' => '', 'data' => $data, 'elapsed_ms' => $elapsedMs, 'method' => $method);
}

function epc_partsapi_list_rows($data): array
{
	if (!is_array($data)) {
		return array();
	}
	if (isset($data['data']) && is_array($data['data'])) {
		return $data['data'];
	}
	$keys = array_keys($data);
	return ($keys === range(0, count($data) - 1)) ? $data : array($data);
}

function epc_partsapi_year_ci($year, $end = false): string
{
	$year = (int) $year;
	return $year > 0 ? ($end ? sprintf('%04d-12-31', $year) : sprintf('%04d-01-01', $year)) : '';
}

function epc_partsapi_map_manufacturers(array $rows, string $carType): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$id = (int) ($row['makeId'] ?? 0);
		$name = trim((string) ($row['makeName'] ?? ''));
		if ($id <= 0 || $name === '') {
			continue;
		}
		$out[] = array('MFA_ID' => $id, 'MANUFACTURER' => $name, 'makeId' => $id, 'makeName' => $name, 'EPART_TYPES' => array($carType));
	}
	return $out;
}

function epc_partsapi_map_models(array $rows, int $makeId): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$id = (int) ($row['modelId'] ?? 0);
		$name = trim((string) ($row['modelName'] ?? ''));
		if ($id <= 0 || $name === '') {
			continue;
		}
		$item = array('MS_ID' => $id, 'MODEL_SERIES' => $name, 'modelId' => $id, 'modelName' => $name, 'MFA_ID' => (int) ($row['makeId'] ?? $makeId));
		if ($from = epc_partsapi_year_ci($row['yearStart'] ?? 0)) {
			$item['CI_FROM'] = $from;
		}
		if ($to = epc_partsapi_year_ci($row['yearEnd'] ?? 0, true)) {
			$item['CI_TO'] = $to;
		}
		$out[] = $item;
	}
	return $out;
}

function epc_partsapi_map_cars(array $rows, string $carType): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$carId = (int) ($row['carId'] ?? 0);
		$name = trim((string) ($row['carName'] ?? ''));
		if ($carId <= 0) {
			continue;
		}
		$item = array(
			'ID' => $carId,
			'carId' => $carId,
			'carName' => $name,
			'MODIFICATION' => $name !== '' ? $name : ('Vehicle ' . $carId),
			'POWER_KW' => $row['POWER_KW'] ?? '',
			'POWER_PS' => $row['POWER_PS'] ?? '',
			'FUEL_TYPE' => $row['ENGINE_TYPE_EN'] ?? ($row['ENGINE_TYPE_RU'] ?? ''),
			'CAPACITY' => $row['CAPACITY'] ?? '',
		);
		if ($from = epc_partsapi_year_ci($row['yearStart'] ?? 0)) {
			$item['CI_FROM'] = $from;
		}
		if ($to = epc_partsapi_year_ci($row['yearEnd'] ?? 0, true)) {
			$item['CI_TO'] = $to;
		}
		if ($carType === 'CV') {
			$item['CV_ID'] = $carId;
			$item['COMMERCIAL_VEHICLE'] = $item['MODIFICATION'];
		} elseif ($carType === 'Motorcycle') {
			$item['MTB_ID'] = $carId;
			$item['MOTORBIKE'] = $item['MODIFICATION'];
		} else {
			$item['PC_ID'] = $carId;
			$item['PASSENGER_CAR'] = $item['MODIFICATION'];
		}
		$out[] = $item;
	}
	return $out;
}

function epc_partsapi_map_categories(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$strId = (int) ($row['NODE_3_STR_ID'] ?? ($row['NODE_2_STR_ID'] ?? ($row['NODE_1_STR_ID'] ?? ($row['STR_ID'] ?? 0))));
		$name = epc_cata_sanitize_category_name(
			trim((string) ($row['NODE_3_TEXT'] ?? ($row['NODE_2_TEXT'] ?? ($row['NODE_1_TEXT'] ?? ($row['ROOT_NODE_TEXT'] ?? ''))))),
			$strId
		);
		if ($strId <= 0 && $name === '') {
			continue;
		}
		$out[] = array(
			'CATEGORY_ID' => $strId,
			'CATEGORY_NAME' => $name !== '' ? $name : ('Category ' . $strId),
			'STR_ID' => $strId,
			'STR_LEVEL' => $row['STR_LEVEL'] ?? '',
			'NODE_1_TEXT' => $row['NODE_1_TEXT'] ?? '',
			'NODE_2_TEXT' => $row['NODE_2_TEXT'] ?? '',
			'NODE_3_TEXT' => $row['NODE_3_TEXT'] ?? '',
		);
	}
	return $out;
}

function epc_partsapi_map_articles(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$artId = (int) ($row['ART_ID'] ?? 0);
		$article = trim((string) ($row['ART_ARTICLE_NR'] ?? ''));
		$brand = trim((string) ($row['ART_SUP_BRAND'] ?? ($row['SUP_BRAND'] ?? '')));
		if ($artId <= 0 && $article === '') {
			continue;
		}
		$out[] = array(
			'ART_ID' => $artId,
			'ART_ARTICLE_NR' => $article,
			'ART_SUP_BRAND' => $brand,
			'SUP_BRAND' => $brand,
			'PRODUCT_GROUP' => $row['PRODUCT_GROUP'] ?? '',
			'PT_ID' => $row['PT_ID'] ?? '',
			'SUP_ID' => $row['SUP_ID'] ?? '',
		);
	}
	return $out;
}

function epc_partsapi_map_crosses(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$num = trim((string) ($row['crossNumber'] ?? ($row['partNumber'] ?? ($row['number'] ?? ''))));
		$brand = trim((string) ($row['crossBrand'] ?? ($row['brand'] ?? '')));
		if ($num === '') {
			continue;
		}
		$out[] = array('ART_ARTICLE_NR' => $num, 'ART_SUP_BRAND' => $brand, 'crossBrand' => $brand, 'crossNumber' => $num);
	}
	return $out;
}

function epc_partsapi_map_vin(array $rows): array
{
	$manufacturers = array();
	$models = array();
	$vehicles = array();
	$manuSeen = array();
	$modelSeen = array();
	foreach ($rows as $row) {
		if (!is_array($row)) {
			continue;
		}
		$manuId = (int) ($row['manuId'] ?? ($row['makeId'] ?? 0));
		$modelId = (int) ($row['modId'] ?? ($row['modelId'] ?? 0));
		$carId = (int) ($row['carId'] ?? 0);
		$manuName = trim((string) ($row['manuName'] ?? ($row['makeName'] ?? '')));
		$modelName = trim((string) ($row['modelName'] ?? ''));
		$carName = trim((string) ($row['carName'] ?? ''));
		if ($manuId > 0 && $manuName !== '' && !isset($manuSeen[$manuId])) {
			$manuSeen[$manuId] = true;
			$manufacturers[] = array('manuId' => $manuId, 'manuName' => $manuName);
		}
		if ($modelId > 0 && $modelName !== '' && !isset($modelSeen[$modelId])) {
			$modelSeen[$modelId] = true;
			$models[] = array('modelId' => $modelId, 'modelName' => $modelName, 'manuId' => $manuId);
		}
		if ($carId > 0) {
			$vehicles[] = array(
				'carId' => $carId,
				'manuId' => $manuId,
				'modelId' => $modelId,
				'carName' => $carName !== '' ? $carName : trim($manuName . ' ' . $modelName),
				'vehicleTypeDescription' => $carName,
				'linkageTargetId' => $carId,
			);
		}
	}
	return array('matchingManufacturers' => $manufacturers, 'matchingModels' => $models, 'matchingVehicles' => $vehicles);
}

function epc_partsapi_ok_payload(array $rows, string $source, array $result, string $action = ''): array
{
	return array(
		'ok' => true,
		'action' => $action,
		'rows' => count($rows),
		'data' => $rows,
		'source' => $source,
		'elapsed_ms' => (int) ($result['elapsed_ms'] ?? 0),
	);
}

function epc_partsapi_catalog_capabilities(): array
{
	$configured = epc_partsapi_credentials_configured();
	$makesOk = false;
	$modelsOk = false;
	$methodStatus = array();
	if ($configured) {
		$makesOk = false;
		$modelsOk = false;
		foreach (epc_partsapi_method_catalog() as $method => $meta) {
			$probe = is_array($meta['probe'] ?? null) ? $meta['probe'] : array();
			$r = epc_partsapi_call($method, $probe, 8);
			if ($method === 'getMakes') {
				$makesOk = $r['ok'];
			}
			if ($method === 'getModels') {
				$modelsOk = $r['ok'];
			}
			$statusMeta = epc_partsapi_probe_status_meta($r, $method);
			$methodStatus[$method] = array_merge(array(
				'label' => (string) ($meta['label'] ?? $method),
				'shop_url' => epc_partsapi_shop_url($method),
				'shop_path' => epc_partsapi_method_shop_path($method),
				'key_configured' => epc_partsapi_resolve_key_for_method($method) !== '',
				'probe' => $probe,
				'subscribed' => $r['ok'],
				'http_status' => (int) ($r['http_status'] ?? 0),
				'error' => (string) ($r['error'] ?? ''),
			), $statusMeta);
		}
	} else {
		foreach (epc_partsapi_method_shop_client_map() as $method => $meta) {
			$methodStatus[$method] = array(
				'label' => $meta['label'],
				'shop_url' => $meta['shop_url'],
				'shop_path' => epc_partsapi_method_shop_path($method),
				'key_configured' => false,
				'subscribed' => false,
				'subscription_required' => false,
			);
		}
	}
	return array(
		'ok' => true,
		'configured' => $configured,
		'makes_ok' => $makesOk,
		'catalog_ready' => $modelsOk,
		'models_subscription_required' => $configured && !empty($methodStatus['getModels']['subscription_required']),
		'shop_url' => epc_partsapi_shop_url('getModels'),
		'subscription_message' => epc_partsapi_subscription_message('models'),
		'methods' => $methodStatus,
		'action_methods' => epc_partsapi_action_shop_client_map(),
	);
}

function epc_partsapi_status_payload(): array
{
	$configured = epc_partsapi_credentials_configured();
	$methodTests = array();
	$carTypeTests = array();
	if ($configured) {
		foreach (array('PC', 'CV', 'Motorcycle') as $carType) {
			$r = epc_partsapi_call('getMakes', array('carType' => $carType), 20);
			$carTypeTests[$carType] = array_merge(array(
				'ok' => $r['ok'],
				'http_status' => $r['http_status'],
				'count' => $r['ok'] ? count(epc_partsapi_list_rows($r['data'])) : 0,
				'elapsed_ms' => $r['elapsed_ms'],
				'error' => $r['error'],
			), epc_partsapi_probe_status_meta($r, 'getMakes'));
		}
		foreach (epc_partsapi_method_catalog() as $method => $meta) {
			$probe = is_array($meta['probe'] ?? null) ? $meta['probe'] : array();
			$r = epc_partsapi_call($method, $probe, 20);
			$methodTests[$method] = array_merge(array(
				'ok' => $r['ok'],
				'http_status' => $r['http_status'],
				'count' => $r['ok'] ? count(epc_partsapi_list_rows($r['data'])) : 0,
				'elapsed_ms' => $r['elapsed_ms'],
				'error' => $r['error'],
				'probe' => $probe,
				'key_prefix' => ($k = epc_partsapi_resolve_key_for_method($method)) !== '' ? substr($k, 0, 8) . '…' : '',
				'label' => $meta['label'] ?? $method,
				'shop_url' => epc_partsapi_shop_url($method),
			), epc_partsapi_probe_status_meta($r, $method));
		}
	}
	$modelsOk = !empty($methodTests['getModels']['ok']);
	return array(
		'ok' => true,
		'configured' => $configured,
		'key_prefix' => $configured ? substr(epc_partsapi_resolve_key(), 0, 8) . '…' : '',
		'api_base_url' => epc_partsapi_api_base_url(),
		'catalog_ready' => $modelsOk,
		'getMakes' => $carTypeTests,
		'methods' => $methodTests,
		'shop_url' => epc_partsapi_shop_url(),
	);
}

function epc_partsapi_umapi_fallback(string $action, array $ctx = array())
{
	if (!epc_partsapi_umapi_fallback_enabled()) {
		return null;
	}
	$lang = epc_partsapi_default_lang();
	if (!empty($ctx['language']) && preg_match('/^[a-z]{2}$/', strtolower((string) $ctx['language']))) {
		$lang = strtolower((string) $ctx['language']);
	}
	$carType = epc_partsapi_car_type_from_section((string) ($ctx['section'] ?? 'passenger'));
	if (!empty($ctx['vehicle_type'])) {
		$vt = (string) $ctx['vehicle_type'];
		if ($vt === 'CV' || $vt === 'Motorcycle') {
			$carType = $vt;
		}
	}
	switch ($action) {
		case 'manufacturers':
			$r = epc_partsapi_call('getMakes', array('carType' => $carType, 'lang' => $lang));
			return $r['ok'] ? epc_partsapi_ok_payload(epc_partsapi_map_manufacturers(epc_partsapi_list_rows($r['data']), $carType), 'partsapi_fallback', $r, $action) : null;
		case 'models':
			$makeId = (int) ($ctx['MFA_ID'] ?? 0);
			if ($makeId <= 0) {
				return null;
			}
			$r = epc_partsapi_call('getModels', array('carType' => $carType, 'makeId' => $makeId, 'lang' => $lang));
			return $r['ok'] ? epc_partsapi_ok_payload(epc_partsapi_map_models(epc_partsapi_list_rows($r['data']), $makeId), 'partsapi_fallback', $r, $action) : null;
		case 'modifications':
			$modelId = (int) ($ctx['MS_ID'] ?? 0);
			if ($modelId <= 0) {
				return null;
			}
			$p = array('carType' => $carType, 'modelId' => $modelId, 'lang' => $lang);
			if ((int) ($ctx['MFA_ID'] ?? 0) > 0) {
				$p['makeId'] = (int) $ctx['MFA_ID'];
			}
			$r = epc_partsapi_call('getCars', $p);
			return $r['ok'] ? epc_partsapi_ok_payload(epc_partsapi_map_cars(epc_partsapi_list_rows($r['data']), $carType), 'partsapi_fallback', $r, $action) : null;
		case 'vin':
			$vin = strtoupper(preg_replace('/[^A-Z0-9]/', '', (string) ($ctx['vin'] ?? '')));
			if ($vin === '') {
				return null;
			}
			$r = epc_partsapi_call('VINdecode', array('vin' => $vin, 'lang' => $lang), 30);
			if (!$r['ok']) {
				return null;
			}
			$mapped = epc_partsapi_map_vin(epc_partsapi_list_rows($r['data']));
			return empty($mapped['matchingVehicles']) ? null : array('ok' => true, 'data' => $mapped, 'source' => 'partsapi_fallback', 'elapsed_ms' => $r['elapsed_ms']);
	}
	return null;
}
