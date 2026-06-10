<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

echo "clp_bin=" . epc_clp_bin() . "\n";
echo "clp_available=" . (epc_clp_available() ? 'yes' : 'no') . "\n";
$r = epc_clp_run('webserver:reload');
echo "reload exit=" . $r['code'] . "\n" . substr((string) $r['output'], 0, 500) . "\n";

$hosts = array('www.thejewellerytrend.com', 'www.taxofinca.com');
foreach ($hosts as $h) {
	echo "\n--- {$h} ---\n";
	echo epc_clp_run_cmd("echo | openssl s_client -connect 127.0.0.1:443 -servername {$h} 2>/dev/null | openssl x509 -noout -subject 2>&1")['output'] . "\n";
	echo epc_clp_run_cmd("curl -sI --max-time 5 -H 'Host: {$h}' http://127.0.0.1/ 2>&1 | head -3")['output'] . "\n";
}

echo "\norphan configs:\n";
foreach (epc_clp_nginx_find_configs_for_hosts(array('www.thejewellerytrend.com'), 'www.ecomae.com') as $c) {
	echo "  {$c}\n";
}
