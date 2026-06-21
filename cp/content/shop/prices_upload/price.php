<?php
/**
 * Страница для создания и редактирования одного прайс-листа
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( !empty($_POST["action"]) )//Действия
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    //Данные формы - одинаково для создания и редактирования
    $price_id = $_POST["price_id"];
    $name = htmlentities($_POST["name"]);
    $load_mode = $_POST["load_mode"];
    
    $ftp_host = $_POST["ftp_host"];
    $ftp_user = $_POST["ftp_user"];
    $ftp_password = $_POST["ftp_password"];
    
    $sender_email = $_POST["sender_email"];
	
	$delete_email_messages = 0;//Этот параметр больше не используется
	
	/*
	if( isset($_POST['delete_email_messages']) && $_POST['delete_email_messages'] == 'delete_email_messages' )
	{
		$delete_email_messages = 1;
	}
	else
	{
		$delete_email_messages = 0;
	}
	*/
	
	
    
    $strings_to_left = (int)$_POST["strings_to_left"];
    $manufacturer_col = (int)$_POST["manufacturer_col"];
    $article_col = (int)$_POST["article_col"];
    $name_col = (int)$_POST["name_col"];
    $exist_col = (int)$_POST["exist_col"];
    $price_col = (int)$_POST["price_col"];
    $time_to_exe_col = (int)$_POST["time_to_exe_col"];
    $storage_col = (int)$_POST["storage_col"];
    $min_order_col = (int)$_POST["min_order_col"];
    
	$file_name_substring = $_POST["file_name_substring"];
	
    $clean_before = 0;
    if(!empty($_POST["clean_before"]))
    {
        $clean_before = 1;
    }
    
    $link = trim($_POST["link"]);
	$encoding = trim($_POST["encoding"]);
	$separator = trim($_POST["separator"]);
	$h_time = (int) trim($_POST["h_time"]);
	
	$encoding_file = trim($_POST["encoding_file"]);
	$separator_file = trim($_POST["separator_file"]);
	$encoding = $encoding_file;
	$separator = $separator_file;
	/*
	if($load_mode == 1){
		$encoding = $encoding_file;
		$separator = $separator_file;
	}
	*/
	
	
	#Доработки при pyprices
	//Подстрока в имени архива
	$file_name_substring_arch = $_POST['file_name_substring_arch'];
	//Имя папки на FTP
	$ftp_folder = trim($_POST['ftp_folder']);
	//Не помечать письма прочитанными при обработке
	if( isset($_POST['not_mark_seen_email_messages']) && $_POST['not_mark_seen_email_messages'] == 'not_mark_seen_email_messages' )
	{
		$not_mark_seen_email_messages = 1;
	}
	else
	{
		$not_mark_seen_email_messages = 0;
	}
	//Подстрока в теме письма
	$message_header_substring = $_POST['message_header_substring'];
	
	
	
	
	if($h_time < 1){
		$h_time = 1;
	}
	
    if($_POST["action"] == "create")
    {
        if( $db_link->prepare("INSERT INTO `shop_docpart_prices` (`name`, `load_mode`, `ftp_host`, `ftp_user`, `ftp_password`, `sender_email`, `delete_email_messages`, `strings_to_left`, `manufacturer_col`, `article_col`, `name_col`, `exist_col`, `price_col`, `time_to_exe_col`, `storage_col`, `clean_before`, `min_order_col`, `file_name_substring`, `link`, `encoding`, `separator`, `h_time`, `file_name_substring_arch`, `ftp_folder`, `not_mark_seen_email_messages`, `message_header_substring`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);")->execute( array($name, $load_mode, $ftp_host, $ftp_user, $ftp_password, $sender_email, $delete_email_messages, $strings_to_left, $manufacturer_col, $article_col, $name_col, $exist_col, $price_col, $time_to_exe_col, $storage_col, $clean_before, $min_order_col, $file_name_substring, $link, $encoding, $separator, $h_time, $file_name_substring_arch, $ftp_folder, $not_mark_seen_email_messages, $message_header_substring) ) != true)
        {
            $error_message = translate_str_by_id(3700);
            epc_cp_redirect('/shop/prices/price?error_message=' . rawurlencode($error_message));
        }
        else//Учетная запись создана
        {
            //Получаем ID созданной записи
            $price_id = $db_link->lastInsertId();
            
            $success_message = translate_str_by_id(3701);
			epc_cp_redirect('/shop/prices/price?price_id=' . (int) $price_id . '&success_message=' . rawurlencode($success_message));
        }
    }
    else//Редактирование
    {
        $SQL_UPDATE = "UPDATE `shop_docpart_prices` SET 
            `name` = ?, 
            `load_mode` = ?, 
            `ftp_host` = ?, 
            `ftp_user` = ?, 
            `ftp_password` = ?, 
            `sender_email` = ?, 
			`delete_email_messages` = ?, 
            `strings_to_left` = ?, 
            `manufacturer_col` = ?, 
            `article_col` = ?,
            `name_col` = ?, 
            `exist_col` = ?, 
            `price_col` = ?, 
            `time_to_exe_col` = ?, 
            `storage_col` = ?, 
            `clean_before` = ?,
            `min_order_col` = ?,
			`file_name_substring` = ?, 
			`link` = ?, 
			`encoding` = ?, 
			`separator` = ?, 
			`h_time` = ?,
			`file_name_substring_arch` = ?, 
			`ftp_folder` = ?, 
			`not_mark_seen_email_messages` = ?, 
			`message_header_substring` = ?
            WHERE `id` = ?;";
			
			
		$binding_values = array($name, $load_mode, $ftp_host, $ftp_user, $ftp_password, $sender_email, $delete_email_messages, $strings_to_left, $manufacturer_col, $article_col, $name_col, $exist_col, $price_col, $time_to_exe_col, $storage_col, $clean_before, $min_order_col, $file_name_substring, $link, $encoding, $separator, $h_time, $file_name_substring_arch, $ftp_folder, $not_mark_seen_email_messages, $message_header_substring, $price_id);
			
        
        
        if( $db_link->prepare($SQL_UPDATE)->execute($binding_values) != true)
        {
            $error_message = translate_str_by_id(3702);
            epc_cp_redirect('/shop/prices/price?price_id=' . (int) $price_id . '&error_message=' . rawurlencode($error_message));
        }
        else//Учетную запись обновили
        {
            $success_message = translate_str_by_id(3703);
            epc_cp_redirect('/shop/prices/price?price_id=' . (int) $price_id . '&success_message=' . rawurlencode($success_message));
        }
    }
}//if( !empty($_POST["action"]) )//Действия
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
    //Исходные данные
    $price_id = 0;
    $page_name = translate_str_by_id(3704);
    $action_type = "create";
    
    $name = "";
    $load_mode = 1;
    
    $ftp_host = "";
    $ftp_user = "";
    $ftp_password = "";
    
    $sender_email = "";
	$delete_email_messages = 0;//Этот параметр больше не используется
    
    $strings_to_left = 0;
    $manufacturer_col = "";
    $article_col = "";
    $name_col = "";
    $exist_col = "";
    $price_col = "";
    $time_to_exe_col = "";
    $storage_col = "";
    $min_order_col = "";
	$file_name_substring = "";
    $clean_before = 1;
	
	$link = '';
	$encoding = 'UTF-8';
	$separator = ';';
	$h_time = '1';
    
	#Доработки при pyprices
	$file_name_substring_arch = "";
	$ftp_folder = "";
	$not_mark_seen_email_messages = 0;
	$message_header_substring = "";
	
    
    //Переход для редактирования прайс-листа
    if(!empty($_GET["price_id"]))
    {
        $price_id = $_GET["price_id"];
		
		$price_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `id` = ?;");
		$price_query->execute( array($price_id) );
        $price_record = $price_query->fetch();
        
        $name = $price_record["name"];
        $page_name = translate_str_by_id(3705)." \"$name\"";
        $action_type = "edit";
        $load_mode = $price_record["load_mode"];
        
        $ftp_host = $price_record["ftp_host"];
        $ftp_user = $price_record["ftp_user"];
        $ftp_password = $price_record["ftp_password"];
        
        $sender_email = $price_record["sender_email"];
		$delete_email_messages = $price_record["delete_email_messages"];//Этот параметр больше не используется
        
        $strings_to_left = $price_record["strings_to_left"];
        $manufacturer_col = $price_record["manufacturer_col"];
        $article_col = $price_record["article_col"];
        $name_col = $price_record["name_col"];
        $exist_col = $price_record["exist_col"];
        $price_col = $price_record["price_col"];
        $time_to_exe_col = $price_record["time_to_exe_col"];
        $storage_col = $price_record["storage_col"];
        $min_order_col = $price_record["min_order_col"];
		$file_name_substring = $price_record["file_name_substring"];
        $clean_before = $price_record["clean_before"];
		
		$link = $price_record["link"];
		$encoding = $price_record["encoding"];
		$separator = $price_record["separator"];
		$h_time = $price_record["h_time"];
		
		
		#Доработки при pyprices
		$file_name_substring_arch = $price_record['file_name_substring_arch'];
		$ftp_folder = $price_record['ftp_folder'];
		$not_mark_seen_email_messages = $price_record['not_mark_seen_email_messages'];
		$message_header_substring = $price_record['message_header_substring'];
    }
    
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
				
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/excel.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(771); ?></div>
				</a>
				
				
				<?php
				if( $price_id > 0 )
				{
					print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/prices/review?price_id=".$price_id, "background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-sync", "caption"=>translate_str_by_id(3706)) );
				}
				?>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
    

    
    
    
    <form method="POST" name="save_form">
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
        <input type="hidden" name="action" value="<?php echo $action_type; ?>" />
        
        <input type="hidden" name="price_id" value="<?php echo $price_id; ?>" />
		
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3707); ?>
				</div>
				<div class="panel-body">
				
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3708); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="name" value="<?php echo $name; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5389); ?>
						</label>
						<div class="col-lg-6">
							<select name="load_mode" id="load_mode" onchange="on_load_mode_changed();" class="form-control">
    	                        <?php
    	                        //Получим способы загрузки прайс-листов
                                $load_modes_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices_load_modes` ORDER BY `id` ASC;");
								$load_modes_query->execute();
                                while($load_mode_record = $load_modes_query->fetch() )
                                {
									//Способ загрузки "Ручной" переименовываем в "Нет", т.к. теперь используется другой контекст: в shop_docpart_prices_load_modes указаны способы получения прайсов от поставщиков (E-mail, FTP, URL). Поэтому первый способ, теперь означает "Нет", т.е. такой прайс обновляется только со своего ПК путем загрузки файла вручную. А вручную загружать файлы с ПК никто не запрещает.
									if($load_mode_record["id"] == 1)
									{
										$load_mode_record["name"] = 2457;
									}
                                    ?>
                                    <option value="<?php echo $load_mode_record["id"]; ?>"><?php echo translate_str_by_id($load_mode_record["name"]); ?></option>
                                    <?php
                                }
    	                        ?>
    	                    </select>
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5390); ?>
						</label>
						<div class="col-lg-6">
							<?php
							$clean_before_check = "";
							if($clean_before == 1)
							{
								$clean_before_check = "checked=\"checked\"";
							}
							?>
							<input type="checkbox" name="clean_before" <?php echo $clean_before_check; ?> />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5391); ?><br><small>(<?php echo translate_str_by_id(5392); ?>)</small>
						</label>
						<div class="col-lg-6">
							<input type="text" name="file_name_substring" value="<?php echo htmlspecialchars($file_name_substring !== '' ? $file_name_substring : $name, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" placeholder="<?php echo htmlspecialchars($name, ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5393); ?> (ZIP, RAR, 7Z, TAR)<br><small>(<?php echo translate_str_by_id(5392); ?>)</small>
						</label>
						<div class="col-lg-6">
							<input type="text" name="file_name_substring_arch" value="<?php echo $file_name_substring_arch; ?>" class="form-control" placeholder="" />
						</div>
					</div>
					
					
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3178); ?>
						</label>
						<div class="col-lg-6">
							<select name="encoding_file" class="form-control">
								<option <?php echo ($encoding == 'auto')?'selected':''; ?> value="auto"><?php echo translate_str_by_id(5394); ?></option>
    	                        <option <?php echo ($encoding == 'utf-8')?'selected':''; ?> value="utf-8">UTF-8</option>
    	                        <option <?php echo ($encoding == 'cp1251')?'selected':''; ?> value="cp1251">Windows-1251 (ANSI)</option>
    	                    </select>
						</div>
					</div>
					<?php /*
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							Разделитель колонок (для файлов CSV и TXT)
						</label>
						<div class="col-lg-6">
							<select name="separator_file" class="form-control">
    	                        <option <?php echo ($separator == ';')?'selected':''; ?> value=";">Точка с запятой</option>
    	                        <option <?php echo ($separator == '\t')?'selected':''; ?> value="\t">Табуляция</option>
    	                    </select>
						</div>
					</div>
					*/ ?>
					<!-- Заглушка. Теперь разделитель в текстовых файлах определяется автоматически -->
					<input type="hidden" name="separator_file" value=";" />
					
					
				</div>
			</div>
		</div>
		
		

        
        <script>
            //Обработка переключения способа загрузки
            function on_load_mode_changed()
            {
                var load_mode = parseInt(document.getElementById("load_mode").value);//Выбранный способ загрузки
                
                //var auto_upload_config_window_file = document.getElementById("auto_upload_config_window_file");//Окно настроек ручной загрузки
                var auto_upload_config_window_link = document.getElementById("auto_upload_config_window_link");//Окно настроек загрузки по ссылке
                var auto_upload_config_window_ftp = document.getElementById("auto_upload_config_window_ftp");//Окно настроек автозагрузки с FTP
                var auto_upload_config_window_email = document.getElementById("auto_upload_config_window_email");//Окно настроек автозагрузки с E-mail
                
                
                switch(load_mode)
				{
					case 1:
						//auto_upload_config_window_file.setAttribute("style", "display:block;");
						auto_upload_config_window_ftp.setAttribute("style", "display:none;");
						auto_upload_config_window_email.setAttribute("style", "display:none;");
						auto_upload_config_window_link.setAttribute("style", "display:none;");
						break;
					case 2:
						//auto_upload_config_window_file.setAttribute("style", "display:none;");
						auto_upload_config_window_ftp.setAttribute("style", "display:block;");
						auto_upload_config_window_email.setAttribute("style", "display:none;");
						auto_upload_config_window_link.setAttribute("style", "display:none;");
						break;
					case 3:
						//auto_upload_config_window_file.setAttribute("style", "display:none;");
						auto_upload_config_window_ftp.setAttribute("style", "display:none;");
						auto_upload_config_window_email.setAttribute("style", "display:block;");
						auto_upload_config_window_link.setAttribute("style", "display:none;");
						break;
					case 4:
						//auto_upload_config_window_file.setAttribute("style", "display:none;");
						auto_upload_config_window_ftp.setAttribute("style", "display:none;");
						auto_upload_config_window_email.setAttribute("style", "display:none;");
						auto_upload_config_window_link.setAttribute("style", "display:block;");
						break;
				}
            }
        </script>
        
		
		<!--
		<div class="col-lg-12" id="auto_upload_config_window_file" style="display:block;">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					Настройки ручной загрузки прайс-листа
				</div>
				<div class="panel-body">
					
				</div>
			</div>
		</div>
		-->
		
		
		<div class="col-lg-12" id="auto_upload_config_window_link" style="display:none;">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(4936); ?>
				</div>
				<div class="panel-body">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5395); ?><br><small>(<?php echo translate_str_by_id(5396); ?> -  http://domain.ru/folder1/folderN/file_name)</small>
						</label>
						<div class="col-lg-6">
							<input type="text" name="link" value="<?php echo $link; ?>" class="form-control" />
						</div>
					</div>		
				</div>
			</div>
		</div>
		
		
		
		<div class="col-lg-12" id="auto_upload_config_window_ftp" style="display:none;">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3712); ?>
				</div>
				<div class="panel-body">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5397); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="ftp_host" value="<?php echo $ftp_host; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5398); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="ftp_user" value="<?php echo $ftp_user; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5399); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="ftp_password" value="<?php echo $ftp_password; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5400); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="ftp_folder" value="<?php echo $ftp_folder; ?>" class="form-control" />
						</div>
					</div>
					
				</div>
			</div>
		</div>
		
		
		
		<div class="col-lg-12" id="auto_upload_config_window_email" style="display:none;">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3714); ?>
				</div>
				<div class="panel-body">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(16); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="sender_email" value="<?php echo $sender_email; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5401); ?><br><small>(<?php echo translate_str_by_id(5392); ?>)</small>
						</label>
						<div class="col-lg-6">
							<input type="text" name="message_header_substring" value="<?php echo $message_header_substring; ?>" class="form-control" />
						</div>
					</div>
					
					
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5402); ?><br>
							<small>(<?php echo translate_str_by_id(5303); ?>)</small>
						</label>
						<div class="col-lg-6">
							<?php
							$checked = "";
							if($not_mark_seen_email_messages == 1)
							{
								$checked = " checked='checked' ";
							}
							?>
						
							<input type="checkbox" name="not_mark_seen_email_messages" value="not_mark_seen_email_messages" class="form-control" <?php echo $checked; ?> />
							
							<!-- Этот параметр больше не используется -->
							<input type="hidden" name="delete_email_messages" value="delete_email_messages" class="form-control" />
						</div>
					</div>
				</div>
			</div>
		</div>
		
		
		
		
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3716); ?>
				</div>
				<div class="panel-body">
				
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5404); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="strings_to_left" value="<?php echo $strings_to_left; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3718); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="manufacturer_col" value="<?php echo $manufacturer_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3719); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="article_col" value="<?php echo $article_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3720); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="name_col" value="<?php echo $name_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3721); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="price_col" value="<?php echo $price_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3722); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="exist_col" value="<?php echo $exist_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3723); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="storage_col" value="<?php echo $storage_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3724); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="time_to_exe_col" value="<?php echo $time_to_exe_col; ?>" class="form-control" />
						</div>
					</div>
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3725); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" name="min_order_col" value="<?php echo $min_order_col; ?>" class="form-control" />
						</div>
					</div>
				
				</div>
			</div>
		</div>
    </form>
    
    
    <?php
    if($action_type == "edit" && $price_id > 0)
    {
        ?>
        <div class="col-lg-12">
            <div class="hpanel">
                <div class="panel-heading hbuilt">
                    Price upload history — all files kept for download
                    <a class="btn btn-xs btn-primary pull-right" href="javascript:void(0);" onclick="epcPriceEditLoadUploadHistory();">Refresh</a>
                </div>
                <div class="panel-body" id="epc_price_edit_upload_history_body" data-price-id="<?php echo (int)$price_id; ?>">
                    <div class="text-center"><i class="fas fa-spinner fa-pulse"></i></div>
                </div>
            </div>
        </div>
        <?php
    }
    //Если был переход для редатирования существующего прайс-листа - инициализируем виджеты
    if($action_type == "edit")
    {
        ?>
        <script>
            //Выставляем способ загрузки
            document.getElementById("load_mode").value = <?php echo $load_mode; ?>;
            on_load_mode_changed();
        </script>
        <?php
    }
    ?>
    
    
    
    
    
    
    <?php
}
?>