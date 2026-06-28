<?php
/**
 * Jewellery ERP — Stock Verification (MSV).
 * Physical count vs computer stock with barcode scanning.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$divisions = epc_jewel_divisions();

erp_page_header('<i class="fa fa-check-square-o"></i> Stock Verification', 'Physical count vs computer stock, barcode scanning.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Stock verification'),
));
?>
<div class="ef-window">
	<div class="ef-title">Stock Verification - (MSV)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs"><i class="fa fa-file-o"></i> New Count</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-save"></i> Save</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-barcode"></i> Scan Mode</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Variance Report</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-check"></i> Post Adjustment</button>
	</div>
	<div class="ef-body">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_stock_verification_save">

		<div class="ef-section">
			<span class="ef-section-title">Verification Details</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label><select name="branch"><option value="HO">HO</option></select></div>
				<div class="ef-field"><label>Count Date</label><input name="count_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Verification No.</label><input name="verify_no" class="ef-readonly" readonly placeholder="Auto" style="width:80px;background:#e8e8e8"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Division</label><select name="division"><option value="">All</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($c . ' — ' . $l); ?></option><?php endforeach; ?></select></div>
				<div class="ef-field"><label>Counter</label><input name="counter" placeholder="Staff name"></div>
				<div class="ef-field"><label>Supervisor</label><input name="supervisor"></div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Barcode Scan / Manual Entry</span>
			<div class="ef-row" style="margin-bottom:10px">
				<div class="ef-field ef-field-wide"><label><i class="fa fa-barcode"></i> Scan Barcode</label><input name="scan_barcode" placeholder="Scan or type barcode" style="min-width:200px"></div>
			</div>
			<table class="ef-grid">
				<thead><tr>
					<th>No.</th><th>Item Code</th><th>Description</th><th>Metal</th><th>Karat</th>
					<th>Comp. Pcs</th><th>Comp. Wt</th><th>Phys. Pcs</th><th>Phys. Wt</th>
					<th>Diff Pcs</th><th>Diff Wt</th><th>Status</th>
				</tr></thead>
				<tbody>
				<?php for ($r = 0; $r < 6; $r++): ?>
				<tr>
					<td><?php echo $r + 1; ?></td>
					<td><input name="lines[<?php echo $r; ?>][item_code]"></td>
					<td><input name="lines[<?php echo $r; ?>][description]" style="min-width:120px"></td>
					<td><input name="lines[<?php echo $r; ?>][metal]" style="width:30px"></td>
					<td><input name="lines[<?php echo $r; ?>][karat]" style="width:30px"></td>
					<td><input name="lines[<?php echo $r; ?>][comp_pcs]" type="number" value="0" class="ef-readonly" readonly style="width:50px"></td>
					<td><input name="lines[<?php echo $r; ?>][comp_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><input name="lines[<?php echo $r; ?>][phys_pcs]" type="number" value="0" style="width:50px"></td>
					<td><input name="lines[<?php echo $r; ?>][phys_wt]" type="number" step="0.001" value="0"></td>
					<td><input name="lines[<?php echo $r; ?>][diff_pcs]" type="number" value="0" class="ef-readonly" readonly style="width:50px"></td>
					<td><input name="lines[<?php echo $r; ?>][diff_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
					<td><select name="lines[<?php echo $r; ?>][status]"><option value="">—</option><option value="OK" style="color:green">OK</option><option value="Short" style="color:red">Short</option><option value="Excess" style="color:blue">Excess</option></select></td>
				</tr>
				<?php endfor; ?>
				</tbody>
			</table>
		</div>

		<div class="ef-section"><span class="ef-section-title">Remarks</span><textarea name="narration" class="ef-narration" placeholder="Verification notes"></textarea></div>
		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
			<button type="button" class="btn btn-warning btn-sm"><i class="fa fa-check"></i> Post Adjustment</button>
			<button type="button" class="btn btn-info btn-sm"><i class="fa fa-print"></i> Variance Report</button>
		</div>
		</form>
	</div>
	<div class="ef-status"><span>Mode:=ADD</span><span>Stock Verification — MSV</span></div>
</div>
