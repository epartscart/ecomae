<?php
/**
 * Страничный скрипт для управления финансовыми операциями пользователей
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if(isset($_POST["action"]))
{
    
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
    
	<?php
	$time_from = "";//1. Время с
	$time_to = "";//2. Время по
	$operation_code = -1;//3. Код операции
	$income = -1;//4. Напрвление операции
	$user_id = "";//5. Покупатель
	$user_id_show = "";//5. Покупатель для показа
	$order_id = "";//Привязка к заказу
	$office_id = -1;//Магазин
	
	
	//Получаем текущие значения фильтра:
	$account_operations_filter = NULL;
	if( isset($_COOKIE["account_operations_filter"]) )
	{
		$account_operations_filter = $_COOKIE["account_operations_filter"];
	}
	if($account_operations_filter != NULL)
	{
		$account_operations_filter = json_decode($account_operations_filter, true);
		$time_from = $account_operations_filter["time_from"];
		$time_to = $account_operations_filter["time_to"];
		$operation_code = $account_operations_filter["operation_code"];
		$income = $account_operations_filter["income"];
		$user_id = $account_operations_filter["user_id"];
		
		if( isset($account_operations_filter["order_id"]) )
		{
			$order_id = $account_operations_filter["order_id"];
		}
		else
		{
			$order_id = "";
		}
		
		
		if( isset($DP_Config->wholesaler) )
		{
			if( isset($account_operations_filter["office_id"]) )
			{
				$office_id = $account_operations_filter["office_id"];
			}
		}
		
		
		//Покупатель для показа
		if( $user_id != "" )
		{
			//SQL-подзапрос компонует строку с данными пользователя
			//Формируем часть SQL-запроса для покупателя (отдельно, чтобы можно было использовать и для WHERE)
			//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
			$users_profile_SQL = "";
			$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
			$users_profile_fields_query->execute();
			while( $users_profile_field = $users_profile_fields_query->fetch() )
			{
				if( $users_profile_SQL != "" )
				{
					$users_profile_SQL = $users_profile_SQL.",";
				}
				
				//Допустимы только буквы и знаки нижнего подчеркивания
				$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
				
				$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `users`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `users`.`user_id`)),'') ";
			}
			if( $users_profile_SQL != "" )
			{
				$users_profile_SQL = ",".$users_profile_SQL;
			}
			//SQL-подзапрос компонует строку с данными пользователя
			$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, 'ID 0, Незарегистрированный', CONCAT( 'ID ', `user_id`, ', E-mail: ', (IF(`email`!='', `email`, 'Не указан')), ', Телефон: ', (IF(`phone`!='', `phone`, 'Не указан')) ".$users_profile_SQL." ) )";
			$user_id_show_query = $db_link->prepare("SELECT *, $SQL_SELECT_CUSTOMER AS `customer` FROM `users` WHERE `user_id` = ?;");
			$user_id_show_query->execute( array($user_id) );
			$user_id_show_record = $user_id_show_query->fetch();
			if( $user_id == 0 )
			{
				$user_id_show = 'ID 0, '.translate_str_by_id(3233);
			}
			else if( $user_id_show_record == false )
			{
				$user_id_show = $user_id;
			}
			else
			{
				$user_id_show = $user_id_show_record['customer'];
			}
		}
		else
		{
			$user_id_show = '';
		}
	}
	?>

	<?php
	$add_operation_arg = '';
	if ((int) $user_id > 0) {
		$add_operation_arg = '?user_id='.(int) $user_id;
	}
	$epc_ao_cfg = array(
		'lang' => isset($multilang_params['lang']) ? (string) $multilang_params['lang'] : 'en',
		'pageUrl' => '/'.$DP_Config->backend_dir.'/shop/finance/account_operations',
		'autocompleteUrl' => '/'.$DP_Config->backend_dir.'/content/users/ajax_get_users_autocomplete.php',
		'csrf' => isset($user_session['csrf_guard_key']) ? (string) $user_session['csrf_guard_key'] : '',
		'wholesaler' => isset($DP_Config->wholesaler),
		'userId' => (string) $user_id,
		'userLabel' => (string) $user_id_show,
		'labels' => array(
			'selected' => translate_str_by_id(3247),
			'modalError' => translate_str_by_id(3541),
		),
	);
	?>
	<textarea id="epc-ao-config" hidden aria-hidden="true"><?php echo htmlspecialchars(json_encode($epc_ao_cfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), ENT_QUOTES, 'UTF-8'); ?></textarea>

	<div class="col-lg-12 epc-ao">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body epc-ao-toolbar">
				<?php
				print_backend_button(array('background_color' => '#63ce1c', 'fontawesome_class' => 'fas fa-plus', 'caption' => translate_str_by_id(3234), 'url' => '/'.$DP_Config->backend_dir.'/shop/finance/account_operations/create'.$add_operation_arg));
				print_backend_button(array('background_color' => '#3498db', 'fontawesome_class' => 'fas fa-align-justify', 'caption' => translate_str_by_id(3235), 'url' => '/'.$DP_Config->backend_dir.'/shop/finance/operations_editor'));
				?>
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>

	<div class="col-lg-12 epc-ao">
		<div class="hpanel epc-ao-filter">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3236); ?>
			</div>
			<div class="panel-body">
				<p class="epc-ao-hint">Set date range, customer, or operation type, then apply the filter. Dates use the calendar picker.</p>
				<div class="epc-ao-grid">
					<div class="epc-ao-field">
						<label for="time_from_show"><?php echo translate_str_by_id(3237); ?></label>
						<div class="epc-ao-date">
							<input type="text" id="time_from_show" class="form-control" placeholder="dd.mm.yyyy hh:mm" autocomplete="off" />
							<input type="hidden" id="time_from" value="<?php echo htmlspecialchars((string) $time_from, ENT_QUOTES, 'UTF-8'); ?>" />
							<span class="epc-ao-date__icon"><i class="fa fa-calendar"></i></span>
						</div>
					</div>
					<div class="epc-ao-field">
						<label for="time_to_show"><?php echo translate_str_by_id(3238); ?></label>
						<div class="epc-ao-date">
							<input type="text" id="time_to_show" class="form-control" placeholder="dd.mm.yyyy hh:mm" autocomplete="off" />
							<input type="hidden" id="time_to" value="<?php echo htmlspecialchars((string) $time_to, ENT_QUOTES, 'UTF-8'); ?>" />
							<span class="epc-ao-date__icon"><i class="fa fa-calendar"></i></span>
						</div>
					</div>
					<div class="epc-ao-field">
						<label for="income"><?php echo translate_str_by_id(3239); ?></label>
						<select id="income" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<option value="1" <?php echo ((string) $income === '1') ? 'selected' : ''; ?>><?php echo translate_str_by_id(3240); ?></option>
							<option value="0" <?php echo ((string) $income === '0') ? 'selected' : ''; ?>><?php echo translate_str_by_id(3241); ?></option>
						</select>
					</div>
					<div class="epc-ao-field">
						<label for="operation_code"><?php echo translate_str_by_id(3242); ?></label>
						<select id="operation_code" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<?php
							$accounting_codes_query = $db_link->prepare('SELECT * FROM `shop_accounting_codes` ORDER BY `id`;');
							$accounting_codes_query->execute();
							while ($accounting_code = $accounting_codes_query->fetch()) {
								$accounting_code['name'] = translate_str_by_id($accounting_code['name']);
								$selected = ((string) $operation_code === (string) $accounting_code['id']) ? 'selected="selected"' : '';
								$direction = ((int) $accounting_code['income'] === 0) ? translate_str_by_id(3241) : translate_str_by_id(3240);
								?>
								<option value="<?php echo (int) $accounting_code['id']; ?>" <?php echo $selected; ?>><?php echo htmlspecialchars(translate_str_by_id(2533).' '.$accounting_code['id'].' '.$accounting_code['name'].' ('.$direction.')', ENT_QUOTES, 'UTF-8'); ?></option>
								<?php
							}
							?>
						</select>
					</div>
					<div class="epc-ao-field">
						<label for="order_id"><?php echo translate_str_by_id(3243); ?></label>
						<input type="text" id="order_id" value="<?php echo htmlspecialchars((string) $order_id, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" placeholder="<?php echo htmlspecialchars(translate_str_by_id(3244), ENT_QUOTES, 'UTF-8'); ?>" />
					</div>
					<div class="epc-ao-field">
						<label for="user_id_search"><?php echo translate_str_by_id(3245); ?></label>
						<div class="epc-ao-customer">
							<input type="text" id="user_id_search" value="" class="form-control" placeholder="<?php echo htmlspecialchars(translate_str_by_id(3246), ENT_QUOTES, 'UTF-8'); ?>" autocomplete="off" />
							<input type="hidden" id="user_id" value="<?php echo htmlspecialchars((string) $user_id, ENT_QUOTES, 'UTF-8'); ?>" />
							<div id="user_id_show" class="epc-ao-customer-chip" role="status">
								<span id="user_id_show_text" class="epc-ao-customer-chip__text"></span>
								<button type="button" id="user_id_clear" class="epc-ao-customer-chip__clear" title="Clear" aria-label="Clear">&times;</button>
							</div>
						</div>
					</div>
					<?php if (isset($DP_Config->wholesaler)) { ?>
					<div class="epc-ao-field">
						<label for="office_id"><?php echo translate_str_by_id(3248); ?></label>
						<select id="office_id" class="form-control">
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<?php
							$offices_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `users` LIKE ?;');
							$offices_query->execute(array('%"'.DP_User::getAdminId().'"%'));
							while ($office = $offices_query->fetch()) {
								$sel = ((string) $office_id === (string) $office['id']) ? 'selected="selected"' : '';
								?>
								<option value="<?php echo (int) $office['id']; ?>" <?php echo $sel; ?>><?php echo htmlspecialchars($office['caption'].', '.$office['city'].', '.$office['address'].'. '.$office['phone'], ENT_QUOTES, 'UTF-8'); ?></option>
								<?php
							}
							?>
						</select>
					</div>
					<?php } ?>
				</div>
			</div>
			<div class="panel-footer">
				<div class="epc-ao-actions">
					<button class="btn btn-success" type="button" onclick="filterOperations();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
					<button class="btn btn-default" type="button" onclick="unsetFilterOperations();"><i class="fa fa-times"></i> <?php echo translate_str_by_id(2233); ?></button>
				</div>
			</div>
		</div>
	</div>

	<div class="col-lg-12 epc-ao">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3249); ?>
			</div>
			<div class="panel-body epc-ao-table-wrap">
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead>
							<tr>
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('id');" id="id_sorter">ID</a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('amount');" id="amount_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('order_id');" id="order_id_sorter"><?php echo translate_str_by_id(3243); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('income');" id="income_sorter"><?php echo translate_str_by_id(3239); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('user_id');" id="user_id_sorter"><?php echo translate_str_by_id(3245); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOperationsItems('operation_code');" id="operation_code_sorter"><?php echo translate_str_by_id(3252); ?></a></th>
								
								<?php
								if( isset($DP_Config->wholesaler) )
								{
									?>
									<th><a href="javascript:void(0);" onclick="sortOperationsItems('office_caption');" id="office_caption_sorter"><?php echo translate_str_by_id(3248); ?></a></th>
									<?php
								}
								?>
								
							</tr>
						</thead>
						<tbody>
						<script>
							<?php
							//Определяем текущую сортировку и обозначаем ее:
							$account_operations_sort = NULL;
							if( isset($_COOKIE["account_operations_sort"]) )
							{
								$account_operations_sort = $_COOKIE["account_operations_sort"];
							}
							$sort_field = "id";
							$sort_asc_desc = "desc";
							if($account_operations_sort != NULL)
							{
								$account_operations_sort = json_decode($account_operations_sort, true);
								$sort_field = $account_operations_sort["field"];
								$sort_asc_desc = $account_operations_sort["asc_desc"];
							}
							
							if( strtolower($sort_asc_desc) == "asc" )
							{
								$sort_asc_desc = "asc";
							}
							else
							{
								$sort_asc_desc = "desc";
							}
							
							$sort_fields_exeptable = array('id', 'time', 'user_id', 'amount', 'operation_code', 'income', 'order_id');
							if( isset( $DP_Config->wholesaler ) )
							{
								$sort_fields_exeptable[] = 'office_caption';
							}
							
							if( array_search( $sort_field, $sort_fields_exeptable, true ) === false )
							{
								$sort_field = "id";
							}
							
							?>
							document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
						</script>
						
						
						<?php
						//Настройки пагинации
						$rows_per_page = $DP_Config->list_page_limit;//Количество строк на страницу (SQL-параметр)
						$row_from = 0;//С какой строки начать (SQL-параметр)
						$current_page = 0;
						if( isset($_GET["page"]) )
						{
							$current_page = (int)$_GET["page"];
						}
						$row_from = $current_page * $rows_per_page;
						
						
						
						
						//Формируем часть SQL-запроса для покупателя (отдельно, чтобы можно было использовать и для WHERE)
						//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
						$users_profile_SQL = "";
						$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
						$users_profile_fields_query->execute();
						while( $users_profile_field = $users_profile_fields_query->fetch() )
						{
							if( $users_profile_SQL != "" )
							{
								$users_profile_SQL = $users_profile_SQL.",";
							}
							
							//Допустимы только буквы и знаки нижнего подчеркивания
							$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
							
							$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `shop_users_accounting`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `shop_users_accounting`.`user_id`)),'') ";
						}
						if( $users_profile_SQL != "" )
						{
							$users_profile_SQL = ",".$users_profile_SQL;
						}
						//SQL-подзапрос компонует строку с данными пользователя
						$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, 'ID 0, ".translate_str_by_id(3233)."', CONCAT( 'ID ', `user_id`, ', E-mail: ', (SELECT IF(`email`!='', `email`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `shop_users_accounting`.`user_id` LiMIT 1 ), ', ".translate_str_by_id(1312).": ', (SELECT IF(`phone`!='', `phone`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `shop_users_accounting`.`user_id` LiMIT 1 ) ".$users_profile_SQL." ) )";
						
						
						
						$binding_values_conditions = array();
						$WHERE_CONDITIONS = " `active` = 1 ";
						$binding_values_conditions_balance = array();
						$WHERE_CONDITIONS_BALANCE = " `active` = 1 ";//Отдельная строка для условия - для подсчета баланса. Здесь нет условий по полю income
						//Ставим ПОЛЬЗОВАТЕЛЬСКИЕ фильтры
						$account_operations_filter = NULL;
						if( isset($_COOKIE["account_operations_filter"]) )
						{
							$account_operations_filter = $_COOKIE["account_operations_filter"];
						}
						if($account_operations_filter != NULL)
						{
							$account_operations_filter = json_decode($account_operations_filter, true);
							
							//1. Время с
							if($account_operations_filter["time_from"] != "")
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `time` > ?";
								array_push($binding_values_conditions, $account_operations_filter["time_from"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `time` > ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["time_from"]);
							}

							//2. Время по
							if($account_operations_filter["time_to"] != "")
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `time` < ?";
								array_push($binding_values_conditions, $account_operations_filter["time_to"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `time` < ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["time_to"]);
							}

							//3. income
							if($account_operations_filter["income"] != "" && $account_operations_filter["income"] != -1)
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `income` = ?";
								array_push($binding_values_conditions, $account_operations_filter["income"]);
							}
							
							//4. operation_code
							if($account_operations_filter["operation_code"] != 0 && $account_operations_filter["operation_code"] != -1)
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `operation_code` = ?";
								array_push($binding_values_conditions, $account_operations_filter["operation_code"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `operation_code` = ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["operation_code"]);
							}
							
							//5. user_id
							if($account_operations_filter["user_id"] != "" )
							{
								if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
								$WHERE_CONDITIONS .= " `user_id` = ?";
								array_push($binding_values_conditions, $account_operations_filter["user_id"]);
								
								if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
								$WHERE_CONDITIONS_BALANCE .= " `user_id` = ?";
								array_push($binding_values_conditions_balance, $account_operations_filter["user_id"]);
							}
							
							
							//6 order_id
							if( isset($account_operations_filter["order_id"]) )
							{
								if($account_operations_filter["order_id"] != "" )
								{
									if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
									$WHERE_CONDITIONS .= " `order_id` = ?";
									array_push($binding_values_conditions, $account_operations_filter["order_id"]);
									
									if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
									$WHERE_CONDITIONS_BALANCE .= " `order_id` = ?";
									array_push($binding_values_conditions_balance, $account_operations_filter["order_id"]);
								}
							}
							
							
							
							//7 office_id
							if( isset( $DP_Config->wholesaler ) )
							{
								//Если менеджер указал конкретный магазин
								if($account_operations_filter["office_id"] != 0 && $account_operations_filter["office_id"] != -1)
								{
									if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
									$WHERE_CONDITIONS .= " `office_id` = ?";
									array_push($binding_values_conditions, $account_operations_filter["office_id"]);
									
									if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
									$WHERE_CONDITIONS_BALANCE .= " `office_id` = ?";
									array_push($binding_values_conditions_balance, $account_operations_filter["office_id"]);
								}
							}
							
						}
						if( isset( $DP_Config->wholesaler ) )
						{
							//Магазин в любом случае должен быть доступен данному менеджеру
							if($WHERE_CONDITIONS != "" )$WHERE_CONDITIONS .= " AND ";
							$WHERE_CONDITIONS .= " `office_id` IN (SELECT `id` FROM `shop_offices` WHERE `users` LIKE ?)";
							array_push($binding_values_conditions, '%"'.DP_User::getAdminId().'"%' );
							
							if($WHERE_CONDITIONS_BALANCE != "" )$WHERE_CONDITIONS_BALANCE .= " AND ";
							$WHERE_CONDITIONS_BALANCE .= " `office_id` IN (SELECT `id` FROM `shop_offices` WHERE `users` LIKE ?)";
							array_push($binding_values_conditions_balance, '%"'.DP_User::getAdminId().'"%');
						}
						if($WHERE_CONDITIONS != "") $WHERE_CONDITIONS = "WHERE ".$WHERE_CONDITIONS;
						if($WHERE_CONDITIONS_BALANCE != "")
						{
							$WHERE_CONDITIONS_BALANCE_INCOME = "WHERE ".$WHERE_CONDITIONS_BALANCE." AND `income` = 1";
							$WHERE_CONDITIONS_BALANCE_ISSUE = "WHERE ".$WHERE_CONDITIONS_BALANCE." AND `income` = 0";
						}
						else
						{
							$WHERE_CONDITIONS_BALANCE_INCOME = "WHERE `income` = 1";
							$WHERE_CONDITIONS_BALANCE_ISSUE = "WHERE `income` = 0";
						}
						
						
						//Формируем запрос
						$SQL_operation_name = "(SELECT `name` FROM `shop_accounting_codes` WHERE `id` = `shop_users_accounting`.`operation_code`)";
						
						
						//Подсчет сальдо
						$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` $WHERE_CONDITIONS_BALANCE_INCOME), 0)";
						$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` $WHERE_CONDITIONS_BALANCE_ISSUE),0)";
						
						
						$SQL_SELECT = "SELECT SQL_CALC_FOUND_ROWS *, $SQL_operation_name AS `name`, ($INCOME_SQL-$ISSUE_SQL) AS `balance`, ($SQL_SELECT_CUSTOMER) AS `customer`, CAST(`amount` AS DECIMAL(8,2) ) AS `amount`, IFNULL( (SELECT CONCAT(`caption`, ', ', `city`, ', ', `address`, ', ', `phone`) FROM `shop_offices` WHERE `id` = `shop_users_accounting`.`office_id`), 'Без привязки' ) AS `office_caption` FROM `shop_users_accounting` $WHERE_CONDITIONS ORDER BY `".$sort_field."` ".$sort_asc_desc." LIMIT ".$row_from.",".$rows_per_page;
						
						//var_dump($SQL_SELECT);
						
						$binding_values = array_merge($binding_values_conditions_balance, $binding_values_conditions_balance);
						$binding_values = array_merge($binding_values, $binding_values_conditions);
						
						$elements_query = $db_link->prepare($SQL_SELECT);
						$elements_query->execute($binding_values);
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						
						$saldo = "no";
						while($element_record = $elements_query->fetch())
						{
							//Для Javascript
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
							
							
							if( $saldo == "no" )
							{
								$saldo = $element_record['balance'];
							}
							
							$amount = $element_record["amount"];
							$css_sub_color = "";
							if($element_record["income"] == 1)
							{
								$css_sub_color = "background-color:#d4ffd0;";
								$amount = "+".$amount;
							}
							else
							{
								$css_sub_color = "background-color:#ffecec;";
								$amount = "-".$amount;
							}
							
							
							$id = $element_record["id"];
							$time = $element_record["time"];
							$user_id = $element_record["user_id"];
							$name = translate_str_by_id($element_record["name"]);
							$customer_id = $element_record["user_id"];//Статистика
							?>
							
							
							
							<tr id="operation_record_<?php echo $id; ?>" style="<?php echo $css_sub_color; ?>">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>" /></td>
								<td><?php echo $id; ?></td>
								<td><?php echo date("d.m.Y", $time)."<br>".date("G:i", $time); ?></td>
								<td><?php echo $amount; ?></td>
								<td>
								<?php
								if( $element_record["order_id"] > 0 )
								{
									?>
									<a style="text-decoration:underline;" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/order?order_id=<?php echo $element_record["order_id"]; ?>" target="_blank"><?php echo $element_record["order_id"]; ?></a>
									<?php
								}
								else
								{
									echo "-";
								}
								?>
								</td>
								<td>
								<?php
								if($element_record["income"] == 1)
								{
									echo translate_str_by_id(3240);
								}
								else
								{
									echo translate_str_by_id(3241);
								}
								?>
								</td>
								<td>
								<?php
								if( empty($element_record["customer"]) )
								{
									echo "ID ".$element_record["user_id"].", ".translate_str_by_id(3254);
								}
								else
								{
									if( $element_record["user_id"] > 0 )
									{
										?>
                                        <?php include $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/users/statistics/modal.php';//Статистика?><a style="text-decoration:underline;" target="_blank" href="/<?php echo $DP_Config->backend_dir; ?>/users/usermanager/user?user_id=<?php echo $element_record["user_id"]; ?>"><?php echo $element_record["customer"]; ?></a>
										<?php
									}
									else
									{
										echo $element_record["customer"];
									}
								}
								?>
								</td>
								<td id="name_<?php echo $id; ?>"><?php echo translate_str_by_id(2533)." ".$element_record["operation_code"].", ".$name; ?></td>
								
								<?php
								if( isset( $DP_Config->wholesaler ) )
								{
									?>
									<td id="name_<?php echo $id; ?>"><?php echo $element_record["office_caption"]; ?></td>
									<?php
								}
								?>
								
							</tr>
							<?php
						}//while()
						?>
						</tbody>
						<tfoot>
							<tr>
								<?php
								$colspan="8";
								if( isset( $DP_Config->wholesaler ) )
								{
									$colspan="9";
								}
								?>
								<td colspan="<?php echo $colspan; ?>" style="text-align:center;">
									<div class="btn-group">
										<?php
										//КНОПКА "ВЛЕВО"
										$to_left_disabled = "";
										if( $current_page == 0 )
										{
											$to_left_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=0"><?php echo translate_str_by_id(4038); ?></a>
										<a class="btn btn-default <?php echo $to_left_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $current_page-1; ?>"><i class="fa fa-chevron-left"></i></a>
										
										
										<?php
										//Определяем количество страниц
										$pages_count = (int)($elements_count_rows/$rows_per_page);
										if( ($elements_count_rows%$rows_per_page) > 0 )
										{
											$pages_count++;
										}
										
										
										//Выводим кнопки для конкретных страниц (с номерами)
										for($i=0; $i < $pages_count; $i++)
										{
											//Две кнопки до текущей - показываем
											if( ($current_page - $i) > 2  )
											{
												continue;
											}
											
											
											//Две кнопки после текущей - показываем
											if( ($i - $current_page) > 2  )
											{
												break;
											}
											
											
											
											$active = "";
											if($i == $current_page)
											{
												$active = "active";
											}
											?>
											<a class="btn btn-default <?php echo $active; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $i; ?>"><?php echo $i+1; ?></a>
											<?php
										}
										
										
										//КНОПКА "ВПРАВО"
										$to_right_disabled = "";
										if( ($current_page+1) == $pages_count )
										{
											$to_right_disabled = "disabled";
										}
										?>
										<a class="btn btn-default <?php echo $to_right_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $current_page+1; ?>"><i class="fa fa-chevron-right"></i></a>
										<a class="btn btn-default <?php echo $to_right_disabled; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations?page=<?php echo $pages_count-1; ?>"><?php echo translate_str_by_id(2184); ?></a>
									</div>
									
									<br>
									<div style="text-align:left;">
									<?php echo translate_str_by_id(3255); ?>: <?php echo $elements_count_rows; ?>, <?php echo translate_str_by_id(3256); ?>: <?php echo $rows_per_page; ?>, <?php echo translate_str_by_id(3257); ?>: <?php echo $pages_count; ?>
									</div>
								</td>
							</tr>
						</tfoot>
					</table>
				</div>
			</div>
			<?php
			if($saldo === 'no'){
				$saldo = 0;
			}
			?>
			<div class="panel-footer">
                <?php echo translate_str_by_id(3258); ?>: <?php echo number_format($saldo, 2, '.', ''); ?>
            </div>
		</div>
	</div>
	
	
	<script>
    // ----------------------------------------------------------------------------------------
    <?php
    echo $for_js;//Выводим массив с чекбоксами для элементов
    ?>
    //Обработка переключения Выделить все/Снять все
    function on_check_uncheck_all()
    {
        var state = document.getElementById("check_uncheck_all").checked;
        
        for(var i=0; i<elements_array.length;i++)
        {
            document.getElementById(elements_array[i]).checked = state;
        }
    }//~function on_check_uncheck_all()
    // ----------------------------------------------------------------------------------------
    //Обработка переключения одного чекбокса
    function on_one_check_changed(id)
    {
        //Если хотя бы один чекбокс снят - снимаем общий чекбокс
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == false)
            {
                document.getElementById("check_uncheck_all").checked = false;
                break;
            }
        }
    }//~function on_one_check_changed(id)
    // ----------------------------------------------------------------------------------------
    //Получение массива id отмеченых элементов
    function getCheckedElements()
    {
        var checked_ids = new Array();
        //По массиву чекбоксов
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == true)
            {
                checked_ids.push(elements_id_array[i]);
            }
        }
        
        return checked_ids;
    }
    // ----------------------------------------------------------------------------------------
    </script>
	
    <?php
}//else//Действий нет - выводим страницу
?>