<?php
/**
 * Jewellery ERP — Journal Voucher (JV).
 * Ref: Suntech JV screenshot (header + debit/credit lines + narration + totals).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$journals = epc_jewel_journal_list($db_link, $companyId);

erp_page_header('<i class="fa fa-book"></i> Journal Voucher', 'Journal voucher entry with debit/credit lines.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Journal voucher'),
));
?>
<div class="ef-window">
	<div class="ef-title">Journal Voucher - (JV)</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_jv_form').style.display='block'"><i class="fa fa-plus"></i> New JV</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-check"></i> Post</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>JV No</th><th>JV Date</th><th>Description</th>
				<th>Total Debit</th><th>Total Credit</th><th>Status</th>
			</tr></thead>
			<tbody>
			<?php if (empty($journals)): ?>
				<tr><td colspan="7" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($journals as $j): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($j['jv_no']); ?></strong></td>
					<td><?php echo epc_erp_h($j['jv_date']); ?></td>
					<td><?php echo epc_erp_h($j['description']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$j['total_debit'], 2); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$j['total_credit'], 2); ?></td>
					<td><?php echo epc_erp_h($j['status']); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_jv_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_journal_voucher_save">

			<div class="ef-section">
				<span class="ef-section-title">Voucher Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>JV Type</label>
						<select name="jv_type"><option value="GEN">General</option><option value="ADJ">Adjustment</option><option value="RCL">Reclassification</option><option value="PRV">Provision</option></select>
					</div>
					<div class="ef-field"><label>JV Date</label><input name="jv_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>JV No</label><input name="jv_no" maxlength="20" placeholder="Auto" readonly style="background:#e8e8e8"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><label>Description</label><input name="jv_description" maxlength="200" required style="width:100%"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Journal Lines</span>
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Account Code</th><th>Account Name</th>
						<th>Cost Centre</th><th>Debit</th><th>Credit</th><th>Narration</th>
					</tr></thead>
					<tbody>
					<?php for ($r = 0; $r < 6; $r++): ?>
						<tr>
							<td><?php echo $r + 1; ?></td>
							<td><input name="lines[<?php echo $r; ?>][account_code]" maxlength="20" style="width:80px"></td>
							<td><input name="lines[<?php echo $r; ?>][account_name]" maxlength="80" style="min-width:120px"></td>
							<td><input name="lines[<?php echo $r; ?>][cost_centre]" maxlength="20" style="width:60px"></td>
							<td><input name="lines[<?php echo $r; ?>][debit]" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="lines[<?php echo $r; ?>][credit]" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="lines[<?php echo $r; ?>][narration]" maxlength="200" style="min-width:100px"></td>
						</tr>
					<?php endfor; ?>
					</tbody>
					<tfoot>
						<tr>
							<td colspan="4" style="text-align:right;font-weight:bold">Total:</td>
							<td><input name="total_debit" type="number" step="0.01" value="0.00" readonly style="width:80px;font-weight:bold"></td>
							<td><input name="total_credit" type="number" step="0.01" value="0.00" readonly style="width:80px;font-weight:bold"></td>
							<td></td>
						</tr>
						<tr>
							<td colspan="4" style="text-align:right;font-weight:bold">Difference:</td>
							<td colspan="2"><input name="difference" type="number" step="0.01" value="0.00" readonly style="width:100px;font-weight:bold;color:red"></td>
							<td></td>
						</tr>
					</tfoot>
				</table>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-success btn-sm"><i class="fa fa-check"></i> Post</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_jv_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Journal Voucher — JV</span>
	</div>
</div>
