<?php
defined('_ASTEXE_') or die('No access');
//Скрипт модуля для принятия Пользовательского соглашения
?>
<div id="users_agreement_div" style="padding: 0px 15px; border: 1px solid #ddd; background: #f7f7f7; margin:20px 0px;">
	<table>
		<tr>
			<td><input style="width:25px; height:25px; cursor:pointer;" type="checkbox" id="users_agreement" onchange="on_agreement_changed();"/></td>
			<td style="line-height: 1.2em; padding: 15px 5px;"><label style="cursor:pointer;" for="users_agreement"><?php echo translate_str_by_id(4751); ?> <a style="text-decoration:underline;" target="_blank" href="<?php echo $multilang_params['lang_href']; ?>/polzovatelskoe-soglashenie"><?php echo translate_str_by_id(4752); ?></a> <?php echo translate_str_by_id(4753); ?></label></td>
		</tr>
	</table>
</div>
<script>
	document.cookie = "users_agreement=no; path=/;";//Предвартельно снимаем согласие
	//Обработка галочки "Согласие с пользовательским соглашением"
	function on_agreement_changed()
	{
		if( document.getElementById("users_agreement").checked )
		{
			document.cookie = "users_agreement=yes; path=/;";
		}
		else
		{
			document.cookie = "users_agreement=no; path=/;";
		}
	}
	//Проверка согласия с обработкой персональных данных
	function check_user_agreement()
	{
		//Проверка согласия с пользовательским соглашением
		if(!document.getElementById("users_agreement").checked)
		{
			alert("<?php echo translate_str_by_id(4754); ?>");
			return false;
		}
		return true;
	}
</script>