<?php
/**
 * CP route shop/bulk_upload — eval-safe wrapper.
 */
defined('_ASTEXE_') or die('No access');

$include = $_SERVER['DOCUMENT_ROOT'] . '/' . $GLOBALS['DP_Config']->backend_dir . '/content/shop/bulk_upload/bulk_upload_hub.php';
if (!is_file($include)) {
	echo '<div class="alert alert-danger">Bulk upload hub file missing.</div>';
	return;
}
require $include;
