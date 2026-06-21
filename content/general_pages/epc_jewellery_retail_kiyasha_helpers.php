<?php
/**
 * Jewellery retail Kiyasha package — SEO, store URL, legacy chrome suppression.
 */
defined('_ASTEXE_') or die('No access');

function epc_jewellery_retail_kiyasha_store_name(): string
{
	if (!function_exists('epc_portal_load_site_settings')) {
		require_once __DIR__ . '/epc_portal.php';
	}
	$settings = epc_portal_load_site_settings();
	if (!empty($settings['system_name'])) {
		return trim((string) $settings['system_name']);
	}
	$site = epc_portal_site_profile();
	if (!empty($site['system_name'])) {
		return trim((string) $site['system_name']);
	}
	return 'The Jewellery Trend';
}

function epc_jewellery_retail_kiyasha_tagline(): string
{
	$settings = epc_portal_load_site_settings();
	if (!empty($settings['tagline'])) {
		return trim((string) $settings['tagline']);
	}
	return 'Fine gold, diamonds & bridal jewellery — UAE delivery, prices in AED';
}

function epc_jewellery_retail_kiyasha_public_url(): string
{
	$settings = epc_portal_load_site_settings();
	if (!empty($settings['domain_path'])) {
		return rtrim((string) $settings['domain_path'], '/') . '/';
	}
	global $DP_Config;
	if (is_object($DP_Config) && !empty($DP_Config->domain_path)) {
		return rtrim((string) $DP_Config->domain_path, '/') . '/';
	}
	$host = epc_portal_host();
	return $host !== '' ? 'https://' . $host . '/' : '/';
}

function epc_jewellery_retail_kiyasha_apply_seo($DP_Content): void
{
	if (!is_object($DP_Content)) {
		return;
	}
	$name = epc_jewellery_retail_kiyasha_store_name();
	$tagline = epc_jewellery_retail_kiyasha_tagline();
	$bad = array('epartscart', 'eParts Cart', 'Autoparts', 'auto parts', 'autoparts', 'spare parts', 'Docpart');

	if (!empty($DP_Content->main_flag)) {
		$DP_Content->title_tag = $name . ' — Fine Jewellery UAE';
		$DP_Content->description_tag = $name . ': ' . $tagline . ' Shop rings, necklaces, earrings & bridal collections with insured UAE delivery.';
		$DP_Content->keywords_tag = 'jewellery UAE, gold Dubai, diamond rings, necklaces, earrings, bridal, AED, ' . $name;
	} else {
		$page = trim(strip_tags((string) ($DP_Content->value ?? '')));
		if ($page === '') {
			$page = 'Shop';
		}
		foreach ($bad as $needle) {
			if (stripos($page, $needle) !== false) {
				$page = 'Shop';
				break;
			}
		}
		$DP_Content->title_tag = $page . ' — ' . $name;
		$DP_Content->description_tag = $page . ' at ' . $name . '. ' . $tagline;
		$DP_Content->keywords_tag = 'jewellery, gold, diamonds, ' . $name . ', UAE, AED';
	}
}

function epc_jewellery_retail_kiyasha_patch_template_html(string $html): string
{
	$title = epc_jewellery_retail_kiyasha_store_name() . ' — Fine Jewellery UAE';
	$desc = epc_jewellery_retail_kiyasha_store_name() . ': ' . epc_jewellery_retail_kiyasha_tagline();
	$keys = 'jewellery UAE, gold Dubai, diamond rings, necklaces, earrings, bridal, AED, ' . epc_jewellery_retail_kiyasha_store_name();
	$titleEsc = htmlspecialchars($title, ENT_QUOTES, 'UTF-8');
	$descEsc = htmlspecialchars($desc, ENT_QUOTES, 'UTF-8');
	$keysEsc = htmlspecialchars($keys, ENT_QUOTES, 'UTF-8');

	$html = preg_replace('/<title>[^<]*<\/title>/i', '<title>' . $titleEsc . '</title>', $html, 1);
	$html = preg_replace('/<meta name="keywords" content="[^"]*"[^>]*>/i', '<meta name="keywords" content="' . $keysEsc . '">', $html, 1);
	$html = preg_replace('/<meta name="description" content="[^"]*"[^>]*>/i', '<meta name="description" content="' . $descEsc . '">', $html, 1);

	return $html;
}

function epc_jewellery_retail_kiyasha_scrub_legacy_strings(string $html): string
{
	$replacements = array(
		'eParts Cart (Autoparts)' => epc_jewellery_retail_kiyasha_store_name(),
		'eParts Cart' => epc_jewellery_retail_kiyasha_store_name(),
		'(Autoparts)' => '',
		'Autoparts' => 'Jewellery',
		'auto parts' => 'jewellery',
		'autoparts' => 'jewellery',
		'spare parts' => 'pieces',
	);
	return str_ireplace(array_keys($replacements), array_values($replacements), $html);
}
