<?php
/**
 * Скрипт для модуля поисковой строки
 * 
 * Поисковая строка - универсальная, используется для поиска:
 * - по наименованию в каталоге Treelax
 * - по артикулу у поставщиков автозапчастей
 * 
 * 
 * Необходимо сделать графический элемент для выбора покупателем способа поиска:
 * - по артикулу;
 * - по наименованию.
 * 
 * Сейчас выбор жестко прописан в коде страницы
*/

$value_for_input = "";//Значение, которое подставляется в input при загрузке страницы
if(!empty($_GET["article"]))
{
	$value_for_input = $_GET["article"];
}

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>




<div class="search_mode_wrap">
    <input type="radio" id="radio_search_mode_1" name="search_mode" value="docpart_article" checked="checked" /> <label for="radio_search_mode_1"> <?php echo translate_str_by_id(4772); ?></label>
    <input type="radio" id="radio_search_mode_2" name="search_mode" value="treelax_catalogue" /> <label for="radio_search_mode_2"> <?php echo translate_str_by_id(4773); ?></label>
</div>

<div class="search_block_container">
    <input type="text" class="search_string_input" id="entered_string" value="<?php echo $value_for_input; ?>" placeholder="<?php echo translate_str_by_id(4774); ?>..." />
    <a class="search_string_button" href="javascript:void(0);" onclick="submit_form();"><?php echo translate_str_by_id(2379); ?></a>
</div>
<script>
document.getElementById('entered_string').onkeypress = function(e){
    if (!e) e = window.event;
    var keyCode = e.keyCode || e.which;
    if (keyCode == '13')
	{
		submit_form();
    }
  }
</script>




<script>
//Метод отправки формы
function submit_form()
{
    var search_mode = $('input[name="search_mode"]:checked').val();
    
    
    var entered_string = document.getElementById("entered_string").value;
    
    //Поиск по наименованию в каталоге Treelax
    if(search_mode == "treelax_catalogue")
    {
        document.getElementById("search_string").value = entered_string;
        document.forms["treelax_search_form"].submit();
    }
    else if(search_mode == "docpart_article")//Поиск автозапчастей по артикулу
    {
        document.getElementById("article").value = entered_string;
        document.forms["part_search_form"].submit();
    }
    
}
</script>





<!-- Форма поиска по наименованию в каталоге Трилакс -->
<form action="<?php echo $multilang_params['lang_href']; ?>/shop/search" method="GET" style="display:none" name="treelax_search_form">
    <input type="hidden" name="search_string" id="search_string" value="" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
</form>


<!-- Форма поиска по артикулу у поставщиков автозапчастей -->
<form action="<?php echo $multilang_params['lang_href']; ?>/shop/part_search" method="GET" style="display:none" name="part_search_form">
    <input type="hidden" name="article" id="article" value="" />
	<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
</form>