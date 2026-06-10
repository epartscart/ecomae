<?php
//Скрипт модуля для параметров каталога Дисков Ucats
defined('_ASTEXE_') or die('No access');
?>


<div style="padding-right:20px;margin-bottom:20px;" id="group_fields">
</div>
<script>
// ------------------------------------------------------------------------------------
//Обработка переключения селектора групп
function groupChanged()
{
	jQuery.ajax({
		type: "POST",
		async: true, //Запрос синхронный
		url: "/content/shop/ucats/akb/ajax_get_group_fields.php",
		dataType: "json",//Тип возвращаемого значения
		success: function(answer){
			console.log(answer);
			showGroupFields(answer);//Отображаем селекторы свойств
			getProducts();//Делаем запрос товаров
		}
	});
}
// ------------------------------------------------------------------------------------
//Функция отображения полей свойств для выбранной группы
var group_fields_list = "";//Переменная для списка полей со свойствами
function showGroupFields(fields)
{
	group_fields_list = new Array();
	
	var html = "";
	for(var i=0 ; i < fields.length; i++)
	{
		//Добавляем свойство в массив описаний свойств
		group_fields_list[group_fields_list.length] = new Object;
		group_fields_list[group_fields_list.length-1].id = "#field_"+fields[i].name;//ID для JS
		group_fields_list[group_fields_list.length-1].name = "field_"+fields[i].name;//Имя колонки
		group_fields_list[group_fields_list.length-1].field = fields[i].name;//Имя колонки
		group_fields_list[group_fields_list.length-1].caption = fields[i].caption;//Название свойства
		//Формируем HTML-код
		html += fields[i].caption+"<br>";
		html += "<select id=\"field_"+fields[i].name+"\">";
		for(var v=0 ; v < fields[i].values.length; v++)
		{
			var caption = fields[i].values[v];
			if(caption == "") caption = "<?php echo translate_str_by_id(4775); ?>";
			html += "<option value=\""+fields[i].values[v]+"\">"+caption+"</option>";
		}
		
		html += "</select>";
	}
	document.getElementById("group_fields").innerHTML = html;
	
	
	//Делаем все селекторы с множественным выбором
	for(var i=0; i < group_fields_list.length; i++)
	{
		$(group_fields_list[i].id).multipleSelect({placeholder: "<?php echo translate_str_by_id(3200); ?>...", width:"100%", onClose: function(){getProducts()}});
		//$(group_fields_list[i].id).multipleSelect('uncheckAll');
		$(group_fields_list[i].id).multipleSelect('checkAll');
	}
}
// ------------------------------------------------------------------------------------
//Метод запроса товаров
var Products = "";
var CurrentPage = 0;//Текущая страница
var Products_per_page = 10;
function getProducts()
{
	//console.log("Запрашиваем товары");
	
	//Объект для запроса товаров
	var products_query = new Object;
	
	//Получаем отмеченные значения свойств
	for(var i=0; i < group_fields_list.length; i++)
	{
		group_fields_list[i].values = $(group_fields_list[i].id).multipleSelect('getSelects');
		if(group_fields_list[i].values.length == 0)
		{
			alert("<?php echo translate_str_by_id(4776); ?> "+group_fields_list[i].caption);
			return;
		}
	}
	products_query.fields = group_fields_list;//Добавляем список со свойствами в объект запроса
	
	jQuery.ajax({
		type: "POST",
		async: true, //Запрос синхронный
		url: "/content/shop/ucats/akb/ajax_get_products.php",
		dataType: "json",//Тип возвращаемого значения
		data: "products_query="+encodeURIComponent(JSON.stringify(products_query)),
		success: function(answer){
			
			console.log(answer);
			
			Products = answer;
			CurrentPage = 0;//Начинаем отображать с нулевой страницы
			showProducts();//Отображаем
		}
	});
}
// ------------------------------------------------------------------------------------
//Функция генерации HTML-кода для товаров
function showProducts()
{
	var html = "";
	
	//console.log(Products);
	var printed = 0;//Счетчик отображенных товаров
	for(var i=CurrentPage*Products_per_page; i < Products.length && printed < Products_per_page; i++)
	{
		//Блок продукта
		html += "<div class=\"ucats_product_block\">";
		
		//Изображение
		html += "<img class=\"product_img\" onerror=\"/\" src=\""+Products[i].img+"\">";
		
		//Производитель
		html += "<div class=\"manufacturer\">"+Products[i].manufacturer+"</div>";
		
		//Артикул
		html += "<div class=\"article\" onclick=\"location='<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article="+Products[i].article+"';\">"+Products[i].article+"</div>";
		
		//Характеристики
		var characteristics = "";
		if(Products[i].value != "")
		{
			if(characteristics != "") characteristics += "/";
			characteristics += Products[i].value+" л";
		}
		if(Products[i].viscosity != "")
		{
			if(characteristics != "") characteristics += "/";
			characteristics += Products[i].viscosity;
		}
		//html += "<div class=\"characteristics\">"+characteristics+"</div>";
		
		
		//Состав
		var composition = "";
		if(Products[i].composition != "")
		{
			if(composition != "") composition += "/";
			composition += Products[i].composition;
		}
		//html += "<div class=\"composition\">"+composition+"</div>";
		
		
		//Наименование
		html += "<div class=\"name_product_top\">"+Products[i].name+"</div>";
		
		//Кнопка "Подробнее"
		html += "<div class=\"product_page_button\"><a href=\"<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/akkumulyatory/tovar?tovar="+Products[i].id+"\" class=\"bread_crumbs_a\"><?php echo translate_str_by_id(3608); ?></a></div>";
		
		html += "</div>";
		printed++;
	}
	
	
	//Выводим переключатели страниц
	/*
	Выводится:
	- первая
	- текущая
	- последняя
	- по две с каждой стороны от текущей
	*/
	
	//HTML-код переключателя страниц
	//Первая страница
	var first_page = "<div onclick=\"go_to_page(0);\" class=\"pages_selector\">0</div>";
	if(CurrentPage == 0) first_page = "";
	//Последняя страница
	var pages_total = parseInt(Products.length / Products_per_page);//Всего страниц
	if(pages_total < Products.length / Products_per_page) pages_total++;
	pages_total --;//Т.е. последняя страница и количество страниц - это не одно и тоже
	var last_page = "<div onclick=\"go_to_page("+pages_total+");\" class=\"pages_selector\">"+pages_total+"</div>";
	if(CurrentPage == pages_total) last_page = "";
	//Текущая страница
	var current_page = "<div class=\"pages_selector current_page\">"+CurrentPage+"</div>";
	//Пара от текущей справа
	var right_pages = "";
	for(var i = CurrentPage+1; i < pages_total && i < CurrentPage + 4; i++)
	{
		if(i == CurrentPage+3)
		{
			right_pages += "<div onclick=\"go_to_page("+i+");\" class=\"pages_selector\">...</div>";
		}
		else
		{
			right_pages += "<div onclick=\"go_to_page("+i+");\" class=\"pages_selector\">"+i+"</div>";
		}
	}
	//Пара от текущей слева
	var left_pages = "";
	for(var i = CurrentPage-1; i > 0 && i > CurrentPage-4; i--)
	{
		if(i == CurrentPage-3)
		{
			left_pages = "<div onclick=\"go_to_page("+i+");\" class=\"pages_selector\">...</div>" + left_pages;
		}
		else
		{
			left_pages = "<div onclick=\"go_to_page("+i+");\" class=\"pages_selector\">"+i+"</div>" + left_pages;
		}
	}
	//Компонуем:
	var pages_selector_container = "<div style=\"padding:5px; width:100%; text-align:center;\">"+first_page + left_pages + current_page + right_pages + last_page+"</div>";
	
	//Отображаем товары
	document.getElementById("products_block").innerHTML = html + pages_selector_container;
}
// ------------------------------------------------------------------------------------
//Переход на требуемую страницу
function go_to_page(need_page)
{
	CurrentPage = need_page;
	showProducts();
}
// ------------------------------------------------------------------------------------
</script>