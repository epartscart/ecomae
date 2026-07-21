<?php
/**
 * Super CP / BOC top mega-menu (ERP-style) regressions.
 *
 *   php tests/erp_advanced/run_boc_topnav_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_boc_kernel.php';
require_once $root . '/content/general_pages/epc_boc_console.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $cond): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== BOC topnav helpers ==\n";
check('epc_boc_render_top_nav exists', function_exists('epc_boc_render_top_nav'));
check('epc_boc_console_open exists', function_exists('epc_boc_console_open'));

$nav = epc_boc_nav();
check('boc nav has groups', is_array($nav) && count($nav) > 0);

ob_start();
epc_boc_render_top_nav($nav, '/cp', 'command_center', epc_boc_brand());
$html = (string) ob_get_clean();
check('renders topnav root', strpos($html, 'id="epc_boc_topnav"') !== false);
check('renders topnav class', strpos($html, 'epc-boc__topnav') !== false);
check('no left rail in topnav render', strpos($html, 'epc-boc__rail') === false);
check('has mega panel', strpos($html, 'epc-boc__topnav-panel') !== false);
check('marks command center active', strpos($html, 'is-active') !== false);
check('includes toggle script', strpos($html, 'data-boc-topnav-toggle') !== false);

ob_start();
epc_boc_console_open(array(
	'active' => 'command_center',
	'title' => 'Command Center',
	'base' => '/cp',
	'operator' => 'Tester',
	'layout' => 'top',
));
$open = (string) ob_get_clean();
epc_boc_console_close();
check('open uses topnav layout class', strpos($open, 'epc-boc--topnav') !== false);
check('open emits topnav', strpos($open, 'id="epc_boc_topnav"') !== false);
check('open does not emit left rail', strpos($open, 'class="epc-boc__rail"') === false && strpos($open, '<aside class="epc-boc__rail') === false);

$css = epc_boc_console_css();
check('css has topnav styles', strpos($css, '.epc-boc__topnav') !== false);
check('css hides rail in topnav mode', strpos($css, '.epc-boc--topnav .epc-boc__rail{display:none') !== false);
check('css hides legacy left_cp_menu', strpos($css, '.left_cp_menu') !== false);

$dash = (string) file_get_contents($root . '/cp/content/control/epc_super_cp_dashboard.php');
check('super dashboard requests layout=top', strpos($dash, "'layout'   => 'top'") !== false || strpos($dash, "'layout' => 'top'") !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
