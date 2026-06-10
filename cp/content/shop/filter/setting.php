<?php
defined('_ASTEXE_') or die('No access');

require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий



$id = (int) trim($_GET['id']);
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			
			<?php
			print_backend_button(array('background_color'=>'#b9babb', 'fontawesome_class'=>'fas fa-chevron-left', 'caption'=>translate_str_by_id(2961), 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/shop/filter'));
			?>
			
			<a class="panel_a" href="javascript:void(0);" onclick="save_action();">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
			</a>
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
			
		</div>
	</div>
</div>

<?php
if($id > 0){
	$sql = "SELECT * FROM `shop_docpart_filter` WHERE `id` = $id;";
	$query = $db_link->prepare($sql);
	$query->execute();
	$rov = $query->fetch();
	if(!empty($rov)){
		$manufacturer   = trim($rov['manufacturer']);
		$article 		= trim($rov['article']);
		$name 			= trim($rov['name']);
		$list_storages 	= json_decode(trim($rov['list_storages']), true);
		
		$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article), "UTF-8");
		$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer), "UTF-8"), ENT_QUOTES, "UTF-8");
		
		$min_price = '';
		$max_price = '';
		
		$min_time = '';
		$max_time = '';
		
		if($rov['min_price'] > 0){
			$min_price = (float) $rov['min_price'];
		}
		
		if($rov['max_price'] > 0){
			$max_price = (float) $rov['max_price'];
		}
		
		if($rov['min_time'] > 0){
			$min_time = (int) $rov['min_time'];
		}
		
		if($rov['max_time'] > 0){
			$max_time = (int) $rov['max_time'];
		}
		?>
		<div class="row" style="margin: 0;">
			<div class="col-lg-12">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5260); ?>
					</div>
					<div class="panel-body">
						<div class="col-lg-12"><h4>
						<?=$manufacturer;?><?=(!empty($article))?' - '.$article:'';?><?=(!empty($name))?' <small>'.translate_str_by_id(5247).':</small> '.$name:'';?>
						<?=(empty($manufacturer) && empty($article) && empty($name))?translate_str_by_id(5261):'';?>
						</h4></div>
					</div>
				</div>
			</div>
		</div>
		
		<script>
			var storages_list = new Array();//Массив с объектами складов
			<?php
			//Заполняем список
			$storages_query = $db_link->prepare("SELECT * FROM `shop_storages`;");
			$storages_query->execute();
			while($storage = $storages_query->fetch())
			{
				?>
				storages_list[storages_list.length] = new Object;
				storages_list[storages_list.length-1].id = <?php echo $storage['id']; ?>;//ID
				storages_list[storages_list.length-1].checked = <?=(in_array((int)$storage['id'], $list_storages))?'true':'false';?>;//Выделение
				storages_list[storages_list.length-1].name = "<?php echo $storage["name"]; ?>";//Название
				storages_list[storages_list.length-1].selected = false;//Флаг - не выделен в данный момент в дереве
				storages_list[storages_list.length-1].groups = new Array();
				<?php
			}
				?>
		</script>
		
		
		
		<div class="row" style="margin: 0;">
			<div class="col-lg-6" id="general_options_div">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5262); ?>
					</div>
					<div class="panel-body">
						<input style="display:none;" type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /> <label style="cursor:pointer;" for="check_uncheck_all"><?php echo translate_str_by_id(5263); ?></label>
						<div id="container_A_storages" style="height:400px;"></div>
						<script>
						//Формирование дерева
						storages_tree = new webix.ui({
							//Шаблон элемента дерева
							template:function(obj, common)//Шаблон узла дерева
								{
									var value_text = "<span>" + obj.value + "</span>";//Вывод текста
									var checkbox = common.checkbox(obj, common);//Чекбокс
					
									return common.icon(obj, common)+ checkbox + common.folder(obj, common) + value_text;
								},//~template
						
							editable:false,//редактируемое
							container:"container_A_storages",//id блока div для дерева
							view:"tree",
							select:false,//можно выделять элементы
							drag:false,//можно переносить
						});
						webix.event(window, "resize", function(){ storages_tree.adjust(); });
						//-----------------------------------------------------
						//Инициализация редактора дерева
						function storages_tree_start_init()
						{
							var flag_check_uncheck_all = true;
							for(var i=0; i < storages_list.length; i++)
							{
								storages_tree.add({id:storages_list[i].id, value:storages_list[i].name}, storages_tree.count(), 0);
								if(storages_list[i].checked){
									storages_tree.checkItem(storages_list[i].id);//Отмечаем в дереве
								}else{
									flag_check_uncheck_all = false;
								}
							}
							
							if(flag_check_uncheck_all){
								document.getElementById("check_uncheck_all").checked = true;
							}
						}
						storages_tree_start_init();
						//-----------------------------------------------------
						</script>
					</div>
				</div>
			</div>
			
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5264); ?>
					</div>
					<div class="panel-body">
						<label for="min_price"><?php echo translate_str_by_id(3736); ?></label><br>
						<input class="form-control" type="number" id="min_price" name="min_price" value="<?=$min_price;?>" />
						
						<label for="max_price"><?php echo translate_str_by_id(3738); ?></label><br>
						<input class="form-control" type="number" id="max_price" name="max_price" value="<?=$max_price;?>" />
					</div>
				</div>
			</div>
			
			<div class="col-lg-6">
				<div class="hpanel">
					<div class="panel-heading hbuilt">
						<?php echo translate_str_by_id(5265); ?>
					</div>
					<div class="panel-body">
						<label for="min_time"><?php echo translate_str_by_id(5266); ?></label><br>
						<input class="form-control" type="number" id="min_time" name="min_time" value="<?=$min_time;?>" />
						
						<label for="max_time"><?php echo translate_str_by_id(5267); ?></label><br>
						<input class="form-control" type="number" id="max_time" name="max_time" value="<?=$max_time;?>" />
					</div>
				</div>
			</div>
			
		</div>
		
		
		
		<script>
		//Функция сохранения
		function save_action()
		{
			//Получить отмеченные склады
			var ckecked_storages = storages_tree.getChecked();
			var storages_list_json = JSON.stringify(ckecked_storages);
			
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'save_storages';
			request_object.id = <?=(int)$id;?>;
			request_object.storages_list_json = storages_list_json;
			
			request_object.min_price = document.getElementById("min_price").value;
			request_object.max_price = document.getElementById("max_price").value;
			
			request_object.min_time = document.getElementById("min_time").value;
			request_object.max_time = document.getElementById("max_time").value;
			
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/filter/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					if(answer.status == true)
					{
						location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/shop/filter?success_message=Данные+сохранены";
					}
					else
					{
						alert("<?php echo translate_str_by_id(2576); ?>.");
					}
				}
			});
		}
		
		//Обработка переключения Выделить все/Снять все
		function on_check_uncheck_all()
		{
			var state = document.getElementById("check_uncheck_all").checked;
			
			for(var i=0; i<storages_list.length;i++)
			{
				storages_list[i].checked = state;
			}
			storages_tree.clearAll();
			storages_tree_start_init();
		}
		</script>
<?php
	}
}
?>