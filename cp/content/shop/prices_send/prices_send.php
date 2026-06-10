<?php
/*
Страница настроек для формирования и отправки прайс листа клиентам
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$admin_id = DP_User::getAdminId();

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>





<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3662); ?>
		</div>
		<div class="panel-body">
			
			<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(3663); ?>
			</div>
			<div class="panel-body">
				<?php
				$user_id = "";
				$group_id = -1;
				$email = "";
				$cellphone = "";
				$surname = "";

				//Получаем текущие значения фильтра:
				$users_filter_send_prices = NULL;
				if( isset($_COOKIE["users_filter_send_prices"]) )
				{
					$users_filter_send_prices = $_COOKIE["users_filter_send_prices"];
				}
				if($users_filter_send_prices != NULL)
				{
					$users_filter_send_prices = json_decode($users_filter_send_prices, true);
					$user_id = $users_filter_send_prices["user_id"];
					$group_id = $users_filter_send_prices["group_id"];
					$email = $users_filter_send_prices["email"];
					$cellphone = $users_filter_send_prices["cellphone"];
					$surname = $users_filter_send_prices["surname"];
				}
				?>
				<div style="margin-top:5px;" class="col-lg-6">
					<div class="form-group">
						<label for="" class="col-lg-4 control-label">
							ID
						</label>
						<div class="col-lg-8">
							<input type="text"  id="user_id" value="<?php echo $user_id; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				<div style="margin-top:5px;" class="col-lg-6">
					<div class="form-group">
						<label for="" class="col-lg-4 control-label">
							<?php echo translate_str_by_id(3664); ?>
						</label>
						<div class="col-lg-8">
							<select id="group_id" class="form-control">
								<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
								<?php
								$groups_query = $db_link->prepare("SELECT * FROM `groups`");
								$groups_query->execute();
								while($group = $groups_query->fetch() )
								{
									?>
									<option value="<?php echo $group["id"]; ?>"><?php echo translate_str_by_id($group["value"])." (ID ".$group["id"].")"; ?></option>
									<?php
								}
								?>
							</select>
							<script>
								document.getElementById("group_id").value = <?php echo $group_id; ?>;
							</script>
						</div>
					</div>
				</div>
				
				<div style="margin-top:5px;" class="col-lg-6">
					<div class="form-group">
						<label for="" class="col-lg-4 control-label">
							E-mail
						</label>
						<div class="col-lg-8">
							<input type="text"  id="email" value="<?php echo $email; ?>" class="form-control"/>
						</div>
					</div>
				</div>
				
				<div style="margin-top:5px;" class="col-lg-6">
					<div class="form-group">
						<label for="" class="col-lg-4 control-label">
							<?php echo translate_str_by_id(1312); ?>
						</label>
						<div class="col-lg-8">
							<input type="text"  id="cellphone" value="<?php echo $cellphone; ?>" class="form-control"/>
						</div>
					</div>
				</div>
				
				
				
				<div style="margin-top:5px;" class="col-lg-6">
					<div class="form-group">
						<label for="" class="col-lg-4 control-label">
							<?php echo translate_str_by_id(3665); ?>
						</label>
						<div class="col-lg-8">
							<input type="text"  id="surname" value="<?php echo $surname; ?>" class="form-control"/>
						</div>
					</div>
				</div>
				
			</div>
			<div class="panel-footer">
				<button class="btn btn-success" type="button" onclick="filterUsers();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
				<button class="btn btn-primary" type="button" onclick="unsetFilterUsers();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2555); ?></button>
			</div>
		</div>
	</div>
	

    
	
	
	<script>
    // ------------------------------------------------------------------------------------------------
    //Устновка cookie в соответствии с фильтром
    function filterUsers()
    {
        var users_filter_send_prices = new Object;
        
		//1. ID пользователя
		users_filter_send_prices.user_id = document.getElementById("user_id").value;
		//2. Группа
		users_filter_send_prices.group_id = document.getElementById("group_id").value;
		//3. E-mail
		users_filter_send_prices.email = document.getElementById("email").value;
		//6. Телефон
		users_filter_send_prices.cellphone = document.getElementById("cellphone").value;
		//7. Фамилия
		users_filter_send_prices.surname = document.getElementById("surname").value;

        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "users_filter_send_prices="+JSON.stringify(users_filter_send_prices)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/prices_send';
    }
    // ------------------------------------------------------------------------------------------------
    //Снять все фильтры
    function unsetFilterUsers()
    {
        var users_filter_send_prices = new Object;
        
		users_filter_send_prices.user_id = "";
		users_filter_send_prices.group_id = -1;
		users_filter_send_prices.email = "";
		users_filter_send_prices.cellphone = "";
		users_filter_send_prices.surname = "";

        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "users_filter_send_prices="+JSON.stringify(users_filter_send_prices)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/prices_send';
    }
    // ------------------------------------------------------------------------------------------------
    </script>
	
	
	
	
	
	
	
	
    
    
    
    
    
    

    <?php
    //Выводим таблицу
    ?>
	<script>
    // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки пользователей
    function sortUsers(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("users_sort_send_prices");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("users_sort_send_prices"));
            //Если поле это же - обращаем направление
            if(current_sort_cookie.field == field)
            {
                if(current_sort_cookie.asc_desc == "asc")
                {
                    asc_desc = "desc";
                }
                else
                {
                    asc_desc = "asc";
                }
            }
        }
        
        
        var users_sort_send_prices = new Object;
        users_sort_send_prices.field = field;//Поле, по которому сортировать
        users_sort_send_prices.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "users_sort_send_prices="+JSON.stringify(users_sort_send_prices)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        location='/<?php echo $DP_Config->backend_dir; ?>/shop/prices_send';
    }
    // ------------------------------------------------------------------------------------------------
    // возвращает cookie с именем name, если есть, если нет, то undefined
    function getCookie(name) 
    {
        var matches = document.cookie.match(new RegExp(
            "(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
        ));
        return matches ? decodeURIComponent(matches[1]) : undefined;
    }
    // ------------------------------------------------------------------------------------------------
    </script>
	
	
	<div class="col-lg-8">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3666); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive" style="height:500px; overflow-y:auto;">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all_users" name="check_uncheck_all_users" onchange="on_check_uncheck_all_users();"/></th>
								<th><a href="javascript:void(0);" onclick="sortUsers('user_id');" id="user_id_sorter">ID</a></th>
								<th><?php echo translate_str_by_id(3664); ?></th>
								<th><a href="javascript:void(0);" onclick="sortUsers('email');" id="email_sorter">E-mail</a></th>
								<th><a href="javascript:void(0);" onclick="sortUsers('fio');" id="fio_sorter"><?php echo translate_str_by_id(3667); ?></a></th>
							</tr>
							<script>
								<?php
								//Определяем текущую сортировку и обозначаем ее:
								$users_sort_send_prices = NULL;
								if( isset($_COOKIE["users_sort_send_prices"]) )
								{
									$users_sort_send_prices = $_COOKIE["users_sort_send_prices"];
								}
								$sort_field = "user_id";
								$sort_asc_desc = "desc";
								if($users_sort_send_prices != NULL)
								{
									$users_sort_send_prices = json_decode($users_sort_send_prices, true);
									$sort_field = $users_sort_send_prices["field"];
									$sort_asc_desc = $users_sort_send_prices["asc_desc"];
								}
								
								if( strtolower($sort_asc_desc) == "asc" )
								{
									$sort_asc_desc = "asc";
								}
								else
								{
									$sort_asc_desc = "desc";
								}
								
								if( array_search($sort_field, array('user_id', 'email', 'fio') ) === false )
								{
									$sort_field = "user_id";
								}
								
								?>
								document.getElementById("<?php echo $sort_field; ?>_sorter").innerHTML += "<img src=\"/content/files/images/sort_<?php echo $sort_asc_desc; ?>.png\" style=\"width:15px\" />";
							</script>
						</thead>
						<tbody>
						<?php
						//Получаем ассоциативный массив group_id => "Имя группы"
						$groups_list_query = $db_link->prepare("SELECT * FROM `groups`");
						$groups_list_query->execute();
						$groups_list = array();
						while( $groups_list_record = $groups_list_query->fetch() )
						{
							$groups_list[$groups_list_record["id"]] = $groups_list_record["value"];
						}
						
						//Массивы для JS с id групп и с чекбоксами групп
						$for_js = "var users_array = new Array();\n";//Выведем массив для JS с чекбоксами пользователй
						$for_js = $for_js."var users_id_array = new Array();\n";//Выведем массив для JS с ID пользователей
						


						//Подстрока с условиями фильтрования пользователей
						$WHERE_CONDITIONS = "";
						
						$binding_values = array();
						
						//По куки фильтра:
						$users_filter_send_prices = NULL;
						if( isset($_COOKIE["users_filter_send_prices"]) )
						{
							$users_filter_send_prices = $_COOKIE["users_filter_send_prices"];
						}
						if($users_filter_send_prices != NULL)
						{
							$users_filter_send_prices = json_decode($users_filter_send_prices, true);
							
							//1. ID
							if($users_filter_send_prices["user_id"] != "")
							{
								if($WHERE_CONDITIONS != "")
								{
									$WHERE_CONDITIONS .= " AND ";
								}
								$WHERE_CONDITIONS .= " `users`.`user_id` = ?";
								
								array_push($binding_values, $users_filter_send_prices["user_id"]);
							}
							
							//2. Группа
							if($users_filter_send_prices["group_id"] != -1)
							{
								if($WHERE_CONDITIONS != "")
								{
									$WHERE_CONDITIONS .= " AND ";
								}
								$WHERE_CONDITIONS .= " `users_groups_bind`.`group_id` = ?";
								
								array_push($binding_values, $users_filter_send_prices["group_id"]);
							}
							
							//3. Email
							if($users_filter_send_prices["email"] != "")
							{
								if($WHERE_CONDITIONS != "")
								{
									$WHERE_CONDITIONS .= " AND ";
								}
								$WHERE_CONDITIONS .= " `users`.`email` = ?";
								
								array_push($binding_values, htmlentities($users_filter_send_prices["email"]));
							}
							
							//6. Телефон
							if($users_filter_send_prices["cellphone"] != "")
							{
								if($WHERE_CONDITIONS != "")
								{
									$WHERE_CONDITIONS .= " AND ";
								}
								$WHERE_CONDITIONS .= " IF( (SELECT COUNT(`users_profiles`.`user_id`) FROM users_profiles WHERE `users_profiles`.`data_key` ='cellphone' AND `users_profiles`.`data_value` = ? AND `users_profiles`.`user_id` = `users`.`user_id`)=1 , 1, 0 )=1";
								
								array_push($binding_values, $users_filter_send_prices["cellphone"]);
							}
							
							//7. Фамилия
							if($users_filter_send_prices["surname"] != "")
							{
								if($WHERE_CONDITIONS != "")
								{
									$WHERE_CONDITIONS .= " AND ";
								}
								$WHERE_CONDITIONS .= " IF( (SELECT COUNT(`users_profiles`.`user_id`) FROM users_profiles WHERE `users_profiles`.`data_key` ='surname' AND `users_profiles`.`data_value` = ? AND `users_profiles`.`user_id` = `users`.`user_id`)=1 , 1, 0 )=1";
								
								
								array_push($binding_values, $users_filter_send_prices["surname"]);
							}
							
							if($WHERE_CONDITIONS != "")
							{
								$WHERE_CONDITIONS = " WHERE ".$WHERE_CONDITIONS;
							}
						}//~if($users_filter_send_prices != NULL)
						
						
						
						
						//Получаем список зарегистрированных пользователей
						$users_list_SQL = "SELECT SQL_CALC_FOUND_ROWS DISTINCT(`users`.`user_id`) AS `user_id`, 
						`users`.`email` AS `email`,
						`users`.`email_confirmed` AS `email_confirmed`,
						trim(concat((SELECT `data_value` FROM `users_profiles` WHERE `users_profiles`.`data_key` ='surname' AND `users_profiles`.`user_id` = `users`.`user_id`), ' ', (SELECT `data_value` FROM `users_profiles` WHERE `users_profiles`.`data_key` ='name' AND `users_profiles`.`user_id` = `users`.`user_id`), ' ', (SELECT `data_value` FROM `users_profiles` WHERE `users_profiles`.`data_key` ='patronymic' AND `users_profiles`.`user_id` = `users`.`user_id`))) AS 'fio'
							FROM
						users
						INNER JOIN `users_profiles` ON `users`.`user_id` = `users_profiles`.`user_id`
						INNER JOIN `users_groups_bind` ON `users_groups_bind`.`user_id` = `users`.`user_id`".$WHERE_CONDITIONS." ORDER BY `$sort_field` $sort_asc_desc";
						
						
						
						//var_dump($users_list_SQL);
						

						$users_list_query = $db_link->prepare($users_list_SQL);
						$users_list_query->execute($binding_values);
						
						
						$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
						$elements_count_rows_query->execute();
						$elements_count_rows = $elements_count_rows_query->fetchColumn();
						
						
						for($i=0; $i<$elements_count_rows; $i++)//Цикл по всех пользователям
						{
							$users_list_array = $users_list_query->fetch();
							 
							?>
							<tr>
								<td>
									<?php
									//Чекбокс показываем только, если Email у пользователя указан. Флаг "Подтвержден" не имеет значения
									if( !empty($users_list_array["email"]) )
									{
										?>
										<input type="checkbox" onchange="on_one_check_changed_users('checked_users_<?php echo $users_list_array["user_id"]; ?>');" id="checked_users_<?php echo $users_list_array["user_id"]; ?>" name="checked_users_<?php echo $users_list_array["user_id"]; ?>"/>
										<?php
										
										$for_js = $for_js."users_array[users_array.length] = \"checked_users_".$users_list_array["user_id"]."\";\n";//Добавляем элемент для JS
										$for_js = $for_js."users_id_array[users_id_array.length] = ".$users_list_array["user_id"].";\n";//Добавляем элемент для JS
									}
									?>
								</td>
								<td><?php echo $users_list_array["user_id"]; ?></td>
								<td>
									<?php
									//Получаем список групп пользователя
									$user_groups_list_query = $db_link->prepare("SELECT * FROM `users_groups_bind` WHERE `user_id` = ?;");
									$user_groups_list_query->execute( array($users_list_array["user_id"]) );
									$first = true;
									while( $user_group_record = $user_groups_list_query->fetch() )
									{
										if(!$first){echo ";<br>";}
										else {$first = false;}
										
										echo translate_str_by_id($groups_list[$user_group_record["group_id"]]);
									}
									?>
								</td>
								<td>
									
									<?php
									if( !empty($users_list_array["email"]) )
									{
										echo $users_list_array["email"];
										
										if( $users_list_array["email_confirmed"] )
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
										echo "E-mail ".translate_str_by_id(3253);
									}
									?>
									
								</td>
								<td><?php echo $users_list_array["fio"]; ?></td>
							</tr>
							<?php
						}//for($i)
						?>
						</tbody>
					</table>
				</div>
			</div>
		</div>
	</div>
	
	<div class="col-lg-4">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3668); ?>
			</div>
			<div class="panel-body">
				<textarea id="my_list_emails" style="height:457px; width:100%; overflow-y:auto;"></textarea>
				<div style="margin-top:5px;" class="col-lg-12">
					<div class="row">
					<div class="row">
					<div class="form-group">
						<label for="" class="col-lg-4 control-label">
							<?php echo translate_str_by_id(3669); ?>
						</label>
						<div class="col-lg-8">
							<select id="group_id_my_list_emails" class="form-control">
								<?php
								$groups_query = $db_link->prepare("SELECT * FROM `groups`");
								$groups_query->execute();
								while($group = $groups_query->fetch() )
								{
									?>
									<option value="<?php echo $group["id"]; ?>"><?php echo translate_str_by_id($group["value"])." (ID ".$group["id"].")"; ?></option>
									<?php
								}
								?>
							</select>
						</div>
					</div>
					</div>
					</div>
				</div>
			</div>
		</div>
	</div>
	
    
    <script>
    <?php
    echo $for_js;//Выводим массив с чекбоксами для пользователей
    ?>
    //Обработка переключения Выделить все/Снять все
    function on_check_uncheck_all_users()
    {
        var state = document.getElementById("check_uncheck_all_users").checked;
        
        for(var i=0; i<users_array.length;i++)
        {
            document.getElementById(users_array[i]).checked = state;
        }
    }//~function on_check_uncheck_all_users()
    
    
    
    //Обработка переключения одного чекбокса
    function on_one_check_changed_users(id)
    {
        //Если хотя бы одна группа снята - снимаем общий чекбокс
        for(var i=0; i<users_array.length;i++)
        {
            if(document.getElementById(users_array[i]).checked == false)
            {
                document.getElementById("check_uncheck_all_users").checked = false;
                break;
            }
        }
    }//~function on_one_check_changed_users(id)
	
	// Получить список выбранных пользователей
	function get_users_list(){
		
        var users_list = new Array();
        for(var i=0; i < users_array.length; i++)
        {
            if(document.getElementById(users_array[i]).checked == true)
            {
                users_list.push(users_id_array[i]);
            }
        }
		return users_list;
	}
	
    </script>
		</div>
	</div>

</div>






<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3670); ?>
		</div>
		<div class="panel-body">
			<label for="">Магазин: </label>
			<select id="offices" name="offices" class="form-control" style="display:inline-block; width: auto;">
			<?php
			$SQL = "SELECT * FROM `shop_offices`";
			$query = $db_link->prepare($SQL);
			$query->execute();
			while( $array = $query->fetch() )
			{
				?>
				<option value="<?php echo $array["id"]; ?>"><?php echo translate_str_by_id($array["caption"])." (ID ".$array["id"].")"; ?></option>
				<?php
			}//for($i)
			?>
			</select>
		</div>
	</div>
</div>







<div class="col-lg-12">
<div class="row">
<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3671); ?>
		</div>
		<div class="panel-body" style="height: 436px; overflow-y: auto;">
			<div class="table-responsive">
				<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
					<thead> 
						<tr> 
							<th><input checked type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
							<th>ID</th>
							<th><?php echo translate_str_by_id(2277); ?></th>
							<th><?php echo translate_str_by_id(3474); ?></th>
						</tr>
					</thead>
					<tbody>
					<?php
					
					//Массивы для JS с id элементов и с чекбоксами элементов
					$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
					$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
					
					
					$elements_query = $db_link->prepare("SELECT *, (SELECT `name` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) AS `interface_type_name` FROM `shop_storages` WHERE `interface_type` = 1 OR `interface_type` = 2;");
					$elements_query->execute();
					
					
					while( $element_record = $elements_query->fetch() )
					{
						//Для Javascript
						$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
						$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
						?>
					
					
						<tr>
							<td><input checked type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
							<td><?php echo $element_record["id"]; ?></td>
							<td><?php echo $element_record["name"]; ?></td>
							<td><?php echo $element_record["interface_type_name"]; ?></td>
						</tr>
					<?php
					}//for
					?>
					</tbody>
				</table>
			</div>
			
		</div>
	</div>

</div>

</div>
</div>





<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
?>


<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3672); ?>
		</div>
		<div class="panel-body">
			
			<div>
				<div style="padding:0 0 10px 0;">
				<button onclick="catalogue_tree.checkAll();" class="btn w-xs btn-success"><?php echo translate_str_by_id(2293); ?></button>
				<button onclick="catalogue_tree.uncheckAll();" class="btn w-xs btn-primary2"><?php echo translate_str_by_id(2294); ?></button>
				</div>
			</div>
			<div id="container_A" style="height:350px;"></div>
			
			<div style="padding:15px 0px;" class="hidden">
				<label for=""><?php echo translate_str_by_id(2750); ?>: </label>
				<select id="storages" name="storages" class="form-control" style="display:inline-block; width: auto;">
				<?php
					$storages_query = $db_link->prepare("SELECT * FROM `shop_storages`");
					$storages_query->execute();
					while( $storages = $storages_query->fetch() )
					{
						if((int)$storages['interface_type'] === 1){
							$arr_users = json_decode($storages['users']);
							foreach($arr_users as $id_user)
							{
								if((int)$id_user === (int)$admin_id)
								{
									?>
									<option value="<?php echo $storages["id"]; ?>"><?php echo $storages["name"]." (ID ".$storages["id"].")"; ?></option>
									<?php
								}
							}
						}
					}
				?>
				</select> 
			</div>
		</div>
	</div>
</div>








<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2755); ?>
		</div>
		<div class="panel-body">
			
			<div>
				<div style="padding:0 0 10px 0;">
				<button onclick="create_prices();" class="btn w-xs btn-success"><?php echo translate_str_by_id(3673); ?></button>
				<button id="send_prices_btn" onclick="send_prices();" disabled class="btn w-xs btn-primary2"><?php echo translate_str_by_id(3674); ?></button>
				</div>
				<div id="create_prices_status"></div>
			</div>
			
		</div>
	</div>
</div>



<script>

// Функция рассылает сформированные прайсы
function send_prices(){
	
	if(!document.getElementById('send_prices_btn').hasAttribute('disabled')){
		document.getElementById('send_prices_btn').setAttribute('disabled', 'disabled');
	}
	
	// Получаем список выбранных пользователей
	var users_list = get_users_list();
	//console.log(users_list);
	
	// Получаем список email перечисленных вручную
	var group_id_my_list_emails = 0;
	var emails_list = document.getElementById('my_list_emails').value;
	//console.log(emails_list);
	if(emails_list != ''){
		// Получаем группу наценок
		var n = document.getElementById("group_id_my_list_emails").options.selectedIndex;
		group_id_my_list_emails = document.getElementById("group_id_my_list_emails").options[n].value;
		//console.log(group_id_my_list_emails);
	}
	
	if(users_list.length == 0 && emails_list == ''){
		alert('<?php echo translate_str_by_id(3675); ?>');
		return;
	}
	
	//--------------------------------------------------
	
	// Отображаем индикатор загрузки
	document.getElementById('create_prices_status').innerHTML = '<div class="panel-body text-left"><img src="/content/files/images/ajax-loader-transparent.gif"/></div>';
	
	
	//Объект для запроса
	var request_object = new Object;
	request_object.action = 'send_prices';
	request_object.users_list = users_list;
	request_object.emails_list = emails_list;
	request_object.group_id_my_list_emails = group_id_my_list_emails;

	// Отправляем запрос
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",//Тип возвращаемого значения
		data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer.status == true)
			{
			   document.getElementById('create_prices_status').innerHTML = '<?php echo translate_str_by_id(3676); ?>';
			}
			else
			{
				alert("<?php echo translate_str_by_id(3677); ?>");
				document.getElementById('create_prices_status').innerHTML = '';
			}
		}
	});
}
	
	
	
// Функция формирует файлы прайс листов по выбранным критериям
var check_office_storages_map = false;//Флаг - означает, что пройдена проверка подключения выбранных складов к выбранному магазину (все выбранные склады подключены к магазину)
function create_prices(){
	
	if(!document.getElementById('send_prices_btn').hasAttribute('disabled')){
		document.getElementById('send_prices_btn').setAttribute('disabled', 'disabled');
	}
	
	// Получаем список выбранных пользователей
	var users_list = get_users_list();
	//console.log(users_list);
	
	// Получаем список email перечисленных вручную
	var group_id_my_list_emails = 0;
	var emails_list = document.getElementById('my_list_emails').value;
	//console.log(emails_list);
	if(emails_list != ''){
		// Получаем группу наценок
		var n = document.getElementById("group_id_my_list_emails").options.selectedIndex;
		group_id_my_list_emails = document.getElementById("group_id_my_list_emails").options[n].value;
		//console.log(group_id_my_list_emails);
	}
	
	if(users_list.length == 0 && emails_list == ''){
		alert('<?php echo translate_str_by_id(3675); ?>');
		return;
	}
	
	//--------------------------------------------------
	
	// Получаем список выбранных категорий товаров
	var storages = 0;
	var arr_category = catalogue_tree.getChecked();
	//console.log(arr_category);
	if(arr_category.length > 0){
		// Получаем склад
		var n = document.getElementById("storages").options.selectedIndex;
		storages = document.getElementById("storages").options[n].value;
		//console.log(storages);
	}
	
	// Получаем список выбранных складов
	var arr_storages = getCheckedElements();
	
	if(arr_storages.length == 0)
	{
		alert('<?php echo translate_str_by_id(3678); ?>');
		return false;
	}
	
	//--------------------------------------------------
	
	// Получаем магазин
	var offices = 0;
	var n = document.getElementById("offices").options.selectedIndex;
	offices = document.getElementById("offices").options[n].value;
	//console.log(offices);
	
	//--------------------------------------------------
	
	//Объект для запроса
	var request_object = new Object;
	request_object.action = 'check_office_storages_map';
	request_object.offices = offices;
	request_object.arr_storages = arr_storages;
	request_object.arr_category = arr_category;
	request_object.users_list = users_list;
	request_object.emails_list = emails_list;
	request_object.group_id_my_list_emails = group_id_my_list_emails;
	
	//--------------------------------------------------
	
	
	//Проверяем подключение каждого выбранного склада к магазину
	check_office_storages_map = false;
	jQuery.ajax({
		type: "POST",
		async: false,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",//Тип возвращаемого значения
		data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			console.log(answer);
			
			if(answer.status != true)
			{
				check_office_storages_map = false;
				
				alert("<?php echo translate_str_by_id(3679); ?>: " + answer.message + ". <?php echo translate_str_by_id(3680); ?>.");
			}
			else
			{
				check_office_storages_map = true;
			}
		}
	});
	if( check_office_storages_map == false )
	{
		return;
	}
	else
	{
		request_object.action = 'create_prices';
	}
	
	// -----------------------------------------------------------
	// Отправляем запрос на формирование прайсов. 
	// Отображаем индикатор загрузки
	document.getElementById('create_prices_status').innerHTML = '<div class="panel-body text-left"><img src="/content/files/images/ajax-loader-transparent.gif"/></div>';
	// Отправляем запрос
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_send/ajax_operations.php",
		dataType: "json",//Тип возвращаемого значения
		data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			console.log(answer);
			
			if(answer.status == true)
			{
			   document.getElementById('create_prices_status').innerHTML = '<?php echo translate_str_by_id(3681); ?>.';
			   document.getElementById('send_prices_btn').removeAttribute('disabled');
			}
			else
			{
				alert("<?php echo translate_str_by_id(3682); ?>");
				document.getElementById('create_prices_status').innerHTML = '';
			}
		}
	});
}













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

// Обработка кнопки обновления цен
function update_price(){
	
	var sel = document.getElementById("storages");
	var val = sel.options[sel.selectedIndex].value;
	
	if(document.getElementById("delete_price_all").checked){
		var delete_price_all = 1;
	}else{
		var delete_price_all = 0;
	}
	
	
	if( (val*1) == 0 ){
		alert('Выберите склад');
		return false;
	}
	
	
	var arr = getCheckedElements();
	if(arr.length == 0){
		alert('<?php echo translate_str_by_id(3683); ?>');
		return false;
	}
	
	var arr_category = catalogue_tree.getChecked();
	if(arr_category.length == 0){
		alert('<?php echo translate_str_by_id(3222); ?>');
		return false;
	}
	
	document.getElementById("array_id_prices").value = JSON.stringify(arr);
	document.getElementById("array_id_category").value = JSON.stringify(arr_category);
	document.getElementById("storage_id").value = val;
	document.getElementById("inp_delete_price_all").value = delete_price_all;
    document.forms["update_price_form"].submit();
	
	return false;
}



/*ДЕРЕВО КАТАЛОГА ТОВАРОВ*/
//Для редактируемости дерева
webix.protoUI({
    name:"edittree"
}, webix.EditAbility, webix.ui.tree);
//Формирование дерева
catalogue_tree = new webix.ui({
    editable:false,//не редактируемое
    container:"container_A",//id блока div для дерева
    view:"tree",
	select:false,//можно выделять элементы
	drag:false,//можно переносить
	//Шаблон элемента дерева
	template:function(obj, common)//Шаблон узла дерева
    	{
            var folder = common.folder(obj, common);
    	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
    	    var checkbox = common.checkbox(obj, common);
            return common.icon(obj, common) + checkbox + folder + value_text;
    	},//~template
});
webix.event(window, "resize", function(){ catalogue_tree.adjust(); });

var saved_catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
catalogue_tree.parse(saved_catalogue);
catalogue_tree.openAll();
/*~ДЕРЕВО*/
</script>