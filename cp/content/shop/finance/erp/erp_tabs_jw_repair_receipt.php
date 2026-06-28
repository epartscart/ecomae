<?php
/**
 * Jewellery ERP — Repair Receipt.
 * Ref: Suntech Repair Receipt screenshot (customer job intake).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$repairs = epc_jewel_repair_list($db_link, $companyId, date('Y-01-01'), date('Y-m-d'), 'Received');

erp_page_header('<i class="fa fa-wrench"></i> Repair Receipt', 'Customer repair job intake — receive items for repair.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Repair receipt'),
));
?>
<div class="ef-window">
	<div class="ef-title">Repair Receipt</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_rr_form').style.display='block'"><i class="fa fa-plus"></i> New Receipt</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Job No</th><th>Receipt Date</th><th>Customer</th>
				<th>Mobile</th><th>Item</th><th>Promise Date</th><th>Status</th>
			</tr></thead>
			<tbody>
			<?php if (empty($repairs)): ?>
				<tr><td colspan="8" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($repairs as $r): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($r['job_no']); ?></strong></td>
					<td><?php echo epc_erp_h($r['receipt_date']); ?></td>
					<td><?php echo epc_erp_h($r['customer_name']); ?></td>
					<td><?php echo epc_erp_h($r['customer_mobile']); ?></td>
					<td><?php echo epc_erp_h($r['item_description']); ?></td>
					<td><?php echo epc_erp_h($r['promise_date']); ?></td>
					<td><?php echo epc_erp_h($r['status']); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_rr_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_repair_receipt_save">

			<div class="ef-section">
				<span class="ef-section-title">Customer Details</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Receipt Date</label><input name="receipt_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Job No</label><input name="job_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Customer Code</label><input name="customer_code" maxlength="20"></div>
					<div class="ef-field"><label>Customer Name</label><input name="customer_name" maxlength="80" required></div>
					<div class="ef-field"><label>Mobile</label><input name="customer_mobile" maxlength="20" required></div>
					<div class="ef-field"><label>Email</label><input name="customer_email" maxlength="80" type="email"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>ID Type</label>
						<select name="id_type"><option value="">--</option><option value="Emirates ID">Emirates ID</option><option value="Passport">Passport</option><option value="Driving License">Driving License</option></select>
					</div>
					<div class="ef-field"><label>ID Number</label><input name="id_number" maxlength="30"></div>
					<div class="ef-field"><label>Nationality</label><input name="nationality" maxlength="30"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Item Details</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><label>Item Description</label><input name="item_description" maxlength="200" required></div>
					<div class="ef-field"><label>Metal</label><input name="metal" maxlength="10" value="Gold"></div>
					<div class="ef-field"><label>Karat</label><input name="karat" maxlength="10"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Gross Wt</label><input name="gross_wt" type="number" step="0.001" value="0.000"></div>
					<div class="ef-field"><label>Net Wt</label><input name="net_wt" type="number" step="0.001" value="0.000"></div>
					<div class="ef-field"><label>No of Stones</label><input name="no_of_stones" type="number" value="0"></div>
					<div class="ef-field"><label>Stone Wt</label><input name="stone_wt" type="number" step="0.001" value="0.000"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Repair Details</span>
				<div class="ef-row">
					<div class="ef-field"><label>Repair Type</label>
						<select name="repair_type"><option value="Resize">Resize</option><option value="Polish">Polish</option><option value="Rhodium">Rhodium</option><option value="Solder">Solder</option><option value="Stone Setting">Stone Setting</option><option value="Engraving">Engraving</option><option value="Other">Other</option></select>
					</div>
					<div class="ef-field ef-field-wide"><label>Work Description</label><input name="work_description" maxlength="500"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Est. Charge</label><input name="est_charge" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Advance</label><input name="advance_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Promise Date</label><input name="promise_date" type="date"></div>
					<div class="ef-field"><label>Priority</label>
						<select name="priority"><option value="Normal">Normal</option><option value="Urgent">Urgent</option><option value="Express">Express</option></select>
					</div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_rr_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
