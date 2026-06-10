<?php
/**
 * Bootstrap + deploy portal to ecomae via CloudPanel file manager + chunk upload.
 * https://www.epartscart.com/epc-ecomae-deploy-via-clp.php?token=...&clp_pass=...&db_password=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpUser = trim((string) ($_GET['clp_user'] ?? 'admin'));
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
$siteUser = 'ecomae';
$remoteDir = '/htdocs/www.ecomae.com';
$host = 'www.ecomae.com';

function ecomae_log(string $msg): void
{
	echo $msg . "\n";
}

function ecomae_clp_put_file(string &$cookie, string $dir, string $name, string $content): void
{
	$panel = epc_clp_panel_url();
	$parts = explode('/', trim(str_replace('\\', '/', $name), '/'));
	$fileName = array_pop($parts);
	$parent = $dir;
	foreach ($parts as $part) {
		epc_clp_web_request($panel . '/file-manager/backend/mkdir', array(
			'method' => 'POST',
			'body' => http_build_query(array('id' => $parent, 'name' => $part)),
		), $cookie);
		$parent .= '/' . $part;
	}
	epc_clp_web_request($panel . '/file-manager/backend/makefile', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent, 'name' => $fileName)),
	), $cookie);
	epc_clp_web_request($panel . '/file-manager/backend/text', array(
		'method' => 'POST',
		'body' => http_build_query(array('id' => $parent . '/' . $fileName, 'content' => $content)),
	), $cookie);
}

function ecomae_chunk_deploy(string $host): bool
{
	$zip = '/tmp/docpart-epartscart-site.zip';
	if (!is_file($zip)) {
		ecomae_log('Zip missing at ' . $zip);
		return false;
	}
	$token = epc_deploy_token();
	$data = file_get_contents($zip);
	$chunkSize = 150000;
	$header = "Host: {$host}\r\n";
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
				'header' => $header . "Content-Type: application/x-www-form-urlencoded\r\n",
				'content' => $body,
				'timeout' => 300,
			),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		));
		@file_get_contents('https://' . $host . '/chunk-receiver.php', false, $ctx);
	}
	$extract = @file_get_contents(
		'https://' . $host . '/extract-zip-ecomae.php?token=' . urlencode($token),
		false,
		stream_context_create(array(
			'http' => array('timeout' => 300),
			'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
		))
	);
	ecomae_log('extract: ' . substr((string) $extract, 0, 1500));
	return is_string($extract) && (
		strpos($extract, 'exit=0') !== false
		|| (strpos($extract, 'inflating:') !== false && strpos($extract, 'index.php') !== false)
	);
}

if ($clpPass === '') {
	exit("clp_pass required\n");
}

$cookie = '';
$login = epc_clp_web_login($clpUser, $clpPass, $cookie);
if (empty($login['ok'])) {
	exit("CloudPanel login failed\n");
}

$pushRel = str_replace('\\', '/', trim((string) ($_POST['push_rel'] ?? $_GET['push_rel'] ?? '')));
$pushB64 = (string) ($_POST['push_b64'] ?? $_POST['b64'] ?? '');
if ($pushRel !== '' && $pushB64 !== '' && strpos($pushRel, '..') === false && $pushRel[0] !== '/') {
	$bin = base64_decode($pushB64, true);
	if ($bin === false || $bin === '') {
		exit("Bad push_b64\n");
	}
	$panel = epc_clp_panel_url();
	foreach (array('www.ecomae.com', 'cp.ecomae.com') as $site) {
		epc_clp_web_request($panel . '/site/' . rawurlencode($site) . '/file-manager', array(), $cookie);
		ecomae_clp_put_file($cookie, '/htdocs/' . $site, $pushRel, $bin);
		echo "Pushed {$pushRel} to {$site}\n";
	}
	exit("Push done.\n");
}

ecomae_log('CloudPanel login OK');

$applyCpFix = !empty($_GET['apply_cp_fix']);
if ($applyCpFix) {
	$marker = 'Nginx try_files sends /cp/* subpaths here';
	$needle = "epc_portal_apply_config(\$DP_Config);\n\nrequire_once \$_SERVER['DOCUMENT_ROOT']";
	$insert = <<<'PHP'
epc_portal_apply_config($DP_Config);

// Nginx try_files sends /cp/* subpaths here — hand off to backend CP (cp/index.php).
$epcBackendDir = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($epcBackendDir !== '' && isset($_SERVER['REQUEST_URI'])) {
	$epcPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	if (!is_string($epcPath) || $epcPath === '') {
		$epcPath = '/';
	}
	$epcCpBase = '/' . $epcBackendDir;
	if ($epcPath === $epcCpBase || $epcPath === $epcCpBase . '/'
		|| (strlen($epcPath) > strlen($epcCpBase) && strpos($epcPath, $epcCpBase . '/') === 0)) {
		$cpEntry = $_SERVER['DOCUMENT_ROOT'] . '/' . $epcBackendDir . '/index.php';
		if (is_file($cpEntry)) {
			require $cpEntry;
			exit;
		}
	}
}

require_once $_SERVER['DOCUMENT_ROOT']
PHP;
	foreach (array(
		'www.ecomae.com' => '/home/ecomae/htdocs/www.ecomae.com/index.php',
		'cp.ecomae.com' => '/home/ecomae/htdocs/cp.ecomae.com/index.php',
	) as $site => $indexPath) {
		if (!is_file($indexPath)) {
			ecomae_log("skip {$site} — index missing");
			continue;
		}
		$content = (string) file_get_contents($indexPath);
		if (strpos($content, $marker) !== false) {
			ecomae_log("{$site}: already patched");
			continue;
		}
		if (strpos($content, $needle) === false) {
			ecomae_log("{$site}: anchor not found");
			continue;
		}
		$newContent = str_replace($needle, $insert, $content);
		ecomae_clp_put_file($cookie, '/htdocs/' . $site, 'index.php', $newContent);
		ecomae_log("{$site}: patched index.php");
	}
	exit("apply_cp_fix done.\n");
}

$panel = epc_clp_panel_url();
epc_clp_web_request($panel . '/site/' . rawurlencode($host) . '/file-manager', array(), $cookie);

$bootstrap = array(
	'chunk-receiver.php' => file_get_contents(__DIR__ . '/chunk-receiver.php'),
	'extract-zip-ecomae.php' => file_get_contents(__DIR__ . '/extract-zip-ecomae.php'),
	'epc_deploy_auth.php' => file_get_contents(__DIR__ . '/epc_deploy_auth.php'),
);
$epcCfg = '/home/epartscart/htdocs/www.epartscart.com/config.php';
if (is_file($epcCfg)) {
	$bootstrap['config.php'] = file_get_contents($epcCfg);
}
foreach ($bootstrap as $name => $content) {
	if ($content === false || $content === '') {
		ecomae_log("Skip missing bootstrap: {$name}");
		continue;
	}
	ecomae_clp_put_file($cookie, $remoteDir, $name, $content);
	ecomae_log("Uploaded bootstrap: {$name}");
}

if (!ecomae_chunk_deploy($host)) {
	ecomae_log('ERROR: chunk deploy failed');
	exit(1);
}

if ($dbPass !== '') {
	$cfg = <<<'PHP'
<?php
$epc_config_local = array(
	'password' => 'DBPASS',
	'db' => 'ecomae',
	'user' => 'ecomae',
	'domain_path' => 'https://www.ecomae.com/',
	'from_name' => 'ecomae',
	'from_email' => 'hello@ecomae.com',
	'epc_contact_phone' => '+971-567607011',
	'epc_head_office_email' => 'hello@ecomae.com',
	'epc_head_office_address' => 'Dubai, United Arab Emirates',
);
PHP;
	$cfg = str_replace('DBPASS', addslashes($dbPass), $cfg);
	ecomae_clp_put_file($cookie, $remoteDir, 'config.local.php', $cfg);
	ecomae_log('Wrote config.local.php');
}

$token = epc_deploy_token();
$setups = array(
	'https://www.ecomae.com/epc-ecomae-setup.php?token=' . urlencode($token),
);
foreach ($setups as $url) {
	$resp = @file_get_contents($url, false, stream_context_create(array(
		'http' => array('timeout' => 180),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	ecomae_log($url . "\n" . substr((string) $resp, 0, 1200) . "\n---");
}

ecomae_log('Done. Check https://www.ecomae.com/');
