<?php
/**
 * Purge all page/widget caches after deploy.
 * Usage: curl -sk "https://www.epartscart.com/epc-cache-purge.php?token=epartscart-deploy-2026"
 */
header('Content-Type: text/plain; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);

$purged = 0;

// Purge full-page cache
require_once __DIR__ . '/content/general_pages/epc_page_cache.php';
$purged += epc_page_cache_purge_all();

// Purge widget/perf cache
require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
$purged += epc_perf_cache_bust_prefix('');

echo "Purged " . $purged . " cache files.\n";
echo "DONE\n";
