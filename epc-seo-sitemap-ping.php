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
		'also_submit' => array(
			'https://www.ecomae.com/sitemap.xml',
			'https://www.ecomae.com/sitemap-marketing.php',
		),
		'sample_urls' => array(
			'https://www.ecomae.com/',
			'https://www.ecomae.com/platform',
			'https://www.ecomae.com/platform/industries',
			'https://www.ecomae.com/platform/auto-price-ai',
			'https://www.ecomae.com/platform/demo',
			'https://www.ecomae.com/platform/capabilities',
		),
	),
	array(
		'property' => 'https://energy.ecomae.com/',
		'role' => 'industry_hub_energy',
		'sitemap' => 'https://energy.ecomae.com/sitemap.xml',
		'also_submit' => array(
			'https://energy.ecomae.com/sitemap-index.php',
		),
		'sample_urls' => array(
			'https://energy.ecomae.com/',
			'https://energy.ecomae.com/biomass-bioenergy',
			'https://energy.ecomae.com/solar-energy',
			'https://energy.ecomae.com/wind-energy',
		),
		'note' => 'Prefer a Domain property for ecomae.com so all *.ecomae.com industry hubs are covered. Sub-industry pages like /biomass-bioenergy are listed in energy sitemap.xml.',
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
	'Best: add a Domain property for ecomae.com (DNS TXT) so www + energy + jewellery + all industry hubs are covered.',
	'Or add URL-prefix properties for https://www.ecomae.com/ and https://energy.ecomae.com/.',
	'Verify ownership (HTML file, DNS TXT, or Google Analytics — whichever you already use).',
	'Sitemaps → Add new sitemap → for energy submit https://energy.ecomae.com/sitemap.xml (includes biomass-bioenergy).',
	'For www submit https://www.ecomae.com/sitemap-index.php (includes marketing + industry absolute URLs).',
	'URL Inspection → paste https://energy.ecomae.com/biomass-bioenergy → Request indexing.',
	'Confirm site:energy.ecomae.com biomass after crawl (can take days). GA4 does not control indexing.',
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
