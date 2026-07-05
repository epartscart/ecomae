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
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'hero' => array(
		'badge' => $isSuperCp ? 'Super CP' : 'Client CP',
		'title' => $isClientSite ? 'Site & module settings' : 'Industry & module settings',
		'html_sub' => true,
		'sub' => $isClientSite
			? 'Configure branding, contact details, and which control panel modules appear on <strong>' . htmlspecialchars($host, ENT_QUOTES, 'UTF-8') . '</strong>. Platform-wide tenant management and deploy tools are on <a href="https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard" target="_blank" rel="noopener">ecomae Super CP</a>.'
			: 'This panel controls branding, theme, contact details, and which CP modules appear on <strong>' . htmlspecialchars($host, ENT_QUOTES, 'UTF-8') . '</strong>. Use Tenant hub to onboard client sites; deploy pushes code to registered targets below.',
	),
));
?>

<div class="epc-portal-settings">
	<div class="epc-portal-settings__host-badge" style="margin-bottom:14px">
		<span>Active industry</span>
		<strong><i class="fa <?php echo htmlspecialchars($active_industry['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i> <?php echo htmlspecialchars($active_industry['name'], ENT_QUOTES, 'UTF-8'); ?></strong>
		<a href="<?php echo $cp_base; ?>/shop/tenant_hub/tenant_hub?tab=industry_sub_areas" class="btn btn-sm btn-primary" style="margin-left:12px"><i class="fa fa-toggle-on"></i> Sub-Area Toggles</a>
		<?php if ($isSuperCp): ?>
		<a href="<?php echo $cp_base; ?>/shop/tenant_hub/tenant_hub?tab=industry_consolidation" class="btn btn-sm btn-default" style="margin-left:6px"><i class="fa fa-compress"></i> Consolidation Dashboard</a>
		<?php endif; ?>
	</div>

	<form id="epc-portal-settings-form" class="epc-portal-settings__form">
		<div class="row">
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading"><h4>Industry template</h4></div>
					<div class="panel-body">
						<div class="form-group">
							<label for="epc_ps_industry">Industry</label>
							<select class="form-control" id="epc_ps_industry" name="industry_code">
								<?php foreach ($industryGroups as $grp) {
									if (empty($grp['industries'])) {
										continue;
									}
									?>
								<optgroup label="<?php echo htmlspecialchars($grp['name'], ENT_QUOTES, 'UTF-8'); ?>">
									<?php foreach ($grp['industries'] as $ind) { ?>
									<option value="<?php echo htmlspecialchars($ind['code'], ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($settings['industry_code'] === $ind['code']) ? ' selected' : ''; ?>>
										<?php echo htmlspecialchars($ind['name'], ENT_QUOTES, 'UTF-8'); ?>
									</option>
									<?php } ?>
								</optgroup>
								<?php } ?>
							</select>
							<p class="help-block">Industry sets modules and business type. Pick one of <strong>four named colour styles</strong> below (same features, different look). Switch anytime — no data loss.</p>
							<?php
							$activePkg = isset($settings['contact']['storefront_package']) ? (string) $settings['contact']['storefront_package'] : '';
							if ($activePkg === '' && function_exists('epc_portal_resolve_storefront_package')) {
								$activePkg = epc_portal_resolve_storefront_package($settings);
							}
							$pkgMeta = $activePkg !== '' ? epc_portal_storefront_package_meta($activePkg) : null;
							?>
							<p class="help-block text-muted" style="margin-top:10px">
								<strong>Storefront package:</strong>
								<?php if ($pkgMeta !== null) {
									echo htmlspecialchars($pkgMeta['label'], ENT_QUOTES, 'UTF-8');
									echo ' (<code>' . htmlspecialchars($activePkg, ENT_QUOTES, 'UTF-8') . '</code>)';
								} else {
									echo 'default layout for this industry';
								}
								?>
								— Auto parts: set <code>contact.storefront_package</code> to <code>automotive_spareparts_pro</code> (piston homepage, legacy SVG logo).
								Override colours in the <code>theme</code> object or pick a visual style above; see <code>epc_theme_presets/automotive_spareparts_pro.json</code>.
							</p>
						</div>
						<div class="form-group">
							<label>Visual style template</label>
							<input type="hidden" name="theme_template" id="epc_ps_theme_template" value="<?php echo htmlspecialchars($activeStyleTemplate, ENT_QUOTES, 'UTF-8'); ?>" />
							<div id="epc-style-templates" class="epc-portal-settings__styles">
								<?php foreach ($styleTemplatesCurrent as $tid => $tpl) {
									$t = $tpl['theme'];
									$sel = ($tid === $activeStyleTemplate);
									?>
								<label class="epc-portal-settings__style<?php echo $sel ? ' is-selected' : ''; ?>" data-template-id="<?php echo htmlspecialchars($tid, ENT_QUOTES, 'UTF-8'); ?>">
									<input type="radio" name="theme_template_pick" value="<?php echo htmlspecialchars($tid, ENT_QUOTES, 'UTF-8'); ?>"<?php echo $sel ? ' checked' : ''; ?> />
									<span class="epc-portal-settings__style-swatches" aria-hidden="true">
										<i title="Primary" style="background:<?php echo htmlspecialchars($t['primary'], ENT_QUOTES, 'UTF-8'); ?>"></i>
										<i title="Accent" style="background:<?php echo htmlspecialchars($t['accent'], ENT_QUOTES, 'UTF-8'); ?>"></i>
										<i title="Sidebar" style="background:linear-gradient(135deg,<?php echo htmlspecialchars($t['sidebar_from'], ENT_QUOTES, 'UTF-8'); ?>,<?php echo htmlspecialchars($t['sidebar_to'], ENT_QUOTES, 'UTF-8'); ?>)"></i>
										<i title="Hero" class="epc-portal-settings__style-hero" style="background:linear-gradient(145deg,<?php echo htmlspecialchars($t['hero_from'], ENT_QUOTES, 'UTF-8'); ?>,<?php echo htmlspecialchars($t['hero_to'], ENT_QUOTES, 'UTF-8'); ?>)"></i>
									</span>
									<span class="epc-portal-settings__style-text">
										<strong><?php echo htmlspecialchars($tpl['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
										<small><?php echo htmlspecialchars($tpl['desc'], ENT_QUOTES, 'UTF-8'); ?></small>
									</span>
								</label>
								<?php } ?>
							</div>
						</div>
						<div class="form-group">
							<label>Storefront layout</label>
							<?php
							$currentIndustry = isset($settings['industry_code']) ? (string) $settings['industry_code'] : '';
							$currentLayout = epc_storefront_active_layout($settings);
							$availableLayouts = epc_storefront_layouts_for_industry($currentIndustry);
							?>
							<input type="hidden" name="storefront_layout" id="epc_ps_storefront_layout" value="<?php echo htmlspecialchars($currentLayout, ENT_QUOTES, 'UTF-8'); ?>" />
							<div id="epc-storefront-layouts" class="epc-portal-settings__styles">
								<?php if (empty($availableLayouts)) { ?>
								<p class="text-muted">No layout templates available for this industry yet.</p>
								<?php } else { foreach ($availableLayouts as $lay) {
									$sel = ($lay['id'] === $currentLayout);
									$isDefault = !empty($lay['default']);
								?>
								<label class="epc-portal-settings__style<?php echo $sel ? ' is-selected' : ''; ?>" data-layout-id="<?php echo htmlspecialchars($lay['id'], ENT_QUOTES, 'UTF-8'); ?>">
									<input type="radio" name="storefront_layout_pick" value="<?php echo htmlspecialchars($lay['id'], ENT_QUOTES, 'UTF-8'); ?>"<?php echo $sel ? ' checked' : ''; ?> />
									<span class="epc-portal-settings__style-swatches" aria-hidden="true">
										<i title="Layout" style="background:#475569;font-style:normal;display:flex;align-items:center;justify-content:center;font-size:16px;color:#fff;width:32px;height:32px;border-radius:6px;">
											<?php
											$icons = array('hero_carousel' => '&#xf1de;', 'category_grid' => '&#xf009;', 'product_showcase' => '&#xf00a;', 'brand_focused' => '&#xf02a;',
												'editorial' => '&#xf1ea;', 'collection_grid' => '&#xf009;', 'minimal_boutique' => '&#xf10c;', 'trend_feed' => '&#xf1e0;',
												'luxury_showcase' => '&#xf219;', 'collection_gallery' => '&#xf03e;', 'catalog_filter' => '&#xf0b0;', 'editorial_luxury' => '&#xf1ea;',
												'professional_services' => '&#xf0b1;', 'calculator_led' => '&#xf1ec;', 'corporate_clean' => '&#xf19c;');
											echo isset($icons[$lay['id']]) ? $icons[$lay['id']] : '&#xf009;';
											?>
										</i>
									</span>
									<span class="epc-portal-settings__style-text">
										<strong><?php echo htmlspecialchars($lay['label'], ENT_QUOTES, 'UTF-8'); ?><?php echo $isDefault ? ' <small style="color:#16a34a">(default)</small>' : ''; ?></strong>
										<small><?php echo htmlspecialchars($lay['desc'], ENT_QUOTES, 'UTF-8'); ?></small>
									</span>
								</label>
								<?php } } ?>
							</div>
							<p class="help-block">Choose the homepage structure for your storefront. Each layout arranges sections differently — hero banners, category grids, product showcases, etc. Combine with any colour theme above. New tenants in the same industry can pick a different layout.</p>
						</div>
						<div class="form-group">
							<label for="epc_ps_system">System name</label>
							<input type="text" class="form-control" id="epc_ps_system" name="system_name" value="<?php echo htmlspecialchars($settings['system_name'], ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label for="epc_ps_hub">Hub / company name</label>
							<input type="text" class="form-control" id="epc_ps_hub" name="hub_name" value="<?php echo htmlspecialchars($settings['hub_name'], ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label for="epc_ps_tagline">Tagline</label>
							<input type="text" class="form-control" id="epc_ps_tagline" name="tagline" value="<?php echo htmlspecialchars($settings['tagline'], ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label for="epc_ps_domain">Public site URL</label>
							<input type="url" class="form-control" id="epc_ps_domain" name="domain_path" placeholder="https://www.example.com/" value="<?php echo htmlspecialchars($settings['domain_path'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
							<p class="help-block">Used for payment webhooks, sitemaps, emails, and document headers. Auto-detected from hostname if empty.</p>
						</div>
						<div class="form-group">
							<label for="epc_ps_access_mode">Tenant access mode</label>
							<?php
							$accessMode = function_exists('epc_portal_resolve_access_mode')
								? epc_portal_resolve_access_mode($settings)
								: (string) ($settings['access_mode'] ?? 'full');
							?>
							<select class="form-control" id="epc_ps_access_mode" name="access_mode">
								<option value="full"<?php echo $accessMode === 'full' ? ' selected' : ''; ?>>Full commerce — storefront + commerce CP</option>
								<option value="erp_only"<?php echo $accessMode === 'erp_only' ? ' selected' : ''; ?>>ERP only — no storefront; CP login → ERP shell</option>
								<option value="mixed"<?php echo $accessMode === 'mixed' ? ' selected' : ''; ?>>Mixed — partial ERP modules + optional commerce</option>
								<option value="consultancy"<?php echo $accessMode === 'consultancy' ? ' selected' : ''; ?>>Consultancy — advisory landing + ERP (no shop cart)</option>
							</select>
							<p class="help-block">ERP-only hides commerce modules and redirects homepage to <code>/erp</code>. Mixed lets you enable a subset of ERP areas below while keeping storefront optional.</p>
						</div>
						<div class="form-group">
							<label for="epc_ps_cp_default_lang">CP default language</label>
							<select class="form-control" id="epc_ps_cp_default_lang" name="cp_default_lang">
								<?php foreach ($cpLangOptions as $code => $label) { ?>
								<option value="<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($cpDefaultLang === $code) ? ' selected' : ''; ?>>
									<?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?> (<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>)
								</option>
								<?php } ?>
							</select>
							<p class="help-block">Default Google Translate language for this tenant's control panel and ERP shell. Users can still override via the language selector. Set to English to use visitor IP/browser auto-detect instead. On Super CP, choose a tenant in <strong>Also apply…</strong> below to push this to the client site.</p>
						</div>
						<div class="form-group">
							<label>ERP modules (granular)</label>
							<div class="btn-group btn-group-sm" style="margin-bottom:10px;display:flex;flex-wrap:wrap;gap:6px">
								<?php foreach ($erpModulePresets as $pid => $preset): ?>
								<button type="button" class="btn btn-default epc-erp-mod-preset" data-preset="<?php echo htmlspecialchars($pid, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($preset['label'], ENT_QUOTES, 'UTF-8'); ?></button>
								<?php endforeach; ?>
							</div>
							<div class="row epc-portal-settings__erp-modules">
								<?php foreach ($erpModuleRegistry as $modId => $mod):
									$checked = in_array($modId, $enabledErpModules, true);
									?>
								<div class="col-sm-6 col-md-4" style="margin-bottom:8px">
									<label class="epc-portal-settings__pack" style="margin:0">
										<input type="checkbox" name="erp_modules[]" value="<?php echo htmlspecialchars($modId, ENT_QUOTES, 'UTF-8'); ?>" class="epc-erp-mod-cb"<?php echo $checked ? ' checked' : ''; ?> />
										<span class="epc-portal-settings__pack-icon"><i class="fa <?php echo htmlspecialchars($mod['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i></span>
										<span class="epc-portal-settings__pack-text">
											<strong><?php echo htmlspecialchars($mod['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
											<small><?php echo htmlspecialchars($mod['desc'], ENT_QUOTES, 'UTF-8'); ?></small>
										</span>
									</label>
								</div>
								<?php endforeach; ?>
							</div>
							<p class="help-block">Unchecked areas are hidden in the ERP shell sidebar and blocked for staff (except full admins). Pushed to client DB when you select a tenant target below.</p>
						</div>
					</div>
				</div>
			</div>
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading"><h4>Site contact (portable per domain)</h4></div>
					<div class="panel-body">
						<?php $c = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array(); ?>
						<div class="form-group">
							<label>Trade / store name</label>
							<input type="text" class="form-control" name="contact_trade_name" value="<?php echo htmlspecialchars($c['trade_name'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label>From email</label>
							<input type="email" class="form-control" name="contact_from_email" value="<?php echo htmlspecialchars($c['from_email'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label>Admin notifications email</label>
							<input type="email" class="form-control" name="contact_admin_email" value="<?php echo htmlspecialchars($c['admin_email'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label>Phone / WhatsApp</label>
							<input type="text" class="form-control" name="contact_phone" value="<?php echo htmlspecialchars($c['contact_phone'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="form-group">
							<label>Head office address</label>
							<input type="text" class="form-control" name="contact_head_office_address" value="<?php echo htmlspecialchars($c['head_office_address'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="row">
							<div class="col-sm-6 form-group">
								<label>City</label>
								<input type="text" class="form-control" name="contact_city" value="<?php echo htmlspecialchars($c['city'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
							</div>
							<div class="col-sm-6 form-group">
								<label>Country</label>
								<input type="text" class="form-control" name="contact_country" value="<?php echo htmlspecialchars($c['country'] ?? 'United Arab Emirates', ENT_QUOTES, 'UTF-8'); ?>" />
							</div>
						</div>
						<p class="help-block"><?php echo $isClientSite
							? 'These values are used for payment webhooks, emails, ERP, and documents on this site only.'
							: 'These values replace hardcoded domain names in ERP, payments, notifications, and documents when this site is deployed.'; ?></p>
					</div>
				</div>
			</div>
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading"><h4>Enabled CP modules</h4></div>
					<div class="panel-body epc-portal-settings__packs">
						<?php foreach ($packs as $code => $pack) {
							$checked = in_array($code, $settings['enabled_packs'], true);
							$disabled = ($code === 'core') ? ' disabled checked' : ($checked ? ' checked' : '');
							?>
						<label class="epc-portal-settings__pack">
							<input type="checkbox" name="enabled_packs[]" value="<?php echo htmlspecialchars($code, ENT_QUOTES, 'UTF-8'); ?>"<?php echo $disabled; ?> />
							<span class="epc-portal-settings__pack-icon"><i class="fa <?php echo htmlspecialchars($pack['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i></span>
							<span class="epc-portal-settings__pack-text">
								<strong><?php echo htmlspecialchars($pack['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
								<small><?php echo htmlspecialchars($pack['desc'], ENT_QUOTES, 'UTF-8'); ?></small>
							</span>
						</label>
						<?php } ?>
					</div>
				</div>
			</div>
			<div class="col-lg-12">
				<div class="hpanel">
					<div class="panel-heading"><h4>Sidebar sections (sub-divisions)</h4></div>
					<div class="panel-body">
						<p class="help-block">Module packs above control broad areas (commerce, ERP, etc.). Here you can hide entire sidebar groups or individual menu links on the <strong>client</strong> control panel. Unchecked = visible. The last group is often <strong>AI Agent</strong> (Parts Agent chat) — disable it if you do not use AI-assisted parts lookup.</p>
						<?php if ($isSuperCp && count($tenantList) > 0) { ?>
						<div class="form-group">
							<label for="epc_ps_target_host">Also apply module &amp; sidebar rules to client site</label>
							<select class="form-control" id="epc_ps_target_host" name="target_host">
								<option value="">— This host only (ecomae Super CP) —</option>
								<?php foreach ($tenantList as $tn) {
									$th = (string) $tn['hostname'];
									?>
								<option value="<?php echo htmlspecialchars($th, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($th, ENT_QUOTES, 'UTF-8'); ?><?php echo !empty($tn['trade_name']) ? ' — ' . htmlspecialchars($tn['trade_name'], ENT_QUOTES, 'UTF-8') : ''; ?></option>
								<?php } ?>
							</select>
							<p class="help-block">Choose a registered tenant to push packs and sidebar blocks into that site’s database (e.g. epartscart).</p>
						</div>
						<?php } ?>
						<div class="row">
							<?php foreach ($cpMenuGroups as $grp) {
								$gBlocked = in_array($grp['id'], $menuPolicy['hidden_groups'], true);
								$packHint = !empty($grp['packs']) ? implode(', ', $grp['packs']) : '';
								?>
							<div class="col-md-6 col-lg-4" style="margin-bottom:12px;">
								<label class="epc-portal-settings__pack" style="display:block;">
									<input type="checkbox" class="epc-cp-group-toggle" data-group-id="<?php echo (int) $grp['id']; ?>" name="visible_groups[]" value="<?php echo (int) $grp['id']; ?>"<?php echo $gBlocked ? '' : ' checked'; ?> />
									<strong><?php echo htmlspecialchars($grp['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
									<small class="text-muted"><?php echo (int) $grp['item_count']; ?> links<?php echo $packHint !== '' ? ' · packs: ' . htmlspecialchars($packHint, ENT_QUOTES, 'UTF-8') : ''; ?></small>
								</label>
								<div class="epc-cp-group-items" data-group-id="<?php echo (int) $grp['id']; ?>" style="margin-left:22px;max-height:140px;overflow:auto;"></div>
							</div>
							<?php } ?>
						</div>
					</div>
				</div>
			</div>
		</div>

		<?php if ($showDeploy) { ?>
		<div class="hpanel">
			<div class="panel-heading"><h4>One-click deploy to sites</h4></div>
			<div class="panel-body">
				<p class="text-muted">Push the current portal package to other sites on your server. Each site uses its own industry settings after deploy + setup. (Platform operator only — not shown on client sites.)</p>
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
							<tr data-site-key="<?php echo htmlspecialchars($target['site_key'], ENT_QUOTES, 'UTF-8'); ?>">
								<td>
									<strong><?php echo htmlspecialchars($target['hostname'], ENT_QUOTES, 'UTF-8'); ?></strong>
									<?php if ($is_local) { ?><span class="label label-primary">This site</span><?php } ?>
								</td>
								<td><?php echo htmlspecialchars(isset($industries[$target['industry_code']]['name']) ? $industries[$target['industry_code']]['name'] : $target['industry_code'], ENT_QUOTES, 'UTF-8'); ?></td>
								<td><?php echo $last > 0 ? date('Y-m-d H:i', $last) : '—'; ?></td>
								<td class="epc-deploy-status"><?php echo htmlspecialchars($target['last_deploy_status'] ?: '—', ENT_QUOTES, 'UTF-8'); ?></td>
								<td>
									<?php if (!$is_local) { ?>
									<button type="button" class="btn btn-sm btn-success epc-deploy-site-btn" data-site-key="<?php echo htmlspecialchars($target['site_key'], ENT_QUOTES, 'UTF-8'); ?>">
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
		</div>
		<?php } ?>

		<div class="epc-portal-settings__actions">
			<button type="submit" class="btn btn-primary btn-lg"><i class="fa fa-save"></i> Save industry settings</button>
			<button type="button" class="btn btn-info btn-lg" id="epc-seed-data-btn" style="margin-left:10px;"><i class="fa fa-database"></i> Seed demo products</button>
			<span id="epc-portal-settings-msg" class="text-muted"></span>
		</div>
	</form>
</div>
<?php epc_cp_page_frame_close(); ?>
