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



//Данные по пользователю - требуются для Авангарда
$user_profile = DP_User::getUserProfile();
$client_name = $user_profile["surname"]." ".$user_profile["name"]." ".$user_profile["patronymic"];
$client_phone = $user_profile["cellphone"];
$client_email = $user_profile["email"];



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
$shop_id = $paysystem_parameters["avangard_shop_id"];
$shop_sign = $paysystem_parameters["avangard_shop_sign"];
$amount = $operation["amount"]*100;
$signature = strtoupper(md5(strtoupper(md5($shop_sign).md5($shop_id.$operation_id.$amount))));
?>


<form action="https://www.avangard.ru/iacq/post" method="POST" name="pay_form" style="display:none">
    <input type="hidden" name="client_name" value="<?php echo $client_name; ?>">
	<input type="hidden" name="client_phone" value="<?php echo $client_phone; ?>">
	<input type="hidden" name="client_email" value="<?php echo $client_email; ?>">
	<input type="hidden" name="shop_id" value="<?php echo $shop_id; ?>">
    <input type="hidden" name="signature" value="<?php echo $signature; ?>">
    <input type="hidden" name="amount" value="<?php echo $amount; ?>">
    <input type="hidden" name="order_number" value="<?php echo $operation_id; ?>">
    <input type="hidden" name="order_description" value="<?php echo translate_str_by_id(4356); ?> <?php echo $DP_Config->site_name; ?>">
    <input type="hidden" name="language" value="RU">
    <input type="hidden" name="back_url" value="<?php echo $DP_Config->domain_path; ?>shop/balans">
	<input type="hidden" name="back_url_ok" value="<?php echo $DP_Config->domain_path; ?>shop/balans?success_message=Выполнено">
	<input type="hidden" name="back_url_fail" value="<?php echo $DP_Config->domain_path; ?>shop/balans?error_message=<?php echo urlencode(translate_str_by_id(4357)); ?>">
</form>
<script>
document.forms["pay_form"].submit();
</script>