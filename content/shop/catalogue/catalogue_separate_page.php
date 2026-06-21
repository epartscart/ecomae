<?php
/**
 * Страничный скрипт для вывода отдельной страницы с каталогом
*/
defined('_ASTEXE_') or die('No access');


$category_block_type = 1;//Тип блоков категорий - для управления наличием (используется в /content/shop/catalogue/printCategories.php)
$category_id = 0;//Выводим корень каталог

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printCategories.php");
?>