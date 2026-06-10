<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/cp/shop/tenant_hub/tenant_hub?tab=onboard';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
$_SERVER['REQUEST_METHOD'] = 'GET';
chdir(__DIR__);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
$isFrontMode = 0;
require_once __DIR__ . '/core/dp_helper.php';
require_once __DIR__ . '/core/dp_content.php';
require_once __DIR__ . '/core/dp_module.php';
require_once __DIR__ . '/core/dp_template.php';

$core = file_get_contents(__DIR__ . '/core/dp_core.php');
$core = preg_replace('/^<\?php\s*/', '', $core, 1);
$core = preg_replace('/eval\(" \?\>" \. \$DP_Template->html \. "<\?php "\);\s*\$db_link = NULL;\s*\?\>\s*$/s', '', $core);
eval($core);

$lines = explode("\n", $DP_Template->html);
$total = count($lines);
echo "total_lines={$total}\n";
echo "content_url=" . ($DP_Content->url ?? '') . "\n";
echo "main_content_bytes=" . strlen($DP_Content->content) . "\n\n";

$focus = 1281;
for ($i = max(1, $focus - 8); $i <= min($total, $focus + 8); $i++) {
	echo str_pad((string) $i, 5, ' ', STR_PAD_LEFT) . '|' . ($lines[$i - 1] ?? '') . "\n";
}

$wrapped = " ?>" . $DP_Template->html . "<?php ";
$tmp = tempnam(sys_get_temp_dir(), 'dp_eval_');
file_put_contents($tmp, $wrapped);
echo "\nphp -l:\n";
passthru('php -l ' . escapeshellarg($tmp) . ' 2>&1');
@unlink($tmp);

// Find unbalanced php tags near error
echo "\nphp_open_tags_near_1281:\n";
for ($i = max(0, $focus - 30); $i < min($total, $focus + 10); $i++) {
	$line = $lines[$i];
	if (preg_match('/<\?/', $line)) {
		echo ($i + 1) . ': ' . trim($line) . "\n";
	}
}
