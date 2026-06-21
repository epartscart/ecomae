<?php
//Страничный скрипт для функции создания дампов структуры сайта
defined('_ASTEXE_') or die('No access');


$all_fields_str = "id;count;url;level;alias;value;parent;description;is_frontend;content_type;content;title_tag;description_tag;keywords_tag;author_tag;main_flag;robots_tag;system_flag;published_flag;time_created;time_edited";//Строка со всеми возможными полями таблицы content
$all_fields_array = explode(";", $all_fields_str);//Массив со всеми возможными полями таблицы content
?>


<?php
if( isset( $_POST["action"] ) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	//Допустимые значения $_POST["action"]
	if( $_POST["action"] != "create_new" && $_POST["action"] != "delete" )
	{
		exit;
	}
	
	if( $_POST["action"] == "create_new" )
	{
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			//Время создания дампа
			$time_created = time();
			
			
			//Получаем поля, которые нужны пользователю
			$fields_to_dump = json_decode($_POST["fields_to_dump"], true);
			$fields_to_dump_str = "";//Строка с перечнем заказанных полей 
			//Проверка корректности json_decode
			if( !is_array($fields_to_dump) )
			{
				throw new Exception(translate_str_by_id(2332));
			}
			//Массив должен быть не пустым
			if( count($fields_to_dump) == 0 )
			{
				throw new Exception(translate_str_by_id(2333));
			}
			//В массиве не должно быть недопустимых элементов
			for($i=0; $i < count($fields_to_dump) ; $i++ )
			{
				if( array_search( $fields_to_dump[$i], $all_fields_array ) === false )
				{
					throw new Exception(translate_str_by_id(2334));
				}
				
				//Строка с перечнем полей, которые будут выведены в дамп
				if( $i > 0 )
				{
					$fields_to_dump_str .= ";";
				}
				$fields_to_dump_str .= $fields_to_dump[$i];
			}
			
			
			//Добавляем новую запись в БД:
			if( ! $db_link->prepare("INSERT INTO `content_structure_dumps` (`time_created`, `fields_in_dump`) VALUES (?,?);")->execute( array($time_created, $fields_to_dump_str) ) )
			{
				throw new Exception(translate_str_by_id(2335));
			}
			
			//Получаем ID добавленной записи
			$dump_id = (int)$db_link->lastInsertId();
			if( $dump_id == 0 )
			{
				throw new Exception(translate_str_by_id(2336));
			}
			
			//Имя файла:
			$file_name = date("Ymd_H-i-s", $time_created)."_".$dump_id.".txt";
			//Полный путь к файлу
			$file_full_path = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/content/structure_dumps/dumps/".$file_name;
			
			
			//Создаем файл
			$file = fopen($file_full_path, "w");
			if( !$file )
			{
				throw new Exception(translate_str_by_id(2337));
			}
			
			
			//Получаем записи материалов (т.е. страниц) сайта
			$content_query = $db_link->prepare("SELECT * FROM `content` ORDER BY `id` ASC;");
			$content_query->execute();
			$strs_counter = 0;//Счетчик количества записей
			while( $content_record = $content_query->fetch() )
			{
				//СНАЧАЛА ДОПОЛНИТЕЛЬНЫЕ ОБРАБОТКИ ЗНАЧЕНИЙ ПОЛЕЙ - ДЛЯ УДОБСТВА ЧТЕНИЯ В ДАМПЕ
				//Если тип страницы - "text", то, содержимое - может быть большим - не выводим его. Если тип - "php", то выводим.
				if( $content_record["content_type"] == "text" )
				{
					$content_record["content"] = "TEXT";
				}
				
				//Если данный материал относится к бэкенду, то в URL добавляем имя папки бэкенда и знак косой черты (от корня сайта)
				if( $content_record["is_frontend"] == 0 )
				{
					$content_record["url"] = "/".$DP_Config->backend_dir."/".$content_record["url"];
				}
				else
				{
					//Либо, просто знак косой черты (от корня сайта)
					$content_record["url"] = "/".$content_record["url"];
				}
				
				
				//Формируем строку для записи в дамп
				$content_string = "";
				//По заказанным полям
				for($i=0; $i < count($fields_to_dump) ; $i++ )
				{
					if( $i > 0 )
					{
						$content_string .= ";";
					}
					
					$content_string .= $content_record[ $fields_to_dump[$i] ];
				}
				$content_string .= "\n";

				
				
				//Если это первая строка - выводим перед ней наименования колонок
				if( $strs_counter == 0 )
				{
					$content_string = $fields_to_dump_str."\n".$content_string;
				}
				
				
				if( !fwrite($file, $content_string) )
				{
					fclose($file);//Закрываем начатый файл
					unlink($file_full_path);//Удаляем начатый файл
					
					
					throw new Exception( translate_str_by_id(2338)." ".$file_name );
				}
				
				$strs_counter++;
			}
			
			
			if( ! fclose($file) )
			{
				throw new Exception( translate_str_by_id(2339)." ".$file_name);
			}
			
			
			
			
			//Дописываем информацию в учетную запись
			if( ! $db_link->prepare("UPDATE `content_structure_dumps` SET `file_name` = ?, `records_count` = ? WHERE `id` = ?;")->execute( array($file_name, $strs_counter, $dump_id) ) )
			{
				unlink($file_full_path);
				throw new Exception( translate_str_by_id(2340)." ".$file_name);
			}
		}
		catch (Exception $e)
		{
			//Откатываем все изменения БД
			$db_link->rollBack();
			
			//Выход с ошибкой
			?>
			<script>
				location = "/<?php echo $DP_Config->backend_dir; ?>/content/structure_dumps?error_message=<?php echo urlencode($e->getMessage()); ?>";
			</script>
			<?php
			exit();
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию


		//Выход со статусом OK
		?>
		<script>
			location = "/<?php echo $DP_Config->backend_dir; ?>/content/structure_dumps?success_message=<?php echo urlencode( translate_str_by_id(2341).": ".$dump_id); ?>";
		</script>
		<?php
		exit();
	}
	//Удаление дампов
	else if( $_POST["action"] == "delete" )
	{
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			//Получаем ID дампов на удаление
			$dumps_to_delete = json_decode($_POST["dumps_to_delete"], true);
			if( !is_array($dumps_to_delete) )
			{
				throw new Exception(translate_str_by_id(2332));
			}
			if( count($dumps_to_delete) == 0 )
			{
				throw new Exception(translate_str_by_id(2342));
			}
			
			
			//Запрос учетных записей дампов
			$SQL_select = "SELECT * FROM `content_structure_dumps` WHERE `id` IN (";
			for($i=0; $i < count($dumps_to_delete); $i++)
			{
				if( $i > 0 )
				{
					$SQL_select .= ",";
				}
				
				$SQL_select .= "?";
			}
			$SQL_select .= ");";
			
			//Выполняем запрос
			$dumps_select_query = $db_link->prepare($SQL_select);
			if( ! $dumps_select_query->execute($dumps_to_delete) )
			{
				throw new Exception(translate_str_by_id(2343));
			}
			
			
			$files_to_delete = array();//Массив файлов на удаление
			$files_directory = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/content/structure_dumps/dumps/";
			while( $dump_record = $dumps_select_query->fetch() )
			{
				$file_full_path = $files_directory.$dump_record["file_name"];
				
				if( !file_exists($file_full_path) )
				{
					throw new Exception( translate_str_by_id(2344)." ".$dump_record["id"].", ".translate_str_by_id(2345)." ".$dump_record["file_name"].", ".translate_str_by_id(2346));
				}
				
				array_push($files_to_delete, $file_full_path);
			}
			
			if( count($files_to_delete) != count($dumps_to_delete) )
			{
				throw new Exception(translate_str_by_id(2347));
			}
			
			
			
			//Сначала удаляем учетные записи
			$SQL_delete = "DELETE FROM `content_structure_dumps` WHERE `id` IN (";
			for($i=0; $i < count($dumps_to_delete); $i++)
			{
				if( $i > 0 )
				{
					$SQL_delete .= ",";
				}
				
				$SQL_delete .= "?";
			}
			$SQL_delete .= ");";
			if( ! $db_link->prepare($SQL_delete)->execute($dumps_to_delete) )
			{
				throw new Exception(translate_str_by_id(2348));
			}
			
			
			//Теперь удаляем файлы
			for($i=0; $i < count($files_to_delete); $i++)
			{
				if( !unlink($files_to_delete[$i]) )
				{
					throw new Exception(translate_str_by_id(2349)." ".$files_to_delete[$i].". ".translate_str_by_id(2350)." ".$i );
				}
			}
			
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
			
			
			//Выход с ошибкой
			?>
			<script>
				location = "/<?php echo $DP_Config->backend_dir; ?>/content/structure_dumps?error_message=<?php echo urlencode($e->getMessage()); ?>";
			</script>
			<?php
			exit();
		}

		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		
		//Выход со статусом OK
		?>
		<script>
			location = "/<?php echo $DP_Config->backend_dir; ?>/content/structure_dumps?success_message=<?php echo urlencode(translate_str_by_id(2351)); ?>";
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
				
				<?php
				print_backend_button( array("onclick"=>"create_new_dump();", "url"=>"javascript:void(0);", "background_color"=>"#33cc33", "fontawesome_class"=>"fas fa-plus", "caption"=>translate_str_by_id(2352), "target"=>"") );
				?>

				<?php
				print_backend_button( array("onclick"=>"delete_dumps();", "url"=>"javascript:void(0);", "background_color"=>"#e74c3c", "fontawesome_class"=>"fas fa-trash", "caption"=>translate_str_by_id(2353), "target"=>"") );
				?>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2354); ?>
			</div>
			<div class="panel-body">
				
				<?php
				//Выводим чекбоксы
				for($i=0; $i < count($all_fields_array) ; $i++)
				{
					?>
					<div class="checkbox" style="display:inline-block; margin-right:20px;">
						<label> <input checked="checked" type="checkbox" id="<?php echo $all_fields_array[$i]; ?>_checkbox" value="<?php echo $all_fields_array[$i]; ?>" /> <?php echo $all_fields_array[$i]; ?> </label>
					</div>
					<?php
				}
				?>
				
				
			</div>
			<div class="panel-footer">
				
				<button class="btn btn-success" onclick="check_all_fields(true);" type="button"><i class="far fa-check-square"></i> <?php echo translate_str_by_id(2355); ?></button>
				<button class="btn btn-primary2" onclick="check_all_fields(false);" type="button"><i class="far fa-square"></i> <?php echo translate_str_by_id(2294); ?></button>
				
				<script>
				//Функция Установить/Снять все
				function check_all_fields( check )
				{
					<?php
					for($i=0; $i < count($all_fields_array) ; $i++)
					{
						?>
						document.getElementById("<?php echo $all_fields_array[$i]; ?>_checkbox").checked =  check;
						<?php
					}
					?>
				}
				</script>
				
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<?php
	//Форма создания нового дампа
	?>
	<form method="POST" name="create_form" style="display:none;">
		<input type="hidden" name="action" value="create_new" />
		<input type="hidden" name="fields_to_dump" id="fields_to_dump" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	//Создание нового дампа
	function create_new_dump()
	{
		//Устанавливаем поля, какие нужно будет вывести в дамп
		var fields_to_dump = new Array();
		<?php
		for($i=0; $i < count($all_fields_array) ; $i++)
		{
			?>
			if( document.getElementById("<?php echo $all_fields_array[$i]; ?>_checkbox").checked )
			{
				fields_to_dump.push('<?php echo $all_fields_array[$i]; ?>');
			}
			<?php
		}
		?>
		
		if(fields_to_dump.length == 0)
		{
			alert("<?php echo translate_str_by_id(2356); ?>");
			return;
		}
		
		document.getElementById("fields_to_dump").value = JSON.stringify(fields_to_dump);
		
		
		document.forms["create_form"].submit();
	}
	</script>
	
	
	
	
	
	<?php
	//Форма удаления дампов
	?>
	<form method="POST" name="delete_form" style="display:none;">
		<input type="hidden" name="action" value="delete" />
		<input type="hidden" name="dumps_to_delete" id="dumps_to_delete" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	// ---------------------------------------------------------------------------------------
	//Функция массового удаления дампов
	function delete_dumps()
	{
		//Получаем ID дампов на удаление в виде массива
		var dumps_to_delete = getCheckedElements();
		
		//Проверка наличия дампов в массиве
		if( dumps_to_delete.length == 0 )
		{
			alert("<?php echo translate_str_by_id(2357); ?>");
			return;
		}
		
		//Подтверждение
		if( !confirm("<?php echo translate_str_by_id(2358); ?>") )
		{
			return;
		}
		
		//Инициализация поля
		document.getElementById("dumps_to_delete").value = JSON.stringify(dumps_to_delete);
		
		//Отправка формы
		document.forms["delete_form"].submit();
	}
	// ---------------------------------------------------------------------------------------
	//Функция удаления одного дампа
	function delete_one_dump(dump_id)
	{
		//Подтверждение
		if( !confirm("<?php echo translate_str_by_id(2359); ?> "+dump_id+" <?php echo translate_str_by_id(2360); ?>") )
		{
			return;
		}
		
		//Приведение к массиву
		var dumps_to_delete = new Array();
		dumps_to_delete.push(dump_id);
		
		//Инициализация поля
		document.getElementById("dumps_to_delete").value = JSON.stringify(dumps_to_delete);
		
		//Отправка формы
		document.forms["delete_form"].submit();
	}
	// ---------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2361); ?>
			</div>
			<div class="panel-body">
				
				<?php
				//Массивы для JS с id элементов и с чекбоксами элементов. Инициализация здесь - чтобы не возникало JS-ошибок в случае отсутствия дампов.
				$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
				$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
				
				
				//Проверяем наличие дампов
				$dumps_exist_query = $db_link->prepare("SELECT COUNT(*) FROM `content_structure_dumps`");
				$dumps_exist_query->execute();
				if( $dumps_exist_query->fetchColumn() > 0 )
				{
					?>
					<table cellpadding="1" cellspacing="1" class="table table-striped">
						<thead>
							<tr>
								<th style="width:50px;"><?php echo translate_str_by_id(2235); ?></th>
								<th style="width:20px;"><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2242); ?></th>
								<th><?php echo translate_str_by_id(2362); ?></th>
								<th><?php echo translate_str_by_id(2363); ?></th>
								<th style="width:300px;"><?php echo translate_str_by_id(2364); ?></th>
								<th><?php echo translate_str_by_id(2113); ?></th>
							</tr>
						</thead>
						<tbody>
							<?php
							//Получаем дампы в обратном порядке (вверху - новые). Получаем ВСЕ. Постранично не требуется, т.к. по логике - старые дампы можно/нужно удалять
							$dumps_query = $db_link->prepare("SELECT * FROM `content_structure_dumps` ORDER BY `id` DESC");
							$dumps_query->execute();
							$counter = 0;//Счетчик п/п
							while( $element_record = $dumps_query->fetch() )
							{
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
								
								$counter++;
								?>
								<tr>
									<td><?php echo $counter; ?></td>
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
									<td><?php echo $element_record["id"]; ?></td>
									<td><?php echo date("d.m.Y, H:i:s", $element_record["time_created"]); ?></td>
									<td><?php echo $element_record["file_name"]; ?></td>
									<td><?php echo $element_record["records_count"]; ?></td>
									<td>
										<?php
										if( $element_record["fields_in_dump"] == $all_fields_str )
										{
											?>
											<font style="font-weight:bold;color:#000;"><?php echo translate_str_by_id(2365); ?></font>
											<?php
										}
										else
										{
											echo str_replace(";",", ",$element_record["fields_in_dump"]);
										}
										?>
									</td>
									<td>
										<?php
										if( file_exists( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/content/structure_dumps/dumps/".$element_record["file_name"] ) )
										{
											?>
											<a class="btn btn-success" href="/<?php echo $DP_Config->backend_dir; ?>/content/content/structure_dumps/dumps/<?php echo $element_record["file_name"]; ?>" download><i class="fas fa-download"></i> <span class="bold"><?php echo translate_str_by_id(2366); ?></span></a>
										
										
											<a class="btn btn-info" target="_blank" href="/<?php echo $DP_Config->backend_dir; ?>/content/content/structure_dumps/dumps/<?php echo $element_record["file_name"]; ?>"><i class="fas fa-desktop"></i> <span class="bold"><?php echo translate_str_by_id(2367); ?></span></a>
										
										
											<button class="btn btn-danger" type="button" onclick="delete_one_dump(<?php echo $element_record["id"]; ?>);">
												<i class="fas fa-trash"></i> <span class="bold"><?php echo translate_str_by_id(2224); ?></span>
											</button>
											<?php
										}
										else
										{
											?>
											<?php echo translate_str_by_id(2368); ?>
											<?php
										}
										?>
									</td>
								</tr>
								<?php
							}
							?>
						</tbody>
					</table>
					<?php
				}
				else
				{
					?>
					<?php echo translate_str_by_id(2369); ?>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	
	

	<script>
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
    </script>
	
	
	
	<?php
}
?>