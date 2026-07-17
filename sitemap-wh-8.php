<?php
declare(strict_types=1);
/**
 * Path-based warehouse sitemap shard (no query string — GSC-safe).
 * Serves /en/parts/{BRAND}/{ARTICLE} URLs for shard 8.
 */
$_GET['n'] = 8;
require __DIR__ . '/sitemap-warehouse.php';
