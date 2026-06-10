<?php
/**
 * Storefront heading logo — ECOM AE animated hub or legacy eparts cart mark.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_brand.php';

function epc_portal_storefront_hub_logo_setting(): ?bool
{
	$settings = epc_portal_load_site_settings();
	$contact = isset($settings['contact']) && is_array($settings['contact']) ? $settings['contact'] : array();
	if (array_key_exists('use_animated_hub_logo', $contact)) {
		return !empty($contact['use_animated_hub_logo']);
	}
	return null;
}

/** Whether the storefront header should render the ECOM AE animated hub. */
function epc_portal_storefront_hub_enabled(): bool
{
	if (!empty($GLOBALS['epc_demo_storefront_context'])) {
		$explicit = epc_portal_storefront_hub_logo_setting();
		if ($explicit !== null) {
			return $explicit;
		}
		return false;
	}
	if (function_exists('epc_portal_is_platform_operator_host') && epc_portal_is_platform_operator_host()) {
		return true;
	}
	$explicit = epc_portal_storefront_hub_logo_setting();
	if ($explicit !== null) {
		return $explicit;
	}
	if (function_exists('epc_portal_commerce_storefront_enabled') && !epc_portal_commerce_storefront_enabled()) {
		return true;
	}
	if (function_exists('epc_brand_mandatory_line_applies') && epc_brand_mandatory_line_applies()) {
		return true;
	}
	return false;
}

function epc_portal_storefront_hub_logo_enqueue(): void
{
	if (epc_portal_tenant_brand_enabled()) {
		epc_portal_tenant_brand_enqueue();
		return;
	}
	if (!epc_portal_storefront_hub_enabled()) {
		return;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
	epc_ecomae_hub_logo_enqueue();
}

function epc_portal_storefront_logo_show_trade_label(): bool
{
	if (!epc_portal_storefront_hub_enabled()) {
		return false;
	}
	if (function_exists('epc_portal_is_platform_operator_host') && epc_portal_is_platform_operator_host()) {
		return false;
	}
	return true;
}

/**
 * Markup for .header-logo / mobile logo anchor (hub, cart SVG, or text).
 */
function epc_portal_storefront_logo_markup(): string
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_storefront_animated_logos.php';
	if (function_exists('epc_portal_active_storefront_package')) {
		$pkg = (string) epc_portal_active_storefront_package();
		if ($pkg !== '' && $pkg !== 'automotive_spareparts_pro') {
			$animated = epc_storefront_animated_logo_markup($pkg);
			if ($animated !== '') {
				return $animated;
			}
		}
	}

	if (epc_portal_storefront_hub_enabled()) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
		$label = epc_brand_trade_name();
		$aria = $label !== '' ? $label . ' — powered by ECOM AE' : 'ECOM AE unified ERP and commerce cloud';
		$hub = epc_ecomae_hub_logo('header', array(
			'show_title' => false,
			'show_tagline' => false,
			'aria_label' => $aria,
		));
		if (!epc_portal_storefront_logo_show_trade_label()) {
			return '<span class="epc-storefront-logo epc-storefront-logo--hub-only">' . $hub . '</span>';
		}
		return '<span class="epc-storefront-logo epc-storefront-logo--hub">'
			. '<span class="epc-storefront-logo__hub">' . $hub . '</span>'
			. '<span class="epc-storefront-logo__label">' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</span>'
			. '</span>';
	}

	if (epc_portal_tenant_brand_enabled()) {
		return epc_portal_tenant_brand_markup('header');
	}

	$site = epc_portal_site_profile();
	$isParts = isset($site['industry']) && $site['industry'] === 'auto_parts'
		&& function_exists('epc_portal_commerce_storefront_enabled')
		&& epc_portal_commerce_storefront_enabled();
	if ($isParts) {
		return epc_portal_storefront_epartscart_svg_markup();
	}
	if (isset($site['industry']) && $site['industry'] === 'electronics'
		&& function_exists('epc_portal_active_storefront_package')
		&& epc_portal_active_storefront_package() === 'electronics_retail_virgin') {
		$label = epc_brand_trade_name();
		if ($label === '' || stripos($label, 'epart') !== false) {
			$label = 'Electronicae';
		}
		return '<span class="epc-text-logo epc-text-logo--electronics" aria-label="' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '">'
			. htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</span>';
	}

	$label = epc_brand_trade_name();
	return '<span class="epc-text-logo" aria-label="' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '">'
		. htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . '</span>';
}

function epc_portal_storefront_epartscart_svg_markup(): string
{
	ob_start();
	?>
<span class="epc-animated-logo" aria-label="EpartsCart">
	<span class="epc-animated-logo__text">eparts</span>
	<span class="epc-animated-logo__mark" aria-hidden="true">
		<svg viewBox="0 0 220 100" xmlns="http://www.w3.org/2000/svg" focusable="false">
			<path class="epc-logo-speed epc-logo-speed--one" d="M10 28 H72" />
			<path class="epc-logo-speed epc-logo-speed--two" d="M0 48 H68" />
			<path class="epc-logo-speed epc-logo-speed--three" d="M20 68 H76" />
			<path class="epc-logo-road" d="M70 96 H190" />
			<g class="epc-logo-cart-motion">
				<path class="epc-logo-cart" d="M66 18 H178 C186 18 192 25 190 33 L177 70 H83 L66 18 Z" />
				<path class="epc-logo-handle" d="M64 18 L52 18 L43 10" />
				<path class="epc-logo-basket" d="M82 32 H172 L163 58 H92 Z" />
				<g class="epc-logo-parts">
					<g class="epc-logo-gear" transform="translate(126 48)">
						<path d="M0 -18 L4 -13 L10 -15 L12 -9 L18 -7 L15 -1 L18 5 L12 8 L10 15 L3 13 L-2 18 L-7 13 L-14 15 L-15 8 L-20 5 L-17 -1 L-20 -7 L-15 -9 L-14 -15 L-7 -13 Z" />
						<circle r="12" />
						<circle r="5" class="epc-logo-gear-hole" />
					</g>
					<g class="epc-logo-piston" transform="translate(98 39)">
						<rect x="0" y="0" width="24" height="18" rx="4" />
						<path d="M3 5 H21 M3 10 H21" />
						<path d="M12 18 V31" />
					</g>
					<g class="epc-logo-ring" transform="translate(152 48)">
						<circle r="12" />
						<path d="M8 -8 L16 -15" />
					</g>
					<rect class="epc-logo-box" x="137" y="31" width="24" height="17" rx="4" />
				</g>
				<g class="epc-logo-wheel epc-logo-wheel--left" transform="translate(86 88)">
					<g class="epc-logo-wheel-spin">
						<circle class="epc-logo-tyre" r="16" />
						<circle class="epc-logo-wheel-rim" r="10" />
						<circle class="epc-logo-wheel-hole" r="4" />
						<path class="epc-logo-wheel-spokes" d="M0 -10 V10 M-10 0 H10 M-7 -7 L7 7 M7 -7 L-7 7" />
						<path class="epc-logo-wheel-tread" d="M-5 -15 L-2 -11 M5 -15 L2 -11 M15 -5 L11 -2 M15 5 L11 2 M5 15 L2 11 M-5 15 L-2 11 M-15 5 L-11 2 M-15 -5 L-11 -2" />
					</g>
				</g>
				<g class="epc-logo-wheel epc-logo-wheel--right" transform="translate(166 88)">
					<g class="epc-logo-wheel-spin">
						<circle class="epc-logo-tyre" r="16" />
						<circle class="epc-logo-wheel-rim" r="10" />
						<circle class="epc-logo-wheel-hole" r="4" />
						<path class="epc-logo-wheel-spokes" d="M0 -10 V10 M-10 0 H10 M-7 -7 L7 7 M7 -7 L-7 7" />
						<path class="epc-logo-wheel-tread" d="M-5 -15 L-2 -11 M5 -15 L2 -11 M15 -5 L11 -2 M15 5 L11 2 M5 15 L2 11 M-5 15 L-2 11 M-15 5 L-11 2 M-15 -5 L-11 -2" />
					</g>
				</g>
			</g>
		</svg>
	</span>
</span>
	<?php
	return ob_get_clean();
}
