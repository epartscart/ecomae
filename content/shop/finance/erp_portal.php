<?php
/**
 * Frontend ERP portal — separate login for finance/ERP team (no CP access required).
 * URL: /shop/erp (registered via epc-erp-frontend-setup.php)
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_access.php';

global $DP_Config;

$user_id = (int)DP_User::getUserId();
$logged_in = $user_id > 0;
$has_access = false;
if ($logged_in && isset($db_link) && $db_link instanceof PDO) {
	$has_access = epc_erp_user_can_access($db_link);
}

$fe = epc_erp_frontend_urls();
$lang = epc_erp_lang_href();
$portal_home = ($lang !== '' ? $lang : '') . '/shop/erp';
$portal_guide = ($lang !== '' ? $lang : '') . '/shop/erp/guide';
?>
<style>
.epc-erp-portal-wrap { margin: 0 0 32px; }
.epc-erp-portal-wrap .hpanel { background: #fff; border: 1px solid #dce4ef; border-radius: 8px; margin-bottom: 18px; }
.epc-erp-portal-wrap .panel-heading { padding: 14px 16px; background: #f5f7fa; border-bottom: 1px solid #dce4ef; font-weight: 700; border-radius: 8px 8px 0 0; }
.epc-erp-portal-wrap .panel-body { padding: 16px; }
.epc-erp-login-panel { background: #fff; border: 1px solid #e1e7ef; border-radius: 10px; padding: 22px; margin-bottom: 20px; }
.epc-erp-login-panel h2 { margin: 0 0 8px; font-size: 22px; color: #172536; }
.epc-erp-login-lead { color: #64748b; line-height: 1.55; margin-bottom: 14px; }
</style>

<div class="epc-erp-portal-wrap">
<?php if (!$logged_in): ?>
	<div class="epc-erp-login-panel">
		<h2><i class="fa fa-lock"></i> ERP Finance — sign in</h2>
		<p class="epc-erp-login-lead">
			Sign in with your <strong>department ERP account</strong> (Sales, Logistics, Finance, Purchase, Accounts, Marketing, HR, Admin).
			You do <strong>not</strong> need control panel access — each role sees only the tabs for their workflow.
		</p>
		<div class="panel panel-primary">
		<?php
		$login_form_postfix = 'erp_finance';
		require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
		?>
		</div>
	</div>
<?php elseif (!$has_access): ?>
	<div class="alert alert-warning">
		<strong>Access denied.</strong> Your account is signed in but does not have ERP Finance access.
		Site administrators and CP backend staff can open ERP here automatically; finance-only staff need the ERP team group
		(<code>epc-erp-frontend-setup.php?token=...&amp;email=YOUR@EMAIL</code>).
		You can also use the control panel:
		<a href="/<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/finance/erp">CP ERP Finance</a>.
	</div>
<?php else: ?>
	<?php
	$user_session = epc_erp_resolve_user_session();
	extract(epc_erp_configure_portal_urls('frontend'));
	$erp_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_main.php';
	if (!is_file($erp_include)) {
		echo '<div class="alert alert-danger">ERP module files not found on server.</div>';
	} else {
		include $erp_include;
	}
	?>
<?php endif; ?>
</div>
<script>
(function(){
	function epcErpHidePreloader(){
		var pre = document.getElementById('preloader');
		var stat = document.getElementById('status');
		if (pre) { pre.style.display = 'none'; }
		if (stat) { stat.style.display = 'none'; }
		if (document.body) { document.body.style.overflow = 'visible'; }
	}
	epcErpHidePreloader();
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', epcErpHidePreloader);
	}
	setTimeout(epcErpHidePreloader, 200);
})();
</script>
