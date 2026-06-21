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



$fields = array(); 
// Добавление полей формы в ассоциативный массив
$fields["WMI_MERCHANT_ID"]    = $paysystem_parameters["wmi_merchant_id"];
$fields["WMI_PAYMENT_AMOUNT"] = $operation["amount"];
$fields["WMI_CURRENCY_ID"]    = "643";
$fields["WMI_PAYMENT_NO"]     = $operation_id;
$fields["WMI_DESCRIPTION"]    = "BASE64:".base64_encode(translate_str_by_id(4356)." ".$DP_Config->site_name);
$fields["WMI_SUCCESS_URL"]    = $DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?success_message=".translate_str_by_id(2722);
$fields["WMI_FAIL_URL"]       = $DP_Config->domain_path.$multilang_params['lang_href_no_slash']."/shop/balans?error_message=".translate_str_by_id(2122);

//Сортировка значений внутри полей
foreach($fields as $name => $val) 
{
	if (is_array($val))
	{
	   usort($val, "strcasecmp");
	   $fields[$name] = $val;
	}
}

// Формирование сообщения, путем объединения значений формы, 
// отсортированных по именам ключей в порядке возрастания.
uksort($fields, "strcasecmp");
$fieldValues = "";

foreach($fields as $value) 
{
	if (is_array($value))
	{
		foreach($value as $v)
		{
			//Конвертация из текущей кодировки (UTF-8)
			//необходима только если кодировка магазина отлична от Windows-1251
			$v = iconv("utf-8", "windows-1251", $v);
			$fieldValues .= $v;
		}
	}
	else
	{
	   //Конвертация из текущей кодировки (UTF-8)
	   //необходима только если кодировка магазина отлична от Windows-1251
	   $value = iconv("utf-8", "windows-1251", $value);
	   $fieldValues .= $value;
	}
}

// Формирование значения параметра WMI_SIGNATURE, путем 
// вычисления отпечатка, сформированного выше сообщения, 
// по алгоритму MD5 и представление его в Base64

$key = "";//Секретный ключ - не используем

$signature = base64_encode(pack("H*", md5($fieldValues . $key)));

//Добавление параметра WMI_SIGNATURE в словарь параметров формы
$fields["WMI_SIGNATURE"] = $signature;


echo "<form name=\"pay_form\" style=\"display:none\" action=\"https://www.walletone.com/checkout/default.aspx\" method=\"POST\">";

foreach($fields as $key => $val)
{
	if (is_array($val))
	foreach($val as $value)
	{
		echo "$key: <input type=\"text\" name=\"$key\" id=\"$key\" value=\"$value\"/>";
	}
	else     
		echo "$key: <input type=\"text\" name=\"$key\" id=\"$key\" value=\"$val\"/>";
}

echo "</form>";
?>
<script>
document.forms["pay_form"].submit();
</script>