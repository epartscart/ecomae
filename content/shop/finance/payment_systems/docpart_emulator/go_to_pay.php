<?php
/**
 * Скрипт перехода на страницу оплаты
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


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");



//Использовать эту систему могут только пользователи с доступом в ПУ
if( ! DP_User::isAdmin())
{
	exit("Forbidden");
}



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


$sum = (float)$operation["amount"];


/*
//Получаем настройки. Этот блок - специальный - у каждой системы свой. У Docpart-Эмулятор настроек нет.
$paysystem_parameters_query = $db_link->prepare('SELECT * FROM `shop_payment_systems` WHERE `handler`= ?;');
$paysystem_parameters_query->execute( array('docpart_emulator') );
$paysystem_parameters_record = $paysystem_parameters_query->fetch();
$paysystem_parameters = json_decode($paysystem_parameters_record["parameters_values"], true);
*/
//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );
?>
<form name="pay_form" style="display:none" method="post" action="/content/shop/finance/payment_systems/docpart_emulator/pay_gateway.php">
	<input type="hidden" name="action" value="pay_page">
	<input type="hidden" name="operation_id" value="<?php echo $operation_id; ?>">
	<input type="hidden" name="sum" value="<?php echo $sum; ?>">
	<input type="hidden" name="operation_description" value="<?php echo $operation_description; ?>">
	<input type="hidden" name="user_id" value="<?php echo $user_id; ?>">
	<input type="hidden" name="shop_id" value="<?php echo $paysystem_parameters['shop_id']; ?>">
	<input type="hidden" name="shop_name" value="<?php echo $paysystem_parameters['shop_name']; ?>">
	<input type="submit" value="Pay order">
</form>
<script>
	document.forms["pay_form"].submit();
</script>
