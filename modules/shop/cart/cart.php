<?php
/**
Скрипт для модуля корзины
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();

//Получаем данные по валюте магазина
$stmt = $db_link->prepare('SELECT * FROM `shop_currencies` WHERE `iso_code` = :iso_code;');
$stmt->bindValue(':iso_code', $DP_Config->shop_currency);
$stmt->execute();
$currency_record = $stmt->fetch(PDO::FETCH_ASSOC);
$currency_sign = $currency_record["sign"];
//Строка для обозначения валюты
if($DP_Config->currency_show_mode == "no")
{
	$currency_indicator = "";
}
else if($DP_Config->currency_show_mode == "sign_before" || $DP_Config->currency_show_mode == "sign_after")
{
	$currency_indicator = $currency_sign;
}
else
{
	$currency_indicator = $currency_record["caption_short"];
}





require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

$cart_items_count = 0;
$cart_items_sum = 0;

//Если пользователь авторизован - получаем содержимое его корзины из базы данных
if($user_id > 0)
{
	$stmt = $db_link->prepare('SELECT COUNT(`id`) AS `count`, IFNULL(SUM(`price`*`count_need`), 0) AS `sum` FROM `shop_carts` WHERE `user_id` = :user_id;');
	$stmt->bindValue(':user_id', $user_id);
	$stmt->execute();
	$cart_record = $stmt->fetch(PDO::FETCH_ASSOC);
	if( $cart_record != false )
	{
		$cart_items_count = $cart_record["count"];
		$cart_items_sum = $cart_record["sum"];
	}
}
else//Если пользователь не авторизован, то получаем записи корзины из куки (id записей)
{
    if($_COOKIE["products_in_cart"] != NULL)
    {
		$cart_in_cookie = json_decode($_COOKIE["products_in_cart"], true);
		
		
		
		$id_list_str = str_repeat('?,', count($cart_in_cookie) - 1).'?';		
		$stmt = $db_link->prepare('SELECT COUNT(`id`) AS `count`, IFNULL(SUM(`price`*`count_need`), 0) AS `sum` FROM `shop_carts` WHERE `id` IN ('.$id_list_str.');');
		$stmt->execute($cart_in_cookie);
		$cart_record = $stmt->fetch(PDO::FETCH_ASSOC);
		
		if( $cart_record != false )
		{
			$cart_items_count = $cart_record["count"];
			$cart_items_sum = $cart_record["sum"];
		}
    }
}


$cart_items_sum = number_format($cart_items_sum, 2, '.', '');


//Индикатор валюты перед ценой
if($DP_Config->currency_show_mode == "sign_before")
{
	$cart_items_sum = $currency_indicator." ".$cart_items_sum;
}
else
{
	$cart_items_sum = $cart_items_sum." ".$currency_indicator;
}
?>


<div class="cart_module" onclick="location = '<?php echo $multilang_params['lang_href']; ?>/shop/cart';">
	<div class="cart_module_positions" id="cart_module_positions"><b><?php echo translate_str_by_id(4495); ?></b> <?php echo $cart_items_count; ?></div>
	<div class="cart_module_sum" id="cart_module_sum"><b>На сумму</b> <?php echo $cart_items_sum; ?></div>
</div>