<?php
//Модуль выбора языка панели управления
defined('_ASTEXE_') or die('No access');
?>


<?php
//Если включен режим мультиязычности
if( $DP_Config->multilang )
{
	//Если включено более 1 языка
	$langs_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ?;');
	$langs_query->execute( array(1) );
	if( $langs_query->fetchColumn() > 1 )
	{
		?>
		<div class="text-center" style="padding:5px;border-top:1px solid #e4e5e7;">
			<?php echo translate_str_by_id(3993); ?>:
			<select class="form-control" onchange="lang_selected(this.value);">
				<?php	
				//Получаем перечень языков и выводим селектор
				$langs_query = $db_link->prepare('SELECT * FROM `lang_languages` WHERE `active` = ?;');
				$langs_query->execute( array(1) );
				while( $lang = $langs_query->fetch() )
				{
					//Выбираем текущий язык
					$selected = '';
					if( $lang['lang_code'] == $multilang_params['lang'] )
					{
						$selected = ' selected="selected" ';
					}
					
					?>
					<option value="<?php echo $lang['lang_code']; ?>" <?php echo $selected; ?>><?php echo strtoupper($lang['lang_code']); ?> | <?php echo translate_str_by_id($lang['caption_str_key'], $lang['lang_code']); ?></option>
					<?php
				}
				?>
			</select>
		</div>
		
		<script>
		//На случай, если модуль подключается в нескольких местах
		if( typeof lang_selected != 'function' )
		{
			window.lang_selected = function(lang)
			{
				//Записать в куки язык
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "lang_cp="+lang+"; path=/; expires=" + date.toUTCString();
				
				//Перезагрузить страницу
				location = location;
			};
		}
		</script>
		
		<?php
	}
}
?>