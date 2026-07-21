<?php
/**
 * BOS black ERP-style top mega-menu regressions.
 *
 *   php tests/erp_advanced/run_bos_topnav_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
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

echo "== BOS black topnav ==\n";

$index = (string) file_get_contents($root . '/bos/index.php');
$css = (string) file_get_contents($root . '/bos/epc_bos_shell.css');
$js = (string) file_get_contents($root . '/bos/epc_bos_shell.js');
$unified = (string) file_get_contents($root . '/content/general_pages/epc_bos_unified.php');

check('index has bos-topnav', strpos($index, 'class="bos-topnav"') !== false || strpos($index, "class=\"bos-topnav\"") !== false);
check('index has topnav id', strpos($index, 'id="bosTopnav"') !== false);
check('index has flyout panels', strpos($index, 'bos-topnav__panel') !== false);
check('index has topnav toggle attrs', strpos($index, 'data-bos-topnav-toggle') !== false);
check('index does not render left sidebar aside', strpos($index, 'id="bosSidebar"') === false && strpos($index, 'class="bos-sidebar"') === false);
check('index body uses topnav class', strpos($index, 'bos-body--topnav') !== false);
check('index keeps tenant switcher', strpos($index, 'id="bosTenantSwitcher"') !== false);

check('css black topnav bg', strpos($css, '--bos-topnav-bg:     #000000') !== false || strpos($css, '--bos-topnav-bg:#000000') !== false || strpos($css, '--bos-topnav-bg:') !== false && strpos($css, '#000000') !== false);
check('css has .bos-topnav rules', strpos($css, '.bos-topnav {') !== false || strpos($css, '.bos-topnav{') !== false);
check('css hides legacy sidebar', strpos($css, '.bos-sidebar') !== false && strpos($css, 'display: none !important') !== false);
check('css main has no left rail margin', strpos($css, 'margin-left: 0 !important') !== false);

check('js binds topnav', strpos($js, 'bindBosTopnav') !== false || strpos($js, 'bosTopnav') !== false);
check('js places flyout panels', strpos($js, 'data-bos-topnav-panel') !== false);

check('version bumped to 1.5.0', strpos($unified, "define('EPC_BOS_VERSION', '1.5.0')") !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
