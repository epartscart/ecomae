<?php
/**
 * Скрипт для редактирования одного модуля
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
			$item = htmlentities($item, ENT_QUOTES, "UTF-8", false);
		}
		
		$data[$key] = $item;
	}
	
	return $data;
}
?>


<?php
if(!empty($_POST["module_save_action"]))
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
	
	
	
    //Получаем css_js от прототипа модуля
    $css_js = "";
	
	$css_js_query = $db_link->prepare( "SELECT `css_js` FROM `modules` WHERE `id` = ?;" );
	$css_js_query->execute( array($_POST["prototype_id"]) );
    $css_js = $css_js_query->fetch();
    $css_js = $css_js["css_js"];
    
    //Создание
    if($_POST["module_save_action"] == "create")
    {
		if( $_POST["content_type"] != 'php' && $_POST["content_type"] != 'text' )
		{
			exit;
		}
		
		//Если тип содержимого - php-скрипт
		if( strtolower($_POST["content_type"]) == 'php' )
		{
			//Получаем расширение файла
			$file_extension = explode(".", $_POST["content"]);
			$file_extension = $file_extension[count($file_extension)-1];
			
			//Проверка расширения
			$file_extension = strtolower($file_extension);
			if( $file_extension != 'php' )
			{
				exit;
			}
		}
		
		//Не допускаем вставку php-кода при текстовом типе контента
		if( strtolower($_POST["content_type"]) == 'text' )
		{
			do
			{
				$_POST["content"] = str_replace('<?', '[CODE]', $_POST["content"]);
			}while( strpos($_POST["content"], '<?') !== false );
			
			
			do
			{
				$_POST["content"] = str_replace('?>', '[/CODE]', $_POST["content"]);
			}while( strpos($_POST["content"], '?>') !== false );
			
			
			//Мультиязычность. Кастомный алгоритм
			$_POST["content"] = save_custom_translation($_POST["content_lang_str_id"], $_POST["content"]);
		}
		
		
        //1. Создание записи в таблице modules
        $SQL_INSERT = "INSERT INTO `modules` (`is_frontend`, `is_prototype`, `prototype_id`, `prototype_name`, `caption`, `content_type`, `content`, `position`, `activated`, `data`, `show_caption`, `order`, `css_js`, `for_all`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);";
        
		
		
		//$_POST["prototype_name"] = htmlentities($_POST["prototype_name"], ENT_QUOTES, "UTF-8", false);
		$_POST["prototype_name"] = $_POST["prototype_name_lang_str_id"];
		$_POST["caption"] = htmlentities($_POST["caption"], ENT_QUOTES, "UTF-8", false);
		
		//Мультиязычность. Кастомный алгоритм
		$_POST["caption"] = save_custom_translation($_POST["caption_lang_str_id"], $_POST["caption"]);
		
		
		$_POST["position"] = htmlentities($_POST["position"], ENT_QUOTES, "UTF-8", false);
		
		$_POST["data_value"] = json_decode($_POST["data_value"], true);
		$_POST["data_value"] = tree_htmlentities($_POST["data_value"]);
		$_POST["data_value"] = json_encode($_POST["data_value"]);
		
		
        if( $db_link->prepare($SQL_INSERT)->execute( array($is_frontend, 0, (int)$_POST["prototype_id"], $_POST["prototype_name"], $_POST["caption"], $_POST["content_type"], $_POST["content"], $_POST["position"], (int)$_POST["activated"], $_POST["data_value"], (int)$_POST["show_caption"], (int)$_POST["order"], $css_js, (int)$_POST["for_all"]) ) != true)
        {
            $error_message = translate_str_by_id(2614);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/module?prototype_id=<?php echo $_POST["prototype_id"]; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit();
        }
        else//Запись модуля успешно создана - определяем его id
        {
            $module_id = $db_link->lastInsertId();
            
            $success_message = translate_str_by_id(2615);
        }
    }//if() - Создание
    else if($_POST["module_save_action"] == "edit")//Редактирование
    {
        $module_id = (int)$_POST["id"];//ID модуля, с которым работаем
        
		
		
		if( $_POST["content_type"] != 'php' && $_POST["content_type"] != 'text' )
		{
			exit;
		}
		
		//Если тип содержимого - php-скрипт
		if( strtolower($_POST["content_type"]) == 'php' )
		{
			//Получаем расширение файла
			$file_extension = explode(".", $_POST["content"]);
			$file_extension = $file_extension[count($file_extension)-1];
			
			//Проверка расширения
			$file_extension = strtolower($file_extension);
			if( $file_extension != 'php' )
			{
				exit;
			}
		}
		
		//Не допускаем вставку php-кода при текстовом типе контента
		if( strtolower($_POST["content_type"]) == 'text' )
		{
			do
			{
				$_POST["content"] = str_replace('<?', '[CODE]', $_POST["content"]);
			}while( strpos($_POST["content"], '<?') !== false );
			
			
			do
			{
				$_POST["content"] = str_replace('?>', '[/CODE]', $_POST["content"]);
			}while( strpos($_POST["content"], '?>') !== false );
			
			
			//Мультиязычность. Кастомный алгоритм
			$_POST["content"] = save_custom_translation($_POST["content_lang_str_id"], $_POST["content"]);
		}
		
		
		
		
        $SQL_UPDATE = "UPDATE `modules` SET `caption` = ?, `content` = ?, `position` = ?, `activated` = ?, `data` = ?, `show_caption` = ?, `order` = ?, `css_js` = ?, `for_all` = ? WHERE `id` = ?;";
		
		
		$_POST["caption"] = htmlentities($_POST["caption"], ENT_QUOTES, "UTF-8", false);
		
		//Мультиязычность. Кастомный алгоритм
		$_POST["caption"] = save_custom_translation($_POST["caption_lang_str_id"], $_POST["caption"]);
		
		$_POST["position"] = htmlentities($_POST["position"], ENT_QUOTES, "UTF-8", false);
		
		$_POST["data_value"] = json_decode($_POST["data_value"], true);
		$_POST["data_value"] = tree_htmlentities($_POST["data_value"]);
		$_POST["data_value"] = json_encode($_POST["data_value"]);
		
		
		//Предотвращаем изменение кода для модулей свойств товаров
		if($_POST["id"] == 33){
			$_POST["content"] = '<div style="margin-left:20px; margin-top:10px;"><div id="side_properties_widgets_div" class="side_properties_widgets_div" style="display: none;"></div></div>';
		}
		if($_POST["id"] == 34){
			$_POST["content"] = '<div id="side_properties_widgets_div" class="side_properties_widgets_div" style="display:none"></div>';
		}
		
		
        if( $db_link->prepare($SQL_UPDATE)->execute( array($_POST["caption"], $_POST["content"], $_POST["position"], (int)$_POST["activated"], $_POST["data_value"], (int)$_POST["show_caption"], (int)$_POST["order"], $css_js, (int)$_POST["for_all"], $module_id) ) != true)
        {
            $error_message = translate_str_by_id(2616);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/module?module_id=<?php echo $module_id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit();
        }
        else//Модуль успешно изменен в таблице modules
        {
            $success_message = translate_str_by_id(2617);
        }
    }//else - редактирование
    
    
    
    //Здесь идет запись привязки к страницам
    $no_error_bind_to_content = true;//Флаг - привязка модуля к страницам прошла без ошибок
    $content_array = json_decode($_POST["content_array"], true);//Массив со страницами, к которым должен быть привязан этот модуль
    //Получаем все страницы сайта
	$content_query = $db_link->prepare("SELECT * FROM `content` WHERE `is_frontend` = ?;");
    $content_query->execute( array($is_frontend) );
    while( $content_record = $content_query->fetch() )
    {
        $modules_array = json_decode($content_record["modules_array"], true);//Список модулей, уже привязанных к странице
        
        //Выясняем, должен ли модуль быть привязан к этой странице:
        if(array_search($content_record["id"], $content_array) !== false)
        {
            //Модуль должен быть привязан - определяем, был ли он привязан ранее - если да, то ничего не делаем, если нет - то привязываем
            if(array_search($module_id, $modules_array) === false)
            {
                //Этот модуль не был привязан к странице - привязываем его
                array_push($modules_array, (int)$module_id);
                if( $db_link->prepare("UPDATE `content` SET `modules_array` = ? WHERE `id` = ?;")->execute( array(json_encode($modules_array), $content_record["id"]) ) != true)
                {
                    $no_error_bind_to_content = false;
                }
            }
        }//~ if() - модуль должен быть привязан
        else//К данной странице модуль НЕ должен быть привязан - удаляем его из списка страницы, если он в нем был
        {
            if(array_search($module_id, $modules_array) !== false)
            {
                $modules_array_new = array();//Новый массив с модулями страницы в который не попадет данный модуль
                for($j=0; $j<count($modules_array); $j++)
                {
                    if($modules_array[$j] != $module_id)
                    {
                        array_push($modules_array_new, (integer)$modules_array[$j]);
                    }
                }
				

                if( $db_link->prepare("UPDATE `content` SET `modules_array` = ? WHERE `id` = ?;")->execute( array(json_encode($modules_array_new), $content_record["id"]) ) != true)
                {
                    $no_error_bind_to_content = false;
                }
            }// if() - модуль был привязан к этой странице ранее
        }//else - модуль не должен быть привязан
    }//for($i) - по каждому материалу сайта
    
    
    
    //НАЗНАЧЕНИЕ ПРАВ ДОСТУПА ГРУППАМ:
    $no_error_access_delete_old = true;//Флаг - нет ошибки при удалении старых записей доступа
    $no_error_access_insert = true;//Флаг - нет ошибки при создании новых записей прав доступа
	if( $db_link->prepare("DELETE FROM `modules_access` WHERE `module_id` = ?;")->execute( array($module_id) ) != true)
    {
         $no_error_access_delete_old = false;
    }
    else
    {
        $groups_allowed = json_decode($_POST["groups_allowed"], true);
        for($i=0; $i < count($groups_allowed); $i++)
        {
            if( $db_link->prepare("INSERT INTO `modules_access` (`module_id`, `group_id`) VALUES (?, ?);")->execute( array($module_id, (int)$groups_allowed[$i]) ) != true)
            {
                $no_error_access_insert = false;
            }
        }
    }
    
    
    $warning_message = "";
    if(!$no_error_bind_to_content)
    {
        $warning_message = "&warning_message=".translate_str_by_id(2618);
    }
    if(!$no_error_access_delete_old)
    {
        if($warning_message == "")
        {
            $warning_message = "&warning_message=".translate_str_by_id(2619);
        }
        else
        {
            $warning_message = ". ".translate_str_by_id(2620);
        }
    }
    else if(!$no_error_access_insert)
    {
        if($warning_message == "")
        {
            $warning_message = "&warning_message=".translate_str_by_id(2621);
        }
        else
        {
            $warning_message = ". ".translate_str_by_id(2622);
        }
    }
    ?>
    <script>
        location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/modules/module?module_id=<?php echo $module_id; ?>&success_message=<?php echo $success_message; ?><?php echo $warning_message; ?>";
    </script>
    <?php
    exit();
}// - if - есть действие
else//Вывод страницы
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	
    //Для получения списка существующих материалов
    require_once("content/content/dp_content_record.php");
    require_once("content/content/get_content_records.php");
    
    
    //Для дерева групп
    require_once("content/users/dp_group_record.php");//Определение класса записи группы
    require_once("content/users/get_group_records.php");//Получение объекта иерархии существующих групп для вывода в дерево-webix
    
    
    //Исходные отображаемые данные
    $id = 0;//ID модуля
    $prototype_id = 0;//ID прототипа
    $prototype_name = "";//Название прототипа
    $prototype_name_lang_str_id = 0;//Название прототипа Мультиязычность
    $content_type = "";//Тип содержимого модуля
    $content = "";//Содержимое
    $content_lang_str_id = 0;//Содержимое (Мультиязычность). Для типа text
    $caption = "";//Заголовок
    $caption_lang_str_id = 0;//Заголовок (Мультиязычность)
    $position = "";//Позиция
    $activated = "";//Включен (либо " checked")
    $show_caption = " checked";//Показывать заголовок
    $order = 0;//Порядок вывода
    $data_structure = "";//Структура специальных настроек - берется из записи прототипа
    $data_value = "[]";//Значения специальных настроек - берется из записи модуля
    $groups_allowed = array();//Допущенные группы
    $for_all = "";//Флаг "Для всех"
    
    $page_mode = "";//Режим страницы
    if(!empty($_GET["prototype_id"]))
    {
        //Выводим страницу создания модуля по прототипу
        $page_mode = "create";
        
        //Получаем текущие данные прототипа модуля
		$prototype_query = $db_link->prepare("SELECT * FROM `modules` WHERE `id` = ?;");
		$prototype_query->execute( array($_GET["prototype_id"]) );
        $prototype_record = $prototype_query->fetch();
        
        $prototype_name_lang_str_id = $prototype_record["prototype_name"];//Название прототипа Мультиязычность
        $prototype_name = translate_str_by_id($prototype_record["prototype_name"]);//Название прототипа
        $prototype_id = $_GET["prototype_id"];//ID прототипа
        $content_type = $prototype_record["content_type"];//Тип содержимого модуля
        $content = $prototype_record["content"];//Содержимое
        
		//Мультиязычность. Содержимое при типе text
		if( $content_type == 'text' )
		{
			$content_lang_str_id = $content;
			$content = translate_str_by_id($content);
		}
		
		
        $data_structure = $prototype_record["data"];//Структура специальных настроек - берется из записи прототипа
    }
    else if(!empty($_GET["module_id"]))
    {
        //Выводим страницу редактирования существующего модуля
        $page_mode = "edit";
        
        //Получаем текущие данные модуля
		$current_values_query = $db_link->prepare("SELECT * FROM `modules` WHERE `id` = ?;");
		$current_values_query->execute( array($_GET["module_id"]) );
        $current_values_record = $current_values_query->fetch();
        
        $id = $current_values_record["id"];//ID модуля
        $prototype_id = $current_values_record["prototype_id"];//ID прототипа
        $prototype_name_lang_str_id = $current_values_record["prototype_name"];//Название прототипа Мультиязычность
        $prototype_name = translate_str_by_id($current_values_record["prototype_name"]);//Название прототипа
        $content_type = $current_values_record["content_type"];//Тип содержимого модуля
        $content = $current_values_record["content"];//Содержимое
        
		
		//Мультиязычность. Содержимое при типе text
		if( $content_type == 'text' )
		{
			$content_lang_str_id = $content;
			$content = translate_str_by_id($content);
		}
		
		
		$caption_lang_str_id = $current_values_record["caption"];//Заголовок (Мультиязычность)
        $caption = translate_str_by_id($current_values_record["caption"]);//Заголовок
        $position = $current_values_record["position"];//Позиция
        $activated = (boolean)$current_values_record["activated"] ? " checked=\"checked\" " : "";//Включен
        $show_caption = (boolean)$current_values_record["show_caption"] ? " checked=\"checked\" ": "";//Показывать заголовок
        $order = (integer)$current_values_record["order"];//Порядок вывода
        $data_value = $current_values_record["data"];//Значения специальных настроек - берется из записи модуля
        
        $for_all = (boolean)$current_values_record["for_all"] ? " checked=\"checked\" " : "";//Флаг "Для всех"
        
        //Получаем структуру специальных настроек из записи прототипа:
        $prototype_query = $db_link->prepare("SELECT * FROM `modules` WHERE `id` = ?;");
		$prototype_query->execute( array($prototype_id) );
        $prototype_record = $prototype_query->fetch();
        $data_structure = $prototype_record["data"];//Структура специальных настроек - берется из записи прототипа
        
        
        //Получаем настройки прав доступа к модулю
		$groups_allowed_query = $db_link->prepare("SELECT * FROM `modules_access` WHERE `module_id` = ?;");
		$groups_allowed_query->execute( array($_GET["module_id"]) );
        while( $group_allowed_record = $groups_allowed_query->fetch() )
        {
            array_push($groups_allowed, $group_allowed_record["group_id"]);
        }
    }
    
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
				<a class="panel_a" onClick="save_module();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/modules/modules_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/modules.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2623); ?></div>
				</a>
				
			 
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
				<?php echo translate_str_by_id(2624); ?>
			</div>
			<div class="panel-body">
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2587); ?>
					</label>
					<div class="col-lg-9">
						<input type="text" name="caption_input" id="caption_input" value="<?php echo $caption; ?>" class="form-control" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2625); ?>
					</label>
					<div class="col-lg-9">
						<input type="checkbox" name="show_caption_checkbox" id="show_caption_checkbox" <?php echo $show_caption; ?>/>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2626); ?>
					</label>
					<div class="col-lg-9">
						<input type="checkbox" name="activated_checkbox" id="activated_checkbox" <?php echo $activated; ?>/>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2627); ?>
					</label>
					<div class="col-lg-9">
						<div class="col-lg-6">
							<input class="form-control" type="text" name="position_input" id="position_input" value="<?php echo $position; ?>"/>
						</div>
						<div class="col-lg-6">
							<button onclick="openPositionWindow();" class="btn btn-success " type="button"><i class="fa fa-upload"></i> <span class="bold"><?php echo translate_str_by_id(2098); ?></span></button>
						</div>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2628); ?>
					</label>
					<div class="col-lg-9">
						<input type="text" name="order_input" id="order_input" value="<?php echo $order; ?>" class="form-control" />
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2307); ?>
			</div>
			<div class="panel-body">
				
				<?php
				if($page_mode == "edit")
				{
					?>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2629); ?>
						</label>
						<div class="col-lg-6">
							<?php echo $id;?>
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					<?php
				}
				?>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2630); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $prototype_name;?>
					</div>
				</div>
				
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2118); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $content_type;?>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	

    
    


    
    
    
    
    <?php
    //Если тип содержимого - текст - предоставляем возможность редактирования
    if($content_type == "text")
    {
        ?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2631); ?>
				</div>
				<div class="panel-body">
					<div style="padding-right:10px; padding-bottom:5px;">
						<textarea class="tinymce_editor" id="tinymce_editor">
						</textarea>
					</div>
					
					<script>
						tinymce.init({
							selector: "textarea.tinymce_editor",
							plugins: [
								"advlist autolink lists link image charmap print preview anchor",
								"searchreplace visualblocks code fullscreen",
								"insertdatetime media table contextmenu paste textcolor"
							],
							toolbar: [ 
									"newdocument | bold italic underline strikethrough | alignleft aligncenter alignright alignjustify | styleselect | formatselect | fontselect | fontsizeselect | ", 
									"cut copy paste | bullist numlist | outdent indent | blockquote | undo redo | removeformat subscript superscript | link image | forecolor backcolor"
							]
						});
						
						
						document.getElementById("tinymce_editor").value = '<?php echo addcslashes(str_replace(array("\n","\r"), '', $content), "'"); ?>';//Исходное содержимое
					</script>
				</div>
			</div>
		</div>
        <?php
    }
    ?>
    
    
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2632); ?>
			</div>
			<div class="panel-body">
				<div id="content_div" style="height:300px;">
				</div>
			</div>
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-7 control-label">
								<?php echo translate_str_by_id(2633); ?>
							</label>
							<div class="col-lg-5">
								<input type="checkbox" name="for_all_checkbox" id="for_all_checkbox" <?php echo $for_all;?>/>
							</div>
						</div>
					</div>
					<div class="col-lg-4">
						<button onclick="content_tree.checkAll();" class="btn btn-success " type="button"><i class="fa fa-check-square"></i> <span class="bold"><?php echo translate_str_by_id(2293); ?></span></button>
					</div>
					<div class="col-lg-4">
						<button onclick="content_tree.uncheckAll();" class="btn btn-success " type="button"><i class="fa fa-square-o"></i> <span class="bold"><?php echo translate_str_by_id(2294); ?></span></button>
					</div>
				</div>
            </div>
		</div>
	</div>
	
	
	
	
    
    
    <!-- Start Блок - привязка к страницам -->
    <script>
    //Создаем дерево
    content_tree = new webix.ui({
        
        //Шаблон элемента дерева
    	template:function(obj, common)//Шаблон узла дерева
        	{
                var folder = common.folder(obj, common);
        	    var icon = "";
        	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
        	    var checkbox = common.checkbox(obj, common);
        	    
        	    //Индикация системного материала
        	    var icon_system = "";
        	    if(obj.system_flag == true)
                {
                    icon_system = "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/gear.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
                }
        	    
        	    //Индикация материала, снятого с публикации
        	    if(obj.published_flag == false)
                {
                    icon_system += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
                    value_text = "<span style=\"color:#AAA\">" + obj.value + "</span>";//Вывод текста
                }
        	    
        	    //Индикация главного материала
        	    if(obj.main_flag == 1)
                {
                    icon_system += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/star.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
                    value_text = "<span style=\"font-weight:bold\">" + obj.value + "</span>";//Вывод текста
                }
        	    
                return common.icon(obj, common) + checkbox + icon + folder + icon_system + value_text;
        	},//~template
    
    
    
    	editable:false,//Не редактируемое
        container:"content_div",//id блока div для дерева
        view:"tree",
    	select:true,//можно выделять элементы
    	drag:false//Нельзя переносить
    });
	
	webix.event(window, "resize", function(){ content_tree.adjust(); });
	
    var site_content = <?php echo $content_tree_dump_JSON; ?>;
    content_tree.parse(site_content);
    //content_tree.openAll();
    checkCurrentContent(null);//Отмечаем текущие материалы, к которым привязан этот модуль

    
    // ----------------------------------------------------------------
    //Функция - отметить текущие материалы, к которым привязан данный модуль
    function checkCurrentContent(data)
    {
        //Если передан null, то это первый вызов функции - начинаем работать с корня дампа
        if(data == null)
        {
            data = <?php echo $content_tree_dump_JSON; ?>;
        }
        
        var module_id = <?php echo $id;?>;//ID данного модуля
        
        for(var i=0; i < data.length; i++)
        {
            //Если в данном материале есть данный модуль - отмечаем в дереве
            if(data[i].modules_array.indexOf(module_id)>=0)
            {
                content_tree.checkItem(data[i].id);
            }
            
            //Если данный элемент содержит вложенные элементы - делаем рекурсивный вызов для вложенных элементов
            if(data[i].$count > 0)
            {
                checkCurrentContent(data[i].data);//Рекурсивный вызов
            }
        }
    }
    // ----------------------------------------------------------------
    </script>
    <!-- End Блок - привязка к страницам -->
    
    
    
    
    
    
    
    <!-- ********************************************************************** -->
    <!-- START - БЛОК СПЕЦИАЛЬНЫХ НАСТРОЕК -->
	<?php
	if($data_structure != "")
	{
		require_once("content/control/get_widget.php");//Скрипт для получения html-кода виджетов различных типов
		?>
		<div class="col-lg-6">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2634); ?>
				</div>
				<div class="panel-body">
					<div class="row">
					<?php
					$data_structure = json_decode($data_structure, true);
					$data_value = json_decode($data_value, true);//Текущие спецнастройки - если идет редактирование существующего модуля
					//Цикл по всем специальным настройкам
					for($i=0; $i<count($data_structure); $i++)
					{
						if($i > 0)
						{
							?>
							<div class="hr-line-dashed col-lg-12"></div>
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
									$options = json_decode($data_structure[$i]["options"], true);
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
						$value = "";//Текущее значение спецнастройки
						if($page_mode == "edit")
						{
							$value = $data_value[$data_structure[$i]["name"]];//Текущее значение спецнастройки
						}
						
						$widget = get_widget($data_structure[$i]["type"], $data_structure[$i]["name"], $value, $options);
						?>

						<div class="form-group col-lg-12">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id($data_structure[$i]["caption"]); ?>
							</label>
							<div class="col-lg-6">
								<?php echo $widget; ?>
							</div>
						</div>
						<?php
					}
					?>
					</div>
				</div>
			</div>
		</div>
		<?php
	}
	?>
    <!-- END - БЛОК СПЕЦИАЛЬНЫХ НАСТРОЕК -->
    <!-- ********************************************************************** -->
    
    
    
    
    
    
    
    <!-- ********************************************************************** -->
    <!-- START - БЛОК НАСТРОЕК ДОСТУПА -->
    <div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2635); ?>
			</div>
			<div class="panel-body">
				<div id="container_G" style="height:250px;"></div>
			</div>
		</div>
	</div>
    <!-- END - БЛОК НАСТРОЕК ДОСТУПА -->
    <!-- ********************************************************************** -->
    <script>
    var groups_tree = "";//ПЕРЕМЕННАЯ ДЛЯ ДЕРЕВА ГРУПП
        	    
    //Инициализация дерева групп после загруки страницы
    function groups_tree_init()
    {
        /*ДЕРЕВО*/
        //Формирование дерева
        groups_tree = new webix.ui({
        
            //Шаблон элемента дерева
        	template:function(obj, common)//Шаблон узла дерева
            	{
                    var folder = common.folder(obj, common);
            	    var icon = "";
            	    //var value_text = "<span>" + obj.value + "</span>";//Вывод текста
            	    //var checkbox = common.checkbox(obj, common);//Чекбокс
                    
                    <?php
                    //Для материалов бэкэнда делаем доступными только группы бэкэнда
                    if(!$is_frontend)
                    {
                        ?>
                        var checkbox = "";
                        var value_text = "";
                        if(is_group_for_backend(obj.id) == true)//ГРУППА ДОСТУПНА
                        {
                            checkbox = common.checkbox(obj, common);//Чекбокс
                            value_text = "<span>" + obj.value + "</span>";//Вывод текста
                        }
                        else//НЕ ДОСТУПНА
                        {
                            checkbox = common.checkbox(obj, common);
                            checkbox = "<input type='checkbox' class='webix_tree_checkbox' disabled='disabled'>";
                            
                            value_text = "<span><font style=\"color:#AAA\">" + obj.value + "</font></span>";//Вывод текста
                        }
                        <?php
                    }
                    else//Для фронтэнда - все группы доступны
                    {
                        ?>
                        var value_text = "<span>" + obj.value + "</span>";//Вывод текста
                        var checkbox = common.checkbox(obj, common);//Чекбокс
                        <?php
                    }
                    ?>
                    
                    if(obj.for_registrated == true)
                    {
                        icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/check.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                    }
                    if(obj.for_guests == true)
                    {
                        icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/guest.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                    }
                    if(obj.for_backend == true)
                    {
                        icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/shield.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                    }
                    if(obj.unblocked == 0)
                    {
                        icon += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='width:18px; height:18px; float:right; margin:0px 4px 8px 4px;'>";
                    }
                    return common.icon(obj, common)+ checkbox + common.folder(obj, common)  + icon + value_text;
            	},//~template
        
            editable:false,//редактируемое
            container:"container_G",//id блока div для дерева
            view:"tree",
        	select:true,//можно выделять элементы
        	drag:false,//можно переносить
        });
        /*~ДЕРЕВО*/
		webix.event(window, "resize", function(){ groups_tree.adjust(); });
    
    	var saved_groups = <?php echo $group_tree_dump_JSON; ?>;
	    groups_tree.parse(saved_groups);
	    groups_tree.openAll();
	    
	    <?php
	    //Отмечаем текущие группы, если идет редактирование. Если идет создание - массив пустой
	    ?>
	    var groups_allowed = <?php echo json_encode($groups_allowed); ?>;
	    for(var i=0; i < groups_allowed.length; i++)
	    {
	        groups_tree.checkItem(groups_allowed[i]);
	    }
	    <?php
	    ?>
	    
    }
    groups_tree_init();
    //-----------------------------------------------------
    //Функция проверки группы на доступ к бэкэнду. Рекурсивный вызов для проверки родительских групп
    function is_group_for_backend(node_id)
    {
        var node = groups_tree.getItem(node_id);//Объект узла группы
    
        if(node.for_backend == true)
        {
            return true;
        }
        
        if(node.$parent != 0)
        {
            return is_group_for_backend(node.$parent);
        }
        else
        {
            return false;
        }
    }
    //-----------------------------------------------------
    </script>
    
    
    
    
    
    
    
    
    
    
    
    
    
    <!--Start Модальное окно: Выбор позиции модуля -->
    <div class="text-center m-b-md">
		<div class="modal fade" id="modalWindow_positions" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2636); ?></h4>
					</div>
					<div class="modal-body">
						<div class="row">
							<?php
							$templates_query = $db_link->prepare("SELECT * FROM `templates` WHERE `is_frontend` = ?;");
							$templates_query->execute( array($is_frontend) );
							?>
							<div class="form-group col-lg-12">
								<label for="" class="col-lg-2 control-label">
									<?php echo translate_str_by_id(2637); ?>
								</label>
								<div class="col-lg-10">
									<select id="templates_selector" onchange="showTemplatePositions();" class="form-control">
										<script>
										var templates_positions = new Array();//Ассоциативный массив с позициями шаблонов
										function showTemplatePositions()
										{
											var template_positions_html = "";//Строка для отображения позиций шаблона
											var current_template = document.getElementById("templates_selector").value;
											for(var i=0; i<templates_positions[current_template].length; i++)
											{
												template_positions_html += "<div class=\"col-lg-4\"><a href=\"javascript:void(0);\" onclick=\"setPosition('"+templates_positions[current_template][i]["name"]+"');\">"+templates_positions[current_template][i]["caption"]+"</a></div>";
											}
											
											document.getElementById("template_positions_list").innerHTML = template_positions_html;
										}
										</script>
										<?php
										while( $template = $templates_query->fetch() )
										{
											//ТЕКУЩИЙ ШАБЛОН
											?>
											<option value="<?php echo $template["id"];?>"><?php echo $template["caption"];?></option>
											<script>
												templates_positions[<?php echo $template["id"];?>] = new Array();
												<?php
												$positions = json_decode($template["positions"], true);//Позиции шаблона - из БД
												$module_positions = array();//Массив для позиций типа "module"
												for($p=0; $p < count($positions); $p++)
												{
													if($positions[$p]["type"] == "module")
													{
														?>
														templates_positions[<?php echo $template["id"];?>].push({"name":"<?php echo $positions[$p]["name"];?>", "caption":"<?php echo $positions[$p]["caption"];?>"});
														<?php
													}
												}
												?>
											</script>
											<?php
										}
										?>
									</select>
								</div>
							</div>
							<div id="template_positions_list" class="col-lg-12">
							</div>
						</div>
					</div>
					<div class="modal-footer">
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
    <script>
        // ----------------------------------------------------------------
        //Открыть окно с позициями
        function openPositionWindow()
        {
            $('#modalWindow_positions').modal();//Открыть окно
            
            showTemplatePositions();//Обработка текущего выбранного шаблона
        }
        // ----------------------------------------------------------------
        //Выбор позиции
        function setPosition(position)
        {
            document.getElementById("position_input").value = position;
            $('#modalWindow_positions').modal('hide');//Скрыть окно
        }
    </script>
    <!--Start Модальное окно: Выбор позиции модуля -->
    
    
    
    
    
    <!-- ********************************************************************** -->
    <!-- START - БЛОК ФОРМЫ СОХРАНЕНИЯ -->
    <script>
    var data_structure = <?php echo json_encode($data_structure); ?>;//Структура спецнастроек
    // ----------------------------------------------------------------
    //Сохранение модуля
    function save_module()
    {
        //1. Содержимое модуля - только если оно текстовое. А если оно php, то содержимое будет выставлено на сервере из записи прототипа
        if("<?php echo $content_type; ?>" == "text")
        {
            document.getElementById("content").value = tinymce.activeEditor.getContent();
        }
        //Если тип содержимого - php, то оно уже заполнено изначально в форме при загрузке страницы и измениться в процессе редактирования модуля не может
        
        //2. Заголовок
        if(document.getElementById("caption_input").value == "")
        {
            webix.message({type:"error", text:"<?php echo translate_str_by_id(2638); ?>"});
            return;
        }
        document.getElementById("caption").value = document.getElementById("caption_input").value;
        
        //3. Позиция
        document.getElementById("position").value = document.getElementById("position_input").value;
        
        //4. Включен
		if( document.getElementById("activated_checkbox").checked == true )
		{
			document.getElementById("activated").value = 1;
		}
		else
		{
			document.getElementById("activated").value = 0;
		}
        
        
        //4.1 Флаг "Для всех"
        if( document.getElementById("for_all_checkbox").checked == true )
		{
			document.getElementById("for_all").value = 1;
		}
		else
		{
			document.getElementById("for_all").value = 0;
		}
        
        //5. Показывать заголовок
		if( document.getElementById("show_caption_checkbox").checked == true )
		{
			document.getElementById("show_caption").value = 1;
		}
		else
		{
			document.getElementById("show_caption").value = 0;
		}
        
        //6. Порядок вывода
        document.getElementById("order").value = document.getElementById("order_input").value;
        
        //7. Список материалов, к которым привязан этот модуль
        document.getElementById("content_array").value = JSON.stringify(content_tree.getChecked());
        
        //8. Значения специальных настроек
        var data_value = new Object;
        //8.1 По списку специальных настроек
        for(var i=0; i < data_structure.length; i++)
        {
            //8.2 Для кажой настройки получить значение из виджета
            //8.3 Записать это значение в общий объект спецнастроек
            data_value[data_structure[i]["name"]] = document.getElementById(data_structure[i]["name"]).value;
        }
        
        //8.4 Перевести объект в JSON-формат и записать его в поле формы
        document.getElementById("data_value").value = JSON.stringify(data_value);
        
        //9. Сохранение прав доступа к группам
        document.getElementById("groups_allowed").value = JSON.stringify(groups_tree.getChecked());
        
        //10. Отправляем форму
    	document.forms["save_module_form"].submit();//Отправляем
    }
    // ----------------------------------------------------------------
    </script>
    <form name="save_module_form" style="display:none" method="POST">
        <input type="hidden" name="module_save_action" value="<?php echo $page_mode; ?>" />
        
        <input type="hidden" name="id" id="id" value="<?php echo $id;?>" /> <!-- ID модуля -->
        <input type="hidden" name="prototype_id" id="prototype_id" value="<?php echo $prototype_id; ?>" /> <!-- ID прототипа -->
        <input type="hidden" name="prototype_name" id="prototype_name" value="<?php echo $prototype_name; ?>" /> <!-- Название прототипа -->
        <input type="hidden" name="prototype_name_lang_str_id" id="prototype_name_lang_str_id" value="<?php echo $prototype_name_lang_str_id; ?>" /> <!-- Название прототипа - Мультиязычность -->
        <input type="hidden" name="content_type" id="content_type" value="<?php echo $content_type; ?>" /> <!-- Тип содержимого модуля -->
        <!-- Ok --><input type="hidden" name="content" id="content" value='<?php echo $content; ?>' /> <!-- Содержимое -->
        <!-- Ok --><input type="hidden" name="content_lang_str_id" id="content_lang_str_id" value='<?php echo $content_lang_str_id; ?>' /> <!-- Мультиязычность Сдержимое при типе text -->
        <!-- Ok --><input type="hidden" name="caption" id="caption" value="" /> <!-- Заголовок -->
        <!-- Ok --><input type="hidden" name="caption_lang_str_id" id="caption_lang_str_id" value="<?php echo $caption_lang_str_id;?>" /> <!-- Заголовок (Мультиязычность) -->
        <!-- Ok --><input type="hidden" name="position" id="position" value="" /> <!-- Позиция -->
        <!-- Ok --><input type="hidden" name="activated" id="activated" value="" /> <!-- Включен (либо " checked") -->
        <!-- Ok --><input type="hidden" name="show_caption" id="show_caption" value="" /> <!-- Показывать заголовок -->
        <!-- Ok --><input type="hidden" name="order" id="order" value="" /> <!-- Порядок вывода -->
        <!-- Ok --><input type="hidden" name="data_value" id="data_value" value="" /> <!-- Значения специальных настроек -->
        <!-- Ok --><input type="hidden" name="content_array" id="content_array" value="" /> <!-- Список материалов, к которым привязан этот модуль -->
        <!-- Ok --><input type="hidden" name="groups_allowed" id="groups_allowed" value="" /> <!-- Список групп ползователей, которые имеют достп к этотому модулю -->
        <!-- Ok --><input type="hidden" name="for_all" id="for_all" value="" /> <!-- Включен (либо " checked") -->
		
		
		<input name="is_frontend" type="hidden" value="<?php echo $is_frontend; ?>" style="display:none"/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!-- END - БЛОК ФОРМЫ СОХРАНЕНИЯ -->
    <!-- ********************************************************************** -->
    
    <?php
}
?>