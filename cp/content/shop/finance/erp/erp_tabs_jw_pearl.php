<?php
/**
 * Jewellery ERP — Pearl Master.
 * Ref: Suntech Pearl Master screenshot.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$pearls = epc_jewel_pearl_list($db_link, $companyId);

erp_page_header('<i class="fa fa-circle-o"></i> Pearl Master', 'Pearl items with grading, pricing and stock.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Pearl master'),
));
?>
<div class="ef-window">
	<div class="ef-title">Pearl Master</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="document.getElementById('jw_pearl_form').style.display='block'"><i class="fa fa-plus"></i> New</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-pencil"></i> Edit</button>
		<button class="btn btn-default btn-xs" disabled><i class="fa fa-trash"></i> Delete</button>
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<table class="ef-grid">
			<thead><tr>
				<th>No.</th><th>Item Code</th><th>Description</th><th>Type</th>
				<th>Shape</th><th>Color</th><th>Size</th><th>Grade</th><th>Cost</th>
			</tr></thead>
			<tbody>
			<?php if (empty($pearls)): ?>
				<tr><td colspan="9" style="text-align:center;color:#999">No records</td></tr>
			<?php else: $n=1; foreach ($pearls as $p): ?>
				<tr class="ef-grid-row" data-id="<?php echo (int)$p['id']; ?>"
					onclick="jwPearlSelect(this)" style="cursor:pointer">
					<td><?php echo $n++; ?></td>
					<td><strong><?php echo epc_erp_h($p['item_code']); ?></strong></td>
					<td><?php echo epc_erp_h($p['description']); ?></td>
					<td><?php echo epc_erp_h($p['pearl_type']); ?></td>
					<td><?php echo epc_erp_h($p['shape']); ?></td>
					<td><?php echo epc_erp_h($p['color']); ?></td>
					<td><?php echo epc_erp_h($p['size']); ?></td>
					<td><?php echo epc_erp_h($p['grade']); ?></td>
					<td style="text-align:right"><?php echo number_format((float)$p['cost_amount'], 2); ?></td>
				</tr>
			<?php endforeach; endif; ?>
			</tbody>
		</table>

		<div id="jw_pearl_form" style="display:none;margin-top:12px;">
			<form method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>">
			<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
			<input type="hidden" name="action" value="jw_pearl_save">

			<div class="ef-section">
				<span class="ef-section-title">Pearl Identification</span>
				<div class="ef-row">
					<div class="ef-field"><label>Item Code</label><input name="item_code" maxlength="20" required></div>
					<div class="ef-field ef-field-wide"><label>Description</label><input name="description" maxlength="120"></div>
					<div class="ef-field"><label>Currency</label><input name="currency" maxlength="5" value="AED"></div>
					<div class="ef-field"><label>Currency Rate</label><input name="currency_rate" type="number" step="0.00001" value="1.00000"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Pearl Type</label>
						<select name="pearl_type"><option value="Natural">Natural</option><option value="Cultured">Cultured</option><option value="Fresh Water">Fresh Water</option><option value="South Sea">South Sea</option><option value="Tahitian">Tahitian</option></select>
					</div>
					<div class="ef-field"><label>Shape</label>
						<select name="shape"><option value="Round">Round</option><option value="Button">Button</option><option value="Drop">Drop</option><option value="Baroque">Baroque</option><option value="Oval">Oval</option><option value="Semi Round">Semi Round</option></select>
					</div>
					<div class="ef-field"><label>Color</label><input name="color" maxlength="20" placeholder="White"></div>
					<div class="ef-field"><label>Size</label><input name="size" maxlength="20" placeholder="6-7mm"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Grade</label>
						<select name="grade"><option value="AAA">AAA</option><option value="AA+">AA+</option><option value="AA">AA</option><option value="A+">A+</option><option value="A">A</option><option value="B">B</option></select>
					</div>
					<div class="ef-field"><label>Lustre</label>
						<select name="lustre"><option value="Excellent">Excellent</option><option value="Very Good">Very Good</option><option value="Good">Good</option><option value="Fair">Fair</option></select>
					</div>
					<div class="ef-field"><label>Surface</label>
						<select name="surface"><option value="Clean">Clean</option><option value="Slightly Spotted">Slightly Spotted</option><option value="Spotted">Spotted</option></select>
					</div>
					<div class="ef-field"><label>Nacre</label>
						<select name="nacre"><option value="Thick">Thick</option><option value="Medium">Medium</option><option value="Thin">Thin</option></select>
					</div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Cost Centre</label><input name="cost_centre" maxlength="20"></div>
					<div class="ef-field"><label>Category</label><input name="category" maxlength="30"></div>
					<div class="ef-field"><label>Vendor</label><input name="vendor" maxlength="20"></div>
					<div class="ef-field"><label>Vendor Ref</label><input name="vendor_ref" maxlength="20"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Pricing</span>
				<div class="ef-row">
					<div class="ef-field"><label>Cost Amount</label><input name="cost_amount" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 1 Code</label><input name="price1_code" maxlength="5" value="GEN"></div>
					<div class="ef-field"><label>Price 1 %</label><input name="price1_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 1 FC</label><input name="price1_fc" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 1 LC</label><input name="price1_lc" type="number" step="0.01" value="0.00"></div>
				</div>
				<div class="ef-row">
					<div class="ef-field"><label>Price 2 Code</label><input name="price2_code" maxlength="5"></div>
					<div class="ef-field"><label>Price 2 %</label><input name="price2_pct" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 2 FC</label><input name="price2_fc" type="number" step="0.01" value="0.00"></div>
					<div class="ef-field"><label>Price 2 LC</label><input name="price2_lc" type="number" step="0.01" value="0.00"></div>
				</div>
			</div>

			<div class="ef-section">
				<span class="ef-section-title">Weight &amp; Stock</span>
				<div class="ef-row">
					<div class="ef-field"><label>Carat</label><input name="carat" type="number" step="0.0001" value="0.0000"></div>
					<div class="ef-field"><label>Pcs</label><input name="pcs" type="number" value="0"></div>
					<div class="ef-field"><label>Weight (Gms)</label><input name="weight_gms" type="number" step="0.0001" value="0.0000"></div>
				</div>
			</div>

			<div class="ef-actions">
				<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save</button>
				<button type="button" class="btn btn-default btn-sm" onclick="document.getElementById('jw_pearl_form').style.display='none'">Cancel</button>
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
function jwPearlSelect(row){
	document.querySelectorAll('.ef-grid-row').forEach(function(r){r.style.background='';});
	row.style.background='#d0e8ff';
}
</script>
