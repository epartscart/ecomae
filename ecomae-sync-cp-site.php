<?php
/**
 * Sync www.ecomae.com docroot → cp.ecomae.com site and fix its nginx /cp/ routing.
 * https://www.ecomae.com/ecomae-sync-cp-site.php?token=...&clp_pass=...
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

$src = '/home/ecomae/htdocs/www.ecomae.com';
$dst = '/home/ecomae/htdocs/cp.ecomae.com';
$dstNested = $dst . '/cp.ecomae.com';
if (!is_dir($src)) {
	exit("source missing {$src}\n");
}
if (!is_dir($dst)) {
	mkdir($dst, 0755, true);
}

echo "=== rsync {$src} -> {$dst} ===\n";
$cmd = 'rsync -a --delete ' . escapeshellarg(rtrim($src, '/') . '/') . ' ' . escapeshellarg(rtrim($dst, '/') . '/') . ' 2>&1';
exec($cmd, $rsOut, $rsCode);
echo "rsync code={$rsCode}\n";
if ($rsOut) {
	echo implode("\n", array_slice($rsOut, 0, 5)) . "\n";
}

if (!is_dir($dstNested)) {
	mkdir($dstNested, 0755, true);
}
echo "=== rsync {$src} -> {$dstNested} (CLP nested docroot) ===\n";
$cmd2 = 'rsync -a --delete ' . escapeshellarg(rtrim($src, '/') . '/') . ' ' . escapeshellarg(rtrim($dstNested, '/') . '/') . ' 2>&1';
exec($cmd2, $rsOut2, $rsCode2);
echo "rsync nested code={$rsCode2}\n";

$cfgWww = $src . '/config.local.php';
if (is_file($cfgWww)) {
	$cfg = str_replace('www.ecomae.com', 'cp.ecomae.com', (string) file_get_contents($cfgWww));
	file_put_contents($dst . '/config.local.php', $cfg);
	if (is_dir($dstNested)) {
		file_put_contents($dstNested . '/config.local.php', $cfg);
	}
	echo "wrote cp config.local.php (flat + nested)\n";
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
$vhost = '';
if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $vhHtml, $em)) {
	$vhost = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} elseif (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $tm)) {
	$vhost = html_entity_decode($tm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}
if ($vhost === '') {
	exit("cp vhost body missing\n");
}

$changed = false;
$cpBlock = <<<'NGINX'

  location = /cp {
    return 301 https://$host/cp/control;
  }

  location = /cp/ {
    return 301 https://$host/cp/control;
  }

  location /cp/ {
    try_files $uri $uri/ /cp/index.php?$args;
  }

NGINX;

if (stripos($vhost, 'location /cp/') === false) {
	$vhost = preg_replace('/(\s+try_files \$uri \$uri\/ \/index\.php\?\$args;)/', $cpBlock . '$1', $vhost, 1, $cpCount);
	if (!empty($cpCount)) {
		$changed = true;
		echo "added /cp/ location to cp.ecomae.com vhost\n";
	}
}

if (preg_match('/^\s*root\s+([^;]+);/m', $vhost, $rm)) {
	echo 'cp vhost root=' . trim($rm[1]) . "\n";
}

if ($changed) {
	epc_clp_web_request($panel . '/site/' . rawurlencode($cpSite) . '/vhost', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'vhost-update' => '1',
			'vhost-template' => $vhost,
			'token' => $vt[1],
		)),
	), $cookie);
	echo "cp vhost saved\n";
} else {
	echo "cp vhost already has /cp/ block\n";
}

$ssl = epc_clp_web_install_ssl($cookie, $cpSite);
echo 'ssl cp: ' . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

exec('chmod -R o+rX ' . escapeshellarg($dst) . ' ' . escapeshellarg($dstNested) . ' 2>&1', $chmodOut, $chmodCode);
echo "chmod code={$chmodCode}\n";

function ecp_sync_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flat = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 90) : '';
	return "HTTP {$code} — {$flat}";
}

echo "\n=== Probes ===\n";
echo "cp hub → " . ecp_sync_probe('https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard') . "\n";
echo "www hub → " . ecp_sync_probe('https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard') . "\n";
