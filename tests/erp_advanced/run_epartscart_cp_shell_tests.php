<?php
/**
 * epartscart CP shell speed / structure regressions.
 *
 *   php tests/erp_advanced/run_epartscart_cp_shell_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = $root;

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

echo "== Inline CSS is opt-in (not always ~200KB) ==\n";
require_once $root . '/content/general_pages/epc_cp_professional_shell.php';
unset($_GET['epc_cp_inline_css']);
$inline = epc_cp_shell_inline_style_block();
check('default inline block empty', $inline === '');
$_GET['epc_cp_inline_css'] = '1';
$inlineForced = epc_cp_shell_inline_style_block();
unset($_GET['epc_cp_inline_css']);
$hasCssOnDisk = is_file($root . '/cp/templates/bootstrap_admin/css/epc_cp_professional.css');
check('forced inline returns style when CSS on disk', !$hasCssOnDisk || strpos($inlineForced, 'epc-cp-inline-css') !== false);
check('css version is non-empty cache bust', epc_cp_shell_css_version() !== '');

echo "\n== Login template is lean ==\n";
$login = (string) file_get_contents($root . '/cp/plugins/authentication/login_form/template.php');
check('login uses CDN font-awesome', strpos($login, 'cdnjs.cloudflare.com/ajax/libs/font-awesome') !== false);
check('login uses CDN bootstrap', strpos($login, 'cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap') !== false);
check('login does not load metisMenu script', strpos($login, 'metisMenu.min.js') === false && strpos($login, 'metisMenu.css') === false);
check('login does not load homer.js', strpos($login, 'homer.js') === false);
check('login particle count reduced', strpos($login, 'totalParticles = 28') !== false);
check('login respects reduced motion', strpos($login, 'prefers-reduced-motion') !== false);

echo "\n== Prices page fast path (epartscart 524) ==\n";
require_once $root . '/cp/content/shop/prices_upload/epc_prices_manager_perf.php';
$src = (string) file_get_contents($root . '/cp/content/shop/prices_upload/epc_prices_manager_perf.php');
check('cleaner only via explicit GET flag', strpos($src, "isset(\$_GET['epc_clean_pyprices'])") !== false);
check('cleaner no longer random on page load', strpos($src, 'mt_rand(1, 40)') === false);
check('listing query uses denormalized records_count', strpos($src, 'COALESCE(p.`records_count`') !== false);
check('listing query has resilient fallbacks', strpos($src, 'Last resort') !== false || strpos($src, 'candidates[]') !== false);
check('listing never COUNTs prices_data for records', strpos($src, 'COUNT(*) AS `records_count`') === false);
$fetchFn = '';
if (preg_match('/function epc_prices_fetch_lists_query\(PDO \$db_link\): PDOStatement\s*\{(.*)\n\}/s', $src, $m)) {
	$fetchFn = $m[1];
}
check('fetch_lists_query body omits prices_data', $fetchFn !== '' && strpos($fetchFn, 'shop_docpart_prices_data') === false);
check('inline history always deferred', epc_prices_defer_inline_update_history() === true);
check('ensure indexes defaults allowAlter=false', preg_match('/function epc_prices_ensure_listing_indexes\(PDO \$db, bool \$allowAlter = false\)/', $src) === 1);
$mgrSrc = (string) file_get_contents($root . '/cp/content/shop/prices_upload/prices_manager.php');
check('lazy history uses parallel pump', strpos($mgrSrc, 'maxParallel') !== false);
check('history cell skips is_file probes', strpos($mgrSrc, 'Avoid is_file()') !== false);
check('safe JS object push (no JSON.parse quote wrap)', preg_match("/JSON\\.parse\\(\\s*'\\s*<\\?php\\s+echo\\s+json_encode/", $mgrSrc) !== 1
	&& strpos($mgrSrc, 'docpart_prices.push(<?php echo $epc_price_js; ?>)') !== false);
check('dead pagination continue removed', strpos($mgrSrc, '$i < $s_page*$p') === false);
check('emex remove script present', is_file($root . '/epc-remove-emex-price.php'));

echo "\n== Script relocate also moves styles ==\n";
require_once $root . '/content/general_pages/epc_cp_script_relocate.php';
epc_cp_footer_scripts_reset();
$GLOBALS['epc_cp_footer_styles'] = array();
$prepared = epc_cp_prepare_cp_page_content(
	'<div class="row"><style>.x{color:red}</style><script>var a=1;</script><p>ok</p></div>'
);
check('style removed from main content', strpos($prepared, '<style') === false);
check('script removed from main content', strpos($prepared, '<script') === false);
check('paragraph kept', strpos($prepared, '<p>ok</p>') !== false);
check('style queued for footer', !empty($GLOBALS['epc_cp_footer_styles']));
check('script queued for footer', !empty($GLOBALS['epc_cp_footer_scripts']));

echo "\n== Left menu skips catalogue tree off catalogue pages ==\n";
$menuSrc = (string) file_get_contents($root . '/cp/modules/left_cp_menu/left_cp_menu.php');
check('catalogue helper loaded first', strpos($menuSrc, 'catalogue_menu_helper.php') !== false);
check('get_catalogue_tree only inside catalogue URL guard', preg_match(
	'/isset\(\s*\$module_modes_map\[.*?\]\s*\)[\s\S]{0,400}get_catalogue_tree\.php/',
	$menuSrc
) === 1);
check('no eager get_catalogue_tree before guard', !preg_match(
	'/get_catalogue_tree\.php[\s\S]{0,800}isset\(\s*\$module_modes_map/',
	$menuSrc
));

echo "\n== CP fix probe avoids CSS false positive ==\n";
$fixSrc = (string) file_get_contents($root . '/epc-epartscart-cp-fix.php');
check('probe looks for word-boundary alert-danger', strpos($fixSrc, '\balert-danger\b') !== false || strpos($fixSrc, '\\balert-danger\\b') !== false);
check('apply ensures listing indexes', strpos($fixSrc, 'epc_prices_ensure_listing_indexes') !== false);

echo "\n== /cp/control speed (menu ACL + shell) ==\n";
$helperSrc = (string) file_get_contents($root . '/cp/content/control/control_helper.php');
check('ACL preload helper present', strpos($helperSrc, 'function epc_cp_acl_preload') !== false);
check('is_anable caches by URL', strpos($helperSrc, 'epc_cp_acl_result_by_url') !== false);
check('menu preloads ACL before loop', strpos($menuSrc, 'epc_cp_acl_preload') !== false);

$controlSrc = (string) file_get_contents($root . '/cp/content/control/control.php');
$earlyReturnPos = strpos($controlSrc, "epc_tenant_cp_dashboard_shown'])) {\n\treturn;\n}");
if ($earlyReturnPos === false) {
	$earlyReturnPos = strpos($controlSrc, 'epc_tenant_cp_dashboard_shown');
}
$controlItemsPos = strpos($controlSrc, 'FROM `control_items`');
check(
	'tenant home returns before control_items loop',
	$earlyReturnPos !== false && $controlItemsPos !== false && $earlyReturnPos < $controlItemsPos
);

$desktopSrc = (string) file_get_contents($root . '/cp/templates/bootstrap_admin/desktop.php');
$gatePos = strpos($desktopSrc, 'if (!epc_cp_top_alerts_use_professional_header())');
$notInPos = strpos($desktopSrc, 'NOT IN(SELECT DISTINCT `product_id` FROM `shop_storages_data`)');
$gateEndPos = strpos($desktopSrc, '} // !epc_cp_top_alerts_use_professional_header()');
check('stock probes gated by professional header', $gatePos !== false);
check(
	'NOT IN stock probe inside gated block',
	$gatePos !== false && $notInPos !== false && $gateEndPos !== false
		&& $gatePos < $notInPos && $notInPos < $gateEndPos
);

$userSrc = (string) file_get_contents($root . '/content/users/dp_user.php');
check('getAdminProfile request-cached', preg_match(
	'/function getAdminProfile\(\)[\s\S]{0,120}static \$cached/',
	$userSrc
) === 1);
check('getAdminId request-cached', preg_match(
	'/function getAdminId\(\)[\s\S]{0,120}static \$cached/',
	$userSrc
) === 1);

$dashSrc = (string) file_get_contents($root . '/cp/content/control/epc_tenant_cp_dashboard.php');
check('tenant KPI stats cached 60s', strpos($dashSrc, 'epc_tcp_dash_stats:v1:') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
