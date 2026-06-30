<?php
/**
 * Shortcut Icon Builder — per-user customizable quick-access shortcuts.
 * Users pin favourite ERP modules/tabs to their dashboard for one-click access.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-th-large"></i> Shortcut Icons',
	'Build your personal ERP dashboard shortcuts — pin frequently used modules, reports, and actions for one-click access.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Shortcut Icons'),
	),
	array(array('label' => 'Add shortcut', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-star"></i> My shortcuts</h4>
	<p class="text-muted">Drag to reorder. Click the × to remove. Each user has their own shortcut layout.</p>
	<div class="row" id="sc_grid" style="margin-top:12px;"></div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus-circle"></i> Add new shortcut</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Module / Tab</label>
			<select class="form-control input-sm" id="sc_module">
				<option value="">— Select module —</option>
				<optgroup label="Finance">
					<option>General Ledger</option><option>Trial Balance</option><option>Profit &amp; Loss</option>
					<option>Balance Sheet</option><option>Journal Entry</option><option>Bank Reconciliation</option>
				</optgroup>
				<optgroup label="Sales">
					<option>Sales Invoice</option><option>Sales Order</option><option>Quotation</option>
					<option>Credit Note</option><option>Customer Statement</option>
				</optgroup>
				<optgroup label="Purchase">
					<option>Purchase Order</option><option>Purchase Invoice</option><option>GRN</option>
					<option>Supplier Payment</option>
				</optgroup>
				<optgroup label="Inventory">
					<option>Stock Movement</option><option>Stock Count</option><option>Barcode</option>
					<option>Transfer</option>
				</optgroup>
				<optgroup label="Reports">
					<option>Aging Report</option><option>VAT Return</option><option>Sales Analysis</option>
					<option>Inventory Valuation</option>
				</optgroup>
			</select>
		</div>
		<div class="pm-field"><label>Custom label (optional)</label><input type="text" class="form-control input-sm" placeholder="e.g. Daily Sales"></div>
		<div class="pm-field"><label>Icon</label>
			<select class="form-control input-sm" id="sc_icon">
				<option value="fa-file-text">📄 Document</option>
				<option value="fa-calculator">🧮 Calculator</option>
				<option value="fa-bar-chart">📊 Chart</option>
				<option value="fa-money">💰 Money</option>
				<option value="fa-truck">🚚 Delivery</option>
				<option value="fa-users">👥 People</option>
				<option value="fa-diamond">💎 Diamond</option>
				<option value="fa-shopping-cart">🛒 Cart</option>
			</select>
		</div>
		<div class="pm-field"><label>Color</label><input type="color" class="form-control input-sm" value="#3b82f6" style="height:30px;padding:2px;"></div>
		<div class="pm-field"><label>&nbsp;</label><button class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Add shortcut</button></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Shortcut settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Grid size</label>
			<select class="form-control input-sm"><option>Large (4 per row)</option><option>Medium (6 per row)</option><option>Small (8 per row)</option></select>
		</div>
		<div class="pm-field"><label>Show on dashboard</label>
			<select class="form-control input-sm"><option value="1">Yes — show shortcuts on ERP dashboard</option><option value="0">No — only this page</option></select>
		</div>
		<div class="pm-field"><label>Reset to defaults</label>
			<button class="btn btn-danger btn-xs"><i class="fa fa-undo"></i> Reset all shortcuts</button>
		</div>
	</div>
</div>
<script>
(function(){
	var shortcuts=[
		{label:'Sales Invoice',icon:'fa-file-text',color:'#3b82f6'},
		{label:'Journal Entry',icon:'fa-pencil-square',color:'#8b5cf6'},
		{label:'Trial Balance',icon:'fa-bar-chart',color:'#059669'},
		{label:'Aging Report',icon:'fa-clock-o',color:'#dc2626'},
		{label:'Inventory',icon:'fa-cubes',color:'#d97706'},
		{label:'Bank Recon',icon:'fa-university',color:'#0891b2'},
		{label:'Purchase Order',icon:'fa-shopping-cart',color:'#7c3aed'},
		{label:'VAT Return',icon:'fa-calculator',color:'#be185d'},
	];
	var grid=document.getElementById('sc_grid');
	shortcuts.forEach(function(s){
		grid.innerHTML+='<div class="col-md-3 col-sm-4 col-xs-6" style="margin-bottom:12px;"><div class="panel panel-default text-center" style="cursor:pointer;border-top:3px solid '+s.color+';"><div class="panel-body" style="padding:16px 8px;"><i class="fa '+s.icon+' fa-2x" style="color:'+s.color+';margin-bottom:8px;display:block;"></i><strong style="font-size:12px;">'+s.label+'</strong><a class="text-danger pull-right" style="position:absolute;top:4px;right:8px;font-size:14px;">&times;</a></div></div></div>';
	});
})();
</script>
<?php
erp_section_card('Shortcut Icons', ob_get_clean(), array('icon' => 'fa-th-large'));
