<?php
/**
 * Скрипт для установки cookie стиля отображения товаров
*/

if( ! isset($_GET["products_style"]) )
{
	exit;
}

//Защита от XSS
$products_style = htmlentities( (int)$_GET["products_style"] );
if( $products_style != 1 && $products_style != 2 && $products_style != 3 )
{
	exit;
}


$cookietime = time()+9999999;//Запоминаем на долго
setcookie("products_style", $products_style, $cookietime, "/");

echo json_encode($products_style);
?>