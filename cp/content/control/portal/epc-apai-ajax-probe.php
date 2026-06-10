<?php
/**
 * CP-path bootstrap probe for ajax_auto_price.php (deploy token via site root copy).
 */
declare(strict_types=1);
define('_ASTEXE_', 1);
header('Content-Type: application/json; charset=utf-8');

$steps = array();
try {
	$steps[] = array('step' => 'document_root', 'path' => (string) ($_SERVER['DOCUMENT_ROOT'] ?? ''));
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$steps[] = array('step' => 'config.php', 'ok' => true);
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	$steps[] = array('step' => 'dp_user.php', 'ok' => true);
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';
	$steps[] = array('step' => 'epc_auto_price_engine.php', 'ok' => true);
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_discovery_adapters.php';
	$steps[] = array('step' => 'epc_discovery_adapters.php', 'ok' => true);
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_country_sources.php';
	$steps[] = array('step' => 'epc_apai_country_sources.php', 'ok' => true);
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_industry_taxonomy.php';
	$steps[] = array('step' => 'epc_industry_taxonomy.php', 'ok' => true);
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php';
	$steps[] = array('step' => 'epc_auto_price_categories.php', 'ok' => true);
	$steps[] = array('step' => 'is_admin', 'ok' => true, 'value' => DP_User::isAdmin());
	echo json_encode(array('ok' => true, 'steps' => $steps), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	$steps[] = array('ok' => false, 'error' => $e->getMessage(), 'file' => $e->getFile(), 'line' => $e->getLine());
	echo json_encode(array('ok' => false, 'steps' => $steps), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
}
