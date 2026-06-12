<?php
/**
 * Module: Landed Cost.
 * Sub-modules: Expense→account mapping, Allocation by value/qty, Warehouse
 * load, Account linking, Goods receipt.
 *
 * The allocator mirrors epc_scm_landed_cost_allocate() (value / qty basis) so
 * users can preview how freight/duty/insurance load onto each item before
 * posting the goods receipt.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-ship"></i> Landed Cost</h3>';
echo '<p class="text-muted">Allocate freight, duty, insurance and other charges across received items (by value or quantity), see the per-unit landed add-on, then load to the warehouse via goods receipt. Per-tenant and configurable.</p></div>';
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-calculator"></i> Landed cost allocation</h4>
	<div class="pm-fields" style="margin-bottom:12px;">
		<div class="pm-field"><label>Allocation basis</label>
			<select id="lc_basis" class="form-control input-sm"><option value="value">By value</option><option value="qty">By quantity</option></select>
		</div>
		<div class="pm-field"><label>Freight</label><input type="number" step="any" id="lc_freight" class="form-control input-sm" value="1200"></div>
		<div class="pm-field"><label>Duty</label><input type="number" step="any" id="lc_duty" class="form-control input-sm" value="800"></div>
		<div class="pm-field"><label>Insurance</label><input type="number" step="any" id="lc_ins" class="form-control input-sm" value="300"></div>
		<div class="pm-field"><label>Other</label><input type="number" step="any" id="lc_other" class="form-control input-sm" value="0"></div>
	</div>
	<table class="table table-bordered table-condensed" id="lc_table" style="font-size:13px;">
		<thead><tr><th>Item</th><th>Qty</th><th>Unit cost</th><th>Base value</th><th>Allocated charge</th><th>Per-unit add-on</th><th>Landed unit cost</th></tr></thead>
		<tbody></tbody>
		<tfoot><tr><th>Total</th><th id="lc_tqty">0</th><th></th><th id="lc_tval">0</th><th id="lc_talloc">0</th><th></th><th></th></tr></tfoot>
	</table>
	<button type="button" class="btn btn-default btn-sm" id="lc_addrow"><i class="fa fa-plus"></i> Add item line</button>
	<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'inventory', $date_from_str, $date_to_str, 'operations')); ?>"><i class="fa fa-cubes"></i> Goods receipt → warehouse load</a>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-link"></i> Expense → account mapping</h4>
	<p class="text-muted">Each charge type posts to a GL account; configure these accounts under AR/AP setup &amp; the chart of accounts.</p>
	<table class="table table-bordered table-condensed" style="max-width:560px;">
		<thead><tr><th>Charge</th><th>Default GL account</th><th>Allocation</th></tr></thead>
		<tbody>
			<tr><td>Freight</td><td>5210 · Freight inwards</td><td>By value / qty</td></tr>
			<tr><td>Duty</td><td>5220 · Customs duty</td><td>By value / qty</td></tr>
			<tr><td>Insurance</td><td>5230 · Insurance</td><td>By value</td></tr>
			<tr><td>Other</td><td>5240 · Other landed</td><td>By value / qty</td></tr>
		</tbody>
	</table>
	<a class="btn btn-default btn-sm" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'coa', $date_from_str, $date_to_str, 'finance')); ?>"><i class="fa fa-list"></i> Chart of accounts</a>
</div>

<script>
(function(){
	var seed=[{n:'Item A',q:100,c:50},{n:'Item B',q:40,c:120},{n:'Item C',q:25,c:300}];
	var tb=document.querySelector('#lc_table tbody');
	function row(d){
		var tr=document.createElement('tr');
		tr.innerHTML='<td><input class="form-control input-sm lc-n" value="'+(d.n||'')+'"></td>'+
			'<td><input type="number" step="any" class="form-control input-sm lc-q" value="'+(d.q||0)+'" style="width:90px"></td>'+
			'<td><input type="number" step="any" class="form-control input-sm lc-c" value="'+(d.c||0)+'" style="width:100px"></td>'+
			'<td class="lc-val">0</td><td class="lc-alloc">0</td><td class="lc-addon">0</td><td class="lc-landed">0</td>';
		tb.appendChild(tr);
	}
	seed.forEach(row);
	function num(id){return parseFloat(document.getElementById(id).value)||0;}
	function calc(){
		var basis=document.getElementById('lc_basis').value;
		var extra=num('lc_freight')+num('lc_duty')+num('lc_ins')+num('lc_other');
		var rows=[].slice.call(tb.querySelectorAll('tr'));
		var tw=0, data=[];
		rows.forEach(function(tr){
			var q=parseFloat(tr.querySelector('.lc-q').value)||0;
			var c=parseFloat(tr.querySelector('.lc-c').value)||0;
			var val=q*c; var w=(basis==='qty')?q:val;
			tw+=w; data.push({tr:tr,q:q,c:c,val:val,w:w});
		});
		var done=0, tqty=0, tval=0, talloc=0;
		data.forEach(function(d,i){
			var alloc=0;
			if(tw>0){ alloc=(i===data.length-1)?(extra-done):Math.round(extra*(d.w/tw)*100)/100; if(i!==data.length-1)done+=alloc; }
			alloc=Math.round(alloc*100)/100;
			var addon=d.q>0?Math.round((alloc/d.q)*10000)/10000:0;
			var landed=Math.round((d.c+addon)*10000)/10000;
			d.tr.querySelector('.lc-val').textContent=d.val.toFixed(2);
			d.tr.querySelector('.lc-alloc').textContent=alloc.toFixed(2);
			d.tr.querySelector('.lc-addon').textContent=addon.toFixed(4);
			d.tr.querySelector('.lc-landed').textContent=landed.toFixed(4);
			tqty+=d.q; tval+=d.val; talloc+=alloc;
		});
		document.getElementById('lc_tqty').textContent=tqty.toFixed(2);
		document.getElementById('lc_tval').textContent=tval.toFixed(2);
		document.getElementById('lc_talloc').textContent=talloc.toFixed(2);
	}
	document.getElementById('lc_addrow').addEventListener('click',function(){row({n:'New item',q:1,c:0});calc();});
	['lc_basis','lc_freight','lc_duty','lc_ins','lc_other'].forEach(function(id){document.getElementById(id).addEventListener('input',calc);});
	tb.addEventListener('input',calc);
	calc();
})();
</script>
<?php
