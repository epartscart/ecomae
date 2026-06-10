<?php
/**
 * Страница одного заказа
*/
defined('_ASTEXE_') or die('No access');

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_background.php");

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$manager_id = DP_User::getAdminId();//ID менежера, который отображает эту страницу
?>

<?php
if(!empty($_POST["action"]))
{
	
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
    //Индикатор непрочитанных сообщений в заказах
	if($_POST["action"] == 'update_msg'){
		$flag = (int) $_POST["flag"];
		$order_id = (int) $_POST["order_id"];
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_script_relocate.php';
		if($db_link->prepare("UPDATE `shop_orders_messages` SET `read` = ? WHERE `order_id` = ? AND `is_customer` = 1 ORDER BY `id` DESC LIMIT 1;")->execute( array($flag, $order_id) ) === true){
			epc_cp_redirect('/shop/orders/orders');
		}
		epc_cp_redirect('/shop/orders/order?order_id=' . $order_id . '&error_message=' . urlencode(translate_str_by_id(5293)));
	}
	
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();

	require_once($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/actions_alert.php');//Вывод сообщений о результатах действий

	$order_id = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;
	if ($order_id <= 0) {
		echo '<div class="alert alert-warning"><strong>Order not specified.</strong> '
			. 'Open an order from <a href="/' . htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8')
			. '/shop/orders/orders">Orders</a> or add <code>?order_id=</code> to the URL.</div>';
	} else {
    
	//Ставим флаг "Просмотрен"
	$db_link->prepare("UPDATE `shop_orders_viewed` SET `viewed_flag` = 1 WHERE `order_id` = ?;")->execute( array($order_id) );
	
	//Делаем все сообщения заказа прочитанными
	$db_link->prepare("UPDATE `shop_orders_messages` SET `read` = 1 WHERE `order_id` = ? AND `is_customer` = 1;")->execute( array($order_id) );
	
	
	
	
	//Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
	if (!is_array($orders_items_statuses_not_count ?? null)) {
		$orders_items_statuses_not_count = array();
	}
	$WHERE_statuses_not_count = "";
	for($i=0; $i<count($orders_items_statuses_not_count); $i++)
	{
		$WHERE_statuses_not_count .= " AND `status` != ".(int)$orders_items_statuses_not_count[$i];
	}
	
	//Для подсчета суммы оплаты по заказу
	$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
	$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
	
	
	//Для определения текущего баланса клиента
	$sub_balance_SQL = "";
	if( isset( $DP_Config->wholesaler ) )
	{
		$sub_balance_SQL = " AND `office_id` = `shop_orders`.`office_id` ";
	}
	$INCOME_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `user_id` = `shop_orders`.`user_id` ".$sub_balance_SQL." ), 0)";
	$ISSUE_USER_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `user_id` = `shop_orders`.`user_id` ".$sub_balance_SQL." ),0)";
	
	
    //Получаем данные заказа
	$order_query = $db_link->prepare("SELECT *, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`, (SELECT `handler` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_handler`, CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) AS DECIMAL(20,2)) AS `price_sum`, CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(20,2) ) AS `paid_sum`, CAST( ($INCOME_USER_SQL - $ISSUE_USER_SQL) AS DECIMAL(20,2) ) AS `customer_balance`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) - ($ISSUE_SQL - $INCOME_SQL) ) AS DECIMAL(20,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;");
	$order_query->execute( array($order_id, $order_id, $order_id, $order_id, $order_id) );
    $order = $order_query->fetch(PDO::FETCH_ASSOC);
    if (!$order || !isset($offices_list[$order['office_id']])) {
        echo '<div class="alert alert-danger"><strong>Order not found or access denied.</strong> '
            . 'Order #' . (int) $order_id . ' may not exist or your office access does not include it.</div>';
    } else {
    
	// Получаем список способов оплаты:
	$shop_orders_paid_type = array();
	$query = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;');
	$query->execute();
	while($rov = $query->fetch()){
		$shop_orders_paid_type[$rov['id']] = $rov['name'];
	}
	
    $time = $order["time"];
    $office_id = $order["office_id"];
    $status = $order["status"];
    $paid = $order["paid"];
	$paid_type = $order["paid_type"];
    $customer_id = $order["user_id"];
    $how_get = $order["how_get"];
	$obtain_caption = $order["obtain_caption"];
	$obtain_handler = $order["obtain_handler"];
	$price_sum = $order["price_sum"];
	$paid_sum = $order["paid_sum"];
	$paid_left = $order["paid_left"];
	$customer_balance = $order["customer_balance"];
    $how_get_json = json_decode($order["how_get_json"], true);
	
	// Backend management margin is shown without VAT/tax. Prices in order items are net item prices.
	$ORDER_MARGIN_purchase_sum_sql = "IFNULL((SELECT SUM(`price_purchase`*(`count_reserved`+`count_issued`+`count_canceled`)) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), CAST(`t2_price_purchase`*`count_need` AS DECIMAL(20,2)))";
	$order_margin_query = $db_link->prepare("SELECT
		CAST(IFNULL(SUM(`price`*`count_need`), 0) AS DECIMAL(20,2)) AS `sale_sum`,
		CAST(IFNULL(SUM($ORDER_MARGIN_purchase_sum_sql), 0) AS DECIMAL(20,2)) AS `purchase_sum`,
		CAST(IFNULL(SUM(`price`*`count_need` - $ORDER_MARGIN_purchase_sum_sql), 0) AS DECIMAL(20,2)) AS `profit_sum`
		FROM `shop_orders_items`
		WHERE `order_id` = ? $WHERE_statuses_not_count;");
	$order_margin_query->execute(array($order_id));
	$order_margin_totals = $order_margin_query->fetch();
	$order_sale_sum_without_vat = (float)$order_margin_totals["sale_sum"];
	$order_purchase_sum_without_vat = (float)$order_margin_totals["purchase_sum"];
	$order_profit_without_vat = (float)$order_margin_totals["profit_sum"];
	$order_margin_percent_without_vat = ($order_sale_sum_without_vat > 0) ? round($order_profit_without_vat * 100 / $order_sale_sum_without_vat, 2) : 0;

	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_vat.php';
	$order_uae_vat = epc_uae_vat_calc_on_exclusive($order_sale_sum_without_vat, $db_link);
	$order_vat_amount = (float)$order_uae_vat['vat_amount'];
	$order_sale_incl_vat = (float)$order_uae_vat['total_incl_vat'];
	$order_vat_rate = (float)$order_uae_vat['vat_rate'];
	$paid_sum_num = (float)$paid_sum;
	$paid_left_incl_vat = max(0, round($order_sale_incl_vat - $paid_sum_num, 2));
	$paid_left_display = ($order_uae_vat['vat_applicable'] && $order_vat_rate > 0) ? $paid_left_incl_vat : (float)$paid_left;
    ?>
    
    
    <div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3505); ?>
			</div>
			<div class="panel-body">
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(1082); ?>
					</label>
					<div class="col-lg-9">
						<?php echo $order_id; ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2242); ?>
					</label>
					<div class="col-lg-9">
						<?php echo date("d.m.Y", $time)." ".date("G:i", $time); ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(5294); ?>
					</label>
					<div class="col-lg-9">
						<?php echo translate_str_by_id($offices_list[$office_id]); ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(3507); ?>
					</label>
					<div class="col-lg-9">
						<?php echo translate_str_by_id($obtain_caption); ?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(4645); ?>
					</label>
					<div class="col-lg-9">
						<?=(!empty($shop_orders_paid_type[$paid_type]))?translate_str_by_id($shop_orders_paid_type[$paid_type]):'';?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(5295); ?>
					</label>
					<div class="col-lg-9">
						<div class="input-group">
							<select id="status_selector" onchange="document.getElementById('apply_order_status_button').disabled = 0;" class="form-control">
								<?php
								foreach($orders_statuses as $key => $value)
								{
									$selected = "";
									if($status == $key)$selected = "selected=\"selected\"";
									?>
									<option value="<?php echo $key; ?>" <?php echo $selected; ?>><?php echo translate_str_by_id($orders_statuses[$key]["name"]); ?></option>
									<?php
								}
								?>
							</select>
							<span class="input-group-btn">
								<button onclick="setOrderStatus();" disabled="disabled" id="apply_order_status_button" type="button" class="btn btn-success"><?php echo translate_str_by_id(2189); ?></button>
							</span>
						</div>
						<?php
						//Статусы заказов:
	
						//Получаем список статусов заказа с флагом "for_finish" - заказ выполнен
						$orders_statuses_for_finish = array();
						$query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1;");
						$query->execute();
						while($status_row = $query->fetch() )
						{
							$orders_statuses_for_finish[] = $status_row["id"];
						}
						
						//Получаем список статусов заказа с флагом "for_inverse" - заказ отменен
						$orders_statuses_for_inverse = array();
						$query = $db_link->prepare("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1;");
						$query->execute();
						while($status_row = $query->fetch() )
						{
							$orders_statuses_for_inverse[] = $status_row["id"];
						}
						
						?>
						<script>
							var orders_statuses_for_finish = JSON.parse('<?php echo json_encode($orders_statuses_for_finish); ?>');
							var orders_statuses_for_inverse = JSON.parse('<?php echo json_encode($orders_statuses_for_inverse); ?>');
							
							//Функция - изменить статус заказа
							function setOrderStatus()
							{
								var needStatus = document.getElementById("status_selector").value;
								
								if(orders_statuses_for_finish.indexOf(needStatus) !== -1){
									<?php
									// Если заказ не оплачен - тогда нельзя установить статус заказа "Выполнен"
									if($paid != 1){
									?>
									
										alert('<?php echo translate_str_by_id(5296); ?>');
										return;
									
									<?php
									}else{
									?>
									
										if( !confirm("<?php echo translate_str_by_id(5297); ?>") )
										{
											return;
										}
									
									<?php
									}
									?>
								}
								
								if(orders_statuses_for_inverse.indexOf(needStatus) !== -1){
									<?php
									// Если заказ не оплачен
									if($paid == 0){
									?>
									
										if( !confirm("<?php echo translate_str_by_id(5298); ?>") )
										{
											return;
										}
									
									<?php
									}else{
									?>
									
										if( !confirm("<?php echo translate_str_by_id(5299); ?>") )
										{
											return;
										}
									
									<?php
									}
									?>
								}
								
								jQuery.ajax({
									type: "GET",
									async: false, //Запрос синхронный
									url: "/content/shop/protocol/set_order_status.php",
									dataType: "json",//Тип возвращаемого значения
									data: "initiator=1&orders=[<?php echo $order_id; ?>]&status="+needStatus+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
									success: function(answer)
									{
										console.log(answer);
										if(answer.status == true)
										{
											//Обновляем страницу
											location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>&success_message='+encodeURI('<?php echo translate_str_by_id(2157); ?>');
										}
										else
										{
											if(answer.message){
												alert("<?php echo translate_str_by_id(3508); ?>. "+answer.message);
											}else{
												alert("<?php echo translate_str_by_id(3508); ?>");
											}
										}
									}
								});
							}
						</script>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2113); ?>
					</label>
					<div class="col-lg-9">
						<button class="btn btn-danger" type="button" onclick="deleteOrder();"><i class="fas fa-trash"></i> <span class="bold"><?php echo translate_str_by_id(3509); ?></span></button>
						<button class="btn btn-default" type="button" onclick="setOrderNoViewed();"><i class="fas fa-eye-slash"></i> <span class="bold"><?php echo translate_str_by_id(5300); ?></span></button>
					</div>
				</div>
</div>
		</div>
		
		
		
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(4550); ?>
			</div>
			<div class="panel-body">
				<?php
				$epc_staff_summary = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/order_process/epc_order_staff_summary.php';
				if (is_readable($epc_staff_summary)) {
					include $epc_staff_summary;
				}
				$customer_profile = DP_User::getUserProfileById($customer_id);//Получаем данные покупателя
				if (!is_array($customer_profile)) {
					$customer_profile = array();
				}

				if($customer_id > 0)
				{
					?>
					<div class="form-group">
						<label for="" class="col-lg-3 control-label"><?php echo translate_str_by_id(3818); ?></label>
						<div class="col-lg-9">
						<?php
							echo $customer_id;
						?>
						
						
						<?php
						//Профиль пользователя
						if( $customer_id > 0 )
						{
						?>
						<a style="float: right; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; margin-right: 0px;" class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/users/usermanager/user?user_id=<?php echo $customer_id; ?>" target="_blank" title="<?php echo translate_str_by_id(3539); ?>">
							<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/user.png') 0 0 no-repeat; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; background-size: contain; margin-right: 0px;"></div>
						</a>
						<?php
						}
						?>
						
						
						<?php
						//Статистика пользователя
						if( $customer_id > 0 )
						{
						?>
						<a style="float: right; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; margin-right: 5px;" class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/users/usermanager/user?user_id=<?php echo $customer_id; ?>&type=statistics" target="_blank" title="<?php echo translate_str_by_id(5301); ?>">
							<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/statistics.png') 0 0 no-repeat; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; background-size: contain; margin-right: 0px;"></div>
						</a>
						<?php
						}
						?>
						
						
						<?php
						//Авторизация от имени пользователя
						if( $customer_id > 0 )
						{
						?>
							<a style="float: right; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; margin-right: 5px;" class="panel_a" href="javascript:void(0);" onclick="auth_with_user();" title="<?php echo translate_str_by_id(5302); ?>">
								<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/key.png') 0 0 no-repeat; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; background-size: contain; margin-right: 0px;"></div>
							</a>
			<?php
						}
						?>
						
						
						<?php
						//Заказы пользователя
						if( $customer_id > 0 )
						{
						?>
							<a style="float: right; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; margin-right: 5px;" class="panel_a" href="javascript:void(0);" onclick="locationOrders();" title="<?php echo translate_str_by_id(3542); ?>">
								<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/store.png') 0 0 no-repeat; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; background-size: contain; margin-right: 0px;"></div>
							</a>
			<?php
						}
						?>
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<?php
					
					//Регистрационный вариант
					$all_reg_variants_query = $db_link->prepare("SELECT COUNT(*) FROM `reg_variants`");//Для получения количества всех вариантов
					$all_reg_variants_query->execute();
					if($all_reg_variants_query->fetchColumn() > 1)
					{
						//Теперь запрос своего варианта
						$user_reg_variant_query = $db_link->prepare("SELECT * FROM `reg_variants` WHERE `id` = ?;");
						$user_reg_variant_query->execute( array($customer_profile["reg_variant"]) );
						$user_reg_variant_record = $user_reg_variant_query->fetch();

						echo "<div class=\"form-group\"><label for=\"\" class=\"col-lg-3 control-label\">".translate_str_by_id(4646)."</label><div class=\"col-lg-9\">".$user_reg_variant_record["caption"]."</div></div> <div class=\"hr-line-dashed col-lg-12\"></div>";
					}//в противном случае не выводим регистрационный вариант
					
					
					//Баланс клиента
					?>
					<div class="form-group">
						<label for="" class="col-lg-3 control-label"><?php echo translate_str_by_id(3543); ?></label>
						<div class="col-lg-9">
						<?php
						if($customer_balance > 0){
							echo number_format($customer_balance,2,'.',' ');
						}else{
							echo $customer_balance;
						}
						?>
						
						
						<?php
						//Авторизация от имени пользователя
						if( $customer_id > 0 )
						{
						?>
						<a style="float: right; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; margin-right: 0px;" class="panel_a" href="javascript:void(0);" onclick="locationBalance();" title="<?php echo translate_str_by_id(3544); ?>">
							<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/credit_card.png') 0 0 no-repeat; width: 30px; height: 30px; max-width: 30px; max-height: 30px; min-width: 30px; min-height: 30px; display: inline-block; background-size: contain; margin-right: 0px;"></div>
						</a>
		<?php
						}
						?>
						
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<?php
					
					
					
					if( $customer_id > 0 )
					{
						$orders = DP_User::getUserOrdersById($customer_id);// Суммарная информация по заказам клиента
						?>
						<div class="form-group">
							<label for="" class="col-lg-3 control-label"><?php echo translate_str_by_id(3583); ?></label>
							<div class="col-lg-9">
                                <?php echo translate_str_by_id(3593); ?>: <?php echo number_format($orders['count'],0,'.',' '); ?> <?php echo translate_str_by_id(4496); ?>: <?php echo number_format($orders['total'],2,'.',' '); ?> <?php echo translate_str_by_id(3518); ?>: <?php echo number_format($orders['total_debt'],2,'.',' '); ?>
							</div>
						</div>
						
						<div class="hr-line-dashed col-lg-12"></div>
						<?php
					}
					
					
					
					//Основные контакты
					?>
					<div class="form-group">
						<label for="" class="col-lg-3 control-label"><?php echo translate_str_by_id(1312); ?></label>
						<div class="col-lg-9">
						<?php
						if( !empty( $customer_profile['phone'] ) )
						{
							echo $customer_profile['phone'];
							
							if( $customer_profile['phone_confirmed'] == 1 )
							{
								?>
								<i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="<?php echo translate_str_by_id(3546); ?>"></i>
								<?php
							}
							else
							{
								?>
								<i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="<?php echo translate_str_by_id(3545); ?>"></i>
								<?php
							}
						}
						else
						{
							?>
							<?php echo translate_str_by_id(3253); ?>
							<?php
						}
						?>
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">E-mail</label>
						<div class="col-lg-9">
						<?php
						if( !empty( $customer_profile['email'] ) )
						{
							echo $customer_profile['email'];
							
							if( $customer_profile['email_confirmed'] == 1 )
							{
								?>
								<i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="<?php echo translate_str_by_id(3546); ?>"></i>
								<?php
							}
							else
							{
								?>
								<i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="<?php echo translate_str_by_id(3545); ?>"></i>
								<?php
							}
						}
						else
						{
							?>
							<?php echo translate_str_by_id(3253); ?>
							<?php
						}
						?>
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					<?php
					
					
				}
				
				$need_hr = false;
				
				
				//Перед выводом профиля получаем имена колонок таблицы users, чтобы отфильтровать их при выводе профиля
				$users_table_columns_query = $db_link->prepare("SELECT `COLUMN_NAME` FROM `INFORMATION_SCHEMA`.`COLUMNS` WHERE TABLE_NAME = 'users' AND `TABLE_SCHEMA` = '".$DP_Config->db."';");
				$users_table_columns_query->execute();
				$users_table_columns = array();
				while( $col_record =  $users_table_columns_query->fetch() )
				{
					$users_table_columns[] = $col_record['COLUMN_NAME'];
				}
				
			   
				foreach($customer_profile as $key => $value)
				{
					//Фильтруем все, что не относится к users_profiles и что не нужно показывать пользователю
					if( array_search($key, $users_table_columns ) !== false )
					{
						continue;
					}
					
					//Получаем название поля
					$parameter = "";
					if($key == "user_id")
					{
						$parameter = translate_str_by_id(3007);
					}
					else if($key == "groups")
					{
						$parameter = translate_str_by_id(3547);
						$groups_names = "";
						$groupIds = is_array($value) ? $value : array();
						//Получаем названия групп
						for($i=0; $i < count($groupIds); $i++)
						{
							$group_query = $db_link->prepare('SELECT * FROM `groups` WHERE `id` = ?;');
							$group_query->execute( array($groupIds[$i]) );
							$group_record = $group_query->fetch();
							if($groups_names != "")
							{
								$groups_names .= ";<br>";
							}
							$groups_names .= translate_str_by_id($group_record["value"]);
						}
						$value = $groups_names;//Для вывода
					}
					else
					{
						//Название из таблицы регистрационны полей
						$field_caption_query = $db_link->prepare('SELECT * FROM `reg_fields` WHERE `name`=?;');
						$field_caption_query->execute( array($key) );
						$field_caption_record = $field_caption_query->fetch();
						$parameter = translate_str_by_id($field_caption_record["caption"]);
					}
					
					if($need_hr)
					{
						echo "<div class=\"hr-line-dashed col-lg-12\"></div>";
					}
					else
					{
						$need_hr = true;
					}
					?>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo $parameter; ?>
						</label>
						<div class="col-lg-9">
							<?php echo $value; ?>
						</div>
					</div>
					<?php
				}//foreach($customer_profile AS $key => $value)
				?>
				
				<?php
				//Если покупатель не авторизован - показываем его контакты
				if( $customer_id == 0)
				{
					?>
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo translate_str_by_id(3548); ?>
						</label>
						<div class="col-lg-9">
							<?php echo translate_str_by_id(3549); ?> (ID 0)
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							<?php echo translate_str_by_id(1312); ?>
						</label>
						<div class="col-lg-9">
							<?php echo $order["phone_not_auth"]; ?>
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-3 control-label">
							E-mail
						</label>
						<div class="col-lg-9">
							<?php echo $order["email_not_auth"]; ?>
						</div>
					</div>
					<?php
				}
				
				$epc_wa_order_partial = $_SERVER['DOCUMENT_ROOT'] . '/cp/content/shop/order_process/epc_order_whatsapp_share.php';
				if (is_readable($epc_wa_order_partial)) {
					if (!isset($customer_profile) || !is_array($customer_profile)) {
						$customer_profile = array();
					}
					include $epc_wa_order_partial;
				}
				?>
			</div>
		</div>
	</div>
	
	
    
    
	
	
	
	
	<?php
	/*
	Выводим блок оплаты, если заказ "Не оплачен" или "Частично оплачен"
	Выводим блок возврата, если заказ "Оплачен" или "Частично оплачен"
	*/
	?>
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3511); ?>
			</div>
			<div class="panel-body">
				
				
				<div class="panel-footer contact-footer">
					<div class="row">
						<div class="col-md-3">
							<div class="contact-stat">
								<span><?php echo translate_str_by_id(3512); ?>: </span> 
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
							</div>
						</div>
						
						<div class="col-md-3">
							<div class="contact-stat">
								<span><?php echo translate_str_by_id(3516); ?>: </span> 
								<strong><?php echo $price_sum; ?></strong>
							</div> 
						</div>
							
						<div class="col-md-3">
							<div class="contact-stat">
								<span><?php echo translate_str_by_id(3517); ?>: </span>
								<strong><?php echo $paid_sum; ?></strong>
							</div>
						</div>
							
						<div class="col-md-3">
							<div class="contact-stat">
								<span><?php echo translate_str_by_id(3518); ?>: </span>
								<strong><?php echo number_format($paid_left_display, 2, '.', ' '); ?><?php echo ($order_uae_vat['vat_applicable'] && $order_vat_rate > 0) ? ' <small>(incl. VAT)</small>' : ''; ?></strong>
							</div>
						</div>
					</div>
					<div class="row" style="margin-top:15px;">
						<div class="col-md-3">
							<div class="contact-stat" style="border-left:4px solid #3498db;padding-left:10px;">
								<span>Sale ex VAT: </span>
								<strong><?php echo number_format($order_sale_sum_without_vat, 2, '.', ' '); ?></strong>
							</div>
						</div>
						<?php if ($order_uae_vat['vat_applicable'] && $order_vat_rate > 0): ?>
						<div class="col-md-3">
							<div class="contact-stat" style="border-left:4px solid #e74c3c;padding-left:10px;">
								<span>Output VAT <?php echo number_format($order_vat_rate, 2, '.', ''); ?>%: </span>
								<strong><?php echo number_format($order_vat_amount, 2, '.', ' '); ?></strong>
							</div>
						</div>
						<div class="col-md-3">
							<div class="contact-stat" style="border-left:4px solid #c0392b;padding-left:10px;">
								<span>Total incl. VAT: </span>
								<strong><?php echo number_format($order_sale_incl_vat, 2, '.', ' '); ?></strong>
							</div>
						</div>
						<?php endif; ?>
						<div class="col-md-3">
							<div class="contact-stat" style="border-left:4px solid #f39c12;padding-left:10px;">
								<span>Purchase cost: </span>
								<strong><?php echo number_format($order_purchase_sum_without_vat, 2, '.', ' '); ?></strong>
							</div>
						</div>
						<div class="col-md-3">
							<div class="contact-stat" style="border-left:4px solid #62cb31;padding-left:10px;">
								<span>Margin ex VAT: </span>
								<strong><?php echo number_format($order_profit_without_vat, 2, '.', ' '); ?></strong>
							</div>
						</div>
						<div class="col-md-3">
							<div class="contact-stat" style="border-left:4px solid #9b59b6;padding-left:10px;">
								<span>Margin %: </span>
								<strong><?php echo number_format($order_margin_percent_without_vat, 2, '.', ' '); ?>%</strong>
							</div>
						</div>
					</div>
				</div>
				
				
				
				<?php
				//Блок добавления оплаты - выводим, если заказа еще оплачен не полностью
				if( $paid != 1 )
				{
					?>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-horizontal">
						<div class="form-group col-md-12 text-center">
							<label><?php echo translate_str_by_id(3519); ?>:</label>
						</div>
						<?php
						//Варианты - только для зарегистрированного покупателя
						if( $customer_id > 0 )
						{
							?>
							<div class="form-group col-md-6">
								<div class="col-sm-12">
									<div class="radio">
										<label>
											<input type="radio" checked="" value="1" id="optionsRadios1" name="pay_source" /> <?php echo translate_str_by_id(3520); ?>
										</label>
									</div>
									<div class="radio">
										<label>
											<input type="radio" value="0" id="optionsRadios2" name="pay_source" /> <?php echo translate_str_by_id(3521); ?>
										</label>
									</div>
								</div>
							</div>
							<?php
						}
						else
						{
							?>
							<div class="form-group col-md-6">
								<div class="col-sm-12">
									<?php echo translate_str_by_id(3522); ?>
								</div>
							</div>
							<?php
						}
						?>
						<div class="form-group col-md-6">
							<label><?php echo translate_str_by_id(3523); ?>:</label>
							<div class="input-group">
								<input type="number" class="form-control" placeholder="<?php echo translate_str_by_id(3524); ?>" value="<?php echo $price_sum - $paid_sum; ?>" id="pay_value" />
								<span class="input-group-btn">
									<button onclick="add_payment_to_order();" type="button" class="btn btn-success"><?php echo translate_str_by_id(3525); ?></button>
								</span>
							</div>
						</div>
					</div>
					<script>
					//Обработка кнопки "Добавить платеж"
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
						if( pay_value > <?php echo $price_sum - $paid_sum; ?> || pay_value <= 0 )
						{
							alert('<?php echo translate_str_by_id(3527); ?>');
							return;
						}
						
						
						
						
						//Источник оплаты
						var direct_pay = 1;//Прямая оплата заказа от клиента (т.е. добавляется приход и той же расход)
						<?php
						//Если клиент зарегистрирован, то, доступна возможность списания денег с баланса клиента
						if( $customer_id > 0 )
						{
							?>
							//Берем значение из радио-кнопок
							direct_pay = $('input[name="pay_source"]:checked').val();
							
							//Если оплата с баланса клиента, нужно проверить баланс. Если на балансе недостаточно средств, нужно предупредить продавца об этом и далее уже - на его усмотрение.
							if( direct_pay == 0 )
							{
								if( pay_value > <?php echo $customer_balance; ?> )
								{
									if( !confirm("<?php echo translate_str_by_id(3528); ?>") )
									{
										return;
									}
								}
							}
							<?php
						}
						?>
						//Если прямая оплата от клиента, то, другие проверки не делаем.
						
						jQuery.ajax({
							type: "GET",
							async: false, //Запрос синхронный
							url: "/content/shop/protocol/pay_for_order.php",
							dataType: "text",//Тип возвращаемого значения
							data: "initiator=1&order_id=<?php echo $order_id; ?>&direct_pay="+direct_pay+"&pay_sum="+pay_value+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								console.log(answer);
								var answer_ob = JSON.parse(answer);
				
								//Если некорректный парсинг ответа
								if( typeof answer_ob.status === "undefined" )
								{
									alert("<?php echo translate_str_by_id(2429); ?>");
								}
								else
								{
									//Корректный парсинг ответа
									if(answer_ob.status == true)
									{
										//Обновляем страницу
										location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>&success_message='+encodeURI('<?php echo translate_str_by_id(3529); ?>');
									}
									else
									{
										alert(answer_ob.message);
									}
								}
							}
						});
						
					}
					</script>
					<?php
				}
				?>
				
				
				
				
				
				<?php
				//Блок возврата оплаты выводим, только если по заказу есть оплата или предоплата
				if( $paid != 0 )
				{
					?>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-horizontal">
						<div class="form-group col-md-12 text-center">
							<label><button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3530); ?>');"><i class="fa fa-info"></i></button> <?php echo translate_str_by_id(3531); ?>:</label>
						</div>
						<?php
						if( $customer_id > 0 )
						{
							?>
							<button onclick="refund(0);" class="btn btn-primary " type="button"><i class="fas fa-money-check-alt"></i> <span class="bold"><?php echo translate_str_by_id(3532); ?></span></button>
							
							<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3503); ?>');"><i class="fa fa-info"></i></button>
							<?php
						}
						else
						{
							?>
							<?php echo translate_str_by_id(3534); ?><br>
							<?php
						}
						?>
						<button onclick="refund(1);" class="btn btn-primary " type="button"><i class="fas fa-hand-holding-usd"></i> <span class="bold"><?php echo translate_str_by_id(3491); ?></span></button> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3536); ?>');"><i class="fa fa-info"></i></button>
					</div>
					<script>
					//Обработка кнопок возврата
					function refund(direct_refund)
					{
						if( !confirm('<?php echo translate_str_by_id(3537); ?>') )
						{
							return;
						}
						
						
						jQuery.ajax({
							type: "POST",
							async: false, //Запрос синхронный
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/order_process/ajax_order_pay_refund.php",
							dataType: "text",//Тип возвращаемого значения
							data: "direct_refund="+direct_refund+"&order_id=<?php echo $order_id; ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								console.log(answer);
								var answer_ob = JSON.parse(answer);
				
								//Если некорректный парсинг ответа
								if( typeof answer_ob.status === "undefined" )
								{
									alert("<?php echo translate_str_by_id(2429); ?>");
								}
								else
								{
									//Корректный парсинг ответа
									if(answer_ob.status == true)
									{
										//Обновляем страницу
										location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>&success_message='+encodeURI('<?php echo translate_str_by_id(3538); ?>');
									}
									else
									{
										alert(answer_ob.message);
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
		
		
		
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(5304); ?>
			</div>
			<div class="panel-body">
				<?php
				$obtain_interface = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/obtaining_modes/' . $obtain_handler . '/manager_interface.php';
				if (is_file($obtain_interface)) {
					require_once $obtain_interface;
				} else {
					echo '<div class="alert alert-warning">Delivery mode panel unavailable for handler '
						. htmlspecialchars((string) $obtain_handler, ENT_QUOTES, 'UTF-8') . '.</div>';
				}
				?>
			</div>
		</div>
		
		
		<?php
		if($customer_id > 0){
			$user_query = $db_link->prepare('SELECT `comment` FROM `users` WHERE `user_id` = ?');
			$user_query->execute( array($customer_id) );
			$comment_row = $user_query->fetch();
			$comment = $comment_row['comment'];
		?>
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3571); ?>
				<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(5305); ?>');"><i class="fa fa-info"></i></button>
				<span id="user_comment_loader"></span>
			</div>
			<div class="panel-body">
				<textarea onchange="setComment();" id="user_comment" style="width:100%; font-size: 14px; font-weight: bold;" rows="6"><?=$comment;?></textarea>
				<script>
					function setComment()
					{
						var comment = document.getElementById('user_comment').value;
						document.getElementById('user_comment_loader').innerHTML = '<img style="height: 18px; margin: 0px 10px; position: absolute;" src="/content/files/images/ajax-loader-transparent.gif" />';
						
						jQuery.ajax({
							type: "POST",
							async: true, //Запрос синхронный
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/users/ajax_set_user_comment.php",
							dataType: "json",//Тип возвращаемого значения
							data: "user_id=<?=$customer_id;?>&comment="+encodeURIComponent(comment)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								//console.log(answer);
								if(answer.status == true)
								{
									//alert('OK');
								}
								else
								{
									alert("<?php echo translate_str_by_id(2576); ?>");
								}
								
								document.getElementById('user_comment_loader').innerHTML = '';
							}
						});
					}
				</script>
			</div>
		</div>
		<?php
		}
		?>
	</div>
	
	
	
	
	
	
	
	
	
	
	
    
    
    
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3498); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<?php
					// ---------- Start SAO ----------
					//Предварительно получаем список возможных SAO-действий:
					$sao_actions = array();
					
					$sao_actions_query = $db_link->prepare("SELECT * FROM `shop_sao_actions`");
					$sao_actions_query->execute();
					while( $sao_action = $sao_actions_query->fetch() )
					{
						$sao_actions[$sao_action["id"]] = array();
						$sao_actions[$sao_action["id"]]["name"] = translate_str_by_id($sao_action["name"]);
						$sao_actions[$sao_action["id"]]["script"] = $sao_action["script"];
						$sao_actions[$sao_action["id"]]["fontawesome"] = $sao_action["fontawesome"];
						$sao_actions[$sao_action["id"]]["btn_class"] = $sao_action["btn_class"];
					}
					
					//Подключаем протокол выполнения действий
					$sao_propocol_mode = 1;//Режим работы протокола - страница заказа
					require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/sao/actions_exec_propocol.php");
					// ---------- End SAO ----------
					
					$items_counter = 0;//Счетчик позиций
					
					//ПОЛЯ ИТОГО ПО ЗАКАЗУ
					$count_need_total = 0;//Итого количество
					$price_sum_total = 0;//Итого сумма
					$price_purchase_sum_total = 0;//Итого закуп
					$profit_total = 0;//Итого маржа
					
					//ПОЛУЧАЕМ ВСЕ ПОЗИЦИИ ЗАКАЗА
					
					//Запрос закупа
					$SELECT_price_purchase_sum = "IFNULL((SELECT SUM(`price_purchase`*(`count_reserved`+`count_issued`+`count_canceled`)) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`), CAST(`t2_price_purchase`*`count_need` AS DECIMAL(20,2)))";

					//Сумма позиции
					$SELECT_item_price_sum = "CAST(`price`*`count_need` AS DECIMAL(20,2))";
					
					//Маржа позиции
					$SELECT_item_profit = "CAST(`price`*`count_need` - $SELECT_price_purchase_sum AS DECIMAL(20,2))";
					
					
					//SAO
					$SELECT_item_sao_state = "IFNULL( (SELECT `name` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state` ), '')";
					$SELECT_item_sao_color_background = "IFNULL( (SELECT `color_background` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state` ), '')";
					$SELECT_item_sao_color_text = "IFNULL( (SELECT `color_text` FROM `shop_sao_states` WHERE `id` = `shop_orders_items`.`sao_state` ), '')";
					//Получаем через запятую возможные дейстия для SAO для данного состояния и данного поставщика
					$SELECT_item_sao_actions = " IFNULL(( SELECT GROUP_CONCAT(`id` SEPARATOR ',') FROM `shop_sao_actions` WHERE id IN (SELECT `action_id` FROM `shop_sao_states_types_actions_link` WHERE `state_type_id` = (SELECT `id` FROM `shop_sao_states_types_link` WHERE `state_id` = `shop_orders_items`.`sao_state` AND `interface_type_id` =  (SELECT `interface_type` FROM `shop_storages` WHERE `id` = `shop_orders_items`.`t2_storage_id` ) )) ), '')";

					
					
					//СЛОЖНЫЙ ВЛОЖЕННЫЙ ЗАПРОС
					$SELECT_ORDER_ITEMS = "SELECT SQL_CALC_FOUND_ROWS *, $SELECT_price_purchase_sum AS `price_purchase_sum`, $SELECT_item_price_sum AS `price_sum`, $SELECT_item_profit AS `profit`, $SELECT_item_sao_state AS `sao_state_name`, $SELECT_item_sao_color_background AS `sao_state_color_background`, $SELECT_item_sao_color_text AS `sao_state_color_text`, $SELECT_item_sao_actions AS `sao_actions`, (SELECT COUNT(`id`) FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `order_item_id` = `shop_orders_items`.`id`) AS `checks_count` FROM `shop_orders_items` WHERE `order_id` = ? ";
					
					/*$sql_log_file = fopen("sql_log_file.txt", "w");
					fwrite($sql_log_file, $SELECT_ORDER_ITEMS);
					fclose($sql_log_file);*/
					

					$order_items_query = $db_link->prepare($SELECT_ORDER_ITEMS);
					$order_items_query->execute( array($order_id) );
					
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					$oc_boot = array(
						'elements_array' => array(),
						'elements_id_array' => array(),
						'orders_items_ids_to_orders_items_objects' => array(),
					);
					$epc_apai_badge_ready = false;
					$apaiBadgeFile = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_fulfillment.php';
					if (is_file($apaiBadgeFile)) {
						require_once $apaiBadgeFile;
						$epc_apai_badge_ready = function_exists('epc_apai_order_fulfillment_badge_html');
					}
					
					?>
					<table id="order_items_table" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $elements_count_rows; ?>">
						<thead>
							<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
							<th data-toggle="true"></th>
							<th>ID</th>
							<th><?php echo translate_str_by_id(2070); ?></th>
							<th><?php echo translate_str_by_id(2071); ?></th>
							<th><?php echo translate_str_by_id(2102); ?></th>
							<th data-hide="phone"><?php echo translate_str_by_id(2751); ?></th>
							<th data-hide="phone"><?php echo translate_str_by_id(4526); ?></th>
							<th data-hide="phone"><?php echo translate_str_by_id(3251); ?></th>
							<th data-hide="phone,tablet"><?php echo translate_str_by_id(5306); ?></th>
							<th data-hide="phone,tablet"><?php echo translate_str_by_id(3499); ?></th>
							<th data-hide="phone,tablet">Margin %</th>
							<th data-hide="phone,tablet"><?php echo translate_str_by_id(2081); ?></th>
							<th data-hide="phone,tablet"><?php echo translate_str_by_id(3550); ?></th>
							<th data-hide="phone,tablet"><?php echo translate_str_by_id(3551); ?></th>
							<th data-hide="phone,tablet,default"><?php echo translate_str_by_id(3500); ?></th>
							<th></th>
						</thead>
						<tbody>
							<?php
							while( $order_item = $order_items_query->fetch() )
							{
								$oc_boot['elements_array'][] = 'checked_' . $order_item['id'];
								$oc_boot['elements_id_array'][] = (int) $order_item['id'];
								$oc_boot['orders_items_ids_to_orders_items_objects'][(string) $order_item['id']] = array(
									'product_name' => trim($order_item['t2_manufacturer'] . ' ' . $order_item['t2_article'] . ' ' . $order_item['t2_name']),
									'name' => (string) $order_item['t2_name'],
									'article' => (string) $order_item['t2_article'],
									'manufacturer' => (string) $order_item['t2_manufacturer'],
									'price' => (float) $order_item['price'],
									'count_need' => (int) $order_item['count_need'],
								);
								
								
								$item_id            = $order_item["id"];
								$item_status        = $order_item["status"];
								$item_count_need    = $order_item["count_need"];
								$item_price         = $order_item["price"];
								$item_price_sum     = $order_item["price_sum"];
								$item_product_type  = $order_item["product_type"];
								$item_product_id    = $order_item["product_id"];
								$item_price_purchase_sum = $order_item["price_purchase_sum"];
								$item_product_name  = $order_item["t2_name"];
								$apai_fulfillment_badge = '';
								if ($epc_apai_badge_ready) {
									try {
										$apai_fulfillment_badge = epc_apai_order_fulfillment_badge_html((string) ($order_item['t2_json_params'] ?? ''));
									} catch (Throwable $e) {
										$apai_fulfillment_badge = '';
									}
								}
								$item_article 		= $order_item["t2_article"];
								$item_manufacturer 	= $order_item["t2_manufacturer"];
								$item_profit        = $order_item["profit"];
								$item_margin_percent = ((float)$item_price_sum > 0) ? round((float)$item_profit * 100 / (float)$item_price_sum, 2) : 0;
								
								//SAO
								$item_sao_state_name = $order_item["sao_state_name"];
								$item_sao_state = $order_item["sao_state"];
								$item_sao_state_color_background = $order_item["sao_state_color_background"];
								$item_sao_state_color_text = $order_item["sao_state_color_text"];
								$item_sao_actions = $order_item["sao_actions"];
								$item_sao_message = $order_item["sao_message"];
								
								$item_t2_time_to_exe = $order_item["t2_time_to_exe"];
								$item_t2_time_to_exe_guaranteed = $order_item["t2_time_to_exe_guaranteed"];
								
								//Срок доставки для продуктов типа 2
								if($item_t2_time_to_exe < $item_t2_time_to_exe_guaranteed)
								{
									$item_t2_time_to_exe = $item_t2_time_to_exe." - ".$item_t2_time_to_exe_guaranteed;
								}
								$item_t2_time_to_exe = $item_t2_time_to_exe." ".translate_str_by_id(5315);
								
								//Считаем поля ИТОГО ПО ЗАКАЗУ (если статус позиции позволяет)
								if( array_search($item_status, $orders_items_statuses_not_count) === false)
								{
									$count_need_total += $item_count_need;
									$price_sum_total += $item_price_sum;
									$price_purchase_sum_total += $item_price_purchase_sum;
									$profit_total += $item_profit;
								}
								
								
								//Чеки
								$item_checks_count = $order_item["checks_count"];
								if( $item_checks_count == 0 )
								{
									$item_checks_count = translate_str_by_id(2457);
								}
								else
								{
									$item_checks_count = "<span onclick=\"show_order_item_checks(".$item_id.");\">".$item_checks_count." <i class=\"fas fa-search\"></i></span>";
								}
								?>
								
								<tr style="background-color:<?php echo $orders_items_statuses[$item_status]["color"]; ?>" id="order_item_record_<?php echo $item_id; ?>">
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $order_item["id"];?>');" id="checked_<?php echo $order_item["id"];?>" name="checked_<?php echo $order_item["id"];?>"/></td>
									<td></td>
									<td><?php echo $item_id; ?></td>
									<td><?php echo $item_manufacturer; ?></td>
									<td><?php echo $item_article; ?></td>
									<td id="order_item_name_<?php echo $item_id; ?>"><?php echo $item_product_name; ?><?php echo $apai_fulfillment_badge; ?></td>
									<td><?php echo number_format($item_price, 2, '.', ''); ?></td>
									<td><?php echo $item_count_need; ?></td>
									<td><?php echo number_format($item_price_sum, 2, '.', ''); ?></td>
									<td><?php echo number_format($item_price_purchase_sum, 2, '.', ''); ?></td>
									<td><?php echo number_format($item_profit, 2, '.', ''); ?></td>
									<td><?php echo number_format($item_margin_percent, 2, '.', ''); ?>%</td>
									<td id="order_item_status_<?php echo $item_id; ?>"><?php echo translate_str_by_id($orders_items_statuses[$item_status]["name"]); ?></td>
									<td><?php echo $item_t2_time_to_exe; ?></td>
									<td><?php echo $item_checks_count; ?></td>
									<td>
										<div class="row">
											<div class="col-lg-12">
												<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped table-bordered">
													<thead>
														<tr>
															<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(2750); ?></th>
															<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(3553); ?></th>
															<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(3425); ?></th>
															<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(2752); ?></th>
															<th rowspan="2" style="vertical-align:middle;"><?php echo translate_str_by_id(3502); ?></th>
															<th colspan="3" style="text-align:center;">
																SAO
															</th>
														</tr>
														<tr>
															<th style="text-align:center;"><?php echo translate_str_by_id(3512); ?></th>
															<th style="text-align:center;"><?php echo translate_str_by_id(4325); ?></th>
															<th style="text-align:center;"><?php echo translate_str_by_id(2113); ?></th>
														</tr>
													</thead>
													
													<tbody>
													<?php
													//Выводим данные по поставкам. Логика зависит от типа продукта
													if($item_product_type == 1)
													{
														$details_query = $db_link->prepare("SELECT *, (`count_reserved`+`count_issued`+`count_canceled`)*`price_purchase` AS `price_purchase_sum`, `count_reserved`+`count_issued`+`count_canceled` AS `count_reserved_issued` FROM `shop_orders_items_details` WHERE `order_item_id` = ?;");
														$details_query->execute( array($item_id) );
														while( $detail = $details_query->fetch() )
														{
															$detail_storage_id = (int) ($detail['storage_id'] ?? 0);
															$detail_storage_name = $storages_list[$detail_storage_id] ?? ('Storage #' . $detail_storage_id);
															?>
															<tr>
																<td><?php echo htmlspecialchars($detail_storage_name, ENT_QUOTES, 'UTF-8'); ?></td>
																<td><?php echo $detail["storage_record_id"]; ?></td>
																<td><?php echo number_format($detail["price_purchase"], 2, '.', ''); ?></td>
																<td><?php echo $detail["count_reserved_issued"]; ?></td>
																<td><?php echo number_format($detail["price_purchase_sum"], 2, '.', ''); ?></td>
																<td colspan="3"><?php echo translate_str_by_id(3610); ?></td>
															</tr>
															<?php
														}
													}
													else if($item_product_type == 2)
													{
														$item_storage_id = (int) ($order_item['t2_storage_id'] ?? 0);
														$item_storage_name = $storages_list[$item_storage_id] ?? ('Storage #' . $item_storage_id);
														?>
														<tr>
															<td><?php echo htmlspecialchars($item_storage_name, ENT_QUOTES, 'UTF-8'); ?></td>
															<td><?php echo "-"; ?></td>
															<td><?php echo number_format($order_item["t2_price_purchase"], 2, '.', ''); ?></td>
															<td><?php echo $order_item["count_need"]; ?></td>
															<td><?php echo number_format($order_item["t2_price_purchase"]*$order_item["count_need"], 2, '.', ''); ?></td>
															<?php
															if( $item_sao_state > 0 )
															{
																?>
																<td style="background-color:<?php echo $item_sao_state_color_background; ?>; color:<?php echo $item_sao_state_color_text; ?>;vertical-align:middle;">
																	<?php echo translate_str_by_id($item_sao_state_name); ?>
																</td>
																<td>
																	<?php
																	if($item_sao_message != "")
																	{
																		echo $item_sao_message;
																	}
																	else
																	{
																		echo "-";
																	}
																	?>
																</td>
																<td>
																	<?php
																	if($item_sao_actions != "")
																	{
																		$item_sao_actions = explode(",", $item_sao_actions);
																		for($ac=0; $ac < count($item_sao_actions); $ac++)
																		{
																			?>
																			<button onclick="exec_action(<?php echo $item_id; ?>, <?php echo $item_sao_actions[$ac]; ?>);" class="btn <?php echo $sao_actions[$item_sao_actions[$ac]]["btn_class"]; ?> " type="button"><i class="fa <?php echo $sao_actions[$item_sao_actions[$ac]]["fontawesome"]; ?>"></i> <span class="bold"><?php echo $sao_actions[$item_sao_actions[$ac]]["name"]; ?></span></button>
																			<?php
																		}
																	}
																	else
																	{
																		?>
																		<?php echo translate_str_by_id(3554); ?>
																		<?php
																	}
																	?>
																</td>
																<?php
															}
															else
															{
																?>
																<td colspan="3"><?php echo translate_str_by_id(3555); ?></td>
																<?php
															}
															?>
														</tr>
														<?php
													}
													?>
													</tbody>
													<tfoot>
														<tr>
															<td colspan="2"></td>
															<td><strong><?php echo translate_str_by_id(3503); ?></strong></td>
															<td><strong><?php echo $item_count_need; ?></strong></td>
															<td><strong><?php echo number_format($item_price_purchase_sum, 2, '.', ''); ?></strong></td>
															<td colspan="3"></td>
														</tr>
													</tfoot>
												</table>
											</div>
										</div>
									</td>
									<td class="text-right">
										<a class="btn btn-sm btn-info" onClick="btn_modal_clicked(<?php echo $order_item["id"];?>, <?php echo number_format($paid_sum, 2, '.', ''); ?>, <?php echo number_format($item_price, 2, '.', ''); ?>, <?php echo $item_count_need; ?>, <?php echo $item_status; ?>)"><i class="fa fa-cut"></i></a>
										
										<a class="btn btn-sm btn-info" <?=((int)$paid !== 0)?'onClick="alert(\''.translate_str_by_id(4647).'.\')"':'href="/'.$DP_Config->backend_dir.'/shop/orders/items/edit?id='.$item_id.'"';?> ><i class="fas fa-pencil-alt"></i></a>
									</td>
								</tr>
								<?php
								$items_counter++;
							}//while - по позициям заказа
							?>
						</tbody>
						<tfoot>
							<tr>
								<td></td>
								<td></td>
								<td></td>
								<td></td>
								<td></td>
								<td></td>
								<td><strong><?php echo translate_str_by_id(3503); ?></strong></td>
								<td><strong><?php echo $count_need_total; ?></strong></td>
								<td><strong><?php echo $price_sum_total; ?></strong></td>
								<td><strong><?php echo $price_purchase_sum_total; ?></strong></td>
								<td><strong><?php echo $profit_total; ?></strong></td>
								<td><strong><?php echo ($price_sum_total > 0) ? number_format($profit_total * 100 / $price_sum_total, 2, '.', ' ').'%' : '0.00%'; ?></strong></td>
								<td></td>
								<td></td>
								<td></td>
								<td></td>
								<td></td>
							</tr>
						</tfoot>
					</table>
					<div id="epc-oc-boot" style="display:none"><?php echo htmlspecialchars(json_encode($oc_boot, JSON_UNESCAPED_UNICODE), ENT_QUOTES, 'UTF-8'); ?></div>
				</div>
			
				
			</div>
			<div class="panel-footer">
				<div class="row float-e-margins">
					<div class="col-lg-8">
						<!--<button class="btn btn-info " type="button" onclick="create_check_for_orders_items();"><i class="fas fa-receipt"></i> <?php echo translate_str_by_id(3556); ?></button>-->
						
						<a class="btn btn-success " type="button" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/items/add?id=<?php echo $order_id; ?>"><i class="fa fa-plus"></i> <span class="bold"><?php echo translate_str_by_id(3557); ?></span></a>
						<!--
						<br/>
						
						<a class="btn btn-success" onClick="doc_print('sales_receipt');"><i class="fa fa-print"></i> <span class="bold"><?php echo translate_str_by_id(1947); ?></span></a>
						<a class="btn btn-success" onClick="doc_print('invoice_for_payment');"><i class="fa fa-print"></i> <span class="bold"><?php echo translate_str_by_id(1949); ?></span></a>
						<a class="btn btn-success" onClick="doc_print('torg_12');"><i class="fa fa-print"></i> <span class="bold"><?php echo translate_str_by_id(1951); ?></span></a>
						<a class="btn btn-success" onClick="doc_print('upd');"><i class="fa fa-print"></i> <span class="bold"><?php echo translate_str_by_id(1953); ?></span></a>
						<a class="btn btn-success" onClick="doc_print('upd_2021');"><i class="fa fa-print"></i> <span class="bold"><?php echo translate_str_by_id(1955); ?></span></a>
						-->

					</div>
					<script>
						function doc_print(doc_name){
							let order_items = getCheckedElements();//Список отмеченных позиций
							window.open('/content/shop/print_docs/service/print.php?doc_name='+doc_name+'&order_id=<?php echo $order_id; ?>&order_items='+JSON.stringify(order_items)+'&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>', '_blank');
						}
					</script>
					
					
					
					<div class="col-lg-4">
						<div><label><?php echo translate_str_by_id(5307); ?>:</label></div>
						<div class="input-group">
							<select id="setOrderItemsStatusSelect" class="form-control">
								<?php
								foreach($orders_items_statuses as $status_id=>$status_data)
								{
									?>
									<option value="<?php echo $status_id; ?>"><?php echo translate_str_by_id($status_data["name"]); ?></option>
									<?php
								}
								?>
							</select>
							<span class="input-group-btn">
								<button onclick="setOrderItemsStatus();" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(3558); ?></span></button>
							</span>
						</div>
						
					</div>
				</div>
			</div>
		</div>
	</div>
    
    
	
	<script>
        //Выставить статус для позиций заказа
        function setOrderItemsStatus()
        {
            var orders_items = getCheckedElements();//Список отмеченных заказов
            if(orders_items.length == 0)
            {
                alert("<?php echo translate_str_by_id(3559); ?>");
                return;
            }
            
            var needStatus = document.getElementById("setOrderItemsStatusSelect").value;
            
            jQuery.ajax({
                    type: "GET",
                    async: false, //Запрос синхронный
                    url: "/content/shop/protocol/set_order_item_status.php",
                    dataType: "json",//Тип возвращаемого значения
                    data: "initiator=1&orders_items="+JSON.stringify(orders_items)+"&status="+needStatus+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                    success: function(answer)
                    {
                        console.log(answer);
                        if(answer.status == true)
                        {
                            //Обновляем страницу
                            location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>&success_message='+encodeURI('<?php echo translate_str_by_id(2722); ?>');
                        }
                        else
                        {
							if( typeof answer.message != undefined )
							{
								alert(answer.message);
							}
							else
							{
								alert("<?php echo translate_str_by_id(3560); ?>");
							}
                        }
                    }
            	});
        }
    </script>

    

    <script>
    // ------------------------------------------------------------------------------------------------------
    //Скрыть / Открыть  информацию по поставкам
    function show_hide_storage_info(item_id)
    {
        var block = document.getElementById("storage_info_"+item_id);
	    if(block == undefined)
	    {
	        return;
	    }
	    
	    var a = document.getElementById("storage_info_button_"+item_id);
	    var state = block.getAttribute("state");
	    if(state == "hidden")
	    {
	        block.setAttribute("state", "shown");
	        $("#storage_info_"+item_id).show("fast");
	        a.innerHTML = "<?php echo translate_str_by_id(3561); ?>";
	    }
	    else
	    {
	        block.setAttribute("state", "hidden");
	        $("#storage_info_"+item_id).hide(150);
	        a.innerHTML = "<?php echo translate_str_by_id(3562); ?>";
	    }
    }
    // ------------------------------------------------------------------------------------------------------
    </script>
    
    
	
	
	
	
	
	
	<!-- Переписка с покупателем -->
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3563); ?>
			</div>
			<div class="panel-body">
				<div class="chat_block" id="chat_block">
				</div>
			</div>
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-12">
						<table style="width:100%;">
							<tr>
								<td>
									<form method="POST" style="display: inline-block;">
										<input type="hidden" name="action" value="update_msg"/>
										<input type="hidden" name="flag" value="0"/>
										<input type="hidden" name="order_id" value="<?=(int)$order_id;?>"/>
										<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>"/>
										<button class="form-control" type="submit" title="<?php echo translate_str_by_id(4648); ?>"><i class="fa fa-envelope" aria-hidden="true"></i></button>
									</form>
								</td>
								<td>
									<div class="input-group">
										<input type="text" id="new_message_area" class="form-control" />
										<span class="input-group-btn">
											<button onclick="sendMessage();" class="btn btn-success " type="button"><i class="fa fa-pencil"></i> <span class="bold"><?php echo translate_str_by_id(3564); ?></span></button>
										</span>
									</div>
								</td>
							</tr>
						</table>
					</div>
				</div>
			</div>
		</div>
	</div>
	<script>
	// --------------------------------------------------------------------------
	//Получить сообщения по заказу
	function getOrderMessages()
	{
		jQuery.ajax({
			type: "GET",
			async: true,
			url: "/content/shop/messager/ajax_get_order_messages.php",
			dataType: "json",//Тип возвращаемого значения
			data: "manager=1&order_id=<?php echo $order_id; ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				var html = "";
				for(var i=0; i < answer.length; i++)
				{
					var class_str = "bubble";
					var sender = "Покупатель";
					if(answer[i].is_customer == false)
					{
						class_str += "2";
						sender = "<?php echo translate_str_by_id(3565); ?>";
					}
					html += "<div class=\""+class_str+"\">"+sender+" "+answer[i].time+"<br>"+answer[i].text+"</div>";	
				}
				if(html == "") html = "<div align=\"center\"><?php echo translate_str_by_id(3566); ?></div>";
				document.getElementById("chat_block").innerHTML = html;
				
				document.getElementById("chat_block").scrollTop = document.getElementById("chat_block").scrollHeight;
			}
		});
	}
	// --------------------------------------------------------------------------
	//Отправить сообщение
	function sendMessage()
	{
		var text = document.getElementById("new_message_area").value;
		if(text == "")
		{
			alert("<?php echo translate_str_by_id(3567); ?>");
			return;
		}
		
		jQuery.ajax({
			type: "GET",
			async: true,
			url: "/content/shop/messager/ajax_send_message.php",
			dataType: "json",//Тип возвращаемого значения
			data: "manager=1&order_id=<?php echo $order_id; ?>&text="+encodeURIComponent(text)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				if(answer == true)
				{
					document.getElementById("new_message_area").value = "";
					getOrderMessages();
				}
				else
				{
					alert("<?php echo translate_str_by_id(3568); ?>");
				}
			}
		});
	}
	// --------------------------------------------------------------------------
	getOrderMessages();//Запрашиваем переписку по заказу
	
	setInterval(function(){
			getOrderMessages();
		}, 300000);
	</script>
	
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3569); ?>
			</div>
			<div class="panel-body">
				<div id="order_log" style="height:150px;border:1px solid #EEE;border-radius:7px;padding:7px;overflow-y:scroll;">
			
				<?php
				$log_records = "";
				$log_query = $db_link->prepare("SELECT * FROM `shop_orders_logs` WHERE `order_id` = ? ORDER BY `id`;");
				$log_query->execute( array($order_id) );
				while($log = $log_query->fetch() )
				{
					//Имя инициатора действия:
					if($log["is_robot"])
					{
						$initiator_name = translate_str_by_id(4054);
					}
					else if($log["is_manager"])
					{
						$initiator_profile = DP_User::getUserProfileById($log["user_id"]);
						$initiator_name = translate_str_by_id(5278)." ".$initiator_profile["surname"];
					}
					else
					{
						if($log["user_id"] == 0)
						{
							$initiator_name = translate_str_by_id(5308);
						}
						else
						{
							$initiator_profile = DP_User::getUserProfileById($log["user_id"]);
							$initiator_name = translate_str_by_id(4550)." ".$initiator_profile["surname"];
						}
					}
					$log_records .= date("Y-m-d H:i:s", $log["time"])." [$initiator_name] ".$log["text"]."<br>";
				}
				echo $log_records;
				?>
				</div>
			</div>
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-12">
						<div class="input-group">
							<input id="new_message_area_log" class="form-control" />
							<span class="input-group-btn">
								<button onclick="addCommentToLog();" type="button" class="btn btn-success">
									<i class="fa fa-pencil"></i>
									<span class="bold"><?php echo translate_str_by_id(3571); ?></span>
								</button>
							</span>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
    <script>
	document.getElementById("order_log").scrollTop = document.getElementById("order_log").scrollHeight;
	// -----------------------------------------------------------------------
	//Добавление комментария в лог
	function addCommentToLog()
	{
		var text = document.getElementById("new_message_area_log").value;
		if(text == "")
		{
			alert("<?php echo translate_str_by_id(3572); ?>");
			return;
		}
		
		jQuery.ajax({
			type: "GET",
			async: true,
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/order_process/ajax_add_comment_to_log.php",
			dataType: "json",//Тип возвращаемого значения
			data: "order_id=<?php echo $order_id; ?>&text="+text+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				if(answer.status == true)
				{
					location = "/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $order_id; ?>";
				}
				else
				{
					alert("<?php echo translate_str_by_id(5309); ?>");
					console.log(answer);
				}
			}
		});
	}
	// -----------------------------------------------------------------------
	</script>
	
	
	
    
    
    
    <?php
	//Загружаем модальное окно разбиения позиции
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_items_reload_modal.php");
    }
	}
}
?>