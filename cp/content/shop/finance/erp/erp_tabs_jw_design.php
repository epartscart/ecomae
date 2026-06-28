<?php
/**
 * Jewellery ERP — Design Master.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-paint-brush"></i> Design Master', 'Jewellery designs with embedded metal & stone composition.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Design master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Design Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_des_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
	</div>
	<div class="ef-body">
		<div id="jw_des_form" style="display:block;">
		<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="jw_design_save">

		<div class="ef-section">
			<div class="ef-row">
				<div class="ef-field"><label>Design Code</label><input name="design_code" required></div>
				<div class="ef-field ef-field-wide"><label>Description</label><input name="description"></div>
				<div class="ef-field"><label>Category</label><input name="category"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Type</label><input name="type"></div>
				<div class="ef-field"><label>Collection</label><input name="collection"></div>
				<div class="ef-field"><label>Style</label><input name="style"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Making Rate/Gm</label><input name="making_rate" type="number" step="0.01" value="0"></div>
				<div class="ef-field"><label>Std Gross Wt</label><input name="std_gross_wt" type="number" step="0.001" value="0"></div>
				<div class="ef-field"><label>Std Net Wt</label><input name="std_net_wt" type="number" step="0.001" value="0"></div>
			</div>
		</div>

		<div class="ef-tabs">
			<ul class="nav nav-tabs" role="tablist">
				<li class="active"><a href="#des_metals" data-toggle="tab">1. Metals</a></li>
				<li><a href="#des_stones" data-toggle="tab">2. Stones</a></li>
			</ul>
			<div class="tab-content">
				<div class="tab-pane active" id="des_metals">
					<table class="ef-grid">
						<thead><tr><th>Metal</th><th>Karat</th><th>Gross Wt</th><th>Net Wt</th><th>Purity</th><th>Pure Wt</th></tr></thead>
						<tbody>
							<tr>
								<td><select name="metals[0][metal]"><option value="G">G</option><option value="S">S</option><option value="T">T</option></select></td>
								<td><input name="metals[0][karat]" style="width:40px"></td>
								<td><input name="metals[0][gross_wt]" type="number" step="0.001" value="0"></td>
								<td><input name="metals[0][net_wt]" type="number" step="0.001" value="0"></td>
								<td><input name="metals[0][purity]" type="number" step="0.000001" value="0.917000"></td>
								<td><input name="metals[0][pure_wt]" type="number" step="0.001" value="0" class="ef-readonly" readonly></td>
							</tr>
						</tbody>
					</table>
				</div>
				<div class="tab-pane" id="des_stones">
					<table class="ef-grid">
						<thead><tr><th>Stone</th><th>Shape</th><th>Size</th><th>Pcs</th><th>Carat</th><th>Rate</th><th>Amount</th></tr></thead>
						<tbody>
							<tr>
								<td><input name="stones[0][stone]"></td>
								<td><input name="stones[0][shape]" style="width:50px"></td>
								<td><input name="stones[0][size]" style="width:40px"></td>
								<td><input name="stones[0][pcs]" type="number" value="0" style="width:40px"></td>
								<td><input name="stones[0][carat]" type="number" step="0.001" value="0"></td>
								<td><input name="stones[0][rate]" type="number" step="0.01" value="0"></td>
								<td><input name="stones[0][amount]" type="number" step="0.01" value="0" class="ef-readonly" readonly></td>
							</tr>
						</tbody>
					</table>
				</div>
			</div>
		</div>

		<div class="ef-actions">
			<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
		</div>
		</form>
		</div>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Header New Record → Function Key (F5)</span></div>
</div>
