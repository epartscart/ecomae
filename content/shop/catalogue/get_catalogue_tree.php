<?php
/**
 * Скрипт формирует иерархическое описание созданных категорий каталога
 * 
 * Скрипт содержит опеределения:
 * - отдельный метод addCategoryToDump() для построения PHP-объекта иерархии категорий на основе класса DP_CatalogueCategory
 * 
 * Скрипт производит:
 * - построение объекта иерархии $catalogue_tree_dump_JSON - который полностью описывает дамп, выводимый в дерево категорий
*/
defined('_ASTEXE_') or die('No access');

$time = 0;

$pages_with_full_dump = array();//Список ID страниц, для которых готовить полный дамп каталога
//292 - Карта сайта (бэкенд)
//351 - Редактор товаров на главной (бэкенд)
//352 - Редактор сопутствующих товаров (бэкенд)

// Если нужно сформировать дерево только отображаемых для клиента категорий (используется при формировании меню категорий)
$where_published = '';
if( isset($where_published_flag) )
{
	if($where_published_flag === true)
	{
		$where_published = 'WHERE `published_flag` = 1';
	}
}

// --------------------------------- Start PHP - метод ---------------------------------
//Метод добавит категорию в массив - когда идет построение иерархии при загрузке страницы
//Метод 100% дает результат, но есть недочет - этот метод, после того, как найдет, куда добавить объект категории, продолжит выполнять рекурсивные вызовы пока не переберет все существующие варианты.
function addCategoryToDump($category, $candidate_data, $candidate_id)
{
    //Знаем уровень level
    if($category->parent == $candidate_id)
    {
        array_push($candidate_data, $category);
        
        return $candidate_data;//Возвращаем массив с добавленной категорией
    }
    else
    {
        for($i=0; $i < count($candidate_data); $i++)
        {
            if($candidate_data[$i]->count == 0)
            {
                continue;
            }
            $current_count = count($candidate_data[$i]->data);//Сколько элементов в массиве до рекурсивного вызова
            $candidate_data[$i]->data = addCategoryToDump($category, $candidate_data[$i]->data, $candidate_data[$i]->id);
        }
        return $candidate_data;
    }
}
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~








// --------------------------------- Start PHP - метод ---------------------------------
//Метод добавляем товары в категорию
function addProductsToCategory($category_id, $level, $category_url)
{
    global $db_link;
    global $DP_Config;
    global $time;
	
	if($time == 0) $time = time();//Текущее время для формирования id элементов дерева
    
    $products_array = array();
    
	
	$products_query = $db_link->prepare('SELECT * FROM `shop_catalogue_products` WHERE `category_id` = :category_id;');
    $products_query->bindValue(':category_id', $category_id);
	$products_query->execute();
    while($product = $products_query->fetch())
    {
        //- создаем объект. Используем для него класс категории
        $current_product = new DP_CatalogueCategory;
        $current_product->id = $time;//Ставится ГАРАНТИРОВАННО уникальный ID в рамках всего дерева (т.е. категории и товары)
        $current_product->alias = $product["alias"];
        $current_product->count = 0;
        $current_product->level = $level;
        $current_product->value = $product["caption"];
        $current_product->parent = $category_id;
		
		//Поля, которые используются только для товара
		$current_product->is_product = true;
        $current_product->product_id = $product["id"];
		
        //URL товара
        if($DP_Config->product_url == "id")
        {
            $current_product->url = $multilang_params['lang_href'].$category_url."/".$product["id"];
        }
        else
        {
            $current_product->url = $multilang_params['lang_href'].$category_url."/".$product["alias"];
        }
        
        array_push($products_array, $current_product);
        
        $time++;
    }
    
    return $products_array;
}
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~















// -------- Start ЗАГРУКА ТЕКУЩЕЙ КОНФИГУРАЦИИ КАТАЛОГА --------
//1. Создаем пустые переменные для получения текущей конфигурации категорий
$root_category = new DP_CatalogueCategory;//Корень дерева - объект иерархии - полностью описывает иерархическую структуру категорий (используем для нее объект Категория)
$catalogue_tree_dump_JSON = '[]';//Строка с JSON-дампом конфигурации каталога (пустая по умолчанию)


//2. SELECT из таблицы категорий всех записей, упорядочненных по полю level
$all_categories_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_catalogue_categories` '.$where_published.' ORDER BY `level`, `order`;');
$all_categories_query->execute();
if($all_categories_query->fetchColumn() > 0)
{
	$all_categories_query = $db_link->prepare('SELECT * FROM `shop_catalogue_categories` '.$where_published.' ORDER BY `level`, `order`;');
	$all_categories_query->execute();
	
	//Обрабатываем результат запроса по циклу:
    while($category_record = $all_categories_query->fetch())
    {
        //- создаем объект категории
        $current_category = new DP_CatalogueCategory;
        $current_category->id = (integer)$category_record["id"];
        $current_category->alias = $category_record["alias"];
        $current_category->url = $category_record["url"];
        $current_category->count = (integer)$category_record['count'];
        $current_category->level = (integer)$category_record['level'];
        //$current_category->value_lang_str_id = $category_record["value"];
        //$current_category->value = translate_str_by_id($category_record["value"]);
        $current_category->parent = (integer)$category_record['parent'];
        //$current_category->title_tag = translate_str_by_id($category_record["title_tag"]);
        //$current_category->description_tag = translate_str_by_id($category_record["description_tag"]);
        //$current_category->keywords_tag = translate_str_by_id($category_record["keywords_tag"]);
        $current_category->robots_tag = $category_record["robots_tag"];
        $current_category->import_format = $category_record["import_format"];
        $current_category->export_format = $category_record["export_format"];
        $current_category->image = $category_record["image"];
		$current_category->order = $category_record["order"];
        $current_category->published_flag = $category_record["published_flag"];
		
		
		//Мультиязычность
		for( $i=0 ; $i < count( $current_category->translated_items ) ; $i++ )
		{
			$field = $current_category->translated_items[$i];
			$rawValue = $category_record[$field];
			$current_category->{$field . "_lang_str_id"} = $rawValue;
			$label = translate_str_by_id($rawValue);
			if ($label === '' || preg_match('/^\?+$/u', $label)) {
				$label = is_numeric($rawValue) ? '' : trim((string) $rawValue);
			}
			if ($label === '' && !empty($category_record['alias'])) {
				$slug = preg_replace('/^apai-[a-z0-9_]+-/', '', (string) $category_record['alias']);
				$label = ucwords(str_replace('-', ' ', $slug));
			}
			$current_category->{$field} = $label;
		}
		
		
        //Получаем свойства категории (skip for header menu — avoids N+1 queries on page load)
        if (empty($catalogue_tree_skip_properties)) {
		$category_properties_query = $db_link->prepare('SELECT * FROM `shop_categories_properties_map` WHERE `category_id` = :category_id ORDER BY `order`;');
		$category_properties_query->bindValue(':category_id', $current_category->id);
		$category_properties_query->execute();
        while($property = $category_properties_query->fetch() )
        {
			//Мультиязычность
			$property['value_lang_str_id'] = $property['value'];//ID строки
			$property['value'] = translate_str_by_id($property['value']);//Перевод на текущий язык
			
            array_push($current_category->properties, $property);
        }
        }
        
        //Если вложенных категорий нет и определена переменная "Полный каталог" - добавляем товары
        if($current_category->count == 0 && array_search($DP_Content->id, $pages_with_full_dump) !== false)
        {
            $current_category->data = addProductsToCategory($current_category->id, $current_category->level+1, $current_category->url);
        }
        
        
        
        //Добавляем объект категории в объект иерархии
        $root_category->data = addCategoryToDump($current_category, $root_category->data, $root_category->id);
    }//~for($i)
    
    //3. Преобразовываем в $root_category->data в JSON, добавляем знак $ к названиям некоторых полей и выдаем в javascript
    $encoded = json_encode($root_category->data, JSON_UNESCAPED_UNICODE | JSON_INVALID_UTF8_SUBSTITUTE);
    $catalogue_tree_dump_JSON = ($encoded !== false) ? $encoded : '[]';
    
    $sweep=array('"level"');
    $catalogue_tree_dump_JSON = str_replace($sweep, '"$level"', $catalogue_tree_dump_JSON);
    $sweep=array('"parent"');
    $catalogue_tree_dump_JSON = str_replace($sweep, '"$parent"', $catalogue_tree_dump_JSON);
    $sweep=array('"count"');
    $catalogue_tree_dump_JSON = str_replace($sweep, '"$count"', $catalogue_tree_dump_JSON);
    //var_dump($catalogue_tree_dump_JSON);
}




// -------- End ЗАГРУКА ТЕКУЩЕЙ КОНФИГУРАЦИИ КАТЕГОРИЙ --------
?>