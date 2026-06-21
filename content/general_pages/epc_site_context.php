<?php
/**
 * Unified site context — one API for domain, branding, contact, industry (multi-site portable).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';

function epc_site_context_cache_key(): string
{
	$key = epc_portal_host();
	if (!empty($GLOBALS['epc_demo_storefront_site_key'])) {
		$key .= ':demo:' . preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_storefront_site_key']);
	} elseif (!empty($GLOBALS['epc_demo_cp_site_key'])) {
		$key .= ':demo-cp:' . preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_cp_site_key']);
	}
	return $key;
}

function epc_site_context_reset(): void
{
	$GLOBALS['epc_site_context_cache_bust'] = true;
}

function epc_site_context($DP_Config = null)
{
	static $ctxByKey = array();
	if (!empty($GLOBALS['epc_site_context_cache_bust'])) {
		$ctxByKey = array();
		unset($GLOBALS['epc_site_context_cache_bust']);
	}
	$cacheKey = epc_site_context_cache_key();
	if (isset($ctxByKey[$cacheKey])) {
		return $ctxByKey[$cacheKey];
	}
	if ($DP_Config === null && isset($GLOBALS['DP_Config'])) {
		$DP_Config = $GLOBALS['DP_Config'];
	}
	$profile = epc_portal_site_profile();
	$host = epc_portal_host();
	$industry = epc_portal_industry();
	$contact = epc_portal_default_contact($profile);
	if (!empty($profile['contact']) && is_array($profile['contact'])) {
		$contact = array_merge($contact, $profile['contact']);
	}

	$domain = '';
	if (!empty($profile['domain_path'])) {
		$domain = rtrim((string) $profile['domain_path'], '/');
	} elseif (is_object($DP_Config) && !empty($DP_Config->domain_path)) {
		$domain = rtrim((string) $DP_Config->domain_path, '/');
	}
	if ($domain === '' || strpos($domain, 'localhost') !== false) {
		$guessed = epc_portal_guess_domain_path($host);
		if ($guessed !== '') {
			$domain = rtrim($guessed, '/');
		}
	}

	if (is_object($DP_Config)) {
		if ($contact['from_email'] === '' && !empty($DP_Config->from_email)) {
			$contact['from_email'] = (string) $DP_Config->from_email;
		}
		if ($contact['from_name'] === '' && !empty($DP_Config->from_name)) {
			$contact['from_name'] = (string) $DP_Config->from_name;
		}
		if ($contact['contact_phone'] === '' && !empty($DP_Config->epc_contact_phone)) {
			$contact['contact_phone'] = (string) $DP_Config->epc_contact_phone;
		}
		if ($contact['whatsapp_number'] === '' && !empty($DP_Config->epc_whatsapp_number)) {
			$contact['whatsapp_number'] = (string) $DP_Config->epc_whatsapp_number;
		}
		if ($contact['head_office_email'] === '' && !empty($DP_Config->epc_head_office_email)) {
			$contact['head_office_email'] = (string) $DP_Config->epc_head_office_email;
		}
		if ($contact['head_office_address'] === '' && !empty($DP_Config->epc_head_office_address)) {
			$contact['head_office_address'] = (string) $DP_Config->epc_head_office_address;
		}
	}
	if ($contact['admin_email'] === '') {
		$contact['admin_email'] = $contact['from_email'];
	}
	if ($contact['head_office_email'] === '') {
		$contact['head_office_email'] = $contact['from_email'];
	}

	if (!empty($GLOBALS['epc_demo_storefront_context']) && !empty($GLOBALS['epc_demo_storefront_site_key'])) {
		$demoKey = preg_replace('/[^a-z0-9_]/', '', (string) $GLOBALS['epc_demo_storefront_site_key']);
		$domain = 'https://www.ecomae.com/demo/' . $demoKey;
	}

	$ctx = array(
		'host' => $host,
		'domain_path' => $domain !== '' ? $domain . '/' : '',
		'domain' => $domain,
		'industry_code' => isset($industry['code']) ? $industry['code'] : 'auto_parts',
		'industry_name' => isset($industry['name']) ? $industry['name'] : 'Commerce',
		'system_name' => epc_brand_system_name(),
		'hub_name' => epc_brand_hub_name(),
		'tagline' => isset($profile['tagline']) ? (string) $profile['tagline'] : '',
		'trade_name' => $contact['trade_name'],
		'contact' => $contact,
		'from_name' => $contact['from_name'],
		'from_email' => $contact['from_email'],
		'admin_email' => $contact['admin_email'],
		'contact_phone' => $contact['contact_phone'],
		'whatsapp_number' => $contact['whatsapp_number'],
		'head_office_address' => $contact['head_office_address'],
		'head_office_email' => $contact['head_office_email'],
		'city' => $contact['city'],
		'country' => $contact['country'],
		'is_auto_parts' => epc_portal_is_auto_parts_site(),
		'home_mode' => epc_portal_home_mode(),
	);
	$ctxByKey[$cacheKey] = $ctx;
	return $ctx;
}

function epc_site_domain()
{
	$c = epc_site_context();
	return (string) $c['domain'];
}

function epc_site_url($path = '')
{
	$base = epc_site_domain();
	if ($base === '') {
		return (string) $path;
	}
	$path = ltrim((string) $path, '/');
	return $path === '' ? $base : $base . '/' . $path;
}

function epc_site_host()
{
	return (string) epc_site_context()['host'];
}

function epc_site_trade_name()
{
	return (string) epc_site_context()['trade_name'];
}

function epc_site_from_email()
{
	return (string) epc_site_context()['from_email'];
}

function epc_site_admin_email()
{
	return (string) epc_site_context()['admin_email'];
}

function epc_site_contact_phone()
{
	return (string) epc_site_context()['contact_phone'];
}

function epc_site_apply_contact_overrides($DP_Config)
{
	if (!is_object($DP_Config)) {
		return;
	}
	$ctx = epc_site_context($DP_Config);
	if (!empty($ctx['domain_path'])) {
		$DP_Config->domain_path = $ctx['domain_path'];
	}
	if (!empty($ctx['from_name'])) {
		$DP_Config->from_name = $ctx['from_name'];
	}
	if (!empty($ctx['from_email'])) {
		$DP_Config->from_email = $ctx['from_email'];
	}
	if (!empty($ctx['contact_phone'])) {
		$DP_Config->epc_contact_phone = $ctx['contact_phone'];
	}
	if (!empty($ctx['whatsapp_number'])) {
		$DP_Config->epc_whatsapp_number = $ctx['whatsapp_number'];
	}
	if (!empty($ctx['head_office_address'])) {
		$DP_Config->epc_head_office_address = $ctx['head_office_address'];
	}
	if (!empty($ctx['head_office_email'])) {
		$DP_Config->epc_head_office_email = $ctx['head_office_email'];
	}
}

/** @deprecated use epc_site_apply_contact_overrides */
function epc_site_apply_config($DP_Config)
{
	epc_site_apply_contact_overrides($DP_Config);
}

function epc_site_document_company_defaults()
{
	$ctx = epc_site_context();
	$addr = trim($ctx['head_office_address']);
	if ($addr === '' && ($ctx['city'] !== '' || $ctx['country'] !== '')) {
		$addr = trim($ctx['city'] . ', ' . $ctx['country'], ', ');
	}
	return array(
		'legal_name' => $ctx['hub_name'] !== '' ? $ctx['hub_name'] : $ctx['trade_name'],
		'trade_name' => $ctx['trade_name'],
		'address_line1' => $addr !== '' ? $addr : $ctx['city'],
		'city' => $ctx['city'] !== '' ? $ctx['city'] : 'Dubai',
		'country' => $ctx['country'],
		'phone' => $ctx['contact_phone'],
		'email' => $ctx['head_office_email'] !== '' ? $ctx['head_office_email'] : $ctx['from_email'],
		'website' => epc_site_url(),
	);
}
