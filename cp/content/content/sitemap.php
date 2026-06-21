<?php
/**
 * Страничный скрипт для генерации файла sitemap.xml
*/
defined('_ASTEXE_') or die('No access');
?>

<div id="messages"></div>


<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			<a class="panel_a" onClick="create_sitemap();" href="javascript:void(0);">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2292); ?></div>
			</a>


			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>






<?php
require_once("content/content/dp_content_record.php");//Определение класса записи материала
require_once("content/content/get_content_records.php");//Получение объекта иерархии существующих материалов для вывода в дерево-webix

//Получить текущий главный материал (id дерева webix)
$get_main_id_query = $db_link->prepare("SELECT * FROM `content` WHERE `main_flag`=1 AND `is_frontend`=?;");
$get_main_id_query->execute( array($is_frontend) );
$get_main_id_record = $get_main_id_query->fetch();
$current_main_id = $get_main_id_record["id"];
?>




<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2271); ?>
		</div>
		<div class="panel-body">
			<div style="padding:0 0 10px 0;">
				<button onclick="content_tree.checkAll();" class="btn w-xs btn-success"><?php echo translate_str_by_id(2293); ?></button>
				<button onclick="content_tree.uncheckAll();" class="btn w-xs btn-primary2"><?php echo translate_str_by_id(2294); ?></button>
			</div>
			<div id="container_A" style="height:350px;">
			</div>
		</div>
	</div>
</div>




<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2295); ?>
		</div>
		<div class="panel-body">
			<?php echo translate_str_by_id(2296); ?>
			<br/>
			<?php echo translate_str_by_id(2297); ?>
			<?php/*
			<div style="padding:0 0 10px 0;">
				<button onclick="catalogue_tree.checkAll();" class="btn w-xs btn-success">Отметить все</button>
				<button onclick="catalogue_tree.uncheckAll();" class="btn w-xs btn-primary2">Снять все</button>
			</div>
			<div id="container_B" style="height:350px;">
			</div>
			*/?>
		</div>
	</div>
</div>




<script>
//Создание файла
function create_sitemap()
{
    document.getElementById("messages").innerHTML = "";
    
    var url_list = new Array();
    
    //Работаем с деревом материалов
    var checked_content = content_tree.getChecked();
    for(var i=0; i < checked_content.length; i++)
    {
        //Объект webix
        var node = content_tree.getItem(checked_content[i]);
    
        var url = new Object;
        url.url = node.url;
        
        url_list.push(url);
    }
    
    /*
    //Работаем с деревом каталога
    var checked_catalogue = catalogue_tree.getChecked();
    for(var i=0; i < checked_catalogue.length; i++)
    {
        //Объект webix
        var node = catalogue_tree.getItem(checked_catalogue[i]);
    
        var url = new Object;
        url.url = node.url;
        
        url_list.push(url);
    }
    */
    if(url_list.length == 0)
    {
        alert("<?php echo translate_str_by_id(2298); ?>");
        return;
    }
    
	<?php
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	?>
	
    //Запрос на создание файла
    jQuery.ajax({
    type: "POST",
    async: false, //Запрос синхронный
    url: "/<?php echo $DP_Config->backend_dir; ?>/content/content/ajax_create_sitemap.php",
    dataType: "text",//Тип возвращаемого значения
    data: "url_list="+JSON.stringify(url_list)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
    success: function(answer)
    {
        alert("<?php echo translate_str_by_id(2299); ?>");
    }
});
}
</script>






















<script type="text/javascript" charset="utf-8">
//Создаем дерево
content_tree = new webix.ui({
    
    //Шаблон элемента дерева
	template:function(obj, common)//Шаблон узла дерева
    	{
            var folder = common.folder(obj, common);
    	    var icon = "";
    	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
    	    var checkbox = common.checkbox(obj, common);
    	    
    	    //Индикация системного материала
    	    var icon_system = "";
    	    if(obj.system_flag == true)
            {
                icon_system = "<img src='/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/gear.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
            }
    	    
    	    //Индикация материала, снятого с публикации
    	    if(obj.published_flag == false)
            {
                icon_system += "<img src='/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
                value_text = "<span style=\"color:#AAA\">" + obj.value + "</span>";//Вывод текста
            }
    	    
    	    //Индикация главного материала
    	    if(obj.main_flag == 1)
            {
                icon_system += "<img src='/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/star.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
                value_text = "<span style=\"font-weight:bold\">" + obj.value + "</span>";//Вывод текста
            }
    	    
            return common.icon(obj, common) + checkbox + icon + folder + icon_system + value_text;
    	},//~template



	editable:false,//Не редактируемое
    container:"container_A",//id блока div для дерева
    view:"tree",
	select:true,//можно выделять элементы
	drag:false//Нельзя переносить
});

webix.event(window, "resize", function(){ content_tree.adjust(); });

var site_content = <?php echo $content_tree_dump_JSON;?>;
content_tree.parse(site_content);
/*~ДЕРЕВО МАТЕРИАЛОВ*/
</script>














<?php
/**
 * **************************************************************************************
 * *******************  ДАЛЬШЕ ИДЕТ ЧАСТЬ ДЛЯ КАРТЫ КАТАЛОГА ТОВАРОВ  *******************
 * **************************************************************************************
*/

/*
define('_FULL_CATALOGUE_TREE_', 1);//Для формирования полного дерева каталога (катагории+товары)


require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
?>


<script type="text/javascript" charset="utf-8">
//ДЕРЕВО КАТАЛОГА ТОВАРОВ
//Для редактируемости дерева
webix.protoUI({
    name:"edittree"
}, webix.EditAbility, webix.ui.tree);
//Формирование дерева
catalogue_tree = new webix.ui({
    editable:false,//не редактируемое
    container:"container_B",//id блока div для дерева
    view:"tree",
	select:true,//можно выделять элементы
	drag:false,//можно переносить
	//Шаблон элемента дерева
	template:function(obj, common)//Шаблон узла дерева
    	{
            var folder = common.folder(obj, common);
    	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
    	    var checkbox = common.checkbox(obj, common);
            return common.icon(obj, common) + checkbox + folder + value_text;
    	},//~template
});
webix.event(window, "resize", function(){ catalogue_tree.adjust(); });

var saved_catalogue = <?php echo $catalogue_tree_dump_JSON;?>;
catalogue_tree.parse(saved_catalogue);
catalogue_tree.openAll();
</script>
*/?>












