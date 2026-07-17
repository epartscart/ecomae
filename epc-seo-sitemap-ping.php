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
		'property' => 'sc-domain:ecomae.com (recommended) or https://www.ecomae.com/',
		'role' => 'all_industries_and_marketing',
		'sitemap' => 'https://www.ecomae.com/sitemap-industries.php',
		'also_submit' => array(
			'https://www.ecomae.com/sitemap-index.php',
			'https://www.ecomae.com/sitemap.xml',
			'https://www.ecomae.com/sitemap-marketing.php',
		),
		'sample_urls' => array(
			'https://www.ecomae.com/platform/industries',
			'https://energy.ecomae.com/',
			'https://energy.ecomae.com/biomass-bioenergy',
			'https://jewellery.ecomae.com/',
			'https://automotive.ecomae.com/',
			'https://healthcare.ecomae.com/',
		),
		'note' => 'sitemap-industries.php lists EVERY industry hub + EVERY sub-industry (energy, jewellery, automotive, …). Use a Domain property for ecomae.com so cross-host URLs are accepted.',
	),
	array(
		'property' => 'https://energy.ecomae.com/',
		'role' => 'industry_hub_energy_only',
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
		'note' => 'ENERGY ONLY — hub + energy sub-industries. Does NOT include jewellery, automotive, etc. For all industries submit https://www.ecomae.com/sitemap-industries.php on the Domain property.',
	),
	array(
		'property' => 'https://www.epartscart.com',
		'role' => 'warehouse_storefront',
		'sitemap' => 'https://www.epartscart.com/sitemap-index.php',
		'also_submit' => array(
			'https://www.epartscart.com/sitemap-products.php',
			'https://www.epartscart.com/sitemap-warehouse-0.xml',
		),
		'sample_urls' => array(
			'https://www.epartscart.com/en/parts',
			'https://www.epartscart.com/en/available-brands',
			'https://www.epartscart.com/en/parts/GMB/GUT21',
			'https://www.epartscart.com/sitemap-warehouse-0.xml',
		),
		'note' => 'Index children MUST be sitemap-warehouse-0.xml … (no query strings — GSC fails on ?n=). After price uploads re-warm with &auto=1. Resubmit only sitemap-index.php.',
		'warm' => 'https://www.epartscart.com/epc-seo-sitemap-warm.php?token=epartscart-deploy-2026&auto=1',
		'warm_status' => 'https://www.epartscart.com/epc-seo-sitemap-warm.php?token=epartscart-deploy-2026&status=1',
		'sample_child' => 'https://www.epartscart.com/sitemap-warehouse-0.xml',
	),
);

$steps = array(
	'Open Google Search Console: https://search.google.com/search-console',
	'Best: add a Domain property for ecomae.com (DNS TXT) so www + energy + jewellery + all industry hubs are covered.',
	'ALL industries + sub-industries: Sitemaps → Add → https://www.ecomae.com/sitemap-industries.php',
	'Also submit https://www.ecomae.com/sitemap-index.php (includes industries + marketing pages).',
	'ENERGY ONLY (optional URL-prefix property): submit https://energy.ecomae.com/sitemap.xml — does not cover other industries.',
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
