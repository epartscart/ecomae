<?php
/**
 * Страница управления одним магазином (создание/редактирование)
*/
defined('_ASTEXE_') or die('No access');


//Массив с именами параметров, значения которых являются ID строк из мультиязычности
$translated_items = array('caption', 'country', 'region', 'city', 'address', 'description', 'timetable');
?>


<?php
if(!empty($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Обработка мультиязычности кастомным алгоритмом
	for( $i = 0 ; $i < count($translated_items) ; $i++ )
	{
		$str_id = $_POST[ $translated_items[$i]."_lang_str_id" ];//ID строки из hidden-поля
		$value = htmlentities($_POST[ $translated_items[$i] ]);//Перевод на текущий язык ПУ
		
		
		//Вызов функции сохранения строки в виде перевода на текущий язык панели управления. В ответ вернется ID этой строки, который нужно будет сохранить в таблице магазинов. Запись идет в именованную переменную, которые ниже закомменчены
		${$translated_items[$i]} = save_custom_translation($str_id, $value);
	}
	
	
    $id = $_POST["office_id"];
    //$caption = htmlentities($_POST["caption"]);
    //$country = htmlentities($_POST["country"]);
    //$region = htmlentities($_POST["region"]);
    //$city = htmlentities($_POST["city"]);
    //$address = htmlentities($_POST["address"]);
    $phone = htmlentities($_POST["phone"]);
    $email = htmlentities($_POST["email"]);
    $coordinates = htmlentities($_POST["coordinates"]);
    //$description = htmlentities($_POST["description"]);
    $users = $_POST["users"];
    //$timetable = htmlentities($_POST["timetable"]);
    
    
    if($_POST["save_action"] == "create")
    {
        if( $db_link->prepare("INSERT INTO `shop_offices` (`caption`, `country`, `region`, `city`, `address`, `phone`, `email`, `coordinates`, `description`, `users`, `timetable`) VALUES (?,?,?,?,?,?,?,?,?,?,?);")->execute( array($caption, $country, $region, $city, $address, $phone, $email, $coordinates, $description, $users, $timetable) ) != true)
        {
            $error_message = translate_str_by_id(3363);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office?error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        
        //Получаем id созданного магазина
        $id = $db_link->lastInsertId();
        
        
        $success_message = translate_str_by_id(3364);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office?office_id=<?php echo $id; ?>&success_message=<?php echo $success_message; ?>";
        </script>
        <?php
        exit;
    }//~if($_POST["save_action"] == "create")
    else if($_POST["save_action"] == "edit")
    {
        if( $db_link->prepare("UPDATE `shop_offices` SET `caption` = ?, `country` = ?, `region` = ?, `city` = ?, `address` = ?, `phone` = ?, `email` = ?, `coordinates` = ?, `description` = ?, `users` = ?, `timetable` = ? WHERE `id` = ?;")->execute( array($caption, $country, $region, $city, $address, $phone, $email, $coordinates, $description, $users, $timetable, $id) ) != true)
        {
            $error_message = translate_str_by_id(3365);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office?office_id=<?php echo $id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
            $success_message = translate_str_by_id(3366);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/logistics/offices/office?office_id=<?php echo $id; ?>&success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit;
        }
    }//~else if($_POST["save_action"] == "edit")
}//~if(!empty($_POST["save_action"]))
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	
    //Исходные данные:
    $page_title = translate_str_by_id(3367);
    $action_type = "create";//Тип действия при сохранении
    $id = 0;//ID магазина
    //$caption = "";
    //$country = "";
    //$region = "";
    //$city = "";
    //$address = "";
    $phone = "";
    $email = "";
    $coordinates = "";
    //$description = "";
    $users = array();
    //$timetable = "";
	
	//Для мультиязычности - массив с id строк для hidden-полей ( 0 - для нового магазина )
	$lang_strs_hidden = array();
	for( $i = 0 ; $i < count($translated_items) ; $i++ )
	{
		$lang_strs_hidden[ $translated_items[$i]."_lang_str_id" ] = 0;
		
		${$translated_items[$i]} = "";
	}
	
    if(!empty($_GET["office_id"]))
    {
        $page_title = translate_str_by_id(3368);
        $id = $_GET["office_id"];
        $action_type = "edit";
		
		$office_query = $db_link->prepare("SELECT * FROM `shop_offices` WHERE `id` = ?;");
		$office_query->execute( array($id) );
        $office = $office_query->fetch();
		
		//Для мультиязычности - массив с id строк для hidden-полей (текущие значения)
		$lang_strs_hidden = array();
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			$lang_strs_hidden[ $translated_items[$i]."_lang_str_id" ] = $office[ $translated_items[$i] ];
			
			${$translated_items[$i]} = translate_str_by_id($office[ $translated_items[$i] ]);
		}
		
        //$caption = translate_str_by_id($office["caption"]);
        //$country = translate_str_by_id($office["country"]);
        //$region = translate_str_by_id($office["region"]);
        //$city = translate_str_by_id($office["city"]);
        //$address = translate_str_by_id($office["address"]);
        $phone = $office["phone"];
        $email = $office["email"];
        $coordinates = $office["coordinates"];
        //$description = translate_str_by_id($office["description"]);
        $users = json_decode($office["users"], true);
        //$timetable = translate_str_by_id($office["timetable"]);
    }
    ?>
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
    
    <form name="save_form" style="display:none" method="POST">
        <input type="hidden" name="save_action" value="<?php echo $action_type; ?>" />
        <input type="hidden" name="office_id" value="<?php echo $id; ?>" />
        
        <input type="hidden" name="caption" id="caption" value="" />
        <input type="hidden" name="country" id="country" value="" />
        <input type="hidden" name="region" id="region" value="" />
        <input type="hidden" name="city" id="city" value="" />
        <input type="hidden" name="address" id="address" value="" />
        <input type="hidden" name="phone" id="phone" value="" />
        <input type="hidden" name="email" id="email" value="" />
        <input type="hidden" name="coordinates" id="coordinates" value="" />
        <input type="hidden" name="description" id="description" value="" />
        <input type="hidden" name="users" id="users" value="" />
        <input type="hidden" name="timetable" id="timetable" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		
		<?php
		//Мультиязычность - hidden-поля с ID строк
		foreach( $lang_strs_hidden AS $key => $value )
		{
			?>
			<input type="hidden" name="<?php echo $key; ?>" value="<?php echo $value; ?>" />
			<?php
		}
		?>
		
    </form>
    
    
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/shop/logistics/offices">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/offices.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3369); ?></div>
				</a>
				
				<?php
				if($id > 0)
				{
					?>
					<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/shop/logistics/offices/office/geo_nodes?office_id=<?php echo $id; ?>">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/geo_link.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(3370); ?></div>
					</a>
					
					<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/shop/logistics/offices/office/storages_link?office_id=<?php echo $id; ?>">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/storages_link.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(3371); ?></div>
					</a>
					<?php
				}
				?>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3372); ?>
			</div>
			<div class="panel-body">
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2277); ?>*
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="caption_input" value="<?php echo $caption; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3373); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="country_input" value="<?php echo $country; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3374); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="region_input" value="<?php echo $region; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3375); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="city_input" value="<?php echo $city; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3376); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="address_input" value="<?php echo $address; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(1312); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="phone_input" value="<?php echo $phone; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						Email
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="email_input" value="<?php echo $email; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3377); ?>
					</label>
					<div class="col-lg-6">
						<input class="form-control" type="text" id="coordinates_input" value="<?php echo $coordinates; ?>" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2073); ?>
					</label>
					<div class="col-lg-6">
						<textarea class="form-control" id="description_input"><?php echo $description; ?></textarea>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3378); ?>
					</label>
					<div class="col-lg-6">
						<textarea class="form-control" id="timetable_input"><?php echo $timetable; ?></textarea>
					</div>
				</div>
				
			</div>
		</div>
	</div>
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3379); ?>
			</div>
			<div class="panel-body">
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3380); ?>
					</label>
					<div class="col-lg-6">
						<?php
						//Получить список групп для бэкенда:
						require_once("content/users/helper.php");//Скрипт со вспомогательными возможностями пакета "Пользователи"
						
						$root_backend_group_query = $db_link->prepare("SELECT * FROM `groups` WHERE `for_backend` = 1;");
						$root_backend_group_query->execute();
						$root_backend_group_record = $root_backend_group_query->fetch();
						$root_backend_group = $root_backend_group_record["id"];//ID корневой группы для бэкэнда
						//Далее по инструкции для функции getInsertedGroups($group) (получение групп с единым корнем)
						$one_root_groups = array();//0
						array_push($one_root_groups, $root_backend_group);//1
						getInsertedGroups($root_backend_group);//2
						//Теперь получаем список пользователей, которые допущены в бэкенд
						$SQL_SELECT_ADMINS = "SELECT DISTINCT(`user_id`) FROM `users_groups_bind` WHERE";
						$binding_values = array();
						for($i=0; $i < count($one_root_groups); $i++)
						{
							if($i > 0) $SQL_SELECT_ADMINS .= " OR";
							$SQL_SELECT_ADMINS .= " `group_id` = ?";
							
							array_push($binding_values, $one_root_groups[$i]);
						}
						
						?>
						<select multiple="multiple" id="users_selector">
						<?php
						$user_query = $db_link->prepare($SQL_SELECT_ADMINS);
						$user_query->execute($binding_values);
						while( $user_id_record = $user_query->fetch() )
						{
							$user_id = $user_id_record["user_id"];
							
							//Запрашиваем подробные данные по пользователю: (<id>)<Фамилия> <Имя> <email phone>
							$general_user_data_query = $db_link->prepare("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ?;");
							$general_user_data_query->execute( array($user_id) );
							$general_user_data_record = $general_user_data_query->fetch();
							$email_phone = '';
							if( !empty( $general_user_data_record["email"] ) )
							{
								$email_phone = 'E-mail: '.$general_user_data_record["email"];
							}
							if( !empty( $general_user_data_record["phone"] ) )
							{
								if( !empty($email_phone) )
								{
									$email_phone = $email_phone . ', ';
								}
								$email_phone = $email_phone.'Телефон: '.$general_user_data_record["phone"];
							}
							//Запрашиваем фамилию:
							$surname_query = $db_link->prepare("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = 'surname';");
							$surname_query->execute( array($user_id) );
							$surname_record = $surname_query->fetch();
							$surname = $surname_record["data_value"];
							//Запрашиваем имя:
							$name_query = $db_link->prepare("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = 'name';");
							$name_query->execute( array($user_id) );
							$name_record = $name_query->fetch();
							$name = $name_record["data_value"];
							?>
							<option value="<?php echo $user_id; ?>"><?php echo "($user_id) $surname $name, $email_phone"; ?></option>
							<?php
						}
						?>
						</select>
						<script>
							//Делаем из селектора виджет с чекбоками
							$('#users_selector').multipleSelect({placeholder: "<?php echo translate_str_by_id(3200); ?>", width:"100%"});
							
							//Инициализируем выбранные значения
							$('#users_selector').multipleSelect('setSelects', <?php echo json_encode($users); ?>);
						</script>
					</div>
				</div>
			</div>
		</div>
	</div>

    
    
  

    
    
    <script>
    //Сохранение
    function save_action()
    {
        //Проверяем корректноть данных
        if(document.getElementById("caption_input").value == "")
        {
            alert("<?php echo translate_str_by_id(2983); ?>");
            return;
        }
        
        //Менеджеры
        var users_array = [].concat( $("#users_selector").multipleSelect('getSelects') );
        if(users_array.length == 0)
        {
            if(!confirm("<?php echo translate_str_by_id(3381); ?>"))
            {
                return;
            }
        }
        document.getElementById("users").value = JSON.stringify(users_array);
        
        //Заполняем текстовые поля
        var fields_names = new Array("caption", "country", "region", "city", "address", "phone", "email", "coordinates", "description", "timetable");
        for(var i=0; i < fields_names.length; i++)
        {
            document.getElementById(fields_names[i]).value = document.getElementById(fields_names[i]+"_input").value;
        }
        
        
        document.forms["save_form"].submit();
    }
    </script>

    <?php
}//else//Действий нет - выводим страницу
?>