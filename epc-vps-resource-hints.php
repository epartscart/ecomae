<?php
/**
 * Read-only VPS resource hints (no writes, no probes loop).
 * https://www.ecomae.com/epc-vps-resource-hints.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: private, max-age=120');

$docroot = __DIR__;
$hints = array(
	'ok' => true,
	'host' => $_SERVER['HTTP_HOST'] ?? '',
	'docroot' => $docroot,
	'php_version' => PHP_VERSION,
	'opcache' => array(
		'enabled' => function_exists('opcache_get_status'),
		'status' => null,
	),
	'memory_limit' => ini_get('memory_limit'),
	'max_execution_time' => ini_get('max_execution_time'),
	'failover' => array(),
	'load' => null,
);

if (function_exists('opcache_get_status')) {
	$st = @opcache_get_status(false);
	if (is_array($st)) {
		$hints['opcache']['status'] = array(
			'cached_scripts' => $st['opcache_statistics']['num_cached_scripts'] ?? null,
			'hit_rate' => isset($st['opcache_statistics']['opcache_hit_rate'])
				? round((float) $st['opcache_statistics']['opcache_hit_rate'], 2)
				: null,
			'memory_used_mb' => isset($st['memory_usage']['used_memory'])
				? round($st['memory_usage']['used_memory'] / 1048576, 1)
				: null,
		);
	}
}

if (is_readable($docroot . '/content/general_pages/epc_platform_failover.php')) {
	require_once $docroot . '/content/general_pages/epc_platform_failover.php';
	$hints['failover'] = array(
		'json_mirror_age_sec' => epc_failover_json_mirror_age_sec(),
		'cache_ttl_sec' => epc_failover_status_cache_ttl(),
		'mode_file' => epc_failover_read_mode_file(),
	);
}

if (function_exists('sys_getloadavg')) {
	$load = sys_getloadavg();
	if (is_array($load)) {
		$hints['load'] = array('1m' => $load[0], '5m' => $load[1], '15m' => $load[2]);
	}
}

echo json_encode($hints, JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
