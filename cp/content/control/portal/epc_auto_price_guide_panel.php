<?php
/**
 * Auto Price AI — tenant operator guide panel (included from engine tab or standalone route).
 * eval()-safe: included via DOCUMENT_ROOT path from parent (never __DIR__ in eval context).
 */
defined('_ASTEXE_') or die('No access');

if (!isset($pdo) || !$pdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}
if (!isset($siteKey) || $siteKey === '') {
	$siteKey = 'electronicae';
}
if (!isset($backend) || $backend === '') {
	global $DP_Config;
	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
}
if (!isset($isSuperCp)) {
	$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
}
if (!isset($pageBase)) {
	$pageBase = '/' . $backend . '/control/portal/epc_auto_price_engine';
}

$guideSiteKey = $siteKey;
if ($isSuperCp && ($siteKey === 'platform' || $siteKey === '')) {
	$guideSiteKey = 'electronicae';
}
$ctx = epc_ape_guide_context($pdo, $guideSiteKey, $backend, $isSuperCp);
$urls = $ctx['urls'];
$demo = $ctx['demo'];
$discKpi = epc_disc_kpi($pdo, $guideSiteKey);
if (!isset($rules)) {
	$rules = epc_ape_rules_get($pdo, $guideSiteKey);
}
$profile = (string) ($ctx['profile'] ?? 'marketplace_arbitrage');
$industryKey = (string) ($ctx['industry_key'] ?? 'electronics');
$engineUrl = $isSuperCp ? $urls['engine_super'] : $urls['engine_tenant'];
$compareUrl = $isSuperCp ? $urls['compare_super'] : $urls['compare_tenant'];
$marginPct = (string) ($rules['min_margin_percent'] ?? 12);
$tenantFeaturesUrl = '/' . $backend . '/control/portal/epc_tenant_features';
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php';
	$tenantCountryCode = epc_apai_tenant_country($guideSiteKey, $pdo);
	$tenantCountryMeta = epc_apai_country_meta($tenantCountryCode);
} else {
	$tenantCountryCode = 'AE';
	$tenantCountryMeta = array('label' => 'United Arab Emirates');
}
$tenantHost = 'www.' . $guideSiteKey . '.com';
if ($guideSiteKey === 'epartscart') {
	$tenantHost = 'www.epartscart.com';
} elseif ($guideSiteKey === 'electronicae') {
	$tenantHost = 'www.electronicae.com';
} elseif ($guideSiteKey === 'ecomae') {
	$tenantHost = 'www.ecomae.com';
}
$runCrawlUrl = 'https://' . $tenantHost . '/epc-apai-hourly-crawl.php?token=epartscart-deploy-2026&site_key=' . urlencode($guideSiteKey);

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_guide_shared.php';
epc_apai_guide_render(array(
	'mode' => 'operator',
	'ctx' => $ctx,
	'site_key' => $guideSiteKey,
	'profile' => $profile,
	'industry_key' => $industryKey,
	'margin_pct' => $marginPct,
	'is_super_cp' => $isSuperCp,
	'urls' => $urls,
	'disc_kpi' => $discKpi,
	'demo' => $demo,
	'engine_url' => $engineUrl,
	'compare_url' => $compareUrl,
	'country_code' => $tenantCountryCode,
	'country_label' => (string) ($tenantCountryMeta['label'] ?? $tenantCountryCode),
	'tenant_features_url' => $tenantFeaturesUrl,
	'run_crawl_url' => $runCrawlUrl,
	'backend' => $backend,
));
