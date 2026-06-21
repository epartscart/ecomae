<?php
//Страничный скрипт - Редактор переводом текстовых строк
defined('_ASTEXE_') or die('No access');



//Режим работы редактора (Основной/Две колонки)
$lang_editor_mode = 'full';//Основной режим, позволяет создавать и редактировать строки, менять флаги и выполнять все функции со строками (некоторые функции могут быть дополнительно ограничены флагом multilang_editor_restricted_mode в config.php)
//Если режим редактирования не ограничен, то, можно переключать редактор в "Две колонки". Потом этот момент пересмотрели. Переключение редактора между режимами должно быть доступено вне зависимости от ограничений. Пользователь в любом случае сможет редактировать только те языки, которые доступны для редактирования (lang_languages.restrict_edit == 0)
if( !$DP_Config->multilang_editor_restricted_mode || true )
{
	if( isset($_COOKIE["lang_editor_mode"]) )
	{
		$lang_editor_mode = $_COOKIE["lang_editor_mode"];
	}
	
	if( $lang_editor_mode != 'full' && $lang_editor_mode != 'two_cols' )
	{
		$lang_editor_mode = 'full';
	}
	
	
	
	//Левый и правый языки
	if( $lang_editor_mode == 'two_cols' )
	{
		$left_lang = '';
		$right_lang = '';
		
		//Получаем список языков и по-умолчанию выбираем первые два языка
		$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
		$languages_query->execute();
		while( $language = $languages_query->fetch() )
		{
			if( $left_lang == '' )
			{
				$left_lang = $language['lang_code'];
				continue;
			}
			
			if( $right_lang == '' )
			{
				$right_lang = $language['lang_code'];
				break;
			}
		}
		
		
		if( isset( $_COOKIE['left_lang'] ) )
		{
			$left_lang = $_COOKIE['left_lang'];
		}
		if( isset( $_COOKIE['right_lang'] ) )
		{
			$right_lang = $_COOKIE['right_lang'];
		}
		
		
		//Если языки равны, или таких языков нет в таблице, сбрасываем куки и перезагружаем страницу - будут выбраны языки по-умолчанию
		$reset_left_n_right_langs = false;
		$check_lang_query = $db_link->prepare("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?;");
		$check_lang_query->execute( array($left_lang) );
		if( $check_lang_query->fetchColumn() != 1 )
		{
			$reset_left_n_right_langs = true;
		}
		$check_lang_query->execute( array($right_lang) );
		if( $check_lang_query->fetchColumn() != 1 )
		{
			$reset_left_n_right_langs = true;
		}
		if( $left_lang == $right_lang )
		{
			$reset_left_n_right_langs = true;
		}
		if( $reset_left_n_right_langs )
		{
			?>
			<script>
			var date = new Date(new Date().getTime() - 1000 );//Время в прошлом
			document.cookie = "left_lang=1; path=/; expires=" + date.toUTCString();
			document.cookie = "right_lang=1; path=/; expires=" + date.toUTCString();
			location=location;
			</script>
			<?php
			exit;
		}
		
	}
}



/*
Описание ограниченного режима работы редактора (multilang_editor_restricted_mode в config.php)
Режим предназначен прежде всего для сторонних переводчиков. Поэтому, оставляем только тот функционал, который потребуется именно для добавления переводов на новые языки. Остальное отключаем.

По функционалу, запрещаем:
(OK) - создавать новые строки
(OK) - менять описание строк
(OK) - менять параметр "Одинаковая для всех"
(OK) - менять флаг "Является текстом ошибки"
(OK) - менять переводы на тех языках, для которых установлен запрет

(OK) Еще ограничение. Не показываем кастомные строки. При установке CMS создаются кастомные строки. Переводы по ним не требуются, поэтому их не показываем в ограниченном режиме.

В веб-интерфейсе ничего не меняем. Ограничения ставим только на уровне серверных скриптов.
*/


//Описание таблиц и колонок с мультиязыным контентом
require_once($_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/lang/lang_tabs_cols.php');



if( isset( $_POST["action"] ) )
{
	
}
else//Действий нет - выводим страницу
{
	
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_key('2113'); ?>
			</div>
			<div class="panel-body">
				
				<?php
				if( $lang_editor_mode != 'two_cols' )
				{
					print_backend_button( array( 'onclick'=>'create_new_str_OpenModal();' , 'background_color'=>'#62cb31', 'url'=>'javascript:void(0);', 'fontawesome_class'=>'fa fa-plus', 'caption'=>translate_str_by_id(2538) ) );
				}

				//Страница настройки режима мультиязычности
				print_backend_button( array("background_color"=>"#8e44ad", "fontawesome_class"=>"fas fa-tasks", "caption"=>translate_str_by_id(2539), "url"=>"/".$DP_Config->backend_dir."/lang/configurator") );

				//Корневой раздел настройки языков
				print_backend_button( array("background_color"=>"#00b05a", "fontawesome_class"=>"fas fa-language", "caption"=>translate_str_by_id(2529), "url"=>"/".$DP_Config->backend_dir."/lang") );
				?>
				
				<?php
				if( $lang_editor_mode != 'two_cols' )
				{
					//Функции по использованию строк (used_found)
					print_backend_button( array("background_color"=>"#34495e", "fontawesome_class"=>"fas fa-quote-right", "caption"=>translate_str_by_key('1706194771_1_5f735d1486aa51eb9a61df1cd635a0fb'), "url"=>"javascript:void(0);", "onclick"=>"open_modal_used_found();") );
				}
				?>
				
				
				<?php
				//Переключатель режима работы редактора (Основной/Две колонки).
				if( !$DP_Config->multilang_editor_restricted_mode || true )
				{
					if( $lang_editor_mode == 'full' )
					{
						print_backend_button( array("background_color"=>"#3498db", "fontawesome_class"=>"fas fa-columns", "caption"=>translate_str_by_key('1707217851_1_5f735d1486aa51eb9a61df1cd635a0fb'), "url"=>"javascript:void(0);", "onclick"=>"switch_lang_editor_mode('two_cols');") );
					}
					else
					{
						print_backend_button( array("background_color"=>"#3498db", "fontawesome_class"=>"fas fa-toolbox", "caption"=>translate_str_by_key('1707217911_1_5f735d1486aa51eb9a61df1cd635a0fb'), "url"=>"javascript:void(0);", "onclick"=>"switch_lang_editor_mode('full');") );
					}
					?>
					<script>
					//Переключение между режимами работы редактора
					function switch_lang_editor_mode(mode)
					{
						var date = new Date(new Date().getTime() + 15552000 );
						document.cookie = "lang_editor_mode="+mode+"; path=/; expires=" + date.toUTCString();
						
						location = location;
					}
					</script>
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
				<?php echo translate_str_by_id(2540); ?>
			</div>
			<div class="panel-body">			
				
				<?php
				//Выставляем фильтры и сортировку, если они были установлены через куки
				
				//По-умолчанию
				$str_key = "";
				$description = "";
				$translation = "";
				
				$str_key_like = "0";
				$description_like = "0";
				$translation_like = "0";
				
				
				$translation_progress = "0";
				$no_translation_in = "0";
				$has_translation_in = "0";
				$same = "0";
				$is_error = "0";
				
				$is_custom = '2';//Все
				$used_found = '3';//Все
				
				
				$table = "0";//Все
				$column = "0";//Все
				
				
				$sort_field = "str_key";
				$sort_asc_desc = "asc";
				
				$remember_to_cookie = "0";
				
				//Получаем текущие значения куки:
				$text_strs_page_cookie = NULL;
				if( isset($_COOKIE["text_strs_page"]) )
				{
					$text_strs_page_cookie = $_COOKIE["text_strs_page"];
				}
				if($text_strs_page_cookie != NULL)
				{
					$text_strs_page_cookie = json_decode($text_strs_page_cookie, true);
					
					$str_key = $text_strs_page_cookie["str_key"];
					$description = $text_strs_page_cookie["description"];
					$translation = $text_strs_page_cookie["translation"];
					
					$str_key_like = $text_strs_page_cookie["str_key_like"];
					$description_like = $text_strs_page_cookie["description_like"];
					$translation_like = $text_strs_page_cookie["translation_like"];
					
					
					$translation_progress = $text_strs_page_cookie["translation_progress"];
					$no_translation_in = $text_strs_page_cookie["no_translation_in"];
					$has_translation_in = $text_strs_page_cookie["has_translation_in"];
					$same = $text_strs_page_cookie["same"];
					$is_error = $text_strs_page_cookie["is_error"];
					
					$is_custom = $text_strs_page_cookie["is_custom"];
					$used_found = $text_strs_page_cookie["used_found"];
					
					$table = $text_strs_page_cookie["table"];
					$column = $text_strs_page_cookie["column"];
					
					$sort_field = $text_strs_page_cookie["sort_field"];
					$sort_asc_desc = $text_strs_page_cookie["sort_asc_desc"];
					
					$remember_to_cookie = "1";
				}
				?>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							STR_KEY<br><input type="checkbox" style="position:relative; top:3px;" id="str_key_like" /> <label style="font-size:0.7em;" for="str_key_like"><?php echo translate_str_by_key('1704985929_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></label>
							<?php
							if( $str_key_like == 1 )
							{
								?>
								<script>
								jQuery('#str_key_like').prop('checked', true);
								</script>
								<?php
							}
							?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="str_key" value="<?php echo $str_key; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2542); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(2543); ?>');"><i class="fa fa-info"></i></button><br><input type="checkbox" style="position:relative; top:3px;" id="description_like" /> <label style="font-size:0.7em;" for="description_like"><?php echo translate_str_by_key('1704985929_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></label>
							<?php
							if( $description_like == 1 )
							{
								?>
								<script>
								jQuery('#description_like').prop('checked', true);
								</script>
								<?php
							}
							?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="description" value="<?php echo $description; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2544); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(2545); ?>');"><i class="fa fa-info"></i></button><br><input type="checkbox" style="position:relative; top:3px;" id="translation_like" /> <label style="font-size:0.7em;" for="translation_like"><?php echo translate_str_by_key('1704985929_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></label>
							<?php
							if( $translation_like == 1 )
							{
								?>
								<script>
								jQuery('#translation_like').prop('checked', true);
								</script>
								<?php
							}
							?>
						</label>
						<div class="col-lg-6">
							<input type="text"  id="translation" value="<?php echo $translation; ?>" class="form-control" />
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2546); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="translation_progress">
								<option value="0"><?php echo translate_str_by_id(2547); ?></option>
								<option value="1"><?php echo translate_str_by_id(2548); ?></option>
								<option value="2"><?php echo translate_str_by_id(2549); ?></option>
								<option value="3"><?php echo translate_str_by_id(2550); ?></option>
								<option value="4"><?php echo translate_str_by_id(2551); ?></option>
							</select>
							<script>
								document.getElementById("translation_progress").value = '<?php echo $translation_progress; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2552); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="no_translation_in">
								<option value="0"><?php echo translate_str_by_id(2553); ?></option>
								<?php
								$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
								$languages_query->execute();
								while( $language = $languages_query->fetch() )
								{
									?>
									<option value="<?php echo $language['lang_code']; ?>"><?php echo $language['lang_code']; ?></option>
									<?php
								}
								?>
							</select>
							<script>
								document.getElementById("no_translation_in").value = '<?php echo $no_translation_in; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2554); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="has_translation_in">
								<option value="0"><?php echo translate_str_by_id(2553); ?></option>
								<?php
								$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
								$languages_query->execute();
								while( $language = $languages_query->fetch() )
								{
									?>
									<option value="<?php echo $language['lang_code']; ?>"><?php echo $language['lang_code']; ?></option>
									<?php
								}
								?>
							</select>
							<script>
								document.getElementById("has_translation_in").value = '<?php echo $has_translation_in; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				
				<!-- Одинаковый перевод на все языки -->
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5097); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo str_replace('\'', '\\\'',translate_str_by_id(5098)); ?>');"><i class="fa fa-info"></i></button>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="same">
								<option value="0"><?php echo translate_str_by_id(2547); ?></option>
								<option value="1"><?php echo translate_str_by_id(2456); ?></option>
								<option value="2"><?php echo translate_str_by_id(2457); ?></option>
								<?php
								$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
								$languages_query->execute();
								while( $language = $languages_query->fetch() )
								{
									?>
									<option value="<?php echo $language['lang_code']; ?>"><?php echo $language['lang_code']; ?></option>
									<?php
								}
								?>
							</select>
							<script>
								document.getElementById("same").value = '<?php echo $same; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				<!-- Тексты ошибок -->
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(5099); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="is_error">
								<option value="0"><?php echo translate_str_by_id(2547); ?></option>
								<option value="1"><?php echo translate_str_by_id(2456); ?></option>
								<option value="2"><?php echo translate_str_by_id(2457); ?></option>
							</select>
							<script>
								document.getElementById("is_error").value = '<?php echo $is_error; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_key('1706194930_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="is_custom">
								<option value="2"><?php echo translate_str_by_key('2094'); ?></option>
								<option value="0"><?php echo translate_str_by_key('1706194966_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
								<option value="1"><?php echo translate_str_by_key('1706194988_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
							</select>
							<script>
								document.getElementById("is_custom").value = '<?php echo $is_custom; ?>';
							</script>
						</div>
					</div>
				</div>
				
				<div class="col-lg-12" style="height:0!important;margin:0;">
				</div>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_key('1706195020_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="used_found">
								<option value="3"><?php echo translate_str_by_key('2094'); ?></option>
								<option value="0"><?php echo translate_str_by_key('1706195052_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
								<option value="1"><?php echo translate_str_by_key('1706195075_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
								<option value="2"><?php echo translate_str_by_key('1706195100_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
							</select>
							<script>
								document.getElementById("used_found").value = '<?php echo $used_found; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				
				
				<script>
				var lang_tabs_cols = JSON.parse('<?php echo json_encode($lang_tabs_cols); ?>');
				//Обработка выбора в селекте таблиц
				function on_table_select_change()
				{
					//Формируем html для селекта колонок
					var column_select_html = '<option value="0"><?php echo translate_str_by_key('2094'); ?></option>';
					
					
					if( document.getElementById('table').value != 0 )
					{
						for (const [key, value] of Object.entries(lang_tabs_cols[ document.getElementById('table').value ])) {
							
							column_select_html += '<option value="'+key+'">'+key+'</option>';
						}
					}

					
					document.getElementById('column').innerHTML = column_select_html;
				}
				</script>
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_key('1706195133_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="table" onchange="on_table_select_change();">
								<option value="0"><?php echo translate_str_by_key('2094'); ?></option>
								<?php
								foreach( $lang_tabs_cols AS $tab => $cols )
								{
									?>
									<option value="<?php echo $tab; ?>"><?php echo $tab; ?></option>
									<?php
								}
								?>
							</select>
							<script>
								document.getElementById("table").value = '<?php echo $table; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				<div class="col-lg-4">
					<div class="form-group">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_key('1706195199_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
						</label>
						<div class="col-lg-6">
							<select class="form-control" id="column">
							</select>
							<script>
								on_table_select_change();
								document.getElementById('column').value = '<?php echo $column; ?>';
							</script>
						</div>
					</div>
				</div>
				
				
				
				
				
				
				
			</div>
			<div class="panel-footer">
				<div class="row">
					<div class="col-lg-8 float-e-margins">
						<button class="btn btn-success" type="button" onclick="filterItems();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
						<button class="btn btn-primary" type="button" onclick="unsetFilterItems();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2555); ?></button>
						
						
						<input type="checkbox" id="remember_to_cookie" style="margin-left:10px;" />
						<label for="remember_to_cookie" class="control-label">
							<?php echo translate_str_by_id(2556); ?>
						</label>
						<?php
						if( $remember_to_cookie == "1" )
						{
							?>
							<script>
							document.getElementById("remember_to_cookie").checked = true;
							</script>
							<?php
						}
						?>
					</div>
					
					<div class="col-lg-4" style="text-align:right;">
						<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(5101); ?>');"><i class="fa fa-info"></i></button>
						
						<button class="btn btn-primary2" type="button" onclick="optimal_choise();"><i class="fas fa-check"></i> <?php echo translate_str_by_id(5100); ?></button>
						<script>
						function optimal_choise()
						{
							//Сначала снимаем все фильтры (false означает, что после снятия фильтра не делать запрос строк)
							unsetFilterItems(false);
							
							//Теперь выставляем нужные фильтры:
							document.getElementById('same').value = '2';//Имеют одинаковый перевод - Нет
							document.getElementById('is_error').value = '2';//Является текстом ошибки - Нет
							
							//Ставим галку "Записать в куки"
							document.getElementById("remember_to_cookie").checked = true;
							
							//Фильтруем
							filterItems();
						}
						</script>
					</div>
					
				</div>
			</div>
		</div>
	</div>
	<script>
	// -------------------------------------------------------------------
	//Блок для функция обработки used_found (поиск и удаление неиспользуемых строк)
	var used_found_process_state = 0;//Статус процесса 0 - ничего не происходит, 1 - идет поиск использования строк, 2 - идет удаление неиспользуемых строк, 3 - возникла ошибка при поиске или удалении неиспользуемых строк
	// -------------------------------------------------------------------
	//Функция проверки. Вернет true, если в данный момент происходит обработка used_found, т.е. поиск или удаление неиспользуемых строк 
	function is_used_found_processing()
	{
		if( used_found_process_state == 0 )
		{
			return false;//Ничего не происходит
		}
		else if( used_found_process_state == 1 || used_found_process_state == 2 )
		{
			alert('<?php echo translate_str_by_key('1706195252_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
			open_modal_used_found();//Открываем окно
			return true;
		}
		else
		{
			alert('<?php echo translate_str_by_key('1706195292_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
			open_modal_used_found();//Открываем окно
			return true;
		}
	}
	// ------------------------------------------------------------------------------------------------
	
	
	
	
	// ------------------------------------------------------------------------------------------------
    var items = new Array();//Массив строк к отображению
	// ------------------------------------------------------------------------------------------------
    //ОБЪЕКТЫ ДЛЯ ЗАПРОСА И ЗНАЧЕНИЯ ПО-УМОЛЧАНИЮ
	
	//Фильтр
	var items_filter = new Object;
	//Иницализация полей
	items_filter.str_key = '<?php echo $str_key; ?>';
	items_filter.description = '<?php echo $description; ?>';
	items_filter.translation = '<?php echo $translation; ?>';
	
	items_filter.str_key_like = '<?php echo $str_key_like; ?>';
	items_filter.description_like = '<?php echo $description_like; ?>';
	items_filter.translation_like = '<?php echo $translation_like; ?>';
	
	items_filter.translation_progress = '<?php echo $translation_progress; ?>';
	items_filter.no_translation_in = '<?php echo $no_translation_in; ?>';
	items_filter.has_translation_in = '<?php echo $has_translation_in; ?>';
	items_filter.same = '<?php echo $same; ?>'
	items_filter.is_error = '<?php echo $is_error; ?>'
	
	items_filter.is_custom = '<?php echo $is_custom; ?>'
	items_filter.used_found = '<?php echo $used_found; ?>'
	
	items_filter.table = '<?php echo $table; ?>'
	items_filter.column = '<?php echo $column; ?>'
	
	
	//Сортировка
	var items_sort = new Object;
	items_sort.field = '<?php echo $sort_field; ?>';//Поле, по которому сортировать
	items_sort.asc_desc = '<?php echo $sort_asc_desc; ?>';//Направление сортировки
	
	//LIMIT
	var limit_count_option = 100;//Количество строк на странице
	//Значения для запроса исходные
	var limit_from = 0;
	var limit_count = limit_count_option;
	
	//Флаг - отобразили полностью
	var reached_end = 0;
	</script>
	
	
	
	<script>
	//Применение фильтра
    function filterItems()
    {
		//Сбросили объект
        items_filter = new Object;
        
		//Иницализация полей
		items_filter.str_key = document.getElementById("str_key").value;
		items_filter.description = document.getElementById("description").value;
		items_filter.translation = document.getElementById("translation").value;
		items_filter.translation_progress = document.getElementById("translation_progress").value;
		items_filter.no_translation_in = document.getElementById("no_translation_in").value;
		items_filter.has_translation_in = document.getElementById("has_translation_in").value;
		items_filter.same = document.getElementById("same").value;
		items_filter.is_error = document.getElementById("is_error").value;
		
		items_filter.is_custom = document.getElementById("is_custom").value;
		items_filter.used_found = document.getElementById("used_found").value;
		
		items_filter.table = document.getElementById("table").value;
		items_filter.column = document.getElementById("column").value;
		
		//Частичное совпадение
		if( jQuery('#str_key_like').is(':checked') )
		{
			items_filter.str_key_like = '1';
		}
		else
		{
			items_filter.str_key_like = '0';
		}
		if( jQuery('#description_like').is(':checked') )
		{
			items_filter.description_like = '1';
		}
		else
		{
			items_filter.description_like = '0';
		}
		if( jQuery('#translation_like').is(':checked') )
		{
			items_filter.translation_like = '1';
		}
		else
		{
			items_filter.translation_like = '0';
		}
		
		//LIMIT - начинаем заново
		limit_from = 0;
		limit_count = limit_count_option;
		
		//Сброс массива строк
		items = new Array();
		items_new = new Array();
		
		//Флаг "Отобразили все"
		reached_end = 0;
		
		
		//Если стоит галка - запомнить в куки. Запишется и фильтр и сортировка.
		if( document.getElementById("remember_to_cookie").checked == true )
		{
			set_cookie_filter();
		}
		
		
		//Запрос строк
		get_text_strings();
    }
    // ------------------------------------------------------------------------------------------------
    //Снять все фильтры
    function unsetFilterItems( need_get_text_strings )
    {
		//Аргумент функции need_get_text_strings (флаг - после снятия фильтров запросить строки) по умолчанию принимаем "Да"
		if( typeof( need_get_text_strings ) === 'undefined' )
		{
			need_get_text_strings = true;
		}
		
		
		//Сбросили объект
        items_filter = new Object;
        
		//Иницализация полей "Не учитывать в запросе"
        items_filter.str_key = '';
		items_filter.description = '';
		items_filter.translation = '';
		
		items_filter.str_key_like = '0';
		items_filter.description_like = '0';
		items_filter.translation_like = '0';
		
		items_filter.translation_progress = '0';
		items_filter.no_translation_in = '0';
		items_filter.has_translation_in = '0';
		items_filter.same = '0';
		items_filter.is_error = '0';
		
		items_filter.is_custom = '2';
		items_filter.used_found = '3';
		
		items_filter.table = '0';
		items_filter.column = '0';
		
		//Виджеты тоже приводим в исходное состояние
		document.getElementById('str_key').value = '';
		document.getElementById('description').value = '';
		document.getElementById("translation").value = '';
		document.getElementById("translation_progress").value = '0';
		document.getElementById("no_translation_in").value = '0';
		document.getElementById("has_translation_in").value = '0';
		document.getElementById("same").value = '0';
		document.getElementById("is_error").value = '0';
		
		document.getElementById("is_custom").value = '2';
		document.getElementById("used_found").value = '3';
		
		document.getElementById("table").value = '0';
		on_table_select_change();
		
		jQuery('#str_key_like').prop('checked', false);
		jQuery('#description_like').prop('checked', false);
		jQuery('#translation_like').prop('checked', false);
		
		//LIMIT - начинаем заново
		limit_from = 0;
		limit_count = limit_count_option;
		
		//Сброс массива строк
		items = new Array();
		items_new = new Array();
		
		//Флаг "Отобразили все"
		reached_end = 0;
		
		
		//При снятии фильтров, куки тоже не остается (сбросится и фильтр и сортировка)
		var date = new Date(new Date().getTime() - 1000 );//Время в прошлом
		document.cookie = "text_strs_page=1; path=/; expires=" + date.toUTCString();
		document.getElementById("remember_to_cookie").checked = false;
		
		//Запрос строк
		if( need_get_text_strings )
		{
			get_text_strings();
		}
    }
    // ------------------------------------------------------------------------------------------------
    </script>


    <script>
    // ------------------------------------------------------------------------------------------------
	//Сортировка
    function sortItems(field)
    {
        //Если поле это же - меняем направление
		if( items_sort.field == field )
		{
			if( items_sort.asc_desc == "asc" )
			{
				items_sort.asc_desc = "desc";
			}
			else
			{
				items_sort.asc_desc = "asc";
			}
		}
        else
		{
			//Поле сортировки выбрано другое, тогда:
			items_sort.field = field;//Поле, по которому сортировать
			items_sort.asc_desc = 'asc';//Направление сортировки
		}
		
		//Сброс массива строк
		items = new Array();
		items_new = new Array();
		
		//LIMIT - начинаем заново
		limit_from = 0;
		limit_count = limit_count_option;
		
		//Флаг "Отобразили все"
		reached_end = 0;
		
		
		//Если стоит галка - запомнить в куки. Запишется и сортировка и фильтр.
		if( document.getElementById("remember_to_cookie").checked == true )
		{
			set_cookie_filter();
		}
		
		
		//Запрос строк
		get_text_strings();
    }
    // ------------------------------------------------------------------------------------------------
	//Запись куки фильтрами и сортировкой
	function set_cookie_filter()
	{
		//Если стоит галка - запомнить в куки.
		if( document.getElementById("remember_to_cookie").checked == true )
		{
			//До вызова этой функции были заполнены объекты фильтра и сортировки. Куки text_strs_page - одна для всех объектов.
			
			//Просто добавляем к текущему объекту фильтра пару полей для сортировки и потом записываем его уже в куки text_strs_page
			items_filter.sort_field = items_sort.field;
			items_filter.sort_asc_desc = items_sort.asc_desc;
			
			
			//Устанавливаем cookie (на полгода)
			var date = new Date(new Date().getTime() + 15552000 * 1000);
			document.cookie = "text_strs_page="+JSON.stringify(items_filter)+"; path=/; expires=" + date.toUTCString();
		}
	}
	// ------------------------------------------------------------------------------------------------
    </script>
	
	
	
	
	
	
	<script>
	/*
	Функция получения текстовых строк. Срабатывает каждый раз, когда:
	- меняется фильтр
	- меняется сортировка (поле и направление)
	
	Соображения по принципу работы.
	Отображение страницы.
	Выставляются фильтры и сортировка в исходное состояние (записываются в объект).	
	Идет запрос первых 100 строк и затем - его отображение.
	
	При прокрутке вниз. Фильтры и сортировка не меняются. Запрос идет с этими же фильтрами и сортировкой, но, другими значениями для LIMIT. Полученный результат добавляется в JavaScript-массиву и идет отображение.
	
	При смене фильтра:
	- перезаписать объект фильтра
	- сортировка остается той же (поле и направление)
	- значения LIMIT установить в исходное (0, 100)
	- после получения резульата - полное переотображение
	
	При смене сортировки (поле или направление)
	- фильтр не меняется
	- перезаписать объект сортировки (поле и направление)
	- значения LIMIT НЕ меняется (X, Y)
	- после получения результата - полное переотображение
	*/
	// ------------------------------------------------------------------------------------------------
	function get_text_strings()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Если уже отобразили все
		if( reached_end )
		{
			//console.log('Не посылаем запрос, т.к. уже все отобразили по данным фильтрам');
			return;
		}
		
		
		//Если есть какие-то несохраненные данные, то, тоже, не добавляем новые строки
		if( !edited_description_changes_saved || !edited_changes_saved )
		{
			//console.log("Идет какое-то редактирование, поэтому, запрос новых строк не делаем");
			return;
		}
		
		
		//Включаем индикатор загрузки
		document.getElementById('loading_giff').innerHTML = '<img src="/content/files/images/ajax-loader-transparent.gif" />';
		
		
		//Для режима "Две колонки"
		var left_lang = '';
		var right_lang = '';
		<?php
		if( $lang_editor_mode == 'two_cols' )
		{
			?>
			left_lang = '<?php echo $left_lang; ?>';
			right_lang = '<?php echo $right_lang; ?>';
			<?php
		}
		?>
		
		
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_get_text_strings.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&items_filter="+encodeURIComponent( JSON.stringify(items_filter) )+"&items_sort="+encodeURIComponent( JSON.stringify(items_sort) )+"&limit_from="+limit_from+"&limit_count="+limit_count+"&items_new="+encodeURIComponent( JSON.stringify(items_new) )+"&left_lang="+left_lang+"&right_lang="+right_lang,
			success: function(answer_str)
			{	
				//console.log(answer_str);
			
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);
				
				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_id(2557); ?>: ' + answer_str );
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Если в результате строк меньше, чем limit_count - значит отобразили уже все, что были
						if( answer_json.items.length < limit_count )
						{
							reached_end = 1;
						}
						
						
						//ЗДЕСЬ ОБРАБАТЫВАЕМ РЕЗУЛЬТАТ
						items = items.concat( answer_json.items );
						
						//console.log(items);
						
						//Показываем таблицу строк
						show_items_table();
					}
					else
					{
						alert(answer_json.message);
					}
				}
				
				
				//ВЫключаем индикатор загрузки
				document.getElementById('loading_giff').innerHTML = '';
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	//Вывод перевода на текущий язык
	function get_showing_str_on_current_lang(str)
	{
		var translation = String(str);
		if( translation == '' )
		{
			translation = '<span style="color:#fff;background-color:#ea6557;"><?php echo translate_str_by_id(2558); ?></span>';
		}
		
		return translation;
	}
	// ------------------------------------------------------------------------------------------------
	//Отображаем таблицу строк
	function show_items_table()
	{
		if( items.length == 0 )
		{
			document.getElementById('items_table_div').innerHTML = '<?php echo translate_str_by_id(2559); ?>';
			return;
		}
		
		
		var html = '<table class="table table-hover table-striped">';
		
		<?php
		//Для полного режима
		if( $lang_editor_mode != 'two_cols' )
		{
			?>
			html += '<thead>';
				html += '<tr>';
					html += '<th>No</th>'
					html += '<th style="cursor:pointer;width:50px;" id="str_key_sorter" onclick="sortItems(\'str_key\');">STR_KEY</th>';
					html += '<th style="cursor:pointer;" id="description_sorter" onclick="sortItems(\'description\');"><?php echo translate_str_by_id(2073); ?></th>';
					html += '<th style="cursor:pointer;width:40%;" id="current_lang_translation_sorter" onclick="sortItems(\'current_lang_translation\');"><?php echo translate_str_by_id(2560); ?> (<?php echo get_work_lang(); ?>)</th>';
				html += '</tr>';
			html += '</thead>';
			<?php
		}
		else
		{
			//Для режима с двумя колонками
			?>
			html += '<thead>';
				html += '<tr>';
					
					html += '<th style="width:4%;text-align:center">Инфо</th>';
					
					
					html += '<th style="width:48%;">';
					html += '<select class="form-control" id="left_lang_select" onchange="select_left_right_langs(\'left\');">';
						<?php
						$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
						$languages_query->execute();
						while( $language = $languages_query->fetch() )
						{
							$selected = '';
							if( $language['lang_code'] == $left_lang )
							{
								$selected = ' selected="selected" ';
							}
							?>
							html += '<option value="<?php echo $language['lang_code']; ?>" <?php echo $selected; ?>><?php echo $language['lang_code']; ?></option>';
							<?php
						}
						?>
					html += '</select>';
					html += '</th>';


					html += '<th style="width:48%;">';
					
					html += '<select class="form-control" id="right_lang_select" onchange="select_left_right_langs(\'right\');">';
						<?php
						$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
						$languages_query->execute();
						while( $language = $languages_query->fetch() )
						{
							$selected = '';
							if( $language['lang_code'] == $right_lang )
							{
								$selected = ' selected="selected" ';
							}
							?>
							html += '<option value="<?php echo $language['lang_code']; ?>" <?php echo $selected; ?>><?php echo $language['lang_code']; ?></option>';
							<?php
						}
						?>
					html += '</select>';
					html += '</th>';
					
					
					
				html += '</tr>';
			html += '</thead>';
			<?php
		}
		?>
		
		
		
		html += '<tbody>';
		
		for( var i = 0 ; i < items.length ; i++ )
		{
			html += '<tr style="cursor:pointer;" id="tr_'+items[i].str_key+'">';
			
			<?php
			//Для полного режима
			if( $lang_editor_mode != 'two_cols' )
			{
				?>
				//Для обозначения текущего значения "Одинаковая для всех"
				var same_selected = '';
				//Для обозначения текущего значения "Является текстом ошибки"
				var is_error_checked = '';
				
				//Номер по порядку в отображаемой таблице
				html += '<td>No&nbsp;' + (i+1) + '</td>';
				
				//Обозначаем строки, которые были добавлены в текущих фильтрах и сортировке (т.е. после последнего переотображения). Чтобы пользователь понимал, почему они отображаются без учета фильтра и сортировки
				if( items_new.indexOf( items[i].str_key ) >= 0 )
				{
					//Новый элемент
					html += '<td onclick="copy_str_key(\'' + items[i].str_key + '\', true);"><span style="text-decoration:underline dotted;" title="<?php echo translate_str_by_id(2561); ?>">'+items[i].str_key+'</span></td>';
				}
				else
				{
					//Обычный элемент
					html += '<td onclick="copy_str_key(\'' + items[i].str_key + '\', true);">'+items[i].str_key+'</td>';
				}
				
				//Колонка с описанием строки
				html += '<td><div class="description_div_usual" id="str_description_table_div_'+items[i].str_key+'"><span class="text_in_N_lines">'+items[i].description+'</span></div> <i id="pencil_table_'+items[i].str_key+'" class="fas fa-pencil-alt" onclick="edit_str_description(\''+items[i].str_key+'\', \'table\');"></i>';
				
				
				html += '<div style="margin-top:2px;">';
				
				html += '<?php echo translate_str_by_key('1704895537_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?> ';
				html += '<select class="same_select_list" id="same_select_list_'+items[i].str_key+'" onchange="on_same_select_list_changed(\''+items[i].str_key+'\');">';
					html += '<option value="no" style="background-color:#FFF!important;color:#000!important;"><?php echo translate_str_by_key('2457'); ?></option>';
					<?php
					$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
					$languages_query->execute();
					while( $language = $languages_query->fetch() )
					{
						?>
						//Выставляем текущее значение
						if( items[i].same == '<?php echo $language['lang_code']; ?>' )
						{
							same_selected = ' selected="selected" ';
						}
						else
						{
							same_selected = '';
						}
						
						html += '<option '+same_selected+' style="background-color:#3f5872;color:#FFF;" value="<?php echo $language['lang_code']; ?>"><?php echo $language['lang_code']; ?></option>';
						<?php
					}
					?>
				html += '</select> | ';
				
				
				//Текущее значение "Является текстом ошибки"
				is_error_checked = '';
				if( parseInt(items[i].is_error) == 1 )
				{
					is_error_checked = ' checked="checked" ';
				}
				
				html += ' <label style="font-weight:normal;" for="is_error_input_list_'+items[i].str_key+'"><?php echo translate_str_by_key('1704895610_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></label> ';
				html += '<div class="is_error_input_list_div" id="is_error_input_list_div_'+items[i].str_key+'"><input type="checkbox" id="is_error_input_list_'+items[i].str_key+'" '+is_error_checked+' onchange="on_is_error_input_list_changed(\''+items[i].str_key+'\')" /></div>';
				
				
				
				//Текущее значение флага "Является кастомной"
				is_custom_checked = '';
				if( parseInt(items[i].is_custom) == 1 )
				{
					is_custom_checked = ' checked="checked" ';
				}
				html += ' | <label style="font-weight:normal;" for="is_custom_input_list_'+items[i].str_key+'"><?php echo translate_str_by_key('1706195568_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></label> ';
				html += '<div class="is_custom_input_list_div" id="is_custom_input_list_div_'+items[i].str_key+'"><input type="checkbox" id="is_custom_input_list_'+items[i].str_key+'" '+is_custom_checked+' onchange="on_is_custom_input_list_changed(\''+items[i].str_key+'\')" /></div>';
				
				
				
				
				//Текущее значение флага "Использование найдено"
				html += ' | <select class="used_found_select_list" id="used_found_select_list_'+items[i].str_key+'" onchange="on_used_found_select_list_changed(\''+items[i].str_key+'\');">';
					
					//Выставляем текущее значение
					var used_found_selected = new Array();
					used_found_selected['used_found_0'] = '';
					used_found_selected['used_found_1'] = '';
					used_found_selected['used_found_2'] = '';
					
					used_found_selected['used_found_' + items[i].used_found ] = ' selected="selected" ';
					
					html += '<option value="1"'+used_found_selected['used_found_1']+' style="background-color:#FFF;color:#000;"><?php echo translate_str_by_key('1706195623_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>';
					html += '<option value="2"'+used_found_selected['used_found_2']+' style="background-color:#ff4b39;color:#FFF;"><?php echo translate_str_by_key('1706195647_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>';
					html += '<option value="0"'+used_found_selected['used_found_0']+' style="background-color:#c2c2c2;color:#000;"><?php echo translate_str_by_key('1706195678_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>';
				html += '</select>';
				
				
				
				html += '</div>';
				//Закрываем колонку с описание строки
				html += '</td>';
				
				
				//Колонка с переводом на текущий язык
				html += '<td><div onclick="edit_str_translation('+i+', \'<?php echo get_work_lang(); ?>\');"><div class="description_div_usual"><span class="text_in_N_lines">' + get_showing_str_on_current_lang(items[i].current_lang_translation) + '</span></div> <i class="fas fa-pencil-alt"></i></div>';
				
				//Для заполнения кнопок с языками
				var lang_buttons = '';
				
				//Сюда будут записываться настройки цвета для кнопок с переводами строки
				var bg_color = '';
				var text_color = '';
				
				<?php
				//Получем массив со всеми языками платформы
				$languages = array();
				$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
				$languages_query->execute();
				while( $language = $languages_query->fetch() )
				{
					?>
					//Если для строки нет перевода
					if( parseInt( items[i]['has_<?php echo $language['lang_code']; ?>'] ) == 0 )
					{
						bg_color = '#ea6557';//Красный
						text_color = '#fff';
					}
					else
					{
						bg_color = '#74d348';//Зеленый
						text_color = '#000';
					}
					
					lang_buttons += '<span class="circle_lang_button" onclick="edit_str_translation('+i+', \'<?php echo $language['lang_code']; ?>\');" style="border-radius:50%; background-color:'+bg_color+';color:'+text_color+';display:inline-block;width:20px;height:20px;text-align:center;margin:2px;"><?php echo $language['lang_code']; ?></span>';

					<?php
				}
				?>
				
				html += '<div style="text-align:right;">' + lang_buttons + '</div></td>';
				
				<?php
			}
			else
			{
				//Для двух колонок
				?>
				
				html += '<td style="text-align:center">';
				
				html += (i+1) + '<br>';
				
				html += '<button class="btn btn-xs btn-info btn-circle" type="button" onclick="get_str_info(\''+items[i].str_key+'\');"><i class="fa fa-info"></i></button>';
				html += '</td>';
				
				html += '<td id="left_lang_td_'+items[i].str_key+'">';
				
				var value_html_entities = $('<div />').text(items[i].left_lang_translation).html();
				
				html += '<textarea str_key="'+items[i].str_key+'" id="left_lang_' + items[i].str_key + '" class="form-control" style="resize:vertical;min-height:55px;" onfocus="on_side_editing_focus(\'left\', \''+items[i].str_key+'\');" onblur="return on_side_editing_onblur(\'left\', \''+items[i].str_key+'\');" oninput="on_side_editing_change(\'left\', \''+items[i].str_key+'\');">'+ value_html_entities +'</textarea>';
				html += '</td>';
				html += '<td id="right_lang_td_'+items[i].str_key+'">';
				
				value_html_entities = $('<div />').text(items[i].right_lang_translation).html();
				
				html += '<textarea id="right_lang_' + items[i].str_key + '" class="form-control" style="resize:vertical;min-height:55px;" onfocus="on_side_editing_focus(\'right\', \''+items[i].str_key+'\');" onblur="return on_side_editing_onblur(\'right\', \''+items[i].str_key+'\');" oninput="on_side_editing_change(\'right\', \''+items[i].str_key+'\');">'+ value_html_entities +'</textarea>';
				html += '</td>';
				
				<?php
			}
			?>
			
			
			html += '</tr>';
		}
		
		html += '</tbody>'
		
		html += '</table>';
		
		
		document.getElementById('items_table_div').innerHTML = html;
		
		<?php
		//Для полного режима
		if( $lang_editor_mode != 'two_cols' )
		{
			?>
			//Индикация колонки с сортировкой
			document.getElementById(items_sort.field+"_sorter").innerHTML += "<img src=\"/content/files/images/sort_"+items_sort.asc_desc+".png\" style=\"width:15px\" />";
			
			//Подсветка селектов "Одинаковая для всех" (для подсветки тех, где уже указаны языки)
			hilight_same_select();
			//Подсветка селектов "Строка используется"
			hilight_used_found_select();
			<?php
		}
		else
		{
			//Режим "Две колонки"
			?>
			//Добавляем обработчики для подстраивния смежных textarea по высоте
			for( var i = 0 ; i < items.length ; i++ )
			{
				//На левую колонку обработка нажатия мыши (нажали мышь - начался процесс изменения размера)
				jQuery('#left_lang_'+items[i].str_key).bind('mousedown', {str_key:items[i].str_key},  function(event) {
					start_textarea_resizing(event.data.str_key, 'left_lang_')
				});
				//На правую колонку обработка нажатия мыши (нажали мышь - начался процесс изменения размера)
				jQuery('#right_lang_'+items[i].str_key).bind('mousedown', {str_key:items[i].str_key},  function(event) {
					start_textarea_resizing(event.data.str_key, 'right_lang_')
				});

				
				//По двойному щелчку подстраиваем размер textarea под содержимое left_lang -> right_lang
				jQuery('#left_lang_'+items[i].str_key).bind('dblclick', {str_key:items[i].str_key},  function(event) {
					if( jQuery('#left_lang_'+event.data.str_key).height() <= '55' )
					{
						//Свернутый разворачиваем
						jQuery('#left_lang_'+event.data.str_key).height( jQuery('#left_lang_'+event.data.str_key)[0].scrollHeight - 12 );
						jQuery('#right_lang_'+event.data.str_key).height( jQuery('#left_lang_'+event.data.str_key)[0].scrollHeight - 12 );
					}
					else
					{
						//Развернутый сворачиваем
						jQuery('#left_lang_'+event.data.str_key).height('55px');
						jQuery('#right_lang_'+event.data.str_key).height('55px');
					}
				});
				//По двойному щелчку подстраиваем размер textarea под содержимое right_lang -> left_lang
				jQuery('#right_lang_'+items[i].str_key).bind('dblclick', {str_key:items[i].str_key},  function(event) {
					if( jQuery('#right_lang_'+event.data.str_key).height() <= '55' )
					{
						//Свернутый разворачиваем
						jQuery('#right_lang_'+event.data.str_key).height( jQuery('#right_lang_'+event.data.str_key)[0].scrollHeight - 12 );
						jQuery('#left_lang_'+event.data.str_key).height( jQuery('#right_lang_'+event.data.str_key)[0].scrollHeight - 12 );
					}
					else
					{
						//Развернутый сворачиваем
						jQuery('#right_lang_'+event.data.str_key).height('55px');
						jQuery('#left_lang_'+event.data.str_key).height('55px');
					}
				});
				
				
				//Автоизменение размера при изменении содержимого
				jQuery('#left_lang_'+items[i].str_key).bind('input', {str_key:items[i].str_key},  function(event) {
					jQuery('#left_lang_'+event.data.str_key).height( (jQuery('#left_lang_'+event.data.str_key)[0].scrollHeight - 12) + 'px' );
					jQuery('#right_lang_'+event.data.str_key).height( (jQuery('#left_lang_'+event.data.str_key)[0].scrollHeight - 12) + 'px' );
				});
				jQuery('#right_lang_'+items[i].str_key).bind('input', {str_key:items[i].str_key},  function(event) {
					jQuery('#left_lang_'+event.data.str_key).height( (jQuery('#right_lang_'+event.data.str_key)[0].scrollHeight - 12) + 'px' );
					jQuery('#right_lang_'+event.data.str_key).height( (jQuery('#right_lang_'+event.data.str_key)[0].scrollHeight - 12) + 'px' );
				});
			}
			<?php
		}
		?>
	}
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	//Обработка редактирования переводов (режим "Две колонки")
	var prev_side_lang_translation_value = new Array;//Здесь храним исходное значение перевода строки
	var side_editing_saved = true;//Флаг - начатое изменение сохранено
	var side_editing = "";//На какой стороне идет редактирование
	var side_str_key_editing = "";//Перевод какой строки редактируется
	//--------------------
	//Обработка получения фокуса в определенном textarea (режим "Две колонки")
	function on_side_editing_focus(side, str_key)
	{
		//Запоминаем исходное значение перевода
		prev_side_lang_translation_value[side + '_lang_' + str_key] = document.getElementById(side + '_lang_' + str_key).value;
		
		//Какую сторону и какую строку редактируем
		side_editing = side;
		side_str_key_editing = str_key;
		
		//console.log('Начали редактировать', side, str_key, prev_side_lang_translation_value[side + '_lang_' + str_key]);
	}
	//--------------------
	//Обработка потери фокуса textarea (режим "Две колонки")
	function on_side_editing_onblur(side, str_key)
	{
		//console.log('Закончили редактировать', side, str_key, prev_side_lang_translation_value[side + '_lang_' + str_key]);
		
		if( !side_editing_saved )
		{
			if( confirm("<?php echo translate_str_by_key('1707218103_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>") )
			{
				//Выходим без сохранения изменений.
				
				//Ставим исходное значение в textarea
				document.getElementById(side + '_lang_' + str_key).value = prev_side_lang_translation_value[side + '_lang_' + str_key];
				
				//Приводим параметры в исходное значение
				prev_side_lang_translation_value = new Array;//Сбрасываем весь массив
				side_editing_saved = true;//Флаг - Всё сохранено
				
				//Убираем подсветку
				document.getElementById(side+'_lang_td_'+str_key).setAttribute('style', '');
			}
			else
			{
				//Остаемся редактировать в textarea
				
				//На случай, если фокус получен другим textarea (чтобы он не возникал, когда у него отберут фокус)
				side_editing_saved = true;
				
				//Предотвращаем изменение исходного значения в текущем textarea
				var prev_value_buff = prev_side_lang_translation_value[side + '_lang_' + str_key];
				
				//Возвращаем курсор в текущий textarea и продолжаем редактировать
				setTimeout(function(){ 
						jQuery("#"+side + '_lang_' + str_key).focus();
						side_editing_saved = false;//Флаг - Есть несохраненные изменения
						//Исходное значение остается неизменным
						prev_side_lang_translation_value[side + '_lang_' + str_key] = prev_value_buff;
					},
				10);
			}
		}
		
	}
	//--------------------
	//Обработка изменения значения перевода в определенном textarea (режим "Две колонки")
	function on_side_editing_change(side, str_key)
	{
		//Текущее значение
		var current_side_lang_translation_value = document.getElementById(side + '_lang_' + str_key).value;
		
		//Если текущее равно исходному, значит нет несохраненных изменений
		if( current_side_lang_translation_value == prev_side_lang_translation_value[side + '_lang_' + str_key] )
		{
			side_editing_saved = true;
			
			//Убираем подсветку
			document.getElementById(side+'_lang_td_'+str_key).setAttribute('style', '');
		}
		else
		{
			side_editing_saved = false;
			
			//Подсвечиваем область синим
			document.getElementById(side+'_lang_td_'+str_key).setAttribute('style', 'background-color:#bdd7e8; border-radius:7px;');
		}
	}
	//--------------------
	//Обработка сохранения перевода (режим "Две колонки")
	function save_side_translation()
	{
		//Функция может вызываться по Ctrl+S, поэтому, сначала проверяем, есть ли, что сохранять.
		if( document.getElementById(side_editing + '_lang_' + side_str_key_editing) == null || document.getElementById(side_editing + '_lang_' + side_str_key_editing) == undefined )
		{
			//console.log("Нет textarea");
			return;
		}
		if( side_editing_saved )
		{
			//console.log("Изменений для сохранения нет");
			return;
		}
		
		
		//Получаем значение из textarea
		var value = document.getElementById(side_editing + '_lang_' + side_str_key_editing).value;
		if( value == '' )
		{
			alert('<?php echo translate_str_by_id(2565); ?>');
			return;
		}
		if( !value )
		{
			alert('<?php echo translate_str_by_id(2569); ?>');
			return;
		}
		
		
		
		//Чтобы не вызвать дважды подряд
		if( is_saving )
		{
			alert('<?php echo translate_str_by_id(2567); ?>');
			return;
		}
		is_saving = true;
		

		//Получаем то, что нужно сохранить
		//console.log("Сохраняем", document.getElementById(side_editing + '_lang_' + side_str_key_editing).value);
		
		//Отправляем запрос на сервер для сохранения перевода
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_save_string_translation.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&str_key="+side_str_key_editing+"&lang_code="+document.getElementById(side_editing + '_lang_select').value+"&value="+encodeURIComponent(value),
			side: side_editing,
			side_editing_translation: 'yes',
			complete: function(){is_saving = false;},
			success: function(answer_str)
			{		
				//Обработка в общем обработчике AJAX-ответа
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	//Получение справочной информации о строке
	function get_str_info(str_key)
	{
		
		//Инициализация окна
		document.getElementById('modal_str_info_h4').innerHTML = '<?php echo translate_str_by_key('1707218168_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?> ' + str_key;
		document.getElementById('modal_str_info_p').innerHTML = '<?php echo translate_str_by_key('1707218191_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>';
		document.getElementById('modal_str_info_body').innerHTML = '<?php echo translate_str_by_key('2182'); ?>...<br><i class="fas fa-spinner"></i>';
		jQuery('#modal_str_info').modal();
		
		
		
		
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_get_str_info.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&str_key="+str_key,
			complete: function(){},
			success: function(answer_str)
			{
				//console.log(answer_str);
				
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('Error getting server answer: ' + answer_str );
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Показываем
						document.getElementById('modal_str_info_h4').innerHTML = '<?php echo translate_str_by_key('1707218168_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?> ' + answer_json.str_info.str_key;
						document.getElementById('modal_str_info_p').innerHTML = '<?php echo translate_str_by_key('1707218191_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>';
						
						var str_info = '';
						
						
						str_info += '<div class="table-responsive">';
							str_info += '<table cellpadding="1" cellspacing="1" class="table table-condensed">';
								str_info += '<tbody>';
									str_info += '<tr>';
										str_info += '<td style="border-top:0;"><strong>str_key</strong></td>';
										str_info += '<td style="border-top:0;">'+ answer_json.str_info.str_key +'</td>';
									str_info += '</tr>';
									str_info += '<tr>';
										str_info += '<td><strong>ID</strong></td>';
										str_info += '<td>'+ answer_json.str_info.id +'</td>';
									str_info += '</tr>';
									
									str_info += '<tr>';
										str_info += '<td><strong><?php echo translate_str_by_key('2073'); ?></strong></td>';
										str_info += '<td>'+ answer_json.str_info.description +'</td>';
									str_info += '</tr>';
									
									str_info += '<tr>';
										str_info += '<td><strong><?php echo translate_str_by_key('5097'); ?></strong></td>';
										if( answer_json.str_info.same )
										{
											str_info += '<td><?php echo translate_str_by_key('1707218317_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?> ' + answer_json.str_info.same + '</td>';
										}
										else
										{
											str_info += '<td><?php echo translate_str_by_key('2057'); ?></td>';
										}
									str_info += '</tr>';
									
									str_info += '<tr>';
										str_info += '<td><strong><?php echo translate_str_by_key('1704895610_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></strong></td>';
										if( answer_json.str_info.is_error )
										{
											str_info += '<td><?php echo translate_str_by_key('2056'); ?></td>';
										}
										else
										{
											str_info += '<td><?php echo translate_str_by_key('2457'); ?></td>';
										}
									str_info += '</tr>';
									
									
									str_info += '<tr>';
										str_info += '<td><strong><?php echo translate_str_by_key('1707218409_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></strong></td>';
										if( answer_json.str_info.is_custom )
										{
											str_info += '<td><?php echo translate_str_by_key('2056'); ?></td>';
										}
										else
										{
											str_info += '<td><?php echo translate_str_by_key('2457'); ?></td>';
										}
									str_info += '</tr>';
									
									
									str_info += '<tr>';
										str_info += '<td><strong><?php echo translate_str_by_key('1706195020_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></strong></td>';
										if( parseInt(answer_json.str_info.used_found) == 0 )
										{
											str_info += '<td><?php echo translate_str_by_key('4207'); ?></td>';
										}
										else if( parseInt(answer_json.str_info.used_found) == 1 )
										{
											str_info += '<td><?php echo translate_str_by_key('1707218458_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></td>';
										}
										else
										{
											str_info += '<td><?php echo translate_str_by_key('4192'); ?></td>';
										}
									str_info += '</tr>';
									
									
									
									str_info += '<tr>';
										str_info += '<td><strong><?php echo translate_str_by_key('1707218489_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></strong></td>';
										str_info += '<td>';
										<?php
										//Получаем существующие в платформе языки
										$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
										$languages_query->execute();
										//Для каждого языка генерим JavaScript с заполнением селектора языка в окне
										while( $language = $languages_query->fetch() )
										{
											?>
											//Если для строки нет перевода
											if( parseInt( answer_json.str_info.has_<?php echo $language['lang_code']; ?> ) == 0 )
											{
												bg_color = '#ea6557';//Красный
												text_color = '#fff';
											}
											else
											{
												bg_color = '#74d348';//Зеленый
												text_color = '#000';
											}
											
											//Добавляем кнопку в блок селектора
											str_info += '<span class="circle_lang_button" style="border-radius:50%; background-color:'+bg_color+';color:'+text_color+';display:inline-block;width:20px;height:20px;text-align:center;margin:2px;cursor:pointer;"><?php echo $language['lang_code']; ?></span>';
											<?php
										}
										?>
										str_info += '</td>';
										
										
									str_info += '</tr>';
									
									
									
									
								str_info += '</tbody>';
							str_info += '</table>';
						str_info += '</div>';
						
						
						
						
						
						document.getElementById('modal_str_info_body').innerHTML = str_info;
					}
					else
					{
						//Если возникла ошибка
						
						//Показали ошибку
						alert(answer_json.message);
					}
				}
			}
		});
		
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка выбора языка в левом и правом селекторе (side - с какой стороны выбрали язык)
	function select_left_right_langs(side)
	{
		//Какой выбрали язык
		var side_value = document.getElementById( side + '_lang_select' ).value;
		
		var other_side = 'right';
		if( side == 'right' )
		{
			other_side = 'left';
		}
		
		//Проверяем, что в другом селекторе выбран другой язык
		var other_side_value = document.getElementById( other_side + '_lang_select' ).value;
		if( side_value == other_side_value )
		{
			alert('<?php echo translate_str_by_key('1707218536_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
			document.getElementById('left_lang_select').value = '<?php echo $left_lang; ?>';
			document.getElementById('right_lang_select').value = '<?php echo $right_lang; ?>';
			return false;
		}
		
		
		var date = new Date(new Date().getTime() + 15552000 );
		document.cookie = side + "_lang="+side_value+"; path=/; expires=" + date.toUTCString();
		location = location;
	}
	// ------------------------------------------------------------------------------------------------
	//Старт процесса изменения размера textarea
	var resizing_str_key = '';//Для какой строки сейчас меняем размеры textarea
	var resizing_textarea_side = '';//С какой строны меняем textarea (левая или правая)
	function start_textarea_resizing(str_key, textarea_side)
	{
		//Запоминаем, какой textarea начали менять
		resizing_str_key = str_key;
		resizing_textarea_side = textarea_side;
		
		//На весь html добавляем обработку события mousemove (движение мышкой)
		jQuery('html').bind('mousemove',  function(event) {
			make_textareas_same_height();//Выравниваем смежные textarea
		});
		//На весь html добавляем обработку события mouseup (отпустили кнопку мыши - завершаем обработку изменения размера textarea)
		jQuery('html').bind('mouseup',  function(event) {
			
			resizing_str_key = '';
			resizing_textarea_side = '';
			//Отзязали обработку событий
			jQuery('html').unbind( "mousemove" );
			jQuery('html').unbind( "mouseup" );
		});
	}
	// ------------------------------------------------------------------------------------------------
	//Выравниваем высоту смежных textarea (режим двух колонок)
	function make_textareas_same_height()
	{
		if( resizing_textarea_side == 'right_lang_' )
		{
			jQuery('#left_lang_' + resizing_str_key ).height(jQuery( '#right_lang_' + resizing_str_key ).height() + 'px' );
		}
		else
		{
			jQuery('#right_lang_' + resizing_str_key ).height(jQuery( '#left_lang_' + resizing_str_key ).height() + 'px' );
		}
	}
	// ------------------------------------------------------------------------------------------------
	//Функция подсветки селектов "Одинаковая для всех". Нужно подсветить, если выбран какой-то язык.
	function hilight_same_select()
	{
		for( var i = 0 ; i < items.length ; i++)
		{
			if( document.getElementById('same_select_list_' + items[i].str_key ).value != 'no' )
			{
				//Подсвечиваем
				document.getElementById('same_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#3f5872;color:#FFF;');
			}
			else
			{
				//Снимаем подсветку
				document.getElementById('same_select_list_' + items[i].str_key ).setAttribute('style', '');
			}
		}
	}
	// ------------------------------------------------------------------------------------------------
	//Функция подсветки селектов "Строка используется"
	function hilight_used_found_select()
	{
		for( var i = 0 ; i < items.length ; i++)
		{
			if( document.getElementById('used_found_select_list_' + items[i].str_key ).value == '0' )
			{
				//Не определено - Серая
				document.getElementById('used_found_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#c2c2c2;color:#000;');
			}
			else if( document.getElementById('used_found_select_list_' + items[i].str_key ).value == '1' )
			{
				//Строка используется - Без подсветки
				document.getElementById('used_found_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#FFF;color:#000;');
			}
			else
			{
				//Не используется - Красный
				document.getElementById('used_found_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#ff4b39;color:#FFF;');
			}
		}
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка выбора в селекте "Одинаковая для всех"
	var str_id_editing_same = 0;//Переменная для хранения ID строки, у которой сейчас редактируется same
	function on_same_select_list_changed(str_key)
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Получаем выбранное значение из селекта
		var same_selected = document.getElementById('same_select_list_'+str_key).value;
		
		//Запомнили ID строки, у которой сейчас редактируется same
		str_id_editing_same = str_key;
		
		
		//Если параметр для данной строки уже редактировался (есть зеленая подсветка), то, перед новым сохранением нужно применить "обычный вид" для индикации, чтобы зеленая подсветка однозначно бы указывала на корректное сохранение.
		for( var i = 0 ; i < items.length ; i++)
		{
			if( items[i].str_key == str_key )
			{
				
				//Стиль селекта приводим в соответствие с предыдущим значением параметра
				if( items[i].same == null )
				{
					//Снимаем подсветку
					document.getElementById('same_select_list_' + items[i].str_key ).setAttribute('style', '');
				}
				else
				{
					//Подсвечиваем
					document.getElementById('same_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#3f5872;color:#FFF;');
				}
				
				break;
			}
		}
		
		
		//Делаем синхронный запрос к серверу - указываем значение параметра
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_set_same.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&same="+same_selected+"&str_key="+str_key,
			complete: function(){},
			success: function(answer_str)
			{
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('Ошибка парсинга ответа сервера (не возможно определить, сохранился ли параметр): ' + answer_str );
					
					//На записываем ни в items, ни обрабатываем выбор в селекте, т.к. не знаем, как сработало. Пользователь уже должен сам сориентироваться.
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Успешно сохранили - обрабатываем
						
						//В JS-массиве items записываем значение параметра (будет использоваться далее)
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == answer_json.str_key )
							{
								items[i].same = answer_json.same;
								//Подсвечиваем селект зеленым цветом (однозначно сигнализирует пользователю, что редактирование выполнено успешно)
								document.getElementById('same_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#62cb31;color:#000;');
								return;
							}
						}
					}
					else
					{
						//Если возникла ошибка, то, меняем обратно значение селекта как было (взять из items. В items значение осталось прежним)
						
						//Показали ошибку
						alert(answer_json.message);
						
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == str_key_editing_same )
							{
								var pre_same = items[i].same;//Значение, какое было
								if( pre_same == null )
								{
									pre_same = 'no';
								}
								
								//В селект вернули предыдущее значение
								document.getElementById('same_select_list_'+str_key_editing_same).value = pre_same;
								break;
							}
						}
					}
					
					
					//Подсветка селекта при выборе языка
					//hilight_same_select();
				}
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка чекбокса "Является строкой с текстом ошибки"
	var str_key_editing_is_error = 0;//Переменная для хранения ID строки, у которой сейчас редактируется флаг "Является текстом ошибки"
	function on_is_error_input_list_changed(str_key)
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Какое значение выбрал пользователь
		var is_error_from_input = 0;
		if( document.getElementById('is_error_input_list_'+str_key).checked == true )
		{
			is_error_from_input = 1;
		}
		
		str_key_editing_is_error = str_key;
		
		
		//Если параметр для данной строки уже редактировался (есть зеленая подсветка), то, перед новым сохранением нужно применить "обычный вид" для индикации, чтобы зеленая подсветка однозначно бы указывала на корректное сохранение.
		document.getElementById('is_error_input_list_' + str_key ).setAttribute('style', '');
		document.getElementById('is_error_input_list_div_' + str_key ).setAttribute('style', '');
		
		
		//Делаем синхронный запрос к серверу - указываем значение параметра
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_set_is_error.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&is_error="+is_error_from_input+"&str_key="+str_key,
			complete: function(){},
			success: function(answer_str)
			{
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('Ошибка парсинга ответа сервера (не возможно определить, сохранился ли параметр): ' + answer_str );
					
					//На записываем ни в items, ни обрабатываем чекбокс, т.к. не знаем, как сработало. Пользователь уже должен сам сориентироваться.
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Успешно сохранили - обрабатываем
						
						//В JS-массиве items записываем значение параметра (будет использоваться далее)
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == answer_json.str_key )
							{
								items[i].is_error = answer_json.is_error;
								//Подсвечиваем чекбокс зеленым цветом (однозначно сигнализирует пользователю, что редактирование выполнено успешно)
								
								if( items[i].is_error )
								{
									//Для чекбокса, который отмечен (устанавливаем атрибут accent-color самого чекбокса)
									document.getElementById('is_error_input_list_' + items[i].str_key ).setAttribute('style', 'accent-color:#62cb31;');
								}
								else
								{
									//Для чекбокса, который не отмечен. accent-color не применится для снятого чекбокса. Поэтому, сам чекбокс - прозрачный делаем, а фон меняем у дива.
									document.getElementById('is_error_input_list_' + items[i].str_key ).setAttribute('style', 'opacity: 0;');
									document.getElementById('is_error_input_list_div_' + items[i].str_key ).setAttribute('style', 'background-color:#62cb31;');
								}
								return;
							}
						}
					}
					else
					{
						//Если возникла ошибка, то, меняем обратно значение чекбокса как было (взять из items. В items значение осталось прежним)
						
						//Показали ошибку
						alert(answer_json.message);
						
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == str_key_editing_is_error )
							{
								//В чекбокс вернули предыдущее значение
								if( parseInt(items[i].is_error) == 1 )
								{
									document.getElementById('is_error_input_list_'+items[i].str_key).checked = true;
								}
								else
								{
									document.getElementById('is_error_input_list_'+items[i].str_key).checked = false;
								}
								break;
							}
						}
					}
				}
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка чекбокса "Является кастомной строкой (пользовательской)"
	var str_key_editing_is_custom = 0;//Переменная для хранения KEY строки, у которой сейчас редактируется флаг is_custom
	function on_is_custom_input_list_changed(str_key)
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Какое значение выбрал пользователь
		var is_custom_from_input = 0;
		if( document.getElementById('is_custom_input_list_'+str_key).checked == true )
		{
			is_custom_from_input = 1;
		}
		
		str_key_editing_is_custom = str_key;
		
		
		//Если параметр для данной строки уже редактировался (есть зеленая подсветка), то, перед новым сохранением нужно применить "обычный вид" для индикации, чтобы зеленая подсветка однозначно бы указывала на корректное сохранение.
		document.getElementById('is_custom_input_list_' + str_key ).setAttribute('style', '');
		document.getElementById('is_custom_input_list_div_' + str_key ).setAttribute('style', '');
		
		
		//Делаем синхронный запрос к серверу - указываем значение параметра
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_set_is_custom.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&is_custom="+is_custom_from_input+"&str_key="+str_key,
			complete: function(){},
			success: function(answer_str)
			{
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_key('1706195854_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>: ' + answer_str );
					
					//Не записываем ни в items, ни обрабатываем чекбокс, т.к. не знаем, как сработало. Пользователь уже должен сам сориентироваться.
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Успешно сохранили - обрабатываем
						
						//В JS-массиве items записываем значение параметра (будет использоваться далее)
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == answer_json.str_key )
							{
								items[i].is_custom = answer_json.is_custom;
								//Подсвечиваем чекбокс зеленым цветом (однозначно сигнализирует пользователю, что редактирование выполнено успешно)
								
								if( items[i].is_custom )
								{
									//Для чекбокса, который отмечен (устанавливаем атрибут accent-color самого чекбокса)
									document.getElementById('is_custom_input_list_' + items[i].str_key ).setAttribute('style', 'accent-color:#62cb31;');
								}
								else
								{
									//Для чекбокса, который не отмечен. accent-color не применится для снятого чекбокса. Поэтому, сам чекбокс - прозрачный делаем, а фон меняем у дива.
									document.getElementById('is_custom_input_list_' + items[i].str_key ).setAttribute('style', 'opacity: 0;');
									document.getElementById('is_custom_input_list_div_' + items[i].str_key ).setAttribute('style', 'background-color:#62cb31;');
								}
								return;
							}
						}
					}
					else
					{
						//Если возникла ошибка, то, меняем обратно значение чекбокса как было (взять из items. В items значение осталось прежним)
						
						//Показали ошибку
						alert(answer_json.message);
						
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == str_key_editing_is_custom )
							{
								//В чекбокс вернули предыдущее значение
								if( parseInt(items[i].is_custom) == 1 )
								{
									document.getElementById('is_custom_input_list_'+items[i].str_key).checked = true;
								}
								else
								{
									document.getElementById('is_custom_input_list_'+items[i].str_key).checked = false;
								}
								break;
							}
						}
					}
				}
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка выбора в селекте "Строка используется" (выставление флага used_found для существующих строк)
	var str_id_editing_used_found = 0;//Переменная для хранения KEY строки, у которой сейчас редактируется used_found
	function on_used_found_select_list_changed(str_key)
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Получаем выбранное значение из селекта
		var used_found_selected = document.getElementById('used_found_select_list_'+str_key).value;
		
		//Запомнили KEY строки, у которой сейчас редактируется used_found
		str_id_editing_used_found = str_key;
		
		
		//Если параметр для данной строки уже редактировался (есть зеленая подсветка), то, перед новым сохранением нужно применить "обычный вид" для индикации, чтобы зеленая подсветка однозначно бы указывала на корректное сохранение.
		for( var i = 0 ; i < items.length ; i++)
		{
			if( items[i].str_key == str_key )
			{
				
				//Стиль селекта приводим в соответствие с предыдущим значением параметра
				if( items[i].used_found == null )
				{
					//Снимаем подсветку
					document.getElementById('used_found_select_list_' + items[i].str_key ).setAttribute('style', '');
				}
				else
				{
					//Подсвечиваем
					document.getElementById('used_found_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#3f5872;color:#FFF;');
				}
				
				break;
			}
		}
		
		
		//Делаем синхронный запрос к серверу - указываем значение параметра
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_set_used_found.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&used_found="+used_found_selected+"&str_key="+str_key,
			complete: function(){},
			success: function(answer_str)
			{
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_key('1706195978_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>: ' + answer_str );
					
					//Не записываем ни в items, ни обрабатываем выбор в селекте, т.к. не знаем, как сработало. Пользователь уже должен сам сориентироваться.
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Успешно сохранили - обрабатываем
						
						//В JS-массиве items записываем значение параметра (будет использоваться далее)
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == answer_json.str_key )
							{
								items[i].used_found = answer_json.used_found;
								//Подсвечиваем селект зеленым цветом (однозначно сигнализирует пользователю, что редактирование выполнено успешно)
								document.getElementById('used_found_select_list_' + items[i].str_key ).setAttribute('style', 'background-color:#62cb31;color:#000;');
								return;
							}
						}
					}
					else
					{
						//Если возникла ошибка, то, меняем обратно значение селекта как было (взять из items. В items значение осталось прежним)
						
						//Показали ошибку
						alert(answer_json.message);
						
						for( var i = 0 ; i < items.length ; i++ )
						{
							if( items[i].str_key == str_key_editing_used_found )
							{								
								//В селект вернули предыдущее значение
								document.getElementById('used_found_select_list_'+str_key_editing_used_found).value = items[i].used_found;
								break;
							}
						}
					}
					
				}
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	<style>
	/*Стили для чекбоксов "Является текстом ошибки" и "Является кастомной строкой" - для индикации корректного сохранения значения*/
	.is_error_input_list_div,
	.is_custom_input_list_div
	{
		display:inline-block;
		border-radius:3px;
		height:13px;
		width:13px;
	}
	.is_error_input_list_div > input,
	.is_custom_input_list_div > input
	{
		margin-top:0;
	}
	</style>
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2562); ?>
			</div>
			<div class="panel-body">			
				
				<div id="items_table_div">
				</div>
				
				
				<div id="loading_giff" style="text-align:center;">
				</div>
				
			</div>
		</div>
	</div>
	
	<!-- Вспомогательный контейнер для функции копирования str_key в буфер -->
	<div id="div_for_copy_input_on_page">
	</div>
	
	<!-- Блок для перекрытия всех элементов (пелена), кроме инпута редактирования описания строки (используется не при модальном окне) -->
	<div id="transparent_mega_shadow_for_table"></div>
	
	
	
	
	
	<script>
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	//Функционал редактирования описания строки
	// ------------------------------------------------------------------------------------------------
	//Текущее состояние редактирования описания
	var edited_description_str_key = 0;//ID строки для которой идет редактирование
	var edited_description_current_value = '';//Значение до начала редактирования
	var edited_description_area = '';//Где редактируем (таблица или модальное окно)
	var edited_description_changes_saved = true;//Флаг "Изменения сохранены"
	// ------------------------------------------------------------------------------------------------
	//Нажатие кнопки "Карандаш"
	/*
	- str_key - key строки
	- area - таблица или модальное окно
	*/
	function edit_str_description(str_key, area)
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		
		//Вызов функции отмены редактирования текущей строки (на случай, если уже идет редактирование)
		if( ! description_edit_cancel())
		{
			//функция вернула false, значит пользователь захотел продолжить редактирование предыдущей строки
			return;
		}
		
		
		//Если в данный момент идет редактирование перевода в модальном окне и есть несохраненные изменения, то, нужно запретить включать режим редактирования описания, чтобы исключить баг (баг: при сохранении перевода по Ctrl+S, происходит затем перерисовка окна и инпут описания будет заменен на текст)
		if( ! edited_changes_saved )
		{
			if( ! confirm('<?php echo translate_str_by_id(2563); ?>') )
			{
				//Нажали Отмена - продолжаем редактировать перевод на текущий язык
				return;
			}
			else
			{
				//Нажали Ок. Переключаемся на редактирование описания. При этом в textarea с переводом указываем его исходное содержимое, которое было до добавления изменений
				document.getElementById('textarea_for_value').value = value_on_show;
				//И, указываем состояние редактирования перевода
				edited_changes_saved = true;//Нет несохраненных изменений
				//И вызываем функцию обработки начала редактирования перевода, которая сделает кнопку Сохранить недоступной
				on_edit_started();
			}
		}
		
		
		//Получили текущее значение из элемента item
		var current_value = '';
		for( var i=0 ; i < items.length ; i++ )
		{
			if( items[i].str_key == str_key )
			{
				current_value = items[i].description;
				break;
			}
		}
		
		
		//Скрываем карандаш
		$( '#pencil_' + area + '_' + str_key ).attr('class', 'hidden');
		
		
		//В исходном диве меняем класс под режим редактирования
		$('#str_description_' + area + '_div_' + str_key).attr('class', 'description_div_editing');
		
		//Заменяем на инпут
		document.getElementById('str_description_' + area + '_div_' + str_key).innerHTML = '<input type="text" style="width:100%!important;" class="form-control" id="str_description_input" onkeyup="on_description_input_change();" onchange="on_description_input_change();" />';
		
		//Ставим фокус и указываем текущее значение
		$('#str_description_input').focus();
		$('#str_description_input').val(current_value);
		
		$('#str_description_input').get(0).scrollLeft = $('#str_description_input').get(0).scrollWidth;
		
		//Закрываем все остальное прозрачным дивом, чтобы проще отлавливать клики мышью, а вверху оставляем только инпут. Т.е. получается только два достапных элемента (пелена и инпут) при работе в таблице или три элемента (пелена, инпут и пелена модального окна) при работе в модальном окне.
		if( area == 'table' )
		{
			document.getElementById('transparent_mega_shadow_for_table').setAttribute('style', 'position:fixed;top:0;bottom:0;left:0;right:0;z-index:5000;');
		}
		else
		{
			//Редактирование идет в модальном окне. В качестве пелены используем другой див.
			document.getElementById('transparent_mega_shadow_for_modal').setAttribute('style', 'position:fixed;top:0;bottom:0;left:0;right:0;z-index:5000;');
		}
		//Сам инпут делаем выше пелены
		document.getElementById('str_description_input').setAttribute('style', 'width:100%!important;position:relative;z-index:5001!important;');
		
		
		
		//Иницализация состояния редактирования
		edited_description_str_key = str_key;//ID строки для которой идет редактирование
		edited_description_current_value = current_value;//Значение до начала редактирования
		edited_description_area = area;//Где редактируем (таблица или модальное окно)
		
		
		//Отлавливаем нажатия клавиш (keypress)
		$("#str_description_input").on('keypress', function (e) {
			
			//Отлавливаем нажатие Enter (Сохранить)
			if (e.key === 'Enter' || e.keyCode === 13) {
				
				//Если изменения не сохранены - сохраняем
				if( ! edited_description_changes_saved )
				{
					save_description();
				}
				else
				{
					//Если сохранены - делаем обработку отмены режима редактирования
					description_edit_cancel();
				}
			}
		});
		//Отлавливаем нажатия клавиш (keydown)
		$("#str_description_input").on('keydown', function (e) {
			
			//Отлавливаем нажатие Esc (Отмена)
			if (e.key === 'Escape' || e.keyCode === 27 || e.key === 'Esc') {
				
				//Если НЕ открыто модальное окно (т.е. редактируем описание в таблице), то обрабатываем здесь. Если модальное окно открыто, то, при нажатии Ecs оно должно начать закрываться и обработка происходит в обработчике закрытия окна
				if( ! $('#modalStringEdit').hasClass('in') )
				{
					//Отмена редактирования без сохранения
					description_edit_cancel();
				}				
			}
		});
		//Отлавливаем левую кнопку мыши
		$(document).mouseup(function(e) {
			if (e.which === 1) 
			{
				/*
				По сути отлавливаем клик по пелене, т.к. при редактировании описания по сути:
				При редактировании в таблице:
				- доступен инпут
				- доступна пелена
				При редактировании в модальном окне:
				- доступен инпут
				- доступна пелена (она внутри окна)
				- доступна пелена от модального окна (за его пределами)
				*/
				
				
				//Если щелкнули по самому инпуту, ничего не делаем
				if( $('#str_description_input').is(":focus") )
				{
					return;
				}
				
				
				//Если идет редактирование описания в модальном окне, то не отлавливаем клик за его пределами, который запускает закрытие окна - чтобы не дублировать вызов description_edit_cancel(), который будет вызван в обработчике закрытия окна по щелчку за пределами окна. Т.е. при редактировании в модальном окне: пелена внутри только окна - по ней и ловим клик. За пределами окна пелены нет - там уже сработает обработка при закрытии окна.
				var target = $(e.target);
				//Если окно показано
				if( $('#modalStringEdit').hasClass('in') )
				{
					//Клик не по пелене в окне, значит за его пределами. Клик не отлавливаем.
					if( ! target.is("#transparent_mega_shadow_for_modal") )
					{
						return;
					}
				}
				
				//Дошли досюда, значит клик точно по пелене - обабатываем.
				
				//Отмена редактирования
				description_edit_cancel();
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка изменения значения в инпуте
	function on_description_input_change()
	{
		//Если текущее значение не равно исходному - значит, есть изменения
		if( edited_description_current_value != document.getElementById('str_description_input').value )
		{
			//Есть изменения
			edited_description_changes_saved = false;//Флаг "Все изменения сохранены" - нет
		}
		else
		{
			//Изменений нет
			edited_description_changes_saved = true;//Флаг "Все изменения сохранены" - да
		}
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка отмены редактирования (возвращает true - если можно начинать редактировать новую строку. false - новую строку редактировать нельзя и пользователь хочет остаться редактировать текущую строку)
	function description_edit_cancel()
	{
		if( ! edited_description_str_key )
		{
			//Сейчас ничего не редактируется
			return true;//Можно редактировать новую строку
		}
		
		
		//Если есть несохраненные изменения, предупреждаем пользователя
		if( ! edited_description_changes_saved )
		{
			if( ! confirm('<?php echo translate_str_by_id(2564); ?>') )
			{
				//Остаемся в предыдущей строке
				
				//Ставим фокус и указываем текущее значение
				$('#str_description_input').focus();
				$('#str_description_input').val( document.getElementById('str_description_input').value );
				
				return false;//Новую строку редактировать нельзя - остаемся
			}
		}
		
		//Дошли до сюда, значит либо нет несохраненных изменений, либо, пользователь не захотел их сохранять
		
		
		//Убираем обработки с инпута, которые были добавлены при инициализации редактирования.
		//Отлавливаем нажатия клавиш (keypress)
		$("#str_description_input").on('keypress', function (e) {});
		//Отлавливаем нажатия клавиш (keydown)
		$("#str_description_input").on('keydown', function (e) {});
		//Отлавливаем левую кнопку мыши (возможно перенести еще куда-то)
		$(document).mouseup(function(e) {});
		
		
		//В исходном диве меняем класс на исходный
		$('#str_description_' + edited_description_area + '_div_' + edited_description_str_key).attr('class', 'description_div_usual');
	
		//В div убираем инпут и указываем исходное значение
		document.getElementById('str_description_' + edited_description_area + '_div_' + edited_description_str_key).innerHTML = '<span class="text_in_N_lines">' + edited_description_current_value + '</span>';
		
		//Показываем карандаш
		$( '#pencil_' + edited_description_area + '_' + edited_description_str_key ).attr('class', 'fas fa-pencil-alt');
		
		//Снимаем пелену
		if( edited_description_area == 'table' )
		{
			document.getElementById('transparent_mega_shadow_for_table').setAttribute('style', '');
		}
		else
		{
			//Редактирование было в модальном окне
			document.getElementById('transparent_mega_shadow_for_modal').setAttribute('style', '');
		}
		
		
		//Указываем состояние
		edited_description_str_key = 0;//ID строки для которой идет редактирование
		edited_description_current_value = '';//Значение до начала редактирования
		edited_description_area = '';//Где редактируем (таблица или модальное окно)
		edited_description_changes_saved = true;//Флаг "Изменения сохранены"
		
		
		return true;//Можно редактировать новую строку
	}
	// ------------------------------------------------------------------------------------------------
	//Функция сохранения описания
	var is_saving = false;//Флаг "Идет какое-то сохранение"
	function save_description()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		if( document.getElementById('str_description_input') == null || document.getElementById('str_description_input') == undefined )
		{
			//console.log("Нет ипута для описания");
			return;
		}
		if( edited_description_changes_saved )
		{
			//console.log("Все изменения и так уже сохранены");
			return;
		}
		
		//Получаем значение из инпута
		var value = document.getElementById('str_description_input').value;
		if( value == '' )
		{
			alert('<?php echo translate_str_by_id(2565); ?>');
			return;
		}
		if( !value )
		{
			alert('<?php echo translate_str_by_id(2566); ?>');
			return;
		}
		
		
		//Чтобы не вызвать дважды подряд
		if( is_saving )
		{
			alert('<?php echo translate_str_by_id(2567); ?>');
			return;
		}
		is_saving = true;
		
		
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_save_string_description.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&value="+encodeURIComponent(value)+"&str_key="+edited_description_str_key,
			complete: function(){is_saving = false;},
			success: function(answer_str)
			{				
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_id(2557); ?>: ' + answer_str );
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//Успешно сохранили - обрабатываем
						
						//Указываем состояние редактирования описания
						edited_description_changes_saved = true;//Все изменения сохранены
						edited_description_current_value = answer_json.new_value;//Указываем новое исходное значение
						
						//В элемент в items перезаписываем описание. На всякий случай, находим его по key строки, а не по индексу
						//Ищем объект в items
						for( var i=0 ; i < items.length ; i++ )
						{
							//Нашли
							if( items[i].str_key == answer_json.str_key )
							{
								//Указываем, что у такой строки теперь есть перевод на такой язык (если еще не было до этого)
								items[i]['description'] = answer_json.new_value;
								break;
							}
						}
						
						//Далее обработка, как при отмене редактирования. Т.е. значение сохранили и теперь просто снимаем режим редактирования
						description_edit_cancel();
						
						
						//Перевызов функции, чтобы обновить отображение таблицы (нужно, если редактирование было в модальном окне - чтобы переотобразить описание в таблице)
						show_items_table();
					}
					else
					{
						alert(answer_json.message);
					}
				}
				
			}
		});
	}
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	<script>
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	// ------------------------------------------------------------------------------------------------
	//Состояние редактирования
	var edited_str_key = 0;//ID строки, которая сейчас редактируется
	var edited_lang_code = '';//Текущий язык. Перевод на какой язык сейчас редактируется для текущей строки
	var edited_changes_saved = true;//Флаг "Нет несохраненных изменений"
	var edited_str_index = 0;//Индекс редактируемой строки в массиве
	// ------------------------------------------------------------------------------------------------
	var value_on_show = '';//Вспомогательная переменная - значение перевода в начале. Затем сравнивается с текущим для определения, было ли редактирование.
	// ------------------------------------------------------------------------------------------------
	/*
	Начать редактировать перевод на определенном языке для определенной строки.
	Вызывается:
	- по нажатию круглой кнопки языка в таблице со строками
	- по нажатию круглой кнопки языка в окне при уже начатом редактировании
	- после успешного сохранения*/
	function edit_str_translation(item_index, lang_code)
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Если есть несохраненные изменения (скорее всего идет переключение на редактирование другого языка)
		if( ! edited_changes_saved )
		{
			if( ! confirm('<?php echo translate_str_by_id(2563); ?>') )
			{
				//Нажали Отмена - продолжаем редактировать текущий язык
				return;
			}
		}
		
		/*
		Закоммитили, т.к. возможно уже не требуется после добавления пелены
		//Если уже идет редактирование описания, то, необходимо обработать
		//Вызов функции отмены редактирования описания строки (на случай, если уже идет редактирование)
		if( ! description_edit_cancel())
		{
			//функция вернула false, значит пользователь захотел продолжить редактирование описания строки
			return;
		}*/
		
		
		//Сначала иницализируем состояние
		edited_changes_saved = true;//В начале нет несохраненных изменений
		edited_str_key = items[item_index].str_key;//ID строки, перевод которой будем редактировать
		edited_lang_code = lang_code;//Язык, на котором будет редактировать
		edited_str_index = item_index;//Индекс элемента в массиве items
		
		
		//Отображаем или Переотображаем окно (вместо textarea отображается индикатор загрузки - до того, как с сервера придет текущее значение перевода)
		open_modalStringEdit(edited_str_index, edited_lang_code);
		
		
		//Получаем текущее значение перевода с сервера
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_get_string_translation.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&str_key="+edited_str_key+"&lang_code="+edited_lang_code,
			success: function(answer_str)
			{
				
				//console.log(answer_str);
				
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_id(2557); ?>: ' + answer_str );
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//То, что пришло от сервера
						//console.log(answer_json.value);
						
						//Страрый вариант (прямая вставка значения в textarea) - преобразовывал мнемоники в символы (так по логике работы редактора быть не должно)
						//textarea для редактирования перевода
						//document.getElementById('div_for_text_area').innerHTML = '<textarea onchange="on_edit_started();" onkeyup="on_edit_started();" style="width:100%;height:200px;" class="form-control" placeholder="<?php echo translate_str_by_id(2568); ?>" id="textarea_for_value">'+answer_json.value+'</textarea>';
						
						//Новый вариант. Текущее значение прогоняем через следующую команду (получается аналог htmlentities на PHP). Подразумевается, что доступ к редактору имеют только доверенные лица, поэтому XSS не является угрозой в данном случае.
						var value_html_entities = $('<div />').text(answer_json.value).html();
						//console.log('--------');
						//console.log(value_html_entities);
						document.getElementById('div_for_text_area').innerHTML = '<textarea onchange="on_edit_started();" onkeyup="on_edit_started();" style="width:100%;height:200px;" class="form-control" placeholder="<?php echo translate_str_by_id(2568); ?>" id="textarea_for_value">'+value_html_entities+'</textarea>';
						//Текущее значение запоминаем "как есть".
						value_on_show = answer_json.value;//Запомнили значение в начале
						//Тогда при сравнении текущего значения со значением в textarea локика не нарушится, т.к. textarea.value возвращает то, что видит пользователь (не htmlentities-закодированную строку)
						
						
						on_edit_started();//Обработка "при редактировании". Чтобы указать, что нет несохраненных изменений. Для чего нужно. Без этого вызова будет баг, т.е. при Ctrl+S сработает textarea.onkeyup() и будет вызов этой функции до перезаписи value_on_show и скрипт будет думать, что есть несохраненные изменения. Поэтому, когда value_on_show перезаписан в строке выше - нужно обработать, чтобы скрипт понял, что несохраненных изменений нет.
					}
					else
					{
						alert(answer_json.message);
					}
				}
				
			}
		});
		
	}
	// ------------------------------------------------------------------------------------------------
	//Обработка начала редактирования перевода (т.е. появились несохраненные изменения)
	function on_edit_started()
	{
		//console.log(value_on_show);
		//console.log('----------------------------------------------------------------');
		//console.log(document.getElementById('textarea_for_value').value);
		
		//Если текущее значение не равно исходному - значит, есть изменения
		//if( value_on_show != document.getElementById('textarea_for_value').value )
		if( value_on_show.localeCompare( document.getElementById('textarea_for_value').value ) != 0 )
		{
			//Есть изменения
			
			edited_changes_saved = false;//Флаг "Все изменения сохранены" - нет
		
			//Кнопка Сохранить - Активна
			$('#modalStringEdit_save_button').prop( "disabled", false );
		}
		else
		{
			//Изменений нет
			
			edited_changes_saved = true;//Флаг "Все изменения сохранены" - да
		
			//Кнопка Сохранить - Не активна, т.е. сохранять нечего
			$('#modalStringEdit_save_button').prop( "disabled", true );
		}
	}
	// ------------------------------------------------------------------------------------------------
	//Функция сохранения перевода
	function save_str_translation()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Функция может вызываться по Ctrl+S, поэтому, сначала проверяем, есть ли, что сохранять.
		if( document.getElementById('textarea_for_value') == null || document.getElementById('textarea_for_value') == undefined )
		{
			//console.log("Нет textarea");
			return;
		}
		if( edited_changes_saved )
		{
			//console.log("Все изменения и так уже сохранены");
			return;
		}
		
		
		
		//Получаем значение из textarea
		var value = document.getElementById('textarea_for_value').value;
		if( value == '' )
		{
			alert('<?php echo translate_str_by_id(2565); ?>');
			return;
		}
		if( !value )
		{
			alert('<?php echo translate_str_by_id(2569); ?>');
			return;
		}
		
		
		
		//Чтобы не вызвать дважды подряд
		if( is_saving )
		{
			alert('<?php echo translate_str_by_id(2567); ?>');
			return;
		}
		is_saving = true;
		
		
		//Отправляем запрос на сервер для сохранения перевода
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_save_string_translation.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&str_key="+edited_str_key+"&lang_code="+edited_lang_code+"&value="+encodeURIComponent(value),
			complete: function(){is_saving = false;},
			success: function(answer_str)
			{				
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_id(2557); ?>: ' + answer_str );
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//СОХРАНИЛИ УСПЕШНО. ЗДЕСЬ ОБРАБАТЫВАЕМ УСПЕШНОЕ СОХРАНЕНИЕ
						
						//1. В items указываем, что есть такой перевод у данной строки. На всякий случай, находим его по key строки, а не по индексу
						//Ищем объект в items
						var item_index = 0;
						for( var i=0 ; i < items.length ; i++ )
						{
							//Нашли
							if( items[i].str_key == answer_json.str_key )
							{
								//Указываем, что у такой строки теперь есть перевод на такой язык (если еще не было до этого)
								items[i]['has_'+answer_json.lang_code] = 1;
								item_index = i;
								
								//И, если этот язык текущий в панели управления, то, записываем поле current_lang_translation
								if( answer_json.lang_code == '<?php echo get_work_lang(); ?>' )
								{
									items[i]['current_lang_translation'] = answer_json.value;
								}
								
								break;
							}
						}
						
						//2. Все изменения сохранены
						edited_changes_saved = true;
						
						
						//3. Перевызов функции, чтобы обновить в окне состояние
						edit_str_translation(item_index, answer_json.lang_code);
						
						
						//4. Перевызов функции, чтобы обновить отображение таблицы
						show_items_table();
					}
					else
					{
						alert(answer_json.message);
					}
				}
				
			}
		});
		
		
		
		
		
		//После получения результата о сохранении, записываем данные в объекты. Если у строки не было перевода на этот язык, то нужно указать в объекте, что теперь перевод есть и переотобразить.
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	<!-- START МОДАЛЬНОЕ ОКНО - Редактирование строки -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalStringEdit" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						
						<!-- Это div, в котором будут добавляться input для функции копирования str_key -->
						<div id="div_for_copy_input">
						</div>
						
						<h4 class="modal-title" id="modal_h4">
							<!-- Сюда грузится заголовок окна -->
						</h4>
						
						<!-- Блок для перекрытия всех элементов (пелена), кроме инпута редактирования описания строки -->
						<div id="transparent_mega_shadow_for_modal"></div>
						
						<p id="modal_p_description">
							<!-- Сюда грузится пояснение выбранной строки -->
						</p>
					</div>
					<div class="modal-body" id="modalStringEdit_workArea">
						<div class="row">
						
							<div class="col-lg-12" id="modal_lang_selector">
								<!-- Сюда грузяется круглые кнопки с выбором языка -->
							</div>

							<div class="col-lg-12" style="margin-top:10px;text-align:center;" id="div_for_text_area">
								<!-- Сюда грузится textarea для редактирования перевода -->
							</div>

						</div>
					</div>
					<div class="modal-footer">
						<button onclick="save_str_translation();" id="modalStringEdit_save_button" class="btn btn-success " type="button"><i class="fa fa-save"></i> <span class="bold"><?php echo translate_str_by_id(2114); ?></span></button>
					
						<button id="modalStringEdit_close_button" type="button" class="btn btn-primary" data-dismiss="modal"><i class="fas fa-times" id="i_in_modal_closeButton"></i> <?php echo translate_str_by_id(2447); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<style>
	/*Чтобы при открытии модального окна, страница не прокручивалась вверх*/
	.modal-open {
		overflow: visible !important; 
	}
	</style>
	<script>
	//Если есть несохраненные изменения - предупреждаем перед закрытием модального окна.
	$('#modalStringEdit').on('hide.bs.modal', function (e) {
		
		//Есть несохраненный перевод
		if( ! edited_changes_saved )
		{
			if( ! confirm('<?php echo translate_str_by_id(2570); ?>') )
			{
				//Нажали Отмена - Окно не закрылось - можно продолжить работать в окне.
				return false;
			}
		}
		
		
		//Если есть несохраненное описание строки (в модальном окне)
		//Вызов функции отмены редактирования описания строки (на случай, если идет редактирование)
		if( ! description_edit_cancel() )
		{
			//функция вернула false, значит пользователь захотел продолжить редактирование предыдущей строки
			return false;
		}
		
		
		
		edited_changes_saved = true;//Если закрываем окно - в любом случае ставим флаг, что все изменения сохранены
	});
	//-----------------------------------------------------------------------------------------
	</script>
	<script>
	//-----------------------------------------------------------------------------------------
	//Копируем str_key в буфер
	function copy_str_key(str_key, from_table = false)
	{
		var div_id = 'div_for_copy_input';
		if( from_table == true )
		{
			div_id = 'div_for_copy_input_on_page';
		}
		
		
		//Создаем временный input в контейнере. Содержимое - str_key
		document.getElementById(div_id).innerHTML = '<input type="text" id="input_for_copy" value="'+str_key+'" />';
		
		//Выделянем его содержимое
		jQuery('#input_for_copy').select();
		
		//Команда Копировать
		document.execCommand("copy");
		
		//Очищаем контейнер от временного input
		document.getElementById(div_id).innerHTML = "";
		
		//Показываем, что функция выполнилась
		toastr["success"]("<?php echo translate_str_by_key('1706196108_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>");
	}
	//-----------------------------------------------------------------------------------------
	//Открыть окно редактирования строки
	function open_modalStringEdit(item_index, lang_code)
	{
		//Заголовок с ID и описание строки
		document.getElementById("modal_h4").innerHTML = '<?php echo translate_str_by_id(2571); ?> '+ items[item_index].str_key + '<i class="far fa-copy" style="font-weight:Normal;font-size:0.5em;margin-left:5px;cursor:pointer;color:#000;" onclick="copy_str_key(\'' + items[item_index].str_key + '\');"></i>';
		document.getElementById("modal_p_description").innerHTML = '<div class="description_div_usual" id="str_description_modal_div_'+items[item_index].str_key+'"><span class="text_in_N_lines">' + items[item_index].description + '</span></div> <i id="pencil_modal_' + items[item_index].str_key + '" class="fas fa-pencil-alt" onclick="edit_str_description(\'' + items[item_index].str_key + '\', \'modal\');"></i>';
		
		
		//Кнопка Сохранить - Не активна
		$('#modalStringEdit_save_button').prop( "disabled", true );
		
		
		//Заполняем селектор языков
		document.getElementById('modal_lang_selector').innerHTML = "";//Очистили блок перед заполнением.
		var bg_color = '';//Фон круглой кнопки
		var text_color = '';//Цвет текста круглой кнопки
		var selected_lang_style = '';//Дополнительный стиль для выбранного языка
		<?php
		//Получаем существующие в платформе языки
		$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
		$languages_query->execute();
		//Для каждого языка генерим JavaScript с заполнением селектора языка в окне
		while( $language = $languages_query->fetch() )
		{
			?>
			//Если для строки нет перевода
			if( parseInt( items[item_index]['has_<?php echo $language['lang_code']; ?>'] ) == 0 )
			{
				bg_color = '#ea6557';//Красный
				text_color = '#fff';
			}
			else
			{
				bg_color = '#74d348';//Зеленый
				text_color = '#000';
			}
			//Обозначаем выбранный язык
			if( lang_code == '<?php echo $language['lang_code']; ?>' )
			{
				selected_lang_style = 'outline: 3px solid #333;';
			}
			else
			{
				selected_lang_style = '';
			}
			
			//Добавляем кнопку в блок селектора
			document.getElementById('modal_lang_selector').innerHTML += '<span class="circle_lang_button" onclick="edit_str_translation('+item_index+', \'<?php echo $language['lang_code']; ?>\');" style="'+selected_lang_style+'border-radius:50%; background-color:'+bg_color+';color:'+text_color+';display:inline-block;width:20px;height:20px;text-align:center;margin:2px;cursor:pointer;"><?php echo $language['lang_code']; ?></span>';
			<?php
		}
		?>
	
	
		//Блок для textarea. Ставим индикатор загрузки - до получения текущего значения с сервера
		document.getElementById('div_for_text_area').innerHTML = '<img src="/content/files/images/ajax-loader-transparent.gif" />';
		

		$('#modalStringEdit').modal();//ОТКРЫВАЕМ ОКНО (если оно еще не открыто)
	}
	</script>
	<!-- END МОДАЛЬНОЕ ОКНО - Редактирование строки -->
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<script>
	// ----------------------------------------------------------------------
	// ----------------------------------------------------------------------
	// ----------------------------------------------------------------------
	// БЛОК СОЗДАНИЯ НОВОЙ СТРОКИ
	// ----------------------------------------------------------------------
	// ----------------------------------------------------------------------
	// ----------------------------------------------------------------------
	var items_new = new Array();//Сюда добавляем ID строк, которые были добавлены до очередного переотображения
	
	//Открытие модального окна создания новой строки
	function create_new_str_OpenModal()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		//Перед открытием очищаем инпут
		document.getElementById('new_string_description_input').value = '';
		
		//Также сбрасываем значения селекта "Одинаковый для всех" и чекбокса "Является текстом ошибки" и другие
		document.getElementById('same_select_creating').value = 'no';
		document.getElementById('is_error_input_creating').checked = false;
		document.getElementById('used_found_select_creating').value = '1';
		document.getElementById('is_custom_input_creating').checked = false;
		
		$('#modalStringCreate').modal();
	}
	// ----------------------------------------------------------------------
	//Нажание кнопки "Создать"
	function create_new_str_Save()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		var new_str_description = document.getElementById('new_string_description_input').value;
		
		if( new_str_description == '' )
		{
			alert('<?php echo translate_str_by_id(2572); ?>');
			return;
		}
		
		
		var is_error = 0;
		if( document.getElementById('is_error_input_creating').checked == true )
		{
			is_error = 1;
		}
		
		var is_custom = 0;
		if( document.getElementById('is_custom_input_creating').checked == true )
		{
			is_custom = 1;
		}
		
		
		//Отправляем запрос на сервер для создания новой строки
		jQuery.ajax({
			type: "POST",
			async: true, //Запрос асинхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_create_new_string.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&description="+encodeURIComponent(new_str_description)+"&same="+document.getElementById('same_select_creating').value+"&is_error="+is_error+"&used_found="+document.getElementById('used_found_select_creating').value+"&is_custom="+is_custom,
			complete: function(){},
			success: function(answer_str)
			{
				//Получаем JSON из строки
				var answer_json = JSON.parse(answer_str);

				if( typeof answer_json.status === 'undefined' )
				{
					//При некорректном чтении JSON
					alert('<?php echo translate_str_by_id(2557); ?>: ' + answer_str );
				}
				else
				{
					//JSON прочитали
					if( answer_json.status == true )
					{
						//СОХРАНИЛИ УСПЕШНО. ЗДЕСЬ ОБРАБАТЫВАЕМ УСПЕШНОЕ СОХРАНЕНИЕ
						
						//Скрываем окно создания строки
						$('#modalStringCreate').modal('hide');
						
						//Добавляем ID новой строки в массив items_new, чтобы при прокрутке вниз, эта строка не подгружаласт с сервера
						items_new.push(answer_json.str.str_key);
						
						//Добавляем объект новой строки в массив items в начало
						items.unshift( answer_json.str );
						
						//Заново переотображаем таблицу
						show_items_table();
						
						//Прокрутили страницу вверх, чтобы пользователь увидел новую строку в таблице
						window.scrollTo(0, 0);
						
						//Сразу открываем окно редактирования переводов добавленной строки
						<?php
						//Получем первый язык
						$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
						$languages_query->execute();
						$language = $languages_query->fetch();//Получаем первый язык
						?>
						//Индекс строки 0, т.к. она добавлена в начало массива
						edit_str_translation(0, '<?php echo $language['lang_code']; ?>');
					}
					else
					{
						alert(answer_json.message);
					}
				}
				
			}
		});
	}
	// ----------------------------------------------------------------------
	</script>
	<!-- START МОДАЛЬНОЕ ОКНО - Создание новой строки -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalStringCreate" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title">
							<?php echo translate_str_by_id(2573); ?>
						</h4>
						<p>
							<?php echo translate_str_by_id(2574); ?>
						</p>
					</div>
					<div class="modal-body">
						<div class="row">
						
							<div class="col-lg-12">
								<input type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2575); ?>" id="new_string_description_input" />
							</div>
						
						</div>
						
						<div class="hr-line-dashed"></div>
						
						
						
						<div class="row">	
							<div class="form-group col-md-6">
								<label class="col-md-6 control-label" for="same_select_1">
									<?php echo translate_str_by_key('1704898609_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
									<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_key('1704898671_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');"><i class="fa fa-info"></i></button>
								</label>
								<div class="col-md-6">
									<select class="form-control" id="same_select_creating">
										<option value="no"><?php echo translate_str_by_key('2457'); ?></option>
										<?php
										$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
										$languages_query->execute();
										while( $language = $languages_query->fetch() )
										{
											?>
											<option value="<?php echo $language['lang_code']; ?>"><?php echo $language['lang_code']; ?></option>
											<?php
										}
										?>
									</select>
								</div>
							</div>
							
							<div class="form-group col-md-6">
								<label class="col-md-8 control-label" for="same_select_1">
									<?php echo translate_str_by_key('1704895610_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
									<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_key('1704898833_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');"><i class="fa fa-info"></i></button>
								</label>
								<div class="col-md-4">
									<input type="checkbox" class="form-control" id="is_error_input_creating" />
								</div>
							</div>
							
							<div class="form-group col-md-12">
							</div>
							
							
							
							
							
							<div class="form-group col-md-6">
								<label class="col-md-6 control-label" for="">
									<?php echo translate_str_by_key('1706196192_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
									<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_key('1706196217_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');"><i class="fa fa-info"></i></button>
								</label>
								<div class="col-md-6">
									<select class="form-control" id="used_found_select_creating" />
										<option value="1"><?php echo translate_str_by_key('1706196316_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
										<option value="2"><?php echo translate_str_by_key('1706195647_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
										<option value="0"><?php echo translate_str_by_key('1706196425_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></option>
									</select>
								</div>
							</div>
							
							
							
							<div class="form-group col-md-6">
								<label class="col-md-8 control-label" for="">
									<?php echo translate_str_by_key('1706196452_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
									<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_key('1706196482_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');"><i class="fa fa-info"></i></button>
								</label>
								<div class="col-md-4">
									<input type="checkbox" class="form-control" id="is_custom_input_creating" />
								</div>
							</div>
							
							
							
						</div>
						

						
					</div>
					<div class="modal-footer">
						<button onclick="create_new_str_Save();" class="btn btn-success " type="button"><i class="fa fa-save"></i> <span class="bold"><?php echo translate_str_by_id(2292); ?></span></button>
					
						<button type="button" class="btn btn-primary" data-dismiss="modal"><i class="fas fa-times"></i> <?php echo translate_str_by_id(2190); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<!-- END МОДАЛЬНОЕ ОКНО - Создание новой строки -->
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<script>
	//После загрузки страницы - добавляем обработку событий
	window.onload = function() 
	{
		// -------------------------------------------------------------------
		//Если есть несохраненные изменения - предупреждаем пользователя перед закрытием страницы браузера. Также, если происходит поиск использования строк или удаление неиспользуемых строк
		window.addEventListener("beforeunload", function (e) {
			
			//Если изменения сохранены, и ничего не происходит в плане обработки used_found - все ок, просто закрываем страницу
			if ( edited_changes_saved && edited_description_changes_saved && parseInt(used_found_process_state) == 0 )
			{
				return undefined;
			}
			
			//Если изменения не сохранены - предупреждаем
			var confirmationMessage = 'It looks like you have been editing something. '
									+ 'If you leave before saving, your changes will be lost.';
			
			(e || window.event).returnValue = confirmationMessage; //Gecko + IE
			return confirmationMessage; //Gecko + Webkit, Safari, Chrome etc.
		});
		// -------------------------------------------------------------------
		//Добавляем событие прокрутки вниз - для получения следующих строк
		$(window).scroll(function() {
			if($(window).scrollTop() + $(window).height() == $(document).height()) {
				limit_from = items.length - items_new.length;
				//limit_from = items.length;
				limit_count = limit_count_option;
				
				get_text_strings();
			}
		});
		// -------------------------------------------------------------------
	};
	
	
	
	
	
	// -------------------------------------------------------------------
	//Перехват Ctrl+S для сохранения переводов 
	document.addEventListener('keydown', function(event) {
		if (event.ctrlKey && (event.key === 's' || event.key === 'S' || event.key === 'ы' || event.key === 'Ы' ) ) 
		{
			event.preventDefault();//Не показываем стандартный диалог сохранения
			
			<?php
			//Наша функция сохранения перевода
			if( $lang_editor_mode == 'two_cols' )
			{
				//Для режима "Две колонки"
				?>
				save_side_translation();
				<?php
			}
			else
			{
				//Для основного режима
				?>
				save_str_translation();
				<?php
			}
			?>
		}
	});
	// -------------------------------------------------------------------
	</script>
	
	
	
	
	
	<script>
	filterItems();//Если были выставлены фильтры через куки - они будут применены
	//get_text_strings();//Первый запрос - вызов уже есть в filterItems()
	</script>
	
	
	
	
	<style>
	/*Обрезка текста в одну строку (описание, перевод на текущий язык)*/
	.text_in_N_lines
	{
		display: -webkit-box;
		-webkit-line-clamp: 1;
		-webkit-box-orient: vertical;
		overflow: hidden;
		text-overflow: ellipsis;
		
		font-weight:bold;
	}
	/*Чтобы карандаш не смещался ниже строки*/
	.fa-pencil-alt
	{
		vertical-align: top;
		margin-top:2px;
	}
	/*Див для описания в обычном режиме (содержит спан с текстом). Используется также для дива в колонке с переводом строки на текущий язык ПУ*/
	.description_div_usual
	{
		display:inline-block;
		max-width:95%;
	}
	/*Див для описания в режиме редактирования (когда в нем отображается инпут)*/
	.description_div_editing
	{
		/*margin-bottom:4px; - это уже не нужно, т.к. круглые кнопки теперь в другой колонке*/
	}
	/*Селекты "Одинаковая для всех" и "Использование строки найдено" в таблице. Без рамки, с закругленными углами*/
	.same_select_list,
	.used_found_select_list
	{
		border:0;
		border-radius:3px;
	}
	</style>
	
	
	
	
	<?php
	// -------------------------------------------------------------------
	//Функции поиска использования строк и удаления неиспользуемых строк
	?>
	<!-- START МОДАЛЬНОЕ ОКНО - Индикация процесса поиска использования строк и процесса удаления неиспользуемых строк -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modal_used_found" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title">
							<?php echo translate_str_by_key('1706196573_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
						</h4>
						<p>
							<?php echo translate_str_by_key('1706196599_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>
						</p>
					</div>
					<div class="modal-body">
						<div class="row" id="modal_used_found_body">	
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
	<!-- Контейнеры для хранения HTML под разные состояния окна used_found -->
	<div style="display:none;" id="used_found_process_state_0">
		<button class="btn btn-success " type="button" onclick="search_used_found();"><i class="fa fa-search"></i> <span class="bold"><?php echo translate_str_by_key('1706196657_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></span></button>
		<button class="btn btn-danger" type="button" onclick="delete_not_used_strings();"><i class="fa fa-trash-o"></i> <span class="bold"><?php echo translate_str_by_key('1706196685_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?></span></button>
	</div>
	<div style="display:none;" id="used_found_process_state_1">
		<div style="text-align:center;">
			<?php echo translate_str_by_key('1706196714_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>...<br>
			<i class="fas fa-spinner fa-pulse"></i>
		</div>
	</div>
	<div style="display:none;" id="used_found_process_state_2">
		<div style="text-align:center;">
			<?php echo translate_str_by_key('1706196752_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>...<br>
			<i class="fas fa-spinner fa-pulse"></i>
		</div>
	</div>
	<div style="display:none;" id="used_found_process_state_3">
		<div style="text-align:center;">
			<?php echo translate_str_by_key('1706196827_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?><br>
			<i class="fas fa-exclamation-triangle" style="color:#C33;font-size:3em;"></i>
		</div>
	</div>
	<!-- END МОДАЛЬНОЕ ОКНО - Индикация процесса поиска использования строк и процесса удаления неиспользуемых строк -->
	<script>
	// -------------------------------------------------------------------
	//Обработчик окончания выполнения скриптов обработки used_found (когда заканчивается поиск или удаление неиспользуемых строк)
	// -------------------------------------------------------------------
	//Здесь добавим обработчики для jQuery
	jQuery('document').ready(function(){
		//Завершен AJAX-запрос
		jQuery(document).ajaxComplete(function(event,xhr,options){
			
			//Получаем текст ответа от серверного скрипта
			var answer_string = "";
			if( typeof xhr.responseText != 'undefined' )
			{
				answer_string = xhr.responseText;
			}
			
			//1. Обработка ответа от скрипта простановки флага used_found (ajax_search_used_found.php) (признак - search_used_found)
			if( typeof options.search_used_found != 'undefined' )
			{
				//Переводим ответ в объект
				var answer_ob = new Object;
				try
				{
					answer_ob = JSON.parse(answer_string);
					
					if( typeof answer_ob.status === 'undefined' )
					{
						//При некорректном чтении JSON
						alert('<?php echo translate_str_by_key('1706196877_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
					}
					else
					{
						//JSON прочитали
						if( answer_ob.status == true )
						{
							//УСПЕХ
							used_found_process_state = 0;//Указываем текущее состояние - ничего не происходит
							all_inputs_disabled(false);//Делаем активными все инпуты и селекты на всей странице
							filterItems();//Переотображаем строки по фильтру
							
							alert('<?php echo translate_str_by_key('1706196920_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
						}
						else
						{
							alert(answer_ob.message + ". <?php echo translate_str_by_key('1706196976_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>");
						}
					}
				}
				catch(err)
				{
					alert(err.message);
				}
				
				
				//Выставляем текущее состояние в ошибку
				if( used_found_process_state != 0 )
				{
					used_found_process_state = 3;
				}
				init_modal_used_found();//Иницализация окна индикации процесса под текущее состояние (будут либо кнопки, либо сообщение об ошибке)
			}
			//2. Обработка ответа от скрипта удаления неиспользуемых строк (ajax_delete_not_used_found.php) (признак - delete_not_used_found)
			else if( typeof options.delete_not_used_found != 'undefined' )
			{
				//Переводим ответ в объект
				var answer_ob = new Object;
				try
				{
					answer_ob = JSON.parse(answer_string);
					
					if( typeof answer_ob.status === 'undefined' )
					{
						//При некорректном чтении JSON
						alert('<?php echo translate_str_by_key('1706197024_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
					}
					else
					{
						//JSON прочитали
						if( answer_ob.status == true )
						{
							//УСПЕХ
							used_found_process_state = 0;//Указываем текущее состояние - ничего не происходит
							all_inputs_disabled(false);//Делаем активными все инпуты и селекты на всей странице
							filterItems();//Переотображаем строки по фильтру
							
							alert('<?php echo translate_str_by_key('1706197120_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>');
						}
						else
						{
							alert(answer_ob.message);
						}
					}
				}
				catch(err)
				{
					alert(err.message);
				}
				
				
				//Выставляем текущее состояние в ошибку
				if( used_found_process_state != 0 )
				{
					used_found_process_state = 3;
				}
				init_modal_used_found();//Иницализация окна индикации процесса под текущее состояние (будут либо кнопки, либо сообщение об ошибке)
			}
			//3. Обработка ответа при сохранении перевода строки (режим "Две колонки")
			else if( typeof options.side_editing_translation != 'undefined' )
			{
				//Переводим ответ в объект
				var answer_ob = new Object;
				try
				{
					answer_ob = JSON.parse(answer_string);
					
					if( typeof answer_ob.status === 'undefined' )
					{
						//При некорректном чтении JSON
						alert('<?php echo translate_str_by_id(2557); ?>: ' + answer_string );
					}
					else
					{
						//JSON прочитали
						if( answer_ob.status == true )
						{
							//СОХРАНИЛИ УСПЕШНО. ЗДЕСЬ ОБРАБАТЫВАЕМ УСПЕШНОЕ СОХРАНЕНИЕ
						
							//1. В items указываем, что есть такой перевод у данной строки. На всякий случай, находим его по key строки, а не по индексу
							//Ищем объект в items
							var item_index = 0;
							for( var i=0 ; i < items.length ; i++ )
							{
								//Нашли
								if( items[i].str_key == answer_ob.str_key )
								{
									//Указываем, что у такой строки теперь есть перевод на такой язык (если еще не было до этого)
									items[i]['has_'+answer_ob.lang_code] = 1;
									item_index = i;
									
									//И, если этот язык текущий в панели управления, то, записываем поле current_lang_translation (хотя возможно для режима "Две колонки" это лишнее)
									if( answer_ob.lang_code == '<?php echo get_work_lang(); ?>' )
									{
										items[i]['current_lang_translation'] = answer_ob.value;
									}
									
									break;
								}
							}
							
							
							//Индикация для режима "Две колонки"
							//Код обработки успешного сохранения
							//Подсвечиваем область зеленым
							document.getElementById(options.side+'_lang_td_'+answer_ob.str_key).setAttribute('style', 'background-color:#b0e19a; border-radius:7px;');
							//Через таймаут снимаем подсветку с анимацией
							setTimeout(function(){ 
									document.getElementById(options.side+'_lang_td_'+answer_ob.str_key).setAttribute('style', 'background-color:inherit; border-radius:7px; transition: background-color 1000ms linear;');
								},
							10);
							side_editing_saved = true;//Флаг - все изменения сохранены
							prev_side_lang_translation_value = new Array;//Сбрасываем весь массив исходных значений textarea
							on_side_editing_focus(options.side, answer_ob.str_key);//textarea сохраняет фокус, поэтому вызываем обработчик получения фокусы
							
						}
						else
						{
							alert(answer_ob.message);
						}
					}
				}
				catch(err)
				{
					alert(err.message);
				}
			}
			
			
		});
	});
	// -------------------------------------------------------------------
	//Функция отключения/включения всех инпутов и селектов
	function all_inputs_disabled(disabled_on)
	{
		jQuery('input').attr('disabled', disabled_on);
		jQuery('select').attr('disabled', disabled_on);
	}
	// -------------------------------------------------------------------
	//Запуск поиска использования текстовых строк
	function search_used_found()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		used_found_process_state = 1;//Выставляем флаг текущего состояния процесса used_found
		
		all_inputs_disabled(true);//Делаем неактивными все инпуты и селекты на всей странице
		
		init_modal_used_found();//Иницализация окна индикации процесса под текущее состояние
		
		//Отправляем запрос на сервер для простановки флага used_found
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_search_used_found.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			complete: function(){},
			search_used_found:'yes',//Опция для обработке в общем обработчике ответа AJAX
			success: function(answer_str)
			{
				//Обработка ответа будет выше - в обработчика завершения AJAX
			}
		});
	}
	// -------------------------------------------------------------------
	//Запуск удаления неиспользуемых строк
	function delete_not_used_strings()
	{
		//Запрещаем действия, если идет обработка used_found (поиск или удаление неиспользуемых строк)
		if( is_used_found_processing() )
		{
			return false;
		}
		
		
		if( !confirm("<?php echo translate_str_by_key('1706197161_1_5f735d1486aa51eb9a61df1cd635a0fb'); ?>") )
		{
			return;
		}
		
		
		used_found_process_state = 2;//Выставляем флаг текущего состояния процесса used_found
		
		all_inputs_disabled(true);//Делаем неактивными все инпуты и селекты на всей странице
		
		init_modal_used_found();//Иницализация окна индикации процесса под текущее состояние
		
		//Отправляем запрос на сервер для простановки флага used_found
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/lang/ajax_delete_not_used_found.php",
			dataType: "text",//Тип возвращаемого значения
			data: "csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			complete: function(){},
			delete_not_used_found:'yes',//Опция для обработке в общем обработчике ответа AJAX
			success: function(answer_str)
			{
				//Обработка ответа будет выше - в обработчика завершения AJAX
			}
		});
	}
	// -------------------------------------------------------------------
	//Функция открытия модального окна с управлением used_found
	function open_modal_used_found()
	{
		//Иницализация окна в соответстии с текущим состоянием
		init_modal_used_found();
		
		$('#modal_used_found').modal();
	}
	// -------------------------------------------------------------------
	//Иницализация окна used_found в соответствии с состоянием used_found_process_state
	function init_modal_used_found()
	{
		//В зависимости от текущего состояния показываем соответствующее содержимое окна
		document.getElementById('modal_used_found_body').innerHTML = document.getElementById('used_found_process_state_' + used_found_process_state ).innerHTML;
	}
	// -------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	
	
	
	
	<!-- Модальное окно для отображения справочной информации о строке (для режима "Две колонки") -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modal_str_info" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title" id="modal_str_info_h4">
						</h4>
						<p id="modal_str_info_p">
						</p>
					</div>
					<div class="modal-body">
						<div class="row" id="modal_str_info_body">
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	
	<?php
}
?>