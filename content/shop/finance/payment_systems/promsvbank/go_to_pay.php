<?php
/**
 * Скрипт перехода на страницу оплаты
*/
date_default_timezone_set('Europe/Moscow');

//------------------------------Подключаем необходимые библиотеки---------------------------------//
require_once($_SERVER["DOCUMENT_ROOT"] ."/config.php");
require_once($_SERVER["DOCUMENT_ROOT"] ."/content/users/dp_user.php");
require_once("genHmacStr.php");
//-------------------------------------------------------------------------------------------------//

$DP_Config = new DP_Config;//Конфигурация CMS
//Соединение с БД
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

include($_SERVER["DOCUMENT_ROOT"] ."/content/shop/order_process/get_customer_offices.php");


//Получаем данные точки обслуживания
$office_id = $customer_offices[0]['office_id'];

$result_query_office = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
$result_query_office->execute( array($office_id) );
$office_profile = $result_query_office->fetch();

//Для работы с пользователями
$user_id = DP_User::getUserId();

$operation_id = (int)$_GET["operation"];

//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );


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

$sum = $operation["amount"];

$time = time() - 60*60*3;

$date = date("YmdHis", $time);

$url_back = $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] ."/shop/balans?".urlencode( translate_str_by_id(4379) );

//----------------------Данные для оправки в сис-му экваринга-------------------------------------------------------------//
// Формирования HMAC запроса проведения оплаты товара: 
// (длина значения параметра)+(значение параметра)

$random			= rand(1000000000000000000, 9000000000000000000); //Получаем рандомное число
$random 		= (int)$random * 2; //Увеличиваем в два раза, т.к нужно число в 16-сис-ме счисления, длиной 16 символов

$nonce			= dechex($random); //Переводим в x16
$offcie_name	= $office_profile["caption"];
$email 			= $office_profile["email"];

if($email == "" || NULL)
{
	$email = $DP_Config->smtp_username;
}

//Порядок формирования строк для хеша
// AMOUNT,CURRENCY,ORDER,MERCH_NAME,MERCHANT,TERMINAL,EMAIL,TRTYPE,TIMESTAMP,NONCE,BACKREF
$post = array(
	"AMOUNT"=>$sum, 											//Сумма операции
	"CURRENCY"=>"RUB",										//Валюта операции (RUB)
	"ORDER"=>"00000".$operation["id"],						//Уникальный номер заказа.()
	"MERCH_NAME"=>$paysystem_parameters["merchant_name"],	//Название Торговой точки
	"MERCHANT"=>$paysystem_parameters["merchant"], 			//Номер торговой точки.
	"TERMINAL"=>$paysystem_parameters["terminal"],			//Уникальный номер виртуального терминала торговой точки
	"EMAIL"=>$email, 											//Адрес эл. почты Торговой  точки
	"TRTYPE"=>1,    	 										//Тип запрашиваемой операции(int - 1 - Оплата)
	"TIMESTAMP"=>$date,										//UTC время проведения/ обработки операции в формате YYYYMMDDHHMISS (Московское время - 3 часа)
	"NONCE"=>$nonce,											//Случайное число в шестнадцатеричном формате
	"BACKREF"=>$url_back,										//URL для возврата на  сайт Торговой точки после  проведения операции
	"DESC"=>$operation_description,							//Описание заказа (необязательный)
	"P_SIGN"=>""												//HMAC запроса / ответа
);

$hmac = genHmacStr($post, $paysystem_parameters["secret_key"]);
$post["P_SIGN"] = $hmac;

$flagTest = $paysystem_parameters["flagTest"];
if($flagTest == 1)
{
	$url = "https://test.3ds.payment.ru/cgi-bin/cgi_link"; //Тестовый адрес запроса
}
else
{
	$url = "https://3ds.payment.ru/cgi-bin/cgi_link"; //Боевой 
}

$inputs = "";

foreach($post as $key => $value)
{
	$input = "<input type=\"hidden\" name=\"{$key}\" value=\"{$value}\" />";
	$inputs .= $input;
}

?>
<form method="POST" action="<?=$url;?>" name="pay_promsvbank" style="display: none;">

<?php
	echo $inputs;
?>
<input type="hidden" name="NOTIFY_URL" value="<?=$DP_Config->domain_path;?>content/shop/finance/payment_systems/promsvbank/notification.php" />
</form>

<script>
document.forms["pay_promsvbank"].submit();
</script>