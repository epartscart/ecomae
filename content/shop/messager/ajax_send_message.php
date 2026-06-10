<?php
/*
Серверный срипт для отправки сообщений
*/

error_reporting(0);

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


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
//Для отправки уведомлений
require_once( $_SERVER["DOCUMENT_ROOT"]."/content/notifications/notify_helper.php" );

//Входные данные:
$order_id = (int)$_GET["order_id"];
$text = $_GET["text"];
$is_customer = 1;

//Проверяем права на запуск
if( !empty($_GET["manager"]) )//Запрос от менеджера
{
    $is_customer = 0;
    //Проверяем право менеджера
    if( ! DP_User::isAdmin())
    {
        $result["status"] = false;
        $result["message"] = "Forbidden";
        $result["code"] = 501;
        exit(json_encode($result));//Вообще не является администратором бэкенда
    }

    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------

    //Для возвратов
    if (isset($_GET["return_id"]))
    {
        $order_sql = "SELECT * FROM `shop_orders_returns` WHERE `id` = ?;";
        $order_query = $db_link->prepare($order_sql);
        $order_query->execute( array($_GET["return_id"]) );
    }
    else
    {
        $order_sql = "SELECT * FROM `shop_orders` WHERE `id` = ?;";
        $order_query = $db_link->prepare($order_sql);
        $order_query->execute( array($order_id) );
    }

}
else//Запрос от пользователя
{
    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------


    $user_id = DP_User::getUserId();

    //Для возвратов
    if (isset($_GET["return_id"]))
    {
        $order_sql = "SELECT * FROM `shop_orders_returns` WHERE `id` = ? AND `user_id` = ?;";
        $order_query = $db_link->prepare($order_sql);
        $order_query->execute( array($_GET["return_id"], $user_id) );
    }
    else
    {
        $order_sql = "SELECT * FROM `shop_orders` WHERE `id` = ? AND `user_id` = ?;";
        $order_query = $db_link->prepare($order_sql);
        $order_query->execute( array($order_id, $user_id) );
    }

}


$order = $order_query->fetch();

if($order == false)
{
    $result["status"] = false;
    $result["message"] = "Forbidden";
    $result["code"] = 501;
    exit(json_encode($result));
}

$office_id = $order["office_id"];
$user_id = $order["user_id"];

//Для возвратов
if (isset($_GET["return_id"]))
{
    if($db_link->prepare('INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (?,?,?,?,?);')->execute( array(0, $is_customer, htmlentities($text), time(), $_GET["return_id"]) ) != true)
        echo "false";
    else
        echo "true";
}
else
{
    if($db_link->prepare('INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`) VALUES (?,?,?,?,?);')->execute( array($order_id, $is_customer, htmlentities($text), time(), 0) ) != true)
    {
        echo "false";
    }
    else
    {
		//Настройки шаблона
		$templates = array();
		$templates_query = $db_link->prepare('SELECT * FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1;');
		$templates_query->execute();
		$templates = $templates_query->fetch();
		$templates = json_decode($templates['data_value'], true);
		$background_order_link = "#799658";
		if(!empty($templates['main_color'])){
			$background_order_link = $templates['main_color'];
		}
		
        //Отправляем уведомление
        if($is_customer)
        {
            $epc_notify_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';
            $notify_vars = array();
            $notify_vars['order_id'] = $order_id;
            $order_link = '<div style="margin-top:10px;"><a style="background: '.$background_order_link.'; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;" target="_blank" href="'. $DP_Config->domain_path . $DP_Config->backend_dir .'/shop/orders/order?order_id='. $order_id .'">View order in Control Panel</a></div>';
            $notify_vars['order_link'] = $order_link;
            $msg_html = '<div style="font-family:Calibri,Arial,sans-serif;font-size:14px;"><p><strong>Customer message on order #'.(int)$order_id.':</strong></p><p>'.nl2br(htmlspecialchars($text, ENT_QUOTES, 'UTF-8')).'</p></div>';
            $notify_vars['order_text'] = $order_link . $msg_html;
            if (is_readable($epc_notify_path)) {
                require_once $epc_notify_path;
                epc_staff_send_notify('order_message_to_manager', $notify_vars, (int)$user_id, (int)$office_id, array(), false);
            } else {
                $managers_query = $db_link->prepare('SELECT `users` FROM `shop_offices` WHERE `id` = ?;');
                $managers_query->execute(array($office_id));
                $managers_record = $managers_query->fetch();
                $managers = json_decode($managers_record['users'], true);
                $persons = array();
                if (is_array($managers)) {
                    for ($i = 0; $i < count($managers); $i++) {
                        $persons[] = array('type' => 'user_id', 'user_id' => (int)$managers[$i]);
                    }
                }
                if (!empty($persons)) {
                    send_notify('order_message_to_manager', $notify_vars, $persons, false);
                }
            }
        }
        else
        {
            //Отправляем сообщение покупателю
            //Для покупателя
            //Значение переменных для уведомления
            $notify_vars = array();
            $notify_vars['order_id'] = $order_id;
			
			if($user_id > 0){
				$order_link = '<div style="margin-top:10px;"><a style="background: '.$background_order_link.'; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;" target="_blank" href="'. $DP_Config->domain_path .'shop/orders/order?order_id='. $order_id .'">Перейти на страницу заказа</a></div>';
			}else{
				$order_link = '<div style="margin-top:10px;"><a style="background: '.$background_order_link.'; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;" target="_blank" href="'. $DP_Config->domain_path .'shop/orders/zakaz-bez-registracii?order_id='. $order_id .'">Перейти на страницу заказа</a></div>';
			}
			$notify_vars['order_link'] = $order_link;
			
            //Получатель
            $persons = array();
            if( $user_id > 0 )
            {
                $persons[] = array('type'=>'user_id', 'user_id'=>$user_id);
            }
            else
            {
                $persons[] = array(
                    'type'=>'direct_contact',
                    'contacts'=>array(
                        'email'=>array('value'=>$order["email_not_auth"]),
                        'phone'=>array('value'=>$order["phone_not_auth"])
                    )
                );
            }
            //Отправляем уведомление (БЕЗ обработки результата)
            send_notify('order_message_to_customer', $notify_vars, $persons, false);
        }



        echo "true";
    }
}


?>