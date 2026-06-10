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


$receiver = $paysystem_parameters["receiver"];// номер счета
$amount = $operation["amount"];// сумма

// адрес сайта
$domain = $DP_Config->domain_path;
$domain = str_replace('http://','',$domain);
$domain = str_replace('https://','',$domain);
$domain = str_replace('/','',$domain);

// Страница после оплаты счета
$lmi_success_url = $DP_Config->domain_path . $multilang_params['lang_href_no_slash'] . '/shop/balans?success_message='.translate_str_by_id(4400);

//////////////// Платежная система /////////////////
?>

<head>
    <meta charset="utf-8">
</head>
<body>
	<div style="width:400px; margin:auto; padding-top:20px;">
		<div style="background:#eee; text-align:center; border:1px solid #ccc;">
			<h2><?=$domain;?></h2>
			<h1><?=$operation_description;?></h1>
		</div>
		<div style="background:#fff;  border:1px solid #ccc;">
			<div style="overflow:hidden;">
				<div>
				
					<form method="POST" action="https://money.yandex.ru/quickpay/confirm.xml"> 
						<input type="hidden" name="receiver" value="<?=$receiver;?>"> 
						<input type="hidden" name="formcomment" value="<?=$domain;?>: <?=$operation_description;?>"> 
						<input type="hidden" name="short-dest" value="<?=$domain;?>: <?=$operation_description;?>"> 
						<input type="hidden" name="label" value="<?=$operation_id;?>"> 
						<input type="hidden" name="quickpay-form" value="shop"> 
						<input type="hidden" name="targets" value="<?php echo translate_str_by_id(4401); ?> [<?=$operation_id;?>]"> 
						<input type="hidden" name="sum" value="<?=number_format($amount, 2, '.', '');?>" data-type="number"> 
						<input type="hidden" name="successURL" value="<?=$lmi_success_url;?>"> 
						<div style="border-bottom:1px solid #ccc; padding:10px 10px;">
							<input style="cursor:pointer;" id="paymentType1" type="radio" name="paymentType" value="PC">
							<label for="paymentType1" style="cursor:pointer; display:inline-block; height:60px; background:url('/content/files/images/general/bank_yandex.png') 0 0 no-repeat; padding-left:60px;"><?php echo translate_str_by_id(4402); ?></label>
						</div>
						<div style="border-bottom:1px solid #ccc; padding:10px 10px;">
							<input style="cursor:pointer;" id="paymentType2" type="radio" name="paymentType" value="AC" checked >
							<label for="paymentType2" style="cursor:pointer; display:inline-block; height:60px; background:url('/content/files/images/general/bank_karta.png') 0 0 no-repeat; padding-left:60px;"><?php echo translate_str_by_id(4403); ?></label>
						</div>
						<div style="text-align:center; padding-top:20px;">
							<button type="submit"><?php echo translate_str_by_id(4404); ?></button>
						</div>
					</form>
					
				</div>
			</div>
		</div>
	</div>
</body>