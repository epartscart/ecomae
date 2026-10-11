<?php
// PHP 8.3 goldens for plan Q1-spar (PartsAPI config). Leftover catalog/portal parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function spar_freeze($value)
{
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = spar_freeze($v);
		}
		return $out;
	}
	return $value;
}

function spar_file($cfg)
{
	$GLOBALS['SPAR_FILE'] = $cfg;
}

function spar_http_route($url)
{
	$query = array();
	$q = parse_url($url, PHP_URL_QUERY);
	parse_str((string) $q, $query);
	$method = (string) ($query['method'] ?? '');
	$key = (string) ($query['key'] ?? '');
	$mode = (string) ($GLOBALS['SPAR_HTTP_MODE'] ?? 'ok');
	if ($mode === 'empty') {
		return array('body' => '', 'status' => 0, 'error' => '');
	}
	if ($mode === 'curlerr') {
		return array('body' => false, 'status' => 0, 'error' => 'Failed to connect');
	}
	if ($mode === 'nonjson') {
		return array('body' => '<html>nope</html>', 'status' => 200, 'error' => '');
	}
	if ($mode === 'http400') {
		return array('body' => json_encode(array('message' => 'bad request')), 'status' => 400, 'error' => '');
	}
	if ($mode === 'errcode') {
		return array('body' => json_encode(array('error_code' => 5002, 'message' => 'Authorization key is invalid')), 'status' => 200, 'error' => '');
	}
	if ($mode === 'rate') {
		return array('body' => json_encode(array('error_code' => 5000, 'message' => 'You have exceeded the number of requests')), 'status' => 200, 'error' => '');
	}
	if ($method === 'getMakes') {
		return array('body' => json_encode(array(array('makeId' => 16, 'makeName' => 'VW'))), 'status' => 200, 'error' => '');
	}
	if ($method === 'getModels') {
		return array('body' => json_encode(array(array('modelId' => 5114, 'modelName' => 'Golf', 'makeId' => 16, 'yearStart' => 2010, 'yearEnd' => 2015))), 'status' => 200, 'error' => '');
	}
	if ($method === 'getCars') {
		return array('body' => json_encode(array(array('carId' => 58963, 'carName' => '1.6', 'yearStart' => 2012))), 'status' => 200, 'error' => '');
	}
	if ($method === 'VINdecode') {
		$vin = (string) ($query['vin'] ?? '');
		if ($vin === 'EMPTYVIN1') {
			return array('body' => json_encode(array()), 'status' => 200, 'error' => '');
		}
		return array('body' => json_encode(array(array('manuId' => 16, 'manuName' => 'VW', 'modId' => 5114, 'modelName' => 'Golf', 'carId' => 7, 'carName' => 'Golf 1.6'))), 'status' => 200, 'error' => '');
	}
	if ($method === 'getCrosses' || $method === 'getCrossesWithBrand' || $method === 'tecdocCrosses') {
		return array('body' => json_encode(array(array('crossNumber' => '1J0971972', 'crossBrand' => 'VAG'))), 'status' => 200, 'error' => '');
	}
	if ($method === 'searchArticles' || $method === 'PartSuggest' || $method === 'getArticles') {
		return array('body' => json_encode(array(array('ART_ID' => 9, 'ART_ARTICLE_NR' => '1J0971972', 'ART_SUP_BRAND' => 'VAG'))), 'status' => 200, 'error' => '');
	}
	if ($method === 'getSearchTree') {
		return array('body' => json_encode(array(array('NODE_3_STR_ID' => 100260, 'NODE_3_TEXT' => 'Filters'))), 'status' => 200, 'error' => '');
	}
	return array('body' => json_encode(array('ok' => true, 'method' => $method, 'key' => $key)), 'status' => 200, 'error' => '');
}

function spar_run_config()
{
	spar_file(array());
	$emptyKey = epc_partsapi_resolve_key();
	$defaultBase = epc_partsapi_api_base_url();
	$defaultLang = epc_partsapi_default_lang();
	$credEmpty = epc_partsapi_credentials_configured();
	$umapiOff = epc_partsapi_umapi_fallback_enabled();
	$shopDefault = epc_partsapi_shop_url();
	$shopMakes = epc_partsapi_shop_url('getMakes');
	$unknownPath = epc_partsapi_method_shop_path('Nope');
	$blankPath = epc_partsapi_method_shop_path('');
	$proxy = array(
		epc_partsapi_proxy_action_method('manufacturers'),
		epc_partsapi_proxy_action_method('cars'),
		epc_partsapi_proxy_action_method('analogs'),
		epc_partsapi_proxy_action_method('unknown'),
	);
	$sections = array(
		epc_partsapi_car_type_from_section('commercial'),
		epc_partsapi_car_type_from_section('motorbike'),
		epc_partsapi_car_type_from_section('passenger'),
		epc_partsapi_section_from_car_type('CV'),
		epc_partsapi_section_from_car_type('Motorcycle'),
		epc_partsapi_section_from_car_type('PC'),
	);
	$GLOBALS['SPAR_EPARTS'] = true;
	$GLOBALS['SPAR_AUTO'] = false;
	$epartsOn = epc_partsapi_enabled_for_request();
	$GLOBALS['SPAR_EPARTS'] = false;
	$GLOBALS['SPAR_AUTO'] = true;
	$autoOff = epc_partsapi_enabled_for_request();
	spar_file(array('allow_auto_parts_tenants' => '1'));
	$autoOn = epc_partsapi_enabled_for_request();
	spar_file(array('allow_auto_parts_tenants' => '0'));
	$autoZero = epc_partsapi_enabled_for_request();
	$GLOBALS['SPAR_AUTO'] = false;
	$neither = epc_partsapi_enabled_for_request();

	spar_file(array(
		'api_base_url' => 'https://parts.example/',
		'api_key' => 'platformkey99',
		'method_keys' => array('getMakes' => 'makeskey1', 'getModels' => '0', 'getCars' => ''),
		'method_shop_urls' => array('getMakes' => 'https://shop.example/makes', 'getModels' => '0'),
		'default_lang' => 'DE',
		'umapi_fallback' => '1',
	));
	$custom = array(
		epc_partsapi_api_base_url(),
		epc_partsapi_resolve_key(),
		epc_partsapi_resolve_key_for_method('getMakes'),
		epc_partsapi_resolve_key_for_method('getModels'),
		epc_partsapi_resolve_key_for_method('getCars'),
		epc_partsapi_default_lang(),
		epc_partsapi_shop_url('getMakes'),
		epc_partsapi_shop_url('getModels'),
		epc_partsapi_shop_url('getCars'),
		epc_partsapi_credentials_configured(),
		epc_partsapi_umapi_fallback_enabled(),
	);
	spar_file(array(
		'method_keys' => array('getMakes' => 'onlymethod'),
		'umapi_fallback' => '1',
		'default_lang' => 'en',
	));
	$methodOnly = array(
		epc_partsapi_credentials_configured(),
		epc_partsapi_resolve_key(),
		epc_partsapi_umapi_fallback_enabled(),
	);
	spar_file(array('method_shop_urls' => array('custom' => 'https://abs.example/x')));
	$httpsPath = epc_partsapi_shop_url('custom');
	$catalogKeys = array_keys(epc_partsapi_method_catalog());
	$clientMap = epc_partsapi_method_shop_client_map();
	$actionMap = epc_partsapi_action_shop_client_map();
	return spar_freeze(array(
		$emptyKey, $defaultBase, $defaultLang, $credEmpty, $umapiOff,
		$shopDefault, $shopMakes, $unknownPath, $blankPath, $proxy, $sections,
		$epartsOn, $autoOff, $autoOn, $autoZero, $neither,
		$custom, $methodOnly, $httpsPath, $catalogKeys, $clientMap['getMakes'], $actionMap['vin'],
	));
}

function spar_run_errors()
{
	$rate = array('data' => array('error_code' => 5000, 'message' => 'You have exceeded the number of requests'), 'error' => '', 'http_status' => 200, 'method' => 'getMakes');
	$svc = array('data' => array('error_code' => 5005, 'message' => array('down', 'retry')), 'error' => '', 'http_status' => 200, 'method' => 'getMakes');
	$auth = array('data' => array('error_code' => 5002, 'message' => 'Authorization key is invalid'), 'error' => '', 'http_status' => 200, 'method' => 'getMakes');
	$http401 = array('data' => null, 'error' => 'nope', 'http_status' => 401, 'method' => 'getMakes');
	$http403 = array('data' => null, 'error' => '', 'http_status' => 403, 'method' => 'getMakes');
	$textRate = array('data' => array('message' => 'rate limit hit'), 'error' => '', 'http_status' => 200, 'method' => 'getMakes');
	$none = array('data' => array(), 'error' => 'x', 'http_status' => 200, 'method' => 'getMakes');
	spar_file(array());
	$noKeyMeta = epc_partsapi_probe_status_meta($http401, 'getMakes');
	$noKeyAuth = epc_partsapi_is_auth_key_error($http401);
	spar_file(array('method_keys' => array('getMakes' => 'abc')));
	$withKeyMeta = epc_partsapi_probe_status_meta($http401, 'getMakes');
	$withKeyAuth = epc_partsapi_is_auth_key_error($http401);
	$failSub = epc_partsapi_fail_payload($auth, 'manufacturers');
	$failPlain = epc_partsapi_fail_payload($none, 'unknown');
	return spar_freeze(array(
		epc_partsapi_error_code($rate),
		epc_partsapi_error_message($rate),
		epc_partsapi_error_message($svc),
		epc_partsapi_error_message($none),
		epc_partsapi_is_rate_limit_error($rate),
		epc_partsapi_is_rate_limit_error($textRate),
		epc_partsapi_is_service_error($svc),
		epc_partsapi_is_service_error($rate),
		epc_partsapi_is_auth_key_error($auth),
		epc_partsapi_is_auth_key_error($rate),
		$noKeyAuth,
		$withKeyAuth,
		$noKeyMeta,
		$withKeyMeta,
		epc_partsapi_probe_status_meta($rate, 'getMakes'),
		epc_partsapi_subscription_message('models'),
		epc_partsapi_subscription_message(''),
		$failSub,
		$failPlain,
		epc_partsapi_is_auth_key_error($http403),
	));
}

function spar_run_maps()
{
	$listWrapped = epc_partsapi_list_rows(array('data' => array(array('a' => 1))));
	$listSeq = epc_partsapi_list_rows(array(array('a' => 1), array('a' => 2)));
	$listAssoc = epc_partsapi_list_rows(array('makeId' => 1, 'makeName' => 'VW'));
	$listBad = epc_partsapi_list_rows('x');
	$years = array(epc_partsapi_year_ci(2010), epc_partsapi_year_ci(2015, true), epc_partsapi_year_ci(0), epc_partsapi_year_ci('2012abc'));
	$manu = epc_partsapi_map_manufacturers(array(
		array('makeId' => 16, 'makeName' => 'VW'),
		array('makeId' => 0, 'makeName' => 'Skip'),
		'skip',
	), 'PC');
	$models = epc_partsapi_map_models(array(
		array('modelId' => 5, 'modelName' => 'Golf', 'yearStart' => 2010, 'yearEnd' => 2012),
		array('modelId' => 6, 'modelName' => ''),
	), 16);
	$carsPc = epc_partsapi_map_cars(array(array('carId' => 7, 'carName' => '')), 'PC');
	$carsCv = epc_partsapi_map_cars(array(array('carId' => 8, 'carName' => 'Truck')), 'CV');
	$carsBike = epc_partsapi_map_cars(array(array('carId' => 9, 'carName' => 'Bike')), 'Motorcycle');
	$cats = epc_partsapi_map_categories(array(
		array('NODE_3_STR_ID' => 10, 'NODE_3_TEXT' => '  Oil  '),
		array('STR_ID' => 0, 'ROOT_NODE_TEXT' => ''),
		array('NODE_1_STR_ID' => 3, 'NODE_1_TEXT' => ''),
	));
	$arts = epc_partsapi_map_articles(array(
		array('ART_ID' => 1, 'ART_ARTICLE_NR' => 'A1', 'ART_SUP_BRAND' => 'VAG'),
		array('ART_ID' => 0, 'ART_ARTICLE_NR' => ''),
	));
	$cross = epc_partsapi_map_crosses(array(
		array('crossNumber' => 'X1', 'crossBrand' => 'VAG'),
		array('partNumber' => 'P1'),
		array('number' => ''),
	));
	$vin = epc_partsapi_map_vin(array(
		array('manuId' => 16, 'manuName' => 'VW', 'modId' => 5, 'modelName' => 'Golf', 'carId' => 7, 'carName' => 'G'),
		array('makeId' => 16, 'makeName' => 'VW', 'modelId' => 5, 'modelName' => 'Golf', 'carId' => 8),
	));
	$ok = epc_partsapi_ok_payload($manu, 'partsapi_fallback', array('elapsed_ms' => 12), 'manufacturers');
	return spar_freeze(array($listWrapped, $listSeq, $listAssoc, $listBad, $years, $manu, $models, $carsPc, $carsCv, $carsBike, $cats, $arts, $cross, $vin, $ok));
}

function spar_run_call()
{
	spar_file(array());
	$missing = epc_partsapi_call('getMakes', array('carType' => 'PC'));
	unset($missing['elapsed_ms']);
	spar_file(array('api_key' => 'platformkey99', 'umapi_fallback' => '1', 'default_lang' => 'en'));
	$GLOBALS['SPAR_HTTP_MODE'] = 'ok';
	$ok = epc_partsapi_call('getMakes', array('carType' => 'PC', 'lang' => 'en'));
	$lastOk = $GLOBALS['SPAR_LAST_URL'] ?? '';
	$GLOBALS['SPAR_HTTP_MODE'] = 'empty';
	$empty = epc_partsapi_call('getMakes');
	$GLOBALS['SPAR_HTTP_MODE'] = 'curlerr';
	$err = epc_partsapi_call('getMakes');
	$GLOBALS['SPAR_HTTP_MODE'] = 'nonjson';
	$nonjson = epc_partsapi_call('getMakes');
	$GLOBALS['SPAR_HTTP_MODE'] = 'http400';
	$http400 = epc_partsapi_call('getMakes');
	$GLOBALS['SPAR_HTTP_MODE'] = 'errcode';
	$errcode = epc_partsapi_call('getMakes');
	$GLOBALS['SPAR_HTTP_MODE'] = 'ok';
	$fbManu = epc_partsapi_umapi_fallback('manufacturers', array('section' => 'passenger'));
	$fbModels = epc_partsapi_umapi_fallback('models', array('MFA_ID' => 16, 'language' => 'en'));
	$fbNoMake = epc_partsapi_umapi_fallback('models', array());
	$fbCars = epc_partsapi_umapi_fallback('modifications', array('MS_ID' => 5114, 'MFA_ID' => 16, 'vehicle_type' => 'PC'));
	$fbVin = epc_partsapi_umapi_fallback('vin', array('vin' => 'WVW-ZZZ 1KZ'));
	$fbVinEmpty = epc_partsapi_umapi_fallback('vin', array('vin' => 'EMPTYVIN1'));
	$fbVinBlank = epc_partsapi_umapi_fallback('vin', array('vin' => ''));
	$fbUnknown = epc_partsapi_umapi_fallback('articles', array());
	spar_file(array('api_key' => 'platformkey99', 'umapi_fallback' => '0'));
	$fbOff = epc_partsapi_umapi_fallback('manufacturers', array());
	spar_file(array());
	$capsOff = epc_partsapi_catalog_capabilities();
	$statOff = epc_partsapi_status_payload();
	spar_file(array('api_key' => 'platformkey99'));
	$GLOBALS['SPAR_HTTP_MODE'] = 'ok';
	$capsOn = epc_partsapi_catalog_capabilities();
	$statOn = epc_partsapi_status_payload();
	foreach (array(&$ok, &$empty, &$err, &$nonjson, &$http400, &$errcode, &$fbManu, &$fbModels, &$fbCars, &$fbVin) as &$row) {
		if (is_array($row)) {
			unset($row['elapsed_ms']);
		}
	}
	unset($row);
	if (isset($capsOn['methods']) && is_array($capsOn['methods'])) {
		foreach ($capsOn['methods'] as $k => $m) {
			unset($capsOn['methods'][$k]['elapsed_ms']);
			unset($capsOn['methods'][$k]['error']);
			unset($capsOn['methods'][$k]['http_status']);
		}
	}
	if (isset($statOn['methods']) && is_array($statOn['methods'])) {
		foreach ($statOn['methods'] as $k => $m) {
			unset($statOn['methods'][$k]['elapsed_ms']);
			unset($statOn['methods'][$k]['error']);
			unset($statOn['methods'][$k]['http_status']);
			unset($statOn['methods'][$k]['count']);
		}
	}
	if (isset($statOn['getMakes']) && is_array($statOn['getMakes'])) {
		foreach ($statOn['getMakes'] as $k => $m) {
			unset($statOn['getMakes'][$k]['elapsed_ms']);
			unset($statOn['getMakes'][$k]['error']);
			unset($statOn['getMakes'][$k]['http_status']);
			unset($statOn['getMakes'][$k]['count']);
		}
	}
	unset($capsOn['subscription_message']);
	unset($statOn['key_prefix']);
	return spar_freeze(array(
		$missing, $ok, $lastOk, $empty, $err, $nonjson, $http400, $errcode,
		$fbManu, $fbModels, $fbNoMake, $fbCars, $fbVin, $fbVinEmpty, $fbVinBlank, $fbUnknown, $fbOff,
		$capsOff['configured'], $capsOff['makes_ok'], $capsOff['catalog_ready'],
		$statOff['configured'], $statOff['catalog_ready'], $statOff['key_prefix'], $statOff['api_base_url'],
		$capsOn['configured'], $capsOn['makes_ok'], $capsOn['catalog_ready'], $capsOn['models_subscription_required'],
		$capsOn['methods']['getMakes']['subscribed'] ?? null,
		$statOn['configured'], $statOn['catalog_ready'], $statOn['shop_url'],
	));
}

function spar_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace(
		"static \$cached = null;\n\tif (\$cached !== null) {\n\t\treturn \$cached;\n\t}",
		"if (isset(\$GLOBALS['SPAR_FILE']) && is_array(\$GLOBALS['SPAR_FILE'])) {\n\t\treturn \$GLOBALS['SPAR_FILE'];\n\t}\n\tstatic \$cached = null;\n\tif (\$cached !== null) {\n\t\treturn \$cached;\n\t}",
		$code
	);
	$old = "\t\$started = microtime(true);\n\t\$body = false;\n\t\$status = 0;\n\t\$curlError = '';\n\tif (function_exists('curl_init')) {";
	$new = "\t\$started = microtime(true);\n\t\$body = false;\n\t\$status = 0;\n\t\$curlError = '';\n\tif (isset(\$GLOBALS['SPAR_HTTP']) && is_callable(\$GLOBALS['SPAR_HTTP'])) {\n\t\t\$GLOBALS['SPAR_LAST_URL'] = \$url;\n\t\t\$hit = \$GLOBALS['SPAR_HTTP'](\$url, \$timeout);\n\t\t\$body = \$hit['body'];\n\t\t\$status = (int) \$hit['status'];\n\t\t\$curlError = (string) \$hit['error'];\n\t} elseif (function_exists('curl_init')) {";
	$code = str_replace($old, $new, $code);
	$code = str_replace(
		'$elapsedMs = (int) round((microtime(true) - $started) * 1000);',
		'$elapsedMs = (int) ($GLOBALS[\'SPAR_ELAPSED\'] ?? 0);',
		$code
	);
	file_put_contents($dest, $code);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1s_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc . '/content/general_pages', 0777, true);
	spar_patch($root . '/content/general_pages/epc_partsapi_config.php', $doc . '/epc_partsapi_config.php');
	$cleanup = function () use ($doc) {
		foreach (glob($doc . '/content/general_pages/*') ?: array() as $file) { @unlink($file); }
		@rmdir($doc . '/content/general_pages');
		@rmdir($doc . '/content');
		@rmdir($doc);
	};
	if (!function_exists('epc_cata_sanitize_category_name')) {
		eval('function epc_cata_sanitize_category_name($name, $id = 0) { $name = trim((string) $name); return $name !== \'\' ? $name : (\'Cat \' . (int) $id); }');
	}
	if (!function_exists('epc_portal_is_epartscart_hostname')) {
		eval('function epc_portal_is_epartscart_hostname() { return !empty($GLOBALS["SPAR_EPARTS"]); }');
	}
	if (!function_exists('epc_portal_is_auto_parts_site')) {
		eval('function epc_portal_is_auto_parts_site() { return !empty($GLOBALS["SPAR_AUTO"]); }');
	}
	$GLOBALS['SPAR_ELAPSED'] = 0;
	$GLOBALS['SPAR_HTTP'] = function ($url, $timeout) {
		return spar_http_route($url);
	};
	$GLOBALS['SPAR_FILE'] = array();
	$GLOBALS['SPAR_EPARTS'] = false;
	$GLOBALS['SPAR_AUTO'] = false;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	require $doc . '/epc_partsapi_config.php';
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1spar_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1spar_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
