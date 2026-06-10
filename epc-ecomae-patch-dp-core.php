<?php
/**
 * Push fixed core/dp_core.php to ecomae docroots via CloudPanel file manager.
 * https://www.epartscart.com/epc-ecomae-patch-dp-core.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}

$src = __DIR__ . '/core/dp_core.php';
if (!is_file($src)) {
	exit("Missing {$src} — deploy hotfix to epartscart first\n");
}
$content = file_get_contents($src);
if ($content === false || $content === '') {
	exit("Could not read dp_core.php\n");
}

$cookie = '';
$login = epc_clp_web_login('admin', $clpPass, $cookie);
if (empty($login['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

function ecomae_patch_put(string &$cookie, string $dir, string $relPath, string $content): void
{
	$panel = epc_clp_panel_url();
	$parts = explode('/', trim($relPath, '/'));
	$name = array_pop($parts);
	$parent = $dir;
	foreach ($parts as $part) {
		epc_clp_web_request($panel . '/file-manager/backend/mkdir', array(
			'method' => 'POST',
			'body' => http_build_query(array('id' => $parent, 'name' => $part)),
		), $cookie);
		$parent .= '/' . $part;
	}
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent, 'name' => $name)),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent . '/' . $name, 'content' => $content)),
	), $cookie);
}

foreach (array('www.ecomae.com', 'cp.ecomae.com') as $host) {
	$remoteDir = '/htdocs/' . $host;
	epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($host) . '/file-manager', array(), $cookie);
	ecomae_patch_put($cookie, $remoteDir, 'core/dp_core.php', $content);
	echo "Patched {$host}/core/dp_core.php\n";
}

$probe = __DIR__ . '/epc-ecomae-probe-config.php';
if (is_file($probe)) {
	$probeContent = file_get_contents($probe);
	foreach (array('www.ecomae.com') as $host) {
		ecomae_patch_put($cookie, '/htdocs/' . $host, 'epc-ecomae-probe-config.php', $probeContent);
		echo "Uploaded probe to {$host}\n";
	}
}
$probeIndex = __DIR__ . '/epc-ecomae-probe-index.php';
if (is_file($probeIndex)) {
	$probeContent = file_get_contents($probeIndex);
	ecomae_patch_put($cookie, '/htdocs/www.ecomae.com', 'epc-ecomae-probe-index.php', $probeContent);
	echo "Uploaded probe-index to www.ecomae.com\n";
}

$token = epc_deploy_token();
foreach (array('https://www.ecomae.com/', 'https://cp.ecomae.com/') as $url) {
	$body = @file_get_contents($url, false, stream_context_create(array(
		'http' => array('timeout' => 30),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	$preview = substr(preg_replace('/\s+/', ' ', (string) $body), 0, 120);
	echo "\n{$url}\n  " . ($preview !== '' ? $preview : '(empty)') . "\n";
}

echo "\nDone.\n";
