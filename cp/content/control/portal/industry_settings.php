<?php
/**
 * CP — Industry Settings (master panel for portal behaviour).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_cp_menu.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_theme_templates.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_packages.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_erp_modules.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_layouts.php';

epc_portal_db_ensure($db_link);
$storefrontPackages = epc_portal_storefront_package_registry();
$settings = epc_portal_load_site_settings($db_link);
$menuPolicy = epc_portal_cp_menu_policy($settings);
$cpMenuGroups = epc_portal_cp_menu_groups_for_settings($db_link);
$industries = epc_portal_settings_industries();
$packs = epc_portal_settings_packs();
$showDeploy = epc_portal_can_deploy_portal_package();
$deploy_targets = $showDeploy ? epc_portal_deploy_targets($db_link) : array();
$host = epc_portal_host();
$isClientSite = epc_portal_is_client_hostname($host);
$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$tenantList = ($isSuperCp && function_exists('epc_portal_list_tenants')) ? epc_portal_list_tenants($db_link) : array();
$active_industry = epc_portal_industry($settings['industry_code']);
$industryGroups = epc_portal_industries_grouped($industries);
$activeStyleTemplate = isset($settings['theme_template']) ? $settings['theme_template'] : 'classic';
$styleTemplatesCurrent = epc_portal_style_templates_for_industry($settings['industry_code']);
$erpModuleRegistry = epc_portal_erp_modules_registry();
$erpModulePresets = epc_portal_erp_modules_presets_ui();
$enabledErpModules = epc_portal_erp_modules_enabled($settings);
$cpDefaultLang = isset($settings['cp_default_lang']) ? (string) $settings['cp_default_lang'] : 'en';
$cpLangOptions = function_exists('epc_cp_translate_language_options') ? epc_cp_translate_language_options() : array('en' => 'English');
if (!function_exists('epc_cp_translate_language_options')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_translate.php';
	$cpLangOptions = epc_cp_translate_language_options();
}
$cp_base = '/' . $DP_Config->backend_dir;
$c = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
$accessMode = function_exists('epc_portal_resolve_access_mode')
	? epc_portal_resolve_access_mode($settings)
	: (string) ($settings['access_mode'] ?? 'full');
$currentIndustry = isset($settings['industry_code']) ? (string) $settings['industry_code'] : '';
$currentLayout = epc_storefront_active_layout($settings);
$availableLayouts = epc_storefront_layouts_for_industry($currentIndustry);
$activePkg = isset($settings['contact']['storefront_package']) ? (string) $settings['contact']['storefront_package'] : '';
if ($activePkg === '' && function_exists('epc_portal_resolve_storefront_package')) {
	$activePkg = epc_portal_resolve_storefront_package($settings);
}
$pkgMeta = $activePkg !== '' ? epc_portal_storefront_package_meta($activePkg) : null;

$packsOn = 0;
foreach ($packs as $code => $_pack) {
	if ($code === 'core' || in_array($code, $settings['enabled_packs'], true)) {
		$packsOn++;
	}
}
$erpOn = count($enabledErpModules);
$groupsVisible = 0;
foreach ($cpMenuGroups as $grp) {
	if (!in_array($grp['id'], $menuPolicy['hidden_groups'], true)) {
		$groupsVisible++;
	}
}
$totalGroups = count($cpMenuGroups);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-inds epc-portal-settings',
));

function epc_inds_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}
?>

<div class="epc-inds-brand">
	<div>
		<div class="epc-inds-brand__mark"><?php echo $isSuperCp ? 'Ecomae · Super CP' : 'EPartsCart · Tenant CP'; ?></div>
		<h2><?php echo $isClientSite ? 'Site &amp; module settings' : 'Industry &amp; module settings'; ?></h2>
		<p>
			<?php if ($isClientSite) { ?>
			Configure branding, contact details, and which control panel modules appear on <strong><?php echo epc_inds_h($host); ?></strong>.
			Platform-wide tenant tools live on ecomae Super CP.
			<?php } else { ?>
			Controls branding, theme, contact details, and which CP modules appear on <strong><?php echo epc_inds_h($host); ?></strong>.
			Use Tenant hub to onboard client sites; deploy pushes code to registered targets below.
			<?php } ?>
		</p>
	</div>
	<div class="epc-inds-brand__actions">
		<a class="btn btn-sm btn-primary" href="<?php echo epc_inds_h($cp_base); ?>/shop/tenant_hub/tenant_hub?tab=industry_sub_areas"><i class="fa fa-toggle-on"></i> Sub-area toggles</a>
		<?php if ($isSuperCp) { ?>
		<a class="btn btn-sm btn-default" href="<?php echo epc_inds_h($cp_base); ?>/shop/tenant_hub/tenant_hub?tab=industry_consolidation"><i class="fa fa-compress"></i> Consolidation</a>
		<?php } ?>
	</div>
</div>

<div class="epc-inds-stats">
	<div class="epc-inds-stat">
		<div class="epc-inds-stat__val"><i class="fa <?php echo epc_inds_h($active_industry['icon']); ?>"></i> <?php echo epc_inds_h($active_industry['name']); ?></div>
		<div class="epc-inds-stat__lbl">Active industry</div>
	</div>
	<div class="epc-inds-stat">
		<div class="epc-inds-stat__val"><?php echo (int) $packsOn; ?> / <?php echo (int) count($packs); ?></div>
		<div class="epc-inds-stat__lbl">CP packs on</div>
	</div>
	<div class="epc-inds-stat">
		<div class="epc-inds-stat__val"><?php echo (int) $erpOn; ?> / <?php echo (int) count($erpModuleRegistry); ?></div>
		<div class="epc-inds-stat__lbl">ERP modules</div>
	</div>
	<div class="epc-inds-stat">
		<div class="epc-inds-stat__val"><?php echo (int) $groupsVisible; ?> / <?php echo (int) $totalGroups; ?></div>
		<div class="epc-inds-stat__lbl">Sidebar groups</div>
	</div>
</div>

<nav class="epc-inds-nav" aria-label="Settings sections">
	<a href="#inds-branding" data-inds-nav><i class="fa fa-paint-brush"></i> Branding</a>
	<a href="#inds-contact" data-inds-nav><i class="fa fa-address-card"></i> Contact</a>
	<a href="#inds-modules" data-inds-nav><i class="fa fa-cubes"></i> Modules</a>
	<a href="#inds-sidebar" data-inds-nav><i class="fa fa-bars"></i> Sidebar</a>
	<?php if ($showDeploy) { ?>
	<a href="#inds-deploy" data-inds-nav><i class="fa fa-cloud-upload"></i> Deploy</a>
	<?php } ?>
</nav>

<form id="epc-portal-settings-form" class="epc-portal-settings__form">

	<section class="epc-inds-section" id="inds-branding">
		<div class="epc-inds-section__head">
			<div>
				<h3><i class="fa fa-paint-brush"></i> Branding &amp; access</h3>
				<p>Industry template, visual style, storefront layout, and how this tenant opens (commerce / ERP-only / mixed).</p>
			</div>
		</div>
		<div class="epc-inds-section__body">
			<div class="row">
				<div class="col-lg-6">
					<div class="form-group">
						<label for="epc_ps_industry">Industry</label>
						<select class="form-control" id="epc_ps_industry" name="industry_code">
							<?php foreach ($industryGroups as $grp) {
								if (empty($grp['industries'])) {
									continue;
								}
								?>
							<optgroup label="<?php echo epc_inds_h($grp['name']); ?>">
								<?php foreach ($grp['industries'] as $ind) { ?>
								<option value="<?php echo epc_inds_h($ind['code']); ?>"<?php echo ($settings['industry_code'] === $ind['code']) ? ' selected' : ''; ?>>
									<?php echo epc_inds_h($ind['name']); ?>
								</option>
								<?php } ?>
							</optgroup>
							<?php } ?>
						</select>
						<p class="help-block">
							Industry sets modules and business type.
							<strong>Storefront package:</strong>
							<?php
							if ($pkgMeta !== null) {
								echo epc_inds_h($pkgMeta['label']) . ' (<code>' . epc_inds_h($activePkg) . '</code>)';
							} else {
								echo 'default layout for this industry';
							}
							?>
						</p>
					</div>
					<div class="form-group">
						<label>Visual style template</label>
						<input type="hidden" name="theme_template" id="epc_ps_theme_template" value="<?php echo epc_inds_h($activeStyleTemplate); ?>" />
						<div id="epc-style-templates" class="epc-portal-settings__styles">
							<?php foreach ($styleTemplatesCurrent as $tid => $tpl) {
								$t = $tpl['theme'];
								$sel = ($tid === $activeStyleTemplate);
								?>
							<label class="epc-portal-settings__style<?php echo $sel ? ' is-selected' : ''; ?>" data-template-id="<?php echo epc_inds_h($tid); ?>">
								<input type="radio" name="theme_template_pick" value="<?php echo epc_inds_h($tid); ?>"<?php echo $sel ? ' checked' : ''; ?> />
								<span class="epc-portal-settings__style-swatches" aria-hidden="true">
									<i title="Primary" style="background:<?php echo epc_inds_h($t['primary']); ?>"></i>
									<i title="Accent" style="background:<?php echo epc_inds_h($t['accent']); ?>"></i>
									<i title="Sidebar" style="background:linear-gradient(135deg,<?php echo epc_inds_h($t['sidebar_from']); ?>,<?php echo epc_inds_h($t['sidebar_to']); ?>)"></i>
									<i title="Hero" class="epc-portal-settings__style-hero" style="background:linear-gradient(145deg,<?php echo epc_inds_h($t['hero_from']); ?>,<?php echo epc_inds_h($t['hero_to']); ?>)"></i>
								</span>
								<span class="epc-portal-settings__style-text">
									<strong><?php echo epc_inds_h($tpl['label']); ?></strong>
									<small><?php echo epc_inds_h($tpl['desc']); ?></small>
								</span>
							</label>
							<?php } ?>
						</div>
					</div>
					<div class="form-group">
						<label>Storefront layout</label>
						<input type="hidden" name="storefront_layout" id="epc_ps_storefront_layout" value="<?php echo epc_inds_h($currentLayout); ?>" />
						<div id="epc-storefront-layouts" class="epc-portal-settings__styles">
							<?php if (empty($availableLayouts)) { ?>
							<p class="text-muted" style="margin:0">No layout templates for this industry yet — package default applies.</p>
							<?php } else {
								foreach ($availableLayouts as $lay) {
									$sel = ($lay['id'] === $currentLayout);
									$isDefault = !empty($lay['default']);
									$icons = array(
										'hero_carousel' => '&#xf1de;', 'category_grid' => '&#xf009;', 'product_showcase' => '&#xf00a;', 'brand_focused' => '&#xf02a;',
										'editorial' => '&#xf1ea;', 'collection_grid' => '&#xf009;', 'minimal_boutique' => '&#xf10c;', 'trend_feed' => '&#xf1e0;',
										'luxury_showcase' => '&#xf219;', 'collection_gallery' => '&#xf03e;', 'catalog_filter' => '&#xf0b0;', 'editorial_luxury' => '&#xf1ea;',
										'professional_services' => '&#xf0b1;', 'calculator_led' => '&#xf1ec;', 'corporate_clean' => '&#xf19c;',
									);
									?>
							<label class="epc-portal-settings__style<?php echo $sel ? ' is-selected' : ''; ?>" data-layout-id="<?php echo epc_inds_h($lay['id']); ?>">
								<input type="radio" name="storefront_layout_pick" value="<?php echo epc_inds_h($lay['id']); ?>"<?php echo $sel ? ' checked' : ''; ?> />
								<span class="epc-portal-settings__style-swatches" aria-hidden="true">
									<i title="Layout" style="background:#475569;font-style:normal;display:flex;align-items:center;justify-content:center;font-size:16px;color:#fff;width:32px;height:32px;border-radius:6px;">
										<?php echo isset($icons[$lay['id']]) ? $icons[$lay['id']] : '&#xf009;'; ?>
									</i>
								</span>
								<span class="epc-portal-settings__style-text">
									<strong><?php echo epc_inds_h($lay['label']); ?><?php echo $isDefault ? ' <small style="color:#16a34a">(default)</small>' : ''; ?></strong>
									<small><?php echo epc_inds_h($lay['desc']); ?></small>
								</span>
							</label>
								<?php }
							} ?>
						</div>
					</div>
				</div>
				<div class="col-lg-6">
					<div class="form-group">
						<label for="epc_ps_system">System name</label>
						<input type="text" class="form-control" id="epc_ps_system" name="system_name" value="<?php echo epc_inds_h($settings['system_name']); ?>" />
					</div>
					<div class="form-group">
						<label for="epc_ps_hub">Hub / company name</label>
						<input type="text" class="form-control" id="epc_ps_hub" name="hub_name" value="<?php echo epc_inds_h($settings['hub_name']); ?>" />
					</div>
					<div class="form-group">
						<label for="epc_ps_tagline">Tagline</label>
						<input type="text" class="form-control" id="epc_ps_tagline" name="tagline" value="<?php echo epc_inds_h($settings['tagline']); ?>" />
					</div>
					<div class="form-group">
						<label for="epc_ps_domain">Public site URL</label>
						<input type="url" class="form-control" id="epc_ps_domain" name="domain_path" placeholder="https://www.example.com/" value="<?php echo epc_inds_h($settings['domain_path'] ?? ''); ?>" />
						<p class="help-block">Used for payment webhooks, sitemaps, emails, and document headers.</p>
					</div>
					<div class="form-group">
						<label for="epc_ps_access_mode">Tenant access mode</label>
						<select class="form-control" id="epc_ps_access_mode" name="access_mode">
							<option value="full"<?php echo $accessMode === 'full' ? ' selected' : ''; ?>>Full commerce — storefront + commerce CP</option>
							<option value="erp_only"<?php echo $accessMode === 'erp_only' ? ' selected' : ''; ?>>ERP only — no storefront; CP login → ERP shell</option>
							<option value="mixed"<?php echo $accessMode === 'mixed' ? ' selected' : ''; ?>>Mixed — partial ERP modules + optional commerce</option>
							<option value="consultancy"<?php echo $accessMode === 'consultancy' ? ' selected' : ''; ?>>Consultancy — advisory landing + ERP (no shop cart)</option>
						</select>
					</div>
					<div class="form-group">
						<label for="epc_ps_cp_default_lang">CP default language</label>
						<select class="form-control" id="epc_ps_cp_default_lang" name="cp_default_lang">
							<?php foreach ($cpLangOptions as $code => $label) { ?>
							<option value="<?php echo epc_inds_h($code); ?>"<?php echo ($cpDefaultLang === $code) ? ' selected' : ''; ?>>
								<?php echo epc_inds_h($label); ?> (<?php echo epc_inds_h($code); ?>)
							</option>
							<?php } ?>
						</select>
					</div>
				</div>
			</div>
		</div>
	</section>

	<section class="epc-inds-section" id="inds-contact">
		<div class="epc-inds-section__head">
			<div>
				<h3><i class="fa fa-address-card"></i> Site contact</h3>
				<p>Portable trade identity for emails, ERP documents, and payment receipts on this domain.</p>
			</div>
		</div>
		<div class="epc-inds-section__body">
			<div class="row">
				<div class="col-md-6 form-group">
					<label>Trade / store name</label>
					<input type="text" class="form-control" name="contact_trade_name" value="<?php echo epc_inds_h($c['trade_name'] ?? ''); ?>" />
				</div>
				<div class="col-md-6 form-group">
					<label>Phone / WhatsApp</label>
					<input type="text" class="form-control" name="contact_phone" value="<?php echo epc_inds_h($c['contact_phone'] ?? ''); ?>" />
				</div>
				<div class="col-md-6 form-group">
					<label>From email</label>
					<input type="email" class="form-control" name="contact_from_email" value="<?php echo epc_inds_h($c['from_email'] ?? ''); ?>" />
				</div>
				<div class="col-md-6 form-group">
					<label>Admin notifications email</label>
					<input type="email" class="form-control" name="contact_admin_email" value="<?php echo epc_inds_h($c['admin_email'] ?? ''); ?>" />
				</div>
				<div class="col-md-12 form-group">
					<label>Head office address</label>
					<input type="text" class="form-control" name="contact_head_office_address" value="<?php echo epc_inds_h($c['head_office_address'] ?? ''); ?>" />
				</div>
				<div class="col-sm-6 form-group">
					<label>City</label>
					<input type="text" class="form-control" name="contact_city" value="<?php echo epc_inds_h($c['city'] ?? ''); ?>" />
				</div>
				<div class="col-sm-6 form-group">
					<label>Country</label>
					<input type="text" class="form-control" name="contact_country" value="<?php echo epc_inds_h($c['country'] ?? 'United Arab Emirates'); ?>" />
				</div>
			</div>
		</div>
	</section>

	<section class="epc-inds-section" id="inds-modules">
		<div class="epc-inds-section__head">
			<div>
				<h3><i class="fa fa-cubes"></i> Modules</h3>
				<p>Toggle ERP shell areas and broad CP packs. Unchecked ERP areas are hidden for staff (admins still see all).</p>
			</div>
		</div>
		<div class="epc-inds-section__body">
			<label style="margin-bottom:8px">ERP modules</label>
			<div class="epc-inds-presets">
				<?php foreach ($erpModulePresets as $pid => $preset) { ?>
				<button type="button" class="btn btn-default btn-sm epc-erp-mod-preset" data-preset="<?php echo epc_inds_h($pid); ?>"><?php echo epc_inds_h($preset['label']); ?></button>
				<?php } ?>
			</div>
			<div class="epc-inds-modules epc-portal-settings__erp-modules">
				<?php foreach ($erpModuleRegistry as $modId => $mod) {
					$checked = in_array($modId, $enabledErpModules, true);
					?>
				<label class="epc-portal-settings__pack">
					<input type="checkbox" name="erp_modules[]" value="<?php echo epc_inds_h($modId); ?>" class="epc-erp-mod-cb"<?php echo $checked ? ' checked' : ''; ?> />
					<span class="epc-portal-settings__pack-icon"><i class="fa <?php echo epc_inds_h($mod['icon']); ?>"></i></span>
					<span class="epc-portal-settings__pack-text">
						<strong><?php echo epc_inds_h($mod['label']); ?></strong>
						<small><?php echo epc_inds_h($mod['desc']); ?></small>
					</span>
				</label>
				<?php } ?>
			</div>

			<hr style="margin:18px 0;border-color:#e2e8f0">

			<label style="margin-bottom:8px">Enabled CP packs</label>
			<div class="epc-inds-modules epc-portal-settings__packs">
				<?php foreach ($packs as $code => $pack) {
					$checked = in_array($code, $settings['enabled_packs'], true);
					$disabled = ($code === 'core') ? ' disabled checked' : ($checked ? ' checked' : '');
					?>
				<label class="epc-portal-settings__pack">
					<input type="checkbox" name="enabled_packs[]" value="<?php echo epc_inds_h($code); ?>"<?php echo $disabled; ?> />
					<span class="epc-portal-settings__pack-icon"><i class="fa <?php echo epc_inds_h($pack['icon']); ?>"></i></span>
					<span class="epc-portal-settings__pack-text">
						<strong><?php echo epc_inds_h($pack['label']); ?></strong>
						<small><?php echo epc_inds_h($pack['desc']); ?></small>
					</span>
				</label>
				<?php } ?>
			</div>
		</div>
	</section>

	<section class="epc-inds-section" id="inds-sidebar">
		<div class="epc-inds-section__head">
			<div>
				<h3><i class="fa fa-bars"></i> Sidebar sections</h3>
				<p>Hide entire sidebar groups or individual menu links on the client control panel. Unchecked = visible.</p>
			</div>
		</div>
		<div class="epc-inds-section__body">
			<?php if ($isSuperCp && count($tenantList) > 0) { ?>
			<div class="form-group">
				<label for="epc_ps_target_host">Also apply module &amp; sidebar rules to client site</label>
				<select class="form-control" id="epc_ps_target_host" name="target_host">
					<option value="">— This host only (ecomae Super CP) —</option>
					<?php foreach ($tenantList as $tn) {
						$th = (string) $tn['hostname'];
						?>
					<option value="<?php echo epc_inds_h($th); ?>"><?php echo epc_inds_h($th); ?><?php echo !empty($tn['trade_name']) ? ' — ' . epc_inds_h($tn['trade_name']) : ''; ?></option>
					<?php } ?>
				</select>
			</div>
			<?php } ?>

			<div class="epc-inds-sidebar-toolbar">
				<div class="epc-inds-sidebar-search">
					<i class="fa fa-search"></i>
					<input type="search" data-inds-sidebar-search placeholder="Search sidebar groups or links…" aria-label="Search sidebar">
				</div>
				<button type="button" class="btn btn-default btn-sm" data-inds-expand-all>Expand all</button>
				<button type="button" class="btn btn-default btn-sm" data-inds-collapse-all>Collapse all</button>
			</div>

			<div id="epc-inds-sidebar-list">
				<?php foreach ($cpMenuGroups as $grp) {
					$gBlocked = in_array($grp['id'], $menuPolicy['hidden_groups'], true);
					$packHint = !empty($grp['packs']) ? implode(', ', $grp['packs']) : '';
					$sub = trim((string) ($grp['subtitle'] ?? ''));
					$searchHay = strtolower($grp['label'] . ' ' . $sub . ' ' . $packHint);
					?>
				<div class="epc-inds-acc" data-inds-acc data-search="<?php echo epc_inds_h($searchHay); ?>">
					<button type="button" class="epc-inds-acc__toggle" data-inds-acc-toggle>
						<input type="checkbox" class="epc-cp-group-toggle epc-inds-acc__check" data-group-id="<?php echo (int) $grp['id']; ?>" name="visible_groups[]" value="<?php echo (int) $grp['id']; ?>"<?php echo $gBlocked ? '' : ' checked'; ?> onclick="event.stopPropagation();" />
						<span class="epc-inds-acc__meta">
							<strong><?php echo epc_inds_h($grp['label']); ?></strong>
							<small><?php
								$bits = array();
								if ($sub !== '') {
									$bits[] = $sub;
								}
								if ($packHint !== '') {
									$bits[] = 'packs: ' . $packHint;
								}
								echo epc_inds_h($bits ? implode(' · ', $bits) : 'Sidebar group');
							?></small>
						</span>
						<span class="epc-inds-acc__count"><?php echo (int) $grp['item_count']; ?> links</span>
						<i class="fa fa-chevron-down epc-inds-acc__chev"></i>
					</button>
					<div class="epc-cp-group-items epc-inds-acc__body" data-group-id="<?php echo (int) $grp['id']; ?>" data-inds-acc-body></div>
				</div>
				<?php } ?>
			</div>
		</div>
	</section>

	<?php if ($showDeploy) { ?>
	<section class="epc-inds-section" id="inds-deploy">
		<div class="epc-inds-section__head">
			<div>
				<h3><i class="fa fa-cloud-upload"></i> One-click deploy</h3>
				<p>Push the current portal package to other sites on this server. Platform operator only.</p>
			</div>
		</div>
		<div class="epc-inds-section__body">
			<div class="table-responsive">
				<table class="table table-striped epc-portal-settings__deploy-table">
					<thead>
						<tr>
							<th>Site</th>
							<th>Industry</th>
							<th>Last deploy</th>
							<th>Status</th>
							<th></th>
						</tr>
					</thead>
					<tbody>
						<?php foreach ($deploy_targets as $target) {
							$is_local = ($target['hostname'] === $host);
							$last = (int) $target['last_deploy_at'];
							?>
						<tr data-site-key="<?php echo epc_inds_h($target['site_key']); ?>">
							<td>
								<strong><?php echo epc_inds_h($target['hostname']); ?></strong>
								<?php if ($is_local) { ?><span class="label label-primary">This site</span><?php } ?>
							</td>
							<td><?php echo epc_inds_h(isset($industries[$target['industry_code']]['name']) ? $industries[$target['industry_code']]['name'] : $target['industry_code']); ?></td>
							<td><?php echo $last > 0 ? epc_inds_h(date('Y-m-d H:i', $last)) : '—'; ?></td>
							<td class="epc-deploy-status"><?php echo epc_inds_h($target['last_deploy_status'] ?: '—'); ?></td>
							<td>
								<?php if (!$is_local) { ?>
								<button type="button" class="btn btn-sm btn-success epc-deploy-site-btn" data-site-key="<?php echo epc_inds_h($target['site_key']); ?>">
									<i class="fa fa-cloud-upload"></i> Deploy now
								</button>
								<?php } else { ?>
								<span class="text-muted">Current host</span>
								<?php } ?>
							</td>
						</tr>
						<?php } ?>
					</tbody>
				</table>
			</div>
			<div id="epc-deploy-log" class="epc-portal-settings__deploy-log" style="display:none;"></div>
		</div>
	</section>
	<?php } ?>

	<div class="epc-inds-sticky">
		<button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save settings</button>
		<button type="button" class="btn btn-default" id="epc-seed-data-btn"><i class="fa fa-database"></i> Seed demo products</button>
		<span id="epc-portal-settings-msg" class="epc-inds-sticky__msg"></span>
	</div>
</form>

<?php epc_cp_page_frame_close(); ?>
