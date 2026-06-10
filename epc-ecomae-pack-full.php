<?php
/**
 * Pack full epartscart site → chunk deploy to ecomae → extract.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
$host = 'www.ecomae.com';
$src = '/home/epartscart/htdocs/www.epartscart.com';
$tar = '/tmp/ecomae-full-site.tar.gz';

if ($clpPass === '') {
	exit("clp_pass required\n");
}

echo "=== pack full site ===\n";
@unlink($tar);
$cmd = 'tar -czf ' . escapeshellarg($tar)
	. ' --ignore-failed-read'
	. ' --exclude=./pyprices/pyprices/Lib'
	. ' --exclude=./pyprices/Lib'
	. ' --exclude=./content/sms/sms/handlers'
	. ' -C ' . escapeshellarg(dirname($src)) . ' ' . escapeshellarg(basename($src)) . ' 2>&1';
exec($cmd, $out, $code);
echo implode("\n", $out) . "\nexit={$code} size=" . (is_file($tar) ? filesize($tar) : 0) . "\n";
if (!is_file($tar) || filesize($tar) < 1000000) {
	exit("pack failed\n");
}

$cookie = '';
$login = epc_clp_web_login('admin', $clpPass, $cookie);
if (empty($login['ok'])) {
	exit("CloudPanel login failed\n");
}
$remoteDir = '/htdocs/www.ecomae.com';
$panel = epc_clp_panel_url();
epc_clp_web_request($panel . '/site/' . rawurlencode($host) . '/file-manager', array(), $cookie);

function ecomae_put(string &$cookie, string $dir, string $name, string $content): void
{
	$panel = epc_clp_panel_url();
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $dir, 'name' => $name)),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $dir . '/' . $name, 'content' => $content)),
	), $cookie);
}

$extractPhp = <<<'PHP'
<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);
$tar = '/tmp/docpart-epartscart-site.zip';
$dest = __DIR__;
if (!is_file($tar)) { exit("archive missing at {$tar}\n"); }
@mkdir('/tmp/ecomae-extract', 0755, true);
exec('rm -rf /tmp/ecomae-extract/* 2>&1');
exec('tar -xzf ' . escapeshellarg($tar) . ' -C /tmp/ecomae-extract 2>&1', $o, $c);
echo implode("\n", $o) . "\ntar exit={$c}\n";
$src = '/tmp/ecomae-extract/www.epartscart.com';
if (!is_dir($src)) {
	foreach (glob('/tmp/ecomae-extract/*') ?: array() as $d) {
		if (is_dir($d)) { $src = $d; break; }
	}
}
if (!is_dir($src)) { exit("extract dir not found\n"); }
exec('cp -a ' . escapeshellarg($src . '/.') . ' ' . escapeshellarg($dest) . '/ 2>&1', $o2, $c2);
echo implode("\n", array_slice($o2, 0, 8)) . "\ncp exit={$c2}\n";
@unlink($tar);
echo "Done. index=" . (is_file($dest . '/index.php') ? 'yes' : 'no') . " cp=" . (is_file($dest . '/cp/index.php') ? 'yes' : 'no') . "\n";
PHP;

foreach (array(
	'epc_deploy_auth.php' => file_get_contents(__DIR__ . '/epc_deploy_auth.php'),
	'chunk-receiver.php' => file_get_contents(__DIR__ . '/chunk-receiver.php'),
	'extract-ecomae-full.php' => $extractPhp,
) as $name => $content) {
	if ($content) {
		ecomae_put($cookie, $remoteDir, $name, $content);
		echo "bootstrap {$name}\n";
	}
}

copy($tar, '/tmp/docpart-epartscart-site.zip');
$token = epc_deploy_token();
$data = file_get_contents($tar);
$chunkSize = 150000;
$hdr = "Host: {$host}\r\n";
for ($idx = 0, $off = 0; $off < strlen($data); $off += $chunkSize, $idx++) {
	$part = substr($data, $off, $chunkSize);
	$body = http_build_query(array(
		'token' => $token,
		'index' => (string) $idx,
		'data' => base64_encode($part),
		'final' => ($off + $chunkSize >= strlen($data)) ? '1' : '0',
	));
	$ctx = stream_context_create(array(
		'http' => array(
			'method' => 'POST',
			'header' => $hdr . "Content-Type: application/x-www-form-urlencoded\r\n",
			'content' => $body,
			'timeout' => 300,
		),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	@file_get_contents('https://' . $host . '/chunk-receiver.php', false, $ctx);
}
echo "chunks uploaded\n";

$extract = @file_get_contents(
	'https://' . $host . '/extract-ecomae-full.php?token=' . urlencode($token),
	false,
	stream_context_create(array(
		'http' => array('timeout' => 600),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	))
);
echo "extract:\n" . substr((string) $extract, 0, 2000) . "\n";

$cfg = <<<'PHP'
<?php
$epc_config_local = array(
	'password' => 'DBPASS',
	'db' => 'ecomae',
	'user' => 'ecomae',
	'domain_path' => 'https://www.ecomae.com/',
	'from_name' => 'ecomae',
	'from_email' => 'hello@ecomae.com',
);
PHP;
if ($dbPass !== '') {
	ecomae_put($cookie, $remoteDir, 'config.local.php', str_replace('DBPASS', addslashes($dbPass), $cfg));
}

$portalFiles = array(
	'content/general_pages/epc_portal.php',
	'content/general_pages/epc_portal_db.php',
	'content/general_pages/epc_portal_tenant.php',
	'content/general_pages/epc_ecomae_platform_home.php',
	'content/shop/tenant_hub/epc_tenant_hub_helpers.php',
	'cp/content/shop/tenant_hub/tenant_hub_main.php',
	'core/dp_core.php',
	'epc-ecomae-setup.php',
	'epc-ecomae-platform-check.php',
	'epc-ecomae-platform-fix.php',
	'ecomae-super-cp-setup.php',
);
foreach ($portalFiles as $rel) {
	$srcFile = __DIR__ . '/' . $rel;
	if (!is_file($srcFile)) {
		continue;
	}
	$parts = explode('/', trim($rel, '/'));
	$name = array_pop($parts);
	$parent = $remoteDir;
	foreach ($parts as $part) {
		epc_clp_web_request($panel . '/file-manager/backend/mkdir', array(
			'method' => 'POST',
			'body' => http_build_query(array('id' => $parent, 'name' => $part)),
		), $cookie);
		$parent .= '/' . $part;
	}
	ecomae_put($cookie, $parent, $name, file_get_contents($srcFile));
}
echo "portal overlay on www\n";

$setup = @file_get_contents('https://' . $host . '/epc-ecomae-setup.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 180),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\nsetup:\n" . substr((string) $setup, 0, 1500) . "\n";

if ($dbPass !== '') {
	$fixUrl = 'https://www.epartscart.com/epc-ecomae-platform-fix.php?token=' . urlencode($token)
		. '&clp_pass=' . urlencode($clpPass) . '&src_db=docpart&db_password=' . urlencode($dbPass);
	$fix = @file_get_contents($fixUrl, false, stream_context_create(array(
		'http' => array('timeout' => 300),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	echo "\ndb fix:\n" . substr((string) $fix, 0, 800) . "\n";
}

$export = @file_get_contents('https://' . $host . '/epc-ecomae-www-export.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 600),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\nwww export:\n" . substr((string) $export, 0, 400) . "\n";

$import = @file_get_contents('https://cp.ecomae.com/epc-ecomae-cp-import-tar.php?token=' . urlencode($token) . '&db_password=' . urlencode($dbPass), false, stream_context_create(array(
	'http' => array('timeout' => 600),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\ncp import:\n" . substr((string) $import, 0, 600) . "\n";

if ($dbPass !== '') {
	$cpCfg = str_replace('www.ecomae.com', 'cp.ecomae.com', str_replace('DBPASS', addslashes($dbPass), $cfg));
	ecomae_put($cookie, '/htdocs/cp.ecomae.com', 'config.local.php', $cpCfg);
}

foreach (array('www.epartscart.com', 'www.taxofinca.com') as $suspendHost) {
	$sus = @file_get_contents(
		'https://www.epartscart.com/epc-clp-suspend-site.php?token=' . urlencode($token)
		. '&clp_pass=' . urlencode($clpPass) . '&domain=' . urlencode($suspendHost) . '&action=suspend',
		false,
		stream_context_create(array('http' => array('timeout' => 60), 'ssl' => array('verify_peer' => false, 'verify_peer_name' => false)))
	);
	echo "\nsuspend {$suspendHost}: " . trim(substr((string) $sus, 0, 80)) . "\n";
}

$check = @file_get_contents('https://' . $host . '/epc-ecomae-platform-check.php?token=' . urlencode($token), false, stream_context_create(array(
	'http' => array('timeout' => 90),
	'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
)));
echo "\ncheck:\n" . substr((string) $check, 0, 3500) . "\n";
