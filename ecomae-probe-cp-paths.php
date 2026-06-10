<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$paths = array(
	'/home/ecomae/htdocs/cp.ecomae.com/cp/index.php',
	'/home/ecomae/htdocs/cp.ecomae.com/cp.ecomae.com/cp/index.php',
	'/home/ecomae/htdocs/www.ecomae.com/cp/index.php',
);
foreach ($paths as $p) {
	echo $p . ' => ' . (is_file($p) ? 'yes ' . filesize($p) : 'no') . "\n";
}
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
$out = epc_clp_run('nginx:test');
echo 'nginx_test=' . substr($out['output'] ?? '', 0, 200) . "\n";
$grep = epc_clp_run('bash -lc ' . escapeshellarg('grep -R "cp.ecomae.com" /etc/nginx/sites-enabled/ 2>/dev/null | head -20'));
echo ($grep['output'] ?? '') . "\n";
