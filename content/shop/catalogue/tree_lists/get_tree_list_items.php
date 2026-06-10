<?php
/**
Скрипт для получения иерархии элементов одного определенного двевовидного списка
*/
defined('_ASTEXE_') or die('No access');

//Подключение определений методов
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/tree_lists/helper.php" );


// -------- Start --------
//1. Создаем пустые переменные для получения текущей конфигурации элементов
$root_item = new DP_TreeListItem;//Корень дерева - объект иерархии - полностью описывает иерархическую структуру элементов (используем для нее объект Элемент)
$tree_list_dump_JSON = json_encode($root_item->data, true);//Строка с JSON-дампом (Пустая по умолчанию)


//2. SELECT из таблицы элементов всех записей, упорядочненных по полю level
$all_items_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id ORDER BY `level`, `order`');
$all_items_query->bindValue(':tree_list_id', $needed_tree_list_id);
$all_items_query->execute();
if( $all_items_query->fetchColumn() > 0 )
{
	$all_items_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id ORDER BY `level`, `order`');
	$all_items_query->bindValue(':tree_list_id', $needed_tree_list_id);
	$all_items_query->execute();
	//Обрабатываем результат запроса по циклу:
	while( $item_record = $all_items_query->fetch() )
	{
		//- создаем объект категории
		$current_item = new DP_TreeListItem;
		$current_item->id = (integer)$item_record["id"];
		$current_item->count = (integer)$item_record['count'];
		$current_item->level = (integer)$item_record['level'];
		$current_item->value_lang_str_id = $item_record["value"];
		$current_item->value = translate_str_by_id($item_record["value"]);
		$current_item->alias = (string)$item_record["alias"];
		$current_item->url = (string)$item_record["url"];
		$current_item->parent = (integer)$item_record['parent'];
		$current_item->image = $item_record["image"];
		$current_item->order = $item_record["order"];
		$current_item->open = (bool)$item_record["open"];
		
		//Добавляем объект элемента в объект иерархии
		$root_item->data = addItemToDump($current_item, $root_item->data, $root_item->id);
	}//~for($trl_i)
	
	//3. Преобразовываем в $root_item->data в JSON, добавляем знак $ к названиям некоторых полей и выдаем в javascript
	$tree_list_dump_JSON = json_encode($root_item->data, true);
	
	$sweep=array('"level"');
	$tree_list_dump_JSON = str_replace($sweep, '"$level"', $tree_list_dump_JSON);
	$sweep=array('"parent"');
	$tree_list_dump_JSON = str_replace($sweep, '"$parent"', $tree_list_dump_JSON);
	$sweep=array('"count"');
	$tree_list_dump_JSON = str_replace($sweep, '"$count"', $tree_list_dump_JSON);
	//var_dump($tree_list_dump_JSON);
}
// -------- End --------
?>