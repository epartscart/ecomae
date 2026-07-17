<?php
/**
 * CP shell phase tracer (temporary diagnostic for the /cp/control 524 hang).
 *
 * Writes each phase marker to a log file IMMEDIATELY (not at shutdown), so when
 * a request hangs, the last line in the log is the phase that blocked — even if
 * the request is killed by Cloudflare/nginx before PHP finishes.
 *
 * Zero cost when disabled. Enabled only when either:
 *   - the request URL has ?epc_trace=1, or
 *   - a flag file exists: sys_get_temp_dir()/epc_cp_trace.on
 *
 * Read the log:  /epc-cp-trace.php?token=...&key=<tech_key>
 *
 * SAFE + REVERSIBLE: pure appends to a temp log; no behaviour change otherwise.
 */

if (!function_exists('epc_cp_trace_enabled')) {
	function epc_cp_trace_enabled(): bool
	{
		static $on = null;
		if ($on !== null) {
			return $on;
		}
		$on = false;
		if (isset($_GET['epc_trace'])) {
			$on = true;
		} elseif (@is_file(rtrim(sys_get_temp_dir(), '/') . '/epc_cp_trace.on')) {
			$on = true;
		}
		return $on;
	}
}

if (!function_exists('epc_cp_trace_logfile')) {
	function epc_cp_trace_logfile(): string
	{
		return rtrim(sys_get_temp_dir(), '/') . '/epc_cp_trace.log';
	}
}

if (!function_exists('epc_cp_trace')) {
	/**
	 * Record a phase marker. Appends immediately with elapsed ms since request start.
	 */
	function epc_cp_trace(string $label): void
	{
		if (!epc_cp_trace_enabled()) {
			return;
		}
		static $t0 = null;
		if ($t0 === null) {
			$t0 = isset($_SERVER['REQUEST_TIME_FLOAT']) ? (float) $_SERVER['REQUEST_TIME_FLOAT'] : microtime(true);
		}
		$elapsed = (int) round((microtime(true) - $t0) * 1000);
		$uri = (string) ($_SERVER['REQUEST_URI'] ?? '');
		$pid = function_exists('getmypid') ? (int) getmypid() : 0;
		$line = sprintf(
			"%s pid=%d +%dms %s %s\n",
			date('H:i:s'),
			$pid,
			$elapsed,
			$label,
			$uri
		);
		@file_put_contents(epc_cp_trace_logfile(), $line, FILE_APPEND | LOCK_EX);
	}
}
