<?php
/**
 * Скрипт перехода на страницу оплаты
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


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");


$user_id = DP_User::getUserId();



//Получаем данные операции
$operation_id = (int)$_GET["operation"];
$operation_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0 AND `user_id` = ?;');
$operation_query->execute( array($operation_id, $user_id) );
$operation = $operation_query->fetch();
if($operation == false)
{
    $answer = array();
	$answer["result"] = false;
	$answer["code"] = 2;
	exit(json_encode($answer));
}
$operation_description = translate_str_by_id(4338);
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$operation_description = translate_str_by_id(4350);
}



//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


// -------------------------------------------------------------------------------------------------


//Регистрируем нашу операцию в системе сбербанка
//ПОСЫЛАЕМ ЗАПРОС В СБЕР:
$ch = curl_init();

if($paysystem_parameters["test_mode"] == 1)
{
	$url = "https://3dsec.sberbank.ru/payment/rest/register.do?";
}
else
{
	$url = "https://securepayments.sberbank.ru/payment/rest/register.do?";
}
$url .= "userName=".urlencode($paysystem_parameters["sbr_userName"]);//Логин магазина, полученный при подключении
$url .= "&password=".urlencode($paysystem_parameters["sbr_password"]);//Пароль магазина, полученный при подключении
$url .= "&orderNumber=".urlencode($operation_id);//Номер (идентификатор) заказа в системе магазина
$url .= "&amount=".urlencode($operation["amount"]*100);//Сумма платежа в копейках (или центах) - ИЗ БД !!!!!
//$url .= "&currency=VALUE";//Код валюты платежа ISO 4217
$url .= "&returnUrl=".urlencode($DP_Config->domain_path."content/shop/finance/payment_systems/sbr/notification.php?account=".$operation_id);//Адрес, на который надо перенаправить пользователя в случае успешной оплаты
$url .= "&failUrl=".urlencode("3dsec.sberbank.ru/payment/merchants/".$paysystem_parameters["sbr_merchant"]."/errors_ru.html");//Адрес, на который надо перенаправить пользователя в случае неуспешной оплаты
$url .= "&description=".urlencode(translate_str_by_id(4356)." ".$DP_Config->site_name);//Описание заказа в свободной форме
$url .= "&clientId=".urlencode($user_id);//Номер (идентификатор) клиента в системе магазина - ИЗ БД !!!!!


curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_HEADER, 0);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($ch, CURLOPT_SSL_VERIFYPEER, 0);
$result = curl_exec($ch);
curl_close($ch);


$result = json_decode($result, true);

if(!empty($result["errorCode"]))
{
	header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4389)));
	exit;
}


// -------------------------------------------------------------------------------------------------
//Оперция успешно зарегистрирована в СБР - записываем ID сбера у себя:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `tech_value_text` = ? WHERE `id` = ?;');
if(! $update_query->execute( array($result["orderId"], $operation_id ) ) )
{
	header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4351)) );
	exit;
}
else
{
	//header("Location: ".$result["formUrl"]);
	?>
	<div style="max-width: 500px; margin: auto; padding: 20px; text-align: center;">
	<div style="text-align: center;">
	<b><?php echo translate_str_by_id(2314); ?>!</b>
	<br><?php echo translate_str_by_id(4390); ?>
	<br><b><?php echo translate_str_by_id(4391); ?>.</b>
	</div>
	<a style="margin-top: 20px; display: inline-block; padding: 10px 30px; background: #21a038; color: #fff; border-radius: 4px; text-decoration: none;" href="<?=$result["formUrl"];?>"><?php echo translate_str_by_id(4392); ?></a>
	</div>
	<?php
}

?>
