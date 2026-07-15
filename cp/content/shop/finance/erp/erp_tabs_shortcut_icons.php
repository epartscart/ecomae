<?php
/**
 * Shortcut Icon Builder — per-user customizable quick-access shortcuts.
 * Users pin favourite ERP modules/tabs to their dashboard for one-click access.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_shortcut_icons.php';
epc_erp_pm_inline_assets();

epc_shortcuts_ensure_schema($db_link);
$csrfLocal = isset($csrf) ? $csrf : '';
// Resolve current user ID — same convention used in ajax_erp.php
$scUserId = 0;
if (isset($_SESSION['user_id'])) { $scUserId = (int) $_SESSION['user_id']; }
elseif (isset($_SESSION['admin_id'])) { $scUserId = (int) $_SESSION['admin_id']; }
$shortcuts = epc_shortcuts_list($db_link, $scUserId);

erp_page_header(
	'<i class="fa fa-th-large"></i> Shortcut Icons',
	'Build your personal ERP dashboard shortcuts — pin frequently used modules, reports, and actions for one-click access.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Shortcut Icons'),
	),
	array(array('label' => 'Add shortcut', 'id' => 'sc_add_btn', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-star"></i> My shortcuts</h4>
	<p class="text-muted">Click × to remove a shortcut. Drag-to-reorder is a planned follow-up — not yet wired.</p>
	<div class="row" id="sc_grid" style="margin-top:12px;">
	<?php if (empty($shortcuts)): ?>
		<div class="col-md-12"><p class="text-muted" style="text-align:center;">No shortcuts yet. Use the form below to add your first shortcut.</p></div>
	<?php else: foreach ($shortcuts as $sc): ?>
		<div class="col-md-3 col-sm-4 col-xs-6" style="margin-bottom:12px;position:relative;">
			<div class="panel panel-default text-center" style="cursor:pointer;border-top:3px solid <?php echo epc_erp_h($sc['icon_color']); ?>;">
				<div class="panel-body" style="padding:16px 8px;">
					<i class="<?php echo epc_erp_h($sc['icon_class']); ?> fa-2x" style="color:<?php echo epc_erp_h($sc['icon_color']); ?>;margin-bottom:8px;display:block;"></i>
					<strong style="font-size:12px;"><?php echo epc_erp_h($sc['label']); ?></strong>
					<?php if ($sc['target_url']): ?>
						<a href="<?php echo epc_erp_h($sc['target_url']); ?>" class="stretched-link" style="position:absolute;inset:0;"></a>
					<?php endif; ?>
					<a class="text-danger sc-delete" data-id="<?php echo (int) $sc['id']; ?>" style="position:absolute;top:4px;right:8px;font-size:14px;cursor:pointer;z-index:10;">&times;</a>
				</div>
			</div>
		</div>
	<?php endforeach; endif; ?>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus-circle"></i> Add new shortcut</h4>
	<form id="sc_new_form" method="POST" action="<?php echo epc_erp_h($erpAjaxUrl); ?>" style="display:none;margin-bottom:16px;">
		<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
		<input type="hidden" name="action" value="shortcut_add">
		<div class="pm-fields">
			<div class="pm-field"><label>Label</label><input type="text" name="label" class="form-control input-sm" required placeholder="e.g. Daily Sales"></div>
			<div class="pm-field"><label>Target URL</label><input type="text" name="target_url" class="form-control input-sm" placeholder="/cp/..."></div>
			<div class="pm-field"><label>ERP tab name (optional)</label><input type="text" name="target_tab" class="form-control input-sm" placeholder="e.g. invoices"></div>
			<div class="pm-field"><label>Icon class</label>
				<select name="icon_class" class="form-control input-sm">
					<option value="fa fa-file-text">📄 Document</option>
					<option value="fa fa-calculator">🧮 Calculator</option>
					<option value="fa fa-bar-chart">📊 Chart</option>
					<option value="fa fa-money">💰 Money</option>
					<option value="fa fa-truck">🚚 Delivery</option>
					<option value="fa fa-users">👥 People</option>
					<option value="fa fa-diamond">💎 Diamond</option>
					<option value="fa fa-shopping-cart">🛒 Cart</option>
					<option value="fa fa-star">⭐ Star</option>
					<option value="fa fa-cubes">📦 Inventory</option>
					<option value="fa fa-clock-o">🕐 Clock</option>
					<option value="fa fa-university">🏦 Bank</option>
				</select>
			</div>
			<div class="pm-field"><label>Color</label><input type="color" name="icon_color" class="form-control input-sm" value="#3b82f6" style="height:30px;padding:2px;"></div>
		</div>
		<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-plus"></i> Add shortcut</button>
	</form>
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
	var endpoint = <?php echo json_encode($erpAjaxUrl); ?>;
	var csrf = <?php echo json_encode($csrfLocal); ?>;

	// Toggle add form
	var addBtn = document.getElementById('sc_add_btn');
	if (addBtn) {
		addBtn.addEventListener('click', function () {
			var f = document.getElementById('sc_new_form');
			if (f) { f.style.display = f.style.display === 'none' ? 'block' : 'none'; }
		});
	}

	// Submit add form
	var form = document.getElementById('sc_new_form');
	if (form) {
		form.addEventListener('submit', function (e) {
			e.preventDefault();
			var fd = new FormData(form);
			fetch(endpoint, { method: 'POST', body: fd, credentials: 'same-origin' })
				.then(function (r) { return r.json(); })
				.then(function () { location.reload(); });
		});
	}

	// Delete shortcut
	document.querySelectorAll('.sc-delete').forEach(function (btn) {
		btn.addEventListener('click', function (e) {
			e.preventDefault();
			e.stopPropagation();
			if (!window.confirm('Remove this shortcut?')) { return; }
			var fd = new FormData();
			fd.append('action', 'shortcut_delete');
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
erp_section_card('Shortcut Icons', ob_get_clean(), array('icon' => 'fa-th-large'));
