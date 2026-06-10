<?php
/**
 * Google Search Console sitemap submission guide (read-only — no GSC API access).
 * GET ?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$sites = array(
	array(
		'property' => 'https://www.ecomae.com',
		'role' => 'marketing_platform',
		'sitemap' => 'https://www.ecomae.com/sitemap-index.php',
		'sample_urls' => array(
			'https://www.ecomae.com/',
			'https://www.ecomae.com/platform',
			'https://www.ecomae.com/platform/auto-price-ai',
			'https://www.ecomae.com/platform/demo',
			'https://www.ecomae.com/platform/capabilities',
		),
	),
	array(
		'property' => 'https://www.epartscart.com',
		'role' => 'warehouse_storefront',
		'sitemap' => 'https://www.epartscart.com/sitemap-index.php',
		'sample_urls' => array(
			'https://www.epartscart.com/en/parts',
			'https://www.epartscart.com/en/available-brands',
			'https://www.epartscart.com/en/spare-parts',
			'https://www.epartscart.com/en/parts/GMB/GUT21',
		),
	),
);

$steps = array(
	'Open Google Search Console: https://search.google.com/search-console',
	'Add each property (URL-prefix https://www.ecomae.com and https://www.epartscart.com) or use Domain property if DNS TXT verification is available.',
	'Verify ownership (HTML file, DNS TXT, or Google Analytics — whichever you already use).',
	'Sitemaps → Add new sitemap → enter sitemap-index.php (full URL shown per site below).',
	'URL Inspection → paste a sample URL → confirm "URL is on Google" or "Indexing requested" after Request indexing.',
	'Settings → Crawl stats: confirm Googlebot fetches /robots.txt and sitemap-index.php without errors.',
	'After 7–14 days: Page indexing report → fix any "Excluded by noindex" on in-stock part pages.',
	'Optional: ping Google when sitemap changes — open https://www.google.com/ping?sitemap=ENCODED_SITEMAP_URL (legacy; GSC submit is preferred).',
);

$verifyProbes = array(
	'ecomae_readiness' => 'https://www.ecomae.com/epc-seo-google-readiness.php?token=epartscart-deploy-2026&host=www.ecomae.com',
	'epartscart_readiness' => 'https://www.ecomae.com/epc-seo-google-readiness.php?token=epartscart-deploy-2026&host=www.epartscart.com',
	'ecomae_index_verify' => 'https://www.ecomae.com/epc-seo-index-verify.php?token=epartscart-deploy-2026&host=www.ecomae.com',
	'epartscart_index_verify' => 'https://www.ecomae.com/epc-seo-index-verify.php?token=epartscart-deploy-2026&host=www.epartscart.com',
);

echo json_encode(array(
	'ok' => true,
	'note' => 'Automated GSC sitemap submission requires OAuth service account access — not configured. Follow manual steps below.',
	'sites' => $sites,
	'manual_steps' => $steps,
	'verify_after_submit' => $verifyProbes,
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
