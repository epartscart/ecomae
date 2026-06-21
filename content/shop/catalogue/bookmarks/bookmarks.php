<?php
/*Скрипт для страницы закладок*/
defined('_ASTEXE_') or die('No access');


//Получить список магазинов покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");


//Техническая информация по интернет-магазину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");


//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$userProfile = DP_User::getUserProfile();
$group_id = $userProfile["groups"][0];//Берем первую группу пользователя


//Функция добавления в корзину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/common_add_to_basket.php");


//Подключаем скрипт с общей функцией вывода блока товара ( printProductBlock(product) )
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/helper.php");
//ТИП БЛОКА (1,2,3,4,5,6)
$product_block_type = 6;//ТИП ДЛЯ БЛОКОВ ТОВАРОВ В ЗАКЛАДКАХ


//СТИЛЬ БЛОКА ДЛЯ ТОВАРА
$products_style = "";
if( isset($_COOKIE["products_style"]) )
{
	$products_style = $_COOKIE["products_style"];
}
switch($products_style)
{
	case 1:
		$main_class_of_block = "product_div_tile col-xs-12 col-sm-4 col-md-4 col-lg-3";
		break;
	case 2:
		$main_class_of_block = "product_div_list_photo col-lg-12";//Список с фото
		break;
	case 3:
		$main_class_of_block = "product_div_list col-lg-12";//Список без фото
		break;
	default:
		$main_class_of_block = "product_div_tile col-xs-12 col-sm-4 col-md-4 col-lg-3";
}


//Получаем закладки
$bookmarks = NULL;
if(isset($_COOKIE["bookmarks"]))
{
	$bookmarks = $_COOKIE["bookmarks"];
}
if($bookmarks == NULL || $bookmarks == "[]")
{
?>
	
	<p><?php echo translate_str_by_id(4079); ?></p>
	<p><?php echo translate_str_by_id(4080); ?></p>
	
<?php
}
else//Есть закладки
{
	//ЗДЕСЬ ДЕЛАЕМ ЕДИНЫЙ SQL-ЗАПРОС НА ПОЛУЧЕНИЕ СПИСКА ТОВАРОВ
	$SQL = "";//Единственный запрос для получения всех нужных товаров
	$SQL_LIGHT = "";//ТОЖЕ, ЧТО И $SQL, ТОЛЬКО БЕЗ ЛИШНИЙ ПОЛЕЙ. ДЛЯ ПОДЗАПРОСА С LIMIT. НУЖЕН, ЧТОБЫ НЕ ДЕЛАТЬ ВЫБОРКУ ЛИШНИЙ ПОЛЕЙ ПРИ ОТСЕИВАНИИ ПО LIMIT
	
	//Приводим значения к INT - чтобы исключить SQL-инъекцию
	$bookmarks = json_decode($bookmarks, true);
	for($b=0; $b < count($bookmarks); $b++)
	{
		$bookmarks[$b] = (int)$bookmarks[$b];
	}
	$bookmarks = json_encode($bookmarks);
	
	$bookmarks = str_replace( array("[", "]"), "", $bookmarks);
	
	// Подстрока для запроса товаров
	$products_ids_str = $bookmarks;
	
	//Подключение построение запроса
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");
	
	//Подключаем скрипт генерации объектов товаров единого формата ( $products_objects )
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/generate_products_objects_by_sql.php");
	?>
	<div class="col-lg-12" id="products_area_turning">
		<div class="products_area_turning">
			<div class="showRestyle_name">Вид</div>
			<div class="showRestyle_wrap">
				<div class="showRestyle" id="showRestyle_1" onclick="showRestyle(1);"></div>
				<div class="showRestyle" id="showRestyle_2" onclick="showRestyle(2);"></div>
				<div class="showRestyle" id="showRestyle_3" onclick="showRestyle(3);"></div>
			</div>
		</div>
	</div>
	<script>
	<?php
	//Устновка стиля отображения товаров
    if(!empty($_COOKIE["products_style"]))
    {
        ?>
        var page_style = <?php echo (int)$_COOKIE["products_style"]; ?>;
        <?php
    }
    else
    {
        ?>
        var page_style = 1;
        <?php
    }
	?>
	document.getElementById("showRestyle_"+page_style).setAttribute("class", "showRestyle showRestyle_current");
	// -------------------------------------------------------------------------
	//Отобразить с другим стилем
	function showRestyle(style_code)
	{
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "products_style="+style_code+"; path=/; expires=" + date.toUTCString();
		
		//Перезагружаем страницу
		location.reload();
	}
	</script>
	
	
	
	<div class="col-lg-12">
	<?php
	foreach( $products_objects AS $id => $product )
	{
		echo printProductBlock($product);
	}
	?>
	</div>
	<?php
}
?>