<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$p = __DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_main.php';
echo 'size=' . filesize($p) . "\n";
echo substr(file_get_contents($p), 0, 400) . "\n...\n";
echo substr(file_get_contents($p), -120) . "\n";
