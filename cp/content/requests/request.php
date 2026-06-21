<?php
/**
 * Страничный скрипт для страницы конкретного запроса пользователя
*/
defined('_ASTEXE_') or die('No access');

$vin_id = (int) $_GET["vin_id"];

//Ставим флаг "Просмотрен"
$db_link->prepare("UPDATE `users_vin` SET `viewed` = 1 WHERE `id` = ?;")->execute( array($vin_id) );

//Информация о запросе
$vin_list_query = $db_link->prepare("SELECT * FROM `users_vin` WHERE `id` = ?;");
$vin_list_query->execute( array($vin_id) );
$vin_list_array = $vin_list_query->fetch();
?>





<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			
			<?php
			print_backend_button(array('background_color'=>'#b9babb', 'fontawesome_class'=>'fas fa-chevron-left', 'caption'=>translate_str_by_id(2961), 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/requests'));
			print_backend_button(array('background_color'=>'#ffb606', 'fontawesome_class'=>'fas fa-eye-slash', 'caption'=>translate_str_by_id(3582), 'onclick'=>'setNoViewed();', 'url'=>'javascript:void(0);'));
			?>
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>
<script>
// --------------------------------------------------------------------------------------------
//Функция изменения статуса просмотра
function setNoViewed()
{
	//Объект запроса
	var request_object = new Object;
	request_object.vins = '[<?php echo $vin_id; ?>]';
	request_object.viewed_flag = 0;

	jQuery.ajax({
		type: "POST",
		async: true, //Запрос асинхронный
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/requests/ajax_set_users_vin_viewed.php",
		dataType: "json",//Тип возвращаемого значения
		data: "request_object="+JSON.stringify(request_object)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer.status == true)
			{
				//Обновляем страницу
				location='/<?php echo $DP_Config->backend_dir; ?>/requests';
			}
			else
			{
				console.log(answer);
				alert("<?php echo translate_str_by_id(3599); ?>");
			}
		}
	});
}
// --------------------------------------------------------------------------------------------
</script>

	
	
	
<link href="/lib/Lightbox/css/lightbox.css" rel="stylesheet" type="text/css" />
<script type="text/javascript" src="/lib/Lightbox/js/lightbox.js"></script>
<div class="col-lg-5">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3145); ?>
		</div>
		<div class="panel-body control-label">
			<?php echo $vin_list_array['text']; ?>
		</div>
	</div>
</div>
<style>
.vin-file-list-img
{
	display: inline-block; 
	border: 1px solid #ddd; 
	border-radius: 5px; 
	padding:15px;
}

.vin-file-list-img > div
{
	background-size: contain; 
	background-repeat: no-repeat; 
	background-position: center center;
	width: 100px;
	height: 100px;
}
</style>





<?php
//Пользователь
//Отображаем блок только для Администраторов с доступом к Менеджеру пользователей
$adminProfile = DP_User::getAdminProfile();//Профиль администратора
$SQL = "SELECT * FROM `content_access` WHERE `content_id` IN(SELECT `id` FROM `content` WHERE `alias` = 'usermanager' AND `is_frontend` = 0) AND `group_id` = ?;";
$query = $db_link->prepare($SQL);
$query->execute(array($adminProfile["groups"][0]));
$row = $query->fetch();
if( !empty($row) )
{
?>
<div class="col-lg-7">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3245); ?>
		</div>
		<div class="panel-body control-label">
		<?php
		if($vin_list_array['user_id'] > 0){
		?>
		
			<?php
			//Формируем массив полей профиля пользователей, которые выводятся в таблицу
			$profile_colomns = array();
			$profile_colomns_names_checked = array();//Массив для хранения имен полей профиля (для проверки на безопасность вставки в SQL-запрос)
			$profile_colomns_query = $db_link->prepare("SELECT * FROM `reg_fields` WHERE `to_users_table` = 1 ORDER BY `order`;");
			$profile_colomns_query->execute();
			while( $column = $profile_colomns_query->fetch() )
			{
				//Обрабатываем значение перед вставкой в SQL-запрос. $column["name"] - могут быть только буквы и знак _
				$column["name"] = str_replace(array(" ", "-", "#", "'", "(", ")"), "", $column["name"]);
				
				array_push($profile_colomns_names_checked, $column["name"]);
				
				array_push($profile_colomns, $column);
			}
			?>
			
			<div class="table-responsive">
				<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
					<thead> 
						<tr> 
							<th>ID</th>
							<th></th>
							<th><?php echo translate_str_by_id(3664); ?></th>
							<th><?php echo translate_str_by_id(3665); ?></th>
							<th><?php echo translate_str_by_id(1307); ?></th>
							<th>E-mail</th>
							<th><?php echo translate_str_by_id(1312); ?></th>
							<th><?php echo translate_str_by_id(376); ?></th>
						</tr>
					</thead>
					<tbody>
					<?php
					//Получаем ассоциативный массив group_id => "Имя группы"
					$groups_list_query = $db_link->prepare("SELECT * FROM `groups`");
					$groups_list_query->execute();
					$groups_list = array();
					while( $groups_list_record = $groups_list_query->fetch() )
					{
						$groups_list[$groups_list_record["id"]] = translate_str_by_id($groups_list_record["value"]);
					}
					
					
					//Баланс покупателя
					$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = users.user_id AND `income`=1 AND `active` = 1), 0)";
					$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = users.user_id AND `income`=0 AND `active` = 1),0)";
					
					
					//Формируем часть SQL-запрос для получения значений колонок профиля пользователя
					$get_profile_cols_SQL = "";
					foreach($profile_colomns AS $column)
					{
						if( $get_profile_cols_SQL != "" )
						{
							$get_profile_cols_SQL = $get_profile_cols_SQL.",";
						}
						
						$get_profile_cols_SQL = $get_profile_cols_SQL." (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$column["name"]."' AND `user_id` = `users`.`user_id`) AS `".$column["name"]."` ";
					}
					if( $get_profile_cols_SQL != "" )
					{
						$get_profile_cols_SQL = ",".$get_profile_cols_SQL;
					}
					
					
					//Получаем список зарегистрированных пользователей
					$users_list_SQL = "SELECT DISTINCT(users.`user_id`) AS `user_id`, 
					`users`.`reg_variant` AS `reg_variant`,
					`users`.`email` AS `email`,
					`users`.`email_confirmed` AS `email_confirmed`,
					`users`.`phone` AS `phone`,
					`users`.`phone_confirmed` AS `phone_confirmed`,
					`users`.`unlocked` AS `unlocked`,
					`users`.`time_registered` AS `time_registered`,
					`users`.`time_last_visit` AS `time_last_visit`,
					($INCOME_SQL-$ISSUE_SQL) AS `balance`,
					`users`.`admin_created` AS `admin_created` ".$get_profile_cols_SQL." 
						FROM
					users
					INNER JOIN reg_variants ON reg_variants.id = users.reg_variant
					INNER JOIN users_profiles ON users.user_id = users_profiles.user_id
					INNER JOIN users_groups_bind ON users_groups_bind.user_id = users.user_id WHERE users.user_id = ?;";
					

					$users_list_query = $db_link->prepare($users_list_SQL);
					$users_list_query->execute( array($vin_list_array['user_id']) );
					$users_list_array = $users_list_query->fetch();
					
					
					if( ! empty($users_list_array))
					{
						$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/users/usermanager/user?user_id=".$users_list_array["user_id"]."\">";
						?>
						<tr>
							<td><?php echo $a_item.$users_list_array["user_id"]; ?></a></td>
							<td style="white-space: nowrap;">
								<a style="display: inline-block; width: 22px; height: 22px; background: url(/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/user.png) 0 0 no-repeat; background-size: cover;" href="/<?php echo $DP_Config->backend_dir; ?>/users/usermanager/user?user_id=<?php echo $users_list_array["user_id"]; ?>"></a>
								<a style="display: inline-block; width: 22px; height: 22px; background: url(/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/store.png) 0 0 no-repeat; background-size: cover;" href="javascript:void(0);" onclick="locationOrders(<?php echo $users_list_array["user_id"]; ?>);"></a>
								<a style="display: inline-block; width: 22px; height: 22px; background: url(/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/credit_card.png) 0 0 no-repeat; background-size: cover;" href="javascript:void(0);" onclick="locationBalance(<?php echo $users_list_array["user_id"]; ?>);"></a>
								<a style="display: inline-block; width: 22px; height: 22px; background: url(/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/key.png) 0 0 no-repeat; background-size: cover;" href="javascript:void(0);" onclick="auth_with_user(<?php echo $users_list_array["user_id"]; ?>);"></a>
								<a style="display: inline-block; width: 22px; height: 22px; background: url(/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/statistics.png) 0 0 no-repeat; background-size: cover;" href="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir."/users/usermanager/user?user_id=".$users_list_array["user_id"]."&type=statistics"; ?>"></a>
							</td>
							<td>
								<?php
									//Получаем список групп пользователя
									$user_groups_list_query = $db_link->prepare("SELECT * FROM `users_groups_bind` WHERE `user_id` = ?;");
									$user_groups_list_query->execute( array($users_list_array["user_id"]) );
									$first = true;
									while( $user_group_record = $user_groups_list_query->fetch() )
									{
										if(!$first)
										{
											echo ";<br>";
										}
										else
										{
											$first = false;
										}
										echo $groups_list[$user_group_record["group_id"]];
									}
									
								?>
							</td>
							<td style="white-space:nowrap;"><?php echo $a_item.$users_list_array["surname"]; ?></a></td>
							<td style="white-space:nowrap;"><?php echo $a_item.$users_list_array["name"]; ?></a></td>
							<td>
								<?php echo $a_item.$users_list_array["email"]; ?></a>
								
								<?php
								if( !empty($users_list_array["email"]) )
								{
									if( $users_list_array["email_confirmed"] == 0 )
									{
										?>
										<i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="<?php echo translate_str_by_id(3545); ?>"></i>
										<?php
									}
									else
									{
										?>
										<i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="<?php echo translate_str_by_id(3546); ?>"></i>
										<?php
									}
								}
								?>
							</td>
							<td>
								<?php echo $a_item.$users_list_array["phone"]; ?></a>
								
								<?php
								if( !empty($users_list_array["phone"]) )
								{
									if( $users_list_array["phone_confirmed"] == 0 )
									{
										?>
										<i class="fa fa-exclamation-triangle" style="color:#F00;cursor:pointer;" title="<?php echo translate_str_by_id(3545); ?>"></i>
										<?php
									}
									else
									{
										?>
										<i class="fa fa-check-circle" style="color:#0A0;cursor:pointer;" title="<?php echo translate_str_by_id(3546); ?>"></i>
										<?php
									}
								}
								?>
							</td>
							<td style="white-space:nowrap;"><?php echo $a_item.number_format($users_list_array["balance"],2,'.',' '); ?></a></td>
						</tr>
					<?php
					}
					?>
					</tbody>
				</table>
			</div>
			<script>
			function locationOrders(user_id)
			{
				var orders_filter = new Object;
				//1. Время с
				orders_filter.time_from = "";
				//2. Время по
				orders_filter.time_to = "";
				//3. Номер заказа
				orders_filter.order_id = "";
				//4. Статус заказа
				orders_filter.status = 0;
				//5. Товар
				orders_filter.paid = -1;
				//6. Просмотрен
				orders_filter.viewed = -1;
				//7. Покупатель
				orders_filter.customer = "";
				orders_filter.customer_id = user_id;
				orders_filter.paid_type = -1;
				//Устанавливаем cookie (на полгода)
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "orders_filter="+JSON.stringify(orders_filter)+"; path=/; expires=" + date.toUTCString();

				//Обновляем страницу
				location='/<?php echo $DP_Config->backend_dir; ?>/shop/orders/orders';
			}
			
			function locationBalance(user_id)
			{
				var account_operations_filter = new Object;

				account_operations_filter.time_from = "";
				account_operations_filter.time_to = "";
				account_operations_filter.income = -1;
				account_operations_filter.operation_code = -1;
				account_operations_filter.user_id = user_id;

				//Устанавливаем cookie (на полгода)
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "account_operations_filter="+JSON.stringify(account_operations_filter)+"; path=/; expires=" + date.toUTCString();

				//Обновляем страницу
				location='/<?php echo $DP_Config->backend_dir; ?>/shop/finance/account_operations';
			}
			
			function auth_with_user(user_id)
			{
				jQuery.ajax({
					type: "POST",
					async: false, //Запрос синхронный
					url: "/<?php echo $DP_Config->backend_dir; ?>/content/users/auth_with_user.php",
					dataType: "json",//Тип возвращаемого значения
					data: "user_id="+user_id+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
					success: function(answer){
						if(answer.status == true)
						{
							window.open(
							  '<?php echo $DP_Config->domain_path; ?>',
							  '_blank'
							);
						}
						else
						{
							alert("<?php echo translate_str_by_id(3541); ?>");
						}
					}
				});
			}
			</script>
		
		<?php
		}else{
			echo translate_str_by_id(3570);
		}
		?>
		</div>
	</div>
</div>
<?php
}
?>





<div class="col-lg-7">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3563); ?>
		</div>
		<div class="panel-body control-label">
			
			<?php
			if($vin_list_array['user_id'] > 0){
			?>
				<!-- Переписка с покупателем -->
				<div>
					<div class="chat_block" id="chat_block">
					</div>
					
					<br>
					
					<?php echo translate_str_by_id(4549); ?>:
					<textarea class="form-control" id="new_message_area"></textarea>
					<a style="margin-top:10px;" class="btn btn-as btn-primary" onclick="sendMessage();"><?php echo translate_str_by_id(3211); ?></a>
				</div>
				<script>
				// --------------------------------------------------------------------------
				//Получить сообщения по заказу
				function getOrderMessages()
				{
					jQuery.ajax({
						type: "GET",
						async: true,
						url: "/content/requests/ajax_get_message.php",
						dataType: "json",//Тип возвращаемого значения
						data: "manager=1&vin_id=<?php echo $vin_id; ?>&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
						success: function(answer)
						{
							var html = "";
							for(var i=0; i < answer.length; i++)
							{
								var class_str = "bubble";
								var sender = "<?php echo translate_str_by_id(4550); ?>";
								if(answer[i].is_customer == false)
								{
									class_str += "2";
									sender = "<?php echo translate_str_by_id(3565); ?>";
								}
								html += "<div class=\""+class_str+"\"><small class=\"sender\">"+sender+" "+answer[i].time+"</small><br>"+answer[i].text+"</div>";	
							}
							if(html == "") html = "<div align=\"center\"><?php echo translate_str_by_id(5220); ?></div>";
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
						url: "/content/requests/ajax_send_message.php",
						dataType: "json",//Тип возвращаемого значения
						data: "manager=1&vin_id=<?php echo $vin_id; ?>&text="+encodeURI(text)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
						success: function(answer)
						{
							if(answer.status == true)
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
				
				//Запрашиваем переписку по заказу
				setInterval(function(){
					getOrderMessages();
				}, 180000);
				getOrderMessages();
				</script>
			
			<?php
			}else{
				echo translate_str_by_id(5221);
			}
			?>
			
		</div>
	</div>
</div>





<style>
/*Стили для чата на странице заказа*/
#new_message_area
{
	width:100%;
	height:100px;
}
/*Стиль для чата с покупателем*/
.chat_block
{
	max-height:600px;
	border:1px solid #EEE; 
	border-radius:7px;
	padding:10px;
	overflow-y:scroll;
}
.chat_left_block
{
	background-color:#c7ffe0;
	width:50%;
	padding:4px;
	border-radius:5px;
	margin:5px;
	display:block;
	box-shadow: 0 0 6px #B2B2B2;
}
.chat_right_block
{
	background-color:#ffe7c7;
	width:50%;
	padding:4px;
	border-radius:5px;
	margin:5px;
	display:block;
	box-shadow: 0 0 6px #B2B2B2;
}
.bubble
{
    background-color: #F2F2F2;
    border-radius: 5px;
    box-shadow: 0 0 6px #B2B2B2;
    display: inline-block;
    padding: 10px 18px;
    position: relative;
    vertical-align: top;
    float: left;   
    margin: 5px 45px 5px 20px; 
	border-color: #cdecb0;
}
.bubble::before 
{
    background-color: #F2F2F2;
    content: "\00a0";
    display: none;
    height: 16px;
    position: absolute;
    top: 11px;
    transform:             rotate( 29deg ) skew( -35deg );
        -moz-transform:    rotate( 29deg ) skew( -35deg );
        -ms-transform:     rotate( 29deg ) skew( -35deg );
        -o-transform:      rotate( 29deg ) skew( -35deg );
        -webkit-transform: rotate( 29deg ) skew( -35deg );
    width:  20px;
    box-shadow: -2px 2px 2px 0 rgba( 178, 178, 178, .4 );
    left: -9px; 
}
.bubble
{
	background-color: #F2F2F2;
	border-radius: 5px;
	box-shadow: 0 0 6px #B2B2B2;
	display: inline-block;
	padding: 10px 18px;
	position: relative;
	vertical-align: top;
	margin: 10px 10px;
	border-color: #cdecb0;
	width: 60%;
}
.bubble2
{
    background-color: #dfeecf;
    border-radius: 5px;
    box-shadow: 0 0 6px #B2B2B2;
    display: inline-block;
    padding: 10px 18px;
    position: relative;
    vertical-align: top;
    float: left;   
    margin: 5px 45px 5px 20px; 
	border-color: #cdecb0;
}
.bubble2::before 
{
	float:right;
    background-color: #dfeecf;
    content: "\00a0";
    display: none;
    height: 19px;
    /*position: absolute;*/
	position:relative;
    left: 26px;
	top: 11px;
    transform:             rotate( 205deg ) skew( -35deg );
        -moz-transform:    rotate( 205deg ) skew( -35deg );
        -ms-transform:     rotate( 205deg ) skew( -35deg );
        -o-transform:      rotate( 205deg ) skew( -35deg );
        -webkit-transform: rotate( 205deg ) skew( -35deg );
    width:  20px;
    box-shadow: -2px 2px 2px 0 rgba( 178, 178, 178, .4 );
}
.bubble2
{
	float: right;
	width: 60%;
	background-color: #dfeecf;
	border-radius: 5px;
	box-shadow: 0 0 6px #B2B2B2;
	display: inline-block;
	padding: 10px 18px;
	position: relative;
	vertical-align: top;
	margin: 10px 12px;
	border-color: #cdecb0;
}
.chat_block .bubble {
    background-color: #ffecec;
}
.chat_block .bubble2 {
    background-color: #e9ffe9;
}
</style>