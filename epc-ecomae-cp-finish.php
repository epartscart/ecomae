<?php
/**
 * Finish cp.ecomae.com: ensure site exists, sync files from www, install SSL.
 * https://www.epartscart.com/epc-ecomae-cp-finish.php?token=...&clp_pass=...&db_password=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
$siteUser = trim((string) ($_GET['site_user'] ?? 'ecomaecp'));
$sitePass = trim((string) ($_GET['site_user_password'] ?? ''));
$token = epc_deploy_token();

if ($clpPass === '') {
	exit("clp_pass required\n");
}
if ($sitePass === '') {
	$sitePass = 'EcomaeCp2026!';
}

$cookie = '';
$login = epc_clp_web_login('admin', $clpPass, $cookie);
if (empty($login['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

$sites = epc_clp_web_sites($cookie);
$dashHtml = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
echo "Dashboard sites: " . implode(', ', $sites) . "\n";

$cpDomain = 'cp.ecomae.com';
$wwwDomain = 'www.ecomae.com';
$cpDocroot = epc_clp_guess_docroot($siteUser, $cpDomain);
$wwwDocroot = epc_clp_guess_docroot('ecomae', $wwwDomain);
echo "www docroot={$wwwDocroot} exists=" . (is_dir($wwwDocroot) ? 'yes' : 'no') . "\n";
echo "cp docroot={$cpDocroot} exists=" . (is_dir($cpDocroot) ? 'yes' : 'no') . "\n";

$cpListed = epc_clp_web_site_listed($dashHtml, $cpDomain);
if (!$cpListed) {
	echo "Creating cp.ecomae.com site in CloudPanel...\n";
	$r = epc_clp_web_create_php_site($cookie, array(
		'domain' => $cpDomain,
		'site_user' => $siteUser,
		'site_user_password' => $sitePass,
		'php_version' => '8.3',
	));
	echo implode("\n", $r['log']) . "\n";
	$cpDocroot = epc_clp_guess_docroot($siteUser, $cpDomain);
	echo "cp docroot after create={$cpDocroot} exists=" . (is_dir($cpDocroot) ? 'yes' : 'no') . "\n";
	$dashHtml = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
	$cpListed = epc_clp_web_site_listed($dashHtml, $cpDomain);
	echo "cp listed after create=" . ($cpListed ? 'yes' : 'no') . "\n";
}

function ecomae_finish_put(string &$cookie, string $dir, string $relPath, string $content): void
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

$exportScript = file_get_contents(__DIR__ . '/epc-ecomae-www-export.php');
$importScript = file_get_contents(__DIR__ . '/epc-ecomae-cp-import-tar.php');
if ($exportScript !== false && $exportScript !== '') {
	epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($wwwDomain) . '/file-manager', array(), $cookie);
	ecomae_finish_put($cookie, '/htdocs/' . $wwwDomain, 'epc-ecomae-www-export.php', $exportScript);
	ecomae_finish_put($cookie, '/htdocs/' . $wwwDomain, 'epc_deploy_auth.php', file_get_contents(__DIR__ . '/epc_deploy_auth.php'));
	echo "Uploaded export script to www\n";
}
if ($importScript !== false && $importScript !== '' && $cpListed) {
	epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($cpDomain) . '/file-manager', array(), $cookie);
	ecomae_finish_put($cookie, '/htdocs/' . $cpDomain, 'epc-ecomae-cp-import-tar.php', $importScript);
	ecomae_finish_put($cookie, '/htdocs/' . $cpDomain, 'epc_deploy_auth.php', file_get_contents(__DIR__ . '/epc_deploy_auth.php'));
	echo "Uploaded import script to cp\n";
}

$exportUrl = 'https://' . $wwwDomain . '/epc-ecomae-www-export.php?token=' . urlencode($token);
$exportResp = @file_get_contents($exportUrl, false, stream_context_create(array(
	'http' => array('timeout' => 600),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\n=== www export ===\n" . substr((string) $exportResp, 0, 800) . "\n";

$importUrl = 'https://' . $cpDomain . '/epc-ecomae-cp-import-tar.php?token=' . urlencode($token);
if ($dbPass !== '') {
	$importUrl .= '&db_password=' . urlencode($dbPass);
}
$importResp = @file_get_contents($importUrl, false, stream_context_create(array(
	'http' => array('timeout' => 600),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\n=== cp import ===\n" . substr((string) $importResp, 0, 2500) . "\n";

if ($dbPass !== '') {
	$cfgPhp = <<<'PHP'
<?php
$epc_config_local = array(
	'password' => 'DBPASS',
	'db' => 'ecomae',
	'user' => 'ecomae',
	'from_name' => 'ecomae',
	'from_email' => 'hello@ecomae.com',
);
PHP;
	$wwwCfg = str_replace('DBPASS', addslashes($dbPass), $cfgPhp);
	$wwwCfg = str_replace("'from_name' => 'ecomae'", "'domain_path' => 'https://www.ecomae.com/',\n\t'from_name' => 'ecomae'", $wwwCfg);
	$cpCfg = str_replace('DBPASS', addslashes($dbPass), $cfgPhp);
	$cpCfg = str_replace("'from_name' => 'ecomae'", "'domain_path' => 'https://cp.ecomae.com/',\n\t'from_name' => 'ecomae Platform'", $cpCfg);
	epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($wwwDomain) . '/file-manager', array(), $cookie);
	ecomae_finish_put($cookie, '/htdocs/' . $wwwDomain, 'config.local.php', $wwwCfg);
	epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($cpDomain) . '/file-manager', array(), $cookie);
	ecomae_finish_put($cookie, '/htdocs/' . $cpDomain, 'config.local.php', $cpCfg);
	echo "Updated config.local.php on www + cp\n";
}

foreach (array($cpDomain, $wwwDomain) as $domain) {
	echo "\n=== SSL {$domain} ===\n";
	$ssl = epc_clp_web_install_ssl($cookie, $domain);
	echo implode("\n", $ssl['log']) . "\n";
	sleep(3);
}

echo "\n=== verify ===\n";
foreach (array(
	'https://cp.ecomae.com/',
	'https://cp.ecomae.com/cp/',
	'https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub',
) as $url) {
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 30),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$headers = isset($http_response_header) ? $http_response_header : array();
	$status = isset($headers[0]) ? $headers[0] : 'unknown';
	$preview = substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 100);
	echo "{$url}\n  {$status}\n  " . ($preview !== '' ? $preview : '(empty)') . "\n";
}

echo "\nDone.\n";
