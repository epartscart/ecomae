<?php
defined('_ASTEXE_') or die('No access');
?>
<!doctype html>
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
	<base href="/templates/limo/">
    <meta charset="utf-8">
    <!--Mobile Specific Meta Tag-->
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <!--Favicon-->
    <link rel="shortcut icon" href="/favicon.ico" type="image/x-icon">
    <link rel="icon" href="/favicon.ico" type="image/x-icon">
    <!--Master Slider Styles-->
    <link href="masterslider/style/masterslider.css" rel="stylesheet" media="screen">
    <!--Styles-->
    <link href="css/styles.css" rel="stylesheet" media="screen">
    
	
	
	<?php
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getUserSession();
	
	
	//Определение цветовых стилей
	if( $DP_Template->data_value->show_options_block == "on" )//Приоритет куки перед настройкой цвета в ПУ
	{
		$limo_main_color = "";
		if( $DP_Template->data_value->main_color == NULL && $_COOKIE["limo_main_color"] == NULL )
		{
			$limo_main_color = "scheme2";
		}
		else if($_COOKIE["limo_main_color"] != NULL)
		{
			$limo_main_color = $_COOKIE["limo_main_color"];
		}
		else if( $DP_Template->data_value->main_color != NULL )
		{
			$limo_main_color = $DP_Template->data_value->main_color;
		}
		else
		{
			$limo_main_color = "scheme2";
		}
	}
	else//Приоритет настройки цвета в ПУ перед куки
	{
		$limo_main_color = "";
		if( $DP_Template->data_value->main_color == NULL && $_COOKIE["limo_main_color"] == NULL )
		{
			$limo_main_color = "scheme2";
		}
		else if( $DP_Template->data_value->main_color != NULL )
		{
			$limo_main_color = $DP_Template->data_value->main_color;
		}
		else if($_COOKIE["limo_main_color"] != NULL)
		{
			$limo_main_color = $_COOKIE["limo_main_color"];
		}
		else
		{
			$limo_main_color = "scheme2";
		}
	}
	$limo_main_color = str_replace('"','',$limo_main_color);
	?>
	<!--Color Scheme-->
    <link class="color-scheme" href="css/colors/color-<?php echo $limo_main_color; ?>.css" rel="stylesheet" media="screen">
    
	
	<!--Color Switcher-->
    <link href="color-switcher/color-switcher.css" rel="stylesheet" media="screen">
    <!--Modernizr-->
		<script src="js/libs/modernizr.custom.js"></script>
    <!--Adding Media Queries Support for IE8-->
    <!--[if lt IE 9]>
      <script src="js/plugins/respond.js"></script>
    <![endif]-->
	
	
	<script src="/lib/jQuery/jQuery.js"></script>
	<docpart type="head" name="head" />
	<link rel="stylesheet" href="/templates/<?php echo $DP_Template->name; ?>/css/docpart/style.css" type="text/css" />
	<script src="/lib/jQuery_ui/jquery-ui.js"></script>
	<link href="/lib/jQuery_ui/jquery-ui.css" rel="stylesheet">
	<link href="https://fonts.googleapis.com/css?family=PT+Sans:regular,italic,bold,bolditalic" rel="stylesheet" type="text/css" />
	
	<link rel="stylesheet" href="/templates/limo/css/shop/geo.css" type="text/css" />
	<link rel="stylesheet" href="/templates/limo/css/catalogue/catalogue.css" type="text/css" />
	<script src="/templates/limo/jquery-ui/jquery-ui-<?php echo $limo_main_color; ?>/jquery-ui.js"></script>
	<link href="/templates/limo/jquery-ui/jquery-ui-<?php echo $limo_main_color; ?>/jquery-ui.css" rel="stylesheet">
	<link href="css/astself.css" rel="stylesheet">
	
	<link href="/modules/slider/css/style.css" rel="stylesheet" type="text/css" />
	<?php require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php'; echo epc_brand_hosted_by_css_link_html(); ?>
  </head>

  <!--Body-->
  <body>
<?php require($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/google_translate_top.php"); ?>
<?php
//Переменные для подстановки в input модулей поисковых строк
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/search_strs_for_inputs.php");
?>
	

	<?php
	$show_options_block = "on";
	if($DP_Template->data_value->show_options_block != NULL)
	{
		$show_options_block = $DP_Template->data_value->show_options_block;
	}
	if($show_options_block == "on")
	{
		?>
		<div class="color-switcher group animated">
		  <div class="toggle"><i class="fa fa-cog"></i></div>
			
		  <div class="color">
			<a style="background-color: #607d8b;" class="current" href="#" data-color="default"></a>
			<span>#607d8b</span>
		  </div>

		  <div class="color">
			<a style="background-color: #ff5722;" href="#" data-color="scheme2"></a>
			<span>#ff5722</span>
		  </div>

		  <div class="color">
			<a style="background-color: #e91e63;" href="#" data-color="scheme3"></a>
			<span>#e91e63</span>
		  </div>

		  <div class="color">
			<a style="background-color: #ff9016;" href="#" data-color="scheme4"></a>
			<span>#ff9016</span>
		  </div>

		  <div class="color">
			<a style="background-color: #8bc34a;" href="#" data-color="scheme5"></a>
			<span>#8bc34a</span>
		  </div>

		  <div class="color">
			<a style="background-color: #03a9f4;" href="#" data-color="scheme6"></a>
			<span>#03a9f4</span>
		  </div>
		  
		  
		  <div style="color:#FFF;font-size:12px;text-align:center;border-top:2px solid #FFF;margin-top:4px;padding-top:4px;">
				<?php echo translate_str_by_id(4803); ?>
			</div>
		</div>
		<?php
	}
	?>
	
	
     
  
  	
	
	
	
    <div class="modal fade" id="loginModal" tabindex="-1" role="dialog" aria-hidden="true">
      <div class="modal-dialog">
        <div class="modal-content">
			<?php
			//Единый механизм формы авторизации
			$login_form_postfix = "top_tab";
			require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
			?>
        </div>
      </div>
    </div>
	
	
	

    <!--Header-->
    <header data-offset-top="500" data-stuck="600"><!--data-offset-top is when header converts to small variant and data-stuck when it becomes visible. Values in px represent position of scroll from top. Make sure there is at least 100px between those two values for smooth animation-->
    
      <!--Search Form-->
		<form class="search-form closed" method="get" role="form" autocomplete="off" action="/shop/part_search">
			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
			<div class="container">
				<div class="close-search"><i class="icon-delete"></i></div>
				<div class="form-group">
					<label class="sr-only" for="search-hd"><?php echo translate_str_by_id(4804); ?></label>
					<input value="<?php echo $value_for_input_search; ?>" type="text" class="form-control" name="article" id="search-hd" placeholder="<?php echo translate_str_by_id(4804); ?>">
					<button type="submit"><i class="icon-magnifier"></i></button>
				</div>
			</div>
		</form>
	  
	  
		
	  
	  
	  
    
    	<!--Mobile Menu Toggle-->
      <div class="menu-toggle"><i class="fa fa-list"></i></div>
		
		<?php
		$logo_text1 = "L";
		if($DP_Template->data_value->logo_text1 != NULL)
		{
			$logo_text1 = $DP_Template->data_value->logo_text1;
		}
		$logo_text2 = "ogo";
		if($DP_Template->data_value->logo_text2 != NULL)
		{
			$logo_text2 = $DP_Template->data_value->logo_text2;
		}
		?>
		
      <div class="container">
		<a class="logo" href="/">
			<span><?php echo $logo_text1; ?></span><?php echo $logo_text2; ?>
		</a>
      </div>
      
	  
	  <div class="container">
		<div class="head_phone">
			<?php echo $DP_Template->data_value->header_phone; ?>
		</div>
      </div>
	  
	  
	  
	  
      <!--Main Menu-->
      <nav class="menu">

        <div class="catalog-block">
          <div class="container">
			<docpart type="module" name="top_menu_expan" />
          </div>
        </div>
      </nav>
      
      <div class="toolbar-container">
        <div class="container">  
          <!--Toolbar-->
          <div class="toolbar group">
		  
			<!-- START Модуль индикации непросмотренных сообщений -->
			<a id="not_viewed_msg" style="display:none; position: relative; top: -3px;" href="/shop/orders?read=0">
				<i class="fa fa-envelope"></i> <span style="margin-left:0;"><b id="not_viewed_msg_count"></b></span>
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
				var timerId = setInterval(function() {
					update_cnt_not_viewed_msg();
				}, 400000);
			</script>
			
			<!-- Модуль индикации непросмотренных сообщений по VIN запросам -->
			<a id="not_viewed_msg_vin" style="display:none; position: relative; top: -3px;" href="/requests">
				<i class="fa fa-paper-plane"></i> <span style="margin-left:0;"><b id="header_vin_count"></b></span>
			</a>
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
			
			<!-- START Модуль индикации непросмотренных сообщений по возвратам -->
			<a id="not_viewed_msg_returns" style="display:none; position: relative; top: -3px;" href="/shop/returns/returns_list?read=0">
				<i class="fa fa-reply"></i> <span style="margin-left:0;"><b id="not_viewed_msg_count_returns"></b></span>
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
		  
            <?php
			if( DP_User::getUserId() == 0 )
			{
				?>
				<a class="login-btn btn-outlined-invert" href="#" data-toggle="modal" data-target="#loginModal">
					<i class="icon-profile"></i> <?php echo translate_str_by_id(4805); ?>
				</a>
				<?php
			}
			else
			{
				?>
				<a class="login-btn btn-outlined-invert" href="#" data-toggle="modal" data-target="#loginModal">
					<i class="icon-profile"></i> <?php echo translate_str_by_id(4806); ?>
				</a>
				<?php
			}
			?>
			

            <a class="btn-outlined-invert" href="wishlist.html"><i class="icon-heart"></i> <span><b>W</b>ishlist</span></a>   

			
			
			
			
			<div class="btn-outlined-invert" style="display:inline;">
				<a href="<?php echo $multilang_params['lang_href']; ?>/shop/cart" title="<?php echo translate_str_by_id(4410); ?>">
					<i style="font-size:1.4em; bottom: 3px; position: relative;" class="fa fa-shopping-cart"></i>
					<span style="font-weight: bold;" id="header_cart_items_sum" class="hidden-xs"></span>
					<span style="line-height:1; color:#333 !important;" class="badge badge-primary badge-round " id="header_cart_items_count"></span>
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
							data: formData,
							url: "/content/shop/order_process/ajax_get_cart_info.php",
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
			
			
			

            <button class="search-btn btn-outlined-invert"><i class="icon-magnifier"></i></button>
			
			<docpart type="module" name="geo_point" />
			
			<div style="display:inline-block;">
			<?php
			//Модуль выбора языка
			require( $_SERVER['DOCUMENT_ROOT'].'/modules/lang/module.php' );
			?>
			<style>
			.lang_select
			{
				width:50px!important;
			}
			</style>
			</div>
			
			
          </div><!--Toolbar Close-->
        </div>
      </div>
    </header><!--Header Close-->
    
	
	
	<?php
	$page_content_style = "";
	if( ! $DP_Content->main_flag)
	{
		$page_content_style = " style=\"padding-top:50px!important;\"";
	}
	?>
	
	
    <!--Page Content-->
    <div class="page-content" <?php echo $page_content_style; ?>>
    
    	
		<?php
		if(false)
		{
			?>
			<section class="hero-slider">
				<div class="master-slider" id="hero-slider">
					<div class="ms-slide" data-delay="7">
					<div class="overlay"></div>
					<img src="masterslider/blank.gif" data-src="img/hero/slideshow/slide_1.jpg" alt=""/>
					<h2 style="width: 456px; left: 110px; top: 110px;" class="dark-color ms-layer" data-effect="top(50,true)" data-duration="700" data-delay="250" data-ease="easeOutQuad">Look for all bags at our shop!</h2>
					<p style="width: 456px; left: 110px; top: 210px;" class="dark-color ms-layer" data-effect="back(500)" data-duration="700" data-delay="500" data-ease="easeOutQuad">In this slider (which works both on touch screen and desktop devices) you can change the title, the description and button texts. It's all that you need to demonstrate your top rated products. </p>
					<div style="left: 110px; top: 300px;" class="ms-layer button" data-effect="left(50,true)" data-duration="500" data-delay="750" data-ease="easeOutQuad"><a class="btn btn-black" href="#">Go to catalog</a></div>
					<div style="left: 350px; top: 300px;" class="ms-layer button" data-effect="bottom(50,true)" data-duration="700" data-delay="950" data-ease="easeOutQuad"><a class="btn btn-primary" href="#">Browse all</a></div>
				  </div>
				  
					<!--Slide 2-->
					<div class="ms-slide" data-delay="7">
					<span class="overlay"></span>
					<img src="masterslider/blank.gif" data-src="img/hero/slideshow/slide_2.jpg" alt="Necessaire"/>
					<h2 style="width: 456px; left: 110px; top: 110px;" class="dark-color ms-layer" data-effect="bottom(50,true)" data-duration="700" data-delay="250" data-ease="easeOutQuad">Necessaire</h2>
					<p style="width: 456px; left: 110px; top: 210px;" class="dark-color ms-layer" data-effect="bottom(50,true)" data-duration="700" data-delay="500" data-ease="easeOutQuad">In this slider (which works both on touch screen and desktop devices) you can change the title, the description and button texts. It's all that you need to demonstrate your top rated products. </p>
					<div style="left: 110px; top: 330px;" class="ms-layer button" data-effect="left(50,true)" data-duration="500" data-delay="750" data-ease="easeOutQuad"><a class="btn btn-black" href="#">Go to catalog</a></div>
					<div style="left: 350px; top: 330px;" class="ms-layer button" data-effect="bottom(50,true)" data-duration="700" data-delay="950" data-ease="easeOutQuad"><a class="btn btn-primary" href="#">Browse all</a></div>
				  </div>
				  
					<!--Slide 3-->
					<div class="ms-slide" data-delay="7">
					<div class="overlay"></div>
					<img src="masterslider/blank.gif" data-src="img/hero/slideshow/slide_3.jpg" alt="Crescent"/>
					<h2 style="width: 456px; left: 110px; top: 110px;" class="dark-color ms-layer" data-effect="left(50,true)" data-duration="700" data-delay="250" data-ease="easeOutQuad">Crescent</h2>
					<p style="width: 456px; left: 110px; top: 210px;" class="dark-color ms-layer" data-effect="left(50,true)" data-duration="700" data-delay="500" data-ease="easeOutQuad">In this slider (which works both on touch screen and desktop devices) you can change the title, the description and button texts. It's all that you need to demonstrate your top rated products. </p>
					<div style="left: 110px; top: 330px;" class="ms-layer button" data-effect="left(50,true)" data-duration="500" data-delay="750" data-ease="easeOutQuad"><a class="btn btn-black" href="#">Go to catalog</a></div>
					<div style="left: 350px; top: 330px;" class="ms-layer button" data-effect="bottom(50,true)" data-duration="700" data-delay="950" data-ease="easeOutQuad"><a class="btn btn-primary" href="#">Browse all</a></div>
				  </div>
				  
				</div>
			  </section>
			<?php
		}
		?>
		
      
	 
	 
<?php
if( ! $DP_Content->main_flag)
{
	?>
	<div class="container">
		<div class="col-lg-6">
			<h1 class="page-title"><?php echo $DP_Content->value; ?></h1>
		</div>
		<div class="col-lg-6">
			<docpart type="module" name="bread_crumbs" />
		</div>
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
        <div <?php echo $left_col_class; ?> id="left_col">
			
			<?php
			//Если это страница категории товаров, то дополнительно выводим строку поиска по наименованию
			if( $DP_Content->content_type == "category" || $DP_Content->url =="shop/search" )
			{
				?>
				<form role="form" action="/shop/search" method="GET" class="search-form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					<div class="form-group">
						<label class="sr-only" for="search-hd1"><?php echo translate_str_by_id(4807); ?></label>
						<input value="<?php echo $value_for_input_search_string; ?>" type="text" class="form-control" name="search_string" id="search-hd1" placeholder="<?php echo translate_str_by_id(4807); ?>">
						<button type="submit"><i class="icon-magnifier"></i></button>
					</div>
				</form>
				<?php
			}
			else
			{
				?>
				<form role="form" action="/shop/part_search" method="GET" class="search-form">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					<div class="form-group">
						<label class="sr-only" for="search-hd1"><?php echo translate_str_by_id(4808); ?></label>
						<input value="<?php echo $value_for_input_search; ?>" type="text" class="form-control" name="article" id="search-hd1" placeholder="<?php echo translate_str_by_id(4808); ?>">
						<button type="submit"><i class="icon-magnifier"></i></button>
					</div>
				</form>
				<?php
			}
			?>
			
			<hr class="dotted">
			
			<docpart type="module" name="left_menu" />
		</div>
		
		<div <?php echo $right_col_class; ?> id="right_col">
            <div class="row" id="Container">
				<?php
				//Получаем дополнительный текст для URL
				$text_before_main = "";//Если текст нужен до основного содержимого
				$text_after_main = "";//Если текст нужен после основного содержимого
				$url = getPageUrl();
				
				$stmt = $db_link->prepare('SELECT * FROM `text_for_url` WHERE `url` = :url;');
				$stmt->bindValue(':url', $url);
				$stmt->execute();
				$url_text_record = $stmt->fetch();

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
			margin-bottom: -10px;
		}
		</style>
		<?php
	}
}
?>


</div><!--Page Content Close-->
    
    <!--Sticky Buttons-->
    <div class="sticky-btns">
    	<!--
		<form class="quick-contact" method="post" name="quick-contact">
      	<h3>Contact us</h3>
        <p class="text-muted">Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do.</p>
        <div class="form-group">
        	<label for="qc-name">Full name</label>
          <input class="form-control input-sm" type="text" name="qc-name" id="qc-name" placeholder="Enter full name" required>
        </div>
        <div class="form-group">
        	<label for="qc-email">Email</label>
          <input class="form-control input-sm" type="email" name="qc-email" id="qc-email" placeholder="Enter email" required>
        </div>
        <div class="form-group">
        	<label for="qc-message">Your message</label>
          <textarea class="form-control input-sm" name="qc-message" id="qc-message" placeholder="Enter your message" required></textarea>
        </div>
        <input class="btn btn-black btn-sm btn-block" type="submit" value="Send">
      </form>
    	<span id="qcf-btn"><i class="fa fa-envelope"></i></span>
		-->
		
      <span id="scrollTop-btn"><i class="fa fa-chevron-up"></i></span>
    </div><!--Sticky Buttons Close-->
    
    
      
  	<!--Footer-->
    <footer class="footer">
    	<div class="container">
      	<div class="row">
        	<div class="col-lg-4 col-md-3">
          	<div class="info_footer">
              
			  
			
			<a class="logo" href="/">
				<span><?php echo $logo_text1; ?></span><?php echo $logo_text2; ?>
				</a>
			  
				<p><?php echo $DP_Template->data_value->footer_description; ?></p>
			  
			  
              <div class="social ya-share-footer">
				
				
				<?php
				//Вывод блока поделиться
				$share_str = "vkontakte,odnoklassniki";
				if($share_str != "")
				{
					?>
					<script type="text/javascript" src="//yastatic.net/es5-shims/0.0.2/es5-shims.min.js" charset="utf-8"></script>
					<script type="text/javascript" src="//yastatic.net/share2/share.js" charset="utf-8"></script>
					<div class="ya-share2" data-services="<?php echo $share_str; ?>"></div>
					<?php
				}
				?>
              </div>
            </div>
          </div>
          <div class="col-lg-4 col-md-3">
          	<h2><?php echo translate_str_by_id(4809); ?></h2>
            <docpart type="module" name="news" />
          </div>
          <div class="contacts col-lg-4 col-md-3">
          	<h2><?php echo translate_str_by_id(4810); ?></h2>
            <p class="p-style3">
            	<p><?php echo $DP_Template->data_value->footer_contacts; ?></p>
            </p>
			
          </div>
        </div>
        <div class="copyright">
        	<div class="row">
          	<div class="col-lg-7 col-md-7 col-sm-7">
              <p>&copy; <?php echo date("Y", time()); ?> <a href="/">EpartsCart</a>. All rights reserved.</p>
			  <div style="margin-top:4px;font-size:12px;opacity:.85;">
				<?php require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php'; echo epc_brand_hosted_by_html(); ?>
			  </div>
            </div>
          	<div class="col-lg-5 col-md-5 col-sm-5">
            	<?php
				if( $DP_Template->data_value->footer_payments == "on" )
				{
					?>
					<div class="payment">
						<img src="img/payment/visa.png" alt="Visa"/>
						<img src="img/payment/master.png" alt="Master Card"/>
					</div>
					<?php
				}
				?>
            </div>
          </div>
        </div>
      </div>
    </footer><!--Footer Close-->
    
	
	
	
	<?php
	//Подключение скрипта нижней панели
	require_once($_SERVER["DOCUMENT_ROOT"]."/modules/shop/bottom_panel/bottom_panel.php");
	?>
	
	
	

	
	
    <!--Javascript (jQuery) Libraries and Plugins-->
		<!--<script src="js/libs/jquery-1.11.1.min.js"></script>
		<script src="js/libs/jquery-ui-1.10.4.custom.min.js"></script>-->
    <script src="js/libs/jquery.easing.min.js"></script>
		<script src="js/plugins/bootstrap.min.js"></script>
		<script src="js/plugins/smoothscroll.js"></script>
		<script src="js/plugins/jquery.validate.min.js"></script>
		<script src="js/plugins/icheck.min.js"></script>
		<script src="js/plugins/jquery.placeholder.js"></script>
		<script src="js/plugins/jquery.stellar.min.js"></script>
		<script src="js/plugins/jquery.touchSwipe.min.js"></script>
		<script src="js/plugins/jquery.shuffle.min.js"></script>
    <script src="js/plugins/lightGallery.min.js"></script>
    <script src="js/plugins/owl.carousel.min.js"></script>
    <script src="js/plugins/masterslider.min.js"></script>
		<script src="js/scripts.js"></script>

    <script src="color-switcher/color-switcher.js"></script>
	
	
	<script src="/templates/limo/bxslider/jquery.bxslider.min.js"></script>
	<link rel="stylesheet" href="/templates/limo/bxslider/jquery.bxslider.min.css" type="text/css" />
	
	<script>
	$(document).ready(function(){
	  $('.bxslider').bxSlider();
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
	
	

  </body><!--Body Close-->
</html>
