<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$p = __DIR__ . '/index.php';
if (!is_file($p)) {
	exit("missing\n");
}
$c = (string) file_get_contents($p);
echo 'bytes=' . strlen($c) . "\n";
echo "has_epc_portal_apply=" . (strpos($c, 'epc_portal_apply_config') !== false ? 'yes' : 'no') . "\n";
echo "has_cp_delegate=" . (strpos($c, 'Nginx try_files sends /cp/*') !== false ? 'yes' : 'no') . "\n";
echo "---HEAD---\n";
echo substr($c, 0, 2500);
echo "\n---SNIP around config.php---\n";
$pos = strpos($c, 'config.php');
if ($pos !== false) {
	echo substr($c, max(0, $pos - 200), 1200);
}
