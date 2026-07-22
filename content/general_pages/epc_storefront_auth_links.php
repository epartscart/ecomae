<?php
/**
 * Storefront header auth — Vendor + Customer + Garage staff login.
 */
defined('_ASTEXE_') or die('No access');

function epc_storefront_auth_lang_href(array $multilang_params = null): string
{
	if ($multilang_params === null && !empty($GLOBALS['multilang_params'])) {
		$multilang_params = $GLOBALS['multilang_params'];
	}
	$href = is_array($multilang_params) ? (string) ($multilang_params['lang_href'] ?? '/en/') : '/en/';
	return rtrim($href, '/') . '/';
}

function epc_storefront_auth_login_url(array $multilang_params = null): string
{
	return epc_storefront_auth_lang_href($multilang_params) . 'users/login';
}

function epc_storefront_auth_signup_url(array $multilang_params = null): string
{
	return epc_storefront_auth_lang_href($multilang_params) . 'users/registration';
}

function epc_storefront_auth_vendor_url(array $multilang_params = null): string
{
	return epc_storefront_auth_lang_href($multilang_params) . 'vendor';
}

function epc_storefront_auth_vendor_register_url(array $multilang_params = null): string
{
	return epc_storefront_auth_lang_href($multilang_params) . 'vendor/register';
}

function epc_storefront_auth_garage_login_url(array $multilang_params = null): string
{
	return epc_storefront_auth_lang_href($multilang_params) . 'garage/login';
}

function epc_storefront_auth_garage_manager_url(array $multilang_params = null): string
{
	return epc_storefront_auth_lang_href($multilang_params) . 'garage/manager';
}

function epc_storefront_auth_links_html(array $multilang_params = null, string $wrapper_class = 'epc-auth-header-links'): string
{
	$garageLogin = htmlspecialchars(epc_storefront_auth_garage_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$garageManager = htmlspecialchars(epc_storefront_auth_garage_manager_url($multilang_params), ENT_QUOTES, 'UTF-8');

	$staffGarage = '';
	if (class_exists('DP_User') && (DP_User::isAdmin() || DP_User::isBackendGroup())) {
		$staffGarage = '<span class="epc-auth-header-links__group epc-auth-header-links__group--garage">'
			. '<i class="fa fa-wrench epc-auth-header-links__icon" aria-hidden="true"></i> '
			. '<a class="epc-auth-header-links__garage" href="' . $garageManager . '" title="Garage Manager System">Garage Manager</a>'
			. '</span>';
	}

	$garageGuest = '<span class="epc-auth-header-links__group epc-auth-header-links__group--garage">'
		. '<i class="fa fa-wrench epc-auth-header-links__icon" aria-hidden="true"></i> '
		. '<a class="epc-auth-header-links__garage" href="' . $garageLogin . '" title="Garage Management System login">Garage login</a>'
		. '</span>';
	$garageBlock = $staffGarage !== '' ? $staffGarage : $garageGuest;

	if (class_exists('DP_User') && (int) DP_User::getUserId() > 0) {
		// Logged-in customer: keep Garage login/Manager visible in the header.
		return '<span class="' . htmlspecialchars($wrapper_class, ENT_QUOTES, 'UTF-8') . '">' . $garageBlock . '</span>';
	}

	$vendorLogin = htmlspecialchars(epc_storefront_auth_vendor_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$vendorReg = htmlspecialchars(epc_storefront_auth_vendor_register_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$login = htmlspecialchars(epc_storefront_auth_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$signup = htmlspecialchars(epc_storefront_auth_signup_url($multilang_params), ENT_QUOTES, 'UTF-8');

	return '<span class="' . htmlspecialchars($wrapper_class, ENT_QUOTES, 'UTF-8') . '">'
		. $garageBlock
		. '<span class="epc-auth-header-links__sep" aria-hidden="true">|</span>'
		. '<span class="epc-auth-header-links__group epc-auth-header-links__group--vendor">'
		. '<i class="fa fa-briefcase epc-auth-header-links__icon" aria-hidden="true"></i> '
		. '<a class="epc-auth-header-links__vendor-login" href="' . $vendorLogin . '" title="Vendor portal login">Vendor login</a>'
		. '<span class="epc-auth-header-links__slash" aria-hidden="true">/</span>'
		. '<a class="epc-auth-header-links__vendor-register" href="' . $vendorReg . '" title="Vendor registration">Register</a>'
		. '</span>'
		. '<span class="epc-auth-header-links__sep" aria-hidden="true">|</span>'
		. '<span class="epc-auth-header-links__group epc-auth-header-links__group--customer">'
		. '<i class="fa fa-user epc-auth-header-links__icon" aria-hidden="true"></i> '
		. '<a class="epc-auth-header-links__login" href="' . $login . '" title="Customer login">Customer login</a>'
		. '<span class="epc-auth-header-links__slash" aria-hidden="true">/</span>'
		. '<a class="epc-auth-header-links__signup" href="' . $signup . '" title="Customer registration">register</a>'
		. '</span>'
		. '</span>';
}

function epc_storefront_auth_links_render(array $multilang_params = null, string $wrapper_class = 'epc-auth-header-links'): void
{
	echo epc_storefront_auth_links_html($multilang_params, $wrapper_class);
}

function epc_storefront_auth_links_styles(): string
{
	return '<style>'
		. '.epc-auth-header-links{display:inline-flex;align-items:center;gap:8px;white-space:nowrap;font-weight:700}'
		. '.epc-auth-header-links__group{display:inline-flex;align-items:center;gap:5px}'
		. '.epc-auth-header-links__icon,.epc-auth-header-links__group > .fa{color:#ef4444;margin-right:2px;font-size:14px;line-height:1;width:1em;text-align:center}'
		. '.epc-auth-header-links__group--garage .epc-auth-header-links__icon{color:#22d3ee}'
		. '.epc-auth-header-links__sep{color:rgba(255,255,255,.35);font-weight:400;padding:0 2px}'
		. '.epc-auth-header-links__slash{color:rgba(255,255,255,.45);padding:0 1px;font-weight:600}'
		. '.epc-auth-header-links a{text-decoration:none;font-weight:700;color:inherit}'
		. '.epc-auth-header-links a:hover{color:#fff;text-decoration:underline}'
		. '.epc-auth-header-links__garage{color:#a5f3fc}'
		. '.epc-auth-header-links__vendor-register,.epc-auth-header-links__signup{color:#fda4af}'
		. '.epc-er-utility__actions .epc-auth-header-links{margin-left:6px}'
		. '@media(max-width:991px){.epc-auth-header-links{flex-wrap:wrap;white-space:normal;row-gap:4px}}'
		. '</style>';
}
