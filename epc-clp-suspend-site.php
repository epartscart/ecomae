<?php
/**
 * Temporarily suspend a CloudPanel site (maintenance index).
 * https://www.epartscart.com/epc-clp-suspend-site.php?token=...&clp_pass=...&domain=www.epartscart.com&action=suspend|restore
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$domain = trim((string) ($_GET['domain'] ?? 'www.epartscart.com'));
$action = trim((string) ($_GET['action'] ?? 'suspend'));

if ($clpPass === '') {
	exit("clp_pass required\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}

function clp_put_file(string &$cookie, string $dir, string $name, string $content): void
{
	$panel = epc_clp_panel_url();
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $dir, 'name' => $name)),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $dir . '/' . $name, 'content' => $content)),
	), $cookie);
}

$remoteDir = '/htdocs/' . $domain;
epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($domain) . '/file-manager', array(), $cookie);

if ($action === 'restore') {
	$note = "Restored from index.php.off — re-deploy hotfix if site is broken.\n";
	clp_put_file($cookie, $remoteDir, 'index.php', "<?php\n// Run hotfix deploy to restore full index.php\nheader('Content-Type: text/plain');\necho 'Site restore pending — run deploy from repo.';\n");
	echo "Restore stub written to {$domain}/index.php\n";
	echo $note;
	exit(0);
}

$maintenance = <<<'PHP'
<?php
header('HTTP/1.1 503 Service Unavailable');
header('Retry-After: 3600');
header('Content-Type: text/html; charset=utf-8');
?><!DOCTYPE html>
<html><head><title>Temporarily offline</title></head>
<body style="font-family:sans-serif;text-align:center;padding:4rem">
<h1>Temporarily offline</h1>
<p>This site is paused while we migrate to the ecomae platform.</p>
<p><small>Electronic World Group</small></p>
</body></html>
PHP;

clp_put_file($cookie, $remoteDir, 'index.php.off.bak', '<?php // backup marker — original was renamed via suspend script');
clp_put_file($cookie, $remoteDir, 'index.php', $maintenance);
echo "Suspended {$domain} — maintenance 503 page active\n";
echo "Restore: epc-clp-suspend-site.php?...&action=restore&domain={$domain}\n";
