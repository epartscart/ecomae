<?php
/**
 * Integrations hub — Super CP + Tenant CP central listing of platform integrations.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	echo '<div class="alert alert-warning">Admin login required.</div>';
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$rows = epc_integrations_hub_rows($pdo, $isSuper);
$backend = epc_int_backend();
$marketLabel = 'United Arab Emirates';
if (!$isSuper) {
	$marketFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/tenant_hub/epc_tenant_country_profile.php';
	if (is_readable($marketFile)) {
		require_once $marketFile;
		$marketLabel = epc_tenant_country_market_label($pdo);
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-int-hub',
	'hero' => array(
		'badge' => $isSuper ? 'Super CP' : 'Tenant CP',
		'title' => 'Integrations hub',
		'sub' => 'Enable → configure → test. Each row links to settings and operator guide.',
		'actions' => $isSuper ? array(
			array('label' => 'Tenant features', 'icon' => 'fa-sliders', 'url' => '/' . $backend . '/control/portal/epc_tenant_features', 'primary' => true),
			array('label' => 'Mobile apps', 'icon' => 'fa-mobile-alt', 'url' => '/' . $backend . '/control/portal/epc_mobile_apps'),
		) : array(
			array('label' => 'Email / SMTP', 'icon' => 'fa-envelope', 'url' => '/' . $backend . '/control/portal/epc_tenant_email_settings'),
			array('label' => 'Mobile apps', 'icon' => 'fa-mobile-alt', 'url' => '/' . $backend . '/control/portal/epc_mobile_apps'),
		),
	),
));
?>

<?php if (!$isSuper) { ?>
<div class="alert alert-info" style="margin-bottom:14px">
	<i class="fa fa-globe"></i> <strong>Your market:</strong> <?php echo epc_int_h($marketLabel); ?>
	<span class="text-muted small"> — Tax, Auto Price AI discovery sources, and ERP defaults follow your registered country. Contact Super CP to request a change.</span>
</div>
<?php } ?>

<div class="panel panel-default">
	<div class="panel-heading"><strong><i class="fa fa-plug"></i> Platform integrations</strong></div>
	<div class="panel-body" style="padding:0">
		<table class="table table-hover" style="margin:0">
			<thead>
				<tr>
					<th>Integration</th>
					<th>Status</th>
					<th style="width:220px">Actions</th>
				</tr>
			</thead>
			<tbody>
			<?php foreach ($rows as $row) { ?>
				<tr>
					<td>
						<i class="fa <?php echo epc_int_h($row['icon']); ?>" style="color:<?php echo epc_int_h($row['color']); ?>"></i>
						<strong><?php echo epc_int_h($row['label']); ?></strong>
						<?php if (!empty($row['super_only'])) { ?>
						<span class="label label-default">Super CP config</span>
						<?php } ?>
					</td>
					<td>
						<?php if (!empty($row['active'])) { ?>
						<span class="label label-success">Active</span>
						<?php } else { ?>
						<span class="label label-default">Inactive</span>
						<?php } ?>
					</td>
					<td>
						<?php if (!empty($row['configure_url']) && (!$row['super_only'] || $isSuper)) { ?>
						<a class="btn btn-xs btn-primary" href="<?php echo epc_int_h($row['configure_url']); ?>"><i class="fa fa-cog"></i> Configure</a>
						<?php } elseif (!empty($row['super_only']) && !$isSuper) { ?>
						<span class="text-muted small">Configured on ecomae.com</span>
						<?php } ?>
					</td>
				</tr>
			<?php } ?>
			</tbody>
		</table>
	</div>
</div>

<div class="panel panel-default">
	<div class="panel-heading">
		<a class="showhide"><i class="fa fa-chevron-up"></i></a>
		<strong><i class="fa fa-book"></i> Activate in 3 steps</strong>
	</div>
	<div class="panel-body">
		<ol>
			<li><strong>Enable</strong> — Super CP operator toggles the feature per tenant under <a href="/<?php echo epc_int_h($backend); ?>/control/portal/epc_tenant_features">Tenant features</a>.</li>
			<li><strong>Configure</strong> — Open <em>Configure</em> for each integration (SMTP credentials, store URLs, payment keys, etc.).</li>
			<li><strong>Test</strong> — Use built-in test buttons (SMTP send, POS sale, OAuth login) before go-live.</li>
		</ol>
		<p class="text-muted small">Full guide: <code>docs/guides/INTEGRATIONS.md</code> in the repo.</p>
	</div>
</div>

<?php epc_cp_page_frame_close(); ?>
