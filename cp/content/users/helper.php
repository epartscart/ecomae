<?php
/**
 * Скрипт со вспомогательными возможностями пакета "Пользователи"
*/


//Рекурсивная функция получения вложенных групп
//Для использования нужно создать (переинициализировать два массива)
//$one_root_groups = array();//Список групп одной ветви, т.е. с единым корнем. В первый вызов функции - передается корень ветви
//Технология использования функции:
//0. Переинициализация $one_root_groups = array();
//1. Добавить в $one_root_groups корневую группу
//2. Вызвать getInsertedGroups(), передав единственный аргумент - корневую группу
//3. После завершения работы функции, список $one_root_groups будет содержать все группы с единым корнем
function getInsertedGroups($group)
{
    global $db_link;
    global $DP_Config;
    global $one_root_groups;
    
	$group_query = $db_link->prepare("SELECT * FROM `groups` WHERE `id` = ?;");
	$group_query->execute( array($group) );
    $group_record = $group_query->fetch();
    if($group_record["count"] == 0)
    {
        return;
    }
    else
    {
		$inserted_groups_query = $db_link->prepare("SELECT * FROM `groups` WHERE `parent` = ?;");
		$inserted_groups_query->execute( array($group) );
        while( $inserted_group_record = $inserted_groups_query->fetch() )
        {
            //Если этой группы нет в списке - добавляем
            if(array_search($inserted_group_record["id"], $one_root_groups) === false)
            {
                array_push($one_root_groups, $inserted_group_record["id"]);
            }
            getAllowedGroups($inserted_group_record["id"]);//Рекурсивный вызов для этой группы
        }
    }
}
?>