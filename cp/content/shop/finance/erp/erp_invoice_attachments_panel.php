<?php
/**
 * Embedded document attachments for an ERP invoice (e-invoice document id).
 */
defined('_ASTEXE_') or die('No access');
if (empty($invoiceId) || (int)$invoiceId <= 0) {
	return;
}
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
$invDocs = epc_erp_documents_list($db_link, 'invoice', (int)$invoiceId, 50);
$erpInvUrl = epc_erp_tab_url($erpUrl, 'invoices', $date_from_str, $date_to_str) . '&inv_id=' . (int)$invoiceId;
?>
<div class="epc-erp-section" style="margin-top:20px;">
	<h4><i class="fa fa-paperclip"></i> Attachments</h4>
	<?php if (empty($invDocs)): ?>
		<p class="text-muted">No files linked to this invoice yet.</p>
	<?php else: ?>
		<table class="table table-condensed table-bordered">
			<thead><tr><th>Date</th><th>Category</th><th>File</th><th>Version note</th><th>Size</th></tr></thead>
			<tbody>
			<?php foreach ($invDocs as $d): ?>
				<tr>
					<td><?php echo epc_erp_h(date('Y-m-d H:i', (int)$d['time_created'])); ?></td>
					<td><?php echo epc_erp_h($d['doc_category']); ?></td>
					<td><a href="<?php echo epc_erp_h($d['file_path']); ?>" target="_blank" rel="noopener"><?php echo epc_erp_h($d['file_name']); ?></a></td>
					<td><?php echo epc_erp_h($d['version_note'] ?? ''); ?></td>
					<td><?php echo epc_erp_h(number_format((int)$d['file_size'] / 1024, 1)); ?> KB</td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
	<?php endif; ?>
	<form id="epc_erp_form_inv_attachment" class="form-inline" enctype="multipart/form-data" style="margin-top:10px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
		<input type="hidden" name="entity_type" value="invoice">
		<input type="hidden" name="entity_id" value="<?php echo (int)$invoiceId; ?>">
		<input type="text" name="doc_category" class="form-control input-sm" value="tax_invoice" placeholder="Category">
		<input type="text" name="version_note" class="form-control input-sm" placeholder="Version note">
		<input type="file" name="file" class="form-control input-sm" required>
		<button type="submit" class="btn btn-default btn-sm">Upload</button>
	</form>
</div>
