<?php
/**
 * Страничный скрипт для выбора способа получения заказа
*/
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
$epc_checkout_user_id = (int)DP_User::getUserId();
if ($epc_checkout_user_id > 0 && !epc_trade_can_place_order($db_link, $epc_checkout_user_id)) {
	$epc_block_msg = epc_trade_checkout_block_message($db_link, $epc_checkout_user_id);
	?>
	<div class="alert alert-warning"><?php echo htmlspecialchars($epc_block_msg, ENT_QUOTES, 'UTF-8'); ?></div>
	<p><a class="btn btn-default" href="<?php echo $multilang_params['lang_href']; ?>/shop/cart">Back to cart</a></p>
	<?php
	return;
}


//Определяем текущий способ
if( !isset($_COOKIE["obtain_mode"]) )
{
	$first_obtain_mode_query = $db_link->prepare('SELECT `id` FROM `shop_obtaining_modes` WHERE `available` = 1 ORDER BY `order` LIMIT 1;');
	$first_obtain_mode_query->execute();
	$first_obtain_mode_record = $first_obtain_mode_query->fetch();
	$current_obtain_mode = (int) $first_obtain_mode_record["id"];
}
else
{
	$current_obtain_mode = (int) $_COOKIE["obtain_mode"];
}
?>


<p class="lead"><?php echo translate_str_by_id(4522); ?>:</p>


<?php
//Вывод способов получения
$obtain_modes_query = $db_link->prepare('SELECT  * FROM `shop_obtaining_modes` WHERE `available` = 1 ORDER BY `order`;');
$obtain_modes_query->execute();
while( $obtain_mode = $obtain_modes_query->fetch() )
{
	$checked = "";
	if( $current_obtain_mode == $obtain_mode["id"] )
	{
		$checked = " checked=\"checked\" ";
		$handler = $obtain_mode["handler"];
	}
	
	?>
	<div class="radio">
		<input onchange="onHowGetChanged(<?php echo $obtain_mode["id"]; ?>);" type="radio" name="how_get_radio" value="<?php echo $obtain_mode["id"]; ?>" id="how_get_radio_<?php echo $obtain_mode["id"]; ?>" class="radio_how_get" <?php echo $checked; ?> /><label class="label_how_get" for="how_get_radio_<?php echo $obtain_mode["id"]; ?>" onclick="onHowGetChanged(<?php echo $obtain_mode["id"]; ?>);"><?php echo translate_str_by_id($obtain_mode["caption"]); ?></label>
	</div>
	<?php
}
?>







<!-- Блок с настроками способа получения -->
<div id="how_get_options_div">
<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/obtaining_modes/".$handler."/customer_interface.php");
?>
</div>



<script>
// ------------------------------------------------------------------------------------
//Обработка изменения способа получения
function onHowGetChanged(mode)
{
	//Устанавливаем cookie (на полгода)
    var date = new Date(new Date().getTime() + 15552000 * 1000);
    document.cookie = "obtain_mode="+JSON.stringify(mode)+"; path=/; expires=" + date.toUTCString();
	
	location="<?php echo $multilang_params['lang_href']; ?>/shop/checkout/how_get";
}
// ------------------------------------------------------------------------------------
</script>