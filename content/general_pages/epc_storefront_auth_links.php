<?php
/**
 * Storefront header auth — Vendor login/Register + Customer login/register.
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

function epc_storefront_auth_links_html(array $multilang_params = null, string $wrapper_class = 'epc-auth-header-links'): string
{
	if (class_exists('DP_User') && (int) DP_User::getUserId() > 0) {
		return '';
	}
	$vendorLogin = htmlspecialchars(epc_storefront_auth_vendor_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$vendorReg = htmlspecialchars(epc_storefront_auth_vendor_register_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$login = htmlspecialchars(epc_storefront_auth_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$signup = htmlspecialchars(epc_storefront_auth_signup_url($multilang_params), ENT_QUOTES, 'UTF-8');

	// Use FA 4.5-safe icons only (theme ships Font Awesome 4.5.0 — no fa-handshake-o).
	return '<span class="' . htmlspecialchars($wrapper_class, ENT_QUOTES, 'UTF-8') . '">'
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
		. '.epc-auth-header-links__sep{color:rgba(255,255,255,.35);font-weight:400;padding:0 2px}'
		. '.epc-auth-header-links__slash{color:rgba(255,255,255,.45);padding:0 1px;font-weight:600}'
		. '.epc-auth-header-links a{text-decoration:none;font-weight:700;color:inherit}'
		. '.epc-auth-header-links a:hover{color:#fff;text-decoration:underline}'
		. '.epc-auth-header-links__vendor-register,.epc-auth-header-links__signup{color:#fda4af}'
		. '.epc-er-utility__actions .epc-auth-header-links{margin-left:6px}'
		. '.new-header-user-box .epc-auth-header-links a{display:inline-block;padding:2px 0}'
		. '.header-box-mobile .epc-auth-header-links{flex-wrap:wrap;gap:4px 8px;font-size:12px}'
		. '.header-box-mobile .epc-auth-header-links__sep{color:#94a3b8}'
		. '.header-box-mobile .epc-auth-header-links__slash{color:#94a3b8}'
		. '.header-box-mobile .epc-auth-header-links__vendor-register,.header-box-mobile .epc-auth-header-links__signup{color:#c0392b}'
		. '</style>';
}
