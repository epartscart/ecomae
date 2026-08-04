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
	$ogAlt = epc_ecomae_h('ECOM AE — One Blockchain BOS Enterprise System: ERP, commerce, compliance, workflows, CRM and cryptographic proof.');
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
		'home' => 'ECOM AE — the multi-tenant Blockchain BOS Enterprise System combining ERP, commerce, compliance, workflows, industry intelligence and cryptographic proof for organizations worldwide.',
		'platform' => 'Explore the ECOM AE Blockchain BOS Enterprise System: ERP, commerce, compliance, workflows, CRM, industry intelligence and verifiable proofs on one multi-tenant cloud.',
		'capabilities' => 'ECOM AE Blockchain BOS Enterprise capabilities — ERP, commerce, compliance, workflow automation, industry intelligence, proof anchoring, and operator Super CP.',
		'auto_price_ai' => 'Auto Price AI — discover, compare, and import products across market sources with margin rules and catalogue sync.',
		'faq' => '105 honest answers on automotive catalog, B2B, supply chain, UAE ERP, AI, infrastructure, and licensing.',
		'pricing' => 'Transparent monthly rental plans for ECOM AE cloud — e-commerce, ERP, and CRM for UAE businesses.',
		'demo' => 'Request a 3-day industry demo tenant on ECOM AE — explore storefront, ERP, and Super CP workflows.',
		'contact' => 'Contact ECOM AE for platform demos, tenant onboarding, and ERP cloud consultations in UAE.',
		'industries' => 'Industry-specific ECOM AE solutions — auto parts, retail, electronics, fashion, and more.',
		'free_tools' => 'Free, country-driven business tools — VAT/GST return, corporate tax, payroll & gratuity, IFRS financials, e-invoice and approval workflow. Register free and use for your own company.',
		'blockchain' => 'ECOM AE Blockchain BOS — architecture layers, proof process (hash → Merkle anchor → verify), tenant modes, auto-proven documents, and operator surfaces for enterprise trust.',
		'legal' => 'Privacy, Terms, Security, Trademark, Right to Use, Copyright, Data Protection, and other legal policies for ECOM AE Blockchain BOS.',
	);
	if ($page === 'legal') {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_legal_pages.php';
		return epc_ecomae_legal_meta($params)[1];
	}
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
			<a href="<?php echo epc_ecomae_h($base); ?>brochure">Product brochure</a>
			<a href="<?php echo epc_ecomae_h($base); ?>brochure/cp">Full CP brochure</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/customer-results">Customer results</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/industries">Industries</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/pricing">Pricing</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/demo">Demo</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/faq">FAQ</a>
			<a href="<?php echo epc_ecomae_h($base); ?>documentation">Documentation</a>
			<a href="<?php echo epc_ecomae_h($base); ?>compare">Compare</a>
			<a href="<?php echo epc_ecomae_h($base); ?>blockchain">Blockchain BOS</a>
			<a href="<?php echo epc_ecomae_h($base); ?>bos">What is Blockchain BOS</a>
			<a href="<?php echo epc_ecomae_h($base); ?>solutions">Solutions</a>
			<a href="<?php echo epc_ecomae_h($base); ?>platform/contact">Contact</a>
		</div>
		<div class="epm-footer__legal">
			<p class="epm-footer__legal-label">Legal &amp; security</p>
			<div class="epm-footer__links epm-footer__links--legal">
				<a href="<?php echo epc_ecomae_h($base); ?>legal">All policies</a>
				<a href="<?php echo epc_ecomae_h($base); ?>privacy">Privacy</a>
				<a href="<?php echo epc_ecomae_h($base); ?>terms">Terms</a>
				<a href="<?php echo epc_ecomae_h($base); ?>cookie-policy">Cookies</a>
				<a href="<?php echo epc_ecomae_h($base); ?>security-policy">Security</a>
				<a href="<?php echo epc_ecomae_h($base); ?>right-to-use">Right to use</a>
				<a href="<?php echo epc_ecomae_h($base); ?>trademark">Trademark</a>
				<a href="<?php echo epc_ecomae_h($base); ?>copyright">Copyright</a>
				<a href="<?php echo epc_ecomae_h($base); ?>data-protection">Data protection</a>
				<a href="<?php echo epc_ecomae_h($base); ?>acceptable-use">Acceptable use</a>
				<a href="<?php echo epc_ecomae_h($base); ?>confidentiality">Confidentiality</a>
				<a href="<?php echo epc_ecomae_h($base); ?>intellectual-property">Intellectual property</a>
				<a href="<?php echo epc_ecomae_h($base); ?>blockchain-disclaimer">Blockchain disclaimer</a>
				<a href="<?php echo epc_ecomae_h($base); ?>dmca">IP notice</a>
			</div>
		</div>
		<p class="epm-footer__copy">&copy; <?php echo date('Y'); ?> Electronic World Group · Dubai, UAE · <a href="<?php echo epc_ecomae_h($base); ?>legal">Legal policies</a></p>
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
<?php
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php';
	if (function_exists('epc_web_tracker_beacon_html')) {
		echo epc_web_tracker_beacon_html();
	}
}
?>
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
			<stop offset="0%" stop-color="#0284c7" stop-opacity=".25"/>
			<stop offset="50%" stop-color="#0ea5e9" stop-opacity="1"/>
			<stop offset="100%" stop-color="#0284c7" stop-opacity=".25"/>
		</linearGradient>
		<radialGradient id="<?php echo $uid; ?>Core" cx="50%" cy="50%" r="50%">
			<stop offset="0%" stop-color="#0ea5e9" stop-opacity=".45"/>
			<stop offset="100%" stop-color="#0ea5e9" stop-opacity="0"/>
		</radialGradient>
		<filter id="<?php echo $uid; ?>Glow"><feGaussianBlur stdDeviation="2" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
	</defs>
	<ellipse cx="<?php echo $cx; ?>" cy="<?php echo $cy; ?>" rx="<?php echo (int) ($rxO * 0.55); ?>" ry="<?php echo (int) ($ryO * 0.55); ?>" fill="url(#<?php echo $uid; ?>Core)" class="epm-flow-svg__pulse"/>
	<path id="<?php echo $uid; ?>Outer" d="<?php echo epc_ecomae_h($outer); ?>" fill="none" stroke="url(#<?php echo $uid; ?>Grad)" stroke-width="<?php echo $compact ? 2 : 3; ?>" stroke-linecap="round" class="epm-flow-svg__ring epm-flow-svg__ring--outer"/>
	<path d="<?php echo epc_ecomae_h($outer); ?>" fill="none" stroke="#0ea5e9" stroke-width="1.5" stroke-linecap="round" stroke-dasharray="6 8" class="epm-flow-svg__dash"/>
	<path id="<?php echo $uid; ?>Inner" d="<?php echo epc_ecomae_h($inner); ?>" fill="none" stroke="rgba(14,165,233,.35)" stroke-width="1" stroke-dasharray="4 6" class="epm-flow-svg__ring epm-flow-svg__ring--inner"/>
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
		<circle r="<?php echo $r; ?>" fill="#0ea5e9" class="epm-flow-svg__packet">
			<animateMotion dur="<?php echo $dur; ?>s" repeatCount="indefinite" begin="<?php echo $begin; ?>" path="<?php echo epc_ecomae_h($outer); ?>"/>
		</circle>
		<?php if (!$compact && $p % 2 === 0) { ?>
		<text class="epm-flow-svg__label" dy="-11" font-size="9" fill="#fca5a5" text-anchor="middle">
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
	<circle r="<?php echo $compact ? 3.5 : 4.5; ?>" fill="#0284c7" opacity=".9" class="epm-flow-svg__packet epm-flow-svg__packet--inner">
		<animateMotion dur="<?php echo $dur; ?>s" repeatCount="indefinite" begin="<?php echo ($p * 0.32); ?>s" path="<?php echo epc_ecomae_h($inner); ?>"/>
	</circle>
		<?php
	}
	?>
	<circle cx="<?php echo $cx; ?>" cy="<?php echo $cy; ?>" r="<?php echo $compact ? 22 : 34; ?>" fill="none" stroke="#0ea5e9" stroke-width="1" class="epm-flow-svg__core-ring"/>
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
				<stop offset="0%" stop-color="#0284c7"/>
				<stop offset="50%" stop-color="#0ea5e9"/>
				<stop offset="100%" stop-color="#0284c7"/>
			</linearGradient>
			<marker id="epmFfArrow" markerWidth="8" markerHeight="8" refX="6" refY="4" orient="auto">
				<path d="M0,0 L8,4 L0,8 Z" fill="#0ea5e9"/>
			</marker>
		</defs>
		<path class="epm-failover-flow__path epm-failover-flow__path--shop-cloud" d="M 168 150 L 318 150" fill="none" stroke="url(#epmFfGrad)" stroke-width="3" marker-end="url(#epmFfArrow)"/>
		<path class="epm-failover-flow__path epm-failover-flow__path--cloud-backup" d="M 562 150 L 712 150" fill="none" stroke="rgba(14,165,233,.55)" stroke-width="2.5" stroke-dasharray="8 6" marker-end="url(#epmFfArrow)"/>
		<path class="epm-failover-flow__path epm-failover-flow__path--backup-cloud" d="M 712 178 L 562 178" fill="none" stroke="rgba(14,165,233,.45)" stroke-width="2" stroke-dasharray="6 5" marker-end="url(#epmFfArrow)"/>
		<path class="epm-failover-flow__path epm-failover-flow__path--sync-back" d="M 440 198 Q 300 248 168 198" fill="none" stroke="rgba(52,211,153,.5)" stroke-width="2" stroke-dasharray="5 5" marker-end="url(#epmFfArrow)"/>
		<circle class="epm-failover-flow__packet epm-failover-flow__packet--a" r="5" fill="#0ea5e9">
			<animateMotion dur="2.8s" repeatCount="indefinite" path="M 168 150 L 318 150"/>
		</circle>
		<circle class="epm-failover-flow__packet epm-failover-flow__packet--b" r="4.5" fill="#0284c7">
			<animateMotion dur="2.4s" repeatCount="indefinite" begin="0.6s" path="M 562 150 L 712 150"/>
		</circle>
		<circle class="epm-failover-flow__packet epm-failover-flow__packet--c" r="4" fill="#34d399">
			<animateMotion dur="3.2s" repeatCount="indefinite" begin="1.1s" path="M 712 178 L 562 178"/>
		</circle>
		<g class="epm-failover-flow__node epm-failover-flow__node--shop">
			<rect x="24" y="108" width="144" height="84" rx="14" fill="#171717" stroke="rgba(14,165,233,.45)" stroke-width="1.5"/>
			<text x="96" y="142" text-anchor="middle" fill="#e2e8f0" font-size="13" font-weight="700">Customer</text>
			<text x="96" y="162" text-anchor="middle" fill="#94a3b8" font-size="10">Storefront</text>
		</g>
		<g class="epm-failover-flow__node epm-failover-flow__node--cloud">
			<rect x="318" y="88" width="244" height="124" rx="16" fill="#171717" stroke="rgba(14,165,233,.65)" stroke-width="2"/>
			<text x="440" y="128" text-anchor="middle" fill="#fff" font-size="14" font-weight="800">Primary cloud</text>
			<text x="440" y="148" text-anchor="middle" fill="#0ea5e9" font-size="10" font-weight="700" letter-spacing=".12em">ECOM AE · ALWAYS ON</text>
			<text x="440" y="168" text-anchor="middle" fill="#94a3b8" font-size="10">Store · CP · ERP</text>
		</g>
		<g class="epm-failover-flow__node epm-failover-flow__node--backup">
			<rect x="712" y="108" width="144" height="84" rx="14" fill="#171717" stroke="rgba(14,165,233,.5)" stroke-width="1.5"/>
			<text x="784" y="142" text-anchor="middle" fill="#e2e8f0" font-size="13" font-weight="700">Backup</text>
			<text x="784" y="162" text-anchor="middle" fill="#94a3b8" font-size="10">On-prem / mirror</text>
		</g>
		<text class="epm-failover-flow__phase-label" x="440" y="36" text-anchor="middle" fill="#0ea5e9" font-size="11" font-weight="700" letter-spacing=".14em">NORMAL · DETECT · BACKUP · SYNC</text>
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
		array('icon' => 'fa-lock', 'label' => 'Blockchain proof layer'),
		array('icon' => 'fa-shield', 'label' => 'Cloud + backup continuity'),
		array('icon' => 'fa-cloud-upload', 'label' => 'Super CP provisioning'),
	);
	ob_start();
	?>
<section class="epm-hub-section" aria-label="ECOM AE Blockchain BOS Enterprise hub">
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
			<p class="epm-hub__pill" style="margin-bottom:10px"><i class="fa fa-cubes"></i> The multi-tenant Blockchain BOS Enterprise System</p>
			<h1 class="epm-hub__headline" aria-label="Blockchain BOS Enterprise System — ERP, Commerce, Compliance, Workflows, Industry Intelligence, Cryptographic Proof">
				<span class="epm-hub__headline-line epm-hub__headline-line--stack epm-hub__headline-line--commerce">Blockchain</span>
				<span class="epm-hub__headline-line epm-hub__headline-line--stack">BOS</span>
				<span class="epm-hub__headline-line epm-hub__headline-line--stack">Enterprise</span>
				<span class="epm-hub__headline-line epm-hub__headline-line--cloud">ONE SYSTEM</span>
			</h1>
			<p class="epm-hub__tagline-sub">ERP · Commerce · Compliance · Workflows · Intelligence · Blockchain Proof — one unified cloud, <a href="<?php echo epc_ecomae_h($continuityUrl); ?>#cloud-continuity" style="color:var(--epm-cyan);text-decoration:none;border-bottom:1px dotted rgba(14,165,233,.5)">backup continuity</a> built in.</p>
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
			<p class="epm-hub__platform-title">One Blockchain BOS Enterprise System for the whole organization</p>
			<p class="epm-hub__platform-sub">ECOM AE is one unified Blockchain BOS Enterprise System — ERP, commerce, compliance, workflows, industry intelligence and cryptographic proof on a single multi-tenant cloud.</p>
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
		array('icon' => 'fa-lock', 'label' => 'Blockchain proof layer'),
		array('icon' => 'fa-shield', 'label' => 'Cloud + backup continuity'),
		array('icon' => 'fa-cloud-upload', 'label' => 'Super CP provisioning'),
	);
	ob_start();
	?>
<section class="epm-static-hero-section" aria-label="ECOM AE Blockchain BOS Enterprise">
	<div class="epm-static-hero">
		<div class="epm-static-hero__bg" aria-hidden="true"></div>
		<div class="epm-static-hero__inner">
			<img class="epm-static-hero__logo" src="<?php echo epc_ecomae_h($logo); ?>" alt="ECOM AE" width="200" height="auto" />
			<p class="epm-static-hero__tagline" style="margin-bottom:8px;opacity:.92"><i class="fa fa-cubes"></i> The multi-tenant Blockchain BOS Enterprise System</p>
			<h1 class="epm-static-hero__headline" aria-label="Blockchain BOS Enterprise System — ERP, Commerce, Compliance, Workflows, Industry Intelligence, Cryptographic Proof">
				<span class="epm-static-hero__line epm-static-hero__line--stack epm-static-hero__line--commerce">Blockchain</span>
				<span class="epm-static-hero__line epm-static-hero__line--stack">BOS</span>
				<span class="epm-static-hero__line epm-static-hero__line--stack">Enterprise</span>
				<span class="epm-static-hero__line epm-static-hero__line--cloud">ONE SYSTEM</span>
			</h1>
			<p class="epm-static-hero__tagline">ERP · Commerce · Compliance · Workflows · Intelligence · Blockchain Proof — one unified cloud, <a href="<?php echo epc_ecomae_h($continuityUrl); ?>#cloud-continuity">backup continuity</a> built in.</p>
			<div class="epm-static-hero__cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/demo"><i class="fa fa-play-circle"></i> <?php echo (int) $demoDays; ?>-day demo</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo epc_ecomae_h($superCp); ?>"><i class="fa fa-th-large"></i> Super CP</a>
				<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform">Platform overview</a>
			</div>
		</div>
		<div class="epm-static-hero__platform">
			<p class="epm-static-hero__platform-title">One Blockchain BOS Enterprise System for the whole organization</p>
			<p class="epm-static-hero__platform-sub">One unified Blockchain BOS Enterprise System — ERP, commerce, compliance, workflows, industry intelligence and cryptographic proof on a single multi-tenant cloud.</p>
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
	// Shared marketing chrome CSS (animated epm-hub + topbar/nav/footer).
	// Same file is loaded by ASP.NET /marketing/app via LegacyPresentationAssets.
	$v = '20260804marketing1';
	$out = '<link rel="stylesheet" href="/content/general_pages/epc_ecomae_platform_marketing_css.php?v=' . rawurlencode($v) . '" />' . "\n";
	$extra = epc_ecomae_platform_tenant_showcase_styles();
	if (is_string($extra) && $extra !== '') {
		$out .= '<style>' . $extra . '</style>';
	}
	return $out;
}
