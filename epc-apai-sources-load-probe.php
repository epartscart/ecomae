<?php
/**
 * Quick probe — marketplace_channels + country_sources load without full CP render.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);

try {
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_marketplace_channels.php';
	$domains = function_exists('epc_apai_tenant_own_domains')
		? epc_apai_tenant_own_domains('epartscart', null)
		: array();
	echo json_encode(array(
		'ok' => true,
		'functions' => array(
			'epc_apai_tenant_own_domains' => function_exists('epc_apai_tenant_own_domains'),
			'epc_apai_source_role' => function_exists('epc_apai_source_role'),
			'epc_apai_marketplace_channels_for_tenant' => function_exists('epc_apai_marketplace_channels_for_tenant'),
		),
		'epartscart_own_domains' => $domains,
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
