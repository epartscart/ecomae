<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$path = __DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_main_page.php';
echo "size=" . filesize($path) . "\n";
echo file_get_contents($path);
