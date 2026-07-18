<?php
/**
 * Скрипт для страницы поиска автозапчастей по артикулу, вариант "Сквозная сортировка"
*/
defined('_ASTEXE_') or die('No access');

if (!isset($epc_storefront_prices_visible)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
	$epc_storefront_prices_visible = epc_storefront_prices_visible_for_user(isset($user_id) ? (int) $user_id : null);
}
?>



<script>
var epc_storefront_prices_visible = <?php echo !empty($epc_storefront_prices_visible) ? 'true' : 'false'; ?>;
var epc_storefront_price_login_cta_html = <?php echo json_encode(epc_storefront_prices_login_cta_html(), JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_storefront_commerce_login_cta_html = <?php echo json_encode(function_exists('epc_storefront_commerce_login_cta_html') ? epc_storefront_commerce_login_cta_html() : '', JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_storefront_login_url = <?php echo json_encode(function_exists('epc_storefront_auth_login_url') ? epc_storefront_auth_login_url(isset($multilang_params) && is_array($multilang_params) ? $multilang_params : null) : '/en/users/login', JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
function epcStorefrontRequireLoginForCommerce()
{
	if(typeof epc_storefront_prices_visible === 'undefined' || epc_storefront_prices_visible)
	{
		return true;
	}
	var url = (typeof epc_storefront_login_url !== 'undefined' && epc_storefront_login_url) ? epc_storefront_login_url : '/en/users/login';
	window.location.href = url;
	return false;
}
function epcStorefrontPriceCellHTML(priceHtml)
{
	if(typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
	{
		return epc_storefront_price_login_cta_html || '&mdash;';
	}
	return priceHtml;
}
//АЛГОРИТМ ПОИСКА ЗАПЧАСТЕЙ ПО АРТИКУЛУ
var search_object = new Object;//Объект запроса

//Первым делом указываем артикул и производителя:
search_object.article = "<?php echo $article; ?>";
search_object.searsch_str = "<?php echo $searsch_str; ?>";
search_object.manufacturers = null;//Задаем значение null
<?php if (!empty($manufacturer)) { ?>
search_object.requested_manufacturer = "<?php echo str_replace(array('\\', '"'), array('\\\\', '\\"'), mb_strtoupper(html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8'), 'UTF-8')); ?>";
<?php } ?>

var group_day = 2;// Аналоги 0 - 2 дня
var epcCrossFallbackRows = <?php echo json_encode($epc_cross_fallback_rows, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;

//ОБЪЕКТ ОПИСАНИЯ ТОВАРОВ ПРИНЯТЫХ ОТ СЕРВЕРА
var Products = new Object;
//Запрошеннын товары
Products.Required = new Array();//Объект с запрошенными товарами
//Найденные по наименованию
Products.SearchName = new Array();
//Аналоги с быстрой доставкой
Products.Quick_Analogs = new Array();
//Аналоги
Products.Analogs = new Array();
//Возможные замены
Products.PossibleReplacement = new Array();
//Дополнительный свободный блок для доработок
Products.Spare_Box = new Array();

//Индексный список для поиска нужного объекта по его клиентскому ID (AID - All ID). Т.е. каждый объект товара при примеме от сервера получает ID в рамках данной страницы.
//Этот список предназначен для получения объекта товара по его AID:
Products.All = new Array();//Список объектов 

function epcCrossEsc(value)
{
	return String(value || '').replace(/[&<>"']/g, function(ch) {
		return ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'})[ch];
	});
}

function epcCrossSearchUrl(row)
{
	row = row || {};
	if(typeof epcChpuBrandArticleUrl === 'function')
	{
		return epcChpuBrandArticleUrl(row.brand || row.manufacturer || '', row.article || row.article_show || '');
	}
	var url = '<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article=' + encodeURIComponent(row.article || '');
	if(row.brand){
		url += '&brend=' + encodeURIComponent(row.brand);
	}
	return url;
}

function epcCrossFallbackHTML()
{
	if(!epcCrossFallbackRows || !epcCrossFallbackRows.length)
	{
		return '';
	}
	var html = '<div class="epc-cross-fallback"><div class="epc-cross-fallback__head"><strong>Cross reference numbers for <?php echo htmlspecialchars($article, ENT_QUOTES, 'UTF-8'); ?></strong><span>Direct stock was not found. Choose a cross number to check availability and price.</span></div>';
	html += '<div class="table-responsive"><table class="table table-condensed table-striped epc-cross-fallback__table"><thead><tr><th>Brand</th><th>Part number</th><th>Cross reference</th><th class="text-right">Search availability & price</th></tr></thead><tbody>';
	for(var i=0; i<epcCrossFallbackRows.length; i++)
	{
		var row = epcCrossFallbackRows[i];
		html += '<tr><td>' + epcCrossEsc(row.brand) + '</td><td><strong>' + epcCrossEsc(row.article) + '</strong></td><td>' + epcCrossEsc(row.cross_brand) + ' ' + epcCrossEsc(row.cross_article) + '</td><td class="text-right"><a class="btn btn-xs btn-primary" href="' + epcCrossEsc(epcCrossSearchUrl(row)) + '">Search availability & price</a></td></tr>';
	}
	html += '</tbody></table></div></div>';
	return html;
}
</script>



<script>
function epcSameArticle(left, right)
{
	var normalize = function(value) {
		return String(value || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	};
	return normalize(left) === normalize(right);
}
function epcSameManufacturer(left, right)
{
	if(right == null)
	{
		return true;
	}
	return String(left || '').trim().toUpperCase() === String(right || '').trim().toUpperCase();
}

// -------------------------------------------------------------------------------------------------------------------------------
//Обработка полученного результата
function onGetStoragesData()
{
	<?php
	if((int)$group_info['for_percentage'] === 1){
	?>
	recalculate_groups_margin();//Функция смены наценки группы
	<?php
	}
	?>
	resultSort();//Сортировка
	resultReview();//Обновляем отображние результата
}
// -------------------------------------------------------------------------------------------------------------------------------
//Метод соединения полученного от сервера результата по одной связке с общим объектом описания найденных товаров
function bindBunchResult(answer)
{
	for(var i=0; i < answer.Products.length; i++)
	{
		// Преобразование типов
		answer.Products[i]["exist"] = answer.Products[i]["exist"] * 1;
		answer.Products[i]["time_to_exe"] = answer.Products[i]["time_to_exe"] * 1;
		answer.Products[i]["price"] = answer.Products[i]["price"] * 1;
		
		var manufacturer 			= String(answer.Products[i]["manufacturer"]);
		manufacturer = $('<textarea />').html(manufacturer).text();
		
        var article 				= String(answer.Products[i]["article"]);
        
        var exist 					= answer.Products[i]["exist"];
        var time_to_exe 			= answer.Products[i]["time_to_exe"];
		var price 					= answer.Products[i]["price"];
		var storage 				= answer.Products[i]["storage_id"] * 1;
		var storage_color 			= answer.Products[i]["color"];
		
		var search_name 			= answer.Products[i]["search_name"] * 1;
		
		// Список найденных брендов
		if(arr_manufacturers.indexOf(manufacturer) === -1)
		{
			arr_manufacturers.push(manufacturer);
		}
		
		// Список найденных складов
		if(arr_storages.indexOf(storage) === -1)
		{
			arr_storages.push(storage);
			// Список фона складов
			arr_storages_color[storage] = storage_color;
		}
		
		
		//Добавляем объект в список всех объектов (AID)
		var AID_Object = new Object;//Учетный объект данного объекта товара, который будет добавлен в список Products.All
		AID_Object.aid = Products.All.length;//AID данного объекта товара
		answer.Products[i].aid = Products.All.length;//AID данного объекта товара
		
		
		//Определяем Б\У
		let by_flag = false;
		if(typeof(answer.Products[i].json_params) == 'string'){
			if(answer.Products[i].json_params){
				let json_params = JSON.parse(answer.Products[i].json_params);
				if(json_params.used == 1){
					by_flag = true;
				}
			}
		}
		
		
		if(by_flag == true){
			// Дополнительный свободный блок
			Products.Spare_Box.push(answer.Products[i]);
				
			//Для учетного объекта - указываем, что товар находится в списке аналогов
			AID_Object.isRequired 				= false;				
			AID_Object.isQuickAnalogs 			= false;
			AID_Object.isAnalogs 				= false;
			AID_Object.isPossibleReplacement 	= false;
			AID_Object.isSpare_Box			 	= true;
		}
		else
		{
			if(search_name === 1){
				
				Products.SearchName.push(answer.Products[i]);
				//Для учетного объекта - указываем, что товар находится в списке найденных по наименованию
				AID_Object.isSearchName 			= true;//Флаг - найденный по наименованию
				AID_Object.isRequired  				= false;
				AID_Object.isQuickAnalogs 			= false;
				AID_Object.isAnalogs 				= false;
				AID_Object.isPossibleReplacement	= false;
				AID_Object.isSpare_Box				= false;
			
			}else{
				//Продукт считает Запрошенным, если совпал Артикул и Производитель (если мы его учитываем)
				var requestedManufacturer = SelectedManufacturer;
				if(requestedManufacturer == null && search_object.requested_manufacturer)
				{
					requestedManufacturer = search_object.requested_manufacturer;
				}
				if( epcSameArticle(article, search_object.article) && epcSameManufacturer(manufacturer, requestedManufacturer) )
				{
					Products.Required.push(answer.Products[i]);

					//Для учетного объекта - указываем, что товар находится в списке запрошенных
					AID_Object.isRequired  				= true;//Флаг - Запрошенный
					AID_Object.isQuickAnalogs 			= false;
					AID_Object.isAnalogs 				= false;
					AID_Object.isPossibleReplacement 	= false;
					AID_Object.isSpare_Box			 	= false;
				}
				else//Товар распределяем в Аналоги и делем на 2 категории:
				{
					if(epcSameArticle(article, search_object.article)){
						// Возможные замены
						Products.PossibleReplacement.push(answer.Products[i]);
							
						//Для учетного объекта - указываем, что товар находится в списке аналогов
						AID_Object.isRequired 				= false;				
						AID_Object.isQuickAnalogs 			= false;
						AID_Object.isAnalogs 				= false;
						AID_Object.isPossibleReplacement 	= true;
						AID_Object.isSpare_Box			 	= false;
					}else{
						if(time_to_exe <= group_day){
							// быстрые аналоги
							Products.Quick_Analogs.push(answer.Products[i]);
							
							//Для учетного объекта - указываем, что товар находится в списке аналогов
							AID_Object.isRequired 				= false;
							AID_Object.isQuickAnalogs 			= true;
							AID_Object.isAnalogs 				= false;
							AID_Object.isPossibleReplacement 	= false;
							AID_Object.isSpare_Box			 	= false;
						}else{
							// Остальные аналоги
							//Добавляем сам объект товара
							Products.Analogs.push(answer.Products[i]);
							
							//Для учетного объекта - указываем, что товар находится в списке аналогов
							AID_Object.isRequired 				= false;				
							AID_Object.isQuickAnalogs 			= false;
							AID_Object.isAnalogs 				= true;
							AID_Object.isPossibleReplacement 	= false;
							AID_Object.isSpare_Box			 	= false;
						}
					}
				}
			}
		}
		
		
		//Добавляем учетный объект в список учетных объектов
		Products.All.push(AID_Object);
		
	}
		
		
	// После того как мы опросили данного поставщика и получили текущий список найденных складов и производителей, нужно обновить фильтр складов и брендов
	
	// Бренды
	filter['manufacturer_blok'].list_options = new Array;
	arr_manufacturers.sort(sortFunction);// Сортировка
	for(var i = 0; i < arr_manufacturers.length; i++)
	{
		filter['manufacturer_blok'].list_options[i] = new Object;
		filter['manufacturer_blok'].list_options[i].id = i;
		filter['manufacturer_blok'].list_options[i].value = false;
		filter['manufacturer_blok'].list_options[i].text = arr_manufacturers[i];
		filter['manufacturer_blok'].list_options[i].search = arr_manufacturers[i];
	}
	
	// Склады
	filter['storages_blok'].list_options = new Array;
	arr_storages.sort(sortFunction);// Сортировка
	for(var i = 0; i < arr_storages.length; i++)
	{
		filter['storages_blok'].list_options[i] = new Object;
		filter['storages_blok'].list_options[i].id = i;
		filter['storages_blok'].list_options[i].value = false;
		
		var table = "<table><tr><td><div style=\"width: 30px; height: 10px; border:1px solid #eee;  display: inline-block; margin-right: 5px; margin-left: 3px; background:"+ arr_storages_color[arr_storages[i]] +";\"></div></td><td>"+ all_storages[arr_storages[i]] +"</td></tr></table>";
		
		filter['storages_blok'].list_options[i].text = table;
		filter['storages_blok'].list_options[i].search = String(arr_storages[i]);
	}
	
}
// ----------------------------------------------------------------------------------------------------------------------------------
//Отображение/Переотображение результата запроса
function resultReview()
{
    //Общее содержимое
    var products_html = "";
    
    //HTML для блока с заголовками колонок
    var headlines = new Object;
    headlines.manufacturer = new Object;
    headlines.manufacturer.caption = "<?php echo translate_str_by_id(2070); ?>";
    headlines.manufacturer.subclass = "";
    headlines.article = new Object;
    headlines.article.caption = "<?php echo translate_str_by_id(2071); ?>";
    headlines.article.subclass = "";
    headlines.name = new Object;
    headlines.name.caption = "<?php echo translate_str_by_id(2102); ?>";
    headlines.name.subclass = "";
    headlines.exist = new Object;
    headlines.exist.caption = "<?php echo translate_str_by_id(4324); ?>";
    headlines.exist.subclass = "";
    headlines.price = new Object;
    headlines.price.caption = "<?php echo translate_str_by_id(2751); ?>, <?php echo str_replace('"', '\"', $currency_sign); ?>";
    headlines.price.subclass = "";
    headlines.time_to_exe = new Object;
    headlines.time_to_exe.caption = "<?php echo translate_str_by_id(3550); ?>";
    headlines.time_to_exe.subclass = "";
    //Ставим обозначение текущей внутренней сортровки
    headlines[sortState.field].caption += " <img src=\"/content/files/images/"+sortState.asc_desc+".png\" />";
    headlines[sortState.field].subclass += " sorted";
    
	
    var head_block = '<tr><th class="th_manufacturer'+headlines.manufacturer.subclass+'" onclick="sortChange(\'manufacturer\');">'+headlines.manufacturer.caption+'</th><th class="th_article'+headlines.article.subclass+'" onclick="sortChange(\'article\');">'+headlines.article.caption+'</th><th class="th_name'+headlines.name.subclass+'" onclick="sortChange(\'name\');">'+headlines.name.caption+'</th><th class="th_exist'+headlines.exist.subclass+'" onclick="sortChange(\'exist\');">'+headlines.exist.caption+'</th><th class="th_time_to_exe'+headlines.time_to_exe.subclass+'" onclick="sortChange(\'time_to_exe\');">'+headlines.time_to_exe.caption+'</th><th class="th_info"><?php echo translate_str_by_id(4325); ?></th><th class="th_price'+headlines.price.subclass+'" onclick="sortChange(\'price\');">'+headlines.price.caption+'</th><th class="th_add_to_cart"></th><th class="th_color"></th></tr>';
    

    //HTML для запрошенного артикула
    var required_block = "";
    var SearchName_block = "";
	//HTML для быстрых аналогов
	var quick_analogs_block = "";
    //HTML для аналогов
    var analogs_block = "";
    var PossibleReplacement_block = "";
    var Spare_Box_block = "";
    
	
	// Найденные бренды после фильтрации = сбрасываем список перед фильтрацией
	arr_manufacturers_posle_filter =  new Array();
	// Найденные склады после фильтрации
	arr_storages_posle_filter =  new Array();
	
	
	
	// Массив всех найденных позиций после фильтрации. Нужен для формирования фильтра
	var ALL_ProductsObjects = new Array;
	
	
	// Если текущий выбранный фильтр производители или склады то сбрасываем фильтры диапазонов на предыдущие значения, что бы можно было отменить фильтр убрав checkbox
	if(this_filter == 'manufacturer_blok' || this_filter == 'storages_blok' || this_filter == 'sam_price_time_blok'){
	// Цена
		filter['price_blok'].min_need = filter['price_blok'].old_min_need;
		filter['price_blok'].max_need = filter['price_blok'].old_max_need;
	// Срок
		filter['time_to_exe_blok'].min_need = filter['time_to_exe_blok'].old_min_need;
		filter['time_to_exe_blok'].max_need = filter['time_to_exe_blok'].old_max_need;
	// Наличие
		filter['exist_blok'].min_need = filter['exist_blok'].old_min_need;
		filter['exist_blok'].max_need = filter['exist_blok'].old_max_need;
	}
	

	// Ограничение количества отображаемых позиций (10, 20, 50)
	// Счетчик отфильтрованных групп
	var Products_Required_Show_count = 0;
	var Products_SearchName_Show_count = 0;
	var Products_Quick_Analogs_Show_count = 0;
	var Products_Analogs_Show_count = 0;
	var Products_PossibleReplacement_Show_count = 0;
	var Products_Spare_Box_Show_count = 0;
	// Количество групп после фильтрации (нужно для определения кнопки Показать еще)
	var Products_Required_count = 0;
	var Products_SearchName_count = 0;
	var Products_Quick_Analogs_count = 0;
	var Products_Analogs_count = 0;
	var Products_PossibleReplacement_count = 0;
	var Products_Spare_Box_count = 0;
	
	
	
	var Required = Products.Required;
	var SearchName = Products.SearchName;
	var Quick_Analogs = Products.Quick_Analogs;
	var Analogs = Products.Analogs;
	var PossibleReplacement = Products.PossibleReplacement;
	var Spare_Box = Products.Spare_Box;
	
	
	// Фильтрация позиций
	if(this_filter != ''){Required = filtering_items(Required);}
	// Если установлены флаги самых быстрых и дешевых позиций в группе
	if(sam_price_time != ''){Required = sam_price_time_fanc(Required);}
	
	
	if(Required.length > 0){
		
		// Общий массив позиций, по нему формируются новые значения фильтра
		ALL_ProductsObjects.push(Required);
   
		// Формируем html позиций блока
		for(var p=0; p < Required.length; p++){
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Required_count++;
			if( (cnt_on_page + start_page_Required) <= Products_Required_Show_count ){continue;}
			Products_Required_Show_count++;
			
			required_block += getProductRecordHTML(Required[p]);
		}
	}
	
	
	
	
	
	// Фильтрация позиций
	if(this_filter != ''){SearchName = filtering_items(SearchName);}
	// Если установлены флаги самых быстрых и дешевых позиций в группе
	if(sam_price_time != ''){SearchName = sam_price_time_fanc(SearchName);}
	
	
	if(SearchName.length > 0){
		
		// Общий массив позиций, по нему формируются новые значения фильтра
		ALL_ProductsObjects.push(SearchName);
   
		// Формируем html позиций блока
		for(var p=0; p < SearchName.length; p++){
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_SearchName_count++;
			if( (cnt_on_page + start_page_SearchName) <= Products_SearchName_Show_count ){continue;}
			Products_SearchName_Show_count++;
			
			SearchName_block += getProductRecordHTML(SearchName[p]);
		}
	}
	
	
	
	
	
	
	
	// Фильтрация позиций
	if(this_filter !== ''){Quick_Analogs = filtering_items(Quick_Analogs);}
	// Если установлены флаги самых быстрых и дешевых позиций в группе
	if(sam_price_time != ''){Quick_Analogs = sam_price_time_fanc(Quick_Analogs);}
	
	if(Quick_Analogs.length > 0){
		
		// Общий массив позиций, по нему формируются новые значения фильтра
		ALL_ProductsObjects.push(Quick_Analogs);
   
		// Формируем html позиций блока
		for(var p=0; p < Quick_Analogs.length; p++){
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Quick_Analogs_count++;
			if( (cnt_on_page + start_page_Quick_Analogs) <= Products_Quick_Analogs_Show_count ){continue;}
			Products_Quick_Analogs_Show_count++;
			
			quick_analogs_block += getProductRecordHTML(Quick_Analogs[p]);
		}
	}
	
	
	
	
	
	// Фильтрация позиций
	if(this_filter !== ''){Analogs = filtering_items(Analogs);}
	// Если установлены флаги самых быстрых и дешевых позиций в группе
	if(sam_price_time != ''){Analogs = sam_price_time_fanc(Analogs);}
	
	if(Analogs.length > 0){
		
		// Общий массив позиций, по нему формируются новые значения фильтра
		ALL_ProductsObjects.push(Analogs);
   
		// Формируем html позиций блока
		for(var p=0; p < Analogs.length; p++){
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Analogs_count++;
			if( (cnt_on_page + start_page_Analogs) <= Products_Analogs_Show_count ){continue;}
			Products_Analogs_Show_count++;
			
			analogs_block += getProductRecordHTML(Analogs[p]);
		}
	}
	
	
	
	
	// Фильтрация позиций PossibleReplacement
	if(this_filter !== ''){PossibleReplacement = filtering_items(PossibleReplacement);}
	// Если установлены флаги самых быстрых и дешевых позиций в группе
	if(sam_price_time != ''){PossibleReplacement = sam_price_time_fanc(PossibleReplacement);}
	
	if(PossibleReplacement.length > 0){
		
		// Общий массив позиций, по нему формируются новые значения фильтра
		ALL_ProductsObjects.push(PossibleReplacement);
   
		// Формируем html позиций блока
		for(var p=0; p < PossibleReplacement.length; p++){
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_PossibleReplacement_count++;
			if( (cnt_on_page + start_page_PossibleReplacement) <= Products_PossibleReplacement_Show_count ){continue;}
			Products_PossibleReplacement_Show_count++;
			
			PossibleReplacement_block += getProductRecordHTML(PossibleReplacement[p]);
		}
	}
	
	
	
	
	// Фильтрация позиций Spare_Box
	if(this_filter !== ''){Spare_Box = filtering_items(Spare_Box);}
	// Если установлены флаги самых быстрых и дешевых позиций в группе
	if(sam_price_time != ''){Spare_Box = sam_price_time_fanc(Spare_Box);}
	
	if(Spare_Box.length > 0){
		
		// Общий массив позиций, по нему формируются новые значения фильтра
		ALL_ProductsObjects.push(Spare_Box);
   
		// Формируем html позиций блока
		for(var p=0; p < Spare_Box.length; p++){
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Spare_Box_count++;
			if( (cnt_on_page + start_page_Spare_Box) <= Products_Spare_Box_Show_count ){continue;}
			Products_Spare_Box_Show_count++;
			
			Spare_Box_block += getProductRecordHTML(Spare_Box[p]);
		}
	}
	
	
	
	
	
	
	// После обработки всех позиций формируем новый фильтр диапазонов
	// Изменяем крайние значения фильтра, если фильтр диапазона не является текущим.
	if(ALL_ProductsObjects.length > 0){
		// Цена
		if(this_filter !== 'price_blok'){
			filter['price_blok'].min_value = undefined;
			filter['price_blok'].max_value = undefined;
		}
		// Срок
		if(this_filter !== 'time_to_exe_blok'){
			filter['time_to_exe_blok'].min_value = undefined;
			filter['time_to_exe_blok'].max_value = undefined;
		}
		// Наличие
		if(this_filter !== 'exist_blok'){
			filter['exist_blok'].min_value = undefined;
			filter['exist_blok'].max_value = undefined;
		}
	}
	
	
	// Устанавливаем новые значения фильтра
	for(var p=0; p < ALL_ProductsObjects.length; p++){
		var ProductsObjects_array = ALL_ProductsObjects[p];
		for(var i=0; i < ProductsObjects_array.length; i++){
			
			var ProductsObjects = ProductsObjects_array[i];
			
			// Цена
			if(filter['price_blok'].min_value == undefined){
				filter['price_blok'].min_value = ProductsObjects["price"];
			}
			if(filter['price_blok'].max_value == undefined){
				filter['price_blok'].max_value = ProductsObjects["price"];
			}
			
			if(ProductsObjects["price"] < filter['price_blok'].min_value){
				filter['price_blok'].min_value = ProductsObjects["price"];
			}
			if(ProductsObjects["price"] > filter['price_blok'].max_value){
				filter['price_blok'].max_value = ProductsObjects["price"];
			}
			
			// Срок
			if(filter['time_to_exe_blok'].min_value == undefined){
				filter['time_to_exe_blok'].min_value = ProductsObjects["time_to_exe"];
			}
			if(filter['time_to_exe_blok'].max_value == undefined){
				filter['time_to_exe_blok'].max_value = ProductsObjects["time_to_exe"];
			}
			
			if(ProductsObjects["time_to_exe"] < filter['time_to_exe_blok'].min_value){
				filter['time_to_exe_blok'].min_value = ProductsObjects["time_to_exe"];
			}
			if(ProductsObjects["time_to_exe"] > filter['time_to_exe_blok'].max_value){
				filter['time_to_exe_blok'].max_value = ProductsObjects["time_to_exe"];
			}
			
			// Наличие
			if(filter['exist_blok'].min_value == undefined){
				filter['exist_blok'].min_value = ProductsObjects["exist"];
			}
			if(filter['exist_blok'].max_value == undefined){
				filter['exist_blok'].max_value = ProductsObjects["exist"];
			}
			
			if(ProductsObjects["exist"] < filter['exist_blok'].min_value){
				filter['exist_blok'].min_value = ProductsObjects["exist"];
			}
			if(ProductsObjects["exist"] > filter['exist_blok'].max_value){
				filter['exist_blok'].max_value = ProductsObjects["exist"];
			}
			
        }
	}
	
	
	
	
	
	
	
	
    
    // Формируем HTML проценки
	
	if(Products_Required_count > 0)
	{
		quick_analogs_block = "";
		analogs_block = "";
		PossibleReplacement_block = "";
		SearchName_block = "";
		Spare_Box_block = "";
	}
	else
	{
		var epc_cross_fallback_html = epcCrossFallbackHTML();
		if(
			epc_cross_fallback_html != '' &&
			Products_Quick_Analogs_count == 0 &&
			Products_Analogs_count == 0 &&
			Products_PossibleReplacement_count == 0 &&
			Products_SearchName_count == 0 &&
			Products_Spare_Box_count == 0
		)
		{
			required_block = '';
			quick_analogs_block = '';
			analogs_block = '';
			PossibleReplacement_block = epc_cross_fallback_html;
		}
	}
	
	if(required_block != ''){
		required_block = '<tr><td colspan="9" class="products_table_block_caption"><?php echo translate_str_by_id(4326); ?></td></tr>' + required_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="9"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Required\');" id="next_page_Required"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}else{
		required_block = '<tr><td colspan="9" class="products_table_block_caption epc-required-not-found-caption"><strong><?php echo translate_str_by_id(4327); ?></strong></td></tr>';
	}
	
	if(quick_analogs_block != "")
	{
		quick_analogs_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="9" class="products_table_block_caption"><?php echo translate_str_by_id(4328); ?></td></tr>' + quick_analogs_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="9"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Quick_Analogs\');" id="next_page_Quick_Analogs"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}
	
	if(analogs_block != ''){
		analogs_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="9" class="products_table_block_caption"><?php echo translate_str_by_id(4329); ?></td></tr>' + analogs_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="9"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Analogs\');" id="next_page_Analogs"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}
	
	if(SearchName_block != ''){
		SearchName_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="9" class="products_table_block_caption"><?php echo translate_str_by_id(4330); ?></td></tr>' + SearchName_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="9"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'SearchName\');" id="next_page_SearchName"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}
	
	if(PossibleReplacement_block != ''){
		if(PossibleReplacement_block.indexOf('epc-cross-fallback') === -1)
		{
			PossibleReplacement_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="9" class="products_table_block_caption"><?php echo translate_str_by_id(4331); ?></td></tr>' + PossibleReplacement_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="9"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'PossibleReplacement\');" id="next_page_PossibleReplacement"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
		}
	}
	
	if(Spare_Box_block != ''){
		Spare_Box_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="9" class="products_table_block_caption"><?php echo translate_str_by_id(4660); ?></td></tr>' + Spare_Box_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="9"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Spare_Box\');" id="next_page_Spare_Box"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}
	
	// Оюъединяем блоки позиций
	products_html = required_block + PossibleReplacement_block + quick_analogs_block + analogs_block + SearchName_block + Spare_Box_block;
	
	if(products_html != ''){
		
		let for_percentage = '';
		<?php
		if((int)$group_info['for_percentage'] === 1){
		?>
		let show_data_checked = '';
		if(flag_show_data){
			show_data_checked = 'checked';
		}
		for_percentage += '<div class="div_show_data_checked" style="text-align: right; position: relative;"><input '+show_data_checked+' style="margin: 0; padding: 0; margin-right: 0px; height: 20px; width: 20px; position: absolute; top: 1px; cursor: pointer; right: 0px;" type="checkbox" id="show_data" onChange="show_data();"/></div><div class="show_data_class" style="text-align: right; padding-right: 25px;"><select id="select_the_groups_margin" onChange="changing_the_groups_margin();">';
			<?php
			$group_query = $db_link->prepare('SELECT * FROM `groups` ORDER BY `order`;');
			$group_query->execute();
			while($group_record = $group_query->fetch()){
				$selected = "";
				if($group_record['id'] === '1'){
					continue;
				}
				if($group_id == $group_record['id'])
				{
					$selected = "selected=\"selected\"";
				}
				echo "for_percentage += '<option ".$selected." value=\"".$group_record['id']."\">".translate_str_by_id($group_record['value'])."</option>';";
			}
			?>
		for_percentage += '</select></div>';
		<?php
		}
		?>
		
		if(products_html.indexOf('epc-cross-fallback') !== -1)
		{
			products_html = for_percentage + products_html;
		}
		else
		{
			products_html = for_percentage + '<table id="all_table_products"><thead>' + head_block + '</thead><tbody>' + products_html + '</tbody></table>';
		}
	}
	
	
	
	// Если после фильтрации все же не осталось позиций
	if(this_filter != '' && products_html == ''){
		products_html = '<div><?php echo translate_str_by_id(4078); ?></div>';
	}
    
	// Отображаем проценку
    document.getElementById("products_area").innerHTML = products_html;
	
	// Отображаем фильтр
	showPropertiesWidgets();
	
	// Работаем с кнопками "показать еще" вконце блоков
	var next_page_Required = document.getElementById('next_page_Required');
	var next_page_SearchName = document.getElementById('next_page_SearchName');
	var next_page_Quick_Analogs = document.getElementById('next_page_Quick_Analogs');
	var next_page_Analogs = document.getElementById('next_page_Analogs');
	var next_page_PossibleReplacement = document.getElementById('next_page_PossibleReplacement');
	var next_page_Spare_Box = document.getElementById('next_page_Spare_Box');
	
	if(next_page_Required){
		if((start_page_Required + cnt_on_page) >= Products_Required_Show_count){
			next_page_Required.style.display = 'none';
		}
		if((start_page_Required + cnt_on_page) < Products_Required_count){
			next_page_Required.style.display = 'inline-block';
		}
	}
	
	if(next_page_SearchName){
		if((start_page_SearchName + cnt_on_page) >= Products_SearchName_Show_count){
			next_page_SearchName.style.display = 'none';
		}
		if((start_page_SearchName + cnt_on_page) < Products_SearchName_count){
			next_page_SearchName.style.display = 'inline-block';
		}
	}
	
	if(next_page_Quick_Analogs){
		if((start_page_Quick_Analogs + cnt_on_page) >= Products_Quick_Analogs_Show_count){
			next_page_Quick_Analogs.style.display = 'none';
		}
		if((start_page_Quick_Analogs + cnt_on_page) < Products_Quick_Analogs_count){
			next_page_Quick_Analogs.style.display = 'inline-block';
		}
	}

	if(next_page_Analogs){
		if((start_page_Analogs + cnt_on_page) >= Products_Analogs_Show_count){
			next_page_Analogs.style.display = 'none';
		}
		if((start_page_Analogs + cnt_on_page) < Products_Analogs_count){
			next_page_Analogs.style.display = 'inline-block';
		}
	}

	if(next_page_PossibleReplacement){
		if((start_page_PossibleReplacement + cnt_on_page) >= Products_PossibleReplacement_Show_count){
			next_page_PossibleReplacement.style.display = 'none';
		}
		if((start_page_PossibleReplacement + cnt_on_page) < Products_PossibleReplacement_count){
			next_page_PossibleReplacement.style.display = 'inline-block';
		}
	}

	if(next_page_Spare_Box){
		if((start_page_Spare_Box + cnt_on_page) >= Products_Spare_Box_Show_count){
			next_page_Spare_Box.style.display = 'none';
		}
		if((start_page_Spare_Box + cnt_on_page) < Products_Spare_Box_count){
			next_page_Spare_Box.style.display = 'inline-block';
		}
	}
	
	
	
	show_pictures_products();// Отображаем миниатюры товаров

	<?php
	if((int)$group_info['for_percentage'] === 1){
	?>
	document.getElementById("select_the_groups_margin").value = this_group_id_margin;
	show_data();
	<?php
	}
	?>
}
// -------------------------------------------------------------------------------------------------------------------------------
//Единая функция формирования HTML-кода для одной записи товара. Эта функция работает для запрошенных товаров, так и для аналогов
function getProductRecordHTML(Product)
{
    var manufacturer = "", article_show = "", name = "";
            	
	var time_to_exe = Product.time_to_exe;
	if(Product.time_to_exe != Product.time_to_exe_guaranteed)
	{
		time_to_exe = Product.time_to_exe + "-" + Product.time_to_exe_guaranteed +" <?php echo translate_str_by_id(4097); ?>.";
	}else{
		if(time_to_exe == 0)
		{
			time_to_exe = "<?php echo translate_str_by_id(4197); ?>";
		}else{
			time_to_exe = time_to_exe + " <?php echo translate_str_by_id(4097); ?>.";
		}
	}
    var color = Product.color;
    
	//Строка для показа цены
	var price = (typeof epcFormatMoney === 'function') ? epcFormatMoney(Product.price) : digit(Product.price);
	price = epcStorefrontPriceCellHTML(price);
	
	
	
	<?php
	if((int)$group_info['for_percentage'] === 1){
	?>
	let price_purchase = Product.price_purchase;//Закупочная цена для блока тех. информации
	price_purchase = digit(price_purchase)
	price += "<div class='show_data_class' style='font-size: 10px; color: #959393; position: relative; top: 1px;'>"+ price_purchase +"</div>";
	<?php
	}
	?>
	
	
	
    //ФОРМИРОВАНИЕ КОЛОНКИ ИНФО
    var info = '';
	
    info += "<a title=\"<?php echo translate_str_by_id(4337); ?>\" href=\"https://www.google.ru/search?q="+encodeURIComponent(Product.manufacturer)+"+"+Product.article+"&newwindow=1&biw=1366&bih=667&tbm=isch&tbo=u&source=univ&sa=X&ved=0CC8QsARqFQoTCMDCoO70jMkCFQGFLAodrT0GFw\" target=\"_blank\"><span><i style=\"font-size: .8em;\" class=\"fa fa-camera\"></i></span></a>";
	
	if(Product.storage_caption != "")
    {
		var storage_caption = '<br><?php echo translate_str_by_id(3606); ?>: '+Product.storage_caption;
			storage_caption = storage_caption + '<br><?php echo translate_str_by_id(2750); ?>: '+all_storages[Product.storage_id];
			storage_caption = storage_caption + '<br>ID: '+Product.storage_id;
    }else{
		var storage_caption = '<br><?php echo translate_str_by_id(2750); ?>: '+all_storages[Product.storage_id];
	}
	
	info += "<a title=\"<?php echo translate_str_by_id(4333); ?>\" href=\"javascript:void(0);\"><span onclick=\"openInfoWindow('<?php echo translate_str_by_id(4333); ?>', '"+Product.office_caption+storage_caption+"');\" ><i class=\"fa fa-home\"></i></span></a>";
    
	<?php
	if($user_id){
	?>
	info += "<a href=\"javascript:void(0);\" title=\"<?php echo translate_str_by_id(2101); ?>\" onclick=\"show_add_bloknot("+Product.aid+");\"><span><i style=\"font-size: .9em;\" class=\"fa fa-car\"></i></span></a>";
	<?php
	}else{
	?>
	info += "<a href=\"javascript:void(0);\" title=\"<?php echo translate_str_by_id(2101); ?>\" onclick=\"alert('<?php echo translate_str_by_id(4323); ?>');\"><span><i style=\"font-size: .9em;\" class=\"fa fa-car\"></i></span></a>";
	<?php
	}
	?>
	
	info += "<br/>";
	
	if(Product.min_order > 1)
    {
        info += "<a title=\"<?php echo translate_str_by_id(1720); ?>\" href=\"javascript:void(0);\"><span onclick=\"openInfoWindow('<?php echo translate_str_by_id(2853); ?>', '<?php echo translate_str_by_id(1720); ?> "+Product.min_order+" <?php echo translate_str_by_id(4095); ?>.');\"><i class=\"fa fa-warning\"></i></span></a>";
    }
	
    if(Product.product_type == 1)//Для товара из каталога Treelax - выводим ссылку на страницу товара
    {
        info += "<a title=\"<?php echo translate_str_by_id(4334); ?>\" href=\""+Product.url+"\" target=\"_blank\"><i class=\"fa fa-file-image-o\"></i></a>";
    }
	
	info = '<span class="info_box">'+ info +'</span>';
	
	<?php
	if((int)$group_info['for_percentage'] === 1){
	?>
	info += "<div class='show_data_class' style=\"margin: 3px 0px; white-space: nowrap; max-width: 80px; font-size: 11px; position: relative;\" title=\""+ Product.storage_caption +"\"><span style=\"overflow: hidden; text-overflow: ellipsis;\" class=\"info_box\">"+ Product.storage_caption +"</span></div>";
	<?php
	}
	?>
    
	info = '<div style="margin: 3px 0px;">'+ info +'</div>';
	
	
    
    //Формирование колонки "Наличие"
    //Объект с описанием наличия - для отображения в информационном окне
    var supply_info_json = "{&quot;exist&quot;:"+Product.exist+",&quot;time_to_exe&quot;:"+Product.time_to_exe+",&quot;time_to_exe_guaranteed&quot;:"+Product.time_to_exe_guaranteed+",&quot;probability&quot;:"+Product.probability+"}";
    //Колонка
    var exist = "<span onclick=\"openInfoWindow(null, null, 1, '"+supply_info_json+"');\">" + Product.exist + "<img src=\"/lib/TreelaxCharts/sectors.php?number=2&value0="+Product.probability+"&value1="+(100-Product.probability)+"&start_angle=30&size=50&inside_size=1&slope=1.1\" /></span>";
    
	
	
	manufacturer = '<span title="'+ Product.manufacturer +'">'+ Product.manufacturer +'</span>';
	article_show = "<a title='<?php echo translate_str_by_id(4335); ?>: "+ Product.article_show +"' class=\"bread_crumbs_a\" style=\"text-decoration:underline; color:#000; font-weight:700;\" href=\""+ (typeof epcChpuBrandArticleUrl === 'function' ? epcCrossEsc(epcChpuBrandArticleUrl(Product.manufacturer, Product.article)) : ('<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article=' + encodeURIComponent(Product.article))) +"\">"+Product.article_show+"</a>";
	name = "<span title=\""+Product.name+"\">"+Product.name+"</span>";
	
	
	
	// Кнопки увеличения количества товара добавляемого в корзину //////////////////////////////////////////////////////////////////////////////////////////////
	var p_min_order = Product.min_order * 1;
	var p_exist = Product.exist * 1;
	if(isNaN(p_exist) || p_exist < 0){ p_exist = 0; }
	
	if(p_min_order == 0){
		p_min_order = 1;
	}
	
	if(typeof epcProductActionsHTML === 'function')
	{
		cart_html = epcProductActionsHTML(Product.aid, p_exist, p_min_order, 'both', Product.manufacturer, Product.article);
	}
	else
	{
		cart_html = '<div class="epc-product-actions">';
		cart_html += '<div class="epc-product-actions__qty"><input type="text" value="'+p_min_order+'" id="count_need_'+Product.aid+'" /></div>';
		cart_html += '<button type="button" class="btn btn-sm btn-danger epc-btn-cart" onclick="addToCart('+Product.aid+');">Add to Cart</button></div>';
	}
	//////////////////////////////////////////////////////////////////////////////////////////////
	
	
	// <!-------------------------------------------- Картинки в проценке -------------------------------------------->

	let key = list_products_info.findIndex(item => item.manufacturer == Product.manufacturer && item.article == Product.article);

	if(key === -1){
		let tmp = new Object;
			tmp.ajax = false;
			tmp.json = false;
			tmp.manufacturer = Product.manufacturer;
			tmp.article = Product.article;
		key = list_products_info.push(tmp) - 1;
	}

	exist = '<table style="float: right;"><tr><td><div style="text-align: right;"><a class="product_img_'+key+'" onClick="show_modal_product_info('+key+', '+Product.aid+')" style="display:none; line-height: 0; border: 1px solid #ddd; padding: 2px; margin-right: 2px; border-radius: 3px; cursor:pointer;" ><span style="width: 40px; height: 28px; display: inline-block; background-size: cover; background-position: center center; cursor:pointer;"></span></a></div></td><td><div style="width: 45px; text-align: right;">'+exist+'</div></td></tr></table>';
	
	name = "<span title=\""+Product.name+"\">"+Product.name+"</span>";
	
	// <!-------------------------------------------- End Картинки в проценке ---------------------------------------->
	
	
	// Товары Б\У - Добавляем картинку товара, если она есть
	if(typeof Product.json_params == "string"){
		if(Product.json_params != ""){
			let json_params = JSON.parse(Product.json_params);
			if(json_params.used == 1){
				let images = '';
				if(json_params.images){
					for(let i=0; i < json_params.images.length; i++)
					{
						if(i==0){
							images += '<a href="'+json_params.images[i]['url']+'" rel="lightbox-product-'+Product.aid+'"><img style="width: 20px; height: 17px; border: 1px solid #ddd; border-radius: 4px; margin-left: 5px;" src="'+json_params.images[i]['url']+'" /></a>';
						}else{
							images += '<a class="hidden" href="'+json_params.images[i]['url']+'" rel="lightbox-product-'+Product.aid+'"><img style="width: 20px; height: 17px; border: 1px solid #ddd; border-radius: 4px; margin-left: 5px;" src="'+json_params.images[i]['url']+'" /></a>';
						}
					}
				}
				name += images;
			}
		}
	}
	
	
	if(all_storages_info[Product.storage_id]['bg_line_color'] == 1){
		return '<tr style="background:'+color+';"><td class="td_manufacturer">'+ manufacturer +'</td><td class="td_article">'+ article_show +'</td><td class="td_name">'+ name +'</td><td class="td_exist">'+ exist +'</td><td class="td_time_to_exe"><span onclick="openInfoWindow(null, null, 1, \''+ supply_info_json +'\');">'+ time_to_exe +'</span></td><td class="td_info">'+ info +'</td><td class="td_price">'+ price +'</td><td class="td_add_to_cart">'+ cart_html +'</td>'+ '<td class="td_color" style="background:'+color+'"></td></tr>';
	}else{
		return '<tr><td class="td_manufacturer">'+ manufacturer +'</td><td class="td_article">'+ article_show +'</td><td class="td_name">'+ name +'</td><td class="td_exist">'+ exist +'</td><td class="td_time_to_exe"><span onclick="openInfoWindow(null, null, 1, \''+ supply_info_json +'\');">'+ time_to_exe +'</span></td><td class="td_info">'+ info +'</td><td class="td_price">'+ price +'</td><td class="td_add_to_cart">'+ cart_html +'</td>'+ '<td class="td_color" style="background:'+color+'"></td></tr>';
	}
}
// --------------------------------------------------------------------------------------------------------------------------------
var sortState = new Object;//Объект описания сортировки
sortState.field = 'price';
sortState.asc_desc = 'asc';
//Функция сортировки
function resultSort()
{
	Products.Required.sort(compareFields);
	Products.SearchName.sort(compareFields);
	Products.Quick_Analogs.sort(compareFields);
    Products.Analogs.sort(compareFields);
    Products.PossibleReplacement.sort(compareFields);
    Products.Spare_Box.sort(compareFields);
}
// ------------------------------------------------------------------------------------------------------------
function compareFields(x, y)
{
	//Для Чисел
	if(sortState.field == "manufacturer" || sortState.field == "article" || sortState.field == "name" )
	{
		if(sortState.asc_desc == "asc")
		{
			if(String(x[sortState.field]) > String(y[sortState.field]))
			{
				return 1;
			}
			else if(String(x[sortState.field]) < String(y[sortState.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
		else
		{
			if(String(x[sortState.field]) < String(y[sortState.field]))
			{
				return 1;
			}
			else if(String(x[sortState.field]) > String(y[sortState.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
	}
	else
	{
		if(sortState.asc_desc == "asc")
		{
			if(parseFloat(x[sortState.field]) > parseFloat(y[sortState.field]))
			{
				return 1;
			}
			else if(parseFloat(x[sortState.field]) < parseFloat(y[sortState.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
		else
		{
			if(parseFloat(x[sortState.field]) < parseFloat(y[sortState.field]))
			{
				return 1;
			}
			else if(parseFloat(x[sortState.field]) > parseFloat(y[sortState.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
	}
}
// ------------------------------------------------------------------------------------------------------------
//Смена сортировки
function sortChange(field)
{
    //Если тоже поле - меняем только направление
    if(sortState.field == field)
    {
        if(sortState.asc_desc == "asc")
        {
            sortState.asc_desc = "desc";
        }
        else
        {
            sortState.asc_desc = "asc";
        }
    }
    else//Если поле другое - ставим это поле и направление asc
    {
        sortState.field = field;
        sortState.asc_desc = "asc";
    }
    
    //Производим саму сортировку
    resultSort();
    //Обновляем отображние результата
    //Обновляем отображние результата
	if(this_filter != ''){
		productsCountRequest(this_filter.replace('_blok', ''));
	}else{
		resultReview();
	}
}
</script>











<!-------------------------------------------- Start Добавление в корзину -------------------------------------------->
<script>
//Добавление в корзину
function addToCart(aid)
{
	if(typeof epcStorefrontRequireLoginForCommerce === 'function' && !epcStorefrontRequireLoginForCommerce())
	{
		return;
	}
    //1. По списку учетных объектов определяем, в где находится объект товара (Запрошенные/Аналоги)
    var AID_Object = Products.All[aid];
    
    //2. Получаем сам объект товара
    var Product = new Object;
    if(AID_Object.isRequired == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.Required.length; i++)
        {
			if( parseInt(Products.Required[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Required[i]);
                break;
			}
        }
    }
    else if(AID_Object.isSearchName == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.SearchName.length; i++)
        {
			if( parseInt(Products.SearchName[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.SearchName[i]);
                break;
			}
        }
    }
    else if(AID_Object.isQuickAnalogs == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.Quick_Analogs.length; i++)
        {
			if( parseInt(Products.Quick_Analogs[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Quick_Analogs[i]);
                break;
			}
        }
    }
	else if (AID_Object.isAnalogs == true)
    {
        //Ищем объект товара в списке аналогов
        for(var i=0; i < Products.Analogs.length; i++)
        {
			if( parseInt(Products.Analogs[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Analogs[i]);
                break;
			}
        }
    }
    else if (AID_Object.isPossibleReplacement == true)
    {
        //Ищем объект товара в списке аналогов
        for(var i=0; i < Products.PossibleReplacement.length; i++)
        {
			if( parseInt(Products.PossibleReplacement[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.PossibleReplacement[i]);
                break;
			}
        }
    }
    else if (AID_Object.isSpare_Box == true)
    {
        //Ищем объект товара в списке аналогов
        for(var i=0; i < Products.Spare_Box.length; i++)
        {
			if( parseInt(Products.Spare_Box[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Spare_Box[i]);
                break;
			}
        }
    }
    
	//2.1 Добавляем пометку о количестве
	if(document.getElementById("count_need_"+aid)){
		var count_need = parseInt(document.getElementById("count_need_"+aid).value);
		Product['count_need'] = count_need;
	}else{
		Product['count_need'] = Product['min_order'];
	}

	//2.2 Заменяем синоним на имя производителя переданное поставщиком
	if(Product['manufacturer_transferred']){
		var manufacturer_tmp = Product['manufacturer'];
		Product['manufacturer'] = Product['manufacturer_transferred'];
		Product['manufacturer_transferred'] = manufacturer_tmp;
	}
	
    //3. Данные в корзину можно класть сразу целым перечнем - поэтому приводим к массиву
    var product_objects = new Array;
    product_objects.push(Product);
    
	//log('Объект добавленного в корзину товара:');
	//log(Product);
	
    //4. Добавляем его в корзину
    jQuery.ajax({
        type: "POST",
        async: false, //Запрос синхронный
        url: "/content/shop/order_process/ajax_add_to_basket.php",
        dataType: "json",//Тип возвращаемого значения
        data: "product_objects="+encodeURIComponent(JSON.stringify(product_objects)),
        success: function(answer)
        {
            if(answer.status == true)
            {
				updateCartInfo();//Обновление корзины снизу
				showAdded();//Показываем лэйбл снизу
            }
            else
            {
                if(answer.code == "already")
                {
                    alert("<?php echo translate_str_by_id(4336); ?>");
                }
                else
                {
                    alert(answer.message);
                }
            }
        }
    });
}//~function addToCart(aid)

function addToQuote(aid)
{
    var AID_Object = Products.All[aid];
    var Product = new Object;
    if(AID_Object.isRequired == true)
    {
        for(var i=0; i < Products.Required.length; i++)
        {
			if( parseInt(Products.Required[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Required[i]);
                break;
			}
        }
    }
    else if(AID_Object.isSearchName == true)
    {
        for(var i=0; i < Products.SearchName.length; i++)
        {
			if( parseInt(Products.SearchName[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.SearchName[i]);
                break;
			}
        }
    }
    else if(AID_Object.isQuickAnalogs == true)
    {
        for(var i=0; i < Products.Quick_Analogs.length; i++)
        {
			if( parseInt(Products.Quick_Analogs[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Quick_Analogs[i]);
                break;
			}
        }
    }
	else if (AID_Object.isAnalogs == true)
    {
        for(var i=0; i < Products.Analogs.length; i++)
        {
			if( parseInt(Products.Analogs[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Analogs[i]);
                break;
			}
        }
    }
    else if (AID_Object.isPossibleReplacement == true)
    {
        for(var i=0; i < Products.PossibleReplacement.length; i++)
        {
			if( parseInt(Products.PossibleReplacement[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.PossibleReplacement[i]);
                break;
			}
        }
    }
    else if (AID_Object.isSpare_Box == true)
    {
        for(var i=0; i < Products.Spare_Box.length; i++)
        {
			if( parseInt(Products.Spare_Box[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Spare_Box[i]);
                break;
			}
        }
    }
	if(document.getElementById("count_need_"+aid)){
		var count_need = parseInt(document.getElementById("count_need_"+aid).value);
		Product['count_need'] = count_need;
	}else{
		Product['count_need'] = Product['min_order'];
	}
	if(Product['manufacturer_transferred']){
		var manufacturer_tmp = Product['manufacturer'];
		Product['manufacturer'] = Product['manufacturer_transferred'];
		Product['manufacturer_transferred'] = manufacturer_tmp;
	}
    var product_objects = new Array;
    product_objects.push(Product);
    jQuery.ajax({
        type: "POST",
        async: false,
        url: "/content/shop/order_process/ajax_add_to_quote.php",
        dataType: "json",
        data: "product_objects="+encodeURIComponent(JSON.stringify(product_objects)),
        success: function(answer)
        {
            if(answer.status == true)
            {
				alert("Added to your quote (#"+answer.quote_id+").");
            }
            else
            {
                if(answer.code == "auth")
                {
                    alert(answer.message || "Please sign in.");
                }
                else
                {
                    alert(answer.message || "Error");
                }
            }
        }
    });
}//~function addToQuote(aid)
</script>
<!-------------------------------------------- End Добавление в корзину -------------------------------------------->