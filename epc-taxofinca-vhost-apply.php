<?php
/**
 * Force taxofinca vhost apply: varnish aliases + site-settings touch + probes.
 * https://www.epartscart.com/epc-taxofinca-vhost-apply.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$hostname = 'www.taxofinca.com';
$bare = 'taxofinca.com';
$platformSite = 'www.epartscart.com';
$sharedRoot = '/home/epartscart/htdocs/www.epartscart.com';
$aliases = array($hostname, $bare);

if ($clpPass === '') {
	exit("clp_pass required\n");
}

function epc_tf_apply_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = '';
	if ($body !== false) {
		if (stripos($body, '403 Forbidden') !== false && stripos($body, 'nginx') !== false) {
			$hint = ' [nginx403]';
		} elseif (stripos($body, 'Taxofin') !== false || stripos($body, 'taxofinca') !== false) {
			$hint = ' [taxofinca]';
		} elseif (stripos($body, 'eParts Cart') !== false || stripos($body, 'epartscart') !== false) {
			$hint = ' [epartscart]';
		}
	}
	return "HTTP {$code}{$hint}";
}

echo "=== Taxofinca vhost apply ===\n\n";
echo "Before origin: " . epc_tf_apply_probe('http://127.0.0.1/', $hostname) . "\n";
echo "Before public: " . epc_tf_apply_probe('https://' . $hostname . '/') . "\n\n";

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

$route = epc_clp_vhost_configure_tenant_via_varnish($cookie, $platformSite, $aliases, $platformSite);
foreach ($route['log'] as $line) {
	echo $line . "\n";
}

$touch = epc_clp_web_set_site_docroot($cookie, $platformSite, 'www.epartscart.com');
echo 'Docroot reset: ' . implode(' | ', $touch['log']) . "\n";

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($sharedRoot));
echo 'permissions code=' . $perm['code'] . "\n";

$purge = epc_clp_run("varnish-cache:purge --purge='https://{$hostname}/,https://{$bare}/'");
echo 'varnish purge code=' . $purge['code'] . "\n";

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
$hasWww = $vf['vhost'] !== '' && stripos($vf['vhost'], $hostname) !== false;
$has8080 = $vf['vhost'] !== '' && preg_match('/listen\s+8080;[\s\S]{0,800}' . preg_quote($hostname, '/') . '/i', $vf['vhost']);
echo 'template has www alias: ' . ($hasWww ? 'yes' : 'no') . "\n";
echo 'template has 8080+www: ' . ($has8080 ? 'yes' : 'no') . "\n";

echo "\nAfter origin: " . epc_tf_apply_probe('http://127.0.0.1/', $hostname) . "\n";
echo "After public: " . epc_tf_apply_probe('https://' . $hostname . '/') . "\n";
echo "CP: " . epc_tf_apply_probe('https://' . $hostname . '/cp/', '') . "\n";
echo "ERP: " . epc_tf_apply_probe('https://' . $hostname . '/erp', '') . "\n";

echo "\nIf still nginx403: CloudPanel → Sites → {$platformSite} → Vhost → Save (regenerates /etc/nginx).\n";
echo "Cloudflare: www + @ CNAME/A same as epartscart (proxied orange cloud), SSL Full, purge cache.\n";
