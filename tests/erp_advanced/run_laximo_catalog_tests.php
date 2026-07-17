<?php
/**
 * Laximo catalog landing / proxy regression tests.
 *
 *   php tests/erp_advanced/run_laximo_catalog_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
$pass = 0;
$fail = 0;
function check(string $label, bool $cond): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== Landing page two sections ==\n";
$idx = (string) file_get_contents($root . '/content/laximo/index.php');
check('index uses AJAX shell for catalogs', strpos($idx, 'Laximo_container') !== false);
check('index loads laximo.js', strpos($idx, '/api/Laximo/laximo.js') !== false);
check('index keeps Guayaquil for deep tasks', strpos($idx, 'com_guayaquil/index.php') !== false);
check('index does not return from CMS eval', !preg_match('/\breturn\s*;/', $idx));
check('index closes PHP for CMS embed', substr(rtrim($idx), -2) === '?>');
check('index uses if/else ajax landing', strpos($idx, '$useAjaxLanding') !== false);

$js = (string) file_get_contents($root . '/api/Laximo/laximo.js');
check('JS section 1 VIN', strpos($js, 'laximo-section--vin') !== false);
check('JS section 2 brands', strpos($js, 'laximo-section--brands') !== false);

$css = (string) file_get_contents($root . '/api/Laximo/laximo.css');
check('CSS has section styles', strpos($css, '.laximo-section--vin') !== false);

echo "\n== Proxy / SOAP hardening ==\n";
$proxy = (string) file_get_contents($root . '/api/laximo_proxy.php');
check('proxy does not pass JSON_SORT_KEYS flag', strpos($proxy, 'json_encode($params, JSON_SORT_KEYS)') === false);
check('proxy cache key uses ksort', strpos($proxy, 'ksort($params)') !== false);
check('proxy SOAP uses https', strpos($proxy, 'https://ws.laximo.net/') !== false);
check('proxy unwraps response wrapper', strpos($proxy, 'function epc_lax_load_xml') !== false);
check('proxy rejects unusable catalogs cache', strpos($proxy, 'function epc_lax_catalogs_usable') !== false);
check('proxy supports refresh/nocache', strpos($proxy, "epc_lax_param('refresh'") !== false);

$soap = (string) file_get_contents($root . '/content/laximo/com_guayaquil/guayaquillib/data/SSLSoapClient.php');
check('SSLSoapClient rejects empty curl response', strpos($soap, 'Empty SOAP response') !== false || strpos($soap, 'cURL ERROR') !== false);
check('SSLSoapClient upgrades http→https', strpos($soap, "stripos(\$url, 'http://')") !== false);

$wrap = (string) file_get_contents($root . '/content/laximo/com_guayaquil/guayaquillib/data/GuayaquilSoapWrapper.php');
check('wrapper always https location', strpos($wrap, "'https://' . Config::\$oemServiceUrl") !== false);

$listObj = (string) file_get_contents($root . '/content/laximo/com_guayaquil/guayaquillib/objects/CatalogListObject.php');
check('CatalogListObject does not in_array on VehiclesColumns int', strpos($listObj, 'in_array($catObj->name, $carBrands, true)') !== false && strpos($listObj, 'is_array(Config::$VehiclesColumns)') !== false);

echo "\n== Parse ListCatalogs under <response> ==\n";
$code = (string) file_get_contents($root . '/api/laximo_proxy.php');
$start = strpos($code, 'function epc_lax_json');
$end = strpos($code, '// --- Main router ---');
check('proxy helpers extractable', $start !== false && $end !== false);
if ($start !== false && $end !== false) {
	$funcs = substr($code, $start, $end - $start);
	eval('?>' . '<?php ' . $funcs);
	$sample = '<response><ListCatalogs>'
		. '<row brand="TOYOTA" code="TOYOTA01" icon="toyota.png" name="Toyota" supportvinsearch="true">'
		. '<features><feature name="vinsearch"/></features></row>'
		. '<row brand="BMW" code="BMW01" name="BMW"/></ListCatalogs></response>';
	$cats = epc_lax_parse_catalogs($sample);
	check('parses 2 catalogs from response wrapper', count($cats) === 2);
	check('first catalog has code', isset($cats[0]['code']) && $cats[0]['code'] === 'TOYOTA01');
	check('rejects features-only junk', epc_lax_catalogs_usable([['features' => []]]) === false);
	check('accepts real catalogs', epc_lax_catalogs_usable($cats) === true);
}

echo "\n== CMS eval embed safety ==\n";
$tpl = '<html><body>MAIN_START' . $idx . 'MAIN_END<footer>F</footer></body></html>';
ob_start();
$evalOk = true;
try {
	eval(' ?>' . $tpl . '<?php ');
} catch (Throwable $e) {
	$evalOk = false;
	echo '  eval error: ' . $e->getMessage() . "\n";
}
$out = (string) ob_get_clean();
check('CMS-style eval succeeds', $evalOk);
check('CMS-style eval keeps footer after content', strpos($out, 'MAIN_END') !== false && strpos($out, '<footer>F</footer>') !== false);
check('CMS-style eval renders Laximo_container', strpos($out, 'Laximo_container') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
