<?php
/**
 * Marketing-only tenant storefront + CP animated previews (no live iframes).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_platform_data.php';

function epc_ecomae_platform_tenant_showcase_themes()
{
	return array(
		'auto_parts' => array(
			'industry' => 'auto_parts',
			'theme' => 'automotive_spareparts_pro',
			'label' => 'Automotive spare parts pro',
			'primary' => '#2563eb',
			'accent' => '#ef4444',
			'bg_from' => '#0f172a',
			'bg_to' => '#111827',
			'cp_sidebar' => '#1e293b',
			'cp_accent' => '#3b82f6',
			'hero_type' => 'piston',
		),
		'tax_advisory' => array(
			'industry' => 'tax_advisory',
			'theme' => 'consulting_primeinvest',
			'label' => 'Prime Invest consulting',
			'primary' => '#1e40af',
			'accent' => '#d4af37',
			'bg_from' => '#0f172a',
			'bg_to' => '#1e3a5f',
			'cp_sidebar' => '#191919',
			'cp_accent' => '#227a40',
			'hero_type' => 'consultancy',
		),
		'electronics' => array(
			'industry' => 'electronics',
			'theme' => 'electronics_retail_virgin',
			'label' => 'Virgin-style electronics retail',
			'primary' => '#e10a0a',
			'accent' => '#000000',
			'bg_from' => '#0a0a0a',
			'bg_to' => '#1a1a1a',
			'cp_sidebar' => '#111111',
			'cp_accent' => '#e10a0a',
			'hero_type' => 'electronics',
		),
		'fashion' => array(
			'industry' => 'fashion',
			'theme' => 'fashion_retail_namshi',
			'label' => 'Namshi fashion & beauty',
			'primary' => '#ec4899',
			'accent' => '#be185d',
			'bg_from' => '#fdf2f8',
			'bg_to' => '#fce7f3',
			'cp_sidebar' => '#831843',
			'cp_accent' => '#ec4899',
			'hero_type' => 'fashion',
		),
		'jewellery' => array(
			'industry' => 'jewellery',
			'theme' => 'jewellery_retail_kiyasha',
			'label' => 'Kiyasha jewellery luxury',
			'primary' => '#b8860b',
			'accent' => '#92400e',
			'bg_from' => '#1c1917',
			'bg_to' => '#292524',
			'cp_sidebar' => '#1c1917',
			'cp_accent' => '#d4af37',
			'hero_type' => 'jewellery',
		),
	);
}

function epc_ecomae_platform_tenant_showcase_theme($industryCode)
{
	$themes = epc_ecomae_platform_tenant_showcase_themes();
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $industryCode));
	return isset($themes[$code]) ? $themes[$code] : $themes['auto_parts'];
}

/** Merge live tenant rows with theme metadata for showcase cards. */
function epc_ecomae_platform_tenant_showcase_rows()
{
	$tenants = epc_ecomae_platform_customer_results();
	$map = array(
		'epartscart' => 'auto_parts',
		'taxofinca' => 'tax_advisory',
		'electronicae' => 'electronics',
		'stylenlook' => 'fashion',
		'thejewellerytrend' => 'jewellery',
	);
	$out = array();
	foreach ($tenants as $row) {
		$key = isset($row['key']) ? (string) $row['key'] : '';
		$industry = isset($row['industry']) ? (string) $row['industry'] : (isset($map[$key]) ? $map[$key] : 'auto_parts');
		$row['industry'] = $industry;
		$row['theme_meta'] = epc_ecomae_platform_tenant_showcase_theme($industry);
		$out[] = $row;
	}
	return $out;
}

function epc_ecomae_platform_tenant_showcase_styles()
{
	return <<<'CSS'
.epm-tenant-showcase{margin:28px 0 36px}
.epm-tenant-showcase__head{margin-bottom:22px}
.epm-tenant-showcase__grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:22px}
.epm-tenant-card{background:var(--epm-card);border:1px solid var(--epm-border);border-radius:18px;padding:18px 18px 20px;box-shadow:var(--epm-glow);display:flex;flex-direction:column;gap:14px}
.epm-tenant-card__head{display:flex;align-items:center;gap:12px;flex-wrap:wrap}
.epm-tenant-card__logo{height:44px;width:auto;max-width:160px;object-fit:contain}
.epm-tenant-card__meta{flex:1;min-width:140px}
.epm-tenant-card__meta h3{margin:0 0 4px;font-size:17px;color:#fff}
.epm-tenant-card__meta p{margin:0;font-size:12px;color:var(--epm-muted);line-height:1.45}
.epm-tenant-card__theme{display:inline-block;font-size:10px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:4px 10px;border-radius:999px;border:1px solid rgba(255,255,255,.12);color:var(--epm-cyan)}
.epm-tenant-card__previews{display:grid;grid-template-columns:1fr 1fr;gap:10px}
@media(max-width:520px){.epm-tenant-card__previews{grid-template-columns:1fr}}
.epm-mini-browser{border-radius:12px;overflow:hidden;border:1px solid rgba(148,163,184,.22);background:#020617}
.epm-mini-browser__bar{display:flex;align-items:center;gap:6px;padding:7px 10px;background:rgba(15,23,42,.95);border-bottom:1px solid rgba(148,163,184,.15);font-size:9px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;color:var(--epm-muted)}
.epm-mini-browser__bar span{width:7px;height:7px;border-radius:50%;background:#334155}
.epm-mini-browser__bar span:nth-child(1){background:#ef4444}
.epm-mini-browser__bar span:nth-child(2){background:#eab308}
.epm-mini-browser__bar span:nth-child(3){background:#22c55e}
.epm-mini-browser__bar em{margin-left:auto;font-style:normal}
.epm-mini-hero{position:relative;height:118px;overflow:hidden;display:flex;align-items:center;justify-content:center}
.epm-mini-hero--live{height:auto;min-height:0;display:block;background:#fff}
.epm-mini-hero--live img{width:100%;height:auto;display:block;max-height:200px;object-fit:cover;object-position:top center}
.epm-mini-hero__copy{position:absolute;left:10px;bottom:10px;z-index:2;max-width:58%}
.epm-mini-hero__copy strong{display:block;font-size:11px;color:#fff;line-height:1.25;margin-bottom:2px;text-shadow:0 1px 8px rgba(0,0,0,.55)}
.epm-mini-hero__copy small{font-size:9px;color:rgba(255,255,255,.78);line-height:1.35}
.epm-mini-logo{position:absolute;top:8px;left:10px;z-index:3;transform:scale(.72);transform-origin:left top}
.epm-mini-cp{height:118px;display:grid;grid-template-columns:34% 1fr;font-size:9px;color:#e2e8f0}
.epm-mini-cp__side{padding:8px 6px;display:flex;flex-direction:column;gap:5px}
.epm-mini-cp__side strong{font-size:8px;letter-spacing:.08em;text-transform:uppercase;opacity:.85;margin-bottom:2px}
.epm-mini-cp__item{padding:4px 6px;border-radius:6px;background:rgba(255,255,255,.06);font-size:8px;line-height:1.3}
.epm-mini-cp__item.is-active{font-weight:700;box-shadow:inset 0 0 0 1px rgba(255,255,255,.18)}
.epm-mini-cp__main{padding:8px;background:rgba(2,6,23,.55);display:flex;flex-direction:column;gap:6px}
.epm-mini-cp__kpi{display:grid;grid-template-columns:repeat(3,1fr);gap:4px}
.epm-mini-cp__kpi span{background:rgba(255,255,255,.06);border-radius:6px;padding:5px 4px;text-align:center;font-size:7px;line-height:1.25}
.epm-mini-cp__kpi span b{display:block;font-size:9px;color:#fff}
.epm-mini-cp__rows{display:flex;flex-direction:column;gap:3px;flex:1}
.epm-mini-cp__row{height:7px;border-radius:999px;background:rgba(255,255,255,.08)}
.epm-mini-cp__row:nth-child(2){width:88%}
.epm-mini-cp__row:nth-child(3){width:72%}
/* Animated logos (marketing-scoped) */
.epm-mk-logo{display:inline-flex;align-items:center;gap:6px;font-weight:800;font-size:14px;letter-spacing:.02em}
.epm-mk-logo svg{display:block;height:34px;width:auto}
.epm-mk-logo__text{line-height:1}
.epm-mk-logo--parts .epm-mk-logo__text{color:#fff}
.epm-mk-logo--parts .epm-mk-logo__cart{animation:epmMkCartBob 2.4s ease-in-out infinite}
.epm-mk-logo--parts .epm-mk-logo__wheel{animation:epmMkWheelSpin 1.2s linear infinite;transform-origin:center}
.epm-mk-logo--consult .epm-mk-logo__text{color:#fff}
.epm-mk-logo--consult .epm-mk-logo__bar{animation:epmMkBarGrow 2.2s ease-in-out infinite;transform-origin:bottom}
.epm-mk-logo--consult .epm-mk-logo__bar:nth-child(2){animation-delay:.25s}
.epm-mk-logo--consult .epm-mk-logo__bar:nth-child(3){animation-delay:.5s}
.epm-mk-logo--electronics .epm-mk-logo__text{color:#fff}
.epm-mk-logo--electronics .epm-mk-logo__pulse{animation:epmMkPulse 1.6s ease-in-out infinite}
.epm-mk-logo--fashion .epm-mk-logo__text{color:#be185d}
.epm-mk-logo--fashion .epm-mk-logo__hanger{animation:epmMkSwing 2.8s ease-in-out infinite;transform-origin:top center}
.epm-mk-logo--jewellery .epm-mk-logo__text{color:#d4af37}
.epm-mk-logo--jewellery .epm-mk-logo__spark{animation:epmMkSparkle 1.8s ease-in-out infinite}
.epm-mk-logo--jewellery .epm-mk-logo__spark:nth-child(2){animation-delay:.4s}
.epm-mk-logo--jewellery .epm-mk-logo__spark:nth-child(3){animation-delay:.8s}
/* Mini hero animations */
.epm-mk-piston{display:flex;gap:8px;align-items:flex-end;height:72px;padding:0 12px}
.epm-mk-piston__cyl{width:22px;height:58px;border-radius:8px 8px 4px 4px;border:2px solid rgba(148,163,184,.55);background:#020617;position:relative;overflow:hidden}
.epm-mk-piston__p{position:absolute;left:3px;right:3px;height:18px;border-radius:6px;background:linear-gradient(180deg,#f8fafc,#94a3b8);animation:epmMkPistonA 1.05s linear infinite}
.epm-mk-piston__cyl:nth-child(2) .epm-mk-piston__p{animation-name:epmMkPistonB}
.epm-mk-piston__cyl:nth-child(3) .epm-mk-piston__p{animation-name:epmMkPistonA}
.epm-mk-consult{display:flex;align-items:flex-end;gap:8px;height:72px;padding:0 16px}
.epm-mk-consult__bar{width:16px;border-radius:4px 4px 0 0;background:linear-gradient(180deg,var(--mk-accent,#d4af37),var(--mk-primary,#1e40af));animation:epmMkBarGrow 2s ease-in-out infinite}
.epm-mk-consult__bar:nth-child(1){height:36px}
.epm-mk-consult__bar:nth-child(2){height:52px;animation-delay:.2s}
.epm-mk-consult__bar:nth-child(3){height:44px;animation-delay:.4s}
.epm-mk-consult__bar:nth-child(4){height:28px;animation-delay:.6s}
.epm-mk-electronics{position:relative;width:100%;height:72px;display:flex;align-items:center;justify-content:center;gap:6px}
.epm-mk-electronics__chip{width:48px;height:32px;border-radius:6px;border:2px solid rgba(225,10,10,.65);background:#111;position:relative;overflow:hidden}
.epm-mk-electronics__chip:before{content:"";position:absolute;inset:0;background:linear-gradient(90deg,transparent,rgba(225,10,10,.35),transparent);animation:epmMkScan 1.8s linear infinite}
.epm-mk-electronics__wave{display:flex;gap:3px;align-items:flex-end;height:36px}
.epm-mk-electronics__wave span{width:4px;background:#e10a0a;border-radius:2px;animation:epmMkWave 1.2s ease-in-out infinite}
.epm-mk-electronics__wave span:nth-child(2){animation-delay:.15s;height:22px}
.epm-mk-electronics__wave span:nth-child(3){animation-delay:.3s;height:30px}
.epm-mk-electronics__wave span:nth-child(4){animation-delay:.45s;height:18px}
.epm-mk-electronics__wave span:nth-child(1){height:26px}
.epm-mk-fashion{display:flex;align-items:center;justify-content:center;gap:14px;height:72px}
.epm-mk-fashion__chip{padding:6px 12px;border-radius:999px;background:rgba(236,72,153,.18);border:1px solid rgba(236,72,153,.45);color:#831843;font-size:9px;font-weight:700;animation:epmMkChipFloat 2.4s ease-in-out infinite}
.epm-mk-fashion__chip:nth-child(2){animation-delay:.5s;background:rgba(190,24,93,.15);color:#be185d}
.epm-mk-fashion__dress{width:34px;height:48px;border-radius:18px 18px 8px 8px;background:linear-gradient(180deg,#ec4899,#be185d);animation:epmMkDressSway 3s ease-in-out infinite;transform-origin:top center}
.epm-mk-jewellery{position:relative;width:100%;height:72px;display:flex;align-items:center;justify-content:center}
.epm-mk-jewellery__ring{width:46px;height:46px;border-radius:50%;border:5px solid #d4af37;box-shadow:0 0 18px rgba(212,175,55,.45);animation:epmMkRingGlow 2.2s ease-in-out infinite}
.epm-mk-jewellery__gem{position:absolute;width:10px;height:10px;background:#fff;border-radius:2px;transform:rotate(45deg);box-shadow:0 0 12px rgba(255,255,255,.8)}
@keyframes epmMkCartBob{0%,100%{transform:translateY(0)}50%{transform:translateY(-3px)}}
@keyframes epmMkWheelSpin{to{transform:rotate(360deg)}}
@keyframes epmMkBarGrow{0%,100%{transform:scaleY(.65);opacity:.7}50%{transform:scaleY(1);opacity:1}}
@keyframes epmMkPulse{0%,100%{opacity:.45;transform:scale(.92)}50%{opacity:1;transform:scale(1.05)}}
@keyframes epmMkSwing{0%,100%{transform:rotate(-6deg)}50%{transform:rotate(6deg)}}
@keyframes epmMkSparkle{0%,100%{opacity:.2;transform:scale(.6)}50%{opacity:1;transform:scale(1.2)}}
@keyframes epmMkPistonA{0%,100%{top:6px}50%{top:28px}}
@keyframes epmMkPistonB{0%,100%{top:28px}50%{top:6px}}
@keyframes epmMkScan{0%{transform:translateX(-100%)}100%{transform:translateX(100%)}}
@keyframes epmMkWave{0%,100%{transform:scaleY(.5)}50%{transform:scaleY(1)}}
@keyframes epmMkChipFloat{0%,100%{transform:translateY(0)}50%{transform:translateY(-4px)}}
@keyframes epmMkDressSway{0%,100%{transform:rotate(-4deg)}50%{transform:rotate(4deg)}}
@keyframes epmMkRingGlow{0%,100%{box-shadow:0 0 12px rgba(212,175,55,.35)}50%{box-shadow:0 0 24px rgba(212,175,55,.75)}}
@media(prefers-reduced-motion:reduce){
.epm-mk-logo *,.epm-mk-piston *,.epm-mk-consult *,.epm-mk-electronics *,.epm-mk-fashion *,.epm-mk-jewellery *{animation:none!important}
}
CSS;
}

function epc_ecomae_platform_tenant_animated_logo($industryCode, $label = '')
{
	$theme = epc_ecomae_platform_tenant_showcase_theme($industryCode);
	$type = isset($theme['hero_type']) ? $theme['hero_type'] : 'piston';
	$name = $label !== '' ? $label : $theme['label'];
	$primary = epc_ecomae_h($theme['primary']);
	$accent = epc_ecomae_h($theme['accent']);
	ob_start();
	if ($type === 'piston') {
		?>
<span class="epm-mk-logo epm-mk-logo--parts" aria-hidden="true">
	<span class="epm-mk-logo__text">eparts</span>
	<svg viewBox="0 0 88 40" xmlns="http://www.w3.org/2000/svg" class="epm-mk-logo__cart" aria-hidden="true">
		<path fill="<?php echo $primary; ?>" d="M8 8h44c3 0 5 2 5 5l-4 16H18L8 8z"/>
		<circle class="epm-mk-logo__wheel" cx="22" cy="32" r="6" fill="#334155" stroke="#94a3b8" stroke-width="2"/>
		<circle class="epm-mk-logo__wheel" cx="44" cy="32" r="6" fill="#334155" stroke="#94a3b8" stroke-width="2"/>
		<rect x="30" y="14" width="10" height="8" rx="2" fill="#e2e8f0"/>
	</svg>
</span>
		<?php
	} elseif ($type === 'consultancy') {
		?>
<span class="epm-mk-logo epm-mk-logo--consult" aria-hidden="true">
	<svg viewBox="0 0 48 36" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<rect class="epm-mk-logo__bar" x="4" y="18" width="8" height="14" rx="2" fill="<?php echo $accent; ?>"/>
		<rect class="epm-mk-logo__bar" x="18" y="10" width="8" height="22" rx="2" fill="<?php echo $primary; ?>"/>
		<rect class="epm-mk-logo__bar" x="32" y="14" width="8" height="18" rx="2" fill="<?php echo $accent; ?>"/>
	</svg>
	<span class="epm-mk-logo__text"><?php echo epc_ecomae_h($name); ?></span>
</span>
		<?php
	} elseif ($type === 'electronics') {
		?>
<span class="epm-mk-logo epm-mk-logo--electronics" aria-hidden="true">
	<svg viewBox="0 0 40 36" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<rect x="6" y="8" width="28" height="20" rx="4" fill="#111" stroke="<?php echo $primary; ?>" stroke-width="2"/>
		<circle class="epm-mk-logo__pulse" cx="20" cy="18" r="5" fill="<?php echo $primary; ?>"/>
	</svg>
	<span class="epm-mk-logo__text"><?php echo epc_ecomae_h($name); ?></span>
</span>
		<?php
	} elseif ($type === 'fashion') {
		?>
<span class="epm-mk-logo epm-mk-logo--fashion" aria-hidden="true">
	<svg viewBox="0 0 36 40" xmlns="http://www.w3.org/2000/svg" class="epm-mk-logo__hanger" aria-hidden="true">
		<path d="M18 4c-2 0-3 1.5-3 3.5S16 11 18 11s3-1.5 3-3.5S20 4 18 4z" fill="<?php echo $accent; ?>"/>
		<path d="M18 11 L6 28 L30 28 Z" fill="<?php echo $primary; ?>" opacity=".9"/>
	</svg>
	<span class="epm-mk-logo__text"><?php echo epc_ecomae_h($name); ?></span>
</span>
		<?php
	} else {
		?>
<span class="epm-mk-logo epm-mk-logo--jewellery" aria-hidden="true">
	<svg viewBox="0 0 44 40" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
		<circle cx="22" cy="22" r="14" fill="none" stroke="<?php echo $primary; ?>" stroke-width="4"/>
		<path class="epm-mk-logo__spark" d="M22 6 L24 12 L30 12 L25 16 L27 22 L22 18 L17 22 L19 16 L14 12 L20 12 Z" fill="<?php echo $accent; ?>"/>
	</svg>
	<span class="epm-mk-logo__text"><?php echo epc_ecomae_h($name); ?></span>
</span>
		<?php
	}
	return ob_get_clean();
}

function epc_ecomae_platform_tenant_mini_hero_visual($industryCode)
{
	$theme = epc_ecomae_platform_tenant_showcase_theme($industryCode);
	$type = isset($theme['hero_type']) ? $theme['hero_type'] : 'piston';
	$style = '--mk-primary:' . epc_ecomae_h($theme['primary']) . ';--mk-accent:' . epc_ecomae_h($theme['accent'])
		. ';background:linear-gradient(135deg,' . epc_ecomae_h($theme['bg_from']) . ',' . epc_ecomae_h($theme['bg_to']) . ')';
	ob_start();
	?>
<div class="epm-mini-hero" style="<?php echo $style; ?>">
	<div class="epm-mini-logo"><?php echo epc_ecomae_platform_tenant_animated_logo($industryCode); ?></div>
	<?php if ($type === 'piston') { ?>
	<div class="epm-mk-piston" aria-hidden="true">
		<div class="epm-mk-piston__cyl"><span class="epm-mk-piston__p"></span></div>
		<div class="epm-mk-piston__cyl"><span class="epm-mk-piston__p"></span></div>
		<div class="epm-mk-piston__cyl"><span class="epm-mk-piston__p"></span></div>
	</div>
	<?php } elseif ($type === 'consultancy') { ?>
	<div class="epm-mk-consult" aria-hidden="true">
		<span class="epm-mk-consult__bar"></span><span class="epm-mk-consult__bar"></span><span class="epm-mk-consult__bar"></span><span class="epm-mk-consult__bar"></span>
	</div>
	<?php } elseif ($type === 'electronics') { ?>
	<div class="epm-mk-electronics" aria-hidden="true">
		<span class="epm-mk-electronics__chip"></span>
		<span class="epm-mk-electronics__wave"><span></span><span></span><span></span><span></span></span>
	</div>
	<?php } elseif ($type === 'fashion') { ?>
	<div class="epm-mk-fashion" aria-hidden="true">
		<span class="epm-mk-fashion__chip">New in</span>
		<span class="epm-mk-fashion__dress"></span>
		<span class="epm-mk-fashion__chip">Beauty</span>
	</div>
	<?php } else { ?>
	<div class="epm-mk-jewellery" aria-hidden="true">
		<span class="epm-mk-jewellery__ring"></span>
		<span class="epm-mk-jewellery__gem"></span>
	</div>
	<?php } ?>
	<div class="epm-mini-hero__copy">
		<strong><?php echo epc_ecomae_h($theme['label']); ?></strong>
		<small>Industry-themed storefront hero</small>
	</div>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_tenant_cp_modules($industryCode)
{
	$map = array(
		'auto_parts' => array('Orders', 'Prices', 'Procurement', 'Logistics'),
		'tax_advisory' => array('Clients', 'VAT', 'Documents', 'ERP'),
		'electronics' => array('Catalogue', 'Orders', 'RMA', 'Stock'),
		'fashion' => array('Collections', 'Orders', 'Campaigns', 'Stock'),
		'jewellery' => array('Gallery', 'Enquiries', 'Certificates', 'CRM'),
	);
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $industryCode));
	return isset($map[$code]) ? $map[$code] : array('Orders', 'Catalogue', 'Finance', 'Settings');
}

function epc_ecomae_platform_tenant_cp_preview($industryCode, $label = 'Client CP')
{
	$theme = epc_ecomae_platform_tenant_showcase_theme($industryCode);
	$mods = epc_ecomae_platform_tenant_cp_modules($industryCode);
	$sidebar = epc_ecomae_h($theme['cp_sidebar']);
	$accent = epc_ecomae_h($theme['cp_accent']);
	ob_start();
	?>
<div class="epm-mini-browser">
	<div class="epm-mini-browser__bar"><span></span><span></span><span></span><em><?php echo epc_ecomae_h($label); ?></em></div>
	<div class="epm-mini-cp">
		<div class="epm-mini-cp__side" style="background:<?php echo $sidebar; ?>">
			<strong>Modules</strong>
			<?php foreach ($mods as $i => $mod) { ?>
			<span class="epm-mini-cp__item<?php echo $i === 0 ? ' is-active' : ''; ?>"<?php echo $i === 0 ? ' style="background:' . $accent . ';color:#fff"' : ''; ?>><?php echo epc_ecomae_h($mod); ?></span>
			<?php } ?>
		</div>
		<div class="epm-mini-cp__main">
			<div class="epm-mini-cp__kpi">
				<span><b>24</b>Today</span>
				<span><b>AED</b>Revenue</span>
				<span><b>Live</b>Status</span>
			</div>
			<div class="epm-mini-cp__rows">
				<span class="epm-mini-cp__row"></span><span class="epm-mini-cp__row"></span><span class="epm-mini-cp__row"></span>
			</div>
		</div>
	</div>
</div>
	<?php
	return ob_get_clean();
}

/** Tenant key for an industry when no explicit key is passed (showcase / industry pages). */
function epc_ecomae_platform_tenant_key_for_industry($industryCode)
{
	$map = array(
		'auto_parts' => 'epartscart',
		'tax_advisory' => 'taxofinca',
		'electronics' => 'electronicae',
		'fashion' => 'stylenlook',
		'jewellery' => 'thejewellerytrend',
	);
	$code = preg_replace('/[^a-z0-9_]/', '', strtolower((string) $industryCode));
	return isset($map[$code]) ? $map[$code] : '';
}

/** Live storefront capture: tenant-{key}-storefront.{png,webp,jpg} on disk; served via epc-ecomae-tenant-asset.php when needed. */
function epc_ecomae_platform_tenant_storefront_screenshot($tenantKey)
{
	$key = preg_replace('/[^a-z0-9]/', '', strtolower((string) $tenantKey));
	if ($key === '') {
		return '';
	}
	$slug = 'tenant-' . $key . '-storefront';
	$disk = epc_ecomae_platform_screenshot($slug, false);
	if ($disk === '') {
		return '';
	}
	$ext = 'png';
	if (preg_match('/\.([a-z0-9]+)$/i', $disk, $m)) {
		$ext = strtolower($m[1]);
	}
	return '/epc-ecomae-tenant-asset.php?f=' . rawurlencode($slug . '.' . $ext);
}

function epc_ecomae_platform_tenant_storefront_preview($industryCode, $label = 'Storefront', $tenantKey = '')
{
	if ($tenantKey === '') {
		$tenantKey = epc_ecomae_platform_tenant_key_for_industry($industryCode);
	}
	$shot = epc_ecomae_platform_tenant_storefront_screenshot($tenantKey);
	ob_start();
	?>
<div class="epm-mini-browser">
	<div class="epm-mini-browser__bar"><span></span><span></span><span></span><em><?php echo epc_ecomae_h($label); ?></em></div>
	<?php if ($shot !== '') { ?>
	<div class="epm-mini-hero epm-mini-hero--live">
		<img src="<?php echo epc_ecomae_h($shot); ?>" alt="<?php echo epc_ecomae_h($label); ?> — live storefront" loading="lazy" width="640" height="360" />
	</div>
	<?php } else {
		echo epc_ecomae_platform_tenant_mini_hero_visual($industryCode);
	} ?>
</div>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_tenant_showcase_card(array $tenant)
{
	$industry = isset($tenant['industry']) ? (string) $tenant['industry'] : 'auto_parts';
	$theme = isset($tenant['theme_meta']) ? $tenant['theme_meta'] : epc_ecomae_platform_tenant_showcase_theme($industry);
	$key = isset($tenant['key']) ? (string) $tenant['key'] : '';
	$name = isset($tenant['name']) ? (string) $tenant['name'] : '';
	$outcome = isset($tenant['outcome']) ? (string) $tenant['outcome'] : '';
	$siteUrl = isset($tenant['site_url']) ? (string) $tenant['site_url'] : '#';
	$portalUrl = isset($tenant['portal_url']) ? (string) $tenant['portal_url'] : '#';
	$logoUrl = isset($tenant['logo_url']) ? (string) $tenant['logo_url'] : '';
	ob_start();
	?>
<article class="epm-tenant-card" data-industry="<?php echo epc_ecomae_h($industry); ?>">
	<div class="epm-tenant-card__head">
		<?php if ($logoUrl !== '') { ?>
		<img class="epm-tenant-card__logo" src="<?php echo epc_ecomae_h($logoUrl); ?>" alt="<?php echo epc_ecomae_h($name); ?> logo" loading="lazy" />
		<?php } else { ?>
		<div class="epm-mini-logo"><?php echo epc_ecomae_platform_tenant_animated_logo($industry, $name); ?></div>
		<?php } ?>
		<div class="epm-tenant-card__meta">
			<h3><?php echo epc_ecomae_h($name); ?></h3>
			<p><?php echo epc_ecomae_h($outcome); ?></p>
		</div>
		<span class="epm-tenant-card__theme"><?php echo epc_ecomae_h($theme['label']); ?></span>
	</div>
	<div class="epm-tenant-card__previews">
		<?php
		echo epc_ecomae_platform_tenant_storefront_preview($industry, 'Storefront', $key);
		echo epc_ecomae_platform_tenant_cp_preview($industry, 'Control panel');
		?>
	</div>
	<div class="epm-cta" style="margin-top:4px">
		<a class="epm-btn epm-btn--primary epm-btn--sm" href="<?php echo epc_ecomae_h($siteUrl); ?>" target="_blank" rel="noopener">Visit site <i class="fa fa-external-link"></i></a>
		<a class="epm-btn epm-btn--outline epm-btn--sm" href="<?php echo epc_ecomae_h($portalUrl); ?>" target="_blank" rel="noopener">Open /cp/</a>
	</div>
</article>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_tenant_showcase_section($variant = 'page')
{
	$rows = epc_ecomae_platform_tenant_showcase_rows();
	$base = epc_ecomae_platform_base_url();
	$isHome = ($variant === 'home');
	$sectionCls = 'epm-tenant-showcase' . ($isHome ? ' epm-tenant-showcase--home' : '');
	ob_start();
	?>
<section class="<?php echo epc_ecomae_h($sectionCls); ?>" id="tenant-showcase" aria-labelledby="epm-tenant-showcase-title">
	<div class="epm-wrap">
		<div class="epm-tenant-showcase__head">
			<div class="epm-badge"><i class="fa fa-paint-brush"></i> Live tenant themes</div>
			<h2 class="epm-section-title" id="epm-tenant-showcase-title" style="margin-top:8px">Industry storefronts in production</h2>
			<p class="epm-section-lead" style="max-width:860px;margin-bottom:0">eParts Cart uses a live capture from <a href="https://www.epartscart.com/" target="_blank" rel="noopener" style="color:var(--epm-cyan)">epartscart.com</a> (piston hero, AI Parts Expert, automotive theme). Other tenants show animated industry heroes until storefront screenshots are added. CP cards use tenant colours, not generic gray mocks.</p>
		</div>
		<div class="epm-tenant-showcase__grid">
			<?php foreach ($rows as $tenant) {
				echo epc_ecomae_platform_tenant_showcase_card($tenant);
			} ?>
		</div>
		<?php if ($isHome) { ?>
		<div class="epm-cta" style="margin-top:18px">
			<a class="epm-btn epm-btn--primary" href="<?php echo epc_ecomae_h($base); ?>platform/customer-results"><i class="fa fa-trophy"></i> Full customer results</a>
			<a class="epm-btn epm-btn--outline" href="<?php echo epc_ecomae_h($base); ?>platform/industries"><i class="fa fa-industry"></i> Browse industries</a>
		</div>
		<?php } ?>
	</div>
</section>
	<?php
	return ob_get_clean();
}

function epc_ecomae_platform_industry_themed_previews($industryCode, $industryName)
{
	$tenantKey = epc_ecomae_platform_tenant_key_for_industry($industryCode);
	$hasLiveShot = epc_ecomae_platform_tenant_storefront_screenshot($tenantKey) !== '';
	$storeCaption = $hasLiveShot
		? 'Live storefront capture from production — e.g. epartscart.com hero, search, and piston animation.'
		: 'Animated industry hero with tenant brand colours — marketing preview until a screenshot is added.';
	ob_start();
	?>
<div class="epm-area__shots" style="margin-bottom:32px">
	<figure class="epm-preview">
		<?php echo epc_ecomae_platform_tenant_storefront_preview($industryCode, 'Storefront — ' . $industryName, $tenantKey); ?>
		<figcaption><strong>Storefront — <?php echo epc_ecomae_h($industryName); ?></strong><p><?php echo epc_ecomae_h($storeCaption); ?></p></figcaption>
	</figure>
	<figure class="epm-preview">
		<?php echo epc_ecomae_platform_tenant_cp_preview($industryCode, 'Client CP — ' . $industryName); ?>
		<figcaption><strong>Client CP — <?php echo epc_ecomae_h($industryName); ?></strong><p>Industry-themed sidebar and module packs — not a generic login screen.</p></figcaption>
	</figure>
</div>
	<?php
	return ob_get_clean();
}
