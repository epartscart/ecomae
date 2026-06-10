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


$sum = (float)$operation["amount"];


//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );

if($paysystem_parameters["check_test"] == 1)
{
	$test = "1";
}
else
{
	$test = "0";
}

$mnt_id = $paysystem_parameters["mnt_id"];// Идентификатор магазина в системе MONETA.RU. Соответствует номеру расширенного счета магазина.


?>
<form name="pay_form" style="display:none" method="post" action="https://www.payanyway.ru/assistant.htm">
	<input type="hidden" name="MNT_ID" value="<?php echo $mnt_id; ?>">
	<input type="hidden" name="MNT_TRANSACTION_ID" value="<?php echo $operation_id; ?>">
	<input type="hidden" name="MNT_CURRENCY_CODE" value="RUB">
	<input type="hidden" name="MNT_AMOUNT" value="<?php echo $sum; ?>">
	<input type="hidden" name="MNT_TEST_MODE" value="<?php echo $test; ?>">
	<input type="hidden" name="MNT_DESCRIPTION" value="<?php echo $operation_description; ?>">
	<input type="hidden" name="MNT_SUBSCRIBER_ID" value="<?php echo $user_id; ?>">
	<input type="hidden" name="MNT_SUCCESS_URL" value="<?php echo $DP_Config->domain_path.$multilang_params['lang_href_no_slash']; ?>/shop/balans?success_message=<?php echo translate_str_by_id(2300); ?>">
	<input type="hidden" name="MNT_FAIL_URL" value="<?php echo $DP_Config->domain_path.$multilang_params['lang_href_no_slash']; ?>/shop/balans?error_message=<?php echo translate_str_by_id(2122); ?>">
	<input type="hidden" name="MNT_RETURN_URL" value="<?php echo $DP_Config->domain_path.$multilang_params['lang_href_no_slash'].'/'; ?>content/shop/finance/payment_systems/payanyway/notification.php">
	<input type="hidden" name="MNT_CUSTOM1" value="<?php echo $operation_id; ?>">
	<input type="submit" value="Pay order">
</form>
<script>
	document.forms["pay_form"].submit();
</script>