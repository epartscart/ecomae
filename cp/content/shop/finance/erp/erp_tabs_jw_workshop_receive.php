<?php
/**
 * Jewellery ERP — Workshop Receive (RRC).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-inbox"></i> Workshop Receive', 'Receive repaired items back from workshop.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Workshop receive'),
));
?>
<div class="ef-window">
	<div class="ef-title">Workshop Receive - (RRC)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find Transfer</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_workshop_receive_save">

		<div class="ef-section">
			<span class="ef-section-title">Receive Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Receive Date</label><input name="receive_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Receive No.</label><input name="receive_no" class="ef-readonly" readonly placeholder="Auto" style="width:80px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Transfer Ref.</label><input name="transfer_ref" placeholder="RET-xxxx"></div>
				<div class="ef-field"><label>From Workshop</label><input name="from_workshop"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Received Items</span>
			<table class="ef-grid">
				<thead><tr><th>No.</th><th>Repair No.</th><th>Item Desc</th><th>Gross Wt In</th><th>Gross Wt Out</th><th>Wt Diff</th><th>Workshop Cost</th><th>QC Status</th></tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 4; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="items[<?php echo $r; ?>][repair_no]"></td>
					<td><input name="items[<?php echo $r; ?>][description]" style="min-width:140px"></td>
					<td><input name="items[<?php echo $r; ?>][wt_in]" type="number" step="0.001" value="0"></td>
					<td><input name="items[<?php echo $r; ?>][wt_out]" type="number" step="0.001" value="0"></td>
					<td><input name="items[<?php echo $r; ?>][wt_diff]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="items[<?php echo $r; ?>][workshop_cost]" type="number" step="0.01" value="0"></td>
					<td><select name="items[<?php echo $r; ?>][qc]"><option value="">—</option><option value="Pass">Pass</option><option value="Fail">Fail</option><option value="Rework">Rework</option></select></td>
				</tr>
				<?php endfor; ?>
				</tbody>
			</table>
		</div>

		<div class="ef-section"><span class="ef-section-title">Remarks</span><textarea name="narration" class="ef-narration"></textarea></div>
		<div class="ef-actions"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button></div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Workshop Receive — RRC</span></div>
</div>
