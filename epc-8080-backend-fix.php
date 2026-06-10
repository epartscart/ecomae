<?php
/**
 * 8080 PHP backend probe + CloudPanel vhost save instructions.
 * Symptom: port 443 301/404, curl -H 'Host: www.ecomae.com' http://127.0.0.1:8080/ → 404.
 *
 * https://www.ecomae.com/epc-8080-backend-fix.php?token=epartscart-deploy-2026
 * https://www.ecomae.com/epc-8080-backend-fix.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

$apply = !empty($_GET['apply']);
$docroot = '/home/ecomae/htdocs/www.ecomae.com';
$platformConf = '/etc/nginx/sites-enabled/www.ecomae.com.conf';
$pasteRel = 'go-live-logs/ecomae-vhost-paste-for-kodee.txt';

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

function epc_8080_probe(string $url, string $host): array
{
	$headers = $host !== '' ? ("Host: {$host}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 12, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$nginxDefault = is_string($body) && (stripos($body, 'nginx') !== false && stripos($body, '404') !== false);
	return array('code' => $code, 'ms' => $ms, 'nginx404' => $nginxDefault);
}

echo "=== EPC 8080 BACKEND FIX — OPERATOR ONE-PAGE ===\n";
echo 'time=' . date('c') . '  apply=' . ($apply ? '1' : '0') . "\n\n";

echo "ROOT CAUSE\n";
echo "  Varnish/front nginx proxies PHP to 127.0.0.1:8080.\n";
echo "  Missing listen 8080 { server_name www.ecomae.com; root {$docroot}; fastcgi... } → 404 marketing + /cp/.\n";
echo "  Port 443 may redirect (301) while backend 8080 is dead.\n\n";

echo "JEWELLERY 526 (Cloudflare Strict)\n";
echo "  Origin SNI www.thejewellerytrend.com presents CN=www.ecomae.com (Model C platform cert).\n";
echo "  CF SSL/TLS → Full (not Strict) until tenant blocks use platform cert in vhost paste.\n";
echo "  After paste + reload: SNI subject should be CN=www.ecomae.com for all tenants → Strict OK.\n\n";

echo "FIX IN CLOUDPANEL (Kodee — paste ALL, Save)\n";
echo "  1) https://31.97.216.247:8443 → Sites → www.ecomae.com → Vhost tab\n";
echo "  2) Select entire editor contents → Delete → Paste from repo:\n";
echo "     {$pasteRel}\n";
echo "  3) Must include:\n";
echo "     - server { listen 8080; server_name www.ecomae.com; {{root}} → {$docroot}\n";
echo "     - server { listen 443 ssl; server_name www.ecomae.com; varnish proxy... }\n";
echo "     - # EPC_TENANT_DIRECT_START … END (5 tenants, root {$docroot})\n";
echo "     - # EPC_TENANT_APEX_REDIRECT_START … END\n";
echo "  4) Click Save (not Preview). Wait for green success.\n\n";

echo "VERIFY ON VPS (SSH / hPanel terminal as root)\n";
echo "  grep -c 'listen 8080' {$platformConf}     # expect >= 1\n";
echo "  grep -c 'server_name www.ecomae.com' {$platformConf}  # expect >= 2\n";
echo "  nginx -t && systemctl reload nginx\n";
echo "  curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1:8080/ | head -1   # NOT 404\n";
echo "  curl -sI -H 'Host: www.ecomae.com' http://127.0.0.1/ | head -1        # 200/301 via varnish\n";
echo "  bash /root/FIX-8080-NOW.sh   (or backups/FIX-8080-NOW.sh from repo)\n\n";

echo "AFTER ORIGIN 200 — from dev machine\n";
echo "  set CLP_PASS=...\n";
echo "  python tools/ecomae_restore_when_up.py\n";
echo "  curl \"https://www.ecomae.com/epc-jewellery-fast-live.php?token=...&clp_pass=...&apply=1\"\n";
echo "  curl \"https://www.ecomae.com/epc-ecomae-force-marketing-home.php?token=...&apply=1\"\n\n";

echo str_repeat('=', 60) . "\n";
echo "LIVE PROBES (this server)\n\n";

$hosts = array(
	'www.ecomae.com' => array('/', '/cp/', '/index.php'),
	'www.thejewellerytrend.com' => array('/en/'),
	'www.epartscart.com' => array('/en/'),
	'www.taxofinca.com' => array('/en/'),
	'www.electronicae.com' => array('/en/'),
	'www.stylenlook.com' => array('/en/'),
);

foreach ($hosts as $host => $paths) {
	foreach ($paths as $path) {
		$r8080 = epc_8080_probe('http://127.0.0.1:8080' . $path, $host);
		$r80 = epc_8080_probe('http://127.0.0.1' . $path, $host);
		echo "  :8080 {$host}{$path} -> HTTP {$r8080['code']} ({$r8080['ms']}ms)";
		if ($r8080['nginx404']) {
			echo ' [nginx-default-404]';
		}
		echo "\n";
		echo "  :80   {$host}{$path} -> HTTP {$r80['code']} ({$r80['ms']}ms)\n";
	}
}

echo "\n=== vhost on disk ===\n";
foreach (array(
	"test -f {$platformConf} && echo EXISTS || echo MISSING",
	"grep -c 'listen 8080' {$platformConf} 2>/dev/null || echo 0",
	"grep -c 'server_name www.ecomae.com' {$platformConf} 2>/dev/null || echo 0",
	"grep -E 'listen 8080|server_name www\\.ecomae|root ' {$platformConf} 2>/dev/null | head -12",
	'ss -lntp 2>/dev/null | grep -E ":8080|:443|:80 " || true',
) as $cmd) {
	echo epc_clp_run_cmd($cmd)['output'] . "\n";
}

echo "\n=== jewellery SNI ===\n";
echo epc_clp_run_cmd(
	"echo | openssl s_client -connect 127.0.0.1:443 -servername www.thejewellerytrend.com 2>/dev/null | openssl x509 -noout -subject 2>&1"
)['output'] . "\n";

if ($apply) {
	echo "\n=== APPLY (reload stack — does NOT recreate missing vhost) ===\n";
	foreach (array(
		'systemctl restart php8.3-fpm 2>&1 || systemctl restart php8.2-fpm 2>&1 || systemctl restart php-fpm 2>&1',
		'nginx -t 2>&1',
		'systemctl reload nginx 2>&1 || service nginx reload 2>&1',
		'systemctl reload varnish 2>&1 || true',
	) as $cmd) {
		$r = epc_clp_run_cmd($cmd);
		echo trim($cmd) . ': ' . trim($r['output']) . " [exit={$r['code']}]\n";
	}
}

$home8080 = epc_8080_probe('http://127.0.0.1:8080/', 'www.ecomae.com');
echo "\n=== VERDICT ===\n";
if ($home8080['code'] === 404 || $home8080['code'] === 0) {
	echo "BLOCKED: 8080 backend still down. CloudPanel Vhost Save required (paste file above).\n";
	echo "Then: bash /root/FIX-8080-NOW.sh\n";
} elseif ($home8080['code'] >= 200 && $home8080['code'] < 500) {
	echo "8080 UP (HTTP {$home8080['code']}) — run python tools/ecomae_restore_when_up.py from laptop.\n";
}
