<?php
/**
 * Legacy demo URL — redirects to the storefront demand page (token required).
 */
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$brand = isset($_GET['brand']) ? trim((string)$_GET['brand']) : 'TOYOTA';
$article = isset($_GET['article']) ? trim((string)$_GET['article']) : '1310154101';
$qs = http_build_query(array_filter(array('brand' => $brand, 'article' => $article)));
$target = '/en/demand-intelligence' . ($qs !== '' ? '?' . $qs : '');
header('Location: ' . $target, true, 302);
exit;
