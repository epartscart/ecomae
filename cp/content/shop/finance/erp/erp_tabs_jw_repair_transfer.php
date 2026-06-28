<?php
/**
 * Jewellery ERP — Repair Transfer (RET/RTC).
 * Transfer repair jobs to workshop or sub-branch.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-truck"></i> Repair Transfer', 'Transfer repair jobs to workshop / sub-branch.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair transfer'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Transfer - (RET / RTC)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-search"></i> Find</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_repair_transfer_save">

		<div class="ef-section">
			<span class="ef-section-title">Transfer Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>From Branch</label><select name="from_branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>To Workshop</label><select name="to_branch"><option value="WS1">Workshop 1</option><option value="WS2">Workshop 2</option><option value="EXT">External</option></select></div>
				<div class="ef-field"><label>Transfer Date</label><input name="transfer_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Transfer No.</label><input name="transfer_no" class="ef-readonly" readonly placeholder="Auto" style="width:80px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Workshop Contact</label><input name="workshop_contact"></div>
				<div class="ef-field"><label>Expected Return</label><input name="expected_return" type="date"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Transfer Items</span>
			<table class="ef-grid">
				<thead><tr><th>No.</th><th>Repair No.</th><th>Item Desc</th><th>Metal</th><th>Gross Wt</th><th>Repair Type</th><th>Instructions</th></tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 4; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="items[<?php echo $r; ?>][repair_no]" placeholder="REP-xxxx"></td>
					<td><input name="items[<?php echo $r; ?>][description]" style="min-width:140px"></td>
					<td><select name="items[<?php echo $r; ?>][metal]"><option value="">—</option><option value="G">G</option><option value="S">S</option><option value="T">T</option></select></td>
					<td><input name="items[<?php echo $r; ?>][gross_wt]" type="number" step="0.001" value="0"></td>
					<td><input name="items[<?php echo $r; ?>][repair_type]" style="width:80px"></td>
					<td><input name="items[<?php echo $r; ?>][instructions]" style="min-width:120px"></td>
				</tr>
				<?php endfor; ?>
				</tbody>
			</table>
		</div>

		<div class="ef-section"><span class="ef-section-title">Remarks</span><textarea name="narration" class="ef-narration"></textarea></div>
		<div class="ef-actions"><button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button></div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Repair Transfer — RET/RTC</span></div>
</div>
