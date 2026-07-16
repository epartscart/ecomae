<?php
/**
 * Shared world-premium 3D animation skin for live custom storefront tenants.
 *
 * Eligible packages (default ON):
 *   - electronics_retail_virgin  (electronicae.com)
 *   - fashion_retail_namshi      (stylenlook.com)
 *   - jewellery_retail_kiyasha   (thejewellerytrend.com)
 *   - consulting_primeinvest     (taxofinca.com)
 *
 * Not enabled for automotive_spareparts_pro / epartscart.com (opt-in later).
 * Disable per tenant: site_settings.contact.premium_3d = 0|false|off
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

function epc_storefront_premium_3d_asset_ver(): string
{
	return '20260716';
}

/**
 * Packages that receive the premium 3D skin by default.
 *
 * @return list<string>
 */
function epc_storefront_premium_3d_eligible_packages(): array
{
	return array(
		'electronics_retail_virgin',
		'fashion_retail_namshi',
		'jewellery_retail_kiyasha',
		'consulting_primeinvest',
	);
}

/**
 * Motif key → industry_3d particle shape family.
 *
 * @return array<string,string>
 */
function epc_storefront_premium_3d_motif_map(): array
{
	return array(
		'electronics_retail_virgin' => 'electronics',
		'fashion_retail_namshi' => 'fashion',
		'jewellery_retail_kiyasha' => 'jewellery',
		'consulting_primeinvest' => 'professional',
		'automotive_spareparts_pro' => 'automotive',
	);
}

function epc_storefront_premium_3d_motif_for_package(string $package): string
{
	$map = epc_storefront_premium_3d_motif_map();
	$package = preg_replace('/[^a-z0-9_]/', '', strtolower($package));
	return $map[$package] ?? 'default';
}

function epc_storefront_premium_3d_icon_for_motif(string $motif): string
{
	$icons = array(
		'electronics' => 'fa-microchip',
		'fashion' => 'fa-shopping-bag',
		'jewellery' => 'fa-diamond',
		'professional' => 'fa-briefcase',
		'finance' => 'fa-line-chart',
		'automotive' => 'fa-cog',
		'default' => 'fa-cube',
	);
	return $icons[$motif] ?? 'fa-cube';
}

/**
 * Whether the current request should load the premium 3D storefront skin.
 */
function epc_storefront_premium_3d_enabled(): bool
{
	static $cached = null;
	if ($cached !== null) {
		return $cached;
	}

	if (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
		$cached = false;
		return false;
	}

	$package = '';
	if (function_exists('epc_portal_active_storefront_package')) {
		$package = (string) epc_portal_active_storefront_package();
	}
	if ($package === '' || !in_array($package, epc_storefront_premium_3d_eligible_packages(), true)) {
		$cached = false;
		return false;
	}

	if (function_exists('epc_portal_load_site_settings')) {
		$settings = epc_portal_load_site_settings();
		$contact = (isset($settings['contact']) && is_array($settings['contact'])) ? $settings['contact'] : array();
		if (array_key_exists('premium_3d', $contact)) {
			$raw = $contact['premium_3d'];
			if ($raw === 0 || $raw === '0' || $raw === false || $raw === 'false' || $raw === 'off' || $raw === 'no') {
				$cached = false;
				return false;
			}
		}
	}

	$cached = true;
	return true;
}

function epc_storefront_premium_3d_motif(): string
{
	$package = function_exists('epc_portal_active_storefront_package')
		? (string) epc_portal_active_storefront_package()
		: '';
	return epc_storefront_premium_3d_motif_for_package($package);
}

/**
 * Head link tags for CSS (empty when disabled).
 */
function epc_storefront_premium_3d_head_html(): string
{
	if (!epc_storefront_premium_3d_enabled()) {
		return '';
	}
	$v = rawurlencode(epc_storefront_premium_3d_asset_ver());
	return '<link rel="stylesheet" href="/content/general_pages/epc_storefront_premium_3d.css?v=' . $v . '" />' . "\n";
}

/**
 * Footer script tag (empty when disabled).
 */
function epc_storefront_premium_3d_footer_html(): string
{
	if (!epc_storefront_premium_3d_enabled()) {
		return '';
	}
	$v = rawurlencode(epc_storefront_premium_3d_asset_ver());
	return '<script src="/content/general_pages/epc_storefront_premium_3d.js?v=' . $v . '" defer></script>' . "\n";
}

/**
 * Body attributes when 3D skin is active.
 *
 * @return array{class:string,motif:string,icon:string}
 */
function epc_storefront_premium_3d_body_meta(): array
{
	if (!epc_storefront_premium_3d_enabled()) {
		return array('class' => '', 'motif' => '', 'icon' => '');
	}
	$motif = epc_storefront_premium_3d_motif();
	return array(
		'class' => 'epc-sf-premium-3d',
		'motif' => $motif,
		'icon' => epc_storefront_premium_3d_icon_for_motif($motif),
	);
}
