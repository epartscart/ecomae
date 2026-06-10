<?php
/**
 * On-origin nginx + SSL debug for tenant hostnames.
 * https://www.ecomae.com/epc-tenant-nginx-debug.php?token=...&host=www.thejewellerytrend.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$host = trim((string) ($_GET['host'] ?? 'www.thejewellerytrend.com'));
$ip = '31.97.216.247';

function epc_tnd(string $cmd): string
{
	$r = epc_clp_run_cmd($cmd);
	return '[exit=' . $r['code'] . '] ' . substr(trim((string) $r['output']), 0, 1200);
}

echo "=== tenant nginx debug host={$host} ===\n\n";

echo "=== curl origin HTTP ===\n";
echo epc_tnd("curl -sI --max-time 8 -H 'Host: {$host}' http://127.0.0.1/ 2>&1 | head -8") . "\n";
echo epc_tnd("curl -sI --max-time 8 -H 'Host: {$host}' http://127.0.0.1:8080/ 2>&1 | head -8") . "\n\n";

echo "=== openssl SNI @127.0.0.1 ===\n";
echo epc_tnd("echo | openssl s_client -connect 127.0.0.1:443 -servername {$host} 2>/dev/null | openssl x509 -noout -subject -ext subjectAltName 2>&1") . "\n";
echo epc_tnd("echo | openssl s_client -connect {$ip}:443 -servername {$host} 2>/dev/null | openssl x509 -noout -subject 2>&1") . "\n\n";

echo "=== cert files ===\n";
$p = epc_clp_ssl_certificate_paths($host);
echo $p !== null ? ($p['crt'] . ' OK') : 'MISSING' . "\n";
echo epc_tnd('ls /etc/nginx/ssl-certificates/ 2>&1 | tail -25') . "\n\n";

echo "=== nginx configs mentioning host ===\n";
echo epc_tnd("grep -rl " . escapeshellarg($host) . ' /etc/nginx/sites-enabled /etc/nginx/conf.d 2>/dev/null | head -20') . "\n";
echo epc_tnd("grep -n " . escapeshellarg($host) . ' /etc/nginx/sites-enabled/*.conf 2>/dev/null | head -30') . "\n\n";

echo "=== nginx -T server_name (grep) ===\n";
echo epc_tnd("nginx -T 2>/dev/null | grep -A3 " . escapeshellarg('server_name ' . $host) . " | head -40") . "\n\n";

echo "=== ss listeners ===\n";
echo epc_tnd('ss -tlnp | grep -E ":80|:443|:8080" 2>/dev/null') . "\n\n";

echo "Done.\n";
