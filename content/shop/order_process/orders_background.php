<?php
/**
 * Скрипт для формирования технических данных, необходимых при работе с заказами:
 * 1. Список статусов заказов
 * 
 * 
 * 2. Статусы позиций заказов:
 *  2.1 Список всех статусов позиций заказов
 *  2.2 Список статусов позиций заказов, для которых НЕ выстален флаг "Учитывать при ценовых расчетах"
 * 
 * 
 * 3. Список офисов
 * 
*/

//1.
$orders_statuses = array();
$orders_statuses_query = $db_link->prepare('SELECT * FROM `shop_orders_statuses_ref` ORDER BY `order` ASC;');
$orders_statuses_query->execute();
while( $status = $orders_statuses_query->fetch() )
{
    //$orders_statuses[$status["id"]] = array("name"=>$status["name"], "color"=>$status["color"]);
    $orders_statuses[$status["id"]] = $status;
}



//2
$orders_items_statuses = array();
$orders_items_statuses_not_count = array();
$orders_items_statuses_query = $db_link->prepare('SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC;');
$orders_items_statuses_query->execute();
while($status = $orders_items_statuses_query->fetch() )
{
    //2.1
    //$orders_items_statuses[$status["id"]] = array("name"=>$status["name"], "color"=>$status["color"]);
    $orders_items_statuses[$status["id"]] = $status;
    
    //2.1
    if($status["count_flag"] == 0)
    {
        array_push($orders_items_statuses_not_count, $status["id"]);
    }
}



//3.
$offices_list = array();//ID=>Название
$offices_query = $db_link->prepare('SELECT `id`,`caption` FROM `shop_offices`;');
$offices_query->execute();
while( $office = $offices_query->fetch() )
{
    $offices_list[$office["id"]] = $office;
}
?>