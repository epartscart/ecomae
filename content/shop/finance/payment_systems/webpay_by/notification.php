<?php
/**
 * Скрипт страницы для переадресации после успешной оплаты
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



//$_POST = json_decode('', true);

/*
$f = fopen('log.txt', 'a');
$data = file_get_contents('php://input');
fwrite($f, date("d.m.Y H:i:s", time())."\n");
fwrite($f, 'REQUEST_METHOD: '.$_SERVER["REQUEST_METHOD"]."\n");
fwrite($f, '$_GET: '.json_encode($_GET)."\n");
fwrite($f, '$_POST: '.json_encode($_POST)."\n");
fwrite($f, '$data: '.json_encode($data)."\n");
fwrite($f, "\n\n\n--------------------------------------\n\n\n");
*/





$transaction_id  = 0;

if(isset($_POST['transaction_id'])){
	$transaction_id  = $_POST['transaction_id'];
}
if(isset($_GET['wsb_tid'])){
	$transaction_id  = $_GET['wsb_tid'];
}



// -------------------------------------------------------------------------------------------------

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );

// -------------------------------------------------------------------------------------------------



if($paysystem_parameters["wsb_test"] == 1)
{
	$url = "https://sandbox.webpay.by/api/login";
}
else
{
	$url = "https://billing.webpay.by/api/login";
}



$request_body = array();
$request_body["merchantId"] = (int) trim($paysystem_parameters["wsb_storeid"]);//Идентификатор магазина в системе WEBPAY
$request_body["username"] = trim($paysystem_parameters["username"]);//Логин учетной записи для входа в личный кабинет системы WEBPAY
$request_body["password"] = trim($paysystem_parameters["password"]);//Пароль от учетной записи для входа в личный кабинет системы WEBPAY

$data_string = json_encode($request_body, JSON_UNESCAPED_UNICODE);

$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_HEADER, 0);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($ch, CURLOPT_POSTFIELDS, $data_string);
curl_setopt($ch, CURLOPT_HTTPHEADER, array(
   'Content-Type: application/json',
   'Content-Length: ' . strlen($data_string))
);
$result = curl_exec($ch);
curl_close($ch);

$result = json_decode($result, true);

$auth_token = '';
if(isset($result['data']['auth_token'])){
	$auth_token = $result['data']['auth_token'];
}

if(!empty($auth_token)){
	
	if($paysystem_parameters["wsb_test"] == 1)
	{
		$url = "https://sandbox.webpay.by/api/v1/transactions/info/".$transaction_id;
	}
	else
	{
		$url = "https://billing.webpay.by/api/v1/transactions/info/".$transaction_id;
	}
	
	$ch = curl_init();
	curl_setopt($ch, CURLOPT_URL, $url);
	curl_setopt($ch, CURLOPT_POST, false);
	curl_setopt($ch, CURLOPT_HEADER, 0);
	curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
	curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
	curl_setopt($ch, CURLOPT_HTTPHEADER, array('Authorization: Bearer ' . $auth_token));
	$result = curl_exec($ch);
	curl_close($ch);

	$result = json_decode($result, true);
	
	/*
	echo '<pre>';
	var_dump($url);
	var_dump($result);
	echo '</pre>';
	exit;
	*/
	
	$orderNum = $result['data']['orderNum'];
	$amount = $result['data']['amount'];
	$status = $result['data']['status'];
	
	//ПОЛУЧАЕМ ДАННЫЕ ПО ПЛАТЕЖУ
	$account_data_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `amount` = ?;');
	$account_data_query->execute( array($orderNum, $amount) );
	$account_data_record = $account_data_query->fetch();
	if($account_data_record == false)
	{
		exit();
	}
	
	//Состояние авторизации платежа (денежные средства заблокированы на банковской карточке покупателя). В этом состоянии у покупателя данная сумма ещё не списана со счета (заблокирована) и данная сумма еще не перечислена на Ваш банковский счет.
	if($status == 'Authorized'){
		if(isset($_GET['wsb_tid'])){
			if($pay_orders > 0){
				header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/orders/order?order_id=".$pay_orders."&success_message=".translate_str_by_id(5618)."");
			}else{
				header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".translate_str_by_id(5618)."");
			}
		}
	}
	
	//Денежные средства списаны с банковской карточки и будут зачислены на Ваш банковский счет в сроки, установленные банком-эквайером.
	if($status == 'Completed'){
		
		//Активируем операцию
		$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ? AND `active` = 0;');
		$update_query->execute( array($orderNum) );

		//Количество затронутых строк
		$elements_count_rows = $update_query->rowCount();

		if($elements_count_rows == 0)
		{
			?>
			<script>
				alert(translate_str_by_id(4395));
			</script>
			<?php
		}
		else
		{
			//Получаем сумму
			$amount_query = $db_link->prepare('SELECT `amount`, `pay_orders` FROM `shop_users_accounting` WHERE `id` = ?;');
			$amount_query->execute( array($orderNum) );
			$amount_record = $amount_query->fetch();
			
			// -----
			//Уведомление менеджерам магазинов
			$operation_id = $orderNum;
			$amount = $amount_record["amount"];
			$pay_orders = $amount_record["pay_orders"];
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
			// -----
			
			
			// -----
			//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
			$operation_id = $orderNum;
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
			// -----
			
			if(isset($_GET['wsb_tid'])){
				if($pay_orders > 0){
					header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/orders/order?order_id=".$pay_orders."&success_message=".urlencode(translate_str_by_id(4355)) );
				}else{
					header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".urlencode(translate_str_by_id(4355)));
				}
			}
		}
		//------------------------------
	}
}
?>