<?php
//Скрипт для вывода содержимого таба "Каталог ТО" (от Ucats)
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>
<div class="search_tab_clar"><?php echo translate_str_by_id(4188); ?>:</div>
<div id="tab_to_catalogue">
</div>


<script>
jQuery.ajax({
	type: "GET",
	async: true,
	url: "/content/shop/catalogue/search_tabs/tabs_content/to_catalogue/ajax_get_to_marks.php"+"?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
	dataType: "text",//Тип возвращаемого значения
	success: function(answer)
	{
		//console.log(answer);
		
		document.getElementById("tab_to_catalogue").innerHTML = answer;
	}
});
</script>