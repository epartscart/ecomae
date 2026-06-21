<?php
//Серверный скрипт для получения JSON-описания материалов постранично. Используется на странице создания/редактирования одного материала для выбора родительского узла
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;


if( $_GET["code"] != $DP_Config->secret_succession )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit( json_encode($answer) );
}


//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------




// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



//Получаем записи материалов. Все, кроме самого материала и вложенных в него


//Исходные данные
$content_id = $_GET["content_id"];
$is_frontend = $_GET["is_frontend"];
$s_page = $_GET["s_page"];
$list_page_limit = (int)$DP_Config->list_page_limit;
$from = (int)$s_page*$list_page_limit;


//Получаем максимальный уровень вложенности материалов
$max_level_query = $db_link->prepare("SELECT MAX(`level`) AS `max_level` FROM `content` WHERE `is_frontend`= ?;");
if( ! $max_level_query->execute( array($is_frontend) ) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2103);
	exit( json_encode($answer) );
}

$max_level_record = $max_level_query->fetch();
if($max_level_record == false)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2104);
	exit( json_encode($answer) );
}
$max_level = $max_level_record["max_level"];

//Формируем SQL-запрос для получения записей в виде древовидной структуры
$SQL = "SELECT ";
$SQL_fields = "";
$SQL_joins = "";
for($l=1; $l <= $max_level; $l++)
{
	if( $l > 1 )
	{
		$SQL_fields = $SQL_fields.",";
		
		$l_last = $l -1;
		
		$SQL_joins = $SQL_joins." LEFT JOIN `content` AS `t$l` ON `t$l`.`parent` = `t$l_last`.`id` ";
	}
	
	
	$SQL_fields = $SQL_fields."
	`t$l`.`id` AS `l".$l."_id`,
	`t$l`.`value` AS `l".$l."_value`,
	`t$l`.`level` AS `l".$l."_level`,
	`t$l`.`parent` AS `l".$l."_parent`";
}
//Собираем строку запроса
$SQL = $SQL.$SQL_fields." FROM `content` AS `t1` ".$SQL_joins." WHERE `t1`.`parent` =0 AND `t1`.`is_frontend`=? LIMIT $from, $list_page_limit";


//Еще нужно получить общее количество - для вывода переключателей страниц. Сначала - для пагинации ($count_total_for_pagination) - значение может отличаться от $count_total, т.к. страницы выводятся в иерархическом виде
$SQL_count_total = "SELECT COUNT(`t1`.`id`) AS `count_total` FROM `content` AS `t1` ".$SQL_joins." WHERE `t1`.`parent` =0 AND `t1`.`is_frontend`=?;";
$count_total_query = $db_link->prepare($SQL_count_total);
if( ! $count_total_query->execute( array($is_frontend) ) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2105);
	exit( json_encode($answer) );
}
$count_total_record = $count_total_query->fetch();
if($count_total_record == false)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2106);
	exit( json_encode($answer) );
}
$count_total_for_pagination = $count_total_record["count_total"];
//Теперь получаем количество элементов всего ($count_total):
$SQL_count_total = "SELECT COUNT(*) AS `count_total` FROM `content` WHERE `is_frontend`=?;";
$count_total_query = $db_link->prepare($SQL_count_total);
if( ! $count_total_query->execute( array($is_frontend) ) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2107);
	exit( json_encode($answer) );
}
$count_total_record = $count_total_query->fetch();
if($count_total_record == false)
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2108);
	exit( json_encode($answer) );
}
$count_total = $count_total_record["count_total"];





//echo $SQL;
//Выполняем запрос
$content_array = array();
$elements_query = $db_link->prepare($SQL);
if( ! $elements_query->execute( array($is_frontend) ) )
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = translate_str_by_id(2109);
	exit( json_encode($answer) );
}
$already_shown = array();//Фильтр - для уже показанных материалов
while( $element_record = $elements_query->fetch() )
{
	//Мультиязычность
	for($l=0; $l <= $max_level; $l++)
	{
		if( isset( $element_record["l".$l."_value"] ) )
		{
			$element_record["l".$l."_value"] = translate_str_by_id($element_record["l".$l."_value"]);
		}
	}
	
	for($l=1; $l <= $max_level; $l++)
	{
		if($element_record["l".$l."_id"] == NULL)
		{
			break;//К следующей ветке
		}
		
		//Такой узел уже был показан выше
		if( array_search((int)$element_record["l".$l."_id"], $already_shown) === false )
		{
			array_push($already_shown, (int)$element_record["l".$l."_id"]);
		}
		else
		{
			continue;
		}
		
		array_push($content_array, $element_record);
	}
}//for




$answer = array();
$answer["status"] = true;
$answer["message"] = "";
$answer["content"] = $content_array;
$answer["max_level"] = $max_level;
$answer["count_total_for_pagination"] = $count_total_for_pagination;
$answer["count_total"] = $count_total;
$answer["list_page_limit"] = $list_page_limit;
exit( json_encode($answer) );
?>