<?php
/**
 * CP Industry Kit Panel — shows the tenant's ERP kit configuration,
 * recommended modules, compliance requirements, and storefront alignment.
 *
 * Accessible from CP sidebar under "Industry Settings" or via direct URL:
 *   /cp/control/portal/epc_industry_kit_panel
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_industry_themes.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

$tenantIndustry = '';
$tenantGroup = '';
if (function_exists('epc_portal_load_site_settings') && isset($db_link)) {
	$settings = epc_portal_load_site_settings($db_link);
	$tenantIndustry = isset($settings['industry']) ? (string) $settings['industry'] : '';
}
if (empty($tenantIndustry) && function_exists('epc_co_profile_get') && isset($db_link)) {
	$profile = epc_co_profile_get($db_link);
	$tenantIndustry = isset($profile['industry']) ? (string) $profile['industry'] : '';
}
if (!empty($tenantIndustry) && function_exists('epc_resolve_industry_group')) {
	$tenantGroup = epc_resolve_industry_group($tenantIndustry);
}
if (empty($tenantGroup)) {
	$tenantGroup = 'retail_ecommerce';
}

$kit = epc_erp_industry_kit($tenantGroup);
$theme = epc_industry_theme($tenantGroup);
$cpAlign = epc_cp_industry_alignment($tenantGroup);

epc_cp_page_frame_open(array(
	'hero' => array(
		'title' => $kit['label'],
		'subtitle' => 'ERP configuration, compliance, and storefront settings for your industry',
		'icon' => isset($theme['hero']['icon']) ? $theme['hero']['icon'] : 'fa-briefcase',
	),
));
?>

<div class="row" style="margin-bottom:20px;">
	<div class="col-md-4">
		<div class="panel panel-default">
			<div class="panel-heading" style="background:<?php echo htmlspecialchars($theme['hero']['accent']); ?>;color:#fff;">
				<h4 class="panel-title"><i class="fa fa-cogs"></i> Costing Method</h4>
			</div>
			<div class="panel-body text-center">
				<h2 style="margin:0;"><?php echo htmlspecialchars(ucwords(str_replace('_', ' ', $kit['costing_method']))); ?></h2>
				<p class="text-muted" style="margin-top:5px;">Recommended for <?php echo htmlspecialchars(str_replace('_', ' ', $tenantGroup)); ?></p>
			</div>
		</div>
	</div>
	<div class="col-md-4">
		<div class="panel panel-default">
			<div class="panel-heading" style="background:<?php echo htmlspecialchars($theme['hero']['accent']); ?>;color:#fff;">
				<h4 class="panel-title"><i class="fa fa-puzzle-piece"></i> Active Modules</h4>
			</div>
			<div class="panel-body text-center">
				<h2 style="margin:0;"><?php echo count($kit['modules']); ?></h2>
				<p class="text-muted" style="margin-top:5px;">ERP modules configured</p>
			</div>
		</div>
	</div>
	<div class="col-md-4">
		<div class="panel panel-default">
			<div class="panel-heading" style="background:<?php echo htmlspecialchars($theme['hero']['accent']); ?>;color:#fff;">
				<h4 class="panel-title"><i class="fa fa-shield"></i> Compliance</h4>
			</div>
			<div class="panel-body text-center">
				<h2 style="margin:0;"><?php echo count($kit['compliance']); ?></h2>
				<p class="text-muted" style="margin-top:5px;">Standards to meet</p>
			</div>
		</div>
	</div>
</div>

<!-- ERP Modules -->
<div class="panel panel-default">
	<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-th-large"></i> ERP Modules for Your Industry</h4></div>
	<div class="panel-body">
		<div class="row">
			<?php foreach ($kit['modules'] as $mod) { ?>
			<div class="col-md-3 col-sm-4 col-xs-6" style="margin-bottom:12px;">
				<div style="border:1px solid #e0e0e0;border-radius:6px;padding:12px;text-align:center;background:#f9fafb;">
					<i class="fa fa-check-circle" style="color:#22c55e;font-size:18px;"></i>
					<div style="margin-top:6px;font-weight:600;font-size:13px;"><?php echo htmlspecialchars(ucwords(str_replace('_', ' ', $mod))); ?></div>
				</div>
			</div>
			<?php } ?>
		</div>
	</div>
</div>

<!-- Inventory Features -->
<div class="panel panel-default">
	<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-cubes"></i> Inventory Features</h4></div>
	<div class="panel-body">
		<table class="table table-bordered table-striped">
			<thead><tr><th>Feature</th><th>Status</th></tr></thead>
			<tbody>
				<?php foreach ($kit['inventory_features'] as $feat) { ?>
				<tr>
					<td><?php echo htmlspecialchars(ucwords(str_replace('_', ' ', $feat))); ?></td>
					<td><span class="label label-success">Active</span></td>
				</tr>
				<?php } ?>
			</tbody>
		</table>
	</div>
</div>

<!-- Compliance Standards -->
<div class="panel panel-default">
	<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-gavel"></i> Compliance Standards</h4></div>
	<div class="panel-body">
		<?php if (!empty($kit['compliance'])) { ?>
		<div class="row">
			<?php foreach ($kit['compliance'] as $std) { ?>
			<div class="col-md-3 col-sm-4" style="margin-bottom:10px;">
				<div style="border:1px solid #fbbf24;border-radius:6px;padding:10px;text-align:center;background:#fffbeb;">
					<i class="fa fa-certificate" style="color:#d97706;"></i>
					<div style="margin-top:4px;font-size:12px;font-weight:600;"><?php echo htmlspecialchars(strtoupper(str_replace('_', ' ', $std))); ?></div>
				</div>
			</div>
			<?php } ?>
		</div>
		<?php } else { ?>
		<p class="text-muted">No specific compliance standards configured for this industry.</p>
		<?php } ?>
	</div>
</div>

<!-- Reports & Integrations -->
<div class="row">
	<div class="col-md-6">
		<div class="panel panel-default">
			<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-bar-chart"></i> Industry Reports</h4></div>
			<div class="panel-body">
				<ul class="list-group">
					<?php foreach ($kit['reports'] as $rpt) { ?>
					<li class="list-group-item"><i class="fa fa-file-text-o"></i> <?php echo htmlspecialchars(ucwords(str_replace('_', ' ', $rpt))); ?></li>
					<?php } ?>
				</ul>
			</div>
		</div>
	</div>
	<div class="col-md-6">
		<div class="panel panel-default">
			<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-plug"></i> Integrations</h4></div>
			<div class="panel-body">
				<ul class="list-group">
					<?php foreach ($kit['integrations'] as $intg) { ?>
					<li class="list-group-item"><i class="fa fa-link"></i> <?php echo htmlspecialchars(ucwords(str_replace('_', ' ', $intg))); ?></li>
					<?php } ?>
				</ul>
			</div>
		</div>
	</div>
</div>

<!-- Storefront Alignment -->
<div class="panel panel-info">
	<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-shopping-bag"></i> Storefront Alignment</h4></div>
	<div class="panel-body">
		<div class="row">
			<div class="col-md-6">
				<h5><strong>Catalog Display</strong></h5>
				<p><code><?php echo htmlspecialchars($cpAlign['catalog_display']); ?></code></p>
				<h5><strong>Price Display</strong></h5>
				<p><code><?php echo htmlspecialchars($cpAlign['price_display']); ?></code></p>
				<h5><strong>Storefront Features</strong></h5>
				<ul>
					<?php foreach ($cpAlign['storefront_features'] as $sf) { ?>
					<li><?php echo htmlspecialchars(ucwords(str_replace('_', ' ', $sf))); ?></li>
					<?php } ?>
				</ul>
			</div>
			<div class="col-md-6">
				<h5><strong>Product Fields (required)</strong></h5>
				<ul>
					<?php foreach ($cpAlign['product_fields'] as $pf) { ?>
					<li><code><?php echo htmlspecialchars($pf); ?></code></li>
					<?php } ?>
				</ul>
				<?php if (!empty($cpAlign['checkout_fields'])) { ?>
				<h5><strong>Checkout Fields</strong></h5>
				<ul>
					<?php foreach ($cpAlign['checkout_fields'] as $cf) { ?>
					<li><code><?php echo htmlspecialchars($cf); ?></code></li>
					<?php } ?>
				</ul>
				<?php } ?>
			</div>
		</div>
	</div>
</div>

<!-- Seed Data -->
<?php if (!empty($theme['products'])) { ?>
<div class="panel panel-default">
	<div class="panel-heading"><h4 class="panel-title"><i class="fa fa-database"></i> Sample Products Available (<?php echo count($theme['products']); ?> items)</h4></div>
	<div class="panel-body">
		<table class="table table-bordered table-striped">
			<thead><tr><th>Product</th><th>Price (USD)</th><th>Category</th><th>Specs</th></tr></thead>
			<tbody>
				<?php foreach ($theme['products'] as $p) { ?>
				<tr>
					<td>
						<?php if (!empty($p['image'])) { ?>
						<img src="<?php echo htmlspecialchars($p['image']); ?>" alt="" style="width:40px;height:40px;object-fit:cover;border-radius:4px;margin-right:8px;vertical-align:middle;">
						<?php } ?>
						<?php echo htmlspecialchars($p['name']); ?>
					</td>
					<td style="text-align:right;">$<?php echo number_format($p['price'], 0); ?></td>
					<td><small class="text-muted"><?php echo htmlspecialchars($p['category_alias']); ?></small></td>
					<td><small><?php echo htmlspecialchars($p['specs']); ?></small></td>
				</tr>
				<?php } ?>
			</tbody>
		</table>
	</div>
</div>
<?php } ?>

<?php epc_cp_page_frame_close(); ?>
