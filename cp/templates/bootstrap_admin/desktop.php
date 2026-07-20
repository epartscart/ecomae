<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_theme_templates.php';
$epc_cp_industry_code = epc_portal_cp_active_industry();
$epc_cp_style_template = epc_portal_normalize_theme_template(
	$epc_cp_industry_code,
	(string) (epc_portal_load_site_settings()['theme_template'] ?? 'classic')
);
$epcApaiPage = !empty($GLOBALS['epc_cp_apai_page']);
if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: shell start'); }
?>
<!DOCTYPE html>
<html lang="<?php echo $multilang_params['lang']; ?>" data-theme="default" data-epc-industry="<?php echo htmlspecialchars($epc_cp_industry_code, ENT_QUOTES, 'UTF-8'); ?>" data-epc-style="<?php echo htmlspecialchars($epc_cp_style_template, ENT_QUOTES, 'UTF-8'); ?>">
<head>
<?php
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_professional_shell.php';
	echo epc_cp_sidebar_first_paint_script();
?>
	<base href="/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/">
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
	<meta http-equiv="Cache-Control" content="no-cache, no-store, must-revalidate" />
	<meta http-equiv="Pragma" content="no-cache" />
	<meta http-equiv="Expires" content="0" />
	<!-- <meta http-equiv="Content-Security-Policy" content="img-src 'self' data: blob:; default-src 'self' *.googleapis.com *.gstatic.com 'unsafe-inline' 'unsafe-eval';"> -->
	
	<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery/dist/jquery.min.js"></script>
	<script src="/lib/jquery_browser/jquery.browser.js"></script>
	<script src="/lib/jquery_form/jquery.form.js"></script>
	
	<link rel="stylesheet" href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/css/modal_window.css" />
    <docpart type="head" name="head" />
<?php
	// PWA: make the Control Panel installable on Android + iOS (Add to Home Screen).
	$epcCpPwa = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_pwa.php';
	if (is_file($epcCpPwa)) {
		require_once $epcCpPwa;
		if (function_exists('epc_pwa_head_tags')) {
			echo "\n" . epc_pwa_head_tags('/cp/manifest.webmanifest', '/cp/sw.js', '#4f46e5') . "\n";
		}
		echo '<link rel="apple-touch-icon" href="/cp/assets/app/icon-192.svg">' . "\n";
	}
?>
	
	
    <!-- Place favicon.ico and apple-touch-icon.png in the root directory -->
    <!--<link rel="shortcut icon" type="image/ico" href="favicon.ico" />-->
<?php
    // Operator console (super-CP host) is the ECOM AE platform, not a tenant
    // storefront — show the ECOM AE brand mark as the tab icon. Tenant CPs keep
    // their own favicon (the root /favicon.ico).
    if (function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
        $epcBosFaviconUrl = '/content/general_pages/epc_ecomae_logo_svg.php';
        echo "\t<link rel=\"icon\" type=\"image/svg+xml\" href=\"" . htmlspecialchars($epcBosFaviconUrl, ENT_QUOTES, 'UTF-8') . "\" />\n";
        echo "\t<link rel=\"apple-touch-icon\" href=\"" . htmlspecialchars($epcBosFaviconUrl, ENT_QUOTES, 'UTF-8') . "\" />\n";
    }
?>

    <!-- Vendor styles -->
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fontawesome/css/font-awesome.css" />
    <?php if (!$epcApaiPage) { ?>
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/metisMenu/dist/metisMenu.css" />
    <?php } ?>
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/animate.css/animate.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/css/bootstrap.css" />
	<link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fooTable/css/footable.core.min.css" />


    <!-- App styles -->
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/fonts/pe-icon-7-stroke/css/pe-icon-7-stroke.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/fonts/pe-icon-7-stroke/css/helper.css" />
	<link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css">
	<link rel="preconnect" href="https://fonts.googleapis.com">
	<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<?php
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_professional_shell.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_top_alerts.php';
	$GLOBALS['epc_cp_shell'] = true;
	echo epc_cp_sidebar_early_init_script();
	echo epc_cp_menu_sections_early_style();
	echo epc_cp_sidebar_collapse_script();
	echo epc_cp_menu_sections_script();
	epc_cp_shell_enqueue_assets(false);
	echo epc_cp_shell_inline_style_block();
	$epcCpHomerVer = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260606portals1';
	echo epc_cp_nuclear_critical_css();
	if (isset($DP_Content) && is_object($DP_Content)) {
		$epcContentUrlHead = trim((string) ($DP_Content->url ?? ''), '/');
		if ($epcContentUrlHead !== '') {
			$epcCpPageAssetsHead = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
			if (is_file($epcCpPageAssetsHead)) {
				require_once $epcCpPageAssetsHead;
				if (function_exists('epc_cp_page_head_assets')) {
					epc_cp_page_head_assets($epcContentUrlHead);
				}
			}
		}
	}
?>
	<style><?php echo epc_portal_theme_css(true); ?></style>
	<style>
		/* chrome1: 56px header + flex sidebar scroll (see epc_cp_professional.css) */
		body.epc-cp-shell.fixed-navbar #header {
			position: fixed !important;
			top: 0 !important;
			left: 0;
			right: 0;
			width: 100%;
			margin: 0;
			z-index: 1100;
			height: 56px !important;
			min-height: 56px !important;
			max-height: 56px !important;
		}
		/* First-paint: CP topnav-only full width under header */
		body.epc-cp-topnav-only #menu,
		body.epc-cp-topnav-only .epc-cp-sidebar-toggle-tab {
			display: none !important;
		}
		body.epc-cp-topnav-only .epc-cp-topnav {
			position: fixed !important;
			top: 56px !important;
			left: 0; right: 0; width: 100%;
			z-index: 1090;
		}
		body.epc-cp-topnav-only.fixed-sidebar.epc-cp-shell.fixed-navbar #wrapper {
			margin-left: 0 !important;
			width: 100% !important;
			margin-top: 100px !important;
			height: calc(100vh - 100px) !important;
			max-height: calc(100vh - 100px) !important;
		}
		body.epc-cp-shell {
			top: 0 !important;
		}
		body.fixed-sidebar.epc-cp-shell #menu {
			display: flex;
			flex-direction: column;
			bottom: 0 !important;
			height: calc(100vh - 56px) !important;
			max-height: calc(100vh - 56px) !important;
			min-height: 0;
			overflow-x: hidden !important;
			overflow-y: hidden !important;
			padding-bottom: 0;
			top: 56px !important;
		}
		html:has(body.fixed-sidebar.epc-cp-shell) {
			height: 100%;
		}
		body.fixed-sidebar.epc-cp-shell {
			height: 100%;
			overflow: hidden !important;
		}
		body.fixed-sidebar.epc-cp-shell.fixed-navbar #wrapper {
			top: 0 !important;
			padding-top: 0 !important;
			margin-top: 56px;
			height: calc(100vh - 56px);
			max-height: calc(100vh - 56px);
			min-height: 0;
			box-sizing: border-box;
			overflow-x: hidden;
			overflow-y: auto;
			-webkit-overflow-scrolling: touch;
			overscroll-behavior: contain;
		}
		body.fixed-sidebar.epc-cp-shell #navigation {
			flex: 1 1 auto;
			min-height: 0;
			height: auto !important;
			max-height: none !important;
			overflow-x: hidden !important;
			overflow-y: auto !important;
			padding-bottom: 170px;
			-webkit-overflow-scrolling: touch;
			overscroll-behavior: contain;
			scrollbar-gutter: stable;
			scrollbar-width: auto;
			scrollbar-color: rgba(148, 163, 184, 0.92) rgba(15, 23, 42, 0.55);
		}
		body.fixed-sidebar #side-menu {
			padding-bottom: 170px;
		}
		/* Header icon row flex — survives homer/style.css float rules */
		body.epc-cp-shell #header {
			display: flex !important;
			align-items: stretch !important;
			height: 56px !important;
			min-height: 56px !important;
			max-height: 56px !important;
			overflow: hidden !important;
		}
		body.epc-cp-shell #header nav[role="navigation"] {
			flex: 0 0 auto !important;
			min-width: 0 !important;
			display: flex !important;
			float: none !important;
			justify-content: flex-end !important;
			height: 56px !important;
		}
		body.epc-cp-shell #header nav .navbar-right {
			float: none !important;
			flex: 1 1 auto !important;
			min-width: 0 !important;
			display: flex !important;
			justify-content: flex-end !important;
		}
		body.epc-cp-shell #header nav .navbar-right > .nav {
			display: flex !important;
			flex-wrap: nowrap !important;
			float: none !important;
			min-width: 0 !important;
			overflow-x: auto !important;
			overflow-y: hidden !important;
		}
		body.epc-cp-shell #header nav .navbar-right .nav > li {
			flex: 0 0 auto !important;
			float: none !important;
		}
		body.epc-cp-shell #header nav .navbar-right .nav > li > a {
			height: 56px !important;
			min-height: 56px !important;
			padding: 0 10px !important;
			font-size: 16px !important;
		}
		body.epc-cp-shell #header .hbreadcrumb,
		body.epc-cp-shell #header .breadcrumb {
			display: none !important;
		}
	</style>
	
	<link rel="stylesheet" href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/css/astself.css" />
	<link href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/css/catalogue.css" rel="stylesheet">
	<link rel="stylesheet" href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/elFinder/css/theme-bootstrap-libreicons-svg.css" />
	
	
	<!-- Подключаем всплывающие подсказки -->
	<link rel="stylesheet" href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/vendor/toastr/build/toastr.min.css" />
	<script src="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/vendor/toastr/build/toastr.min.js"></script>
	<?php
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_translate.php';
	$epc_cp_industry_code = epc_portal_cp_active_industry();
	if ($epc_cp_industry_code === '' && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()
		&& !(function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context())) {
		$epc_cp_industry_code = 'platform_host';
	}
	$epc_cp_industry_meta = epc_portal_industry($epc_cp_industry_code);
	$product_name = epc_brand_system_name();
	$epcCpShell = epc_cp_shell_context();
	$epcShowPlatformErpNav = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator()
		&& !(function_exists('epc_portal_demo_is_cp_context') && epc_portal_demo_is_cp_context());
	$epcPlatformErpNavUrl = '/' . $DP_Config->backend_dir . '/platform-erp/';
?>
</head>
<body class="fixed-navbar fixed-sidebar epc-cp epc-cp-shell epc-cp-topnav-only epc-cp--<?php echo htmlspecialchars($epc_cp_industry_code, ENT_QUOTES, 'UTF-8'); ?><?php echo $epcApaiPage ? ' epc-apai-page' : ''; ?> <?php echo htmlspecialchars(epc_cp_shell_body_classes(), ENT_QUOTES, 'UTF-8'); ?>">
<?php
$GLOBALS['epc_cp_topnav_only'] = true;
echo epc_cp_force_visible_body_style();
echo epc_cp_force_visible_script();
//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();


//Функция вывода кнопок для панели управления
function print_backend_button($button_params)
{
	global $DP_Config;
	global $DP_Template;
	
	
	$target = "";
	if( isset($button_params["target"]) )
	{
		if( $button_params["target"] == "_blank" )
		{
			$target = "target=\"_blank\"";
		}
	}
	
	
	$onclick = "";
	if( isset($button_params["onclick"]) )
	{
		$onclick = "onclick=\"".$button_params["onclick"]."\"";
	}
	
	if( $button_params["background_color"] == "" && !empty($button_params["fontawesome_class"]) )
	{
		$button_params["background_color"] = "#2563eb";
	}
	if( $button_params["background_color"] == "" )
	{
		//Изображение
		$img = "/".$DP_Config->backend_dir."/templates/".$DP_Template->name."/images/".$button_params["img"];
		if(!file_exists($_SERVER["DOCUMENT_ROOT"]."/".$img))
		{
			$img = "content/control/images/window.png";
		}
		if(!file_exists($_SERVER["DOCUMENT_ROOT"]."/".$img) && !empty($button_params["fontawesome_class"]))
		{
			$button_params["background_color"] = "#2563eb";
		}
	}
	if( $button_params["background_color"] != "" )
	{
		?>
		<a class="panel_a" href="<?php echo $button_params["url"]; ?>" <?php echo $onclick; ?> <?php echo $target; ?>>
			<div class="panel_a_img" style="background-color: <?php echo $button_params["background_color"]; ?>;width:96px;height:96px;display:table-cell;vertical-align:middle;"><i class="<?php echo $button_params["fontawesome_class"]; ?>" style="color:#FFF;font-size:45px"></i></div>
			<div class="panel_a_caption"><?php echo $button_params["caption"]; ?></div>
		</a>
		<?php
	}
	else if( $button_params["background_color"] == "" )
	{
		?>
		<a class="panel_a" href="<?php echo $button_params["url"];?>" <?php echo $onclick; ?> <?php echo $target; ?>>
			<div class="panel_a_img" style="background: url('<?php echo $img; ?>') 0 0 no-repeat;"></div>
			<div class="panel_a_caption"><?php echo $button_params["caption"];?></div>
		</a>
		<?php
	}
}
?>


<!-- Simple splash screen-->
<div class="splash"> <div class="color-line"></div><div class="splash-title"><h1><?php echo $product_name; ?> - <?php echo translate_str_by_id(3992); ?></h1><p><?php echo translate_str_by_id(4001); ?>... </p><div class="spinner"> <div class="rect1"></div> <div class="rect2"></div> <div class="rect3"></div> <div class="rect4"></div> <div class="rect5"></div> </div> </div> </div>
<script>(function(){function epcHideCpSplash(){var s=document.querySelector('.splash');if(s){s.style.display='none';}}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',epcHideCpSplash);}else{epcHideCpSplash();}window.addEventListener('load',epcHideCpSplash);})();</script>
<!--[if lt IE 7]>
<p class="alert alert-danger">You are using an <strong>outdated</strong> browser. Please <a href="http://browsehappy.com/">upgrade your browser</a> to improve your experience.</p>
<![endif]-->

<!-- Header -->
<div id="header">
    <div class="color-line">
    </div>
	<a href="/<?php echo $DP_Config->backend_dir; ?>">
    <div id="logo" class="light-version ech-logo-wrap epc-cp-header-logo">
		<?php echo epc_ecomae_static_logo('header', array('show_title' => false, 'show_tagline' => false, 'aria_label' => 'ECOM AE')); ?>
    </div>
	</a>
	<?php if (!empty($epcCpShell['company'])) { ?>
	<div class="epc-cp-topbar-strip hidden-xs">
		<span class="epc-cp-topbar-company"><?php echo htmlspecialchars($epcCpShell['company'], ENT_QUOTES, 'UTF-8'); ?></span>
		<span class="epc-cp-topbar-role epc-cp-topbar-role--<?php echo htmlspecialchars($epcCpShell['type'], ENT_QUOTES, 'UTF-8'); ?>">
			<i class="fa fa-id-badge"></i> <?php echo htmlspecialchars($epcCpShell['label'], ENT_QUOTES, 'UTF-8'); ?>
		</span>
	</div>
	<?php } ?>
	<div class="epc-cp-header-breadcrumb hidden-xs" aria-label="Breadcrumb">
		<nav id="epc-cp-header-breadcrumb" class="epc-cp-header-breadcrumb__nav">
			<docpart type="module" name="breadcrumb" />
		</nav>
	</div>
    <nav role="navigation">
        <div class="header-link hide-menu"><i class="fa fa-bars"></i></div>
		
		
        <div class="small-logo hidden">
			<a href="/<?php echo $DP_Config->backend_dir; ?>">
				<span class="text-primary"><?php echo translate_str_by_id(3992); ?></span>
			</a>
        </div>
        
        <div class="mobile-menu">
            <button type="button" class="navbar-toggle mobile-menu-toggle" data-toggle="collapse" data-target="#mobile-collapse">
                <i class="fa fa-chevron-down"></i>
            </button>
            <div class="collapse mobile-navbar" id="mobile-collapse">
                <ul class="nav navbar-nav">
                    <li>
                        <a class="" href="javascript:void(0);" onclick="document.forms['logout_form'].submit();"><?php echo translate_str_by_id(3996); ?></a>
                    </li>
                </ul>
            </div>
        </div>
        <div class="navbar-right">
            <ul class="nav navbar-nav no-borders epc-cp-header-icons">
				<?php if ($epcShowPlatformErpNav) { ?>
				<li class="epc-cp-header-extra">
					<a href="<?php echo htmlspecialchars($epcPlatformErpNavUrl, ENT_QUOTES, 'UTF-8'); ?>" title="ECOM AE Platform ERP (ecomae DB)">
						<i class="fa fa-chart-line"></i>
						<span class="hidden-xs">Platform ERP</span>
					</a>
				</li>
				<?php } ?>
				<li class="epc-cp-header-extra">
					<a class="epc-cp-industry-toggle" href="/<?php echo $DP_Config->backend_dir; ?>/control/portal/industry_settings" title="Industry settings">
						<i class="fa <?php echo htmlspecialchars($epc_cp_industry_meta['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i>
						<span class="hidden-xs"><?php echo htmlspecialchars($epc_cp_industry_meta['name'], ENT_QUOTES, 'UTF-8'); ?></span>
					</a>
				</li>
				
				<?php
				if ((int) $DP_Config->show_ssl_checker === 1) {
					require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/modules/check_ssl/check_ssl.php';
				}
				epc_cp_top_alerts_render_email_item($db_link, $DP_Config);
				epc_cp_top_alerts_render_sms_item($db_link, $DP_Config);
				?>
				
				
				<?php
				// Professional shell hides legacy stock badges — skip heavy catalogue/stock probes.
				// (NOT IN over shop_storages_data is especially expensive on large tenants.)
				if (!epc_cp_top_alerts_use_professional_header()) {
				// Наличие товаров с критическим количеством
				$query = $db_link->prepare("SELECT COUNT(`id`) FROM `shop_catalogue_products` WHERE `min_limit_status` = '1';");
				$query->execute();
				$limited_products_count = $query->fetchColumn();

				if((int)$limited_products_count > 0) {

					$update_datetime = translate_str_by_key('2122');
					if(file_exists($_SERVER["DOCUMENT_ROOT"]."/content/cron/cron_update_products_limit_log.txt")) {
						$update_time = htmlentities(file_get_contents($_SERVER["DOCUMENT_ROOT"]."/content/cron/cron_update_products_limit_log.txt"));
						$update_datetime = date('H:i d.m.Y', $update_time);
					}

					?>
					<li class="dropdown">
							<a class="dropdown-toggle label-menu-corner" style="color:#ff3a3a;" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/stock?limited=1" title="<?php echo translate_str_by_key('1711374884_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?> <?= $update_datetime ;?>">
								<?= $limited_products_count ;?> <i class="fas fa-exclamation"></i>
							</a>
					</li>
					<?php
				}

				// Наличие складских записей с ценой <= 0 и наличием <= 0
				$query = $db_link->prepare("SELECT `id` FROM `shop_storages_data` WHERE `price` <= 0 AND `exist` <= 0 LIMIT 1;");
				$query->execute();
				$row = $query->fetch();
				if(empty($row)){
					$query = $db_link->prepare("SELECT `id` FROM `shop_catalogue_products` WHERE `id` NOT IN(SELECT DISTINCT `product_id` FROM `shop_storages_data`) LIMIT 1;");
					$query->execute();
					$row = $query->fetch();
				}
				if (!empty($row)) {
				?>
				<li class="dropdown epc-cp-stock-header-badge">
					<a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/stock" title="<?php echo translate_str_by_key('1711374928_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>">
						<i class="fa fa-exclamation-triangle" style="font-size: 17px; color: #ff5722;" aria-hidden="true"></i>
					</a>
				</li>
				<?php
				}

				// Наличие складских записей с ценой > 0 и наличием <= 0
				$query = $db_link->prepare("SELECT `id` FROM `shop_storages_data` WHERE `price` > 0 AND `exist` <= 0 LIMIT 1;");
				$query->execute();
				$row = $query->fetch();
				if (!empty($row)) {
				?>
				<li class="dropdown epc-cp-stock-header-badge">
					<a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/stock" title="<?php echo translate_str_by_key('1711375016_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>">
						<i class="fa fa-exclamation-circle" style="font-size: 17px; color: #f59e0b;" aria-hidden="true"></i>
					</a>
				</li>
				<?php
				}
				} // !epc_cp_top_alerts_use_professional_header()
			
				//Вывод быстрой навигации по наиболее востребованным функциям
				$control_items_query = $db_link->prepare('SELECT * FROM `control_items` WHERE `id` IN (?,?,?,?,?) ORDER BY `order`;');
				$control_items_query->execute( array(24, 4, 11, 26, 25) );
				$template_classes_fontawesome = array("fas fa-user-alt"=>"pe-7s-user", "fas fa-user-plus"=>"pe-7s-add-user", "fas fa-shopping-bag"=>"pe-7s-shopbag", "fas fa-shapes"=>"pe-7s-keypad", "fas fa-shopping-cart"=>"pe-7s-cart", "fas fa-money-check-alt"=>"pe-7s-credit");//Сопоставление с более подходящими под шаблон пиктограммами
				while( $control_item = $control_items_query->fetch() )
				{
					$control_item["url"] = str_replace( array("<backend>"), $DP_Config->backend_dir, $control_item["url"]);
					
					if (!epc_cp_top_alerts_use_professional_header()) {
						$control_item['fontawesome_class'] = $template_classes_fontawesome[$control_item['fontawesome_class']] ?? $control_item['fontawesome_class'];
					}
					?>

					<li class="dropdown hidden-xs hidden-sm epc-cp-quicknav-item">
						<a href="<?php echo $control_item['url']; ?>" title="<?php echo translate_str_by_id($control_item['caption']); ?>">
							<i class="<?php echo htmlspecialchars($control_item['fontawesome_class'], ENT_QUOTES, 'UTF-8'); ?>"></i>
						</a>
					</li>
					
					<?php
				}
				?>
				
				

				

				<!-- START Модуль индикации непросмотренных VIN -->
				<li id="not_viewed_vin" style="display:none;">
					<a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/requests" title="<?php echo translate_str_by_id(5140); ?>">
						<i style="position: relative; top: 1px;" class="pe-7s-upload pe-7s-gleam"></i>
						<span style="right:3px;" class="label label-primary" id="not_viewed_users_vin_count"></span>
					</a>
				</li>
				<script>
					var current_not_viewed_users_vin = -1;//Текущее количество непросмотренных запросов
					var title_original = document.title;//Исходное значение заголовка страницы
					
					//Функция обновления информации по количеству не просмотренных запросов
					function update_viewed_users_vin_info()
					{
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/requests/ajax_get_vin_info.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							dataType: "json",//Тип возвращаемого значения
							success: function(answer)
							{
								if(answer.status == 1)
								{
									//Первоначальная запись текущего количества
									if( current_not_viewed_users_vin == -1 )
									{
										current_not_viewed_users_vin = parseInt(answer.count);
									}
									//----------------------
									//Обработка виджета
									if( parseInt(answer.count) > 0)
									{
										document.getElementById("not_viewed_users_vin_count").innerHTML = answer.count;
										document.getElementById("not_viewed_vin").style.display = 'block';
									}
									else
									{
										document.getElementById("not_viewed_users_vin_count").innerHTML = "";
										document.getElementById("not_viewed_vin").style.display = 'none';
									}
									//----------------------
									//Обработка добавления новых пользователей (т.е. если количество увеличилось во время просмотра страницы)
									if( parseInt(answer.count) > parseInt(current_not_viewed_users_vin) )
									{
										//Звуковой сигнал//...
										//Фавикон//...
										//Title
										document.title = "<?php echo translate_str_by_id(5589); ?> "+title_original;
									}//~ if Индикация новых
									else if( parseInt(answer.count) == 0 )//Если новых нет (просмотрены на другой вкладке)
									{
										document.title = title_original;
									}
									//----------------------
									//Записываем новое текущее количество
									current_not_viewed_users_vin = answer.count;
								}
							}
						});
					}
					
					update_viewed_users_vin_info();//Запрос при загрузке страницы
					
					//Запускаем запросы
					var timerId_vin = setInterval(function() {
						update_viewed_users_vin_info();
					}, 600000);
				</script>
				<!-- END Модуль индикации непросмотренных VIN -->
				
				
				
				
				
				<!-- START Модуль индикации непросмотренных сообщений -->
				<li id="not_viewed_msg" class="dropdown" style="display:none;">
                    <a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders?read=0" title="<?php echo translate_str_by_id(4656); ?>">
                        <i style="font-size: 30px;" class="pe-7s-mail"></i>
                        <span class="label label-warning" id="not_viewed_msg_count"></span>
                    </a>
                </li>
				<script>
					//Функция обновления информации о количестве непрочитанных сообщений
					function update_cnt_not_viewed_msg()
					{
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							dataType: "json",//Тип возвращаемого значения
							success: function(answer)
							{
								if(answer.status == 1)
								{
									if(answer.count > 0){
										document.getElementById("not_viewed_msg_count").innerHTML = answer.count;
										document.getElementById("not_viewed_msg").style.display = 'block';
									}else{
										document.getElementById("not_viewed_msg").style.display = 'none';
									}
								}else{
									document.getElementById("not_viewed_msg").style.display = 'none';
								}
							}
						});
					}
					update_cnt_not_viewed_msg();//Запрос при загрузке страницы
					//Запускаем запросы непросмотренных сообщений
					var timerId_orders = setInterval(function() {
						update_cnt_not_viewed_msg();
					}, 400000);
				</script>
				<!-- END Модуль индикации непросмотренных сообщений -->
				

				


				<!-- START Модуль индикации непросмотренных заказов -->
				<?php
				//Для работы с пользователями
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
				$manager_id = DP_User::getAdminId();//ID менежера, который отображает эту страницу
				?>
				<li class="dropdown epc-cp-header-extra">
                    <a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders" title="<?php echo translate_str_by_id(3583); ?>">
                        <i class="pe-7s-shopbag"></i>
                        <span class="label label-success" id="not_viewed_orders_count"></span>
                    </a>
                </li>
				<script>
					var current_not_viewed_orders = -1;//Текущее количество непросмотренных заказов
					var title_original = document.title;//Исходное значение заголовка страницы
					//Функция обновления информации по просмотренным заказам
					function update_viewed_info()
					{
						var request_object = new Object;
						request_object.user_id = <?php echo $manager_id; ?>;
						
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/order_process/ajax_get_orders_info.php",
							dataType: "json",//Тип возвращаемого значения
							data: "request_object="+JSON.stringify(request_object)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								if(answer.status == 1)
								{
									//Первоначальная запись текущего количества заказов
									if( current_not_viewed_orders == -1 )
									{
										current_not_viewed_orders = parseInt(answer.message);
									}
									//----------------------
									//Обработка виджета
									if( parseInt(answer.message) > 0)
									{
										document.getElementById("not_viewed_orders_count").innerHTML = answer.message;
									}
									else
									{
										document.getElementById("not_viewed_orders_count").innerHTML = "";
									}
									//----------------------
									//Обработка добавления новых заказов (т.е. если количество увеличилось во время просмотра страницы)
									if( parseInt(answer.message) > parseInt(current_not_viewed_orders) )
									{
										//Звуковой сигнал//...
										//Фавикон//...
										//Title
										document.title = "<?php echo translate_str_by_id(4029); ?>! "+title_original;
									}//~ if Индикация новых заказов
									else if( parseInt(answer.message) == 0 )//Если новых заказов нет (просмотрены на другой вкладке)
									{
										document.title = title_original;
									}
									//----------------------
									//Записываем новое текущее количество
									current_not_viewed_orders = answer.message;
								}
							}
						});
					}
					update_viewed_info();//Запрос при загрузке страницы
					//Запускаем запросы непросмотренных заказов 1 раз в 10 секунд
					var timerId = setInterval(function() {
						update_viewed_info();
					}, 300000);
				</script>
				<!-- END Модуль индикации непросмотренных заказов -->
				
				
				
				
				
				<!-- START Модуль индикации заказов в статусе Обрабатывается -->
				<?php
				$orders_statuses_query = $db_link->prepare("SELECT * FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1 LIMIT 1;");
				$orders_statuses_query->execute();
				$orders_statuses_record = $orders_statuses_query->fetch();
				?>
				<li id="not_viewed_for_paid_orders" class="dropdown" style="display:none;">
                    <a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders?status_id=<?php echo $orders_statuses_record['id']; ?>" title="<?php echo translate_str_by_id(4657); ?> <?php echo $orders_statuses_record['name']; ?>">
                        <i class="pe-7s-shopbag"></i>
                        <span class="label label-info" id="for_paid_orders_count"></span>
                    </a>
                </li>
				<script>
					var for_paid_orders = -1;//Текущее количество непросмотренных заказов
					var title_original = document.title;//Исходное значение заголовка страницы
					//Функция обновления информации по просмотренным заказам
					function update_for_paid_orders_info()
					{
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/order_process/ajax_get_cnt_for_paid_orders.php",
							dataType: "json",//Тип возвращаемого значения
							data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								if(answer.status == 1)
								{
									//Первоначальная запись текущего количества заказов
									if( for_paid_orders == -1 )
									{
										for_paid_orders = parseInt(answer.count);
									}
									//----------------------
									//Обработка виджета
									if( parseInt(answer.count) > 0)
									{
										document.getElementById("for_paid_orders_count").innerHTML = answer.count;
										document.getElementById("not_viewed_for_paid_orders").style.display = 'block';
									}
									else
									{
										document.getElementById("for_paid_orders_count").innerHTML = "";
										document.getElementById("not_viewed_for_paid_orders").style.display = 'none';
									}
									//----------------------
									//Обработка добавления новых заказов (т.е. если количество увеличилось во время просмотра страницы)
									if( parseInt(answer.count) > parseInt(for_paid_orders) )
									{
										//Звуковой сигнал//...
										//Фавикон//...
										//Title
										document.title = "<?php echo translate_str_by_id(4658); ?> "+title_original;
									}//~ if Индикация новых заказов
									else if( parseInt(answer.count) == 0 )//Если новых заказов нет (просмотрены на другой вкладке)
									{
										document.title = title_original;
									}
									//----------------------
									//Записываем новое текущее количество
									for_paid_orders = answer.count;
								}
							}
						});
					}
					update_for_paid_orders_info();//Запрос при загрузке страницы
					//Запускаем запросы непросмотренных заказов 1 раз в 10 секунд
					var timerId_for_paid = setInterval(function() {
						update_for_paid_orders_info();
					}, 300000);
				</script>
				<!-- END Модуль индикации заказов в статусе Обрабатывается -->
				
				
				
				
				
				<!-- START Модуль индикации непросмотренных сообщений возвратов -->
				<li id="not_viewed_msg_returns" class="dropdown" style="display:none;">
                    <a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/returns-manager?read=0" title="<?php echo translate_str_by_id(4659); ?>">
                        <i style="font-size: 30px;" class="pe-7s-mail"></i>
                        <span class="label label-warning" id="not_viewed_msg_count_returns"></span>
                    </a>
                </li>
				<script>
					//Функция обновления информации о количестве непрочитанных сообщений
					function update_cnt_not_viewed_msg_returns()
					{
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php?returns=1&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							dataType: "json",//Тип возвращаемого значения
							success: function(answer)
							{
								if(answer.status == 1)
								{
									if(answer.count > 0){
										document.getElementById("not_viewed_msg_count_returns").innerHTML = answer.count;
										document.getElementById("not_viewed_msg_returns").style.display = 'block';
									}else{
										document.getElementById("not_viewed_msg_returns").style.display = 'none';
									}
								}else{
									document.getElementById("not_viewed_msg_returns").style.display = 'none';
								}
							}
						});
					}
					update_cnt_not_viewed_msg_returns();//Запрос при загрузке страницы
					//Запускаем запросы непросмотренных сообщений
					var timerId_returns = setInterval(function() {
						update_cnt_not_viewed_msg_returns();
					}, 400000);
				</script>
				<!-- END Модуль индикации непросмотренных сообщений возвратов -->
				
				
				
				
				
                <!-- Модуль индикации возвратов-->
                <li class="dropdown" id="indicator_returns">
                    <a class="dropdown-toggle label-menu-corner" href="/<?php echo $DP_Config->backend_dir; ?>/shop/returns-manager" title="<?php echo translate_str_by_id(4030); ?>">
                        <i class="pe-7s-back"></i>
                        <span class="label label-danger" id="not_viewed_returns_count"></span>
                    </a>
                </li>
                <script>
                    var current_not_viewed_returns = -1;//Текущее количество непросмотренных заказов
                    //Функция обновления информации по просмотренным заказам
                    function update_viewed_info_returns()
                    {
                        var request_object = new Object;
                        request_object.user_id = <?php echo $manager_id; ?>;

                        jQuery.ajax({
                            type: "POST",
                            async: true,
                            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/returns/ajax/ajax_get_returns_info.php",
                            dataType: "json",//Тип возвращаемого значения
                            data: "request_object="+JSON.stringify(request_object)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                            success: function(answer)
                            {
                                if(answer.status == 1)
                                {
                                    //Первоначальная запись текущего количества заказов
                                    if( current_not_viewed_returns == -1 )
                                    {
                                        current_not_viewed_returns = parseInt(answer.message);
                                    }
                                    //----------------------
                                    //Обработка виджета
                                    if( parseInt(answer.message) > 0)
                                    {
                                        document.getElementById("not_viewed_returns_count").innerHTML = answer.message;
                                    }
                                    else
                                    {
                                        document.getElementById("not_viewed_returns_count").innerHTML = "";
                                    }

                                    //Записываем новое текущее количество
                                    current_not_viewed_returns = answer.message;
                                }
                            }
                        });
                    }
                    update_viewed_info_returns();//Запрос при загрузке страницы
                    //Запускаем запросы непросмотренных заказов 1 раз в 10 секунд
                    var timerIdReturns = setInterval(function() {
                        update_viewed_info_returns();
                    }, 300000);
                </script>

                <!-- END Модуль индикации возвратов-->				
				
				<li class="dropdown epc-cp-header-keep">
                    <a href="<?php echo $DP_Config->domain_path; ?>" target="_blank" title="<?php echo translate_str_by_id(4031); ?>">
                        <i class="fa fa-external-link"></i>
                    </a>
                </li>
				
				<?php echo epc_cp_translate_render('cp'); ?>
				
                <li class="dropdown epc-cp-header-keep epc-cp-header-action--logout">
                    <a href="javascript:void(0);" onclick="document.forms['logout_form'].submit();" title="<?php echo translate_str_by_id(3996); ?>">
                        <i class="fa fa-sign-out"></i>
                    </a>
                </li>
            </ul>
        </div>
    </nav>
</div>

<?php
// Hidden logout form still needed by header actions when left rail is off.
$admin_profile = DP_User::getAdminProfile();
?>
<form id="logout_form" method="POST" name="logout_form" style="display:none;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	<input type="hidden" name="logout" value="logout" />
</form>
<?php
$epcCpTopNavFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_top_nav.php';
if (is_file($epcCpTopNavFile)) {
	require_once $epcCpTopNavFile;
	if (function_exists('epc_cp_render_top_nav')) {
		epc_cp_render_top_nav();
	}
}
?>

<!-- Navigation (hidden when top mega-menu is primary) -->
<aside id="menu" aria-hidden="true">
    <div id="navigation">
        <div class="profile-picture epc-cp-user-card">
            <div class="stats-label text-color epc-cp-user-card__body">
				<?php
				//Блок слева - профиль пользователя и форма выхода (kept for legacy; hidden in topnav-only)
				?>
				<form id="logout_form_sidebar" method="POST" name="logout_form_sidebar" onsubmit="document.forms['logout_form'].submit(); return false;">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					<input type="hidden" name="logout" value="logout" />
				</form>
				<div class="epc-cp-user-card__row">
					<div class="epc-cp-user-card__identity">
						<span class="font-extra-bold font-uppercase epc-cp-user-card__name"><?php echo $admin_profile["name"]." ".$admin_profile["surname"]; ?></span>
						<small class="text-muted epc-cp-user-card__role"><?php echo translate_str_by_id(3452); ?></small>
					</div>
					<button type="button" class="epc-cp-user-card__quit" onclick="document.forms['logout_form'].submit();"><?php echo translate_str_by_id(3996); ?></button>
				</div>
            </div>
        </div>
		
		
		<div class="epc-cp-sidebar-meta">
			<div class="text-center epc-cp-sidebar-meta__edit">
				<?php
				$edit_mode = null;
				if( isset($_COOKIE["edit_mode"]) )
				{
					$edit_mode = $_COOKIE["edit_mode"];
				}
				switch($edit_mode)
				{
					case "frontend":
						$is_frontend = 1;
						break;
					case "backend":
						$is_frontend = 0;
						break;
					default:
						$is_frontend = 1;
						break;
				}
				if($is_frontend)
				{
					?>
					<?php echo translate_str_by_id(4032); ?>: <b><?php echo translate_str_by_id(4033); ?></b> <img style="height:15px; border-radius:10px; vertical-align:middle" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/earth.png" alt="" />
					<?php
				}
				else
				{
					?>
					<?php echo translate_str_by_id(4032); ?>: <b><?php echo translate_str_by_id(4034); ?></b> <img style="height:15px; border-radius:10px; vertical-align:middle" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/shield.png" alt="" />
					<?php
				}
				?>
			</div>
			<?php
			require( $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/modules/lang/module.php' );
			?>
		</div>
		
		<docpart type="module" name="left_cp_menu" />
    </div>
</aside>

<!-- Main Wrapper -->
<div id="wrapper">
	
	<?php
	// Блок новостей с intask.pro — приходит только на русском; при английском UI не показываем
	$dp_news_lang_ok = true;
	if( function_exists('get_work_lang') && get_work_lang() === 'en' )
	{
		$dp_news_lang_ok = false;
	}
	//Блок новостей - отображаем только на главной странице
	if( $DP_Content->main_flag && isset($DP_Config->intask_server) && $dp_news_lang_ok )
	{
		//Запрос через определенный промежуток времени
		if( empty($_COOKIE["dp_news_time"]) || $_COOKIE["dp_news_time"] < (time() - 3600) )
		{
			$SQL = "SELECT `time` FROM `version_control` ORDER BY `id` DESC LIMIT 1;";
			$query = $db_link->prepare($SQL);
			$query->execute();
			$row = $query->fetch();
			if($row['time'] < (time() - (86400 * 30)))
			{
				//Отображаем блок только для Администраторов с доступом к Настройкам сайта
				$adminProfile = DP_User::getAdminProfile();//Профиль администратора
				$SQL = "SELECT * FROM `content_access` WHERE `content_id` IN(SELECT `id` FROM `content` WHERE `alias` = 'config' AND `is_frontend` = 0) AND `group_id` = ?;";
				$query = $db_link->prepare($SQL);
				$query->execute(array($adminProfile["groups"][0]));
				$row = $query->fetch();
				if( !empty($row) )
				{
					// Bound timeout — unbounded file_get_contents hung CP when intask was down.
					$dp_news_ctx = stream_context_create(array(
						'http' => array('timeout' => 2, 'ignore_errors' => true),
						'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
					));
					$dp_news_text = @file_get_contents($DP_Config->intask_server.'intask_messages.php', false, $dp_news_ctx);
					$dp_news_hash = md5($dp_news_text);
					if( ! isset($_COOKIE["dp_news_hash"]) ){
						$_COOKIE["dp_news_hash"] = '';
					}
					if( !empty($dp_news_text) && $dp_news_hash !== $_COOKIE["dp_news_hash"] )
					{
		?>
						<div id="dp_news" class="normalheader transition animated fadeIn">
							<div class="hpanel">
								<div class="panel-body">
									<a title="<?php echo translate_str_by_id(2447); ?>" class="small-header-action" onclick="dp_news_hidden();">
										<div class="clip-header">
											<i class="fa fa-chevron-up"></i>
										</div>
									</a>

									<?php echo $dp_news_text; ?>
									
								</div>
							</div>
						</div>
						<script>
						function dp_news_hidden(){
							let hash = '<?php echo $dp_news_hash; ?>';
							let time = '<?php echo time(); ?>';
							let date = new Date(new Date().getTime() + 15552000 * 1000);
							document.cookie = "dp_news_hash="+hash+"; path=/; expires=" + date.toUTCString();
							document.cookie = "dp_news_time="+time+"; path=/; expires=" + date.toUTCString();
							document.getElementById("dp_news").style.display = "none";
						}
						</script>
		<?php
					}
					else
					{
						//Если новость не изменилась запишем время последнего запроса
						?>
						<script>
							let time = '<?php echo time(); ?>';
							let date = new Date(new Date().getTime() + 15552000 * 1000);
							document.cookie = "dp_news_time="+time+"; path=/; expires=" + date.toUTCString();
						</script>
						<?php
					}
				}
			}
		}
	}
	?>
	
	<?php
	// Home (/cp/control) already has the tenant dashboard hero — skip duplicate CMS page header.
	$epcSkipPageHeader = !empty($DP_Content->main_flag)
		|| (isset($DP_Content->url) && in_array((string) $DP_Content->url, array('control', ''), true));
	if (!$epcSkipPageHeader) {
	?>
	<div class="epc-cp-page-header transition animated fadeIn">
		<?php
		$epcPageHeader = epc_cp_page_header_context();
		?>
		<div class="epc-cp-page-header__card">
			<div class="epc-cp-page-header__main">
				<div class="epc-cp-page-header__eyebrow">
					<span class="epc-cp-page-header__eyebrow-icon"><i class="fa <?php echo htmlspecialchars($epcPageHeader['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i></span>
					<span class="epc-cp-page-header__eyebrow-text"><?php echo htmlspecialchars($epcPageHeader['eyebrow'], ENT_QUOTES, 'UTF-8'); ?></span>
					<?php if (!empty($epcPageHeader['role_label'])) { ?>
					<span class="epc-cp-page-header__role epc-cp-page-header__role--<?php echo htmlspecialchars($epcPageHeader['role_type'], ENT_QUOTES, 'UTF-8'); ?>">
						<?php echo htmlspecialchars($epcPageHeader['role_label'], ENT_QUOTES, 'UTF-8'); ?>
					</span>
					<?php } ?>
				</div>
				<h1 class="epc-cp-page-header__title"><?php echo $DP_Content->value; ?></h1>
				<?php if (trim((string) $DP_Content->description) !== '') { ?>
				<p class="epc-cp-page-header__desc"><?php echo $DP_Content->description; ?></p>
				<?php } ?>
				<?php echo epc_cp_page_header_actions_html($epcPageHeader['actions']); ?>
			</div>
		</div>
	</div>
	<?php } ?>
	
	
	
	
    <div class="content<?php echo ' epc-cp-main-pane'; ?>">
		<div class="epc-cp-content-inner">
		<div class="row">
			
			<?php
				require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/actions_alert.php';
				if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: before main content'); }
			?>
		
			<!--epc-cp-main-begin-->
			<docpart type="main" name="main" />
			<!--epc-cp-main-end-->
			<?php if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: after main content'); } ?>
		</div>
		</div>
    </div>

    <!-- Right sidebar -->
    <div id="right-sidebar" class="animated fadeInRight">
		
		<!--
		<div class="p-m">
			<button id="sidebar-close" class="right-sidebar-toggle sidebar-button btn btn-default m-b-md"><i class="pe pe-7s-close"></i>
            </button>
        </div>
		-->
		
		
		
		<div class="row">
			<div class="col-lg-12 text-left" style="margin:7px;">
				<button id="sidebar-close" class="right-sidebar-toggle sidebar-button btn btn-default ">
					<i class="pe pe-7s-close"></i>
				</button>
			</div>
		
			<?php if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: before right-sidebar module'); } ?>
			<docpart type="module" name="left_cp_menu1" />
			<?php if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: after right-sidebar module'); } ?>
		</div>
    </div>

    <!-- Footer-->
    <footer class="footer">
        <span class="pull-right">
            <?php echo htmlspecialchars(epc_brand_system_name(), ENT_QUOTES, 'UTF-8'); ?>
        </span>
        &copy; <?php echo date('Y', time()); ?> &middot; <?php echo epc_brand_copyright_html(); ?>
    </footer>

</div>

<button type="button" id="epc-cp-sidebar-toggle" class="epc-cp-sidebar-toggle-tab" aria-expanded="true" aria-label="Hide menu" title="Menu"><i class="fa fa-chevron-left"></i></button>

<!-- Vendor scripts -->
<?php
$epcCpFastTenant = function_exists('epc_cp_fast_tenant_active') && epc_cp_fast_tenant_active();
$epcCpNeedCharts = !$epcCpFastTenant;
$epcContentUrlForJs = isset($DP_Content) && is_object($DP_Content) ? trim((string) ($DP_Content->url ?? ''), '/') : '';
if ($epcCpFastTenant) {
	// Skip flot/peity/sparkline on tenant CP for ~1s feel; statistics can still load them.
	$epcCpNeedCharts = ($epcContentUrlForJs !== '' && strpos($epcContentUrlForJs, 'users/statistics') === 0);
}
?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery/dist/jquery.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery-ui/jquery-ui.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/slimScroll/jquery.slimscroll.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/js/bootstrap.min.js"></script>
<?php if ($epcCpNeedCharts) { ?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery-flot/jquery.flot.js" defer></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery-flot/jquery.flot.resize.js" defer></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery-flot/jquery.flot.pie.js" defer></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/flot.curvedlines/curvedLines.js" defer></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery.flot.spline/index.js" defer></script>
<?php } ?>
<?php if (!$epcApaiPage) { ?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/metisMenu/dist/metisMenu.min.js" defer></script>
<?php } ?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/iCheck/icheck.min.js" defer></script>
<?php if ($epcCpNeedCharts) { ?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/peity/jquery.peity.min.js" defer></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/sparkline/index.js" defer></script>
<?php } ?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fooTable/dist/footable.all.min.js" defer></script>


<!-- App scripts -->
<?php
// Always use PHP proxy — /cp/js/* often 404s behind nginx on tenants.
$epcCpTopnavJsVer = function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260720cptopnav1';
echo '<script src="/content/general_pages/epc_cp_topnav_js.php?v=' . htmlspecialchars($epcCpTopnavJsVer, ENT_QUOTES, 'UTF-8') . '"></script>' . "\n";
?>
<?php echo epc_cp_sidebar_collapse_script(); ?>
<?php if (!$epcApaiPage) {
	echo epc_cp_menu_sections_script();
} else { ?>
<script>
(function(){function e(){if(typeof window.epcCpClearUiBlockers==='function'){window.epcCpClearUiBlockers();}if(typeof window.epcCpNuclearForceVisible==='function'){window.epcCpNuclearForceVisible();}}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',e);}else{e();}})();
</script>
<?php } ?>
<script>
/* Inline homer CP essentials when /scripts/homer.js 404s on nginx */
(function($){if(!$||!$('body').hasClass('epc-cp-shell')){return;}if(typeof window.epcCpClearUiBlockers==='function'){window.epcCpClearUiBlockers();}$('.splash').css('display','none');$('.animate-panel').removeClass('animate-panel opacity-0');$('.animate-panel .row > div, .animate-panel .row > [class*="col-"]').removeClass('opacity-0 zoomIn animated-panel stagger').css({opacity:'',visibility:'',animation:''});if(typeof window.epcCpNuclearForceVisible==='function'){window.epcCpNuclearForceVisible();}if(typeof window.epcCpMenuSectionsInit==='function'){window.epcCpMenuSectionsInit();}})(window.jQuery);
</script>
<script src="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/scripts/homer.js&v=<?php echo htmlspecialchars($epcCpHomerVer, ENT_QUOTES, 'UTF-8'); ?>"></script>
<script>
(function () {
	if (!document.body.classList.contains('epc-cp-shell')) {
		return;
	}
	if (typeof window.epcCpNuclearForceVisible === 'function') {
		window.epcCpNuclearForceVisible();
	}
})();
</script>
<?php if (!empty($epcCpNeedCharts)) { ?>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/scripts/charts.js" defer></script>
<?php } ?>
<script>
/* Strip leftover developer-facing language prefixes from CP chrome. */
(function () {
	function epcCpCleanTextNodes(root) {
		if (!root) return;
		var walk = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
		var node;
		while ((node = walk.nextNode())) {
			var t = node.nodeValue;
			if (!t || t.indexOf('ERROR STR_KEY') === -1) continue;
			node.nodeValue = t.replace(/ERROR STR_KEY:\s*[^\.]+\.\s*/g, '').replace(/==Empty string==/g, '');
		}
	}
	function run() {
		epcCpCleanTextNodes(document.getElementById('wrapper') || document.body);
	}
	if (document.readyState === 'loading') {
		document.addEventListener('DOMContentLoaded', run);
	} else {
		run();
	}
})();
</script>


<script>
	if( typeof webix != 'undefined' )
	{
		webix.Touch.disable();
	}
</script>

<script>
	(function() {
		var navEl = null;
		var rafId = 0;
		var resizeTimer = 0;
		var lastViewport = 0;

		function epcFixCpSidebarScroll() {
			navEl = navEl || document.getElementById('navigation');
			if (!navEl) {
				return;
			}
			var vh = window.innerHeight || document.documentElement.clientHeight || 0;
			if (navEl.dataset.epcScrollReady === '1' && Math.abs(vh - lastViewport) < 4) {
				return;
			}
			lastViewport = vh;
			navEl.dataset.epcScrollReady = '1';
			navEl.style.height = '';
			navEl.style.maxHeight = '';
			navEl.style.flex = '1 1 auto';
			navEl.style.minHeight = '0';
			navEl.style.overflowY = 'auto';
			navEl.style.overflowX = 'hidden';
			navEl.style.paddingBottom = '170px';
			var sideMenu = document.getElementById('side-menu');
			if (sideMenu) {
				sideMenu.style.paddingBottom = '170px';
			}
		}

		function scheduleSidebarScrollFix() {
			if (rafId) {
				return;
			}
			rafId = window.requestAnimationFrame(function() {
				rafId = 0;
				epcFixCpSidebarScroll();
			});
		}

		function scheduleSidebarScrollFixDebounced() {
			if (resizeTimer) {
				window.clearTimeout(resizeTimer);
			}
			resizeTimer = window.setTimeout(function() {
				resizeTimer = 0;
				scheduleSidebarScrollFix();
			}, 120);
		}

		window.epcFixCpSidebarScroll = scheduleSidebarScrollFix;
		window.addEventListener('load', scheduleSidebarScrollFix, { passive: true });
		window.addEventListener('resize', scheduleSidebarScrollFixDebounced, { passive: true });
		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', scheduleSidebarScrollFix, { passive: true });
		} else {
			scheduleSidebarScrollFix();
		}
	})();
</script>




<script>
// -------------------------------------------------
//Настройка высплывающих подсказок
toastr.options = {
  "closeButton": true,
  "debug": false,
  "newestOnTop": false,
  "progressBar": false,
  "positionClass": "toast-top-center",
  "preventDuplicates": false,
  "onclick": null,
  "showDuration": "300",
  "hideDuration": "1000",
  "timeOut": "10000",
  "extendedTimeOut": "1000",
  "showEasing": "swing",
  "hideEasing": "linear",
  "showMethod": "fadeIn",
  "hideMethod": "fadeOut"
}
// -------------------------------------------------
//Показать подсказку
function show_hint(hint_text)
{
	toastr.info(hint_text);
}
// -------------------------------------------------
</script>

<?php
$epcKktModal = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/kkt/check_create_modal_window.php';
if (is_file($epcKktModal)) {
	require_once $epcKktModal;
}
$epcPhoneMask = $_SERVER['DOCUMENT_ROOT'] . '/lib/inputmask/phone_mask.php';
if (is_file($epcPhoneMask)) {
	require_once $epcPhoneMask;
}

$epcPosShellJs = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pos/epc_pos_shell_js.php';
$epcCpPageAssets = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
if (is_file($epcCpPageAssets)) {
	require_once $epcCpPageAssets;
}
if (is_file($epcPosShellJs)) {
	require_once $epcPosShellJs;
}
$epcCpScriptRelocateFooter = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_script_relocate.php';
if (is_file($epcCpScriptRelocateFooter)) {
	require_once $epcCpScriptRelocateFooter;
	if (function_exists('epc_cp_render_relocated_footer_scripts')) {
		epc_cp_render_relocated_footer_scripts();
	}
}
if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: before footer scripts'); }
if (isset($DP_Content) && is_object($DP_Content)) {
	$contentUrl = (string) ($DP_Content->url ?? '');
	if (function_exists('epc_cp_page_footer_scripts')) {
		epc_cp_page_footer_scripts($contentUrl);
	}
	if (function_exists('epc_pos_cp_footer_scripts')) {
		epc_pos_cp_footer_scripts($contentUrl);
	}
}
if (function_exists('epc_cp_trace')) { epc_cp_trace('desktop: after footer scripts'); }
?>

</body>
</html>