<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$paths = array(
	__DIR__ . '/cp/templates/bootstrap_admin/scripts/homer.js',
	'/home/ecomae/htdocs/www.ecomae.com/cp/templates/bootstrap_admin/scripts/homer.js',
);
foreach ($paths as $p) {
	echo $p . ' exists=' . (is_file($p) ? 'yes bytes=' . filesize($p) : 'no') . "\n";
}
