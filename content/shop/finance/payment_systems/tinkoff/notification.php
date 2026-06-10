<?php
require_once("include.php");

$operation_id = $_POST["OrderId"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );

$terminal_id = $paysystem_parameters["terminal_id"];
$password = $paysystem_parameters["password"];

$data = file_get_contents('php://input');

/*
$f = fopen('log.txt', 'w');
fwrite($f, date("d.m.Y H:i:s", time())."\n");
fwrite($f, 'REQUEST_METHOD: '.$_SERVER["REQUEST_METHOD"]."\n");
fwrite($f, '$_GET: '.json_encode($_GET)."\n");
fwrite($f, '$_POST: '.json_encode($_POST)."\n");
fwrite($f, '$data: '.json_encode($data)."\n");
fwrite($f, "\n\n\n--------------------------------------\n\n\n");
*/

if(empty($_POST) && !empty($data)){
	$_POST = json_decode($data, true);
}

if($_SERVER["REQUEST_METHOD"] == "POST")
{
	$token_res 	= $_POST["Token"]; //Подпись запроса. Алгоритм формирования подписи описан в разделе "Проверка токенов"
	$order_id	= $_POST["OrderId"];
	
	$array_data_res = array();
	
	//Формируем массив для проверки ключа
	foreach($_POST as $key => $v)
	{
		if($key == "Token")
			continue;
		$array_data_res[$key] = $v;
	}
	
	//Добавляем к массиву пароль
	$array_data_res["Password"] = $password;
	
	//Получаем хеш
	$hash = genHash( $array_data_res );
	
	$bank_operation_id = $_POST["PaymentId"];

	$log_message = "";
	$date = date("Y-m-d H:i:s");

	if($token_res == $hash) //Хеши совпали, делаем операцию активной. 
	{
		if($_POST["Success"] == true)
		{
			if($_POST["Status"] == "CONFIRMED") //Операция подтверждена
			{
				
				$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ?;');
				$operation_query->execute( array($order_id) );
				$operation = $operation_query->fetch();
				if($operation["active"] == 1)
				{
					// Операция уже активирована
				}
				else
				{
					$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1, `tech_value_text` = ? WHERE `id` = ?;');
					if( $update_query->execute( array($bank_operation_id, $order_id) ) != true)
					{
						/*
						$log_message .= "===============================================================================\n";
						$log_message .="Ошибка Активации операции! {$date}\nID фин операции:{$order_id}\nID операции в банке: {$bank_operation_id}\n";
						$log_message .="=============================================================================\n";
*/						
					}
					else//Оплата заказа - если по этой операции были заказы
					{
						//Получаем сумму
						$amount_query = $db_link->prepare('SELECT `amount` FROM `shop_users_accounting` WHERE `id` = ?;');
						$amount_query->execute( array($order_id) );
						$amount_record = $amount_query->fetch();
						
						// -----
						//Уведомление менеджерам магазинов
						$operation_id = $order_id;
						$amount = $amount_record["amount"];
						require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
						// -----
						
						
						// -----
						//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
						$operation_id = $order_id;
						require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
						// -----
					}
				}
			}
			else if($_POST["Status"] == "REJECTED") //Списание денежных средств закончилась ошибкой
			{
				/*
				$log_message .= "===============================================================================\n";
				$log_message .= "Ошибка списания денежных ср-в!\nID фин операции: {$order_id}\nID операции в сис-ме банка: {$bank_operation_id}\n";
				
				ob_start();
				
				var_dump($_POST);
				
				$buffer = ob_get_contents();
				
				ob_end_clean();
				
				$log_message .= $buffer;
				
				$log_message .= "===============================================================================\n";
				*/
			}
		}
		else
		{
			/*
			$log_message .= "===============================================================================\n";
			$log_message .="Ошибка выполенения операции! {$date}\nID фин операции:{$order_id}\nID операции в банке: {$bank_operation_id}\n";
			$log_message .="=============================================================================\n";
			*/
		}
	}
	else
	{
		/*
		$log_message .= "===============================================================================\n";
		$log_message .= "Ошибка сравнения хешей! {$date}\nID фин операции:{$order_id}\nID операции в банке: {$bank_operation_id}\nХеш банка:{$_POST["Token"]}\nХеш сайта:{$hash}\n";
		$log_message .="=============================================================================\n";
		*/
	}
		
	echo "OK";
	
	if($log_message != "")
	{
		//file_put_contents("error_notify.log", $log_message, FILE_APPEND);
	}

}
?>