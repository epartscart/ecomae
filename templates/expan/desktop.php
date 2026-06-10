<?php
defined('_ASTEXE_') or die('No access');
?>
<!DOCTYPE html>
<html lang="<?php echo $multilang_params['lang']; ?>" data-theme="default">
<head>
	<!-- Google tag (gtag.js) -->
	<script async src="https://www.googletagmanager.com/gtag/js?id=G-J19D1KHXCG"></script>
	<script>
	  window.dataLayer = window.dataLayer || [];
	  function gtag(){dataLayer.push(arguments);}
	  gtag('js', new Date());

	  gtag('config', 'G-J19D1KHXCG');
	</script>
	<base href="/templates/expan/">
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, minimum-scale=1.0, maximum-scale=1.0, user-scalable=no">

    <link rel="shortcut icon" href="/favicon.ico" />

	
	<link href='https://fonts.googleapis.com/css?family=Roboto&subset=latin,latin-ext,cyrillic' rel='stylesheet' type='text/css'>
	
    <!-- CSS -->
    <link href="assets/css/preload.css" rel="stylesheet">
    
    <!-- Compiled in vendors.js -->
    <!--
    <link href="assets/css/bootstrap.min.css" rel="stylesheet">
    <link href="assets/css/bootstrap-switch.min.css" rel="stylesheet">
    <link href="assets/css/font-awesome.min.css" rel="stylesheet">
    <link href="assets/css/animate.min.css" rel="stylesheet">
    <link href="assets/css/slidebars.min.css" rel="stylesheet">
    <link href="assets/css/lightbox.css" rel="stylesheet">
    <link href="assets/css/jquery.bxslider.css" rel="stylesheet" />
    <link href="assets/css/buttons.css" rel="stylesheet">
    -->

    <link href="assets/css/vendors.css" rel="stylesheet">
    <link href="assets/css/syntaxhighlighter/shCore.css" rel="stylesheet" >

    <link href="assets/css/style-<?php echo $DP_Template->data_value->main_color; ?>.css" rel="stylesheet" title="default">
    
	<link href="assets/css/width-<?php echo $DP_Template->data_value->container_type; ?>.css" rel="stylesheet" title="default">

    

    <!-- HTML5 shim and Respond.js IE8 support of HTML5 elements and media queries -->
    <!--[if lt IE 9]>
        <script src="assets/js/html5shiv.min.js"></script>
        <script src="assets/js/respond.min.js"></script>
    <![endif]-->
	
	<script src="assets/js/vendors.js"></script>
	<docpart type="head" name="head" />
	<link rel="stylesheet" href="/templates/<?php echo $DP_Template->name; ?>/css/docpart/style.css" type="text/css" />
	<script src="/lib/jQuery_ui/jquery-ui.js"></script>
	<link href="/lib/jQuery_ui/jquery-ui.css" rel="stylesheet">
	<link href="https://fonts.googleapis.com/css?family=PT+Sans:regular,italic,bold,bolditalic" rel="stylesheet" type="text/css" />
	
	<link rel="stylesheet" href="/templates/expan/css/shop/geo.css" type="text/css" />
	<link rel="stylesheet" href="/templates/expan/css/catalogue/catalogue.css" type="text/css" />
	<script src="/lib/jQuery_ui/jquery-ui.js"></script>
	<link href="/lib/jQuery_ui/jquery-ui.css" rel="stylesheet">
	
	<link href="css/astself.css" rel="stylesheet">
	
	<link href="/modules/slider/css/style.css" rel="stylesheet" type="text/css" />
	<?php require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php'; echo epc_brand_hosted_by_css_link_html(); ?>
</head>

<!-- Preloader -->
<div id="preloader">
    <div id="status">&nbsp;</div>
</div>

<body>
<?php require($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/google_translate_top.php"); ?>
<?php
//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();


//Переменные для подстановки в input модулей поисковых строк
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/search_strs_for_inputs.php");
?>


<?php
//Определяем, выводить ли блок настройки стилей
if($DP_Template->data_value->show_options_block == "on")
{
	?>
	<div id="theme-options" class="hidden-xs">
		<div id="body-options">
			<div id="color-options">
				<h2 class="no-margin-top"><?php echo translate_str_by_id(4788); ?></h2>
				<a href="javascript:void(0);" rel="style-blue.css" class="color-box color-blue">blue</a>
				<a href="javascript:void(0);" rel="style-blue2.css" class="color-box color-blue2">blue2</a>
				<a href="javascript:void(0);" rel="style-blue3.css" class="color-box color-blue3">blue3</a>
				<a href="javascript:void(0);" rel="style-blue4.css" class="color-box color-blue4">blue4</a>
				<a href="javascript:void(0);" rel="style-blue5.css" class="color-box color-blue5">blue5</a>
				<a href="javascript:void(0);" rel="style-green.css" class="color-box color-green">green</a>
				<a href="javascript:void(0);" rel="style-green2.css" class="color-box color-green2">green2</a>
				<a href="javascript:void(0);" rel="style-green3.css" class="color-box color-green3">green3</a>
				<a href="javascript:void(0);" rel="style-green4.css" class="color-box color-green4">green4</a>
				<a href="javascript:void(0);" rel="style-green5.css" class="color-box color-green5">green5</a>
				<a href="javascript:void(0);" rel="style-red.css" class="color-box color-red">red</a>
				<a href="javascript:void(0);" rel="style-red2.css" class="color-box color-red2">red2</a>
				<a href="javascript:void(0);" rel="style-red3.css" class="color-box color-red3">red3</a>
				<a href="javascript:void(0);" rel="style-fuchsia.css" class="color-box color-fuchsia">fuchsia</a>
				<a href="javascript:void(0);" rel="style-pink.css" class="color-box color-pink">pink</a>
				<a href="javascript:void(0);" rel="style-yellow.css" class="color-box color-yellow">yellow</a>
				<a href="javascript:void(0);" rel="style-yellow2.css" class="color-box color-yellow2">yellow2</a>
				<a href="javascript:void(0);" rel="style-orange.css" class="color-box color-orange">orange</a>
				<a href="javascript:void(0);" rel="style-orange2.css" class="color-box color-orange2">orange2</a>
				<a href="javascript:void(0);" rel="style-orange3.css" class="color-box color-orange3">orange3</a>
				<a href="javascript:void(0);" rel="style-violet.css" class="color-box color-violet">violet</a>
				<a href="javascript:void(0);" rel="style-violet2.css" class="color-box color-violet2">violet2</a>
				<a href="javascript:void(0);" rel="style-violet3.css" class="color-box color-violet3">violet3</a>
				<a href="javascript:void(0);" rel="style-gray.css" class="color-box color-gray">gray</a>
				<a href="javascript:void(0);" rel="style-aqua.css" class="color-box color-aqua">aqua</a>
			</div>
			<div>
				<h2><?php echo translate_str_by_id(2587); ?></h2>
				<form id="header-option">
					<div class="radio">
						<input type="radio" name="headerRadio" id="header-full-radio" value="header-full" checked>
						<label for="header-full-radio"><?php echo translate_str_by_id(4789); ?></label>
					</div>
					<div class="radio">
						<input type="radio" name="headerRadio" id="header-full-dark-radio" value="header-full-dark">
						<label for="header-full-dark-radio"><?php echo translate_str_by_id(4790); ?></label>
					</div>
					<div class="radio">
						<input type="radio" name="headerRadio" id="no-header-radio" value="no-header">
						<label for="no-header-radio"><?php echo translate_str_by_id(4791); ?></label>
					</div>
				</form>
			</div>
			<div>
				<h2><?php echo translate_str_by_id(4792); ?></h2>
				<form id="navbar-option">
					<div class="radio">
						<input type="radio" name="navbarRadio" id="navbar-light-radio" value="navbar-light">
						<label for="navbar-light-radio"><?php echo translate_str_by_id(4793); ?></label>
					</div>
					<div class="radio">
						<input type="radio" name="navbarRadio" id="navbar-dark-radio" value="navbar-dark" checked>
						<label for="navbar-dark-radio"><?php echo translate_str_by_id(4794); ?></label>
					</div>
					<div class="radio">
						<input type="radio" name="navbarRadio" id="navbar-inverse-radio" value="navbar-inverse">
						<label for="navbar-inverse-radio"><?php echo translate_str_by_id(4788); ?></label>
					</div>
				</form>
			</div>
			<div id="width-options">
				<h2><?php echo translate_str_by_id(4795); ?></h2>
				<div class="btn-group">
					<form>
						<input type="checkbox" name="full-width-checkbox" data-label-width="80" data-label-text="Full Width" checked> 
					</form>
				</div>
			</div>
		</div>
		<div id="icon-options">
			<i class="fa fa-gears fa-2x fa-flip-horizontal"></i>
		</div>
	</div>
	<?php
}
?>
<div id="sb-site">
<div class="boxed">

<?php
//Стиль Header
$header_class = "";
$navbar_logo_class = "";//Подкласс для логотипа блока меню. В зависимости от наличия Header.
switch($DP_Template->data_value->header_style)
{
	case "light":
		$header_class = "hidden-xs header-full";
		$navbar_logo_class = " hidden-lg hidden-md hidden-sm";
		break;
	case "dark":
		$header_class = "hidden-xs header-full-dark";
		$navbar_logo_class = " hidden-lg hidden-md hidden-sm";
		break;
	case "no_header":
		$header_class = "hidden-xs header-full-dark hidden-sm hidden-md hidden-lg";
		$navbar_logo_class = "";
		break;
	default:
		$header_class = "hidden-xs header-full";
		$navbar_logo_class = " hidden-lg hidden-md hidden-sm";
}
?>

<header id="header-full-top" class="<?php echo $header_class; ?>">
    <div class="container">
        <div class="header-full-title">
            <h1 class="animated fadeInRight"><a href="/"><?php echo $DP_Template->data_value->logo_text1; ?> <span><?php echo $DP_Template->data_value->logo_text2; ?></span></a></h1>
            <p class="animated fadeInRight"><?php echo $DP_Template->data_value->slogan; ?></p>
        </div>
        <nav class="top-nav">
			
			
			<div class="dropdown animated fadeInDown animation-delay-13">
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/cart" title="<?php echo translate_str_by_id(4410); ?>">
					<i style="font-size:1.4em;" class="fa fa-shopping-cart"></i>
					<span id="header_cart_items_sum" class="hidden-xs"></span>
					<span class="badge badge-primary badge-round " id="header_cart_items_count"></span>
				</a>
				<script>
					//Функция обновления информации по корзине
					function updateCartInfoHeader(){
						
						//Создаем данные для формы
						var formData = new FormData();
						formData.append('csrf_guard_key', '<?php echo $user_session["csrf_guard_key"]; ?>');
						
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/content/shop/order_process/ajax_get_cart_info.php",
							data: formData,
							dataType: "json",
							success: function(answer){
								document.getElementById("header_cart_items_count").innerHTML = answer.cart_items_count;
								document.getElementById("header_cart_items_sum").innerHTML = answer.cart_items_sum;
								if( answer.cart_items_count == 0 ){
									document.getElementById("header_cart_items_count").setAttribute("class", "badge badge-default badge-round ");//Указатель количества
								}
								else{
									document.getElementById("header_cart_items_count").setAttribute("class", "badge badge-primary badge-round ");//Указатель количества
								}}});}

					updateCartInfoHeader();//После загрузки страницы обновляем модуль корзин
					//Функция показа лэйбла "Добавлено"
					//function showAdded(){return false;}//Расскомментировать если убрана нижняя панель
				</script>
			</div>
			
			
			<!-- Модуль индикации непросмотренных сообщений -->
			<div id="not_viewed_msg" class="dropdown animated fadeInDown animation-delay-13" style="display:none;">
				<a href="/shop/orders?read=0">
					<i style="font-size:1.3em;" class="fa fa-envelope"></i> <?php echo translate_str_by_id(3194); ?>  
					<span class="badge badge-primary badge-round " id="not_viewed_msg_count"></span>
				</a>
				<script>
					//Функция обновления информации о количестве непрочитанных сообщений
					function update_cnt_not_viewed_msg()
					{
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							dataType: "json",//Тип возвращаемого значения
							success: function(answer)
							{
								if(answer.status == 1)
								{
									if(answer.count > 0){
										document.getElementById("not_viewed_msg_count").innerHTML = answer.count;
										document.getElementById("not_viewed_msg").style.display = 'inline-block';
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
					var timerId_cnt_not_viewed_msg = setInterval(function() {
						update_cnt_not_viewed_msg();
					}, 400000);
				</script>
			</div>
			
			<!-- Модуль индикации непросмотренных сообщений по VIN запросам -->
			<div id="not_viewed_msg_vin" class="dropdown animated fadeInDown animation-delay-13" style="display:none;">
				<a href="/requests">
					<i style="font-size:1.3em;" class="fa fa-paper-plane"></i>  
					<span class="badge badge-primary badge-round " id="header_vin_count"></span>
				</a>
			</div>
			<script>
				//Функция обновления информации по корзине
				function update_cnt_not_viewed_msg_vin()
				{
					jQuery.ajax({
						type: "POST",
						async: true,
						url: "/content/requests/ajax_get_vin_info.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
						dataType: "json",
						success: function(answer){
							if(answer.count > 0){
								document.getElementById("header_vin_count").innerHTML = answer.count;
								document.getElementById("not_viewed_msg_vin").style.display = 'inline-block';
							}else{
								document.getElementById("not_viewed_msg_vin").style.display = 'none';
							}
						}
					});
				}

				update_cnt_not_viewed_msg_vin();//Запрос при загрузке страницы
				//Запускаем запросы непросмотренных сообщений
				var timerId_cnt_not_viewed_msg_vin = setInterval(function() {
					update_cnt_not_viewed_msg_vin();
				}, 400000);
			</script>

			<!-- Модуль индикации непросмотренных сообщений по возвратам -->
			<div id="not_viewed_msg_returns" class="dropdown animated fadeInDown animation-delay-13" style="display:none;">
				<a href="/shop/returns/returns_list?read=0">
					<i style="font-size:1.3em;" class="fa fa-envelope"></i> <?php echo translate_str_by_id(4030); ?>  
					<span class="badge badge-primary badge-round " id="not_viewed_msg_count_returns"></span>
				</a>
				<script>
					//Функция обновления информации о количестве непрочитанных сообщений
					function update_cnt_not_viewed_msg_returns()
					{
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/content/shop/order_process/ajax_get_cnt_not_viewed_msg.php?returns=1&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							dataType: "json",//Тип возвращаемого значения
							success: function(answer)
							{
								if(answer.status == 1)
								{
									if(answer.count > 0){
										document.getElementById("not_viewed_msg_count_returns").innerHTML = answer.count;
										document.getElementById("not_viewed_msg_returns").style.display = 'inline-block';
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
					var timerId_cnt_not_viewed_msg_returns = setInterval(function() {
						update_cnt_not_viewed_msg_returns();
					}, 400000);
				</script>
			</div>
			

            <div class="dropdown animated fadeInDown animation-delay-11">
				<?php
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
				$user_tab_caption = translate_str_by_id(3452);
				if( DP_User::getUserId() != 0 )
				{
					$userProfile = DP_User::getUserProfile();
					
					$user_tab_caption = $userProfile["name"]." ".$userProfile["surname"];
				}
				?>
                <a href="#" class="dropdown-toggle" data-toggle="dropdown"><i class="fa fa-user"></i> <?php echo $user_tab_caption; ?></a>
                <div class="dropdown-menu dropdown-menu-right dropdown-login-box animated flipCenter">
                    
					<?php
					//Единый механизм формы авторизации
					$login_form_postfix = "top_tab";
					require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
					?>
                </div>
            </div> <!-- dropdown -->

            <div class="dropdown animated fadeInDown animation-delay-13">
                <a href="#" class="dropdown-toggle" data-toggle="dropdown"><i class="fa fa-search"></i></a>
                <div class="dropdown-menu dropdown-menu-right dropdown-search-box animated fadeInUp">
                    <form role="form" action="/shop/part_search" method="GET">
                        <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
						<div class="input-group">
                            <input value="<?php echo $value_for_input_search; ?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4176); ?>" name="article" />
                            <span class="input-group-btn">
                                <button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
                            </span>
                        </div><!-- /input-group -->
                    </form>
                </div>
            </div> <!-- dropdown -->
			
			
			
			<?php
			$geo_point_class = 'hidden';
			$query_geo = $db_link->prepare("SELECT `activated` FROM `modules` WHERE `id` = ?;");
			$query_geo->execute( array(38) );
			$module_geo = $query_geo->fetch();
			if($module_geo['activated'] == 1)
			{
				$geo_point_class = '';
			}
			?>
			<div class="dropdown animated fadeInDown animation-delay-13 <?=$geo_point_class;?>">
				<div class="geo_point_wrap">
					<!-- Выбор географического узла -->
					<docpart type="module" name="geo_point" />
				</div>
			</div>
			
			
			
			<div class="dropdown animated fadeInDown animation-delay-13">
				<?php
				//Модуль выбора языка
				require( $_SERVER['DOCUMENT_ROOT'].'/modules/lang/module.php' );
				?>
			</div>
			
			
			
			
        </nav>
    </div> <!-- container -->
</header> <!-- header-full -->

<?php
//Стиль Navbar
$navbar_class = "";
switch($DP_Template->data_value->navbar_style)
{
	case "light":
		$navbar_class = "navbar-light";
		break;
	case "dark":
		$navbar_class = "navbar-dark";
		break;
	case "inverse":
		$navbar_class = "navbar-inverse";
		break;
	default:
		$navbar_class = "navbar-light";
}
?>

<nav class="navbar navbar-default navbar-header-full <?php echo $navbar_class; ?> yamm navbar-static-top" role="navigation" id="header">
    <div class="container">
        <!-- Brand and toggle get grouped for better mobile display -->
        <div class="navbar-header">
            <button type="button" class="navbar-toggle" data-toggle="collapse" data-target="#bs-example-navbar-collapse-1">
                <span class="sr-only">Toggle navigation</span>
                <i class="fa fa-bars"></i>
            </button>
            <a id="ar-brand" class="navbar-brand <?php echo $navbar_logo_class; ?>" href="/"><?php echo $DP_Template->data_value->logo_text1; ?> <span><?php echo $DP_Template->data_value->logo_text2; ?></span></a>
			
			
			<div class="lang_module_mobile hidden-lg hidden-md hidden-sm">
				<?php
				//Модуль выбора языка
				require( $_SERVER['DOCUMENT_ROOT'].'/modules/lang/module.php' );
				?>
			</div>
			<style>
			.lang_module_mobile
			{
				position:relative;
			}
			 .lang_module_mobile > .lang_select
			{
				position:absolute;
				top:14px;
				right:50px;
			}
			</style>
			
        </div> <!-- navbar-header -->

        <!-- Collect the nav links, forms, and other content for toggling -->

		<?php
		if($DP_Template->data_value->show_right_block == "on")
		{
			?>
			<div class="pull-right">
				<a href="javascript:void(0);" class="sb-icon-navbar sb-toggle-right"><i class="fa fa-bars"></i></a>
			</div>
			<?php
		}
		?>
        <div class="collapse navbar-collapse" id="bs-example-navbar-collapse-1">
            <docpart type="module" name="top_menu_expan" />
        </div><!-- navbar-collapse -->
    </div><!-- container -->
</nav>


<?php
if( ! $DP_Content->main_flag)
{
	?>
	<header class="main-header">
		<div class="container">
			<div class="col-lg-6">
				<h1 class="page-title"><?php echo $DP_Content->value; ?></h1>
			</div>
			<div class="col-lg-6">
				<docpart type="module" name="bread_crumbs" />
			</div>
		</div>
	</header>
	<?php
}
else
{
	?>
	<div style="margin-bottom:20px;">
	</div>
	<?php
}
?>


<?php
//Контроллер страницы - меняем верстку шаблона для некоторых страниц: главной, сравнения. Т.е. убираем левую колонку
if( ! isset($product_id) )
{
	$product_id = null;
}
if( $DP_Content->main_flag || 
	$DP_Content->id == 269 || 
	$DP_Content->id == 271 || 
	$DP_Content->id == 273 || 
	$DP_Content->id == 274 || 
	$DP_Content->id == 275 || 
	$DP_Content->id == 276 || 
	$DP_Content->id == 283 || 
	$DP_Content->id == 285 || 
	$DP_Content->id == 315 || 
	$DP_Content->id == 354 || 
	$DP_Content->id == 376 || 
	$DP_Content->id == 385 || 
	$DP_Content->id == 377 || 
	$product_id != 0 || 
	$DP_Content->url === "shop/part_search" || 
	isset($DP_Content->service_data["article_search_chpu"]) || 
	$DP_Content->url === "originalnye-katalogi" )
{
	$left_col_class = " class=\"hidden-xs hidden-sm hidden-md hidden-lg \"";
	$right_col_class = " class=\"col-md-12\"";
}
else
{
	$left_col_class = " class=\"col-md-3\"";
	$right_col_class = " class=\"col-md-9\"";
}
?>

<div class="container">
    <div class="row">
        <div<?php echo $left_col_class; ?> id="left_col">
			
			<?php
			//Если это страница категории товаров, то дополнительно выводим строку поиска по наименованию
			if( $DP_Content->content_type == "category" || $DP_Content->url =="shop/search" )
			{
				?>
				<form role="form" action="/shop/search" method="GET">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					<div class="input-group">
						<input value="<?php echo $value_for_input_search_string; ?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4796); ?>" name="search_string" />
						<span class="input-group-btn">
							<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
						</span>
					</div><!-- /input-group -->
				</form>
				<?php
			}
			else//Для всех остальных выводим строку поиска по артикулу
			{
				?>
				<form role="form" action="/shop/part_search" method="GET">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					<div class="input-group">
						<input value="<?php echo $value_for_input_search; ?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4176); ?>" name="article" />
						<span class="input-group-btn">
							<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
						</span>
					</div><!-- /input-group -->
				</form>
				<?php
			}
			?>
			
			<hr class="dotted">
			
			<docpart type="module" name="left_menu" />
		</div>
		
		<div<?php echo $right_col_class; ?> id="right_col">
            <div class="row" id="Container">
				<?php
				//Получаем дополнительный текст для URL
				$text_before_main = "";//Если текст нужен до основного содержимого
				$text_after_main = "";//Если текст нужен после основного содержимого
				$url = getPageUrl();
				
				$stmt = $db_link->prepare('SELECT * FROM `text_for_url` WHERE `url` = :url;');
				$stmt->bindValue(':url', $url);
				$stmt->execute();
				$url_text_record = $stmt->fetch(PDO::FETCH_ASSOC);
				
				if( $url_text_record != false )
				{
					if($url_text_record["before_main"] == 1)
					{
						$text_before_main = $url_text_record["content"];
					}
					else
					{
						$text_after_main = $url_text_record["content"];
					}
				}
				echo "<div class=\"row\" style=\"margin:0;\"><div class=\"col-lg-12\">".$text_before_main."</div></div>";
				?>
				<div class="col-lg-12">
				<?php
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий
				?>
				</div>
				<?php
				// Для некоторых страниц нужно дополнительно добавить обертку
				$add_row_div = 'style="margin:0;"';
				if( $product_id > 0 || $DP_Content->content_type == "category" || $DP_Content->id == 298 || $DP_Content->id == 302 || $DP_Content->id == 376 || $DP_Content->id == 385 || $DP_Content->id == 385 || isset($DP_Content->service_data["article_search_chpu"]))
				{
					$add_row_div = '';
				}
				?>
				<div class="row" <?=$add_row_div;?>>
				<div class="col-lg-12">
				<docpart type="main" name="main" />
				</div>
				</div>
				<?php
				echo "<div class=\"row\" style=\"margin:0;\"><div class=\"col-lg-12\">".$text_after_main."</div></div>";
				?>
			</div>
		</div>
	</div>
</div>



<?php
if($DP_Content->main_flag)
{
	//Секция "Отправить запрос продавцу"
	if(isset($DP_Config->section_send_request) && $DP_Config->section_send_request)
	{
		echo '<br/>';
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/vin_zapros/section_vin_request.php");
		?>
		<style>
		.section-vin .section-vin__info p{
			color:#fff;
		}
		.section-vin{
			margin-bottom: -30px;
		}
		</style>
		<?php
	}
}
?>



<aside id="footer-widgets">
    <div class="container">
        <div class="row">
            <div class="col-md-4">
                <h3 class="footer-widget-title"><?php echo translate_str_by_id(4797); ?></h3>
                <docpart type="module" name="footer_menu" />
                <h3 class="footer-widget-title"><?php echo translate_str_by_id(4798); ?></h3>
                <p><?php echo translate_str_by_id(4799); ?></p>
				<div class="input-group">
					<a class="btn btn-block btn-ar btn-primary" href="<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu"><?php echo translate_str_by_id(4800); ?></a>
                </div>
            </div>
            <div class="col-md-4">
                <div class="footer-widget">
                    <h3 class="footer-widget-title"><?php echo translate_str_by_id(4801); ?></h3>
                    <docpart type="module" name="news" />
                </div>
            </div>
            <div class="col-md-4">
                <div class="footer-widget">
					
					<h3 class="footer-widget-title"><?php echo translate_str_by_id(4802); ?></h3>
					<div>
						<script type="text/javascript" src="//yastatic.net/es5-shims/0.0.2/es5-shims.min.js" charset="utf-8"></script>
						<script type="text/javascript" src="//yastatic.net/share2/share.js" charset="utf-8"></script>
						<div class="ya-share2" data-services="vkontakte,odnoklassniki"></div>
					</div>
					
                </div>
				
				
            </div>
        </div> <!-- row -->
    </div> <!-- container -->
</aside> <!-- footer-widgets -->
<footer id="footer">
	<p>&copy; <?php echo date("Y", time()); ?> <a href="/">EpartsCart</a>. All rights reserved.
		<?php require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php'; echo epc_brand_hosted_by_html(); ?>
	<p>
</footer>

</div> <!-- boxed -->
</div> <!-- sb-site -->



<?php
if($DP_Template->data_value->show_right_block == "on")
{
	?>
	<div class="sb-slidebar sb-right">
		
		<div class="panel panel-primary">
		<?php
		//Единый механизм формы авторизации
		$login_form_postfix = "sb_right";
		require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
		?>
		</div>

		<form role="form" action="/shop/part_search" method="GET">
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		<div class="input-group">
			<input type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4176); ?>..." value="<?php echo $value_for_input_search; ?>" name="article" />
			<span class="input-group-btn">
				<button class="btn btn-default" type="submit"><i class="fa fa-search"></i></button>
			</span>
		</div><!-- /input-group -->
		</form>
		
		
		<h2 class="slidebar-header"><?php echo translate_str_by_id(4802); ?></h2>
		<div class="slidebar-social-icons">
			<script type="text/javascript" src="//yastatic.net/es5-shims/0.0.2/es5-shims.min.js" charset="utf-8"></script>
			<script type="text/javascript" src="//yastatic.net/share2/share.js" charset="utf-8"></script>
			<div class="ya-share2" data-services="vkontakte,odnoklassniki"></div>
		</div>
		
	</div> <!-- sb-slidebar sb-right -->
	<?php
}
?>



<?php
//Подключение скрипта нижней панели
require_once($_SERVER["DOCUMENT_ROOT"]."/modules/shop/bottom_panel/bottom_panel.php");
?>



<div id="back-top">
    <a href="#header"><i class="fa fa-chevron-up"></i></a>
</div>

<!-- Scripts -->
<!-- Compiled in vendors.js -->
<!--
<script src="assets/js/jquery.min.js"></script>
<script src="assets/js/jquery.cookie.js"></script>
<script src="assets/js/imagesloaded.pkgd.min.js"></script>
<script src="assets/js/bootstrap.min.js"></script>
<script src="assets/js/bootstrap-switch.min.js"></script>
<script src="assets/js/wow.min.js"></script>
<script src="assets/js/slidebars.min.js"></script>
<script src="assets/js/jquery.bxslider.min.js"></script>
<script src="assets/js/holder.js"></script>
<script src="assets/js/buttons.js"></script>
<script src="assets/js/jquery.mixitup.min.js"></script>
<script src="assets/js/circles.min.js"></script>
<script src="assets/js/masonry.pkgd.min.js"></script>
<script src="assets/js/jquery.matchHeight-min.js"></script>
-->

<!-- Это перенесли на верх
<script src="assets/js/vendors.js"></script>
-->


<script src="assets/js/styleswitcher.js"></script>

<!-- Syntaxhighlighter -->
<!--<script src="assets/js/syntaxhighlighter/shCore.js"></script>
<script src="assets/js/syntaxhighlighter/shBrushXml.js"></script>
<script src="assets/js/syntaxhighlighter/shBrushJScript.js"></script>-->
<script>SyntaxHighlighter = {all: function(){return;}};</script>

<script src="assets/js/DropdownHover.js"></script>
<script src="assets/js/app.js"></script>
<script src="assets/js/holder.js"></script>
<script src="assets/js/commerce.js"></script>

<script src="assets/js/e-commerce_product.js"></script>




<script>
var left_col_bottom_line = 0;//Y-координата нижней границы правого блока
//Обработка прокрутки - для сворачивания правого блока
window.onscroll = function() 
{
	return;//Пока эту возможность не используем
	
	var left_col_height = $("#left_col").height();//Высота правого блока
	if(left_col_bottom_line == 0)
	{
		left_col_bottom_line = document.getElementById("left_col").offsetTop + left_col_height;
	}

	//console.log(left_col_bottom_line + " " + window.pageYOffset);
	
	if( window.pageYOffset < left_col_bottom_line )//Прокрутили окно выше нижней границы правого блока
	{
		//Показываем правый блок
		document.getElementById("left_col").setAttribute("class", "col-md-3");
		document.getElementById("right_col").setAttribute("class", "col-md-9");
		
		//Обработка других элементов
		var products_divs = document.getElementsByClassName("product_div_tile");
		for(var i = 0; i < products_divs.length; i++)//Делаем по 4 блока на страницу
		{
			products_divs[i].setAttribute("class", "product_div_tile col-xs-12 col-sm-4 col-md-4 col-lg-3")
		}
	}
	else
	{
		//Скрываем правый блок
		document.getElementById("left_col").setAttribute("class", "hidden-xs hidden-sm hidden-md hidden-lg");
		document.getElementById("right_col").setAttribute("class", "col-md-12");
		
		//Обработка других элементов
		var products_divs = document.getElementsByClassName("product_div_tile");
		for(var i = 0; i < products_divs.length; i++)//Делаем по 5 блоков на страницу
		{
			products_divs[i].setAttribute("class", "product_div_tile col-xs-12 col-sm-4 col-md-3 col-lg-1-5")
		}
	}

}
</script>




<script type="text/javascript">
    $(function(){
       $('[rel="tooltip"]').tooltip();
       $('[rel="popover"]').popover();
    });
</script>




<?php
if($DP_Content->id == 349)
{
?>
<!-- Модальное Окно для страницы vin-запроса -->
<link href="/content/general_pages/vin_zapros/hystmodal.min.css" rel="stylesheet" type="text/css"/>
<script src="/content/general_pages/vin_zapros/hystmodal.min.js" ></script>
<div class="hystmodal" id="vinModal" aria-hidden="true">
	<div class="hystmodal__wrap">
		<div class="hystmodal__window" role="dialog" aria-modal="true">
			<button data-hystclose class="hystmodal__close"><?php echo translate_str_by_id(2447); ?></button>
			<!-- Ваш HTML код модального окна -->
			<div class="body_modal">
				<div class="text">
					<h4><?php echo translate_str_by_id(5653); ?></h4>
					<p><?php echo translate_str_by_id(5654); ?></p>
					<p><?php echo translate_str_by_id(5655); ?></p>
				</div>
				<div class="img"><img src="/content/general_pages/vin_zapros/vin.png" alt=""></div>
			</div>
		</div>
	</div>
</div>
<?php
}
?>





</body>
</html>