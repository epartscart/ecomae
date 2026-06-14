<?php
/**
 * ecomae.com marketing — shared layout, logo, dark ERP cloud theme.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_tenant_showcase.php';

function epc_ecomae_platform_logo_url()
{
	// Served by a .php endpoint (always served on every host) because the
	// static PNG asset is not present in the docroot.
	return '/content/general_pages/epc_ecomae_logo_svg.php';
}

function epc_ecomae_platform_pack_label($code)
{
	static $labels = null;
	if ($labels === null) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
		$labels = epc_portal_pack_definitions();
		$labels['tax_advisory'] = array(
			'label' => 'Tax & advisory',
			'desc' => 'Client hub, VAT workflows, advisory CRM',
			'icon' => 'fa-balance-scale',
		);
	}
	return isset($labels[$code]) ? $labels[$code] : array(
		'label' => ucfirst(str_replace('_', ' ', $code)),
		'desc' => '',
		'icon' => 'fa-cube',
	);
}

function epc_ecomae_platform_head_html($title, $canonicalPath = '/', $description = '')
{
	$logo = epc_ecomae_h(epc_ecomae_platform_logo_url());
	$t = epc_ecomae_h($title);
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$canonical = epc_ecomae_h($base . ($canonicalPath === '/' ? '/' : $canonicalPath));
	$desc = epc_ecomae_h($description !== '' ? $description : 'ECOM AE — hosted e-commerce, ERP, and CRM cloud for UAE businesses. Multi-tenant storefronts, Super CP (operator control panel), and Peppol e-invoicing.');
	$ogImg = epc_ecomae_h($base . '/epc-static.php?f=content/general_pages/marketing_screens/og_cover.png');
	$ogAlt = epc_ecomae_h('ECOM AE — One Business Operating System: ERP, commerce, compliance, workflows and CRM.');
	return '<!DOCTYPE html><html lang="en"><head><!-- ECOMAE-MARKETING-HOME-v8 -->'
		. '<meta charset="utf-8">'
		. '<meta name="viewport" content="width=device-width,initial-scale=1">'
		. '<meta name="theme-color" content="#080b14">'
		. '<title>' . $t . '</title>'
		. '<meta name="description" content="' . $desc . '">'
		. '<meta name="robots" content="index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1">'
		. '<meta name="author" content="ECOM AE">'
		. '<link rel="canonical" href="' . $canonical . '">'
		. '<meta property="og:type" content="website">'
		. '<meta property="og:site_name" content="ECOM AE">'
		. '<meta property="og:locale" content="en_US">'
		. '<meta property="og:title" content="' . $t . '">'
		. '<meta property="og:description" content="' . $desc . '">'
		. '<meta property="og:url" content="' . $canonical . '">'
		. '<meta property="og:image" content="' . $ogImg . '">'
		. '<meta property="og:image:secure_url" content="' . $ogImg . '">'
		. '<meta property="og:image:type" content="image/png">'
		. '<meta property="og:image:width" content="1200">'
		. '<meta property="og:image:height" content="630">'
		. '<meta property="og:image:alt" content="' . $ogAlt . '">'
		. '<meta name="twitter:card" content="summary_large_image">'
		. '<meta name="twitter:title" content="' . $t . '">'
		. '<meta name="twitter:description" content="' . $desc . '">'
		. '<meta name="twitter:image" content="' . $ogImg . '">'
		. '<meta name="twitter:image:alt" content="' . $ogAlt . '">'
		. '<script type="application/ld+json">' . json_encode(array(
			'@context' => 'https://schema.org',
			'@graph' => array(
				array(
					'@type' => 'Organization',
					'name' => 'ECOM AE',
					'url' => $base,
					'logo' => $ogImg,
					'address' => array('@type' => 'PostalAddress', 'addressLocality' => 'Dubai', 'addressCountry' => 'AE'),
					'parentOrganization' => array('@type' => 'Organization', 'name' => 'Electronic World Group'),
				),
				array(
					'@type' => 'WebSite',
					'name' => 'ECOM AE',
					'url' => $base . '/',
					'potentialAction' => array(
						'@type' => 'SearchAction',
						'target' => $base . '/platform?q={search_term_string}',
						'query-input' => 'required name=search_term_string',
					),
				),
				array(
					'@type' => 'SoftwareApplication',
					'name' => 'ECOM AE Cloud',
					'applicationCategory' => 'BusinessApplication',
					'operatingSystem' => 'Web',
					'offers' => array('@type' => 'Offer', 'priceCurrency' => 'AED'),
				),
			),
		), JSON_UNESCAPED_SLASHES) . '</script>'
		. '<link rel="preconnect" href="https://fonts.googleapis.com" />'
		. '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />'
		. '<link href="https://fonts.googleapis.com/css2?family=Syne:wght@700;800&display=swap" rel="stylesheet" />'
		. '<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css" />'
		. epc_ecomae_platform_styles()
		. '</head><body class="epm-body">';
}

function epc_ecomae_platform_page_description($page, array $params = array()): string
{
	$descriptions = array(
		'home' => 'ECOM AE — the multi-tenant Business Operating System (BOS) combining ERP, commerce, compliance, workflows and industry-specific operational intelligence for organizations across industries worldwide.',
		'platform' => 'Explore the ECOM AE Business Operating System (BOS): ERP, commerce, compliance, workflows, CRM and industry intelligence on one multi-tenant cloud.',
		'capabilities' => 'ECOM AE BOS capabilities — ERP, commerce, compliance, workflow automation, industry intelligence, and operator Super CP for hosted businesses.',
		'auto_price_ai' => 'Auto Price AI — discover, compare, and import products across market sources with margin rules and catalogue sync.',
		'faq' => '105 honest answers on automotive catalog, B2B, supply chain, UAE ERP, AI, infrastructure, and licensing.',
		'pricing' => 'Transparent monthly rental plans for ECOM AE cloud — e-commerce, ERP, and CRM for UAE businesses.',
		'demo' => 'Request a 3-day industry demo tenant on ECOM AE — explore storefront, ERP, and Super CP workflows.',
		'contact' => 'Contact ECOM AE for platform demos, tenant onboarding, and ERP cloud consultations in UAE.',
		'industries' => 'Industry-specific ECOM AE solutions — auto parts, retail, electronics, fashion, and more.',
		'free_tools' => 'Free, country-driven business tools — VAT/GST return, corporate tax, payroll & gratuity, IFRS financials, e-invoice and approval workflow. Register free and use for your own company.',
	);
	if ($page === 'industry') {
		$industries = epc_ecomae_platform_industry_marketing();
		$code = (string) ($params['code'] ?? '');
		if (isset($industries[$code]['tagline'])) {
			return (string) $industries[$code]['tagline'];
		}
	}
	if ($page === 'free_tools') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_free_tools.php';
		$seo = epc_free_tools_seo((string) ($params['tool'] ?? ''));
		return $seo['description'];
	}
	return $descriptions[$page] ?? 'ECOM AE cloud platform for e-commerce, ERP, and CRM in UAE.';
}

function epc_ecomae_platform_layout_open($active = '')
{
	$nav = epc_ecomae_platform_nav();
	$superCp = epc_ecomae_platform_super_cp_url();
	$logoUrl = epc_ecomae_platform_logo_url();
	ob_start();
	?>
<header class="epm-topbar">
	<div class="epm-topbar__inner">
		<a class="epm-brand" href="<?php echo epc_ecomae_h(epc_ecomae_platform_base_url()); ?>">
			<img class="epm-brand__logo" src="<?php echo epc_ecomae_h($logoUrl); ?>" alt="" width="48" height="48" />
			<span class="epm-brand__text">
				<strong>ECOM <span class="epm-brand__ae">AE</span></strong>
				<small>One cloud: E-commerce + ERP + CRM</small>
			</span>
		</a>
		<button type="button" class="epm-nav-toggle" id="epm_nav_toggle" aria-expanded="false" aria-controls="epm_nav_drawer" aria-label="Open menu">
			<span></span><span></span><span></span>
		</button>
		<div class="epm-nav-drawer" id="epm_nav_drawer">
			<nav class="epm-nav">
				<?php foreach ($nav as $item) {
					$href = (string) $item['href'];
					$key = (string) ($item['key'] ?? '');
					$isActive = ($active !== '' && $key === $active) || ($active === '' && $key === 'home');
					?>
				<a class="epm-nav__link<?php echo $isActive ? ' is-active' : ''; ?>" href="<?php echo epc_ecomae_h($href); ?>"><?php echo epc_ecomae_h($item['label']); ?></a>
				<?php }
				foreach (epc_ecomae_platform_nav_dropdowns() as $drop) {
					$groupActive = false;
					foreach ($drop['items'] as $sub) {
						if ($active !== '' && (string) ($sub['key'] ?? '') === $active) { $groupActive = true; break; }
					}
					?>
				<div class="epm-nav__group">
					<button type="button" class="epm-nav__group-trigger<?php echo $groupActive ? ' is-active' : ''; ?>" aria-expanded="false"><?php echo epc_ecomae_h($drop['label']); ?><span class="epm-nav__caret" aria-hidden="true">&#9662;</span></button>
					<div class="epm-nav__panel">
					<?php foreach ($drop['items'] as $sub) {
						$sk = (string) ($sub['key'] ?? '');
						$subActive = ($active !== '' && $sk === $active);
						?>
						<a class="epm-nav__panel-link<?php echo $subActive ? ' is-active' : ''; ?>" href="<?php echo epc_ecomae_h($sub['href']); ?>"><?php echo epc_ecomae_h($sub['label']); ?></a>
					<?php } ?>
					</div>
				</div>
				<?php } ?>
			</nav>
			<div class="epm-nav-drawer__cta">
				<a class="epm-topbar__cta epm-topbar__cta--ghost" href="<?php echo epc_ecomae_h(epc_ecomae_platform_company_erp_url()); ?>"><i class="fa fa-building"></i> Platform ERP</a>
				<a class="epm-topbar__cta epm-topbar__cta--ghost" href="<?php echo epc_ecomae_h(epc_ecomae_platform_erp_demo_url()); ?>"><i class="fa fa-line-chart"></i> Client ERP</a>
				<a class="epm-topbar__cta" href="<?php echo epc_ecomae_h($superCp); ?>" title="Super CP — operator control panel"><i class="fa fa-cloud"></i> Super CP</a>
			</div>
		</div>
		<div class="epm-topbar__cta-row">
			<a class="epm-topbar__cta epm-topbar__cta--ghost" href="<?php echo epc_ecomae_h(epc_ecomae_platform_company_erp_url()); ?>"><i class="fa fa-building"></i> Platform ERP</a>
			<a class="epm-topbar__cta epm-topbar__cta--ghost" href="<?php echo epc_ecomae_h(epc_ecomae_platform_erp_demo_url()); ?>"><i class="fa fa-line-chart"></i> Client ERP</a>
			<a class="epm-topbar__cta" href="<?php echo epc_ecomae_h($superCp); ?>" title="Super CP — operator control panel"><i class="fa fa-cloud"></i> Super CP</a>
		</div>
	</div>
</header>
<script defer>
(function(){
	var btn=document.getElementById('epm_nav_toggle'),drawer=document.getElementById('epm_nav_drawer');
	if(!btn||!drawer)return;
	btn.addEventListener('click',function(){
		var open=drawer.classList.toggle('is-open');
		btn.setAttribute('aria-expanded',open?'true':'false');
		document.body.classList.toggle('epm-nav-open',open);
	});
	drawer.querySelectorAll('a').forEach(function(a){a.addEventListener('click',function(){drawer.classList.remove('is-open');btn.setAttribute('aria-expanded','false');document.body.classList.remove('epm-nav-open');});});
	var groups=drawer.querySelectorAll('.epm-nav__group');
	groups.forEach(function(g){
		var t=g.querySelector('.epm-nav__group-trigger');
		if(!t)return;
		t.addEventListener('click',function(e){
			e.stopPropagation();
			var open=!g.classList.contains('is-open');
			groups.forEach(function(o){o.classList.remove('is-open');var ot=o.querySelector('.epm-nav__group-trigger');if(ot)ot.setAttribute('aria-expanded','false');});
			if(open){g.classList.add('is-open');t.setAttribute('aria-expanded','true');}
		});
	});
	document.addEventListener('click',function(){groups.forEach(function(o){o.classList.remove('is-open');var ot=o.querySelector('.epm-nav__group-trigger');if(ot)ot.setAttribute('aria-expanded','false');});});
})();
</script>
<main class="epm-main">
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_layout_close()
{
	$base = epc_ecomae_platform_base_url();
	ob_start();
	?>
</main>
<footer class="epm-footer">
	<div class="epm-footer__inner">
		<div class="epm-footer__brand">
			<img src="<?php echo epc_ecomae_h(epc_ecomae_platform_logo_url()); ?>" alt="" class="epm-footer__logo" />
			<p><strong>ECOM AE</strong> — storefront, CP, and ERP in one hosted stack.<br />Go live in 24 hours · UAE e-invoice (Peppol / PINT-AE).</p>
		</div>
		<div class="epm-footer__links">
			<a href="<?php echo epc_ecomae_h($base); ?>platform">Platform</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/platform-guides">Super CP guides</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/capabilities">Capabilities</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/customer-results">Customer results</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/industries">Industries</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/pricing">Pricing</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/demo">Demo</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/faq">FAQ</a>
			<a href="<?php echo epc_ecomae_h($base); ?>documentation">Documentation</a>
			<a href="<?php echo epc_ecomae_h($base); ?>compare">Compare</a>
			<a href="<?php echo epc_ecomae_h($base); ?>bos">What is a BOS</a>
			<a href="<?php echo epc_ecomae_h($base); ?>solutions">Solutions</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact</a>
		</div>
		<p class="epm-footer__copy">&copy; <?php echo date('Y'); ?> Electronic World Group · Dubai, UAE</p>
	</div>
</footer>
<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_demo_layla.php';
$demo = epc_ecomae_platform_demo_package();
$base = epc_ecomae_platform_base_url();
$showSplash = !empty($GLOBALS['epc_ecomae_layla_splash']);
echo epc_ecomae_demo_layla_styles();
if ($showSplash) {
	echo epc_ecomae_demo_layla_splash_html($demo, $base);
}
echo epc_ecomae_demo_layla_footer_widget_html($demo, $base);
if (empty($GLOBALS['epc_ecomae_layla_scripts_done'])) {
	$pref = !empty($GLOBALS['epc_ecomae_layla_pref']) ? (string) $GLOBALS['epc_ecomae_layla_pref'] : 'auto_parts';
	echo epc_ecomae_demo_layla_scripts((int) $demo['days'], $pref, true);
}
echo epc_ecomae_demo_layla_marketing_scripts((int) $demo['days'], $base, $showSplash);
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_parts_agent_widget.php')) {
	if (!isset($DP_Config)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
		$DP_Config = new DP_Config();
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_parts_agent_widget.php';
}
?>
<!-- ECOMAE-MARKETING-HOME-v4 -->
<script defer>
(function(){if(!('IntersectionObserver' in window))return;var s=document.querySelectorAll('.epm-hub,.epm-ecosystem__viz,.epm-failover-flow');if(!s.length)return;var io=new IntersectionObserver(function(es){for(var i=0;i<es.length;i++){es[i].target.classList.toggle('epm-anim-paused',!es[i].isIntersecting);}},{rootMargin:'80px',threshold:0.05});for(var j=0;j<s.length;j++){io.observe(s[j]);}})();
</script>
</body></html>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_preview_frame($imageUrl, $label, $caption, array $features = array())
{
	ob_start();
	?>
<figure class="epm-preview">
	<div class="epm-preview__browser">
		<div class="epm-preview__chrome">
			<span></span><span></span><span></span>
			<em><?php echo epc_ecomae_h($label); ?></em>
		</div>
		<div class="epm-preview__shot">
			<img src="<?php echo epc_ecomae_h($imageUrl); ?>" alt="<?php echo epc_ecomae_h($label); ?>" loading="lazy" width="800" height="500" />
		</div>
	</div>
	<figcaption>
		<strong><?php echo epc_ecomae_h($label); ?></strong>
		<p><?php echo epc_ecomae_h($caption); ?></p>
		<?php if ($features !== array()) { ?>
		<ul class="epm-preview__feats">
			<?php foreach ($features as $f) { ?>
			<li><?php echo epc_ecomae_h($f); ?></li>
			<?php } ?>
		</ul>
		<?php } ?>
	</figcaption>
</figure>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_area_section(array $area, $index = 0)
{
	$sf = $area['storefront'];
	$cp = $area['cp'];
	$flip = ($index % 2) === 1;
	$packs = isset($area['packs']) ? $area['packs'] : array();
	ob_start();
	?>
<section class="epm-area" id="<?php echo epc_ecomae_h($area['id']); ?>">
	<div class="epm-area__head">
		<span class="epm-area__icon"><i class="fa <?php echo epc_ecomae_h($area['icon']); ?>"></i></span>
		<div>
			<h2><?php echo epc_ecomae_h($area['title']); ?></h2>
			<p class="epm-area__summary"><?php echo epc_ecomae_h($area['summary']); ?></p>
			<?php if ($packs !== array()) { ?>
			<p class="epm-area__packs">
				<?php foreach ($packs as $pack) {
					$pl = epc_ecomae_platform_pack_label($pack);
					?>
				<span class="epm-pill" title="<?php echo epc_ecomae_h($pl['desc']); ?>"><i class="fa <?php echo epc_ecomae_h($pl['icon']); ?>"></i> <?php echo epc_ecomae_h($pl['label']); ?></span>
				<?php } ?>
			</p>
			<?php } ?>
			<div class="epm-cta epm-area__links">
				<?php if (!empty($area['live']['url'])) { ?>
				<a class="epm-btn epm-btn--primary epm-btn--sm" href="<?php echo epc_ecomae_h($area['live']['url']); ?>" target="_blank" rel="noopener"><?php echo epc_ecomae_h($area['live']['label']); ?> <i class="fa fa-external-link"></i></a>
				<?php } ?>
				<?php if (!empty($area['live_cp']['url'])) { ?>
				<a class="epm-btn epm-btn--outline epm-btn--sm" href="<?php echo epc_ecomae_h($area['live_cp']['url']); ?>" target="_blank" rel="noopener"><?php echo epc_ecomae_h($area['live_cp']['label']); ?></a>
				<?php } ?>
				<?php if (!empty($area['industry'])) {
					$base = epc_ecomae_platform_base_url();
					?>
				<a class="epm-btn epm-btn--ghost epm-btn--sm" href="<?php echo epc_ecomae_h($base); ?>platform/industry/<?php echo epc_ecomae_h($area['industry']); ?>">Industry page</a>
				<?php } ?>
			</div>
		</div>
	</div>
	<div class="epm-area__shots<?php echo $flip ? ' epm-area__shots--flip' : ''; ?>">
		<?php
		echo epc_ecomae_platform_preview_frame(
			$sf['image'],
			$sf['label'],
			$sf['caption'],
			isset($sf['features']) ? $sf['features'] : array()
		);
		echo epc_ecomae_platform_preview_frame(
			$cp['image'],
			$cp['label'],
			$cp['caption'],
			isset($cp['features']) ? $cp['features'] : array()
		);
		?>
	</div>
</section>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_areas_toc(array $areas)
{
	ob_start();
	?>
<nav class="epm-toc" aria-label="Platform areas">
	<p class="epm-toc__title">Jump to capability</p>
	<div class="epm-toc__links">
		<?php foreach ($areas as $a) { ?>
		<a href="#<?php echo epc_ecomae_h($a['id']); ?>"><?php echo epc_ecomae_h($a['title']); ?></a>
		<?php } ?>
	</div>
</nav>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_flow_nodes()
{
	return array(
		array('icon' => 'fa-shopping-cart', 'title' => 'Commerce', 'sub' => 'Orders & checkout', 'data' => 'Orders'),
		array('icon' => 'fa-cubes', 'title' => 'Inventory', 'sub' => 'Stock & warehouses', 'data' => 'Stock'),
		array('icon' => 'fa-users', 'title' => 'CRM', 'sub' => 'Clients & leads', 'data' => 'CRM'),
		array('icon' => 'fa-line-chart', 'title' => 'Dashboard', 'sub' => 'Live KPIs', 'data' => 'Analytics', 'featured' => true),
		array('icon' => 'fa-file-text-o', 'title' => 'Finance', 'sub' => 'GL & VAT', 'data' => 'Invoices'),
		array('icon' => 'fa-id-badge', 'title' => 'HR', 'sub' => 'Payroll', 'data' => 'Payroll'),
		array('icon' => 'fa-truck', 'title' => 'Logistics', 'sub' => 'Fulfilment', 'data' => 'Delivery'),
		array('icon' => 'fa-cloud', 'title' => 'Tenants', 'sub' => 'Super CP sync', 'data' => 'Config'),
	);
}

/**
 * SVG data-flow rings — packets orbit ECOM AE core (animateMotion).
 *
 * @param string $variant hub|compact
 */
function epc_ecomae_platform_flow_svg($variant = 'hub')
{
	$compact = ($variant === 'compact');
	$cx = $compact ? 200 : 350;
	$cy = $compact ? 200 : 350;
	$rxO = $compact ? 178 : 235;
	$ryO = $compact ? 132 : 178;
	$rxI = $compact ? 125 : 155;
	$ryI = $compact ? 92 : 117;
	$uid = $compact ? 'epfC' : 'epfH';
	$outer = 'M ' . ($cx + $rxO) . ',' . $cy . ' A ' . $rxO . ',' . $ryO . ' 0 1,1 ' . ($cx + $rxO - 0.01) . ',' . $cy;
	$inner = 'M ' . ($cx + $rxI) . ',' . $cy . ' A ' . $rxI . ',' . $ryI . ' 0 1,0 ' . ($cx + $rxI - 0.01) . ',' . $cy;
	$labels = array('Orders', 'Stock', 'VAT', 'CRM', 'GL', 'Payroll', 'DNS', 'Sync');
	ob_start();
	?>
<svg class="epm-flow-svg<?php echo $compact ? ' epm-flow-svg--compact' : ''; ?>" viewBox="0 0 <?php echo $compact ? '400 400' : '700 700'; ?>" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
	<defs>
		<linearGradient id="<?php echo $uid; ?>Grad" x1="0%" y1="0%" x2="100%" y2="0%">
			<stop offset="0%" stop-color="#0ea5e9" stop-opacity=".25"/>
			<stop offset="50%" stop-color="#22d3ee" stop-opacity="1"/>
			<stop offset="100%" stop-color="#0ea5e9" stop-opacity=".25"/>
		</linearGradient>
		<radialGradient id="<?php echo $uid; ?>Core" cx="50%" cy="50%" r="50%">
			<stop offset="0%" stop-color="#22d3ee" stop-opacity=".45"/>
			<stop offset="100%" stop-color="#22d3ee" stop-opacity="0"/>
		</radialGradient>
		<filter id="<?php echo $uid; ?>Glow"><feGaussianBlur stdDeviation="2" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
	</defs>
	<ellipse cx="<?php echo $cx; ?>" cy="<?php echo $cy; ?>" rx="<?php echo (int) ($rxO * 0.55); ?>" ry="<?php echo (int) ($ryO * 0.55); ?>" fill="url(#<?php echo $uid; ?>Core)" class="epm-flow-svg__pulse"/>
	<path id="<?php echo $uid; ?>Outer" d="<?php echo epc_ecomae_h($outer); ?>" fill="none" stroke="url(#<?php echo $uid; ?>Grad)" stroke-width="<?php echo $compact ? 2 : 3; ?>" stroke-linecap="round" class="epm-flow-svg__ring epm-flow-svg__ring--outer"/>
	<path d="<?php echo epc_ecomae_h($outer); ?>" fill="none" stroke="#22d3ee" stroke-width="1.5" stroke-linecap="round" stroke-dasharray="6 8" class="epm-flow-svg__dash"/>
	<path id="<?php echo $uid; ?>Inner" d="<?php echo epc_ecomae_h($inner); ?>" fill="none" stroke="rgba(34,211,238,.35)" stroke-width="1" stroke-dasharray="4 6" class="epm-flow-svg__ring epm-flow-svg__ring--inner"/>
	<?php
	$spokeN = $compact ? 8 : 12;
	for ($s = 0; $s < $spokeN; $s++) {
		$rad = deg2rad(270 + $s * (360 / $spokeN));
		$x2 = $cx + cos($rad) * $rxO;
		$y2 = $cy + sin($rad) * $ryO;
		?>
	<line x1="<?php echo $cx; ?>" y1="<?php echo $cy; ?>" x2="<?php echo round($x2, 1); ?>" y2="<?php echo round($y2, 1); ?>" class="epm-flow-svg__spoke" style="--spoke-delay:<?php echo $s * 0.35; ?>s"/>
		<?php
	}
	$packetCount = $compact ? 10 : 16;
	$labelsExtra = array('Sync', 'API', 'SKU', 'VAT', 'GL', 'DNS', 'ERP', 'CRM');
	$labels = array_merge($labels, $labelsExtra);
	for ($p = 0; $p < $packetCount; $p++) {
		$dur = $compact ? (4 + ($p % 5) * 0.6) : (3.2 + ($p % 6) * 0.45);
		$begin = ($p * ($compact ? 0.55 : 0.38)) . 's';
		$lbl = isset($labels[$p]) ? $labels[$p] : 'Data';
		$r = $compact ? 4.5 : 5.5;
		?>
	<g filter="url(#<?php echo $uid; ?>Glow)">
		<circle r="<?php echo $r; ?>" fill="#22d3ee" class="epm-flow-svg__packet">
			<animateMotion dur="<?php echo $dur; ?>s" repeatCount="indefinite" begin="<?php echo $begin; ?>" path="<?php echo epc_ecomae_h($outer); ?>"/>
		</circle>
		<?php if (!$compact && $p % 2 === 0) { ?>
		<text class="epm-flow-svg__label" dy="-11" font-size="9" fill="#a5f3fc" text-anchor="middle">
			<animateMotion dur="<?php echo $dur; ?>s" repeatCount="indefinite" begin="<?php echo $begin; ?>" path="<?php echo epc_ecomae_h($outer); ?>"/>
			<tspan><?php echo epc_ecomae_h($lbl); ?></tspan>
		</text>
		<?php } ?>
	</g>
		<?php
	}
	$innerCount = $compact ? 6 : 10;
	for ($p = 0; $p < $innerCount; $p++) {
		$dur = $compact ? (3.5 + $p * 0.4) : (2.8 + $p * 0.35);
		?>
	<circle r="<?php echo $compact ? 3.5 : 4.5; ?>" fill="#0ea5e9" opacity=".9" class="epm-flow-svg__packet epm-flow-svg__packet--inner">
		<animateMotion dur="<?php echo $dur; ?>s" repeatCount="indefinite" begin="<?php echo ($p * 0.32); ?>s" path="<?php echo epc_ecomae_h($inner); ?>"/>
	</circle>
		<?php
	}
	?>
	<circle cx="<?php echo $cx; ?>" cy="<?php echo $cy; ?>" r="<?php echo $compact ? 22 : 34; ?>" fill="none" stroke="#22d3ee" stroke-width="1" class="epm-flow-svg__core-ring"/>
</svg>
	<?php
	return ob_get_clean();
}

/**
 * Homepage strip — area cards linking to /platform/platform-guides sections.
 *
 * @param string $variant home|page
 */
function epc_ecomae_platform_super_cp_guides_strip($variant = 'home')
{
	$areas = epc_ecomae_platform_super_cp_guide_areas();
	$categories = epc_ecomae_platform_super_cp_capability_categories();
	$capCount = epc_ecomae_platform_super_cp_capability_count();
	$base = epc_ecomae_platform_base_url();
	$guidesUrl = $base . 'platform/platform-guides';
	$capUrl = $base . 'platform/capabilities';
	$isHome = ($variant === 'home');
	$catalog = $isHome ? epc_ecomae_platform_super_cp_capabilities_catalog() : array();
	$sectionCls = 'epm-super-cp-guides' . ($isHome ? ' epm-super-cp-guides--home' : '');
	ob_start();
	?>
<section class="<?php echo epc_ecomae_h($sectionCls); ?>" id="super-cp-guides" aria-labelledby="epm-scp-guides-title">
	<div class="epm-wrap">
		<div class="epm-super-cp-guides__head">
			<div class="epm-badge"><i class="fa fa-th-large"></i> Super CP</div>
			<h2 class="epm-section-title" id="epm-scp-guides-title" style="margin-top:8px">What you get in Super CP</h2>
			<p class="epm-section-lead" style="max-width:860px;margin-bottom:12px"><strong><?php echo (int) $capCount; ?>+ capabilities</strong> across pricing, e-invoice, fulfilment, ERP, AI, logistics, and nine industry templates — plus six guided areas for operators evaluating the platform.</p>
			<?php if ($isHome) { ?>
			<div class="epm-cap-chips epm-cap-chips--strip epm-cap-chips--filter" role="toolbar" aria-label="Filter by category">
				<button type="button" class="epm-cap-chip is-active" data-category="">All <span class="epm-cap-chip__count"><?php echo (int) $capCount; ?></span></button>
				<?php foreach ($categories as $catName => $catCount) { ?>
				<button type="button" class="epm-cap-chip" data-category="<?php echo epc_ecomae_h($catName); ?>"><?php echo epc_ecomae_h($catName); ?> <span class="epm-cap-chip__count"><?php echo (int) $catCount; ?></span></button>
				<?php } ?>
			</div>
			<?php } else { ?>
			<div class="epm-cap-chips epm-cap-chips--strip" role="list" aria-label="Capability categories">
				<?php foreach ($categories as $catName => $catCount) { ?>
				<a class="epm-cap-chip" href="<?php echo epc_ecomae_h($capUrl); ?>?category=<?php echo epc_ecomae_h(rawurlencode($catName)); ?>" role="listitem"><?php echo epc_ecomae_h($catName); ?> <span class="epm-cap-chip__count"><?php echo (int) $catCount; ?></span></a>
				<?php } ?>
			</div>
			<?php } ?>
		</div>
		<?php if ($isHome) { ?>
		<p class="epm-cap-browser__status" id="epm-scp-cap-status" aria-live="polite">Showing <?php echo (int) $capCount; ?> capabilities</p>
		<div class="epm-cap-grid epm-super-cp-guides__cap-grid" id="epm-scp-cap-grid">
			<?php foreach ($catalog as $cap) {
				$cat = isset($cap['category']) ? (string) $cap['category'] : '';
				?>
			<article class="epm-cap-card" data-category="<?php echo epc_ecomae_h($cat); ?>" data-cap-id="<?php echo epc_ecomae_h(isset($cap['id']) ? $cap['id'] : ''); ?>" tabindex="0" role="button" aria-label="View details: <?php echo epc_ecomae_h($cap['title']); ?>">
				<span class="epm-cap-card__icon"><i class="fa <?php echo epc_ecomae_h($cap['icon']); ?>"></i></span>
				<span class="epm-cap-card__badge"><?php echo epc_ecomae_h($cat); ?></span>
				<h3 class="epm-cap-card__title"><?php echo epc_ecomae_h($cap['title']); ?></h3>
				<p class="epm-cap-card__summary"><?php echo epc_ecomae_h($cap['summary']); ?></p>
			</article>
			<?php } ?>
		</div>
		<?php } else { ?>
		<div class="epm-grid epm-super-cp-guides__grid">
			<?php foreach ($areas as $area) { ?>
			<a class="epm-card epm-card--accent epm-super-cp-guides__card" href="<?php echo epc_ecomae_h($guidesUrl); ?>#<?php echo epc_ecomae_h($area['id']); ?>">
				<span class="epm-super-cp-guides__icon"><i class="fa <?php echo epc_ecomae_h($area['icon']); ?>"></i></span>
				<h4><?php echo epc_ecomae_h($area['title']); ?></h4>
				<p><?php echo epc_ecomae_h($area['tagline']); ?></p>
				<span class="epm-pill">Read area →</span>
			</a>
			<?php } ?>
		</div>
		<?php } ?>
		<div class="epm-cta epm-super-cp-guides__cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($capUrl); ?>"><i class="fa fa-th"></i> Full catalog (<?php echo (int) $capCount; ?> capabilities)</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($guidesUrl); ?>"><i class="fa fa-book"></i> Super CP guides</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform">Platform overview</a>
		</div>
	</div>
</section>
<?php if ($isHome) { ?>
<script defer>
(function(){
	var grid=document.getElementById('epm-scp-cap-grid');if(!grid)return;
	var cards=grid.querySelectorAll('.epm-cap-card');
	var chips=document.querySelectorAll('#super-cp-guides .epm-cap-chips--filter .epm-cap-chip');
	var status=document.getElementById('epm-scp-cap-status');
	var activeCat='';
	function apply(){
		var n=0;
		for(var i=0;i<cards.length;i++){
			var c=cards[i];
			var show=!activeCat||c.getAttribute('data-category')===activeCat;
			c.style.display=show?'':'none';
			if(show)n++;
		}
		if(status)status.textContent='Showing '+n+' capabilit'+(n===1?'y':'ies');
	}
	for(var j=0;j<chips.length;j++){
		chips[j].addEventListener('click',function(){
			for(var k=0;k<chips.length;k++){chips[k].classList.remove('is-active');}
			this.classList.add('is-active');
			activeCat=this.getAttribute('data-category')||'';
			apply();
		});
	}
	apply();
})();
</script>
<?php echo epc_ecomae_platform_capability_modal_shell(); ?>
<?php echo epc_ecomae_platform_capability_modal_script($catalog); ?>
<?php } ?>
	<?php
	return ob_get_clean();
}

/**
 * Searchable capability grid with category filter chips.
 */
function epc_ecomae_platform_capabilities_browser(array $catalog, array $categories, $totalCount)
{
	$prefCat = isset($_GET['category']) ? (string) $_GET['category'] : '';
	ob_start();
	?>
<section class="epm-cap-browser" id="capabilities-catalog" aria-labelledby="epm-cap-browser-title">
	<div class="epm-cap-browser__toolbar">
		<div>
			<h2 class="epm-section-title" id="epm-cap-browser-title" style="margin-top:0"><?php echo (int) $totalCount >= 90 ? '90+' : (int) $totalCount; ?> capabilities included</h2>
			<p class="epm-section-lead" style="margin-bottom:0">Filter by category or search by title — every item maps to a Super CP or client CP module pack.</p>
		</div>
		<div class="epm-cap-browser__search-wrap">
			<label class="sr-only" for="epm-cap-search">Search capabilities</label>
			<input type="search" id="epm-cap-search" class="epm-cap-browser__search" placeholder="Search by title…" autocomplete="off" />
		</div>
	</div>
	<div class="epm-cap-chips epm-cap-chips--filter" role="toolbar" aria-label="Filter by category">
		<button type="button" class="epm-cap-chip is-active" data-category="">All <span class="epm-cap-chip__count"><?php echo (int) $totalCount; ?></span></button>
		<?php foreach ($categories as $catName => $catCount) {
			$active = ($prefCat !== '' && $prefCat === $catName) ? ' is-active' : '';
			?>
		<button type="button" class="epm-cap-chip<?php echo $active; ?>" data-category="<?php echo epc_ecomae_h($catName); ?>"><?php echo epc_ecomae_h($catName); ?> <span class="epm-cap-chip__count"><?php echo (int) $catCount; ?></span></button>
		<?php } ?>
	</div>
	<p class="epm-cap-browser__status" id="epm-cap-status" aria-live="polite">Showing <?php echo (int) $totalCount; ?> capabilities</p>
	<div class="epm-cap-grid" id="epm-cap-grid">
		<?php foreach ($catalog as $cap) {
			$cat = isset($cap['category']) ? (string) $cap['category'] : '';
			?>
		<article class="epm-cap-card" data-category="<?php echo epc_ecomae_h($cat); ?>" data-title="<?php echo epc_ecomae_h(strtolower($cap['title'])); ?>" data-cap-id="<?php echo epc_ecomae_h(isset($cap['id']) ? $cap['id'] : ''); ?>" tabindex="0" role="button" aria-label="View details: <?php echo epc_ecomae_h($cap['title']); ?>">
			<span class="epm-cap-card__icon"><i class="fa <?php echo epc_ecomae_h($cap['icon']); ?>"></i></span>
			<span class="epm-cap-card__badge"><?php echo epc_ecomae_h($cat); ?></span>
			<h3 class="epm-cap-card__title"><?php echo epc_ecomae_h($cap['title']); ?></h3>
			<p class="epm-cap-card__summary"><?php echo epc_ecomae_h($cap['summary']); ?></p>
		</article>
		<?php } ?>
	</div>
</section>
<script defer>
(function(){
	var grid=document.getElementById('epm-cap-grid');if(!grid)return;
	var cards=grid.querySelectorAll('.epm-cap-card');
	var chips=document.querySelectorAll('.epm-cap-chips--filter .epm-cap-chip');
	var search=document.getElementById('epm-cap-search');
	var status=document.getElementById('epm-cap-status');
	var activeCat='<?php echo epc_ecomae_h(addslashes($prefCat)); ?>';
	function apply(){
		var q=search?search.value.trim().toLowerCase():'';
		var n=0;
		for(var i=0;i<cards.length;i++){
			var c=cards[i];
			var okCat=!activeCat||c.getAttribute('data-category')===activeCat;
			var okQ=!q||c.getAttribute('data-title').indexOf(q)!==-1;
			var show=okCat&&okQ;
			c.style.display=show?'':'none';
			if(show)n++;
		}
		if(status)status.textContent='Showing '+n+' capabilit'+(n===1?'y':'ies');
	}
	for(var j=0;j<chips.length;j++){
		chips[j].addEventListener('click',function(){
			for(var k=0;k<chips.length;k++){chips[k].classList.remove('is-active');}
			this.classList.add('is-active');
			activeCat=this.getAttribute('data-category')||'';
			apply();
		});
	}
	if(activeCat){for(var m=0;m<chips.length;m++){if((chips[m].getAttribute('data-category')||'')===activeCat){chips[m].classList.add('is-active');}else if(!chips[m].getAttribute('data-category')){chips[m].classList.remove('is-active');}}}
	if(search){search.addEventListener('input',apply);}
	apply();
})();
</script>
<?php echo epc_ecomae_platform_capability_modal_shell(); ?>
<?php echo epc_ecomae_platform_capability_modal_script($catalog); ?>
	<?php
	return ob_get_clean();
}

/**
 * Detailed guide area block on /platform/platform-guides.
 */
function epc_ecomae_platform_guide_area_section(array $area, $index = 0)
{
	$base = epc_ecomae_platform_base_url();
	$flip = ($index % 2) === 1;
	ob_start();
	?>
<section class="epm-guide-area<?php echo $flip ? ' epm-guide-area--flip' : ''; ?>" id="<?php echo epc_ecomae_h($area['id']); ?>">
	<div class="epm-guide-area__head">
		<span class="epm-area__icon"><i class="fa <?php echo epc_ecomae_h($area['icon']); ?>"></i></span>
		<div>
			<h2><?php echo epc_ecomae_h($area['title']); ?></h2>
			<p class="epm-area__summary"><?php echo epc_ecomae_h($area['summary']); ?></p>
		</div>
	</div>
	<div class="epm-split epm-guide-area__body">
		<div>
			<h3 class="epm-section-title" style="margin-top:0;font-size:18px">Customer benefits</h3>
			<ul class="epm-feature-list">
				<?php foreach ($area['benefits'] as $b) { ?>
				<li><?php echo epc_ecomae_h($b); ?></li>
				<?php } ?>
			</ul>
			<?php if (!empty($area['cta']['href'])) { ?>
			<div class="epm-cta">
				<a class="epm-btn epm-btn--primary epm-btn--sm" href="<?php echo epc_ecomae_h($area['cta']['href']); ?>"><?php echo epc_ecomae_h($area['cta']['label']); ?></a>
			</div>
			<?php } ?>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-check-circle text-primary"></i> Platform capabilities</h4>
			<ul class="epm-guide-area__caps">
				<?php foreach ($area['capabilities'] as $cap) {
					$isTheme = !empty($cap['example_url']);
					?>
				<li>
					<strong><?php echo epc_ecomae_h($cap['label']); ?></strong>
					<span><?php echo epc_ecomae_h($cap['text']); ?></span>
					<?php if ($isTheme) { ?>
					<span class="epm-guide-area__example">
						Live: <a href="<?php echo epc_ecomae_h($cap['example_url']); ?>" target="_blank" rel="noopener"><?php echo epc_ecomae_h($cap['example']); ?></a>
						<?php if (!empty($cap['industry'])) { ?>
						· <a href="<?php echo epc_ecomae_h($base); ?>platform/industry/<?php echo epc_ecomae_h($cap['industry']); ?>">Industry page</a>
						<?php } ?>
					</span>
					<?php } ?>
				</li>
				<?php } ?>
			</ul>
		</div>
	</div>
</section>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_ecosystem_strip()
{
	$nodes = epc_ecomae_platform_flow_nodes();
	ob_start();
	?>
<section class="epm-ecosystem" aria-label="Data flows across ECOM AE">
	<div class="epm-wrap">
		<p class="epm-ecosystem__eyebrow">Data circulates through one hub</p>
		<p class="epm-ecosystem__lead">Orders, stock, finance, and tenant config move between modules and your client sites — always through the ECOM AE core.</p>
		<div class="epm-ecosystem__viz">
			<?php echo epc_ecomae_platform_flow_svg('compact'); ?>
			<div class="epm-ecosystem__core-badge"><span>ECOM</span> <span class="epm-hub__ae">AE</span></div>
			<div class="epm-ecosystem__orbit-spin">
				<?php
				$i = 0;
				foreach ($nodes as $n) {
					$deg = 270 + ($i * 45);
					$i++;
					?>
				<div class="epm-ecosystem__node" style="--hub-i: <?php echo (int) $deg; ?>deg">
					<div class="epm-ecosystem__node-inner">
						<span class="epm-ecosystem__icon"><i class="fa <?php echo epc_ecomae_h($n['icon']); ?>"></i></span>
						<strong><?php echo epc_ecomae_h($n['title']); ?></strong>
						<small><?php echo epc_ecomae_h($n['sub']); ?></small>
						<em class="epm-ecosystem__data-tag"><?php echo epc_ecomae_h($n['data']); ?></em>
					</div>
				</div>
					<?php
				}
				?>
			</div>
		</div>
		<ul class="epm-ecosystem__legend">
			<li><span class="epm-ecosystem__dot"></span> Live data packets (orders, stock, VAT, sync)</li>
			<li><span class="epm-ecosystem__dot epm-ecosystem__dot--inner"></span> Config &amp; tenant pushes (Super CP → client DB)</li>
		</ul>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/**
 * Homepage / platform hero block — unified stack (e-commerce + ERP + CRM).
 *
 * @param bool $onHome When true, section sits directly under the animated hub.
 */
function epc_ecomae_platform_unified_stack_section($onHome = false)
{
	$base = epc_ecomae_platform_base_url();
	$erpDemo = epc_ecomae_platform_erp_demo_url();
	$superCp = epc_ecomae_platform_super_cp_url();
	$shotCommerce = epc_ecomae_platform_area_shot('commerce', 'storefront');
	$shotCp = epc_ecomae_platform_area_shot('commerce', 'cp');
	$shotErp = epc_ecomae_platform_area_shot('erp-finance', 'cp');
	$wrapCls = 'epm-wrap epm-unified-stack' . ($onHome ? ' epm-unified-stack--home' : '');
	ob_start();
	?>
<section class="<?php echo $wrapCls; ?>" id="unified-stack" aria-labelledby="epm-unified-title">
	<div class="epm-badge"><i class="fa fa-sitemap"></i> Core value</div>
	<h2 class="epm-section-title" id="epm-unified-title" style="margin-top:8px">One cloud: E-commerce + ERP + CRM</h2>
	<p class="epm-section-lead" style="max-width:820px">Storefront + Control Panel + ERP in one hosted stack — not separate disconnected tools. <strong>CRM is built into ERP Finance</strong> (pipeline, quotes, support tickets). Every tenant gets <code>www.client.com</code>, <code>/cp/</code>, and <code>/erp</code> on the same database.</p>
	<div class="epm-three-col epm-unified-stack__cols">
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-shopping-bag text-primary"></i> E-commerce</h4>
			<p>B2B/B2C catalogue, cart, checkout, and client login — orders flow straight into ERP.</p>
			<ul class="epm-feature-list">
				<li>Industry-themed storefront</li>
				<li>Logged-in trade accounts</li>
				<li>Same SKU &amp; stock as CP / ERP</li>
			</ul>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-th-large text-primary"></i> Control panel</h4>
			<p>Staff run orders, stock, finance, and documents at <code>/cp/</code> with role-based access.</p>
			<ul class="epm-feature-list">
				<li>Module packs per industry</li>
				<li>Procurement &amp; fulfilment</li>
				<li>Customer management + TRN</li>
			</ul>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-university text-primary"></i> ERP + CRM</h4>
			<p>GL, VAT, inventory, payroll hooks, and CRM pipeline — plus <strong>UAE e-invoice (PINT-AE)</strong> in the same ERP.</p>
			<ul class="epm-feature-list">
				<li>Finance &amp; VAT returns</li>
				<li>CRM inside ERP (not a silo)</li>
				<li>Standalone <code>/erp</code> portal option</li>
			</ul>
		</div>
	</div>
	<div class="epm-split epm-unified-stack__shots">
		<?php
		echo epc_ecomae_platform_preview_frame($shotCommerce, 'Storefront', 'Client-facing shop on their domain.', array('Catalogue & checkout', 'B2B login'));
		echo epc_ecomae_platform_preview_frame($shotCp, 'Control panel', 'Operations at /cp/ — orders, stock, users.', array('Role-based modules', 'Industry packs'));
		echo epc_ecomae_platform_preview_frame($shotErp, 'ERP Finance', 'GL, VAT, e-invoice, CRM pipeline.', array('Peppol-ready invoices', 'Client /erp portal'));
		?>
	</div>
	<div class="epm-cta">
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform"><i class="fa fa-th-list"></i> Platform overview</a>
		<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($erpDemo); ?>"><i class="fa fa-line-chart"></i> Client ERP demo</a>
		<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($superCp); ?>"><i class="fa fa-cloud"></i> Super CP</a>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/**
 * Worldwide Tax Toolkit — complete business tax, ERP hooks, one-click update.
 */
function epc_ecomae_platform_worldwide_tax_toolkit_section($onHome = false)
{
	$base = epc_ecomae_platform_base_url();
	$superCp = epc_ecomae_platform_super_cp_url();
	$platformUrl = $base . 'platform';
	$contactUrl = $base . 'platform/contact';
	$demoUrl = $base . 'platform/demo';
	$wrapCls = 'epm-wrap epm-tax-toolkit' . ($onHome ? ' epm-tax-toolkit--home' : '');
	ob_start();
	?>
<section class="<?php echo $wrapCls; ?>" id="worldwide-tax-toolkit" aria-labelledby="epm-tax-toolkit-title">
	<div class="epm-badge"><i class="fa fa-globe"></i> Finance pack</div>
	<h2 class="epm-section-title" id="epm-tax-toolkit-title" style="margin-top:8px">Worldwide Tax Toolkit — Complete Business Tax</h2>
	<p class="epm-section-lead" style="max-width:860px">Every tenant gets installable jurisdiction kits for <strong>195+ countries</strong> covering the full business tax stack — not VAT/GST alone. <strong>VAT &amp; GST</strong>, <strong>corporate income tax (CIT)</strong>, <strong>import/export &amp; customs duty</strong>, <strong>withholding tax</strong>, <strong>double taxation treaties &amp; foreign tax credits (FTC)</strong>, plus native <strong>ERP hooks</strong> for purchase inventory, sales output tax, and profit-level CIT estimates. One-click <strong>Update tax data</strong> refreshes seed rates and UAE FTA legislation. Tax resolves from <strong>tenant jurisdiction</strong> — UAE remains the default for GCC tenants.</p>
	<div class="epm-three-col">
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-map-marker text-primary"></i> Indirect + direct tax</h4>
			<p>VAT, GST, sales tax, excise, and corporate income tax with thresholds — UAE CIT 9% above AED 375k alongside 5% VAT.</p>
			<ul class="epm-feature-list">
				<li>VAT / GST / sales tax rates</li>
				<li>Corporate tax (CIT) per country</li>
				<li>Excise &amp; special levies (UAE reference)</li>
			</ul>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-ship text-primary"></i> Trade &amp; international</h4>
			<p>Import duty on landed cost, export zero-rating, reverse charge, DTT notes, and foreign tax credit eligibility for major trading nations.</p>
			<ul class="epm-feature-list">
				<li>Import duty on inventory cost</li>
				<li>Export VAT zero-rating</li>
				<li>Double taxation &amp; FTC flags</li>
			</ul>
		</div>
		<div class="epm-card epm-card--accent">
			<h4><i class="fa fa-line-chart text-primary"></i> ERP integration</h4>
			<p>Purchases recover input VAT + import duty; sales apply output tax; P&amp;L CIT estimates from tenant kit. POS uses indirect tax only.</p>
			<ul class="epm-feature-list">
				<li>PO / purchase — recoverable VAT + duty</li>
				<li>SO / invoice — output VAT/GST</li>
				<li>One-click Update in Super CP</li>
			</ul>
		</div>
	</div>
	<div class="epm-cta">
		<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($platformUrl); ?>#erp-finance"><i class="fa fa-university"></i> ERP &amp; finance</a>
		<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($superCp); ?>/control/portal/epc_tax_toolkit_manage"><i class="fa fa-balance-scale"></i> Tax Toolkit (Super CP)</a>
		<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($demoUrl); ?>"><i class="fa fa-play-circle"></i> Request demo</a>
		<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($contactUrl); ?>"><i class="fa fa-envelope"></i> Contact sales</a>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/**
 * 24-hour tenant launch + UAE e-invoice promise (marketing).
 */
function epc_ecomae_platform_go_live_24_section()
{
	$base = epc_ecomae_platform_base_url();
	$superCp = epc_ecomae_platform_super_cp_url();
	$erpDemo = epc_ecomae_platform_erp_demo_url();
	$demo = epc_ecomae_platform_demo_package();
	$superErpInv = epc_ecomae_platform_super_cp_url() . '/shop/finance/erp?area=sales&tab=invoices';
	ob_start();
	?>
<section class="epm-wrap epm-go-live" id="go-live-24-hours" aria-labelledby="epm-golive-title">
	<div class="epm-highlight epm-go-live__panel">
		<div class="epm-badge"><i class="fa fa-clock-o"></i> Fast onboarding</div>
		<h2 class="epm-section-title" id="epm-golive-title" style="margin-top:10px">ERP live within 24 hours</h2>
		<p class="epm-section-lead" style="max-width:820px;margin-bottom:18px">Register the client in <strong>Super CP</strong>, apply an industry template, point DNS — we provision an isolated tenant database, storefront, <code>/cp/</code>, and <code>/erp</code>. Most operators go from intro form to a working stack in <strong>24 hours</strong>, not weeks of separate hosting projects.</p>
		<div class="epm-promise-grid">
			<div class="epm-card">
				<h4><i class="fa fa-cloud-upload text-primary"></i> Super CP provisioning</h4>
				<p>Onboard at <a href="<?php echo epc_ecomae_h($superCp); ?>" style="color:var(--epm-cyan)">ecomae.com/cp</a> — industry pack, visual style, DNS checklist, tenant status draft → live.</p>
			</div>
			<div class="epm-card">
				<h4><i class="fa fa-file-text-o text-primary"></i> UAE e-invoice (Peppol / PINT-AE)</h4>
				<p>Tax invoices with TRN, Peppol endpoints, and PINT-AE XML/JSON — built into ERP Finance. Create from orders, validate, print, and export for FTA readiness.</p>
			</div>
			<div class="epm-card">
				<h4><i class="fa fa-play-circle text-primary"></i> Try before rental</h4>
				<p>Start with a <?php echo (int) $demo['days']; ?>-day industry demo, then convert to monthly rental — same stack, no re-platforming.</p>
			</div>
		</div>
		<ol class="epm-steps epm-go-live__steps">
			<li>Intro + industry template in Super CP</li>
			<li>Client A record → tenant routing on our cloud</li>
			<li>Storefront + CP + ERP enabled on one MySQL tenant</li>
			<li>Seller TRN &amp; e-invoice profile configured in ERP</li>
			<li>Status <strong>Live</strong> — team signs in at /cp and /erp</li>
		</ol>
		<div class="epm-cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> Start <?php echo (int) $demo['days']; ?>-day demo</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($superCp); ?>"><i class="fa fa-th-large"></i> Super CP</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($erpDemo); ?>"><i class="fa fa-line-chart"></i> Client /erp</a>
		</div>
		<p class="epm-go-live__fine" style="margin:16px 0 0;font-size:12px;color:var(--epm-muted)">Operator ERP e-invoice tab (live tenants): <a href="<?php echo epc_ecomae_h($superErpInv); ?>" style="color:var(--epm-cyan)">Finance → Invoices (e-invoice)</a> · Example client: <a href="https://www.taxofinca.com/cp/shop/finance/erp?area=sales&amp;tab=invoices" style="color:var(--epm-cyan)">taxofinca.com</a></p>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/**
 * Animated Customer → Cloud ⇄ Backup flow (marketing).
 *
 * @param string $variant home|page
 */
function epc_ecomae_platform_failover_flow_diagram($variant = 'home')
{
	$cls = 'epm-failover-flow' . ($variant === 'page' ? ' epm-failover-flow--page' : '');
	ob_start();
	?>
<figure class="<?php echo epc_ecomae_h($cls); ?>" aria-labelledby="epm-failover-flow-title" role="img">
	<figcaption id="epm-failover-flow-title" class="sr-only">Animated flow: customers use primary cloud; on outage, traffic routes to backup and syncs back when restored.</figcaption>
	<svg class="epm-failover-flow__svg" viewBox="0 0 880 300" preserveAspectRatio="xMidYMid meet" aria-hidden="true">
		<defs>
			<linearGradient id="epmFfGrad" x1="0%" y1="0%" x2="100%" y2="0%">
				<stop offset="0%" stop-color="#0ea5e9"/>
				<stop offset="50%" stop-color="#22d3ee"/>
				<stop offset="100%" stop-color="#0ea5e9"/>
			</linearGradient>
			<marker id="epmFfArrow" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto">
				<path d="M0,0 L8,4 L0,8 Z" fill="#22d3ee"/>
			</marker>
		</defs>
		<path class="epm-failover-flow__path epm-failover-flow__path--shop-cloud" d="M 168 150 L 318 150" fill="none" stroke="url(#epmFfGrad)" stroke-width="3" marker-end="url(#epmFfArrow)"/>
		<path class="epm-failover-flow__path epm-failover-flow__path--cloud-backup" d="M 562 150 L 712 150" fill="none" stroke="rgba(34,211,238,.55)" stroke-width="2.5" stroke-dasharray="8 6" marker-end="url(#epmFfArrow)"/>
		<path class="epm-failover-flow__path epm-failover-flow__path--backup-cloud" d="M 712 178 L 562 178" fill="none" stroke="rgba(14,165,233,.45)" stroke-width="2" stroke-dasharray="6 5" marker-end="url(#epmFfArrow)"/>
		<path class="epm-failover-flow__path epm-failover-flow__path--sync-back" d="M 440 198 Q 300 248 168 198" fill="none" stroke="rgba(52,211,153,.5)" stroke-width="2" stroke-dasharray="5 5" marker-end="url(#epmFfArrow)"/>
		<circle class="epm-failover-flow__packet epm-failover-flow__packet--a" r="5" fill="#22d3ee">
			<animateMotion dur="2.8s" repeatCount="indefinite" path="M 168 150 L 318 150"/>
		</circle>
		<circle class="epm-failover-flow__packet epm-failover-flow__packet--b" r="4.5" fill="#0ea5e9">
			<animateMotion dur="2.4s" repeatCount="indefinite" begin="0.6s" path="M 562 150 L 712 150"/>
		</circle>
		<circle class="epm-failover-flow__packet epm-failover-flow__packet--c" r="4" fill="#34d399">
			<animateMotion dur="3.2s" repeatCount="indefinite" begin="1.1s" path="M 712 178 L 562 178"/>
		</circle>
		<g class="epm-failover-flow__node epm-failover-flow__node--shop">
			<rect x="24" y="108" width="144" height="84" rx="14" fill="#0f172a" stroke="rgba(34,211,238,.45)" stroke-width="1.5"/>
			<text x="96" y="142" text-anchor="middle" fill="#e2e8f0" font-size="13" font-weight="700">Customer</text>
			<text x="96" y="162" text-anchor="middle" fill="#94a3b8" font-size="10">Storefront</text>
		</g>
		<g class="epm-failover-flow__node epm-failover-flow__node--cloud">
			<rect x="318" y="88" width="244" height="124" rx="16" fill="#0f172a" stroke="rgba(34,211,238,.65)" stroke-width="2"/>
			<text x="440" y="128" text-anchor="middle" fill="#fff" font-size="14" font-weight="800">Primary cloud</text>
			<text x="440" y="148" text-anchor="middle" fill="#22d3ee" font-size="10" font-weight="700" letter-spacing=".12em">ECOM AE · ALWAYS ON</text>
			<text x="440" y="168" text-anchor="middle" fill="#94a3b8" font-size="10">Store · CP · ERP</text>
		</g>
		<g class="epm-failover-flow__node epm-failover-flow__node--backup">
			<rect x="712" y="108" width="144" height="84" rx="14" fill="#0f172a" stroke="rgba(14,165,233,.5)" stroke-width="1.5"/>
			<text x="784" y="142" text-anchor="middle" fill="#e2e8f0" font-size="13" font-weight="700">Backup</text>
			<text x="784" y="162" text-anchor="middle" fill="#94a3b8" font-size="10">On-prem / mirror</text>
		</g>
		<text class="epm-failover-flow__phase-label" x="440" y="36" text-anchor="middle" fill="#22d3ee" font-size="11" font-weight="700" letter-spacing=".14em">NORMAL · DETECT · BACKUP · SYNC</text>
	</svg>
	<ol class="epm-failover-flow__steps" aria-hidden="false">
		<li class="epm-failover-flow__step epm-failover-flow__step--1"><span>1</span> Shop on cloud</li>
		<li class="epm-failover-flow__step epm-failover-flow__step--2"><span>2</span> Detect outage</li>
		<li class="epm-failover-flow__step epm-failover-flow__step--3"><span>3</span> Backup splash</li>
		<li class="epm-failover-flow__step epm-failover-flow__step--4"><span>4</span> Sync restored</li>
	</ol>
</figure>
	<?php
	return ob_get_clean();
}

/**
 * Cloud + backup continuity block (homepage strip or full page body).
 *
 * @param string $variant home|page
 */
function epc_ecomae_platform_continuity_section($variant = 'home')
{
	$bc = epc_ecomae_platform_business_continuity();
	$base = epc_ecomae_platform_base_url();
	$isHome = ($variant === 'home');
	$sectionCls = 'epm-continuity' . ($isHome ? ' epm-continuity--home' : '');
	ob_start();
	?>
<section class="<?php echo epc_ecomae_h($sectionCls); ?>" id="cloud-continuity" aria-labelledby="epm-continuity-title">
	<div class="epm-wrap">
		<div class="epm-continuity__head">
			<div>
				<div class="epm-badge"><i class="fa fa-shield"></i> <?php echo epc_ecomae_h($bc['headline']); ?></div>
				<h2 class="epm-section-title" id="epm-continuity-title" style="margin-top:8px"><?php echo epc_ecomae_h($bc['subhead']); ?></h2>
				<p class="epm-section-lead" style="max-width:820px;margin-bottom:0"><?php echo epc_ecomae_h($bc['lead']); ?></p>
			</div>
			<?php if ($isHome) { ?>
			<div class="epm-continuity__head-cta">
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/business-continuity"><i class="fa fa-long-arrow-right"></i> Full continuity story</a>
			</div>
			<?php } ?>
		</div>
		<div class="epm-continuity__viz">
			<?php echo epc_ecomae_platform_failover_flow_diagram($variant); ?>
		</div>
		<div class="epm-promise-grid epm-continuity__pillars">
			<?php foreach ($bc['pillars'] as $p) { ?>
			<div class="epm-card epm-card--accent">
				<h4><i class="fa <?php echo epc_ecomae_h($p['icon']); ?> text-primary"></i> <?php echo epc_ecomae_h($p['title']); ?></h4>
				<p><?php echo epc_ecomae_h($p['text']); ?></p>
			</div>
			<?php } ?>
		</div>
		<?php if (!$isHome) { ?>
		<div class="epm-split epm-continuity__splash">
			<div>
				<h3 class="epm-section-title" style="margin-top:0;font-size:22px">Professional splash for shoppers</h3>
				<p class="epm-section-lead">When failover is active, visitors see clear steps — not a blank error page. Modes are designed for operator control and tenant peace of mind.</p>
				<ul class="epm-feature-list">
					<?php foreach ($bc['splash_modes'] as $m) { ?>
					<li><strong><?php echo epc_ecomae_h($m['title']); ?></strong> — <?php echo epc_ecomae_h($m['detail']); ?></li>
					<?php } ?>
				</ul>
			</div>
			<div class="epm-card epm-card--accent">
				<h4><i class="fa fa-list-ol text-primary"></i> Operator journey (summary)</h4>
				<ol class="epm-steps">
					<?php foreach ($bc['flow_steps'] as $step) { ?>
					<li><strong><?php echo epc_ecomae_h($step['label']); ?></strong> — <?php echo epc_ecomae_h($step['detail']); ?></li>
					<?php } ?>
				</ol>
				<p style="margin:14px 0 0;font-size:12px;color:var(--epm-muted)">Detailed failover runbooks are available to platform operators in Super CP — not published on this marketing site.</p>
			</div>
		</div>
		<?php } ?>
		<div class="epm-cta epm-continuity__cta">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> Request demo</a>
			<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($base); ?>platform/contact"><i class="fa fa-envelope"></i> Talk to onboarding</a>
			<?php if ($isHome) { ?>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/business-continuity"><i class="fa fa-shield"></i> Business continuity</a>
			<?php } else { ?>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>"><i class="fa fa-home"></i> Home</a>
			<?php } ?>
		</div>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/**
 * Animated center hub — ECOM AE core with orbiting modules + circulating data packets.
 */
function epc_ecomae_platform_hub($base, $superCp, $demoDays = 3)
{
	$logo = epc_ecomae_platform_logo_url();
	$nodes = epc_ecomae_platform_flow_nodes();
	$continuityUrl = $base . 'platform/business-continuity';
	$platformPills = array(
		array('icon' => 'fa-clock-o', 'label' => 'Live in 24 hours'),
		array('icon' => 'fa-file-text-o', 'label' => 'Compliance · e-invoice · Peppol'),
		array('icon' => 'fa-sitemap', 'label' => 'ERP + Commerce + CRM + Workflows'),
		array('icon' => 'fa-shield', 'label' => 'Cloud + backup continuity'),
		array('icon' => 'fa-cloud-upload', 'label' => 'Super CP provisioning'),
	);
	ob_start();
	?>
<section class="epm-hub-section" aria-label="ECOM AE platform hub">
	<div class="epm-hub">
		<div class="epm-hub__matrix" aria-hidden="true">
			<?php
			$matrixChars = '01アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンVATGLERPCRM';
			$mLen = strlen($matrixChars);
			for ($c = 0; $c < 28; $c++) {
				$col = '';
				for ($k = 0; $k < 22; $k++) {
					$col .= $matrixChars[($c + $k) % $mLen];
					$col .= "\n";
				}
				$dur = 1.4 + ($c % 7) * 0.22;
				$delay = ($c * 0.11) . 's';
				?>
			<span class="epm-hub__matrix-col" style="--fall-dur:<?php echo epc_ecomae_h(number_format($dur, 2, '.', '')); ?>s;--fall-delay:<?php echo epc_ecomae_h($delay); ?>;left:<?php echo (int) (($c / 27) * 100); ?>%"><?php echo epc_ecomae_h($col); ?></span>
				<?php
			}
			?>
		</div>
		<div class="epm-hub__map" aria-hidden="true"></div>
		<div class="epm-hub__cloud" aria-hidden="true">
			<span class="epm-hub__cloud-shape"><i class="fa fa-cloud"></i></span>
			<span class="epm-hub__servers">
				<span></span><span></span><span></span>
			</span>
		</div>

		<div class="epm-hub__flow-layer" aria-hidden="true">
			<?php echo epc_ecomae_platform_flow_svg('hub'); ?>
		</div>

		<div class="epm-hub__core">
			<div class="epm-hub__core-glow" aria-hidden="true"></div>
			<div class="epm-hub__core-pulse" aria-hidden="true"></div>
			<img class="epm-hub__logo" src="<?php echo epc_ecomae_h($logo); ?>" alt="" />
			<p class="epm-hub__pill" style="margin-bottom:10px"><i class="fa fa-cubes"></i> The multi-tenant Business Operating System</p>
			<h1 class="epm-hub__headline" aria-label="Business Operating System — ERP, Commerce, Compliance, Workflows, Industry Intelligence">
				<span class="epm-hub__headline-line epm-hub__headline-line--stack epm-hub__headline-line--commerce">Business</span>
				<span class="epm-hub__headline-line epm-hub__headline-line--stack">Operating</span>
				<span class="epm-hub__headline-line epm-hub__headline-line--stack">System</span>
				<span class="epm-hub__headline-line epm-hub__headline-line--cloud">ONE BOS</span>
			</h1>
			<p class="epm-hub__tagline-sub">ERP · Commerce · Compliance · Workflows · Industry Intelligence — one cloud, <a href="<?php echo epc_ecomae_h($continuityUrl); ?>#cloud-continuity" style="color:var(--epm-cyan);text-decoration:none;border-bottom:1px dotted rgba(34,211,238,.5)">backup continuity</a> built in.</p>
			<p class="epm-hub__pill"><i class="fa fa-circle epm-hub__live-dot"></i> Data flowing · multi-tenant hub</p>
			<div class="epm-hub__cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demoDays; ?>-day demo</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($superCp); ?>"><i class="fa fa-th-large"></i> Super CP</a>
			</div>
		</div>

		<div class="epm-hub__orbit-spin">
			<?php
			$i = 0;
			foreach ($nodes as $n) {
				$deg = 270 + ($i * 45);
				$featured = !empty($n['featured']);
				$cls = 'epm-hub__node' . ($featured ? ' epm-hub__node--featured' : '');
				$delay = number_format($i * 0.09, 2, '.', '');
				$i++;
				?>
			<a class="<?php echo $cls; ?>" href="<?php echo epc_ecomae_h($base); ?>platform" style="--hub-i: <?php echo (int) $deg; ?>deg; --hub-delay: <?php echo epc_ecomae_h($delay); ?>s" title="<?php echo epc_ecomae_h($n['title'] . ' — ' . $n['data']); ?>">
				<div class="epm-hub__node-inner">
					<span class="epm-hub__node-tile"><i class="fa <?php echo epc_ecomae_h($n['icon']); ?>"></i></span>
					<strong><?php echo epc_ecomae_h($n['title']); ?></strong>
					<small><?php echo epc_ecomae_h($n['sub']); ?></small>
					<span class="epm-hub__data-chip"><?php echo epc_ecomae_h($n['data']); ?></span>
				</div>
			</a>
				<?php
			}
			?>
		</div>

		<div class="epm-hub__platform">
			<p class="epm-hub__platform-title">One Business Operating System for the whole organization</p>
			<p class="epm-hub__platform-sub">ECOM AE is a multi-tenant BOS combining ERP, commerce, compliance, workflows and industry-specific operational intelligence — across industries worldwide.</p>
			<div class="epm-hub__platform-pills">
				<?php foreach ($platformPills as $p) { ?>
				<span class="epm-hub__platform-pill"><i class="fa <?php echo epc_ecomae_h($p['icon']); ?>"></i> <?php echo epc_ecomae_h($p['label']); ?></span>
				<?php } ?>
			</div>
		</div>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/**
 * Static homepage hero — no orbit/matrix/flow animation (ecomae.com home only).
 */
function epc_ecomae_platform_static_hero($base, $superCp, $demoDays = 3)
{
	$logo = epc_ecomae_platform_logo_url();
	$continuityUrl = $base . 'platform/business-continuity';
	$platformPills = array(
		array('icon' => 'fa-clock-o', 'label' => 'Live in 24 hours'),
		array('icon' => 'fa-file-text-o', 'label' => 'Compliance · e-invoice · Peppol'),
		array('icon' => 'fa-sitemap', 'label' => 'ERP + Commerce + CRM + Workflows'),
		array('icon' => 'fa-shield', 'label' => 'Cloud + backup continuity'),
		array('icon' => 'fa-cloud-upload', 'label' => 'Super CP provisioning'),
	);
	ob_start();
	?>
<section class="epm-static-hero-section" aria-label="ECOM AE platform">
	<div class="epm-static-hero">
		<div class="epm-static-hero__bg" aria-hidden="true"></div>
		<div class="epm-static-hero__inner">
			<img class="epm-static-hero__logo" src="<?php echo epc_ecomae_h($logo); ?>" alt="ECOM AE" width="200" height="auto" />
			<p class="epm-static-hero__tagline" style="margin-bottom:8px;opacity:.92"><i class="fa fa-cubes"></i> The multi-tenant Business Operating System</p>
			<h1 class="epm-static-hero__headline" aria-label="Business Operating System — ERP, Commerce, Compliance, Workflows, Industry Intelligence">
				<span class="epm-static-hero__line epm-static-hero__line--stack epm-static-hero__line--commerce">Business</span>
				<span class="epm-static-hero__line epm-static-hero__line--stack">Operating</span>
				<span class="epm-static-hero__line epm-static-hero__line--stack">System</span>
				<span class="epm-static-hero__line epm-static-hero__line--cloud">ONE BOS</span>
			</h1>
			<p class="epm-static-hero__tagline">ERP · Commerce · Compliance · Workflows · Industry Intelligence — one cloud, <a href="<?php echo epc_ecomae_h($continuityUrl); ?>#cloud-continuity">backup continuity</a> built in.</p>
			<div class="epm-static-hero__cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demoDays; ?>-day demo</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($superCp); ?>"><i class="fa fa-th-large"></i> Super CP</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform">Platform overview</a>
			</div>
		</div>
		<div class="epm-static-hero__platform">
			<p class="epm-static-hero__platform-title">One Business Operating System for the whole organization</p>
			<p class="epm-static-hero__platform-sub">A multi-tenant BOS combining ERP, commerce, compliance, workflows and industry-specific operational intelligence — across industries worldwide.</p>
			<div class="epm-static-hero__platform-pills">
				<?php foreach ($platformPills as $p) { ?>
				<span class="epm-static-hero__platform-pill"><i class="fa <?php echo epc_ecomae_h($p['icon']); ?>"></i> <?php echo epc_ecomae_h($p['label']); ?></span>
				<?php } ?>
			</div>
		</div>
	</div>
</section>
	<?php
	return ob_get_clean();
}

/** Load capability id => guide detail (steps + screenshot slug). */
function epc_ecomae_platform_capability_guides_map()
{
	static $map = null;
	if ($map === null) {
		$map = require __DIR__ . '/epc_ecomae_platform_capability_guides.php';
		if (!is_array($map)) {
			$map = array();
		}
	}
	return $map;
}

/** JSON payload for capability detail modal (homepage + catalog). */
function epc_ecomae_platform_capability_modal_payload(array $catalog)
{
	$guides = epc_ecomae_platform_capability_guides_map();
	$out = array();
	foreach ($catalog as $cap) {
		$id = isset($cap['id']) ? (string) $cap['id'] : '';
		if ($id === '') {
			continue;
		}
		$row = array(
			'id' => $id,
			'title' => isset($cap['title']) ? (string) $cap['title'] : '',
			'summary' => isset($cap['summary']) ? (string) $cap['summary'] : '',
			'intro' => '',
			'category' => isset($cap['category']) ? (string) $cap['category'] : '',
			'steps' => array(),
			'image' => '',
			'images' => array(),
		);
		if (isset($guides[$id]) && is_array($guides[$id])) {
			$g = $guides[$id];
			if (!empty($g['intro'])) {
				$row['intro'] = (string) $g['intro'];
			}
			if (!empty($g['steps']) && is_array($g['steps'])) {
				$row['steps'] = $g['steps'];
			}
			$imageSlugs = array();
			if (!empty($g['images']) && is_array($g['images'])) {
				$imageSlugs = $g['images'];
			} elseif (!empty($g['image'])) {
				$imageSlugs = array((string) $g['image']);
			}
			foreach ($imageSlugs as $slug) {
				$slug = (string) $slug;
				if ($slug === '') {
					continue;
				}
				$url = epc_ecomae_platform_capability_screenshot($slug);
				if ($url !== '' && strpos($url, 'mock-') === false) {
					$row['images'][] = $url;
				}
				if ($row['image'] === '') {
					$row['image'] = $url;
				}
			}
		}
		$out[] = $row;
	}
	return $out;
}

function epc_ecomae_platform_capability_modal_shell()
{
	ob_start();
	?>
<div class="epm-cap-modal" id="epm-cap-modal" role="dialog" aria-modal="true" aria-labelledby="epm-cap-modal-title" hidden>
	<div class="epm-cap-modal__panel">
		<button type="button" class="epm-cap-modal__close" id="epm-cap-modal-close" aria-label="Close">&times;</button>
		<span class="epm-cap-modal__badge" id="epm-cap-modal-cat"></span>
		<h3 class="epm-cap-modal__title" id="epm-cap-modal-title"></h3>
		<p class="epm-cap-modal__intro" id="epm-cap-modal-intro"></p>
		<div class="epm-cap-modal__shots" id="epm-cap-modal-shots" hidden></div>
		<ol class="epm-cap-modal__steps" id="epm-cap-modal-steps"></ol>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_capability_modal_script(array $catalog)
{
	$payload = epc_ecomae_platform_capability_modal_payload($catalog);
	$json = json_encode($payload, JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE);
	if ($json === false) {
		$json = '[]';
	}
	ob_start();
	?>
<script defer>
(function(){
	var data=<?php echo $json; ?>;
	var byId={};
	for(var i=0;i<data.length;i++){byId[data[i].id]=data[i];}
	var modal=document.getElementById('epm-cap-modal');
	if(!modal)return;
	var titleEl=document.getElementById('epm-cap-modal-title');
	var catEl=document.getElementById('epm-cap-modal-cat');
	var introEl=document.getElementById('epm-cap-modal-intro');
	var stepsEl=document.getElementById('epm-cap-modal-steps');
	var shotsEl=document.getElementById('epm-cap-modal-shots');
	var closeBtn=document.getElementById('epm-cap-modal-close');
	function openCap(id){
		var cap=byId[id];if(!cap)return;
		catEl.textContent=cap.category||'';
		titleEl.textContent=cap.title||'';
		var introText=cap.intro||cap.summary||'';
		introEl.textContent=introText;
		introEl.hidden=!introText;
		stepsEl.innerHTML='';
		var steps=cap.steps||[];
		if(steps.length){
			for(var s=0;s<steps.length;s++){
				var li=document.createElement('li');
				li.innerHTML='<strong>'+(steps[s].title||'')+'</strong><span>'+(steps[s].body||'')+'</span>';
				stepsEl.appendChild(li);
			}
			stepsEl.hidden=false;
		}else{
			stepsEl.hidden=true;
		}
		shotsEl.innerHTML='';
		var imgs=cap.images&&cap.images.length?cap.images:(cap.image?[cap.image]:[]);
		if(imgs.length){
			for(var i=0;i<imgs.length;i++){
				var fig=document.createElement('figure');
				fig.className='epm-cap-modal__shot';
				fig.innerHTML='<img src="'+String(imgs[i]).replace(/"/g,'&quot;')+'" alt="" loading="lazy" />';
				shotsEl.appendChild(fig);
			}
			shotsEl.hidden=false;
		}else{
			shotsEl.hidden=true;
		}
		modal.hidden=false;modal.classList.add('is-open');
		document.body.style.overflow='hidden';
	}
	function closeCap(){
		modal.hidden=true;modal.classList.remove('is-open');
		document.body.style.overflow='';
	}
	document.addEventListener('click',function(ev){
		var card=ev.target.closest('.epm-cap-card[data-cap-id]');
		if(card){ev.preventDefault();openCap(card.getAttribute('data-cap-id'));return;}
		if(ev.target===modal)closeCap();
	});
	if(closeBtn)closeBtn.addEventListener('click',closeCap);
	document.addEventListener('keydown',function(ev){if(ev.key==='Escape'&&modal.classList.contains('is-open'))closeCap();});
})();
</script>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_styles()
{
	$css = <<<'CSS'
<style>
:root{
--epm-bg:#020617;
--epm-bg2:#0b1220;
--epm-card:#0f172a;
--epm-card2:#111827;
--epm-border:rgba(56,189,248,.18);
--epm-cyan:#22d3ee;
--epm-blue:#0ea5e9;
--epm-blue-dark:#0284c7;
--epm-text:#e2e8f0;
--epm-muted:#94a3b8;
--epm-glow:0 0 40px rgba(34,211,238,.15);
--epm-max-w:1440px;
--epm-pad-x:clamp(20px,3vw,40px);
}
.epm-body{margin:0;background:radial-gradient(ellipse 120% 80% at 50% -20%,#0c4a6e 0%,var(--epm-bg) 55%);color:var(--epm-text);font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;line-height:1.55}
.epm-topbar{position:sticky;top:0;z-index:100;background:rgba(2,6,23,.88);backdrop-filter:blur(12px);border-bottom:1px solid var(--epm-border)}
.epm-topbar__inner{max-width:var(--epm-max-w);width:100%;margin:0 auto;padding:10px var(--epm-pad-x);box-sizing:border-box;display:flex;align-items:center;gap:16px;flex-wrap:nowrap}
.epm-brand{display:flex;align-items:center;gap:12px;text-decoration:none;color:var(--epm-text)}
.epm-brand__logo{height:48px;width:auto;display:block}
.epm-brand__text{display:flex;flex-direction:column;line-height:1.15}
.epm-brand__text strong{font-size:15px;letter-spacing:.06em}
.epm-brand__ae{color:var(--epm-cyan)}
.epm-brand__text small{font-size:10px;color:var(--epm-muted);text-transform:uppercase;letter-spacing:.12em;white-space:nowrap}
.epm-nav-toggle{display:none;background:transparent;border:1px solid rgba(255,255,255,.25);border-radius:8px;padding:8px 10px;cursor:pointer;margin-left:auto}
.epm-nav-toggle span{display:block;width:20px;height:2px;background:#e2e8f0;margin:4px 0;border-radius:2px}
.epm-nav-drawer{display:flex;flex:0 1 auto;min-width:0;align-items:center;gap:16px;flex-wrap:nowrap}
.epm-topbar__cta-row{display:flex;gap:8px;align-items:center;flex-shrink:0;margin-left:auto}
.epm-nav-drawer__cta{display:none}
.epm-nav{display:flex;flex-wrap:nowrap;align-items:center;gap:2px 18px;flex:0 1 auto}
.epm-nav__link{color:var(--epm-muted);text-decoration:none;font-size:13px;font-weight:600;padding:6px 0;border-bottom:2px solid transparent;white-space:nowrap}
.epm-nav__link:hover,.epm-nav__link.is-active{color:var(--epm-cyan);border-bottom-color:var(--epm-cyan)}
.epm-nav__group{position:relative}
.epm-nav__group-trigger{background:transparent;border:0;color:var(--epm-muted);font:inherit;font-size:13px;font-weight:600;cursor:pointer;padding:6px 0;display:inline-flex;align-items:center;gap:5px;border-bottom:2px solid transparent;white-space:nowrap}
.epm-nav__group-trigger:hover,.epm-nav__group.is-open .epm-nav__group-trigger,.epm-nav__group-trigger.is-active{color:var(--epm-cyan)}
.epm-nav__caret{font-size:9px;transition:transform .15s;opacity:.8}
.epm-nav__group.is-open .epm-nav__caret{transform:rotate(180deg)}
.epm-nav__panel{position:absolute;top:calc(100% + 10px);left:0;min-width:230px;background:rgba(2,6,23,.98);border:1px solid var(--epm-border);border-radius:12px;padding:8px;display:none;flex-direction:column;gap:2px;box-shadow:0 18px 44px rgba(0,0,0,.5);z-index:120}
.epm-nav__panel::before{content:"";position:absolute;top:-10px;left:0;right:0;height:10px}
.epm-nav__group:hover .epm-nav__panel,.epm-nav__group.is-open .epm-nav__panel,.epm-nav__group:focus-within .epm-nav__panel{display:flex}
.epm-nav__group:last-child .epm-nav__panel{left:auto;right:0}
.epm-nav__panel-link{color:#cbd5e1;text-decoration:none;font-size:13px;font-weight:500;padding:9px 12px;border-radius:8px;white-space:nowrap}
.epm-nav__panel-link:hover,.epm-nav__panel-link.is-active{background:rgba(56,189,248,.12);color:var(--epm-cyan)}
@media(max-width:1500px){.epm-brand__text small{display:none}}
@media(max-width:1440px){.epm-nav{gap:2px 12px}.epm-nav__link,.epm-nav__group-trigger{font-size:12px}.epm-topbar__cta{font-size:11px;padding:7px 10px}.epm-topbar__cta-row{gap:6px}}
@media(max-width:1280px){
.epm-nav-toggle{display:block}
.epm-topbar__cta-row{display:none}
.epm-nav-drawer{position:fixed;inset:56px 0 auto 0;max-height:calc(100vh - 56px);overflow:auto;background:rgba(2,6,23,.97);border-bottom:1px solid var(--epm-border);flex-direction:column;align-items:stretch;flex-wrap:wrap;padding:16px 20px 24px;transform:translateY(-8px);opacity:0;pointer-events:none;transition:opacity .2s,transform .2s;z-index:99}
.epm-nav-drawer.is-open{opacity:1;pointer-events:auto;transform:translateY(0)}
.epm-nav{flex-direction:column;align-items:stretch;flex-wrap:wrap;gap:0}
.epm-nav__link{padding:12px 0;border-bottom:1px solid rgba(148,163,184,.12);font-size:15px}
.epm-nav-drawer__cta{display:flex;flex-direction:column;gap:10px;margin-top:16px}
.epm-brand__text small{display:block}
}
@media(max-width:1280px){
.epm-nav__group{width:100%}
.epm-nav__group-trigger{width:100%;justify-content:space-between;padding:12px 0;border-bottom:1px solid rgba(148,163,184,.12);font-size:15px}
.epm-nav__panel{position:static;display:flex;min-width:0;border:0;box-shadow:none;background:transparent;padding:4px 0 8px 14px;border-radius:0}
.epm-nav__panel::before{display:none}
.epm-nav__panel-link{font-size:14px;padding:8px 0}
}
@media(max-width:900px){
.epm-hero h1{font-size:clamp(22px,7vw,36px);overflow-wrap:anywhere;word-break:break-word}
.epm-hero .lead{font-size:14px;max-width:100%;overflow-wrap:anywhere}
.epm-brand__text strong{font-size:14px;white-space:nowrap}
}
.epm-topbar__cta{background:linear-gradient(135deg,var(--epm-blue),var(--epm-cyan));color:#020617!important;font-weight:700;font-size:12px;padding:8px 14px;border-radius:999px;text-decoration:none;white-space:nowrap}
.epm-topbar__cta--ghost{background:transparent;border:1px solid rgba(255,255,255,.35);color:#e2e8f0!important;margin-right:8px}
.epm-topbar__cta--ghost:hover{background:rgba(255,255,255,.08);color:#fff!important}
.epm-main{min-height:60vh}
.epm-wrap{max-width:var(--epm-max-w);width:100%;margin:0 auto;padding:0 var(--epm-pad-x) 48px;box-sizing:border-box}
.epm-hero{position:relative;border-radius:24px;overflow:hidden;margin:24px 0 28px;border:1px solid var(--epm-border);box-shadow:var(--epm-glow)}
.epm-hero--home{min-height:420px;display:grid;grid-template-columns:1fr 1fr;align-items:center;background:linear-gradient(135deg,#041525 0%,#0c4a6e 45%,#020617 100%)}
@media(max-width:900px){.epm-hero--home{grid-template-columns:1fr;text-align:center}}
.epm-hero__visual{padding:32px;display:flex;justify-content:center;align-items:center}
.epm-hero__visual img{max-width:100%;max-height:340px;filter:drop-shadow(0 20px 40px rgba(34,211,238,.25))}
.epm-hero__body{padding:36px 32px 40px}
.epm-hero__img{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.28}
.epm-hero__shade{position:absolute;inset:0;background:linear-gradient(135deg,rgba(2,6,23,.92),rgba(14,165,233,.35))}
.epm-hero__content{position:relative;z-index:1;padding:40px 36px}
.epm-hero h1{font-size:clamp(28px,4vw,42px);font-weight:800;margin:0 0 12px;color:#fff;line-height:1.15}
.epm-hero .lead{font-size:17px;line-height:1.65;max-width:640px;color:#cbd5e1;margin:0}
.epm-badge{display:inline-flex;align-items:center;gap:8px;background:rgba(34,211,238,.12);border:1px solid rgba(34,211,238,.35);padding:7px 14px;border-radius:999px;font-size:11px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;margin-bottom:14px;color:var(--epm-cyan)}
.epm-cta{display:flex;flex-wrap:wrap;gap:12px;margin-top:22px}
.epm-btn{display:inline-flex;align-items:center;gap:8px;padding:12px 20px;border-radius:12px;font-weight:700;font-size:14px;text-decoration:none;border:1px solid transparent}
.epm-btn--primary{background:linear-gradient(135deg,var(--epm-blue),var(--epm-cyan));color:#020617!important}
.epm-btn--ghost{background:rgba(255,255,255,.06);border-color:rgba(255,255,255,.15);color:#fff!important}
.epm-btn--outline{background:transparent;border-color:var(--epm-cyan);color:var(--epm-cyan)!important}
.epm-section-title{font-size:clamp(22px,3vw,30px);font-weight:800;color:#fff;margin:36px 0 12px}
.epm-section-lead{color:var(--epm-muted);font-size:16px;margin:-4px 0 22px;max-width:min(100%,900px)}
.epm-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:18px;margin:24px 0}
.epm-card{background:var(--epm-card);border:1px solid var(--epm-border);border-radius:16px;padding:22px;box-shadow:var(--epm-glow);transition:border-color .2s,transform .2s}
.epm-card:hover{border-color:rgba(34,211,238,.45)}
.epm-card h3,.epm-card h4{margin:0 0 10px;color:#fff;font-weight:700}
.epm-card p{margin:0;color:var(--epm-muted);font-size:14px;line-height:1.6}
.epm-tenant-logo-wrap{height:72px;display:flex;align-items:center;justify-content:flex-start;margin-bottom:12px}
.epm-tenant-logo{max-width:100%;max-height:56px;width:auto;height:auto;display:block;object-fit:contain;filter:drop-shadow(0 4px 16px rgba(2,6,23,.45))}
.epm-tenant-logo-fallback{display:inline-flex;align-items:center;justify-content:center;min-width:56px;height:56px;padding:0 12px;border-radius:12px;background:rgba(34,211,238,.14);border:1px solid rgba(34,211,238,.38);color:var(--epm-cyan);font-weight:800;letter-spacing:.08em}
.epm-card--photo{padding:0;overflow:hidden;text-decoration:none;color:inherit;display:block}
.epm-card--photo:hover{transform:translateY(-3px)}
.epm-card--photo img{width:100%;height:170px;object-fit:cover;display:block;opacity:.85}
.epm-card--photo .epm-card__inner{padding:18px 20px 22px}
.epm-card--accent{border-color:rgba(34,211,238,.35);background:linear-gradient(145deg,rgba(14,165,233,.08),var(--epm-card))}
.epm-split{display:grid;grid-template-columns:1.05fr .95fr;gap:28px;align-items:start;margin:32px 0}
@media(max-width:900px){.epm-split{grid-template-columns:1fr}}
.epm-feature-list{list-style:none;padding:0;margin:0}
.epm-feature-list li{padding:12px 0 12px 36px;position:relative;border-bottom:1px solid rgba(148,163,184,.12);color:#cbd5e1;font-size:15px;line-height:1.5}
.epm-feature-list li:before{content:"\f00c";font-family:FontAwesome;position:absolute;left:0;top:12px;color:var(--epm-cyan);font-size:14px}
.epm-steps{counter-reset:epmstep;list-style:none;padding:0;margin:0}
.epm-steps li{counter-increment:epmstep;padding:14px 0 14px 52px;position:relative;border-bottom:1px solid rgba(148,163,184,.12);color:#cbd5e1;line-height:1.55}
.epm-steps li:before{content:counter(epmstep);position:absolute;left:0;top:14px;width:36px;height:36px;border-radius:50%;background:linear-gradient(135deg,var(--epm-blue),var(--epm-cyan));color:#020617;text-align:center;line-height:36px;font-weight:800;font-size:14px}
.epm-pill{display:inline-block;background:rgba(34,211,238,.12);color:var(--epm-cyan);font-size:11px;font-weight:700;padding:5px 10px;border-radius:999px;margin:0 6px 8px 0;border:1px solid rgba(34,211,238,.25)}
.epm-pill i{margin-right:4px}
.epm-module-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:14px;margin:20px 0}
.epm-module{background:var(--epm-card2);border:1px solid var(--epm-border);border-radius:14px;padding:16px}
.epm-module i{color:var(--epm-cyan);font-size:18px;margin-bottom:8px;display:block}
.epm-module strong{display:block;color:#fff;font-size:14px;margin-bottom:4px}
.epm-module small{color:var(--epm-muted);font-size:12px;line-height:1.45}
.epm-ecosystem{padding:40px 0 36px;border-top:1px solid var(--epm-border);border-bottom:1px solid var(--epm-border);background:rgba(15,23,42,.55);margin:32px 0}
.epm-ecosystem__eyebrow{text-align:center;color:var(--epm-cyan);font-size:12px;font-weight:700;letter-spacing:.14em;text-transform:uppercase;margin:0 0 10px}
.epm-ecosystem__lead{text-align:center;color:var(--epm-muted);font-size:15px;line-height:1.6;max-width:min(100%,780px);margin:0 auto 28px;padding:0 var(--epm-pad-x);box-sizing:border-box}
.epm-ecosystem__viz{position:relative;max-width:min(100%,920px);width:100%;margin:0 auto;min-height:480px;display:flex;align-items:center;justify-content:center;--hub-orbit-r:min(28vw,240px)}
.epm-ecosystem__core-badge{position:absolute;left:50%;top:50%;transform:translate(-50%,-50%);z-index:4;font-size:22px;font-weight:800;color:#fff;letter-spacing:.06em;text-shadow:0 0 24px rgba(34,211,238,.5);pointer-events:none}
.epm-ecosystem__orbit-spin{position:absolute;inset:0;z-index:3;animation:epmHubOrbitSpin 48s linear infinite}
.epm-ecosystem__node{position:absolute;left:50%;top:50%;width:96px;margin-left:-48px;text-align:center;transform:rotate(var(--hub-i)) translateY(calc(-1 * var(--hub-orbit-r, 200px)))}
.epm-ecosystem__node-inner{transform:rotate(calc(-1 * var(--hub-i)));animation:epmHubNodeUpright 48s linear infinite}
.epm-ecosystem__icon{display:inline-flex;width:42px;height:42px;border-radius:12px;align-items:center;justify-content:center;background:rgba(34,211,238,.12);border:1px solid rgba(34,211,238,.4);color:var(--epm-cyan);margin-bottom:6px;box-shadow:0 0 16px rgba(34,211,238,.2)}
.epm-ecosystem__node strong{display:block;font-size:11px;color:#fff;font-weight:800;text-transform:uppercase}
.epm-ecosystem__node small{display:block;font-size:9px;color:var(--epm-muted);line-height:1.3;margin-top:2px}
.epm-ecosystem__data-tag{display:block;margin-top:4px;font-size:8px;font-style:normal;font-weight:700;color:var(--epm-cyan);letter-spacing:.08em;text-transform:uppercase;padding:2px 6px;border-radius:999px;background:rgba(34,211,238,.12);border:1px solid rgba(34,211,238,.25)}
.epm-ecosystem__legend{list-style:none;margin:24px auto 0;padding:0;max-width:520px;display:flex;flex-wrap:wrap;justify-content:center;gap:10px 24px;font-size:12px;color:var(--epm-muted)}
.epm-ecosystem__legend li{display:flex;align-items:center;gap:8px}
.epm-ecosystem__dot{width:8px;height:8px;border-radius:50%;background:#22d3ee;box-shadow:0 0 8px #22d3ee;animation:epmPacketBlink 2s ease-in-out infinite}
.epm-ecosystem__dot--inner{background:#0ea5e9;animation-delay:.6s}
.epm-flow-svg{position:absolute;left:50%;top:50%;width:100%;max-width:min(100%,720px);transform:translate(-50%,-50%);z-index:1;pointer-events:none}
.epm-flow-svg--compact{max-width:100%}
.epm-flow-svg__pulse{animation:epmHubGlow 4s ease-in-out infinite}
.epm-flow-svg__ring{opacity:.85}
.epm-flow-svg__ring--outer{animation:epmFlowRingPulse 3s ease-in-out infinite}
.epm-flow-svg__ring--inner{animation:epmFlowRingPulse 3s ease-in-out infinite reverse}
.epm-flow-svg__dash{animation:epmHubArcDash 5s linear infinite}
.epm-flow-svg__spoke{stroke:rgba(34,211,238,.2);stroke-width:1;stroke-dasharray:4 8;animation:epmSpokePulse 2.8s ease-in-out infinite;animation-delay:var(--spoke-delay,0s)}
.epm-flow-svg__packet{opacity:.95;animation:epmPacketTrail .8s ease-in-out infinite}
.epm-flow-svg__packet--inner{opacity:.85;animation:epmPacketTrail .55s ease-in-out infinite}
.epm-flow-svg__label{font-family:system-ui,sans-serif;font-weight:700;pointer-events:none;opacity:.9}
.epm-flow-svg__core-ring{animation:epmCoreRingSpin 7s linear infinite;transform-origin:center;transform-box:fill-box}
@keyframes epmHubOrbitSpin{to{transform:rotate(360deg)}}
@keyframes epmHubNodeUpright{to{transform:rotate(calc(-360deg - var(--hub-i)))}}
@keyframes epmFlowRingPulse{0%,100%{opacity:.55}50%{opacity:1}}
@keyframes epmSpokePulse{0%,100%{stroke-opacity:.15}50%{stroke-opacity:.55;stroke-dashoffset:0}}
@keyframes epmPacketBlink{0%,100%{opacity:.4;transform:scale(.9)}50%{opacity:1;transform:scale(1.35)}}
@keyframes epmPacketTrail{0%,100%{opacity:.55}50%{opacity:1;filter:drop-shadow(0 0 6px #22d3ee)}}
@keyframes epmCoreRingSpin{to{transform:rotate(360deg)}}
@keyframes epmMatrixFall{0%{transform:translateY(-110%);opacity:0}8%{opacity:.85}92%{opacity:.75}100%{transform:translateY(110vh);opacity:0}}
.epm-industry-hero{min-height:320px}
.epm-industry-accent{height:4px;border-radius:999px;margin:0 0 24px;background:linear-gradient(90deg,var(--ind-accent,#0ea5e9),var(--epm-cyan))}
.epm-three-col{display:grid;grid-template-columns:repeat(3,1fr);gap:18px;margin:24px 0}
@media(max-width:900px){.epm-three-col{grid-template-columns:1fr}}
.epm-price{border:1px solid var(--epm-border);border-radius:16px;padding:24px;text-align:center;background:var(--epm-card)}
.epm-price.featured{border-color:var(--epm-cyan);background:linear-gradient(180deg,rgba(34,211,238,.08),var(--epm-card));box-shadow:var(--epm-glow)}
.epm-price h4{margin:0 0 8px;font-size:22px;color:#fff}
.epm-price .amt{font-size:36px;font-weight:800;color:var(--epm-cyan);margin:8px 0}
.epm-price ul{text-align:left;margin:16px 0 0;padding-left:18px;color:var(--epm-muted);font-size:14px}
.epm-alert{padding:14px 18px;border-radius:12px;margin-bottom:20px;font-size:14px}
.epm-alert.ok{background:rgba(16,185,129,.12);border:1px solid rgba(52,211,153,.35);color:#6ee7b7}
.epm-alert.err{background:rgba(239,68,68,.12);border:1px solid rgba(248,113,113,.35);color:#fca5a5}
.epm-form .form-group{margin-bottom:14px}
.epm-form label{font-weight:600;color:#cbd5e1;font-size:13px;display:block;margin-bottom:4px}
.epm-form .form-control{width:100%;padding:10px 12px;border-radius:10px;border:1px solid var(--epm-border);background:#0b1220;color:#fff;box-sizing:border-box}
.epm-highlight{background:linear-gradient(135deg,rgba(14,165,233,.12),rgba(34,211,238,.06));border-radius:16px;padding:24px;margin:24px 0;border:1px solid var(--epm-border)}
.epm-highlight h3{margin-top:0;color:#fff}
.epm-footer{border-top:1px solid var(--epm-border);background:#020617;padding:32px var(--epm-pad-x) 40px;margin-top:48px}
.epm-footer__inner{max-width:var(--epm-max-w);width:100%;margin:0 auto;box-sizing:border-box;display:grid;grid-template-columns:1.2fr 1fr;gap:24px;align-items:start}
@media(max-width:700px){.epm-footer__inner{grid-template-columns:1fr}}
.epm-footer__logo{height:40px;margin-bottom:10px}
.epm-footer__brand p{color:var(--epm-muted);font-size:13px;margin:0}
.epm-footer__links{display:flex;flex-wrap:wrap;gap:10px 18px}
.epm-footer__links a{color:var(--epm-muted);text-decoration:none;font-size:13px;font-weight:600}
.epm-footer__links a:hover{color:var(--epm-cyan)}
.epm-footer__copy{grid-column:1/-1;color:var(--epm-muted);font-size:12px;margin:16px 0 0}
.text-primary{color:var(--epm-cyan)!important}

/* —— Static homepage hero (replaces animated hub on ecomae.com home) —— */
.epm-static-hero-section{width:100%;padding:0;margin:0 auto;box-sizing:border-box}
.epm-static-hero{
	position:relative;width:100%;max-width:min(100%,1600px);margin:0 auto;
	padding:clamp(48px,8vh,88px) var(--epm-pad-x) clamp(28px,4vh,40px);
	box-sizing:border-box;overflow:hidden;
}
.epm-static-hero__bg{
	position:absolute;inset:0;pointer-events:none;z-index:0;
	background:
		radial-gradient(ellipse 60% 50% at 15% 15%,rgba(0,204,255,.07),transparent 55%),
		radial-gradient(ellipse 50% 40% at 85% 85%,rgba(0,255,178,.05),transparent 55%),
		radial-gradient(ellipse 40% 30% at 50% 50%,rgba(0,204,255,.03),transparent 60%),
		linear-gradient(180deg,#041525 0%,#050d1a 45%,#020617 100%);
}
.epm-static-hero__inner{position:relative;z-index:1;text-align:center;max-width:min(92vw,640px);margin:0 auto;padding:0 12px;box-sizing:border-box}
.epm-static-hero__logo{max-width:min(200px,42vw);height:auto;display:block;margin:0 auto 20px;filter:drop-shadow(0 16px 40px rgba(34,211,238,.35))}
.epm-static-hero__headline{font-family:'Syne',system-ui,sans-serif;font-weight:800;letter-spacing:-.03em;line-height:1.05;margin:0 0 16px;display:flex;flex-direction:column;align-items:center}
.epm-static-hero__line{display:block;font-size:clamp(28px,4.8vw,58px)}
.epm-static-hero__line--stack{color:#f8fafc;text-shadow:0 2px 28px rgba(0,0,0,.55),0 0 20px rgba(226,232,240,.18)}
.epm-static-hero__line--commerce{white-space:nowrap}
.epm-static-hero__line--cloud{
	font-size:clamp(34px,5.8vw,76px);margin-top:8px;letter-spacing:.06em;text-transform:uppercase;white-space:nowrap;
	background:linear-gradient(135deg,#fbbf24 0%,#22d3ee 38%,#a78bfa 68%,#fcd34d 100%);
	-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;
	filter:drop-shadow(0 0 18px rgba(251,191,36,.5)) drop-shadow(0 0 36px rgba(34,211,238,.4));
}
.epm-static-hero__tagline{margin:0 0 22px;font-size:14px;font-weight:600;color:var(--epm-cyan);letter-spacing:.04em;line-height:1.6}
.epm-static-hero__tagline a{color:var(--epm-cyan);text-decoration:none;border-bottom:1px dotted rgba(34,211,238,.5)}
.epm-static-hero__cta{display:flex;flex-wrap:wrap;gap:10px;justify-content:center;margin-bottom:clamp(28px,4vh,40px)}
.epm-static-hero__platform{
	position:relative;z-index:1;width:min(96%,960px);margin:0 auto;padding:16px 20px 18px;text-align:center;
	background:linear-gradient(180deg,rgba(15,23,42,.75),rgba(2,6,23,.92));
	border:1px solid var(--epm-border);border-radius:16px;box-shadow:var(--epm-glow);box-sizing:border-box;
}
.epm-static-hero__platform-title{margin:0 0 4px;font-size:12px;font-weight:800;letter-spacing:.12em;text-transform:uppercase;color:#fff}
.epm-static-hero__platform-sub{margin:0 0 12px;font-size:11px;color:var(--epm-muted)}
.epm-static-hero__platform-pills{display:flex;flex-wrap:wrap;justify-content:center;gap:8px 14px}
.epm-static-hero__platform-pill{font-size:10px;font-weight:600;color:var(--epm-cyan);text-transform:uppercase;letter-spacing:.05em}
.epm-static-hero__platform-pill i{margin-right:4px;opacity:.9}
@media (max-width:640px){
.epm-static-hero{padding-top:36px;padding-bottom:24px}
.epm-static-hero__line{font-size:clamp(22px,7.2vw,36px)}
.epm-static-hero__line--cloud{font-size:clamp(28px,8.5vw,46px);margin-top:4px}
.epm-static-hero__platform{margin-top:8px;padding:14px 16px}
}

/* —— Animated center hub (other pages / legacy) —— */
.epm-hub-section{width:100%;padding:8px var(--epm-pad-x) 0;margin:0 auto;box-sizing:border-box}
.epm-hub{
	--hub-orbit-r:min(28vw,280px);
	--hub-orbit-duration:38s;
	position:relative;width:100%;max-width:min(100%,1600px);min-height:min(88vh,820px);margin:0 auto;display:flex;align-items:center;justify-content:center;padding:24px 0 40px;overflow:hidden;box-sizing:border-box
}
.epm-hub__matrix{position:absolute;inset:0;z-index:0;pointer-events:none;overflow:hidden;opacity:.35;mask-image:radial-gradient(ellipse 75% 70% at 50% 44%,#000 15%,transparent 72%)}
.epm-hub__matrix-col{
	position:absolute;top:0;bottom:0;width:2.2%;min-width:14px;max-width:22px;
	font-family:ui-monospace,Consolas,monospace;font-size:11px;line-height:1.15;font-weight:600;
	color:rgba(34,211,238,.55);text-shadow:0 0 8px rgba(34,211,238,.5);
	white-space:pre;text-align:center;
	animation:epmMatrixFall var(--fall-dur,2s) linear infinite;
	animation-delay:var(--fall-delay,0s);
}
.epm-hub__map{position:absolute;left:0;top:0;width:42%;height:45%;opacity:.35;background:radial-gradient(circle at 30% 40%,rgba(34,211,238,.08) 0%,transparent 55%),repeating-radial-gradient(circle at 2px 2px,rgba(56,189,248,.15) 0 1px,transparent 1px 12px);mask-image:linear-gradient(135deg,#000 20%,transparent 70%);pointer-events:none}
.epm-hub__cloud{position:absolute;right:4%;top:6%;display:flex;flex-direction:column;align-items:center;gap:6px;animation:epmHubCloud 4s ease-in-out infinite;pointer-events:none;z-index:5}
.epm-hub__cloud-shape{font-size:42px;color:rgba(34,211,238,.5);text-shadow:0 0 24px rgba(34,211,238,.4)}
.epm-hub__servers{display:flex;gap:4px;padding:6px 10px;background:rgba(15,23,42,.85);border:1px solid rgba(34,211,238,.35);border-radius:8px;box-shadow:0 0 20px rgba(34,211,238,.2)}
.epm-hub__servers span{display:block;width:14px;height:28px;background:linear-gradient(180deg,#1e3a5f,#0ea5e9);border-radius:3px;animation:epmHubServer 2s ease-in-out infinite}
.epm-hub__servers span:nth-child(2){animation-delay:.25s}
.epm-hub__servers span:nth-child(3){animation-delay:.5s}
@keyframes epmHubCloud{0%,100%{transform:translateY(0)}50%{transform:translateY(-8px)}}
@keyframes epmHubServer{0%,100%{opacity:.7}50%{opacity:1;box-shadow:0 0 8px rgba(34,211,238,.6)}}
@keyframes epmHubArcDash{to{stroke-dashoffset:-120}}
@keyframes epmHubGlow{0%,100%{transform:translate(-50%,-50%) scale(1);opacity:.6}50%{transform:translate(-50%,-50%) scale(1.08);opacity:1}}
@keyframes epmHubCoreIn{from{opacity:0;transform:translateY(16px)}to{opacity:1;transform:translateY(0)}}

.epm-hub__flow-layer{position:absolute;left:50%;top:44%;width:min(78vw,calc(var(--epm-max-w) - 80px));transform:translate(-50%,-50%);z-index:2;pointer-events:none}
.epm-hub__flow-layer .epm-flow-svg{position:relative;left:auto;top:auto;transform:none;max-width:100%;width:100%;filter:drop-shadow(0 0 28px rgba(34,211,238,.25))}

.epm-hub__core{position:relative;z-index:6;text-align:center;max-width:min(92vw,560px);margin-top:-4vh;animation:epmHubCoreIn 1s ease-out;padding:0 12px;box-sizing:border-box}
.epm-hub__core-glow{position:absolute;left:50%;top:40%;width:300px;height:300px;transform:translate(-50%,-50%);background:radial-gradient(circle,rgba(14,165,233,.3) 0%,transparent 70%);animation:epmHubGlow 3s ease-in-out infinite;pointer-events:none}
.epm-hub__core-pulse{position:absolute;left:50%;top:40%;width:120px;height:120px;transform:translate(-50%,-50%);border:2px solid rgba(34,211,238,.35);border-radius:50%;animation:epmCorePulseOut 2.5s ease-out infinite;pointer-events:none}
@keyframes epmCorePulseOut{0%{transform:translate(-50%,-50%) scale(.6);opacity:.8}100%{transform:translate(-50%,-50%) scale(2.2);opacity:0}}

.epm-hub__logo{max-width:min(200px,42vw);height:auto;display:block;margin:0 auto 8px;filter:drop-shadow(0 16px 40px rgba(34,211,238,.35));animation:epmHubLogoFloat 5s ease-in-out infinite}
@keyframes epmHubLogoFloat{0%,100%{transform:translateY(0)}50%{transform:translateY(-6px)}}
.epm-hub__headline{font-family:'Syne',system-ui,sans-serif;font-weight:800;letter-spacing:-.03em;line-height:1.05;margin:0 0 12px;display:flex;flex-direction:column;align-items:center}
.epm-hub__headline-line{display:block;font-size:clamp(28px,4.8vw,58px)}
.epm-hub__headline-line--stack{color:#f8fafc;text-shadow:0 2px 28px rgba(0,0,0,.55),0 0 20px rgba(226,232,240,.18)}
.epm-hub__headline-line--commerce{white-space:nowrap}
.epm-hub__headline-line--cloud{font-size:clamp(34px,5.8vw,76px);margin-top:8px;letter-spacing:.06em;text-transform:uppercase;white-space:nowrap;background:linear-gradient(135deg,#fbbf24 0%,#22d3ee 38%,#a78bfa 68%,#fcd34d 100%);-webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;filter:drop-shadow(0 0 18px rgba(251,191,36,.5)) drop-shadow(0 0 36px rgba(34,211,238,.4))}
.epm-hub__title{font-size:clamp(32px,5vw,48px);font-weight:800;margin:0 0 6px;letter-spacing:.04em;color:#fff;line-height:1.1}
.epm-hub__ae{color:var(--epm-cyan);text-shadow:0 0 30px rgba(34,211,238,.45)}
.epm-hub__tagline{margin:0 0 12px;font-size:13px;font-weight:700;letter-spacing:.18em;text-transform:uppercase;color:#cbd5e1}
.epm-hub__pill{display:inline-flex;align-items:center;gap:8px;margin:0 0 18px;padding:8px 18px;border-radius:999px;font-size:11px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--epm-cyan);background:rgba(34,211,238,.1);border:1px solid rgba(34,211,238,.4);box-shadow:0 0 20px rgba(34,211,238,.15)}
.epm-hub__live-dot{font-size:8px;color:#34d399;animation:epmPacketBlink 1.5s ease-in-out infinite}
.epm-hub__cta{display:flex;flex-wrap:wrap;gap:10px;justify-content:center}

.epm-hub__orbit-spin{position:absolute;inset:0;z-index:4;animation:epmHubOrbitSpin var(--hub-orbit-duration) linear infinite;pointer-events:none}
.epm-hub__node{position:absolute;left:50%;top:44%;width:112px;margin-left:-56px;text-decoration:none;color:#fff;pointer-events:auto;transform:rotate(var(--hub-i)) translateY(calc(-1 * var(--hub-orbit-r)));animation:epmHubNodeIn .8s ease-out backwards;animation-delay:var(--hub-delay)}
.epm-hub__node-inner{text-align:center;transform:rotate(calc(-1 * var(--hub-i)));animation:epmHubNodeUpright var(--hub-orbit-duration) linear infinite}
.epm-hub__node--featured{width:124px;margin-left:-62px}
.epm-hub__node--featured .epm-hub__node-tile{width:64px;height:64px;font-size:26px;border-color:rgba(34,211,238,.7);box-shadow:0 0 32px rgba(34,211,238,.4)}
@keyframes epmHubNodeIn{from{opacity:0;transform:rotate(var(--hub-i)) translateY(calc(-1 * var(--hub-orbit-r) + 40px)) scale(.85)}to{opacity:1;transform:rotate(var(--hub-i)) translateY(calc(-1 * var(--hub-orbit-r))) scale(1)}}
.epm-hub__node-tile{display:flex;align-items:center;justify-content:center;width:56px;height:56px;margin:0 auto 8px;border-radius:14px;background:linear-gradient(145deg,rgba(14,165,233,.4),rgba(15,23,42,.95));border:1px solid rgba(34,211,238,.5);font-size:22px;color:#fff;transition:transform .25s,box-shadow .25s,border-color .25s;animation:epmNodeTilePulse 1.6s ease-in-out infinite;animation-delay:var(--hub-delay)}
@keyframes epmNodeTilePulse{0%,100%{box-shadow:0 0 14px rgba(34,211,238,.25)}50%{box-shadow:0 0 28px rgba(34,211,238,.65)}}
.epm-hub__node:hover .epm-hub__node-tile{transform:scale(1.1);border-color:var(--epm-cyan)}
.epm-hub__node strong{display:block;font-size:11px;font-weight:800;letter-spacing:.06em;text-transform:uppercase;line-height:1.2}
.epm-hub__node small{display:block;margin-top:3px;font-size:9px;color:var(--epm-muted);line-height:1.3;letter-spacing:.03em}
.epm-hub__data-chip{display:inline-block;margin-top:5px;padding:2px 8px;border-radius:999px;font-size:8px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:#a5f3fc;background:rgba(34,211,238,.15);border:1px solid rgba(34,211,238,.35);animation:epmDataChipGlow 1.1s ease-in-out infinite;animation-delay:var(--hub-delay)}
@keyframes epmDataChipGlow{0%,100%{opacity:.65}50%{opacity:1;box-shadow:0 0 14px rgba(34,211,238,.55)}}

.epm-hub__platform{position:absolute;left:50%;bottom:0;transform:translateX(-50%);z-index:4;width:min(96%,960px);padding:16px 20px 18px;text-align:center;background:linear-gradient(180deg,rgba(15,23,42,.75),rgba(2,6,23,.92));border:1px solid var(--epm-border);border-radius:16px;box-shadow:var(--epm-glow);animation:epmHubCoreIn 1s ease-out .5s backwards}
.epm-hub__platform-title{margin:0 0 4px;font-size:12px;font-weight:800;letter-spacing:.12em;text-transform:uppercase;color:#fff}
.epm-hub__platform-sub{margin:0 0 12px;font-size:11px;color:var(--epm-muted)}
.epm-hub__platform-pills{display:flex;flex-wrap:wrap;justify-content:center;gap:8px 14px}
.epm-hub__platform-pill{font-size:10px;font-weight:600;color:var(--epm-cyan);text-transform:uppercase;letter-spacing:.05em}
.epm-hub__platform-pill i{margin-right:4px;opacity:.9}

.epm-toc{position:sticky;top:62px;z-index:50;background:rgba(2,6,23,.92);backdrop-filter:blur(10px);border:1px solid var(--epm-border);border-radius:14px;padding:14px 18px;margin:0 0 28px}
.epm-toc__title{margin:0 0 10px;font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;color:var(--epm-cyan)}
.epm-toc__links{display:flex;flex-wrap:wrap;gap:8px 12px}
.epm-toc__links a{color:var(--epm-muted);text-decoration:none;font-size:12px;font-weight:600;padding:4px 10px;border-radius:999px;border:1px solid transparent}
.epm-toc__links a:hover{color:var(--epm-cyan);border-color:rgba(34,211,238,.35);background:rgba(34,211,238,.08)}
.epm-area{margin:48px 0;padding:0 0 40px;border-bottom:1px solid rgba(148,163,184,.12)}
.epm-area:last-of-type{border-bottom:0}
.epm-area__head{display:flex;gap:20px;align-items:flex-start;margin-bottom:28px}
.epm-area__icon{flex:0 0 56px;width:56px;height:56px;border-radius:14px;display:flex;align-items:center;justify-content:center;background:linear-gradient(135deg,rgba(14,165,233,.25),rgba(34,211,238,.1));border:1px solid var(--epm-border);color:var(--epm-cyan);font-size:22px}
.epm-area__head h2{margin:0 0 10px;font-size:clamp(22px,3vw,28px);color:#fff;font-weight:800}
.epm-area__summary{margin:0 0 12px;color:var(--epm-muted);font-size:16px;line-height:1.65;max-width:820px}
.epm-area__packs{margin:0 0 12px}
.epm-area__links{margin-top:4px!important}
.epm-btn--sm{padding:8px 14px;font-size:12px}
.epm-area__shots{display:grid;grid-template-columns:1fr 1fr;gap:22px;align-items:start}
.epm-area__shots--flip .epm-preview:first-child{order:2}
.epm-area__shots--flip .epm-preview:last-child{order:1}
@media(max-width:900px){.epm-area__shots{grid-template-columns:1fr}.epm-area__shots--flip .epm-preview{order:unset}}
.epm-preview{margin:0}
.epm-preview__browser{border-radius:14px;overflow:hidden;border:1px solid var(--epm-border);box-shadow:var(--epm-glow);background:#020617}
.epm-preview__chrome{display:flex;align-items:center;gap:8px;padding:10px 14px;background:#0f172a;border-bottom:1px solid var(--epm-border)}
.epm-preview__chrome span{width:10px;height:10px;border-radius:50%;background:#334155}
.epm-preview__chrome span:nth-child(1){background:#ef4444}
.epm-preview__chrome span:nth-child(2){background:#eab308}
.epm-preview__chrome span:nth-child(3){background:#22c55e}
.epm-preview__chrome em{margin-left:auto;font-size:11px;font-style:normal;color:var(--epm-muted);font-weight:600;text-transform:uppercase;letter-spacing:.06em}
.epm-preview__shot{line-height:0;background:#0b1220}
.epm-preview__shot img{width:100%;height:auto;display:block;vertical-align:middle}
.epm-preview figcaption{padding:14px 4px 0}
.epm-preview figcaption strong{display:block;color:#fff;font-size:14px;margin-bottom:4px}
.epm-preview figcaption>p{margin:0 0 10px;font-size:13px;color:var(--epm-muted);line-height:1.5}
.epm-preview__feats{list-style:none;margin:0;padding:0;display:grid;grid-template-columns:1fr 1fr;gap:4px 12px}
@media(max-width:600px){.epm-preview__feats{grid-template-columns:1fr}}
.epm-preview__feats li{font-size:12px;color:#cbd5e1;padding-left:16px;position:relative;line-height:1.45}
.epm-preview__feats li:before{content:"\f111";font-family:FontAwesome;font-size:6px;position:absolute;left:0;top:5px;color:var(--epm-cyan)}
.epm-model-cards{display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin:24px 0 32px}
@media(max-width:800px){.epm-model-cards{grid-template-columns:1fr}}
.epm-model-card{background:var(--epm-card);border:1px solid var(--epm-border);border-radius:14px;padding:20px;text-align:center}
.epm-model-card i{font-size:28px;color:var(--epm-cyan);margin-bottom:10px}
.epm-model-card strong{display:block;color:#fff;margin-bottom:6px;font-size:15px}
.epm-model-card p{margin:0;font-size:13px;color:var(--epm-muted);line-height:1.5}
.epm-hub__tagline-sub{margin:-6px 0 14px;font-size:14px;font-weight:600;color:var(--epm-cyan);letter-spacing:.04em}
.epm-unified-stack{margin:0 auto 12px;padding-top:8px}
.epm-unified-stack--home{margin-top:-12px;padding-bottom:8px}
.epm-unified-stack__cols{margin-bottom:20px}
.epm-unified-stack__shots .epm-preview{margin-bottom:0}
.epm-go-live{margin:28px auto}
.epm-go-live__panel{margin:0}
.epm-promise-grid{display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin:0 0 20px}
@media(max-width:900px){.epm-promise-grid{grid-template-columns:1fr}}
.epm-go-live__steps{margin:0 0 18px}
.epm-eco-model{margin:30px 0 18px}
.epm-eco-model__head{margin-bottom:8px}
.epm-eco-model__grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;margin:18px 0}
.epm-eco-model__card h3{margin-bottom:8px;font-size:18px}
.epm-eco-model__label{margin:10px 0 8px;color:#cbd5e1;font-size:13px;letter-spacing:.03em}
.epm-eco-model__list{list-style:none;padding:0;margin:0}
.epm-eco-model__list li{position:relative;padding:6px 0 6px 18px;color:#cbd5e1;font-size:14px;line-height:1.45}
.epm-eco-model__list li:before{content:"\f111";font-family:FontAwesome;position:absolute;left:0;top:11px;color:var(--epm-cyan);font-size:7px}
.epm-eco-model__list--roadmap li{color:var(--epm-muted)}
.epm-eco-model__meta p{margin:0 0 10px;color:#cbd5e1;font-size:14px;line-height:1.6}
.epm-eco-model__meta p:last-child{margin-bottom:0}
@media(max-width:900px){.epm-eco-model__grid{grid-template-columns:1fr}}

.epm-super-cp-guides{margin:0 auto 24px;padding:28px 0 12px;border-top:1px solid var(--epm-border);border-bottom:1px solid var(--epm-border);background:rgba(15,23,42,.45)}
.epm-super-cp-guides--home{margin-top:-4px}
.epm-super-cp-guides__head{margin-bottom:20px}
.epm-super-cp-guides__grid{margin-bottom:8px}
.epm-super-cp-guides__card{text-decoration:none;color:inherit;display:block;transition:transform .2s,border-color .2s}
.epm-super-cp-guides__card:hover{transform:translateY(-3px);border-color:rgba(34,211,238,.55)}
.epm-super-cp-guides__icon{display:inline-flex;width:44px;height:44px;border-radius:12px;align-items:center;justify-content:center;background:rgba(34,211,238,.12);border:1px solid rgba(34,211,238,.35);color:var(--epm-cyan);font-size:18px;margin-bottom:12px}
.epm-super-cp-guides__cta{margin-top:16px}

.epm-cap-chips{display:flex;flex-wrap:wrap;gap:8px 10px;margin:12px 0 4px}
.epm-cap-chips--strip{margin-bottom:18px}
.epm-cap-chip{display:inline-flex;align-items:center;gap:6px;padding:7px 14px;border-radius:999px;font-size:11px;font-weight:700;letter-spacing:.03em;text-decoration:none;color:var(--epm-cyan);background:rgba(34,211,238,.08);border:1px solid rgba(34,211,238,.28);cursor:pointer;transition:border-color .2s,background .2s,color .2s;font-family:inherit}
a.epm-cap-chip:hover,button.epm-cap-chip:hover{border-color:rgba(34,211,238,.55);background:rgba(34,211,238,.14);color:#fff}
.epm-cap-chip.is-active{background:linear-gradient(135deg,rgba(14,165,233,.35),rgba(34,211,238,.2));border-color:var(--epm-cyan);color:#fff}
.epm-cap-chip__count{font-size:10px;opacity:.85;padding:1px 6px;border-radius:999px;background:rgba(2,6,23,.35)}

.epm-cap-browser{margin:8px 0 32px}
.epm-cap-browser__toolbar{display:flex;flex-wrap:wrap;gap:16px 24px;align-items:flex-end;justify-content:space-between;margin-bottom:16px}
.epm-cap-browser__search-wrap{flex:0 1 320px;min-width:200px}
.epm-cap-browser__search{width:100%;padding:11px 14px;border-radius:12px;border:1px solid var(--epm-border);background:#0b1220;color:#fff;font-size:14px;box-sizing:border-box}
.epm-cap-browser__search:focus{outline:none;border-color:var(--epm-cyan);box-shadow:0 0 0 2px rgba(34,211,238,.15)}
.epm-cap-browser__status{font-size:13px;color:var(--epm-muted);margin:0 0 14px}
.epm-cap-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:16px;margin:0 0 24px}
.epm-cap-card{background:var(--epm-card);border:1px solid var(--epm-border);border-radius:16px;padding:20px 20px 18px;box-shadow:var(--epm-glow);transition:border-color .2s,transform .2s}
.epm-cap-card:hover{border-color:rgba(34,211,238,.45);transform:translateY(-2px)}
.epm-cap-card__icon{display:inline-flex;width:40px;height:40px;border-radius:12px;align-items:center;justify-content:center;background:rgba(34,211,238,.12);border:1px solid rgba(34,211,238,.35);color:var(--epm-cyan);font-size:16px;margin-bottom:10px}
.epm-cap-card__badge{display:inline-block;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--epm-muted);margin-bottom:8px;line-height:1.3}
.epm-cap-card__title{margin:0 0 8px;font-size:16px;font-weight:800;color:#fff;line-height:1.3}
.epm-cap-card__summary{margin:0;font-size:13px;color:var(--epm-muted);line-height:1.55}

.epm-guide-area{margin:40px 0;padding:0 0 36px;border-bottom:1px solid rgba(148,163,184,.12)}
.epm-guide-area:last-of-type{border-bottom:0}
.epm-guide-area__head{display:flex;gap:20px;align-items:flex-start;margin-bottom:24px}
.epm-guide-area__caps{list-style:none;margin:0;padding:0}
.epm-guide-area__caps li{padding:12px 0;border-bottom:1px solid rgba(148,163,184,.12);font-size:14px;line-height:1.55;color:#cbd5e1}
.epm-guide-area__caps li:last-child{border-bottom:0}
.epm-guide-area__caps strong{display:block;color:#fff;margin-bottom:4px;font-size:14px}
.epm-guide-area__caps span{display:block;color:var(--epm-muted)}
.epm-guide-area__example{display:block;margin-top:6px;font-size:12px}
.epm-guide-area__example a{color:var(--epm-cyan)}

.sr-only{position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap;border:0}
.epm-continuity{margin:0 auto 20px;padding:28px 0 8px;border-top:1px solid var(--epm-border);border-bottom:1px solid var(--epm-border);background:linear-gradient(180deg,rgba(15,23,42,.65),rgba(2,6,23,.4))}
.epm-continuity--home{margin-top:-8px}
.epm-continuity__head{display:flex;flex-wrap:wrap;gap:16px 28px;align-items:flex-end;justify-content:space-between;margin-bottom:20px}
.epm-continuity__head-cta{flex:0 0 auto}
.epm-continuity__viz{margin:8px 0 24px;padding:20px 16px 12px;border-radius:20px;border:1px solid var(--epm-border);background:rgba(2,6,23,.55);box-shadow:var(--epm-glow)}
.epm-continuity__pillars .epm-card h4{font-size:15px}
.epm-continuity__pillars{margin-bottom:8px;grid-template-columns:repeat(auto-fit,minmax(200px,1fr))}
.epm-continuity__splash{margin-top:8px}
.epm-continuity__cta{margin-top:20px}
.epm-failover-flow{margin:0;position:relative}
.epm-failover-flow__svg{width:100%;max-width:min(100%,1200px);display:block;margin:0 auto}
.epm-failover-flow__path{opacity:.75}
.epm-failover-flow__path--shop-cloud{animation:epmFfPathGlow 4s ease-in-out infinite}
.epm-failover-flow__path--cloud-backup{animation:epmFfPathFailover 16s ease-in-out infinite}
.epm-failover-flow__path--backup-cloud{animation:epmFfPathFailover 16s ease-in-out infinite reverse}
.epm-failover-flow__path--sync-back{animation:epmFfPathSync 16s ease-in-out infinite}
.epm-failover-flow__node--cloud>rect{animation:epmFfCloudPulse 16s ease-in-out infinite}
.epm-failover-flow__node--backup>rect{animation:epmFfBackupPulse 16s ease-in-out infinite}
.epm-failover-flow__phase-label{animation:epmFfPhaseLabel 16s steps(4,end) infinite}
.epm-failover-flow__steps{display:flex;flex-wrap:wrap;justify-content:center;gap:10px 16px;list-style:none;margin:18px 0 0;padding:0}
.epm-failover-flow__step{display:flex;align-items:center;gap:8px;font-size:12px;font-weight:700;color:var(--epm-muted);text-transform:uppercase;letter-spacing:.06em;padding:8px 14px;border-radius:999px;border:1px solid rgba(148,163,184,.2);background:rgba(15,23,42,.6)}
.epm-failover-flow__step span{display:inline-flex;width:22px;height:22px;border-radius:50%;align-items:center;justify-content:center;background:rgba(34,211,238,.15);color:var(--epm-cyan);font-size:11px}
.epm-failover-flow__step--1{animation:epmFfStepOn 16s steps(4,end) infinite}
.epm-failover-flow__step--2{animation:epmFfStepOn 16s steps(4,end) infinite;animation-delay:-12s}
.epm-failover-flow__step--3{animation:epmFfStepOn 16s steps(4,end) infinite;animation-delay:-8s}
.epm-failover-flow__step--4{animation:epmFfStepOn 16s steps(4,end) infinite;animation-delay:-4s}
@keyframes epmFfPathGlow{0%,100%{stroke-opacity:.7}50%{stroke-opacity:1;filter:drop-shadow(0 0 6px #22d3ee)}}
@keyframes epmFfPathFailover{0%,22%{stroke-opacity:.2}28%,52%{stroke-opacity:1;stroke:#fbbf24}58%,100%{stroke-opacity:.35}}
@keyframes epmFfPathSync{0%,68%{stroke-opacity:.15}74%,92%{stroke-opacity:1;stroke:#34d399}100%{stroke-opacity:.2}}
@keyframes epmFfCloudPulse{0%,22%{stroke-color:rgba(34,211,238,.65)}28%,52%{stroke-color:#fbbf24}58%,68%{stroke-color:rgba(34,211,238,.4)}74%,100%{stroke-color:#34d399}}
@keyframes epmFfBackupPulse{0%,28%{stroke-color:rgba(14,165,233,.45);filter:none}32%,58%{stroke-color:#22d3ee;filter:drop-shadow(0 0 12px rgba(34,211,238,.55))}62%,100%{stroke-color:rgba(14,165,233,.45)}}
@keyframes epmFfPhaseLabel{0%,24%{opacity:1}25%,100%{opacity:.85}}
@keyframes epmFfStepOn{0%,24%{color:var(--epm-muted);border-color:rgba(148,163,184,.2)}25%,49%{color:#fde68a;border-color:rgba(251,191,36,.45)}50%,74%{color:var(--epm-cyan);border-color:rgba(34,211,238,.55)}75%,100%{color:#6ee7b7;border-color:rgba(52,211,153,.45)}}
@media(prefers-reduced-motion:reduce){
.epm-failover-flow__path,.epm-failover-flow__node--cloud>rect,.epm-failover-flow__node--backup>rect,.epm-failover-flow__phase-label,.epm-failover-flow__step{animation:none!important}
.epm-failover-flow__packet{display:none}
.epm-failover-flow__path{stroke-opacity:1!important}
.epm-failover-flow__step--3{color:var(--epm-cyan);border-color:rgba(34,211,238,.45)}
}

.epm-anim-paused,.epm-anim-paused *{animation-play-state:paused!important}
.epm-hub,.epm-ecosystem__viz,.epm-failover-flow{content-visibility:auto;contain-intrinsic-size:420px}
@media(prefers-reduced-motion:reduce){
.epm-hub__orbit-spin,.epm-ecosystem__orbit-spin,.epm-hub__node-inner,.epm-ecosystem__node-inner,.epm-flow-svg animateMotion,.epm-failover-flow animateMotion,.epm-flow-svg__dash,.epm-flow-svg__core-ring,.epm-hub__core-pulse,.epm-hub__matrix-col{animation:none!important}
.epm-flow-svg__packet,.epm-flow-svg__label,.epm-failover-flow__packet{display:none}
.epm-hub__matrix{opacity:.15}
}
@media(min-width:1200px){
.epm-hub{--hub-orbit-r:min(26vw,310px)}
.epm-ecosystem__viz{--hub-orbit-r:min(22vw,280px)}
}
@media(min-width:1600px){
.epm-hub{--hub-orbit-r:min(22vw,340px)}
}
.epm-cap-card{cursor:pointer}
.epm-cap-card:focus{outline:2px solid var(--epm-cyan);outline-offset:2px}
.epm-cap-modal{position:fixed;inset:0;z-index:1000;display:none;align-items:center;justify-content:center;padding:20px;background:rgba(2,6,23,.82);backdrop-filter:blur(6px)}
.epm-cap-modal.is-open{display:flex}
.epm-cap-modal__panel{max-width:720px;width:100%;max-height:90vh;overflow-y:auto;background:var(--epm-card);border:1px solid var(--epm-border);border-radius:18px;padding:24px 28px;box-shadow:var(--epm-glow);position:relative}
.epm-cap-modal__close{position:absolute;top:12px;right:12px;background:rgba(255,255,255,.08);border:1px solid rgba(255,255,255,.15);color:#fff;width:32px;height:32px;border-radius:8px;cursor:pointer;font-size:18px;line-height:1;z-index:2}
.epm-cap-modal__badge{display:inline-block;font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--epm-muted);margin-bottom:8px}
.epm-cap-modal__title{margin:0 0 10px;font-size:22px;color:#fff;font-weight:800;padding-right:36px}
.epm-cap-modal__intro{margin:0 0 16px;color:var(--epm-muted);font-size:14px;line-height:1.6}
.epm-cap-modal__shots{margin:0 0 16px;display:flex;flex-direction:column;gap:12px}
.epm-cap-modal__shot{margin:0;border-radius:12px;overflow:hidden;border:1px solid var(--epm-border)}
.epm-cap-modal__shot img{width:100%;height:auto;display:block}
.epm-cap-modal__steps{margin:0 0 8px;padding:0 0 0 22px;list-style:decimal}
.epm-cap-modal__steps li{padding:12px 0 12px 6px;border-bottom:1px solid rgba(148,163,184,.12)}
.epm-cap-modal__steps li:last-child{border-bottom:0}
.epm-cap-modal__steps strong{display:block;color:#fff;margin-bottom:4px;font-size:14px}
.epm-cap-modal__steps span{color:var(--epm-muted);font-size:13px;line-height:1.55;display:block}
.epm-tenant-showcase--home{margin-top:-8px;padding-top:12px;border-top:1px solid var(--epm-border)}
@media(max-width:768px){
.epm-hub{min-height:auto;padding-bottom:120px}
.epm-hub__core{margin-top:0;max-width:100%}
.epm-hub__headline-line{font-size:clamp(22px,7.2vw,36px)}
.epm-hub__headline-line--cloud{font-size:clamp(28px,8.5vw,46px);margin-top:4px}
.epm-hub__orbit-spin{position:relative;inset:auto;animation:none;display:grid;grid-template-columns:repeat(2,1fr);gap:12px;max-width:400px;margin:20px auto 0;pointer-events:auto}
.epm-hub__node{position:relative;left:auto!important;top:auto!important;transform:none!important;width:auto;margin:0!important}
.epm-hub__node-inner{animation:none!important;transform:none!important}
.epm-hub__flow-layer,.epm-hub__cloud{display:none}
.epm-hub__platform{position:relative;left:auto;bottom:auto;transform:none;margin-top:28px}
.epm-ecosystem__viz{min-height:auto;padding:20px 0}
.epm-ecosystem__orbit-spin{position:relative;animation:none;display:grid;grid-template-columns:repeat(2,1fr);gap:12px;max-width:360px;margin:0 auto}
.epm-ecosystem__node{position:relative;left:auto!important;top:auto!important;transform:none!important;margin:0!important}
.epm-ecosystem__node-inner{animation:none!important;transform:none!important}
.epm-flow-svg{display:none}
.epm-ecosystem__core-badge{position:relative;left:auto;top:auto;transform:none;text-align:center;margin-bottom:16px}
}
CSS;
	return $css . epc_ecomae_platform_tenant_showcase_styles() . '</style>';
}
