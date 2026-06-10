<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$dcUrl = function_exists('epc_document_control_cp_url') ? epc_document_control_cp_url() : '';
$probe = array();
if (function_exists('epc_document_control_cp_probe') && isset($db_link)) {
	$backend = isset($DP_Config) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	$probe = epc_document_control_cp_probe($db_link, $_SERVER['DOCUMENT_ROOT'], $backend);
}

erp_page_header(
	'<i class="fa fa-print"></i> Document Control',
	'Print templates, company letterhead, and document attachments — opens the Document Control CP module.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Document Control'),
	),
	$dcUrl !== '' ? array(array('label' => 'Open Document Control', 'url' => $dcUrl, 'class' => 'btn-primary', 'icon' => 'fa-external-link')) : array()
);

ob_start();
if ($dcUrl !== '') {
	echo '<p><a class="btn btn-lg btn-primary" href="' . epc_erp_h($dcUrl) . '" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open Document Control CP</a></p>';
	echo '<p class="text-muted">URL: <code>' . epc_erp_h($dcUrl) . '</code></p>';
} else {
	erp_empty_state('Document Control URL could not be resolved for this tenant.');
}
if (!empty($probe['content'])) {
	echo '<h4>Route registration</h4><table class="table table-condensed table-bordered"><thead><tr><th>URL</th><th>Published</th></tr></thead><tbody>';
	foreach ($probe['content'] as $url => $row) {
		echo '<tr><td>' . epc_erp_h($url) . '</td><td>';
		echo $row ? ((int) ($row['published_flag'] ?? 0) ? 'Yes' : 'No') : '<span class="text-danger">Missing</span>';
		echo '</td></tr>';
	}
	echo '</tbody></table>';
	if (empty($probe['content']['shop/document_control/document_control'])) {
		echo '<div class="alert alert-warning">Run <code>epc-document-control-cp-setup-all.php?apply=1</code> on this host to register Document Control routes.</div>';
	}
}
erp_section_card('Document Control hub', ob_get_clean(), array('icon' => 'fa-print'));
