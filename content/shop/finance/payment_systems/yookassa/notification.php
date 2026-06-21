<?php
header('Content-Type: text/html; charset=utf-8');
/**
 * Скрипт уведомления о платеже
*/

//Соединение с БД
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["result"] = false;
	exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


$operation_id = 0;

if(!empty($_GET['operation_id'])){
	
	$operation_id = (int) $_GET['operation_id'];
	
	$f = fopen('z_log_notification_'.date("m_Y", time()).'.txt', 'a');
	fwrite($f, "request_of_user"."\n");
	fwrite($f, $operation_id."\n\n");
	
}else{
	
	$result = file_get_contents('php://input');
	
	$f = fopen('z_log_notification_'.date("m_Y", time()).'.txt', 'a');
	fwrite($f, "request_of_api"."\n");
	fwrite($f, $result."\n\n");
			
	$result = json_decode($result, true);
	
	if( ! isset($result['metadata']) && isset($result['object']['metadata']) ){
		$result = $result['object'];
	}
	
	if(!empty($result['metadata']['operation_id'])){
		$operation_id = (int) $result['metadata']['operation_id'];
	}
}


$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ?;');
$operation_query->execute( array($operation_id) );
$operation = $operation_query->fetch();

$payment_id = $operation['tech_value_text'];//ID операции на стороне платежной системы

//Сумма (должна быть с копейками)
$amount = (float)$operation["amount"];
$amount = number_format($amount, 2, '.', '');

if(empty($payment_id))
{
    exit;
}

if((int)$operation['active'] === 1){
	if(!empty($_GET['operation_id'])){
		header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".urlencode(translate_str_by_id(4355)) );
		exit;
	}else{
		exit;
	}
}


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );



$shopId = trim($paysystem_parameters["shopId"]);//Идентификатор магазина
$shopKey = trim($paysystem_parameters["shopKey"]);//Секретный ключ
$currency = trim($paysystem_parameters["currency"]);//Валюта

if(empty($currency)){
	$currency = 'RUB';
}



//Проверим статус операции платежа на стороне платежной системы

$curl = curl_init();
//curl_setopt($curl, CURLOPT_URL, "https://payment.yandex.net/api/v3/payments/".$payment_id);
curl_setopt($curl, CURLOPT_URL, "https://api.yookassa.ru/v3/payments/".$payment_id);
curl_setopt($curl, CURLOPT_HEADER, 0);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
curl_setopt($curl, CURLOPT_HTTPHEADER, array(
   'Authorization: Basic '.base64_encode("$shopId:$shopKey")
   )
);
curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 30); 
curl_setopt($curl, CURLOPT_TIMEOUT, 30);
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
$curl_result = curl_exec($curl);



fwrite($f, $curl_result."\n\n\n\n");


/*
echo '<pre>';
var_dump($curl_result);
echo '</pre>';
*/


$result = json_decode($curl_result, true);

if( ! isset($result['status']) && isset($result['object']['status']) ){
	$result = $result['object'];
}

if($result['status'] === 'succeeded'){
	if( ($result['paid'] === true) && (((float)$result['amount']['value']) === ((float)$amount)) && (((int)$result['metadata']['operation_id']) === ((int)$operation_id)) ){
		
		
		//Активируем операцию
		$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ? AND `active` = 0;');
		if( $update_query->execute( array($operation_id) ) != true)
		{
			if(!empty($_GET['operation_id'])){
			?>
			<script>
				alert("<?php echo translate_str_by_id(4395); ?>");
			</script>
			<?php
			}
		}
		else
		{
			//Получаем сумму
			$amount_query = $db_link->prepare('SELECT `amount` FROM `shop_users_accounting` WHERE `id` = ?;');
			$amount_query->execute( array($operation_id) );
			$amount_record = $amount_query->fetch();
			
			// -----
			//Уведомление менеджерам магазинов
			$operation_id = $operation_id;
			$amount = $amount_record["amount"];
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
			// -----
			
			
			// -----
			//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
			$operation_id = $operation_id;
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
			// -----
			
			if(!empty($_GET['operation_id'])){
				header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".urlencode(translate_str_by_id(4355)) );
			}
		}
		
		
	}
}else{
	if($result['status'] === 'canceled'){
		if(!empty($_GET['operation_id'])){
			header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4408)) );
		}
	}else{
		if(!empty($_GET['operation_id'])){
			header("Location: /");
		}
	}
}




?>