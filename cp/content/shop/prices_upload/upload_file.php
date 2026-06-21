<?php
/**
 * Страничный скрипт для загрузки файла прайс-листа
 * 
 * 
 * - сразу импортировать файл в таблицу назначения;
 * - таблицу сразу создавать с именованными колонками нужного типа
 * - обработку значений колонок запускать отдельно из js
*/
defined('_ASTEXE_') or die('No access');

//--------------------------------------------------------------------------------------------------------
//Функция очистки каталога ($clear_only: true - только очистить, false - удалить и сам каталог)
function clear_dir($dir, $clear_only) 
{
	foreach(glob($dir . '/*') as $file) 
	{
		if(is_dir($file))
		{
			clear_dir($file, false);
		}
		else
		{
			$file_name = explode("/", $file);
			$file_name = $file_name[ count($file_name) - 1 ];
			if( $file_name != "index.html" )
			{
				unlink($file);
			}
		}
	}
	if(!$clear_only)
	{
		rmdir($dir);
	}
}
//--------------------------------------------------------------------------------------------------------

function downloadFile ($URL, $PATH) {
    $ReadFile = fopen ($URL, "rb");
    if ($ReadFile) {
        $WriteFile = fopen ($PATH, "wb");
        if ($WriteFile){
            while(!feof($ReadFile)) {
                fwrite($WriteFile, fread($ReadFile, 4096 ));
            }
            fclose($WriteFile);
        }
        fclose($ReadFile);
    }
}

//--------------------------------------------------------------------------------------------------------
?>



<?php
if(!empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
    if($_POST["action"] == "upload")
    {
		//Для работы с пользователем
		require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
		$user_session = DP_User::getAdminSession();
		
		
        $price_id = $_POST["price_id"];
        $clean_before = 0;
        if( !empty($_POST["clean_before"]) )
        {
            $clean_before = 1;
        }
        
		$price_configuration_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `id` = ?;");
		$price_configuration_query->execute( array($price_id) );
		$item_prices = $price_configuration_query->fetch();
        
        //Проверяем наличие временного каталога для загрузки. ПРИ НЕОБХОДИМОСТИ 0 СОЗДАЕМ
        $treelax_tmp_dir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload;//Путь к каталогу для загрузки файлов прайс-листов
        if(!is_dir($treelax_tmp_dir))
        {
            if(!mkdir($treelax_tmp_dir))
            {
                $error_message = translate_str_by_id(3768);
                epc_cp_redirect('/shop/prices/upload?price_id=' . (int) $price_id . '&error_message=' . rawurlencode($error_message));
            }
        }
        else//Каталог есть - предварительно очищаем его
        {
            clear_dir($treelax_tmp_dir, true);//Функция очистки каталога (true - очистить, а сам каталог оставить)
        }
		
		if($item_prices['load_mode'] == 4 && empty($_FILES['price_file']['name'])){
			
			//Проверям на расширение из трех знаков (txt, csv, xls)
			$file_format = substr($item_prices['link'], strlen($item_prices['link'])-4, 4);
			$file_format = strtolower($file_format);//К нижнему регистру
			if($file_format != ".txt" && $file_format != ".csv" && $file_format != ".xls" && $file_format != ".zip" && $file_format != ".rar")
			{
				//Из трех знаков не подходит - получаем четыре знака
				$file_format = substr($item_prices['link'], strlen($item_prices['link'])-5, 5);
			}
			//Теперь полная проверка расширения
			if(strtoupper($file_format) != ".TXT" &&
			strtoupper($file_format) != ".CSV" &&
			strtoupper($file_format) != ".XLS" &&
			strtoupper($file_format) != ".XLSX" &&
			strtoupper($file_format) != ".ZIP" &&
			strtoupper($file_format) != ".RAR")
			{
				$error_message = translate_str_by_id(3769);
				epc_cp_redirect('/shop/prices/upload?price_id=' . (int) $price_id . '&error_message=' . rawurlencode($error_message));
			}
			
			$uploaddir = $treelax_tmp_dir."/";
			$uploadfile = $uploaddir . basename($item_prices['link']);
			
			downloadFile($item_prices['link'], $uploadfile);
		
		}else{
			
			//БРОСАЕМ В НЕГО ФАЙЛ
			//Проверям на расширение из трех знаков (txt, csv, xls)
			$file_format = substr($_FILES['price_file']['name'], strlen($_FILES['price_file']['name'])-4, 4);
			$file_format = strtolower($file_format);//К нижнему регистру
			if($file_format != ".txt" && $file_format != ".csv" && $file_format != ".xls" && $file_format != ".zip" && $file_format != ".rar")
			{
				//Из трех знаков не подходит - получаем четыре знака
				$file_format = substr($_FILES['price_file']['name'], strlen($_FILES['price_file']['name'])-5, 5);
			}
			//Теперь полная проверка расширения
			if(strtoupper($file_format) != ".TXT" &&
			strtoupper($file_format) != ".CSV" &&
			strtoupper($file_format) != ".XLS" &&
			strtoupper($file_format) != ".XLSX" &&
			strtoupper($file_format) != ".ZIP" &&
			strtoupper($file_format) != ".RAR")
			{
				$error_message = translate_str_by_id(3769);
				epc_cp_redirect('/shop/prices/upload?price_id=' . (int) $price_id . '&error_message=' . rawurlencode($error_message));
			}
			//Загружаем файл
			$uploaddir = $treelax_tmp_dir."/";
			$uploadfile = $uploaddir . basename($_FILES['price_file']['name']);
			if (! move_uploaded_file($_FILES['price_file']['tmp_name'], $uploadfile)) 
			{
				$error_message = translate_str_by_id(3126);
				epc_cp_redirect('/shop/prices/upload?price_id=' . (int) $price_id . '&error_message=' . rawurlencode($error_message));
			}
			
        }
        
        //Файл загружен - теперь выводим страницу с фунцией импорта прайс-листа по AJAX-запросу
        ?>
        
        
        <!-- ЛОГ ПРОЦЕССА -->
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3770); ?>
				</div>
				<div class="panel-body" id="import_log">
				</div>
			</div>
			
			<div class="log_div_loading_gif" id="loading_gif" style="text-align:center;">
				<img src="/content/files/images/ajax-loader-transparent.gif" />
			</div>
		</div>
		
        
        
        <script>
			//Панель кнопок после выполнения
			var buttons_panel_after_work = "<a class=\"btn w-xs btn-success\" href=\"/<?php echo $DP_Config->backend_dir; ?>/shop/prices\"><?php echo translate_str_by_id(771); ?></a> <a class=\"btn w-xs btn-primary2\" href=\"/<?php echo $DP_Config->backend_dir; ?>/shop/prices/upload?price_id=<?php echo $price_id; ?>\"><i class=\"fa fa-upload\"></i> <span class=\"bold\"><?php echo translate_str_by_id(3771); ?></span></a>";
		
		
            //УПРАВЛЕНИЕ ПРОЦЕССОМ ИПОРТА
            
            var action_loading_img = "<img src=\"/content/files/images/ajax-loader-transparent.gif\" style=\"width:15px\" />";
            
            var current_action_indicator = 2;
            
            document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3772); ?> - <span id=\"indicator_0\" class=\"complete\"><?php echo translate_str_by_id(2722); ?></span></p>";
            document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3773); ?> - <span id=\"indicator_1\" class=\"complete\"><?php echo translate_str_by_id(2722); ?></span></p>";
            document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3774); ?> - <span id=\"indicator_"+current_action_indicator+"\" class=\"process\">"+action_loading_img+"</span></p>";
            
            
            //АЛГОРИТМ ИМПОРТА ФАЙЛА
            jQuery.ajax({
                type: "GET",
                async: true, //Запрос асинхронный
                url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_2_extract_files.php"+"?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                dataType: "json",//Тип возвращаемого значения
                success: function(answer){
                    console.log(answer);
                    if(answer.packs_count == 0)//Архивов не было - все нормально
                    {
                        //Работаем с логом
                        var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                        action_indicator.innerHTML = "<?php echo translate_str_by_id(3775); ?>";
                        action_indicator.setAttribute("class", "complete");
                        
                        excel_convert();//Далее запускаем конвертирование файлов Excel
                    }
                    else//Были обнаружены архивы
                    {
                        if(answer.packs_error > 0)//Есть ошибки при работе с архивами
                        {
                            //Работаем с логом
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(3776); ?>: "+answer.packs_count+". <?php echo translate_str_by_id(3777); ?>: "+answer.packs_error;
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                        else//Все архивы извлечены успешно
                        {
                            //Работаем с логом
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(3778); ?>: "+answer.packs_count;
                            action_indicator.setAttribute("class", "complete");
                            
                            excel_convert();//Далее запускаем конвертирование файлов Excel
                        }
                    }
                },
				error: function (e, ajaxOptions, thrownError){
					//Пишем лог
					var action_indicator = document.getElementById("indicator_"+current_action_indicator);
					action_indicator.innerHTML = "Ошибка. "+'Ошибка: '+ e.status +' - '+ thrownError;
					action_indicator.setAttribute("class", "error");
					
					//Убираем loading.gif
					document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
				}
            });
            
            
            
            // --------------------------------------------------------------------------------------------------------------
            //Команда на конвертацию файлов Excel
            function excel_convert()
            {
                //Пишем лог
                current_action_indicator++;//Следующий шаг
                document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3779); ?> - <span id=\"indicator_"+current_action_indicator+"\" class=\"process\">"+action_loading_img+"</span></p>";
                document.getElementById("import_log").scrollTop = document.getElementById("import_log").scrollHeight;//Прокручиваем лог
            
                jQuery.ajax({
                    type: "GET",
                    async: true, //Запрос асинхронный
                    url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_3_excel_convert.php"+"?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                    dataType: "json",//Тип возвращаемого значения
                    success: function(answer){
                        console.log(answer);
                        if(answer.result == 1)
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2722); ?>";
                            action_indicator.setAttribute("class", "complete");
                            
                            //ЗАПУСК СЛЕДУЮЩЕЙ КОМАНДЫ...
                            csv_prepare();//Обработка файлов csv
                        }
                        else
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+answer.message;
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                    },
					error: function (e, ajaxOptions, thrownError){
						//Пишем лог
						var action_indicator = document.getElementById("indicator_"+current_action_indicator);
						action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+'<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError;
						action_indicator.setAttribute("class", "error");
						
						//Убираем loading.gif
						document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
					}
                });
            }//~function excel_convert()
            // --------------------------------------------------------------------------------------------------------------
            //Команда на обработку файлов csv
            function csv_prepare()
            {
                //Пишем лог
                current_action_indicator++;//Следующий шаг
                document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3780); ?> - <span id=\"indicator_"+current_action_indicator+"\" class=\"process\">"+action_loading_img+"</span></p>";
                document.getElementById("import_log").scrollTop = document.getElementById("import_log").scrollHeight;//Прокручиваем лог
                
                jQuery.ajax({
                    type: "GET",
                    async: true, //Запрос асинхронный
                    url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_4_prepare_csv.php"+"?price_id=<?php echo $price_id; ?>&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                    dataType: "json",//Тип возвращаемого значения
                    success: function(answer){
                        console.log(answer);
                        if(answer.result == 1)
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2722); ?>";
                            action_indicator.setAttribute("class", "complete");
                            
                            //ЗАПУСК СЛЕДУЮЩЕЙ КОМАНДЫ...
                            import_csv_to_db();//Команда импорта csv файлов в БД
                        }
                        else
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+answer.message;
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                    }
                });
            }//~function csv_prepare()
            // --------------------------------------------------------------------------------------------------------------
            //Команда импорта csv файлов в БД
            var has_import_json_answer = false;//Флаг - импорт файла вернул JSON-ответ в штатном режиме
			function import_csv_to_db()
            {
                //Пишем лог
                current_action_indicator++;//Следующий шаг
                document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3781); ?> - <span id=\"indicator_"+current_action_indicator+"\" class=\"process\">"+action_loading_img+"</span></p>";
                document.getElementById("import_log").scrollTop = document.getElementById("import_log").scrollHeight;//Прокручиваем лог
                
                jQuery.ajax({
                    type: "GET",
                    async: true, //Запрос асинхронный
                    url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_5_import_csv_to_db.php",
                    dataType: "json",//Тип возвращаемого значения
                    data: "price_id=<?php echo $price_id; ?>&initiator=js&clean_before=<?php echo $clean_before; ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                    success: function(answer){
                        console.log(answer);
                        if(answer.result == 1)
                        {
							has_import_json_answer = true;
							
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2722); ?>";
                            action_indicator.setAttribute("class", "complete");
                            
                            //ЗАПУСК СЛЕДУЮЩЕЙ КОМАНДЫ...
                            complete_session();//Завершение сессии
                        }
                        else
                        {
							has_import_json_answer = true;
							
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+answer.message;
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                    },
					error: function (e, ajaxOptions, thrownError){
						//Пишем лог
						var action_indicator = document.getElementById("indicator_"+current_action_indicator);
						action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+'<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError;
						action_indicator.setAttribute("class", "error");
						
						//Убираем loading.gif
						document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
					},
					complete: function( jqXHR, textStatus )
					{
						if( ! has_import_json_answer)
						{
							//Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(3782); ?>";
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
							
							table_enable_keys();
						}
					}
					
                });
            }
            // --------------------------------------------------------------------------------------------------------------
            //Завершение сессии
            function complete_session()
            {
                //Пишем лог
                current_action_indicator++;//Следующий шаг
                document.getElementById("import_log").innerHTML += "<p><?php echo translate_str_by_id(3783); ?> - <span id=\"indicator_"+current_action_indicator+"\" class=\"process\">"+action_loading_img+"</span></p>";
                document.getElementById("import_log").scrollTop = document.getElementById("import_log").scrollHeight;//Прокручиваем лог
                
                jQuery.ajax({
                    type: "GET",
                    async: true, //Запрос асинхронный
                    url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_6_complete_session.php",
                    dataType: "json",//Тип возвращаемого значения
                    data: "price_id=<?php echo $price_id; ?>"+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                    success: function(answer){
                        console.log(answer);
                        if(answer.result == 1)
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2722); ?>";
                            action_indicator.setAttribute("class", "complete");
                            
                            //ВСЕ ВЫПОЛНЕНО
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                        else
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+answer.message;
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                    },
                    statusCode: {
                        502: function () {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?> 502";
                            action_indicator.setAttribute("class", "error");
                            
                            //Убираем loading.gif
                            document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
                        }
                    },
					error: function (e, ajaxOptions, thrownError){
						//Пишем лог
						var action_indicator = document.getElementById("indicator_"+current_action_indicator);
						action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+'<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError;
						action_indicator.setAttribute("class", "error");
						
						//Убираем loading.gif
						document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
					}
                });
            }
			// --------------------------------------------------------------------------------------------------------------
            //Включить индексы в таблице - необходимо, если заргузка CSV не успела выполниться полностью
            function table_enable_keys()
            {
                jQuery.ajax({
                    type: "GET",
                    async: true, //Запрос асинхронный
                    url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_7_enable_keys.php"+"?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                    dataType: "json",//Тип возвращаемого значения
                    success: function(answer){
                        console.log(answer);
						if(answer.result == 1)
                        {
                            //Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = action_indicator.innerHTML + "<br><b><?php echo translate_str_by_id(3784); ?></b>";
                        }
						else
						{
							//Пишем лог
                            var action_indicator = document.getElementById("indicator_"+current_action_indicator);
                            action_indicator.innerHTML = action_indicator.innerHTML + "<br><b><?php echo translate_str_by_id(3785); ?></b>";
						}
                    },
					error: function (e, ajaxOptions, thrownError){
						//Пишем лог
						var action_indicator = document.getElementById("indicator_"+current_action_indicator);
						action_indicator.innerHTML = "<?php echo translate_str_by_id(2122); ?>. "+'<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError;
						action_indicator.setAttribute("class", "error");
						
						//Убираем loading.gif
						document.getElementById("loading_gif").innerHTML = buttons_panel_after_work;
					}
                });
            }
            // --------------------------------------------------------------------------------------------------------------
        </script>
        
        
        
        <?php
    }
}
else//Действий нет - выводим форму выбора файла
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    $price_id = $_GET['price_id'];
    
    //Получаем имя прайс-листа
	$price_info_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `id` = ?;");
	$price_info_query->execute( array($price_id) );
    $price_record = $price_info_query->fetch();
	if($price_record == false)
    {
        exit("No such price record");
    }
    $price_name = $price_record["name"];
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
				<a class="panel_a" href="javascript:void(0);" onclick="submitForm();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/upload.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3181); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/excel.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(771); ?></div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
    
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2446); ?>
			</div>
			<div class="panel-body">
				<form method="post" enctype="multipart/form-data" name="upload_form">
					<input type="hidden" name="price_id" value="<?php echo $price_id; ?>" />
					<input type="hidden" name="action" value="upload" />
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
					
					<input type="file" name="price_file" id="price_file" class="form-control" />
					
					<?php
					if($price_record["load_mode"] == 4){
					?>
					<?php echo translate_str_by_id(4653); ?>: <?=$price_record["link"];?>
					<?php
					}
					?>
					
					<div class="hr-line-dashed col-lg-12"></div>
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(3786); ?>
						</label>
						<div class="col-lg-6">
							<input type="checkbox" name="clean_before" checked="checked" />
						</div>
					</div>
					
				</form>
			</div>
		</div>
	</div>
	
    
    
    <script>
    //Проверка формы
    function submitForm()
    {
        var price_file = document.getElementById("price_file");
        
		<?php
		if($price_record["load_mode"] != 4){
		?>
		if(price_file.value == "" || price_file.value == null)
        {
            alert("<?php echo translate_str_by_id(3192); ?>");
            return;
        }
		<?php
		}
		?>
        
        document.forms['upload_form'].submit();
    }
    </script>
    <?php
}
?>