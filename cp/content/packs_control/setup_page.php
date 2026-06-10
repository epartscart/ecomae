<?php
/**
 * Скрипт для страницы установки пакета
*/
defined('_ASTEXE_') or die('No access');

if(!empty($_POST["setup_pack"]))
{
	// -------------------------------------------------------------------------------
	//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
	$pages_to_check = array();
	$pages_to_check[] = array('id'=>241, 'url'=>'packs');//Корневой раздел "Пакеты"
	$pages_to_check[] = array('id'=>242, 'url'=>'packs/packs_manager');//Менеджер пакетов
	$pages_to_check[] = array('id'=>247, 'url'=>'packs/setup');//Установить пакет
	$pages_to_check[] = array('id'=>248, 'url'=>'packs/pack_control');//Управление пакетом
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/check_admin_access/check_admin_access.php");
	// -------------------------------------------------------------------------------
	
	
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    //Проверяем расширение файла
    $file_name = $_FILES['pack_file']['name'];
    $file_ext = explode(".", $file_name);
    $file_ext = $file_ext[count($file_ext) - 1];
    if(strtoupper($file_ext) != "ZIP")
    {
        $error_message = translate_str_by_id(2714);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/packs/setup?error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    }
    
    //Загружаем файл
    $uploaddir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/pack_setup/";
    $uploadfile = $uploaddir . basename($_FILES['pack_file']['name']);
    if (! move_uploaded_file($_FILES['pack_file']['tmp_name'], $uploadfile)) 
    {
        $error_message = translate_str_by_id(2715);
        ?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/packs/setup?error_message=<?php echo $error_message; ?>";
        </script>
        <?php
        exit;
    } 
    
    //ДАЛЕЕ ВЫВОД СТРАНИЦЫ
    ?>
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/packs/packs_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/packs.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2699); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/packs/setup">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/pack_setup.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2716); ?></div>
				</a>
			
			
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2717); ?>
			</div>
			<div class="panel-body">
				<div id="progressbar"></div>
				<div id="setup_messages" style="padding:5px"></div>
				<div id="pack_info_div" style="padding:5px"></div>
			</div>
		</div>
	</div>
	
    
    
    
    
    <script>
    //УПРАВЛЕНИЕ ПРОЦЕССОМ УСТАНОВКИ ПАКЕТА
    var pack_id = 0;//Переменная для ID устанавливаемого пакета (определеяется после первого шага)
    start_prepare_setup();
    
    <?php
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	?>
    
    
    // ---------------------------------------------------------------------------------------------
    //1. ЗАПРОС: проверка файла пакета и создание учетной записи
    function start_prepare_setup()
    {
        setupIndication(10, "<?php echo translate_str_by_id(2718); ?>");
        jQuery.ajax({
                type: "POST",
                async: true, //Запрос асинхронный
                url: "/<?php echo $DP_Config->backend_dir; ?>/content/packs_control/ajax_prepare_setup.php",
                dataType: "json",//Тип возвращаемого значения
                data: "pack_file=<?php echo $uploadfile; ?>&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer) {
                    after_prepare_setup(answer);
                }
            });
    }
    // ---------------------------------------------------------------------------------------------
    //1. ОТВЕТ: Обработка ответа от скрипта подготовки пакета
    function after_prepare_setup(answer)
    {
        //Подготовка прошла успешно - следующий шаг - Копирование файлов
        if(answer.result_code == 0)
        {
            pack_id = answer.pack_id;//Принимаем ID устанавливаемого пакета
            processingFiles();//Запуск второго запроса - обработка файлов
        }
        else//Ошибка на данном шаге:
        {
            clearTmpFolder();//Очищаем временный каталог
            showMessage(answer.message, "error", 1);
        }
    }
    // ---------------------------------------------------------------------------------------------
    //2. ЗАПРОС: обработка файлов (копирование)
    function processingFiles()
    {
        setupIndication(20, "<?php echo translate_str_by_id(2719); ?>");
        jQuery.ajax({
                type: "POST",
                async: true, //Запрос асинхронный
                url: "/<?php echo $DP_Config->backend_dir; ?>/content/packs_control/ajax_processing_files.php",
                dataType: "json",//Тип возвращаемого значения
                data: "pack_id=" + pack_id + "&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer) {
                    after_processing_files(answer);
                }
            });
    }
    // ---------------------------------------------------------------------------------------------
    //2. ОТВЕТ: обработка файлов (копирование)
    function after_processing_files(answer)
    {
        //Копирование фалов прошло успешно
        if(answer.result_code == 0)
        {
            insert_extensions();//Следующий шаг - создание записей расширений
        }
        else//Ошибка на данном шаге:
        {
            clearTmpFolder();//Очищаем временный каталог
            showMessage(answer.message, "error", 2);
        }
    }
    // ---------------------------------------------------------------------------------------------
    //3. ЗАПРОС - СОЗДАНИЕ ЗАПИСЕЙ РАСШИРЕНИЙ
    function insert_extensions()
    {
        setupIndication(55, "<?php echo translate_str_by_id(2720); ?>");
        jQuery.ajax({
                type: "POST",
                async: true, //Запрос асинхронный
                url: "/<?php echo $DP_Config->backend_dir; ?>/content/packs_control/ajax_insert_extensions.php",
                dataType: "json",//Тип возвращаемого значения
                data: "pack_id=" + pack_id + "&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer) {
                    after_insert_extensions(answer);    
                }
            });
    }
    // ---------------------------------------------------------------------------------------------
    //3. ОТВЕТ - СОЗДАНИЕ ЗАПИСЕЙ РАСШИРЕНИЙ
    function after_insert_extensions(answer)
    {
        //Создание записей расширений прошло успешно
        if(answer.result_code == 0)
        {
            setupIndication(90, "<?php echo translate_str_by_id(2721); ?>");
            afterSuccessInstall(answer.pack_info_ob);
        }
        else//Ошибка на данном шаге:
        {
            clearTmpFolder();//Очищаем временный каталог
            showMessage(answer.message, "error", 3);
        }
    }
    // ---------------------------------------------------------------------------------------------
    //4. УСТАНОВКА ЗАВЕРШЕНА - ОТОБРАЖАЕМ ИНФОРМАЦИЮ ПО УСТАНОВЛЕННОМУ ПАКЕТУ
    function afterSuccessInstall(pack_info_ob)
    {
        setupIndication(100, "<?php echo translate_str_by_id(2722); ?>");
    
        var html_pack_info = " <div class=\"panel panel-default\"><div class=\"panel-heading\"><?php echo translate_str_by_id(2723); ?></div><div class=\"panel-body\">";
        html_pack_info += "<table class=\"table\">";
        
        html_pack_info += "<tr> <td><?php echo translate_str_by_id(2277); ?>:</td> <td>"+pack_info_ob.caption+"</td> </tr>";
        html_pack_info += "<tr> <td>ID:</td> <td>"+pack_id+"</td> </tr>";
        html_pack_info += "<tr> <td><?php echo translate_str_by_id(2703); ?>:</td> <td>"+pack_info_ob.author+"</td> </tr>";
        html_pack_info += "<tr> <td><?php echo translate_str_by_id(2704); ?>:</td> <td>"+pack_info_ob.version+"</td> </tr>";
        
        html_pack_info += "<tr> <td colspan=\"2\" align=\"center\"><b><?php echo translate_str_by_id(2724); ?></b></td> </tr>";
        if(pack_info_ob.files.length > 0)
        {
            html_pack_info += "<tr> <td colspan=\"2\"><?php echo translate_str_by_id(2706); ?>:</td> </tr>";
            for(var i=0; i < pack_info_ob.files.length; i++)
            {
                html_pack_info += "<tr> <td colspan=\"2\">"+pack_info_ob.files[i]["server_path"]+pack_info_ob.files[i]["file_name"]+"</td> </tr>";
            }
        }
        if(pack_info_ob.modules_prototypes.length > 0)
        {
            html_pack_info += "<tr> <td colspan=\"2\"><?php echo translate_str_by_id(2708); ?>:</td> </tr>";
            for(var i=0; i < pack_info_ob.modules_prototypes.length; i++)
            {
                html_pack_info += "<tr> <td colspan=\"2\">"+pack_info_ob.modules_prototypes[i]["caption"]+", ID "+pack_info_ob.modules_prototypes[i]["id"]+"</td> </tr>";
            }
        }
        if(pack_info_ob.plugins.length > 0)
        {
            html_pack_info += "<tr> <td colspan=\"2\"><?php echo translate_str_by_id(2709); ?>:</td> </tr>";
            for(var i=0; i < pack_info_ob.plugins.length; i++)
            {
                html_pack_info += "<tr> <td colspan=\"2\">"+pack_info_ob.plugins[i]["caption"]+", ID "+pack_info_ob.plugins[i]["id"]+"</td> </tr>";
            }
        }
        if(pack_info_ob.templates.length > 0)
        {
            html_pack_info += "<tr> <td colspan=\"2\"><?php echo translate_str_by_id(2707); ?>:</td> </tr>";
            for(var i=0; i < pack_info_ob.templates.length; i++)
            {
                html_pack_info += "<tr> <td colspan=\"2\">"+pack_info_ob.templates[i]["caption"]+", ID "+pack_info_ob.templates[i]["id"]+"</td> </tr>";
            }
        }
        
        
        html_pack_info += "</table></div></div>";
        
        document.getElementById("pack_info_div").innerHTML = html_pack_info;//Информация по установленному пакету
        showMessage("Пакет установлен", "success", 4);//Сообщение об успешной становке
        clearTmpFolder();//ОЧИЩАЕМ ВРЕМЕННЫЙ КАТАЛОГ С ПАКЕТОМ НА СЕРВЕРЕ
        
        document.getElementById("actions_panel").setAttribute("style", "display:block");//Показываем панель действий
    }
    // ---------------------------------------------------------------------------------------------
    //Функция индикации прогресса
    function setupIndication(percent, message)
    {
        //Значение progressbar
        $( "#progressbar" ).progressbar({
				value: percent
			});
    	
    	//Сообщение (текущее действие)
    	document.getElementById("setup_messages").innerHTML = message;
    }
    // ---------------------------------------------------------------------------------------------
    //Функция очистки временного каталога
    function clearTmpFolder()
    {
        jQuery.ajax({
            type: "POST",
            async: true, //Запрос асинхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/packs_control/ajax_clear_tmp_folder.php",
            dataType: "json",//Тип возвращаемого значения
            data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer) {
                if(answer == "Success")
                {
                    showMessage("<?php echo translate_str_by_id(2725); ?>", "info", 5);
                }
                else
                {
                    showMessage("<?php echo translate_str_by_id(2726); ?>", "error", 6);
                }
                
            }
        });
    }
    // ---------------------------------------------------------------------------------------------
    //Вывод сообщенний
    function showMessage(text, type, id_pre)
    {
        document.getElementById("setup_messages").innerHTML += "<div class=\"alert alert-"+type+" alert-dismissable\" id=\""+type+"_div_"+id_pre+"\"><button type=\"button\" class=\"close\" onclick=\"clearAlert('"+type+"_div_"+id_pre+"');\">&times;</button>"+text+"</div>";
    }
    // ---------------------------------------
    //Удаляем сообщение
    function clearAlert(alert_div_id)
    {
        var alert_div = document.getElementById(alert_div_id);
        alert_div.parentNode.removeChild(alert_div);
    }
    // ---------------------------------------------------------------------------------------------
    </script>
    <?php
}
else//Действий нет - выводим страницу указания файла.
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
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
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/packs/packs_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/packs.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2699); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2446); ?>
			</div>
			<div class="panel-body">
				<form method="POST" enctype="multipart/form-data" onsubmit="return checkSubmit();">
        	        <input type="hidden" name="setup_pack" id="setup_pack" value="setup_pack" />
					
					<div class="col-lg-6">
						<input class="form-control" type="file" name="pack_file" id="pack_file" accept="multipart/x-zip,application/zip,application/x-zip-compressed,application/x-compressed" />
					</div>
					<div class="col-lg-6">
						<button class="btn btn-success " type="submit"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2508); ?></span></button>
					</div>
					
        	        
					
        	        <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
        	    </form>
			</div>
		</div>
	</div>
    
    
    
    
    
    
    <script>
        //Проверка выбора файла
        function checkSubmit()
        {
            if(document.getElementById("pack_file").value == "")
            {
                alert("<?php echo translate_str_by_id(2727); ?>");
                return false;
            }
            return true;
        }
    </script>
    
    <?php
}
?>