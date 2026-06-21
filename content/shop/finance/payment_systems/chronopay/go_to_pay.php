<?php
/**
 * Скрипт перехода на страницу оплаты
*/
header('Content-Type: text/html; charset=utf-8');
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


$amount_str = (string) number_format($operation["amount"], 2, '.', '');

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );



$sign = md5($paysystem_parameters["product_id"].'-'.$amount_str.'-'.$operation_id.'-'.$paysystem_parameters["shared_sec"]);
?>



<div align="center" style="position:relative; width:100%; height:100%;">
<div align="center" style="position:absolute; width:250px; height:250px; top:50%; left:50%; margin: -125px 0 0 -125px;">
	<div style="background:#18264d; padding:20px 0px 10px 0px; text-align:center;">
		<img src="https://chronopay.com/img/logo.png"/><br/><br/>
		<form action="https://payments.chronopay.com/" method="POST">
			<input type="hidden" name="product_id" value="<?=$paysystem_parameters["product_id"];?>" />
			<input type="hidden" name="order_id" value="<?=$operation_id;?>" />
			<input type="hidden" name="product_price" value="<?=$amount_str;?>" />
			<input type="hidden" name="product_name" value="<?=$operation_description;?>" />
			<input type="hidden" name="cb_url" value="http://zip54.ru/content/shop/finance/payment_systems/chronopay/notification.php" />
			<input type="hidden" name="success_url" value="http://zip54.ru/shop/balans?status=1" />
			<input type="hidden" name="decline_url" value="http://zip54.ru/shop/balans?status=2" />
			<input type="hidden" name="sign" value="<?=$sign;?>" />
			<input style="background:#fff; cursor:pointer; font-weight:bold; padding:10px 5px; color:#333; border:none;" type="submit" value="<?php echo translate_str_by_id(4358); ?>" />
		</form>
	</div>
</div>
</div>