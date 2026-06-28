<?php
/**
 * Jewellery ERP — Purchase Window.
 * Ref: Suntech Purchase Window screenshot (modal for item selection).
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

erp_page_header('<i class="fa fa-search-plus"></i> Purchase Window', 'Item lookup and selection for purchase entry.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Purchase window'),
));
?>
<div class="ef-window">
	<div class="ef-title">Purchase Window — Item Selection</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Search Filters</span>
			<div class="ef-row">
				<div class="ef-field"><label>Metal</label>
					<select id="pw_metal"><?php foreach ($divisions as $c => $l): ?><option value="<?php echo epc_erp_h($c); ?>"><?php echo epc_erp_h($l); ?></option><?php endforeach; ?></select>
				</div>
				<div class="ef-field"><label>Karat</label>
					<select id="pw_karat"><option value="">All</option><?php foreach ($karats as $k): ?><option value="<?php echo epc_erp_h($k['karat_code']); ?>"><?php echo epc_erp_h($k['karat_code']); ?></option><?php endforeach; ?></select>
				</div>
				<div class="ef-field"><label>Category</label><input id="pw_category" maxlength="30" placeholder="RING, CHAIN..."></div>
				<div class="ef-field"><label>Item Code</label><input id="pw_item_code" maxlength="20" placeholder="Search..."></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Type</label><input id="pw_type" maxlength="30"></div>
				<div class="ef-field"><label>Brand</label><input id="pw_brand" maxlength="60"></div>
				<div class="ef-field"><label>Vendor</label><input id="pw_vendor" maxlength="20"></div>
				<div class="ef-field">
					<label>&nbsp;</label>
					<button type="button" class="btn btn-primary btn-xs" onclick="jwPwSearch()"><i class="fa fa-search"></i> Search</button>
				</div>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Item Detail</span>
			<div class="ef-row">
				<div class="ef-field"><label>Item Code</label><input id="pw_sel_item" readonly></div>
				<div class="ef-field"><label>Description</label><input id="pw_sel_desc" readonly></div>
				<div class="ef-field"><label>Karat</label><input id="pw_sel_karat" readonly></div>
				<div class="ef-field"><label>Purity</label><input id="pw_sel_purity" readonly></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Pcs</label><input id="pw_pcs" type="number" value="1"></div>
				<div class="ef-field"><label>Gross Wt</label><input id="pw_gross_wt" type="number" step="0.001" value="0.000"></div>
				<div class="ef-field"><label>Rate Type</label><input id="pw_rate_type" maxlength="10" value="GMS"></div>
				<div class="ef-field"><label>Metal Rate</label><input id="pw_metal_rate" type="number" step="0.01" value="0.00"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>MC Type</label>
					<select id="pw_mc_type"><option value="FIX">FIX</option><option value="PCT">PCT</option><option value="PGM">PGM</option></select>
				</div>
				<div class="ef-field"><label>MC Rate</label><input id="pw_mc_rate" type="number" step="0.01" value="0.00"></div>
				<div class="ef-field"><label>MC Amount</label><input id="pw_mc_amount" type="number" step="0.01" value="0.00"></div>
				<div class="ef-field"><label>Amount</label><input id="pw_amount" type="number" step="0.01" value="0.00"></div>
			</div>
		</div>

		<table class="ef-grid" id="pw_results">
			<thead><tr>
				<th>No.</th><th>Item Code</th><th>Description</th><th>Karat</th>
				<th>Purity</th><th>Stock Pcs</th><th>Stock Gms</th><th>Cost</th>
			</tr></thead>
			<tbody>
				<tr><td colspan="8" style="text-align:center;color:#999">Use filters above to search items</td></tr>
			</tbody>
		</table>
	</div>
	<div class="ef-status">
		<span>Mode:=SEARCH</span>
		<span>Select item from results to populate detail</span>
	</div>
</div>
<script>
function jwPwSearch(){
	document.getElementById('pw_results').querySelector('tbody').innerHTML='<tr><td colspan="8" style="text-align:center"><i class="fa fa-spinner fa-spin"></i> Searching...</td></tr>';
}
</script>
