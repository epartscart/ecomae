<?php
/**
 * Серверный скрипт для получения информации по заказу, который был оформлен без регистрации
*/


//Этот скрипт больше не требуется. Его можно удалить
exit;



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
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");




//Получаем данные по валюте магазина
$currency_query = $db_link->prepare('SELECT * FROM `shop_currencies` WHERE `iso_code` = ?;');
$currency_query->execute( array($DP_Config->shop_currency) );
$currency_record = $currency_query->fetch();
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





//Общая информация по заказам
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");


//Объект запроса от клиента
$query_object = json_decode($_POST["query_object"], true);

//1. CAPTCHA
//Проверям правильность ввода captcha, чтобы исключить вероятность обращения напрямую
//Получаем значение от пользователя и сразу переводим его в md5:1
$user_captcha = md5($query_object["captcha"]);
//Правильная captcha из Куки, которая уже в md5:
$cookie_captcha = $_COOKIE["captcha"];

if($user_captcha != $cookie_captcha)
{
    ?>
    Captcha incorrect
    <?php
	exit();
}
//Связываемые значения
$binding_values = array();

$binding_values[] = $query_object["order_id"]; //Первый order_id

$WHERE_statuses_not_count = '';

for ( $i=0; $i<count($orders_items_statuses_not_count ); $i++ ) {
	
	$WHERE_statuses_not_count .= ' AND `status` != ?';
	
	$binding_values[] = $orders_items_statuses_not_count[$i];
	
}

$binding_values[] = $query_object["order_id"]; //Второй order_id

//2. Получаем данные заказа
$SQL_SELECT_ORDER = "SELECT *, (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = ? {$WHERE_statuses_not_count} ) AS `sum`, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption` FROM `shop_orders` WHERE `id` = ? AND `user_id` = 0;";
$order_query = $db_link->prepare($SQL_SELECT_ORDER);
$order_query->execute( $binding_values );
$order = $order_query->fetch();
if($order == false)
{
    ?>
    Заказ не найден. Возможно Вы оформили этот заказ авторизовавшись под своим логином. В этом случае заказ можно найти на <a class="text_a" href="/shop/orders">странице заказов зарегистрированных покупателей</a><br>
    <?php
	exit();
}
else
{
    $time = $order["time"];
    $office_id = $order["office_id"];
    $status = $order["status"];
    $paid = $order["paid"];
    $customer_id = $order["user_id"];
    $sum = number_format($order["sum"], 2, '.', '');
	$obtain_caption = $order["obtain_caption"];
    ?>
    <table class="table">
        <tr> <td>Номер заказа</td> <td><?php echo $query_object["order_id"]; ?></td> <tr>
        <tr> <td>Создан</td> <td><?php echo date("d.m.Y", $time)." ".date("G:i", $time); ?></td> <tr>
        <tr> <td>Офис обслуживания</td> <td><?php echo $offices_list[$office_id]["caption"]; ?></td> <tr>
        <tr> <td>Способ получения</td> <td><?php echo $obtain_caption; ?></td> <tr>
        <tr> <td>Статус</td> <td><?php echo $orders_statuses[$status]["name"]; ?></td> <tr>
        <tr> <td>Оплачен</td> <td><?php if($paid)echo "Да";else echo "Нет"; ?></td> <tr>
        <tr> <td>Сумма</td> <td> <?php echo $currency_indicator." ".$sum; ?></td> <tr>
    </table>
	
	
	<a class="product_main_button btn btn-ar btn-primary" href="javascript:void(0);" onclick="direct_pay_by_id(<?php echo $query_object["order_id"]; ?>);">Оплатить online</a><br>
    <?php
}
?>