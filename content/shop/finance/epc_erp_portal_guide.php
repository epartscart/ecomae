<?php
/**
 * Standalone ERP guide body.
 */
defined('_ASTEXE_') or die('No access');

if (!isset($multilang_params) || !is_array($multilang_params)) {
	if (function_exists('multilang_init')) {
		$multilang_params = multilang_init();
	} else {
		$langHref = function_exists('epc_erp_lang_href') ? epc_erp_lang_href() : '';
		$multilang_params = array('lang_href' => $langHref, 'lang' => 'en', 'multilang' => false);
	}
}

global $DP_Config;

$user_id = (int) DP_User::getUserId();
$logged_in = $user_id > 0;
$has_access = false;
if ($logged_in && isset($db_link) && $db_link instanceof PDO) {
	$has_access = epc_erp_user_can_access($db_link);
}

$lang = epc_erp_lang_href();
$portal_home = epc_erp_portal_canonical_base($lang);
?>
<div class="epc-erp-portal-wrap">
<?php if (!$logged_in): ?>
	<div class="epc-erp-login-panel epc-erp-login-panel--standalone">
		<h2>ERP guide — sign in required</h2>
		<p>Please <a href="<?php echo htmlspecialchars($portal_home, ENT_QUOTES, 'UTF-8'); ?>">sign in to ERP Finance</a> first.</p>
		<?php
		$login_form_postfix = 'erp_guide';
		require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
		?>
	</div>
<?php elseif (!$has_access): ?>
	<div class="alert alert-warning"><strong>Access denied.</strong> ERP team membership required.</div>
<?php else: ?>
	<?php
	$user_session = epc_erp_resolve_user_session();
	$epc_erp_portal = 'frontend';
	extract(epc_erp_configure_portal_urls('frontend'));
	$guide_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_guide.php';
	$full_guide_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_full_guide.php';
	if (!is_file($guide_include)) {
		echo '<div class="alert alert-danger">ERP guide file not found.</div>';
	} else {
		echo '<div class="epc-erp-workspace">';
		include $guide_include;
		if (is_file($full_guide_include)) {
			include $full_guide_include;
		}
		echo '</div>';
	}
	?>
<?php endif; ?>
</div>
