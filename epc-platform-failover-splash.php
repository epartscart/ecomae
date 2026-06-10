<?php
/**
 * Failover splash router — serves splash when platform unhealthy or preview requested.
 * https://www.ecomae.com/epc-platform-failover-splash.php?epc_splash_preview=1&mode=backup_active
 */
declare(strict_types=1);

require_once __DIR__ . '/content/general_pages/epc_platform_failover.php';

$preview = epc_failover_splash_preview_requested();
$modeParam = trim((string) ($_GET['mode'] ?? ''));

if ($preview && $modeParam !== '' && in_array($modeParam, epc_failover_valid_modes(), true)) {
	$qs = http_build_query(array('epc_splash_preview' => '1', 'mode' => $modeParam));
	header('Location: /epc-platform-splash.html?' . $qs, true, 302);
	exit;
}

if ($preview) {
	header('Location: /epc-platform-splash.html?epc_splash_preview=1&mode=backup_active', true, 302);
	exit;
}

$status = epc_failover_current_status(!empty($_GET['probe']));
$mode = (string) ($status['mode'] ?? 'primary_ok');

if (!epc_failover_should_show_splash($mode)) {
	header('Location: /', true, 302);
	exit;
}

$static = __DIR__ . '/epc-platform-splash.html';
if (!is_readable($static)) {
	http_response_code(503);
	header('Content-Type: text/html; charset=utf-8');
	echo '<!DOCTYPE html><html><body><h1>Service temporarily unavailable</h1><p>Backup splash file missing.</p></body></html>';
	exit;
}

header('Content-Type: text/html; charset=utf-8');
header('Cache-Control: no-store');
readfile($static);
