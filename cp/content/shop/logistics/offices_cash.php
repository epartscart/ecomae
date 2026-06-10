<?php
/**
 * Страница управления операциями в разделе Наличные магазина
 * 
 * Данная страница предназначена для создания приходных / расходных операций в разделе Наличные магазина
*/

defined('_ASTEXE_') or die('No access');



//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );

//Проверяем право менеджера
if( ! DP_User::isAdmin() )
{
	$result["status"] = false;
	$result["message"] = "Forbidden";
	$result["code"] = 501;
	exit(json_encode($result));//Вообще не является администратором бэкенда
}

//Менеджер
$user_session = DP_User::getAdminSession();
$manager_id = DP_User::getAdminId();

//Получаем список магазинов в которых данный пользователь указан в качестве менеджера
$manager_offices_list = array();
$query = $db_link->prepare('SELECT `id`, `caption` FROM `shop_offices` WHERE `users` LIKE ?;');
$query->execute( array('%"'.$manager_id.'"%') );
while($row = $query->fetch(PDO::FETCH_ASSOC)){
	$manager_offices_list[$row['id']] = $row;
}



if( ! empty($_POST["action"]))
{

	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	$office_id = ((int) $_POST['office_id']);
	
	if( ! isset($manager_offices_list[$office_id]) )
	{
		$location_url = '/'.$DP_Config->backend_dir.'/shop/cash';
		?>
		<script>
			location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5268)); ?>";
		</script>
		<?php
		exit;
	}
	else
	{
		switch($_POST["action"]){
			case 'add' :
				if( (int)$_POST["income"] > 0 ){
					$income = 1;
				}else{
					$income = 0;
				}
				
				$comment = trim($_POST["comment"]);
				$comment = htmlentities(str_replace(array("\"", "\\", "'", "\r", "\t"), "", $comment), ENT_QUOTES, "UTF-8");
				$comment = str_replace("\n","<br/>",$comment);
				
				$amount = (float) $_POST["amount"];
				if($amount <= 0){
					$location_url = '/'.$DP_Config->backend_dir.'/shop/cash?office_id='.$office_id;
					?>
					<script>
						location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5269)); ?>";
					</script>
					<?php
					exit;
				}
				
				$name = (int) trim($_POST["name"]);
				$query = $db_link->prepare('SELECT `id` FROM `shop_offices_cash_codes` WHERE `income` = ? AND `id` = ? AND `office_id` = ?;');
				$query->execute( array($income, $name, $office_id) );
				$row = $query->fetch(PDO::FETCH_ASSOC);
				if( empty($row) )
				{
					$location_url = '/'.$DP_Config->backend_dir.'/shop/cash?office_id='.$office_id;
					?>
					<script>
						location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5270)); ?>";
					</script>
					<?php
					exit;
				}
				else
				{
					if( $db_link->prepare('INSERT INTO `shop_offices_cash`(`id`, `office_id`, `manager_id`, `time`, `income`, `amount`, `operation_code`, `comment`) VALUES (NULL, ?, ?, ?, ?, ?, ?, ?);')->execute( array($office_id, $manager_id, time(), $income, $amount, $name, $comment) ) != true )
					{
						$location_url = '/'.$DP_Config->backend_dir.'/shop/cash?office_id='.$office_id;
						?>
						<script>
							location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5271)); ?>";
						</script>
						<?php
						exit;
					}
					else
					{
						$location_url = '/'.$DP_Config->backend_dir.'/shop/cash?office_id='.$office_id;
						?>
						<script>
							location="<?=$location_url?>&success_message=<?php echo urlencode(translate_str_by_id(5272)); ?>";
						</script>
						<?php
						exit;
					}
				}
			break;
			default :
				$location_url = '/'.$DP_Config->backend_dir.'/shop/cash';
				?>
				<script>
					location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(2304)); ?>";
				</script>
				<?php
				exit;
			break;
		}
	}

}
else//Действий нет - выводим страницу
{
	require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий

	
	
	//Выбран ли магазина
	if( ((int) $_GET['office_id']) > 0 )
	{
		if( ! isset($manager_offices_list[$_GET['office_id']]) ){
			echo translate_str_by_id(5268);
		}
		else
		{
			$office_id = ((int) $_GET['office_id']);
		?>
			
			<div class="col-lg-12">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(2113); ?>
					</div>
					<div class="panel-body">
						
						<?php
						if(count($manager_offices_list) > 1)
						{
							print_backend_button(array('background_color'=>'#b9babb', 'fontawesome_class'=>'fas fa-chevron-left', 'caption'=>translate_str_by_id(2961), 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/shop/cash'));
						}
						print_backend_button(array("background_color"=>"#3498db", "fontawesome_class"=>"fas fa-align-justify", "caption"=>translate_str_by_id(3235), "url"=>"/".$DP_Config->backend_dir."/shop/cash/operations_editor?office_id=".$office_id));
						?>
						<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
							<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
							<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
						</a>
					</div>
				</div>
			</div>
			
			<div class="col-lg-4">
				<form method="POST">
					<div class="hpanel">
						<div class="panel-heading hbuilt">
							<?php echo translate_str_by_id(5273); ?>
						</div>
						<div class="panel-body">
							<div class="table-responsive">
								<input type="hidden" name="office_id" value="<?php echo $office_id; ?>" />
								<input type="hidden" name="action" value="add" />
								<input type="hidden" name="income" value="1" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
								
								<label><?php echo translate_str_by_id(3251); ?></label>
								<input class="form-control" type="number" name="amount" placeholder="0" value="" />
								
								<br/>
								<label><?php echo translate_str_by_id(2277); ?></label>
								<select placeholder="<?php echo translate_str_by_id(5274); ?>" name="name" class="form-control">
									<?php
									$query = $db_link->prepare('SELECT * FROM `shop_offices_cash_codes` WHERE `income` = 1 AND `office_id` = ?;');
									$query->execute( array($office_id) );
									while($row = $query->fetch(PDO::FETCH_ASSOC))
									{
										?>
										<option value="<?php echo $row['id']; ?>" ><?php echo translate_str_by_id($row["name"]); ?></option>
										<?php
									}
									?>
								</select>
								
								<br/>
								<label><?php echo translate_str_by_id(3571); ?></label>
								<textarea class="form-control" name="comment" placeholder="<?php echo translate_str_by_id(5275); ?>"></textarea>
							</div>
						</div>
						<div class="panel-footer">
							<button class="btn btn-ar btn-success" type="submit"><?php echo translate_str_by_id(2292); ?></button>
						</div>
					</div>
				</form>
			</div>
			
			
			<div class="col-lg-4">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5139); ?>
					</div>
					<div class="panel-body text-center">
						<?php
						$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_offices_cash` WHERE `office_id` = ? AND `income` = 1), 0)";
						$ISSUE_SQL  = "IFNULL((SELECT SUM(`amount`) FROM `shop_offices_cash` WHERE `office_id` = ? AND `income` = 0), 0)";
						
						$SQL = "SELECT ($INCOME_SQL - $ISSUE_SQL) AS `balance`;";
						
						$query = $db_link->prepare($SQL);
						$query->execute( array($office_id, $office_id) );
						$row = $query->fetch(PDO::FETCH_ASSOC);
						
						$balance = $row["balance"];
						if($balance == "")
						{
							$balance = 0;
						}
						
						if($row["balance"] > 0){
							$balance = number_format($balance, 2, ',', ' ');
							$balance = str_replace(',', '<small class="text-success">,', $balance).'</small>';
						?>
							<h1 class="m-xs text-success" style="font-weight: 600; font-size: 56px;" ><?php echo $balance; ?></h1>
						<?php
						}else{
							$balance = number_format($balance, 2, ',', ' ');
							$balance = str_replace(',', '<small class="text-danger">,', $balance).'</small>';
						?>
							<h1 class="m-xs text-danger" style="font-weight: 600; font-size: 56px;" ><?php echo $balance; ?></h1>
						<?php
						}
						?>
					</div>
				</div>
			</div>
			
			
			<div class="col-lg-4">
				<form method="POST">
					<div class="hpanel">
						<div class="panel-heading hbuilt">
							<?php echo translate_str_by_id(5276); ?>
						</div>
						<div class="panel-body">
							<div class="table-responsive">
								<input type="hidden" name="office_id" value="<?php echo $office_id; ?>" />
								<input type="hidden" name="action" value="add" />
								<input type="hidden" name="income" value="0" />
								<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
								
								<label><?php echo translate_str_by_id(3251); ?></label>
								<input class="form-control" type="number" name="amount" placeholder="0" value="" />
								
								<br/>
								<label><?php echo translate_str_by_id(2277); ?></label>
								<select placeholder="<?php echo translate_str_by_id(5274); ?>" name="name" class="form-control">
									<?php
									$query = $db_link->prepare('SELECT * FROM `shop_offices_cash_codes` WHERE `income` = 0 AND `office_id` = ?;');
									$query->execute( array($office_id) );
									while($row = $query->fetch(PDO::FETCH_ASSOC))
									{
										?>
										<option value="<?php echo $row['id']; ?>" ><?php echo translate_str_by_id($row["name"]); ?></option>
										<?php
									}
									?>
								</select>
								
								<br/>
								<label><?php echo translate_str_by_id(3571); ?></label>
								<textarea class="form-control" name="comment" placeholder="<?php echo translate_str_by_id(5275); ?>"></textarea>
							</div>
						</div>
						<div class="panel-footer text-right">
							<button class="btn btn-ar btn-danger" type="submit"><?php echo translate_str_by_id(2292); ?></button>
						</div>
					</div>
				</form>
			</div>
			
			<div class="col-lg-12"></div>
			
			<div class="col-lg-12">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5277); ?>
					</div>
					<div class="panel-body">
						<div class="table-responsive" style="max-height: 320px; overflow: auto;">
							<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
								<thead> 
									<tr>
										<th>ID</th>
										<th><?php echo translate_str_by_id(5278); ?></th>
										<th><?php echo translate_str_by_id(3250); ?></th>
										<th><?php echo translate_str_by_id(2277); ?></th>
										<th><?php echo translate_str_by_id(3251); ?></th>
										<th><?php echo translate_str_by_id(3571); ?></th>
									</tr>
								</thead>
								<tbody>
									<?php
									$query = $db_link->prepare('SELECT *, (SELECT `name` FROM `shop_offices_cash_codes` WHERE `id` = `shop_offices_cash`.`operation_code`) AS "name" FROM `shop_offices_cash` WHERE `office_id` = ? ORDER BY `id` DESC;');
									$query->execute( array($office_id) );
									while($row = $query->fetch(PDO::FETCH_ASSOC))
									{
										if($row['income'] == 1){
											?>
												<tr style="background:#edf1c8;">
													<td><?php echo $row["id"]; ?></td>
													<td><?php echo $row["manager_id"]; ?></td>
													<td><?php echo date('d.m.Y H:i', $row["time"]); ?></td>
													<td><?php echo translate_str_by_id($row["name"]); ?></td>
													<td><?php echo $row["amount"]; ?></td>
													<td><?php echo $row["comment"]; ?></td>
												</tr>
											<?php
										}else{
											?>
												<tr style="background:#f7d9d7;">
													<td><?php echo $row["id"]; ?></td>
													<td><?php echo $row["manager_id"]; ?></td>
													<td><?php echo date('d.m.Y H:i', $row["time"]); ?></td>
													<td><?php echo translate_str_by_id($row["name"]); ?></td>
													<td><?php echo $row["amount"]; ?></td>
													<td><?php echo $row["comment"]; ?></td>
												</tr>
											<?php
										}
									}//foreach
									?>
								</tbody>
							</table>
						</div>
					</div>
				</div>
			</div>
			
		<?php
		}
	}
	else
	{
		//Отображаем список магазинов для которых данный пользователь является менеджером
		
		if( empty($manager_offices_list) )
		{
			echo translate_str_by_id(5279);
		}
		else
		{
		?>
			
			<?php
			if(count($manager_offices_list) === 1)
			{
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/cash?office_id=<?php echo array_shift($manager_offices_list)['id']; ?>";
			</script>
			<?php
			}
			?>
			
			<div class="col-lg-12">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(3413); ?>
					</div>
					<div class="panel-body">
						<div class="table-responsive">
							<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
								<thead> 
									<tr>
										<th>ID</th>
										<th><?php echo translate_str_by_id(2277); ?></th>
									</tr>
								</thead>
								<tbody>
									<?php
									foreach($manager_offices_list as $item_office)
									{
										$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/shop/cash?office_id=".$item_office["id"]."\">";
									?>
										<tr>
											<td><?php echo $a_item.$item_office["id"]; ?></a></td>
											<td><?php echo $a_item.$item_office["caption"]; ?></a></td>
										</tr>
									<?php
									}//foreach
									?>
								</tbody>
							</table>
						</div>
					</div>
				</div>
			</div>
		<?php
		}
	}
}
?>