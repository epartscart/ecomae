<?php
/**
 * Animated eParts Cart logo (moving cart) — storefront + CP/ERP shared.
 *
 * Markup matches the storefront SVG; CSS is self-contained so admin shells
 * (CP header, ERP top bar, login) can animate without loading the storefront shell.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Whether this request should show the animated epartscart cart mark
 * (tenant host or auto_parts commerce site), not the ECOM AE hub.
 */
function epc_animated_epartscart_logo_applies(): bool
{
	if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
		return true;
	}
	$host = '';
	if (function_exists('epc_portal_host')) {
		$host = strtolower(trim((string) epc_portal_host()));
	}
	if ($host === '') {
		$host = strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? '')));
	}
	$host = preg_replace('/^www\./', '', (string) $host);
	$host = preg_replace('/:\d+$/', '', (string) $host);
	if ($host === 'epartscart.com') {
		return true;
	}
	if (function_exists('epc_portal_is_platform_operator_host') && epc_portal_is_platform_operator_host()) {
		return false;
	}
	if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
		return false;
	}
	// Auto-parts commerce tenants that use the same cart mark on the storefront.
	if (function_exists('epc_portal_cp_active_industry')) {
		$ind = (string) epc_portal_cp_active_industry();
		if ($ind === 'auto_parts' && function_exists('epc_portal_commerce_storefront_enabled')
			&& epc_portal_commerce_storefront_enabled()) {
			return true;
		}
	}
	return false;
}

/** Compact CSS for admin chrome (header / topbar / login). */
function epc_animated_epartscart_logo_css(): string
{
	return <<<'CSS'
.epc-animated-logo{align-items:center;display:inline-flex;gap:8px;line-height:1;max-width:100%;white-space:nowrap;vertical-align:middle}
.epc-animated-logo__mark{display:inline-flex;flex:0 0 auto;height:40px;width:90px}
.epc-animated-logo__mark svg{display:block;height:100%;overflow:visible;width:100%}
.epc-animated-logo__text{color:#dc2626!important;font-family:Arial,Helvetica,sans-serif;font-size:28px;font-style:italic;font-weight:900;letter-spacing:-.055em;text-transform:lowercase;transform:skewX(-8deg)}
.epc-logo-speed,.epc-logo-cart,.epc-logo-handle,.epc-logo-basket{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-linejoin:round}
.epc-logo-cart{stroke-width:12}
.epc-logo-handle,.epc-logo-basket{stroke-width:9}
.epc-logo-speed{stroke-width:8;animation:epcLogoSpeed 1.4s ease-in-out infinite}
.epc-logo-road{fill:none;stroke:#dc2626;stroke-dasharray:14 12;stroke-linecap:round;stroke-width:5;animation:epcLogoRoadMove .9s linear infinite;opacity:.55}
.epc-logo-speed--two{animation-delay:.15s}
.epc-logo-speed--three{animation-delay:.3s}
.epc-logo-cart-motion{animation:epcLogoCartDrive 1.2s ease-in-out infinite;transform-box:fill-box;transform-origin:center}
.epc-logo-gear{animation:epcLogoGearSpin 2.4s linear infinite;transform-box:fill-box;transform-origin:center}
.epc-logo-gear path,.epc-logo-gear circle{fill:#dc2626}
.epc-logo-gear .epc-logo-gear-hole{fill:#fff}
.epc-logo-parts{animation:epcLogoPartsBounce 1.6s ease-in-out infinite}
.epc-logo-piston rect,.epc-logo-box{fill:#dc2626}
.epc-logo-piston path{fill:none;stroke:#fff;stroke-linecap:round;stroke-width:3}
.epc-logo-ring circle,.epc-logo-ring path{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-width:5}
.epc-logo-wheel{filter:drop-shadow(0 2px 0 rgba(0,0,0,.08))}
.epc-logo-wheel-spin{animation:epcLogoWheelRoll .45s linear infinite;transform-box:fill-box;transform-origin:center}
.epc-logo-tyre{fill:#dc2626}
.epc-logo-wheel-rim{fill:#fff}
.epc-logo-wheel .epc-logo-wheel-hole{fill:#dc2626}
.epc-logo-wheel-spokes,.epc-logo-wheel-tread{fill:none;stroke:#dc2626;stroke-linecap:round;stroke-width:2.4}
.epc-logo-wheel-tread{stroke:#fff;stroke-width:2.8}
@keyframes epcLogoSpeed{0%,100%{opacity:.42;transform:translateX(0)}50%{opacity:1;transform:translateX(-10px)}}
@keyframes epcLogoGearSpin{to{transform:rotate(360deg)}}
@keyframes epcLogoCartDrive{0%,100%{transform:translateX(-4px) translateY(0)}50%{transform:translateX(8px) translateY(2px)}}
@keyframes epcLogoWheelRoll{to{transform:rotate(360deg)}}
@keyframes epcLogoRoadMove{to{stroke-dashoffset:-26}}
@keyframes epcLogoPartsBounce{0%,100%{transform:translateY(0)}50%{transform:translateY(-3px)}}
/* CP header */
#logo.epc-cp-header-logo .epc-animated-logo__mark,.epc-cp-header-logo .epc-animated-logo__mark{height:36px;width:80px}
#logo.epc-cp-header-logo .epc-animated-logo__text,.epc-cp-header-logo .epc-animated-logo__text{font-size:24px}
#logo.epc-cp-header-logo{overflow:visible!important;min-width:150px}
/* eParts cart on CP header — light bar so red mark reads (override blue hub plate) */
body.epc-cp-shell #header #logo.epc-cp-header-logo:has(.epc-animated-logo),
body.epc-cp-shell #header #logo.light-version:has(.epc-animated-logo){
	background:#fff!important;
	border-right:1px solid #fecaca!important;
}
body.epc-cp-shell #header #logo .epc-animated-logo,
body.epc-cp-shell #header #logo .epc-animated-logo *{
	animation-play-state:running!important;
}
body.epc-cp-shell #header #logo .epc-logo-cart-motion{animation:epcLogoCartDrive 1.2s ease-in-out infinite!important}
body.epc-cp-shell #header #logo .epc-logo-wheel-spin{animation:epcLogoWheelRoll .45s linear infinite!important}
body.epc-cp-shell #header #logo .epc-logo-speed{animation:epcLogoSpeed 1.4s ease-in-out infinite!important}
body.epc-cp-shell #header #logo .epc-logo-road{animation:epcLogoRoadMove .9s linear infinite!important}
body.epc-cp-shell #header #logo .epc-logo-gear{animation:epcLogoGearSpin 2.4s linear infinite!important}
body.epc-cp-shell #header #logo .epc-logo-parts{animation:epcLogoPartsBounce 1.6s ease-in-out infinite!important}
/* ERP top bar */
.epc-erp-topbar__brand .epc-animated-logo{gap:6px}
.epc-erp-topbar__brand .epc-animated-logo__mark{height:34px;width:76px}
.epc-erp-topbar__brand .epc-animated-logo__text{font-size:22px;color:#fff!important}
.epc-erp-topbar__brand-mark--animated{background:rgba(255,255,255,.12);border-radius:10px;display:inline-flex;padding:4px 10px 4px 8px}
.epc-erp-topbar__brand-mark--animated .epc-animated-logo__text{color:#dc2626!important}
.epc-erp-topbar__brand .epc-logo-cart-motion{animation:epcLogoCartDrive 1.2s ease-in-out infinite!important}
.epc-erp-topbar__brand .epc-logo-wheel-spin{animation:epcLogoWheelRoll .45s linear infinite!important}
.epc-erp-topbar__brand .epc-logo-speed{animation:epcLogoSpeed 1.4s ease-in-out infinite!important}
.epc-erp-topbar__brand .epc-logo-road{animation:epcLogoRoadMove .9s linear infinite!important}
.epc-erp-topbar__brand .epc-logo-gear{animation:epcLogoGearSpin 2.4s linear infinite!important}
.epc-erp-topbar__brand .epc-logo-parts{animation:epcLogoPartsBounce 1.6s ease-in-out infinite!important}
/* Login / compact */
.epc-animated-logo--login .epc-animated-logo__mark{height:56px;width:124px}
.epc-animated-logo--login .epc-animated-logo__text{font-size:36px}
.epc-animated-logo--compact .epc-animated-logo__mark{height:32px;width:72px}
.epc-animated-logo--compact .epc-animated-logo__text{font-size:20px}
.epc-cp-dash-brand .epc-animated-logo__mark{height:44px;width:98px}
.epc-cp-dash-brand .epc-animated-logo__text{font-size:30px}
.epc-erp-login-tenant-brand--animated .epc-erp-login-tenant-brand__title{display:none}
.epc-erp-login-tenant-brand--animated{align-items:flex-start;display:flex;flex-direction:column;gap:10px}
@media (prefers-reduced-motion:reduce){
	.epc-logo-cart-motion,.epc-logo-wheel-spin,.epc-logo-speed,.epc-logo-road,.epc-logo-gear,.epc-logo-parts{animation:none!important}
}
CSS;
}

function epc_animated_epartscart_logo_enqueue(): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	$css = epc_animated_epartscart_logo_css();
	if (!isset($GLOBALS['epc_head_extra_html'])) {
		$GLOBALS['epc_head_extra_html'] = '';
	}
	// Always echo a style block when called from templates (reliable in CP/ERP).
	echo '<style id="epc-animated-epartscart-logo-css">' . $css . '</style>' . "\n";
}

/**
 * @param string $variant header|compact|login|dash
 */
function epc_animated_epartscart_logo_markup(string $variant = 'header'): string
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';
	$html = epc_portal_storefront_epartscart_svg_markup();
	$variant = preg_replace('/[^a-z0-9_-]/', '', strtolower($variant)) ?: 'header';
	if ($variant !== 'header' && $variant !== '') {
		$html = preg_replace(
			'/class="epc-animated-logo"/',
			'class="epc-animated-logo epc-animated-logo--' . htmlspecialchars($variant, ENT_QUOTES, 'UTF-8') . '"',
			$html,
			1
		);
	}
	return $html;
}

/**
 * Admin shell brand mark: animated cart on epartscart / auto-parts, else null
 * (caller keeps platform logo).
 *
 * @param string $variant header|compact|login|dash
 */
function epc_admin_shell_animated_logo_markup(string $variant = 'header'): string
{
	if (!epc_animated_epartscart_logo_applies()) {
		return '';
	}
	return epc_animated_epartscart_logo_markup($variant);
}
