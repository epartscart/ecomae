<?php
/**
 * Deploy a previously pushed *.new file over a root-owned target via rename.
 * GET: token, rel=content/shop/docpart/part_search_page.php
 * Expects: {rel}.new already written (writable) next to target.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$rel = str_replace('\\', '/', trim((string) ($_GET['rel'] ?? '')));
if ($rel === '' || strpos($rel, '..') !== false || $rel[0] === '/') {
	exit("rel required\n");
}
$target = rtrim(__DIR__, '/') . '/' . $rel;
$new = $target . '.new';
if (!is_file($new)) {
	exit("missing {$new}\n");
}
$lint = array();
$code = 0;
if (substr($rel, -4) === '.php') {
	exec('php -l ' . escapeshellarg($new) . ' 2>&1', $lint, $code);
	echo implode("\n", $lint) . "\n";
	if ($code !== 0) {
		exit("lint failed\n");
	}
}
$bak = $target . '.bak-deploy-' . date('YmdHis');
if (is_file($target)) {
	@copy($target, $bak);
}
if (!@rename($new, $target)) {
	exit("rename failed\n");
}
clearstatcache(true, $target);
if (function_exists('opcache_invalidate')) {
	opcache_invalidate($target, true);
}
if (function_exists('opcache_reset')) {
	@opcache_reset();
}
echo "deployed {$rel} md5=" . md5_file($target) . "\n";
exit;
