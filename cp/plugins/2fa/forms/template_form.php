<!DOCTYPE html>
<html>
<head>
	<base href="/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/">
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">

    <!-- Page title -->
    <docpart type="head" name="head" />

    <!-- Place favicon.ico and apple-touch-icon.png in the root directory -->
    <!--<link rel="shortcut icon" type="image/ico" href="favicon.ico" />-->

    <!-- Vendor styles -->
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/fontawesome/css/font-awesome.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/metisMenu/dist/metisMenu.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/animate.css/animate.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/css/bootstrap.css" />

    <!-- App styles -->
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/fonts/pe-icon-7-stroke/css/pe-icon-7-stroke.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/fonts/pe-icon-7-stroke/css/helper.css" />
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css">
	<link rel="preconnect" href="https://fonts.googleapis.com">
	<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
	<link rel="stylesheet" href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/css/epc_cp_ui.css&v=20260530b">
<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
extract(epc_brand_cp_context());
$loginBrand = epc_cp_login_branding();
epc_ecomae_hub_logo_enqueue();
epc_cp_login_hero_enqueue();
?>

</head>
<body class="blank epc-cp epc-cp-login epc-cp-login-hero epc-cp-login--<?php echo htmlspecialchars($loginBrand['cp_type'], ENT_QUOTES, 'UTF-8'); ?>">


<!-- Simple splash screen-->
<div class="splash"> <div class="color-line"></div><div class="splash-title"><h1><?php echo $product_name; ?> - <?php echo translate_str_by_id(3992); ?></h1><p><?php echo translate_str_by_id(4001); ?>... </p><div class="spinner"> <div class="rect1"></div> <div class="rect2"></div> <div class="rect3"></div> <div class="rect4"></div> <div class="rect5"></div> </div> </div> </div>
<script>(function(){function epcHideCpSplash(){var s=document.querySelector('.splash');if(s){s.style.display='none';}}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',epcHideCpSplash);}else{epcHideCpSplash();}window.addEventListener('load',epcHideCpSplash);})();</script>
<!--[if lt IE 7]>
<p class="alert alert-danger">You are using an <strong>outdated</strong> browser. Please <a href="http://browsehappy.com/">upgrade your browser</a> to improve your experience.</p>
<![endif]-->

<div class="color-line"></div>

<div class="epc-cp-login-shell">
	<div class="epc-cp-login-brand">
		<div class="epc-cp-login-brand-inner">
			<span class="epc-cp-login-brand-badge"><?php echo htmlspecialchars($loginBrand['badge'], ENT_QUOTES, 'UTF-8'); ?></span>
			<h1><?php echo htmlspecialchars($loginBrand['heading'], ENT_QUOTES, 'UTF-8'); ?></h1>
			<p class="epc-cp-login-brand-sub"><?php echo htmlspecialchars($loginBrand['sub'], ENT_QUOTES, 'UTF-8'); ?></p>
			<p class="epc-cp-login-brand-tagline"><?php echo $loginBrand['hero_tagline']; ?></p>
			<div class="epc-cp-login-visual">
				<?php echo epc_ecomae_hub_logo('login', array('show_title' => true, 'show_tagline' => false, 'aria_label' => 'ECOM AE unified ERP and commerce cloud')); ?>
			</div>
		</div>
	</div>
	<div class="epc-cp-login-panel">
<div class="login-container">
    <div class="row">
        <div class="col-md-12">
            <div class="text-center m-b-md">
                <h3><?php echo translate_str_by_id(4007); ?></h3>
                <small><?php echo translate_str_by_id(4006); ?></small>
            </div>
            <div class="hpanel">
                <div class="panel-body">
					
					
					<?php
					if( $message_to_show != "" )
					{	
						echo $message_to_show;
					}
					?>
					
					<form id="2fa_form" method="GET">
						<div class="form-group">
							<label class="control-label" for="2fa_code"><?php echo translate_str_by_id(4007); ?></label>
							<input type="text" placeholder="<?php echo translate_str_by_id(4007); ?>" title="<?php echo translate_str_by_id(4007); ?>" required="" value="" name="2fa_code" id="2fa_code" class="form-control" />
						</div>
						<button type="submit" class="btn btn-success btn-block"><?php echo translate_str_by_id(4008); ?></button>
						<a class="btn btn-default btn-block" href="<?php echo getPageUrl(); ?>"><?php echo translate_str_by_id(4009); ?></a>
					</form>
                </div>
            </div>
        </div>
    </div>
    <div class="row login-footer">
        <div class="col-md-12 text-center">
            <?php echo $product_description; ?><br/><?php echo $brand_copyright; ?>
        </div>
    </div>
</div>
	</div>
</div>


<!-- Vendor scripts -->
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery/dist/jquery.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/jquery-ui/jquery-ui.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/slimScroll/jquery.slimscroll.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/bootstrap/dist/js/bootstrap.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/metisMenu/dist/metisMenu.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/iCheck/icheck.min.js"></script>
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/vendor/sparkline/index.js"></script>

<!-- App scripts -->
<script src="/epc-static.php?f=cp/templates/bootstrap_admin/scripts/homer.js"></script>

</body>
</html>
