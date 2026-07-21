<?php
/**
 * Config page — group summaries + frontend impact hints for CP Settings.
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<int,array{icon:string,blurb:string,tone:string,frontend:string}>
 */
function epc_config_group_meta(): array
{
	return array(
		13 => array(
			'icon' => 'fa-search',
			'blurb' => 'SEO titles and catalog API keys used by search and OEM catalogues.',
			'tone' => 'sky',
			'frontend' => 'Site title / description / keywords appear in browser tabs and search results. Catalog APIs power storefront part lookup.',
		),
		1 => array(
			'icon' => 'fa-globe',
			'blurb' => 'Storefront contacts, footer offices, WhatsApp, cookies, and CP folder.',
			'tone' => 'teal',
			'frontend' => 'Phone, WhatsApp, footer address/locations, cookie banner, and SSL notice show on the public site.',
		),
		7 => array(
			'icon' => 'fa-cloud-download',
			'blurb' => 'Where the control panel checks for product updates.',
			'tone' => 'slate',
			'frontend' => 'No direct storefront effect — CP / updates only.',
		),
		12 => array(
			'icon' => 'fa-user-plus',
			'blurb' => 'Customer registration and login rules.',
			'tone' => 'indigo',
			'frontend' => 'Affects sign-up / login forms: password length, phone mask, and code-based auth.',
		),
		3 => array(
			'icon' => 'fa-envelope',
			'blurb' => 'SMTP used for order and account e-mails from the site.',
			'tone' => 'amber',
			'frontend' => 'Customers receive order and account e-mails from these sender / SMTP settings.',
		),
		4 => array(
			'icon' => 'fa-shopping-cart',
			'blurb' => 'Checkout currency, balances, and payment behaviour.',
			'tone' => 'rose',
			'frontend' => 'Controls price display, guest checkout, partial payment, and balance rules on the storefront.',
		),
		6 => array(
			'icon' => 'fa-inbox',
			'blurb' => 'Mailbox used to receive supplier price-list files.',
			'tone' => 'slate',
			'frontend' => 'No direct storefront effect — price upload pipeline only.',
		),
		11 => array(
			'icon' => 'fa-th-large',
			'blurb' => 'Catalogue pagination and product URL style.',
			'tone' => 'emerald',
			'frontend' => 'Changes how product lists and product page URLs appear to customers.',
		),
		9 => array(
			'icon' => 'fa-list',
			'blurb' => 'Article / spare-parts search results layout and sorting.',
			'tone' => 'violet',
			'frontend' => 'Directly shapes the part-search results table, filters, sorting, and warehouse wait time.',
		),
		14 => array(
			'icon' => 'fa-undo',
			'blurb' => 'Customer return / refund rules.',
			'tone' => 'orange',
			'frontend' => 'Enables refund requests and shows withholding / notification text to customers when returns are used.',
		),
	);
}

/**
 * Short “effect on frontend” lines keyed by config item name.
 *
 * @return array<string,string>
 */
function epc_config_item_frontend_effects(): array
{
	return array(
		'site_name' => 'Browser tab title and SEO site name on the public site.',
		'description_tag' => 'Meta description for search engines / social previews.',
		'keywords_tag' => 'Meta keywords tag on storefront pages.',
		'show_page_title' => 'Shows or hides the page heading on frontend pages.',
		'show_site_name' => 'Shows or hides the site name next to page titles.',
		'page_title_first' => 'Order of page title vs site name in the browser tab.',
		'umapi_api_key' => 'Powers OEM / manufacturer catalogue lookup on the storefront.',
		'list_length' => 'Default rows per page in many storefront lists.',
		'backend_dir' => 'CP folder only — changing this can break admin URLs (not storefront).',
		'epc_contact_phone' => 'Phone shown in storefront header / contact areas.',
		'epc_whatsapp_number' => 'WhatsApp number used by storefront share / contact buttons.',
		'epc_whatsapp_api_enabled' => 'Turns automated WhatsApp notifications on/off (backend send).',
		'epc_whatsapp_api_token' => 'Cloud API credential — not shown on storefront.',
		'epc_whatsapp_phone_number_id' => 'Cloud API phone id — not shown on storefront.',
		'epc_whatsapp_api_version' => 'Graph API version for WhatsApp sends — backend only.',
		'epc_whatsapp_notify_names' => 'Which order events trigger WhatsApp messages.',
		'epc_whatsapp_bilingual_notify' => 'Sends WhatsApp text in English + Arabic when enabled.',
		'epc_head_office_title' => 'Footer “head office” title on the public site.',
		'epc_head_office_address' => 'Footer head-office address block on the storefront.',
		'epc_head_office_email' => 'Footer head-office e-mail on the storefront.',
		'epc_head_office_map_url' => 'Map link for the footer head office.',
		'epc_global_locations_summary' => 'Short footer summary of global locations.',
		'epc_global_locations_countries' => 'Full countries / locations block in the storefront footer.',
		'epc_global_locations_map_url' => 'Global map link in the storefront footer.',
		'show_vin_request' => 'Shows the VIN request block on the main page.',
		'show_ssl_checker' => 'Shows SSL certificate notice on the storefront.',
		'show_cookie' => 'Shows the cookie consent warning for visitors.',
		'tech_key' => 'Secret for cron / scripts — never expose on frontend.',
		'can_edit_system_content' => 'CP permission only — not a storefront setting.',
		'update_server' => 'CP update checks only.',
		'min_password_length' => 'Minimum password length on customer registration.',
		'reg_variant' => 'Simplifies login / registration by one-time code on the storefront.',
		'show_phone_mask' => 'Formats the phone field on registration / profile forms.',
		'country_phone_mask' => 'Which country phone mask the storefront uses.',
		'from_name' => '“From” name on e-mails customers receive.',
		'from_email' => '“From” address on e-mails customers receive.',
		'smtp_mode' => 'How the site sends customer / staff e-mails.',
		'shop_currency' => 'Currency used for storefront prices and checkout.',
		'currency_show_mode' => 'Where the currency sign appears next to prices.',
		'price_rounding' => 'How storefront prices are rounded.',
		'order_partial_payment' => 'Lets customers pay part of an order at checkout.',
		'order_min_partial_payment' => 'Minimum partial-payment percent shown at checkout.',
		'client_balance_available' => 'Allows negative customer balance behaviour.',
		'client_balance_limit' => 'Overdraft / balance limit rules for customers.',
		'purchase_without_reg' => 'Allows checkout without creating an account.',
		'payment_on_receipt' => 'Offers pay-on-delivery style options when enabled.',
		'products_count_for_page' => 'How many catalogue products appear per page.',
		'product_url' => 'How product page URLs are built for customers.',
		'catalogue_show_mode' => 'Pagination vs load-more style catalogue browsing.',
		'async_search' => 'Loads search results asynchronously on the article search page.',
		'products_table_mode' => 'Layout of the found-parts table for customers.',
		'show_manufacturers_filter' => 'Shows manufacturer filter on article search.',
		'show_storage_filter' => 'Shows warehouse / storage filter on article search.',
		'show_search_string' => 'Shows the search string block on results.',
		'let_refunds' => 'Allows customers to start refund / return requests.',
		'retention_percentage' => 'Withholding % explained when a refund is created.',
		'retention_percentage_text' => 'Customer-facing refund notification text.',
	);
}

function epc_config_item_frontend_effect(string $name): string
{
	$map = epc_config_item_frontend_effects();
	return isset($map[$name]) ? (string) $map[$name] : '';
}

/**
 * @return array{icon:string,blurb:string,tone:string,frontend:string}
 */
function epc_config_group_meta_for(int $groupId): array
{
	$all = epc_config_group_meta();
	if (isset($all[$groupId])) {
		return $all[$groupId];
	}
	return array(
		'icon' => 'fa-cog',
		'blurb' => 'Configuration group for this site.',
		'tone' => 'slate',
		'frontend' => '',
	);
}

/**
 * Clearer English labels for EPC-specific keys (fallback: translated caption).
 */
function epc_config_item_label(string $name, $translatedCaption = ''): string
{
	$translatedCaption = trim((string) $translatedCaption);
	$labels = array(
		'epc_contact_phone' => 'Frontend phone number',
		'epc_whatsapp_number' => 'Frontend WhatsApp number',
		'epc_whatsapp_api_enabled' => 'WhatsApp API — automated notifications (0/1)',
		'epc_whatsapp_api_token' => 'WhatsApp Cloud API token',
		'epc_whatsapp_phone_number_id' => 'WhatsApp phone_number_id',
		'epc_whatsapp_api_version' => 'WhatsApp Graph API version',
		'epc_whatsapp_notify_names' => 'WhatsApp notify events (comma-separated)',
		'epc_whatsapp_bilingual_notify' => 'WhatsApp bilingual notify (0/1)',
		'umapi_api_key' => 'Epart catalog API key (key only, not full URL)',
		'epc_head_office_title' => 'Footer head office title',
		'epc_head_office_address' => 'Footer head office address',
		'epc_head_office_email' => 'Footer head office email',
		'epc_head_office_map_url' => 'Footer head office map URL',
		'epc_global_locations_summary' => 'Footer global locations summary',
		'epc_global_locations_countries' => 'Footer countries / locations text',
		'epc_global_locations_map_url' => 'Footer global map URL',
	);
	return isset($labels[$name]) ? $labels[$name] : $translatedCaption;
}
