
//API UCatalog


var UCatalog_breadcrumbs = new Object;// Список breadcrumbs
var UCatalog_request_object = new Object;// Предыдущий запрос, используется в функции гаража





// Функция формирования запроса к API
function UCatalog_loading(action, caption, type, mark_id, model_id, modification_id, parent_id, parent_chain, section_id){
	
	let request_object = new Object;
		request_object.action = action;
		request_object.breadcrumbs = UCatalog_breadcrumbs;
		request_object.caption = caption;
		request_object.type = type;
		request_object.mark_id = mark_id;
		request_object.model_id = model_id;
		request_object.modification_id = modification_id;
		request_object.parent_id = parent_id;
		request_object.parent_chain = parent_chain;
		request_object.section_id = section_id;
	
	if(request_object.action === 'get_marks' && request_object.type !== ''){
		$(".UCatalog_nav_tabs .active").removeClass('active');
		$("#UCatalog_nav_tab_"+request_object.type).addClass('active');
	}
	
	UCatalog_ajax(request_object);
}





// Функция вставки HTML полученного от API
function UCatalog_show(answer){
	if(answer && answer.status == true){

		UCatalog_breadcrumbs = answer.breadcrumbs;
		
		if(answer.tag){
			document.getElementById(answer.tag).innerHTML = answer.html;
		}else{
			document.getElementById("UCatalog_container").innerHTML = answer.html;
		}
		
	}else{
		document.getElementById("UCatalog_container").innerHTML = answer.message;
	}
}





// Функция ajax запроса к API
function UCatalog_ajax(request_object){
	
	//console.log(request_object.action);
	//console.log(request_object);
	
	jQuery.ajax({
		type: "POST",
		async: true,
		url: "/api/UCatalog/api.php",
		dataType: "json",
		data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
		success: function(answer)
		{
			//console.log(answer);
			
			if(answer && answer.status == true){
				if(request_object.action != 'add_garage' && request_object.action != 'get_notepad' && request_object.action != 'add_notepad'){
					UCatalog_request_object = answer.request_object;// Записываем предыдущий запрос, для гаража
					UCatalog_show(answer);
					
					if(request_object.action === 'get_types'){
						UCatalog_loading('get_marks', answer.request_object.caption, answer.request_object.type);
					}else{
						if(request_object.action !== 'get_marks'){
							if(request_object.type != '' && (request_object.parent_id == '0' || request_object.parent_id == undefined)){
								$([document.documentElement, document.body]).animate({
									scrollTop: $("#UCatalog_container").offset().top - 70
								}, 300);
							}
						}
						// На маленьких экранах делаем автоматическую прокрутку до таблицы товаров после выбора элемнта дерева
						if(request_object.action == 'get_parts'){
							if ($(window).width() < 991){
								$([document.documentElement, document.body]).animate({
									scrollTop: $("#UCatalog_parts").offset().top - 70
								}, 300);
							}
						}
					}
				}else{
					if(answer.tag){
						document.getElementById(answer.tag).innerHTML = answer.html;
					}
				}
			}else{
				document.getElementById("UCatalog_container").innerHTML = answer.message;
			}
		},
		error: function(){
			console.log('UCatalog NO loading...');
			let answer = new Object;
				answer.status = false;
				answer.message = '';
			UCatalog_show(answer);
		}
	});
}





// Функция раскрытия блока подробных свойств модификации
function UCatalog_modifications_show_hide_property(id){
	if($("#UCatalog_modifications_property_"+id).css('display') != 'block'){
		$("#UCatalog_modifications_property_"+id).css('display', 'block');
	}else{
		$("#UCatalog_modifications_property_"+id).css('display', 'none');
	}
}





// Функция отображения элементов определенной буквы
function UCatalog_letter(letter){
	$("#UCatalog_tab_content_no_data").addClass('hidden');
	$(".UCatalog_nav_letters .active").removeClass('active');
	$("#UCatalog_nav_letter_"+letter).addClass('active');
	$("#UCatalog_container .UCatalog_tab_element:not(.hidden)").addClass('hidden');
	if(letter == 'popular'){
		$("#UCatalog_container .popular").removeClass('hidden');
	}else if(letter == 'all'){
		$("#UCatalog_container .UCatalog_tab_element").removeClass('hidden');
	}else{
		$("#UCatalog_container .letter_"+letter).removeClass('hidden');
	}
	
	$(".UCatalog_nav_filters .active").removeClass('active');
	$("#UCatalog_nav_filter_all").addClass('active');
}





// Функция применения фильтра на странице моделей
function UCatalog_filter(filter){
	UCatalog_letter($(".UCatalog_nav_letters .active").attr('id').replace("UCatalog_nav_letter_", ""));
	$(".UCatalog_nav_filters .active").removeClass('active');
	$("#UCatalog_nav_filter_"+filter).addClass('active');
	if(filter != 'all'){
		$("#UCatalog_container .UCatalog_tab_element:not(.hidden, .filter_"+filter+")").addClass('hidden');
	}
	if($("#UCatalog_container .UCatalog_tab_element:not(.hidden)").length == 0){
		$("#UCatalog_tab_content_no_data").removeClass('hidden');
	}
}





// Функция раскрытия вложенной группы дерева узлов
function UCatalog_tree_drop(id) {
	if ( $("#UCatalog_tree_drop_"+id).hasClass('UCatalog_tree_drop_open')) {
		$("#UCatalog_tree_drop_"+id).removeClass('UCatalog_tree_drop_open');
		$("#UCatalog_tree_drop_"+id).html('+');
	}else{
		$("#UCatalog_tree_drop_"+id).addClass('UCatalog_tree_drop_open');
		$("#UCatalog_tree_drop_"+id).html('-');
	}
	$("#UCatalog_tree_drop_"+id).attr('onclick', 'UCatalog_tree_drop(\''+id+'\');');
	$("#UCatalog_tree_caption_"+id).attr('onclick', 'UCatalog_tree_drop(\''+id+'\');');
}





// Функция заливки цветом выделенного узла дерева
function UCatalog_tree_caption_bg(id) {
	$(".UCatalog_tree_caption").css('background', 'none');
	$("#UCatalog_tree_caption_"+id).css('background', '#f2f2f2');
}





//++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

// ФУНКЦИИ РАБОТЫ С ГАРАЖОМ

// Функция добавления автомобиля в гараж
function UCatalog_add_garage(){
	let request_object = new Object;
		request_object.action = 'add_garage';
		request_object.request_object = UCatalog_request_object;
		
	UCatalog_ajax(request_object);
}

// Функция выбора автомобиля из гаража
function UCatalog_get_garage(id){
	let request_object = new Object;
		request_object.action = 'get_garage';
		request_object.id = id;
		
	UCatalog_ajax(request_object);
}

// Функция отображения списка автомобилей из гаража
function UCatalog_show_garage_list(){
	if($(".UCatalog_garage_btn").next().css('display') == 'none'){
		$(".UCatalog_garage_btn").next().css('display', 'block');
	}else{
		$(".UCatalog_garage_btn").next().css('display', 'none');
	}
}

// Функция отображения окна добавления в блокнот
function UCatalog_show_modal_add_notepad(manufacturer, article, name){
	$("#UCatalog_modal_garage_body").html('');
	
	let request_object = new Object;
		request_object.action = 'get_notepad';
		request_object.manufacturer = manufacturer;
		request_object.article = article;
		request_object.name = name;
		
	UCatalog_ajax(request_object);
	
	$("#UCatalog_modal_garage").modal();
}

// Функция добавления в блокнот
function UCatalog_add_notepad(manufacturer, article, name){
	
	let n = document.getElementById("UCatalog_garage_auto").options.selectedIndex;
	let id_notepad = document.getElementById("UCatalog_garage_auto").options[n].value;
	
	let request_object = new Object;
		request_object.action = 'add_notepad';
		request_object.id_notepad = id_notepad;
		request_object.manufacturer = manufacturer;
		request_object.article = article;
		request_object.name = name;
		
	UCatalog_ajax(request_object);
}

//++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++





//////////////////////////////////////////////////////////////////////////////////////////////////////

// ФУНКЦИИ РАБОТЫ С ИНФОРМАЦИЕЙ О ТОВАРЕ

// Список товаров для которых ранее была запрошена информация
var list_products_info = new Array;

// Функция отображения информации товара
function show_modal_product_info(manufacturer, article, name){
	let key = list_products_info.findIndex(item => item.manufacturer == manufacturer && item.article == article);

	if(key === -1){
		let tmp = new Object;
			tmp.ajax = false;
			tmp.json = false;
			tmp.manufacturer = manufacturer;
			tmp.article = article;
			tmp.name = name;
		key = list_products_info.push(tmp) - 1;
	}
	
	//console.log(key);
	//console.log(list_products_info);
	
	if(list_products_info[key].ajax === false){
		list_products_info[key].ajax = true;
		
		let request_object = new Object;
			request_object.action = 'get_info';
			request_object.key = key;
			request_object.manufacturer = list_products_info[key].manufacturer;
			request_object.article = list_products_info[key].article;
		
		jQuery.ajax({
			type: "POST",
			async: false,
			url: "/api/UCatalog/api.php",
			dataType: "json",
			data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
			success: function(answer)
			{
				if(answer.status === true){
					list_products_info[answer.key].json = true;
					$("#UCatalog_container").append('<div style="display:none; max-height: 100px; overflow: auto; text-align: left; margin-bottom: 20px; border: 1px solid #999; padding: 10px;" id="product_info_'+answer.key+'">'+answer.json+'</div>');
				}
			},
			error: function(){
				console.log('Error: get_info');
			}
		});
	}
	
	if(list_products_info[key].json === true){
		let json = $("#product_info_"+key).html();
		
		let request_object = new Object;
			request_object.action = 'get_info_html';
			request_object.json = json;
			request_object.key = key;
			request_object.manufacturer = list_products_info[key].manufacturer;
			request_object.article = list_products_info[key].article;
			request_object.name = list_products_info[key].name;
		
		jQuery.ajax({
			type: "POST",
			async: false,
			url: "/api/UCatalog/api.php",
			dataType: "json",
			data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
			success: function(answer)
			{
				if(answer.status === true){
					document.getElementById("UCatalog_modal_products_info_body").innerHTML = answer.html;
					$("#UCatalog_modal_products_info").modal();
				}
			},
			error: function(){
				console.log('Error: get_info_html');
			}
		});
		
	}
}

// Функция переключения таба с информацией о товаре
function show_product_info_tab(id){
	$(".product_info_tab").css('display','none');
	$("#product_info_tab_"+id).css('display','block');
}

//////////////////////////////////////////////////////////////////////////////////////////////////////





// Для корректной одновременной работы нескольких модальных окон
$(document).on('hidden.bs.modal', '.modal', function () {
    $('.modal:visible').length && $(document.body).addClass('modal-open');
});





// После загрузки страницы
$(document).ready(function(){
	// Устанавливаем cookie
    var date = new Date(new Date().getTime() + 15552000 * 1000);
    document.cookie = "UCatalog=1; path=/; expires=" + date.toUTCString();
	
	// Загружаем вкладки
	UCatalog_loading('get_types', '', '');
});
