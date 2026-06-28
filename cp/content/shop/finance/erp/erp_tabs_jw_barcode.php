<?php
/**
 * Jewellery ERP — Metal Barcode Generation.
 * Ref: Suntech Metal Barcode Generation screenshot (generate/print barcodes for stock items).
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
$karats = epc_jewel_karat_list($db_link, $companyId);

erp_page_header('<i class="fa fa-barcode"></i> Metal Barcode Generation', 'Generate and print barcodes for metal stock items.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Barcode generation'),
));
?>
<div class="ef-window">
	<div class="ef-title">Metal Barcode Generation</div>
	<div class="ef-toolbar">
		<button class="btn btn-primary btn-xs" onclick="jwBcGenerate()"><i class="fa fa-barcode"></i> Generate</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-print"></i> Print Labels</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Clear</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Generation Settings</span>
			<div class="ef-row">
				<div class="ef-field"><label>Branch</label>
					<select name="branch"><option value="HO">HO</option></select>
				</div>
				<div class="ef-field"><label>Division</label>
					<select id="bc_division"><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?></select>
				</div>
				<div class="ef-field"><label>Karat</label>
					<select id="bc_karat"><option value="">All</option><?php foreach ($karats as $k): ?><option value="<?php echo epc_erp_h($k['karat_code']); ?>"><?php echo epc_erp_h($k['karat_code']); ?></option><?php endforeach; ?></select>
				</div>
				<div class="ef-field"><label>Category</label><input id="bc_category" maxlength="30" placeholder="All"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Item Code From</label><input id="bc_from" maxlength="20"></div>
				<div class="ef-field"><label>Item Code To</label><input id="bc_to" maxlength="20"></div>
				<div class="ef-field"><label>Barcode Prefix</label><input id="bc_prefix" maxlength="10" value="JW"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Label Size</label>
					<select id="bc_label_size"><option value="small">Small (30x15mm)</option><option value="medium" selected>Medium (50x25mm)</option><option value="large">Large (70x40mm)</option></select>
				</div>
				<div class="ef-field"><label>Copies Per Item</label><input id="bc_copies" type="number" value="1" min="1" max="99"></div>
				<div class="ef-field"><label>Include Price</label>
					<select id="bc_price"><option value="Y">Yes</option><option value="N">No</option></select>
				</div>
				<div class="ef-field"><label>Include Weight</label>
					<select id="bc_weight"><option value="Y">Yes</option><option value="N">No</option></select>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Items to Print</span>
			<table class="ef-grid" id="bc_items">
				<thead><tr>
					<th><input type="checkbox" id="bc_check_all" onclick="jwBcCheckAll(this)"></th>
					<th>No.</th><th>Item Code</th><th>Description</th><th>Karat</th>
					<th>Gross Wt</th><th>Price</th><th>Barcode</th><th>Copies</th>
				</tr></thead>
				<tbody>
					<tr><td colspan="9" style="text-align:center;color:#999">Click "Generate" to load items</td></tr>
				</tbody>
			</table>
		</div>

		<div class="ef-totals">
			<div class="ef-row">
				<div class="ef-field"><label>Total Items</label><input id="bc_total_items" value="0" readonly></div>
				<div class="ef-field"><label>Selected Items</label><input id="bc_selected" value="0" readonly></div>
				<div class="ef-field"><label>Total Labels</label><input id="bc_total_labels" value="0" readonly></div>
			</div>
		</div>
	</div>
	<div class="ef-status">
		<span>Mode:=GENERATE</span>
		<span>Metal Barcode Generation</span>
	</div>
</div>
<script>
function jwBcGenerate(){
	document.getElementById('bc_items').querySelector('tbody').innerHTML='<tr><td colspan="9" style="text-align:center"><i class="fa fa-spinner fa-spin"></i> Loading items...</td></tr>';
}
function jwBcCheckAll(cb){
	document.querySelectorAll('#bc_items tbody input[type=checkbox]').forEach(function(c){c.checked=cb.checked;});
}
</script>
