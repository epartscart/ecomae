<?php
/**
 * Consulting Prime Invest package — SEO and autoparts string suppression.
 */
defined('_ASTEXE_') or die('No access');

function epc_cpi_store_name(): string
{
	if (!function_exists('epc_portal_load_site_settings')) {
		require_once __DIR__ . '/epc_portal.php';
	}
	$settings = epc_portal_load_site_settings();
	if (!empty($settings['contact']['trade_name'])) {
		return trim((string) $settings['contact']['trade_name']);
	}
	if (!empty($settings['system_name'])) {
		return trim((string) $settings['system_name']);
	}
	$site = epc_portal_site_profile();
	if (!empty($site['system_name'])) {
		return trim((string) $site['system_name']);
	}
	return 'Taxofinca';
}

function epc_cpi_tagline(): string
{
	$settings = epc_portal_load_site_settings();
	if (!empty($settings['tagline'])) {
		return trim((string) $settings['tagline']);
	}
	return 'Tax & advisory services — UAE corporate tax, VAT and business compliance';
}

function epc_cpi_apply_seo($DP_Content): void
{
	if (!is_object($DP_Content)) {
		return;
	}
	$name = epc_cpi_store_name();
	$tagline = epc_cpi_tagline();
	$bad = array('epartscart', 'eParts Cart', 'Autoparts', 'auto parts', 'autoparts', 'spare parts', 'Docpart');

	if (!empty($DP_Content->main_flag)) {
		$DP_Content->title_tag = $name . ' — Tax, Accounting & Advisory UAE';
		$DP_Content->description_tag = $name . ': ' . $tagline;
		$DP_Content->keywords_tag = 'tax advisory UAE, corporate tax, VAT, accounting, ' . $name . ', business compliance';
	} else {
		$page = trim(strip_tags((string) ($DP_Content->value ?? '')));
		if ($page === '') {
			$page = 'Portal';
		}
		foreach ($bad as $needle) {
			if (stripos($page, $needle) !== false) {
				$page = 'Portal';
				break;
			}
		}
		$DP_Content->title_tag = $page . ' — ' . $name;
		$DP_Content->description_tag = $page . ' at ' . $name . '. ' . $tagline;
		$DP_Content->keywords_tag = 'tax advisory, accounting, ' . $name . ', UAE';
	}
}

function epc_cpi_patch_template_html(string $html): string
{
	$name = epc_cpi_store_name();
	$title = $name . ' — Tax, Accounting & Advisory UAE';
	$desc = $name . ': ' . epc_cpi_tagline();
	$keys = 'tax advisory UAE, corporate tax, VAT, accounting, ' . $name;
	$titleEsc = htmlspecialchars($title, ENT_QUOTES, 'UTF-8');
	$descEsc = htmlspecialchars($desc, ENT_QUOTES, 'UTF-8');
	$keysEsc = htmlspecialchars($keys, ENT_QUOTES, 'UTF-8');

	$html = preg_replace('/<title>[^<]*<\/title>/i', '<title>' . $titleEsc . '</title>', $html, 1);
	$html = preg_replace('/<meta name="keywords" content="[^"]*"[^>]*>/i', '<meta name="keywords" content="' . $keysEsc . '">', $html, 1);
	$html = preg_replace('/<meta name="description" content="[^"]*"[^>]*>/i', '<meta name="description" content="' . $descEsc . '">', $html, 1);

	$replacements = array(
		'eParts Cart (Autoparts)' => $name,
		'eParts Cart' => $name,
		'(Autoparts)' => '',
		'Autoparts' => 'Tax advisory',
	);
	return str_ireplace(array_keys($replacements), array_values($replacements), $html);
}
