<?php
header('Content-Type: application/json;charset=utf-8;');
if($_GET["initiator"] != 1 && $_GET["initiator"] != 4)
{
	exit();
} 

/**
 * Серверный скрипт для изменения статуса заказа
 * 
 * //Инициаторы:
 * 1 - менеджер
 * 4 - скрипт (например, при оплате заказа)
 * 
*/

require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------



//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );


$result = array();//Результат работы


//Входные данные:
$initiator = $_GET["initiator"];
$orders = json_decode($_GET["orders"], true);
$status = $_GET["status"];


//ПРОВЕРКА ПРАВ
if($initiator == 1)
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
    //Проверяем право менеджера
    if( ! DP_User::isAdmin())
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));//Вообще не является администратором бэкенда
    }
}
else if($initiator == 4)
{
    if($_GET["key"] != $DP_Config->tech_key)
    {
        $result["status"] = false;
        $result["message"] = "Wrong key";
        $result["code"] = 503;
        exit(json_encode($result));
    }
}




//ДАЛЕЕ САМ АЛГОРИТМ
// -----------------------------------------------------------------------------------------------------------
//1. Массив покупателей и менеджеров по заказам
$orders_data = array();//Ассоциативный массив Заказ=>[покупатель, офис]
for($i=0; $i < count($orders); $i++)
{
    //1. Получаем информацию по заказам:
	$order_query = $db_link->prepare('SELECT `user_id` AS `customer`, `paid` AS `paid`, `email_not_auth`, `phone_not_auth`, (SELECT `users` FROM `shop_offices` WHERE `id`=`shop_orders`.`office_id`) AS `managers` FROM `shop_orders` WHERE `id`= ?;');
	$order_query->execute( array($orders[$i]) );
    $order_record = $order_query->fetch();
    $orders_data[$orders[$i]] = array("customer"=>$order_record["customer"], "paid"=>$order_record["paid"], "managers"=>json_decode($order_record["managers"], true), "email_not_auth"=>$order_record["email_not_auth"], "phone_not_auth"=>$order_record["phone_not_auth"]);    
}

// -----------------------------------------------------------------------------------------------------------

//АВТОМАТИЧЕСКАЯ СМЕНА СТАТУСОВ ПО УСЛОВИЯМ

//Выполняется только при условии $initiator == 1 то есть когда запрос отправил менеджер, а не робот из другого скрипта.
//Код выполняется до смены самого статуса заказа что бы отменить сему в случае ошибки смены статуса позиций, поэтому ниже код переносить нельзя.

if($initiator == 1)
{
	//Статусы заказов:
	
	//Получаем список статусов заказа с флагом "for_finish" - заказ выполнен
	$orders_statuses_for_finish = array();
	$query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1;");
	$query->execute();
	while($status_row = $query->fetch() )
	{
		$orders_statuses_for_finish[] = $status_row["id"];
	}
	
	//Получаем список статусов заказа с флагом "for_inverse" - заказ отменен
	$orders_statuses_for_inverse = array();
	$query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1;");
	$query->execute();
	while($status_row = $query->fetch() )
	{
		$orders_statuses_for_inverse[] = $status_row["id"];
	}
	
	//Статусы позиций:
	
	//Получаем список статусов позиций с флагом "for_inverse" - заказ отменен
	$orders_items_statuses_for_inverse = array();
	$query = $db_link->prepare("SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0 ORDER BY `order` ASC;");
	$query->execute();
	while($status_row = $query->fetch() )
	{
		$orders_items_statuses_for_inverse[] = $status_row["id"];
	}
	
	//Получаем список статусов позиций с флагом "for_finish" - позиция выдана
	$orders_items_statuses_for_finish = array();
	$query = $db_link->prepare("SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC;");
	$query->execute();
	while($status_row = $query->fetch() )
	{
		$orders_items_statuses_for_finish[] = $status_row["id"];
	}
	
	// ...........
	
		//Если менеджер меняет статус заказа на "Отменен" - Автоматически меняем статус всех позиций на "Отменена"
		if( in_array($status, $orders_statuses_for_inverse) )
		{
			//Получаем список id позиций выбранных заказов
			$items_id = '';
			$SQL_str = '';
			for($i=0; $i < count($orders); $i++)
			{
				if($i > 0)$SQL_str .=",";
				$SQL_str .= $orders[$i];
			}
			$query = $db_link->prepare("SELECT `id`, `status` FROM `shop_orders_items` WHERE `order_id` IN($SQL_str);");
			$query->execute();
			while($row = $query->fetch())
			{
				if( ! in_array($row['status'], $orders_items_statuses_for_inverse) ){
					if($items_id != '')$items_id .= ",";
					$items_id .= $row['id'];
				}
			}
			
			if($items_id != ''){
				//Меняем статус позиций
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/protocol/set_order_item_status.php?initiator=2&key=".urlencode($DP_Config->tech_key)."&status=".$orders_items_statuses_for_inverse[0]."&orders_items=[".$items_id."]");
				curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
				curl_setopt($curl, CURLOPT_POST, false);
				curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
				curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
				curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 30); 
				curl_setopt($curl, CURLOPT_TIMEOUT, 30);
				curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
				$curl_result = curl_exec($curl);
				curl_close($curl);
				
				$result = json_decode($curl_result, true);
				if($result['status'] != true){
					exit(json_encode($result));
				}
			}
		}
	
	// ...........
	
		//Если менеджер меняет статус заказа на "Выполнен" - Автоматически меняем статус всех позиций на "Выдана" кроме тех позиций которые находятся в статусе "Отменена"
		if( in_array($status, $orders_statuses_for_finish) )
		{
			//Получаем список id позиций выбранных заказов
			$items_id = '';
			$SQL_str = '';
			for($i=0; $i < count($orders); $i++)
			{
				//Если заказ не оплачен - данный статус присвоить нельзя
				if($orders_data[$orders[$i]]['paid'] != 1){
					$result["status"] = false;
					$result["message"] = translate_str_by_id(5296);
					$result["code"] = 101;
					exit(json_encode($result));
				}
				if($i > 0)$SQL_str .=",";
				$SQL_str .= $orders[$i];
			}
			$query = $db_link->prepare("SELECT `id`, `status` FROM `shop_orders_items` WHERE `order_id` IN($SQL_str);");
			$query->execute();
			while($row = $query->fetch())
			{
				if( ! in_array($row['status'], $orders_items_statuses_for_inverse) && ! in_array($row['status'], $orders_items_statuses_for_finish) ){
					if($items_id != '')$items_id .= ",";
					$items_id .= $row['id'];
				}
			}
			
			if($items_id != ''){
				//Меняем статус позиций
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/protocol/set_order_item_status.php?initiator=2&key=".urlencode($DP_Config->tech_key)."&status=".$orders_items_statuses_for_finish[0]."&orders_items=[".$items_id."]");
				curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
				curl_setopt($curl, CURLOPT_POST, false);
				curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
				curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
				curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 30); 
				curl_setopt($curl, CURLOPT_TIMEOUT, 30);
				curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
				$curl_result = curl_exec($curl);
				curl_close($curl);
				
				$result = json_decode($curl_result, true);
				if($result['status'] != true){
					exit(json_encode($result));
				}
			}
		}
	
	// ...........
	
}

// -----------------------------------------------------------------------------------------------------------

//2. Меняем статус
$binding_values = array();
array_push($binding_values, $status);
$SQL_UPDATE_STATUS = "UPDATE `shop_orders` SET `status`=? WHERE ";
for($i=0; $i < count($orders); $i++)
{
    if($i > 0)$SQL_UPDATE_STATUS .=" OR ";
    $SQL_UPDATE_STATUS .= "`id`=?";
	
	array_push($binding_values, $orders[$i]);
}
$SQL_UPDATE_STATUS .=";";
if( $db_link->prepare($SQL_UPDATE_STATUS)->execute( $binding_values ) != true )
{
    $result["status"] = false;
    $result["message"] = "SQL error";
    $result["code"] = 701;
    exit(json_encode($result));
}

// -----------------------------------------------------------------------------------------------------------

//3. Уведомления

foreach($orders_data as $order_id=>$data)
{
    //3.1 ДЛЯ МЕНЕДЖЕРОВ
	$persons = array();
    for($i=0; $i < count($data["managers"]); $i++)
    {
		//Проверяем что пользователь являеться менеджером
		if( ! DP_User::isBackendGroupById($data["managers"][$i]) ){
			continue;
		}
		$persons[] = array('type'=>'user_id', 'user_id'=>$data["managers"][$i]);
    }
	
	//Формируем полный текст позиций заказа
	$order_text = '';
	include( $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_manager.php' );
	
	//Переменные для уведомления
	$notify_vars = array();
	$notify_vars['order_id'] = $order_id;
	$notify_vars['status_name'] = translate_str_by_id($orders_statuses[$status]["name"]);
	$notify_vars['status_ref'] = $orders_statuses[$status];//Этой переменной нет в спецификации уведомления. Но, она используется для учета настроек отправки по разным статусам
	$notify_vars['order_text'] = $order_text;
	
	//Отправляем уведомление (БЕЗ обработки результата)
	send_notify('order_status_to_manager', $notify_vars, $persons, false);
	
    
	
    //3.2 ДЛЯ ПОКУПАТЕЛЯ
	$persons = array();
    if( $data["customer"] > 0 )
    {
        $persons[] = array( 'type'=>'user_id', 'user_id'=>$data["customer"] );
    }
	else
	{
		$persons[] = array(
			'type'=>'direct_contact',
			'contacts'=>array(
					'email'=>array('value'=>$data["email_not_auth"]),
					'phone'=>array('value'=>$data["phone_not_auth"])
				)
			);
	}
	
	//Формируем полный текст позиций заказа
	$order_text = '';
	include( $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/get_order_info_html/get_order_info_html_for_user.php' );
	
	//Переменные для уведомления
	$notify_vars = array();
	$notify_vars['order_id'] = $order_id;
	$notify_vars['status_name'] = translate_str_by_id($orders_statuses[$status]["name"]);
	$notify_vars['status_ref'] = $orders_statuses[$status];//Этой переменной нет в спецификации уведомления. Но, она используется для учета настроек отправки по разным статусам
	$notify_vars['order_text'] = $order_text;
	
	//Отправляем уведомление (БЕЗ обработки результата)
	send_notify('order_status_to_customer', $notify_vars, $persons, false);

	// WhatsApp delivery-status template (wa.me link logged on order — operator or automation can send)
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';
		if (function_exists('epc_wa_notify_order_status_change')) {
			$order_row = array('user_id' => (int)($data['customer'] ?? 0), 'phone_not_auth' => (string)($data['phone_not_auth'] ?? ''));
			epc_wa_notify_order_status_change($db_link, $DP_Config, (int)$order_id, (string)$notify_vars['status_name'], $order_row);
		}
	}
}

// -----------------------------------------------------------------------------------------------------------

//ЗАПИСЬ ИСТОРИИ ДЕЙСТВИЙ С ЗАКАЗАМИ
if($initiator == 1) 
{
	$is_manager = 1;
	$user_id = DP_User::getAdminId();
	$is_robot = 0;
}
else if( $initiator == 4 )
{
	$is_manager = 0;
	$user_id = 0;
	$is_robot = 1;
}
else 
{
	$is_manager = 0;
	$user_id = DP_User::getUserId();
	$is_robot = 0;
}
for($i=0; $i < count($orders); $i++)
{
	$order_id = $orders[$i];
	
	//Пишем лог заказа
	$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id, time(), $user_id, $is_manager,translate_str_by_id(4568).' <b>'.translate_str_by_id($orders_statuses[$status]["name"]).'</b>', $is_robot) );
}

// -----------------------------------------------------------------------------------------------------------


//4. Выдаем ответ (JSON)
$result["status"] = true;
exit(json_encode($result));

?>