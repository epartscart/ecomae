<?php
/**
 * Standalone ERP portal body (login or ERP workspace).
 */
defined('_ASTEXE_') or die('No access');

if (!isset($multilang_params) || !is_array($multilang_params)) {
	if (function_exists('multilang_init')) {
		$multilang_params = multilang_init();
	} else {
		$langHref = function_exists('epc_erp_lang_href') ? epc_erp_lang_href() : '';
		$multilang_params = array(
			'lang_href' => $langHref,
			'lang' => 'en',
			'multilang' => false,
		);
	}
}

$user_id = (int) DP_User::getUserId();
$logged_in = $user_id > 0;
$has_access = false;
if ($logged_in && isset($db_link) && $db_link instanceof PDO) {
	$has_access = epc_erp_user_can_access($db_link);
}

$fe = epc_erp_frontend_urls();
$lang = epc_erp_lang_href();
$portal_home = epc_erp_portal_canonical_base($lang);
if (!function_exists('epc_erp_is_erp_only_context')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_vouchers.php';
}
$epc_erp_is_erp_only = function_exists('epc_erp_is_erp_only_context') && epc_erp_is_erp_only_context();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
?>
<div class="epc-erp-portal-wrap">
<?php if (!$logged_in): ?>
	<!-- BOS-style animated background -->
	<div class="epc-erp-portal-bg" id="erpPortalBg">
		<div class="epc-erp-portal-bg__grid"></div>
		<div class="epc-erp-portal-bg__particles" id="erpPortalParticles"></div>
		<div class="epc-erp-portal-bg__glow epc-erp-portal-bg__glow--1"></div>
		<div class="epc-erp-portal-bg__glow epc-erp-portal-bg__glow--2"></div>
		<div class="epc-erp-portal-bg__glow epc-erp-portal-bg__glow--3"></div>
	</div>
	<?php
	$epc_erp_show_platform_home = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
	if ($epc_erp_show_platform_home) {
		require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_portal_home.php';
	}
	?>
	<?php if (!empty($_GET['auth_failed'])): ?>
	<script>document.addEventListener('DOMContentLoaded', function () { alert(<?php echo json_encode(translate_str_by_id(4787)); ?>); });</script>
	<?php endif; ?>
	<div id="sign-in" class="epc-erp-login-panel epc-erp-login-panel--standalone epc-erp-login-panel--split">
		<div class="row">
			<div class="col-md-5 epc-erp-login-panel__brand">
				<?php echo epc_ecomae_static_logo('login', array('show_title' => true, 'show_tagline' => true, 'aria_label' => 'ECOM AE')); ?>
				<h1><i class="fa fa-line-chart"></i> ERP Finance</h1>
				<p class="epc-erp-login-lead">
					Department sign-in for finance, sales, logistics, purchase, HR, and operations.
					No storefront account required — this area is separate from the public website.
				</p>
				<ul class="epc-erp-login-features">
					<li><i class="fa fa-check"></i> Role-based tabs per department</li>
					<li><i class="fa fa-check"></i> GL, VAT, inventory &amp; payables</li>
					<li><i class="fa fa-check"></i> Same data as control panel ERP</li>
				</ul>
			</div>
			<div class="col-md-7">
				<div class="hpanel">
					<div class="panel-heading">Sign in</div>
					<div class="panel-body">
						<?php
						$login_form_postfix = 'erp_finance';
						require $_SERVER['DOCUMENT_ROOT'] . '/modules/login/login_form_general.php';
						?>
					</div>
				</div>
				<?php if (!$epc_erp_is_erp_only): ?>
				<p class="text-muted text-center epc-erp-login-footnote" style="margin-top:12px;font-size:12px;">
					Administrators:
					<a href="/<?php echo htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/finance/erp?epc_erp_shell=1">Control panel (advanced)</a>
					<?php if (function_exists('epc_portal_storefront_enabled') && epc_portal_storefront_enabled()): ?>
					· <a href="/">E-commerce website</a>
					<?php endif; ?>
				</p>
				<?php endif; ?>
			</div>
		</div>
	</div>
<?php elseif (!$has_access): ?>
	<div class="alert alert-warning">
		<strong>Access denied.</strong> Your account is signed in but is not in an ERP department or finance team group.
		Contact your administrator<?php if (!$epc_erp_is_erp_only): ?> or open
		<a href="/<?php echo htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/finance/erp">CP ERP</a><?php endif; ?>.
	</div>
<?php else: ?>
	<?php
	$user_session = epc_erp_resolve_user_session();
	$epc_erp_portal = 'frontend';
	extract(epc_erp_configure_portal_urls('frontend'));
	$erp_include = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/finance/erp/erp_main.php';
	if (!is_file($erp_include)) {
		echo '<div class="alert alert-danger">ERP module files not found on server.</div>';
	} else {
		echo '<div class="epc-erp-workspace">';
		include $erp_include;
		echo '</div>';
	}
	?>
<?php endif; ?>
</div>
