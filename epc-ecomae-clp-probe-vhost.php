<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$html = epc_clp_web_request(epc_clp_panel_url() . '/site/www.ecomae.com/vhost', array(), $cookie);
echo "vhost len=" . strlen($html) . "\n";
if (preg_match_all('/name="([^"]+)"/', $html, $n)) {
	$names = array_unique($n[1]);
	foreach ($names as $name) {
		if (stripos($name, 'domain') !== false || stripos($name, 'alias') !== false || stripos($name, 'server') !== false || stripos($name, 'vhost') !== false) {
			echo $name . "\n";
		}
	}
}
if (preg_match_all('/<label[^>]*>([^<]+)</', $html, $labels)) {
	foreach (array_unique($labels[1]) as $l) {
		$l = trim($l);
		if ($l !== '' && (stripos($l, 'domain') !== false || stripos($l, 'alias') !== false)) {
			echo "label: {$l}\n";
		}
	}
}

$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
preg_match_all('#/site/([a-zA-Z0-9._-]+)#', $dash, $sites);
echo "\nsites on dashboard:\n" . implode("\n", array_unique($sites[1])) . "\n";
