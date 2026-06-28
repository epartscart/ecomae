<?php
/**
 * Jewellery ERP — Barcode Generation & Print.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
include __DIR__ . '/erp_entry_form_css.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';

erp_page_header('<i class="fa fa-barcode"></i> Barcode Generation', 'Generate and print barcodes for jewellery items.', array(
	array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
	array('label' => 'Jewellery', 'url' => epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str)),
	array('label' => 'Barcode'),
));
?>
<div class="ef-window">
	<div class="ef-title">Barcode Generation</div>
	<div class="ef-toolbar">
		<button class="btn btn-primary btn-xs"><i class="fa fa-plus"></i> Generate</button>
		<button class="btn btn-default btn-xs"><i class="fa fa-print"></i> Print Selected</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title">Generation Options</span>
			<div class="ef-row">
				<div class="ef-field"><label>Item Code</label><input name="item_code" placeholder="Select item"></div>
				<div class="ef-field"><label>Quantity</label><input name="qty" type="number" value="1" style="width:50px"></div>
				<div class="ef-field"><label>Prefix</label><input name="prefix" placeholder="GLD" style="width:60px"></div>
			</div>
			<div class="ef-row">
				<div class="ef-field"><label>Label Size</label><select name="label_size"><option value="small">Small (25x15mm)</option><option value="medium" selected>Medium (40x20mm)</option><option value="large">Large (50x30mm)</option></select></div>
				<div class="ef-field"><label>Include</label></div>
			</div>
			<div class="ef-checks">
				<label><input type="checkbox" name="show_price" value="1" checked> Show Price</label>
				<label><input type="checkbox" name="show_karat" value="1" checked> Show Karat</label>
				<label><input type="checkbox" name="show_weight" value="1" checked> Show Weight</label>
				<label><input type="checkbox" name="show_making" value="1"> Show Making</label>
			</div>
		</div>

		<div class="ef-section">
			<span class="ef-section-title">Barcode Preview</span>
			<div style="display:flex;flex-wrap:wrap;gap:10px;padding:10px;">
				<div style="border:1px dashed #ccc;padding:10px;text-align:center;min-width:150px;">
					<div style="font-size:10px;color:#666">Sample barcode label</div>
					<div style="font-family:monospace;font-size:18px;letter-spacing:3px;margin:6px 0">|||||||||||||||</div>
					<div style="font-size:10px">GLD-000001</div>
					<div style="font-size:9px;color:#666">22K | 5.250g | AED 1,250</div>
				</div>
			</div>
		</div>

		<table class="ef-grid">
			<thead><tr><th><input type="checkbox"></th><th>Barcode</th><th>Item Code</th><th>Description</th><th>Metal</th><th>Karat</th><th>Weight</th><th>Generated</th></tr></thead>
			<tbody>
				<tr><td colspan="8" style="text-align:center;color:#999;padding:20px">No barcodes generated. Use the form above to generate.</td></tr>
			</tbody>
		</table>
	</div>
	<div class="ef-status"><span>Mode:=VIEW</span><span>Barcode Generation</span></div>
</div>
