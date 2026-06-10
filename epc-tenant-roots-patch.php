<?php
/**
 * Patch every Model C tenant direct block: replace {{root}} with platform docroot.
 * https://www.ecomae.com/epc-tenant-roots-patch.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$apply = !empty($_GET['apply']);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';

echo "=== epc-tenant-roots-patch ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

if ($clpPass === '') {
	exit("clp_pass required\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login: OK\n";

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
$before = substr_count($vf['vhost'], '{{root}}');
echo "vhost_len=" . strlen($vf['vhost']) . " {{root}}_count={$before}\n";

$rootLine = '  root ' . rtrim($platformDocroot, '/') . ';';
$patched = (string) preg_replace_callback(
	'/# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END/',
	function (array $m) use ($rootLine) {
		$block = $m[0];
		$block = (string) preg_replace('/\{\{root\}\}/', $rootLine, $block);
		return (string) preg_replace('/^\s*root\s+[^;]+;/m', $rootLine, $block);
	},
	$vf['vhost']
);
$after = substr_count($patched, '{{root}}');
echo "after_patch {{root}}_count={$after}\n";

if ($patched === $vf['vhost']) {
	echo "No change needed.\n";
} elseif (!$apply) {
	echo "Dry run. Re-run with apply=1.\n";
	exit;
} elseif (!epc_clp_vhost_save($cookie, $platformSite, $patched, $vf['token'])) {
	exit("vhost save failed\n");
}
echo "Saved vhost.\n";

$tenants = array(
	'www.epartscart.com',
	'www.taxofinca.com',
	'www.electronicae.com',
	'www.stylenlook.com',
	'www.thejewellerytrend.com',
);
echo "\n=== Origin probes (curl) ===\n";
foreach ($tenants as $host) {
	$cmd = 'curl -skI -m 12 -H ' . escapeshellarg('Host: ' . $host) . ' http://127.0.0.1/ 2>&1';
	$out = (string) @shell_exec($cmd);
	$code = 0;
	if (preg_match('/^HTTP\/\S+\s+(\d{3})/m', $out, $m)) {
		$code = (int) $m[1];
	}
	echo "{$host}: HTTP {$code}\n";
	if ($host === 'www.thejewellerytrend.com' && $code === 0) {
		$cmd2 = 'curl -skI -m 12 --resolve ' . escapeshellarg($host . ':443:127.0.0.1')
			. ' https://' . $host . '/ 2>&1';
		$out2 = (string) @shell_exec($cmd2);
		if (preg_match('/^HTTP\/\S+\s+(\d{3})/m', $out2, $m2)) {
			echo "  https SNI: HTTP {$m2[1]}\n";
		} else {
			echo '  https SNI: ' . trim(substr($out2, 0, 120)) . "\n";
		}
	}
}
echo "\nDone.\n";
