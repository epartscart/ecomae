<?php
/**
 * Скрипт для страницы менеджера прайс-листов
*/
defined('_ASTEXE_') or die('No access');

$epc_is_upload_guide_view = false;
if (empty($_POST['action'])) {
	if (isset($_GET['view']) && (string)$_GET['view'] === 'guide') {
		$epc_is_upload_guide_view = true;
	} else {
		$epc_req_uri = (string)($_SERVER['REQUEST_URI'] ?? '');
		if (stripos($epc_req_uri, 'view=guide') !== false) {
			$epc_is_upload_guide_view = true;
		}
	}
}

// ?view=guide loads the full price manager through CMS eval and can hang the CP shell — use the dedicated guide page.
if ($epc_is_upload_guide_view && empty($_POST['action'])) {
	$epc_backend = (isset($DP_Config) && is_object($DP_Config)) ? $DP_Config->backend_dir : 'cp';
	if (!headers_sent()) {
		header('Location: /' . $epc_backend . '/shop/prices/guide', true, 302);
		exit;
	}
}

if (!$epc_is_upload_guide_view) {
	//Это нужно для подключения единого скрипта записи файла crontab
	define('_PYPRICES_CRONTAB_', 1);
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/prices_upload/epc_prices_manager_perf.php");
	//Очистка технологических таблиц (skip most Super CP page loads — avoids table locks on large tenants)
	if (epc_prices_should_run_tables_cleaner()) {
		require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/pyprices_tables_cleaner.php");
	}
}

if( ! empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Действия - удаление отмеченных прайс-листов
    if($_POST["action"] == "delete_prices")
    {
		//Делаем через транзакцию
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			//Список прайс-листов на удаление
			$prices = json_decode($_POST["prices"], true);
			
			//Делаем строку с перечислением через запятую прайс-листов
			$binding_values = array();
			$prices_str = "";
			for($i=0; $i < count($prices); $i++)
			{
				if($i > 0) $prices_str .= ",";
				$prices_str .= "?";
				
				array_push($binding_values, $prices[$i]);
			}
			
			//Удаляем учетную запись (записи) прайс-листа
			if( $db_link->prepare("DELETE FROM `shop_docpart_prices` WHERE `id` IN ($prices_str);")->execute($binding_values) != true)
			{
				throw new Exception(translate_str_by_id(5405));
			}
			
			
			//Удаляем данные прайс-листов
			if( $db_link->prepare("DELETE FROM `shop_docpart_prices_data` WHERE `price_id` IN ($prices_str);")->execute($binding_values) != true)
			{
				throw new Exception(translate_str_by_id(5406));
			}
			
			
			//Удаляем записи связей между прайс-листами и заданиями по расписанию
			if( ! $db_link->prepare("DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `price_id` IN ($prices_str);")->execute($binding_values) )
			{
				throw new Exception(translate_str_by_id(5407));
			}
			
			
			//Если количество прайс-листов в задании по расписанию теперь 0, то, само задание тоже удаляем.
			if( ! $db_link->prepare("DELETE FROM `shop_docpart_pyprices_crontab` WHERE ( SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = `shop_docpart_pyprices_crontab`.`id` ) = ?;")->execute( array(0) ) )
			{
				throw new Exception(translate_str_by_id(5408));
			}
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			epc_cp_redirect('/shop/prices?error_message=' . rawurlencode($e->getMessage()));
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		//Перезапись файла crontab (единый скрипт) - на случай, если при удалении прайс-листов были также удалены связанные задания по расписанию
		require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/for_cron/crontab_writer.php"); 
		
		epc_cp_redirect('/shop/prices?success_message=' . rawurlencode(translate_str_by_id(3757)));
    }
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();

	if ($epc_is_upload_guide_view) {
		require $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/prices_upload/guide.php';
		return;
	} else {
	
    ?>
    
    <?php
        require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
	
	
	
	
	
	
	<!-- START - Подсказки и инструкции для pyprices, тестирование работы, автоматический деплой pyprices -->
	<?php
	$show_pyprices_recomendation_anyway = false;//Флаг - показывать это окно в любом случае при заходе на страницу
	?>
	<div class="text-center m-b-lg">
		<div class="modal fade" id="pypriceTechRecomendation_Window" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(5409); ?></h4>
						<small class="font-bold"><?php echo translate_str_by_id(5410); ?></small>
					</div>
					<div class="modal-body" id="pypriceTechRecomendation_Window_body">
						<div id="epc_pyprices_health_panel" class="text-center" style="padding:12px;">
							<i class="fas fa-spinner fa-pulse"></i>
							<?php echo translate_str_by_id(5421); ?>…
						</div>
					</div>
					<div class="modal-footer" id="pypriceTechRecomendation_Window_body_footer">
						<button type="button" class="btn btn-default" id="epc_pyprices_dismiss_forever_btn" style="display:none;" onclick="do_not_show_instructions_any_more();"><?php echo translate_str_by_id(5420); ?></button>
						<button type="button" class="btn btn-primary" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<script>
	// --------------------------------------------------------------------------------------
	var epc_pyprices_health_critical = false;
	function epc_load_pyprices_health(onDone)
	{
		jQuery.ajax({
			type: 'POST',
			url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_epc_pyprices_health.php',
			dataType: 'json',
			timeout: 8000,
			data: 'csrf_guard_key=' + encodeURIComponent('<?php echo $user_session["csrf_guard_key"]; ?>'),
			success: function(answer) {
				if (answer && answer.html) {
					document.getElementById('epc_pyprices_health_panel').innerHTML = answer.html;
				}
				epc_pyprices_health_critical = !!(answer && answer.critical);
				var dismissBtn = document.getElementById('epc_pyprices_dismiss_forever_btn');
				if (dismissBtn) {
					dismissBtn.style.display = epc_pyprices_health_critical ? 'none' : '';
				}
				if (typeof onDone === 'function') {
					onDone(answer);
				}
			},
			error: function() {
				epc_pyprices_health_critical = true;
				document.getElementById('epc_pyprices_health_panel').innerHTML =
					'<p style="background-color:#e74c3c;color:#FFF;padding:5px;">pyprices health check timed out</p>';
				if (typeof onDone === 'function') {
					onDone(null);
				}
			}
		});
	}
	//Функция показа модального окна с результатами тестирования pyprices. Вызывается по событию ready в document.
	function show_pyprices_instructions()
	{
		var epc_user_dismissed_pyprices_modal = <?php echo isset($_COOKIE['do_not_show_instructions_any_more']) ? 'true' : 'false'; ?>;
		epc_load_pyprices_health(function(answer) {
			var critical = !answer || answer.critical;
			if (critical || !epc_user_dismissed_pyprices_modal) {
				$('#pypriceTechRecomendation_Window').modal('show');
				if (critical) {
					var date = new Date(new Date().getTime() - 15552000 * 1000);
					document.cookie = "do_not_show_instructions_any_more=1; path=/; expires=" + date.toUTCString();
				}
			}
		});
	}
	// --------------------------------------------------------------------------------------
	//Обработка "Больше не показывать"
	function do_not_show_instructions_any_more()
	{
		//Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "do_not_show_instructions_any_more=1; path=/; expires=" + date.toUTCString();
		
		//Закрываем окно
		$('#pypriceTechRecomendation_Window').modal('hide');
	}
	// --------------------------------------------------------------------------------------
	//Функция запуска деплоя pyprices
	function pyprices_deploy()
	{
		//Показываем индикацию выполнения
		document.getElementById('pypriceTechRecomendation_Window_body').innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-pulse"></i><br><?php echo translate_str_by_id(5421); ?>...<br><?php echo translate_str_by_key('1708339287_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></div>';
		
		//Footer модального окна скрываем
		document.getElementById('pypriceTechRecomendation_Window_body_footer').setAttribute('style', 'display:none;');
		
		
		//Отправляем запрос на серверный скрипт деплоя
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/pyprices_deploy.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer){
				
				//Если не удалось развернуть - показываем ошибку. Если удалось - нужно будет перезагрузить эту страницу
				
				//Переводим ответ в объект
				var answer_ob = new Object;
				try
				{
					answer_ob = JSON.parse(answer);
					
					if( typeof answer_ob['status'] == 'undefined' )
					{
						document.getElementById('pypriceTechRecomendation_Window_body').innerHTML = "<?php echo translate_str_by_id(5422); ?>";
					}
					else
					{
						if( answer_ob['status'] == true )
						{
							document.getElementById('pypriceTechRecomendation_Window_body').innerHTML = '';
							alert("<?php echo translate_str_by_id(5423); ?>");
							location = location;
						}
						else
						{
							document.getElementById('pypriceTechRecomendation_Window_body').innerHTML = answer_ob['message'];
						}
					}
				}
				catch(err)
				{
					document.getElementById('pypriceTechRecomendation_Window_body').innerHTML = err.message;
				}
				
				
			}//END - обработка ответа от pyprices
		});
		
	}
	// --------------------------------------------------------------------------------------
	</script>
	<!-- END - Подсказки и инструкции для pyprices, тестирование работы, автоматический деплой pyprices -->
	
	
	
	
	
	
	
	<div class="epc-prices-page">
	<div class="col-lg-12">
		<div class="hpanel epc-prices-toolbar">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/price">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<a class="panel_a" href="javascript:void(0);" onclick="deletePrices();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/prices_edit">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_edit.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(4651); ?></div>
				</a>

				<a class="panel_a" href="javascript:void(0);" onclick="epcShowAllPriceUploadHistory();" title="Upload history — all price lists">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/excel.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption">Upload history</div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/commerce" title="Sales / purchase / inventory Excel → *-S *.P *-L warehouse lists">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/excel.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption">Commerce data (S / P / L)</div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/guide" title="All upload methods, e-mail per list, upload history">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption">Upload guide &amp; status</div>
				</a>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>

	<?php require $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/prices_upload/epc_storefront_storage_panel.php'; ?>
	
	
    
    
    
    
    
    
    
    <!-- Блок удаления прайс-листов -->
    <form name="delete_prices_form" method="POST">
        <input type="hidden" name="action" value="delete_prices" />
        <input type="hidden" name="prices" id="prices_to_delete" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <script>
        //Удаление прайс-листов
        function deletePrices()
        {
            var prices = getCheckedElements();
            
            if(prices.length == 0)
            {
                alert("<?php echo translate_str_by_id(3760); ?>");
                return;
            }
            if(!confirm("<?php echo translate_str_by_id(3761); ?>"))
            {
                return;
            }
            
            
            
            
            document.getElementById("prices_to_delete").value = JSON.stringify(prices);
            document.forms["delete_prices_form"].submit();
        }
    </script>
    
    
    
    
    
    
    
    
    
    
    <?php
    //Получим способы загрузки прайс-листов
    $load_modes = array();
	$load_modes_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices_load_modes`");
    $load_modes_query->execute();
    while($load_mode = $load_modes_query->fetch() )
    {
        $load_modes[$load_mode["id"]] = $load_mode["name"];
    }
    ?>
    
	
	<?php
	//Типы файлов прайс-листов, которые можно загружать с ПК
	$file_type_allowed = array('csv', 'txt', 'xls', 'xlsx', 'rar', 'zip', '7z', 'tar');
	$file_type_checking_js = "";//Строка проверки файла для JS
	$file_types_info_str = "";
	foreach($file_type_allowed AS $key=>$file_type)
	{
		if( $file_type_checking_js != "" )
		{
			$file_type_checking_js .= " && ";
			$file_types_info_str .= ", ";
		}
		$file_type_checking_js .= "fextension != '".$file_type."'";
		
		
		$file_types_info_str .= $file_type;
	}
	?>
	
    
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3762); ?>
			</div>
			<div class="panel-body">
				<p class="alert alert-info" style="margin-bottom:15px;">
					<strong>Multiple currencies:</strong> Amounts in uploaded files are in the <strong>currency of the storage</strong> linked to each price list (see <em>Shop → Logistics → Storages</em> → currency). The storefront converts to the shop currency using <em>Shop → Finance → Currency rates</em> (e.g. AED vs USD).
				</p>
				<div class="table-responsive">
					
					<script>
					//Массив для хранения настроек всех прайс-листов в Javascript
					var docpart_prices = new Array();
					
					var prices_indicate_items = new Object;//Объект для индикации результата обработки заданий. Ключ price_<price_id>. Значение - объект task (копируется сюда, чтобы не искать его в tasks)
					
					//Вспомогательный массив, в котором храним нормальное состояние кнопок колонки "Ручное обновление"
					var prices_manual_upgrading_buttons = new Object;
					
					var files_uploads = new Array();//Массив для учета отправленных на загрузку файлов. Элемент массива - объект, содержащий price_id, имя файла, флаг о завершенности процесса загрузки файла. Этот массив используется для индикации ошибки - если файл не загрузится, то, флаг completed не установится в true в функции success и тогда функция oncomplete поймет, что произошла ошибка.
					
					var files_uploads_count = 0;//Счетчик загрузок файлов для их идентификации
					</script>
					
					<?php
					//Массивы для JS с id элементов и с чекбоксами элементов
					$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
					$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
					
					
					epc_prices_ensure_listing_indexes($db_link);
					$elements_query = epc_prices_fetch_lists_query($db_link);
					
					
					$elements_count_rows_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_docpart_prices`;');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					
					// Склады привязанные к прайс листам
					$shop_storages_link_prices = array();
					$link_prices_query = $db_link->prepare("SELECT * FROM `shop_storages` WHERE `interface_type` = 2;");
					$link_prices_query->execute();
					while($record = $link_prices_query->fetch())
					{
						$connection_options = json_decode($record["connection_options"], true);
						if(!empty($connection_options['price_id']))
						{
							//Один прайс-лист может быть привязан к нескольким складам
							if( isset($shop_storages_link_prices[$connection_options['price_id']]) )
							{
								$shop_storages_link_prices[$connection_options['price_id']] = $shop_storages_link_prices[$connection_options['price_id']]."<br>";
							}
							
							$shop_storages_link_prices[$connection_options['price_id']] = $shop_storages_link_prices[$connection_options['price_id']] . $record['id'].' - '.$record['name'];
						}
					}

					require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
					epc_price_history_ensure_schema($db_link);
					$epc_latest_uploads = epc_price_history_get_latest_map($db_link);
					$epc_history_download_base = "/".$DP_Config->backend_dir."/content/shop/prices_upload/ajax_epc_price_upload_history.php";
					?>
					
					
					
					<table cellpadding="1" cellspacing="1" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $elements_count_rows; ?>" id="prices_table">
					
					<!--<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">-->
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th data-toggle="true"></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
								<th class="text-left"><?php echo translate_str_by_id(5424); ?></th>
								<th><?php echo translate_str_by_id(3763); ?></th>
								<th><?php echo translate_str_by_id(5425); ?></th>
								<th>Update file / history</th>
								<!--<th><?php //echo translate_str_by_id(3709); ?></th>-->
								<th class="text-center"><?php //echo translate_str_by_id(3212); ?> <?php echo translate_str_by_id(5426); ?></th>
								<th class="text-left"><?php echo translate_str_by_id(5427); ?></th>
								<th class="text-center"><?php echo translate_str_by_id(2113); ?></th>
								<th style="text-align:center;" data-hide="phone,tablet,default"></th>
							</tr>
						</thead>
						<tbody>
						<?php
						//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
						//---------------------------------------------------------------------------------------------->
						/*
						//Определяем количество страниц для вывода:
						$p = $elements_count_rows;//Штук на страницу (решили, что провильно показать все прайс-листы)
						if( !$p )
						{
							$p = $DP_Config->list_page_limit;//Штук на страницу
						}
						$count_pages = (int)($elements_count_rows / $p);//Количество страниц
						if($elements_count_rows%$p)//Если остались еще элементы
						{
							$count_pages++;
						}
						//Определяем, с какой страницы начать вывод:
						$s_page = 0;
						if(!empty($_GET['s_page']))
						{
							$s_page = $_GET['s_page'];
						}
						*/
						//----------------------------------------------------------------------------------------------|
						
						
						//for($i=0, $d=0; $i<$elements_count_rows && $d<$p; $i++, $d++)
						while( $element_record = $elements_query->fetch() )
						{
							//$element_record = $elements_query->fetch();
							?>
							<script>
							//Добавляем данный прайс-лист в Javascript
							docpart_prices.push( JSON.parse('<?php echo json_encode($element_record); ?>') );
							
							//Создаем объект для индикации этого прайс-листа
							prices_indicate_items['price_<?php echo (int)$element_record["id"]; ?>'] = new Object;
							</script>
							
							
							<?php
							//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
							if($i < $s_page*$p)
							{
								$d--;
								continue;
							}
							
							//Для Javascript
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
							
							//Последнее обновление
							if($element_record["last_updated"] == 0)
							{
								$element_record["last_updated"] = translate_str_by_id(3765);
							}
							else
							{
								$element_record["last_updated"] = date("d.m.Y H:i:s", $element_record["last_updated"]);
							}
							?>
							
							<?php
							$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/shop/prices/price?price_id=".$element_record["id"]." \">";
							?>
						
						
							<tr onclick="get_price_preview_<?php echo $element_record["id"]; ?>();">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
								<td></td>
								<td><?php echo $a_item.$element_record["id"]; ?></a></td>
								<td><?php echo $a_item.$element_record["name"]; ?></a></td>
								<td class="text-left">
									<?php
									if(isset($shop_storages_link_prices[$element_record["id"]]))
									{
										?>
										
										<?php echo $a_item.$shop_storages_link_prices[$element_record["id"]]; ?></a>
										
										<?php
									}
									else
									{
										echo $a_item;
										?>
										<span class="epc-price-wh-empty">Not linked</span></a>
										<?php
									}
									?>
								
									
								</td>
								<td>
									<!-- Здесь тег a нужен с ID (для указания времени обновления) -->
									<a id="last_updated_<?php echo $element_record["id"]; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/price?price_id=<?php echo $element_record["id"]; ?>"><?php echo $element_record["last_updated"]; ?></a>
								</td>
								<td>
									<!-- Здесь тег a нужен с ID (для указания количества строк после обновления) -->
									<a id="records_count_<?php echo $element_record["id"]; ?>" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/price?price_id=<?php echo $element_record["id"]; ?>"><?php echo $element_record["records_count"]; ?></a>
								</td>
								<td class="text-left epc-price-hist-cell">
									<?php
									$epc_latest = $epc_latest_uploads[(int)$element_record['id']] ?? null;
									// Avoid is_file() per row on page load (NFS/disk probes add up on large CP pages).
									$epc_has_file = $epc_latest && trim((string)$epc_latest['stored_relpath']) !== '';
									$epc_dl = $epc_history_download_base.'?action=download_latest&price_id='.(int)$element_record['id'].'&csrf_guard_key='.urlencode($user_session['csrf_guard_key']);
									$epc_db_dl = $epc_history_download_base.'?action=export_db&price_id='.(int)$element_record['id'].'&csrf_guard_key='.urlencode($user_session['csrf_guard_key']);
									if ($epc_latest) {
										?>
										<a class="btn btn-xs btn-success" href="<?php echo htmlspecialchars($epc_dl, ENT_QUOTES, 'UTF-8'); ?>" title="<?php echo $epc_has_file ? 'Download active upload file' : 'Download current prices (DB export — archive missing)'; ?>" onclick="event.stopPropagation();">
											<i class="fas fa-download"></i>
										</a>
										<span class="epc-price-hist-meta">
											<?php echo htmlspecialchars(mb_substr((string)$epc_latest['original_filename'], 0, 28, 'UTF-8'), ENT_QUOTES, 'UTF-8'); ?><br>
											<?php echo htmlspecialchars((string)$epc_latest['created_at'], ENT_QUOTES, 'UTF-8'); ?>
											<?php if (!$epc_has_file): ?><br><span class="text-warning">archive missing → DB export</span><?php endif; ?>
										</span>
										<?php
									} else {
										?>
										<a class="btn btn-xs btn-default" href="<?php echo htmlspecialchars($epc_db_dl, ENT_QUOTES, 'UTF-8'); ?>" title="Export current DB prices" onclick="event.stopPropagation();">
											<i class="fas fa-database"></i>
										</a>
										<span class="epc-price-badge-empty">No archive yet</span>
										<?php
									}
									?>
									<a class="epc-price-hist-link" href="javascript:void(0);" onclick="event.stopPropagation();epcShowPriceUploadHistory(<?php echo (int)$element_record['id']; ?>, '<?php echo htmlspecialchars($element_record['name'], ENT_QUOTES, 'UTF-8'); ?>');">
										<i class="fas fa-history"></i> Upload history
									</a>
								</td>
								<!--<td><?php //echo $a_item.translate_str_by_id($load_modes[$element_record["load_mode"]]); ?></a></td>-->
								<!--<td class="text-left"><?php //echo $a_item.$load_modes[$element_record["load_mode"]]; ?></a></td>-->
								<td class="text-left">
									<!-- Переход на старую страницу
									<a class="btn btn-ar btn-success" href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/upload?price_id=<?php echo $element_record["id"]; ?>">
										<?php echo translate_str_by_id(3181); ?>
									</a>
									-->
									
									<!-- Для загрузки файла с ПК (отдельная форма для каждого прайс-листа) -->
									<form method="POST" enctype="multipart/form-data" style="display:none;" id="file_form_<?php echo $element_record['id']; ?>">
										<input type="hidden" name="price_id" value="<?php echo $element_record['id']; ?>" />
										<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
										<input id="file_<?php echo $element_record['id']; ?>" name="file_<?php echo $element_record['id']; ?>" type="file" />
										
										<!-- ID загрузки файла -->
										<input type="hidden" name="upload_id" id="upload_id_<?php echo $element_record['id']; ?>"  value="" />
									</form>
									<script>
									//Обработка выбора файла в инпуте
									jQuery('#file_<?php echo $element_record['id']; ?>').on('change', function () {
										
										//Выбранный файл
										var file = this.files[0];
										
										//Если пользователь нажал "Отмена" в окне выбора файла
										if( ! file )
										{
											return;
										}
										
										//Имя файла
										var fname = file.name;
										
										//Расширение файла
										var fextension = fname.slice((Math.max(0, fname.lastIndexOf(".")) || Infinity) + 1);
										
										
										//Проверяем расширение файла - для пользователя. Более серьезная проверка будет на сервере
										if(<?php echo $file_type_checking_js; ?>)
										{
											alert('<?php echo translate_str_by_id(5428); ?> <?php echo $file_types_info_str; ?>')
											return;
										}
										else
										{
											//Расширение допустимо
											console.log("Тут начинаем загрузку файла на сервер");
											
											//Показываем индикацию загрузки файла
											document.getElementById('price_manual_upgrading_<?php echo $element_record['id']; ?>').innerHTML = "<i class=\"fas fa-spinner fa-pulse\"></i> <?php echo translate_str_by_id(5429); ?>...";
											
											
											
											//Добавляем объект в массив учета загрузки файлов
											files_uploads_count++;
											var file_upload = new Object;
											file_upload.upload_id = files_uploads_count;//ID загрузки
											file_upload.price_id = <?php echo $element_record['id']; ?>;
											file_upload.file_name = file.name;
											file_upload.completed = false;//Загрузка не завершена
											files_uploads.push(file_upload);
											
											//В отправляемую форму указываем upload_id, чтобы потом понять, по какому файлу пришел ответ от сервера
											document.getElementById('upload_id_<?php echo $element_record['id']; ?>').value = file_upload.upload_id;
											
											//Передаем файл на сервер
											jQuery.ajax({
												
												url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/upload_file.php',
												
												type: 'POST',

												data: new FormData(jQuery('#file_form_<?php echo $element_record['id']; ?>')[0]),

												cache: false,
												contentType: false,
												processData: false,
												
												dataType: "text",//Тип возвращаемого значения
												
												file_upload_id:file_upload.upload_id,//Для oncomplete (для обработки ошибок)
												
												success: function(answer)
												{
													//После отработки php-скрипта пришел ответ
													
													console.log(answer);
													
													//Переводим ответ в объект
													var answer_ob = new Object;
													try
													{
														answer_ob = JSON.parse(answer);
													}
													catch(err)
													{
														alert(err.message);
														return;
													}
													
													//Если нет status, значит ошибка парсинга ответа. Не можем определить, по какому файлу пришел ответ.
													if( typeof answer_ob['status'] == 'undefined' )
													{
														console.log(answer);
														alert('<?php echo translate_str_by_id(5430); ?>');
														return;
													}
													
													
													if( answer_ob['status'] == true )
													{
														//Помечаем, что задача на загрузку файла выполнена.
														//По массиву начатых загрузок файлов
														for( var i = 0 ; i < files_uploads.length ; i++ )
														{
															//Нашли задание на загрузку файла
															if( parseInt(files_uploads[i].upload_id) == parseInt(answer_ob['upload_id']) )
															{
																//ФАЙЛ УСПЕШНО ЗАРУЖЕН.
																
																//Пометили, что файл загружен (чтобы oncomplete понимал, что ошибки нет)
																files_uploads[i].completed = true;

																//Создаем задание ротоколу pyprices. Указываем полный путь к загруженному файлу
																var task = prepare_task(<?php echo $element_record['id']; ?>, 'local_path', answer_ob.file_dir);
																
																//Дополнительно в задании, отдельно указываем уникальное имя временной папки, где сейчас находится загруженный файл (чтобы после обработки задания эту папку удалить вместе с файлом)
																task.tmp_folder_name = answer_ob['tmp_folder_name'];
																
																
																if( task.client_task_id > 0 )
																{
																	//Файл на сервере. Задание успешно подготовлено. Запускаем задание на обработку в pyprices
																	start_pyprices(task);
																}
																else
																{
																	//Индикация ошибки
																	var indicator = new Object;
																	indicator.other_error = "<?php echo translate_str_by_id(5431); ?>";
																	indicator.price_id = <?php echo $element_record['id']; ?>;
																	indicator.price_name = '<?php echo $element_record['name']; ?>';
																	prices_indicate_items['price_' + indicator.price_id] = indicator;
																	indicate_task_result(indicator.price_id);
																	
																	del_tmp_folder(task);//Удаляем временную папку
																}
																break;
															}
														}
													}
													else
													{
														alert("<?php echo translate_str_by_id(5432); ?>: " + answer_ob['message']);
													}
													
													//Если возникла какая-либо ошибка при загрузке файла, то в массиве учета загрузок files_uploads не проставится completed=true и тогда ошибка обработается далее в обработчике ajaxComplete, а само задание в pyprices не будет отправлено.
												}
											});
											return;
										}
									});
									</script>
									
									
									
									<div id="price_manual_upgrading_<?php echo $element_record['id']; ?>">
										
										<!-- При клике - сбрасываем значение у файлового инпута и имитируем клик по нему для начала выбора файла -->
										<a href="javascript:void(0);" onclick="jQuery('#file_<?php echo $element_record['id']; ?>').val('');jQuery('#file_<?php echo $element_record['id']; ?>').click();" title="<?php echo translate_str_by_id(5433); ?>"><i class="fas fa-desktop"></i></a> 
										
										<?php
										//Если доступна загрузка из других источников. Добавляем кнопки, при клике на которые формируется соответствующее задание и отправляется в pyprices
										switch($element_record["load_mode"])
										{
											case 2:
												?>
												<a href="javascript:void(0);" onclick="start_pyprices(prepare_task(<?php echo $element_record['id']; ?>, 'ftp'));" title="<?php echo translate_str_by_id(5434); ?>" style="margin-left:7px;"><i class="fas fa-server"></i></a> 
												<?php
												break;
											case 3:
												?>
												<a href="javascript:void(0);" onclick="start_pyprices(prepare_task(<?php echo $element_record['id']; ?>, 'email'));" title="<?php echo translate_str_by_id(5435); ?>" style="margin-left:7px;"><i class="far fa-envelope"></i></a> 
												<?php
												break;
											case 4:
												?>
												<a href="javascript:void(0);" onclick="start_pyprices(prepare_task(<?php echo $element_record['id']; ?>, 'url'));" title="<?php echo translate_str_by_id(5436); ?>" style="margin-left:7px;"><i class="fas fa-link"></i></a>
												<?php
												break;
										}
										?>
									</div>
									<script>
									//Запоминаем содержимое дива выше, т.е. запоминается HTML этих кнопок, чтобы потом их можно было восстанавливать после завершения загрузки файлов или выполнения задания на обработку прайс-листов.
									prices_manual_upgrading_buttons['price_id_<?php echo $element_record['id']; ?>'] = document.getElementById('price_manual_upgrading_<?php echo $element_record['id']; ?>').innerHTML;
									</script>
									
									<!-- Сюда запишем HTML для индикации последнего обновления этого прайс-листа - так, чтобы при открытии страницы пользователь сразу увидел -->
									<div id="last_update_single_time_history_<?php echo $element_record['id']; ?>" style="display:none;"<?php if (epc_prices_defer_inline_update_history()) { ?> data-epc-lazy-history="1" data-price-id="<?php echo (int)$element_record['id']; ?>"<?php } ?>>
									<?php
									if (!epc_prices_defer_inline_update_history()) {
										require($_SERVER['DOCUMENT_ROOT']."/".$DP_Config->backend_dir."/content/shop/prices_upload/for_pyprices/get_update_history.php");
									}
									?>
									</div>
									<script>
									<?php if (!epc_prices_defer_inline_update_history()) { ?>
									document.getElementById('price_manual_upgrading_<?php echo $element_record['id']; ?>').innerHTML += document.getElementById('last_update_single_time_history_<?php echo $element_record['id']; ?>').innerHTML;
									<?php } ?>
									</script>
								</td>
								
								<td class="text-left">
									<?php
									//Если у прайса нет настроек E-mail/FTP/URL, то, настройка crontab для него не доступна
									if($element_record["load_mode"] == 1)
									{
										?>

										<i class="fas fa-plus" style="color:#DDD;" title="<?php echo translate_str_by_id(5437); ?>"></i>
										
										
										<i class="far fa-clock" style="color:#DDD;" title="<?php echo translate_str_by_id(5437); ?>"></i>
										<?php
									}
									else
									{
										?>
										<a href="javascript:void(0);" onclick="cron_task_open(<?php echo $element_record['id']; ?>);" title="<?php echo translate_str_by_id(5438); ?>">
											<i class="fas fa-plus"></i>
										</a>

										<a id="cron_tasks_list_open_a_<?php echo $element_record['id']; ?>" href="javascript:void(0);" title="<?php echo translate_str_by_id(5439); ?>" onclick="cron_tasks_list_open(<?php echo $element_record['id']; ?>);">
											<?php
											$i_for_not_specified = "far";//Класс fontawesome для прайса, у которого не настроены задания по расписанию (crontab)
											if($element_record["cron_tasks_count"] > 0)
											{
												$i_for_not_specified = "fas";
											}
											?>
											<i class="<?php echo $i_for_not_specified; ?> fa-clock"></i>
										</a>
										
										<?php
									}
									?>	
								</td>
								
								<td class="text-left">
									<?php echo $a_item; ?><i style="margin-right:10px;" title="<?php echo translate_str_by_id(4652); ?>" class="fa fas fa-pencil-alt"></i></a>
									<a href="/<?php echo $DP_Config->backend_dir; ?>/shop/prices/review?price_id=<?php echo $element_record["id"]; ?>" title="<?php echo translate_str_by_id(3766); ?>"><i class="fas fa-sync"></i></a>
									
									
									
									
									
									
									<a href="javascript:void(0);" onclick="epcShowPriceUploadHistory(<?php echo $element_record["id"]; ?>, '<?php echo htmlspecialchars($element_record["name"], ENT_QUOTES, 'UTF-8'); ?>');" title="Upload file history (download, stats)"><i class="fas fa-file-upload" style="margin-left:10px;color:#2980b9;"></i></a>

									<!-- START Показ истории обновлений -->
									<a href="javascript:void(0);" onclick="show_update_history_<?php echo $element_record["id"]; ?>();"><i class="fas fa-history" title="<?php echo translate_str_by_id(5440); ?>" style="margin-left:10px;"></i></a>
									<script>
									// ----------------------------------------------------------------------------
									//Показать окно с таблицей истории заданий для данного прайс-листа
									function show_update_history_<?php echo $element_record["id"]; ?>()
									{
										//Индикатор загрузки
										document.getElementById("update_history_modal_window_body_<?php echo $element_record["id"]; ?>").innerHTML = "<div style=\"text-align:center;\"><i class=\"fas fa-spinner fa-pulse\"></i><br><?php echo translate_str_by_id(2182); ?>...</div>";
										
										//Показываем окно, в котором будет таблица с историей заданий на обновление данного прайс-листа.
										$('#update_history_modal_window_<?php echo $element_record["id"]; ?>').modal('show');

										//AJAX-вызов - для получения таблицы с историей заданий по данному прайс-листу
										jQuery.ajax({
											type: "POST",
											async: true,
											url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/get_update_history.php",
											dataType: "text",//Тип возвращаемого значения
											data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&price_id=<?php echo $element_record["id"]; ?>",
											success: function(answer)
											{
												document.getElementById("update_history_modal_window_body_<?php echo $element_record["id"]; ?>").innerHTML = answer;
											}
										});
									}
									// ----------------------------------------------------------------------------
									</script>
									<div class="modal fade in" id="update_history_modal_window_<?php echo $element_record["id"]; ?>" tabindex="-1" role="dialog" aria-hidden="true">
										<div class="modal-dialog modal-lg">
											<div class="modal-content">
												<div class="color-line"></div>
												<div class="modal-header">
													<h4 class="modal-title"><?php echo translate_str_by_id(5441); ?> ID <?php echo $element_record["id"]; ?> "<?php echo $element_record["name"]; ?>"</h4>
												</div>
												<div class="modal-body" id="update_history_modal_window_body_<?php echo $element_record["id"]; ?>">
													<?php echo translate_str_by_id(5442); ?>
												</div>
												<div class="modal-footer">
													<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
												</div>
											</div>
										</div>
									</div>
									<!-- END Показ истории обновлений -->
								</td>
								
								
								<td style="text-align:center;">
									
									<div class="price_preview_<?php echo $element_record["id"]; ?>">
									</div>
									
								</td>
								<script>
								function get_price_preview_<?php echo $element_record["id"]; ?>()
								{
									jQuery(".price_preview_<?php echo $element_record["id"]; ?>").html("<div class=\"text-center\"><?php echo translate_str_by_id(3767); ?>.<br><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><?php echo translate_str_by_id(2182); ?>...</div>");


									jQuery.ajax({
										type: "POST",
										async: true, //Запрос синхронный
										url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/ajax_get_price_preview.php",
										dataType: "text",//Тип возвращаемого значения
										data: "price_id=<?php echo $element_record["id"]; ?>&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
										success: function(answer)
										{
											jQuery(".price_preview_<?php echo $element_record["id"]; ?>").html(answer);
										}
									}); 
								}
								</script>
								
							</tr>
						<?php
						}//for
						?>
						</tbody>
						<tfoot style="display:none;"><tr><td><ul class="pagination"></ul></td></tr></tfoot>
					</table>
				</div>
				
				
				<?php
				//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				/*
				if( $count_pages > 1 )
				{
					?>
					<div class="row">
						<div class="col-lg-12 text-center">
							<div class="dataTables_paginate paging_simple_numbers">
								<ul class="pagination">
								<?php
								for($i=0; $i < $count_pages; $i++)
								{
									//Класс первой страницы
									$previous = "";
									if($i == 0) $previous = "previous";
									
									//Класс последней страницы
									$next = "";
									if($i == $count_pages-1) $next = "next";
									
									if($i == $s_page)//Текущая страница
									{
										?>
										<li class="paginate_button active <?php echo $previous; ?> <?php echo $next; ?>"><a href="javascript:void(0);"><?php echo $i; ?></a></li>
										<?php
									}
									else
									{
										?>
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/prices?s_page=$i"; ?>"><?php echo $i; ?></a></li>
										<?php
									}
								}
								?>
								</ul>
							</div>
						</div>
					</div>
				<?php
				}
				*/
				//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				?>
				
				
			</div>
			
			<div class="panel-footer epc-prices-actions">
				
				<button class="btn btn-primary" type="button" onclick="execute_checked();">
					<i class="fas fa-redo"></i> <span class="bold"><?php echo translate_str_by_id(5443); ?></span>
				</button>
				<script>
				// --------------------------------------------------------------------------------------
				//Типы источников файлов
				var load_modes = new Object;
				load_modes['load_mode_2'] = 'ftp';
				load_modes['load_mode_3'] = 'email';
				load_modes['load_mode_4'] = 'url';
				// --------------------------------------------------------------------------------------
				//Функция запуска обновления по выбранным прайс-листам
				function execute_checked()
				{
					//Получаем выбранные
					var prices_checked = getCheckedElements();
					
					if( prices_checked.length == 0 )
					{
						alert('<?php echo translate_str_by_id(5444); ?>');
						return;
					}
					
					/*
					Фильтруем:
					- те, у которых только mode == 1 (т.е. НЕ E-mail/FTP/URL)
					- те, которые в данный момент уже обновляются
					*/
					
					//Последоваьельно создаем задания и тут же отправляем каждое задание
					var count_sent = 0;//Количество запущенных заданий
					
					//Цикл по отмеченным
					for( var i = 0 ; i < prices_checked.length ; i++ )
					{
						//Цикл по всем
						for( var p = 0 ; p < docpart_prices.length ; p++ )
						{
							if( parseInt(docpart_prices[p].id) == parseInt(prices_checked[i]) )
							{
								//Нашли прайс-лист
								if( parseInt(docpart_prices[p].load_mode) == 1 )
								{
									//В качестве источника данного прайс-листа указан не E-mail/FTP/URL
									break;//p (данный прайс-лист не подходит)
								}
								
								//Источник подходящий. Проверяем, нет ли для него выполняющихся заданий
								var is_active_task = false;//Флаг - если ли активные задания
								//Цикл по запущенным заданиям
								for( var t = 0 ; t < tasks.length ; t++ )
								{
									if( parseInt(tasks[t].price_id) == parseInt(prices_checked[i]) )
									{
										if( tasks[t].completed != true )
										{
											//Для данного прайс-листа уже идет обновление
											is_active_task = true;
											break;//t
										}
									}
								}
								if(is_active_task)
								{
									break;//p (данный прайс-лист не подходит)
								}
								
								//Для данного прайс-листа нет незавершенных заданий
								
								//Запускаем
								start_pyprices( prepare_task(prices_checked[i], load_modes['load_mode_'+docpart_prices[p].load_mode]) );
								count_sent++;
								
								break;
							}
						}
					}
					
					
					if( count_sent == 0 )
					{
						alert('<?php echo translate_str_by_id(5445); ?>');
					}
				}
				// --------------------------------------------------------------------------------------
				</script>
				
				
				
				
				
				<button class="btn btn-info" type="button" onclick="execute_checked_multi();">
					<i class="fas fa-redo"></i> <span class="bold"><?php echo translate_str_by_id(5446); ?></span>
				</button>
				<script>
				// --------------------------------------------------------------------------------------
				/*
				Соображения по мультизаданиям.
				
				Создаем глобальный массив учета отправленных мультизаданий multi_tasks (по аналогии с tasks).
				
				При вызове функции execute_checked_multi():
				1. Создаем объект multi_task. У него уникальный ID
				
				2. Для каждого выбранного прайса создаем задание task - для обработки для pyprices:
				- это задание добавляем в массив tasks (будет означать, что для данного прайс-листа теперь есть выполняемое задание)
				- это задание добавляем в массив multi_task.list_to_handle (массив для запроса в pypices)
				
				3. Добавляем multi_task в multi_tasks
				
				
				4. Затем идет вызов pyprices с передачей multi_task.list_to_handle и указанием settings.multi_task_id. Обработку результата лучше сразу делать в oncomplete:
				- определяем по какому мультитаску пришел ответ. По нему можем определить перечень заданий task и найти их в массиве tasks
				- парсим JSON (из textResponse) и указываем результат по каждому прайс-листу отдельно
				- если распарсить JSON не удалось, то, по каждому task из tasks по данному мультизаданию индицируем ошибку и сообщение о невозможности определить статус обработки
				*/
				// --------------------------------------------------------------------------------------
				var multi_tasks = new Array();//Массив учета отправленных мультизаданий multi_tasks (по аналогии с tasks)
				var multi_tasks_counter = 0;//Счетчик мультизаданий для идентификации
				// --------------------------------------------------------------------------------------
				function execute_checked_multi()
				{
					var multi_task = new Object;//Создаем объект мультизадания
					multi_task.list_to_handle = new Array();//Сюда будем добавлять задания task
					
					//Получаем выбранные прайс-листы
					var prices_checked = getCheckedElements();
					if( prices_checked.length == 0 )
					{
						alert('<?php echo translate_str_by_id(5444); ?>');
						return;
					}
					
					/*
					Фильтруем:
					- те, у которых только mode == 1 (т.е. НЕ E-mail/FTP/URL)
					- те, которые в данный момент уже обновляются
					*/
					
					//Цикл по отмеченным прайс-листам
					for( var i = 0 ; i < prices_checked.length ; i++ )
					{
						//Цикл по всем прайс-листам
						for( var p = 0 ; p < docpart_prices.length ; p++ )
						{
							if( parseInt(docpart_prices[p].id) == parseInt(prices_checked[i]) )
							{
								//Нашли прайс-лист
								if( parseInt(docpart_prices[p].load_mode) == 1 )
								{
									//В качестве источника данного прайс-листа указан не E-mail/FTP/URL
									break;//p (данный прайс-лист не подходит)
								}
								
								//Источник подходящий. Проверяем, нет ли для него выполняющихся заданий
								var is_active_task = false;//Флаг - нет незавершенных заданий
								//Цикл по запущенным заданиям
								for( var t = 0 ; t < tasks.length ; t++ )
								{
									if( parseInt(tasks[t].price_id) == parseInt(prices_checked[i]) )
									{
										if( tasks[t].completed != true )
										{
											//Для данного прайс-листа уже идет обновление
											is_active_task = true;//Есть незавершенные задания
											break;//t
										}
									}
								}
								//Есть активное задание - этот прайс-лист не подходит.
								if(is_active_task)
								{
									break;//p (данный прайс-лист не подходит)
								}
								
								//Для данного прайс-листа нет незавершенных заданий
								
								//Создаем задание
								var task = prepare_task(prices_checked[i], load_modes['load_mode_'+docpart_prices[p].load_mode]);
								
								if( task.client_task_id > 0 )
								{
									//Добаляем его в tasks (нужно в том числе для учета незавершенных заданий)
									tasks.push(JSON.parse(JSON.stringify(task)));
									//Добавляем его в объект мультизадания
									multi_task.list_to_handle.push(JSON.parse(JSON.stringify(task)));
								}
								

								break;//p (прайс подошел)
							}
						}
					}
					//Если в итоге нет заданий, добавленных в multi_task.list_to_handle
					if( multi_task.list_to_handle.length == 0 )
					{
						alert('<?php echo translate_str_by_id(5447); ?>');
						return;
					}
					
					
					//Дошли до сюда, значит в объекте мультизадания есть задания для pyprices
					//Назначаем ID для мультизадания
					multi_tasks_counter++;
					multi_task.multi_task_id = multi_tasks_counter;
					
					
					//Добавляем объект мультизадания в массив учета мультизаданий
					multi_tasks.push(multi_task);
					
					
					//Индикация обработки по каждому заданию в мультизадании
					for( var i = 0 ; i < multi_task.list_to_handle.length ; i++ )
					{
						//Указываем пиктограмму loading
						document.getElementById('price_manual_upgrading_' + multi_task.list_to_handle[i].price_id ).innerHTML = "<i class=\"fas fa-spinner fa-pulse\"></i> <?php echo translate_str_by_id(5448); ?>...";
					}
					
					
					
					//Вызов pyprices
					//Отправляем запрос на обработку задания
					jQuery.ajax({
						type: "POST",
						async: true, //Запрос асинхронный
						url: "/pyprices/pyprices-api.php",
						dataType: "text",//Тип возвращаемого значения
						data: "key=" + encodeURIComponent('<?php echo $DP_Config->tech_key; ?>') + "&list_to_handle="+encodeURIComponent( JSON.stringify(multi_task.list_to_handle) ),
						multi_task_id: multi_task.multi_task_id,//Для oncomplete
						success: function(answer)
						{
							console.log("<?php echo translate_str_by_id(5449); ?>");
							return;
						}
					});
				}
				// --------------------------------------------------------------------------------------
				</script>
				
				<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(5450); ?>');"><i class="fa fa-info"></i></button>
				
				
				
				
				
				
				
				
				
				
				
				<!-- START - Настройка заданий по расписанию -->
				<script src="/lib/multiple_select/jquery.multiple.select.js"></script>
				<link href="/lib/multiple_select/multiple-select.css" rel="stylesheet">
				<script src="/lib/datetimepicker/jquery.datetimepicker.js" type="text/javascript"></script>
				<link href="/lib/datetimepicker/jquery.datetimepicker.css" rel="stylesheet">
				<button class="btn btn-danger" type="button" onclick="cron_task_open();">
					<i class="far fa-clock"></i> <span class="bold"><?php echo translate_str_by_id(5451); ?></span>
				</button>
				<button class="btn btn-warning" type="button" onclick="cron_tasks_list_open();">
					<i class="far fa-clock"></i> <span class="bold"><?php echo translate_str_by_id(5452); ?></span>
				</button>
				<script>
				// --------------------------------------------------------------------------------------
				//Функция обновления индикации наличия заданий по расписанию по всем прайс-листам на странице
				function cron_tasks_refresh_indications()
				{
					//AJAX-вызов
					jQuery.ajax({
						type: "POST",
						async: true,
						url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/for_cron/cron_tasks_actions.php",
						dataType: "text",//Тип возвращаемого значения
						data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&action=get_cron_tasks_count_by_prices",
						success: function(answer)
						{
							//Переводим ответ в объект
							var answer_ob = new Object;
							try
							{
								answer_ob = JSON.parse(answer);
								
								if( typeof answer_ob['status'] == 'undefined' )
								{
									alert("<?php echo translate_str_by_id(5453); ?>");
								}
								else
								{
									if( answer_ob['status'] == true )
									{
										//По всем прайс-листам на странице
										for( var i = 0; i < docpart_prices.length ; i++ )
										{
											//По всем прайс-листам из ответа
											for( var a = 0 ; a < answer_ob['count_array'].length ; a++ )
											{
												if( parseInt(docpart_prices[i].id) == parseInt(answer_ob['count_array'][a].price_id) )
												{
													if( parseInt(answer_ob['count_array'][a].cron_tasks_count) > 0 )
													{
														document.getElementById('cron_tasks_list_open_a_' + docpart_prices[i].id).innerHTML = '<i class="fas fa-clock"></i>';
													}
													else
													{
														document.getElementById('cron_tasks_list_open_a_' + docpart_prices[i].id).innerHTML = '<i class="far fa-clock"></i>';
													}
													break;
												}
											}
										}
									}
									else
									{
										alert( "<?php echo translate_str_by_id(5454); ?>" );
									}
								}
							}
							catch(err)
							{
								alert("<?php echo translate_str_by_id(5455); ?>");
							}
						}
					});
				}
				// --------------------------------------------------------------------------------------
				//Функция удаления задания по расписанию (price_id, order_by, asc_desc - это фильтры для последующего отображения таблицы заданий после удаления задания с cron_task_id)
				function delete_cron_task(cron_task_id, price_id = 0, order_by='id', asc_desc='ASC')
				{
					if( !confirm("<?php echo translate_str_by_id(5456); ?>") )
					{
						return;
					}
					
					//AJAX-вызов
					jQuery.ajax({
						type: "POST",
						async: true,
						url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/for_cron/cron_tasks_actions.php",
						dataType: "text",//Тип возвращаемого значения
						data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&cron_task_id="+cron_task_id+"&price_id="+price_id+"&order_by="+order_by+"&asc_desc="+asc_desc+"&action=delete_cron_task",
						success: function(answer)
						{
							//Переводим ответ в объект
							var answer_ob = new Object;
							try
							{
								answer_ob = JSON.parse(answer);
								
								if( typeof answer_ob['status'] == 'undefined' )
								{
									alert("<?php echo translate_str_by_id(5453); ?>");
								}
								else
								{
									if( answer_ob['status'] == true )
									{
										//Задание по расписанию успешно удалено - переотображаем таблицу с теми настройками, какие были до удаления задания
										cron_tasks_list_open(answer_ob['price_id'], answer_ob['order_by'], answer_ob['asc_desc'])
									}
									else
									{
										alert( answer_ob['message'] );
									}
								}
							}
							catch(err)
							{
								alert("<?php echo translate_str_by_id(5455); ?>");
							}
							
							
							//Обновление индикации наличия заданий по расписанию по всем прайс-листам
							cron_tasks_refresh_indications();
						}
					});
					
				}
				// --------------------------------------------------------------------------------------
				//Открыть окно со списком заданий по расписанию
				function cron_tasks_list_open(price_id = 0, order_by='id', asc_desc='ASC')
				{
					//AJAX-вызов
					jQuery.ajax({
						type: "POST",
						async: true,
						url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/for_cron/cron_tasks_actions.php",
						dataType: "text",//Тип возвращаемого значения
						data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&price_id="+encodeURIComponent( JSON.stringify(price_id) ) + "&order_by=" + order_by + "&asc_desc=" + asc_desc+"&action=get_cron_tasks_html_table",
						success: function(answer)
						{
							//Заголовок окна
							document.getElementById('modalCronWindow_h4').innerHTML = "<?php echo translate_str_by_id(5452); ?>";
							//Кнопка Создать/Сохранить одно задание - скрываем
							document.getElementById('create_edit_cron_task_button').setAttribute('style', 'display:none;');
							//Делаем НЕвидимой область для создания/редактирования задания по расписанию
							document.getElementById('modalCronWindow_workArea_taskCreateEdit').setAttribute('style', 'display:none;');
							//Делаем видимой область для списка всех заданий
							document.getElementById('modalCronWindow_workArea_tasksList').setAttribute('style', '');
							
							//Внопка "Список заданий" слева в footer - невидимая
							document.getElementById('modalCronWindow_footer_left_button_tasks_list').setAttribute('style', 'display:none;');
							
							
							
							//То, что вернул php-скрипт
							document.getElementById('modalCronWindow_workArea_tasksList').innerHTML = answer;
							
							
							//Показываем окно
							$('#modalCronWindow').modal('show');
						}
					});					
				}
				// --------------------------------------------------------------------------------------
				//Открыть окно для одного задания (создание/редактирование)
				function cron_task_open(price_id = 0, cron_task_id = 0)
				{
					//В зависимости от cron_task_id (создание/редактирование)
					if( parseInt(cron_task_id) == 0 )
					{
						//СОЗДАНИЕ
						
						//Делаем видимой область для создания нового задания по расписанию
						document.getElementById('modalCronWindow_workArea_taskCreateEdit').setAttribute('style', '');
						//Делаем невидимой область для списка всех заданий
						document.getElementById('modalCronWindow_workArea_tasksList').setAttribute('style', 'display:none;');
						
						//Внопка "Список заданий" слева в footer - невидимая
						document.getElementById('modalCronWindow_footer_left_button_tasks_list').setAttribute('style', 'display:none;');
						
						//ID задания в форму (0 - будет создание, больше 0 - будет редактирование)
						document.getElementById('cron_task_id').value = cron_task_id;
						
						
						//Заголовок окна
						document.getElementById('modalCronWindow_h4').innerHTML = "<?php echo translate_str_by_id(5457); ?>";
						
						//Кнопка
						document.getElementById('create_edit_cron_task_button').setAttribute('style', '');
						document.getElementById('create_edit_cron_task_button').innerHTML = "<?php echo translate_str_by_id(2292); ?>";
						
						//Перед открытием окна нужно сбросить значения виджетов в исходное состояние
						jQuery('#cron_task_prices').multipleSelect('setSelects', [] );//Снимаем выбор прайс-листов
						jQuery('#cron_task_active').prop('checked', true);//Задание включено
						jQuery('#cron_task_days').multipleSelect('setSelects', ["1", "2", "3", "4", "5", "6", "7"] );//Дни выбраны все
						document.getElementById('cron_task_time').value = "00:00";
						
						
						//Если указан ID прайс-листа - сразу его выбираем (было нажатие кнопки в колонке с конкретным прайс-листом)
						if( parseInt(price_id) > 0 )
						{
							jQuery('#cron_task_prices').multipleSelect('setSelects', [price_id] );
						}
						
						//Показываем окно
						$('#modalCronWindow').modal('show');
					}
					else
					{
						//РЕДАКТИРОВАНИЕ

						//Остальные виджеты необходимо заполнить текущими значениями данного задания
						//AJAX-вызов
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/for_cron/cron_tasks_actions.php",
							dataType: "text",//Тип возвращаемого значения
							data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&cron_task_id="+encodeURIComponent( JSON.stringify(cron_task_id) )+"&action=get_cron_task_objest",
							success: function(answer)
							{
								//Переводим ответ в объект
								var answer_ob = new Object;
								try
								{
									answer_ob = JSON.parse(answer);
									
									if( typeof answer_ob['status'] == 'undefined' )
									{
										alert("<?php echo translate_str_by_id(5453); ?>");
									}
									else
									{
										if( answer_ob['status'] == true )
										{
											//Заголовок окна
											document.getElementById('modalCronWindow_h4').innerHTML = "<?php echo translate_str_by_id(5458); ?> ID " + answer_ob['cron_task']['id'];
											
											//Кнопка
											document.getElementById('create_edit_cron_task_button').setAttribute('style', '');
											document.getElementById('create_edit_cron_task_button').innerHTML = "<?php echo translate_str_by_id(2114); ?>";
											
											//Делаем видимой область для создания нового задания по расписанию
											document.getElementById('modalCronWindow_workArea_taskCreateEdit').setAttribute('style', '');
											//Делаем невидимой область для списка всех заданий
											document.getElementById('modalCronWindow_workArea_tasksList').setAttribute('style', 'display:none;');
											
											
											//Внопка "Список заданий" слева в footer - видимая
											document.getElementById('modalCronWindow_footer_left_button_tasks_list').setAttribute('style', '');
											
											
											//ID задания в форму (0 - будет создание, больше 0 - будет редактирование)
											document.getElementById('cron_task_id').value = answer_ob['cron_task']['id'];
											
											//Указываем текущие настройки задания по расписанию
											//Прайс-листы
											jQuery('#cron_task_prices').multipleSelect('setSelects', answer_ob['cron_task']['prices'] );
											//Включено
											if( parseInt(answer_ob['cron_task']['active']) == 1 )
											{
												jQuery('#cron_task_active').prop('checked', true);
											}
											else
											{
												jQuery('#cron_task_active').prop('checked', false);
											}
											//Дни недели
											jQuery('#cron_task_days').multipleSelect('setSelects', answer_ob['cron_task']['day_week'] );
											//Время
											document.getElementById('cron_task_time').value = answer_ob['cron_task']['time'];

											//Показываем окно
											$('#modalCronWindow').modal('show');
										}
										else
										{
											alert( answer_ob['message'] );
										}
									}
								}
								catch(err)
								{
									alert("<?php echo translate_str_by_id(5455); ?>");
								}
							}
						});
					}
				}
				// --------------------------------------------------------------------------------------
				//Обработка кнопки "Создать" (создать новое задание) / "Сохранить" (отредактировать существующее)
				function create_edit_cron_task()
				{
					//Формируем объект с заданием
					var cron_task = new Object;
					
					
					//Если идет редактирование задания по расписанию
					if( parseInt( document.getElementById('cron_task_id').value ) > 0 )
					{
						cron_task.id = document.getElementById('cron_task_id').value;
					}
					
					
					//Массив под выбранные прайс-листы
					cron_task.prices = $('#cron_task_prices').multipleSelect('getSelects', "value");
					if(cron_task.prices.length == 0)
					{
						alert('<?php echo translate_str_by_id(5459); ?>');
						return;
					}
					
					//Флаг - включен
					if( jQuery('#cron_task_active').prop('checked') == true )
					{
						cron_task.active = 1;
					}
					else
					{
						cron_task.active = 0;
					}
					
					
					//Массив дней недели
					cron_task.days = $('#cron_task_days').multipleSelect('getSelects', "value");
					if(cron_task.days.length == 0)
					{
						alert('<?php echo translate_str_by_id(5460); ?>');
						return;
					}
					
					
					//Время суток
					cron_task.time = document.getElementById('cron_task_time').value;
					
					console.log(cron_task);
					
					
					//AJAX-вызов
					jQuery.ajax({
						type: "POST",
						async: true,
						url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/for_cron/create_edit_cron_task.php",
						dataType: "text",//Тип возвращаемого значения
						data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&cron_task="+encodeURIComponent( JSON.stringify(cron_task) ),
						success: function(answer)
						{							
							//Переводим ответ в объект
							var answer_ob = new Object;
							try
							{
								answer_ob = JSON.parse(answer);
								
								if( typeof answer_ob['status'] == 'undefined' )
								{
									alert("<?php echo translate_str_by_id(5453); ?>");
								}
								else
								{
									if( answer_ob['status'] == true )
									{
										alert(answer_ob['message']);
										//Вызов функции начала редактирования существующего задания по расписанию
										cron_task_open(0, parseInt(answer_ob['cron_task_id']) );
									}
									else
									{
										alert( answer_ob['message'] );
									}
								}
							}
							catch(err)
							{
								alert("<?php echo translate_str_by_id(5455); ?>");
							}
							
							//Обновление индикации наличия заданий по расписанию по всем прайс-листам
							cron_tasks_refresh_indications();
						}
					}); 
					
				}
				// --------------------------------------------------------------------------------------
				</script>
				<div class="text-center m-b-md">
					<div class="modal fade" id="modalCronWindow" tabindex="-1" role="dialog"  aria-hidden="true">
						<div class="modal-dialog modal-lg">
							<div class="modal-content">
								<div class="color-line"></div>
								<div class="modal-header">
									<h4 class="modal-title" id="modalCronWindow_h4"><?php echo translate_str_by_id(5461); ?></h4>
								</div>
								<div class="modal-body" id="modalCronWindow_workArea_tasksList">
									<?php echo translate_str_by_id(5462); ?>
								</div>
								<div class="modal-body" id="modalCronWindow_workArea_taskCreateEdit">
									<div class="row">
										<div class="col-md-12">
											<select multiple="multiple" id="cron_task_prices">
												<?php
												//Получаем список тех прайс-листов, которые имеют не только ручной список получения
												$prices_from_sources_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `load_mode` != ?;");
												$prices_from_sources_query->execute( array(1) );
												while($item = $prices_from_sources_query->fetch() )
												{
													
													$load_mode = "";
													switch( $item["load_mode"] )
													{
														case 2:
															$load_mode = "(".translate_str_by_id(5463).")";
															break;
														case 3:
															$load_mode = "(".translate_str_by_id(5464).")";
															break;
														case 4:
															$load_mode = "(".translate_str_by_id(5465).")";
															break;
													}
													
													?>
													<option value="<?php echo $item["id"]; ?>">ID <?php echo $item["id"]; ?> "<?php echo $item["name"]; ?>" <?php echo $load_mode; ?></option>
													<?php
												}
												?>
											</select>
										</div>
										
										<div class="hr-line-dashed col-lg-12"></div>
										
										<div class="col-md-12">
											<input type="checkbox" checked="checked" id="cron_task_active" /> <label for="cron_task_active"> <?php echo translate_str_by_id(5466); ?></label>
										</div>
										
										<div class="hr-line-dashed col-lg-12"></div>
										
										<div class="col-md-12"><h4><?php echo translate_str_by_id(5467); ?>:</h4></div>
										
										
										<div class="hr-line-dashed col-lg-12"></div>
										
										
										
										<div class="col-lg-12">
											<div class="form-group">
												<label for="" class="col-lg-6 control-label">
													<?php echo translate_str_by_id(5468); ?> 
												</label>
												<div class="col-lg-6">
													
													<select multiple="multiple" id="cron_task_days">
														<option value="1"><?php echo translate_str_by_id(5469); ?></option>
														<option value="2"><?php echo translate_str_by_id(5470); ?></option>
														<option value="3"><?php echo translate_str_by_id(5471); ?></option>
														<option value="4"><?php echo translate_str_by_id(5472); ?></option>
														<option value="5"><?php echo translate_str_by_id(5473); ?></option>
														<option value="6"><?php echo translate_str_by_id(5474); ?></option>
														<option value="7"><?php echo translate_str_by_id(5475); ?></option>
													</select>
													
												</div>
											</div>
										</div>
										
										
										
										<div class="hr-line-dashed col-lg-12"></div>
										
										
										
										
										<div class="col-lg-12">
											<div class="form-group">
												<label for="" class="col-lg-6 control-label">
													<?php echo translate_str_by_id(5357); ?>
												</label>
												<div class="col-lg-6">
													
													<input class="form-control" type="time" id="cron_task_time" min="00:00" max="23:59" value="00:00" />
													
												</div>
											</div>
										</div>
										
										
										<!-- 0 - Создание; Больше 0 - Редактирование -->
										<input type="hidden" id="cron_task_id" value="0" />
										
										
									</div>
									
									<script>
									$('#cron_task_prices').multipleSelect({ placeholder: "<?php echo translate_str_by_id(5476); ?>", width: "100%" });
									$('#cron_task_days').multipleSelect({ placeholder: "<?php echo translate_str_by_id(5477); ?>", width: "100%" });
									//Дни выбираем сразу все (т.е. обновление ежедневно, а пользователь может сам уже настроить на определенные дни)
									$('#cron_task_days').multipleSelect('setSelects', ["1", "2", "3", "4", "5", "6", "7"] );
									</script>
								</div>
								<div class="modal-footer">
									<div class="col-md-6 text-left">
										<button class="btn btn-warning right" type="button" onclick="cron_tasks_list_open();" id="modalCronWindow_footer_left_button_tasks_list">
											<i class="far fa-clock"></i> <span class="bold"><?php echo translate_str_by_id(5452); ?></span>
										</button>
									</div>
									<div class="col-md-6 text-right">
										<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2190); ?></button>
										<button type="button" id="create_edit_cron_task_button" class="btn btn-danger" onclick="create_edit_cron_task();"><?php echo translate_str_by_id(2292); ?></button>
									</div>
								</div>
							</div>
						</div>
					</div>
				</div>
				<!-- END - Настройка заданий по расписанию -->
				
				
				
				
				
			</div>
			
		</div>
	</div>
	
	
	
	
	
	
	
	
	<script>
	// --------------------------------------------------------------------------------------
	// --------------------------------------------------------------------------------------
	//КОНТРОЛЛЕР ОБНОВЛЕНИЯ ПРАЙС-ЛИСТОВ
	var tasks = new Array();//Массив учета запускаемых заданий на обновление, т.е. обращений к pyprices
	// --------------------------------------------------------------------------------------
	//Функция удаления временной папки с уникальным именем, в которую был загружен файл с ПК (при source=='local_path')
	function del_tmp_folder(task)
	{
		if( task.source == 'local_path' && typeof task.tmp_folder_name != 'undefined' )
		{
			jQuery.ajax({
				type: "POST",
				async: true,
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/del_tmp_folder.php",
				dataType: "text",//Тип возвращаемого значения
				data: "tmp_folder_name=" + task.tmp_folder_name + "&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//Задача не критическая, поэтому, результат не обрабатываем - просто выводим в log
					try
					{
						console.log(JSON.parse(answer));
					}
					catch(err)
					{
						alert(err.message);
					}
				}
			}); 
		}
	}
	// --------------------------------------------------------------------------------------
	//Здесь добавим обработчики для jQuery
	jQuery('document').ready(function(){
		
		//Показ инструкций для модуля pyprices сразу после открытия окна
		show_pyprices_instructions();
		
		/*
		Добавляем обработчик oncomplete (ajaxComplete). Используется для:
		
		1. Обработка ответа от pyprices для одного задания tasks[i]:
		- чтение результата и его запись в tasks[i] с последующей индикацией для пользователя
		- удаление временной папки с уникальным именем (через вызов for_pyprices/del_tmp_folder.php), в которую был загружен файл с ПК, если заданием имеет source=='local_path'
		
		2. Обработка неудачной загрузки файла с ПК на сервер (если не удастся прочитать JSON-ответ от php-скрипта загрузки файла с ПК):
		- восстановление нормальных кнопок, индикации ошибки, выставления флага completed для процесса загрузки файла
		
		3. Обработка ответа от pyprices для мультизадания (в котором несколько tasks[i]):
		- чтение результата и его запись в каждый tasks[i] с последующей индикацией для пользователя
		*/
		jQuery(document).ajaxComplete(function(event,xhr,options){
			
			//Получаем текст ответа от серверного скрипта
			var answer = "";
			if( typeof xhr.responseText != 'undefined' )
			{
				answer = xhr.responseText;
			}
			
			
			//1. Обработка ответа от pyprices для одного задания tasks[i] (признак - client_task_id)
			if( typeof options.client_task_id != 'undefined' )
			{
				//Ищем в локальном массиве tasks данное задание
				for( var i = 0 ; i < tasks.length ; i++ )
				{
					//Нашли
					if( parseInt(options.client_task_id) == parseInt(tasks[i].client_task_id) )
					{
						tasks[i].completed = true;//Обработка задания завершена (это в любом случае)
						
						//Переводим ответ в объект
						var answer_ob = new Object;
						try
						{
							//Если в pyprices незакоммичены отладочные print() - берем именно JSON-ответ.
							answer_string = answer;
							answer_string = answer_string.split('{"status": ');
							answer_string = '{"status": ' + answer_string[ answer_string.length - 1 ];
							
							answer_ob = JSON.parse(answer_string);
						}
						catch(err)
						{
							alert(err.message);
						}


						//Если нет status, значит ошибка парсинга ответа. Не можем вытянуть объект задания из JSON-ответа и соответственно, не можем понять результат обновления прайс-листа. Поэтому в tasks[i] просто заполняем поле parsing_error под ошибку парсинга - на этом всё.
						if( typeof answer_ob['status'] == 'undefined' )
						{
							tasks[i].parsing_error = "<?php echo translate_str_by_id(5478); ?><br>" + answer;
						}
						else
						{
							//Статус читается, поэтому скорее всего можем и объект задания вытянуть из JSON-ответа и понять результат обновления прайса
							var task = new Object;//Сюда запишем объект задания, который пришел от pyprices

							if( answer_ob['status'] == true )
							{
								//Завершение с успехом еще ничего не говорит. Это значит, что сам модуль отработал технически корректно, НО, еще нужно проверить сам объект задания. Раз, статус true, то, ЕДИНСТВЕННЫЙ объект находится в массиве list_to_handle - смотрим его.
								
								task = answer_ob['list_to_handle'][0];
							}
							else
							{
								//Завершение со status false говорит о том, что обновление прайса не прозошло - какая-то серьезная ошибка (указана в поле message). Но, раз JSON прочитался, мы точно можем вытянуть объект задания. В ответе, единственный объект (т.к. запрос к pyprices был с одним заданием в запросе) может находиться, как в list_to_handle, так и в list_to_handle_incorrect (в случае, если он не прошел валидацию)
								
								//Ищем объект задания в ответе
								if( answer_ob['list_to_handle'].length == 1 )
								{
									task = answer_ob['list_to_handle'][0];
								}
								else if( answer_ob['list_to_handle_incorrect'].length == 1 )
								{
									task = answer_ob['list_to_handle_incorrect'][0];
								}
							}
							//Точно теперь имеем объект задания из ответа pyprices
							
							//Инициализируем массивы сообщений в tasks[i] (для модального окна)
							tasks[i].validation_messages = task.validation_messages.slice();
							tasks[i].error_messages = task.error_messages.slice();
							tasks[i].other_messages = task.other_messages.slice();
							
							//Следующие сообщения из pyprices не относятся к объекту задания, а относятся к работе модуля pyprices в целом. Но, их тоже заполним в tasks[i] в соответствующие поля, чтобы показать в модальном окне
							tasks[i].errors_general_list = answer_ob.errors_general_list.slice();
							tasks[i].answer_message = answer_ob.message;//Основное сообщение в ответе модуля pyprices
							
							
							//Указываем также данные по обновлению прайс-листа:
							tasks[i].records_handled = task.records_handled;//Количество обработанных строк из файлов
							tasks[i].last_updated = task.last_updated;//Время последнего обновления
						}

						//В tasks[i] заполнили всё могли. Далее - индикация результата обработки задания для данного прайс-листа
						prices_indicate_items['price_' + tasks[i].price_id ] = tasks[i];
						indicate_task_result(tasks[i].price_id);
						
						
						//Еще, если задание имело source==local_path, то, нужно также удалить временную папку с уникальным именем, в которую был предварительно загружен файл для этого задания (это действие выполняется, как в случае успеха pyprices, так и в случае ошибки). pyprices это делает, НО, если вдруг на его уровне возникла ошибка и до удаления этой временной папки модуль не дошел, то, запрос ниже тоже пробует удалить эту папку (если pyprices отработал нормально и папка уже была удалена, то, вызываемый ниже скрипт del_tmp_folder.php ничего не сделает)
						if( tasks[i].source == 'local_path' )
						{
							del_tmp_folder(tasks[i]);
						}
						break;
					}
				}
			}
			
			
			//2. Обработка неудачной загрузки файла с ПК на сервер (если не удастся прочитать JSON-ответ от php-скрипта загрузки файла с ПК). Признак - file_upload_id
			if( typeof options.file_upload_id != 'undefined' )
			{
				//Ищем задание на загрузку файла
				for( var i = 0 ; i < files_uploads.length ; i++ )
				{
					//Нашли задание на загрузку файла
					if( parseInt(files_uploads[i].upload_id) == parseInt(options.file_upload_id) )
					{
						//Если флаг completed == false, значит была ошибка загрузки (иначе бы этот флаг проставился бы в функции success, которая выполняется до oncomplete).
						if( ! files_uploads[i].completed )
						{
							//ЕСТЬ ОШИБКА загрузки файла с ПК
							
							//Задание на загрузку файла с ПК завершено
							files_uploads[i].completed = true;
							
							//Индикация ошибки загрузки файла с ПК на сервер через единый механизм индикации. Для индикации создается объект и заполняются его поля, которые можно здесь заполнить.
							var indicator = new Object;//Объект индикации
							indicator.price_id = files_uploads[i].price_id;
							indicator.price_name = get_price_object(files_uploads[i].price_id)['name'];
							indicator.file_upload_errors = "<?php echo translate_str_by_id(5479); ?><br>"+answer;
							prices_indicate_items['price_' + files_uploads[i].price_id] = indicator;
							indicate_task_result(files_uploads[i].price_id);
						}
					}
				}
			}
			
			
			
			//3. Обработка ответа от pyprices для мультизадания (в котором несколько tasks[i]). Признак - multi_task_id
			if( typeof options.multi_task_id != 'undefined' )
			{
				//console.log(answer);
				
				//Переводим ответ в объект
				var answer_ob = new Object;
				try
				{
					//Если в pyprices незакоммичены отладочные print() - берем именно JSON-ответ.
					answer_string = answer;
					answer_string = answer_string.split('{"status": ');
					answer_string = '{"status": ' + answer_string[ answer_string.length - 1 ];
					
					answer_ob = JSON.parse(answer_string);
				}
				catch(err)
				{
					//Ошибка парсинга JSON. Выходить все равно не нужно
					alert(err.message);
				}
				
				
				//Получаем массивы из ответа pyprices (с результатами по каждому заданию)
				//Массив с заданиями, которые pyprices пробовал выполнять
				var list_to_handle = new Array();
				if( typeof answer_ob['list_to_handle'] != 'undefined' )
				{
					list_to_handle = JSON.parse(JSON.stringify(answer_ob['list_to_handle']));
				}
				//Массив с заданиями, которые pyprices посчитал некорректными
				var list_to_handle_incorrect = new Array();
				if( typeof answer_ob['list_to_handle_incorrect'] != 'undefined' )
				{
					list_to_handle_incorrect = JSON.parse(JSON.stringify(answer_ob['list_to_handle_incorrect']));
				}
				
				
				
				//Ищем это мультизадание в multi_tasks (массив учета мультизаданий)
				for( var i = 0 ; i < multi_tasks.length ; i++ )
				{
					if( parseInt(multi_tasks[i].multi_task_id) == parseInt(options.multi_task_id) )
					{
						//Нашли мультизадание в массиве multi_tasks
						//По списку заданий task в этом мультизадании
						for( var t = 0 ; t < multi_tasks[i].list_to_handle.length ; t++ )
						{
							//Здесь нужно обработать результат по данному заданию task
							//Ищем это задание в массиве tasks (основной массив учета заданий task)
							for( var t_index = 0 ; t_index < tasks.length ; t_index++ )
							{
								if( parseInt(multi_tasks[i].list_to_handle[t].client_task_id) == parseInt(tasks[t_index].client_task_id) )
								{
									//Если уже был обработан на предыдущих итерациях
									if( tasks[t_index].completed )
									{
										continue;
									}
									//Нашли соответствующее задание tasks[i] и тут уже обрабатываем результат работы pyprices конкретно для него. Т.е. в tasks[i] заполняем все поля, какие можем заполнить исходя из ответа от pyprices:
									
									tasks[t_index].completed = true;//Это в любом случае
									
									var task = new Object;//Пустой объект. Далее его нужно будет иницилизировать исходя из результата
									
									//Ищем в list_to_handle от pyprices (корректные с точки зрения pyprices, и которые могли быть обновлены)
									for( var cor = 0 ; cor < list_to_handle.length ; cor++ )
									{
										if( parseInt(list_to_handle[cor].client_task_id) == parseInt(tasks[t_index].client_task_id) )
										{
											//Нашли объект в ответе от pyprices
											task = JSON.parse(JSON.stringify(list_to_handle[cor]));
											break;//cor
										}
									}
									
									//Если не нашли в list_to_handle, ищем в list_to_handle_incorrect от pyprices (задания, которые оказались некорректными с точки зрения pyprices)
									if( typeof task.client_task_id == 'undefined' )
									{
										for( var inc = 0 ; inc < list_to_handle_incorrect.length ; inc++ )
										{
											if( parseInt(list_to_handle_incorrect[inc].client_task_id) == parseInt(tasks[t_index].client_task_id) )
											{
												//Нашли объект в ответе от pyprices
												task = JSON.parse(JSON.stringify(list_to_handle_incorrect[inc]));
												break;//inc
											}
										}
									}
									
									
									if( typeof task.client_task_id != 'undefined' )
									{
										//ОБЪЕКТ БЫЛ НАЙДЕН В ОТВЕТЕ ОТ pyprices
										
										//Инициализируем массивы сообщений в tasks[i] (для модального окна)
										tasks[t_index].validation_messages = task.validation_messages.slice();
										tasks[t_index].error_messages = task.error_messages.slice();
										tasks[t_index].other_messages = task.other_messages.slice();
										
										//Результат обновления (сколько строк из файлов было обработано)
										tasks[t_index].records_handled = task.records_handled;
										tasks[t_index].last_updated = task.last_updated;//Время последнего обновления
									}
									else
									{
										//Объект не был найден в JSON-ответе. Возможно ошибка парсинга. Поэтому заполняем можем только заполнить поле под текст ошибки парсинга
										tasks[t_index].parsing_error = "<?php echo translate_str_by_id(5480); ?><br>" + answer;
									}
									
									//Следующие сообщения из pyprices не относятся к объекту задания, а относятся к результату работы модуля pyprices в целом. Но, заполняем их в каждый объект tasks[i], по которому было мультизадание.
									if( typeof answer_ob.errors_general_list != 'undefined' )
									{
										tasks[t_index].errors_general_list = answer_ob.errors_general_list.slice();
									}
									if( typeof answer_ob.message != 'undefined' )
									{
										tasks[t_index].answer_message = answer_ob.message;//Основное сообщение в ответе модуля pyprices
									}
									
									//Индикация
									prices_indicate_items['price_' + tasks[t_index].price_id ] = tasks[t_index];
									indicate_task_result(tasks[t_index].price_id);
									
									break;//Нашли задание в массиве task
								}
							}
						}
						break;//Нашли мультизадание в массиве multi_tasks
					}
				}
			}//if - ответ от pyprices при обработке мультизадания
			
			
		});
	});
	// --------------------------------------------------------------------------------------
	/*
	Единая функция индикации результата обработки задания
	Индикация работает для конкретного прайс-листа после конкретной попытки выполнить задание или загрузить файл на сервер.
	Объект для индикации берется отсюда prices_indicate_items['price_<price_id>']
	*/
	function indicate_task_result(price_id)
	{
		//Восстанавливаем нормальные кнопки для ручного обновления данного прайс-листа
		document.getElementById('price_manual_upgrading_' + price_id ).innerHTML = prices_manual_upgrading_buttons['price_id_' + price_id ];
		
		//Объект индикации (построен на основе объекта task)
		var indicator = prices_indicate_items['price_' + price_id];
		
		/*
		ДАЛЕЕ у нас в отображаемой таблице прайс-листов, после завершения задания, могут быть три кнопки по результатам обработки этого задания:
		- "Ошибки"
		- "Инфо"
		- "Обновлен успешно" / "Не обновлен" (один из 2)
		Добавляются в колонку к кнопкам ручного обновления прайс-листа.
		
		"Ошибки" - красный треугольник с восклицательным знаком. При нажатии отображает модальное окно, в котором показываются ВСЕ сообщения из:
		- indicator.answer_message, взятый из answer.message (основное сообщение в ответе от pyprices)
		- indicator.errors_general_list, взятый из answer.errors_general_list (общие ошибки модуля pyprices)
		- indicator.validation_messages (ошибки валидации задания в pyprices)
		- indicator.error_messages (ошибки обработки задания в pyprices)
		- indicator.parsing_error (это сообщение добавляется ТОЛЬКО в функции oncomplete, если произошла ошибка парсинга JSON в ответе от pyprices и из ответа вообще не возможно вытянуть объект задания. Остальные поля под ошибки тогда не заполны)
		- indicator.file_upload_errors (возникла ошибка ручной загрузки файла с ПК)
		- indicator.other_error (прочие ошибки)
		
		"Инфо" - синий круг со значком i. При нажатии отображает модальное окно, в котором показываются ВСЕ сообщения из:
		- indicator.other_messages (обычные пояснения к процессу обработки задания из pyprices)
		
		"Обновлен успешно" - зеленый круг со значком "checkbox". При нажатии показывает alert "Прайс-лист успешно обновлен". Этот значек показывается, когда количество добавленных в БД товарных позиций больше 0.
		"Не обновлен" - если обновление товарных позиций прайс-листа не произошло, то, вместо "Обновлен успешно" показываем "Не обновлен" - синий пустой круг
		*/
		

		//Кнопка "Ошибки"
		if( (typeof indicator.validation_messages != 'undefined' && indicator.validation_messages.length > 0) || ( typeof indicator.error_messages != 'undefined' && indicator.error_messages.length > 0) || (typeof indicator.errors_general_list != 'undefined' && indicator.errors_general_list.length > 0) || typeof indicator.parsing_error != 'undefined' || typeof indicator.file_upload_errors != 'undefined' || typeof indicator.other_error != 'undefined' )
		{
			document.getElementById('price_manual_upgrading_' + price_id ).innerHTML += "<i class=\"fas fa-exclamation-triangle\" onclick=\"show_modalTaskResult("+ price_id +", 'errors');\" style=\"margin-left:7px;color:#e74c3c;\"></i>";
		}
		
		//Кнопка "Инфо" (сообщения от pyprices)
		if( typeof indicator.other_messages != 'undefined' && indicator.other_messages.length > 0 )
		{
			document.getElementById('price_manual_upgrading_' + price_id ).innerHTML += "<i class=\"fas fa-info-circle\" onclick=\"show_modalTaskResult("+ price_id +", 'info');\" style=\"margin-left:7px;color:#3498db;\"></i>";
		}
		
		//Кнопка "Успех" (статус фактического обновления товаров)
		if( typeof indicator.records_handled != 'undefined' && parseInt(indicator.records_handled) > 0 )
		{
			document.getElementById('price_manual_upgrading_' + price_id ).innerHTML += "<i class=\"fas fa-check-circle\" style=\"margin-left:7px;color:#62cb31\" onclick=\"alert('<?php echo translate_str_by_id(5481); ?>')\"></i>";
		}
		else
		{
			//Этот значек показываем, только если реально получили объект задания в ответе от pyprices (иначе, мы не можем быть уверенными в том, что в БД действительно нет изменений). Если в tasks[task_index] есть поля, то, он был прочтен из ответа pyprices
			if( typeof indicator.validation_messages != 'undefined' && typeof indicator.error_messages != 'undefined' && typeof indicator.other_messages != 'undefined' )
			{
				document.getElementById('price_manual_upgrading_' + price_id ).innerHTML += "<i class=\"far fa-circle\" style=\"margin-left:7px;color:#3498db\" onclick=\"alert('<?php echo translate_str_by_id(5482); ?>')\"></i>";
			}
		}
		
		//Еще, если records_handled больше 0, значит при обработке задания были обработаны строки из файлов и тогда считаем, что прайс-лист успешно обновлен. Тогда нужно показать текущее количество строк в прайс-листе (в БД) и время его обновления
		if( typeof indicator.records_handled != 'undefined' && parseInt(indicator.records_handled) > 0 )
		{
			//Указываем время обновления
			var last_updated = new Date(indicator.last_updated * 1000);
			var yyyy = last_updated.getFullYear();
			var mm = last_updated.getMonth() + 1;// Months start at 0!
			var dd = last_updated.getDate();
			//Часы, минуты, секунды
			var hh = last_updated.getHours();
			var ii = last_updated.getMinutes();
			var ss = last_updated.getSeconds();
			//Ведущие нули
			if (dd < 10) dd = '0' + dd;
			if (mm < 10) mm = '0' + mm;
			if (hh < 10) hh = '0' + hh;
			if (ii < 10) ii = '0' + ii;
			if (ss < 10) ss = '0' + ss;
			document.getElementById('last_updated_' + indicator.price_id ).innerHTML = dd + '.' + mm + '.' + yyyy + " " + hh + ":" + ii + ":" + ss;
			
			//Указываем количество товаров в данном прайс-листе после его обновления
			//В задании может быть установлен флаг clear_old_records==false. Тогда старые товары не были очищены. Тогда records_handled прибавляем к количеству старых позиций. Если тут будут баги, то, можно сделать отдельное поле в объекте задания, которое будет инициализироваться в pyprices колиством реально сущестсвующих строк в shop_docpart_prices_data по данном прайс-листу, либо отдельный php-скрипт, вызываемый по AJAX.
			if( ! indicator.clear_old_records )
			{
				document.getElementById('records_count_' + indicator.price_id ).innerHTML = String( parseInt(document.getElementById('records_count_' + indicator.price_id ).innerHTML) + parseInt(indicator.records_handled) );
			}
			else
			{
				document.getElementById('records_count_' + indicator.price_id ).innerHTML = indicator.records_handled;
			}
		}
	}
	// --------------------------------------------------------------------------------------
	//Получить объект с настройками прайс-листа по его id
	function get_price_object(price_id)
	{
		//Ищем объект прайс-листа, из которого брать настройки
		for( var i = 0 ; i < docpart_prices.length ; i++ )
		{
			if( parseInt(docpart_prices[i].id) == parseInt(price_id) )
			{
				return docpart_prices[i];
			}
		}
	}
	// --------------------------------------------------------------------------------------
	//Функция подготовки задания. Аргумент local_path указывается только при mode == 'local_path'
	function prepare_task(price_id, mode, local_path)
	{
		//Формируем задание в соответствии с протоколом pyprices
		var task = new Object;
		
		//Получаем объект прайс-листа, из которого брать настройки
		var docpart_price = get_price_object(price_id);
		
		//ID прайс-листа
		task.price_id = price_id;
		
		//Название прайс-листа
		task.price_name = docpart_price['name'];
		
		//Общие настройки задания
		task.file_name_substring = docpart_price['file_name_substring'];
		task.file_name_substring_arch = docpart_price['file_name_substring_arch'];
		task.file_encoding = docpart_price['encoding'];
		task.cols_delimiter = docpart_price['separator'];
		task.clear_old_records = docpart_price['clean_before'];
		task.rows_per_query = 1000;
		
		//Структура файла
		task.col_name = docpart_price['name_col'];
		task.col_article = docpart_price['article_col'];
		task.col_manufacturer = docpart_price['manufacturer_col'];
		task.col_price = docpart_price['price_col'];
		task.col_exist = docpart_price['exist_col'];
		task.col_storage = docpart_price['storage_col'];
		task.col_min_order = docpart_price['min_order_col'];
		task.col_time_to_exe = docpart_price['time_to_exe_col'];
		task.cols_to_left = docpart_price['strings_to_left'];
		
		//Источник
		task.source = mode;
		
		//В зависимости от источника
		if( task.source == 'local_path' )
		{
			task.local_path = local_path;
			task.del_file_from_local_path = true;//Удалить исходный файл из временной папки после его обработки в pyprices. Удаление происходит именно самой временной папки с уникальным именем, которая создается при загрузке файла с ПК на сервер. Имя этой папки устанавливается в task.tmp_folder_name из AJAX-ответа от php-скрипта, который загружает файл с ПК на сервер.
		}
		else if( task.source == 'email' )
		{
			task.email_price_sender = docpart_price['sender_email'];
			task.email_message_header_substring = docpart_price['message_header_substring'];
			task.not_mark_seen_email_messages = docpart_price['not_mark_seen_email_messages'];//Проверить учет этой настройки со строны pyprices
		}
		else if( task.source == 'url' )
		{
			task.url = docpart_price['link'];
		}
		else if( task.source == 'ftp' )
		{
			task.ftp_host = docpart_price['ftp_host'];
			task.ftp_username = docpart_price['ftp_user'];
			task.ftp_password = docpart_price['ftp_password'];
			task.ftp_folder = docpart_price['ftp_folder'];
		}
		
		//Устанавливаем флаг - "Обработка не завершена"
		task.completed = false;
		
		
		//Отправляем синхронный запрос на php-скрипт для добавления задания в таблицу и получения его ID. Без этого задание отправить в pyprices нельзя.
		task.client_task_id = 0;
		var request = new XMLHttpRequest();
		var params = 'price_id=' + price_id + '&price_channel=' + encodeURIComponent(mode) + '&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>';
		request.open("POST", "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/add_new_task.php", false);//false - запрос синхронный
		request.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
		request.send(params);
		if (request.status === 200)
		{
			//Ответ от скрипта
			var answer = request.responseText;
			
			//Переводим ответ в объект
			var answer_ob = new Object;
			try
			{
				answer_ob = JSON.parse(answer);
				
				if( typeof answer_ob['status'] == 'undefined' )
				{
					alert("<?php echo translate_str_by_id(5483); ?>");
				}
				else
				{
					if( answer_ob['status'] == true )
					{
						task.client_task_id = parseInt(answer_ob['task_id']);
						
						if( !(task.client_task_id > 0) )
						{
							alert("<?php echo translate_str_by_id(5484); ?>");
						}
					}
					else
					{
						alert( "<?php echo translate_str_by_id(5485); ?>: " + answer_ob['message']);
					}
				}
			}
			catch(err)
			{
				alert(err.message);
			}
		}
		else
		{
			alert('<?php echo translate_str_by_id(5486); ?>');
		}
		
		return task;
	}
	// --------------------------------------------------------------------------------------
	//Функция старта задания на обновление (в одном запросе к pyprices - одно задание task)
	function start_pyprices(task)
	{
		if(!task)
		{
			return;
		}
		
		if( !(task.client_task_id > 0) )
		{
			alert('<?php echo translate_str_by_id(5487); ?>');
			return;
		}
		
		//Перед началом работы проверяем, есть ли задание с данным прайсом, которое еще не завершилось. Если есть незавершенное задание, то, task полученный в этот раз - будет отброшен 
		for(var i = 0; i < tasks.length; i++)
		{
			if( parseInt(tasks[i].price_id) == parseInt(task.price_id) )
			{
				if( tasks[i].completed != true )
				{
					alert("<?php echo translate_str_by_id(5488); ?> ID " + task.price_id + " <?php echo translate_str_by_id(5489); ?>." )
					return;
				}
			}
		}
		
		//Указываем пиктограмму loading
		document.getElementById('price_manual_upgrading_' + task.price_id).innerHTML = "<i class=\"fas fa-spinner fa-pulse\"></i> <?php echo translate_str_by_id(5448); ?>...";
		
		//Добавляем задание в список для учета отправленных на обработку
		tasks.push(task);
		
		//Формируем аргумент в соответствии с протоколом (должен быть массив заданий)
		var list_to_handle = new Array();
		list_to_handle.push(task);
		
		//Отправляем запрос на обработку задания
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/pyprices/pyprices-api.php",
			dataType: "text",//Тип возвращаемого значения
			data: "key=" + encodeURIComponent('<?php echo $DP_Config->tech_key; ?>') + "&list_to_handle="+encodeURIComponent( JSON.stringify(list_to_handle) ),
			client_task_id:task.client_task_id, //Этот параметр нужен для обработчика oncomplete
			success: function(answer){
				
				console.log("<?php echo translate_str_by_id(5490); ?>");
				return;
			}//END - обработка ответа от pyprices
		});
	}
	// --------------------------------------------------------------------------------------
	
	// --------------------------------------------------------------------------------------
	// --------------------------------------------------------------------------------------
	</script>
	
	
	<!-- START МОДАЛЬНОЕ ОКНО - Результат выполнения задания -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalTaskResult" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title" id="modalTaskResult_h4"><?php echo translate_str_by_id(5491); ?></h4>
					</div>
					<div class="modal-body" id="modalTaskResult_workArea">
						
						<?php echo translate_str_by_id(2828); ?>
						
					</div>
					<div class="modal-footer">
						<button type="button" class="btn btn-primary" data-dismiss="modal">Ok</button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<script>
	//Функция открытия модального окна (по индикации)
	function show_modalTaskResult(price_id, text_type, indicator_ob = false )
	{
		//Объект индикации
		var indicator = indicator_ob;//Пробуем получить из аргумента (это на случай показа окна для объекта из истории обновлений - там объект индикации передается напрямую сюда)
		if( ! indicator )
		{
			//Если объект не передан в качестве аргумента, то, берем его из массива - это для индикации текущих завершаенмых заданий, обрабатываемых на странице
			indicator = prices_indicate_items['price_' + price_id];
		}
		
		
		var modal_h4="";
		var modal_body = "";
		
		//Окно "Ошибки"
		if( text_type == 'errors' )
		{
			//Формируем сообщения об ошибках, которые могут содержаться в разных объектах ответа
			
			//Заголовок окна
			modal_h4 = "<?php echo translate_str_by_id(5492); ?> \""+indicator.price_name+"\" (ID "+indicator.price_id+")";
			
			
			//Содержимое окна
			
			//Основное сообщение от pyprices
			if( typeof indicator.answer_message != 'undefined' )
			{
				modal_body += "<strong><?php echo translate_str_by_id(5493); ?>:</strong> " + indicator.answer_message;
			}
			
			//Ошибка при парсинге ответа от pyprices - когда не удалось прочитать JSON и сработала функция complete
			if( typeof indicator.parsing_error != 'undefined' )
			{
				modal_body += "<strong>parsing_error:</strong> " + indicator.parsing_error;
			}
			
			//Ошибка при при загрузке файла с ПК. Когда задание в массиве tasks - виртуальное, т.е. только для использования механизма показа ошибок
			if( typeof indicator.file_upload_errors != 'undefined' )
			{
				modal_body += "<strong>file_upload_errors:</strong> " + indicator.file_upload_errors;
			}
			
			//Прочие ошибки, которые могут возникать при работе в окне
			if( typeof indicator.other_error != 'undefined' )
			{
				modal_body += "<strong>other_error:</strong> " + indicator.other_error;
			}
			
			
			
			//Массив validation_messages от pyprices
			if( typeof indicator['validation_messages'] != 'undefined' && indicator['validation_messages'].length > 0 )
			{
				modal_body += "<br><strong>indicator.validation_messages:</strong><br>";
				for(var i = 0; i < indicator['validation_messages'].length; i++)
				{
					modal_body += (i+1).toString() + ". " + indicator['validation_messages'][i] + "<br>";
				}
			}
			//Массив error_messages от pyprices
			if( typeof indicator['error_messages'] != 'undefined' && indicator['error_messages'].length > 0 )
			{
				modal_body += "<br><strong>indicator.error_messages:</strong><br>";
				for(var i = 0; i < indicator['error_messages'].length; i++)
				{
					modal_body += (i+1).toString() + ". " + indicator['error_messages'][i] + "<br>";
				}
			}
			//Массив errors_general_list от pyprices
			if(typeof indicator['errors_general_list'] != 'undefined' && indicator['errors_general_list'].length > 0 )
			{
				modal_body += "<br><strong>answer.errors_general_list:</strong><br>";
				for(var i = 0; i < indicator['errors_general_list'].length; i++)
				{
					modal_body += (i+1).toString() + ". " + indicator['errors_general_list'][i] + "<br>";
				}
			}
		}
		//Окно "Инфо"
		else if( text_type == 'info' )
		{
			//Формируем обычные сообщения модуля при обработке
			
			//Заголовок окна
			modal_h4 = "<?php echo translate_str_by_id(5494); ?> \""+indicator.price_name+"\" (ID "+indicator.price_id+")";
			
			//Содержимое окна
			
			//Основное сообщение от pyprices
			if( typeof indicator.answer_message != 'undefined' )
			{
				modal_body += "<strong><?php echo translate_str_by_id(5493); ?>:</strong> " + indicator.answer_message;
			}
			
			
			//Массив other_messages от pyprices
			if( typeof indicator['other_messages'] != 'undefined' )
			{
				modal_body = "<br><strong>indicator.other_messages:</strong><br>";
				for(var i = 0; i < indicator['other_messages'].length; i++)
				{
					modal_body += (i+1).toString() + ". " + indicator['other_messages'][i] + "<br>";
				}
			}
		}

		document.getElementById('modalTaskResult_h4').innerHTML = modal_h4;
		document.getElementById('modalTaskResult_workArea').innerHTML = modal_body;
		
		$('#modalTaskResult').modal('show');
	}
	// --------------------------------------------------------------------------------------
	//Это нужно - если поверх окна с историей заданий, открывается единое окно индикации. Тогда после закрытия окна индикации восстанавливается прокрутка окна с историей заданий
	$(document).on('hidden.bs.modal', function (event) {
		if ($('.modal:visible').length) {
			$('body').addClass('modal-open');
		}
	});
	// --------------------------------------------------------------------------------------																																							
	</script>
	<!-- END МОДАЛЬНОЕ ОКНО - Результат выполнения задания -->
	
	
	
	
	<script>
	<?php
    echo $for_js;//Выводим массив с чекбоксами для элементов (нужно здесь, т.к. ниже идет модуль контроля внешних заданий, в котором используется массив с ID прайс-листов)
    ?>
	</script>
	
	
	
	
	
	
	<!-- START - Контроль внешних заданий -->
	<script>
	// --------------------------------------------------------------------------------------
	//Исходные данные для запросов к скрипту external_tasks_account.php
	var tasks_not_to_account = new Array();//Массив с заданиями (ID заданий), которые не нужно отслеживать (для фильтра)
	var tasks_exeternal_to_account = new Array();//Массив с заданиями, которые нужно отследить
	// --------------------------------------------------------------------------------------
	//Функция учета внешних заданий - вызывается через интервал
	function external_tasks_account()
	{
		//Перед вызовом актуализируем исходные данные
		tasks_not_to_account = new Array();//Сбросили массив и заново его заполняем
		for(var i = 0; i < tasks.length ; i++ )
		{
			tasks_not_to_account.push( tasks[i].client_task_id );
		}

		//Отправляем запрос
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/external_tasks_account.php",
			dataType: "text",//Тип возвращаемого значения
			data: 'csrf_guard_key=' + encodeURIComponent('<?php echo $user_session["csrf_guard_key"]; ?>') + '&tasks_not_to_account=' + encodeURIComponent( JSON.stringify(tasks_not_to_account) ) + '&tasks_exeternal_to_account=' + encodeURIComponent( JSON.stringify(tasks_exeternal_to_account) ) + '&prices_to_account=' + encodeURIComponent( JSON.stringify(elements_id_array) ),
			success: function(answer){
				//Переводим ответ в объект
				var answer_ob = new Object;
				try
				{
					answer_ob = JSON.parse(answer);
					
					if( typeof answer_ob['status'] == 'undefined' )
					{
						console.log("<?php echo translate_str_by_id(5495); ?>");
					}
					else
					{
						if( answer_ob['status'] == true )
						{
							//Здесь парсим результат
							
							//Новые внешние задания, которые сейчас выполняются
							for( var i = 0 ; i < answer_ob['new_external_tasks'].length ; i++ )
							{
								var task = answer_ob['new_external_tasks'][i];
								
								//Делаем индикацию по прайс-листу, который относится к заданию
								document.getElementById('price_manual_upgrading_' + task.price_id).innerHTML = "<i class=\"fas fa-spinner fa-pulse\"></i> <?php echo translate_str_by_id(5448); ?>...";
								
								
								//Добавляем задание в массив учета заданий
								tasks.push( task );
								
								
								//Добавляем его на отслеживание внешних заданий
								tasks_exeternal_to_account.push( task.client_task_id );
							}
							
							//Обрабатываем завершение отслеживаемых внешних заданий
							for( var i = 0 ; i < answer_ob['tasks_exeternal_to_account_results'].length ; i++ )
							{
								var task = answer_ob['tasks_exeternal_to_account_results'][i];
								
								//Это еще не завершено
								if( !task['passed'] )
								{
									continue;
								}
								
								//Завершено. Ищем его в tasks.
								for( var t = 0; t < tasks.length ; t++ )
								{
									
									if( parseInt(tasks[t].client_task_id) == parseInt( task.client_task_id ) )
									{
										tasks[t].completed = true;//Флаг - завершено
										
										//Удаляем его из массива tasks_exeternal_to_account, чтобы больше не отслеживать
										for( var e = 0 ; e < tasks_exeternal_to_account.length ; e++ )
										{
											if( parseInt(tasks_exeternal_to_account[e]) == parseInt(task.client_task_id) )
											{
												tasks_exeternal_to_account.splice(e,1);
												break;
											}
										}
										
										//Инициализируем объект для индикации результата
										prices_indicate_items['price_' + tasks[t].price_id ] = task;
										indicate_task_result(tasks[t].price_id);
										break;
									}
								}
							}
							
							
						}
						else
						{
							console.log(answer_ob['message'] || answer_ob['error'] || 'external_tasks_account error');
						}
					}
				}
				catch(err)
				{
					console.log("<?php echo translate_str_by_id(5496); ?>");
				}
				
			}
		});
	}
	function epc_lazy_load_price_history_indicators()
	{
		var nodes = document.querySelectorAll('[data-epc-lazy-history="1"]');
		var idx = 0;
		var inflight = 0;
		var maxParallel = 3;
		function pump() {
			while (inflight < maxParallel && idx < nodes.length) {
				(function (node) {
					var priceId = node.getAttribute('data-price-id');
					inflight++;
					jQuery.ajax({
						type: 'POST',
						url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/prices_upload/for_pyprices/get_update_history.php',
						data: 'csrf_guard_key=' + encodeURIComponent('<?php echo $user_session["csrf_guard_key"]; ?>') + '&price_id=' + encodeURIComponent(priceId) + '&epc_embed=1',
						timeout: 8000,
						success: function(html) {
							node.innerHTML = html;
							var target = document.getElementById('price_manual_upgrading_' + priceId);
							if (target) {
								target.innerHTML += node.innerHTML;
							}
						},
						complete: function() {
							inflight--;
							pump();
						}
					});
				})(nodes[idx++]);
			}
		}
		if (nodes.length) {
			// After first paint — never block the prices table TTFB.
			setTimeout(pump, 50);
		}
	}
	external_tasks_account();//Вызываем первый раз сразу
	var external_tasks_time_interval = setInterval(external_tasks_account, <?php echo (int) epc_prices_external_poll_interval_ms(); ?>);
	jQuery(document).ready(function() {
		epc_lazy_load_price_history_indicators();
	});
	// --------------------------------------------------------------------------------------
	</script>
	<!-- END - Контроль внешних заданий -->
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
    
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
    <script>
    <?php
    //echo $for_js;//Выводим массив с чекбоксами для элементов (выведен выше)
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
    </script>
    
	
	<script>
		jQuery( window ).load(function() {
			$('#prices_table').footable();
		});
	</script>

	<div class="modal fade" id="epc_price_upload_history_modal" tabindex="-1" role="dialog" aria-hidden="true">
		<div class="modal-dialog modal-lg">
			<div class="modal-content">
				<div class="color-line"></div>
				<div class="modal-header">
					<button type="button" class="close" data-dismiss="modal" aria-label="Close" style="color:#fff;opacity:.85;"><span aria-hidden="true">&times;</span></button>
					<h4 class="modal-title" id="epc_price_upload_history_modal_title">Upload history</h4>
					<small class="epc-hist-modal-sub" id="epc_price_upload_history_modal_sub">Archived source files, import stats, and quick downloads.</small>
				</div>
				<div class="modal-body" id="epc_price_upload_history_modal_body">
					<div class="epc-hist-loading"><div class="epc-hist-spinner" aria-hidden="true"></div>Loading upload history…</div>
				</div>
				<div class="modal-footer">
					<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
				</div>
			</div>
		</div>
	</div>
	</div><!-- /.epc-prices-page -->
    <?php
	}// view !== guide
}//~else - вывод страницы
?>