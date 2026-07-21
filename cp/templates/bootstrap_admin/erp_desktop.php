<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

$user_session = DP_User::getAdminSession();
$user_label = '';
if (!empty($user_session['user_id'])) {
	$user_label = !empty($user_session['email']) ? (string) $user_session['email'] : ('User #' . (int) $user_session['user_id']);
}
$brand = epc_brand_cp_context();
$backend = htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8');
$erpHome = htmlspecialchars(epc_erp_cp_shell_launcher_url(), ENT_QUOTES, 'UTF-8');
$portalUrl = htmlspecialchars(epc_erp_cp_shell_portal_url(), ENT_QUOTES, 'UTF-8');
$ecomCpUrl = htmlspecialchars(epc_erp_cp_shell_ecom_url(), ENT_QUOTES, 'UTF-8');
$guideUrl = epc_erp_cp_shell_url_with_subpath(epc_erp_cp_shell_launcher_url(), 'guide');
$logoutUrl = function_exists('epc_cp_logout_redirect_url')
	? epc_cp_logout_redirect_url() . (strpos(epc_cp_logout_redirect_url(), '?') !== false ? '&' : '?') . 'logout=1'
	: '/' . $backend . '/?logout=1';
$showStore = function_exists('epc_portal_storefront_enabled') && epc_portal_storefront_enabled();
$platformHost = function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_platform_erp_router.php';
$isPlatformErp = function_exists('epc_platform_erp_is_active') && epc_platform_erp_is_active();
$isPlatformOperator = $platformHost && function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
$showTenants = $isPlatformOperator && !$isPlatformErp;
$isSharedErp = !empty($brand['is_shared_erp_session']);
$roleLabel = '';
if ($isPlatformErp) {
	$roleLabel = 'Platform ERP';
} elseif ($isPlatformOperator) {
	$roleLabel = 'Super CP Operator';
} elseif ($isSharedErp) {
	$roleLabel = trim((string) ($brand['company_name'] ?? '')) !== ''
		? trim((string) $brand['company_name']) . ' ERP'
		: 'Client ERP';
}
$tenantHubUrl = '/' . $backend . '/shop/tenant_hub/tenant_hub';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_professional_shell.php';
$cssVer = epc_cp_shell_css_version();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_topbar_brand.php';
$epcErpTopBrand = epc_erp_topbar_brand_context();
$headerTitle = (string) ($epcErpTopBrand['title'] ?? 'ERP Suite');
$hubTagline = (string) ($epcErpTopBrand['tagline'] ?? 'Finance & operations');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_translate.php';
?>
<!DOCTYPE html>
<html lang="<?php echo isset($multilang_params['lang']) ? htmlspecialchars($multilang_params['lang'], ENT_QUOTES, 'UTF-8') : 'en'; ?>">
<head>
	<meta charset="utf-8">
	<meta name="viewport" content="width=device-width, initial-scale=1">
	<title><?php echo htmlspecialchars((string) ($DP_Content->value ?? 'ERP Suite'), ENT_QUOTES, 'UTF-8'); ?> — <?php echo htmlspecialchars((string) ($brand['product_name'] ?? 'ERP'), ENT_QUOTES, 'UTF-8'); ?></title>
<?php
	// Favicon follows ERP top-bar brand (tenant logo or ECOM AE).
	if (!empty($epcErpTopBrand['logo_url'])) {
		$epcErpFaviconUrl = (string) $epcErpTopBrand['logo_url'];
		$epcFavType = (substr($epcErpFaviconUrl, -4) === '.svg' || strpos($epcErpFaviconUrl, 'logo_svg') !== false)
			? 'image/svg+xml' : 'image/png';
		echo "\t<link rel=\"icon\" type=\"" . htmlspecialchars($epcFavType, ENT_QUOTES, 'UTF-8') . "\" href=\"" . htmlspecialchars($epcErpFaviconUrl, ENT_QUOTES, 'UTF-8') . "\" />\n";
		echo "\t<link rel=\"apple-touch-icon\" href=\"" . htmlspecialchars($epcErpFaviconUrl, ENT_QUOTES, 'UTF-8') . "\" />\n";
	} elseif ($platformHost || $isPlatformErp || $isPlatformOperator) {
		$epcErpFaviconUrl = '/content/general_pages/epc_ecomae_logo_svg.php';
		echo "\t<link rel=\"icon\" type=\"image/svg+xml\" href=\"" . htmlspecialchars($epcErpFaviconUrl, ENT_QUOTES, 'UTF-8') . "\" />\n";
		echo "\t<link rel=\"apple-touch-icon\" href=\"" . htmlspecialchars($epcErpFaviconUrl, ENT_QUOTES, 'UTF-8') . "\" />\n";
	}
?>
	<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css" />
	<link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/css/bootstrap.min.css" />
	<link rel="stylesheet" href="<?php echo htmlspecialchars(epc_erp_shell_asset_href('/' . $backend . '/templates/bootstrap_admin/css/epc_cp_ui.css', '/content/general_pages/epc_cp_ui_css.php'), ENT_QUOTES, 'UTF-8'); ?>" />
	<link rel="stylesheet" href="<?php echo htmlspecialchars(epc_erp_shell_asset_href('/content/shop/finance/epc_erp_portal.css', '/content/shop/finance/epc_erp_portal_css.php'), ENT_QUOTES, 'UTF-8'); ?>" />
	<link rel="stylesheet" href="<?php echo htmlspecialchars(epc_erp_shell_asset_href('/content/shop/finance/epc_erp_ui.css', '/content/shop/finance/epc_erp_ui_css.php'), ENT_QUOTES, 'UTF-8'); ?>" />
	<link rel="stylesheet" href="<?php echo htmlspecialchars(epc_erp_shell_asset_href('/content/shop/finance/epc_erp_professional.css', '/content/shop/finance/epc_erp_professional_css.php'), ENT_QUOTES, 'UTF-8'); ?>" />
	<link rel="stylesheet" href="<?php echo htmlspecialchars(epc_erp_shell_asset_href('/' . $backend . '/templates/bootstrap_admin/css/epc_cp_professional.css', '/content/general_pages/epc_cp_professional_css.php'), ENT_QUOTES, 'UTF-8'); ?>" />
	<?php
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_animated_epartscart_logo.php';
	if (epc_animated_epartscart_logo_applies()) {
		epc_animated_epartscart_logo_enqueue();
	}
	$epcCpPageAssetsHead = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
	if (isset($DP_Content) && is_object($DP_Content)) {
		$epcContentUrlHead = trim((string) ($DP_Content->url ?? ''), '/');
		if ($epcContentUrlHead !== '' && is_file($epcCpPageAssetsHead)) {
			require_once $epcCpPageAssetsHead;
			if (function_exists('epc_cp_page_head_assets')) {
				epc_cp_page_head_assets($epcContentUrlHead);
			}
		}
	}
	?>
	<?php echo epc_erp_sidebar_early_init_script(); ?>
	<?php echo epc_erp_shell_nav_script_tag(); ?>
	<?php if (function_exists('epc_erp_voice_command_js_script_tag')) { echo epc_erp_voice_command_js_script_tag(); } ?>
	<?php if (function_exists('epc_erp_ai_assistant_js_script_tag')) { echo epc_erp_ai_assistant_js_script_tag(); } ?>
	<?php if (function_exists('epc_erp_seed_form_js_script_tag')) { echo epc_erp_seed_form_js_script_tag(); } ?>
	<?php if (($epcErpTopBrand['mode'] ?? '') === 'ecomae') { epc_ecomae_hub_logo_enqueue(); } ?>
	<docpart type="head" name="head" />
<?php
	// PWA: make the ERP shell installable on Android + iOS.
	$epcErpPwa = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_pwa.php';
	if (is_file($epcErpPwa)) {
		require_once $epcErpPwa;
		if (function_exists('epc_pwa_head_tags')) {
			echo "\n" . epc_pwa_head_tags('/cp/manifest.webmanifest', '/cp/sw.js', '#4f46e5') . "\n";
		}
		echo '<link rel="apple-touch-icon" href="/cp/assets/app/icon-192.svg">' . "\n";
	}
?>
</head>
<body class="epc-erp-standalone epc-erp-cp-shell epc-cp-shell <?php echo htmlspecialchars(epc_cp_shell_body_classes(), ENT_QUOTES, 'UTF-8'); ?>">
<header class="epc-erp-topbar epc-erp-topbar--cp">
	<div class="epc-erp-topbar__inner">
		<a class="epc-erp-topbar__brand epc-erp-topbar__brand--hub" href="<?php echo $erpHome; ?>" aria-label="<?php echo htmlspecialchars((string) ($epcErpTopBrand['aria'] ?? $headerTitle), ENT_QUOTES, 'UTF-8'); ?>">
			<?php echo epc_erp_topbar_brand_markup(); ?>
		</a>
		<nav class="epc-erp-topbar__nav epc-erp-topbar__nav--compact">
			<a href="<?php echo htmlspecialchars($guideUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-book"></i> Guide</a>
			<?php if ($showTenants): ?>
			<a href="<?php echo htmlspecialchars($tenantHubUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-cloud"></i> Tenants</a>
			<?php endif; ?>
			<?php if ($showStore && !$isSharedErp && !$isPlatformErp): ?>
			<a href="<?php echo $ecomCpUrl; ?>" class="epc-erp-topbar__muted"><i class="fa fa-shopping-cart"></i> E-commerce CP</a>
			<?php endif; ?>
		</nav>
		<div class="epc-erp-topbar__user">
			<?php if ($roleLabel !== ''): ?>
			<span class="epc-erp-topbar__user-label epc-erp-topbar__role"><i class="fa fa-id-badge"></i> <?php echo htmlspecialchars($roleLabel, ENT_QUOTES, 'UTF-8'); ?></span>
			<?php endif; ?>
			<?php if ($user_label !== ''): ?>
			<span class="epc-erp-topbar__user-label"><i class="fa fa-user"></i> <?php echo htmlspecialchars($user_label, ENT_QUOTES, 'UTF-8'); ?></span>
			<?php endif; ?>
			<?php echo epc_cp_translate_render('erp'); ?>
			<a class="btn btn-default btn-xs" href="<?php echo htmlspecialchars($logoutUrl, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-sign-out"></i> Logout</a>
		</div>
	</div>
</header>
<main class="epc-erp-main epc-erp-main--cp">
	<!--epc-cp-main-begin-->
	<docpart type="main" name="main" />
	<!--epc-cp-main-end-->
</main>
<footer class="epc-erp-foot">
	<?php echo epc_brand_hosted_by_html(); ?>
</footer>
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/js/bootstrap.min.js"></script>
<?php
$epcCpPageAssets = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
$epcCpScriptRelocateFooter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_script_relocate.php';
if (is_file($epcCpScriptRelocateFooter)) {
	require_once $epcCpScriptRelocateFooter;
	if (function_exists('epc_cp_render_relocated_footer_scripts')) {
		epc_cp_render_relocated_footer_scripts();
	}
}
if (isset($DP_Content) && is_object($DP_Content)) {
	$contentUrl = trim((string) ($DP_Content->url ?? ''), '/');
	if ($contentUrl !== '' && is_file($epcCpPageAssets)) {
		require_once $epcCpPageAssets;
		if (function_exists('epc_cp_page_footer_scripts')) {
			epc_cp_page_footer_scripts($contentUrl);
		}
	}
}
echo epc_cp_sidebar_collapse_script();
?>
</body>
</html>
