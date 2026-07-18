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


// Available modes that still have a customer_interface handler on disk.
$epc_obtain_modes = array();
$obtain_modes_query = $db_link->prepare('SELECT * FROM `shop_obtaining_modes` WHERE `available` = 1 ORDER BY `order`;');
$obtain_modes_query->execute();
while ($obtain_mode = $obtain_modes_query->fetch())
{
	$handler_name = preg_replace('/[^a-zA-Z0-9_-]/', '', (string)$obtain_mode['handler']);
	$interface_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/obtaining_modes/' . $handler_name . '/customer_interface.php';
	if ($handler_name === '' || !is_file($interface_path))
	{
		continue;
	}
	$obtain_mode['handler'] = $handler_name;
	$obtain_mode['_interface_path'] = $interface_path;
	$epc_obtain_modes[] = $obtain_mode;
}

//Определяем текущий способ
$current_obtain_mode = 0;
if (isset($_COOKIE['obtain_mode']))
{
	$current_obtain_mode = (int)$_COOKIE['obtain_mode'];
}
$handler = '';
$epc_obtain_interface = '';
foreach ($epc_obtain_modes as $obtain_mode)
{
	if ($current_obtain_mode === (int)$obtain_mode['id'])
	{
		$handler = $obtain_mode['handler'];
		$epc_obtain_interface = $obtain_mode['_interface_path'];
		break;
	}
}
if ($handler === '' && !empty($epc_obtain_modes))
{
	$current_obtain_mode = (int)$epc_obtain_modes[0]['id'];
	$handler = $epc_obtain_modes[0]['handler'];
	$epc_obtain_interface = $epc_obtain_modes[0]['_interface_path'];
}
?>


<p class="lead"><?php echo translate_str_by_id(4522); ?>:</p>


<?php
if (empty($epc_obtain_modes))
{
	?>
	<div class="alert alert-danger">No delivery / pickup methods are available right now. Please contact support.</div>
	<?php
	return;
}
//Вывод способов получения
foreach ($epc_obtain_modes as $obtain_mode)
{
	$checked = "";
	if( $current_obtain_mode == $obtain_mode["id"] )
	{
		$checked = " checked=\"checked\" ";
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
require_once $epc_obtain_interface;
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
