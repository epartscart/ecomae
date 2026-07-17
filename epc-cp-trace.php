<?php
/**
 * Reader/controller for the CP shell phase tracer.
 *
 *   Read log:   /epc-cp-trace.php?token=epartscart-deploy-2026&key=<tech_key>
 *   Enable all: ...&on=1     (turns tracing on for ALL cp requests via flag file)
 *   Disable:    ...&off=1
 *   Clear log:  ...&clear=1
 *
 * With tracing on, open /cp/control in a browser (it will hang), then read this
 * — the LAST line for that pid is the phase that blocked.
 */
declare(strict_types=1);
header('Content-Type: text/plain; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit("Forbidden\n");
}

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
if ((string) ($_GET['key'] ?? '') !== (string) $cfg->tech_key) {
	http_response_code(403);
	exit("Invalid key\n");
}

$flag = rtrim(sys_get_temp_dir(), '/') . '/epc_cp_trace.on';
$log = rtrim(sys_get_temp_dir(), '/') . '/epc_cp_trace.log';

if (!empty($_GET['on'])) {
	@file_put_contents($flag, "1");
	echo "Tracing ENABLED for all /cp requests (flag: $flag).\n";
	echo "Now open https://www.epartscart.com/cp/control in a browser, then re-read this without &on=1.\n";
	exit;
}
if (!empty($_GET['off'])) {
	@unlink($flag);
	echo "Tracing DISABLED.\n";
	exit;
}
if (!empty($_GET['clear'])) {
	@unlink($log);
	echo "Log cleared.\n";
	exit;
}

echo "Trace flag: " . (is_file($flag) ? "ON" : "off") . "\n";
echo "Log file:   $log\n";
echo str_repeat('-', 60) . "\n";
if (is_file($log)) {
	$lines = @file($log, FILE_IGNORE_NEW_LINES) ?: array();
	$tail = array_slice($lines, -200);
	echo implode("\n", $tail) . "\n";
	echo str_repeat('-', 60) . "\n";
	echo count($lines) . " total lines. Showing last " . count($tail) . ".\n";
} else {
	echo "No trace log yet. Enable with &on=1, then hit /cp/control.\n";
}
