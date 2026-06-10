<?php
/**
 * cp.ecomae.com → 301 redirect to www.ecomae.com (same paths). Super CP works at www.ecomae.com/cp/
 * https://www.ecomae.com/ecomae-cp-redirect-vhost.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$cpSite = 'cp.ecomae.com';
$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($cpSite) . '/vhost', array(), $cookie);
if (!preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
	exit("cp vhost token missing\n");
}

$redirectBlock = <<<'NGINX'
server {
  listen 80;
  listen [::]:80;
  listen 443 ssl http2;
  listen [::]:443 ssl http2;
  {{ssl_certificate_key}}
  {{ssl_certificate}}
  server_name cp.ecomae.com;
  return 301 https://www.ecomae.com$request_uri;
}

NGINX;

// Preserve CLP SSL placeholders if present in current template.
$vhost = '';
if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $vhHtml, $em)) {
	$vhost = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} elseif (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $tm)) {
	$vhost = html_entity_decode($tm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}
if ($vhost !== '' && preg_match('/\{\{ssl_certificate_key\}\}/', $vhost)) {
	// Replace cp.ecomae.com server block with redirect-only block.
	$newVhost = preg_replace('/server\s*\{[\s\S]*?server_name[^;]*cp\.ecomae\.com[\s\S]*?\n\}/m', trim($redirectBlock) . "\n", $vhost, 1, $count);
	if ($count > 0) {
		$vhost = $newVhost;
		echo "patched existing cp server block -> redirect\n";
	} else {
		$vhost = $redirectBlock . "\n" . $vhost;
		echo "prepended cp redirect server block\n";
	}
} else {
	echo "Could not patch vhost template safely — use Cloudflare redirect: cp.ecomae.com/* -> https://www.ecomae.com/$1\n";
	exit(1);
}

epc_clp_web_request($panel . '/site/' . rawurlencode($cpSite) . '/vhost', array(
	'method' => 'POST',
	'body' => http_build_query(array(
		'vhost-update' => '1',
		'vhost-template' => $vhost,
		'token' => $vt[1],
	)),
), $cookie);
echo "cp vhost saved\n";

$ssl = epc_clp_web_install_ssl($cookie, $cpSite);
echo 'ssl: ' . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

function ecp_redir_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	@file_get_contents($url, false, $ctx);
	$code = 0;
	$loc = '';
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	foreach ($http_response_header ?? array() as $h) {
		if (stripos($h, 'Location:') === 0) {
			$loc = trim(substr($h, 9));
		}
	}
	return "HTTP {$code}" . ($loc !== '' ? " -> {$loc}" : '');
}

echo "\n=== Probes ===\n";
echo "cp hub redirect → " . ecp_redir_probe('https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard') . "\n";
echo "www hub → " . ecp_redir_probe('https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard') . "\n";
