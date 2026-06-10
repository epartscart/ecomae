<?php
/**
 * Серверный скрипт для получения страницы товаров
*/

//Скрипт для формирования списка id товаров
require_once("ajax_get_products_list.php");


//Подключаем скрипт с общей функцией вывода блока товара ( printProductBlock(product) )
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/helper.php");


if(count($products_objects) == 0)
{
    ?>
    <div style="text-center"><?php echo translate_str_by_id(4078); ?></div>
    <?php
    exit();
}
else
{
	foreach( $products_objects AS $product_id => $product )
	{
		printProductBlock($product);
	}
    exit();
}
?>