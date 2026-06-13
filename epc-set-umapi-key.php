<?php
/**
 * One-shot: set the Epart catalog (umapi) API key in config.php (server-only).
 * The key is passed as a request param so it is never committed to git.
 *
 *   curl "https://www.ecomae.com/epc-set-umapi-key.php?token=...&umapi_key=THEKEY"
 *
 * Safe: backs up config.php before writing, then does a live upstream test.
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$key = trim((string)($_REQUEST['umapi_key'] ?? ''));
if ($key === '') {
	exit("Missing umapi_key param\n");
}
if (!preg_match('/^[A-Za-z0-9\-]{8,80}$/', $key)) {
	exit("umapi_key has unexpected format\n");
}

$config_path = __DIR__ . '/config.php';
if (!is_file($config_path)) {
	exit("config.php not found\n");
}

$content = (string)file_get_contents($config_path);
$backup = $config_path . '.bak-umapi-' . date('Ymd-His');
if (file_put_contents($backup, $content) === false) {
	exit("Could not write backup\n");
}

$prop = "public \$umapi_api_key = '" . addslashes($key) . "';";
if (preg_match('/public\s+\$umapi_api_key\s*=\s*\'[^\']*\'\s*;/', $content)) {
	$content = preg_replace(
		'/public\s+\$umapi_api_key\s*=\s*\'[^\']*\'\s*;/',
		$prop,
		$content,
		1
	);
	$mode = 'replaced existing property';
} elseif (preg_match('/(class\s+DP_Config[^{]*\{)/', $content, $m, PREG_OFFSET_CAPTURE)) {
	$pos = (int)$m[1][1] + strlen($m[1][0]);
	$content = substr($content, 0, $pos) . "\n\t" . $prop . substr($content, $pos);
	$mode = 'inserted new property';
} else {
	exit("Could not locate DP_Config class in config.php\n");
}

if (file_put_contents($config_path, $content) === false) {
	exit("Could not write config.php (restored from backup needed: $backup)\n");
}

// Re-read to confirm the value lands.
$verify = (string)file_get_contents($config_path);
$ok = (bool)preg_match('/public\s+\$umapi_api_key\s*=\s*\'' . preg_quote(addslashes($key), '/') . '\'\s*;/', $verify);

echo "config.php: $mode\n";
echo "backup:     $backup\n";
echo "verify:     " . ($ok ? 'OK (key present in config.php)' : 'FAILED (key not found after write)') . "\n";
echo "key tail:   ..." . substr($key, -6) . "\n\n";

// Live upstream test with the new key.
$testUrl = 'https://api.umapi.ru/v2/autocatalog/en-WWW/Manufacturers?type=PC&popular=true';
$status = 0;
$body = '';
if (function_exists('curl_init')) {
	$ch = curl_init($testUrl);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_CONNECTTIMEOUT => 10,
		CURLOPT_TIMEOUT => 25,
		CURLOPT_HTTPHEADER => array('Accept: application/json', 'X-App-Key: ' . $key),
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_SSL_VERIFYHOST => 2,
	));
	$body = (string)curl_exec($ch);
	$status = (int)curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);
}
$decoded = json_decode($body, true);
$count = is_array($decoded) ? count($decoded) : 0;
echo "upstream test (api.umapi.ru): HTTP $status, rows=$count\n";
echo "upstream sample: " . substr($body, 0, 160) . "\n";
echo "\nDone.\n";
