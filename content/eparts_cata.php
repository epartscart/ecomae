<?php
/**
 * Storefront — EParts CATA (unified parts catalog).
 * Route: /en/eparts-cata  (content url: eparts-cata)
 *
 * Shares the vehicle + category catalog shell with EParts Mod so CP
 * "Open storefront" and category deep-links (?category=) work.
 */
defined('_ASTEXE_') or die('No access');

$epartsCataShell = $_SERVER['DOCUMENT_ROOT'] . '/content/eparts_mod_catalog.php';
if (!is_file($epartsCataShell)) {
	echo '<div class="error_message">EParts catalog shell is not installed on this host.</div>';
} else {
	require $epartsCataShell;
}
?>
