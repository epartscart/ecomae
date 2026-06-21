<?php
/**
 * Super CP — per-tenant feature enable/disable matrix.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!function_exists('epc_portal_is_super_cp_host') || !epc_portal_is_super_cp_host()) {
	echo '<div class="alert alert-warning">Tenant feature registry is available on Super CP only.</div>';
	return;
}
if (!DP_User::isAdmin()) {
	echo '<div class="alert alert-warning">Admin login required.</div>';
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}

epc_integrations_ensure_schema($pdo);
$tenants = function_exists('epc_portal_list_tenants') ? epc_portal_list_tenants($pdo) : array();
$catalog = epc_integrations_catalog();
$selectedKey = preg_replace('/[^a-z0-9_-]/', '', (string) ($_GET['site_key'] ?? ''));
if ($selectedKey === '' && $tenants) {
	$selectedKey = (string) ($tenants[0]['site_key'] ?? '');
}
$flags = epc_integrations_features_for_site($selectedKey, $pdo);
$backend = epc_int_backend();
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_integrations.php';

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-tenant-features',
	'hero' => array(
		'badge' => 'Super CP',
		'title' => 'Tenant feature registry',
		'sub' => 'Enable or disable platform features per site_key. Disabled features hide from tenant CP menu.',
		'actions' => array(
			array('label' => 'Integrations hub', 'icon' => 'fa-plug', 'url' => '/' . $backend . '/control/portal/epc_integrations_hub', 'primary' => true),
		),
	),
));
?>

<div id="epc-tf-flash" class="alert" style="display:none"></div>

<form id="epc-tf-form" method="get" class="form-inline" style="margin-bottom:16px">
	<label>Tenant</label>
	<select name="site_key" class="form-control input-sm" onchange="this.form.submit()">
		<?php foreach ($tenants as $t) {
			$sk = (string) ($t['site_key'] ?? '');
			?>
		<option value="<?php echo epc_int_h($sk); ?>"<?php echo $sk === $selectedKey ? ' selected' : ''; ?>>
			<?php echo epc_int_h($t['trade_name'] ?? $sk); ?> (<?php echo epc_int_h($sk); ?>)
		</option>
		<?php } ?>
	</select>
</form>

<form id="epc-tf-save" class="panel panel-default" data-ajax-url="<?php echo epc_int_h($ajaxUrl); ?>" data-site-key="<?php echo epc_int_h($selectedKey); ?>">
	<div class="panel-heading"><strong>Features for <?php echo epc_int_h($selectedKey); ?></strong></div>
	<div class="panel-body">
		<input type="hidden" name="action" value="save_feature_flags">
		<input type="hidden" name="site_key" value="<?php echo epc_int_h($selectedKey); ?>">
		<div class="row">
		<?php foreach ($catalog as $key => $meta) {
			if ($key === 'tenant_registry') {
				continue;
			}
			?>
			<div class="col-md-4 col-sm-6" style="margin-bottom:10px">
				<label class="checkbox" style="font-weight:normal">
					<input type="checkbox" name="features[<?php echo epc_int_h($key); ?>]" value="1" <?php echo !empty($flags[$key]) ? 'checked' : ''; ?>>
					<i class="fa <?php echo epc_int_h($meta['icon'] ?? 'fa-plug'); ?>"></i>
					<?php echo epc_int_h($meta['label']); ?>
				</label>
			</div>
		<?php } ?>
		</div>
		<button type="submit" class="btn btn-success"><i class="fa fa-save"></i> Save feature flags</button>
	</div>
</form>

<div class="panel panel-default">
	<div class="panel-heading"><strong><i class="fa fa-book"></i> How it works</strong></div>
	<div class="panel-body">
		<ol>
			<li>Select tenant → check features they should use.</li>
			<li>Save — tenant CP sidebar hides menu items for disabled features.</li>
			<li>Tenant admin opens <strong>Integrations hub</strong> to configure credentials for enabled features.</li>
		</ol>
	</div>
</div>

<?php epc_cp_page_frame_close(); ?>
