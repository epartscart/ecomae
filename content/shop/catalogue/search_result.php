<?php
/**
 * Страничный скрипт для вывода товаров при обращении черезе поисковую строку
*/
defined('_ASTEXE_') or die('No access');

$product_block_type = 4;//Режим работы скрипта - запрос по наименованию через строку поиска
$category_id = 0;


//В зависимости от выбранного способа отображения каталога
if( $DP_Config->catalogue_html_way == "async" )
{
	//Асинхронная загрузка товаров на страницу
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProducts.php");
}
else
{
	//По умолчанию - пагинация
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProducts_2.php");
}
?>