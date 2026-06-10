<?php
/**
 * Deploy e-world portal to ecomae.com (copy from epartscart or chunk deploy).
 * https://www.epartscart.com/epc-ecomae-deploy-portal.php?token=epartscart-deploy-2026&db_password=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$dbPassword = trim((string) ($_GET['db_password'] ?? getenv('ECOMAE_DB_PASS') ?: ''));
$src = __DIR__;
$siteUser = 'ecomae';
$destCandidates = array(
	epc_clp_guess_docroot($siteUser, 'www.ecomae.com'),
	epc_clp_guess_docroot($siteUser, 'cp.ecomae.com'),
	'/home/ecomae/htdocs/www.ecomae.com',
	'/home/ecomae/htdocs/cp.ecomae.com',
);
$destCandidates = array_values(array_unique($destCandidates));

function epc_ecomae_log(string $msg): void
{
	echo $msg . "\n";
}

function epc_ecomae_mirror(string $src, string $dest): array
{
	$stats = array('copied' => 0, 'skipped' => 0, 'errors' => 0);
	if (!is_dir($dest)) {
		@mkdir($dest, 0755, true);
	}
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($src, FilesystemIterator::SKIP_DOTS),
		RecursiveIteratorIterator::SELF_FIRST
	);
	$skip = array('.git', 'hotfix-build', 'node_modules', '.cursor');
	foreach ($it as $item) {
		$rel = substr($item->getPathname(), strlen($src) + 1);
		foreach ($skip as $s) {
			if (strpos($rel, $s) === 0) {
				$stats['skipped']++;
				continue 2;
			}
		}
		$target = $dest . '/' . $rel;
		if ($item->isDir()) {
			if (!is_dir($target) && !@mkdir($target, 0755, true)) {
				$stats['errors']++;
			}
		} else {
			if (@copy($item->getPathname(), $target)) {
				$stats['copied']++;
			} else {
				$stats['errors']++;
			}
		}
	}
	return $stats;
}

function epc_ecomae_write_config(string $dest, string $dbPassword): void
{
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
	$cfg = str_replace('DBPASS', addslashes($dbPassword), $cfg);
	file_put_contents($dest . '/config.local.php', $cfg);
	epc_ecomae_log('Wrote config.local.php');
}

function epc_ecomae_deploy_chunks(string $host): bool
{
	$zip = '/tmp/docpart-epartscart-site.zip';
	if (!is_file($zip)) {
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
		@file_get_contents('http://127.0.0.1/chunk-receiver.php', false, $ctx);
	}
	$extractCtx = stream_context_create(array(
		'http' => array('timeout' => 300, 'header' => $header),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$extract = @file_get_contents(
		'http://127.0.0.1/extract-zip-ecomae.php?token=' . urlencode($token),
		false,
		$extractCtx
	);
	if (!is_string($extract) || strpos($extract, 'exit=0') === false) {
		$extract = @file_get_contents(
			'https://' . $host . '/extract-zip-ecomae.php?token=' . urlencode($token),
			false,
			$extractCtx
		);
	}
	epc_ecomae_log("extract:\n" . substr((string) $extract, 0, 2000));
	return is_string($extract) && strpos($extract, 'exit=0') !== false;
}

epc_ecomae_log('=== ecomae portal deploy ===');

$destinations = array();
foreach ($destCandidates as $c) {
	if (is_dir($c)) {
		$destinations[] = $c;
	}
}

if ($destinations === array()) {
	epc_ecomae_log('Docroot not found yet — trying chunk deploy to www.ecomae.com');
	if (epc_ecomae_deploy_chunks('www.ecomae.com')) {
		epc_ecomae_log('Chunk deploy OK');
		foreach ($destCandidates as $c) {
			if (is_dir($c) && !in_array($c, $destinations, true)) {
				$destinations[] = $c;
			}
		}
	}
}

if ($destinations === array()) {
	epc_ecomae_log('ERROR: ecomae docroot not available. Run epc-ecomae-provision.php first.');
	exit(1);
}

$deployed = false;
foreach ($destinations as $dest) {
	epc_ecomae_log('Destination: ' . $dest);
	$canWrite = is_writable($dest) || (@touch($dest . '/.w') && @unlink($dest . '/.w'));
	if (!$canWrite) {
		epc_ecomae_log('Skip (not writable): ' . $dest);
		continue;
	}
	$stats = epc_ecomae_mirror($src, $dest);
	epc_ecomae_log('Copy: copied=' . $stats['copied'] . ' errors=' . $stats['errors']);
	if ($dbPassword !== '') {
		epc_ecomae_write_config($dest, $dbPassword);
	}
	if ($stats['copied'] > 50) {
		$deployed = true;
	}
}

if (!$deployed) {
	epc_ecomae_log('Direct copy blocked — chunk deploy...');
	$deployed = epc_ecomae_deploy_chunks('www.ecomae.com');
	if ($deployed && count($destinations) > 1) {
		$primary = $destinations[0];
		for ($i = 1; $i < count($destinations); $i++) {
			$extra = $destinations[$i];
			if (is_writable($extra) || (@touch($extra . '/.w') && @unlink($extra . '/.w'))) {
				$stats = epc_ecomae_mirror($primary, $extra);
				epc_ecomae_log('Synced ' . $extra . ': copied=' . $stats['copied']);
			}
		}
	}
}

if (!$deployed) {
	epc_ecomae_log('ERROR: deploy failed');
	exit(1);
}

epc_ecomae_log('Running setup scripts on ecomae...');
$token = epc_deploy_token();
$setups = array(
	'https://www.ecomae.com/epc-ecomae-setup.php?token=' . urlencode($token),
);
foreach ($setups as $url) {
	$resp = @file_get_contents($url, false, stream_context_create(array(
		'http' => array('timeout' => 120),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	epc_ecomae_log($url . "\n" . substr((string) $resp, 0, 800) . "\n---");
}

epc_ecomae_log('Done. Visit https://www.ecomae.com/ and https://cp.ecomae.com/cp/');
