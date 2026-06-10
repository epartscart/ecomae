<?php
/**
 * Copy portal/license fixes from platform docroot into tenant docroot(s).
 * https://www.epartscart.com/epc-tenant-code-sync.php?token=...&host=www.taxofinca.com
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$host = strtolower(trim((string) ($_GET['host'] ?? 'www.taxofinca.com')));
$bare = preg_replace('/^www\./', '', $host);
$platformRoot = rtrim(__DIR__, '/\\');

$files = array(
	'core/dp_core.php',
	'content/general_pages/epc_portal.php',
	'content/general_pages/epc_portal_db.php',
	'content/general_pages/epc_portal_theme_templates.php',
	'index.php',
	'cp/index.php',
	'epc-tenant-license-fix.php',
);

function epc_tcs_find_roots(string $host, string $bare): array
{
	$roots = array();
	$patterns = array(
		"/home/*/htdocs/{$host}",
		"/home/*/htdocs/{$host}/public",
		"/home/*/htdocs/{$bare}",
		"/home/*/htdocs/{$bare}/public",
		"/home/*/htdocs/www.{$bare}",
		"/home/*/htdocs/www.{$bare}/public",
	);
	foreach ($patterns as $pat) {
		foreach (glob($pat) ?: array() as $path) {
			if (is_dir($path)) {
				$roots[$path] = true;
			}
		}
	}
	foreach (glob('/home/*/htdocs/*' . $bare . '*') ?: array() as $path) {
		if (is_dir($path)) {
			$roots[rtrim($path, '/')] = true;
		}
	}
	return array_keys($roots);
}

echo "=== Tenant code sync ===\n";
echo "host={$host}\n";
echo "platform_root={$platformRoot}\n\n";

$destRoots = epc_tcs_find_roots($host, $bare);
echo 'dest_roots=' . (count($destRoots) ? implode(', ', $destRoots) : '(none)') . "\n\n";

if (count($destRoots) === 0) {
	echo "No separate tenant docroot found — tenant may already use shared platform root.\n";
	exit(0);
}

foreach ($destRoots as $destRoot) {
	if (realpath($destRoot) === realpath($platformRoot)) {
		echo "skip same root: {$destRoot}\n";
		continue;
	}
	echo "Sync -> {$destRoot}\n";
	foreach ($files as $rel) {
		$src = $platformRoot . '/' . $rel;
		if (!is_file($src)) {
			echo "  missing src {$rel}\n";
			continue;
		}
		$dest = $destRoot . '/' . $rel;
		$dir = dirname($dest);
		if (!is_dir($dir)) {
			mkdir($dir, 0755, true);
		}
		$ok = @copy($src, $dest);
		echo '  ' . $rel . ': ' . ($ok ? 'ok' : 'FAIL') . ' bytes=' . filesize($src) . "\n";
	}
}

echo "\nDone.\n";
