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
 * 3. Список офисов, с которыми может работать данный менеджер
 * 
 * 4. Список ВСЕХ складов
 * 
 * 5. Список складов, с которыми может работать данный менеджер
 * 
*/
defined('_ASTEXE_') or die('No access');


//1.
$orders_statuses = array();

$orders_statuses_query = $db_link->prepare("SELECT * FROM `shop_orders_statuses_ref` ORDER BY `order` ASC;");
$orders_statuses_query->execute();
while($status = $orders_statuses_query->fetch() )
{
    $orders_statuses[$status["id"]] = array("name"=>$status["name"], "color"=>$status["color"]);
}



//2
$orders_items_statuses = array();
$orders_items_statuses_not_count = array();
$orders_items_statuses_query = $db_link->prepare("SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC;");
$orders_items_statuses_query->execute();
while($status = $orders_items_statuses_query->fetch() )
{
    //2.1
    $orders_items_statuses[$status["id"]] = array("name"=>$status["name"], "color"=>$status["color"]);
    
    //2.1
    if($status["count_flag"] == 0)
    {
        array_push($orders_items_statuses_not_count, $status["id"]);
    }
}



//3.
$offices_list = array();//ID=>Название
$user_id = DP_User::getAdminId();

$is_orders_full_admin = false;
if (!empty($GLOBALS['epc_orders_smoke_all_offices'])) {
	$offices_query = $db_link->prepare("SELECT `id`,`caption` FROM `shop_offices` ORDER BY `id` ASC;");
	$offices_query->execute();
	while ($office = $offices_query->fetch()) {
		$offices_list[$office['id']] = $office['caption'];
	}
} else {
$admin_profile = DP_User::getAdminProfile();
if(is_array($admin_profile) && !empty($admin_profile["groups"]))
{
    $admin_groups = array_map('intval', $admin_profile["groups"]);
    $admin_groups_placeholders = implode(',', array_fill(0, count($admin_groups), '?'));
    $admin_group_query = $db_link->prepare("SELECT COUNT(*) FROM `groups` WHERE `id` IN (".$admin_groups_placeholders.") AND (`value` LIKE ? OR `for_backend` = 1);");
    $admin_group_args = $admin_groups;
    $admin_group_args[] = '%Администратор%';
    $admin_group_query->execute($admin_group_args);
    $is_orders_full_admin = ((int)$admin_group_query->fetchColumn() > 0);
}

if($is_orders_full_admin)
{
    $offices_query = $db_link->prepare("SELECT `id`,`caption` FROM `shop_offices` ORDER BY `id` ASC;");
    $offices_query->execute();
}
else
{
    $offices_query = $db_link->prepare("SELECT `id`,`caption` FROM `shop_offices` WHERE `users` LIKE ?;");
    $offices_query->execute( array('%"'.$user_id.'"%') );
}
while( $office = $offices_query->fetch() )
{
    $offices_list[$office["id"]] = $office["caption"];
}
}






//4.
$storages_list = array();//ID=>Название
$storages_query = $db_link->prepare("SELECT `id`,`name` FROM `shop_storages`;");
$storages_query->execute();
while( $storage = $storages_query->fetch() )
{
    $storages_list[$storage["id"]] = $storage["name"];
}



//5.
$available_storages_list = array();//ID название
$available_storages_query = $db_link->prepare("SELECT `id`,`name` FROM `shop_storages` WHERE `users` LIKE ?;");
$available_storages_query->execute( array('%'.$user_id.'%') );
while( $storage = $available_storages_query->fetch() )
{
    $available_storages_list[$storage["id"]] = $storage["name"];
}
?>