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

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


$user_id = DP_User::getUserId();

if(empty($_GET["account"]) && !empty($_GET["orderNumber"])){
	$_GET["account"] = $_GET["orderNumber"];
	
	sleep(5);
}

//ПОЛУЧАЕМ ДАННЫЕ ПО ПЛАТЕЖУ
$account_data_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$account_data_query->execute( array($_GET["account"]) );
$account_data_record = $account_data_query->fetch();
if($account_data_record == false)
{
    exit();
}


$operation_id = $_GET["account"];


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


//ПОСЫЛАЕМ ЗАПРОС В СБЕР (ПРОВЕРКА ПЛАТЕЖА):
$ch = curl_init();
if($paysystem_parameters["test_mode"] == 1)
{
	$url = "https://3dsec.sberbank.ru/payment/rest/getOrderStatus.do?";
}
else
{
	$url = "https://securepayments.sberbank.ru/payment/rest/getOrderStatus.do?";
}
$url .= "userName=".urlencode($paysystem_parameters["sbr_userName"]);//Логин магазина, полученный при подключении
$url .= "&password=".urlencode($paysystem_parameters["sbr_password"]);//Пароль магазина, полученный при подключении
$url .= "&orderId=".urlencode($account_data_record["tech_value_text"]);//Номер заказа в платежной системе (В Сбере)


curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_HEADER, 0);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
$result = curl_exec($ch);
curl_close($ch);

$result = json_decode($result, true);

//var_dump($result);
if($result["ErrorCode"] != "0" )
{
	header('Content-Type: text/html; charset=utf-8');
	exit( translate_str_by_id(4393) );
}
else if($result["OrderStatus"] != "2")
{
	header('Content-Type: text/html; charset=utf-8');
	exit(translate_str_by_id(4394).": ".$result["OrderStatus"]);
}




//Еще раз проверим что операция не активирована
$account_data_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$account_data_query->execute( array($_GET["account"]) );
$account_data_record = $account_data_query->fetch();
if($account_data_record == false)
{
    exit();
}


//Активируем операцию
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ? AND `active` = 0;');
$update_query->execute( array($_GET["account"]) );

//Количество затронутых строк
$elements_count_rows = $update_query->rowCount();

if($elements_count_rows == 0)
{
    ?>
    <script>
        alert("<?php echo translate_str_by_id(4395); ?>");
    </script>
    <?php
}
else
{
	//Получаем сумму
	$amount_query = $db_link->prepare('SELECT `amount` FROM `shop_users_accounting` WHERE `id` = ?;');
	$amount_query->execute( array($_GET["account"]) );
	$amount_record = $amount_query->fetch();
	
	// -----
	//Уведомление менеджерам магазинов
	$operation_id = $_GET["account"];
	$amount = $amount_record["amount"];
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
	// -----
	
	
	// -----
	//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
	$operation_id = $_GET["account"];
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
	// -----
	
	
    header( "Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".urlencode( translate_str_by_id(4355) ) );
}
?>