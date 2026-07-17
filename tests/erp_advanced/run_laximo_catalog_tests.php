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

$soap = (string) file_get_contents($root . '/content/laximo/com_guayaquil/guayaquillib/data/SSLSoapClient.php');
check('SSLSoapClient rejects empty curl response', strpos($soap, 'Empty SOAP response') !== false || strpos($soap, 'cURL ERROR') !== false);
check('SSLSoapClient upgrades http→https', strpos($soap, "stripos(\$url, 'http://')") !== false);

$wrap = (string) file_get_contents($root . '/content/laximo/com_guayaquil/guayaquillib/data/GuayaquilSoapWrapper.php');
check('wrapper always https location', strpos($wrap, "'https://' . Config::\$oemServiceUrl") !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
