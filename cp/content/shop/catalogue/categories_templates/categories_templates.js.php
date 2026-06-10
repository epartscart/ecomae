<?php
//Имеет расширение php, чтобы работала мультиязычность

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
//Страничный функционал для шаблонов категорий


// -------------------------------------------------------------------------------------------------------
//Открыть модальное окно
//После отображения окна - подгоняем дерево под размер
$('#modalCategoriesTemplates').on('shown.bs.modal',function(){
	templates_tree.adjust();
});
var templates_tree = "";//Переменная для дерева шаблонов
function open_templates_window()
{
	document.getElementById('modalCategoriesTemplates_workArea').innerHTML = '<div class="text-center"><?php echo translate_str_by_id(2831); ?><br><img src="/content/files/images/ajax-loader-transparent.gif" /></div>';
	$('#modalCategoriesTemplates').modal('show');
	
	
	document.getElementById('delete_template_button').disabled = true;
	document.getElementById('copy_template_button').disabled = true;
	
	
	var formData = new FormData();
	formData.append('action', 'get_all');
	formData.append('csrf_guard_key', csrf_guard_key);
	

	
	jQuery.ajax({
		type: "POST",
		async: false, //Запрос синхронный
		url: "/"+backend_dir+"/content/shop/catalogue/categories_templates/ajax_templates_actions.php",
		dataType: "text",//Тип возвращаемого значения
		data: formData,
		processData: false,// tell jQuery not to process the data
		contentType: false,// tell jQuery not to set contentType
		success: function(answer)
		{
			//console.log(answer);
			
			var answer_ob = JSON.parse(answer);
			
			//Если некорректный парсинг ответа
			if( typeof answer_ob.status === "undefined" )
			{
				alert("<?php echo translate_str_by_id(2429); ?>");
			}
			else
			{
				//Корректный парсинг ответа
				if(answer_ob.status == true)
				{
					var templates_html = '';
					
					if( answer_ob.templates.length == 0 )
					{
						templates_html = '<?php echo translate_str_by_id(2832); ?>';
						
						document.getElementById('modalCategoriesTemplates_workArea').innerHTML = templates_html;
					}
					else
					{
						//Есть шаблоны
						
						templates_html = '<div class="row"><div class="col-lg-6"><div class="hpanel"><div class="panel-heading hbuilt"><?php echo translate_str_by_id(2827); ?></div><div class="panel-body"><div id="container_C" style="height:250px;"></div></div></div></div><div class="col-lg-6" id="template_info_div_container"><div class="hpanel"><div class="panel-heading hbuilt"><?php echo translate_str_by_id(2833); ?></div><div class="panel-body"><div id="template_info_div"></div></div></div></div></div>';
						
						document.getElementById('modalCategoriesTemplates_workArea').innerHTML = templates_html;
						
						/**Инициализируем дерево шаблонов*/
						//Для редактируемости дерева
						webix.protoUI({
							name:"edittree"
						}, webix.EditAbility, webix.ui.tree);
						//Формирование дерева
						templates_tree = new webix.ui({
							
							//Шаблон элемента дерева
							template:function(obj, common)//Шаблон узла дерева
								{
									
									var folder = common.folder(obj, common);
									var icon = "";
									var value_text = "<span>" + obj.value + "</span>";//Вывод текста

									return common.icon(obj, common) + icon + folder + value_text;
								},//~template

							editable:true,//редактируемое
							editValue:"value",
							editaction:"dblclick",//редактирование по двойному нажатию
							container:"container_C",//id блока div для дерева
							view:"edittree",
							select:true,//можно выделять элементы
							//drag:true,//можно переносить
							editor:"text",//тип редактирование - текстовый
						});
						webix.event(window, "resize", function(){ templates_tree.adjust(); });
						/*~ДЕРЕВО*/
						//-----------------------------------------------------
						webix.protoUI({
							name:"editlist" // or "edittree", "dataview-edit" in case you work with them
						}, webix.EditAbility, webix.ui.list);

						templates_tree.parse(answer_ob.templates);
						templates_tree.openAll();
						//-----------------------------------------------------
						//Событие при выборе элемента дерева
						templates_tree.attachEvent("onAfterSelect", function(id)
						{
							onSelected_templates_tree();
						});
						onSelected_templates_tree();//Обрабатываем текущее выделение (его отсутствие)
					}
				}
				else
				{
					alert(answer_ob.message);
				}
			}
		}
	});
	
	
}
// -------------------------------------------------------------------------------------------------------
//Обработка выбора шаблона категории
function onSelected_templates_tree()
{
	var template_node_id = templates_tree.getSelectedId();//ID выделенного узла
	if(template_node_id == 0)
	{
		document.getElementById('delete_template_button').disabled = true;
		document.getElementById('copy_template_button').disabled = true;
		
		document.getElementById("template_info_div_container").setAttribute("style", "display:none;");
		return;
	}
	
	document.getElementById('delete_template_button').disabled = false;
	document.getElementById('copy_template_button').disabled = false;
	
	document.getElementById("template_info_div_container").setAttribute("style", "display:block;");
	
	var template_node = "";//Ссылка на объект узла
	//Выделенный узел
	template_node = templates_tree.getItem(template_node_id);
	
	
	
	//DIV для информации по шаблону
	var template_info_div = document.getElementById('template_info_div');
	
	
	//Обозначение свойства справа (название и тип)
	var template_info_text = "";
	
	template_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+template_node.id+"</div></div>";
	
	template_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	template_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+template_node.value+"</div></div>";

	template_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
	
	template_info_text += "<div class=\"form-group text-center\"><img id=\"template_"+template_node_id+"\" onerror = \"this.src = '/content/files/images/no_image.png'\" style=\"max-width:96px; max-height:96px\" /></div>";
	
	template_info_div.innerHTML = template_info_text;
	
	
	
	document.getElementById('template_'+template_node_id).setAttribute('src', "data:image;base64,"+template_node.image);
}
// -------------------------------------------------------------------------------------------------------
//Функция создания шаблона
function create_template()
{
	//Выделенный узел
	var node_id = tree.getSelectedId();//ID выделенного узла
	if(node_id == 0)
	{
		alert("<?php echo translate_str_by_id(2834); ?>");
		return;
	}
	
	var node = "";//Ссылка на объект узла
	//Выделенный узел
	node = tree.getItem(node_id);
	
	if(node.$count > 0)
	{
		alert("<?php echo translate_str_by_id(2835); ?>");
		return;
	}
	
	
	
	//Данные для создаваемого шаблона
	var formData = new FormData();
	formData.append('action', 'create');
	formData.append('caption', node.value);
	formData.append('category_object', JSON.stringify(node));
	
	//Изображение для шаблона
	var image_type = 'from_category';
	var input_file = document.getElementById("img_"+node_id);//input для файла изображения
	var file = input_file.files[0];//Получаем выбранный файл
	if(file != undefined)
	{
		image_type = 'from_input';
		formData.append('image', file);
	}
	
	formData.append('image_type', image_type);
	formData.append('csrf_guard_key', csrf_guard_key);
	
	jQuery.ajax({
		type: "POST",
		async: false, //Запрос синхронный
		url: "/"+backend_dir+"/content/shop/catalogue/categories_templates/ajax_templates_actions.php",
		dataType: "text",//Тип возвращаемого значения
		data: formData,
		processData: false,// tell jQuery not to process the data
		contentType: false,// tell jQuery not to set contentType
		success: function(answer)
		{
			//console.log(answer);
			
			var answer_ob = JSON.parse(answer);
			
			//Если некорректный парсинг ответа
			if( typeof answer_ob.status === "undefined" )
			{
				alert("<?php echo translate_str_by_id(2429); ?>");
			}
			else
			{
				//Корректный парсинг ответа
				if(answer_ob.status == true)
				{
					alert('<?php echo translate_str_by_id(2836); ?>');
					open_templates_window();
					templates_tree.select(answer_ob.template_id);
				}
				else
				{
					alert(answer_ob.message);
				}
			}
		}
	});
}
// -------------------------------------------------------------------------------------------------------
//Функция удаления выбранного шаблона категории
function delete_category_template()
{
	var template_node_id = templates_tree.getSelectedId();//ID выделенного узла
	if(template_node_id == 0)
	{
		alert('<?php echo translate_str_by_id(2837); ?>');
		return;
	}
	
	
	if( !confirm('<?php echo translate_str_by_id(2838); ?>') )
	{
		return;
	}
	
	
	//Данные для создаваемого шаблона
	var formData = new FormData();
	formData.append('action', 'delete');
	formData.append('template_id', template_node_id);
	formData.append('csrf_guard_key', csrf_guard_key);
	
	jQuery.ajax({
		type: "POST",
		async: false, //Запрос синхронный
		url: "/"+backend_dir+"/content/shop/catalogue/categories_templates/ajax_templates_actions.php",
		dataType: "text",//Тип возвращаемого значения
		data: formData,
		processData: false,// tell jQuery not to process the data
		contentType: false,// tell jQuery not to set contentType
		success: function(answer)
		{
			//console.log(answer);
			
			var answer_ob = JSON.parse(answer);
			
			//Если некорректный парсинг ответа
			if( typeof answer_ob.status === "undefined" )
			{
				alert("<?php echo translate_str_by_id(2429); ?>");
			}
			else
			{
				//Корректный парсинг ответа
				if(answer_ob.status == true)
				{
					alert('<?php echo translate_str_by_id(2839); ?>');
					open_templates_window();
				}
				else
				{
					alert(answer_ob.message);
				}
			}
		}
	});
}
// -------------------------------------------------------------------------------------------------------
//Функция добавления шаблона в буфер, чтобы потом его можно было вставлять, как при копировании
function template_to_buffer()
{
	var template_node_id = templates_tree.getSelectedId();//ID выделенного узла
	if(template_node_id == 0)
	{
		alert('<?php echo translate_str_by_id(2840); ?>');
		return;
	}
	
	
	var category_template = "";//Ссылка на объект шаблона
	//Выделенный узел
	category_template = templates_tree.getItem(template_node_id);
	
	
	
	//В буфере копирования категорий будет единственный объект - выбранный шаблон
	copied_element_buffer = JSON.parse(category_template.category_object);	
	
	
	//Создаем дополнительные поля: img_blob для изображения и by_template (чтобы при копировании было понятно, что категория будет создаваться на основе шаблона)
	copied_element_buffer['img_blob'] = category_template.image;
	copied_element_buffer['img_blob_name'] = category_template.image_name;
	copied_element_buffer['by_template'] = template_node_id;
	
	
	//Текущее действие - копирование
	copy_paste_action = 'copy';
	
	//Cоздаем пустой инпут
	var input_file = document.createElement("input");
	input_file.setAttribute("type","file");
	input_file.setAttribute("name","template_img_"+copied_element_buffer.id);
	input_file.setAttribute("id","template_img_"+copied_element_buffer.id);
	input_file.setAttribute("accept","image/jpeg,image/jpg,image/png,image/gif");
	input_file.setAttribute("onchange","onFileChanged();");
	document.getElementById('template_input_container').innerHTML = '';
	document.getElementById('template_input_container').appendChild(input_file);//Добавили его в html, чтобы следом можно было клонировать в буферный массив
	
	//Клонируем input в буферный массив. Таким образом, при вставке категории, будет создаваться пустой инпут, который потом может быть задействован пользователем
	copied_element_file_inputs_buffer = new Object;
	copied_element_file_inputs_buffer[''+copied_element_buffer.id] = $("#template_img_"+copied_element_buffer.id).clone();
	
	
	$('.paste-button').css('background', '#33cc33');//Кнопка Вставить - активная
	
	document.getElementById('copy_cut_buffer_div').innerHTML = '<?php echo translate_str_by_id(2841); ?>: '+copied_element_buffer.value + ' (<?php echo translate_str_by_id(2637); ?> ID '+template_node_id+')';
	
	alert('<?php echo translate_str_by_id(2842); ?>');
	
	$('#templates_window_close_button').click();
}
// -------------------------------------------------------------------------------------------------------