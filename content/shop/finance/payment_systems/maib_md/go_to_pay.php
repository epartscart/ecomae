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
$operation_description = "Replenishment of the balance";
if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL)
{
	$operation_description = "Order payment";
}


//Получаем настройки. Этот блок - специальный - у каждой системы свой
$paysystem_parameters_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `handler`= ?;');
$paysystem_parameters_query->execute( array('maib_md') );
$paysystem_parameters_record = $paysystem_parameters_query->fetch();
$paysystem_parameters = json_decode($paysystem_parameters_record["parameters_values"], true);


// -------------------------------------------------------------------------------------------------


//Регистрируем нашу операцию в системе


if($paysystem_parameters["test_mode"] == 1)
{
	$url = "https://ecomm.maib.md:4499/ecomm2/MerchantHandler";
}
else
{
	$url = "https://ecomm.maib.md:7443/ecomm2/ClientHandler";
}


$postdata = array(
		'command' => 'V',
		'amount' => $operation["amount"]*100,
		'currency' => $paysystem_parameters["currency"],
		'client_ip_addr' => $paysystem_parameters["ip"],
		'description' => $operation_description,
		'language' => $paysystem_parameters["language"],
		'msg_type' => 'SMS'
);


/*
echo '<pre>';
var_dump($_SERVER["REMOTE_ADDR"]);
var_dump($postdata);
*/


$ch = curl_init();
curl_setopt($ch, CURLOPT_URL, $url);
curl_setopt($ch, CURLOPT_HEADER, 0);
curl_setopt($ch, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($ch, CURLOPT_POST, true);
curl_setopt($ch, CURLOPT_CONNECTTIMEOUT, 20); 
curl_setopt($ch, CURLOPT_TIMEOUT, 20);
curl_setopt($ch, CURLOPT_POSTFIELDS, http_build_query($postdata));
$result = curl_exec($ch);
curl_close($ch);


//var_dump($result);


$trans_id = trim(str_replace('TRANSACTION_ID:', '', $result));


//var_dump($trans_id);
//exit;


if($trans_id == ''){
	header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4373)) );
	exit;
}


//Оперция успешно зарегистрирована - записываем ID у себя:
$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `tech_value_text` = ? WHERE `id` = ?;');
if(! $update_query->execute( array($trans_id, $operation_id ) ) )
{
	header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".urlencode(translate_str_by_id(4373)) );
}
else
{
	header("Location: https://ecomm.maib.md:7443/ecomm2/ClientHandler?trans_id=".$trans_id);
}
?>