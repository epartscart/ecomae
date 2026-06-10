<?php
//Скрипт для вывода содержимого таба "Поиск по наименованию"
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>
<div class="search_tab_clar"><?php echo translate_str_by_id(4177); ?></div>
<form role="form" action="<?php echo $multilang_params['lang_href']; ?>/shop/search" method="GET">
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	<div class="input-group">
		<input value="<?php echo $value_for_input_search_string; ?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4178); ?>" name="search_string" />
		<span class="input-group-btn">
			<button class="btn btn-ar btn-default" type="submit"><?php echo translate_str_by_id(2763); ?></button>
		</span>
	</div>
</form>