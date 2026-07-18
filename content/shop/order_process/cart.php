<?php
/**
 * Страничный скрипт для корзины
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();


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




require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';
echo epc_storefront_prices_styles();
echo epc_wa_styles();
echo epc_wa_frontend_script($DP_Config);
$session_record = false;
if($user_id > 0)
{
	//Поля для авторизованного пользователя
	$session_id = 0;
}
else
{
	//Поля для НЕ авторизованного пользователя
	$session_record = DP_User::getUserSession();
	if($session_record == false)
	{
		// HTML cart page: do not return raw JSON (breaks the whole layout).
		$session_id = 0;
	}
	else
	{
		$session_id = (int)$session_record["id"];
	}
	if (epc_storefront_guest_commerce_blocked(0)) {
		if ($session_id > 0 && isset($db_link) && $db_link instanceof PDO) {
			epc_storefront_clear_guest_cart($db_link, $session_id);
		}
		$login = htmlspecialchars(epc_storefront_auth_login_url(isset($multilang_params) && is_array($multilang_params) ? $multilang_params : null), ENT_QUOTES, 'UTF-8');
		$signup = htmlspecialchars(epc_storefront_auth_signup_url(isset($multilang_params) && is_array($multilang_params) ? $multilang_params : null), ENT_QUOTES, 'UTF-8');
		?>
		<div id="cart_area">
			<div class="epc-cart-login-gate">
				<h2>Sign in to use your cart</h2>
				<p>Prices, checkout, quotes, and WhatsApp ordering are available after you log in or register.</p>
				<div class="epc-commerce-login-cta">
					<a class="btn btn-sm btn-primary" href="<?php echo $login; ?>">Log in</a>
					<span class="epc-commerce-login-cta__sep">or</span>
					<a class="btn btn-sm btn-default" href="<?php echo $signup; ?>">Register</a>
				</div>
			</div>
		</div>
		<?php
		return;
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
$epc_trade_cart_blocked = ($user_id > 0 && !epc_trade_can_place_order($db_link, (int)$user_id));
$epc_trade_cart_message = $epc_trade_cart_blocked ? epc_trade_checkout_block_message($db_link, (int)$user_id) : '';

if(!is_array($user_session) || empty($user_session["csrf_guard_key"]))
{
	$user_session = (is_array($session_record) && !empty($session_record["csrf_guard_key"]))
		? $session_record
		: array("csrf_guard_key" => "");
}




$cart_records = array();//Список записей корзины для этого пользователя

//Получаем содержимое его корзины из базы данных
$cart_records_query = $db_link->prepare('SELECT `id` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ?;');
$cart_records_query->execute( array($user_id, $session_id) );
while($cart_record = $cart_records_query->fetch() )
{
	array_push($cart_records, $cart_record["id"]);
}
?>




<div id="cart_area"></div>
<style>
#cart_area .table th,
#cart_area .table td{
	vertical-align: middle;
    white-space: nowrap;
}
#cart_area .table input[type="checkbox"]{
	width: 25px;
    height: 25px;
}
</style>



<?php
if(count($cart_records) == 0)
{
    ?>
    <script>
        document.getElementById("cart_area").innerHTML = "<?php echo translate_str_by_id(4472); ?>";
    </script>
    <?php
}
else
{
    ?>
    <script>
    // --------------------------------------------------------------------------------------
    //Переотобразить корзину
    function refreshCartArea()
    {
		//Строки с суммой в корзине
		sum_total = Number(sum_total).toFixed(2);
		var sum_total_num = Number(sum_total);
		sum_total = (typeof epcFormatMoney === 'function') ? epcFormatMoney(sum_total_num) : digit(sum_total);
		// ----------------------------------------------
		
        //Обновление корзины снизу
        if(typeof updateCartInfo == 'function') {
            updateCartInfo();
        }
        
        // ----------------------------------------------
        
		//Обработка модуля корзины - в случае наличия: (Для старых шаблонов)
		var cart_module_positions = document.getElementById("cart_module_positions");
		if(cart_module_positions != undefined)
		{
			cart_module_positions.innerHTML = "<b><?php echo translate_str_by_id(4495); ?></b> " + cart_records.length;
		}
		var cart_module_sum = document.getElementById("cart_module_sum");
		if(cart_module_sum != undefined)
		{
			cart_module_sum.innerHTML = "<b><?php echo translate_str_by_id(4496); ?></b> " + sum_total;
		} 
		
		// ----------------------------------------------
		
		
        //Если последний товар был удален из корзины
        if(cart_records.length == 0)
        {
            document.getElementById("cart_area").innerHTML = "<?php echo translate_str_by_id(4472); ?>";
            return;
        }
        
       
		// Определим есть ли в корзине товары с изображениями и только при их наличии будем отображать колонку с картинкой товара
		var there_is_images = false;
		for(var i=0; i < cart_records.length; i++)
        {
			if( cart_records[i].image_src != '' ){
				there_is_images = true;
				break;
			}
		}
		
		// Определим все ли позиции уорзины выбраны
		var check_all = 'checked';
		for(var i=0; i < cart_records.length; i++)
        {
			if( cart_records[i].checked_for_order == 0 ){
				check_all = '';
				break;
			}
		}
		
		var cart_html = "";//HTML корзины
		
		cart_html += '<div style="overflow: hidden; overflow-x: auto;">';
		cart_html += '<table class="table cart_table">';
		cart_html += 	'<tr>';
		cart_html += 		'<th><input type="checkbox" '+check_all+' id="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>';
		
		if(there_is_images === true){
		cart_html += 		'<th></th>';
		}
		
		cart_html += 		'<th><?php echo translate_str_by_id(2070); ?></th>';
		cart_html += 		'<th><?php echo translate_str_by_id(2071); ?></th>';
		cart_html += 		'<th><?php echo translate_str_by_id(2102); ?></th>';
		cart_html += 		'<th><?php echo translate_str_by_id(3550); ?></th>';
		cart_html += 		'<th><?php echo translate_str_by_id(2751); ?></th>';
		cart_html += 		'<th><?php echo translate_str_by_id(2752); ?></th>';
		cart_html += 		'<th><?php echo translate_str_by_id(3251); ?></th>';
		cart_html += 		'<th></th>';
		cart_html += 	'</tr>';
		
        for(var i=0; i < cart_records.length; i++)
        {
            var checked_for_order = 'checked';
			if( cart_records[i].checked_for_order == 0 ){
				checked_for_order = '';
			}
			
			//Строки с ценами
			var price = (typeof epcFormatMoney === 'function') ? epcFormatMoney(cart_records[i].price) : digit(Number(cart_records[i].price).toFixed(2));//Цена позиции
			var price_sum = (typeof epcFormatMoney === 'function') ? epcFormatMoney(cart_records[i].price_sum) : digit(Number(cart_records[i].price_sum).toFixed(2));//Сумма позиции
			
			
			var style_tr = '';// Стили строки позиции корзины (используется при различных доработках)
			
			
			//////////////////////////////////////////////////////////////////////// - Доработка id 34
			if(cart_records[i].access == 0){
				style_tr = 'background:#f4f90e;';
				price = '<s>'+price+'</s>';
				price_sum = '<s>'+price_sum+'</s>';
			}//////////////////////////////////////////////////////////////////////// - END Доработка id 34
			
			
			cart_html += '<tr style="'+style_tr+'">';
			
				cart_html += '<td>';
					cart_html += '<input type="checkbox" '+checked_for_order+' onclick="check_for_order('+cart_records[i].id+');">';
				cart_html += '</td>';
				
				
				if(there_is_images === true){
				if(cart_records[i].image_src !== ''){
				cart_html += '<td>';
                    cart_html += '<img style="max-width:50px; max-height:70px;" src="'+cart_records[i].image_src+'"/>';
                cart_html += '</td>';
				}else{
				cart_html += '<td>';
					cart_html += '';
				cart_html += '</td>';
				}
				}
			
			
                cart_html += '<td style="font-weight: bold;">';
                    cart_html += cart_records[i].manufacturer;
                cart_html += '</td>';
                
				
				cart_html += '<td style="font-weight: bold;">';
                    cart_html += cart_records[i].article;
                cart_html += '</td>';
				
				
				cart_html += '<td style="white-space: normal; line-height: 1em; width: 100%; min-width: 200px;">';
                    cart_html += cart_records[i].name;
                cart_html += '</td>';
				
				
				cart_html += '<td>';
                    cart_html += cart_records[i].time_to_exe;
                cart_html += '</td>';
				
				
				cart_html += '<td>';
                    cart_html += price;
                cart_html += '</td>';
				
				
				cart_html += '<td><table style="margin:auto;"><tr>';
                    cart_html += '<td><a style="display: inline-block; background: #f5f5f5; font-weight: bold; width: 22px; height: 25px; line-height: 24px; text-decoration: none; text-align: center; border-radius: 3px 0px 0px 3px; border: 1px solid #999; border-right: 0;" onclick="minusCountNeed('+cart_records[i].id+');" href="javascript:void(0);"><span style="position: relative; top: -2px;">-</span></a></td>';
                    cart_html += '<td><input style="width: 40px; height: 25px; line-height: 24px; text-align: center; border: 1px solid #999; border-radius: unset; box-shadow: none;" type="text" value="'+cart_records[i].count_need+'" onkeyup="onKeyUpCountNeed('+cart_records[i].id+');" id="count_need_'+cart_records[i].id+'"/></td>';
                    cart_html += '<td><a style="display: inline-block; background: #f5f5f5; font-weight: bold; width: 22px; height: 25px; line-height: 24px; text-decoration: none; text-align: center; border-radius: 0px 3px 3px 0px; border: 1px solid #999; border-left: 0;" onclick="plusCountNeed('+cart_records[i].id+');" href="javascript:void(0);"><span>+</span></a></td>';
                cart_html += '</tr></table></td>';
				
				
				cart_html += '<td style="font-weight: bold;">';
                    cart_html += price_sum;
                cart_html += '</td>';
				
				
				cart_html += '<td style="text-align: right;">';
					
					cart_html += '<a style="margin-right:5px;" href="javascript:void(0);" onclick="RefreschRecord('+cart_records[i].id+');" title="<?php echo translate_str_by_id(4497); ?>"><i style="font-size: 20px; position: relative; top: -1px;" class="fa fa-refresh" aria-hidden="true"></i></a>';
					
					cart_html += '<a style="margin-right:5px;" href="javascript:void(0);" title="<?php echo translate_str_by_id(2101); ?>" onclick="show_add_bloknot('+cart_records[i].id+');"><i style="font-size: 18px; position: relative; top: -2px;" class="fa fa-car"></i></a>';
					
					cart_html += '<a href="javascript:void(0);" onclick="deleteRecord('+cart_records[i].id+');" title="<?php echo translate_str_by_id(2224); ?>"><i style="font-size: 25px;" class="fa fa-times" aria-hidden="true"></i></a>';
                
				cart_html += '</td>';
				
            cart_html += '</tr>';
        }
		
        cart_html += '</table>';
        cart_html += '</div>';
		
		
		//////////////////////////////////////////////////////////////////////// - Доработка id 34
			var flag_no_access_exist = false;
			for(var i=0; i < cart_records.length; i++)
			{
				if(cart_records[i].access == 0){
					flag_no_access_exist = true;
					break;
				}
			}
			if(flag_no_access_exist === true){
				cart_html += '<p style="background: #f4f90e; padding: 10px; margin: 10px 0px; border-radius: 5px; border: 1px solid #ddd;"><i class="fa fa-info-circle" aria-hidden="true"></i> <?php echo translate_str_by_id(4498); ?> <i class="fa fa-refresh" aria-hidden="true"></i></p>';
			}
		//////////////////////////////////////////////////////////////////////// - END Доработка id 34
		
		
		cart_html += '<p style="background: #eee; padding: 10px; margin: 10px 0px; border-radius: 5px; border: 1px solid #ddd;"><i class="fa fa-info-circle" aria-hidden="true"></i> <?php echo translate_str_by_id(4499); ?></p>';
		
		
        cart_html += '<div style="padding-top: 10px; text-align: right; font-size: 18px; font-weight: bold;"><span style="font-size: 14px; font-weight: normal;"><?php echo translate_str_by_id(3503); ?>:</span> '+sum_total+'</div>';
        cart_html += '<div style="text-align:right;margin-top:10px;">';
        cart_html += '<button type="button" class="btn btn-sm epc-wa-share-btn" onclick="epcWaShareCart();"><i class="fa fa-whatsapp"></i> Share cart on WhatsApp</button>';
        cart_html += '</div>';
        
        
        <?php
        //В зависимости от того, зарегистрирован ли пользователь - указываем ссылку для кнопки "Оформить заказ"
        if($user_id > 0)
        {
            $order_link = $multilang_params['lang_href']."/shop/checkout/how_get";//Сразу на страницу выбора способа получения
        }
        else
        {
            $order_link = $multilang_params['lang_href']."/shop/checkout/login_offer";//Предложить авторизацию
        }
        ?>
        
		
		if( sum_total_num > 0 )
		{
			<?php if (!empty($epc_trade_cart_blocked)) { ?>
			cart_html += <?php echo json_encode('<div class="alert alert-warning" style="margin-top:12px;text-align:left;">' . $epc_trade_cart_message . '</div><div style="text-align: right;"><span class="btn btn-default disabled" style="opacity:.65;cursor:not-allowed;">' . translate_str_by_id(4500) . ' (awaiting approval)</span></div>', JSON_UNESCAPED_UNICODE); ?>;
			<?php } else { ?>
			cart_html += '<div style="text-align: right;"><a class="btn btn-ar btn-primary" href="<?php echo $order_link; ?>"><?php echo translate_str_by_id(4500); ?></a></div>';
			<?php } ?>
		}
		
		
        document.getElementById("cart_area").innerHTML = cart_html;
    }
    // --------------------------------------------------------------------------------------
	var ajax_flag = false;
    // --------------------------------------------------------------------------------------
    //Функция добавления требуемого количества
    function plusCountNeed(cart_record_id)
    {
		if(ajax_flag){
			return;
		}
		ajax_flag = true;
        //Текущее количество
        var current_count_need = parseInt(document.getElementById("count_need_"+cart_record_id).value);
		
		var min_order = 1;
		for(var i=0; i < cart_records.length; i++)
        {
			if(cart_records[i]['id'] == cart_record_id){
				min_order = cart_records[i]['min_order'];
			}
		}
        //Объект для запроса
        var request_object = new Object;
        request_object.id = cart_record_id;
        request_object.count_need = parseInt(current_count_need) + parseInt(min_order);
        
        
        //Увеличиваем наличие на сервере и только после этого отображаем
        jQuery.ajax({
            type: "POST",
            async: true, //Запрос синхронный
            url: "/content/shop/order_process/ajax_change_count_need.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
				//console.log(answer);
				
                if(answer.status == true)
                {
                    cart_records[getElementIndex(answer.id)].count_need = answer.count_need;//В объект на уровне клиента
                    calculateSums();//Пересчитываем суммы по товарам и итого
                    refreshCartArea();//Отображаем товары в корзине
                }
                else
                {
                    if(answer.code == "not_enough")
                    {
                        alert("<?php echo translate_str_by_id(4311); ?>");
                    }
                    else
                    {
                        alert("<?php echo translate_str_by_id(4501); ?>");
                    }
                }
				ajax_flag = false;
            }
        });
    }
    // --------------------------------------------------------------------------------------
    //Функция вычитания требуемого количества
    function minusCountNeed(cart_record_id)
    {
		if(ajax_flag){
			return;
		}
		ajax_flag = true;
        //Текущее количество
        var current_count_need = parseInt(document.getElementById("count_need_"+cart_record_id).value);
		
		var min_order = 1;
		for(var i=0; i < cart_records.length; i++)
        {
			if(cart_records[i]['id'] == cart_record_id){
				min_order = cart_records[i]['min_order'];
			}
		}
        //Проверка допустимости
        if( parseInt(current_count_need-1) > 0 )
        {
            //Объект для запроса
            var request_object = new Object;
            request_object.id = cart_record_id;
            request_object.count_need = parseInt(current_count_need) - parseInt(min_order);
            
            //Уменьшаем наличие на сервере и только после этого отображаем
            jQuery.ajax({
                type: "POST",
                async: true, //Запрос синхронный
                url: "/content/shop/order_process/ajax_change_count_need.php",
                dataType: "json",//Тип возвращаемого значения
                data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer)
                {
                    if(answer.status == true)
                    {
                        cart_records[getElementIndex(answer.id)].count_need = answer.count_need;//В объект на уровне клиента
                        calculateSums();//Пересчитываем суммы по товарам и итого
                        refreshCartArea();//Отображаем товары в корзине
                    }
                    else
                    {
                        alert("<?php echo translate_str_by_id(4502); ?>");
                    }
					ajax_flag = false;
                }
            });
        }else{
			ajax_flag = false;
		}
    }
    // --------------------------------------------------------------------------------------
    //Функция изменения количества при ручном вводе в поле
    function onKeyUpCountNeed(cart_record_id)
    {
		if(ajax_flag){
			return;
		}
		ajax_flag = true;
		
        //Текущее количество
        var current_count_need = parseInt(document.getElementById("count_need_"+cart_record_id).value);
        
        //Если введено допустимое значение
        if(current_count_need > 0)
        {
            //Объект для запроса
            var request_object = new Object;
            request_object.id = cart_record_id;
            request_object.count_need = current_count_need;
            
            //Уменьшаем наличие на сервере и только после этого отображаем
            jQuery.ajax({
                type: "POST",
                async: true, //Запрос синхронный
                url: "/content/shop/order_process/ajax_change_count_need.php",
                dataType: "json",//Тип возвращаемого значения
                data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer)
                {
                    if(answer.status == true)
                    {
                        cart_records[getElementIndex(answer.id)].count_need = answer.count_need;//В объект на уровне клиента
                        calculateSums();//Пересчитываем суммы по товарам и итого
                        refreshCartArea();//Отображаем товары в корзине
                    }
                    else
                    {
                        if(answer.code == "not_enough")//Превышено наличие на складе - но мы зарезервировали максимально-доступное количество
                        {
                            cart_records[getElementIndex(answer.id)].count_need = answer.count_need;//В объект на уровне клиента
                            calculateSums();//Пересчитываем суммы по товарам и итого
                            refreshCartArea();//Отображаем товары в корзине
                            alert("<?php echo translate_str_by_id(4311); ?>");
                        }
                        else
                        {
                            cart_records[getElementIndex(answer.id)].count_need = answer.count_need;//В объект на уровне клиента
                            calculateSums();//Пересчитываем суммы по товарам и итого
                            refreshCartArea();//Отображаем товары в корзине
							alert("<?php echo translate_str_by_id(4503); ?>");
                        }
                    }
					ajax_flag = false;
                }
            });
        }
        else//Просто исправляем обратно
        {
            alert("<?php echo translate_str_by_id(4313); ?>");
            document.getElementById("count_need_"+cart_record_id).value = cart_records[getElementIndex(cart_record_id)].count_need;
			ajax_flag = false;
        }
    }
    // --------------------------------------------------------------------------------------
    //Удаление из Корзины
    function deleteRecord(cart_record_id)
    {
        //Объект для запроса
        var request_object = new Object;
		request_object.records_to_del = new Array();
		request_object.records_to_del.push(cart_record_id)
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/content/shop/order_process/ajax_delete_cart_record.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                console.log(answer);
                if(answer.status == true)
                {
                    //Удаляем элемет из массива
                    cart_records.splice(getElementIndex(answer.records_to_del[0]), 1);
                    calculateSums();//Пересчитываем суммы по товарам и итого
                    refreshCartArea();//Отображаем товары в корзине
                }
                else
                {
                    alert("<?php echo translate_str_by_id(2610); ?>");
                }
            }
        });
    }
    // --------------------------------------------------------------------------------------
    //Получить индекс из списка по ID записи
    function getElementIndex(cart_record_id)
    {
        //Сначала определяем индекс объекта в списке javascript
        for(var i=0; i < cart_records.length; i++)
        {
            if(cart_record_id == cart_records[i].id)
            {
                return i;
            }
        }
    }
    // --------------------------------------------------------------------------------------
    //Пересчитать суммы по продуктам
    function calculateSums()
    {
        sum_total = 0;//Обнуляем общую сумму
        
        for(var i=0; i < cart_records.length; i++)
        {
			cart_records[i].price_sum = cart_records[i].price*cart_records[i].count_need;
            
			if( cart_records[i].checked_for_order == 0 )
			{
				continue;
			}
			
            sum_total = sum_total + cart_records[i].price_sum;
        }
    }
    // --------------------------------------------------------------------------------------
    //Снять / отметить для заказа
	function check_for_order(cart_record_id)
	{
		//////////////////////////////////////////////////////////////////////// - Доработка id 34
		if(cart_records[getElementIndex(cart_record_id)].access == 0){
			calculateSums();//Пересчитываем суммы по товарам и итого
			refreshCartArea();//Отображаем товары в корзине
			return;
		}
		//////////////////////////////////////////////////////////////////////// - END Доработка id 34
		
		//Объект для запроса
        var request_object = new Object;
		request_object.records = new Array();
		request_object.records.push(cart_record_id);
		
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/content/shop/order_process/ajax_check_for_order.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                console.log(answer);
                if(answer.status == true)
                {
                    cart_records[getElementIndex(answer.records[0].cart_record_id)].checked_for_order = answer.records[0].checked_for_order;
					
					
                    calculateSums();//Пересчитываем суммы по товарам и итого
                    refreshCartArea();//Отображаем товары в корзине
                }
                else
                {
                    alert("<?php echo translate_str_by_id(4504); ?>");
                }
            }
        });
	}
	// --------------------------------------------------------------------------------------
	//Обработка переключения Выделить все / Снять все
    function on_check_uncheck_all()
    {
        var state = document.getElementById("check_uncheck_all").checked;
        
		var request_object = new Object;
		request_object.records = new Array();
		
		for(var i=0; i < cart_records.length; i++)
        {
			if( cart_records[i].checked_for_order != state )
			{
				//////////////////////////////////////////////////////////////////////// - Доработка id 34
				if(cart_records[i].access == 0){
					continue;
				}
				//////////////////////////////////////////////////////////////////////// - END Доработка id 34
				
				request_object.records.push(cart_records[i].id);
			}
        }
		
        if(request_object.records.length > 0){
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/content/shop/order_process/ajax_check_for_order.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					if(answer.status == true)
					{
						for(var i=0; i < answer.records.length; i++)
						{
							cart_records[getElementIndex(answer.records[i].cart_record_id)].checked_for_order = answer.records[i].checked_for_order;
						}
						
						calculateSums();//Пересчитываем суммы по товарам и итого
						refreshCartArea();//Отображаем товары в корзине
					}
					else
					{
						alert("<?php echo translate_str_by_id(4504); ?>");
					}
				}
			});
		}else{
			calculateSums();//Пересчитываем суммы по товарам и итого
			refreshCartArea();//Отображаем товары в корзине
		}
    }//~function on_check_uncheck_all()
	// --------------------------------------------------------------------------------------
	// Функция отделяет тысячные знаки пробелом. Используется для отображения цены
	function digit(str){
		var parts = (str + '').split('.'),
			main = parts[0],
			len = main.length,
			output = '',
			i = len - 1;
		
		while(i >= 0) {
			output = main.charAt(i) + output;
			if ((len - i) % 3 === 0 && i > 0) {
				output = ' ' + output;
			}
			--i;
		}

		if (parts.length > 1) {
			output += '.' + parts[1];
		}
		return output;
	}
	// --------------------------------------------------------------------------------------
	// Функция удаляет из корзине товар и переносит пользователя в проценку
	function RefreschRecord(id){
		var url = cart_records[getElementIndex(id)].url_refresh;
		deleteRecord(id);
		window.location = url;
	}
	// --------------------------------------------------------------------------------------
    var cart_records = new Array();
    var sum_total = 0;//Сумма заказа
    <?php

	$cart = array();// Массив объектов содержимого корзины

	//Формируем объекты содержимого корзины
    for($i=0; $i < count($cart_records); $i++)
    {
        $cart_object = array();
    
		$cart_record_query = $db_link->prepare('SELECT * FROM `shop_carts` WHERE `id` = ?;');
        $cart_record_query->execute( array($cart_records[$i]) );
        $cart_record = $cart_record_query->fetch();
        
        //Заполняем поля, которые не зависят от типа продукта:
        $cart_object["id"] = $cart_records[$i];
        $cart_object["price"] = $cart_record["price"];
        $cart_object["product_type"] = $cart_record["product_type"];
        $cart_object["count_need"] = $cart_record["count_need"];
        $cart_object["checked_for_order"] = $cart_record["checked_for_order"];
		$cart_object["access"] = 1;
		
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
		
		//Минимальная партия
		$cart_object["min_order"] = (int) $cart_record["t2_min_order"];
		if($cart_object["min_order"] <= 0){
			$cart_object["min_order"] = 1;
		}
        
		//Получаем Наименование
		$product_name = trim($cart_record["t2_name"]);
		$product_name = str_replace( array("'", '"', "\n", "\t", "\r", "\\"), "", $product_name);
		if(!empty($product_name)){
			$cart_object["name"] = $product_name;
		}
		
		$cart_object["manufacturer"] = $cart_record["t2_manufacturer"];
		$cart_object["article"] = $cart_record["t2_article"];
		
		//Поля зависящие от типа продукта
		$cart_object["image_src"] = '';
		$cart_object["product_id"] = '';
		$cart_object["url_refresh"] = '';
		
		//////////////////////////////////////////////////////////////////////// - Доработка id 34
        
        //////////////////////////////////////////////////////////////////////// - END Доработка id 34
		
        //Заполняем поля объекта корзины в зависимости от типа продукта (1 - каталожный, 2 - docpart)
        switch($cart_record["product_type"])
        {
            case 1:
                $product_id = (int) $cart_record["product_id"];
				
				$cart_object["product_id"] = $product_id;
				
				//////////////////////////////////////////////////////////////////////// - Доработка id 34
				
					$product_query = $db_link->prepare("SELECT *, CAST((SELECT `price` FROM `shop_carts_details` WHERE `cart_record_id` = ?) AS decimal(20,2)) AS 'price_not_markup' FROM `shop_storages_data` WHERE `id` = (SELECT `storage_record_id` FROM `shop_carts_details` WHERE `cart_record_id` = ?) AND `product_id` = ?;");
					$product_query->execute( array($cart_record["id"], $cart_record["id"], $product_id) );
					$record = $product_query->fetch();
					
					// Проверяем наличие складской записи
					if(empty($record) || ($record['price'] != $record["price_not_markup"])){
						$cart_object["access"] = 0;
						
						//Снимаем выделение позиции
						$product_query = $db_link->prepare("UPDATE `shop_carts` SET `checked_for_order` = 0 WHERE `id` = ?;");
						$product_query->execute( array($cart_record["id"]) );
						$cart_object["checked_for_order"] = 0;
					}
				
				//////////////////////////////////////////////////////////////////////// - END Доработка id 34
				
                //Получаем id категории и alias продукта
                $product_query = $db_link->prepare("SELECT `category_id`, `caption`, `alias` FROM `shop_catalogue_products` WHERE `id` = ?;");
				$product_query->execute( array($product_id) );
                $product_record = $product_query->fetch();
				if(!empty($product_record))
				{
				$category_id = (int) $product_record['category_id'];
				$product_alias = trim($product_record['alias']);
				
                //Получаем изображение
				$image_query = $db_link->prepare("SELECT `id`, `file_name` FROM `shop_products_images` WHERE `product_id` = ? ORDER BY `id` ASC LIMIT 1;");
				$image_query->execute( array($product_id) );
                $image_record = $image_query->fetch();
				if(!empty($image_record))
				{
				$image_record["file_name"] = trim($image_record["file_name"]);
                if( !empty($image_record["file_name"]) )
                {
                    if (function_exists('epc_product_image_url')) {
						$cart_object["image_src"] = epc_product_image_url((string) $image_record["file_name"]);
					} elseif (strpos($image_record["file_name"], "/") !== false || strpos($image_record["file_name"], 'auto_price/') === 0) {
						$cart_object["image_src"] = strpos($image_record["file_name"], '/') === 0
							? $image_record["file_name"]
							: '/content/files/images/' . $image_record["file_name"];
					} elseif (file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/products_images/".$image_record["file_name"])) {
						$cart_object["image_src"] = "/content/files/images/products_images/".$image_record["file_name"];
					}
                }
				}
            
				//Получаем ссылку на продукт для переоценки
				$product_category_query = $db_link->prepare("SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ?");
				$product_category_query->execute( array($category_id) );
                $product_category_record = $product_category_query->fetch();
				if(!empty($product_category_record))
				{
				$product_category_url = trim($product_category_record['url']);
				
				if($DP_Config->product_url == 'id'){
					$cart_object["url_refresh"] = '/'.$product_category_url.'/'.$product_id;
				}else{
					$cart_object["url_refresh"] = '/'.$product_category_url.'/'.$product_alias;
				}
				}
				}
				
                break;
            case 2:
				
                //Получаем изображение
				$product_image = NULL;
				if( isset($cart_record["image"]) )
				{
					$product_image = trim($cart_record["image"]);
				}
				if( !empty($product_image) )
                {
                    if( strpos($product_image, "/") )
					{
						$cart_object["image_src"] = $product_image;
					}else{
						if(file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/products_images/".$product_image)){
							$cart_object["image_src"] = "/content/files/images/products_images/".$product_image;
						}
					}
                }
				
				//Получаем ссылку на продукт для переоценки
				$cart_object["url_refresh"] = "/parts/".htmlentities($cart_object["manufacturer"])."/".$cart_object["article"];
				
                break;
        }
		
		$cart[] = $cart_object;
		
    }//for($i) - формируем объект корзины
    ?>
	cart_records = <?php echo json_encode($cart, JSON_HEX_TAG | JSON_HEX_AMP | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
    calculateSums();//Пересчитываем суммы по товарам и итого
    refreshCartArea();//Отображаем товары в корзине
    </script>
    <?php
}//~else - в корзине есть записи
?>














<!---------------------------------------------- ГАРАЖ ---------------------------------------------->
<style>
#cart_area {
	margin-bottom: 20px;
}
#my_modal_box_for_garage .modal {
	z-index:99999999;
	padding-right: 0px !important;
}
#my_modal_box_for_garage .modal-header {
  text-align: center;
  font-size: 14px;
  background: #fff;
  color:#000;
  border-bottom: 1px solid #999;
}
#my_modal_box_for_garage .close{
	color:#000;
}
#my_modal_box_for_garage .modal-footer {
	border-top: 1px solid #999;
	text-align: center;
}
#my_modal_box_for_garage .modal-dialog {
    max-width: 601px;
    margin: 30px auto;
    width: 100%;
}
</style>
<div id="my_modal_box_for_garage">
  <div class="modal fade" id="modal_garage" role="dialog">
    <div class="modal-dialog">
      <div class="modal-content">
        <div class="modal-header" style="padding:10px 15px;">
          <button type="button" class="close" data-dismiss="modal">&times;</button>
          <b><?php echo translate_str_by_id(4320); ?></b>
        </div>
        <div class="modal-body" style="color:#000; padding:40px 50px;">
			<div id="add_bloknot_content">
				<?php
				$query = $db_link->prepare('SELECT *, (SELECT `caption` FROM `shop_docpart_cars` WHERE `id` = `shop_docpart_garage`.`mark_id`) AS `mark` FROM `shop_docpart_garage` WHERE `user_id` = ?;');
				$query->execute( array($user_id) );
				echo '<select id="garage_auto" class="form-control">';
				echo '<option value="0">'.translate_str_by_id(2100).'</option>';
				while($car = $query->fetch())
				{
					echo '<option value="'.$car['id'].'">'. $car["mark"]." ".$car["model"]." ".$car["year"]." ".translate_str_by_key('4321')." - ". $car["caption"] .'</option>';
				}
				echo '</select>';
				?>
			</div>
			<div id="add_bloknot_msg"></div>
        </div>
        <div id="add_bloknot_btn" class="modal-footer">
			<a style="margin-bottom: 5px;" class="btn btn-ar btn-primary" onclick="add_bloknot();"><?php echo translate_str_by_id(2101); ?></a>
        </div>
      </div>
    </div>
  </div>
</div>
<script>
var id_in_bloknot = -1;// id позиции корзины которую будем добавлять в блокнот
// Функция отображения блока добавления позиции в блокнот
function show_add_bloknot(id){
	<?php
	if(empty($user_id)){
	?>
	alert('<?php echo translate_str_by_id(4505); ?>');
	return;
	<?php
	}
	?>
	id_in_bloknot = id;
	$("#modal_garage").modal();
}
// Функция добавления позиции в блокнот гаража
function add_bloknot(){
	if(id_in_bloknot >= 0){
		var n = document.getElementById("garage_auto").options.selectedIndex;
		var garage_id = document.getElementById("garage_auto").options[n].value;
		
		var Products = cart_records[getElementIndex(id_in_bloknot)];
		
		var request_object = new Object;
		request_object.manufacturer = encodeURIComponent(Products.manufacturer);
		request_object.article = encodeURIComponent(Products.article);
		request_object.name = encodeURIComponent(Products.name);
		request_object.exist = encodeURIComponent(Products.count_need);
		request_object.price = encodeURIComponent(Products.price);
		
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/content/shop/docpart/garage/ajax_add_to_notepad.php",
			dataType: "json",
			data: "garage="+garage_id+"&product="+JSON.stringify(request_object)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				var icon = '<i style="font-size: 30px; color: green;" class="fa fa-check"></i> ';
				if(answer.status != true){
					icon = '<i style="font-size: 30px; color: red;" class="fa fa-times"></i> ';
				}
				
				document.getElementById('add_bloknot_content').style.display = "none";
				document.getElementById('add_bloknot_btn').style.display = "none";
				document.getElementById('add_bloknot_msg').innerHTML = '<table><tr><td style="padding-right:5px;">'+icon+'</td><td>'+answer.message+'</td></tr></table>';
				
				setTimeout(function(){
					$("#modal_garage").modal('hide');
					
				}, 2000);
				
				setTimeout(function(){
					document.getElementById('add_bloknot_content').style.display = "block";
					document.getElementById('add_bloknot_btn').style.display = "block";
					document.getElementById('add_bloknot_msg').innerHTML = '';
				}, 2300);
			},
			error: function (e, ajaxOptions, thrownError){
				alert('<?php echo translate_str_by_id(2122); ?>');
			}
		});
	}
}
</script>
<!-------------------------------------------- End ГАРАЖ -------------------------------------------->