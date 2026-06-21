<?php
/**
 * Скрипт страницы для переадресации после успешной оплаты
*/
header('Content-Type: text/html; charset=utf-8');

// Подключаем класс работы с api банка
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/payment_systems/alfabank/SoapClient.php");


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


$query = $db_link->prepare('SELECT `id` FROM `shop_users_accounting` WHERE `tech_value_text` = ?;');
$query->execute( array($_GET['orderId']) );
$record = $query->fetch();
$operation_id = $record['id'];
//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


// Авторизационные данные
$login = $paysystem_parameters["login"];
$password = $paysystem_parameters["password"];

if($paysystem_parameters["test_mode"] == 1)
{
    $wsdl = "https://alfa.rbsuat.com/payment/webservices/merchant-ws?wsdl";
}
else
{
    $wsdl = "https://payment.alfabank.ru/payment/webservices/merchant-ws?wsdl";
}


// Проверка совершения платежа
if ($_SERVER['REQUEST_METHOD'] == 'GET' && isset($_GET['orderId']))
{
	$client = new Gateway($wsdl);
	$data = array('orderParams' => array('orderId' => $_GET['orderId']));
	
	$client->login = $login;
	$client->password = $password;
	
	$response = $client->__call('getOrderStatus', $data);
	
	if($response->errorCode != "0" )
	{
		$error_message = translate_str_by_id(4352);
		$error_message = urlencode($error_message);
		?>
		<script>
			location="<?php echo $multilang_params['lang_href']; ?>/shop/balans?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	else if($response->orderStatus != "2")
	{		
		$error_message = translate_str_by_id(4353).": ".$response->orderStatus;
		$error_message = urlencode($error_message);
		?>
		<script>
			location="<?php echo $multilang_params['lang_href']; ?>/shop/balans?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	
	
	//Еще раз проверим что операция не активирована
	$account_data_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
	$account_data_query->execute( array($response->orderNumber) );
	$account_data_record = $account_data_query->fetch();
	if($account_data_record == false)
	{
		?>
		<script>
			location="<?php echo $multilang_params['lang_href']; ?>/shop/balans?success_message=<?php echo urlencode(translate_str_by_id(4355)); ?>";
		</script>
		<?php
		exit;
	}
	
	
	//Активируем операцию
	$result = $db_link->prepare('UPDATE `shop_users_accounting` SET `active`=1 WHERE `id` = ? AND `active` = 0;');
	if($result != $result->execute( array($response->orderNumber) ) )
	{
		$error_message = translate_str_by_id(4354).": ".$response->orderNumber;
		$error_message = urlencode($error_message);
		?>
		<script>
			location="<?php echo $multilang_params['lang_href']; ?>/shop/balans?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	else
	{
		//Получаем сумму
		$amount_query = $db_link->prepare('SELECT `amount` FROM `shop_users_accounting` WHERE `id` = ?;');
		$amount_query->execute( array($response->orderNumber) );
		$amount_record = $amount_query->fetch();
		
		
		// -----
		//Уведомление менеджерам магазинов
		$operation_id = $response->orderNumber;
		$amount = $amount_record["amount"];
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
		// -----
		
		
		// -----
		//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
		$operation_id = $response->orderNumber;
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
		// -----
		
		
		?>
		<script>
			location="<?php echo $multilang_params['lang_href']; ?>/shop/balans?success_message=<?php echo urlencode(translate_str_by_id(4355)); ?>";
		</script>
		<?php
		exit;
	}
}
?>