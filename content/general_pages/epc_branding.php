<?php
/**
 * White-label branding — driven by multi-industry portal profile.
 * Mandatory platform line on all client storefronts and CP (not editable by tenants).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';

function epc_brand_system_name()
{
	$site = epc_portal_site_profile();
	return isset($site['system_name']) && $site['system_name'] !== ''
		? $site['system_name']
		: 'ECOM AE portal';
}

function epc_brand_hub_name()
{
	$site = epc_portal_site_profile();
	return isset($site['hub_name']) && $site['hub_name'] !== ''
		? $site['hub_name']
		: 'ecomae';
}

function epc_brand_designer_name()
{
	return 'ecomae';
}

function epc_brand_tagline_html()
{
	$site = epc_portal_site_profile();
	$tagline = isset($site['tagline']) && $site['tagline'] !== ''
		? $site['tagline']
		: 'Designed by ecomae';
	return '<strong>' . htmlspecialchars(epc_brand_system_name(), ENT_QUOTES, 'UTF-8') . '</strong> &mdash; ' . htmlspecialchars($tagline, ENT_QUOTES, 'UTF-8');
}

function epc_brand_copyright_html()
{
	return 'Designed by ' . epc_brand_designer_name();
}

function epc_brand_trade_name()
{
	$site = epc_portal_site_profile();
	if (!empty($site['contact']['trade_name'])) {
		return (string) $site['contact']['trade_name'];
	}
	if (!empty($site['trade_name'])) {
		return (string) $site['trade_name'];
	}
	return epc_brand_hub_name();
}

/** True when mandatory ECOM AE footer line must appear (all client tenants). */
function epc_brand_mandatory_line_applies()
{
	if (function_exists('epc_portal_demo_is_autoparts_parity') && epc_portal_demo_is_autoparts_parity()) {
		return false;
	}
	return function_exists('epc_portal_is_client_hostname') && epc_portal_is_client_hostname();
}

function epc_brand_hosted_by_css_link_html()
{
	if (!epc_brand_mandatory_line_applies()) {
		return '';
	}
	return '<link rel="stylesheet" href="/content/general_pages/epc_branding.css?v=20260527" />' . "\n";
}

function epc_brand_hosted_by_html()
{
	if (!epc_brand_mandatory_line_applies()) {
		return '';
	}
	$label = 'Built &amp; Managed by ';
	$link = '<a href="https://www.ecomae.com/" target="_blank" rel="noopener noreferrer">ecomae.com</a>';
	return '<span class="epc-hosted-by">' . $label . $link . '</span>';
}

function epc_brand_cp_context()
{
	$productName = epc_brand_system_name();
	$companyName = epc_brand_trade_name();
	$hubTagline = 'Finance & operations';
	$isPlatformHost = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
	$isSharedErp = false;
	if ($isPlatformHost && function_exists('epc_portal_is_cp_request') && epc_portal_is_cp_request()) {
		$sharedErpFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_shared_erp.php';
		if (is_file($sharedErpFile)) {
			require_once $sharedErpFile;
			if (function_exists('epc_portal_shared_erp_active_tenant')) {
				$sharedRow = epc_portal_shared_erp_active_tenant();
				if ($sharedRow !== null) {
					$isSharedErp = true;
					$trade = trim((string) ($sharedRow['trade_name'] ?? ''));
					if ($trade !== '') {
						$productName = $trade;
						$companyName = $trade;
					}
				}
			}
		}
	}
	if (function_exists('epc_portal_demo_cp_is_erp_only') && epc_portal_demo_cp_is_erp_only()) {
		$row = $GLOBALS['epc_demo_cp_tenant_row'] ?? null;
		$trade = is_array($row) ? trim((string) ($row['trade_name'] ?? '')) : '';
		if ($trade !== '') {
			$productName = $trade . ' ERP Demo';
			$companyName = $trade;
			$hubTagline = 'ERP sandbox · finance & operations';
		}
	} elseif (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
		// Tenant ERP on epartscart.com — storefront brand, not parent hub name.
		$productName = 'eParts Cart';
		$companyName = 'eParts Cart';
		$hubTagline = 'epartscart.com · Finance & operations';
	} elseif (function_exists('epc_portal_demo_is_autoparts_parity') && epc_portal_demo_is_autoparts_parity()) {
		$settings = epc_portal_load_site_settings();
		$productName = (string) ($settings['system_name'] ?? 'eParts Cart');
		$companyName = (string) (($settings['contact']['trade_name'] ?? '') ?: $productName);
		$hubTagline = (string) ($settings['tagline'] ?? 'Finance & operations');
	} elseif (function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active()) {
		$productName = 'ECOM AE Platform ERP';
		$companyName = 'ECOM AE Operations';
		$hubTagline = 'Platform ERP · ecomae registry';
	} elseif ($isPlatformHost && !$isSharedErp) {
		$hubTagline = 'Finance & operations · ECOM AE';
	}
	return array(
		'product_name' => $productName,
		'company_name' => $companyName,
		'product_description' => 'Multi-industry commerce platform',
		'hub_tagline' => $hubTagline,
		'brand_copyright' => epc_brand_copyright_html(),
		'designer_name' => epc_brand_designer_name(),
		'trade_name' => epc_brand_trade_name(),
		'hosted_by_html' => epc_brand_hosted_by_html(),
		'is_shared_erp_session' => $isSharedErp,
	);
}
