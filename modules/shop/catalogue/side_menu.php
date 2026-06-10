<?php
/**
 * Скрипт модуля, который выводит категории каталога в виде бокового меню
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
require_once($_SERVER["DOCUMENT_ROOT"]."/modules/shop/catalogue/printCatalogueNode.php");//Определение функции для вывода категорий в виде бокового меню

$catalogue_tree_dump_PHP = json_decode($catalogue_tree_dump_JSON, true);

?>
<div id="cssmenu">
<?php
printCatalogueNode($catalogue_tree_dump_PHP, 1);
?>
</div>
