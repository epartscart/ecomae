<?php
/**
 * Extract portal zip on taxofinca — wipe WordPress leftovers first.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$target = __DIR__;

function epc_tf_remove_wordpress(string $dir): array
{
	$removed = array();
	if (!is_dir($dir)) {
		return $removed;
	}
	foreach (array('wp-admin', 'wp-content', 'wp-includes') as $sub) {
		$path = $dir . '/' . $sub;
		if (!is_dir($path)) {
			continue;
		}
		$it = new RecursiveIteratorIterator(
			new RecursiveDirectoryIterator($path, FilesystemIterator::SKIP_DOTS),
			RecursiveIteratorIterator::CHILD_FIRST
		);
		foreach ($it as $item) {
			if ($item->isDir()) {
				@rmdir($item->getPathname());
			} else {
				@unlink($item->getPathname());
			}
		}
		@rmdir($path);
		$removed[] = $sub;
	}
	foreach (glob($dir . '/wp-*.php') ?: array() as $file) {
		if (@unlink($file)) {
			$removed[] = basename($file);
		}
	}
	foreach (array('xmlrpc.php', 'license.txt', 'readme.html', 'wordfence-waf.php') as $file) {
		$f = $dir . '/' . $file;
		if (is_file($f) && @unlink($f)) {
			$removed[] = $file;
		}
	}
	return $removed;
}

$wiped = epc_tf_remove_wordpress($target);
echo "WordPress removed: " . (count($wiped) ? implode(', ', $wiped) : '(none found)') . "\n";

$zip = '/tmp/docpart-epartscart-site.zip';
if (!is_file($zip)) {
	exit("Zip missing at {$zip}\n");
}

$cmd = 'unzip -o ' . escapeshellarg($zip) . ' -d ' . escapeshellarg($target) . ' 2>&1';
if (!function_exists('exec')) {
	exit("exec disabled\n");
}
exec($cmd, $out, $code);
echo implode("\n", $out) . "\n";
echo "exit={$code}\n";

if ($code === 0) {
	@unlink($zip);
	epc_tf_remove_wordpress($target);
	echo "Done. index.php exists: " . (is_file($target . '/index.php') ? 'yes' : 'no') . "\n";
}
