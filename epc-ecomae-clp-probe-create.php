<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$panel = epc_clp_panel_url();

foreach (array('settings', 'domains', 'domain', 'vhost') as $page) {
	$html = epc_clp_web_request($panel . '/site/www.ecomae.com/' . $page, array(), $cookie);
	echo "=== {$page} len=" . strlen($html) . " ===\n";
	preg_match_all('/name="([^"]+)"/', $html, $n);
	foreach (array_unique($n[1]) as $name) {
		if (preg_match('/domain|alias|server/i', $name)) {
			echo "  {$name}\n";
		}
	}
}

$form = epc_clp_web_request($panel . '/site/new/php', array(), $cookie);
preg_match('/name="site_new_php\[_token\]" value="([^"]+)"/', $form, $m);
$body = http_build_query(array(
	'site_new_php' => array(
		'application' => 'Generic',
		'domainName' => 'cp.ecomae.com',
		'phpVersion' => '8.3',
		'siteUser' => 'ecomaecp',
		'siteUserPassword' => 'EcomaeCp2026!',
		'submit' => 'Create',
		'_token' => $m[1],
	),
));
$resp = epc_clp_web_request($panel . '/site/new/php', array('method' => 'POST', 'body' => $body), $cookie);
echo "\ncreate ecomaecp:\n";
if (preg_match('/alert-danger[^>]*>(.*?)<\/div/is', $resp, $err)) {
	echo "error: " . trim(strip_tags($err[1])) . "\n";
}
if (preg_match('/alert-success[^>]*>(.*?)<\/div/is', $resp, $ok)) {
	echo "success: " . trim(strip_tags($ok[1])) . "\n";
}
$dash = epc_clp_web_request($panel . '/', array(), $cookie);
echo "cp listed: " . (stripos($dash, '/site/cp.ecomae.com') !== false ? 'yes' : 'no') . "\n";
echo "ecomaecp home: " . (is_dir('/home/ecomaecp/htdocs/cp.ecomae.com') ? 'yes' : 'no') . "\n";
