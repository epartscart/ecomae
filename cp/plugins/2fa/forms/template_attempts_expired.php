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
	<link rel="stylesheet" href="/epc-static.php?f=<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/templates/bootstrap_admin/css/epc_cp_ui.css&v=20260527">
<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ecomae_hub_logo.php';
epc_ecomae_hub_logo_enqueue();
?>

</head>
<body class="blank epc-cp epc-cp-login">


<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
extract(epc_brand_cp_context());
?>


<!-- Simple splash screen-->
<div class="splash"> <div class="color-line"></div><div class="splash-title"><h1><?php echo $product_name; ?> - <?php echo translate_str_by_id(3992); ?></h1><p><?php echo translate_str_by_id(4001); ?>... </p><div class="spinner"> <div class="rect1"></div> <div class="rect2"></div> <div class="rect3"></div> <div class="rect4"></div> <div class="rect5"></div> </div> </div> </div>
<!--[if lt IE 7]>
<p class="alert alert-danger">You are using an <strong>outdated</strong> browser. Please <a href="http://browsehappy.com/">upgrade your browser</a> to improve your experience.</p>
<![endif]-->

<div class="color-line"></div>

<div class="epc-cp-login-shell">
	<div class="epc-cp-login-brand">
		<div class="epc-cp-login-brand-inner">
			<span class="epc-cp-login-brand-badge"><?php echo translate_str_by_id(3992); ?></span>
			<h1><?php echo $product_name; ?></h1>
			<p><?php echo translate_str_by_id(4002); ?></p>
			<div class="epc-cp-login-visual">
				<?php echo epc_ecomae_static_logo('login', array('show_title' => true, 'show_tagline' => true, 'aria_label' => 'ECOM AE')); ?>
			</div>
		</div>
	</div>
	<div class="epc-cp-login-panel">
<div class="login-container">
    <div class="row">
        <div class="col-md-12">
            <div class="text-center m-b-md">
                <h3><?php echo translate_str_by_id(4002); ?></h3>
                <small><?php echo translate_str_by_id(4003); ?></small>
            </div>
            <div class="hpanel">
                <div class="panel-body">
					<?php echo translate_str_by_id(4003); ?>
					<?php echo translate_str_by_id(4004); ?>
					<a class="btn btn-default btn-block" href="<?php echo getPageUrl(); ?>"><?php echo translate_str_by_id(4005); ?></a>
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