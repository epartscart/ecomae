<?php
/**
 * One-shot origin repair (run on VPS). Upload to docroot, open in browser once, delete after.
 * https://www.ecomae.com/epc-vps-repair.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(600);

function epc_vr(string $cmd): string
{
	$out = array();
	$code = 0;
	@exec($cmd . ' 2>&1', $out, $code);
	return '[' . $code . "] {$cmd}\n" . implode("\n", $out);
}

echo "=== epc-vps-repair " . gmdate('c') . " ===\n\n";
foreach (array(
	'systemctl restart php8.3-fpm nginx 2>/dev/null',
	'systemctl restart varnish 2>/dev/null',
	'clpctl site:list 2>/dev/null',
	'clpctl lets-encrypt:install:certificate --domainName=www.ecomae.com 2>/dev/null',
	'clpctl lets-encrypt:install:certificate --domainName=ecomae.com 2>/dev/null',
	'nginx -t 2>&1',
	'systemctl reload nginx 2>/dev/null',
	'ss -tlnp | grep -E ":80|:443|:8080" 2>/dev/null || true',
	'curl -sI --resolve www.ecomae.com:8080:127.0.0.1 http://www.ecomae.com/ 2>&1 | head -3',
	'curl -sIk --resolve www.ecomae.com:443:127.0.0.1 https://www.ecomae.com/ 2>&1 | head -3',
) as $cmd) {
	echo epc_vr($cmd) . "\n";
}
echo "=== done ===\n";
