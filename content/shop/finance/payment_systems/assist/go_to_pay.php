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


//Данные формы
$order_number = (int)$operation["id"];// ID опирации в магазине
$order_amount = (float)$operation["amount"];// Сумма платежа
$merchant_id  = (int)$paysystem_parameters['Merchant_ID'];// id магазина в платежной системе
$order_currency = $paysystem_parameters["currency"];// Валюта платежа

// В зависимости от режима берем соответствующий адрес на который нужно отослать форму
if(empty($paysystem_parameters['test_mode'])){
	$url = $paysystem_parameters['url'];
}else{
	$url = $paysystem_parameters['url_test'];
}

// Формируем Checkvalue - проверочную строку
$str_x = $merchant_id .';'. $order_number .';'. $order_amount .';'. $order_currency;
$str_x = md5($str_x);
$salt  = md5($paysystem_parameters['salt']);
$hash  = mb_strtoupper($salt . $str_x, 'UTF-8');
$hash  = md5($hash);
$hash  = mb_strtoupper($hash, 'UTF-8');

?>
<form action="<?=$url;?>" method="POST" name="pay_form" style="display:none">
<input type="hidden" name="Merchant_ID" value="<?=$merchant_id;?>">
<input type="hidden" name="OrderNumber" value="<?=$order_number;?>">
<input type="hidden" name="OrderAmount" value="<?=$order_amount;?>">
<input type="hidden" name="OrderCurrency" value="<?=$order_currency;?>">
<input type="hidden" name="OrderComment" value="<?=$operation_description;?>">
<input type="hidden" name="Language"value="RU">
<input type="hidden" name="Checkvalue"value="<?=$hash;?>">
</form>
<script>
document.forms["pay_form"].submit();
</script>