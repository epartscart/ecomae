<?php
/**
 * Repair epartscart when PHP is not executed (raw source / php version banner visible).
 * https://www.ecomae.com/epc-epartscart-php-storefront-fix.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$apply = !empty($_GET['apply']);
$hostname = 'www.epartscart.com';
$bare = 'epartscart.com';
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$legacyDocroot = '/home/epartscart/htdocs/www.epartscart.com';
$aliasHosts = array($hostname, $bare);

function epc_epc_php_probe(string $url, string $hostHeader = ''): array
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flat = is_string($body) ? $body : '';
	$leak = (stripos($flat, '<?php') !== false)
		|| (stripos($flat, 'Decoded file for php version') !== false)
		|| (stripos($flat, 'phpinfo') !== false)
		|| (bool) preg_match('/\b(Fatal error|Parse error)\b/i', $flat);
	$storefront = stripos($flat, 'eParts Cart') !== false
		|| stripos($flat, 'epart-front-original-data') !== false
		|| stripos($flat, 'data-epc-storefront') !== false;
	return array(
		'code' => $code,
		'leak' => $leak,
		'storefront' => $storefront,
		'snippet' => substr(preg_replace('/\s+/', ' ', strip_tags($flat)), 0, 100),
	);
}

echo "=== epartscart PHP storefront fix ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'platform_docroot=' . $platformDocroot . "\n";
echo 'legacy_docroot=' . $legacyDocroot . "\n\n";

echo "=== BEFORE ===\n";
foreach (array('/', '/en/') as $path) {
	$p = epc_epc_php_probe('http://127.0.0.1' . $path, $hostname);
	echo "  origin {$path}: HTTP {$p['code']}" . ($p['leak'] ? ' RAW_PHP_LEAK' : '') . ($p['storefront'] ? ' storefront' : '') . " — {$p['snippet']}\n";
}
$pPub = epc_epc_php_probe('https://' . $hostname . '/en/');
echo "  public /en/: HTTP {$pPub['code']}" . ($pPub['leak'] ? ' RAW_PHP_LEAK' : '') . ($pPub['storefront'] ? ' storefront' : '') . "\n\n";

if (!$apply) {
	echo "Dry run. Re-run with apply=1&clp_pass=...\n";
	exit;
}
if ($clpPass === '') {
	exit("apply=1 requires clp_pass=\n");
}

$syncFiles = array(
	'index.php',
	'core/dp_core.php',
	'.htaccess',
	'config.php',
	'templates/nero/desktop.php',
	'content/general_pages/epart_catalog_front_links.php',
);
echo "=== Sync platform code → epartscart docroot ===\n";
if (!is_dir($legacyDocroot)) {
	echo "legacy docroot missing — skip file sync\n";
} else {
	foreach ($syncFiles as $rel) {
		$from = $platformDocroot . '/' . $rel;
		$to = $legacyDocroot . '/' . $rel;
		if (!is_file($from)) {
			echo "  missing src {$rel}\n";
			continue;
		}
		$dir = dirname($to);
		if (!is_dir($dir)) {
			mkdir($dir, 0755, true);
		}
		$ok = copy($from, $to);
		echo '  ' . $rel . ': ' . ($ok ? 'copied' : 'FAIL') . "\n";
	}
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "\n=== CloudPanel nginx (Model C tenant direct PHP) ===\n";

foreach (array($hostname, $bare) as $orphan) {
	$del = epc_clp_web_delete_site($cookie, $orphan);
	echo 'Remove standalone site ' . $orphan . ': ' . implode(' ', array_slice($del['log'], 0, 2)) . "\n";
}

$direct = epc_clp_vhost_configure_tenant_direct_php($cookie, $platformSite, $aliasHosts, $platformSite);
foreach ($direct['log'] as $line) {
	echo '  direct: ' . $line . "\n";
}

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] !== '' && $vf['token'] !== '') {
	$patched = epc_clp_vhost_patch_tenant_direct_root($vf['vhost'], $platformDocroot);
	if ($patched !== $vf['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $patched, $vf['token']);
		echo "Patched tenant direct root → {$platformDocroot}\n";
		$vf['vhost'] = $patched;
	}
	if (substr_count($vf['vhost'], '{{root}}') > 0) {
		$rootLine = '  root ' . rtrim($platformDocroot, '/') . ';';
		$allRoots = (string) preg_replace_callback(
			'/# EPC_TENANT_DIRECT_START[\s\S]*?# EPC_TENANT_DIRECT_END/',
			function (array $m) use ($rootLine) {
				$block = (string) preg_replace('/\{\{root\}\}/', $rootLine, $m[0]);
				return (string) preg_replace('/^\s*root\s+[^;]+;/m', $rootLine, $block);
			},
			$vf['vhost']
		);
		if ($allRoots !== $vf['vhost']) {
			epc_clp_vhost_save($cookie, $platformSite, $allRoots, $vf['token']);
			echo "Resolved {{root}} placeholders in tenant direct block\n";
		}
	}
}

$repoint = epc_clp_web_set_site_docroot($cookie, $hostname, $platformDocroot);
echo 'CLP docroot ' . $hostname . ': ' . implode(' | ', array_slice($repoint['log'], 0, 2)) . "\n";

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($platformDocroot));
echo 'permissions code=' . $perm['code'] . "\n";
@exec('chmod -R o+rX ' . escapeshellarg($platformDocroot) . ' 2>&1');

$quarantine = epc_clp_nginx_quarantine_orphan_configs($aliasHosts, $platformSite);
foreach ($quarantine['log'] as $line) {
	echo 'quarantine: ' . $line . "\n";
}

echo "\n=== AFTER ===\n";
foreach (array('/', '/en/') as $path) {
	$p = epc_epc_php_probe('http://127.0.0.1' . $path, $hostname);
	echo "  origin {$path}: HTTP {$p['code']}" . ($p['leak'] ? ' RAW_PHP_LEAK' : '') . ($p['storefront'] ? ' storefront' : '') . " — {$p['snippet']}\n";
}
$pPub = epc_epc_php_probe('https://' . $hostname . '/en/');
echo "  public /en/: HTTP {$pPub['code']}" . ($pPub['leak'] ? ' RAW_PHP_LEAK' : '') . ($pPub['storefront'] ? ' storefront' : '') . "\n";

echo "\nVerify: https://www.epartscart.com/en/\n";
