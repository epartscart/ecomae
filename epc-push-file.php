<?php
/**
 * Minimal deploy: write one file to epartscart + ecomae docroots (no portal bootstrap).
 * GET/POST: token, push_rel, push_b64
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$pushRel = str_replace('\\', '/', trim((string) ($_POST['push_rel'] ?? $_GET['push_rel'] ?? '')));
$pushB64 = (string) ($_POST['push_b64'] ?? $_GET['push_b64'] ?? '');
if ($pushRel === '' || $pushB64 === '' || strpos($pushRel, '..') !== false || $pushRel[0] === '/') {
	exit("push_rel and push_b64 required\n");
}
$bin = base64_decode($pushB64, true);
if ($bin === false) {
	exit("Bad push_b64\n");
}

$roots = array(
	rtrim(__DIR__, '/') . '/',
	'/home/epartscart/htdocs/www.epartscart.com/',
	'/home/ecomae/htdocs/www.epartscart.com/',
	'/home/ecomae/htdocs/www.ecomae.com/',
	'/home/ecomae/htdocs/cp.ecomae.com/',
	'/home/ecomaecp/htdocs/cp.ecomae.com/',
);
foreach ($roots as $root) {
	if (!is_dir($root)) {
		continue;
	}
	$dest = $root . $pushRel;
	$dir = dirname($dest);
	if (!is_dir($dir)) {
		mkdir($dir, 0755, true);
	}
	$n = @file_put_contents($dest, $bin);
	if ($n === false) {
		$err = error_get_last();
		$msg = is_array($err) ? (string) ($err['message'] ?? 'write failed') : 'write failed';
		echo "FAIL {$dest} writable_dir=" . (is_writable($dir) ? '1' : '0')
			. " exists=" . (file_exists($dest) ? '1' : '0')
			. " writable_file=" . (file_exists($dest) && is_writable($dest) ? '1' : '0')
			. " err={$msg}\n";
		continue;
	}
	clearstatcache(true, $dest);
	$ok = is_file($dest) && (int) filesize($dest) === strlen($bin);
	echo ($ok ? 'wrote' : 'FAIL size-mismatch') . " {$dest} bytes={$n}"
		. " sha256=" . hash_file('sha256', $dest) . "\n";
}
exit("Push done.\n");
