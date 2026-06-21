<?php
/**
 * Скрипт для управления группами складов для асинхронного опроса складов по группам в проценке
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
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/shop/logistics/storages">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name;?>/images/storage.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(763); ?></div>
			</a>
			
			<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
				<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name;?>/images/power_off.png') 0 0 no-repeat;"></div>
				<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
			</a>
		</div>
	</div>
</div>


<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt" style="position:relative;">
			<?php echo translate_str_by_id(3351); ?>
		</div>
		<div class="panel-body">
			<table class="table">
				<tr>
					<th>ID</th>
					<th><?php echo translate_str_by_id(2102); ?></th>
				</tr>
			<?php
			// Выбирем склады с типом: Treelax - БД, Docpart Price, Docpart-Treelax Каталог
			$storages_query = $db_link->prepare("SELECT `id`, `name`, `interface_type` FROM `shop_storages` WHERE `interface_type` IN(1, 2, 6) ORDER BY `id`;");
			$storages_query->execute();
			while($storage = $storages_query->fetch())
			{
			?>
				<tr>
					<td><?=$storage['id'];?></td>
					<td style="width:100%;"><?=$storage['name'];?></td>
				</tr>
			<?php
			}
			?>
			</table>
		</div>
	</div>
</div>


<div id="div_table"></div>


<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt" style="position:relative;">
			<?php echo translate_str_by_id(2073); ?>
		</div>
		<div class="panel-body" style="min-height: 442px;">
			<?php echo translate_str_by_id(3352); ?>
		</div>
	</div>
</div>


<div class="col-lg-6">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3353); ?>
		</div>
		<div class="panel-body">
			<div class="col-lg-12"><label><?php echo translate_str_by_id(2102); ?>:</label><input style="border-color: #a4bed4 !important;" class="form-control" type="text" id="new_name"/></div>
			<div class="col-lg-12">
				<div style="margin-top:15px;"><label><?php echo translate_str_by_id(763); ?>:</label></div>
				<div id="container_A_storages" style="height:250px;"></div>
			</div>
		</div>
		<div class="panel-footer text-right">
			<img id="img_add" style="height: 31px; margin-right: 5px;" class="hidden" src="/content/files/images/ajax-loader-transparent.gif"/><a id="btn_add" onclick="add();" class="btn btn-ar btn-primary"><i class="fa fa-plus"></i> <?php echo translate_str_by_id(2267); ?></a>
		</div>
	</div>
</div>

<script>
	var storages_list = new Array();//Массив с объектами складов
	<?php
	// Заполняем список складов
	$storages_query = $db_link->prepare("SELECT `id`, `name`, `interface_type` FROM `shop_storages` WHERE `id` NOT IN(SELECT CONCAT(`storages`, ', ') FROM `shop_storages_groups`) AND `interface_type` NOT IN(1, 2, 6) ORDER BY `name`;");
	$storages_query->execute();
	while($storage = $storages_query->fetch())
	{
	?>
		storages_list[storages_list.length] = new Object;
		storages_list[storages_list.length-1].id = <?php echo $storage['id']; ?>;//ID
		storages_list[storages_list.length-1].checked = false;//Выделение
		storages_list[storages_list.length-1].name = "<?php echo $storage["name"]; ?>";//Название
		storages_list[storages_list.length-1].selected = false;//Флаг - не выделен в данный момент в дереве
	<?php
	}
	?>
	// Формирование дерева
	storages_tree = new webix.ui({
		// Шаблон элемента дерева
		template:function(obj, common)
		{
			var value_text = "<span>" + obj.value + "</span>";// Вывод текста
			var checkbox = common.checkbox(obj, common);// Чекбокс

			return common.icon(obj, common)+ checkbox + common.folder(obj, common) + value_text;
		},
	
		editable:false,// Редактируемое
		container:"container_A_storages",// ID блока div для дерева
		view:"tree",
		select:false,// Можно выделять элементы
		drag:false,// Можно переносить
	});
	webix.event(window, "resize", function(){ storages_tree.adjust(); });
	
	// Инициализация редактора дерева
	function storages_tree_start_init()
	{
		storages_tree.clearAll();
		for(var i=0; i < storages_list.length; i++)
		{
			storages_tree.add({id:storages_list[i].id, value:storages_list[i].name}, storages_tree.count(), 0);
		}
	}
</script>


<script>
	var ajax_url = '/<?php echo $DP_Config->backend_dir; ?>/content/shop/logistics/groups/ajax_operations.php';
	
	// Функция отображает таблицы
	function show_table(){
		document.getElementById('div_table').innerHTML = '';
		
		get_storages();
		
		setTimeout(function(){
			if(document.getElementById('div_table').innerHTML == ''){
				// Отображаем индикатор загрузки
				document.getElementById('div_table').innerHTML = '<div class="panel-body text-center"><img src="/content/files/images/ajax-loader-transparent.gif"/></div>';
			}
		}, 500)

		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'get_table';

		// Отправляем запрос
		jQuery.ajax({
            type: "POST",
            async: true,
            url: ajax_url,
            dataType: "text",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
				// Вставляем сформированный html на страницу
				document.getElementById('div_table').innerHTML = answer;
		    }
        });
	}
	
	
	
	// Функция выбора не распределенных складов
	function get_storages(){
		if(document.getElementById('btn_add').classList.contains('disabled')){
			return;
		}
		
		storages_list = new Array();
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'get_storages';

		// Отправляем запрос
		jQuery.ajax({
            type: "POST",
            async: true,
            url: ajax_url,
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
				console.log(answer);
				if(answer.storages_list.length > 0){
					storages_list = answer.storages_list;
				}
				// Переотображаем список складов
				storages_tree_start_init();
		    }
        });
	}
	
	
	
	// Функция ручного добавления
	function add(){
		if(document.getElementById('btn_add').classList.contains('disabled')){
			return;
		}
		
		var name = document.getElementById('new_name').value;
		
		if( name === '' ){
			alert("<?php echo translate_str_by_id(3354); ?>");
			return;
		}
		
		// Получить отмеченные склады
		var ckecked_storages = storages_tree.getChecked();
		
		if(ckecked_storages.length == 0){
			alert("<?php echo translate_str_by_id(3355); ?>");
			return;
		}
		
		//Объект для запроса
		var request_object = new Object;
		request_object.action = 'add_group';
		request_object.name = encodeURIComponent(name);
		request_object.storages = ckecked_storages;
		
		// Очищаем форму
		document.getElementById("new_name").value = '';
		
		$('#btn_add').addClass('disabled');// Блокируем кнопку
		$('#img_add').removeClass('hidden');// Отображаем индикатор загрузки
		
        jQuery.ajax({
            type: "POST",
            async: true, //Запрос синхронный
            url: ajax_url,
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                $('#btn_add').removeClass('disabled');// Разблокируем кнопку
				$('#img_add').addClass('hidden');// Убираем индикатор загрузки
				
				//console.log(answer);
                if(answer.status == true)
                {
				   show_table();
                }
                else
                {
					alert("<?php echo translate_str_by_id(3356); ?>");
                }
            }
        });
	}
	
	// Функция удаления
	function del(id){
        if(confirm('<?php echo translate_str_by_id(3357); ?>')){
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del';
			request_object.id = id;
		
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: ajax_url,
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					if(answer.status == true)
					{
						show_table();
					}
					else
					{
						alert("<?php echo translate_str_by_id(2610); ?>");
					}
				}
			});
		}
	}
	
	
	
	// После открытия страницы отображаем таблицы
	show_table();
</script>





























