<?php
/**
Скрипт для просмотра древовидного списка в асинхронном режиме
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>



<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">

			<a class="panel_a" onClick="edit_brunch_of_item();" href="javascript:void(0);">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/in.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(3081); ?></div>
			</a>
		
		
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/tree_lists.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(783); ?></div>
			</a>
		
		
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
			
		</div>
	</div>
</div>



<?php
$tree_list_id = $_GET["tree_list_id"];//ID списка

//Получаем текущие данные списка
$list_query = $db_link->prepare("SELECT * FROM `shop_tree_lists` WHERE `id` = ?;");
$list_query->execute( array($tree_list_id) );
$list_record = $list_query->fetch();
$caption = $list_record["caption"];
?>



<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3095); ?> "<?php echo $caption; ?>"
		</div>
		<div class="panel-body">
			<div style="height:350px;" id="tree_list_div">
			</div>
		</div>
	</div>
</div>






<script>
tree_list = new webix.ui({
	view:"tree",
	
	//template:"{common.icon()} {common.folder()} #value# (#id#)",
	
	editable:false,
	container:"tree_list_div",
	select:true,
	drag:false,
	editor:"text",
	on:{
		onAfterOpen:function(id)
		{
		},
		onDataRequest: function (id)
		{
			var promise = webix.ajax().sync().get("/content/shop/catalogue/tree_lists/ajax/ajax_async_tree_loader.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&parent_id="+id+"&tree_list_id=<?php echo $_GET["tree_list_id"]; ?>");
			
			this.parse(
				promise.responseText
			);
			
			return false;
		}
		,
		onAfterLoad:function()
		{
			/*
			if (typeof (tree_list_init_".$property_record["id"].") === \"function\") {
				//Проверка пройдена
				//Вызываем функцию...
				tree_list_init_".$property_record["id"]."();
			}
			*/
		}
	  },
	
});
webix.event(window, "resize", function(){ tree_list.adjust(); });
tree_list.load("/content/shop/catalogue/tree_lists/ajax/ajax_async_tree_loader.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>&parent_id=0&tree_list_id=<?php echo $_GET["tree_list_id"]; ?>");
</script>


<script>
//Переход на редактирование ветви выделенного узла
function edit_brunch_of_item()
{
	var node_id = tree_list.getSelectedId();//ID выделенного узла
	if(node_id == 0)
	{
		alert("<?php echo translate_str_by_id(3083); ?>");
		return;
	}
	
	
	location = "/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/tree_lists/redaktor-po-vetvyam?tree_list_id=<?php echo $_GET["tree_list_id"]; ?>&parent_id="+node_id;
}
</script>