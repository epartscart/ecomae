<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_storefront_logo.php';
if (epc_portal_tenant_brand_enabled()) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_brand.php';
	epc_portal_tenant_brand_enqueue();
	echo epc_portal_tenant_brand_markup('header');
} else {
	echo epc_portal_storefront_epartscart_svg_markup();
}
