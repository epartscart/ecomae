<?php
/**
 * Verify epartscart storefront fixes (APAI redirect, EN placeholder, market-source hiding).
 * https://www.ecomae.com/epc-platform-fix-epartscart-storefront.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$host = 'www.epartscart.com';
$checks = array(
	array('url' => 'https://' . $host . '/en/', 'expect' => array('no_leak' => true)),
	array('url' => 'https://' . $host . '/en/apai-root-auto-parts', 'expect' => array('redirect' => true)),
	array('url' => 'https://' . $host . '/en/parts/toyota/1780131090', 'expect' => array('warehouse' => true, 'no_market' => true)),
);

function epc_epc_sf_probe(string $url): array
{
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => 25,
			'ignore_errors' => true,
			'follow_location' => 0,
			'header' => "User-Agent: EPC-Storefront-Verify/1.0\r\n",
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	$location = '';
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	foreach ((array) $http_response_header as $hdr) {
		if (stripos($hdr, 'Location:') === 0) {
			$location = trim(substr($hdr, 9));
		}
	}
	$flat = is_string($body) ? $body : '';
	return array(
		'code' => $code,
		'location' => $location,
		'body' => $flat,
		'has_russian_no_image' => (bool) preg_match('/Нет\s+изображения/ui', $flat),
		'has_apai_grid' => stripos($flat, 'apai-root-auto-parts') !== false && stripos($flat, 'Air Conditioning') !== false,
		'has_market_block' => stripos($flat, 'epc-apai-market-prices') !== false
			|| stripos($flat, 'Market prices (UAE)') !== false
			|| stripos($flat, 'spare247') !== false
			|| stripos($flat, 'sharafdg') !== false,
		'has_warehouse' => stripos($flat, 'warehouse') !== false || stripos($flat, 'stock') !== false,
		'db_leak' => stripos($flat, 'No DB connect') !== false,
	);
}

echo "=== epartscart storefront verify ===\n\n";
$ok = 0;
$fail = 0;
foreach ($checks as $c) {
	$url = (string) $c['url'];
	$exp = (array) ($c['expect'] ?? array());
	$p = epc_epc_sf_probe($url);
	echo $url . "\n";
	echo "  HTTP {$p['code']}";
	if ($p['location'] !== '') {
		echo " → {$p['location']}";
	}
	echo "\n";

	$pass = true;
	if (!empty($exp['redirect'])) {
		$redirOk = in_array($p['code'], array(301, 302, 303, 307, 308), true) || $p['location'] !== '';
		echo '  redirect: ' . ($redirOk ? 'OK' : 'FAIL') . "\n";
		$pass = $pass && $redirOk;
	}
	if (!empty($exp['no_leak'])) {
		echo '  db_connect: ' . ($p['db_leak'] ? 'FAIL' : 'OK') . "\n";
		$pass = $pass && !$p['db_leak'];
	}
	if (!empty($exp['warehouse'])) {
		echo '  warehouse_hints: ' . ($p['has_warehouse'] ? 'OK' : 'WARN') . "\n";
	}
	if (!empty($exp['no_market'])) {
		echo '  market_source_hidden: ' . ($p['has_market_block'] ? 'FAIL' : 'OK') . "\n";
		$pass = $pass && !$p['has_market_block'];
	}
	echo '  russian_no_image_text: ' . ($p['has_russian_no_image'] ? 'FAIL' : 'OK') . "\n";
	$pass = $pass && !$p['has_russian_no_image'];
	echo '  result: ' . ($pass ? 'PASS' : 'FAIL') . "\n\n";
	if ($pass) {
		$ok++;
	} else {
		$fail++;
	}
}

echo "summary: pass={$ok} fail={$fail}\n";
