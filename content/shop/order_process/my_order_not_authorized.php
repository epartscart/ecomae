<?php
/**
 * Страничный скрипт для заказов неавторизованных покупателей.
 * 
 * Сюда переадресуется НЕ авторизованный покупатель после успешного создания заказа.
 * Здесь же покупатель может проверять статус заказа через некоторое время
 * 
 * На этой странице должна быть информация для покупателя, необходимая для осуществления заказа
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();//ID активного пользователя. Может быть больше 0, если зарегистрированный пользователь ранее оформлял заказ без регистрации, а теперь хочет посмотреть его статус.

//Общая информация по заказам
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();

require_once($_SERVER["DOCUMENT_ROOT"]."/content/general/actions_alert.php");//Вывод сообщений о результатах выполнения действий

//Способ оплаты - на странице созданного заказа
if(!empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	$error_message = translate_str_by_id(2122).": <br/> ".translate_str_by_id(2304).".";
	$success_message = "";
	
	if($_POST["action"] === 'pay_on_place')
	{
		$order_id = (int) $_POST['order_id'];
		
		// Получаем список способов оплаты:
		$shop_orders_paid_type = array();
		$query = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;');
		$query->execute();
		while($rov = $query->fetch()){
			$shop_orders_paid_type[$rov['id']] = translate_str_by_id($rov['name']);
		}
		
		if($user_id == 0)
		{
			$order_query = $db_link->prepare('SELECT * FROM `shop_orders` WHERE `id` = ? AND `user_id` = ? AND `paid_type` = 0;');
			$order_query->execute( array($order_id, $user_id) );
			$order = $order_query->fetch();
			if( $offices_list[$order["office_id"]] !== NULL )
			{
				if( $db_link->prepare("UPDATE `shop_orders` SET `paid_type` = 1 WHERE `id` = ?;")->execute( array($order_id) ) != true)
				{
					$error_message = translate_str_by_id(2122).": <br/> ".translate_str_by_id(3629).".";
				}
				else
				{
					$success_message = translate_str_by_id(3630);
					$error_message = "";
					
					//Пишем лог заказа
					$db_link->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);')->execute( array($order_id, time(), $user_id, 0, translate_str_by_id(4645).': <b>'.$shop_orders_paid_type[1].'</b>', 0) );
					
					// Меняем статус заказа (если требуется)
					$for_paid_status_query = $db_link->prepare('SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1;');
					$for_paid_status_query->execute();
					$for_paid_status_record = $for_paid_status_query->fetch();
					if( $for_paid_status_record != false )
					{
						$for_paid_status = $for_paid_status_record["id"];
						
						//ВЫЗЫВАЕМ СКРИПТ ДЛЯ ИЗМЕНЕНИЯ СТАТУСА ЗАКАЗА
						//Если требуется авторизация
						$username = $DP_Config->http_login;
						$password = $DP_Config->http_password;
						$context = stream_context_create(array(
							'http' => array(
								'header'  => "Authorization: Basic " . base64_encode("$username:$password")
							)
						));
						$set_order_status_result = file_get_contents($DP_Config->domain_path."content/shop/protocol/set_order_status.php?initiator=4&orders=[$order_id]&status=$for_paid_status&key=".urlencode($DP_Config->tech_key), false, $context);
						$set_order_status_result = json_decode($set_order_status_result, true);
						if($set_order_status_result['status'] == false )
						{
							$error_message = translate_str_by_id(2385)." <br/> ".translate_str_by_id(4662).".";
						}
					}
				}
			}
		}
	}
	$location_url = $multilang_params['lang_href'].'/shop/orders/zakaz-bez-registracii?order_id='.$order_id;
	?>
	<script>
		location="<?=$location_url?>&error_message=<?=$error_message;?>&success_message=<?=$success_message;?>";
	</script>
	<?php
	exit;
}

?>
<?php

//1. Если установлена куки с оформленным заказом - отображаем информацию по заказу. Это значит, что переход на страницу был после оформления заказа без регистрации
if( isset($_GET['order_id']) )
{
    $order_id = $_GET['order_id'];
    
	
	
	
	//Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
	$WHERE_statuses_not_count = "";
	for($i=0; $i<count($orders_items_statuses_not_count); $i++)
	{
		$WHERE_statuses_not_count .= " AND `status` != ".(int)$orders_items_statuses_not_count[$i];
	}
	
	
	
	//Для подсчета суммы оплаты по заказу
	$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
	$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
	
	
	
	
	
    //Ищем заказ. user_id в запросе равен 0. Т.е. нельзя показывать информацию по заказам зарегистрированных клиентов.
    $order_query = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(20,2) ) AS `paid_sum`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(20,2) )  AS `paid_left`, CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) AS DECIMAL(20,2)) AS `price_sum` FROM `shop_orders` WHERE `id` = ? AND `user_id` = ?;");
	$order_query->execute( array($order_id, $order_id, $order_id, $order_id, $order_id, 0) );
    $order = $order_query->fetch();
    if( $order == false )
	{
		?>
		<script>
		location = '<?php echo $multilang_params['lang_href']; ?>/shop/orders/zakaz-bez-registracii?info_message=<?php echo urlencode(translate_str_by_id(4525)); ?>';
		</script>
		<?php
		exit;
	}
	
	// Получаем список способов оплаты:
	$shop_orders_paid_type = array();
	$query = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;');
	$query->execute();
	while($rov = $query->fetch()){
		$shop_orders_paid_type[$rov['id']] = translate_str_by_id($rov['name']);
	}
	
    $time = $order["time"];
    $office_id = $order["office_id"];
    $status = $order["status"];
    $paid = $order["paid"];
	$paid_type = $order["paid_type"];
	$paid_sum = $order["paid_sum"];
	$paid_left = $order["paid_left"];
	$price_sum_total = $order["price_sum"];
    $obtain_caption = $order["obtain_caption"];
    ?>
    
    <p><?php echo translate_str_by_id(4551); ?></p>
    
    <table class="table">
        <tr> <td><?php echo translate_str_by_id(3244); ?></td> <td><?php echo $order_id; ?></td> <tr>
        <tr> <td><?php echo translate_str_by_id(2242); ?></td> <td><?php echo date("d.m.Y", $time)." ".date("G:i", $time); ?></td> <tr>
        <tr> <td><?php echo translate_str_by_id(4418); ?></td> <td><?php echo translate_str_by_id($offices_list[$office_id]["caption"]); ?></td> <tr>
		<tr> <td><?php echo translate_str_by_id(4645); ?></td> <td><?=(!empty($shop_orders_paid_type[$paid_type]))?$shop_orders_paid_type[$paid_type]:'';?></td> </tr>
        <tr> <td><?php echo translate_str_by_id(3507); ?></td> <td><?php echo translate_str_by_id($obtain_caption); ?></td> <tr>
        <tr> <td><?php echo translate_str_by_id(2081); ?></td> <td><?php echo translate_str_by_id($orders_statuses[$status]["name"]); ?></td> <tr>
    </table>
    
	

	
	<div id="">
		<div class="panel panel-primary">
			<div class="panel-body">
				<div style="overflow: hidden; overflow-x: auto;">
					<p class="lead"><?php echo translate_str_by_id(4528); ?></p>
				
					<table class="table">
						<tr>
							<td><?php echo translate_str_by_id(4529); ?>:</td>
							<td><?php echo translate_str_by_id(3516); ?>:</td>
							<td><?php echo translate_str_by_id(4379); ?>:</td>
							<td><?php echo translate_str_by_id(4530); ?>:</td>
						</tr>
						<tr>
							<td>
								<strong>
									<?php 
									switch( $paid )
									{
										case 0:
											echo '<div style="color:#FFF;background-color:#e74c3c;border-radius:3px;padding:6px 12px;font-weight:normal;">'.translate_str_by_id(3513).'</div>';
											break;
										case 1:
											echo '<div style="color:#FFF;background-color:#62cb31;border-radius:3px;padding:6px 12px;font-weight:normal;">'.translate_str_by_id(3514).'</div>';
											break;
										case 2:
											echo '<div style="color:#FFF;background-color:#3498db;border-radius:3px;padding:6px 12px;font-weight:normal;">'.translate_str_by_id(3515).'</div>';
											break;
									}
									?>
								</strong>
							</td>
							<td><?php echo $price_sum_total; ?></td>
							<td><?php echo $paid_sum; ?></td>
							<td><?php echo $paid_left; ?></td>
						</tr>
					</table>
				</div>
				
				
				
				
				<?php
				//Блок добавления оплаты - выводим, если заказа еще оплачен не полностью
				if( $paid != 1 )
				{
					?>
					<div class="form-horizontal">
						<?php require $_SERVER['DOCUMENT_ROOT'] . '/content/shop/payments/epc_payment_method_picker.php'; ?>
						<?php
						//Если включена частичная оплата заказа
						if( $DP_Config->partial_payment )
						{
							//Определяем сумму, менее которой клиент не сможет оплатить при неполной оплате
							$min_pay = $price_sum_total*($DP_Config->partial_payment_min_percent/100)
							?>							
							<div class="form-group col-md-12">
							<?php
							if( $paid_left <= $min_pay )
							{
								//Долг по заказу меньше, чем минимально-допустимый платеж.
								?>
								<input type="hidden" value="<?php echo $paid_left; ?>" id="pay_value" />
								<p><?php echo translate_str_by_id(4552); ?> <?php echo $paid_left; ?></p>
								<button onclick="add_payment_to_order();" type="button" class="btn btn-ar btn-primary"><?php echo translate_str_by_id(4533); ?></button>
								<?php
							}
							else
							{
								//Долг по заказу больше, чем минимально-допустимый платеж. Клиент может сам определить желаемую сумму платежа
								?>
								<label><?php echo translate_str_by_id(4534); ?>:</label>
								<div class="header-search-box">
									<div class="input-group">
										<input style="padding-left:7px;!important;" type="number" class="form-control" placeholder="<?php echo translate_str_by_id(3524); ?>" value="<?php echo $paid_left; ?>" id="pay_value" />
										<span class="input-group-btn">
											<button onclick="add_payment_to_order();" type="button" class="btn btn-ar btn-primary"><?php echo translate_str_by_id(4533); ?></button>
										</span>
									</div>
								</div>
								<?php
							}
							?>
							</div>
							<?php
						}
						else
						{
							//Частичная предоплата выключена. Платеж возможен только в сумме, равной paid_left (не больше и не меньше)
							?>
							<input type="hidden" value="<?php echo $paid_left; ?>" id="pay_value" />
							<?php
							//Кнопка "Оплатить online" доступна всегда
							?>
							<a class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="add_payment_to_order();"><?php echo translate_str_by_id(4535); ?></a>
							<?php
						}
						?>
						
						<?php
						if($DP_Config->order_pay_on_place == 1 && $paid == false && $paid_type == 0)
						{
						?>
							<a class="btn btn-ar btn-default" href="javascript:void(0);" onclick="pay_on_place();"><?php echo $shop_orders_paid_type[1]; ?></a>
							<form id="pay_form" name="pay_form" method="POST" style="display:none;">
								<input type="hidden" name="action" id="action" value="pay_on_place" />
								<input type="hidden" name="order_id" value="<?=$order_id;?>" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
							</form>
							<script>
							function pay_on_place()
							{
								if( !confirm('<?php echo translate_str_by_id(4664); ?>') )
								{
									return;
								}
								document.forms["pay_form"].submit();
							}
							</script>
						<?php
						}
						?>
						
					</div>
					
					
					
					<script>
					//Обработка кнопки оплаты
					function add_payment_to_order()
					{
						//Сумма из поля ввода
						var pay_value = document.getElementById('pay_value').value;
						pay_value = parseFloat(pay_value).toFixed(2);
						
						//Локальные проверки:
						
						//1. Должна быть указана сумма
						if( pay_value == '' || pay_value == 'NaN' )
						{
							alert('<?php echo translate_str_by_id(3526); ?>');
							return;
						}
						//2. Сумма не должна превышать остаток долга клиента по заказу, не должна быть отрицательной, не должна быть равна 0
						if( pay_value > <?php echo $paid_left; ?> || pay_value <= 0 )
						{
							alert('<?php echo translate_str_by_id(4539); ?>');
							return;
						}
						
						
						<?php
						//Если включена частичная оплата заказа - делаем проверки
						if( $DP_Config->partial_payment )
						{
							?>
							//Если желаемый платеж меньше оставшегося долга по заказу
							if( pay_value < <?php echo $paid_left; ?> )
							{
								//Проверяем, чтобы он был не менее минимально-допустимого платежа
								if( pay_value < <?php echo $min_pay; ?> )
								{
									alert('<?php echo translate_str_by_id(4540); ?> <?php echo $DP_Config->partial_payment_min_percent; ?>% <?php echo translate_str_by_id(4541); ?>: <?php echo $min_pay; ?>');
									return;
								}
							}
							<?php
						}
						?>
						

						var request_object = new Object;
						request_object.order_id = <?php echo $order_id; ?>;
						request_object.amount = pay_value;
						if (typeof window.epcSelectedPayHandler === 'function') {
							request_object.pay_handler = window.epcSelectedPayHandler();
						}
						
						jQuery.ajax({
							type: "POST",
							async: false, //Запрос синхронный
							url: "/content/shop/finance/ajax_create_operation.php",
							dataType: "text",//Тип возвращаемого значения
							data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								console.log(answer);
				
								var answer_ob = JSON.parse(answer);
								
								if( typeof answer_ob.result == 'undefined' )
								{
									alert("<?php echo translate_str_by_id(4343); ?>");
								}
								else
								{
									if(answer_ob.result == true)
									{					
										if( answer_ob.pay_system == 0 )
										{
											alert("<?php echo translate_str_by_id(4344); ?>");
											return;
										}
										else
										{
											location = "/content/shop/finance/payment_systems/"+answer_ob.pay_system+"/go_to_pay.php?operation="+answer_ob.operation+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>";
										}
									}
									else
									{
										alert("<?php echo translate_str_by_id(4345); ?>");
									}
								}
							}
						});
					}
					</script>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	<?php
}
?>
<div class="panel panel-primary">
	<div class="panel-heading"><?php echo translate_str_by_id(4553); ?></div>
	<div class="panel-body">
		<form method="GET">
			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
			
			<div class="input-group">
				<input value="" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4554); ?>" name="order_id" />
				<span class="input-group-btn">
					<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(4555); ?></button>
				</span>
			</div>

		</form>
	</div>
</div>