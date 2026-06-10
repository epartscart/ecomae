<?php
/**
 * Super CP — POS overview (enable/disable, stats per tenant registry).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pos/epc_pos_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_control.php';

function epc_pos_manage_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
global $db_link, $DP_Config;

if (!isset($db_link) || !($db_link instanceof PDO)) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	$backend = epc_pos_manage_h((string) ($DP_Config->backend_dir ?? 'cp'));
	echo '<div class="alert alert-warning">Please <a href="/' . $backend . '/">log in to Super CP</a>.</div>';
	return;
}

$pdo = $db_link;
epc_pos_ensure_schema($pdo);
$settings = epc_pos_get_settings($pdo);
$stats = epc_pos_dashboard_stats($pdo);
$backend = epc_pos_manage_h((string) ($DP_Config->backend_dir ?? 'cp'));
$posUrl = '/' . $backend . '/shop/pos/terminal';
$ajaxUrl = '/' . $backend . '/content/shop/pos/ajax_pos_endpoint.php';

$registryTenants = array();
if ($isSuper && function_exists('epc_portal_tenant_control_list_all')) {
	epc_portal_tenant_control_ensure_schema($pdo);
	foreach (epc_portal_tenant_control_list_all($pdo) as $t) {
		if (empty($t['in_registry'])) {
			continue;
		}
		$registryTenants[] = array(
			'site_key' => (string) ($t['site_key'] ?? ''),
			'label' => (string) ($t['trade_name'] ?? $t['site_key'] ?? ''),
			'hostname' => (string) ($t['hostname'] ?? ''),
			'db_name' => (string) ($t['db_name'] ?? ''),
		);
	}
}
?>
<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-erp-shell',
	'hero' => array(
		'badge' => 'Super CP',
		'title' => 'POS overview',
		'sub' => 'Point of Sale is tenant-scoped — each commerce tenant gets POS Terminal in ERP Suite menu after setup.',
	),
));
?>

<div class="epc-erp-shell">
	<div class="hpanel">
		<div class="panel-body">
			<div class="epc-pos-kpi">
				<div class="k"><div class="l">Today sales</div><div class="v"><?php echo (int) $stats['today_sales']; ?></div></div>
				<div class="k"><div class="l">Today total</div><div class="v"><?php echo number_format((float) $stats['today_total'], 2); ?></div></div>
				<div class="k"><div class="l">Week sales</div><div class="v"><?php echo (int) $stats['week_sales']; ?></div></div>
				<div class="k"><div class="l">POS enabled</div><div class="v"><?php echo !empty($settings['pos_enabled']) ? 'Yes' : 'No'; ?></div></div>
			</div>

			<p>
				<a class="btn btn-primary" href="<?php echo epc_pos_manage_h($posUrl); ?>"><i class="fa fa-cash-register"></i> Open POS Terminal</a>
				<a class="btn btn-default" href="/epc-pos-setup-all.php?token=epartscart-deploy-2026&apply=1" target="_blank" rel="noopener"><i class="fa fa-cogs"></i> Run setup all tenants</a>
			</p>

			<form id="epc-pos-settings-form" class="well" style="max-width:640px;margin-top:16px" data-ajax-url="<?php echo epc_pos_manage_h($ajaxUrl); ?>">
				<h4 style="margin-top:0">Platform / tenant settings (this DB)</h4>
				<div class="checkbox"><label><input type="checkbox" name="pos_enabled" value="1" <?php echo !empty($settings['pos_enabled']) ? 'checked' : ''; ?>> POS enabled</label></div>
				<div class="form-group">
					<label>Register name</label>
					<input type="text" class="form-control" name="register_name" value="<?php echo epc_pos_manage_h($settings['register_name'] ?? 'Register 1'); ?>">
				</div>
				<div class="form-group">
					<label>Receipt header</label>
					<textarea class="form-control" name="receipt_header" rows="2"><?php echo epc_pos_manage_h($settings['receipt_header'] ?? ''); ?></textarea>
				</div>
				<div class="form-group">
					<label>Receipt footer</label>
					<input type="text" class="form-control" name="receipt_footer" value="<?php echo epc_pos_manage_h($settings['receipt_footer'] ?? 'Thank you for your purchase'); ?>">
				</div>
				<button type="submit" class="btn btn-success">Save settings</button>
				<span id="epc-pos-settings-msg" class="text-muted" style="margin-left:10px"></span>
			</form>

			<?php if ($isSuper && $registryTenants): ?>
			<h4 style="margin-top:24px">Tenant POS URLs</h4>
			<p class="text-muted">Run <code>epc-pos-setup-all.php?apply=1</code> once to register routes on every live tenant DB.</p>
			<div class="table-responsive">
				<table class="table table-striped table-condensed">
					<thead><tr><th>Tenant</th><th>Hostname</th><th>POS URL</th></tr></thead>
					<tbody>
					<?php foreach ($registryTenants as $t): ?>
					<?php $host = trim((string) $t['hostname']); if ($host === '') continue; ?>
					<tr>
						<td><?php echo epc_pos_manage_h($t['label']); ?></td>
						<td><code><?php echo epc_pos_manage_h($host); ?></code></td>
						<td><a href="https://<?php echo epc_pos_manage_h($host); ?>/cp/shop/pos/terminal" target="_blank" rel="noopener">/cp/shop/pos/terminal</a></td>
					</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>
			<?php endif; ?>

			<div class="alert alert-info" style="margin-top:20px">
				<strong>Operator workflow:</strong> Open register → enter opening float → search/scan products → optional customer → checkout (cash/card/split) → print receipt. Sales create ERP Sales Order + invoice + receipt voucher; inventory decrements when warehouse is configured.
			</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
