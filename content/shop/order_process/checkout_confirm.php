<?php
/**
 * Страничный скрипт для подтверждения заказа
 * 
 * - выводим перечень товаров
 * - выводим способ получения
 * 
 * - также можно вывести ссылки для корректировки - на корзину и на способ получения
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_session = DP_User::getUserSession();

//Рекурвиная функция. Обрабатывает все значения древовидного массива через htmlentities
function prepare_json_htmlentities($how_get)
{
	if (!is_array($how_get))
	{
		return array();
	}
	foreach($how_get AS $key=>$value)
	{
		if( is_array($value) )
		{
			$how_get[$key] = prepare_json_htmlentities($value);
		}
		else
		{
			$how_get[$key] = htmlentities((string)$value);
		}
	}
	
	return $how_get;
}


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




//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
$epc_trade_checkout_blocked = ($user_id > 0 && !epc_trade_can_place_order($db_link, (int)$user_id));
$epc_trade_checkout_message = $epc_trade_checkout_blocked ? epc_trade_checkout_block_message($db_link, (int)$user_id) : '';

if($user_id > 0)
{
	//Поля для авторизованного пользователя
	$session_id = 0;
}
else
{
	//Поля для НЕавторизованного пользователя
	$session_record = DP_User::getUserSession();
	if($session_record == false)
	{
		$result = array();
		$result["status"] = false;
		$result["code"] = "incorrect_session";
		$result["message"] = translate_str_by_id(4460);
		exit(json_encode($result));
	}
	
	$session_id = $session_record["id"];
}


?>
<p class="lead"><?php echo translate_str_by_id(4506); ?></p>
<p><?php echo translate_str_by_id(4507); ?></p>
<?php
if (!empty($epc_trade_checkout_message)) {
	?>
	<div class="alert alert-warning" style="margin:15px 0;"><?php echo htmlspecialchars($epc_trade_checkout_message, ENT_QUOTES, 'UTF-8'); ?></div>
	<?php
}


//1. ВЫВОДИМ ПЕРЕЧЕНЬ ТОВАРОВ

//Получаем содержимое корзины
$cart_records = array();//Список для id записей корзины
$cart_records_query = $db_link->prepare('SELECT `id` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ?;');
$cart_records_query->execute( array($user_id, $session_id) );
while($cart_record = $cart_records_query->fetch() )
{
	array_push($cart_records, $cart_record["id"]);
}

//Отображаем:
?>
<div style="overflow: hidden; overflow-x: auto;">
    <table class="table">
		<tr>
			<th style="vertical-align: middle; white-space: nowrap;"><?php echo translate_str_by_id(4508); ?></th>
			<th style="vertical-align: middle; white-space: nowrap; text-align: right;"><?php echo translate_str_by_id(3550); ?></th>
			<th style="vertical-align: middle; white-space: nowrap; text-align: right;"><?php echo translate_str_by_id(2751); ?></th>
			<th style="vertical-align: middle; white-space: nowrap; text-align: center;"><?php echo translate_str_by_id(2752); ?></th>
			<th style="vertical-align: middle; white-space: nowrap; text-align: right;"><?php echo translate_str_by_id(3251); ?></th>
		</tr>
<?php
$price_total = 0;//Сумма заказа
for($i=0; $i < count($cart_records); $i++)
{
	$cart_record_query = $db_link->prepare('SELECT * FROM `shop_carts` WHERE `id` = ? AND user_id = ? AND `checked_for_order` = 1;');
	$cart_record_query->execute( array($cart_records[$i], $user_id) );
	$cart_record = $cart_record_query->fetch();
	if( $cart_record == false )
	{
		continue;
	}
    
    
    $name = $cart_record["t2_manufacturer"]." ".$cart_record["t2_article"]." ".$cart_record["t2_name"];
    $name = trim($name);
    
	//Срок поставки
	if($cart_record["t2_time_to_exe"] < $cart_record["t2_time_to_exe_guaranteed"]){
		$cart_object["time_to_exe"] = $cart_record["t2_time_to_exe"] .' - '. $cart_record["t2_time_to_exe_guaranteed"];
	}else{
		$cart_object["time_to_exe"] = $cart_record["t2_time_to_exe"];
	}
	if($cart_object["time_to_exe"] == 0){
		$cart_object["time_to_exe"] = translate_str_by_id(4197);
	}else{
		$cart_object["time_to_exe"] = $cart_object["time_to_exe"] . ' '.translate_str_by_id(4097).'.';
	}
	
    //CSS подкласс для оформления
    $sub_css = "";
    if(count($cart_records) == 1)
    {
        $sub_css = " product_div_single";
    }
    else if($i == 0)
    {
        $sub_css = " product_div_first";
    }
    else if($i == count($cart_records) - 1)
    {
        $sub_css = " product_div_last";
    }
    
    
    //Считаем деньги:
	$price = $cart_record["price"];//Цена позиции
    $price_sum = $price*$cart_record["count_need"];//Сумма по позиции
    $price_total = $price_total + $price_sum;//Сумма заказа
    ?>
    
    
    
    <tr>
        <td style="vertical-align: middle; width: 100%; min-width: 200px; max-width: 800px; word-wrap: break-word;">
            <?php echo $name; ?>
        </td>
        
		<?php
		//Строки с ценами:
		$price = number_format($price, 2, '.', ' ');
		$price_sum = number_format($price_sum, 2, '.', ' ');
		//Индикатор валюты перед ценой
		if($DP_Config->currency_show_mode == "sign_before")
		{
			$price = "<font class=\"currency\">$currency_indicator</font> <font class=\"price\">".$price."</font>";
			$price_sum = "<b><font class=\"currency\">$currency_indicator</font> <font class=\"price\">".$price_sum."</font></b>";
		}
		//Индикатор валюты после цены
		else if($DP_Config->currency_show_mode == "sign_after" || $DP_Config->currency_show_mode == "short_name_after")
		{
			$price = "<font class=\"price\">".$price."</font> <font class=\"currency\">$currency_indicator</font>";
			$price_sum = "<b><font class=\"price\">".$price_sum."</font> <font class=\"currency\">$currency_indicator</font></b>";
		}
		?>
		
		
        <td style="vertical-align: middle; white-space: nowrap; text-align: right;">
            <?php echo $cart_object["time_to_exe"]; ?>
        </td>
        <td style="vertical-align: middle; white-space: nowrap; text-align: right;">
            <?php echo $price; ?>
        </td>
        
        <td style="vertical-align: middle; white-space: nowrap; text-align: center;">
            <?php echo $cart_record["count_need"]; ?>
        </td>
        
        <td style="vertical-align: middle; white-space: nowrap; text-align: right;">
            <?php echo $price_sum; ?>
        </td>
    </tr>
    <?php
}
?>
</table>
</div>

<?php
//Строка с суммой:
$price_total = number_format($price_total, 2, '.', ' ');
//Индикатор валюты перед ценой
if($DP_Config->currency_show_mode == "sign_before")
{
	$price_total = "<font class=\"currency\">$currency_indicator</font> <font class=\"price\">".$price_total."</font>";
}
//Индикатор валюты после цены
else if($DP_Config->currency_show_mode == "sign_after" || $DP_Config->currency_show_mode == "short_name_after")
{
	$price_total = "<font class=\"price\">".$price_total."</font> <font class=\"currency\">$currency_indicator</font>";
}
?>

<div style="margin-bottom: 0px; text-align: right; font-size: 18px; font-weight: bold;"><span style="font-size: 14px; font-weight: normal;"><?php echo translate_str_by_id(3503); ?>:</span> <?php echo $price_total; ?></div>

<div class="hidden-sm hidden-md hidden-lg" style="margin-bottom:40px;"></div>

<?php
//2. ВЫВОДИМ СПОСОБ ПОЛУЧЕНИЯ
$epc_how_get_url = (isset($multilang_params['lang_href']) ? $multilang_params['lang_href'] : '') . '/shop/checkout/how_get';
$how_get_raw = isset($_COOKIE['how_get']) ? (string)$_COOKIE['how_get'] : '';
$how_get_json = json_decode($how_get_raw, true);
if (!is_array($how_get_json) || empty($how_get_json['mode']))
{
	?>
	<div class="alert alert-warning" style="margin:16px 0;">Please choose a delivery / pickup method to continue checkout.</div>
	<p><a class="btn btn-ar btn-primary" href="<?php echo htmlspecialchars($epc_how_get_url, ENT_QUOTES, 'UTF-8'); ?>">Choose delivery method</a></p>
	<?php
	return;
}
$how_get_json = prepare_json_htmlentities($how_get_json);
//Получаем имя папки с обработчиком:
$obtain_query = $db_link->prepare('SELECT * FROM `shop_obtaining_modes` WHERE `id` = ? AND `available` = 1;');
$obtain_query->execute( array((int)$how_get_json['mode']) );
$obtain_mode = $obtain_query->fetch();
$epc_obtain_handler = (is_array($obtain_mode) && !empty($obtain_mode['handler']))
	? preg_replace('/[^a-zA-Z0-9_-]/', '', (string)$obtain_mode['handler'])
	: '';
$epc_obtain_details = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/obtaining_modes/' . $epc_obtain_handler . '/show_details.php';
if ($epc_obtain_handler === '' || !is_file($epc_obtain_details))
{
	?>
	<div class="alert alert-danger" style="margin:16px 0;">The selected delivery method is unavailable. Please choose another method.</div>
	<p><a class="btn btn-ar btn-primary" href="<?php echo htmlspecialchars($epc_how_get_url, ENT_QUOTES, 'UTF-8'); ?>">Choose delivery method</a></p>
	<?php
	return;
}
echo '<div style="overflow-x: auto;">';
require_once $epc_obtain_details;
echo '</div>';
?>






<div class="row">
	<div class="col-lg-12">
		<p class="lead"><?php echo translate_str_by_id(4509); ?>:</p>
		<textarea style="height: 36px;" class="form-control" id="message_textarea" rows="1" placeholder="<?php echo translate_str_by_id(4510); ?>..."></textarea>
	</div>
</div>

<?php
$epc_checkout_show_po = ($user_id > 0);
$epc_checkout_customer_type = '';
if ($epc_checkout_show_po) {
	$epc_checkout_customer_type = epc_trade_profile_get($db_link, (int)$user_id, 'epc_customer_type', '');
}
if ($epc_checkout_show_po) {
?>
<div class="row" style="margin-top:12px;">
	<div class="col-lg-6">
		<p class="lead">Purchase order (PO) number<?php echo ($epc_checkout_customer_type === 'wholesale') ? '' : ' <small class="text-muted">(optional)</small>'; ?>:</p>
		<input style="height: 36px;" class="form-control" type="text" id="buyer_po_number" maxlength="64" placeholder="Your internal PO reference" />
	</div>
</div>
<?php
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_complementary_parts.php';
$epc_complementary_html = epc_complementary_render_html(
	epc_complementary_suggest_for_cart($db_link, (int)$user_id, (int)$session_id, 8),
	'Customers also order these related parts'
);
if ($epc_complementary_html !== '') {
	echo $epc_complementary_html;
}
?>








<?php
//3. Для неавторизованного пользователя - нужно указать контакты
if($user_id == 0)
{
	?>
	<div class="row" style="margin-top:20px;">
		<div class="col-lg-6">
			<p class="lead"><?php echo translate_str_by_id(4511); ?>*:</p>
			<input style="height: 36px;" class="form-control" type="text" id="phone_not_auth" value="" placeholder="<?php echo translate_str_by_id(4512); ?>" />
		</div>

	
		<div class="col-lg-6">
			<p class="lead">E-mail:</p>
			<input style="height: 36px;" class="form-control" type="text" id="email_not_auth" value="" placeholder="<?php echo translate_str_by_id(4513); ?>" />
		</div>

		<div class="col-xs-12 hidden-lg" style="margin-top: 20px;"></div>
	</div>
	<?php	
}
?>


<script>
//ОБРАБОТКА КНОПКИ ПОДТВЕРЖДЕНИЯ
function confirm()
{
	document.getElementById("confirm_btn").style.display = 'none';
	document.getElementById("confirm_loader").style.display = 'block';
	
	var result = confirm_order();
	
	if(result == false){
		document.getElementById("confirm_loader").style.display = 'none';
		document.getElementById("confirm_btn").style.display = 'inline';
	}
}



//ПОДТВЕРЖДЕНИЕ ЗАКАЗА
function confirm_order()
{
	//Проверка согласия с обработкой персональных данных
	if( !check_user_agreement() )
	{
		return false;
	}
	
	
	
	var phone_not_auth = '';
	var email_not_auth = '';
	<?php
	//Для неавторизованного получаем контакты
	if( $user_id == 0 )
	{
		?>
		//Телефон - обязателен
		phone_not_auth = document.getElementById("phone_not_auth").value;
		if( String(phone_not_auth) == '' )
		{
			alert("<?php echo translate_str_by_id(4514); ?>");
			return false;
		}
		
		//E-mail - не обязателен
		email_not_auth = document.getElementById("email_not_auth").value;
		
		//var date = new Date(new Date().getTime() + 15552000 * 1000);
		//document.cookie = "phone_not_auth="+encodeURIComponent(phone_not_auth)+"; path=/; expires=" + date.toUTCString();
		<?php
		//Проверка контактов на соответствие регулярному выражению
		//Телефон
		$phone_field_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `name` = ?;');
		$phone_field_query->execute( array('phone') );
		$phone_field = $phone_field_query->fetch();
		$phone_field_regexp = $phone_field["regexp"];
		if( $phone_field_regexp != "" )
		{
			//Телефон проверяем в любом случае, т.к. он обязателен
			?>
			var current_value = String(phone_not_auth);//Заполненное значение
			var regex = new RegExp('<?php echo $phone_field_regexp; ?>');//Регулярное выражение для поля
			//Далее ищем подстроку по регулярному выражению
			var match = regex.exec(String(current_value));
			if(match == null)
			{
				alert("<?php echo translate_str_by_id(4515); ?>");
				return false;
			}
			else
			{
				var match_value = String(match[0]);//Подходящая подстрока
				if(match_value != current_value)
				{
					alert("<?php echo translate_str_by_id(4516); ?>");
					return false;
				}
			}
			<?php
		}
		//E-mail
		$email_field_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `name` = ?;');
		$email_field_query->execute( array('email') );
		$email_field = $email_field_query->fetch();
		$email_field_regexp = $email_field["regexp"];
		if( $email_field_regexp != "" )
		{
			//E-mail проверяем только в случае его заполнения клиентом, т.к. email заполнять не обязательно для неавторизованного пользователя
			?>
			if( String( email_not_auth ) != "" )
			{
				var current_value = String(email_not_auth);//Заполненное значение
				var regex = new RegExp('<?php echo $email_field_regexp; ?>');//Регулярное выражение для поля
				//Далее ищем подстроку по регулярному выражению
				var match = regex.exec(String(current_value));
				if(match == null)
				{
					alert("<?php echo translate_str_by_id(4517); ?>");
					return false;
				}
				else
				{
					var match_value = String(match[0]);//Подходящая подстрока
					if(match_value != current_value)
					{
						alert("<?php echo translate_str_by_id(4518); ?>");
						return false;
					}
				}
				//Заполнено правильно, если: есть подстрока по регулярному выражению и она полностью равна самой строке
			}
			<?php
		}
		
	}//~if - пользователь не авторизован
	?>

	
	// Комментарий заказа
	var message = document.getElementById("message_textarea").value;
	var buyer_po = '';
	var poEl = document.getElementById("buyer_po_number");
	if (poEl) {
		buyer_po = poEl.value || '';
	}
	
	
    jQuery.ajax({
        type: "POST",
        async: true, //Запрос синхронный
        url: "/content/shop/order_process/ajax_checkout_create.php",
        dataType: "text",//Тип возвращаемого значения
		data: "order_message="+encodeURIComponent(message)+"&buyer_po_number="+encodeURIComponent(buyer_po)+"&phone_not_auth="+encodeURIComponent(phone_not_auth)+"&email_not_auth="+encodeURIComponent(email_not_auth)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
        success: function(answer)
        {
			console.log(answer);
				
			var answer_ob = JSON.parse(answer);
			
			//Если некорректный парсинг ответа
			if( typeof answer_ob.status === "undefined" )
			{
				alert("<?php echo translate_str_by_id(4519); ?>");
			}
			else
			{
				//Корректный парсинг ответа
				if(answer_ob.status == true)
				{
					<?php
					//Заказ успешно создан - далее переадресация.
					//Для зарегистрированного клиента - страница заказа
					if($user_id != 0)
					{
						?>
						location = "<?php echo $multilang_params['lang_href']; ?>/shop/orders/order?order_id="+answer_ob.order_id+"&success_message="+encodeURI("<?php echo translate_str_by_id(4520); ?>.");
						<?php
					}
					else//Для незарегистрированного - страница с информацией по заказу
					{
						?>
						location = "<?php echo $multilang_params['lang_href']; ?>/shop/orders/zakaz-bez-registracii?order_id="+answer_ob.order_id+"&success_message="+encodeURI("<?php echo translate_str_by_id(4520); ?>.");
						<?php
					}
					?>
				}
				else
				{
					alert(answer_ob.message);
				
					document.getElementById("confirm_loader").style.display = 'none';
					document.getElementById("confirm_btn").style.display = 'inline';
					
					return false;
				}
			}
        }
    });
}
</script>


<?php
//Подключаем общий модуль принятия пользовательского соглашения
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/users_agreement_module.php");
?>


<div class="order_confirm_button_div text-center">
	<?php if (!empty($epc_trade_checkout_blocked)) { ?>
	<p class="text-muted">Place order is disabled until your trade account is approved.</p>
	<?php } else { ?>
	<button id="confirm_btn" class="btn btn-ar btn-primary" onclick="confirm();"><?php echo translate_str_by_id(4521); ?></button>
	
	<div id="confirm_loader" style="display:none;">
		<p><?php echo translate_str_by_id(4293); ?></p>
		<img src="/content/files/images/ajax-loader-transparent.gif" />
	</div>
	<?php } ?>
</div>