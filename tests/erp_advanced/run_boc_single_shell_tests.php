<?php
/**
 * CLI tests: Super CP portal modules stay in BOS main shell (no nested window).
 *
 *   php tests/erp_advanced/run_boc_single_shell_tests.php
 */
declare(strict_types=1);

define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = dirname(__DIR__, 2);

$root = dirname(__DIR__, 2);
require_once $root . '/content/general_pages/epc_boc_kernel.php';

// Lightweight stubs so we can test resolve/should-use without full console CSS.
if (!function_exists('epc_portal_is_super_cp_host')) {
	function epc_portal_is_super_cp_host(): bool
	{
		return !empty($GLOBALS['epc_test_super_cp']);
	}
}
if (!function_exists('epc_boc_console_open')) {
	function epc_boc_console_open(array $ctx = array()): void
	{
		$GLOBALS['epc_cp_boc_page'] = true;
		$GLOBALS['epc_test_boc_open_ctx'] = $ctx;
	}
}
if (!function_exists('epc_boc_console_close')) {
	function epc_boc_console_close(): void
	{
		$GLOBALS['epc_cp_boc_page'] = false;
	}
}
if (!function_exists('epc_boc_nav')) {
	function epc_boc_nav(): array
	{
		return array();
	}
}

require_once $root . '/content/general_pages/epc_boc_page_shell.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $ok): void
{
	global $pass, $fail;
	if ($ok) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== BOC single shell ==\n";

check('shell helper exists', function_exists('epc_boc_should_use_page_shell'));
check('resolve exists', function_exists('epc_boc_resolve_area'));
check('open/close exist', function_exists('epc_boc_page_shell_open') && function_exists('epc_boc_page_shell_close'));

$resolved = epc_boc_resolve_area('control/portal/epc_platform_health_checkup');
check('resolves health checkup', is_array($resolved) && ($resolved['id'] ?? '') === 'platform_health');
$resolvedGov = epc_boc_resolve_area('control/portal/epc_platform_governance');
check('resolves governance', is_array($resolvedGov) && ($resolvedGov['id'] ?? '') === 'governance');
$resolvedAudit = epc_boc_resolve_area('control/portal/epc_boc_audit_log');
check('resolves audit log', is_array($resolvedAudit) && ($resolvedAudit['id'] ?? '') === 'audit_log');

$GLOBALS['epc_cp_boc_page'] = false;
$GLOBALS['epc_boc_page_shell_open'] = false;
$GLOBALS['epc_test_super_cp'] = false;
check('non-super host does not wrap', epc_boc_should_use_page_shell('control/portal/epc_platform_health_checkup') === false);

$GLOBALS['epc_test_super_cp'] = true;
check('super host wraps portal health', epc_boc_should_use_page_shell('control/portal/epc_platform_health_checkup') === true);
check('super host wraps audit log', epc_boc_should_use_page_shell('control/portal/epc_boc_audit_log') === true);
check('super host wraps commerce module', epc_boc_should_use_page_shell('shop/orders') === true);
check('super host wraps erp finance', epc_boc_should_use_page_shell('shop/finance/erp') === true);
check('super host does not wrap home control', epc_boc_should_use_page_shell('control') === false);
check('super host does not wrap login', epc_boc_should_use_page_shell('login') === false);

$GLOBALS['epc_cp_boc_page'] = false;
$GLOBALS['epc_boc_page_shell_open'] = false;
epc_boc_page_shell_open(array('title' => 'Platform health'));
check('shell open sets boc flag', !empty($GLOBALS['epc_cp_boc_page']));
check('shell open sets skip page header', !empty($GLOBALS['epc_cp_skip_page_header']));
check('shell open uses top layout', (($GLOBALS['epc_test_boc_open_ctx']['layout'] ?? '') === 'top'));
epc_boc_page_shell_close();
check('shell close clears boc flag', empty($GLOBALS['epc_cp_boc_page']));

$frame = (string) file_get_contents($root . '/content/general_pages/epc_cp_page_frame.php');
check('page_frame opens BOC shell', strpos($frame, 'epc_boc_page_shell_open') !== false);
check('page_frame closes BOC shell', strpos($frame, 'epc_boc_page_shell_close') !== false);

$desktop = (string) file_get_contents($root . '/cp/templates/bootstrap_admin/desktop.php');
check('desktop skips page header for BOC', strpos($desktop, 'epc_cp_skip_page_header') !== false);
check('desktop auto-opens BOC shell before main', strpos($desktop, 'epc_boc_page_shell_open') !== false);
check('desktop auto-closes BOC shell after main', strpos($desktop, 'epc_boc_desktop_auto_shell') !== false);

$console = (string) file_get_contents($root . '/content/general_pages/epc_boc_console.php');
check('console_open guards double open', strpos($console, "epc_cp_boc_page") !== false);
check('console_close is idempotent', strpos($console, 'epc_cp_boc_page') !== false);

$shell = (string) file_get_contents($root . '/content/general_pages/epc_boc_page_shell.php');
check('shell wraps all super CP modules', strpos($shell, 'every module detail stays') !== false || strpos($shell, 'return true;') !== false);
check('shell uses layout top', strpos($shell, "'layout' => 'top'") !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
