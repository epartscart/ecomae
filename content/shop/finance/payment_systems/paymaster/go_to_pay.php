<?php
/**
 * Скрипт перехода на страницу оплаты
 * https://paymaster.ru/docs/ru/wmi
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
	$operation_description = translate_str_by_id(4350)." id ". $operation["pay_orders"];
}



//Общий скрипт получения настроек платежной системы.
require_once( $_SERVER['DOCUMENT_ROOT'].'/content/shop/finance/get_pay_system_parameters.php' );



$amount = $operation["amount"];



//*************************************************************************
//*************************************************************************
//*************************************************************************

// ПОЛУЧАЕМ ПОЧТУ ИЛИ ТЕЛЕФОН КЛИЕНТА НА КОТОРУЮ БУДЕТ ОТПРАВЛЕН ЭЛЕКТРОННЫЙ ЧЕК

$email = '';
$phone = '';

if($operation["user_id"] > 0)
{
	// Клиент зарегистрирован
	$main_field_query = $db_link->prepare("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ?;");
	$main_field_query->execute( array($operation["user_id"]) );
	$main_field_record = $main_field_query->fetch();
	
	$phone = '+7'.$main_field_record["phone"];
	$email = $main_field_record["email"];
	
}else{
	// Клиент без регистрации, возьмем данные для уведомления из заказа
	if(!empty($operation["pay_orders"]))
	{
		
		$order_id_tmp = (int) trim($operation["pay_orders"]);
		
		
		$order_data_query = $db_link->prepare("SELECT * FROM `shop_orders` WHERE `id` = ?;");
		$order_data_query->execute( array($order_id_tmp) );
		
		
		$order_data_record = $order_data_query->fetch();
		
		$phone = '+7'.$order_data_record["phone_not_auth"];
		$email = $order_data_record["email_not_auth"];
	}
}



// ДАННЫЕ ДЛЯ ФОРМИРОВАНИЯ ЧЕКА
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

$positions = array();

if($flag_pay_orders == false){
	$positions[] = array(
		"description" => translate_str_by_id(4378)." id $user_id",
		"quantity" => 1,
		"amount" => $amount
	);
	
}else{
	
	$order_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` = ?;");
	$order_items_query->execute( array($operation["pay_orders"]) );
	$count_need_total = 0;//Итого количество
	$price_sum_total = 0;//Итого сумма
	while($order_items_record = $order_items_query->fetch()){
		
		$item_status 		= $order_items_record["status"];
		$item_count_need    = $order_items_record["count_need"];
		$item_price         = $order_items_record["price"];
		
		//Считаем поля ИТОГО ПО ЗАКАЗУ (если статус позиции позволяет)
		if( array_search($item_status, $orders_items_statuses_not_count) === false)
		{
			$count_need_total += $item_count_need;
			$price_sum_total  += $item_price * $item_count_need;
		}else{
			// Если статус позиции - отмена то пропускаем позицию
			continue;
		}
		
		// Получаем наименование товара
		$name = '';
		if($order_items_record['product_type'] == 1){
			$product_id = (int)$order_items_record['product_id'];
			$product_query = $db_link->prepare("SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = ?;");
			$product_query->execute( array($product_id) );
			$caption = $product_query->fetch();
			$caption = $caption['caption'];
			$name = mb_substr($caption, 0, 100, 'UTF-8');
		}else{
			$name = $order_items_record['t2_manufacturer'] .' - '. $order_items_record['t2_article'] .' - '. $order_items_record['t2_name'];
		}
		
		if($name == ''){
			$name = translate_str_by_id(4376);
		}
		
		$positions[] = array(
			"description" => $name,
			"quantity" => $order_items_record['count_need'],
			"amount" => $order_items_record['price']
		);
	}
}

//*************************************************************************
//*************************************************************************
//*************************************************************************
?>
<div style="margin:auto; width:300px; height:40px;
position:absolute; left:50%; margin-left:-150px; text-align:center; top:50%; margin-top:-20px;"><img src="/content/files/images/ajax-loader-transparent.gif"/><br/><?php echo translate_str_by_id(4293); ?>...</div>
<form style="display:none;" action="https://paymaster.ru/payment/init" method="post">
    <input name="LMI_MERCHANT_ID" value="<?php echo $paysystem_parameters["paymaster_lmi_merchant_id"]; ?>" type="hidden"/>
    <input name="LMI_PAYMENT_AMOUNT" value="<?php echo $operation["amount"]; ?>" type="hidden"/>
    <input name="LMI_CURRENCY" value="RUB" type="hidden">
    <input name="LMI_PAYMENT_NO" value="<?php echo $operation_id; ?>" type="hidden"/>
    <input name="LMI_PAYMENT_DESC" value="<?php echo $operation_description; ?>" type="hidden"/>
    
	<?php
	if($email != ''){
	?>
	<input name="LMI_PAYER_EMAIL" value="<?php echo $email; ?>" type="hidden"/>
	<?php
	}
	?>
	
	<?php
	if($phone != ''){
	?>
	<input name="LMI_PAYER_PHONE_NUMBER" value="<?php echo $phone; ?>" type="hidden"/>
	<?php
	}
	?>
    
	<?php
	if(!empty($positions)){
		$i = 0;
		foreach($positions as $item){
			?>
			
			<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].NAME" value="<?php echo $item['description']; ?>" type="hidden"/>
			<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].QTY" value="<?php echo $item['quantity']; ?>" type="hidden"/>
			<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].PRICE" value="<?php echo $item['amount']; ?>" type="hidden"/>
			<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].TAX" value="no_vat" type="hidden"/>
			<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].METHOD" value="4" type="hidden"/>
			
			<?php
			if($operation["pay_orders"] != "" && $operation["pay_orders"] != NULL){
			?>
				<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].METHOD" value="1" type="hidden"/>
			<?php
			}else{
			?>
				<input name="LMI_SHOPPINGCART.ITEMS[<?=$i;?>].METHOD" value="10" type="hidden"/>
			<?php
			}
			
			$i++;
		}
	}
	?>
	
  <input id="btn" type="submit"/>
</form>
<script>
	document.getElementById('btn').click();
</script>