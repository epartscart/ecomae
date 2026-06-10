<?php
/**
 * Скрипт для получения магазинов покупателя
*/

//Получаем географический узел покупателя
$geo_id = NULL;
if(isset($_COOKIE["my_city"]))
{
	$geo_id = $_COOKIE["my_city"];
}


//Куки не были еще выставлены - выводим для самого первого гео-узла, чтобы хоть что-то показать
if($geo_id == NULL)
{
	$min_geo_id_query = $db_link->prepare('SELECT MIN(`id`) AS `id` FROM `shop_geo`;');
	$min_geo_id_query->execute();
	$min_geo_id_record = $min_geo_id_query->fetch();
	$geo_id = $min_geo_id_record["id"];
}

//Получаем список магазинов для данного географического узла
$customer_offices = array();
$offices_query = $db_link->prepare('SELECT `office_id` FROM `shop_offices_geo_map` WHERE `geo_id` = :geo_id;');
$offices_query->bindValue(':geo_id', $geo_id);
$offices_query->execute();
while($office = $offices_query->fetch())
{
    array_push($customer_offices, $office["office_id"]);
}


//Обработка некорректных настроек админа. Если так получилось, что он удалил гео-узлы, а у покупателя остался в куки удаленный гео-узел или админ вообще не привязал точки обслуживания ни к одному из гео-узлов, то выполняем следующие действия
if( count($customer_offices) == 0 )
{
	//В список точек выдачи (пустой) добавляем первую точку выдачи
	$offices_query = $db_link->prepare('SELECT `id` FROM `shop_offices` ORDER BY `id` LIMIT 1;');
	$offices_query->execute();
	if( $office = $offices_query->fetch() )
	{
		array_push($customer_offices, $office["id"]);
	}
}
?>