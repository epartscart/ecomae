<?php
/**
 * Jewellery ERP — Metal Purchase Fixing.
 * Ref: Suntech Purchase Fixing screenshot (simplified header form).
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$fixings = epc_jewel_fixing_list($db_link, $companyId, 'PURCHASE');

erp_page_header('<i class="fa fa-lock"></i> Metal Purchase Fixing', 'Fix metal rates against unfixed purchase vouchers.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Purchase fixing'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Purchase Fixing</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_pf_form').style.display='block'"><i class="fa fa-plus"></i> New Fixing</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Fix No</th><th>Fix Date</th><th>Ref Voc</th>
				<th>Party Code</th><th>Metal</th><th>Fixed Rate</th><th>Net Wt</th><th>Amount</th>
			</tr></thead>
			<tbody>
			<?php if (empty($fixings)): ?>
				<tr><td colspan="9" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($fixings as $f): ?>
				<tr class="ef-grid-row" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($f['fix_no']); ?></strong></td>
					<td><?php echo epc_erp_h($f['fix_date']); ?></td>
					<td><?php echo epc_erp_h($f['ref_voc_no']); ?></td>
					<td><?php echo epc_erp_h($f['party_code']); ?></td>
					<td><?php echo epc_erp_h($f['metal']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$f['fixed_rate'], 5); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$f['net_wt'], 3); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$f['amount'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_pf_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_purchase_fixing_save">

			<div class="ef-section">
				<span class="ef-section-title">Fixing Details</span>
				<div class="ef-row">
					<div class="ef-field"><label>Branch</label><input name="branch" maxlength="10" value="HO"></div>
					<div class="ef-field"><label>Fix Date</label><input name="fix_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
					<div class="ef-field"><label>Fix No</label><input name="fix_no" maxlength="20" placeholder="Auto"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Ref Voc Type</label><input name="ref_voc_type" maxlength="5" value="PUR"></div>
					<div class="ef-field"><label>Ref Voc No</label><input name="ref_voc_no" maxlength="20" required placeholder="Original purchase voc"></div>
					<div class="ef-field"><label>Party Code</label><input name="party_code" maxlength="20"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Metal</label><input name="metal" maxlength="2" value="G"></div>
					<div class="ef-field"><label>Fixed Rate (GMS)</label><input name="fixed_rate" type="number" step="0.00001" value="0.00000" required></div>
					<div class="ef-field"><label>Net Wt</label><input name="net_wt" type="number" step="0.001" value="0.000"></div>
					<div class="ef-field"><label>Amount</label><input name="amount" type="number" step="0.01" value="0.00"></div>
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
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_pf_form').style.display='none'">Cancel</button>
			</div>
			</form>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=VIEW</span>
		<span>Header New Record &rarr; Function Key (F5)</span>
	</div>
</div>
