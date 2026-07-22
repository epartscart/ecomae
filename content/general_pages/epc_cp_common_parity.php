<?php
/**
 * Common CP feature parity across platform + industry tenants.
 *
 * Roles (do not blur):
 * - epartscart.com  → spare-parts storefront (industry auto_parts), shared DB docpart
 * - ecomae.com / cp → overall platform control (DB ecomae); ERP-only companies share this host
 * - Other Model C hosts → industry storefronts on shared docpart (electronics, fashion, …)
 *
 * Rule: commerce OMS and other “common” CP packs stay in sync both ways —
 * if missing on a target, apply there; industry-specific packs stay scoped.
 */
declare(strict_types=1);

/**
 * Platform + tenant targets for common CP DB/menu applies.
 * site_key => [host, role, industry, db_hint]
 */
function epc_cp_common_parity_targets(): array
{
	return array(
		'ecomae' => array(
			'host' => 'www.ecomae.com',
			'alt_hosts' => array('ecomae.com', 'cp.ecomae.com'),
			'role' => 'platform_control',
			'industry' => 'platform',
			'db_hint' => 'ecomae',
			'label' => 'ecomae.com / CP — overall platform control',
		),
		'epartscart' => array(
			'host' => 'www.epartscart.com',
			'alt_hosts' => array('epartscart.com'),
			'role' => 'spare_parts',
			'industry' => 'auto_parts',
			'db_hint' => 'docpart',
			'label' => 'epartscart.com — spare parts only',
		),
		'electronicae' => array(
			'host' => 'www.electronicae.com',
			'alt_hosts' => array(),
			'role' => 'industry_storefront',
			'industry' => 'electronics',
			'db_hint' => 'docpart',
			'label' => 'electronicae.com — electronics',
		),
		'taxofinca' => array(
			'host' => 'www.taxofinca.com',
			'alt_hosts' => array(),
			'role' => 'industry_storefront',
			'industry' => 'tax_advisory',
			'db_hint' => 'docpart',
			'label' => 'taxofinca.com — tax advisory',
		),
		'stylenlook' => array(
			'host' => 'www.stylenlook.com',
			'alt_hosts' => array(),
			'role' => 'industry_storefront',
			'industry' => 'fashion',
			'db_hint' => 'docpart',
			'label' => 'stylenlook.com — fashion',
		),
		'thejewellerytrend' => array(
			'host' => 'www.thejewellerytrend.com',
			'alt_hosts' => array(),
			'role' => 'industry_storefront',
			'industry' => 'jewellery',
			'db_hint' => 'docpart',
			'label' => 'thejewellerytrend.com — jewellery',
		),
	);
}

/**
 * Feature packs that should match across platform + all commerce tenants.
 * Industry-scoped packs list which industries receive them.
 */
function epc_cp_common_parity_packs(): array
{
	return array(
		'oms_orders_menu' => array(
			'label' => 'OMS · Orders — one Shop sidebar entry (no statuses/items rows)',
			'scope' => 'common',
			'industries' => array('*'),
			'applies_to_roles' => array('platform_control', 'spare_parts', 'industry_storefront'),
		),
		'cp_menu_full' => array(
			'label' => 'Full CP menu parity (Customers & accounts, Documents, Payments, Marketing, Integrations, …)',
			'scope' => 'common',
			'industries' => array('*'),
			'applies_to_roles' => array('platform_control', 'spare_parts', 'industry_storefront'),
		),
		'oms_daily_guide' => array(
			'label' => 'OMS daily guide route (/cp/shop/orders/oms-guide)',
			'scope' => 'common',
			'industries' => array('*'),
			'applies_to_roles' => array('platform_control', 'spare_parts', 'industry_storefront'),
		),
		'multivendor_upload' => array(
			'label' => 'Multivendor price upload CP (ajaxUrl always configured)',
			'scope' => 'common',
			'industries' => array('*'),
			'applies_to_roles' => array('platform_control', 'spare_parts', 'industry_storefront'),
			'files_only' => true,
		),
		'vehicle_catalog' => array(
			'label' => 'Vehicle / VIN catalog CP',
			'scope' => 'industry',
			'industries' => array('auto_parts'),
			'applies_to_roles' => array('spare_parts'),
		),
		'platform_governance' => array(
			'label' => 'Platform governance / tenant control',
			'scope' => 'platform',
			'industries' => array('platform'),
			'applies_to_roles' => array('platform_control'),
		),
	);
}

function epc_cp_common_parity_pack_applies(string $packKey, array $target): bool
{
	$packs = epc_cp_common_parity_packs();
	if (!isset($packs[$packKey])) {
		return false;
	}
	$pack = $packs[$packKey];
	$role = (string) ($target['role'] ?? '');
	$industry = (string) ($target['industry'] ?? '');
	$roles = $pack['applies_to_roles'] ?? array();
	if ($roles && !in_array($role, $roles, true) && !in_array('*', $roles, true)) {
		return false;
	}
	$inds = $pack['industries'] ?? array('*');
	if (in_array('*', $inds, true)) {
		return true;
	}
	return in_array($industry, $inds, true);
}

/**
 * Host => site_key map for portal HTTP_HOST binding during setup.
 */
function epc_cp_common_parity_host_map(): array
{
	$map = array();
	foreach (epc_cp_common_parity_targets() as $siteKey => $t) {
		$map[$siteKey] = (string) $t['host'];
	}
	return $map;
}
