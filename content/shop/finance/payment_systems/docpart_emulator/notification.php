<?php
header('Content-Type: text/html; charset=utf-8');
/*
Тестовый платежный шлюз Docpart Эмулятор вызывает этот скрипт после проведения оплаты
*/

//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

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



//Использовать эту систему могут только пользователи с доступом в ПУ
if( ! DP_User::isAdmin())
{
	exit("Forbidden");
}


//Получаем данные от платежного шлюза
$user_id = $_POST['user_id'];
$operation_id = $_POST['operation_id'];
$sum = $_POST['sum'];



/*
//Получаем настройки. Этот блок - специальный - у каждой системы свой. У Docpart-Эмулятор настроек нет.
$paysystem_parameters_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `handler`= ?;');
$paysystem_parameters_query->execute( array('docpart_emulator') );
$paysystem_parameters_record = $paysystem_parameters_query->fetch();
$paysystem_parameters = json_decode($paysystem_parameters_record["parameters_values"], true);
*/
//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


//Проверку подписи от платежного шлюза не проверяем, т.к. этот скрипт может запускаться только пользователем ПУ (проверка есть выше)


// ЗДЕСЬ МЕНЯМ СТАТУС ОПЕРАЦИИ (active=1)
//Проверяем заказ
$check_operation = $db_link->prepare('SELECT COUNT(*) FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
$check_operation->execute( array($operation_id) );
if( $check_operation->fetchColumn() == 1 )
{
	$update_query = $db_link->prepare('UPDATE `shop_users_accounting` SET `active` = 1 WHERE `id` = ?;');
	$update_query->execute( array($operation_id) );	
	
	// -----
	//Уведомление менеджерам магазинов
	//$operation_id = $_POST["order_id"];
	$amount = $sum;
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_notify.php");
	// -----
	
	
	// -----
	//Вызов протокола оплаты заказа, если в операцию был вписан номер заказа
	//$operation_id = $_POST["order_id"];
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/finance/pay_for_order.php");
	// -----
	
	
	header("Location: ".$DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".urldecode(translate_str_by_id(4355)) );
}
?>