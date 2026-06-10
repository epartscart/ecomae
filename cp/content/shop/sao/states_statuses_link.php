<?php
/**
Страничный скрипт редактора связи SAO-состояний и статусов позиций заказов
*/
defined('_ASTEXE_') or die('No access');
?>



<?php
if(!empty($_POST["save_action"]))
{	
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------

	//Накопительная ошибка
	$update_error = false;

	//Получаем список SAO-состояний
	$sao_states_query = $db_link->prepare("SELECT * FROM `shop_sao_states`");
	$sao_states_query->execute();
	while( $sao_state = $sao_states_query->fetch() )
	{
		if( $db_link->prepare("UPDATE `shop_sao_states` SET `status_id` = ? WHERE `id` = ?;")->execute( array($_POST["select_item_status_".$sao_state["id"]], $sao_state["id"]) ) != true )
		{
			$update_error = true;
		}
	}
	
	if( $update_error )
	{
		$error_message = translate_str_by_id(3825);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/orders/sao_states_statuses_link?error_message=<?php echo $error_message; ?>";
		</script>
		<?php
		exit;
	}
	else
	{
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/orders/sao_states_statuses_link?success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
	
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	?>
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    

	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				
				<a class="panel_a" href="javascript:void(0);" onclick="document.forms['save_form'].submit();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
    
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3826); ?>
			</div>
			<div class="panel-body">
				<form name="save_form" method="POST">
					<input type="hidden" name="save_action" value="save_action" />
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th><?php echo translate_str_by_id(3827); ?></th>
								<th><?php echo translate_str_by_id(3604); ?></th>
							</tr>
						</thead>
						<tbody>
							<?php
							//Получаем статусы позиций
							$orders_items_statuses = array();
							
							$orders_items_statuses_query = $db_link->prepare("SELECT * FROM `shop_orders_items_statuses_ref` ORDER BY `order`;");
							$orders_items_statuses_query->execute();
							while( $item_status = $orders_items_statuses_query->fetch() )
							{
								$orders_items_statuses[$item_status["id"]] = $item_status["name"];
							}
							
							
							//Получаем возможные SAO-состояния
							$sao_states_query = $db_link->prepare("SELECT * FROM `shop_sao_states`");
							$sao_states_query->execute();
							while( $sao_state = $sao_states_query->fetch() )
							{
								?>
								<tr>
									<td><?php echo translate_str_by_id($sao_state["name"]); ?></td>
									<td>
										<select class="form-control" name="select_item_status_<?php echo $sao_state["id"]; ?>">
											<option value="0"><?php echo translate_str_by_id(2457); ?> (<?php echo translate_str_by_id(3828); ?>)</option>
											<?php
											foreach($orders_items_statuses AS $item_status_id => $item_status_name )
											{
												$selected = "";
												if( $sao_state["status_id"] == $item_status_id )
												{
													$selected = " selected = 'selected' ";
												}
												
												?>
												<option <?php echo $selected; ?> value="<?php echo $item_status_id; ?>"><?php echo translate_str_by_id($item_status_name); ?></option>
												<?php
											}
											?>
										</select>
									</td>
								</tr>
								<?php
							}
							?>
						</tbody>
					</table>
				</form>
			</div>
		</div>
	</div>
    

    <?php
}//~else//Действий нет - выводим страницу
?>