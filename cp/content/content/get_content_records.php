<?php
/**
 * Скрипт формирует иерархическое описание созданных материалов
 * 
 * Скрипт содержит опеределения:
 * - отдельный метод addContentToDump() для построения PHP-объекта иерархии материалов на основе класса DP_ContentRecord
 * 
 * Скрипт производит:
 * - построение объекта иерархии $content_tree_dump_JSON - который полностью описывает дамп, выводимый в дерево категорий
*/
defined('_ASTEXE_') or die('No access');


//Режим редактирования Фронтэнд/Бэкэнд
$edit_mode = null;
if( isset($_COOKIE["edit_mode"]) )
{
	$edit_mode = $_COOKIE["edit_mode"];
}
switch($edit_mode)
{
    case "frontend":
        $is_frontend = 1;
        break;
    case "backend":
        $is_frontend = 0;
        break;
    default:
        $is_frontend = 1;
        break;
}



// --------------------------------- Start PHP - метод ---------------------------------
//Метод добавит категорию в массив - когда идет построение иерархии при загрузке страницы
//Метод 100% дает результат, но есть недочет - этот метод, после того, как найдет, куда добавить объект категории, продолжит выполнять рекурсивные вызовы пока не переберет все существующие варианты.
function addContentToDump($content, $candidate_data, $candidate_id)
{
    //Знаем уровень level
    if($content->parent == $candidate_id)
    {
        array_push($candidate_data, $content);
        return $candidate_data;//Возвращаем массив с добавленной катерией
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
            $candidate_data[$i]->data = addContentToDump($content, $candidate_data[$i]->data, $candidate_data[$i]->id);
        }
        return $candidate_data;
    }
}
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~




// -------- Start ЗАГРУКА ТЕКУЩЕЙ КОНФИГУРАЦИИ МАТЕРИАЛОВ --------
//1. Создаем пустые переменные для получения текущей конфигурации материалов
$root_content = new DP_ContentRecord;//Корень дерева - объект иерархии - полностью описывает иерархическую структуру материалов (используем для нее объект Материал)
$content_tree_dump_JSON = json_encode($root_content->data, true);//Строка с JSON-дампом конфигурации материалов (Пустая по умолчанию)

//2. SELECT из таблицы материалов всех записей, упорядочненных по полю level
$all_content_query = $db_link->prepare("SELECT COUNT(*) FROM `content` WHERE `is_frontend`=? ORDER BY `level`, `order`;");
$all_content_query->execute( array($is_frontend) );

if($all_content_query->fetchColumn() > 0)
{
	$all_content_query = $db_link->prepare("SELECT * FROM `content` WHERE `is_frontend`=? ORDER BY `level`, `order`;");
	$all_content_query->execute( array($is_frontend) );
	
    //Обрабатываем результат запроса по циклу:
    while( $content_record = $all_content_query->fetch() )
    {
        //- создаем объект материала
        $current_content = new DP_ContentRecord;
        $current_content->id = (integer)$content_record["id"];
        $current_content->count = (integer)$content_record['count'];
        $current_content->url = $content_record["url"];
        $current_content->level = (integer)$content_record['level'];
        $current_content->alias = $content_record["alias"];
        //$current_content->value = translate_str_by_id($content_record["value"]);
        $current_content->parent = (integer)$content_record['parent'];
        //$current_content->description = translate_str_by_id($content_record["description"]);
        $current_content->main_flag = $content_record["main_flag"];
        //$current_content->title_tag = translate_str_by_id($content_record["title_tag"]);
        //$current_content->description_tag = translate_str_by_id($content_record["description_tag"]);
        //$current_content->keywords_tag = translate_str_by_id($content_record["keywords_tag"]);
        //$current_content->author_tag = translate_str_by_id($content_record["author_tag"]);
        $current_content->robots_tag = $content_record["robots_tag"];
        $current_content->modules_array = json_decode($content_record["modules_array"], true);
        $current_content->system_flag = $content_record["system_flag"];
        $current_content->published_flag = $content_record["published_flag"];
        $current_content->open = (bool)$content_record["open"];
        $current_content->css_js = $content_record["css_js"];
        
		
		//Мультиязычность
		for( $i=0 ; $i < count( $current_content->translated_items ) ; $i++ )
		{
			$current_content->{$current_content->translated_items[$i]."_lang_str_id"} = $content_record[$current_content->translated_items[$i]];//ID строки
			$current_content->{$current_content->translated_items[$i]} = translate_str_by_id($content_record[$current_content->translated_items[$i]]);//Ее перевод
		}
		
		
        //- делаем запрос SELECT для допущенных групп пользователей
        $content_access_query = $db_link->prepare("SELECT * FROM `content_access` WHERE `content_id`=?;");
		$content_access_query->execute( array( $current_content->id) );
        while( $content_access_record = $content_access_query->fetch() )
        {
            array_push($current_content->groups_access, $content_access_record["group_id"]);
        }
        
        //Добавляем объект категории в объект иерархии
        $root_content->data = addContentToDump($current_content, $root_content->data, $root_content->id);
    }//~for($i)
    
    //3. Преобразовываем в $root_content->data в JSON, добавляем знак $ к названиям некоторых полей и выдаем в javascript
    $content_tree_dump_JSON = json_encode($root_content->data, true);
    
    $sweep=array('"level"');
    $content_tree_dump_JSON = str_replace($sweep, '"$level"', $content_tree_dump_JSON);
    $sweep=array('"parent"');
    $content_tree_dump_JSON = str_replace($sweep, '"$parent"', $content_tree_dump_JSON);
    $sweep=array('"count"');
    $content_tree_dump_JSON = str_replace($sweep, '"$count"', $content_tree_dump_JSON);
    //var_dump($content_tree_dump_JSON);
}

// -------- End ЗАГРУКА ТЕКУЩЕЙ КОНФИГУРАЦИИ КАТЕГОРИЙ --------
?>