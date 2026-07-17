<?php
declare(strict_types=1);
/**
 * Path-based warehouse sitemap shard (no query string — GSC-safe).
 * Serves /en/parts/{BRAND}/{ARTICLE} URLs for shard 63.
 */
$_GET['n'] = 63;
require __DIR__ . '/sitemap-warehouse.php';
