<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/document_control/epc_document_control_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

$dcCat = isset($_GET['dc_cat']) ? (string)$_GET['dc_cat'] : '';
$docEntity = isset($_GET['doc_entity']) ? (string)$_GET['doc_entity'] : '';
$docEntityId = (int)($_GET['doc_id'] ?? 0);
$docSource = isset($_GET['doc_source']) ? (string)$_GET['doc_source'] : 'all';

$attachments = ($docSource === 'erp' || $docSource === 'all')
	? epc_erp_documents_list($db_link, $docEntity, $docEntityId, 200, $dcCat, $date_from, $date_to)
	: array();
$dcAttachments = ($docSource === 'dc' || $docSource === 'all')
	? epc_dc_list_attachments($db_link, $docEntity, $docEntityId, $dcCat)
	: array();
$dcHub = function_exists('epc_document_control_cp_url') ? epc_document_control_cp_url() : ('/' . (isset($DP_Config) ? (string) $DP_Config->backend_dir : 'cp') . '/shop/document_control/document_control');
$totalShown = count($attachments) + count($dcAttachments);

erp_page_header(
	'<i class="fa fa-folder-open-o"></i> Document control',
	'Central ECM library — upload, link to invoices, POs, orders, CRM leads, and projects.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Documents'),
	),
	array(
		array('label' => 'Document Control CP', 'url' => $dcHub, 'class' => 'btn-default', 'icon' => 'fa-external-link'),
	)
);
erp_filter_bar($erpUrl, 'documents', $date_from_str, $date_to_str,
	'<input type="hidden" name="area" value="collaboration">'
	. '<label>Entity type</label> <select name="doc_entity" class="form-control input-sm">'
	. '<option value="">All</option>'
	. '<option value="invoice"' . ($docEntity === 'invoice' ? ' selected' : '') . '>Invoice</option>'
	. '<option value="purchase"' . ($docEntity === 'purchase' ? ' selected' : '') . '>Purchase</option>'
	. '<option value="purchase_order"' . ($docEntity === 'purchase_order' ? ' selected' : '') . '>Purchase order</option>'
	. '<option value="order"' . ($docEntity === 'order' ? ' selected' : '') . '>Sales order</option>'
	. '<option value="opportunity"' . ($docEntity === 'opportunity' ? ' selected' : '') . '>CRM opportunity</option>'
	. '<option value="project"' . ($docEntity === 'project' ? ' selected' : '') . '>Project</option>'
	. '<option value="lead"' . ($docEntity === 'lead' ? ' selected' : '') . '>CRM lead</option>'
	. '</select>'
	. ' <label>Entity ID</label> <input type="number" name="doc_id" class="form-control input-sm" value="' . ($docEntityId > 0 ? (int)$docEntityId : '') . '" placeholder="ID">'
	. ' <label>Category</label> <input type="text" name="dc_cat" class="form-control input-sm" value="' . epc_erp_h($dcCat) . '" placeholder="e.g. supplier_invoice">'
	. ' <label>Source</label> <select name="doc_source" class="form-control input-sm">'
	. '<option value="all"' . ($docSource === 'all' ? ' selected' : '') . '>All</option>'
	. '<option value="erp"' . ($docSource === 'erp' ? ' selected' : '') . '>ERP ECM</option>'
	. '<option value="dc"' . ($docSource === 'dc' ? ' selected' : '') . '>Document Control</option>'
	. '</select>'
);
erp_stat_cards(array(
	array('label' => 'Attachments shown', 'value' => (string)$totalShown),
	array('label' => 'ERP native', 'value' => (string)count($attachments)),
	array('label' => 'Doc Control', 'value' => (string)count($dcAttachments)),
));

ob_start();
if ($totalShown === 0) {
	erp_empty_state('No attachments match your filters. Upload a file below or use Document Control CP for templates and print docs.');
} else {
	erp_table_open(array('Date', 'Source', 'Entity', 'Category', 'Reference', 'File', 'Version', 'Size', ''));
	foreach ($attachments as $a) {
		echo '<tr><td>' . epc_erp_h(date('Y-m-d H:i', (int)$a['time_created'])) . '</td>';
		echo '<td><span class="label label-primary">ERP</span></td>';
		echo '<td>' . epc_erp_h($a['entity_type'] . ' #' . (int)$a['entity_id']) . '</td>';
		echo '<td>' . epc_erp_h($a['doc_category']) . '</td>';
		echo '<td>' . epc_erp_h($a['notes'] ?: '—') . '</td>';
		echo '<td><a href="' . epc_erp_h($a['file_path']) . '" target="_blank" rel="noopener">' . epc_erp_h($a['file_name']) . '</a></td>';
		echo '<td>' . epc_erp_h($a['version_note'] ?? '') . '</td>';
		echo '<td>' . epc_erp_h(number_format((int)$a['file_size'] / 1024, 1)) . ' KB</td>';
		echo '<td><form class="epc-erp-doc-delete" style="display:inline;"><input type="hidden" name="csrf_guard_key" value="' . epc_erp_h($csrf) . '">'
			. '<input type="hidden" name="doc_id" value="' . (int)$a['id'] . '">'
			. '<button type="submit" class="btn btn-xs btn-danger" title="Delete"><i class="fa fa-trash"></i></button></form></td></tr>';
	}
	foreach ($dcAttachments as $a) {
		echo '<tr><td>' . epc_erp_h(date('Y-m-d H:i', (int)$a['uploaded_at'])) . '</td>';
		echo '<td><span class="label label-default">DC</span></td>';
		echo '<td>' . epc_erp_h($a['entity_type'] . ' #' . (int)$a['entity_id']) . '</td>';
		echo '<td>' . epc_erp_h($a['doc_category']) . '</td>';
		echo '<td>' . epc_erp_h($a['reference_no'] ?: $a['supplier_name'] ?: '—') . '</td>';
		echo '<td><a href="' . epc_erp_h($a['file_path']) . '" target="_blank" rel="noopener">' . epc_erp_h($a['file_name']) . '</a></td>';
		echo '<td>' . epc_erp_h($a['notes'] ?? '') . '</td>';
		echo '<td>' . epc_erp_h(number_format((int)$a['file_size'] / 1024, 1)) . ' KB</td><td></td></tr>';
	}
	erp_table_close();
}
erp_section_card('Document library', ob_get_clean(), array('icon' => 'fa-paperclip'));

ob_start();
?>
<form id="epc_erp_form_doc_upload" class="form-horizontal" style="max-width:760px;" enctype="multipart/form-data">
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
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-upload"></i> Upload attachment</button></div></div>
</form>
<?php
$uploadHtml = ob_get_clean();
erp_section_card('Upload attachment', $uploadHtml, array('icon' => 'fa-upload'));
?>
<script>
(function(){
	document.querySelectorAll('.epc-erp-doc-delete').forEach(function(f){
		f.addEventListener('submit', function(ev){
			ev.preventDefault();
			if (!confirm('Delete this attachment?')) return;
			var fd = new FormData(f);
			fd.append('action', 'document_delete');
			fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function(r){ return r.json(); })
				.then(function(j){ alert(j.message || (j.status ? 'Deleted' : 'Error')); if (j.status) location.reload(); });
		});
	});
})();
</script>
