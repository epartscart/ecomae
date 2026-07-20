<?php
/**
 * ERP Document Control — native module for ERP (including ERP-only / no CP).
 * URL: /erp/?area=tax&tab=document_control
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';

$isErpOnly = function_exists('epc_erp_is_erp_only_context') && epc_erp_is_erp_only_context();
$dcArea = function_exists('epc_erp_tab_to_area') ? epc_erp_tab_to_area('document_control') : 'tax';
if ($dcArea === '' || $dcArea === 'overview') {
	$dcArea = 'tax';
}
$dcBase = epc_erp_tab_url($erpUrl, 'document_control', $date_from_str, $date_to_str, $dcArea);
$dcAjax = isset($erpAjaxEndpoint) ? (string) $erpAjaxEndpoint : '';
if ($dcAjax === '') {
	$dcAjax = '/' . (isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp')
		. '/content/shop/finance/erp/ajax_erp_endpoint.php';
}

$assetVer = function_exists('epc_cp_page_asset_version') ? epc_cp_page_asset_version() : '20260720dc1';
$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
if ($backend === '') {
	$backend = 'cp';
}
$dcCssHref = '/content/general_pages/epc_document_control_cp_css.php?v=' . rawurlencode($assetVer);
$dcJsSrc = '/' . $backend . '/content/shop/document_control/epc_document_control.js?v=' . rawurlencode($assetVer);
if (!isset($GLOBALS['epc_cp_page_assets']) || !is_array($GLOBALS['epc_cp_page_assets'])) {
	$GLOBALS['epc_cp_page_assets'] = array('css' => array(), 'js' => array());
}
$GLOBALS['epc_cp_page_assets']['css'][$dcCssHref] = true;
$GLOBALS['epc_cp_page_assets']['js'][$dcJsSrc] = true;

// Inline CSS for standalone /erp (static CP CSS may 404 on marketing host).
$dcCssFile = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/document_control/epc_document_control.css';
if (is_file($dcCssFile)) {
	echo '<style data-erp-inline="epc_document_control.css">' . "\n" . file_get_contents($dcCssFile) . "\n</style>\n";
}

$headerActions = array(
	array('label' => 'Print preview', 'url' => '/content/shop/document_control/service/print.php?doc=fta_tax_invoice&preview=1', 'class' => 'btn-default', 'icon' => 'fa-eye'),
);
if (!$isErpOnly) {
	$cpDc = function_exists('epc_document_control_cp_url') ? epc_document_control_cp_url() : '';
	if ($cpDc !== '') {
		$headerActions[] = array('label' => 'Open in CP', 'url' => $cpDc, 'class' => 'btn-default', 'icon' => 'fa-external-link');
	}
}

erp_page_header(
	'<i class="fa fa-print"></i> Document Control',
	$isErpOnly
		? 'ERP-native document management — company letterhead, FTA print templates, attachments, and ECM library (no commerce CP required).'
		: 'Print templates, company letterhead, attachments, and ERP document library — built into ERP.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Document Control'),
	),
	$headerActions
);

// Build ERP ECM library panel (shown on dc_tab=library).
ob_start();
$docEntity = isset($_GET['doc_entity']) ? (string) $_GET['doc_entity'] : '';
$docEntityId = (int) ($_GET['doc_id'] ?? 0);
$dcCat = isset($_GET['dc_cat']) ? (string) $_GET['dc_cat'] : '';
$erpDocs = epc_erp_documents_list($db_link, $docEntity, $docEntityId, 200, $dcCat, $date_from, $date_to);
$dcAtts = epc_dc_list_attachments($db_link, $docEntity, $docEntityId, $dcCat);
$totalLib = count($erpDocs) + count($dcAtts);
?>
<p class="text-muted">Upload files against invoices, POs, orders, CRM leads, and projects. Stored in the ERP ECM library and Document Control attachments.</p>
<form class="form-inline" method="get" action="<?php echo epc_erp_h($erpUrl); ?>" style="margin-bottom:14px;">
	<input type="hidden" name="area" value="<?php echo epc_erp_h($dcArea); ?>">
	<input type="hidden" name="tab" value="document_control">
	<input type="hidden" name="dc_tab" value="library">
	<input type="hidden" name="from" value="<?php echo epc_erp_h($date_from_str); ?>">
	<input type="hidden" name="to" value="<?php echo epc_erp_h($date_to_str); ?>">
	<label>Entity</label>
	<select name="doc_entity" class="form-control input-sm">
		<option value="">All</option>
		<?php
		$ents = array(
			'invoice' => 'Invoice',
			'purchase' => 'Purchase',
			'purchase_order' => 'Purchase order',
			'order' => 'Sales order',
			'opportunity' => 'CRM opportunity',
			'project' => 'Project',
			'lead' => 'CRM lead',
		);
		foreach ($ents as $ek => $el) {
			echo '<option value="' . epc_erp_h($ek) . '"' . ($docEntity === $ek ? ' selected' : '') . '>' . epc_erp_h($el) . '</option>';
		}
		?>
	</select>
	<label>ID</label>
	<input type="number" name="doc_id" class="form-control input-sm" value="<?php echo $docEntityId > 0 ? (int) $docEntityId : ''; ?>" placeholder="ID">
	<label>Category</label>
	<input type="text" name="dc_cat" class="form-control input-sm" value="<?php echo epc_erp_h($dcCat); ?>" placeholder="e.g. supplier_invoice">
	<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-filter"></i> Filter</button>
</form>
<div class="epc-dc-kpi" style="margin-bottom:16px;">
	<div class="kpi"><div class="lbl">Shown</div><div class="val"><?php echo (int) $totalLib; ?></div></div>
	<div class="kpi"><div class="lbl">ERP ECM</div><div class="val"><?php echo (int) count($erpDocs); ?></div></div>
	<div class="kpi"><div class="lbl">Doc Control</div><div class="val"><?php echo (int) count($dcAtts); ?></div></div>
</div>
<?php if ($totalLib === 0): ?>
	<p class="text-muted">No attachments match your filters. Upload below.</p>
<?php else: ?>
	<table class="table table-striped table-condensed">
		<thead><tr><th>Date</th><th>Source</th><th>Entity</th><th>Category</th><th>File</th><th>Size</th><th></th></tr></thead>
		<tbody>
		<?php foreach ($erpDocs as $a): ?>
			<tr>
				<td><?php echo epc_erp_h(date('Y-m-d H:i', (int) $a['time_created'])); ?></td>
				<td><span class="label label-primary">ERP</span></td>
				<td><?php echo epc_erp_h($a['entity_type'] . ' #' . (int) $a['entity_id']); ?></td>
				<td><?php echo epc_erp_h($a['doc_category']); ?></td>
				<td><a href="<?php echo epc_erp_h($a['file_path']); ?>" target="_blank" rel="noopener"><?php echo epc_erp_h($a['file_name']); ?></a></td>
				<td><?php echo epc_erp_h(number_format((int) $a['file_size'] / 1024, 1)); ?> KB</td>
				<td>
					<form class="epc-erp-doc-delete" style="display:inline;">
						<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
						<input type="hidden" name="doc_id" value="<?php echo (int) $a['id']; ?>">
						<button type="submit" class="btn btn-xs btn-danger" title="Delete"><i class="fa fa-trash"></i></button>
					</form>
				</td>
			</tr>
		<?php endforeach; ?>
		<?php foreach ($dcAtts as $a): ?>
			<tr>
				<td><?php echo epc_erp_h(date('Y-m-d H:i', (int) $a['uploaded_at'])); ?></td>
				<td><span class="label label-default">DC</span></td>
				<td><?php echo epc_erp_h($a['entity_type'] . ' #' . (int) $a['entity_id']); ?></td>
				<td><?php echo epc_erp_h($a['doc_category']); ?></td>
				<td><a href="<?php echo epc_erp_h($a['file_path']); ?>" target="_blank" rel="noopener"><?php echo epc_erp_h($a['file_name']); ?></a></td>
				<td><?php echo epc_erp_h(number_format((int) $a['file_size'] / 1024, 1)); ?> KB</td>
				<td></td>
			</tr>
		<?php endforeach; ?>
		</tbody>
	</table>
<?php endif; ?>

<form id="epc_erp_form_doc_upload" class="form-horizontal well" style="max-width:760px;" enctype="multipart/form-data">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Entity</label><div class="col-sm-9 form-inline">
		<select name="entity_type" class="form-control input-sm">
			<option value="invoice">Invoice</option>
			<option value="purchase">Purchase</option>
			<option value="purchase_order">Purchase order</option>
			<option value="order">Sales order</option>
			<option value="opportunity">CRM opportunity</option>
			<option value="project">Project</option>
			<option value="lead">CRM lead</option>
		</select>
		<input name="entity_id" type="number" class="form-control input-sm" placeholder="Record ID" required>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Category</label><div class="col-sm-9"><input name="doc_category" class="form-control input-sm" value="general"></div></div>
	<div class="form-group"><label class="col-sm-3">Version note</label><div class="col-sm-9"><input name="version_note" class="form-control input-sm" placeholder="e.g. v2 signed PDF"></div></div>
	<div class="form-group"><label class="col-sm-3">Notes</label><div class="col-sm-9"><input name="notes" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">File</label><div class="col-sm-9"><input type="file" name="file" class="form-control input-sm" required></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-upload"></i> Upload to ERP library</button></div></div>
</form>
<script>
(function(){
	var ajax = <?php echo json_encode($dcAjax); ?>;
	document.querySelectorAll('.epc-erp-doc-delete').forEach(function(f){
		f.addEventListener('submit', function(ev){
			ev.preventDefault();
			if (!confirm('Delete this attachment?')) return;
			var fd = new FormData(f);
			fd.append('action', 'document_delete');
			fetch(ajax, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ alert(j.message || (j.status ? 'Deleted' : 'Error')); if (j.status) location.reload(); });
		});
	});
	var uf = document.getElementById('epc_erp_form_doc_upload');
	if (uf) {
		uf.addEventListener('submit', function(ev){
			ev.preventDefault();
			var fd = new FormData(uf);
			fd.append('action', 'document_upload');
			fetch(ajax, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ alert(j.message || (j.status ? 'Uploaded' : 'Error')); if (j.status) location.reload(); });
		});
	}
})();
</script>
<?php
$epc_dc_library_html = ob_get_clean();

// Embed Document Control UI (same module as CP, ERP-native URLs + AJAX).
$GLOBALS['epc_dc_tab_param'] = 'dc_tab';
$epc_dc_embed = true;
$epc_dc_erp_only = $isErpOnly;
$dcUrl = $dcBase;
$dcAjaxUrl = $dcAjax;
$printBase = '/content/shop/document_control/service/print.php';
$erpEinvoiceUrl = epc_erp_tab_url($erpUrl, 'einvoice', $date_from_str, $date_to_str, 'tax');
$ordersUrl = $isErpOnly
	? epc_erp_tab_url($erpUrl, 'sales_orders', $date_from_str, $date_to_str, 'sales')
	: ('/' . $backend . '/shop/orders/orders');

$dcMain = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/document_control/document_control_main.php';
if (!is_file($dcMain)) {
	$dcMain = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/document_control/document_control_main.php';
}
if (!is_file($dcMain)) {
	erp_empty_state('Document Control module files are missing on this host.');
	return;
}
include $dcMain;
?>
<script>
window.EPC_DOCUMENT_CONTROL = {
	ajaxUrl: <?php echo json_encode($dcAjax); ?>,
	csrf: <?php echo json_encode((string) $csrf); ?>
};
</script>
<script src="<?php echo epc_erp_h($dcJsSrc); ?>"></script>
<?php
