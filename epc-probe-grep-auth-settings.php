<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$f = __DIR__ . '/cp/content/control/portal/epc_cp_auth_settings.php';
$raw = is_file($f) ? (string) file_get_contents($f) : '';
echo "file={$f} bytes=" . strlen($raw) . "\n";
echo 'getUserParam=' . (strpos($raw, 'getUserParam') !== false ? 'YES' : 'no') . "\n";
echo 'getAdminProfile=' . (strpos($raw, 'getAdminProfile') !== false ? 'yes' : 'no') . "\n";
if (preg_match('/adminEmail[^\n]{0,200}/', $raw, $m)) {
	echo 'adminEmail_snippet=' . trim($m[0]) . "\n";
}
