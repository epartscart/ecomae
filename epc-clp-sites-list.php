<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$cookie = '';
if (empty(epc_clp_web_login('admin', $pass, $cookie)['ok'])) {
	exit("login fail\n");
}
$sites = epc_clp_web_sites($cookie);
echo "sites:\n";
foreach ($sites as $s) {
	echo "  {$s}\n";
}
foreach (array('www.thejewellerytrend.com', 'www.taxofinca.com', 'www.ecomae.com') as $d) {
	echo $d . ' listed=' . (epc_clp_web_site_listed(epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie), $d) ? 'yes' : 'no') . "\n";
}
