<?php
/**
 * Страница управления - Коды операций для shop_offices_cash
 * 
 * Данная страница предназначена для создания приходных / расходных наименований для операций в разделе Наличные магазина
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



if( ! empty($_POST["action"]) )
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
				$name = trim($_POST["name"]);
				$name = htmlentities(str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), "", $name), ENT_QUOTES, "UTF-8");
				
				
				//Мультиязычность - кастомный алгоритм
				$name = save_custom_translation($_POST["name_lang_str_id"], $name);
				if( $name == 0 )
				{
					exit("Error save_custom_translation()");
				}
				
				
				
				$query = $db_link->prepare('SELECT `id` FROM `shop_offices_cash_codes` WHERE `income` = ? AND `name` = ? AND `office_id` = ?;');
				$query->execute( array($income, $name, $office_id) );
				$row = $query->fetch(PDO::FETCH_ASSOC);
				if(!empty($row))
				{
					$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
					?>
					<script>
						location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5280)); ?>";
					</script>
					<?php
					exit;
				}
				else
				{
					if( $db_link->prepare('INSERT INTO `shop_offices_cash_codes` (`id`, `income`, `name`, `office_id`) VALUES (NULL, ?, ?, ?);')->execute( array($income, $name, $office_id) ) != true )
					{
						$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
						?>
						<script>
							location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(3621)); ?>";
						</script>
						<?php
						exit;
					}
					else
					{
						$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
						?>
						<script>
							location="<?=$location_url?>&success_message=<?php echo urlencode(translate_str_by_id(5272)); ?>";
						</script>
						<?php
						exit;
					}
				}
			break;
			case 'del' :
				$id = (int) trim($_POST["id"]);
				
				$query = $db_link->prepare('SELECT `id` FROM `shop_offices_cash_codes` WHERE `id` = ? AND `office_id` = ?;');
				$query->execute( array($id, $office_id) );
				$row = $query->fetch(PDO::FETCH_ASSOC);
				if(empty($row))
				{
					$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
					?>
					<script>
						location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5281)); ?>";
					</script>
					<?php
					exit;
				}
				else
				{
					$query = $db_link->prepare('SELECT `id` FROM `shop_offices_cash` WHERE `operation_code` = ?;');
					$query->execute( array($id) );
					$row = $query->fetch(PDO::FETCH_ASSOC);
					if(!empty($row))
					{
						$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
						?>
						<script>
							location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5282)); ?>";
						</script>
						<?php
						exit;
					}else{
						if( $db_link->prepare('DELETE FROM `shop_offices_cash_codes` WHERE `id` = ?;')->execute( array($id) ) != true )
						{
							$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
							?>
							<script>
								location="<?=$location_url?>&error_message=<?php echo urlencode(translate_str_by_id(5263)); ?>";
							</script>
							<?php
							exit;
						}
						else
						{
							$location_url = '/'.$DP_Config->backend_dir.'/shop/cash/operations_editor?office_id='.$office_id;
							?>
							<script>
								location="<?=$location_url?>&success_message=<?php echo urlencode(translate_str_by_id(5284)); ?>";
							</script>
							<?php
							exit;
						}
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
						print_backend_button(array('background_color'=>'#b9babb', 'fontawesome_class'=>'fas fa-chevron-left', 'caption'=>'Назад', 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/shop/cash?office_id='.$office_id));
						?>
						<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
							<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
							<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
						</a>
					</div>
				</div>
			</div>
			
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5285); ?> <?php echo $office_id; ?>
					</div>
					<div class="panel-body">
						<div class="table-responsive">
							<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
								<thead> 
									<tr style="background:#edf1c8;">
										<th><?php echo translate_str_by_id(2277); ?></th>
										<th></th>
									</tr>
								</thead>
								<tbody>
									<tr>
										<td colspan="2">
											<form method="POST">
												<input type="hidden" name="office_id" value="<?php echo $office_id; ?>" />
												<input type="hidden" name="action" value="add" />
												<input type="hidden" name="income" value="1" />
												<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
												<div class="input-group" style="margin-bottom: 30px; margin-top: 10px;">
													<input value="" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(5274); ?>" name="name" />
													<input value="0" type="hidden" name="name_lang_str_id" />
													<span class="input-group-btn">
														<button class="btn btn-ar btn-success" type="submit"><?php echo translate_str_by_id(2292); ?></button>
													</span>
												</div>
											</form>
										</td>
									</tr>
									<?php
									$query = $db_link->prepare('SELECT * FROM `shop_offices_cash_codes` WHERE `income` = 1 AND `office_id` = ?;');
									$query->execute( array($office_id) );
									while($row = $query->fetch(PDO::FETCH_ASSOC))
									{
									?>
										<tr>
											<td><?php echo translate_str_by_id($row["name"]); ?></td>
											<td class="text-right"><a onClick="del_cash_codes(<?php echo $row['id']; ?>);" class="btn btn-xs btn-danger"><?php echo translate_str_by_id(2224); ?></a></td>
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
			
			
			
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5286); ?> <?php echo $office_id; ?>
					</div>
					<div class="panel-body">
						<div class="table-responsive">
							<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
								<thead> 
									<tr style="background:#f7d9d7;">
										<th><?php echo translate_str_by_id(2277); ?></th>
										<th></th>
									</tr>
								</thead>
								<tbody>
									<tr>
										<td colspan="2">
											<form method="POST">
												<input type="hidden" name="office_id" value="<?php echo $office_id; ?>" />
												<input type="hidden" name="action" value="add" />
												<input type="hidden" name="income" value="0" />
												<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
												<div class="input-group" style="margin-bottom: 30px; margin-top: 10px;">
													<input value="" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(5274); ?>" name="name" />
													<input value="0" type="hidden" name="name_lang_str_id" />
													<span class="input-group-btn">
														<button class="btn btn-ar btn-success" type="submit"><?php echo translate_str_by_id(2292); ?></button>
													</span>
												</div>
											</form>
										</td>
									</tr>
									<?php
									$query = $db_link->prepare('SELECT * FROM `shop_offices_cash_codes` WHERE `income` = 0 AND `office_id` = ?;');
									$query->execute( array($office_id) );
									while($row = $query->fetch(PDO::FETCH_ASSOC))
									{
									?>
										<tr>
											<td><?php echo translate_str_by_id($row["name"]); ?></td>
											<td class="text-right"><a onClick="del_cash_codes(<?php echo $row['id']; ?>);" class="btn btn-xs btn-danger"><?php echo translate_str_by_id(2224); ?></a></td>
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
			
			
			
			<form id="del_form" name="del_form" method="POST" class="hidden">
				<input type="hidden" name="office_id" value="<?php echo $office_id; ?>" />
				<input type="hidden" name="action" value="del" />
				<input type="hidden" id="code_id_del" name="id" value="" />
				<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
			</form>
			<script>
			function del_cash_codes(id){
				if( !confirm("<?php echo translate_str_by_id(5287); ?>") )
				{
					return;
				}
				document.getElementById('code_id_del').value = id;
				document.forms["del_form"].submit();
			}
			</script>
			
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
			echo translate_str_by_id(5288);
		}
	}
}
?>