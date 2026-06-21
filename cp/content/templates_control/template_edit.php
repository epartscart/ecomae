<?php
/**
 * Скрипт для работы с одним шаблоном
*/
defined('_ASTEXE_') or die('No access');


//Режим редактирования Фронтэнд/Бэкэнд
$edit_mode = null;
if( isset($_COOKIE["edit_mode"]) )
{
	$edit_mode = $_COOKIE["edit_mode"];
}
switch($edit_mode)
{
    case "frontend":
        $is_frontend = 1;
        break;
    case "backend":
        $is_frontend = 0;
        break;
    default:
        $is_frontend = 1;
        break;
}






//Обработка древовидной структуры
function tree_htmlentities($data)
{
	foreach( $data AS $key => $item )
	{
		//Проверяем ключ
		if( htmlentities($key) != $key )
		{
			unset($data[$key]);
			continue;
		}
		
		
		if( is_array($item) )
		{
			$item = tree_htmlentities($item);
		}
		else
		{
			//Если строка, то, обрабатываем
			if( !is_numeric($item) )
			{
				$item_before = '';
				
				//Очищаем от тегов. Разрешаем только перенос строки.
				do
				{
					//До strip_tags
					$item_before = $item;
					
					//Разрешаем только тег переноса строки и наш тег для подстановки языка
					$item = strip_tags($item, '<br><lang>');
				}while( $item !== $item_before );
			}
		}
		
		$data[$key] = $item;
	}
	
	return $data;
}






if(!empty($_POST["save_template_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Если админ в другой вкладке браузера переключил режим редактирования (фронтенд/бэкенд), то, запрещаем сохранять изменения.
	if( $is_frontend != $_POST["is_frontend"] )
	{
		$warning_message = urlencode(translate_str_by_id(2204));
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/?warning_message=<?php echo $warning_message; ?>";
		</script>
		<?php
		exit;
	}
	
	
    //Сохранение изменений шаблона
    
    $warning_message = "";//Для сообщения об ошибке при назначении текущего шаблона
    
    //Проверяем, было выставление шаблона текущим:
    if(!empty($_POST["current"]))
    {
        if(filter_var($_POST["current"], FILTER_VALIDATE_BOOLEAN) == true)
        {
            //1. Выставляем для всех шаблонов "Не текущий"
			$current_off_all_result = $db_link->prepare("UPDATE `templates` SET `current` = 0 WHERE `is_frontend` = ?;")->execute( array($is_frontend) );
			
            //2. Выставляем текущий
			$current_on_result = $db_link->prepare("UPDATE `templates` SET `current` = 1 WHERE `id` = ?;")->execute( array((int)$_POST["id"]) );
            
            if($current_off_all_result != true || $current_on_result != true)
            {
                $warning_message = "&warning_message=".translate_str_by_id(3846);
                if($current_off_all_result != true)
                {
                    $error_message .= "<br> ".translate_str_by_id(3847);
                }
                if($current_on_result != true)
                {
                    $error_message .= "<br> ".translate_str_by_id(3848);
                }
            }
        }
    }//if - выставляем текущим
    
	$name = htmlentities(trim(str_replace(array("\n","\r","\t","'","`",'"','#','--'), '', $_POST["name"])), ENT_QUOTES, "UTF-8", false);
	$caption = htmlentities(trim(str_replace(array("\n","\r","\t","'","`",'"','#','--'), '', $_POST["caption"])), ENT_QUOTES, "UTF-8", false);
	$data_value = json_decode($_POST["data_value"], true);


	$data_value = tree_htmlentities($data_value);


	foreach($data_value as $k => $value){
		switch($k){
			case 'navbar_style' :
				if($value == 'inverse'){
					if(!empty($data_value['navbar_color']) && !empty($data_value['main_color'])){
						$data_value['navbar_color'] = $data_value['main_color'];
					}
				}
			break;
			case 'link_style' :
				if($value == 'main'){
					if(!empty($data_value['link_color']) && !empty($data_value['main_color'])){
						$data_value['link_color'] = $data_value['main_color'];
					}
				}
			break;
		}
	}
	
	// Формирование файла стилей
	if(!empty($data_value['main_color'])){
		if(file_exists($_SERVER["DOCUMENT_ROOT"]."/templates/$name/assets/css/generate_style/generate_style.php")){
			$postdata = http_build_query(
				array(
					'key' => $DP_Config->secret_succession,
					'dir' => $name,
					'data_value' => json_encode($data_value)
				)
			);
			$curl = curl_init();
			curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."templates/$name/assets/css/generate_style/generate_style.php");
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, false);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			curl_exec($curl);
			curl_close($curl);
		}
	}
	
	$data_value = json_encode($data_value);
	
    //Далее сохраняем данные шаблона	
    if( $db_link->prepare("UPDATE `templates` SET `caption` = ?, `data_value` = ? WHERE `id` = ?;")->execute( array($caption, $data_value, (int)$_POST["id"]) ) != true)
    {
        //Возникли ошибки
        $error_message = translate_str_by_id(3849);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/template?template_id=<?php echo (int)$_POST["id"]; ?>&error_message=<?php echo $error_message.$warning_message; ?>";
        </script>
        <?php
        exit();
    }
    else
    {
        //Выполнено без ошибок
        $success_message = translate_str_by_id(3850);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/templates/template?template_id=<?php echo (int)$_POST["id"]; ?>&success_message=<?php echo $success_message.$warning_message; ?>";
        </script>
        <?php
        exit();
    }
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" onClick="save_template();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/templates/templates_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/pallete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3851); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
<?php
    //Получаем текущие данные шаблона
	$template_query = $db_link->prepare("SELECT * FROM `templates` WHERE `id` = ?;");
	$template_query->execute( array($_GET["template_id"]) );
    $template_record = $template_query->fetch();

    //Данные шаблона:
    $id = $template_record["id"];//ID шаблона
    $name = $template_record["name"];//name шаблона
    $caption = $template_record["caption"];//Заголовок шаблона
    $current = $template_record["current"];//Текущий
    $data_structure = $template_record["data_structure"];//Структура спецнастроек
    $data_value = $template_record["data_value"];//Значения спецнастроек
?>
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3852); ?>
			</div>
			<div class="panel-body">
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3853); ?>
					</label>
					<div class="col-lg-6">
						<input type="text" name="caption_input" id="caption_input" value="<?php echo $caption; ?>" class="form-control" />
					</div>
				</div>
				<?php
				if($current == false)
				{
					?>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3854); ?>
						</label>
						<div class="col-lg-6">
							<input type="checkbox" name="current_checkbox" id="current_checkbox" />
						</div>
					</div>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	
	
    
	
	<?php
	if( ! ($data_structure == "" || $data_structure == "[]") )
	{
		?>
		<div class="col-lg-6">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3855); ?>
				</div>
				<div class="panel-body">
					<?php
					require_once("content/control/get_widget.php");//Скрипт для получения html-кода виджетов различных типов
					
					$data_value = json_decode($data_value, true);//Текущие значения спецнастроек

					$data_structure = json_decode($data_structure, true);//Структура спецнастроек
					for($i=0; $i < count($data_structure); $i++)
					{
						
						$class = '';
						if($data_structure[$i]["type"] == 'hidden'){
							$class = 'hidden ';
						}
						
						if( $i > 0 )
						{
							?>
							<div class="<?=$class;?>hr-line-dashed col-lg-12"></div>
							<?php
						}
						
						
						$options = array();//Переменная для списка возможных опций
						//Проверяем существование поля "Способ получения возможных значений"
						if(!empty($data_structure[$i]["options_way"]))
						{
							//Получаем список опций указанным способом
							switch($data_structure[$i]["options_way"])
							{
								case "direct":
									$options = $data_structure[$i]["options"];
									break;
								case "sql":
									$SQL_SELECT_OPTIONS = $data_structure[$i]["options"];
									
									$SQL_SELECT_OPTIONS = str_replace(array("<is_frontend>"), $is_frontend, $SQL_SELECT_OPTIONS);//Подставляем режим работы
									
									$options_query = $db_link->prepare($SQL_SELECT_OPTIONS);
									$options_query->execute();
									while( $options_record = $options_query->fetch() )
									{
										array_push($options, array("caption"=>$options_record["caption"], "value"=>$options_record["value"]));
									}
									break;
							};
						}//if() - если предполагается получение возможных опций настройки
						
						$value = $data_value[$data_structure[$i]["name"]];//Текущее значение спецнастройки
						
						if($data_structure[$i]["name"] == 'version'){
							$value = (((int) $value) + 1);
						}
						
						$widget = get_widget($data_structure[$i]["type"], $data_structure[$i]["name"], $value, $options);
						
						?>
						<div class="<?=$class;?>form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id($data_structure[$i]["caption"]); ?>
							</label>
							<div class="col-lg-6">
								<?php echo $widget; ?>
							</div>
						</div>
						<?php
					}//~for($i)
					?>
				</div>
			</div>
		</div>
		<?php
	}
	?>
	

    
    <!-- ********************************************************************** -->
    <!-- START - БЛОК ФОРМЫ СОХРАНЕНИЯ -->
    <script>
    <?php
    if($data_structure == "[]")
    {
        ?>
        var data_structure = "";
        <?php
    }
    else
    {
        ?>
        var data_structure = <?php echo json_encode($data_structure); ?>;//Структура спецнастроек
        <?php
    }
    ?>
    // ----------------------------------------------------------------
    //Сохранение шаблона
    function save_template()
    {
        //1. Заголовок
        if(document.getElementById("caption_input").value == "")
        {
            webix.message({type:"error", text:"<?php echo translate_str_by_id(3856); ?>"});
            return;
        }
        document.getElementById("caption").value = document.getElementById("caption_input").value;
        
        
        <?php
        //Эту настройку предоставляем только если этот шаблон не главный
        if($current == false)
        {
        ?>
            //2. Назначить текущим
            document.getElementById("current").value = document.getElementById("current_checkbox").checked;
        <?php
        }
        ?>
        
        
        //3. Значения специальных настроек
        var data_value = new Object;
        if(data_structure.length > 0)
        {
            //3.1 По списку специальных настроек
            for(var i=0; i < data_structure.length; i++)
            {
                //3.2 Для кажой настройки получить значение из виджета
                //3.3 Записать это значение в общий объект спецнастроек
                if(document.getElementById(data_structure[i]["name"]).type == 'checkbox'){
					if(document.getElementById(data_structure[i]["name"]).checked){
						data_value[data_structure[i]["name"]] = 1;
					}else{
						data_value[data_structure[i]["name"]] = 0;
					}
				}else{
					data_value[data_structure[i]["name"]] = document.getElementById(data_structure[i]["name"]).value;
				}
            }
        }
        
        //3.4 Перевести объект в JSON-формат и записать его в поле формы
        document.getElementById("data_value").value = JSON.stringify(data_value);
       	
        //4. Отправляем форму
    	document.forms["save_template_form"].submit();//Отправляем
    }
    // ----------------------------------------------------------------
    </script>
    <form name="save_template_form" style="display:none" method="POST">
        <input type="hidden" name="save_template_action" id="save_template_action" value="save_template_action" /> <!-- Говорит скрипту, что идет сохранение шаблона -->
        <input type="hidden" name="id" id="id" value="<?php echo $id; ?>" /> <!-- ID шаблона -->
        <input type="hidden" name="name" id="name" value="<?php echo $name; ?>" /> <!-- name шаблона -->
        <!-- Ok --><input type="hidden" name="caption" id="caption" value="" /> <!-- Заголовок -->
        
         <?php
        //Эту настройку предоставляем только если этот шаблон не главный
        if($current == false)
        {
        ?>
            <!-- Ok --><input type="hidden" name="current" id="current" value="" /> <!-- Текущий -->
        <?php
        }
        ?>
        
        <!-- Ok --><input type="hidden" name="data_value" id="data_value" value="" /> <!-- Значения специальных настроек -->
		
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none"/>
		
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!-- END - БЛОК ФОРМЫ СОХРАНЕНИЯ -->
    <!-- ********************************************************************** -->
    <?php
}
?>