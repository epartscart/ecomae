<?php
/**
 * ecomae.com platform marketing — page renderers (home, platform, industries, industry, pricing, demo, contact).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_layout.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_tenant_showcase.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_demo_layla.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_home_sections.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_faq.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_marketing_pages.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_free_tools.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_blockchain_page.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_legal_pages.php';

if (!function_exists('epc_ecomae_platform_get_industry_groups')) {
	function epc_ecomae_platform_get_industry_groups()
	{
		$marketing = epc_ecomae_platform_industry_marketing();
		if (function_exists('epc_ecomae_platform_industry_marketing_grouped')) {
			try {
				$groups = epc_ecomae_platform_industry_marketing_grouped();
				if (is_array($groups) && $groups !== array()) {
					return $groups;
				}
			} catch (Throwable $e) {
				// fall through
			}
		}
		return array(
			array(
				'code' => 'all',
				'name' => 'Industry solutions',
				'industries' => $marketing,
				'placeholders' => array(),
			),
		);
	}
}

function epc_ecomae_platform_render_page($page, array $params = array(), $mode = 'full')
{
	if ($page === 'brochure') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_marketing_brochure.php';
		return epc_brochure_render_html('ecomae', array(
			'print' => !empty($params['print']),
		));
	}
	if ($page === 'brochure_cp') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_full_brochure.php';
		return epc_cp_full_brochure_render_html(array(
			'brand' => 'ecomae',
			'scope' => (string) ($params['scope'] ?? 'all'),
			'print' => !empty($params['print']),
			'base_path' => '/brochure/cp',
		));
	}
	$inner = epc_ecomae_platform_render_inner($page, $params);
	if ($mode === 'inner') {
		return $inner;
	}
	$GLOBALS['epc_ecomae_layla_splash'] = ($page === '' || $page === 'home');
	$active = ($page === 'industry') ? 'industries' : $page;
	if ($active === 'customer-results') {
		$active = 'customer_results';
	}
	if ($active === 'platform-guides') {
		$active = 'platform_guides';
	}
	if ($active === 'capabilities') {
		$active = 'capabilities';
	}
	if ($active === 'api-documentation') {
		$active = 'api_documentation';
	}
	if ($active === 'auto-price-ai') {
		$active = 'auto_price_ai';
	}
	if ($active === 'api-services' || $active === 'catalog-api' || $active === 'price-pro-api') {
		$active = 'api_services';
	}
	$title = epc_ecomae_platform_page_title($page, $params);
	$description = epc_ecomae_platform_page_description($page, $params);
	$canonicalPath = '/';
	$mktSlugPages = array('docs', 'compare', 'bos', 'solution', 'legal');
	if (in_array($page, $mktSlugPages, true)) {
		if ($page === 'legal') {
			$meta = epc_ecomae_legal_meta($params);
			$title = $meta[0];
			$description = $meta[1];
			$canonicalPath = epc_ecomae_legal_canonical_path($params);
		} else {
			$meta = epc_ecomae_marketing_meta($page, $params);
			if ($meta) { $title = $meta[0]; $description = $meta[1]; }
			$segMap = array('solution' => 'solutions', 'docs' => 'documentation');
			$seg = $segMap[$page] ?? $page;
			$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
			$canonicalPath = '/' . $seg . ($slug !== '' ? '/' . $slug : '');
		}
	} elseif ($page === 'blockchain') {
		$canonicalPath = '/blockchain';
	} elseif ($page === 'platform') {
		$canonicalPath = '/platform';
	} elseif ($page === 'free_tools') {
		$canonicalPath = '/platform/free-tools';
		$tool = isset($params['tool']) ? preg_replace('/[^a-z]/', '', (string) $params['tool']) : '';
		if ($tool !== '') {
			$canonicalPath .= '/' . $tool;
		}
	} elseif ($page !== '' && $page !== 'home') {
		$canonicalPath = '/platform/' . str_replace('_', '-', $page);
		if ($page === 'industry' && !empty($params['slug'])) {
			$canonicalPath = '/platform/industries/' . rawurlencode((string) $params['slug']);
		} elseif ($page === 'industry' && !empty($params['code'])) {
			$canonicalPath = '/platform/industry/' . rawurlencode((string) $params['code']);
		}
	}
	return epc_ecomae_platform_head_html($title, $canonicalPath, $description)
		. epc_ecomae_platform_layout_open($active)
		. $inner
		. epc_ecomae_platform_layout_close();
}

function epc_ecomae_platform_render_inner($page, array $params = array())
{
	$page = preg_replace('/[^a-z0-9_]/', '', (string) $page);
	if ($page === '' || $page === 'home') {
		return epc_ecomae_platform_page_home();
	}
	$method = 'epc_ecomae_platform_page_' . $page;
	if (!function_exists($method)) {
		return epc_ecomae_platform_page_home();
	}
	return $method($params);
}

function epc_ecomae_platform_page_home()
{
	$demo = epc_ecomae_platform_demo_package();
	$base = epc_ecomae_platform_base_url();
	$superCp = epc_ecomae_platform_super_cp_url();
	ob_start();
	?>
<?php echo epc_ecomae_platform_hub($base, $superCp, (int) $demo['days']); ?>
<?php echo epc_ecomae_home_sections_render($base, $superCp, (int) $demo['days']); ?>
<?php echo epc_ecomae_demo_layla_styles(); ?>
<?php $GLOBALS['epc_ecomae_layla_scripts_done'] = true; echo epc_ecomae_demo_layla_scripts((int) $demo['days'], 'auto_parts', true); ?>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_ecosystem_model_section()
{
	$model = epc_ecomae_platform_ecosystem_model();
	ob_start();
	?>
<section class="epm-eco-model" id="ecosystem-model" aria-labelledby="epm-eco-model-title">
	<div class="epm-eco-model__head">
		<div class="epm-badge"><i class="fa fa-sitemap"></i> Ecosystem structure</div>
		<h2 class="epm-section-title" id="epm-eco-model-title" style="margin-top:8px">5 connected ecosystems for structured expansion</h2>
		<p class="epm-section-lead" style="max-width:860px">Designed as a disciplined operating model: each ecosystem has active industries today and expansion-ready industries on roadmap.</p>
	</div>
	<div class="epm-eco-model__grid">
		<?php foreach ($model['ecosystems'] as $eco) { ?>
		<article class="epm-card epm-card--accent epm-eco-model__card">
			<h3><?php echo epc_ecomae_h($eco['name']); ?></h3>
			<p class="epm-eco-model__label"><strong>Active industries</strong></p>
			<ul class="epm-eco-model__list">
				<?php foreach ($eco['active'] as $item) { ?>
				<li><?php echo epc_ecomae_h($item); ?></li>
				<?php } ?>
			</ul>
			<p class="epm-eco-model__label"><strong>Expansion-ready roadmap</strong></p>
			<ul class="epm-eco-model__list epm-eco-model__list--roadmap">
				<?php foreach ($eco['roadmap'] as $item) { ?>
				<li><?php echo epc_ecomae_h($item); ?></li>
				<?php } ?>
			</ul>
		</article>
		<?php } ?>
	</div>
	<div class="epm-highlight epm-eco-model__meta">
		<p><strong>Governance rule:</strong> <?php echo epc_ecomae_h($model['governance']); ?></p>
		<p><strong>Scale horizon:</strong> <?php echo epc_ecomae_h($model['scale']['now']); ?><br /><?php echo epc_ecomae_h($model['scale']['six_months']); ?><br /><?php echo epc_ecomae_h($model['scale']['one_to_two_years']); ?></p>
		<p><strong>Positioning:</strong> <?php echo epc_ecomae_h($model['positioning']); ?></p>
	</div>
</section>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_platform()
{
	$areas = epc_ecomae_platform_product_areas();
	$items = epc_ecomae_platform_client_deliverables();
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<img class="epm-hero__img" src="https://images.unsplash.com/photo-1558494949-ef010cbdcc31?auto=format&fit=crop&w=800&q=75" alt="" loading="lazy" />
		<div class="epm-hero__shade"></div>
		<div class="epm-hero__content">
			<div class="epm-badge">Blockchain BOS Enterprise System · complete overview</div>
			<h1>Everything in the ECOM AE Blockchain BOS — by area</h1>
			<p class="lead">One Blockchain BOS Enterprise System: <strong>ERP + commerce + compliance + workflows + CRM + industry intelligence + cryptographic proof</strong> — go live in <strong>24 hours</strong> with <strong>e-invoice</strong> built in. Below: what the storefront, CP, and Super CP deliver per capability.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="#super-cp">Super CP</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/platform-guides">Super CP guides</a>
				<a class="epm-btn epm-btn--ghost" href="#client-cp-storefront">Client site + CP</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/industries">Industries</a>
			</div>
		</div>
	</div>

	<?php echo epc_ecomae_platform_ecosystem_strip(); ?>

	<?php echo epc_ecomae_platform_unified_stack_section(false); ?>
	<?php echo epc_ecomae_platform_worldwide_tax_toolkit_section(false); ?>
	<?php echo epc_ecomae_platform_go_live_24_section(); ?>

	<div class="epm-model-cards">
		<div class="epm-model-card">
			<i class="fa fa-cloud"></i>
			<strong>Super CP</strong>
			<p>You onboard tenants, modules, and DNS from <a href="https://www.ecomae.com/cp/" target="_blank" rel="noopener">www.ecomae.com/cp</a> — not visible on client domains.</p>
		</div>
		<div class="epm-model-card">
			<i class="fa fa-desktop"></i>
			<strong>Client CP</strong>
			<p>Staff work at <code>www.client.com/cp</code> — orders, stock, finance, documents.</p>
		</div>
		<div class="epm-model-card">
			<i class="fa fa-shopping-bag"></i>
			<strong>Storefront</strong>
			<p>Shoppers and clients use <code>www.client.com</code> — themed per industry.</p>
		</div>
	</div>

	<?php
	$tocAreas = array_merge(
		array(
			array('id' => 'unified-stack', 'title' => 'One cloud: E-commerce + ERP + CRM'),
			array('id' => 'go-live-24-hours', 'title' => 'ERP live in 24 hours'),
		),
		$areas
	);
	echo epc_ecomae_platform_areas_toc($tocAreas);
	?>

	<p class="epm-section-lead">Screenshots are captured from live tenants — e.g. <a href="https://www.taxofinca.com/" target="_blank" rel="noopener" style="color:var(--epm-cyan)">taxofinca.com</a> (tax advisory), <a href="https://www.electronicae.com/" target="_blank" rel="noopener" style="color:var(--epm-cyan)">electronicae.com</a> (electronics commerce), and <a href="https://www.ecomae.com/cp/" target="_blank" rel="noopener" style="color:var(--epm-cyan)">Super CP</a>. Refresh assets with <code>python tools/capture_ecomae_platform_screenshots.py</code>.</p>

	<?php
	$i = 0;
	foreach ($areas as $area) {
		echo epc_ecomae_platform_area_section($area, $i);
		$i++;
	}
	?>

	<h2 class="epm-section-title">What every rental includes</h2>
	<div class="epm-grid">
		<?php foreach ($items as $item) { ?>
		<div class="epm-card epm-card--photo">
			<img src="<?php echo epc_ecomae_h($item['photo']); ?>" alt="" loading="lazy" />
			<div class="epm-card__inner">
				<h4><i class="fa <?php echo epc_ecomae_h($item['icon']); ?> text-primary"></i> <?php echo epc_ecomae_h($item['title']); ?></h4>
				<p><?php echo epc_ecomae_h($item['text']); ?></p>
			</div>
		</div>
		<?php } ?>
	</div>

	<h2 class="epm-section-title">How onboarding works</h2>
	<ol class="epm-steps">
		<li>Register the client in Super CP (intro form + industry template + visual style).</li>
		<li>Client adds registrar A records → platform IP — no separate hosting package.</li>
		<li>We add their domain as an nginx alias on the live storefront stack — tenant DB routing by hostname.</li>
		<li>Set tenant status to <strong>Live</strong> — storefront at www.client.com, CP at /cp/.</li>
		<li>Optional: <?php echo (int) $demo['days']; ?>-day demo, then monthly rental.</li>
	</ol>
	<div class="epm-highlight">
		<h3>Pick an industry template</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Each vertical has its own page with storefront vs CP features, workflows, and module packs.</p>
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/industries">Browse <?php echo count(epc_ecomae_platform_industry_marketing()); ?> industries</a>
	</div>
	<div class="epm-cta">
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h(epc_ecomae_platform_onboard_url()); ?>">Open onboard client</a>
		<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/pricing">View rental plans</a>
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Book <?php echo (int) $demo['days']; ?>-day demo</a>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_industries()
{
	$industries = epc_ecomae_platform_industry_marketing();
	$industryGroups = epc_ecomae_platform_get_industry_groups();
	$details = epc_ecomae_platform_industry_details();
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();

	// Load consolidation groups + DED mapping
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ded_activity_mapping.php';
	$consolidatedGroups = epc_industry_groups();
	$groupCount = count($consolidatedGroups);
	$dedDivisions = epc_ded_divisions();
	$dedTotal = epc_ded_total_activities();
	$registries = epc_worldwide_business_registries();

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_seo.php';

	// Industry photos for hero backgrounds
	$industryPhotos = array(
		'automotive' => 'https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?w=800&q=75',
		'healthcare_medical' => 'https://images.unsplash.com/photo-1631815588090-d4bfec5b1ccb?w=800&q=75',
		'food_beverage' => 'https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=800&q=75',
		'fashion_apparel' => 'https://images.unsplash.com/photo-1445205170230-053b83016050?w=800&q=75',
		'jewellery_luxury' => 'https://images.unsplash.com/photo-1515562141589-67f0d72cec37?w=800&q=75',
		'electronics_technology' => 'https://images.unsplash.com/photo-1518770660439-4636190af475?w=800&q=75',
		'construction_realestate' => 'https://images.unsplash.com/photo-1504307651254-35680f356dfd?w=800&q=75',
		'manufacturing_industrial' => 'https://images.unsplash.com/photo-1565043666747-69f6646db940?w=800&q=75',
		'professional_services' => 'https://images.unsplash.com/photo-1521737711867-e3b97375f902?w=800&q=75',
		'education_training' => 'https://images.unsplash.com/photo-1523050854058-8df90110c9f1?w=800&q=75',
		'hospitality_travel' => 'https://images.unsplash.com/photo-1566073771259-6a8506099945?w=800&q=75',
		'beauty_wellness' => 'https://images.unsplash.com/photo-1560750588-73207b1ef5b8?w=800&q=75',
		'retail_ecommerce' => 'https://images.unsplash.com/photo-1441986300917-64674bd600d8?w=800&q=75',
		'agriculture_farming' => 'https://images.unsplash.com/photo-1500937386664-56d1dfef3854?w=800&q=75',
		'logistics_transport' => 'https://images.unsplash.com/photo-1586528116311-ad8dd3c8310d?w=800&q=75',
		'energy_utilities' => 'https://images.unsplash.com/photo-1509391366360-2e959784a276?w=800&q=75',
		'financial_services' => 'https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=800&q=75',
		'it_software' => 'https://images.unsplash.com/photo-1461749280684-dccba630e2f6?w=800&q=75',
		'media_entertainment' => 'https://images.unsplash.com/photo-1478720568477-152d9b164e26?w=800&q=75',
		'sports_fitness' => 'https://images.unsplash.com/photo-1534438327276-14e5300c3a48?w=800&q=75',
		'home_living' => 'https://images.unsplash.com/photo-1556909114-f6e7ad7d3136?w=800&q=75',
		'wholesale_trading' => 'https://images.unsplash.com/photo-1553413077-190dd305871c?w=800&q=75',
		'rental_leasing' => 'https://images.unsplash.com/photo-1449965408869-eaa3f722e40d?w=800&q=75',
		'nonprofit_government' => 'https://images.unsplash.com/photo-1559027615-cd4628902d4a?w=800&q=75',
		'cleaning_maintenance' => 'https://images.unsplash.com/photo-1581578731548-c64695cc6952?w=800&q=75',
		'pet_animal' => 'https://images.unsplash.com/photo-1587300003388-59208cc962cb?w=800&q=75',
		'printing_signage' => 'https://images.unsplash.com/photo-1562654501-a0ccc0fc3fb1?w=800&q=75',
		'security_safety' => 'https://images.unsplash.com/photo-1558002038-1055907df827?w=800&q=75',
	);

	// Build hub rows + flat sub-industry directory for presentation / SEO links
	$hubRows = array();
	$subDirectory = array();
	foreach ($consolidatedGroups as $gk => $ginfo) {
		$templateKey = (string) ($ginfo['template_key'] ?? $gk);
		$siteUrl = rtrim(epc_industry_seo_site_url_for_template($templateKey), '/');
		$seoSubs = epc_industry_seo_template_sub_industries($templateKey);
		if ($seoSubs === array() && !empty($ginfo['available_sub_areas']) && is_array($ginfo['available_sub_areas'])) {
			$seoSubs = array_values($ginfo['available_sub_areas']);
		}
		$subs = array();
		foreach ($seoSubs as $label) {
			$label = (string) $label;
			$slug = epc_industry_seo_sub_slug($label);
			if ($slug === '') {
				continue;
			}
			$pres = epc_industry_seo_sub_presentation($slug);
			$cats = epc_industry_seo_template_sub_categories($templateKey, $label);
			$entry = array(
				'label' => $label,
				'slug' => $slug,
				'url' => $siteUrl . '/' . $slug,
				'pres' => $pres,
				'cats' => $cats,
			);
			$subs[] = $entry;
			$subDirectory[] = array(
				'label' => $label,
				'slug' => $slug,
				'url' => $siteUrl . '/' . $slug,
				'pres' => $pres,
				'cats' => $cats,
				'group' => (string) ($ginfo['label'] ?? $gk),
				'group_key' => (string) $gk,
				'site' => $siteUrl,
				'icon' => (string) ($ginfo['icon'] ?? 'fa-industry'),
				'color' => (string) (($ginfo['color_scheme']['primary'] ?? '#3b82f6')),
			);
		}
		$hubRows[] = array(
			'gk' => (string) $gk,
			'ginfo' => $ginfo,
			'template_key' => $templateKey,
			'site_url' => $siteUrl,
			'subs' => $subs,
			'photo' => $industryPhotos[$gk] ?? 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&q=75',
			'color' => (string) (($ginfo['color_scheme']['primary'] ?? '#3b82f6')),
		);
	}
	$subIndustryCount = count($subDirectory);

	// Featured verticals (diverse industries + known category-rich pages)
	$featuredWanted = array(
		'food-supplements-nutrition',
		'furniture-retail',
		'supermarket-grocery',
		'precious-metals-refining',
		'pharmacy-drug-dispensing',
		'biomass-bioenergy',
		'diamond-gemstones',
		'gym-fitness-center',
	);
	$featuredBySlug = array();
	foreach ($subDirectory as $row) {
		$featuredBySlug[$row['slug']] = $row;
	}
	$featuredSubs = array();
	foreach ($featuredWanted as $fs) {
		if (isset($featuredBySlug[$fs])) {
			$featuredSubs[] = $featuredBySlug[$fs];
		}
	}
	// Fill remaining from start of directory if some missing
	if (count($featuredSubs) < 6) {
		foreach ($subDirectory as $row) {
			$already = false;
			foreach ($featuredSubs as $f) {
				if ($f['slug'] === $row['slug']) {
					$already = true;
					break;
				}
			}
			if (!$already) {
				$featuredSubs[] = $row;
			}
			if (count($featuredSubs) >= 8) {
				break;
			}
		}
	}

	// Primary catalogue: UAE/GCC portal industries (onboard codes), each with a live URL when available
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_industry_live_bridge.php';
	$liveDefs = epc_portal_industry_live_defs();
	$portalLiveRows = array();
	foreach ($industries as $pCode => $pRow) {
		$live = epc_portal_industry_live_storefront_url($pCode);
		// CP demos use template keys from the live bridge (e.g. healthcare), not consolidation group keys
		$tplKey = isset($liveDefs[$pCode]['template_key']) ? (string) $liveDefs[$pCode]['template_key'] : '';
		$highlights = isset($pRow['highlights']) && is_array($pRow['highlights']) ? $pRow['highlights'] : array();
		$color = (string) (($pRow['theme']['primary'] ?? '') ?: '#0f766e');
		$portalLiveRows[] = array(
			'code' => $pCode,
			'name' => (string) ($pRow['name'] ?? $pCode),
			'icon' => (string) ($pRow['icon'] ?? 'fa-industry'),
			'tagline' => (string) ($pRow['tagline'] ?? ''),
			'photo' => (string) (($pRow['photo'] ?? '') ?: 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=800&q=75'),
			'live' => $live,
			'platform' => $base . 'platform/industry/' . rawurlencode($pCode),
			'template_key' => $tplKey,
			'highlights' => $highlights,
			'color' => $color,
			'ecosystem' => (string) ($pRow['ecosystem'] ?? ''),
		);
	}
	$portalIndustryCount = count($portalLiveRows);

	ob_start();
	?>
<style>
*{box-sizing:border-box}
/* Industries page — photo-rich with expandable detail */
.epm-ind-page{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif}
.epm-ind-hero2{position:relative;padding:100px 40px 80px;background:linear-gradient(135deg,#0a0f1a 0%,#0d1b2a 50%,#162032 100%);overflow:hidden;margin-bottom:0}
.epm-ind-hero2::before{content:'';position:absolute;inset:0;background:radial-gradient(ellipse at 30% 20%,rgba(56,189,248,.08),transparent 60%),radial-gradient(ellipse at 70% 80%,rgba(129,140,248,.06),transparent 50%)}
.epm-ind-hero2::after{content:'';position:absolute;inset:0;background:url("data:image/svg+xml,%3Csvg width='80' height='80' xmlns='http://www.w3.org/2000/svg'%3E%3Ccircle cx='40' cy='40' r='.8' fill='%23ffffff' opacity='.04'/%3E%3C/svg%3E")}
.epm-ind-hero2__inner{position:relative;z-index:2;max-width:900px;margin:0 auto;text-align:center}
.epm-ind-hero2 h1{color:#fff;font-size:clamp(32px,5vw,52px);font-weight:800;margin:0 0 16px;letter-spacing:-1px;background:linear-gradient(135deg,#fff 40%,#38bdf8);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;animation:epmFadeUp .8s ease-out}
.epm-ind-hero2 .lead2{color:#94a3b8;font-size:18px;line-height:1.7;margin:0 auto 32px;max-width:700px;animation:epmFadeUp .8s .1s both}
/* Search */
.epm-search2{position:relative;max-width:640px;margin:0 auto 32px;animation:epmFadeUp .8s .2s both}
.epm-search2 input{width:100%;padding:16px 20px 16px 52px;border:2px solid rgba(255,255,255,.12);border-radius:14px;background:rgba(255,255,255,.04);color:#fff;font-size:16px;outline:none;transition:all .3s;backdrop-filter:blur(8px)}
.epm-search2 input:focus{border-color:#38bdf8;background:rgba(255,255,255,.08);box-shadow:0 0 30px rgba(56,189,248,.15)}
.epm-search2 input::placeholder{color:rgba(255,255,255,.4)}
.epm-search2 .s-icon{position:absolute;left:18px;top:50%;transform:translateY(-50%);color:rgba(255,255,255,.4);font-size:18px}
.epm-search2 .s-count{position:absolute;right:18px;top:50%;transform:translateY(-50%);color:rgba(255,255,255,.35);font-size:12px}
.epm-search2 .s-hint{display:none;position:absolute;top:100%;left:0;right:0;margin-top:8px;background:#1e293b;border:1px solid rgba(255,255,255,.1);border-radius:12px;padding:12px 16px;color:#94a3b8;font-size:13px;z-index:100;box-shadow:0 10px 40px rgba(0,0,0,.5)}
.epm-search2 .s-hint.active{display:block}
.epm-search2 .s-hint strong{color:#38bdf8}
/* Stats */
.epm-stats2{display:flex;justify-content:center;gap:40px;flex-wrap:wrap;margin-top:36px;animation:epmFadeUp .8s .3s both}
.epm-stat2{text-align:center}
.epm-stat2 .val{font-size:clamp(28px,4vw,42px);font-weight:800;background:linear-gradient(135deg,#38bdf8,#a78bfa);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.epm-stat2 .lbl{color:#64748b;font-size:12px;text-transform:uppercase;letter-spacing:1px;margin-top:4px}
/* Demo bar */
.epm-demo2{margin:32px auto 0;max-width:700px;padding:16px 24px;background:rgba(56,189,248,.06);border:1px solid rgba(56,189,248,.2);border-radius:12px;display:flex;align-items:center;gap:14px;flex-wrap:wrap;animation:epmFadeUp .8s .4s both}
.epm-demo2 .key-icon{width:38px;height:38px;background:rgba(56,189,248,.12);border-radius:8px;display:flex;align-items:center;justify-content:center;color:#38bdf8;font-size:16px;flex-shrink:0}
.epm-demo2 .info{flex:1;min-width:200px}
.epm-demo2 .info strong{display:block;color:#fff;font-size:13px;margin-bottom:2px}
.epm-demo2 .info span{color:#64748b;font-size:12px}
.epm-demo2 .info code{background:rgba(255,255,255,.08);padding:1px 6px;border-radius:3px;color:#38bdf8;font-size:11px}
/* Industry Grid — Photo Cards */
.epm-ind-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(320px,1fr));gap:20px;padding:48px 24px;max-width:1400px;margin:0 auto}
.epm-ind-card{position:relative;border-radius:16px;overflow:hidden;cursor:pointer;transition:all .4s cubic-bezier(.16,1,.3,1);border:1px solid #e2e8f0;background:#fff}
.epm-ind-card:hover{transform:translateY(-6px);box-shadow:0 20px 50px rgba(0,0,0,.12);border-color:transparent}
.epm-ind-card__photo{height:180px;position:relative;overflow:hidden}
.epm-ind-card__photo img{width:100%;height:100%;object-fit:cover;transition:transform .6s cubic-bezier(.16,1,.3,1)}
.epm-ind-card:hover .epm-ind-card__photo img{transform:scale(1.08)}
.epm-ind-card__photo-overlay{position:absolute;inset:0;background:linear-gradient(to top,rgba(0,0,0,.7) 0%,rgba(0,0,0,.1) 60%,transparent 100%)}
.epm-ind-card__badge{position:absolute;top:12px;right:12px;background:rgba(255,255,255,.95);color:#1e293b;padding:4px 10px;border-radius:6px;font-size:11px;font-weight:700;backdrop-filter:blur(4px)}
.epm-ind-card__photo-title{position:absolute;bottom:12px;left:16px;right:16px;display:flex;align-items:center;gap:10px}
.epm-ind-card__photo-title .ic{width:36px;height:36px;border-radius:8px;display:flex;align-items:center;justify-content:center;color:#fff;font-size:15px;flex-shrink:0}
.epm-ind-card__photo-title h3{color:#fff;font-size:16px;font-weight:700;margin:0;text-shadow:0 1px 3px rgba(0,0,0,.5)}
.epm-ind-card__body{padding:16px 16px 14px}
.epm-ind-card__desc{font-size:13px;color:#64748b;line-height:1.5;margin:0 0 12px}
.epm-ind-card__subs{display:flex;flex-wrap:wrap;gap:5px;margin-bottom:12px}
.epm-ind-card__sub{padding:3px 8px;background:#f0f9ff;color:#0369a1;border-radius:4px;font-size:10px;font-weight:500;transition:all .2s}
.epm-ind-card__sub:hover{background:#0369a1;color:#fff}
.epm-ind-card__links{display:flex;gap:6px}
.epm-ind-card__link{padding:6px 12px;border-radius:6px;font-size:11px;font-weight:600;text-decoration:none;color:#fff;display:inline-flex;align-items:center;gap:4px;transition:all .2s}
.epm-ind-card__link:hover{transform:translateY(-1px);box-shadow:0 3px 10px rgba(0,0,0,.2);text-decoration:none}
.epm-ind-card__link--site{background:#0284c7}
.epm-ind-card__link--cp{background:#7c3aed}
.epm-ind-card__link--erp{background:#059669}
/* Expanded detail panel */
.epm-ind-card__detail{max-height:0;overflow:hidden;transition:max-height .55s cubic-bezier(.16,1,.3,1);background:#f8fafc;border-top:1px solid #e2e8f0}
.epm-ind-card.expanded .epm-ind-card__detail{max-height:2200px}
.epm-ind-card__detail-inner{padding:16px}
.epm-ind-card__detail h4{font-size:13px;font-weight:700;color:#1e293b;margin:0 0 6px}
.epm-ind-card__detail-note{font-size:12px;color:#64748b;margin:0 0 12px;line-height:1.45}
.epm-ind-card__all-subs{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:8px}
.epm-ind-card__all-subs a.epm-sub-tile{
	display:flex;flex-direction:column;gap:4px;padding:10px 12px;background:#fff;border:1px solid #e2e8f0;
	border-radius:10px;font-size:12px;color:#0f172a;text-decoration:none;transition:all .2s;min-height:64px;
}
.epm-ind-card__all-subs a.epm-sub-tile:hover{border-color:#0284c7;box-shadow:0 6px 16px rgba(2,132,199,.12);transform:translateY(-2px)}
.epm-ind-card__all-subs .epm-sub-tile__name{font-weight:700;line-height:1.3}
.epm-ind-card__all-subs .epm-sub-tile__meta{display:flex;flex-wrap:wrap;gap:4px;align-items:center}
.epm-pres-pill{display:inline-block;padding:2px 7px;border-radius:999px;font-size:9px;font-weight:700;letter-spacing:.04em;text-transform:uppercase}
.epm-pres-pill--atelier{background:#fff7ed;color:#c2410c}
.epm-pres-pill--ledger{background:#f1f5f9;color:#334155}
.epm-pres-pill--mosaic{background:#ecfeff;color:#0e7490}
.epm-pres-pill--dock{background:#eff6ff;color:#1d4ed8}
.epm-sub-tile__cats{font-size:10px;color:#64748b;line-height:1.35}
.epm-ind-card__expand{display:flex;align-items:center;gap:4px;color:#3b82f6;font-size:11px;font-weight:600;cursor:pointer;margin-top:8px;transition:color .2s}
.epm-ind-card__expand:hover{color:#1d4ed8}
.epm-ind-card__expand i{transition:transform .3s}
.epm-ind-card.expanded .epm-ind-card__expand i{transform:rotate(180deg)}
/* Dedicated sub-industry showcase — light text on dark epm-body shell */
.epm-subhub{max-width:1400px;margin:0 auto;padding:56px 24px 20px}
.epm-subhub__head{text-align:center;margin-bottom:28px}
.epm-subhub__head h2{font-size:clamp(24px,3vw,32px);font-weight:800;color:#f8fafc;margin:0 0 10px;letter-spacing:-.02em;text-shadow:0 1px 18px rgba(14,165,233,.18)}
.epm-subhub__head p{color:#cbd5e1;font-size:15px;margin:0 auto;max-width:720px;line-height:1.6}
.epm-subhub__head p a{color:#38bdf8;font-weight:700}
.epm-subhub__featured{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:16px;margin-bottom:36px}
.epm-feat-sub{
	display:flex;flex-direction:column;gap:10px;padding:18px;border-radius:16px;border:1px solid #e2e8f0;
	background:linear-gradient(180deg,#fff,#f8fafc);text-decoration:none;color:inherit;
	transition:transform .25s,box-shadow .25s,border-color .25s;
}
.epm-feat-sub:hover{transform:translateY(-4px);box-shadow:0 16px 36px rgba(15,23,42,.1);border-color:#38bdf8;text-decoration:none;color:inherit}
.epm-feat-sub__top{display:flex;align-items:center;gap:10px}
.epm-feat-sub__ico{width:36px;height:36px;border-radius:10px;display:flex;align-items:center;justify-content:center;color:#fff;font-size:14px;flex-shrink:0}
.epm-feat-sub__group{font-size:11px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:.04em}
.epm-feat-sub__title{font-size:16px;font-weight:800;color:#0f172a;margin:0;line-height:1.25}
.epm-feat-sub__cats{display:flex;flex-wrap:wrap;gap:5px}
.epm-feat-sub__cats span{padding:3px 8px;border-radius:6px;background:#fff;border:1px solid #e2e8f0;font-size:10px;font-weight:600;color:#334155}
.epm-feat-sub__cta{margin-top:auto;font-size:12px;font-weight:700;color:#0284c7}
.epm-subdir{background:#0b1220;border-radius:20px;padding:24px;border:1px solid rgba(255,255,255,.06)}
.epm-subdir__bar{display:flex;flex-wrap:wrap;gap:12px;align-items:center;justify-content:space-between;margin-bottom:16px}
.epm-subdir__bar h3{margin:0;color:#f8fafc;font-size:18px;font-weight:800}
.epm-subdir__bar p{margin:0;color:#94a3b8;font-size:13px}
.epm-subdir__search{flex:1;min-width:220px;max-width:420px;position:relative}
.epm-subdir__search input{
	width:100%;padding:12px 14px 12px 38px;border-radius:10px;border:1px solid rgba(255,255,255,.12);
	background:rgba(255,255,255,.06);color:#f8fafc;font-size:14px;outline:none;
}
.epm-subdir__search input::placeholder{color:#64748b}
.epm-subdir__search i{position:absolute;left:12px;top:50%;transform:translateY(-50%);color:#64748b}
.epm-subdir__count{color:#38bdf8;font-size:12px;font-weight:700}
.epm-subdir__list{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:8px;max-height:420px;overflow:auto;padding-right:4px}
.epm-subdir__item{
	display:flex;flex-direction:column;gap:3px;padding:10px 12px;border-radius:10px;
	background:rgba(255,255,255,.04);border:1px solid rgba(255,255,255,.06);text-decoration:none;color:#e2e8f0;
}
.epm-subdir__item:hover{background:rgba(56,189,248,.12);border-color:rgba(56,189,248,.35);text-decoration:none;color:#fff}
.epm-subdir__item strong{font-size:13px;font-weight:700}
.epm-subdir__item span{font-size:11px;color:#94a3b8}
.epm-subdir__item.is-hidden{display:none}
@media(max-width:768px){
.epm-subhub{padding:40px 16px 12px}
.epm-subdir__list{max-height:320px}
}
/* Best Fit section */
.epm-bestfit{max-width:1400px;margin:0 auto;padding:60px 24px;text-align:center}
.epm-bestfit h2{font-size:28px;font-weight:800;color:#f8fafc;margin:0 0 12px}
.epm-bestfit p{color:#cbd5e1;font-size:15px;margin:0 0 32px}
.epm-bestfit__result{display:none;margin:20px auto;max-width:600px;padding:24px;background:linear-gradient(135deg,#eff6ff,#f0fdf4);border:2px solid #3b82f6;border-radius:16px;text-align:left}
.epm-bestfit__result.active{display:block;animation:epmFadeUp .4s ease-out}
.epm-bestfit__result h3{font-size:18px;font-weight:700;color:#1e293b;margin:0 0 6px}
.epm-bestfit__result p{font-size:13px;color:#475569;margin:0 0 12px}
.epm-bestfit__result .match-score{color:#059669;font-weight:700;font-size:14px}
/* What's Included */
.epm-included{max-width:1400px;margin:0 auto;padding:60px 24px}
.epm-included h2{font-size:28px;font-weight:800;color:#f8fafc;text-align:center;margin:0 0 40px}
.epm-included__grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:20px}
.epm-included__item{padding:28px;background:linear-gradient(135deg,#0f172a,#1e293b);border-radius:14px;border:1px solid rgba(255,255,255,.06)}
.epm-included__item h4{color:#38bdf8;font-size:14px;margin:0 0 12px}
.epm-included__item ul{list-style:none;padding:0;margin:0}
.epm-included__item li{font-size:12px;color:#94a3b8;padding:4px 0;border-bottom:1px solid rgba(255,255,255,.04)}
.epm-included__item li:last-child{border:0}
/* Animations */
@keyframes epmFadeUp{from{opacity:0;transform:translateY(20px)}to{opacity:1;transform:translateY(0)}}
@keyframes pulse{0%,100%{opacity:1}50%{opacity:.4}}
.epm-reveal{opacity:0;transform:translateY(20px);transition:all .6s cubic-bezier(.16,1,.3,1)}
.epm-reveal.vis{opacity:1;transform:translateY(0)}
@media(max-width:768px){
.epm-ind-grid{grid-template-columns:1fr;padding:24px 16px}
.epm-ind-hero2{padding:60px 20px 50px}
.epm-stats2{gap:20px}
}
</style>
<div class="epm-ind-page">
<!-- Hero -->
<section class="epm-ind-hero2">
<div class="epm-ind-hero2__inner">
<h1>Every Industry. One Platform.</h1>
<p class="lead2"><?php echo (int) $portalIndustryCount; ?> UAE/GCC client industries with live storefronts — plus <?php echo (int) $groupCount; ?> consolidation hubs and <?php echo number_format($subIndustryCount); ?>+ dedicated sub-industry pages. Search food supplements, furniture, pharmacy, grocery, or any DET–DED niche.</p>
<!-- Smart Search -->
<div class="epm-search2">
<i class="fa fa-search s-icon"></i>
<input type="text" id="indSearch2" placeholder="Search industry (e.g. food supplements, furniture, pharmacy, biomass...)" autocomplete="off" />
<span class="s-count" id="sCount2"><?php echo (int) $portalIndustryCount; ?> industries</span>
<div class="s-hint" id="searchHint"></div>
</div>
<!-- Demo -->
<div class="epm-demo2">
<div class="key-icon"><i class="fa fa-key"></i></div>
<div class="info">
<strong>Free Demo Access — All Industries</strong>
<span>Email: <code>demo@ecomae.com</code> &nbsp;|&nbsp; Password: <code>demo2026</code></span>
</div>
</div>
<!-- Stats -->
<div class="epm-stats2">
<div class="epm-stat2"><div class="val"><?php echo (int) $portalIndustryCount; ?></div><div class="lbl">Client Industries</div></div>
<div class="epm-stat2"><div class="val"><?php echo (int) $groupCount; ?></div><div class="lbl">Industry Hubs</div></div>
<div class="epm-stat2"><div class="val"><?php echo number_format($subIndustryCount); ?>+</div><div class="lbl">Sub-Industry Pages</div></div>
<div class="epm-stat2"><div class="val"><?php echo count($dedDivisions); ?></div><div class="lbl">DED Divisions</div></div>
</div>
</div>
</section>

<!-- Primary: UAE/GCC portal industries (onboard catalogue) -->
<section class="epm-subhub epm-reveal" id="uae-gcc-industries" style="padding-top:48px;padding-bottom:0">
<div class="epm-subhub__head">
	<h2><?php echo (int) $portalIndustryCount; ?> UAE / GCC industries</h2>
	<p>Every Super CP onboard industry — food supplements, furniture, pharmacy, grocery, and more — with a working live storefront or platform page.</p>
</div>
</section>
<div class="epm-ind-grid" id="indGrid">
<?php foreach ($portalLiveRows as $pr):
	$hl = array_slice($pr['highlights'], 0, 4);
	$kw = strtolower($pr['name'] . ' ' . $pr['code'] . ' ' . $pr['tagline'] . ' ' . $pr['ecosystem'] . ' ' . implode(' ', $hl));
	$liveHost = $pr['live'] !== '' ? preg_replace('#^https://#', '', rtrim($pr['live'], '/')) : '';
	$tpl = $pr['template_key'];
	?>
<div class="epm-ind-card epm-reveal" id="industry-<?php echo epc_ecomae_h($pr['code']); ?>" data-keywords="<?php echo epc_ecomae_h($kw); ?>" data-industry="<?php echo epc_ecomae_h($pr['code']); ?>">
	<div class="epm-ind-card__photo">
		<img src="<?php echo epc_ecomae_h($pr['photo']); ?>" alt="<?php echo epc_ecomae_h($pr['name']); ?> — ecomae industry" loading="lazy">
		<div class="epm-ind-card__photo-overlay"></div>
		<span class="epm-ind-card__badge"><?php echo $pr['live'] !== '' ? 'Live site' : 'Platform'; ?></span>
		<div class="epm-ind-card__photo-title">
			<div class="ic" style="background:<?php echo epc_ecomae_h($pr['color']); ?>"><i class="fa <?php echo epc_ecomae_h($pr['icon']); ?>"></i></div>
			<h3><?php echo epc_ecomae_h($pr['name']); ?></h3>
		</div>
	</div>
	<div class="epm-ind-card__body">
		<p class="epm-ind-card__desc"><?php echo epc_ecomae_h($pr['tagline'] !== '' ? $pr['tagline'] : ('Hosted storefront and CP for ' . $pr['name'] . '.')); ?></p>
		<?php if ($hl !== array()): ?>
		<div class="epm-ind-card__subs">
			<?php foreach ($hl as $chip): ?>
			<span class="epm-ind-card__sub"><?php echo epc_ecomae_h($chip); ?></span>
			<?php endforeach; ?>
		</div>
		<?php elseif ($liveHost !== ''): ?>
		<div class="epm-ind-card__subs">
			<span class="epm-ind-card__sub"><?php echo epc_ecomae_h($liveHost); ?></span>
		</div>
		<?php endif; ?>
		<div class="epm-ind-card__links">
			<?php if ($pr['live'] !== ''): ?>
			<a href="<?php echo epc_ecomae_h($pr['live']); ?>" class="epm-ind-card__link epm-ind-card__link--site" target="_blank" rel="noopener"><i class="fa fa-globe"></i> Live site</a>
			<?php endif; ?>
			<a href="<?php echo epc_ecomae_h($pr['platform']); ?>" class="epm-ind-card__link epm-ind-card__link--cp"><i class="fa fa-info-circle"></i> Details</a>
			<?php if ($tpl !== ''): ?>
			<a href="/cp/demo/<?php echo epc_ecomae_h($tpl); ?>/" class="epm-ind-card__link epm-ind-card__link--erp"><i class="fa fa-th-large"></i> CP</a>
			<?php endif; ?>
		</div>
	</div>
</div>
<?php endforeach; ?>
</div>
<p style="text-align:center;margin:0 0 8px;padding:0 24px;font-size:13px;color:#94a3b8"><?php echo (int) $portalIndustryCount; ?> client industries · <a href="#hubGrid" style="color:#38bdf8;font-weight:600">Browse <?php echo (int) $groupCount; ?> consolidation hubs below</a></p>

<!-- Secondary: consolidation hub cards -->
<section class="epm-subhub epm-reveal" id="industry-hubs" style="padding-top:40px;padding-bottom:0">
<div class="epm-subhub__head">
	<h2><?php echo (int) $groupCount; ?> industry hubs</h2>
	<p>Consolidation domains with expandable sub-industry verticals — each hub hosts multiple client industries above.</p>
</div>
</section>
<div class="epm-ind-grid" id="hubGrid">
<?php foreach ($hubRows as $row):
	$ginfo = $row['ginfo'];
	$gk = $row['gk'];
	$subs = $row['subs'];
	$siteUrl = $row['site_url'];
	$templateKey = $row['template_key'];
	$primaryColor = $row['color'];
	$photo = $row['photo'];
	$subLabels = array();
	foreach ($subs as $s) {
		$subLabels[] = $s['label'];
	}
	$kw = strtolower(($ginfo['label'] ?? '') . ' ' . ($ginfo['description'] ?? '') . ' ' . implode(' ', $subLabels));
	?>
<div class="epm-ind-card epm-reveal" id="group-<?php echo epc_ecomae_h($gk); ?>" data-keywords="<?php echo epc_ecomae_h($kw); ?>" data-group="<?php echo epc_ecomae_h($gk); ?>">
	<div class="epm-ind-card__photo">
		<img src="<?php echo epc_ecomae_h($photo); ?>" alt="<?php echo epc_ecomae_h($ginfo['label']); ?> — ecomae industry site" loading="lazy">
		<div class="epm-ind-card__photo-overlay"></div>
		<span class="epm-ind-card__badge"><?php echo count($subs); ?> verticals</span>
		<div class="epm-ind-card__photo-title">
			<div class="ic" style="background:<?php echo epc_ecomae_h($primaryColor); ?>"><i class="fa <?php echo epc_ecomae_h($ginfo['icon']); ?>"></i></div>
			<h3><?php echo epc_ecomae_h($ginfo['label']); ?></h3>
		</div>
	</div>
	<div class="epm-ind-card__body">
		<p class="epm-ind-card__desc"><?php echo epc_ecomae_h($ginfo['description']); ?></p>
		<div class="epm-ind-card__subs">
			<?php $shown = 0; foreach ($subs as $sub) { if ($shown >= 4) break; $shown++; ?>
			<a class="epm-ind-card__sub" href="<?php echo epc_ecomae_h($sub['url']); ?>"><?php echo epc_ecomae_h($sub['label']); ?></a>
			<?php } ?>
			<?php if (count($subs) > 4): ?>
			<span class="epm-ind-card__sub" style="background:#f1f5f9;color:#64748b;font-weight:600">+<?php echo count($subs) - 4; ?> more</span>
			<?php endif; ?>
		</div>
		<div class="epm-ind-card__links">
			<a href="<?php echo epc_ecomae_h($siteUrl); ?>" class="epm-ind-card__link epm-ind-card__link--site" target="_blank" rel="noopener"><i class="fa fa-globe"></i> Hub site</a>
			<a href="/cp/demo/<?php echo epc_ecomae_h($templateKey); ?>/" class="epm-ind-card__link epm-ind-card__link--cp"><i class="fa fa-th-large"></i> CP</a>
			<a href="/cp/demo/<?php echo epc_ecomae_h($templateKey); ?>/shop/finance/erp" class="epm-ind-card__link epm-ind-card__link--erp"><i class="fa fa-calculator"></i> ERP</a>
		</div>
		<div class="epm-ind-card__expand" onclick="toggleDetail(this)"><i class="fa fa-chevron-down"></i> Browse <?php echo count($subs); ?> dedicated sub-industry pages</div>
	</div>
	<div class="epm-ind-card__detail">
		<div class="epm-ind-card__detail-inner">
			<h4>Dedicated pages in <?php echo epc_ecomae_h($ginfo['label']); ?></h4>
			<p class="epm-ind-card__detail-note">Each vertical opens on <strong><?php echo epc_ecomae_h(preg_replace('#^https://#', '', $siteUrl)); ?></strong> with its own layout (Atelier / Ledger / Mosaic / Dock) and product-service categories — not the hub hero.</p>
			<div class="epm-ind-card__all-subs">
				<?php foreach ($subs as $sub):
					$presKey = (string) ($sub['pres']['key'] ?? 'atelier');
					$presLabel = (string) ($sub['pres']['label'] ?? 'Page');
					$catBits = array_slice($sub['cats'], 0, 3);
					?>
				<a class="epm-sub-tile" href="<?php echo epc_ecomae_h($sub['url']); ?>">
					<span class="epm-sub-tile__name"><?php echo epc_ecomae_h($sub['label']); ?></span>
					<span class="epm-sub-tile__meta">
						<span class="epm-pres-pill epm-pres-pill--<?php echo epc_ecomae_h($presKey); ?>"><?php echo epc_ecomae_h($presLabel); ?></span>
						<?php if ($catBits !== array()): ?>
						<span class="epm-sub-tile__cats"><?php echo epc_ecomae_h(implode(' · ', $catBits)); ?></span>
						<?php else: ?>
						<span class="epm-sub-tile__cats">Open dedicated page →</span>
						<?php endif; ?>
					</span>
				</a>
				<?php endforeach; ?>
			</div>
		</div>
	</div>
</div>
<?php endforeach; ?>
</div>

<!-- Sub-industry presentation hub -->
<section class="epm-subhub epm-reveal" id="sub-industries">
<div class="epm-subhub__head">
	<h2>Dedicated sub-industry pages</h2>
	<p>Every niche gets its own URL, categories, and presentation style — separate from the industry hub’s 3D hero. Example: <a href="https://jewellery.ecomae.com/precious-metals-refining" style="color:#0284c7;font-weight:700">jewellery.ecomae.com/precious-metals-refining</a>.</p>
</div>
<div class="epm-subhub__featured">
<?php foreach ($featuredSubs as $feat):
	$presKey = (string) ($feat['pres']['key'] ?? 'atelier');
	$presLabel = (string) ($feat['pres']['label'] ?? 'Page');
	$cats = array_slice($feat['cats'], 0, 5);
	if ($cats === array()) {
		$cats = array('ERP', 'CP', 'Storefront');
	}
	?>
	<a class="epm-feat-sub" href="<?php echo epc_ecomae_h($feat['url']); ?>">
		<div class="epm-feat-sub__top">
			<span class="epm-feat-sub__ico" style="background:<?php echo epc_ecomae_h($feat['color']); ?>"><i class="fa <?php echo epc_ecomae_h($feat['icon']); ?>"></i></span>
			<div>
				<div class="epm-feat-sub__group"><?php echo epc_ecomae_h($feat['group']); ?></div>
				<span class="epm-pres-pill epm-pres-pill--<?php echo epc_ecomae_h($presKey); ?>"><?php echo epc_ecomae_h($presLabel); ?> layout</span>
			</div>
		</div>
		<h3 class="epm-feat-sub__title"><?php echo epc_ecomae_h($feat['label']); ?></h3>
		<div class="epm-feat-sub__cats">
			<?php foreach ($cats as $c): ?>
			<span><?php echo epc_ecomae_h($c); ?></span>
			<?php endforeach; ?>
		</div>
		<div class="epm-feat-sub__cta">Open page →</div>
	</a>
<?php endforeach; ?>
</div>

<div class="epm-subdir" id="subDirectory">
	<div class="epm-subdir__bar">
		<div>
			<h3>Browse all sub-industries</h3>
			<p>Filter <?php echo number_format($subIndustryCount); ?> dedicated vertical pages across every hub.</p>
		</div>
		<div class="epm-subdir__search">
			<i class="fa fa-search"></i>
			<input type="search" id="subDirSearch" placeholder="Filter verticals (biomass, refining, solar...)" autocomplete="off" />
		</div>
		<div class="epm-subdir__count" id="subDirCount"><?php echo number_format($subIndustryCount); ?> pages</div>
	</div>
	<div class="epm-subdir__list" id="subDirList">
		<?php foreach ($subDirectory as $sd):
			$presLabel = (string) ($sd['pres']['label'] ?? 'Page');
			$kw = strtolower($sd['label'] . ' ' . $sd['group'] . ' ' . $sd['slug'] . ' ' . implode(' ', $sd['cats']));
			?>
		<a class="epm-subdir__item" href="<?php echo epc_ecomae_h($sd['url']); ?>" data-subkw="<?php echo epc_ecomae_h($kw); ?>">
			<strong><?php echo epc_ecomae_h($sd['label']); ?></strong>
			<span><?php echo epc_ecomae_h($sd['group']); ?> · <?php echo epc_ecomae_h($presLabel); ?></span>
		</a>
		<?php endforeach; ?>
	</div>
</div>
</section>

<!-- What Every Industry Gets -->
<section class="epm-included epm-reveal">
<h2>What Every Industry Gets</h2>
<div class="epm-included__grid">
<div class="epm-included__item">
<h4><i class="fa fa-shopping-bag"></i> Live Storefront</h4>
<ul><li>Industry hub + dedicated sub-industry pages</li><li>Distinct layouts (Atelier / Ledger / Mosaic / Dock)</li><li>Category grids per vertical</li><li>Mobile-first responsive layout</li><li>SEO URLs + JSON-LD for every niche</li></ul>
</div>
<div class="epm-included__item">
<h4><i class="fa fa-th-large"></i> Control Panel (CP)</h4>
<ul><li>Industry-specific module toggles</li><li>Sub-area configuration system</li><li>Multi-template storefront chooser</li><li>AI pricing & analytics</li><li>Staff roles & permissions</li></ul>
</div>
<div class="epm-included__item">
<h4><i class="fa fa-calculator"></i> Full ERP System</h4>
<ul><li>Industry costing method (FIFO/WAC/specific)</li><li>Country tax localization (VAT/GST/Sales)</li><li>22+ specialized modules</li><li>Process flows per business activity</li><li>Compliance (AML, e-invoicing)</li></ul>
</div>
<div class="epm-included__item">
<h4><i class="fa fa-globe"></i> Worldwide Ready</h4>
<ul><li>35+ currencies with auto-convert</li><li>Country-specific tax & labour rules</li><li>DED / SIC / NAICS / ISIC aligned</li><li>Local language support (65+ countries)</li><li>Multi-entity consolidation</li></ul>
</div>
</div>
</section>

<!-- Live Client Sites -->
<section class="epm-reveal" style="padding:60px 24px;max-width:1400px;margin:0 auto">
<h2 style="font-size:28px;font-weight:800;color:#f8fafc;text-align:center;margin:0 0 12px">Live Client Sites</h2>
<p style="color:#cbd5e1;font-size:15px;text-align:center;margin:0 0 36px">Real businesses running on ecomae platform — see the same technology powering diverse industries</p>
<div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:20px">
<?php
$liveClients = array(
	array('name'=>'epartscart.com','industry'=>'Automotive Parts','color'=>'#0284c7','icon'=>'fa-car','url'=>'https://www.epartscart.com','desc'=>'OEM & aftermarket auto parts — complete vehicle catalog with VIN search'),
	array('name'=>'electronicae.com','industry'=>'Electronics','color'=>'#7c3aed','icon'=>'fa-microchip','url'=>'https://www.electronicae.com','desc'=>'Consumer electronics marketplace — smartphones, laptops, accessories'),
	array('name'=>'stylenlook.com','industry'=>'Fashion & Beauty','color'=>'#db2777','icon'=>'fa-diamond','url'=>'https://www.stylenlook.com','desc'=>'Fashion boutique — clothing, beauty products, trending styles'),
	array('name'=>'thejewellerytrend.com','industry'=>'Jewellery','color'=>'#d97706','icon'=>'fa-gem','url'=>'https://www.thejewellerytrend.com','desc'=>'Gold & diamond jewellery — TAG system, hallmark certified'),
	array('name'=>'taxofinca.com','industry'=>'Professional Services','color'=>'#059669','icon'=>'fa-briefcase','url'=>'https://www.taxofinca.com','desc'=>'Tax advisory & financial consulting — client portal, ERP integrated'),
);
foreach($liveClients as $lci => $lc):
?>
<a href="<?php echo epc_ecomae_h($lc['url']); ?>" target="_blank" style="display:block;text-decoration:none;border-radius:16px;border:1px solid #e2e8f0;overflow:hidden;transition:all .4s cubic-bezier(.16,1,.3,1);background:#fff;animation:epmFadeUp .6s <?php echo ($lci * 0.1); ?>s both" onmouseover="this.style.transform='translateY(-8px) scale(1.02)';this.style.boxShadow='0 20px 40px rgba(0,0,0,.12)'" onmouseout="this.style.transform='';this.style.boxShadow=''">
<div style="height:6px;background:<?php echo $lc['color']; ?>"></div>
<div style="padding:20px">
<div style="display:flex;align-items:center;gap:12px;margin-bottom:12px">
<div style="width:42px;height:42px;border-radius:10px;background:<?php echo $lc['color']; ?>;display:flex;align-items:center;justify-content:center;color:#fff;font-size:16px"><i class="fa <?php echo $lc['icon']; ?>"></i></div>
<div><div style="font-size:14px;font-weight:700;color:#0f172a"><?php echo epc_ecomae_h($lc['name']); ?></div><div style="font-size:11px;color:<?php echo $lc['color']; ?>;font-weight:600"><?php echo epc_ecomae_h($lc['industry']); ?></div></div>
</div>
<p style="font-size:12px;color:#64748b;line-height:1.5;margin:0 0 12px"><?php echo epc_ecomae_h($lc['desc']); ?></p>
<div style="display:flex;align-items:center;gap:6px;font-size:11px;color:#10b981;font-weight:600"><span style="width:8px;height:8px;background:#10b981;border-radius:50%;display:inline-block;animation:pulse 2s infinite"></span> Live &amp; Running</div>
</div>
</a>
<?php endforeach; ?>
</div>
</section>

<!-- CTA -->
<div style="text-align:center;padding:60px 24px;background:linear-gradient(135deg,#0f172a,#1e293b)">
<h2 style="font-size:28px;font-weight:800;color:#fff;margin:0 0 12px">Not Sure Which Industry Fits?</h2>
<p style="color:#94a3b8;font-size:15px;margin:0 0 24px">Search above or start a free demo in any industry. We load sample data so you see a realistic setup immediately.</p>
<div style="display:flex;gap:12px;justify-content:center;flex-wrap:wrap">
<a href="/platform/demo" style="padding:14px 28px;background:linear-gradient(135deg,#3b82f6,#8b5cf6);color:#fff;border-radius:10px;font-weight:600;text-decoration:none;transition:all .3s;display:inline-flex;align-items:center;gap:8px"><i class="fa fa-rocket"></i> Start Free Demo</a>
<a href="/cp/" style="padding:14px 28px;background:rgba(255,255,255,.08);color:#fff;border:1px solid rgba(255,255,255,.2);border-radius:10px;font-weight:600;text-decoration:none;transition:all .3s;display:inline-flex;align-items:center;gap:8px"><i class="fa fa-sign-in"></i> Login to CP</a>
</div>
</div>
</div>
<script>
(function(){
var portalCards=document.querySelectorAll('#indGrid .epm-ind-card');
var hubCards=document.querySelectorAll('#hubGrid .epm-ind-card');
var input=document.getElementById('indSearch2');
var counter=document.getElementById('sCount2');
var hint=document.getElementById('searchHint');
var subInput=document.getElementById('subDirSearch');
var subItems=document.querySelectorAll('#subDirList .epm-subdir__item');
var subCount=document.getElementById('subDirCount');
var totalPortal=<?php echo (int) $portalIndustryCount; ?>;
var totalSubs=<?php echo (int) $subIndustryCount; ?>;

function scoreText(kw,q){
	if(!q)return 1;
	var words=q.split(/\s+/),score=0;
	words.forEach(function(w){if(w&&kw.indexOf(w)!==-1)score++});
	return score;
}
function filterSubs(q){
	var n=0;
	subItems.forEach(function(it){
		var kw=it.getAttribute('data-subkw')||'';
		var ok=!q||scoreText(kw,q)>0;
		it.classList.toggle('is-hidden',!ok);
		if(ok)n++;
	});
	if(subCount)subCount.textContent=n.toLocaleString()+' page'+(n===1?'':'s');
}
if(input){
input.addEventListener('input',function(){
	var q=this.value.toLowerCase().trim();
	var visible=0,bestMatch=null,bestScore=0;
	portalCards.forEach(function(c){
		var kw=c.getAttribute('data-keywords')||'';
		if(!q){c.style.display='';visible++;return}
		var score=scoreText(kw,q);
		if(score>0){c.style.display='';visible++;if(score>bestScore){bestScore=score;bestMatch=c}}
		else{c.style.display='none'}
	});
	hubCards.forEach(function(c){
		var kw=c.getAttribute('data-keywords')||'';
		if(!q){c.style.display='';return}
		c.style.display=scoreText(kw,q)>0?'':'none';
	});
	counter.textContent=visible+(visible===1?' industry':' industries')+(q?' matched':'');
	filterSubs(q);
	if(subInput&&subInput.value!==this.value)subInput.value=this.value;
	if(bestMatch&&q.length>2){
		var name=bestMatch.querySelector('h3').textContent;
		hint.innerHTML='<strong>Best match:</strong> '+name+' — click to open card';
		hint.className='s-hint active';
		hint.onclick=function(){
			bestMatch.scrollIntoView({behavior:'smooth',block:'center'});
			hint.className='s-hint';
		};
	}else{hint.className='s-hint'}
});
}
if(subInput){
	subInput.addEventListener('input',function(){
		var q=this.value.toLowerCase().trim();
		filterSubs(q);
	});
}
window.toggleDetail=function(el){
	var card=el.closest('.epm-ind-card');
	card.classList.toggle('expanded');
};
// Deep-link #industry-nutrition_supplements, #group-jewellery, or ?group=
var hash=window.location.hash||'';
var params=new URLSearchParams(window.location.search);
var indCode=params.get('industry')||(hash.indexOf('#industry-')===0?hash.slice(10):'');
if(indCode){
	var indTarget=document.getElementById('industry-'+indCode.replace(/[^a-z0-9_]/g,''));
	if(indTarget){
		setTimeout(function(){indTarget.scrollIntoView({behavior:'smooth',block:'center'})},200);
	}
}
var g=params.get('group')||(hash.indexOf('#group-')===0?hash.slice(7):'');
if(g){
	var target=document.getElementById('group-'+g.replace(/[^a-z0-9_]/g,''));
	if(target){
		target.classList.add('expanded');
		setTimeout(function(){target.scrollIntoView({behavior:'smooth',block:'center'})},200);
	}
}
var reveals=document.querySelectorAll('.epm-reveal');
var io=new IntersectionObserver(function(entries){
entries.forEach(function(e){if(e.isIntersecting){e.target.classList.add('vis');io.unobserve(e.target)}})
},{threshold:.1,rootMargin:'0px 0px -40px 0px'});
reveals.forEach(function(el){io.observe(el)});
})();
</script>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_industry(array $params)
{
	$code = (string) ($params['code'] ?? '');
	$ind = epc_ecomae_platform_industry_full($code);
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	if ($ind === null) {
		ob_start();
		?>
<div class="epm-wrap">
	<div class="epm-alert err">Industry not found. <a href="<?php echo epc_ecomae_h($base); ?>platform/industries" style="color:inherit">Browse all industries</a></div>
</div>
		<?php
		return ob_get_clean();
	}

	// Consolidation group info
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
	$groupKey = epc_industry_resolve_group($ind['name'] ?? $code);
	$groupInfo = epc_industry_get_group($ind['name'] ?? $code);

	$storefront = isset($ind['storefront']) ? $ind['storefront'] : $ind['highlights'];
	$cpSpecial = isset($ind['cp_special']) ? $ind['cp_special'] : $ind['highlights'];
	$workflows = isset($ind['workflows']) ? $ind['workflows'] : array();
	$summary = isset($ind['summary']) ? $ind['summary'] : $ind['tagline'];
	$idealFor = isset($ind['ideal_for']) ? $ind['ideal_for'] : '';

	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero epm-industry-hero">
		<img class="epm-hero__img" src="<?php echo epc_ecomae_h($ind['photo']); ?>" alt="<?php echo epc_ecomae_h($ind['name']); ?>" loading="lazy" />
		<div class="epm-hero__shade"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa <?php echo epc_ecomae_h($ind['icon']); ?>"></i> Industry solution</div>
			<h1><?php echo epc_ecomae_h($ind['name']); ?></h1>
			<p class="lead"><?php echo epc_ecomae_h($summary); ?></p>
			<?php
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_industry_live_bridge.php';
			$liveStoreUrl = epc_portal_industry_live_storefront_url($code);
			?>
			<div class="epm-cta">
				<?php if ($liveStoreUrl !== ''): ?>
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($liveStoreUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Open live storefront</a>
				<?php endif; ?>
				<a class="epm-btn epm-btn--<?php echo $liveStoreUrl !== '' ? 'ghost' : 'primary'; ?>" href="<?php echo epc_ecomae_h($base); ?>platform/demo?industry=<?php echo epc_ecomae_h($code); ?>"><i class="fa fa-play-circle"></i> <?php echo (int) $demo['days']; ?>-day demo</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h(epc_ecomae_platform_onboard_url($code)); ?>">Onboard in Super CP</a>
			</div>
			<?php if ($liveStoreUrl !== ''): ?>
			<p style="margin:14px 0 0;font-size:13px;opacity:.9"><i class="fa fa-link"></i> Live: <a href="<?php echo epc_ecomae_h($liveStoreUrl); ?>" style="color:#fff;text-decoration:underline" target="_blank" rel="noopener"><?php echo epc_ecomae_h($liveStoreUrl); ?></a></p>
			<?php endif; ?>
		</div>
	</div>
	<div class="epm-industry-accent"></div>

	<div style="margin:24px 0;padding:16px 20px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:10px;display:flex;align-items:center;gap:14px;flex-wrap:wrap">
		<div style="width:40px;height:40px;border-radius:8px;background:<?php echo epc_ecomae_h($groupInfo['color_scheme']['primary'] ?? '#3b82f6'); ?>;display:flex;align-items:center;justify-content:center">
			<i class="fa <?php echo epc_ecomae_h($groupInfo['icon']); ?>" style="color:#fff;font-size:18px"></i>
		</div>
		<div style="flex:1">
			<strong style="font-size:14px">Template group: <?php echo epc_ecomae_h($groupInfo['label']); ?></strong>
			<p style="margin:2px 0 0;font-size:12px;color:#64748b"><?php echo epc_ecomae_h($groupInfo['description']); ?> — Shared template with <?php echo count($groupInfo['available_sub_areas'] ?? array()); ?> toggleable sub-areas.</p>
			<?php if ($liveStoreUrl !== ''): ?>
			<p style="margin:8px 0 0;font-size:13px"><a href="<?php echo epc_ecomae_h($liveStoreUrl); ?>" target="_blank" rel="noopener" style="color:#0284c7;font-weight:700"><i class="fa fa-external-link"></i> <?php echo epc_ecomae_h($liveStoreUrl); ?></a></p>
			<?php endif; ?>
		</div>
		<div style="display:flex;flex-wrap:wrap;gap:4px">
			<?php foreach (array_slice($groupInfo['available_sub_areas'] ?? array(), 0, 4) as $aLabel) { ?>
			<span style="display:inline-block;padding:3px 8px;background:#e0e7ff;color:#3730a3;border-radius:4px;font-size:11px"><?php echo epc_ecomae_h($aLabel); ?></span>
			<?php } ?>
			<?php if (count($groupInfo['available_sub_areas'] ?? array()) > 4): ?>
			<span style="display:inline-block;padding:3px 8px;background:#f1f5f9;color:#64748b;border-radius:4px;font-size:11px">+<?php echo count($groupInfo['available_sub_areas']) - 4; ?> more</span>
			<?php endif; ?>
		</div>
	</div>

	<h2 class="epm-section-title">What your client sees</h2>
	<p class="epm-section-lead">Storefront for their customers · Control panel for their team (same platform, industry theme).</p>
	<?php echo epc_ecomae_platform_industry_themed_previews($code, $ind['name']); ?>

	<div class="epm-three-col">
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-shopping-bag text-primary"></i> Storefront — what's special</h4>
			<ul class="epm-feature-list">
				<?php foreach ($storefront as $item) { ?>
				<li><?php echo epc_ecomae_h($item); ?></li>
				<?php } ?>
			</ul>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-th-large text-primary"></i> Control panel — what's special</h4>
			<ul class="epm-feature-list">
				<?php foreach ($cpSpecial as $item) { ?>
				<li><?php echo epc_ecomae_h($item); ?></li>
				<?php } ?>
			</ul>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-cubes text-primary"></i> CP module packs</h4>
			<p style="margin-bottom:12px">
				<?php foreach ($ind['cp_packs'] as $pack) {
					$pl = epc_ecomae_platform_pack_label($pack);
					?>
				<span class="epm-pill" title="<?php echo epc_ecomae_h($pl['desc']); ?>"><i class="fa <?php echo epc_ecomae_h($pl['icon']); ?>"></i> <?php echo epc_ecomae_h($pl['label']); ?></span>
				<?php } ?>
			</p>
			<p style="font-size:13px;color:var(--epm-muted)"><strong>Demo:</strong> <?php echo epc_ecomae_h($ind['demo_note']); ?></p>
			<?php if ($idealFor !== '') { ?>
			<p style="font-size:13px;color:var(--epm-muted);margin-top:12px"><strong>Ideal for:</strong> <?php echo epc_ecomae_h($idealFor); ?></p>
			<?php } ?>
		</div>
	</div>

	<?php if ($workflows !== array()) { ?>
	<h2 class="epm-section-title">Typical workflow</h2>
	<ol class="epm-steps">
		<?php foreach ($workflows as $step) { ?>
		<li><?php echo epc_ecomae_h($step); ?></li>
		<?php } ?>
	</ol>
	<?php } ?>

	<div class="epm-highlight">
		<h3>Ready to show <?php echo epc_ecomae_h($ind['name']); ?> to a client?</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">We provision a sandbox tenant with this industry's theme and CP modules. After <?php echo (int) $demo['days']; ?> days, upgrade to monthly rental or we remove the demo.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo?industry=<?php echo epc_ecomae_h($code); ?>">Book <?php echo epc_ecomae_h($ind['name']); ?> demo</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/industries">← All industries</a>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_pricing()
{
	$plans = epc_ecomae_platform_rental_plans();
	$base = epc_ecomae_platform_base_url();
	$demoUrl = $base . 'platform/demo';
	$contactUrl = $base . 'platform/contact';
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407,#075985 60%,#0a0a0a)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge">Monthly rental</div>
			<h1>Platform rental — per client tenant</h1>
			<p class="lead">Predictable AED pricing for hosted storefront + CP + database. Demo is free for <?php echo (int) epc_ecomae_platform_demo_package()['days']; ?> days; rental starts when they go live.</p>
		</div>
	</div>
	<div class="epm-grid">
		<?php foreach ($plans as $plan) {
			$cls = !empty($plan['featured']) ? 'epm-price featured' : 'epm-price';
			$ctaHref = ($plan['price_aed'] === null) ? $contactUrl : $demoUrl;
			$ctaLabel = ($plan['price_aed'] === null) ? 'Contact sales' : 'Start demo';
			?>
		<div class="<?php echo $cls; ?>">
			<h4><?php echo epc_ecomae_h($plan['name']); ?></h4>
			<?php if (!empty($plan['tagline'])) { ?>
			<div style="font-size:13px;color:var(--epm-muted);margin:-4px 0 8px"><?php echo epc_ecomae_h($plan['tagline']); ?></div>
			<?php } ?>
			<?php if ($plan['price_aed'] === null) { ?>
			<div class="amt">Custom</div>
			<?php } else { ?>
			<div class="amt">AED <?php echo number_format((int) $plan['price_aed']); ?><small style="font-size:14px;font-weight:600;color:var(--epm-muted)"> / <?php echo epc_ecomae_h($plan['period']); ?></small></div>
			<?php } ?>
			<ul><?php foreach ($plan['items'] as $item) { ?><li><?php echo epc_ecomae_h($item); ?></li><?php } ?></ul>
			<div class="epm-cta" style="margin-top:14px">
				<a class="epm-btn epm-btn--primary epm-btn--sm" href="<?php echo epc_ecomae_h($ctaHref); ?>"><?php echo epc_ecomae_h($ctaLabel); ?></a>
			</div>
		</div>
		<?php } ?>
	</div>
	<p style="color:var(--epm-muted);font-size:14px;margin-top:20px">All plans include SSL, tenant isolation, country-driven compliance, unlimited users, blockchain proof anchoring and Blockchain BOS onboarding tools. Prices are indicative — final commercial terms per client contract.</p>
	<?php
	$bench = function_exists('epc_ecomae_platform_price_benchmark') ? epc_ecomae_platform_price_benchmark() : null;
	if ($bench) { ?>
	<div class="epm-section" style="margin-top:40px">
		<div class="epm-badge">How we compare</div>
		<h2 style="margin:8px 0 4px">Enterprise ERP &amp; commerce — without the enterprise bill</h2>
		<p class="lead" style="margin-bottom:18px">The same multi-tenant, multi-vendor, multi-warehouse, multichannel control the big platforms charge per user for — flat per tenant, unlimited users, no implementation fee.</p>
		<div style="overflow-x:auto">
		<table class="epm-cmp" style="width:100%;border-collapse:collapse;font-size:14px">
			<thead>
				<tr style="text-align:left;border-bottom:2px solid rgba(148,163,184,.25)">
					<th style="padding:10px 12px">Platform</th>
					<th style="padding:10px 12px">Indicative price</th>
					<th style="padding:10px 12px">Model</th>
					<th style="padding:10px 12px">Setup</th>
				</tr>
			</thead>
			<tbody>
			<?php foreach ($bench['rows'] as $r) {
				$rowStyle = !empty($r['highlight'])
					? 'background:linear-gradient(90deg,rgba(2,132,199,.16),rgba(2,132,199,.04));font-weight:700'
					: '';
				?>
				<tr style="border-bottom:1px solid rgba(148,163,184,.15);<?php echo $rowStyle; ?>">
					<td style="padding:10px 12px"><?php echo epc_ecomae_h($r['name']); ?></td>
					<td style="padding:10px 12px"><?php echo epc_ecomae_h($r['price']); ?></td>
					<td style="padding:10px 12px"><?php echo epc_ecomae_h($r['model']); ?></td>
					<td style="padding:10px 12px"><?php echo epc_ecomae_h($r['setup']); ?></td>
				</tr>
			<?php } ?>
			</tbody>
		</table>
		</div>
		<p style="color:var(--epm-muted);font-size:12px;margin-top:12px"><?php echo epc_ecomae_h($bench['note']); ?></p>
	</div>
	<?php } ?>
	<?php echo epc_ecomae_platform_go_live_24_section(); ?>
	<div class="epm-cta">
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Try free demo first</a>
		<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact sales</a>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_demo()
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_demo.php';
	$demo = epc_ecomae_platform_demo_package();
	$presets = epc_portal_demo_industry_presets();
	$pref = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['industry'] ?? '')));
	$flash = epc_ecomae_platform_demo_flash();
	ob_start();
	?>
<?php echo epc_ecomae_demo_layla_styles(); ?>
<div class="epm-wrap">
	<section class="epm-layla-hero" aria-labelledby="epm-layla-demo-title">
		<div class="epm-layla-hero__grid">
			<div>
				<?php echo epc_ecomae_demo_layla_stage_html((int) $demo['days'], 'epm-layla'); ?>
			</div>
			<div>
				<div class="epm-layla-hero__badge-row">
					<div class="epm-badge"><i class="fa fa-magic"></i> AI demo wizard</div>
					<div class="epm-badge" style="background:rgba(124,58,237,.12);border-color:rgba(167,139,250,.35);color:#c4b5fd"><i class="fa fa-clock-o"></i> <?php echo (int) $demo['days']; ?>-day sandbox</div>
				</div>
				<h1 id="epm-layla-demo-title"><?php echo epc_ecomae_h($demo['title']); ?></h1>
				<p class="lead">Layla provisions an isolated tenant with <strong>storefront, control panel, and ERP</strong> in minutes. Choose <strong>auto spare parts</strong> or <strong>fashion retail</strong> — ERP + CRM + e-commerce in one cloud.</p>
			</div>
		</div>
	</section>
	<?php echo epc_ecomae_demo_layla_wizard_html($demo, $presets, $pref, $flash); ?>
</div>
<?php
	$GLOBALS['epc_ecomae_layla_pref'] = $pref !== '' ? $pref : 'auto_parts';
	$GLOBALS['epc_ecomae_layla_scripts_done'] = true;
	echo epc_ecomae_demo_layla_scripts((int) $demo['days'], $GLOBALS['epc_ecomae_layla_pref'], true);
	?>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_contact()
{
	$superCp = epc_ecomae_platform_super_cp_url();
	$base = epc_ecomae_platform_base_url();
	$wa = '971567607011';
	$flash = null;
	if (($_SERVER['REQUEST_METHOD'] ?? '') === 'POST' && !empty($_POST['epc_contact_request'])) {
		$name = trim((string) ($_POST['contact_name'] ?? ''));
		$email = trim((string) ($_POST['contact_email'] ?? ''));
		$company = trim((string) ($_POST['company'] ?? ''));
		$message = trim((string) ($_POST['message'] ?? ''));
		if ($name !== '' && filter_var($email, FILTER_VALIDATE_EMAIL)) {
			@mail(
				'hello@ecomae.com',
				'ECOM AE contact — ' . $name,
				"Name: {$name}\nEmail: {$email}\nCompany: {$company}\n\n{$message}",
				'From: ' . $email . "\r\nReply-To: " . $email
			);
			$flash = array('ok' => true, 'message' => 'Thank you — we respond within one business day (GST).');
		} else {
			$flash = array('ok' => false, 'message' => 'Please enter your name and a valid email.');
		}
	}
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero" style="min-height:200px">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407,#5a0f16)"></div>
		<div class="epm-hero__content">
			<h1>Contact ECOM AE</h1>
			<p class="lead">Demo requests, rental quotes, and operator support. Response within one business day.</p>
		</div>
	</div>
	<?php if (is_array($flash)) { ?>
	<div class="epm-alert <?php echo !empty($flash['ok']) ? 'ok' : 'err'; ?>"><?php echo epc_ecomae_h($flash['message']); ?></div>
	<?php } ?>
	<div class="epm-grid">
		<div class="epm-card">
			<h4><i class="fa fa-phone text-primary"></i> Phone</h4>
			<p><a href="tel:+971567607011" style="color:var(--epm-cyan)">+971 56 760 7011</a></p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-whatsapp text-primary"></i> WhatsApp</h4>
			<p><a href="https://wa.me/<?php echo epc_ecomae_h($wa); ?>?text=<?php echo rawurlencode('Hello ECOM AE — I would like to discuss platform rental or a demo.'); ?>" style="color:var(--epm-cyan)" target="_blank" rel="noopener">Chat on WhatsApp</a></p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-envelope text-primary"></i> Email</h4>
			<p><a href="mailto:hello@ecomae.com" style="color:var(--epm-cyan)">hello@ecomae.com</a></p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-th-large text-primary"></i> Super CP</h4>
			<p><a href="<?php echo epc_ecomae_h($superCp); ?>" style="color:var(--epm-cyan)">Operator control panel</a></p>
			<p class="text-muted" style="font-size:13px">Tenant hub, onboard client, DNS checklist.</p>
		</div>
	</div>
	<div class="epm-split">
		<div class="epm-card epm-form">
			<h4>Send a message</h4>
			<form method="post" action="">
				<input type="hidden" name="epc_contact_request" value="1" />
				<div class="form-group">
					<label>Name</label>
					<input class="form-control" name="contact_name" required />
				</div>
				<div class="form-group">
					<label>Email</label>
					<input class="form-control" type="email" name="contact_email" required />
				</div>
				<div class="form-group">
					<label>Company</label>
					<input class="form-control" name="company" />
				</div>
				<div class="form-group">
					<label>Message</label>
					<textarea class="form-control" name="message" rows="4" required></textarea>
				</div>
				<button type="submit" class="epm-btn epm-btn--primary" style="width:100%;justify-content:center;border:0;cursor:pointer">Send enquiry</button>
			</form>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-map-marker text-primary"></i> Electronic World Group</h4>
			<p>Dubai, United Arab Emirates</p>
			<p style="color:var(--epm-muted);font-size:14px">Hosted multi-tenant cloud · Go live in 24 hours · UAE e-invoice ready.</p>
		</div>
	</div>
	<div class="epm-cta">
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Request demo</a>
		<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/industries">Browse industries</a>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_about()
{
	$base = epc_ecomae_platform_base_url();
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero" style="min-height:220px">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407,#5a0f16 50%,#0a0a0a)"></div>
		<div class="epm-hero__content">
			<h1>About ECOM AE</h1>
			<p class="lead">Built by Electronic World Group in Dubai — one cloud for commerce, ERP, CRM, and UAE e-invoicing.</p>
		</div>
	</div>
	<div class="epm-split">
		<div>
			<h2 class="epm-section-title" style="margin-top:0">Who we are</h2>
			<p class="epm-section-lead">ECOM AE is the hosted operator platform behind live tenants including eParts Cart, Taxofinca, Electronicae, Stylenlook, and The Jewellery Trend. We provision isolated MySQL databases, industry themes, and Super CP onboarding so agencies go live in as little as 24 hours after DNS.</p>
			<ul class="epm-feature-list">
				<li>Multi-tenant Model C — one nginx docroot, hostname routing</li>
				<li>Super CP (operator control panel) at ecomae.com/cp</li>
				<li>Client CP and storefront on each client domain</li>
				<li>Peppol / PINT-AE e-invoice in ERP Finance</li>
			</ul>
		</div>
		<div class="epm-card epm-card--accent">
			<h4>Electronic World Group</h4>
			<p>Dubai, UAE · B2B SaaS and commerce infrastructure for the GCC.</p>
			<div class="epm-cta" style="margin-top:12px">
				<a class="epm-btn epm-btn--primary epm-btn--sm" href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact us</a>
				<a class="epm-btn epm-btn--ghost epm-btn--sm" href="<?php echo epc_ecomae_h($base); ?>platform/customer-results">Customer results</a>
			</div>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_platform_guides()
{
	$areas = epc_ecomae_platform_super_cp_guide_areas();
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407 0%,#5a0f16 42%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-th-large"></i> Super CP capability map</div>
			<h1>What you get in Super CP — by area</h1>
			<p class="lead">Every operator guide in Super CP translated into customer-facing benefits. No internal admin URLs or credentials here — just what the platform delivers for you and your clients.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/capabilities"><i class="fa fa-th"></i> Full catalog (<?php echo (int) epc_ecomae_platform_super_cp_capability_count(); ?> capabilities)</a>
				<a class="epm-btn epm-btn--ghost" href="#guide-areas"><i class="fa fa-th-list"></i> Browse areas</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demo['days']; ?>-day demo</a>
			</div>
		</div>
	</div>

	<?php
	$toc = array();
	foreach ($areas as $a) {
		$toc[] = array('id' => $a['id'], 'title' => $a['title']);
	}
	echo epc_ecomae_platform_areas_toc($toc);
	?>

	<div id="guide-areas">
		<?php foreach ($areas as $i => $area) {
			echo epc_ecomae_platform_guide_area_section($area, $i);
		} ?>
	</div>

	<div class="epm-highlight">
		<h3>Browse all <?php echo (int) epc_ecomae_platform_super_cp_capability_count(); ?> capabilities</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Pricing, e-invoice, fulfilment, AI, ERP, logistics, and every industry template — searchable catalog with category filters.</p>
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/capabilities"><i class="fa fa-th"></i> Open capabilities catalog</a>
	</div>

	<div class="epm-highlight">
		<h3>Ready to onboard your first tenant?</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Start with a <?php echo (int) $demo['days']; ?>-day industry demo or talk to our onboarding team about monthly rental.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Book demo</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform">Platform overview</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact sales</a>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_capabilities()
{
	$catalog = epc_ecomae_platform_super_cp_capabilities_catalog();
	$categories = epc_ecomae_platform_super_cp_capability_categories();
	$count = count($catalog);
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407 0%,#5a0f16 42%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-th"></i> <?php echo (int) $count; ?> capabilities</div>
			<h1>Super CP — full capability catalog</h1>
			<p class="lead">Every module your tenants can run: pricing, UAE e-invoice, fulfilment, ERP, AI, logistics, CRM, and industry templates — disclosed for agencies evaluating the platform.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/platform-guides"><i class="fa fa-book"></i> Super CP guides</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demo['days']; ?>-day demo</a>
			</div>
		</div>
	</div>

	<?php echo epc_ecomae_platform_capabilities_browser($catalog, $categories, $count); ?>

	<div class="epm-highlight">
		<h3>Want the guided tour by area?</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Six narrative guide areas cover onboarding, themes, continuity, and customer proof — or start a demo sandbox with your industry template.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/platform-guides">Super CP guides</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform">Platform overview</a>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_business_continuity()
{
	$bc = epc_ecomae_platform_business_continuity();
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407 0%,#5a0f16 42%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-shield"></i> Business continuity</div>
			<h1><?php echo epc_ecomae_h($bc['headline']); ?></h1>
			<p class="lead"><?php echo epc_ecomae_h($bc['lead']); ?></p>
		</div>
	</div>
</div>
<?php echo epc_ecomae_platform_continuity_section('page'); ?>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_customer_results()
{
	$base = epc_ecomae_platform_base_url();
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407,#5a0f16 55%,#0a0a0a)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-trophy"></i> Live implementation proof</div>
			<h1>Customer results across live tenants</h1>
			<p class="lead">Each tenant below shows an animated industry-themed storefront hero and styled control panel preview — plus direct links to production storefront and <code>/cp/</code>.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/contact">Talk to onboarding</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Start 3-day demo</a>
			</div>
		</div>
	</div>
</div>
<?php echo epc_ecomae_platform_tenant_showcase_section('page'); ?>
<div class="epm-wrap">
	<div class="epm-highlight">
		<h3>Need your tenant listed here next?</h3>
		<p style="margin-bottom:14px;color:var(--epm-muted)">Onboard your tenant and go live with storefront + portal + ERP-ready stack through ECOM AE deployment workflows.</p>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_auto_price_ai()
{
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	$superCp = epc_ecomae_platform_super_cp_url();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_guide_shared.php';
	ob_start();
	?>
<link rel="stylesheet" href="/content/general_pages/epc_auto_price_engine_css.php?v=apai-marketing">
<style>
.epm-apai--marketing .epc-ape-guide__flow { max-width: 100%; }
.epm-apai--marketing .epc-ape-guide__diagram { margin: 24px 0; }
.epm-apai__step-num { display: inline-flex; align-items: center; justify-content: center; width: 36px; height: 36px; border-radius: 50%; background: linear-gradient(135deg, #0d9488, #14b8a6); color: #fff; font-weight: 700; font-size: 16px; margin-bottom: 10px; }
.epm-apai__profiles .epm-card h4 { margin-top: 0; }
@media (max-width: 640px) {
	.epm-apai--marketing .epc-ape-guide__flow-step { flex: 1 1 45%; min-width: 120px; }
	.epm-apai--marketing .epc-ape-guide__legend-card { flex: 1 1 100%; }
}
</style>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#0f766e 0%,#115e59 45%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-magic"></i> Discover · Price · Import · Sell</div>
			<h1>Auto Price AI — discover, price, and list across markets</h1>
			<p class="lead">Multi-source price intelligence built into ECOM AE. Crawl buy sources and sell marketplaces in your country, find real margin, import winners to your catalogue, and cross-list on Noon, Amazon, or eBay — for auto parts, electronics, fashion, jewellery, and professional services.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> Start <?php echo (int) $demo['days']; ?>-day demo</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($superCp); ?>control/portal/epc_auto_price_engine?site_key=electronicae&amp;tab=guide" target="_blank" rel="noopener"><i class="fa fa-book"></i> Open CP guide</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/contact"><i class="fa fa-envelope"></i> Contact sales</a>
			</div>
		</div>
	</div>

	<?php
	epc_apai_guide_render(array(
		'mode' => 'marketing',
		'profile' => 'marketplace_arbitrage',
		'industry_key' => 'electronics',
		'country_code' => 'AE',
		'country_label' => 'United Arab Emirates',
		'margin_pct' => '12',
	));
	?>

	<div class="epm-highlight">
		<h3>Ready to discover margin in your market?</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Auto Price AI ships with every commerce tenant on ECOM AE. Start a demo, open the operator guide in Super CP, or contact us to enable it on your live stack.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-rocket"></i> Book demo</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($superCp); ?>control/portal/epc_auto_price_engine" target="_blank" rel="noopener"><i class="fa fa-cloud"></i> Super CP engine</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/capabilities?category=<?php echo rawurlencode('AI & automation'); ?>"><i class="fa fa-th"></i> All AI capabilities</a>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_faq()
{
	return epc_ecomae_faq_render_page();
}

function epc_ecomae_platform_page_api_documentation()
{
	$base = epc_ecomae_platform_base_url();
	$demo = epc_ecomae_platform_demo_package();
	$endpoints = array(
		array('GET', '/epc-api/v1/health', 'None', 'Platform API status and endpoint list'),
		array('GET', '/epc-api/v1/capabilities', 'None', 'Capability area summary from marketing catalog'),
		array('GET', '/epc-api/v1/openapi.json', 'None', 'Machine-readable OpenAPI 3 spec'),
		array('GET', '/epc-api/v1/tenant/info', 'X-API-Key', 'Tenant name, industry, access mode'),
		array('GET', '/epc-api/v1/orders', 'X-API-Key', 'Recent shop orders (limit 20)'),
		array('GET', '/epc-api/v1/products/search?q=', 'X-API-Key', 'Search published catalogue products'),
		array('GET', '/epc-api/v1/erp/dashboard-summary', 'X-API-Key', 'ERP KPI summary (read-only)'),
	);
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1e1b4b 0%,#7c3aed 45%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-code"></i> Phase 1 — read-only</div>
			<h1>API documentation</h1>
			<p class="lead">Integrate your ERP, BI, or middleware with ECOM AE tenant data via JSON REST. Phase 1 covers health checks, catalogue search, orders, and ERP KPIs — authenticated per tenant with API keys issued from Super CP.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>epc-api/v1/openapi.json" target="_blank" rel="noopener"><i class="fa fa-file-code-o"></i> OpenAPI JSON</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>epc-api/v1/health" target="_blank" rel="noopener"><i class="fa fa-heartbeat"></i> API health</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demo['days']; ?>-day demo</a>
			</div>
		</div>
	</div>

	<h2 class="epm-section-title">Why integrate?</h2>
	<p class="epm-section-lead">Connect warehouse systems, accounting exports, partner portals, or custom dashboards without scraping the storefront. Each tenant receives isolated API keys scoped to their MySQL database — orders and ERP figures never cross tenants.</p>

	<h2 class="epm-section-title">Phase 1 endpoints</h2>
	<div class="epm-card" style="overflow-x:auto;padding:0">
		<table style="width:100%;border-collapse:collapse;font-size:14px">
			<thead>
				<tr style="background:#171717;color:#e2e8f0">
					<th style="padding:12px;text-align:left">Method</th>
					<th style="padding:12px;text-align:left">Path</th>
					<th style="padding:12px;text-align:left">Auth</th>
					<th style="padding:12px;text-align:left">Description</th>
				</tr>
			</thead>
			<tbody>
				<?php foreach ($endpoints as $ep) { ?>
				<tr>
					<td style="padding:10px 12px;border-bottom:1px solid #e2e8f0"><code><?php echo epc_ecomae_h($ep[0]); ?></code></td>
					<td style="padding:10px 12px;border-bottom:1px solid #e2e8f0"><code><?php echo epc_ecomae_h($ep[1]); ?></code></td>
					<td style="padding:10px 12px;border-bottom:1px solid #e2e8f0"><?php echo epc_ecomae_h($ep[2]); ?></td>
					<td style="padding:10px 12px;border-bottom:1px solid #e2e8f0"><?php echo epc_ecomae_h($ep[3]); ?></td>
				</tr>
				<?php } ?>
			</tbody>
		</table>
	</div>

	<h2 class="epm-section-title">Authentication</h2>
	<div class="epm-split">
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-key text-primary"></i> API key header</h4>
			<ol class="epm-steps">
				<li>Request enterprise API access via <a href="<?php echo epc_ecomae_h($base); ?>platform/contact" style="color:var(--epm-cyan)">contact</a> or your <?php echo (int) $demo['days']; ?>-day demo onboarding.</li>
				<li>Platform operator issues a tenant-scoped key from Super CP (stored as SHA-256 hash only).</li>
				<li>Send the key on every authenticated request: <code>X-API-Key: your_key_here</code></li>
				<li>Keys include read scopes: tenant info, orders, products, ERP dashboard.</li>
			</ol>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-terminal text-primary"></i> Example curl (safe placeholders)</h4>
			<pre style="background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:0"># Public health — no key
curl -s https://www.ecomae.com/epc-api/v1/health

# Tenant info — replace placeholder
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX" \
  https://www.ecomae.com/epc-api/v1/tenant/info

# Product search
curl -s -H "X-API-Key: epc_YOUR_TENANT_read_XXXXXXXXXXXXXXXX" \
  "https://www.ecomae.com/epc-api/v1/products/search?q=brake"</pre>
			<p style="font-size:13px;color:var(--epm-muted);margin-top:10px">Never commit real keys to source control or public pages.</p>
		</div>
	</div>

	<h2 class="epm-section-title">Existing integrations scope</h2>
	<div class="epm-grid">
		<div class="epm-card">
			<h4><i class="fa fa-globe text-primary"></i> Public REST API (Phase 1)</h4>
			<p>JSON at <code>/epc-api/v1/</code> — designed for external systems, BI, and partner integrations with API keys.</p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-lock text-primary"></i> ERP ajax (internal)</h4>
			<p>CP session endpoints under <code>/cp/shop/finance/erp/ajax_*</code> require staff login — not exposed as public API.</p>
		</div>
	</div>

	<h2 class="epm-section-title">Future scope</h2>
	<ul class="epm-feature-list">
		<li>Webhooks — order placed, stock low, lead captured</li>
		<li>E-invoice submit — Peppol / PINT-AE outbound</li>
		<li>Microsoft Dynamics 365 sync</li>
		<li>Marketplace channel write-back</li>
		<li>Write APIs v2 — create orders, update stock, post invoices</li>
	</ul>

	<div class="epm-highlight">
		<h3>Need API keys for production?</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Enterprise tenants receive scoped keys after onboarding. Start with a <?php echo (int) $demo['days']; ?>-day demo or contact sales for API access on your live tenant.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact for API keys</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Book demo</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>epc-api/v1/openapi.json" target="_blank" rel="noopener">Download OpenAPI</a>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_page_api_services(array $params = array())
{
	$base = epc_ecomae_platform_base_url();
	$focus = (string) ($params['focus'] ?? 'overview');
	$catalogActions = array(
		array('manufacturers', 'List vehicle manufacturers (passenger, commercial, motorbike)'),
		array('models', 'Models for a manufacturer'),
		array('modifications', 'Engine / trim variants for a model'),
		array('categories', 'Part categories for a vehicle modification'),
		array('products', 'Products in a category'),
		array('articles', 'Articles in a category'),
		array('article', 'Single article detail by brand + number'),
		array('analogs', 'Cross-reference / analog parts'),
		array('brands', 'Aftermarket brands list'),
		array('vin', 'VIN decode → vehicle + applicable catalog tree'),
		array('engines', 'Engine code lookup'),
		array('engine_search', 'Search engines by code fragment'),
		array('status', 'Connection status and daily quota usage'),
	);
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#5a0f16 0%,#8a131c 40%,#0a0a0a 100%)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-plug"></i> For your own website — not a tenant</div>
			<h1>Autoparts Catalog &amp; Price PRO API</h1>
			<p class="lead">Offer vehicle spare-parts lookup on <strong>your</strong> site without renting a full ECOM AE storefront or ERP. Choose <strong>Catalog API</strong> (vehicle &amp; parts catalog data), <strong>Price PRO API</strong> (live supplier price &amp; availability), or both — we issue client keys and document integration for your developers.</p>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/contact"><i class="fa fa-envelope"></i> Request API access</a>
				<a class="epm-btn epm-btn--ghost" href="#catalog-api"><i class="fa fa-car"></i> Catalog API</a>
				<a class="epm-btn epm-btn--outline" href="#price-pro-api"><i class="fa fa-tags"></i> Price PRO</a>
			</div>
		</div>
	</div>

	<h2 class="epm-section-title">Who this is for</h2>
	<p class="epm-section-lead">These products are for <strong>integration partners</strong> — independent websites, marketplaces, garage portals, and apps that need catalog or price data only. They are <em>not</em> the same as a monthly tenant (storefront + CP + ERP) or ERP-only client access.</p>
	<div class="epm-grid">
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-book text-primary"></i> Catalog customer</h4>
			<p>JSON REST for manufacturers → models → categories → articles, VIN decode, engine search, analogs. Powered by the Epart vehicle catalog service. Embed in your UX with your branding.</p>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-line-chart text-primary"></i> Price PRO customer</h4>
			<p>Article + brand → supplier offers, stock hints, and list prices from connected price feeds. For B2B portals and comparison widgets on third-party sites.</p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-building text-primary"></i> Not a tenant</h4>
			<p>No CP login, no shop checkout, no full ERP. Need orders, inventory, and UAE e-invoice? See <a href="<?php echo epc_ecomae_h($base); ?>platform/pricing">tenant plans</a> or <a href="<?php echo epc_ecomae_h($base); ?>platform/api-documentation">Tenant ERP API</a>.</p>
		</div>
	</div>

	<h2 class="epm-section-title" id="catalog-api">Autoparts Catalog API</h2>
	<p class="epm-section-lead">Vehicle-aware spare parts catalog (Epart catalog API). Your server or front-end calls our gateway; we handle upstream catalog keys, caching, and daily quotas.</p>
	<div class="epm-card" style="overflow-x:auto;padding:0;margin-bottom:20px">
		<table style="width:100%;border-collapse:collapse;font-size:14px">
			<thead>
				<tr style="background:#171717;color:#e2e8f0">
					<th style="padding:12px;text-align:left">Query param <code>action</code></th>
					<th style="padding:12px;text-align:left">Purpose</th>
				</tr>
			</thead>
			<tbody>
				<?php foreach ($catalogActions as $row) { ?>
				<tr>
					<td style="padding:10px 12px;border-bottom:1px solid #e2e8f0"><code><?php echo epc_ecomae_h($row[0]); ?></code></td>
					<td style="padding:10px 12px;border-bottom:1px solid #e2e8f0"><?php echo epc_ecomae_h($row[1]); ?></td>
				</tr>
				<?php } ?>
			</tbody>
		</table>
	</div>
	<div class="epm-split">
		<div class="epm-card">
			<h4><i class="fa fa-link text-primary"></i> Base URL (keys issued on onboarding)</h4>
			<p>After onboarding you receive an <code>X-API-Key</code> (prefix <code>epc_catalog_</code>…) and daily quota. Public entry point:</p>
			<pre style="background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:12px 0 0">GET https://www.ecomae.com/api/v1/catalog.php?action=manufacturers&amp;section=passenger
Header: X-API-Key: epc_catalog_YOUR_CLIENT_KEY</pre>
			<p style="font-size:13px;color:var(--epm-muted);margin-top:10px">Hosted ECOM AE storefronts use the built-in catalog widget on your domain. Third-party sites and apps should call <code>/api/v1/catalog.php</code> only.</p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-terminal text-primary"></i> Example integration flow</h4>
			<ol class="epm-steps">
				<li>User picks manufacturer → your site calls <code>action=models&amp;manufacturer_id=…</code></li>
				<li>User picks model → <code>action=modifications</code></li>
				<li>User browses category tree → <code>categories</code>, then <code>articles</code> or <code>products</code></li>
				<li>Optional VIN tab → <code>action=vin&amp;vin=…</code> or engine code → <code>engine_search</code></li>
			</ol>
		</div>
	</div>
	<pre style="background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:16px 0"># Manufacturers (passenger cars) — replace YOUR_CLIENT_KEY after onboarding
curl -s -H "X-API-Key: epc_catalog_YOUR_CLIENT_KEY" \
  "https://www.ecomae.com/api/v1/catalog.php?action=manufacturers&amp;section=passenger"

# VIN decode
curl -s -H "X-API-Key: epc_catalog_YOUR_CLIENT_KEY" \
  "https://www.ecomae.com/api/v1/catalog.php?action=vin&amp;vin=WBAXG1103CDW29096"

# Quota / status (includes api_client.remaining when authenticated)
curl -s -H "X-API-Key: epc_catalog_YOUR_CLIENT_KEY" \
  "https://www.ecomae.com/api/v1/catalog.php?action=status"</pre>

	<h2 class="epm-section-title" id="price-pro-api">Spare Parts Price PRO API</h2>
	<p class="epm-section-lead">Look up supplier list prices and availability for an article on a brand — designed for B2B widgets, garage systems, and marketplaces. Requires a <strong>Price PRO</strong> client key (separate from Catalog-only).</p>
	<div class="epm-split">
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-cogs text-primary"></i> Price PRO endpoint (beta)</h4>
			<pre style="background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:0">GET https://www.ecomae.com/api/v1/price/lookup.php?brand=BOSCH&amp;article=0986424590
Header: X-API-Key: epc_pricepro_YOUR_CLIENT_KEY

Response: { "ok": true, "beta": true, "offers": [ { "supplier", "price", "currency", "stock_hint" } ] }</pre>
			<p style="font-size:13px;color:var(--epm-muted);margin-top:10px">Price PRO keys are issued on onboarding. Empty <code>offers</code> with a beta message means supplier feeds are not enabled yet — contact sales for enablement.</p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-info-circle text-primary"></i> vs Catalog API</h4>
			<ul class="epm-feature-list">
				<li><strong>Catalog</strong> — vehicle tree, OEM categories, article metadata, VIN, analogs</li>
				<li><strong>Price PRO</strong> — commercial offers from connected supplier price files</li>
				<li>Many clients start with Catalog only; add Price PRO when ready to show live buy prices</li>
			</ul>
		</div>
	</div>
	<pre style="background:#171717;color:#e2e8f0;padding:14px;border-radius:8px;font-size:12px;overflow-x:auto;margin:16px 0"># Price PRO — supplier offers for brand + article
curl -s -H "X-API-Key: epc_pricepro_YOUR_CLIENT_KEY" \
  "https://www.ecomae.com/api/v1/price/lookup.php?brand=BOSCH&amp;article=0986424590"</pre>

	<h2 class="epm-section-title">How integration works</h2>
	<div class="epm-grid">
		<div class="epm-card">
			<h4>1. Choose product</h4>
			<p>Catalog customer, Price PRO customer, or bundled. No tenant storefront required.</p>
		</div>
		<div class="epm-card">
			<h4>2. Receive credentials</h4>
			<p>We issue <code>X-API-Key</code>, daily quota, and allowed actions. Keys are hashed server-side; rotate on request.</p>
		</div>
		<div class="epm-card">
			<h4>3. Build on your site</h4>
			<p>Call JSON REST from your backend (recommended) or SPA with a thin proxy — never expose upstream catalog API keys in browser code.</p>
		</div>
		<div class="epm-card">
			<h4>4. Go live</h4>
			<p>Usage metering and support via ECOM AE. Upgrade to full tenant later if you need shop, CP, and ERP.</p>
		</div>
	</div>

	<h2 class="epm-section-title">Related products</h2>
	<div class="epm-split">
		<div class="epm-card">
			<h4><i class="fa fa-code text-primary"></i> Tenant ERP API</h4>
			<p>For existing ECOM AE tenants: orders, shop products, ERP KPIs at <code>/epc-api/v1/</code>.</p>
			<p><a href="<?php echo epc_ecomae_h($base); ?>platform/api-documentation">Tenant API documentation →</a></p>
		</div>
		<div class="epm-card">
			<h4><i class="fa fa-shopping-cart text-primary"></i> Full storefront tenant</h4>
			<p>Hosted shop + CP + ERP on your domain — see <a href="<?php echo epc_ecomae_h($base); ?>platform/pricing">monthly plans</a>.</p>
		</div>
	</div>

	<div class="epm-highlight">
		<h3>Start as a Catalog or Price PRO client</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Tell us your use case (website URL, expected daily calls, catalog vs price). We enable sandbox access and integration support.</p>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact for API keys</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Book platform demo</a>
		</div>
	</div>
</div>
	<?php if ($focus === 'catalog') { ?>
<script>document.addEventListener('DOMContentLoaded',function(){var el=document.getElementById('catalog-api');if(el)el.scrollIntoView({behavior:'smooth'});});</script>
	<?php } elseif ($focus === 'price_pro') { ?>
<script>document.addEventListener('DOMContentLoaded',function(){var el=document.getElementById('price-pro-api');if(el)el.scrollIntoView({behavior:'smooth'});});</script>
	<?php } ?>
	<?php
	return ob_get_clean();
}
