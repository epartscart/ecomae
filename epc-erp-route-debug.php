<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
echo '__DIR__=' . __DIR__ . "\n";
echo 'REQUEST_URI=' . ($_SERVER['REQUEST_URI'] ?? '') . "\n";
echo 'HTTP_HOST=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
$idx = (string) @file_get_contents(__DIR__ . '/index.php');
echo 'index_has_erp_route=' . (strpos($idx, 'epc_erp_portal_try_route') !== false ? 'yes' : 'no') . "\n";
require_once __DIR__ . '/content/shop/finance/epc_erp_portal_router.php';
$m = epc_erp_portal_match_request('/erp/');
echo 'match_/erp/=' . ($m === null ? 'null' : json_encode($m)) . "\n";
$m2 = epc_erp_portal_match_request();
echo 'match_live=' . ($m2 === null ? 'null' : json_encode($m2)) . "\n";
