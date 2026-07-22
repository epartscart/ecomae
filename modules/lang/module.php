<?php
//Модуль выбора языка
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
		<select onchange="lang_selected(this.value);" style="border:0;width:40px;background:transparent;" class="lang_select">
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
		
		
		
		<script>
	//На случай, если модуль подключается в нескольких местах
	if( typeof lang_selected != 'function' )
	{
		window.lang_selected = function(lang)
		{
			lang = String(lang || '').toLowerCase().replace(/[^a-z\-]/g, '');
			if (!lang) {
				return;
			}
			//Записать в куки язык
			var date = new Date(new Date().getTime() + 15552000 * 1000);
			document.cookie = "lang="+lang+"; path=/; expires=" + date.toUTCString();
			
			//Перейти на URL этой же страницы, но, для другого языка
			var page_url_with_lang_tag = '<?php echo isset($multilang_params['page_url_with_lang_tag']) ? $multilang_params['page_url_with_lang_tag'] : ''; ?>';
			var target = '';
			if (page_url_with_lang_tag && page_url_with_lang_tag.indexOf('<lang>') !== -1) {
				target = page_url_with_lang_tag.replace('<lang>', lang);
			} else {
				// Fallback: swap first path segment when it looks like a language code
				var path = window.location.pathname || '/';
				var search = window.location.search || '';
				var hash = window.location.hash || '';
				var parts = path.split('/');
				// path like /en/parts/... → ["", "en", "parts", ...]
				if (parts.length > 1 && /^[a-z]{2}(?:-[a-zA-Z]+)?$/i.test(parts[1] || '')) {
					parts[1] = lang;
					target = parts.join('/') + search + hash;
				} else {
					target = '/' + lang + (path === '/' ? '/' : path) + search + hash;
				}
			}
			if (target) {
				window.location.assign(target);
			}
		};
	}
</script>
		
		
		
		<?php
	}
}
?>