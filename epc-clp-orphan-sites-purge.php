<?php
/**
 * Delete orphan CloudPanel sites for Model C tenant hostnames (keeps www.ecomae.com).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$apply = !empty($_GET['apply']);
if ($clpPass === '') {
	exit("clp_pass required\n");
}

$hosts = array(
	'www.epartscart.com', 'epartscart.com',
	'www.taxofinca.com', 'taxofinca.com',
	'www.electronicae.com', 'electronicae.com',
	'www.stylenlook.com', 'stylenlook.com',
	'www.thejewellerytrend.com', 'thejewellerytrend.com',
	'www.thethejewellerytrend.com', 'thethejewellerytrend.com',
);

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "=== CLP orphan site purge ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$list = epc_clp_run('site:list');
echo "site:list exit=" . ($list['code'] ?? -1) . "\n";
echo substr((string) ($list['output'] ?? ''), 0, 2000) . "\n\n";

foreach ($hosts as $host) {
	if (!$apply) {
		echo "would delete {$host}\n";
		continue;
	}
	$del = epc_clp_web_delete_site($cookie, $host);
	echo $host . ': ' . implode(' ', array_slice($del['log'], 0, 3)) . "\n";
}

echo "\nDone.\n";
