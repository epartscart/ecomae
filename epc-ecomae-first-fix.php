<?php
/**
 * Fix ecomae.com + cp.ecomae.com: services, SSL (525), probes.
 * Run on VPS (CLI): php epc-ecomae-first-fix.php TOKEN CLP_PASSWORD
 * Or web: ?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

$isCli = PHP_SAPI === 'cli';
if ($isCli) {
	$_GET['token'] = $argv[1] ?? '';
	$_GET['clp_pass'] = $argv[2] ?? '';
	$_GET['apply'] = '1';
}

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

header('Content-Type: text/plain; charset=utf-8');
set_time_limit(600);

$apply = !empty($_GET['apply']);
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$cpDocroot = '/home/ecomae/htdocs/cp.ecomae.com';

function epc_eff_run(string $cmd): array
{
	$out = array();
	$code = 0;
	@exec($cmd . ' 2>&1', $out, $code);
	return array('cmd' => $cmd, 'code' => $code, 'output' => implode("\n", $out));
}

function epc_eff_probe(string $url, string $host): array
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 10, 'header' => "Host: {$host}\r\n", 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	return array('code' => $code, 'ms' => $ms, 'ok' => $code >= 200 && $code < 400);
}

echo "=== ecomae first fix ===\n";
echo 'time=' . gmdate('c') . "\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$domains = array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com');

echo "=== BEFORE probes (127.0.0.1) ===\n";
foreach (array(
	array('www.ecomae.com', 'http://127.0.0.1/'),
	array('www.ecomae.com', 'https://127.0.0.1/'),
	array('cp.ecomae.com', 'http://127.0.0.1/'),
) as $p) {
	$r = epc_eff_probe($p[1], $p[0]);
	echo "  {$p[0]} {$p[1]}: HTTP {$r['code']} {$r['ms']}ms\n";
}

if ($apply) {
	echo "\n=== Services ===\n";
	foreach (array(
		'systemctl start mariadb 2>/dev/null || systemctl start mysql 2>/dev/null',
		'systemctl start php8.3-fpm 2>/dev/null || systemctl start php8.2-fpm 2>/dev/null || systemctl start php-fpm 2>/dev/null',
		'systemctl start nginx 2>/dev/null',
		'nginx -t',
		'systemctl reload nginx 2>/dev/null || service nginx reload 2>/dev/null',
	) as $cmd) {
		$r = epc_eff_run($cmd);
		echo trim($r['output']) . " [{$r['code']}]\n";
	}

	if ($clpPass !== '' && function_exists('epc_clp_web_login')) {
		echo "\n=== CloudPanel SSL (Let's Encrypt) ===\n";
		$cookie = '';
		$login = epc_clp_web_login('admin', $clpPass, $cookie);
		echo 'CLP login: ' . ($login ? 'ok' : 'fail') . "\n";
		if (function_exists('epc_clp_web_install_ssl')) {
			$ssl = epc_clp_web_install_ssl($cookie, 'www.ecomae.com', array('ecomae.com'));
			echo 'www.ecomae.com SSL: ' . json_encode($ssl, JSON_UNESCAPED_SLASHES) . "\n";
			$ssl2 = epc_clp_web_install_ssl($cookie, 'cp.ecomae.com', array());
			echo 'cp.ecomae.com SSL: ' . json_encode($ssl2, JSON_UNESCAPED_SLASHES) . "\n";
		}
	}

	if (epc_clp_available()) {
		echo "\n=== clpctl SSL ===\n";
		foreach (array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com') as $dom) {
			$r = epc_clp_run('lets-encrypt:install:certificate --domainName=' . escapeshellarg($dom));
			echo "{$dom}: exit={$r['code']} " . substr(trim($r['output']), 0, 200) . "\n";
		}
	}

	echo "\n=== Cert files ===\n";
	echo epc_eff_run('ls -la /etc/nginx/ssl-certificates/ 2>/dev/null | head -20')['output'] . "\n";
}

echo "\n=== AFTER probes ===\n";
foreach (array(
	array('www.ecomae.com', 'http://127.0.0.1/'),
	array('www.ecomae.com', 'https://127.0.0.1/'),
	array('cp.ecomae.com', 'http://127.0.0.1/'),
	array('cp.ecomae.com', 'https://127.0.0.1/'),
) as $p) {
	$r = epc_eff_probe($p[1], $p[0]);
	$ok = $r['ok'] ? 'OK' : 'FAIL';
	echo "  {$p[0]} {$p[1]}: HTTP {$r['code']} {$r['ms']}ms {$ok}\n";
}

echo "\n=== Next ===\n";
echo "1. Cloudflare SSL/TLS -> Full (strict) after https://127.0.0.1/ is 200\n";
echo "2. Purge Cloudflare cache for ecomae.com\n";
echo "3. From PC: python tools/deploy_ecomae_first.py\n";
echo "4. https://www.ecomae.com/epc-ecomae-force-marketing-home.php?token=...&apply=1\n";
echo "5. Tenants: epc-tenants-connectivity-fix.php?apply=1\n";
