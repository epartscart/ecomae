<?php
/**
 * Скрипт главной страницы интернет-магазина
*/
defined('_ASTEXE_') or die('No access');

$category_block_type = 1;//Тип блоков категорий - для управления наличием (используется в /content/shop/catalogue/printCategories.php)
$category_id = 0;//Выводим корень каталог


//API UCatalog
//require_once($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/ucatalog_index.php");


//Search Tab
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/search_tabs/search_tabs.php");


//Подключение виджета каталога levam
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/levam/levam.php");


//Подключение встроенного слайдера с редактором из ПУ
require_once($_SERVER["DOCUMENT_ROOT"]."/modules/slider/slider.php");


//Каталоги Ucats
if($DP_Template->id != 63){
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/ucats/catalogues.php");
}


$stmt = $db_link->prepare('SELECT COUNT(`id`) AS `count_id` FROM `shop_catalogue_categories` WHERE `published_flag` = :published_flag AND `parent` = :parent;');
$stmt->bindValue(':published_flag', 1);
$stmt->bindValue(':parent', 0);
$stmt->execute();
$check_categories_exist_record = $stmt->fetch(PDO::FETCH_ASSOC);
if( $check_categories_exist_record["count_id"] > 0 )
{
	?>
	<div class="row">
	<div class="col-xs-12 col-sm-12 col-md-12 col-lg-12" style="margin-bottom: -10px;">
	<h2 class="section-title"><?php echo translate_str_by_id(3994); ?></h2>
	<?php
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printCategories.php");
	?>
	</div>
	</div>
	<?php
}



echo '<div class="row">';
//Подключение вывода специального поиска
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printSpecialSearches.php");
echo '</div>';



echo '<div class="row">';
//Товары на главной странице
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/special_suggestions.php");
echo '</div>';


?>