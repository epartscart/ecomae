<?php
/**
 * Renderers for ECOM AE public legal policies (/legal, /legal/<slug>, aliases).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_legal_content.php';

/**
 * Title + meta for legal hub / policy pages.
 *
 * @return array{0:string,1:string}
 */
function epc_ecomae_legal_meta(array $params = array()): array
{
	$cat = epc_ecomae_legal_catalog();
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
	if ($slug !== '' && isset($cat[$slug])) {
		return array(
			$cat[$slug]['title'] . ' — ECOM AE Legal',
			$cat[$slug]['summary'],
		);
	}
	return array(
		'Legal policies — ECOM AE',
		'Privacy, Terms, Security, Trademark, Right to Use, Copyright, Data Protection, and other legal policies for the ECOM AE Blockchain BOS Enterprise System.',
	);
}

/**
 * Canonical path for a legal page.
 */
function epc_ecomae_legal_canonical_path(array $params = array()): string
{
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
	return $slug !== '' ? '/legal/' . $slug : '/legal';
}

/**
 * Shared footer strip of all policy links (used on legal pages).
 */
function epc_ecomae_legal_related_links_html(string $currentSlug = ''): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$cat = epc_ecomae_legal_catalog();
	$html = '<nav class="epm-legal-related" aria-label="All legal policies" style="margin-top:36px;padding-top:20px;border-top:1px solid var(--epm-border)">';
	$html .= '<p style="margin:0 0 10px;color:var(--epm-muted);font-size:13px;font-weight:700">All policies</p>';
	$html .= '<div style="display:flex;flex-wrap:wrap;gap:8px 16px">';
	$html .= '<a href="' . epc_ecomae_h($base . '/legal') . '" style="color:var(--epm-cyan);font-size:13px;font-weight:600;text-decoration:none">Legal hub</a>';
	foreach ($cat as $slug => $p) {
		$style = ($slug === $currentSlug)
			? 'color:#fff;font-size:13px;font-weight:700;text-decoration:none'
			: 'color:var(--epm-muted);font-size:13px;font-weight:600;text-decoration:none';
		$html .= '<a href="' . epc_ecomae_h($base . '/legal/' . $slug) . '" style="' . $style . '">' . epc_ecomae_h($p['title']) . '</a>';
	}
	$html .= '</div></nav>';
	return $html;
}

function epc_ecomae_platform_page_legal(array $params = array()): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$cat = epc_ecomae_legal_catalog();
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
	$effective = epc_ecomae_legal_effective_date();

	if ($slug !== '' && isset($cat[$slug])) {
		$d = $cat[$slug];
		ob_start();
		echo '<div class="epm-wrap"><div class="epm-section">';
		if (function_exists('epc_ecomae_marketing_crumb')) {
			echo epc_ecomae_marketing_crumb($d['title'], $base . '/legal', 'Legal');
		}
		echo '<div class="epm-badge"><i class="fa ' . epc_ecomae_h($d['icon']) . '"></i> Legal policy</div>';
		echo '<h1 class="epm-section-title" style="font-size:32px;margin-top:10px">' . epc_ecomae_h($d['title']) . '</h1>';
		echo '<p class="epm-section-lead" style="max-width:820px">' . epc_ecomae_h($d['summary']) . '</p>';
		echo '<p style="color:var(--epm-muted);font-size:13px;margin:0 0 22px">Effective date: ' . epc_ecomae_h($effective) . ' · Electronic World Group · Dubai, UAE</p>';

		foreach ($d['sections'] as $sec) {
			echo '<section style="margin-top:22px;max-width:820px">';
			echo '<h2 style="font-size:20px;margin:0 0 8px;color:#e2e8f0">' . epc_ecomae_h($sec['h']) . '</h2>';
			if (!empty($sec['p'])) {
				foreach ($sec['p'] as $para) {
					echo '<p style="color:#94a3b8;line-height:1.75;margin:0 0 10px">' . epc_ecomae_h($para) . '</p>';
				}
			}
			if (!empty($sec['bullets'])) {
				echo '<ul class="epm-eco-model__list" style="max-width:820px;color:#94a3b8">';
				foreach ($sec['bullets'] as $b) {
					echo '<li style="margin-bottom:6px">' . epc_ecomae_h($b) . '</li>';
				}
				echo '</ul>';
			}
			echo '</section>';
		}

		echo '<p style="margin-top:28px;max-width:820px;color:var(--epm-muted);font-size:13px;line-height:1.6">These policies are provided for transparency regarding the ECOM AE Blockchain BOS Enterprise System. They do not constitute legal advice. For enterprise contracts, the signed commercial agreement prevails if there is a conflict.</p>';
		echo epc_ecomae_legal_related_links_html($slug);
		echo '</div></div>';
		if (function_exists('epc_ecomae_marketing_article_jsonld')) {
			echo epc_ecomae_marketing_article_jsonld(
				$d['title'] . ' — ECOM AE',
				$d['summary'],
				$base . '/legal/' . $slug
			);
		}
		return ob_get_clean();
	}

	// Hub
	ob_start();
	echo '<div class="epm-wrap">';
	echo '<div class="epm-hero" style="min-height:auto;padding:36px 0"><div class="epm-hero__content">';
	echo '<div class="epm-badge"><i class="fa fa-balance-scale"></i> Legal</div>';
	echo '<h1 class="epm-section-title" style="font-size:36px;margin-top:10px">ECOM AE legal policies</h1>';
	echo '<p class="epm-section-lead" style="max-width:780px">Privacy, security, trademark, right to use, copyright, data protection, and related policies that govern the ECOM AE Blockchain BOS Enterprise System and our websites.</p>';
	echo '<p style="color:var(--epm-muted);font-size:13px;margin:12px 0 0">Effective date: ' . epc_ecomae_h($effective) . '</p>';
	echo '</div></div>';

	echo '<div class="epm-eco-model__grid" style="margin-top:8px">';
	foreach ($cat as $key => $d) {
		echo '<a class="epm-card epm-card--accent" style="text-decoration:none;color:inherit;display:block" href="' . epc_ecomae_h($base . '/legal/' . $key) . '">';
		echo '<h3><i class="fa ' . epc_ecomae_h($d['icon']) . '"></i> ' . epc_ecomae_h($d['title']) . '</h3>';
		echo '<p style="color:#94a3b8;margin:6px 0 0">' . epc_ecomae_h($d['summary']) . '</p></a>';
	}
	echo '</div>';
	echo epc_ecomae_legal_related_links_html('');
	echo '</div>';
	return ob_get_clean();
}
