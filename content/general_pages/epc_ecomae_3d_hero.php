<?php
/**
 * ecomae.com — premium full-bleed 3D marketing hero.
 */
defined('_ASTEXE_') or die('No access');

function epc_ecomae_3d_hero_asset_ver()
{
	return '20260716b';
}

function epc_ecomae_3d_hero_enqueue()
{
	static $done = false;
	if ($done) {
		return '';
	}
	$done = true;
	$v = rawurlencode(epc_ecomae_3d_hero_asset_ver());
	return '<link rel="preconnect" href="https://fonts.googleapis.com" />' . "\n"
		. '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />' . "\n"
		. '<link href="https://fonts.googleapis.com/css2?family=Syne:wght@600;700;800&family=DM+Sans:opsz,wght@9..40,400;9..40,500;9..40,600;9..40,700&display=swap" rel="stylesheet" />' . "\n"
		. '<link rel="stylesheet" href="/epc-static.php?f=content/general_pages/epc_ecomae_3d_hero.css&v=' . $v . '" />' . "\n"
		. '<script defer src="/epc-static.php?f=content/general_pages/epc_ecomae_3d_hero.js&v=' . $v . '"></script>' . "\n";
}

/**
 * Premium 3D homepage hero — brand-first, full-bleed WebGL plane with CSS fallback.
 *
 * @param string $base
 * @param string $superCp
 * @param int $demoDays
 * @return string
 */
function epc_ecomae_3d_hero_render($base, $superCp, $demoDays = 3)
{
	$logo = function_exists('epc_ecomae_platform_logo_url')
		? epc_ecomae_platform_logo_url()
		: '/content/general_pages/epc_ecomae_logo_svg.php';
	$continuityUrl = rtrim((string) $base, '/') . '/platform/business-continuity';
	$demoUrl = rtrim((string) $base, '/') . '/platform/demo';
	$h = 'epc_ecomae_h';
	if (!function_exists($h)) {
		$h = function ($v) {
			return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
		};
	}

	ob_start();
	echo epc_ecomae_3d_hero_enqueue();
	?>
<section class="epm-3d-hero-section" aria-label="ECOM AE platform">
	<div class="epm-3d-hero" id="epm-3d-hero">
		<canvas class="epm-3d-hero__canvas" aria-hidden="true"></canvas>
		<div class="epm-3d-hero__fallback" aria-hidden="true">
			<div class="epm-3d-hero__stage">
				<div class="epm-3d-hero__ring"></div>
				<div class="epm-3d-hero__panel epm-3d-hero__panel--core">ECOM AE<small>One cloud</small></div>
				<div class="epm-3d-hero__panel epm-3d-hero__panel--a">Storefront<small>Commerce</small></div>
				<div class="epm-3d-hero__panel epm-3d-hero__panel--b">Control Panel<small>Operations</small></div>
				<div class="epm-3d-hero__panel epm-3d-hero__panel--c">ERP + CRM<small>Finance</small></div>
				<div class="epm-3d-hero__panel epm-3d-hero__panel--d">Super CP<small>Multi-tenant</small></div>
			</div>
		</div>
		<div class="epm-3d-hero__veil" aria-hidden="true"></div>
		<div class="epm-3d-hero__content">
			<img class="epm-3d-hero__logo" src="<?php echo $h($logo); ?>" alt="ECOM AE" width="220" height="auto" />
			<p class="epm-3d-hero__brand">ECOM <span>AE</span></p>
			<h1 class="epm-3d-hero__headline">One cloud for commerce, ERP &amp; CRM</h1>
			<p class="epm-3d-hero__support">Go live in 24 hours with UAE e-invoice built in — one hosted stack, isolated tenants, <a href="<?php echo $h($continuityUrl); ?>#cloud-continuity">backup continuity</a> included.</p>
			<div class="epm-3d-hero__cta">
				<a class="epm-btn epm-btn--primary" href="<?php echo $h($demoUrl); ?>"><i class="fa fa-play-circle"></i> <?php echo (int) $demoDays; ?>-day demo</a>
				<a class="epm-btn epm-btn--ghost" href="<?php echo $h($superCp); ?>"><i class="fa fa-th-large"></i> Super CP</a>
			</div>
		</div>
		<div class="epm-3d-hero__scroll" aria-hidden="true">
			<span>Explore</span>
			<span class="epm-3d-hero__scroll-line"></span>
		</div>
	</div>
</section>
	<?php
	return ob_get_clean();
}
