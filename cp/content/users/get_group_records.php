<?php
/**
 * Скрипт формирует иерархическое описание созданных групп
 * 
 * Скрипт содержит опеределения:
 * - отдельный метод addGroupToDump() для построения PHP-объекта иерархии групп на основе класса DP_GroupRecord
 * 
 * Скрипт производит:
 * - построение объекта иерархии $group_tree_dump_JSON - который полностью описывает дамп, выводимый в дерево категорий
*/
defined('_ASTEXE_') or die('No access');


// --------------------------------- Start PHP - метод ---------------------------------
//Метод добавит группу в массив - когда идет построение иерархии при загрузке страницы
//Метод 100% дает результат, но есть недочет - этот метод, после того, как найдет, куда добавить объект группы, продолжит выполнять рекурсивные вызовы пока не переберет все существующие варианты.
function addGroupToDump($group, $candidate_data, $candidate_id)
{
    //Знаем уровень level
    if($group->parent == $candidate_id)
    {
        array_push($candidate_data, $group);
        return $candidate_data;//Возвращаем массив с добавленной группой
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
            $candidate_data[$i]->data = addGroupToDump($group, $candidate_data[$i]->data, $candidate_data[$i]->id);
        }
        return $candidate_data;
    }
}
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~




// -------- Start ЗАГРУКА ТЕКУЩЕЙ КОНФИГУРАЦИИ ГРУПП --------
//1. Создаем пустые переменные для получения текущей конфигурации групп
$root_group = new DP_GroupRecord;//Корень дерева - объект иерархии - полностью описывает иерархическую структуру групп (используем для нее объект Записи группы)
$group_tree_dump_JSON = json_encode($root_group->data, true);//Строка с JSON-дампом конфигурации групп (Пустая по умолчанию)

//2. SELECT из таблицы групп всех записей, упорядочненных по полю level
$all_groups_query = $db_link->prepare("SELECT COUNT(*) FROM `groups` ORDER BY `level`, `order`;");
$all_groups_query->execute();

if( $all_groups_query->fetchColumn() > 0)
{
	$all_groups_query_2 = $db_link->prepare("SELECT * FROM `groups` ORDER BY `level`, `order`;");
	$all_groups_query_2->execute();
	
    //Обрабатываем результат запроса по циклу:
    while( $group_record = $all_groups_query_2->fetch() )
    {	
        //- создаем объект группы
        $current_group = new DP_GroupRecord;
        $current_group->id = (integer)$group_record["id"];
        $current_group->value_lang_str_id = $group_record["value"];
        $current_group->value = translate_str_by_id($group_record["value"]);
        $current_group->count = (integer)$group_record['count'];
        $current_group->level = (integer)$group_record['level'];
        $current_group->parent = (integer)$group_record['parent'];
        $current_group->unblocked = $group_record['unblocked'];
        $current_group->for_guests = $group_record['for_guests'];
        $current_group->for_registrated = $group_record['for_registrated'];
        $current_group->for_backend = $group_record['for_backend'];
		$current_group->for_percentage = (int)$group_record["for_percentage"];
        $current_group->description_lang_str_id = $group_record["description"];
        $current_group->description = translate_str_by_id($group_record["description"]);
        //Добавляем объект группы в объект иерархии
        $root_group->data = addGroupToDump($current_group, $root_group->data, $root_group->id);
    }//~for($i)
    
    //3. Преобразовываем в $root_group->data в JSON, добавляем знак $ к названиям некоторых полей и выдаем в javascript
    $group_tree_dump_JSON = json_encode($root_group->data, true);
    
    $sweep=array('"level"');
    $group_tree_dump_JSON = str_replace($sweep, '"$level"', $group_tree_dump_JSON);
    $sweep=array('"parent"');
    $group_tree_dump_JSON = str_replace($sweep, '"$parent"', $group_tree_dump_JSON);
    $sweep=array('"count"');
    $group_tree_dump_JSON = str_replace($sweep, '"$count"', $group_tree_dump_JSON);
    //var_dump($group_tree_dump_JSON);
}

// -------- End ЗАГРУКА ТЕКУЩЕЙ КОНФИГУРАЦИИ КАТЕГОРИЙ --------
?>