<?php
/**
 * Jewellery ERP — Metal Stock Balance.
 * Ref: Suntech Metal Stock Balance screenshot (tree-view by division/karat with qty/weight/value).
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

erp_page_header('<i class="fa fa-cubes"></i> Metal Stock Balance', 'Current stock position by metal, karat, and item.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Stock balance'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Stock Balance</div>
	<div class="ef-toolbar">
		<button class="btn btn-primary btn-xs" onclick="jwSbRun()"><i class="fa fa-play"></i> Generate</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-file-excel-o"></i> Export</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Filter</span>
			<div class="ef-row">
				<div class="ef-field"><label>As At Date</label><input id="sb_date" type="date" value="<?php echo date('Y-m-d'); ?>"></div>
				<div class="ef-field"><label>Branch</label>
					<select id="sb_branch"><option value="">All Branches</option><option value="HO">HO</option></select>
				</div>
				<div class="ef-field"><label>Division</label>
					<select id="sb_division"><option value="">All Metals</option><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?></select>
				</div>
				<div class="ef-field"><label>Karat</label><input id="sb_karat" maxlength="10" placeholder="All"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Category</label><input id="sb_category" maxlength="30" placeholder="All"></div>
				<div class="ef-field"><label>Item Code</label><input id="sb_item" maxlength="20" placeholder="All"></div>
				<div class="ef-field"><label>Show Zero Stock</label>
					<select id="sb_zero"><option value="N">No</option><option value="Y">Yes</option></select>
				</div>
				<div class="ef-field"><label>Group By</label>
					<select id="sb_group"><option value="division">Division</option><option value="karat">Karat</option><option value="category">Category</option><option value="item">Item Code</option></select>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Stock Balance</span>
			<table class="ef-grid" id="sb_results">
				<thead><tr>
					<th>No.</th><th>Group</th><th>Item Code</th><th>Description</th>
					<th>Karat</th><th>Purity</th><th>Stock Pcs</th><th>Stock Wt (Gms)</th>
					<th>Avg Cost</th><th>Stock Value</th>
				</tr></thead>
				<tbody>
					<tr><td colspan="10" style="text-align:center;color:#999">Click "Generate" to load stock balance</td></tr>
				</tbody>
			</table>
		</div>

		<div class="ef-totals">
			<div class="ef-row">
				<div class="ef-field"><label>Total Items</label><input id="sb_total_items" value="0" readonly></div>
				<div class="ef-field"><label>Total Pcs</label><input id="sb_total_pcs" value="0" readonly></div>
				<div class="ef-field"><label>Total Weight (Gms)</label><input id="sb_total_wt" value="0.000" readonly></div>
				<div class="ef-field"><label>Total Value</label><input id="sb_total_val" value="0.00" readonly style="font-weight:bold"></div>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=REPORT</span>
		<span>Metal Stock Balance as at <?php echo date('d/m/Y'); ?></span>
	</div>
</div>
<script>
function jwSbRun(){
	document.getElementById('sb_results').querySelector('tbody').innerHTML='<tr><td colspan="10" style="text-align:center"><i class="fa fa-spinner fa-spin"></i> Loading stock balance...</td></tr>';
}
</script>
