<?php
/**
 * Jewellery ERP — Diamond Purchase.
 * Ref: Suntech Diamond Purchase screenshot (tabs: Line Items / Consignment / Attachments).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$purchases = epc_jewel_purchase_list($db_link, $companyId, 'DIAMOND');

erp_page_header('<i class="fa fa-diamond"></i> Diamond Purchase', 'Diamond purchase with consignment and attachments.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Diamond purchase'),
));
?>
<div class="ef-window">
	<div class="ef-title">Diamond Purchase</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_dp_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Voc No</th><th>Voc Date</th><th>Party Code</th>
				<th>Supp Inv</th><th>Net Amt</th>
			</tr></thead>
			<tbody>
			<?php if (empty($purchases)): ?>
				<tr><td colspan="6" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($purchases as $p): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($p['voc_no']); ?></strong></td>
					<td><?php echo epc_erp_h($p['voc_date']); ?></td>
					<td><?php echo epc_erp_h($p['party_code']); ?></td>
					<td><?php echo epc_erp_h($p['supp_inv_no']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$p['net_amount'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_dp_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_diamond_purchase_save">

			<div class="ef-section">
				<span class="ef-section-title">Voucher Header</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Voc Type</label><input name="voc_type" maxlength="5" value="DPR" readonly></div>
					<div class="ef-field"><label>Voc Date</label><input name="voc_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Voc No</label><input name="voc_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Party Code</label><input name="party_code" maxlength="20" required></div>
					<div class="ef-field"><label>Party Name</label><input name="party_name" maxlength="80" readonly></div>
					<div class="ef-field"><label>Party Curr</label><input name="party_currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Salesman</label><input name="salesman" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Supp. Inv No</label><input name="supp_inv_no" maxlength="30"></div>
					<div class="ef-field"><label>Supp. Inv Date</label><input name="supp_inv_date" type="date"></div>
					<div class="ef-field"><label>Cr. Days</label><input name="cr_days" type="number" value="0"></div>
					<div class="ef-field"><label><input type="checkbox" name="consignment" value="1"> Consignment</label></div>
				</div>
			</div>

			<div class="ef-tabs">
				<button type="button" class="ef-tab active" onclick="jwDpTab(this,'items')">1. Line Items</button>
				<button type="button" class="ef-tab" onclick="jwDpTab(this,'consign')">2. Consignment</button>
				<button type="button" class="ef-tab" onclick="jwDpTab(this,'attach')">3. Attachments</button>
				<button type="button" class="ef-tab" onclick="jwDpTab(this,'other')">4. Other Amounts</button>
			</div>

			<div id="jw_dp_items" class="ef-tab-pane">
				<table class="ef-grid">
					<thead><tr>
						<th>No.</th><th>Item Code</th><th>Description</th><th>Pcs</th>
						<th>Carat</th><th>Rate/Ct</th><th>Amount</th><th>Certificate</th>
					</tr></thead>
					<tbody>
						<tr>
							<td>1</td>
							<td><input name="li_item_code" maxlength="20" style="width:80px"></td>
							<td><input name="li_description" maxlength="120" style="width:140px"></td>
							<td><input name="li_pcs" type="number" value="0" style="width:50px"></td>
							<td><input name="li_carat" type="number" step="0.0001" value="0.0000" style="width:70px"></td>
							<td><input name="li_rate_ct" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="li_amount" type="number" step="0.01" value="0.00" style="width:80px"></td>
							<td><input name="li_certificate" maxlength="40" style="width:80px"></td>
						</tr>
					</tbody>
				</table>
			</div>

			<div id="jw_dp_consign" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Consignment Details</span>
					<div class="ef-row">
						<div class="ef-field"><label>Consignment No</label><input name="consign_no" maxlength="20"></div>
						<div class="ef-field"><label>Consignment Date</label><input name="consign_date" type="date"></div>
						<div class="ef-field"><label>Return Date</label><input name="consign_return_date" type="date"></div>
					</div>
					<div class="ef-row">
						<div class="ef-field ef-field-wide"><label>Remarks</label><textarea name="consign_remarks" rows="2" maxlength="500" style="width:100%"></textarea></div>
					</div>
				</div>
			</div>

			<div id="jw_dp_attach" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Attachments</span>
					<div class="ef-row">
						<div class="ef-field"><label>File</label><input type="file" name="attachment" disabled><br><small style="color:#999">File upload not available yet</small></div>
					</div>
				</div>
			</div>

			<div id="jw_dp_other" class="ef-tab-pane" style="display:none">
				<div class="ef-section">
					<span class="ef-section-title">Other Amounts</span>
					<div class="ef-row">
						<div class="ef-field"><label>Discount</label><input name="discount_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Freight</label><input name="freight_amount" type="number" step="0.01" value="0.00"></div>
						<div class="ef-field"><label>Insurance</label><input name="insurance_amount" type="number" step="0.01" value="0.00"></div>
					</div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Narration</span>
				<div class="ef-row">
					<div class="ef-field ef-field-wide"><textarea name="narration" rows="2" maxlength="500" style="width:100%"></textarea></div>
				</div>
			</div>

			<div class="ef-totals">
				<div class="ef-row">
					<div class="ef-field"><label>Total Pcs</label><input name="total_pcs" type="number" value="0" readonly></div>
					<div class="ef-field"><label>Total Carat</label><input name="total_carat" type="number" step="0.0001" value="0.0000" readonly></div>
					<div class="ef-field"><label>Net Amount</label><input name="net_amount" type="number" step="0.01" value="0.00" readonly></div>
					<div class="ef-field"><label>VAT</label><input name="vat_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Gross Total</label><input name="gross_total" type="number" step="0.01" value="0.00" readonly></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_dp_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
<script>
function jwDpTab(btn, pane){
	document.querySelectorAll('.ef-tab').forEach(function(t){t.classList.remove('active');});
	btn.classList.add('active');
	['items','consign','attach','other'].forEach(function(p){
		document.getElementById('jw_dp_'+p).style.display=(p===pane)?'block':'none';
	});
}
</script>
