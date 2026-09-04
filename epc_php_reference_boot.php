<?php
/**
 * PHP-FPM entry for /php-reference/* compare shells.
 *
 * Classic-entry nginx steals product /index.php to Kestrel. Compare URLs must
 * rewrite here — never to /index.php — so side-by-side CP/ERP twins stay on PHP.
 * Does not flip cutover / readyForPhpRemoval. Does not delete PHP source.
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/content/general_pages/epc_php_reference_router.php';
if (function_exists('epc_php_reference_try_route') && epc_php_reference_try_route()) {
	exit;
}

require __DIR__ . '/index.php';
