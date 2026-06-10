<?php
/**
 * Quick VPS + tenant reachability report (run on server via ecomae.com).
 * https://www.ecomae.com/epc-vps-firewall-status.php?token=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$ip = '31.97.216.247';
$hosts = array(
	'www.epartscart.com',
	'www.taxofinca.com',
	'www.electronicae.com',
	'www.stylenlook.com',
	'www.thejewellerytrend.com',
	'www.ecomae.com',
);

echo "=== VPS firewall / tenant status ===\n";
echo 'server_time=' . gmdate('c') . "\n";
echo 'server_public_ip=' . trim((string) @file_get_contents('https://api.ipify.org')) . "\n\n";

echo "=== DNS ===\n";
foreach ($hosts as $h) {
	echo "  {$h} → " . gethostbyname($h) . "\n";
}

function epc_vfs_probe(string $url): string
{
	$t0 = microtime(true);
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$loc = '';
	foreach ((array) $http_response_header as $hdr) {
		if (stripos($hdr, 'Location:') === 0) {
			$loc = ' → ' . trim(substr($hdr, 9));
		}
	}
	$hint = $body === false ? 'TIMEOUT' : (stripos((string) $body, 'No DB connect') !== false ? ' [no-db]' : '');
	return "HTTP {$code}{$hint}{$loc} ({$ms}ms)";
}

echo "\n=== Public HTTPS (from this VPS) ===\n";
foreach (array('www.epartscart.com', 'www.taxofinca.com', 'www.electronicae.com', 'www.stylenlook.com', 'www.thejewellerytrend.com') as $h) {
	echo "  https://{$h}/: " . epc_vfs_probe("https://{$h}/") . "\n";
	echo "  https://{$h}/cp/: " . epc_vfs_probe("https://{$h}/cp/") . "\n";
}

echo "\n=== Hairpin to {$ip} ===\n";
$ctx = stream_context_create(array(
	'http' => array('timeout' => 15, 'ignore_errors' => true, 'header' => "Host: www.epartscart.com\r\n"),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
));
$t0 = microtime(true);
$body = @file_get_contents("https://{$ip}/", false, $ctx);
$ms = (int) round((microtime(true) - $t0) * 1000);
echo '  https://' . $ip . '/ (Host epartscart): ' . ($body === false ? 'TIMEOUT' : 'OK') . " ({$ms}ms)\n";

echo "\n=== hPanel checklist (if your PC still times out) ===\n";
echo "1. VPS → srv1672837 → Firewall (sidebar ON THE VPS) — firewall group must be ACTIVE on this server.\n";
echo "2. After any rule edit → yellow Synchronize button.\n";
echo "3. Wait 5 minutes. Test from phone LTE: https://www.epartscart.com/\n";
echo "4. Optional API sync: hostinger_token + epc-hostinger-firewall-open-web.php?apply=1\n";
echo "\nTenants on server: OK when public probes above show HTTP 200/302.\n";
