<?php
/**
 * Customer Groups / Types — classify customers for segmented reporting, pricing tiers, and credit policies.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_customer_groups.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';
epc_erp_pm_inline_assets();

epc_cust_groups_ensure_schema($db_link);
$cgCompanyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;
$csrfLocal = isset($csrf) ? $csrf : '';
$groups = epc_cust_groups_list($db_link, $cgCompanyId);

erp_page_header(
	'<i class="fa fa-users"></i> Customer Groups',
	'Classify customers by group/type for reporting, pricing tiers, credit policies, and marketing segmentation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Customer Groups'),
	),
	array(array('label' => 'New group', 'id' => 'cg_add', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-th-large"></i> Customer group master</h4>
	<p class="text-muted">Define groups to segment customers — reports, statements, aging, and promotions can all be filtered by group.</p>
	<form id="cg_new_form" method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:none;margin-bottom:16px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="cust_group_save">
		<div class="pm-fields">
			<div class="pm-field"><label>Code</label><input type="text" name="group_code" class="form-control input-sm" required></div>
			<div class="pm-field"><label>Group name</label><input type="text" name="group_name" class="form-control input-sm" required></div>
			<div class="pm-field"><label>Type</label><select name="group_type" class="form-control input-sm">
				<option value="general">General</option><option value="vip">VIP</option><option value="wholesale">Wholesale</option>
				<option value="retail">Retail</option><option value="corporate">Corporate</option><option value="government">Government</option>
			</select></div>
			<div class="pm-field"><label>Credit limit</label><input type="number" step="any" name="credit_limit" class="form-control input-sm" value="0"></div>
			<div class="pm-field"><label>Payment terms (days)</label><input type="number" name="payment_terms_days" class="form-control input-sm" value="30"></div>
			<div class="pm-field"><label>Discount %</label><input type="number" step="any" name="discount_pct" class="form-control input-sm" value="0"></div>
		</div>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-save"></i> Save group</button>
	</form>
	<table class="table table-bordered table-condensed" id="cg_table" style="font-size:13px;">
		<thead><tr><th>Code</th><th>Group name</th><th>Type</th><th>Credit limit</th><th>Payment terms</th><th>Discount %</th><th>Customers</th><th>Actions</th></tr></thead>
		<tbody id="cg_tbody">
		<?php if (empty($groups)): ?>
			<tr><td colspan="8" style="text-align:center;color:#999">No customer groups yet</td></tr>
		<?php else: foreach ($groups as $g): ?>
			<tr>
				<td><code><?php echo epc_erp_h($g['group_code']); ?></code></td>
				<td><strong><?php echo epc_erp_h($g['group_name']); ?></strong></td>
				<td><span class="label label-info"><?php echo epc_erp_h($g['group_type']); ?></span></td>
				<td><?php echo number_format((float) $g['credit_limit'], 2); ?></td>
				<td>Net <?php echo (int) $g['payment_terms_days']; ?></td>
				<td><?php echo number_format((float) $g['discount_pct'], 2); ?>%</td>
				<td><span class="badge"><?php echo (int) $g['member_count']; ?></span></td>
				<td><a class="btn btn-xs btn-danger cg-delete" data-id="<?php echo (int) $g['id']; ?>"><i class="fa fa-trash"></i></a></td>
			</tr>
		<?php endforeach; endif; ?>
		</tbody>
	</table>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-bar-chart"></i> Group-level reporting</h4>
	<p class="text-muted">Generate reports filtered by customer group — revenue by group, aging by group, top products by group.</p>
	<div class="row">
		<div class="col-md-4">
			<div class="panel panel-default"><div class="panel-body text-center">
				<i class="fa fa-pie-chart fa-2x text-primary"></i>
				<h5>Revenue by group</h5>
				<p class="text-muted small">Monthly/yearly breakdown per group</p>
			</div></div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default"><div class="panel-body text-center">
				<i class="fa fa-clock-o fa-2x text-warning"></i>
				<h5>Aging by group</h5>
				<p class="text-muted small">Outstanding receivables grouped</p>
			</div></div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default"><div class="panel-body text-center">
				<i class="fa fa-trophy fa-2x text-success"></i>
				<h5>Top products by group</h5>
				<p class="text-muted small">Best sellers per segment</p>
			</div></div>
		</div>
	</div>
</div>

<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Group settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Default group for new customers</label>
			<select class="form-control input-sm"><option>General</option><option>VIP</option><option>Wholesale</option></select>
		</div>
		<div class="pm-field"><label>Auto-upgrade rule</label>
			<select class="form-control input-sm"><option value="0">Manual only</option><option value="1">Auto-upgrade on revenue threshold</option></select>
		</div>
		<div class="pm-field"><label>Revenue threshold for upgrade</label>
			<input type="number" class="form-control input-sm" value="50000">
		</div>
	</div>
</div>

<script>
(function(){
	var endpoint = <?php echo json_encode($erpAjaxUrl); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;
	var addBtn = document.getElementById('cg_add');
	if (addBtn) {
		addBtn.addEventListener('click', function () {
			var f = document.getElementById('cg_new_form');
			if (f) { f.style.display = f.style.display === 'none' ? 'block' : 'none'; }
		});
	}
	document.querySelectorAll('.cg-delete').forEach(function (btn) {
		btn.addEventListener('click', function () {
			if (!window.confirm('Delete this customer group? Members will be unassigned.')) { return; }
			var fd = new FormData();
			fd.append('action', 'cust_group_delete');
			fd.append('csrf_guard_key', csrf);
			fd.append('id', btn.getAttribute('data-id'));
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function () { location.reload(); });
		});
	});
})();
</script>
<?php
erp_section_card('Customer Groups', ob_get_clean(), array('icon' => 'fa-users'));
