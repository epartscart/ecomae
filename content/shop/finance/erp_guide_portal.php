<?php
/**
 * Frontend ERP step-by-step guide.
 * URL: /shop/erp/guide
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';

$user_id = (int)DP_User::getUserId();
$logged_in = $user_id > 0;
$has_access = false;
if ($logged_in && isset($db_link) && $db_link instanceof PDO) {
	$has_access = epc_erp_user_can_access($db_link);
}

$lang = epc_erp_lang_href();
$portal_home = ($lang !== '' ? $lang : '') . '/shop/erp';
?>
<style>
.epc-erp-portal-wrap { margin: 0 0 32px; }
.epc-erp-portal-wrap .hpanel { background: #fff; border: 1px solid #dce4ef; border-radius: 8px; margin-bottom: 18px; }
.epc-erp-portal-wrap .panel-heading { padding: 14px 16px; background: #f5f7fa; border-bottom: 1px solid #dce4ef; font-weight: 700; }
.epc-erp-portal-wrap .panel-body { padding: 16px; }
.epc-erp-login-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 22px; margin-bottom: 20px; }
</style>

<div class="epc-erp-portal-wrap">
<?php if (!$logged_in): ?>
	<div class="epc-erp-login-panel">
		<h2>ERP guide — sign in required</h2>
		<p>Please <a href="<?php echo htmlspecialchars($portal_home, ENT_QUOTES, 'UTF-8'); ?>">sign in to ERP Finance</a> first.</p>
		<?php
		$login_form_postfix = 'erp_guide';
		require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
		?>
	</div>
<?php elseif (!$has_access): ?>
	<div class="alert alert-warning"><strong>Access denied.</strong> ERP Finance team membership required.</div>
<?php else: ?>
	<?php
	$user_session = epc_erp_resolve_user_session();
	extract(epc_erp_configure_portal_urls('frontend'));
	$guide_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_guide.php';
	if (!is_file($guide_include)) {
		echo '<div class="alert alert-danger">ERP guide file not found.</div>';
	} else {
		include $guide_include;
	}
	?>
<?php endif; ?>
</div>
