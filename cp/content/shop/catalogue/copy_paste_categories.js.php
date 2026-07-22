<?php
//Имеет расширение php, чтобы работала мультиязычность
header('Content-Type: application/javascript; charset=utf-8');
header('X-Content-Type-Options: nosniff');

//Конфигурация Docpart
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    exit("No DB Connect");
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------

?>
//Функционал для копирования категорий товаров (для редактора дерева категорий)
// ------------------------------------------------------------------------------------------------
//Функция для получения HTML для кнопки (кнопки в панели управления)
function print_backend_button(button_params, type = 1)
{
	var target = '';
	if( typeof button_params['target'] != 'undefined' )
	{
		if( button_params['target'] == '_blank' )
		{
			target = 'target="_blank"';
		}
	}
	
	
	var onclick = '';
	if( typeof button_params['onclick'] != 'undefined' )
	{
		onclick = 'onclick="'+button_params['onclick']+'"';
	}
	
	
	var sub_class = '';
	if( typeof button_params['sub_class'] != 'undefined' )
	{
		sub_class = button_params['sub_class'];
	}
	
	
	if( type == 1 )
	{
		return '<a class="panel_a" href="'+button_params["url"]+'" '+onclick+' '+target+'><div class="panel_a_img '+sub_class+'" style="background-color: '+button_params["background_color"]+';width:96px;height:96px;display:table-cell;vertical-align:middle;"><i class="'+button_params["fontawesome_class"]+'" style="color:#FFF;font-size:45px"></i></div><div class="panel_a_caption">'+button_params["caption"]+'</div></a>';
	}
	else if(type == 2 )
	{
		return '<a class="btn btn-success '+sub_class+'" style="background-color: '+button_params["background_color"]+';border:0;" href="'+button_params["url"]+'" '+onclick+' '+target+'><i class="'+button_params["fontawesome_class"]+'"></i> <span class="bold">'+button_params["caption"]+'</span></a>';
	}
	
}
// ------------------------------------------------------------------------------------------------
// Добавить кнопки: Копировать, Вставить, Вырезать
function showButtons() 
{
	var copy_button = new Object;
	copy_button.onclick = 'copy_category();';
	copy_button.url = 'javascript:void(0);';
	copy_button.background_color = '#8d43ac';
	copy_button.fontawesome_class = 'fas fa-copy';
	copy_button.sub_class = 'copy-button';
	copy_button.caption = 'Копировать';


	var paste_button = new Object;
	paste_button.onclick = 'paste_category();';
	paste_button.url = 'javascript:void(0);';
	paste_button.background_color = '#33cc33';
	paste_button.fontawesome_class = 'fas fa-paste';
	paste_button.sub_class = 'paste-button';
	paste_button.caption = 'Вставить';


	var cut_button = new Object;
	cut_button.onclick = 'cut_category();';
	cut_button.url = 'javascript:void(0);';
	cut_button.background_color = '#e74c3c';
	cut_button.fontawesome_class = 'fas fa-cut';
	cut_button.sub_class = 'cut-button';
	cut_button.caption = 'Вырезать';



	document.getElementById('tree_footer_buttons').innerHTML = print_backend_button(copy_button, 2)+' '+print_backend_button(cut_button, 2)+' '+print_backend_button(paste_button, 2)+' <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint(\'<b><?php echo translate_str_by_id(2846); ?> &quot;<?php echo translate_str_by_id(2847); ?>&quot;.</b><p><?php echo translate_str_by_id(2848); ?></p><b><?php echo translate_str_by_id(2849); ?> &quot;<?php echo translate_str_by_id(2850); ?>&quot;.</b><p><?php echo translate_str_by_id(2851); ?><br><?php echo translate_str_by_id(2852); ?></p><p><b><?php echo translate_str_by_id(2853); ?></b> <?php echo translate_str_by_id(2854); ?></p><p><b><?php echo translate_str_by_id(2855); ?>.</b> <?php echo translate_str_by_id(2856); ?> &quot;<?php echo translate_str_by_id(2114); ?>&quot; <?php echo translate_str_by_id(2857); ?>.</p>\');"><i class="fa fa-info"></i></button>';

	
	//Пока вставлять нечего
	$('.paste-button').css('background', '#EEE');
	//Копировать и Вырезать тоже
	activate_copy_cut_buttons(false);
}
showButtons();
// ------------------------------------------------------------------------------------------------
//Обработка доступности кнопок Копировать и Вырезать
function activate_copy_cut_buttons(activate)
{
	if( activate )
	{
		$('.copy-button').css('background', '#8d43ac');
		$('.cut-button').css('background', '#e74c3c');
		$('.create-template-button').css('background', '');
	}
	else
	{
		$('.copy-button').css('background', '#EEE');
		$('.cut-button').css('background', '#EEE');
		$('.create-template-button').css('background', '#EEE');
	}
}
// ------------------------------------------------------------------------------------------------
var copy_paste_action = null;//Текущее действие Копировать/Вырезать
var copied_element_buffer = null;//Буфер для копируемого/перемещаемого элемента
var copied_element_file_inputs_buffer = null;//Здесь будут хранится клоны инпутов копируемого элемента. Это нужно, т.к. нежелательно клонировать инпуты напрямую при создании новой категории. Если клонировать инпуты напрямую, то, может возникнуть ситуация, когда сначала нажали "Копировать", а потом для исходного элемента загрузили новый файл - тогда этот новый файл будет копироваться в создаваемые категории. А правильно будет - когда создаваемые категории имеют то же состояние, которое было у исходного элемента в момент нажатия "Копировать". Кроме этого, при прямом копировании будет неоткуда клонировать инпуты, если исходную категорию удалить.
// Обработка нажатия "Копировать"
function copy_category()
{
	//ID копируемого элемента
	var node_id = tree.getSelectedId();
	if (node_id == 0) 
	{
		alert('<?php echo translate_str_by_id(2843); ?>');
		return false;
	}
	
	//Если текущее действие - cut (т.е. нажали Вырезать, но никуда не вставили, то, нужно снять отметку с текущих вырезаемых категорий)
	if( copy_paste_action == 'cut' )
	{
		mark_cutting_branch( copied_element_buffer, false );
	}
	
	
	//Массив data, в котором находится копируемый элемент
	let level_tree = tree.serialize(tree.getParentId(node_id), true);

	//Сбрасываем буферы
	copied_element_buffer = null;
	copied_element_file_inputs_buffer = null;
	
	//Получаем объект копируемого элемента в буфер
	for (let i = 0; i < level_tree.length; i++) 
	{
		if (level_tree[i].id == node_id) 
		{
			copied_element_buffer = level_tree[i];
			break
		}
	}
	if (!copied_element_buffer) 
	{
		alert("<?php echo translate_str_by_id(2844); ?>");
		return false;
	}
	
	
	//Инпуты для файлов тоже в буфер
	clone_inputs_to_buffer( copied_element_buffer, true );
	if(!copied_element_file_inputs_buffer)
	{
		alert("<?php echo translate_str_by_id(2844); ?>");
		return false;
	}
	
	
	copy_paste_action = 'copy';//Текущее действие для кнопки Вставить - Копирование
	$('.paste-button').css('background', '#33cc33');//Кнопка Вставить - активная
	
	document.getElementById('copy_cut_buffer_div').innerHTML = '<?php echo translate_str_by_id(2841); ?>: '+copied_element_buffer.value + ' (ID '+copied_element_buffer.id + ')';
	
	return true;
}
// ------------------------------------------------------------------------------------------------
//Рекурсивная функция клонирования инпутов в буферный массив
function clone_inputs_to_buffer( elem, is_begin )
{
	var $elem_input = $("#img_"+elem['id']);//Исходный инпут
	var $clone_input = $elem_input.clone();//Создаем клон
	
	//Первый вызов - пересоздаем буфер
	if( is_begin )
	{
		copied_element_file_inputs_buffer = new Object;
	}
	
	//Доблавляем клон в буфер
	copied_element_file_inputs_buffer[''+elem['id']] = $clone_input;
	
	
	//Если есть вложенные
	if( typeof elem.data != 'undefined' )
	{
		for( var i=0 ; i < elem.data.length ; i++ )
		{
			clone_inputs_to_buffer(elem.data[i], false);
		}
	}
}
// ------------------------------------------------------------------------------------------------
//Рекурсивная функция вставки копируемого элемента
function insert_copied_element( elem, parent_node_id )
{	
	var parentId = tree.getSelectedId();//Выбранный элемент (куда копировать)
	var first_call = false;//Флаг - первый вызов
	if( parent_node_id )
	{
		//Задан parent_node_id, значит идет уже рекурсивный вызов
		parentId = parent_node_id;
	}
	else
	{
		//parent_node_id не задан, значит идет первый вызов функции
		first_call = true;
	}
	
	
	//Отдельно обработка свойств
	var properties = new Array;
	if( typeof elem['properties'] != 'undefined' )
	{
		properties = elem['properties'];
		
		for( var i = 0 ; i < properties.length ; i++ )
		{
			properties[i]['category_id'] = next_id;
			properties[i]['just_created'] = 1;
		}
	}
	
	
	//Если пользователь будет вставлять элемент из буфера несколько раз в один и тот же узел, то нужно обработать алиас, чтобы он не повторялся.
	//Только для корневого элемента из буфера
	var elem_alias = elem['alias'];
	var alias_pref = 1;
	if( first_call )
	{	
		var parent_nodes = new Array;
		if( parentId > 0 )
		{
			var parent_item = tree.getItem(parentId);
			if( parent_item['$count'] > 0 )
			{
				parent_nodes = tree.serialize(parentId, true);
			}
		}
		else
		{
			parent_nodes = tree.serialize();
		}			
		
		
		if( typeof parent_nodes != 'undefined' )
		{
			//По будущим соседям
			for(var i=0; i < parent_nodes.length; i++)
			{
				//Если такой алиас уже есть
				if( elem_alias == parent_nodes[i]['alias'] )
				{
					elem_alias = elem['alias'] + '_' + alias_pref;//Добавляем префикс
					alias_pref++;
					i = 0;//Начинаем проверку сначала
				}
			}
		}
	}
	
	
	//Если в буфере шаблон, указываем его ID
	var by_template = 0;
	var img_blob = '';
	var img_blob_name = '';
	if( typeof elem['by_template'] != 'undefined' )
	{
		if( elem['by_template'] > 0 )
		{
			by_template = elem['by_template'];
			img_blob = elem['img_blob'];
			img_blob_name = elem['img_blob_name'];
		}
	}
	
	
	
	//Добавляем новый элемент по образцу копируемого
	var elem_id = tree.add( {value:elem['value'], id:next_id, alias:elem_alias, url:elem['url'], title_tag:elem['title_tag'], description_tag:elem['description_tag'], keywords_tag:elem['keywords_tag'], robots_tag:elem['robots_tag'], import_format:elem['import_format'], export_format:elem['export_format'], image_url:elem['image_url'], published_flag:elem['published_flag'], properties:properties, open:elem['open'], image:elem['image'], to_cut:0, by_template:by_template, img_blob:img_blob, img_blob_name:img_blob_name}, -1, parentId);
	

	//Клонируем инпут для изображения - из буфера
	var $clone_input = copied_element_file_inputs_buffer[''+elem['id']];
	$clone_input.prop('name', "img_"+next_id);
	$clone_input.prop('id', "img_"+next_id);
	$('#img_box').append($clone_input);
	
	
	next_id++;//Следующий ID
	
	
	//Если есть вложенные
	if( typeof elem.data != 'undefined' )
	{
		for( var i=0 ; i < elem.data.length ; i++ )
		{
			insert_copied_element( elem.data[i], elem_id );
		}
	}
	
	
	//Если это был первый вызов
	if(first_call)
	{
		tree.open(parentId);//Раскрываем родительский узел
		onSelected();//Обработка текущего выделения
		tree.refresh();
	}
}
// ------------------------------------------------------------------------------------------------
//Обработка "Вставить"
function paste_category()
{
	if( !copied_element_buffer )
	{
		return;
	}
	
	
	//Если идет копирование
	if( copy_paste_action == 'copy' )
	{
		insert_copied_element( copied_element_buffer, null );
	}
	else if( copy_paste_action == 'cut' )
	{
		//Проверяем, что вставка идет не в вырезаемую ветвь
		if( !check_paste_not_to_cutted_brahch( copied_element_buffer ) )
		{
			alert('<?php echo translate_str_by_id(2845); ?>');
			return;
		}
		
		//Удалить исходную ветвь
		if( tree.getItem(copied_element_buffer.id) )
		{
			tree.remove(copied_element_buffer.id);
			onSelected();
		}
    	
		//После первой вставки, далее уже не будет отличий от копирования
		copy_paste_action = 'copy';
		
		
		insert_copied_element( copied_element_buffer, null );
	}
	else
	{
		alert('<?php echo translate_str_by_id(2844); ?>');
	}
}
// ------------------------------------------------------------------------------------------------
//Функция проверки, что вставка идет не в вырезаемую ветвь
function check_paste_not_to_cutted_brahch(elem)
{
	var node_id = tree.getSelectedId();//Куда идет вставка
	if (node_id == 0) 
	{
		//Вставка в корень дерева допускается
		return true;
	}
	
	//Нельзя вставлять при вырезании ни в один из элементов вырезаемой ветви
	if( node_id == elem.id )
	{
		return false;
	}
	
	
	//Если есть вложенные
	if( typeof elem.data != 'undefined' )
	{
		for( var i=0 ; i < elem.data.length ; i++ )
		{
			if( !check_paste_not_to_cutted_brahch(elem.data[i]) )
			{
				return false;
			}
		}
	}
	
	return true;
}
// ------------------------------------------------------------------------------------------------
//Помечаем исходную ветвь на вырезание
function mark_cutting_branch( elem, cut_on )
{
	var node = tree.getItem(elem.id);//Соответствующий узел дерева
	node['to_cut'] = cut_on;
	
	
	//Если есть вложенные
	if( typeof elem.data != 'undefined' )
	{
		for( var i=0 ; i < elem.data.length ; i++ )
		{
			mark_cutting_branch(elem.data[i], cut_on);
		}
	}
	
	onSelected();//Обработка текущего выделения
	tree.refresh();
}
// ------------------------------------------------------------------------------------------------
//Обработка кнопки "Вырезать"
function cut_category()
{
	//Сначала просто копируем (в буфер попадает категория и инпуты)
	if( !copy_category() )
	{
		return;
	}
	
	
	copy_paste_action = 'cut';//Текущее действие - Вырезать
	
	
	//Теперь исходный узел помечаем, как вырезаемый
	mark_cutting_branch( copied_element_buffer, true );
}
// ------------------------------------------------------------------------------------------------