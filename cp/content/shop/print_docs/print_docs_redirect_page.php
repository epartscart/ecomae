<?php
/**
 * Legacy print module redirect → Document Control.
 */
defined('_ASTEXE_') or die('No access');

$backend = htmlspecialchars($GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$url = '/' . $backend . '/shop/document_control/document_control';
?>
<div class="alert alert-info">
	<strong>Print module upgraded.</strong> The Russian legacy print module has been replaced by the English
	<a href="<?php echo htmlspecialchars($url, ENT_QUOTES, 'UTF-8'); ?>"><strong>Document Control System</strong></a>
	(FTA tax invoice, packing slip, delivery note, payment receipt).
</div>
<script>window.location.replace(<?php echo json_encode($url); ?>);</script>
