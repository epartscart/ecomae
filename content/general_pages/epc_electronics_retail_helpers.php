<?php
/**
 * Electronics retail (Virgin package) — SEO, store URL, autoparts chrome suppression.
 */
defined('_ASTEXE_') or die('No access');

function epc_electronics_retail_store_name(): string
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
	return 'Electronicae';
}

function epc_electronics_retail_tagline(): string
{
	$settings = epc_portal_load_site_settings();
	if (!empty($settings['tagline'])) {
		return trim((string) $settings['tagline']);
	}
	return 'Shop phones, gaming, audio & laptops — UAE delivery, prices in AED';
}

function epc_electronics_retail_public_url(): string
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

/**
 * Override CMS meta tags so autoparts / eParts defaults never appear on electronics retail.
 */
function epc_electronics_retail_apply_seo($DP_Content): void
{
	if (!is_object($DP_Content)) {
		return;
	}
	$name = epc_electronics_retail_store_name();
	$tagline = epc_electronics_retail_tagline();
	$bad = array('epartscart', 'eParts Cart', 'Autoparts', 'auto parts', 'autoparts', 'spare parts', 'Docpart');

	if (!empty($DP_Content->main_flag)) {
		$DP_Content->title_tag = $name . ' — Tech, Gaming & Electronics UAE';
		$DP_Content->description_tag = $name . ': ' . $tagline . ' Free delivery across the UAE. Samsung, Apple, Sony, gaming & more.';
		$DP_Content->keywords_tag = 'electronics UAE, smartphones, gaming UAE, laptops, headphones, smart home, AED, ' . $name;
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
		$DP_Content->keywords_tag = 'electronics, ' . $name . ', UAE, AED';
	}
}

/**
 * Replace autoparts SEO tags already injected by dp_core (before template eval).
 */
function epc_electronics_retail_patch_template_html(string $html): string
{
	$title = epc_electronics_retail_store_name() . ' — Tech, Gaming & Electronics UAE';
	$desc = epc_electronics_retail_store_name() . ': ' . epc_electronics_retail_tagline();
	$keys = 'electronics UAE, smartphones, gaming, laptops, headphones, smart home, AED, ' . epc_electronics_retail_store_name();
	$titleEsc = htmlspecialchars($title, ENT_QUOTES, 'UTF-8');
	$descEsc = htmlspecialchars($desc, ENT_QUOTES, 'UTF-8');
	$keysEsc = htmlspecialchars($keys, ENT_QUOTES, 'UTF-8');

	$html = preg_replace('/<title>[^<]*<\/title>/i', '<title>' . $titleEsc . '</title>', $html, 1);
	$html = preg_replace('/<meta name="keywords" content="[^"]*"[^>]*>/i', '<meta name="keywords" content="' . $keysEsc . '">', $html, 1);
	$html = preg_replace('/<meta name="description" content="[^"]*"[^>]*>/i', '<meta name="description" content="' . $descEsc . '">', $html, 1);

	return $html;
}

function epc_electronics_retail_scrub_autoparts_strings(string $html): string
{
	$replacements = array(
		'eParts Cart (Autoparts)' => epc_electronics_retail_store_name(),
		'eParts Cart' => epc_electronics_retail_store_name(),
		'(Autoparts)' => '',
		'Autoparts' => 'Electronics',
		'auto parts' => 'electronics',
		'autoparts' => 'electronics',
		'spare parts' => 'products',
	);
	return str_ireplace(array_keys($replacements), array_values($replacements), $html);
}
