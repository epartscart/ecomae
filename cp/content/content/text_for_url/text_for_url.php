<?php
/*Страничный скрипт для одного дополнительного текста*/
defined('_ASTEXE_') or die('No access');

//Мультиязычность
$translated_items = array('text', 'title_tag', 'description_tag', 'keywords_tag');
?>


<?php
if( !empty($_POST["action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	$url = $_POST["url"];
	$text = $_POST["text"];
	$title_tag = htmlentities($_POST["title_tag"], ENT_QUOTES, "UTF-8", false);
	$description_tag = htmlentities($_POST["description_tag"], ENT_QUOTES, "UTF-8", false);
	$keywords_tag = htmlentities($_POST["keywords_tag"], ENT_QUOTES, "UTF-8", false);
	$before_main = $_POST["before_main"];
	$SQL = "";
	
	
	
	//Не допускаем вставку php-кода при текстовом типе контента
	if( true )
	{
		do
		{
			$text = str_replace('<?', '[CODE]', $text);
		}while( strpos($text, '<?') !== false );
		
		
		do
		{
			$text = str_replace('?>', '[/CODE]', $text);
		}while( strpos($text, '?>') !== false );
	}
	
	
	
	//Мультиязычность. Кастомный алгоритм
	for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
	{
		${$translated_items[$i]} = save_custom_translation($_POST[$translated_items[$i]."_lang_str_id"], ${$translated_items[$i]});
	}
	
	
	
	//Проверяем, есть ли такой URL и формируем SQL запрос
	$check_url_query = $db_link->prepare("SELECT `id` FROM `text_for_url` WHERE `url` = ?;");
	$check_url_query->execute( array($url) );
	$check_url_record = $check_url_query->fetch();
	if( $check_url_record != false )
	{
		$SQL = "UPDATE `text_for_url` SET `content` = ?, `before_main` = ?, `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?  WHERE `id` = ?;";
		
		$sql_result = $db_link->prepare($SQL)->execute( array($text, $before_main, $title_tag, $description_tag, $keywords_tag, $check_url_record["id"]) );
	}
	else
	{
		$SQL = "INSERT INTO `text_for_url` (`content`, `url`, `before_main`, `title_tag`, `description_tag`, `keywords_tag`) VALUES (?, ?, ?, ?, ?, ?);";
		
		$sql_result = $db_link->prepare($SQL)->execute( array($text, $url, $before_main, $title_tag, $description_tag, $keywords_tag) );
	}
	
	if( $sql_result != true )
	{
		$error_message = translate_str_by_id(2370);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/content/dopolnitelnye-teksty/dopolnitelnyj-tekst?error_message=<?php echo urlencode($error_message); ?>&url=<?php echo urlencode($url); ?>";
		</script>
		<?php
		exit();
	}
	else
	{
		$success_message = translate_str_by_id(2157);
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/content/dopolnitelnye-teksty/dopolnitelnyj-tekst?success_message=<?php echo urlencode($success_message); ?>&url=<?php echo urlencode($url); ?>";
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
	
	<?php
	$url = '';
	$text = "";
	$title_tag = "";
	$description_tag = "";
	$keywords_tag = "";
	
	
	//Мультиязычность
	for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
	{
		${$translated_items[$i]."_lang_str_id"} = 0;
		${$translated_items[$i]} = "";
	}
	
	
	
	$before_main = 0;
	if( !empty($_GET["url"]) )
	{
		$url = $_GET["url"];
	}
	//Получаем текущее содержимое страницы
	$text_query = $db_link->prepare("SELECT * FROM `text_for_url` WHERE `url` = ?;");
	$text_query->execute( array($url) );
	$text_record = $text_query->fetch();
	if( $text_record != false )
	{
		$text = $text_record["content"];
		
		
		$title_tag = $text_record["title_tag"];
		$description_tag = $text_record["description_tag"];
		$keywords_tag = $text_record["keywords_tag"];
		
		$before_main = $text_record["before_main"];
		
		
		//Мультиязычность
		for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
		{
			${$translated_items[$i]."_lang_str_id"} = ${$translated_items[$i]};
			${$translated_items[$i]} = translate_str_by_id(${$translated_items[$i]});
		}
		
		$text = addcslashes(str_replace(array("\n","\r"), '', $text), "'");
		$text = str_replace("/", "\/", $text);
	}
	?>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Действия
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/content/dopolnitelnye-teksty">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/text_for_url.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2371); ?></div>
				</a>
				
				

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
				<?php echo translate_str_by_id(2117); ?>
			</div>
			<div class="panel-body">
				<div class="row">
					
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2372); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" class="form-control" name="page_url" id="page_url" value="<?php echo $url; ?>" />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12 hidden-lg"></div>
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-9 control-label">
							<?php echo translate_str_by_id(2373); ?>
						</label>
						<div class="col-lg-3">
							<?php
							$checked = "";
							if( $before_main )
							{
								$checked = " checked = 'checked' ";
							}
							?>
							<input type="checkbox" class="form-control" name="before_main_check" id="before_main_check" <?php echo $checked; ?> />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
				
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2167); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" class="form-control" id="title_tag_input" value="<?php echo $title_tag; ?>" />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12 hidden-lg"></div>
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2168); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" class="form-control" id="description_tag_input" value="<?php echo $description_tag; ?>" />
						</div>
					</div>
					
					<div class="hr-line-dashed col-lg-12"></div>
					
					<div class="form-group col-lg-6">
						<label for="" class="col-lg-6 control-label">
							<?php echo translate_str_by_id(2169); ?>
						</label>
						<div class="col-lg-6">
							<input type="text" class="form-control" id="keywords_tag_input" value="<?php echo $keywords_tag; ?>" />
						</div>
					</div>
					
					
				</div>
			</div>
		</div>
	</div>
	
	
	
	

	

	
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2374); ?>
			</div>
			<div class="panel-body">
				<div id="content_value_area"></div>
			</div>
		</div>
	</div>
	
	
	<!-- Для загрузки файлов через TinyMCE -->
	<iframe id="file_form_target" name="file_form_target" style="display:none"></iframe>
	<form id="file_form" action="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/lib/tinymce/postAcceptor.php" target="file_form_target" method="post" enctype="multipart/form-data" style="width:0px;height:0;overflow:hidden">
		<input id="image_input" name="image" type="file" onchange="onFileSelected();">
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
    <script>
	//Обработка выбора файла текстовом редакторе
	function onFileSelected()
	{
		//Создаем данные для формы
		var formData = new FormData();
		formData.append('image', $('input[type=file]')[0].files[0]); 
		formData.append('csrf_guard_key', '<?php echo $user_session["csrf_guard_key"]; ?>');
		
		//Передаем форму с файлом на сервер
		$.ajax({
			url: '<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/lib/tinymce/postAcceptor.php',
			data: formData,
			dataType:"json",
			type: "POST",
			contentType: false,
			processData: false,
			success : function (answer){
				//console.log("Ответ сервера: "+answer);
				
				if(answer.status == true)
				{
					//Указываем имя файл в окне его выбора от TinyMCE и закрываем окно
					top.$('.mce-btn.mce-open').parent().find('.mce-textbox').val(answer.url).closest('.mce-window').find('.mce-primary').click();
					
					//Очищаем input
					document.getElementById("image_input").value = '';
				}
				else
				{
					alert("<?php echo translate_str_by_id(2122); ?>: "+answer.message)
					
					//Очищаем input
					document.getElementById("image_input").value = '';
				}
			}
		})
	}
	</script>
	
	

    <form method="POST" name="save_form" style="display:none;">
		<input type="hidden" name="action" value="ok" />
		<input type="hidden" name="url" id="url" value="" />
		<input type="hidden" name="text" id="text" value="" />
		<input type="hidden" name="before_main" id="before_main" value="" />
		
		<input type="hidden" name="title_tag" id="title_tag" value="" />
		<input type="hidden" name="description_tag" id="description_tag" value="" />
		<input type="hidden" name="keywords_tag" id="keywords_tag" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		
		<?php
		//Мультиязычность
		for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
		{
			?>
			<input type="hidden" name="<?php echo $translated_items[$i]."_lang_str_id"; ?>" value="<?php echo ${$translated_items[$i]."_lang_str_id"}; ?>" />
			<?php
		}
		?>
		
	</form>
    <script>
    //-----------------------------------------------------
    //Нажатие Сохранить
    function save_action()
    {
        document.getElementById("url").value = document.getElementById("page_url").value;
		
		document.getElementById("text").value = tinymce.activeEditor.getContent();;
		
		var before_main_value = 0;
		if(document.getElementById("before_main_check").checked == true)
		{
			before_main_value = 1;
		}
		document.getElementById("before_main").value = before_main_value;
		
		document.getElementById("title_tag").value = document.getElementById("title_tag_input").value;
		document.getElementById("description_tag").value = document.getElementById("description_tag_input").value;
		document.getElementById("keywords_tag").value = document.getElementById("keywords_tag_input").value;
		
		document.forms["save_form"].submit();
    }
    //-----------------------------------------------------
    //Инициализация редактора
    function init_TinyMCE()
    {
        var content_value_area = document.getElementById("content_value_area");
        

		content_value_area.innerHTML = "<textarea style=\"min-height:400px\" class=\"tinymce_editor\" id=\"tinymce_editor\"></textarea>";
		tinymce.init({
			selector: "textarea.tinymce_editor",
			plugins: [
				"advlist autolink lists link image charmap print preview anchor",
				"searchreplace visualblocks code fullscreen",
				"insertdatetime media table contextmenu paste textcolor"
			],
			toolbar: [ 
					"newdocument | bold italic underline strikethrough | alignleft aligncenter alignright alignjustify | styleselect | formatselect | fontselect | fontsizeselect | ", 
					"cut copy paste | bullist numlist | outdent indent | blockquote | undo redo | removeformat subscript superscript | link image | forecolor backcolor",
			],
			file_browser_callback: function(field_name, url, type, win) {
				if(type=='image') $('#file_form input').click();
			}
		});
		
		
		//Заполняем текущее содержимое:
		var text = '<?php echo $text; ?>';
		console.log(text);
		document.getElementById("tinymce_editor").value = text;

		
    }//~function init_TinyMCE()
    //-----------------------------------------------------

    
    
    
    //-------------- ДЕЙСВТВИЯ ПОСЛЕ ЗАГРУЗКИ СТРАНИЦЫ -------------->
    init_TinyMCE();
    </script>
	<?php
}

?>