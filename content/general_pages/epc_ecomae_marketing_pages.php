<?php
/**
 * Renderers for ECOM AE marketing / SEO + AI-visibility pages.
 * Page data lives in epc_ecomae_marketing_content.php. These functions are
 * dispatched by epc_ecomae_platform_render_inner() as page_<name>($params).
 */

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_marketing_content.php';

/**
 * Title + meta description for the marketing pages (slug-aware).
 * @return array{0:string,1:string}|null [title, description] or null if not ours.
 */
function epc_ecomae_marketing_meta(string $page, array $params = array())
{
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
	switch ($page) {
		case 'docs':
			$cat = epc_ecomae_docs_catalog();
			if ($slug !== '' && isset($cat[$slug])) {
				return array($cat[$slug]['title'] . ' — ECOM AE documentation', $cat[$slug]['summary']);
			}
			return array('Documentation — ECOM AE', 'Public documentation for the ECOM AE Blockchain BOS Enterprise System: overview, ERP modules, API, security, industry packs and user guides.');
		case 'compare':
			$cat = epc_ecomae_compare_catalog();
			if ($slug !== '' && isset($cat[$slug])) {
				return array($cat[$slug]['tagline'] . ' — comparison | ECOM AE', $cat[$slug]['intro']);
			}
			return array('ECOM AE compared vs Odoo, ERPNext, Zoho, NetSuite, Dynamics 365', 'How ECOM AE — a Blockchain BOS Enterprise System — compares with leading ERP and business platforms.');
		case 'bos':
			$cat = epc_ecomae_bos_articles_catalog();
			if ($slug !== '' && isset($cat[$slug])) {
				return array($cat[$slug]['title'] . ' | ECOM AE', $cat[$slug]['summary']);
			}
			return array('What is a Blockchain BOS Enterprise System? | ECOM AE', 'Understand the Blockchain BOS Enterprise category: Blockchain BOS vs ERP, vs CRM, and why modern businesses run on one unified verifiable system.');
		case 'solution':
			$cat = epc_ecomae_solutions_catalog();
			if ($slug !== '' && isset($cat[$slug])) {
				return array($cat[$slug]['h1'] . ' | ECOM AE', $cat[$slug]['lead']);
			}
			return array('ECOM AE solutions', 'Purpose-built solution pages for the platform, region and modules ECOM AE serves.');
	}
	return null;
}

/** FAQ JSON-LD block for a list of [question, answer] pairs. */
function epc_ecomae_marketing_faq_jsonld(array $faqs): string
{
	if (!$faqs) {
		return '';
	}
	$items = array();
	foreach ($faqs as $f) {
		$items[] = array(
			'@type' => 'Question',
			'name' => (string) $f[0],
			'acceptedAnswer' => array('@type' => 'Answer', 'text' => (string) $f[1]),
		);
	}
	$data = array('@context' => 'https://schema.org', '@type' => 'FAQPage', 'mainEntity' => $items);
	return '<script type="application/ld+json">' . json_encode($data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . '</script>';
}

/** Article JSON-LD. */
function epc_ecomae_marketing_article_jsonld(string $headline, string $desc, string $url): string
{
	$data = array(
		'@context' => 'https://schema.org',
		'@type' => 'Article',
		'headline' => $headline,
		'description' => $desc,
		'mainEntityOfPage' => $url,
		'author' => array('@type' => 'Organization', 'name' => 'ECOM AE'),
		'publisher' => array('@type' => 'Organization', 'name' => 'ECOM AE'),
	);
	return '<script type="application/ld+json">' . json_encode($data, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE) . '</script>';
}

/** Visible FAQ section. */
function epc_ecomae_marketing_faq_html(array $faqs): string
{
	if (!$faqs) {
		return '';
	}
	$h = '<section class="epm-card" style="margin-top:24px"><h2 class="epm-section-title">Frequently asked questions</h2>';
	foreach ($faqs as $f) {
		$h .= '<div style="margin-top:14px"><h3 style="margin:0 0 4px;font-size:17px">' . epc_ecomae_h($f[0]) . '</h3>'
			. '<p style="margin:0;color:#475569">' . epc_ecomae_h($f[1]) . '</p></div>';
	}
	$h .= '</section>';
	return $h;
}

/** Simple breadcrumb + back link. */
function epc_ecomae_marketing_crumb(string $label, string $hubUrl, string $hubLabel): string
{
	return '<nav class="epm-badge" style="margin-bottom:14px"><a href="' . epc_ecomae_h($hubUrl) . '" style="color:inherit"><i class="fa fa-angle-left"></i> ' . epc_ecomae_h($hubLabel) . '</a> · ' . epc_ecomae_h($label) . '</nav>';
}

/* =====================================================================
 * /docs — documentation hub + individual sections
 * ===================================================================== */
function epc_ecomae_platform_page_docs(array $params = array()): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$cat = epc_ecomae_docs_catalog();
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';

	if ($slug !== '' && isset($cat[$slug])) {
		$d = $cat[$slug];
		ob_start();
		echo '<div class="epm-wrap"><div class="epm-section">';
		echo epc_ecomae_marketing_crumb($d['title'], $base . '/documentation', 'Documentation');
		echo '<h1 class="epm-section-title" style="font-size:32px"><i class="fa ' . epc_ecomae_h($d['icon']) . '"></i> ' . epc_ecomae_h($d['title']) . '</h1>';
		echo '<p class="epm-section-lead" style="max-width:820px">' . epc_ecomae_h($d['summary']) . '</p>';
		foreach ($d['body'] as $p) {
			echo '<p style="max-width:820px;color:#334155;line-height:1.7">' . epc_ecomae_h($p) . '</p>';
		}
		if (!empty($d['bullets'])) {
			echo '<ul class="epm-eco-model__list" style="max-width:820px">';
			foreach ($d['bullets'] as $b) { echo '<li>' . epc_ecomae_h($b) . '</li>'; }
			echo '</ul>';
		}
		echo '</div></div>';
		echo epc_ecomae_marketing_article_jsonld($d['title'] . ' — ECOM AE documentation', $d['summary'], $base . '/documentation/' . $slug);
		return ob_get_clean();
	}

	ob_start();
	echo '<div class="epm-wrap">';
	echo '<div class="epm-hero" style="min-height:auto;padding:36px 0"><div class="epm-hero__content"><div class="epm-badge"><i class="fa fa-book"></i> Documentation</div>';
	echo '<h1 class="epm-section-title" style="font-size:36px;margin-top:10px">ECOM AE documentation</h1>';
	echo '<p class="epm-section-lead" style="max-width:780px">Public documentation for the ECOM AE Blockchain BOS Enterprise System — platform overview, ERP modules, API, security, industry packs and user guides.</p></div></div>';
	echo '<div class="epm-eco-model__grid" style="margin-top:8px">';
	foreach ($cat as $key => $d) {
		echo '<a class="epm-card epm-card--accent" style="text-decoration:none;color:inherit;display:block" href="' . epc_ecomae_h($base . '/documentation/' . $key) . '">';
		echo '<h3><i class="fa ' . epc_ecomae_h($d['icon']) . '"></i> ' . epc_ecomae_h($d['title']) . '</h3>';
		echo '<p style="color:#475569;margin:6px 0 0">' . epc_ecomae_h($d['summary']) . '</p></a>';
	}
	echo '</div></div>';
	return ob_get_clean();
}

/* =====================================================================
 * /compare — comparison hub + individual comparisons
 * ===================================================================== */
function epc_ecomae_platform_page_compare(array $params = array()): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$cat = epc_ecomae_compare_catalog();
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';

	if ($slug !== '' && isset($cat[$slug])) {
		$c = $cat[$slug];
		ob_start();
		echo '<div class="epm-wrap"><div class="epm-section">';
		echo epc_ecomae_marketing_crumb($c['tagline'], $base . '/compare', 'Comparisons');
		echo '<h1 class="epm-section-title" style="font-size:32px">' . epc_ecomae_h($c['tagline']) . '</h1>';
		echo '<p class="epm-section-lead" style="max-width:840px">' . epc_ecomae_h($c['intro']) . '</p>';
		echo '<div style="overflow-x:auto"><table class="epm-compare-table" style="width:100%;border-collapse:collapse;margin-top:18px">';
		echo '<thead><tr><th style="text-align:left;padding:10px;border-bottom:2px solid #e2e8f0"></th>'
			. '<th style="text-align:left;padding:10px;border-bottom:2px solid #e2e8f0">ECOM AE</th>'
			. '<th style="text-align:left;padding:10px;border-bottom:2px solid #e2e8f0">' . epc_ecomae_h($c['competitor']) . '</th></tr></thead><tbody>';
		foreach ($c['rows'] as $r) {
			echo '<tr><td style="padding:10px;border-bottom:1px solid #eef2f7;font-weight:600">' . epc_ecomae_h($r[0]) . '</td>'
				. '<td style="padding:10px;border-bottom:1px solid #eef2f7;color:#0f766e">' . epc_ecomae_h($r[1]) . '</td>'
				. '<td style="padding:10px;border-bottom:1px solid #eef2f7;color:#475569">' . epc_ecomae_h($r[2]) . '</td></tr>';
		}
		echo '</tbody></table></div>';
		echo '<div class="epm-highlight" style="margin-top:18px"><p><strong>When to choose which:</strong> ' . epc_ecomae_h($c['whenThem']) . '</p></div>';
		echo epc_ecomae_marketing_faq_html($c['faq']);
		echo '</div></div>';
		echo epc_ecomae_marketing_faq_jsonld($c['faq']);
		echo epc_ecomae_marketing_article_jsonld($c['tagline'], $c['intro'], $base . '/compare/' . $slug);
		return ob_get_clean();
	}

	ob_start();
	echo '<div class="epm-wrap"><div class="epm-section">';
	echo '<div class="epm-badge"><i class="fa fa-balance-scale"></i> Comparisons</div>';
	echo '<h1 class="epm-section-title" style="font-size:34px;margin-top:10px">ECOM AE compared</h1>';
	echo '<p class="epm-section-lead" style="max-width:760px">How ECOM AE — a Blockchain BOS Enterprise System — compares with leading ERP and business platforms.</p>';
	echo '<div class="epm-eco-model__grid" style="margin-top:8px">';
	foreach ($cat as $key => $c) {
		echo '<a class="epm-card epm-card--accent" style="text-decoration:none;color:inherit;display:block" href="' . epc_ecomae_h($base . '/compare/' . $key) . '">';
		echo '<h3>' . epc_ecomae_h($c['tagline']) . '</h3>';
		echo '<p style="color:#475569;margin:6px 0 0">' . epc_ecomae_h($c['intro']) . '</p></a>';
	}
	echo '</div></div></div>';
	return ob_get_clean();
}

/* =====================================================================
 * /bos — Business-Operating-System category articles
 * ===================================================================== */
function epc_ecomae_platform_page_bos(array $params = array()): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$cat = epc_ecomae_bos_articles_catalog();
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';

	if ($slug !== '' && isset($cat[$slug])) {
		$a = $cat[$slug];
		ob_start();
		echo '<div class="epm-wrap"><div class="epm-section">';
		echo epc_ecomae_marketing_crumb($a['title'], $base . '/bos', 'Blockchain BOS knowledge');
		echo '<h1 class="epm-section-title" style="font-size:32px">' . epc_ecomae_h($a['title']) . '</h1>';
		echo '<p class="epm-section-lead" style="max-width:840px">' . epc_ecomae_h($a['summary']) . '</p>';
		foreach ($a['body'] as $p) {
			echo '<p style="max-width:840px;color:#334155;line-height:1.7">' . epc_ecomae_h($p) . '</p>';
		}
		echo epc_ecomae_marketing_faq_html($a['faq']);
		echo '</div></div>';
		echo epc_ecomae_marketing_faq_jsonld($a['faq']);
		echo epc_ecomae_marketing_article_jsonld($a['title'], $a['summary'], $base . '/bos/' . $slug);
		return ob_get_clean();
	}

	ob_start();
	echo '<div class="epm-wrap"><div class="epm-section">';
	echo '<div class="epm-badge"><i class="fa fa-lightbulb-o"></i> Blockchain BOS knowledge</div>';
	echo '<h1 class="epm-section-title" style="font-size:34px;margin-top:10px">The Blockchain BOS Enterprise category</h1>';
	echo '<p class="epm-section-lead" style="max-width:780px">What a Blockchain BOS Enterprise System is, how it differs from ERP and CRM, and why modern businesses run on one unified verifiable system.</p>';
	echo '<div class="epm-eco-model__grid" style="margin-top:8px">';
	foreach ($cat as $key => $a) {
		echo '<a class="epm-card epm-card--accent" style="text-decoration:none;color:inherit;display:block" href="' . epc_ecomae_h($base . '/bos/' . $key) . '">';
		echo '<h3>' . epc_ecomae_h($a['title']) . '</h3>';
		echo '<p style="color:#475569;margin:6px 0 0">' . epc_ecomae_h($a['summary']) . '</p></a>';
	}
	echo '</div></div></div>';
	return ob_get_clean();
}

/* =====================================================================
 * /solutions/<slug> — AI-visibility landing pages
 * ===================================================================== */
function epc_ecomae_platform_page_solution(array $params = array()): string
{
	$base = rtrim(epc_ecomae_platform_base_url(), '/');
	$cat = epc_ecomae_solutions_catalog();
	$slug = isset($params['slug']) ? preg_replace('/[^a-z0-9\-]/', '', (string) $params['slug']) : '';
	if ($slug === '' || !isset($cat[$slug])) {
		// Index of solutions.
		ob_start();
		echo '<div class="epm-wrap"><div class="epm-section">';
		echo '<div class="epm-badge"><i class="fa fa-rocket"></i> Solutions</div>';
		echo '<h1 class="epm-section-title" style="font-size:34px;margin-top:10px">ECOM AE solutions</h1>';
		echo '<p class="epm-section-lead" style="max-width:760px">Purpose-built solution pages for the platform, region and modules ECOM AE serves.</p>';
		echo '<div class="epm-eco-model__grid" style="margin-top:8px">';
		foreach ($cat as $key => $s) {
			echo '<a class="epm-card epm-card--accent" style="text-decoration:none;color:inherit;display:block" href="' . epc_ecomae_h($base . '/solutions/' . $key) . '">';
			echo '<h3>' . epc_ecomae_h($s['h1']) . '</h3>';
			echo '<p style="color:#475569;margin:6px 0 0">' . epc_ecomae_h($s['lead']) . '</p></a>';
		}
		echo '</div></div></div>';
		return ob_get_clean();
	}

	$s = $cat[$slug];
	$superCp = function_exists('epc_ecomae_platform_super_cp_url') ? epc_ecomae_platform_super_cp_url() : ($base . '/cp/');
	ob_start();
	echo '<div class="epm-wrap">';
	echo '<div class="epm-hero" style="min-height:auto;padding:40px 0"><div class="epm-hero__content">';
	echo epc_ecomae_marketing_crumb($s['h1'], $base . '/solutions', 'Solutions');
	echo '<h1 class="epm-section-title" style="font-size:38px">' . epc_ecomae_h($s['h1']) . '</h1>';
	echo '<p class="epm-section-lead" style="max-width:820px">' . epc_ecomae_h($s['lead']) . '</p>';
	echo '<div style="margin-top:18px"><a class="epm-btn epm-btn--primary" href="' . epc_ecomae_h($base . '/platform/demo') . '">Get a demo</a> '
		. '<a class="epm-btn epm-btn--outline" href="' . epc_ecomae_h($base . '/documentation') . '" style="margin-left:8px">Read the docs</a></div>';
	echo '</div></div>';
	echo '<div class="epm-section">';
	foreach ($s['body'] as $p) {
		echo '<p style="max-width:820px;color:#334155;line-height:1.7">' . epc_ecomae_h($p) . '</p>';
	}
	if (!empty($s['features'])) {
		echo '<div class="epm-eco-model__grid" style="margin-top:14px">';
		foreach ($s['features'] as $f) {
			echo '<div class="epm-card"><h3 style="font-size:16px;margin:0"><i class="fa fa-check text-success"></i> ' . epc_ecomae_h($f) . '</h3></div>';
		}
		echo '</div>';
	}
	echo epc_ecomae_marketing_faq_html($s['faq']);
	echo '</div></div>';
	echo epc_ecomae_marketing_faq_jsonld($s['faq']);
	echo epc_ecomae_marketing_article_jsonld($s['h1'] . ' — ECOM AE', $s['lead'], $base . '/solutions/' . $slug);
	return ob_get_clean();
}
