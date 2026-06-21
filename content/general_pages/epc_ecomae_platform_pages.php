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
	$mktSlugPages = array('docs', 'compare', 'bos', 'solution');
	if (in_array($page, $mktSlugPages, true)) {
		$meta = epc_ecomae_marketing_meta($page, $params);
		if ($meta) { $title = $meta[0]; $description = $meta[1]; }
		$segMap = array('solution' => 'solutions', 'docs' => 'documentation');
		$seg = $segMap[$page] ?? $page;
		$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
		$canonicalPath = '/' . $seg . ($slug !== '' ? '/' . $slug : '');
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
			<div class="epm-badge">Business Operating System · complete overview</div>
			<h1>Everything in the ECOM AE BOS — by area</h1>
			<p class="lead">One Business Operating System: <strong>ERP + commerce + compliance + workflows + CRM + industry intelligence</strong> — go live in <strong>24 hours</strong> with <strong>e-invoice</strong> built in. Below: what the storefront, CP, and Super CP deliver per capability.</p>
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
	ob_start();
	?>
<div class="epm-wrap">
	<div class="epm-hero">
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407,#5a0f16 50%,#0a0a0a)"></div>
		<div class="epm-hero__content">
			<div class="epm-badge"><i class="fa fa-industry"></i> <?php echo count($industries); ?> verticals</div>
			<h1>Every industry, its own solution page</h1>
			<p class="lead">Each template ships with a themed storefront, CP module packs, and workflows specific to that sector — not a one-size-fits-all catalogue.</p>
		</div>
	</div>

	<?php foreach ($industryGroups as $grp) {
		if (empty($grp['industries'])) {
			continue;
		}
		?>
	<h2 class="epm-section-title" style="font-size:24px;margin-top:28px"><?php echo epc_ecomae_h($grp['name']); ?></h2>
	<div class="epm-grid">
		<?php foreach ($grp['industries'] as $code => $ind) {
			$summary = isset($details[$code]['summary']) ? $details[$code]['summary'] : $ind['tagline'];
			?>
		<a class="epm-card epm-card--photo" href="<?php echo epc_ecomae_h($base); ?>platform/industry/<?php echo epc_ecomae_h($code); ?>">
			<img src="<?php echo epc_ecomae_h($ind['photo']); ?>" alt="<?php echo epc_ecomae_h($ind['name']); ?>" loading="lazy" />
			<div class="epm-card__inner">
				<h4><i class="fa <?php echo epc_ecomae_h($ind['icon']); ?> text-primary"></i> <?php echo epc_ecomae_h($ind['name']); ?></h4>
				<p><?php echo epc_ecomae_h($summary); ?></p>
				<span class="epm-pill">Dedicated page →</span>
			</div>
		</a>
		<?php } ?>
	</div>
	<?php } ?>

	<div class="epm-highlight">
		<h3>Not sure which template fits?</h3>
		<p style="color:var(--epm-muted);margin-bottom:14px">Start a <?php echo (int) $demo['days']; ?>-day demo in any vertical — we load sample data and CP modules so you can show the client a realistic sandbox.</p>
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo">Book a demo</a>
	</div>
</div>
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
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo?industry=<?php echo epc_ecomae_h($code); ?>"><i class="fa fa-play-circle"></i> <?php echo (int) $demo['days']; ?>-day demo</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h(epc_ecomae_platform_onboard_url($code)); ?>">Onboard in Super CP</a>
			</div>
		</div>
	</div>
	<div class="epm-industry-accent"></div>

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
		<div class="epm-hero__shade" style="opacity:1;background:linear-gradient(135deg,#1a0407,#b01722 60%,#0a0a0a)"></div>
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
	<p style="color:var(--epm-muted);font-size:14px;margin-top:20px">All plans include SSL, tenant isolation, country-driven compliance, unlimited users and BOS onboarding tools. Prices are indicative — final commercial terms per client contract.</p>
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
