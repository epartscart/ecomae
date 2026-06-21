<?php
/**
 * Storefront header — separate Login and Sign up links (all tenants).
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

function epc_storefront_auth_links_html(array $multilang_params = null, string $wrapper_class = 'epc-auth-header-links'): string
{
	if (class_exists('DP_User') && (int) DP_User::getUserId() > 0) {
		return '';
	}
	$login = htmlspecialchars(epc_storefront_auth_login_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$signup = htmlspecialchars(epc_storefront_auth_signup_url($multilang_params), ENT_QUOTES, 'UTF-8');
	$loginLabel = function_exists('translate_str_by_id') ? translate_str_by_id(4008) : 'Login';
	$signupLabel = function_exists('translate_str_by_id') ? translate_str_by_id(3987) : 'Sign up';
	return '<span class="' . htmlspecialchars($wrapper_class, ENT_QUOTES, 'UTF-8') . '">'
		. '<a class="epc-auth-header-links__login" href="' . $login . '"><i class="fa fa-sign-in" aria-hidden="true"></i> ' . htmlspecialchars($loginLabel, ENT_QUOTES, 'UTF-8') . '</a>'
		. '<span class="epc-auth-header-links__sep" aria-hidden="true">|</span>'
		. '<a class="epc-auth-header-links__signup" href="' . $signup . '"><i class="fa fa-user-plus" aria-hidden="true"></i> ' . htmlspecialchars($signupLabel, ENT_QUOTES, 'UTF-8') . '</a>'
		. '</span>';
}

function epc_storefront_auth_links_render(array $multilang_params = null, string $wrapper_class = 'epc-auth-header-links'): void
{
	echo epc_storefront_auth_links_html($multilang_params, $wrapper_class);
}

function epc_storefront_auth_links_styles(): string
{
	return '<style>'
		. '.epc-auth-header-links{display:inline-flex;align-items:center;gap:8px;white-space:nowrap}'
		. '.epc-auth-header-links__sep{color:#94a3b8;font-weight:400;padding:0 2px}'
		. '.epc-auth-header-links a{text-decoration:none;font-weight:600}'
		. '.epc-auth-header-links__login{color:inherit}'
		. '.epc-auth-header-links__signup{color:#c0392b}'
		. '.epc-er-utility__actions .epc-auth-header-links{margin-left:6px}'
		. '.new-header-user-box .epc-auth-header-links a{display:inline-block;padding:2px 0}'
		. '</style>';
}
