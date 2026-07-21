<?php
/**
 * Скрипт для страницы поиска автозапчастей по артикулу, вариант "Группировка по товарам"
 * 
 * Как работает сортировка при первоначальной загрузке страницы:
 * Внутри группы позиции сортируются по цене, затем позиции с доставкой 0-2дн. поднимаются выше остальных.
 * Внешняя сортировка происходит по сроку первых позиций в группе.
 * Внутрення сортировка происходит внутри каждой группы позиции по выбранному полю, затем позиции с сроком доставки, который настраевается ниже, поднимаются выше остальных.
 * При смене сортировки внешняя сортировка происходит по первым позициям в группе.
 * 
*/

defined('_ASTEXE_') or die('No access');

if (!isset($epc_storefront_prices_visible)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
	$epc_storefront_prices_visible = epc_storefront_prices_visible_for_user(isset($user_id) ? (int) $user_id : null);
}
$epc_vat_price_label = '';
$epc_vat_display_mode = 'inclusive';
$epc_vat_type = 'local_b2c';
$epc_vat_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
if (is_readable($epc_vat_file) && isset($db_link) && $db_link instanceof PDO) {
	require_once $epc_vat_file;
	$epc_vat_resolved = epc_uae_customer_vat_resolve($db_link, isset($user_id) ? (int) $user_id : 0);
	$epc_vat_price_label = (string)($epc_vat_resolved['price_label'] ?? '');
	$epc_vat_display_mode = (string)($epc_vat_resolved['display_mode'] ?? 'inclusive');
	$epc_vat_type = (string)($epc_vat_resolved['vat_type'] ?? 'local_b2c');
	echo epc_uae_customer_vat_styles();
}
?>



<script>
var epc_storefront_prices_visible = <?php echo !empty($epc_storefront_prices_visible) ? 'true' : 'false'; ?>;
var epc_storefront_sensitive_mask = <?php echo json_encode(epc_storefront_sensitive_mask(), JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_storefront_price_login_cta_html = <?php echo json_encode(epc_storefront_prices_login_cta_html(), JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_storefront_commerce_login_cta_html = <?php echo json_encode(epc_storefront_commerce_login_cta_html(null, true), JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_storefront_login_url = <?php echo json_encode(epc_storefront_auth_login_url(isset($multilang_params) && is_array($multilang_params) ? $multilang_params : null), JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_vat_price_label = <?php echo json_encode($epc_vat_price_label, JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
var epc_vat_display_mode = <?php echo json_encode($epc_vat_display_mode, JSON_HEX_TAG | JSON_HEX_APOS | JSON_HEX_QUOT | JSON_UNESCAPED_UNICODE); ?>;
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
function epcVatPriceLabelHTML(product)
{
	var lbl = '';
	if(product && product.vat_price_label)
	{
		lbl = String(product.vat_price_label);
	}
	else if(typeof epc_vat_price_label !== 'undefined' && epc_vat_price_label)
	{
		lbl = String(epc_vat_price_label);
	}
	if(!lbl){ return ''; }
	return '<span class="epc-vat-price-label">' + lbl + '</span>';
}
function epcStorefrontSensitiveMask()
{
	return (typeof epc_storefront_sensitive_mask !== 'undefined' && epc_storefront_sensitive_mask)
		? String(epc_storefront_sensitive_mask)
		: '**';
}
function epcStorefrontSensitiveCellHTML(html)
{
	if(typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
	{
		return epcStorefrontSensitiveMask();
	}
	return html;
}
function epcStorefrontPriceCellHTML(priceHtml, product)
{
	if(typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
	{
		// Guests / pending wholesale: mask price the same as qty / term / info.
		return epcStorefrontSensitiveMask();
	}
	var label = epcVatPriceLabelHTML(product || null);
	if(label){ priceHtml += label; }
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
<?php if (!empty($epc_chpu_direct_pricing) || (!empty($epc_brand_picker_mode) && !empty($epc_initial_price_bunch['Products']))) { ?>
var epcChpuAnchorHasDbStock = <?php echo !empty($epc_chpu_anchor_has_stock) ? 'true' : 'false'; ?>;
<?php } ?>
function epcUsesArticleOnlyStockUi()
{
	return (typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		|| (typeof epc_brand_picker_stock_preview !== 'undefined' && epc_brand_picker_stock_preview);
}



//Настройки связанные с этим типом отображения:
var group_day = 2;// Аналоги 0 - 2 дня
var cnt_to_hide = 99999;// Do not collapse extra warehouse rows — show all offers expanded
var epc_show_all_result_rows = true;// Show all cross-reference groups without "Show more" pagination
var flag_one = true;// true - Флаг показывает что клиент первый раз загружает проценку
var flag_time_head = <?=(int)$DP_Config->flag_time_head?>;// Перемещать ли позиции со сроком 0-2 дня выше остальных позиций в группе при первой загрузке проценки
var epcCrossFallbackRows = <?php echo json_encode($epc_cross_fallback_rows, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
var epcGenuinePartTypeIndex = <?php echo json_encode(isset($epc_genuine_part_type_index) ? $epc_genuine_part_type_index : array('brands' => array(), 'meta' => array()), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;


//ОБЪЕКТ ОПИСАНИЯ ТОВАРОВ ПРИНЯТЫХ ОТ СЕРВЕРА
var Products = new Object;

//Запрошенные товары
Products.Required = new Object;//Объект с запрошенными товарами
Products.Required.Products = new Object;//Структура для объектов товаров
Products.Required.Products.Manufacturers = new Object;//Разделение на производителей
Products.Required.ProductsTypes = new Array();//Список типов товаров

//Товары найденные по наименованию в каталоге или прайс листах
Products.SearchName = new Object;//Объект с найденными товарами
Products.SearchName.Products = new Object;//Структура для объектов товаров
Products.SearchName.Products.Manufacturers = new Object;//Разделение на производителей
Products.SearchName.ProductsTypes = new Array();//Список типов товаров

//Аналоги с быстрой доставкой 0 - 2 дня
Products.Quick_Analogs = new Object;
Products.Quick_Analogs.Products = new Object;
Products.Quick_Analogs.Products.Manufacturers = new Object;
Products.Quick_Analogs.ProductsTypes = new Array();//Список типов товаров

//Остальные Аналоги
Products.Analogs = new Object;
Products.Analogs.Products = new Object;
Products.Analogs.Products.Manufacturers = new Object;
Products.Analogs.ProductsTypes = new Array();//Список типов товаров

//Возможные замены
Products.PossibleReplacement = new Object;
Products.PossibleReplacement.Products = new Object;
Products.PossibleReplacement.Products.Manufacturers = new Object;
Products.PossibleReplacement.ProductsTypes = new Array();//Список типов товаров

//Дополнительный свободный блок для доработок
Products.Spare_Box = new Object;
Products.Spare_Box.Products = new Object;
Products.Spare_Box.Products.Manufacturers = new Object;
Products.Spare_Box.ProductsTypes = new Array();//Список типов товаров

//Индексный список для поиска нужного объекта по его клиентскому ID (AID - All ID). Т.е. каждый объект товара при примеме от сервера получает ID в рамках данной страницы.
//Этот список предназначен для получения объекта товара по его AID:
Products.All = new Array();//Список объектов

// Must exist before CHPU bootstrap calls resultReview (declared later in part_search_page.php).
var list_products_info = new Array;

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

var epcCrossAllReferences = window.epcCrossAllReferences || [];
var epcCrossReferencesTotal = window.epcCrossReferencesTotal || 0;
var epcCrossReferencesLoaded = window.epcCrossReferencesLoaded || 0;
var epcCrossStockKeyMap = window.epcCrossStockKeyMap || {};
window.epcCrossAllReferences = epcCrossAllReferences;
window.epcCrossReferencesTotal = epcCrossReferencesTotal;
window.epcCrossReferencesLoaded = epcCrossReferencesLoaded;
window.epcCrossStockKeyMap = epcCrossStockKeyMap;
function epcSyncCrossReferenceGlobals()
{
	if(window.epcCrossAllReferences && window.epcCrossAllReferences.length)
	{
		epcCrossAllReferences = window.epcCrossAllReferences.slice(0);
	}
	if(typeof window.epcCrossReferencesTotal !== 'undefined')
	{
		epcCrossReferencesTotal = window.epcCrossReferencesTotal;
	}
	if(window.epcCrossStockKeyMap && typeof window.epcCrossStockKeyMap === 'object')
	{
		epcCrossStockKeyMap = window.epcCrossStockKeyMap;
	}
	window.epcCrossAllReferences = epcCrossAllReferences;
	window.epcCrossReferencesTotal = epcCrossReferencesTotal;
	window.epcCrossStockKeyMap = epcCrossStockKeyMap;
}
function epcCrossRefKey(brand, article)
{
	return String(brand || '').trim().toUpperCase() + '|' + epcCrossNormalizeArticle(article);
}
function epcCrossNormalizeArticle(value)
{
	return String(value || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
}
function epcCrossNormalizeBrand(value)
{
	return String(value || '').trim().toUpperCase().replace(/"/g, "'");
}
function epcCrossCanonicalBrand(brand)
{
	var normalized = epcCrossNormalizeBrand(brand);
	if(normalized === '')
	{
		return '';
	}
	var canonMap = window.epcManufacturerCanonicalMap || {};
	if(canonMap[normalized])
	{
		return canonMap[normalized];
	}
	return normalized;
}
function epcCrossRebuildBrandByArticleMap()
{
	window.epcCrossBrandByArticleNorm = {};
	var source = (window.epcCrossAllReferences && window.epcCrossAllReferences.length) ? window.epcCrossAllReferences : ((typeof epcCrossAllReferences !== 'undefined' && epcCrossAllReferences.length) ? epcCrossAllReferences : []);
	var stockSource = window.epcLastCrossStock || ((typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock) ? epcPendingCrossStock : []);
	var i;
	function remember(brand, articleValue)
	{
		var norm = epcCrossNormalizeArticle(articleValue);
		var label = String(brand || '').trim();
		if(norm === '' || label === '')
		{
			return;
		}
		if(!window.epcCrossBrandByArticleNorm[norm])
		{
			window.epcCrossBrandByArticleNorm[norm] = label;
		}
	}
	for(i = 0; i < source.length; i++)
	{
		remember(source[i].brand, source[i].article_norm || source[i].article);
	}
	for(i = 0; i < stockSource.length; i++)
	{
		remember(stockSource[i].brand, stockSource[i].article_norm || stockSource[i].article);
	}
}
function epcCrossInferBrandFromArticleNorm(articleNorm)
{
	var norm = epcCrossNormalizeArticle(articleNorm);
	if(norm === '')
	{
		return '';
	}
	if(/^90915/.test(norm))
	{
		return 'TOYOTA';
	}
	if(/^15400/.test(norm))
	{
		return 'HONDA';
	}
	if(/^15208/.test(norm) || /^22040/.test(norm))
	{
		return 'NISSAN';
	}
	if(/^26300/.test(norm) || /^28113/.test(norm))
	{
		return 'HYUNDAI';
	}
	return '';
}
function epcCrossResolveReferenceBrand(ref)
{
	if(!ref)
	{
		return '';
	}
	var norm = epcCrossNormalizeArticle(ref.article_norm || ref.article);
	var inferred = epcCrossInferBrandFromArticleNorm(norm);
	var brand = String(ref.brand || '').trim();
	if(inferred !== '' && (brand === '' || ((ref.source === 'crossbase' || ref.source === 'crossbase_oem') && /^90915/.test(norm))))
	{
		return epcCrossCanonicalBrand(inferred);
	}
	if(brand !== '')
	{
		return epcCrossCanonicalBrand(brand);
	}
	if(norm !== '' && window.epcCrossBrandByArticleNorm && window.epcCrossBrandByArticleNorm[norm])
	{
		return epcCrossCanonicalBrand(window.epcCrossBrandByArticleNorm[norm]);
	}
	return inferred;
}
function epcCrossBrandsEquivalent(left, right)
{
	var leftNorm = epcCrossNormalizeBrand(left);
	var rightNorm = epcCrossNormalizeBrand(right);
	if(leftNorm === rightNorm)
	{
		return true;
	}
	if(leftNorm === '' || rightNorm === '')
	{
		return false;
	}
	var map = window.epcManufacturerSynonymMap || {};
	var leftNames = map[leftNorm];
	if(leftNames && leftNames.length)
	{
		for(var i = 0; i < leftNames.length; i++)
		{
			if(leftNames[i] === rightNorm)
			{
				return true;
			}
		}
	}
	var rightNames = map[rightNorm];
	if(rightNames && rightNames.length)
	{
		for(var j = 0; j < rightNames.length; j++)
		{
			if(rightNames[j] === leftNorm)
			{
				return true;
			}
		}
	}
	return false;
}
function epcCrossReferenceKeySet()
{
	var keys = {};
	var source = (epcCrossAllReferences && epcCrossAllReferences.length) ? epcCrossAllReferences : [];
	if(!source.length && window.epcCrossAllReferences && window.epcCrossAllReferences.length)
	{
		source = window.epcCrossAllReferences;
	}
	for(var i = 0; i < source.length; i++)
	{
		var ref = source[i];
		var norm = epcCrossNormalizeArticle(ref.article_norm || ref.article);
		if(norm !== '')
		{
			keys[epcCrossRefKey(ref.brand, norm)] = true;
		}
	}
	return keys;
}
function epcCrossRefIsKnownReference(brand, articleValue)
{
	var norm = epcCrossNormalizeArticle(articleValue);
	if(norm === '')
	{
		return false;
	}
	var source = (epcCrossAllReferences && epcCrossAllReferences.length) ? epcCrossAllReferences : [];
	if(!source.length && window.epcCrossAllReferences && window.epcCrossAllReferences.length)
	{
		source = window.epcCrossAllReferences;
	}
	for(var i = 0; i < source.length; i++)
	{
		var ref = source[i];
		var refNorm = epcCrossNormalizeArticle(ref.article_norm || ref.article);
		if(refNorm !== norm)
		{
			continue;
		}
		var refBrand = String(ref.brand || '').trim();
		var stockBrand = String(brand || '').trim();
		if(refBrand === '' || stockBrand === '')
		{
			return true;
		}
		if(epcCrossBrandsEquivalent(stockBrand, refBrand))
		{
			return true;
		}
	}
	return false;
}
function epcCrossRegisterReference(brand, article, articleNorm, source)
{
	epcSyncCrossReferenceGlobals();
	var norm = articleNorm || epcCrossNormalizeArticle(article);
	if(norm === '')
	{
		return false;
	}
	var sourceList = epcCrossAllReferences;
	for(var i = 0; i < sourceList.length; i++)
	{
		var existingNorm = epcCrossNormalizeArticle(sourceList[i].article_norm || sourceList[i].article);
		if(existingNorm !== norm)
		{
			continue;
		}
		if(epcCrossBrandsEquivalent(brand, sourceList[i].brand))
		{
			return false;
		}
	}
	epcCrossAllReferences.push({
		brand: String(brand || '').trim(),
		article: String(article || '').trim(),
		article_norm: norm,
		source: source || 'ingest'
	});
	window.epcCrossAllReferences = epcCrossAllReferences;
	return true;
}
function epcCrossMergeFallbackIntoReferences()
{
	if(!epcCrossFallbackRows || !epcCrossFallbackRows.length)
	{
		return;
	}
	for(var i = 0; i < epcCrossFallbackRows.length; i++)
	{
		var row = epcCrossFallbackRows[i];
		epcCrossRegisterReference(row.brand, row.article, null, 'cp_fallback');
	}
}
function epcCrossIsSearchedReference(ref)
{
	if(typeof search_object === 'undefined' || !search_object || !search_object.article)
	{
		return false;
	}
	var searchedNorm = epcCrossNormalizeArticle(search_object.article);
	var refNorm = epcCrossNormalizeArticle(ref.article_norm || ref.article);
	if(refNorm !== searchedNorm)
	{
		return false;
	}
	var urlBrand = '';
	if(search_object.requested_manufacturer)
	{
		urlBrand = String(search_object.requested_manufacturer).trim();
	}
	else if(typeof SelectedManufacturer !== 'undefined' && SelectedManufacturer)
	{
		urlBrand = String(SelectedManufacturer).trim();
	}
	if(urlBrand === '')
	{
		return false;
	}
	return epcCrossBrandsEquivalent(ref.brand, urlBrand);
}
function epcCrossIngestFromLoadedProducts()
{
	if(typeof Products === 'undefined' || typeof search_object === 'undefined' || !search_object)
	{
		return;
	}
	var searchedNorm = epcCrossNormalizeArticle(search_object.article);
	function registerProduct(manufacturer, article)
	{
		var norm = epcCrossNormalizeArticle(article);
		if(norm === '' || norm === searchedNorm)
		{
			return;
		}
		epcCrossRegisterReference(manufacturer, article, norm, 'supplier');
	}
	function scanBucket(bucket)
	{
		if(!bucket || !bucket.ProductsTypes)
		{
			return;
		}
		for(var i = 0; i < bucket.ProductsTypes.length; i++)
		{
			var manufacturer = bucket.ProductsTypes[i].manufacturer;
			var article = bucket.ProductsTypes[i].article;
			if(bucket.Products.Manufacturers[manufacturer] && bucket.Products.Manufacturers[manufacturer][article])
			{
				var items = bucket.Products.Manufacturers[manufacturer][article];
				for(var p = 0; p < items.length; p++)
				{
					registerProduct(items[p].manufacturer || manufacturer, items[p].article || article);
				}
			}
		}
	}
	scanBucket(Products.Quick_Analogs);
	scanBucket(Products.Analogs);
	scanBucket(Products.PossibleReplacement);
	scanBucket(Products.Required);
}
function epcCrossExpandReferenceGraph()
{
	epcCrossMergeFallbackIntoReferences();
	epcCrossIngestFromLoadedProducts();
	epcSyncCrossReferenceGlobals();
}
function epcCrossTryBindPendingStockForNewReferences()
{
	if(typeof epcPendingCrossStock === 'undefined' || !epcPendingCrossStock || !epcPendingCrossStock.length)
	{
		return;
	}
	if(typeof epcBindCrossStockProducts !== 'function')
	{
		return;
	}
	var pending = [];
	for(var i = 0; i < epcPendingCrossStock.length; i++)
	{
		var item = epcPendingCrossStock[i];
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		{
			pending.push(item);
		}
		else if(epcCrossRefIsKnownReference(item.brand, item.article_norm || item.article))
		{
			pending.push(item);
		}
	}
	if(pending.length)
	{
		epcBindCrossStockProducts(pending);
	}
}
function epcChpuPrepareFiltersBeforeReview()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing || typeof filter === 'undefined')
	{
		return;
	}
	window.epcChpuActiveFilterState = null;
	window.epcChpuSkipFilterPreserveOnce = true;
	window.epcChpuSkipFilterDomRead = true;
	window.epcChpuSkipResultFiltersOnce = true;
	var rangeBlocks = ['price_blok', 'time_to_exe_blok', 'exist_blok'];
	for(var r = 0; r < rangeBlocks.length; r++)
	{
		var block = filter[rangeBlocks[r]];
		if(!block)
		{
			continue;
		}
		block.min_need = undefined;
		block.max_need = undefined;
	}
	if(typeof epcChpuUpdateFilterRangesFromProducts === 'function')
	{
		epcChpuUpdateFilterRangesFromProducts();
	}
	if(filter['manufacturer_blok'] && typeof arr_manufacturers !== 'undefined' && arr_manufacturers.length)
	{
		filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers.slice(0);
		if(filter['manufacturer_blok'].list_options)
		{
			for(var m = 0; m < filter['manufacturer_blok'].list_options.length; m++)
			{
				filter['manufacturer_blok'].list_options[m].value = true;
			}
		}
	}
	if(filter['storages_blok'] && typeof arr_storages !== 'undefined' && arr_storages.length)
	{
		filter['storages_blok'].storages_in_filter = arr_storages.slice(0);
	}
}
function epcChpuUpdateFilterRangesFromProducts()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing || typeof filter === 'undefined' || typeof Products === 'undefined' || !Products.All || !Products.All.length)
	{
		return;
	}
	var rangeMap = {
		price_blok: 'price',
		time_to_exe_blok: 'time_to_exe',
		exist_blok: 'exist'
	};
	for(var blockName in rangeMap)
	{
		if(!rangeMap.hasOwnProperty(blockName) || !filter[blockName])
		{
			continue;
		}
		var field = rangeMap[blockName];
		var minVal = null;
		var maxVal = null;
		for(var i = 0; i < Products.All.length; i++)
		{
			var value = parseFloat(Products.All[i][field]);
			if(isNaN(value))
			{
				continue;
			}
			if(minVal === null || value < minVal)
			{
				minVal = value;
			}
			if(maxVal === null || value > maxVal)
			{
				maxVal = value;
			}
		}
		if(minVal === null || maxVal === null)
		{
			continue;
		}
		filter[blockName].min_value = minVal;
		filter[blockName].max_value = maxVal;
		filter[blockName].min_need = minVal;
		filter[blockName].max_need = maxVal;
		filter[blockName].old_min_need = minVal;
		filter[blockName].old_max_need = maxVal;
	}
}
function epcChpuHasRenderableRows()
{
	if(typeof epcPartSearchHasAnyStockRows === 'function' && epcPartSearchHasAnyStockRows())
	{
		return true;
	}
	return (typeof Products !== 'undefined' && Products.All && Products.All.length > 0);
}
function epcChpuBuildDirectStockTableHtml()
{
	if(typeof Products === 'undefined' || !Products.All || !Products.All.length || typeof getProductRecordHTML !== 'function')
	{
		return '';
	}
	var head = window.epcLastProductsHeadBlock;
	if(!head)
	{
		head = '<tr><th class="th_photo" title="Photo"></th><th class="th_manufacturer"><?php echo translate_str_by_id(2070); ?></th><th class="th_article"><?php echo translate_str_by_id(2071); ?></th><th class="th_name"><?php echo translate_str_by_id(2102); ?></th><th class="th_exist"><?php echo translate_str_by_id(4324); ?></th><th class="th_time_to_exe"><?php echo translate_str_by_id(3550); ?></th><th class="th_info"><?php echo translate_str_by_id(4325); ?></th><th class="th_price"><?php echo translate_str_by_id(2751); ?></th><th class="th_add_to_cart">Actions</th><th class="th_color"></th></tr>';
	}
	var rowsHtml = '';
	var rendered = {};
	var stockProducts = [];
	var groupedStock = {};
	var groupedStockOrder = [];
	for(var i = 0; i < Products.All.length; i++)
	{
		var product = (typeof epcGetBoundProductByAid === 'function')
			? epcGetBoundProductByAid(i)
			: null;
		if(!product)
		{
			continue;
		}
		var exist = parseFloat(product.exist);
		if(isNaN(exist) || exist <= 0)
		{
			continue;
		}
		var storageKey = (typeof epcChpuProductArticleKey === 'function')
			? epcChpuProductArticleKey(product.manufacturer, product.article || product.article_show, product.storage_id)
			: (product.manufacturer + '|' + (product.article || product.article_show) + '|' + product.storage_id);
		if(rendered[storageKey])
		{
			continue;
		}
		rendered[storageKey] = true;
		stockProducts.push(product);
		var groupKey = (typeof epcChpuBrandArticleGroupKey === 'function')
			? epcChpuBrandArticleGroupKey(product.manufacturer, product.article || product.article_show)
			: (product.manufacturer + '|' + (product.article || product.article_show));
		var mergedGroupKey = (typeof epcChpuFindEquivalentGroupKey === 'function')
			? epcChpuFindEquivalentGroupKey(groupKey, groupedStock)
			: groupKey;
		if(!groupedStock[mergedGroupKey])
		{
			groupedStock[mergedGroupKey] = [];
			groupedStockOrder.push(mergedGroupKey);
		}
		groupedStock[mergedGroupKey].push(product);
	}
	for(var g = 0; g < groupedStockOrder.length; g++)
	{
		var groupProducts = groupedStock[groupedStockOrder[g]];
		if(!groupProducts || !groupProducts.length)
		{
			continue;
		}
		var leadProduct = groupProducts[0];
		var productType = {
			manufacturer: leadProduct.manufacturer,
			article: leadProduct.article,
			name: leadProduct.name,
			article_show: leadProduct.article_show || leadProduct.article,
			exist: leadProduct.exist,
			time_to_exe: leadProduct.time_to_exe,
			price: leadProduct.price,
			storage: leadProduct.storage_id
		};
		for(var p = 0; p < groupProducts.length; p++)
		{
			rowsHtml += epcChpuCrossStockRowHtml(groupProducts[p], productType, 'quick_analogs', p, groupProducts.length);
		}
	}
	if(rowsHtml === '')
	{
		return '';
	}
	if(typeof ALL_ProductsObjects !== 'undefined' && ALL_ProductsObjects.length === 0)
	{
		ALL_ProductsObjects.push(stockProducts);
	}
	rowsHtml = epcCrossInStockCaptionHTML('Cross references in stock (available in UAE warehouses)') + rowsHtml;
	return '<table id="all_table_products"><thead>' + head + '</thead><tbody>' + rowsHtml + '</tbody></table>';
}
function epcChpuProductsFromStockItems(stockItems)
{
	var products = [];
	if(!stockItems || !stockItems.length)
	{
		return products;
	}
	for(var i = 0; i < stockItems.length; i++)
	{
		var item = stockItems[i];
		var storageId = parseInt(item.storage_id, 10) || 6;
		var price = parseFloat(item.price);
		if(isNaN(price))
		{
			price = 0;
		}
		var exist = parseFloat(item.qty);
		if(isNaN(exist) || exist <= 0)
		{
			exist = parseFloat(item.exist);
		}
		if(isNaN(exist))
		{
			exist = 0;
		}
		if(exist <= 0)
		{
			continue;
		}
		var timeToExe = parseFloat(item.delivery);
		if(isNaN(timeToExe))
		{
			timeToExe = 0;
		}
		var rawBrand = String(item.brand || item.manufacturer || '').trim();
		var displayBrand = rawBrand;
		if(rawBrand !== '' && typeof epcCrossCanonicalBrand === 'function')
		{
			var canonBrand = epcCrossCanonicalBrand(rawBrand);
			if(canonBrand !== '')
			{
				displayBrand = canonBrand;
			}
		}
		var warehouseCaption = String(item.warehouse || '');
		if(warehouseCaption === '' && typeof all_storages_info !== 'undefined' && all_storages_info[storageId])
		{
			warehouseCaption = String(all_storages_info[storageId].name || '');
		}
		if(warehouseCaption === '' && typeof all_storages !== 'undefined' && all_storages[storageId])
		{
			warehouseCaption = String(all_storages[storageId] || '');
		}
		products.push({
			manufacturer: displayBrand,
			article: String(item.article || ''),
			article_show: String(item.article || ''),
			name: String(item.name || ''),
			exist: exist,
			price: price,
			time_to_exe: timeToExe,
			time_to_exe_guaranteed: 0,
			storage: '',
			min_order: 1,
			probability: 0,
			office_id: 1,
			office_caption: '',
			storage_id: storageId,
			color: '#ffffff',
			storage_caption: warehouseCaption,
			price_purchase: String(price),
			markup: 0,
			product_type: 2,
			product_id: 0,
			storage_record_id: 0,
			product_url: null,
			json_params: '',
			valid: true,
			check_hash: '',
			search_name: 0,
			url: ''
		});
	}
	return products;
}
function epcChpuComposeStockTableHtml(stockProducts)
{
	if(!stockProducts || !stockProducts.length || typeof getProductRecordHTML !== 'function')
	{
		return '';
	}
	var head = window.epcLastProductsHeadBlock;
	if(!head)
	{
		head = '<tr><th class="th_photo" title="Photo"></th><th class="th_manufacturer"><?php echo translate_str_by_id(2070); ?></th><th class="th_article"><?php echo translate_str_by_id(2071); ?></th><th class="th_name"><?php echo translate_str_by_id(2102); ?></th><th class="th_exist"><?php echo translate_str_by_id(4324); ?></th><th class="th_time_to_exe"><?php echo translate_str_by_id(3550); ?></th><th class="th_info"><?php echo translate_str_by_id(4325); ?></th><th class="th_price"><?php echo translate_str_by_id(2751); ?></th><th class="th_add_to_cart">Actions</th><th class="th_color"></th></tr>';
	}
	var rowsHtml = '';
	var groupedStock = {};
	var groupedStockOrder = [];
	for(var p = 0; p < stockProducts.length; p++)
	{
		var product = stockProducts[p];
		if((product.aid === undefined || product.aid === null) && typeof epcFindBoundProduct === 'function')
		{
			var matchedProduct = epcFindBoundProduct(product.manufacturer, product.article || product.article_show, product.storage_id);
			if(matchedProduct)
			{
				product = matchedProduct;
			}
		}
		var groupKey = (typeof epcChpuBrandArticleGroupKey === 'function')
			? epcChpuBrandArticleGroupKey(product.manufacturer, product.article || product.article_show)
			: (product.manufacturer + '|' + (product.article || product.article_show));
		var mergedGroupKey = (typeof epcChpuFindEquivalentGroupKey === 'function')
			? epcChpuFindEquivalentGroupKey(groupKey, groupedStock)
			: groupKey;
		if(!groupedStock[mergedGroupKey])
		{
			groupedStock[mergedGroupKey] = [];
			groupedStockOrder.push(mergedGroupKey);
		}
		groupedStock[mergedGroupKey].push(product);
	}
	for(var g = 0; g < groupedStockOrder.length; g++)
	{
		var groupProducts = groupedStock[groupedStockOrder[g]];
		if(!groupProducts || !groupProducts.length)
		{
			continue;
		}
		var leadProduct = groupProducts[0];
		var productType = {
			manufacturer: leadProduct.manufacturer,
			article: leadProduct.article,
			name: leadProduct.name,
			article_show: leadProduct.article_show || leadProduct.article,
			exist: leadProduct.exist,
			time_to_exe: leadProduct.time_to_exe,
			price: leadProduct.price,
			storage: leadProduct.storage_id
		};
		for(var gp = 0; gp < groupProducts.length; gp++)
		{
			rowsHtml += epcChpuCrossStockRowHtml(groupProducts[gp], productType, 'quick_analogs', gp, groupProducts.length);
		}
	}
	if(rowsHtml === '')
	{
		return '';
	}
	return epcCrossInStockCaptionHTML('Cross references in stock (available in UAE warehouses)') + rowsHtml;
}
function epcChpuCrossStockRowHtml(product, productType, blok, rowIndex, rowCount)
{
	if(!product || !productType || typeof getProductRecordHTML !== 'function')
	{
		return '';
	}
	if(typeof rowIndex === 'undefined')
	{
		rowIndex = 0;
	}
	if(typeof rowCount === 'undefined')
	{
		rowCount = 1;
	}
	if(!productType.manufacturer && product.manufacturer)
	{
		productType.manufacturer = product.manufacturer;
	}
	if(!productType.article && product.article)
	{
		productType.article = product.article;
	}
	if(!productType.article_show && (product.article_show || product.article))
	{
		productType.article_show = product.article_show || product.article;
	}
	if(!productType.name && product.name)
	{
		productType.name = product.name;
	}
	var rowProduct = product;
	if((rowProduct.aid === undefined || rowProduct.aid === null) && typeof epcFindBoundProduct === 'function')
	{
		var boundProduct = epcFindBoundProduct(product.manufacturer, product.article || product.article_show, product.storage_id);
		if(boundProduct)
		{
			rowProduct = boundProduct;
		}
	}
	return getProductRecordHTML(rowProduct, rowIndex, rowCount, productType, blok || 'quick_analogs');
}
function epcChpuBuildDirectStockTableFromCrossStock(stockItems)
{
	if(stockItems && stockItems.length && typeof epcBindCrossStockProducts === 'function')
	{
		epcBindCrossStockProducts(stockItems);
	}
	if(typeof epcChpuBuildDirectStockTableHtml === 'function')
	{
		var boundTableHtml = epcChpuBuildDirectStockTableHtml();
		if(boundTableHtml !== '')
		{
			return boundTableHtml;
		}
	}
	var stockProducts = epcChpuProductsFromStockItems(stockItems);
	var rowsHtml = epcChpuComposeStockTableHtml(stockProducts);
	if(rowsHtml === '')
	{
		return '';
	}
	var head = window.epcLastProductsHeadBlock;
	if(!head)
	{
		head = '<tr><th class="th_photo" title="Photo"></th><th class="th_manufacturer"><?php echo translate_str_by_id(2070); ?></th><th class="th_article"><?php echo translate_str_by_id(2071); ?></th><th class="th_name"><?php echo translate_str_by_id(2102); ?></th><th class="th_exist"><?php echo translate_str_by_id(4324); ?></th><th class="th_time_to_exe"><?php echo translate_str_by_id(3550); ?></th><th class="th_info"><?php echo translate_str_by_id(4325); ?></th><th class="th_price"><?php echo translate_str_by_id(2751); ?></th><th class="th_add_to_cart">Actions</th><th class="th_color"></th></tr>';
	}
	return '<table id="all_table_products"><thead>' + head + '</thead><tbody>' + rowsHtml + '</tbody></table>';
}
function epcChpuAppendCrossNotInStockToTable()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing || typeof epcCrossRefsNotInStockRowsHTML !== 'function')
	{
		return false;
	}
	if(typeof epcSyncCrossReferenceGlobals === 'function')
	{
		epcSyncCrossReferenceGlobals();
	}
	if(typeof epcCrossExpandReferenceGraph === 'function')
	{
		epcCrossExpandReferenceGraph();
	}
	var missingRows = epcCrossRefsNotInStockRowsHTML();
	if(missingRows === '')
	{
		return false;
	}
	var tbody = document.querySelector('#all_table_products tbody');
	if(tbody)
	{
		if(tbody.innerHTML.indexOf('epc-cross-not-found-caption') !== -1)
		{
			return true;
		}
		tbody.insertAdjacentHTML('beforeend', missingRows);
		if(typeof epcBindSearchRowPhotoLoaders === 'function')
		{
			epcBindSearchRowPhotoLoaders(tbody);
		}
		return true;
	}
	var productsArea = document.getElementById('products_area');
	if(productsArea)
	{
		productsArea.insertAdjacentHTML('beforeend', '<table class="epc-cross-missing-table"><tbody>' + missingRows + '</tbody></table>');
		return true;
	}
	return false;
}
function epcChpuSyncCrossDataBeforeRender(stockItems)
{
	if(stockItems && stockItems.length && typeof epcMarkCrossStockKeys === 'function')
	{
		epcMarkCrossStockKeys(stockItems);
	}
	if(typeof epcSyncCrossReferenceGlobals === 'function')
	{
		epcSyncCrossReferenceGlobals();
	}
	if(typeof epcCrossExpandReferenceGraph === 'function')
	{
		epcCrossExpandReferenceGraph();
	}
	if(typeof epcSyncChpuFilterManufacturers === 'function')
	{
		epcSyncChpuFilterManufacturers();
	}
}
function epcChpuEnsureCrossStockTableVisible(stockItems, showUnavailableNotice)
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return false;
	}
	var items = stockItems;
	if(!items || !items.length)
	{
		items = window.epcLastCrossStock;
	}
	if(!items || !items.length)
	{
		return false;
	}
	window.epcLastCrossStock = items.slice(0);
	var productsArea = document.getElementById('products_area');
	if(!productsArea)
	{
		return false;
	}
	// Only keep an existing table when it already has real stock rows.
	// Caption-only / "not found" tables must be rebuilt (otherwise status bar
	// can show "2 IN STOCK" while the products area stays empty).
	var areaHasStock = (typeof epcChpuAreaHasStockPaint === 'function')
		? epcChpuAreaHasStockPaint(productsArea)
		: (productsArea.innerHTML.indexOf('td_exist') !== -1 || productsArea.innerHTML.indexOf('epc-btn-cart') !== -1);
	if(productsArea.innerHTML.indexOf('all_table_products') !== -1 && areaHasStock)
	{
		epcChpuSyncCrossDataBeforeRender(items);
		epcChpuAppendCrossNotInStockToTable();
		if(typeof epcChpuUpdateFilterRangesFromProducts === 'function')
		{
			epcChpuUpdateFilterRangesFromProducts();
		}
		if(typeof showPropertiesWidgets === 'function')
		{
			try
			{
				showPropertiesWidgets();
			}
			catch(filterRefreshErr) {}
		}
		return true;
	}
	epcChpuSyncCrossDataBeforeRender(items);
	window.epcChpuSkipFilterDomRead = true;
	window.epcChpuSkipResultFiltersOnce = true;
	if(typeof epcBindCrossStockProducts === 'function')
	{
		try
		{
			epcBindCrossStockProducts(items);
		}
		catch(bindErr)
		{
			console.error('epcChpuEnsureCrossStockTableVisible bind', bindErr);
		}
	}
	if(typeof epcChpuPrepareFiltersBeforeReview === 'function')
	{
		epcChpuPrepareFiltersBeforeReview();
	}
	var tableHtml = '';
	if(typeof epcChpuBuildDirectStockTableHtml === 'function')
	{
		tableHtml = epcChpuBuildDirectStockTableHtml();
	}
	if(tableHtml === '' && typeof epcChpuBuildDirectStockTableFromCrossStock === 'function')
	{
		tableHtml = epcChpuBuildDirectStockTableFromCrossStock(items);
	}
	if(tableHtml === '')
	{
		return false;
	}
	var html = '';
	if(showUnavailableNotice === true && typeof epcPartSearchNotAvailableHTML === 'function')
	{
		var unavailableHtml = epcPartSearchNotAvailableHTML();
		if(unavailableHtml !== '')
		{
			html += unavailableHtml;
		}
	}
	html += tableHtml;
	productsArea.innerHTML = html;
	epcChpuAppendCrossNotInStockToTable();
	if(typeof epcBindSearchRowPhotoLoaders === 'function')
	{
		epcBindSearchRowPhotoLoaders(productsArea);
	}
	if(typeof ALL_ProductsObjects !== 'undefined')
	{
		var stockProducts = epcChpuProductsFromStockItems(items);
		if(stockProducts.length && ALL_ProductsObjects.length === 0)
		{
			ALL_ProductsObjects.push(stockProducts);
		}
	}
	if(typeof epcChpuHideProcessingIndicator === 'function')
	{
		epcChpuHideProcessingIndicator();
	}
	else
	{
		var processing = document.getElementById('processing_indicator');
		if(processing)
		{
			processing.innerHTML = '';
			processing.style.display = 'none';
		}
	}
	if(typeof epcChpuUpdateFilterRangesFromProducts === 'function')
	{
		epcChpuUpdateFilterRangesFromProducts();
	}
	if(typeof showPropertiesWidgets === 'function')
	{
		try
		{
			showPropertiesWidgets();
		}
		catch(filterErr)
		{
			console.error('epcChpuEnsureCrossStockTableVisible filters', filterErr);
		}
	}
	window.epcChpuSkipFilterDomRead = false;
	window.epcChpuSkipResultFiltersOnce = false;
	return true;
}
function epcChpuSafeResultReview()
{
	if(typeof resultReview !== 'function')
	{
		return false;
	}
	try
	{
		resultReview();
		return true;
	}
	catch(reviewErr)
	{
		console.error('epcChpuSafeResultReview', reviewErr);
		return false;
	}
}
function epcCrossStockItemHasQty(item)
{
	if(!item)
	{
		return false;
	}
	var qty = parseFloat(item.qty);
	if(isNaN(qty) || qty <= 0)
	{
		qty = parseFloat(item.exist);
	}
	return !isNaN(qty) && qty > 0;
}
function epcCrossMarkStockKeyForBrandArticle(brand, articleNorm)
{
	if(articleNorm === '')
	{
		return;
	}
	var brandName = String(brand || '').trim();
	if(brandName !== '')
	{
		epcCrossStockKeyMap[epcCrossRefKey(brandName, articleNorm)] = true;
		var canonBrand = epcCrossCanonicalBrand(brandName);
		if(canonBrand !== '' && canonBrand !== brandName)
		{
			epcCrossStockKeyMap[epcCrossRefKey(canonBrand, articleNorm)] = true;
		}
	}
	else
	{
		epcCrossStockKeyMap['|' + articleNorm] = true;
	}
}
function epcCrossMarkStockForReferences(stockBrand, articleNorm)
{
	var source = [];
	if(window.epcCrossAllReferences && window.epcCrossAllReferences.length)
	{
		source = window.epcCrossAllReferences;
	}
	else if(epcCrossAllReferences && epcCrossAllReferences.length)
	{
		source = epcCrossAllReferences;
	}
	for(var i = 0; i < source.length; i++)
	{
		var ref = source[i];
		var refNorm = epcCrossNormalizeArticle(ref.article_norm || ref.article);
		if(refNorm !== articleNorm)
		{
			continue;
		}
		var refBrand = (typeof epcCrossResolveReferenceBrand === 'function')
			? epcCrossResolveReferenceBrand(ref)
			: (ref.brand || '');
		if(stockBrand !== '' && refBrand !== '' && !epcCrossBrandsEquivalent(stockBrand, refBrand))
		{
			continue;
		}
		epcCrossMarkStockKeyForBrandArticle(refBrand || stockBrand, refNorm);
	}
	window.epcCrossStockKeyMap = epcCrossStockKeyMap;
}
function epcMarkCrossStockKeys(stockItems)
{
	if(!stockItems || !stockItems.length)
	{
		return;
	}
	if(!epcCrossStockKeyMap || typeof epcCrossStockKeyMap !== 'object')
	{
		epcCrossStockKeyMap = {};
	}
	for(var i = 0; i < stockItems.length; i++)
	{
		var item = stockItems[i];
		if(!epcCrossStockItemHasQty(item))
		{
			continue;
		}
		var norm = item.article_norm || epcCrossNormalizeArticle(item.article);
		if(norm === '')
		{
			continue;
		}
		var stockBrand = String(item.brand || item.manufacturer || '').trim();
		epcCrossMarkStockKeyForBrandArticle(stockBrand, norm);
		epcCrossMarkStockForReferences(stockBrand, norm);
	}
	window.epcCrossStockKeyMap = epcCrossStockKeyMap;
}
function epcRebuildCrossInStockMaps()
{
	function markProduct(product)
	{
		if(!product)
		{
			return;
		}
		var exist = parseFloat(product.exist);
		if(isNaN(exist) || exist <= 0)
		{
			return;
		}
		var norm = epcCrossNormalizeArticle(product.article || product.article_show);
		if(norm === '')
		{
			return;
		}
		epcCrossMarkStockKeyForBrandArticle(product.manufacturer, norm);
		epcCrossMarkStockForReferences(product.manufacturer, norm);
	}
	function scanBucket(bucket)
	{
		if(!bucket || !bucket.ProductsTypes)
		{
			return;
		}
		for(var i = 0; i < bucket.ProductsTypes.length; i++)
		{
			var manufacturer = bucket.ProductsTypes[i].manufacturer;
			var article = bucket.ProductsTypes[i].article;
			if(bucket.Products.Manufacturers[manufacturer] && bucket.Products.Manufacturers[manufacturer][article])
			{
				var items = bucket.Products.Manufacturers[manufacturer][article];
				for(var p = 0; p < items.length; p++)
				{
					markProduct(items[p]);
				}
			}
		}
	}
	if(typeof Products !== 'undefined' && Products.All && Products.All.length)
	{
		for(var a = 0; a < Products.All.length; a++)
		{
			markProduct(Products.All[a]);
		}
	}
	if(typeof Products !== 'undefined')
	{
		scanBucket(Products.Required);
		scanBucket(Products.Quick_Analogs);
		scanBucket(Products.Analogs);
		scanBucket(Products.PossibleReplacement);
		scanBucket(Products.SearchName);
		scanBucket(Products.Spare_Box);
	}
	var stockPools = [];
	if(window.epcLastCrossStock && window.epcLastCrossStock.length)
	{
		stockPools.push(window.epcLastCrossStock);
	}
	if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
	{
		stockPools.push(epcPendingCrossStock);
	}
	for(var s = 0; s < stockPools.length; s++)
	{
		epcMarkCrossStockKeys(stockPools[s]);
	}
	window.epcCrossStockKeyMap = epcCrossStockKeyMap;
}
function epcCrossRefHasStockInLiveData(brand, norm)
{
	var brandName = String(brand || '').trim();
	var pools = [];
	if(window.epcLastCrossStock && window.epcLastCrossStock.length)
	{
		pools.push(window.epcLastCrossStock);
	}
	if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
	{
		pools.push(epcPendingCrossStock);
	}
	for(var p = 0; p < pools.length; p++)
	{
		for(var i = 0; i < pools[p].length; i++)
		{
			var item = pools[p][i];
			if(!epcCrossStockItemHasQty(item))
			{
				continue;
			}
			var itemNorm = item.article_norm || epcCrossNormalizeArticle(item.article);
			if(itemNorm !== norm)
			{
				continue;
			}
			if(brandName === '' || epcCrossBrandsEquivalent(brandName, item.brand || item.manufacturer))
			{
				return true;
			}
		}
	}
	if(typeof Products !== 'undefined' && Products.All && Products.All.length)
	{
		for(var j = 0; j < Products.All.length; j++)
		{
			var product = Products.All[j];
			if(!product)
			{
				continue;
			}
			var exist = parseFloat(product.exist);
			if(isNaN(exist) || exist <= 0)
			{
				continue;
			}
			var productNorm = epcCrossNormalizeArticle(product.article || product.article_show);
			if(productNorm !== norm)
			{
				continue;
			}
			if(brandName === '' || epcCrossBrandsEquivalent(brandName, product.manufacturer))
			{
				return true;
			}
		}
	}
	return false;
}
function epcSyncCrossStockKeysLight()
{
	if(!epcCrossStockKeyMap || typeof epcCrossStockKeyMap !== 'object')
	{
		epcCrossStockKeyMap = {};
	}
	if(window.epcLastCrossStock && window.epcLastCrossStock.length)
	{
		epcMarkCrossStockKeys(window.epcLastCrossStock);
	}
	else if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
	{
		epcMarkCrossStockKeys(epcPendingCrossStock);
	}
	window.epcCrossStockKeyMap = epcCrossStockKeyMap;
}
function epcCrossRefStockQty(brand, articleValue)
{
	var norm = epcCrossNormalizeArticle(articleValue);
	if(norm === '')
	{
		return 0;
	}
	var brandName = String(brand || '').trim();
	var bestQty = 0;
	var pools = [];
	if(window.epcLastCrossStock && window.epcLastCrossStock.length)
	{
		pools.push(window.epcLastCrossStock);
	}
	if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
	{
		pools.push(epcPendingCrossStock);
	}
	for(var p = 0; p < pools.length; p++)
	{
		for(var i = 0; i < pools[p].length; i++)
		{
			var item = pools[p][i];
			if(!epcCrossStockItemHasQty(item))
			{
				continue;
			}
			var itemNorm = item.article_norm || epcCrossNormalizeArticle(item.article);
			if(itemNorm !== norm)
			{
				continue;
			}
			if(brandName !== '' && !epcCrossBrandsEquivalent(brandName, item.brand || item.manufacturer))
			{
				continue;
			}
			var qty = parseFloat(item.qty);
			if(isNaN(qty) || qty <= 0)
			{
				qty = parseFloat(item.exist);
			}
			if(!isNaN(qty) && qty > bestQty)
			{
				bestQty = qty;
			}
		}
	}
	if(typeof Products !== 'undefined' && Products.All && Products.All.length)
	{
		for(var j = 0; j < Products.All.length; j++)
		{
			var product = Products.All[j];
			if(!product)
			{
				continue;
			}
			var exist = parseFloat(product.exist);
			if(isNaN(exist) || exist <= 0)
			{
				continue;
			}
			var productNorm = epcCrossNormalizeArticle(product.article || product.article_show);
			if(productNorm !== norm)
			{
				continue;
			}
			if(brandName !== '' && !epcCrossBrandsEquivalent(brandName, product.manufacturer))
			{
				continue;
			}
			if(exist > bestQty)
			{
				bestQty = exist;
			}
		}
	}
	return bestQty;
}
function epcCrossRefFormatDisplayName(brand, article, description)
{
	var brandLabel = String(brand || '').trim();
	var articleLabel = String(article || '').trim();
	var desc = String(description || '').trim();
	var base = (brandLabel !== '' && articleLabel !== '') ? (brandLabel + ' ' + articleLabel) : (articleLabel || brandLabel);
	if(base === '')
	{
		return '';
	}
	if(desc === '')
	{
		return base;
	}
	var articleNorm = epcCrossNormalizeArticle(articleLabel);
	var descNorm = epcCrossNormalizeArticle(desc);
	if(descNorm !== '' && articleNorm !== '' && descNorm === articleNorm)
	{
		return base;
	}
	var descUp = desc.toUpperCase();
	var brandUp = brandLabel.toUpperCase();
	if(brandUp !== '' && descUp.indexOf(brandUp) === 0)
	{
		return desc;
	}
	if(articleNorm !== '' && descUp.indexOf(articleNorm) >= 0 && brandUp !== '' && descUp.indexOf(brandUp) >= 0)
	{
		return desc;
	}
	return base + ' — ' + desc;
}
function epcCrossRefResolveName(brand, articleValue, row)
{
	var brandName = String(brand || '').trim();
	var articleLabel = (row && row.article) ? String(row.article).trim() : String(articleValue || '').trim();
	if(row && String(row.name || '').trim() !== '')
	{
		return epcCrossRefFormatDisplayName(brandName, articleLabel, row.name);
	}
	var norm = epcCrossNormalizeArticle(articleValue);
	if(norm === '')
	{
		return epcCrossRefFormatDisplayName(brandName, articleLabel, '');
	}
	var pools = [];
	if(window.epcLastCrossStock && window.epcLastCrossStock.length)
	{
		pools.push(window.epcLastCrossStock);
	}
	if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
	{
		pools.push(epcPendingCrossStock);
	}
	for(var p = 0; p < pools.length; p++)
	{
		for(var i = 0; i < pools[p].length; i++)
		{
			var item = pools[p][i];
			var itemNorm = item.article_norm || epcCrossNormalizeArticle(item.article);
			if(itemNorm !== norm)
			{
				continue;
			}
			if(brandName !== '' && !epcCrossBrandsEquivalent(brandName, item.brand || item.manufacturer))
			{
				continue;
			}
			var itemName = String(item.name || '').trim();
			if(itemName !== '')
			{
				return epcCrossRefFormatDisplayName(brandName, articleLabel, itemName);
			}
		}
	}
	if(typeof Products !== 'undefined' && Products.All && Products.All.length)
	{
		for(var j = 0; j < Products.All.length; j++)
		{
			var product = epcGetBoundProductByAid(j);
			if(!product)
			{
				continue;
			}
			var productNorm = epcCrossNormalizeArticle(product.article || product.article_show);
			if(productNorm !== norm)
			{
				continue;
			}
			if(brandName !== '' && !epcCrossBrandsEquivalent(brandName, product.manufacturer))
			{
				continue;
			}
			var productName = String(product.name || '').trim();
			if(productName !== '')
			{
				return epcCrossRefFormatDisplayName(brandName, articleLabel, productName);
			}
		}
	}
	return epcCrossRefFormatDisplayName(brandName, articleLabel, '');
}
function epcCrossRefIsInProductsTable(brand, articleValue)
{
	if(typeof Products === 'undefined' || !Products.All || !Products.All.length)
	{
		return false;
	}
	var norm = epcCrossNormalizeArticle(articleValue);
	if(norm === '')
	{
		return false;
	}
	var brandName = String(brand || '').trim();
	for(var j = 0; j < Products.All.length; j++)
	{
		var product = Products.All[j];
		if(!product)
		{
			continue;
		}
		var exist = parseFloat(product.exist);
		if(isNaN(exist) || exist <= 0)
		{
			continue;
		}
		var productNorm = epcCrossNormalizeArticle(product.article || product.article_show);
		if(productNorm !== norm)
		{
			continue;
		}
		if(brandName === '' || epcCrossBrandsEquivalent(brandName, product.manufacturer))
		{
			return true;
		}
	}
	return false;
}
function epcCrossRefIsInStock(brand, articleValue)
{
	var norm = epcCrossNormalizeArticle(articleValue);
	if(norm === '')
	{
		return false;
	}
	if(epcCrossRefHasStockInLiveData(brand, norm))
	{
		return true;
	}
	// Exact manufacturer+article or same synonym group (CP manufacturers_synonyms) with stock in table.
	if(!!epcCrossStockKeyMap[epcCrossRefKey(brand, norm)])
	{
		return true;
	}
	if(!!epcCrossStockKeyMap['|' + norm])
	{
		return true;
	}
	for(var key in epcCrossStockKeyMap)
	{
		if(!epcCrossStockKeyMap[key])
		{
			continue;
		}
		var sep = key.indexOf('|');
		if(sep < 0)
		{
			continue;
		}
		var keyBrand = key.substring(0, sep);
		var keyNorm = key.substring(sep + 1);
		if(keyNorm !== norm)
		{
			continue;
		}
		if(epcCrossBrandsEquivalent(brand, keyBrand))
		{
			return true;
		}
	}
	return false;
}
function epcFormatCrossCountLabel(loaded, total, stockCount)
{
	loaded = parseInt(loaded, 10) || 0;
	total = parseInt(total, 10) || loaded;
	stockCount = parseInt(stockCount, 10) || 0;
	var text = '';
	if(total > loaded && loaded > 0)
	{
		text = loaded + ' shown of ' + total.toLocaleString() + ' crosses';
	}
	else if(total > 0)
	{
		text = total.toLocaleString() + ' crosses';
	}
	else
	{
		text = loaded + ' crosses';
	}
	if(stockCount > 0)
	{
		text += ' · ' + stockCount + ' in stock';
	}
	return text;
}
function epcCrossAvailabilityBadgeHTML(inStock, qty)
{
	if(typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
	{
		return epcStorefrontSensitiveMask();
	}
	if(inStock)
	{
		var q = parseFloat(qty);
		var label = (!isNaN(q) && q > 0) ? ('In stock · ' + q) : 'In stock';
		return '<span class="epc-avail-badge epc-avail-badge--yes">' + epcCrossEsc(label) + '</span>';
	}
	return '<span class="epc-avail-badge epc-avail-badge--no">Not in stock</span>';
}
function epcCrossInStockCaptionHTML(caption)
{
	return '<tr><td colspan="10" class="products_table_block_caption epc-cross-in-stock-caption">' + epcCrossEsc(caption) + '</td></tr>';
}
function epcCrossNotFoundCaptionHTML(caption)
{
	return '<tr><td colspan="10" class="products_table_block_caption epc-cross-not-found-caption">' + epcCrossEsc(caption) + '</td></tr>';
}
function epcCrossTableCaptionHTML(caption)
{
	return epcCrossInStockCaptionHTML(caption);
}
function epcPartTypeBrandKeys()
{
	if(!window.epcGenuinePartTypeIndex || !epcGenuinePartTypeIndex.brands)
	{
		return {};
	}
	return epcGenuinePartTypeIndex.brands;
}
function epcIsGenuineManufacturer(brand)
{
	var norm = epcCrossNormalizeBrand(brand);
	if(norm === '')
	{
		return false;
	}
	var keys = epcPartTypeBrandKeys();
	if(keys[norm])
	{
		return true;
	}
	var canon = epcCrossCanonicalBrand(brand);
	if(canon !== '' && keys[canon])
	{
		return true;
	}
	for(var genuineKey in keys)
	{
		if(keys.hasOwnProperty(genuineKey) && epcCrossBrandsEquivalent(brand, genuineKey))
		{
			return true;
		}
	}
	return false;
}
function epcPartTypeCreateSplit()
{
	return { genuine: [], aftermarket: [] };
}
function epcPartTypeExtractRowPrice(rowHtml, sortPrice)
{
	var priceNum = parseFloat(sortPrice);
	if(!isNaN(priceNum) && priceNum > 0)
	{
		return priceNum;
	}
	if(!rowHtml)
	{
		return 999999999;
	}
	var match = rowHtml.match(/class="td_price"[^>]*>([\s\S]*?)<\/td>/i);
	if(match)
	{
		priceNum = parseFloat(String(match[1]).replace(/[^\d.]/g, ''));
		if(!isNaN(priceNum) && priceNum > 0)
		{
			return priceNum;
		}
	}
	return 999999999;
}
function epcPartTypeAppendRow(split, rowHtml, brand, sortPrice)
{
	if(!split || !rowHtml)
	{
		return;
	}
	var rowKind = epcIsGenuineManufacturer(brand) ? 'genuine' : 'aftermarket';
	var rowClass = 'epc-part-type-row epc-part-type-row--' + rowKind;
	var tintedRow = rowHtml;
	if(tintedRow.indexOf('class="') !== -1)
	{
		tintedRow = tintedRow.replace('<tr ', '<tr class="' + rowClass + '" ');
	}
	else
	{
		tintedRow = tintedRow.replace('<tr', '<tr class="' + rowClass + '"');
	}
	var entry = { html: tintedRow, price: epcPartTypeExtractRowPrice(rowHtml, sortPrice) };
	if(rowKind === 'genuine')
	{
		split.genuine.push(entry);
	}
	else
	{
		split.aftermarket.push(entry);
	}
}
function epcPartTypeCountRows(rows)
{
	return Array.isArray(rows) ? rows.length : 0;
}
function epcPartTypeJoinSortedRows(rows)
{
	if(!Array.isArray(rows) || !rows.length)
	{
		return '';
	}
	rows.sort(function(left, right) {
		return left.price - right.price;
	});
	return rows.map(function(row) { return row.html; }).join('');
}
function epcPartTypeSplitHeadingHTML(kind, rowCount)
{
	var isGenuine = (kind === 'genuine');
	var title = isGenuine ? 'Genuine (OE)' : 'Aftermarket';
	var badge = isGenuine
		? '<span class="epc-part-type-badge epc-part-type-badge--genuine">OE</span>'
		: '<span class="epc-part-type-badge epc-part-type-badge--am">AM</span>';
	var countLabel = rowCount > 0 ? (' <span class="epc-part-type-count">(' + rowCount + ')</span>') : '';
	return '<tr><td colspan="10" class="epc-part-type-split epc-part-type-split--' + kind + '">'
		+ '<div class="epc-part-type-split__inner">'
		+ '<div class="epc-part-type-split__head">'
		+ badge
		+ '<strong class="epc-part-type-split__title">' + title + countLabel + '</strong>'
		+ '</div>'
		+ '</div></td></tr>';
}
function epcPartTypeComposeRows(split)
{
	var html = '';
	if(!split)
	{
		return '';
	}
	if(split.genuine && split.genuine.length)
	{
		html += epcPartTypeSplitHeadingHTML('genuine', epcPartTypeCountRows(split.genuine));
		html += epcPartTypeJoinSortedRows(split.genuine);
	}
	if(split.aftermarket && split.aftermarket.length)
	{
		html += epcPartTypeSplitHeadingHTML('aftermarket', epcPartTypeCountRows(split.aftermarket));
		html += epcPartTypeJoinSortedRows(split.aftermarket);
	}
	return html;
}
function epcPartTypeHasRows(split)
{
	return !!(split && ((split.genuine && split.genuine.length) || (split.aftermarket && split.aftermarket.length)));
}
function epcPartTypeMergeSplits(left, right)
{
	return {
		genuine: [].concat(left && left.genuine ? left.genuine : [], right && right.genuine ? right.genuine : []),
		aftermarket: [].concat(left && left.aftermarket ? left.aftermarket : [], right && right.aftermarket ? right.aftermarket : [])
	};
}
function epcRequiredNotFoundCaptionHTML()
{
	return '<tr><td colspan="10" class="products_table_block_caption epc-required-not-found-caption"><strong><?php echo translate_str_by_id(4327); ?></strong></td></tr>';
}
function epcChpuGetRequestedBrandArticle()
{
	var article = '';
	var brand = '';
	if(typeof search_object !== 'undefined' && search_object)
	{
		article = String(search_object.article || '').trim();
		brand = String(search_object.requested_manufacturer || search_object.manufacturer || '').trim();
	}
	if(brand === '' && typeof SelectedManufacturer !== 'undefined' && SelectedManufacturer)
	{
		brand = String(SelectedManufacturer).trim();
	}
	return {brand: brand, article: article};
}
function epcChpuRequestedPartHasStock()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return false;
	}
	if(typeof epcChpuAnchorHasDbStock !== 'undefined' && epcChpuAnchorHasDbStock)
	{
		return true;
	}
	var req = epcChpuGetRequestedBrandArticle();
	if(req.article === '')
	{
		return false;
	}
	function productMatchesRequest(product)
	{
		if(!product)
		{
			return false;
		}
		var exist = parseFloat(product.exist);
		if(isNaN(exist) || exist <= 0)
		{
			return false;
		}
		if(typeof epcSameArticle === 'function')
		{
			if(!epcSameArticle(product.article || product.article_show, req.article))
			{
				return false;
			}
		}
		else if(epcCrossNormalizeArticle(product.article || product.article_show) !== epcCrossNormalizeArticle(req.article))
		{
			return false;
		}
		if(req.brand !== '')
		{
			if(typeof epcSameManufacturer === 'function' && epcSameManufacturer(product.manufacturer, req.brand))
			{
				return true;
			}
			if(typeof epcCrossBrandsEquivalent === 'function' && epcCrossBrandsEquivalent(product.manufacturer, req.brand))
			{
				return true;
			}
			return false;
		}
		return true;
	}
	if(typeof Products !== 'undefined' && Products.All && Products.All.length)
	{
		for(var i = 0; i < Products.All.length; i++)
		{
			if(productMatchesRequest(Products.All[i]))
			{
				return true;
			}
		}
	}
	var stockSources = [];
	if(window.epcLastCrossStock && window.epcLastCrossStock.length)
	{
		stockSources = window.epcLastCrossStock;
	}
	else if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
	{
		stockSources = epcPendingCrossStock;
	}
	for(var s = 0; s < stockSources.length; s++)
	{
		var item = stockSources[s];
		var qty = parseFloat(item.qty);
		if(isNaN(qty) || qty <= 0)
		{
			qty = parseFloat(item.exist);
		}
		if(isNaN(qty) || qty <= 0)
		{
			continue;
		}
		var itemArticle = item.article || item.article_norm || '';
		if(typeof epcSameArticle === 'function')
		{
			if(!epcSameArticle(itemArticle, req.article))
			{
				continue;
			}
		}
		else if(epcCrossNormalizeArticle(itemArticle) !== epcCrossNormalizeArticle(req.article))
		{
			continue;
		}
		if(req.brand === '')
		{
			return true;
		}
		if(typeof epcCrossBrandsEquivalent === 'function' && epcCrossBrandsEquivalent(item.brand, req.brand))
		{
			return true;
		}
		if(typeof epcSameManufacturer === 'function' && epcSameManufacturer(item.brand, req.brand))
		{
			return true;
		}
	}
	return false;
}
function epcPartSearchShouldShowUnavailableNotice()
{
	if(typeof epcChpuAnchorHasDbStock !== 'undefined' && epcChpuAnchorHasDbStock)
	{
		return false;
	}
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi() && epcChpuRequestedPartHasStock())
	{
		return false;
	}
	return true;
}
function epcPartSearchNotAvailableHTML()
{
	if(!epcPartSearchShouldShowUnavailableNotice())
	{
		return '';
	}
	var req = epcChpuGetRequestedBrandArticle();
	var article = req.article;
	var brand = req.brand;
	var meta = '';
	if(brand !== '' || article !== '')
	{
		meta = '<p class="epc-part-search-unavailable__meta"><strong>' + epcCrossEsc(brand || '—') + '</strong> · ' + epcCrossEsc(article || '—') + '</p>';
	}
	return '<div class="epc-part-search-unavailable alert alert-warning" role="status">'
		+ '<div class="epc-part-search-unavailable__title"><i class="fa fa-info-circle"></i> <strong>Not available</strong></div>'
		+ '<p>This part number was not found in stock on any connected UAE price list or warehouse. Cross references may still be listed below for manual lookup.</p>'
		+ meta
		+ '</div>';
}
function epcPartSearchHasAnyStockRows()
{
	// Prefer bound Products.All — mode-local counters are often shadowed/reset inside resultReview.
	if(typeof Products !== 'undefined' && Products && Products.All && Products.All.length)
	{
		for(var i = 0; i < Products.All.length; i++)
		{
			var bound = (typeof epcGetBoundProductByAid === 'function') ? epcGetBoundProductByAid(i) : null;
			var exist = bound ? parseFloat(bound.exist) : 0;
			if(!isNaN(exist) && exist > 0)
			{
				return true;
			}
		}
	}
	var required = (typeof Products_Required_count === 'number') ? Products_Required_count : 0;
	var quick = (typeof Products_Quick_Analogs_count === 'number') ? Products_Quick_Analogs_count : 0;
	var analogs = (typeof Products_Analogs_count === 'number') ? Products_Analogs_count : 0;
	var possible = (typeof Products_PossibleReplacement_count === 'number') ? Products_PossibleReplacement_count : 0;
	var searchName = (typeof Products_SearchName_count === 'number') ? Products_SearchName_count : 0;
	var spare = (typeof Products_Spare_Box_count === 'number') ? Products_Spare_Box_count : 0;
	return (required + quick + analogs + possible + searchName + spare) > 0;
}
function epcCrossStockToProducts(stockItems)
{
	var products = [];
	for(var i = 0; i < stockItems.length; i++)
	{
		var item = stockItems[i];
		if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
		{
			if(!epcCrossRefIsKnownReference(item.brand, item.article_norm || item.article))
			{
				continue;
			}
		}
		var storageId = parseInt(item.storage_id, 10) || 6;
		var rawBrand = String(item.brand || item.manufacturer || '').trim();
		var displayBrand = rawBrand;
		if(rawBrand !== '')
		{
			var canonFromStock = epcCrossCanonicalBrand(rawBrand);
			if(canonFromStock !== '')
			{
				displayBrand = canonFromStock;
			}
		}
		if(epcChpuProductExists(displayBrand, item.article, storageId))
		{
			epcApplyCrossStockItemToBoundProduct(item);
			continue;
		}
		var price = parseFloat(item.price);
		if(isNaN(price))
		{
			price = 0;
		}
		var exist = parseFloat(item.qty);
		if(isNaN(exist) || exist <= 0)
		{
			exist = parseFloat(item.exist);
		}
		if(isNaN(exist))
		{
			exist = 0;
		}
		var timeToExe = parseFloat(item.delivery);
		if(isNaN(timeToExe))
		{
			timeToExe = 0;
		}
		var warehouseCaption = String(item.warehouse || '');
		if(warehouseCaption === '' && typeof all_storages_info !== 'undefined' && all_storages_info[storageId])
		{
			warehouseCaption = String(all_storages_info[storageId].name || '');
		}
		if(warehouseCaption === '' && typeof all_storages !== 'undefined' && all_storages[storageId])
		{
			warehouseCaption = String(all_storages[storageId] || '');
		}
		products.push({
			manufacturer: displayBrand,
			article: String(item.article || ''),
			article_show: String(item.article || ''),
			name: String(item.name || ''),
			exist: exist,
			price: price,
			time_to_exe: timeToExe,
			time_to_exe_guaranteed: 0,
			storage: '',
			min_order: 1,
			probability: 0,
			office_id: 1,
			storage_id: storageId,
			color: '#ffffff',
			storage_caption: warehouseCaption,
			price_purchase: String(price),
			markup: 0,
			product_type: 2,
			product_id: 0,
			storage_record_id: 0,
			product_url: null,
			json_params: '',
			valid: true,
			check_hash: '',
			search_name: 0,
			url: ''
		});
	}
	return products;
}
function epcBindCrossStockProducts(stockItems)
{
	if(!stockItems || !stockItems.length || typeof bindBunchResult !== 'function')
	{
		return;
	}
	if(stockItems.length)
	{
		for(var i = 0; i < stockItems.length; i++)
		{
			epcApplyCrossStockItemToBoundProduct(stockItems[i]);
		}
	}
	var products = epcCrossStockToProducts(stockItems);
	if(products.length)
	{
		bindBunchResult({result: 1, Products: products});
	}
	epcMarkCrossStockKeys(stockItems);
}
function epcTryMergePendingCrossStock()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	if(typeof epcChpuMainSearchPending !== 'undefined' && epcChpuMainSearchPending)
	{
		return;
	}
	if(typeof epcPendingCrossStock === 'undefined' || !epcPendingCrossStock || !epcPendingCrossStock.length)
	{
		return;
	}
	var stock = epcPendingCrossStock.slice(0);
	epcPendingCrossStock = null;
	try
	{
		epcBindCrossStockProducts(stock);
		if(typeof epcSyncChpuFilterManufacturers === 'function')
		{
			epcSyncChpuFilterManufacturers();
		}
		if(typeof epcChpuPrepareFiltersBeforeReview === 'function')
		{
			epcChpuPrepareFiltersBeforeReview();
		}
		if(typeof epcChpuInitFiltersAfterLoad === 'function')
		{
			epcChpuInitFiltersAfterLoad();
		}
		this_filter = '';
		if(typeof resultReview === 'function')
		{
			resultReview();
		}
		if(typeof epcRemoveWarehouseSidebarBlock === 'function')
		{
			epcRemoveWarehouseSidebarBlock();
		}
	}
	catch(err)
	{
		console.error('epcTryMergePendingCrossStock', err);
	}
}
function epcAttrEsc(value)
{
	return String(value || '').replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
}
function epcSearchRowPhotoKey(brand, article)
{
	return String(brand || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase() + '|' + String(article || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
}
function epcApplySearchRowPhoto(brand, article, url)
{
	if(!url)
	{
		return;
	}
	var key = epcSearchRowPhotoKey(brand, article);
	var cells = document.querySelectorAll('[data-epc-row-photo="' + key + '"]');
	for(var i = 0; i < cells.length; i++)
	{
		var cell = cells[i];
		if(cell.getAttribute('data-epc-row-photo-loaded') === '1')
		{
			continue;
		}
		cell.setAttribute('data-epc-row-photo-loaded', '1');
		cell.innerHTML = '<button type="button" class="epc-search-row-photo__btn" aria-label="View product photo"><img src="' + epcCrossEsc(url) + '" alt="" loading="lazy"></button>';
		var loadedBtn = cell.querySelector('button');
		loadedBtn.style.cursor = 'zoom-in';
		(function(imageUrl, photoBrand, photoArticle, button) {
			button.onclick = function(ev) {
				ev.preventDefault();
				ev.stopPropagation();
				if(typeof window.epcOpenImageLightbox === 'function')
				{
					window.epcOpenImageLightbox(imageUrl, photoBrand + ' ' + photoArticle);
				}
			};
		})(url, brand, article, loadedBtn);
	}
}
function epcLoadSearchRowPhotoOnClick(brand, article, triggerEl)
{
	if(!brand || !article)
	{
		return;
	}
	var key = epcSearchRowPhotoKey(brand, article);
	if(triggerEl)
	{
		if(triggerEl.getAttribute('data-epc-photo-loading') === '1')
		{
			return;
		}
		triggerEl.setAttribute('data-epc-photo-loading', '1');
		triggerEl.innerHTML = '<i class="fa fa-spinner fa-spin"></i>';
	}
	function finishEmpty()
	{
		if(!triggerEl)
		{
			return;
		}
		triggerEl.removeAttribute('data-epc-photo-loading');
		triggerEl.innerHTML = '<i class="fa fa-ban"></i>';
		triggerEl.title = 'No photo available';
	}
	function finishWithUrl(imageUrl)
	{
		if(!imageUrl)
		{
			finishEmpty();
			return;
		}
		epcApplySearchRowPhoto(brand, article, imageUrl);
		if(typeof window.epcOpenImageLightbox === 'function')
		{
			window.epcOpenImageLightbox(imageUrl, brand + ' ' + article);
		}
	}
	function tryFetch(attempts)
	{
		if(typeof window.epcFetchBrandPartImage === 'function')
		{
			window.epcFetchBrandPartImage(brand, article).then(function(imageUrl) {
				if(triggerEl)
				{
					triggerEl.removeAttribute('data-epc-photo-loading');
				}
				finishWithUrl(imageUrl);
			}).catch(function() {
				finishEmpty();
			});
			return;
		}
		if(attempts > 60)
		{
			if(triggerEl)
			{
				triggerEl.removeAttribute('data-epc-photo-loading');
				triggerEl.innerHTML = '<i class="fa fa-image"></i>';
			}
			return;
		}
		window.setTimeout(function() { tryFetch(attempts + 1); }, 100);
	}
	tryFetch(0);
}
window.epcLoadSearchRowPhotoOnClick = epcLoadSearchRowPhotoOnClick;
function epcBindSearchRowPhotoLoaders(root)
{
	var scope = root || document;
	var buttons = scope.querySelectorAll('.epc-search-row-photo__btn--load');
	for(var i = 0; i < buttons.length; i++)
	{
		var btn = buttons[i];
		if(btn.getAttribute('data-epc-photo-bound') === '1')
		{
			continue;
		}
		btn.setAttribute('data-epc-photo-bound', '1');
		btn.onclick = function(ev) {
			ev.preventDefault();
			ev.stopPropagation();
			var el = this;
			epcLoadSearchRowPhotoOnClick(
				el.getAttribute('data-epc-photo-brand') || '',
				el.getAttribute('data-epc-photo-article') || '',
				el
			);
		};
	}
}
function epcSearchRowPhotoCellHTML(brand, article, index)
{
	if(index > 0)
	{
		return '<td class="td_photo epc-search-row-photo--subrow"></td>';
	}
	if(!brand || !article)
	{
		return '<td class="td_photo"></td>';
	}
	var key = epcSearchRowPhotoKey(brand, article);
	return '<td class="td_photo" data-epc-row-photo="' + key + '">'
		+ '<button type="button" class="epc-search-row-photo__btn epc-search-row-photo__btn--load" '
		+ 'data-epc-photo-brand="' + epcAttrEsc(brand) + '" '
		+ 'data-epc-photo-article="' + epcAttrEsc(article) + '" '
		+ 'title="Click to load product photo" aria-label="Load product photo">'
		+ '<i class="fa fa-image"></i></button></td>';
}
function epcFitmentCheckButtonHTML(brand, article)
{
	var art = String(article || '').trim();
	if(art === '')
	{
		return '';
	}
	return '<button type="button" class="btn btn-sm epc-btn-fitment epc-fitment-check-btn epc-fitment-check-btn--row"'
		+ ' data-epc-fitment-article="' + epcAttrEsc(art) + '"'
		+ ' data-epc-fitment-brand="' + epcAttrEsc(brand || '') + '"'
		+ ' onclick="epcOpenFitmentCheckRow(this);"'
		+ ' title="Add to Fitment / fitment check"><i class="fa fa-car" aria-hidden="true"></i> Fitment</button>';
}
function epcOpenFitmentCheckRow(btn)
{
	if(!btn || typeof window.epcOpenFitmentCheck !== 'function')
	{
		return;
	}
	window.epcOpenFitmentCheck(
		btn.getAttribute('data-epc-fitment-article') || '',
		btn.getAttribute('data-epc-fitment-brand') || '',
		btn
	);
}
function epcProductActionsHTML(aid, exist, minOrder, mode, brand, article)
{
	mode = mode || 'cart_only';
	var prod = (typeof Products !== 'undefined' && Products.All && Products.All[aid]) ? Products.All[aid] : null;
	brand = String(brand || (prod && prod.manufacturer) || '').trim();
	article = String(article || (prod && prod.article) || (prod && prod.article_show) || '').trim();
	var stockExist = exist * 1;
	if(isNaN(stockExist) || stockExist < 0){ stockExist = 0; }
	var inStock = stockExist > 0;
	var pMin = minOrder * 1;
	var pExist = stockExist > 0 ? stockExist : 1;
	if(pMin === 0){ pMin = 1; }
	// In stock: Fitment → WhatsApp → qty → Add to Cart (no Quote).
	// Out of stock: Fitment → WhatsApp → Add to Quote (no Cart).
	var showCart = (mode === 'cart_only') || (mode === 'both' && inStock);
	var showQuote = (mode === 'quote_only') || (mode === 'both' && !inStock);
	if(mode === 'cart_only'){ showQuote = false; }
	if(mode === 'quote_only'){ showCart = false; }
	// Never show Quote next to Cart for available stock, regardless of mode.
	if(inStock){ showQuote = false; }
	if(!inStock){ showCart = false; showQuote = true; }
	var guestBlocked = (typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible);
	var guestCta = (typeof epc_storefront_commerce_login_cta_html !== 'undefined' && epc_storefront_commerce_login_cta_html)
		? epc_storefront_commerce_login_cta_html
		: (epc_storefront_price_login_cta_html || '');
	// Guests: Fitment + "Log in or register" text on a single row (no stacked buttons).
	if(guestBlocked)
	{
		return '<div class="epc-product-actions epc-product-actions--guest">'
			+ '<div class="epc-product-actions__tools epc-product-actions__tools--guest">'
			+ epcFitmentCheckButtonHTML(brand, article)
			+ guestCta
			+ '</div></div>';
	}
	var html = '<div class="epc-product-actions' + (showQuote && !showCart ? ' epc-product-actions--quote-only' : '') + '">';
	html += '<div class="epc-product-actions__tools">';
	html += epcFitmentCheckButtonHTML(brand, article);
	if(typeof epcWaShareBtnHTML === 'function')
	{
		var nm = prod && prod.name ? prod.name : '';
		var pr = prod && prod.price != null ? prod.price : '';
		html += epcWaShareBtnHTML(brand, article, nm, pr);
	}
	html += '</div>';
	if(showCart)
	{
		html += '<div class="epc-product-actions__buy">';
		html += '<div class="epc-product-actions__qty">';
		html += '<a class="count_need_minus" href="javascript:void(0);" onclick="minusCountNeed(' + aid + ', ' + pExist + ', ' + pMin + ');"><i class="fa fa-minus"></i></a>';
		html += '<input class="epc-qty-input" type="text" value="' + pMin + '" onchange="onKeyUpCountNeed(' + aid + ', ' + pExist + ', ' + pMin + ');" id="count_need_' + aid + '" />';
		html += '<a class="count_need_plus" href="javascript:void(0);" onclick="plusCountNeed(' + aid + ', ' + pExist + ', ' + pMin + ');"><i class="fa fa-plus"></i></a>';
		html += '</div>';
		html += '<button type="button" class="btn btn-sm btn-danger epc-btn-cart" onclick="addToCart(' + aid + ');">Add to Cart</button>';
		html += '</div>';
	}
	if(showQuote)
	{
		html += '<button type="button" class="btn btn-sm btn-primary epc-btn-quote" onclick="addToQuote(' + aid + ');">Add to Quote</button>';
	}
	html += '</div>';
	return html;
}
function epcManualQuoteButtonHTML(brand, article, articleShow, name)
{
	if(typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
	{
		return '<div class="epc-product-actions epc-product-actions--guest">'
			+ '<div class="epc-product-actions__tools epc-product-actions__tools--guest">'
			+ epcFitmentCheckButtonHTML(brand, article)
			+ ((typeof epc_storefront_commerce_login_cta_html !== 'undefined' && epc_storefront_commerce_login_cta_html)
				? epc_storefront_commerce_login_cta_html
				: (epc_storefront_price_login_cta_html || ''))
			+ '</div></div>';
	}
	// Out-of-stock / catalog-only: Fitment → WhatsApp → Add to Quote (one row).
	return '<div class="epc-product-actions epc-product-actions--quote-only">'
		+ '<div class="epc-product-actions__tools">'
		+ epcFitmentCheckButtonHTML(brand, article)
		+ (typeof epcWaShareBtnHTML === 'function' ? epcWaShareBtnHTML(brand, articleShow || article, name || '', '') : '')
		+ '</div>'
		+ '<button type="button" class="btn btn-sm btn-primary epc-btn-quote"'
		+ ' data-epc-brand="' + epcAttrEsc(brand) + '"'
		+ ' data-epc-article="' + epcAttrEsc(article) + '"'
		+ ' data-epc-article-show="' + epcAttrEsc(articleShow || article) + '"'
		+ ' data-epc-name="' + epcAttrEsc(name || '') + '"'
		+ ' onclick="epcAddManualToQuoteBtn(this);">Add to Quote</button>'
		+ '</div>';
}
function epcAddManualToQuoteBtn(btn)
{
	epcAddManualToQuote(
		btn.getAttribute('data-epc-brand'),
		btn.getAttribute('data-epc-article'),
		btn.getAttribute('data-epc-article-show'),
		btn.getAttribute('data-epc-name'),
		1
	);
}
function epcAddManualToQuoteFromSearch()
{
	var brand = (search_object.requested_manufacturer || SelectedManufacturer || '').toString().trim();
	var article = (search_object.article || '').toString().trim();
	epcAddManualToQuote(brand, article, article, '', 1);
}
function epcAddManualToQuote(brand, article, articleShow, name, countNeed)
{
	if(typeof epcStorefrontRequireLoginForCommerce === 'function' && !epcStorefrontRequireLoginForCommerce())
	{
		return;
	}
	jQuery.ajax({
		type: 'POST',
		async: false,
		url: '/content/shop/order_process/ajax_add_to_quote_manual.php',
		dataType: 'json',
		data: {
			manufacturer: brand,
			article: article,
			article_show: articleShow || article,
			name: name || '',
			count_need: countNeed || 1
		},
		success: function(answer)
		{
			if(answer.status === true)
			{
				alert('Added to your quote (#' + answer.quote_id + ').');
			}
			else if(answer && answer.code === 'auth')
			{
				if(answer.login_url){ window.location.href = answer.login_url; }
				else if(typeof epcStorefrontRequireLoginForCommerce === 'function'){ epcStorefrontRequireLoginForCommerce(); }
				else { alert(answer.message || 'Please sign in to use quotes.'); }
			}
			else
			{
				alert(answer.message || 'Error');
			}
		}
	});
}
function epcCrossReferenceRowHTML(row, inStock, qty)
{
	var brandLabel = epcCrossResolveReferenceBrand(row);
	var searchUrl = epcCrossSearchUrl({brand: brandLabel, article: row.article});
	var manufacturer = brandLabel !== ''
		? '<span title="' + epcCrossEsc(brandLabel) + '">' + epcCrossEsc(brandLabel) + '</span>'
		: '&mdash;';
	var articleLink = '<a class="bread_crumbs_a" style="text-decoration:underline; color:#000; font-weight:700;" href="' + epcCrossEsc(searchUrl) + '" title="' + epcCrossEsc(row.article) + '"><strong>' + epcCrossEsc(row.article) + '</strong></a>';
	var sensitiveDash = (typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
		? epcStorefrontSensitiveMask()
		: '&mdash;';
	var partName = epcCrossRefResolveName(brandLabel, row.article_norm || row.article, row);
	var nameCell = partName !== ''
		? '<span title="' + epcCrossEsc(partName) + '">' + epcCrossEsc(partName) + '</span>'
		: '&mdash;';
	var availCell = epcCrossAvailabilityBadgeHTML(!!inStock, qty);
	var info = '<a title="Search availability and price" href="' + epcCrossEsc(searchUrl) + '"><span><i class="fa fa-search"></i></span></a>';
	var quoteBtn = epcManualQuoteButtonHTML(brandLabel, row.article, partName || row.article, partName);
	var photoCell = epcSearchRowPhotoCellHTML(brandLabel, row.article || row.article_norm || '', 0);
	return '<tr>'
		+ photoCell
		+ '<td class="td_manufacturer">' + manufacturer + '</td>'
		+ '<td class="td_article">' + articleLink + '</td>'
		+ '<td class="td_name">' + nameCell + '</td>'
		+ '<td class="td_exist">' + availCell + '</td>'
		+ '<td class="td_time_to_exe">' + sensitiveDash + '</td>'
		+ '<td class="td_info">' + info + '</td>'
		+ '<td class="td_price">' + sensitiveDash + '</td>'
		+ '<td class="td_add_to_cart">' + quoteBtn + '</td>'
		+ '<td class="td_color"></td></tr>';
}
function epcCrossNotInStockRowHTML(row)
{
	return epcCrossReferenceRowHTML(row, false, 0);
}
function epcCrossRefsNotInStockRowsHTML()
{
	epcSyncCrossReferenceGlobals();
	epcCrossRebuildBrandByArticleMap();
	epcSyncCrossStockKeysLight();
	var source = [];
	if(epcCrossAllReferences && epcCrossAllReferences.length)
	{
		source = epcCrossAllReferences;
	}
	else if(window.epcCrossAllReferences && window.epcCrossAllReferences.length)
	{
		source = window.epcCrossAllReferences;
		epcCrossAllReferences = source.slice(0);
	}
	if(!source.length && epcCrossFallbackRows && epcCrossFallbackRows.length)
	{
		for(var f = 0; f < epcCrossFallbackRows.length; f++)
		{
			var fallbackRow = epcCrossFallbackRows[f];
			source.push({
				brand: fallbackRow.brand,
				article: fallbackRow.article,
				article_norm: epcCrossNormalizeArticle(fallbackRow.article)
			});
		}
	}
	if(!source.length)
	{
		return '';
	}
	var crossRows = [];
	var seenCross = {};
	var inStockListed = 0;
	var notInStockListed = 0;
	for(var i = 0; i < source.length; i++)
	{
		var ref = source[i];
		var articleValue = ref.article_norm || ref.article;
		var refBrand = (typeof epcCrossResolveReferenceBrand === 'function')
			? epcCrossResolveReferenceBrand(ref)
			: (ref.brand || '');
		if(epcCrossIsSearchedReference(ref))
		{
			continue;
		}
		var dedupeKey = epcCrossRefKey(refBrand, articleValue);
		if(seenCross[dedupeKey])
		{
			continue;
		}
		seenCross[dedupeKey] = true;
		var inStock = epcCrossRefIsInStock(refBrand, articleValue);
		var qty = inStock ? epcCrossRefStockQty(refBrand, articleValue) : 0;
		if(inStock && epcCrossRefIsInProductsTable(refBrand, articleValue))
		{
			continue;
		}
		if(inStock)
		{
			inStockListed++;
		}
		else
		{
			notInStockListed++;
		}
		crossRows.push({ref: ref, inStock: inStock, qty: qty, brand: refBrand});
	}
	if(!crossRows.length)
	{
		return '';
	}
	// Unique brand+article combinations (same rule as the API).
	var loadedCount = 0;
	for(var loadedKey in seenCross)
	{
		if(Object.prototype.hasOwnProperty.call(seenCross, loadedKey))
		{
			loadedCount++;
		}
	}
	if(!loadedCount)
	{
		loadedCount = source.length;
	}
	var totalReported = epcCrossReferencesTotal || loadedCount;
	var caption = 'Cross references';
	var hasWarehouseOeStock = (typeof Products !== 'undefined' && Products && Products.All && Products.All.length > 0);
	if(notInStockListed > 0 && inStockListed > 0)
	{
		caption += ' (' + inStockListed + ' in stock · ' + notInStockListed + ' not in stock on this page';
	}
	else if(notInStockListed > 0)
	{
		// Do not imply the searched OE warehouse offer is missing when it is already listed above.
		if(hasWarehouseOeStock)
		{
			caption += ' — additional numbers not in warehouse stock (' + notInStockListed + ' on this page';
		}
		else
		{
			caption += ' — not in stock on UAE warehouses (' + notInStockListed + ' on this page';
		}
	}
	else
	{
		caption += ' — in stock (' + inStockListed + ' on this page';
	}
	if(totalReported > loadedCount)
	{
		caption += '; ' + loadedCount + ' of ' + totalReported.toLocaleString() + ' unique crosses loaded';
	}
	caption += ')';
	var notInStockSplit = epcPartTypeCreateSplit();
	for(var j = 0; j < crossRows.length; j++)
	{
		epcPartTypeAppendRow(
			notInStockSplit,
			epcCrossReferenceRowHTML(crossRows[j].ref, crossRows[j].inStock, crossRows[j].qty),
			crossRows[j].brand,
			crossRows[j].inStock ? 1 : 0
		);
	}
	var rowsHtml = epcCrossNotFoundCaptionHTML(caption);
	if(epcPartTypeHasRows(notInStockSplit))
	{
		rowsHtml += epcPartTypeComposeRows(notInStockSplit);
	}
	return rowsHtml;
}
function epcCrossRefsNotInStockHTML(head_block)
{
	var rowsHtml = epcCrossRefsNotInStockRowsHTML();
	if(rowsHtml === '')
	{
		return '';
	}
	if(!head_block && typeof window.epcLastProductsHeadBlock !== 'undefined')
	{
		head_block = window.epcLastProductsHeadBlock;
	}
	if(!head_block)
	{
		head_block = '<tr><th class="th_photo" title="Photo"></th><th class="th_manufacturer"><?php echo translate_str_by_id(2070); ?></th><th class="th_article"><?php echo translate_str_by_id(2071); ?></th><th class="th_name"><?php echo translate_str_by_id(2102); ?></th><th class="th_exist"><?php echo translate_str_by_id(4324); ?></th><th class="th_time_to_exe"><?php echo translate_str_by_id(3550); ?></th><th class="th_info"><?php echo translate_str_by_id(4325); ?></th><th class="th_price"><?php echo translate_str_by_id(2751); ?></th><th class="th_add_to_cart">Actions</th><th class="th_color"></th></tr>';
	}
	return '<table id="all_table_products"><thead>' + head_block + '</thead><tbody>' + rowsHtml + '</tbody></table>';
}
function epcCrossFallbackHTML(withStockFound)
{
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		return epcCrossRefsNotInStockHTML();
	}
	if(!epcCrossFallbackRows || !epcCrossFallbackRows.length)
	{
		return '';
	}
	var subtitle = withStockFound
		? 'Related cross numbers for this part. Open any row to check availability and price.'
		: 'Direct stock was not found. Choose a cross number to check availability and price.';
	var html = '<div class="epc-cross-fallback"><div class="epc-cross-fallback__head"><strong>Cross reference numbers for <?php echo htmlspecialchars($article, ENT_QUOTES, 'UTF-8'); ?></strong><span>' + subtitle + '</span></div>';
	html += '<div class="table-responsive"><table class="table table-condensed table-striped epc-cross-fallback__table"><thead><tr><th>Brand</th><th>Part number</th><th>Cross reference</th><th class="text-right">Search availability & price</th></tr></thead><tbody>';
	for(var i=0; i<epcCrossFallbackRows.length; i++)
	{
		var row = epcCrossFallbackRows[i];
		html += '<tr><td>' + epcCrossEsc(row.brand) + '</td><td><strong>' + epcCrossEsc(row.article) + '</strong></td><td>' + epcCrossEsc(row.cross_brand) + ' ' + epcCrossEsc(row.cross_article) + '</td><td class="text-right"><a class="btn btn-xs btn-primary" href="' + epcCrossEsc(epcCrossSearchUrl(row)) + '">Search availability & price</a></td></tr>';
	}
	html += '</tbody></table></div></div>';
	return html;
}




// ------------------------------------------------------------------------------------------------------------------------------




function epcDecodeHtmlEntities(value)
{
	var el = document.createElement('textarea');
	el.innerHTML = String(value || '');
	return el.value;
}
function epcChpuProductArticleKey(manufacturer, article, storageId)
{
	var norm = function(value) {
		return String(value || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	};
	var brandKey = epcCrossCanonicalBrand(manufacturer);
	var key = brandKey + '|' + norm(article);
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		var sid = parseInt(storageId, 10);
		if(!isNaN(sid) && sid > 0)
		{
			key += '|' + sid;
		}
	}
	return key;
}
function epcChpuProductKey(manufacturer, article, storageId)
{
	return epcChpuProductArticleKey(manufacturer, article, storageId);
}
function epcChpuNormalizeGroupBrand(manufacturer)
{
	// CHPU: never collapse warehouse brands via synonym/canonical maps.
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		return String(manufacturer || '').trim().toUpperCase();
	}
	var canon = epcCrossCanonicalBrand(manufacturer);
	var req = (typeof search_object !== 'undefined' && search_object.requested_manufacturer) ? search_object.requested_manufacturer : (typeof SelectedManufacturer !== 'undefined' ? SelectedManufacturer : null);
	if(req && epcSameManufacturer(manufacturer, req))
	{
		return epcCrossCanonicalBrand(req);
	}
	return canon;
}
function epcChpuBrandArticleGroupKey(manufacturer, article)
{
	return epcChpuNormalizeGroupBrand(manufacturer) + '|' + epcCrossNormalizeArticle(article);
}
function epcChpuFindEquivalentGroupKey(groupKey, groupsMap)
{
	var parts = String(groupKey || '').split('|');
	if(parts.length < 2 || parts[1] === '')
	{
		return groupKey;
	}
	for(var existingKey in groupsMap)
	{
		if(!groupsMap.hasOwnProperty(existingKey))
		{
			continue;
		}
		var existingParts = existingKey.split('|');
		if(existingParts.length < 2 || existingParts[1] !== parts[1])
		{
			continue;
		}
		// Synonym-aware brand equality (AISIN ≡ AISINC).
		if(epcSameManufacturer(existingParts[0], parts[0]))
		{
			return existingKey;
		}
	}
	return groupKey;
}
function epcChpuGroupProductBlocks(productTypes, productsManufacturers)
{
	if(!productTypes || !productTypes.length || !productsManufacturers)
	{
		return [];
	}
	var groups = {};
	var groupOrder = [];
	for(var i = 0; i < productTypes.length; i++)
	{
		var productType = productTypes[i];
		var manufacturer = productType.manufacturer;
		var article = productType.article;
		if(!productsManufacturers[manufacturer] || !productsManufacturers[manufacturer][article])
		{
			continue;
		}
		var groupKey = epcChpuBrandArticleGroupKey(manufacturer, article);
		var mergedKey = epcChpuFindEquivalentGroupKey(groupKey, groups);
		if(!groups[mergedKey])
		{
			groups[mergedKey] = { productType: productType, products: [] };
			groupOrder.push(mergedKey);
		}
		else if(String(productType.name || '').length > String(groups[mergedKey].productType.name || '').length)
		{
			groups[mergedKey].productType.name = productType.name;
		}
		var productsObjects = productsManufacturers[manufacturer][article];
		for(var p = 0; p < productsObjects.length; p++)
		{
			groups[mergedKey].products.push(productsObjects[p]);
		}
	}
	var result = [];
	for(var g = 0; g < groupOrder.length; g++)
	{
		if(groups[groupOrder[g]] && groups[groupOrder[g]].products.length)
		{
			result.push(groups[groupOrder[g]]);
		}
	}
	return result;
}
function epcGetBoundProductByAid(aid)
{
	if(typeof Products === 'undefined' || !Products.All || !Products.All.length)
	{
		return null;
	}
	var aidNum = parseInt(aid, 10);
	if(isNaN(aidNum) || aidNum < 0 || aidNum >= Products.All.length)
	{
		return null;
	}
	var AID_Object = Products.All[aidNum];
	if(!AID_Object)
	{
		return null;
	}
	var buckets = [
		{flag: 'isRequired', types: Products.Required},
		{flag: 'isSearchName', types: Products.SearchName},
		{flag: 'isQuickAnalogs', types: Products.Quick_Analogs},
		{flag: 'isAnalogs', types: Products.Analogs},
		{flag: 'isPossibleReplacement', types: Products.PossibleReplacement},
		{flag: 'isSpare_Box', types: Products.Spare_Box}
	];
	for(var b = 0; b < buckets.length; b++)
	{
		if(!AID_Object[buckets[b].flag] || !buckets[b].types || !buckets[b].types.ProductsTypes)
		{
			continue;
		}
		var types = buckets[b].types.ProductsTypes;
		for(var i = 0; i < types.length; i++)
		{
			var manufacturer = types[i].manufacturer;
			var article = types[i].article;
			var productsMap = buckets[b].types.Products.Manufacturers[manufacturer];
			if(!productsMap || !productsMap[article])
			{
				continue;
			}
			var productsObjects = productsMap[article];
			for(var p = 0; p < productsObjects.length; p++)
			{
				if(parseInt(productsObjects[p].aid, 10) === aidNum)
				{
					return productsObjects[p];
				}
			}
		}
	}
	return null;
}
function epcFindBoundProduct(manufacturer, article, storageId)
{
	if(typeof Products === 'undefined' || !Products.All || !Products.All.length)
	{
		return null;
	}
	var key = epcChpuProductArticleKey(manufacturer, article, storageId);
	for(var i = 0; i < Products.All.length; i++)
	{
		var product = epcGetBoundProductByAid(i);
		if(!product)
		{
			continue;
		}
		if(epcChpuProductArticleKey(product.manufacturer, product.article || product.article_show, product.storage_id) === key)
		{
			return product;
		}
	}
	return null;
}
function epcChpuProductExists(manufacturer, article, storageId)
{
	return epcFindBoundProduct(manufacturer, article, storageId) !== null;
}
function epcApplyCrossStockItemToBoundProduct(item)
{
	if(!item)
	{
		return null;
	}
	var storageId = parseInt(item.storage_id, 10) || 6;
	var rawBrand = String(item.brand || item.manufacturer || '').trim();
	var displayBrand = rawBrand;
	if(rawBrand !== '' && typeof epcCrossCanonicalBrand === 'function')
	{
		var canonBrand = epcCrossCanonicalBrand(rawBrand);
		if(canonBrand !== '')
		{
			displayBrand = canonBrand;
		}
	}
	var product = epcFindBoundProduct(displayBrand, String(item.article || ''), storageId);
	if(!product)
	{
		return null;
	}
	var exist = parseFloat(item.qty);
	if(isNaN(exist) || exist <= 0)
	{
		exist = parseFloat(item.exist);
	}
	if(!isNaN(exist) && exist > 0)
	{
		product.exist = exist;
	}
	// Never overwrite price / price_purchase / check_hash from cross-stock.
	// Warehouse AJAX already priced the row; mutating purchase price invalidates
	// check_hash and causes "price data expired" on Add to Cart (e.g. S-UAE).
	return product;
}
function epcDedupeChpuProductsByBrandArticle(products)
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing || !products || !products.length)
	{
		return products;
	}
	var bestByKey = {};
	for(var i = 0; i < products.length; i++)
	{
		var product = products[i];
		var key = epcChpuProductArticleKey(product.manufacturer, product.article || product.article_show, product.storage_id);
		var price = parseFloat(product.price);
		if(isNaN(price))
		{
			price = Number.MAX_VALUE;
		}
		if(!bestByKey[key] || price < parseFloat(bestByKey[key].price))
		{
			bestByKey[key] = product;
		}
	}
	var deduped = [];
	for(var k in bestByKey)
	{
		if(bestByKey.hasOwnProperty(k))
		{
			deduped.push(bestByKey[k]);
		}
	}
	return deduped;
}
function epcFilterChpuCrossStockNotYetRendered(products, renderedKeys)
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing || !products || !products.length)
	{
		return products;
	}
	var filtered = [];
	for(var i = 0; i < products.length; i++)
	{
		var product = products[i];
		var key = epcChpuProductArticleKey(product.manufacturer, product.article || product.article_show, product.storage_id);
		if(renderedKeys[key])
		{
			continue;
		}
		renderedKeys[key] = true;
		filtered.push(product);
	}
	return filtered;
}
function epcChpuRangeBlockActive(blockName)
{
	if(typeof filter === 'undefined' || !filter[blockName])
	{
		return false;
	}
	return filter[blockName].min_need !== undefined && filter[blockName].max_need !== undefined;
}
function epcProductPassesRangeFilters(product)
{
	if(typeof filter === 'undefined')
	{
		return true;
	}
	if(epcChpuRangeBlockActive('price_blok'))
	{
		var price = parseFloat(product.price);
		var priceMin = parseFloat(filter['price_blok'].min_need);
		var priceMax = parseFloat(filter['price_blok'].max_need);
		if(!isNaN(priceMin) && !isNaN(priceMax) && priceMin <= priceMax && !(priceMin === 0 && priceMax === 0))
		{
			if(isNaN(price) || price < priceMin || price > priceMax)
			{
				return false;
			}
		}
	}
	if(epcChpuRangeBlockActive('time_to_exe_blok'))
	{
		var timeToExe = parseFloat(product.time_to_exe);
		var timeMin = parseFloat(filter['time_to_exe_blok'].min_need);
		var timeMax = parseFloat(filter['time_to_exe_blok'].max_need);
		if(!isNaN(timeMin) && !isNaN(timeMax) && timeMin <= timeMax)
		{
			if(isNaN(timeToExe) || timeToExe < timeMin || timeToExe > timeMax)
			{
				return false;
			}
		}
	}
	if(epcChpuRangeBlockActive('exist_blok'))
	{
		var exist = parseFloat(product.exist);
		var existMin = parseFloat(filter['exist_blok'].min_need);
		var existMax = parseFloat(filter['exist_blok'].max_need);
		if(!isNaN(existMin) && !isNaN(existMax) && existMin <= existMax && !(existMin === 0 && existMax === 0))
		{
			if(isNaN(exist) || exist < existMin || exist > existMax)
			{
				return false;
			}
		}
	}
	return true;
}
function epcProductMatchesManufacturerFilter(productManufacturer, manufacturerFilter)
{
	if(!manufacturerFilter || !manufacturerFilter.length)
	{
		return false;
	}
	for(var i = 0; i < manufacturerFilter.length; i++)
	{
		if(productManufacturer === manufacturerFilter[i])
		{
			return true;
		}
		if(typeof epcCrossBrandsEquivalent === 'function' && epcCrossBrandsEquivalent(productManufacturer, manufacturerFilter[i]))
		{
			return true;
		}
	}
	return false;
}
function epcFilterProductsByListFilters(ProductsObjects)
{
	if(!ProductsObjects || !ProductsObjects.length)
	{
		return ProductsObjects;
	}
	var manufacturerFilter = filter['manufacturer_blok'].manufacturer_in_filter;
	var storageFilter = filter['storages_blok'].storages_in_filter;
	var tmp_arr = [];
	for(var p = 0; p < ProductsObjects.length; p++)
	{
		if(manufacturerFilter.length > 0 && !epcProductMatchesManufacturerFilter(ProductsObjects[p].manufacturer, manufacturerFilter))
		{
			continue;
		}
		if(storageFilter.length > 0 && storageFilter.indexOf(ProductsObjects[p].storage_id * 1) === -1)
		{
			continue;
		}
		tmp_arr.push(ProductsObjects[p]);
	}
	return tmp_arr;
}
function epcReadChpuFilterRanges()
{
	if(typeof filter === 'undefined')
	{
		return;
	}
	var rangeDefs = [
		{block: 'price_blok', propertyId: 'price'},
		{block: 'time_to_exe_blok', propertyId: 'time_to_exe'},
		{block: 'exist_blok', propertyId: 'exist'}
	];
	for(var r = 0; r < rangeDefs.length; r++)
	{
		var def = rangeDefs[r];
		var block = filter[def.block];
		if(!block)
		{
			continue;
		}
		var minInput = document.getElementById('range_min_' + def.propertyId);
		var maxInput = document.getElementById('range_max_' + def.propertyId);
		if(minInput && maxInput)
		{
			var minVal = parseFloat(minInput.value);
			var maxVal = parseFloat(maxInput.value);
			if(!isNaN(minVal))
			{
				block.min_need = minVal;
			}
			if(!isNaN(maxVal))
			{
				block.max_need = maxVal;
			}
		}
		if(typeof jQuery !== 'undefined')
		{
			var sliderNode = jQuery('#slider-range_' + def.propertyId);
			if(sliderNode.length && typeof sliderNode.slider === 'function')
			{
				try
				{
					var values = sliderNode.slider('values');
					if(values && values.length > 1)
					{
						block.min_need = values[0];
						block.max_need = values[1];
					}
				}
				catch(sliderErr) {}
			}
		}
	}
}
function epcReadChpuManufacturerFilterFromDom()
{
	if(typeof filter === 'undefined' || !filter['manufacturer_blok'] || !filter['manufacturer_blok'].list_options)
	{
		return [];
	}
	var selected = [];
	for(var k = 0; k < filter['manufacturer_blok'].list_options.length; k++)
	{
		var option = filter['manufacturer_blok'].list_options[k];
		var checkbox = document.getElementById('list_manufacturer_' + option.id);
		var isChecked = checkbox ? checkbox.checked : (option.value === true);
		option.value = isChecked;
		if(isChecked && typeof arr_manufacturers !== 'undefined' && arr_manufacturers[option.id])
		{
			selected.push(arr_manufacturers[option.id]);
		}
	}
	filter['manufacturer_blok'].manufacturer_in_filter = selected.slice(0);
	return selected;
}
function epcStoreChpuActiveFilterState()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing || typeof filter === 'undefined')
	{
		return;
	}
	epcReadChpuFilterRanges();
	var manufacturers = epcReadChpuManufacturerFilterFromDom();
	window.epcChpuActiveFilterState = {
		ranges: {},
		manufacturers: manufacturers.slice(0)
	};
	var rangeBlocks = ['price_blok', 'time_to_exe_blok', 'exist_blok'];
	for(var i = 0; i < rangeBlocks.length; i++)
	{
		var block = filter[rangeBlocks[i]];
		if(block && block.min_need !== undefined && block.max_need !== undefined)
		{
			window.epcChpuActiveFilterState.ranges[rangeBlocks[i]] = {
				min: block.min_need,
				max: block.max_need
			};
		}
	}
}
function epcBuildChpuFilterUiPreserve()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	if(!window.epcChpuActiveFilterState && typeof epcStoreChpuActiveFilterState === 'function')
	{
		epcStoreChpuActiveFilterState();
	}
	window.epcChpuFilterUiPreserve = {ranges: {}, manufacturers: {}};
	var state = window.epcChpuActiveFilterState;
	if(!state)
	{
		return;
	}
	var rangeBlocks = ['price_blok', 'time_to_exe_blok', 'exist_blok'];
	for(var rb = 0; rb < rangeBlocks.length; rb++)
	{
		var rangeBlockName = rangeBlocks[rb];
		if(state.ranges && state.ranges[rangeBlockName])
		{
			window.epcChpuFilterUiPreserve.ranges[rangeBlockName] = {
				min_need: state.ranges[rangeBlockName].min,
				max_need: state.ranges[rangeBlockName].max
			};
			if(filter[rangeBlockName])
			{
				filter[rangeBlockName].min_need = state.ranges[rangeBlockName].min;
				filter[rangeBlockName].max_need = state.ranges[rangeBlockName].max;
			}
		}
	}
	if(filter['manufacturer_blok'] && filter['manufacturer_blok'].list_options && state.manufacturers)
	{
		for(var mo = 0; mo < filter['manufacturer_blok'].list_options.length; mo++)
		{
			var moOpt = filter['manufacturer_blok'].list_options[mo];
			if(!moOpt || !moOpt.text)
			{
				continue;
			}
			var moName = arr_manufacturers[moOpt.id];
			var moChecked = (state.manufacturers.indexOf(moName) !== -1);
			moOpt.value = moChecked;
			window.epcChpuFilterUiPreserve.manufacturers[moOpt.text] = moChecked;
		}
		filter['manufacturer_blok'].manufacturer_in_filter = state.manufacturers.slice(0);
	}
}
function epcApplyChpuActiveFilterState()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	if(window.epcChpuSkipFilterDomRead)
	{
		return;
	}
	var state = window.epcChpuActiveFilterState;
	if(!state || typeof filter === 'undefined')
	{
		epcReadChpuFilterRanges();
		epcReadChpuManufacturerFilterFromDom();
		return;
	}
	if(state.ranges)
	{
		for(var rangeKey in state.ranges)
		{
			if(!state.ranges.hasOwnProperty(rangeKey) || !filter[rangeKey])
			{
				continue;
			}
			filter[rangeKey].min_need = state.ranges[rangeKey].min;
			filter[rangeKey].max_need = state.ranges[rangeKey].max;
		}
	}
	if(state.manufacturers)
	{
		filter['manufacturer_blok'].manufacturer_in_filter = state.manufacturers.slice(0);
		if(filter['manufacturer_blok'].list_options)
		{
			for(var m = 0; m < filter['manufacturer_blok'].list_options.length; m++)
			{
				var manufacturerOption = filter['manufacturer_blok'].list_options[m];
				var manufacturerName = arr_manufacturers[manufacturerOption.id];
				manufacturerOption.value = (state.manufacturers.indexOf(manufacturerName) !== -1);
			}
		}
	}
}
function epcSyncChpuManufacturerFilterFromListOptions()
{
	if(typeof filter === 'undefined' || !filter['manufacturer_blok'] || !filter['manufacturer_blok'].list_options)
	{
		return;
	}
	if(window.epcChpuActiveFilterState && window.epcChpuActiveFilterState.manufacturers)
	{
		filter['manufacturer_blok'].manufacturer_in_filter = window.epcChpuActiveFilterState.manufacturers.slice(0);
		return;
	}
	var selected = [];
	for(var k = 0; k < filter['manufacturer_blok'].list_options.length; k++)
	{
		if(filter['manufacturer_blok'].list_options[k].value === true)
		{
			selected.push(arr_manufacturers[filter['manufacturer_blok'].list_options[k].id]);
		}
	}
	filter['manufacturer_blok'].manufacturer_in_filter = selected;
}
function epcGetChpuManufacturerFilterList()
{
	if(window.epcChpuActiveFilterState && window.epcChpuActiveFilterState.manufacturers)
	{
		return window.epcChpuActiveFilterState.manufacturers.slice(0);
	}
	if(typeof filter !== 'undefined' && filter['manufacturer_blok'] && filter['manufacturer_blok'].manufacturer_in_filter)
	{
		return filter['manufacturer_blok'].manufacturer_in_filter.slice(0);
	}
	return [];
}
function epcFilterChpuProducts(ProductsObjects)
{
	if(!ProductsObjects || !ProductsObjects.length)
	{
		return ProductsObjects;
	}
	if(window.epcChpuActiveFilterState)
	{
		epcApplyChpuActiveFilterState();
	}
	var manufacturerFilter = epcGetChpuManufacturerFilterList();
	var storageFilter = filter['storages_blok'].storages_in_filter;
	var tmp_arr = [];
	for(var p = 0; p < ProductsObjects.length; p++)
	{
		var product = ProductsObjects[p];
		if(manufacturerFilter.length > 0 && !epcProductMatchesManufacturerFilter(product.manufacturer, manufacturerFilter))
		{
			continue;
		}
		if(storageFilter.length > 0 && storageFilter.indexOf(product.storage_id * 1) === -1)
		{
			continue;
		}
		if(!epcProductPassesRangeFilters(product))
		{
			continue;
		}
		tmp_arr.push(product);
	}
	return tmp_arr;
}
function epcApplyResultFilters(ProductsObjects)
{
	if(!ProductsObjects || !ProductsObjects.length)
	{
		return ProductsObjects;
	}
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi() && window.epcChpuSkipResultFiltersOnce)
	{
		return ProductsObjects;
	}
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi())
	{
		return epcFilterChpuProducts(ProductsObjects);
	}
	if(typeof epcSyncChpuManufacturerFilterFromCheckboxes === 'function')
	{
		epcSyncChpuManufacturerFilterFromCheckboxes();
	}
	if(this_filter !== '' && typeof filtering_items === 'function')
	{
		return filtering_items(ProductsObjects);
	}
	return epcFilterProductsByListFilters(ProductsObjects);
}
function epcSyncChpuManufacturerFilterFromCheckboxes()
{
	if(typeof filter === 'undefined' || !filter['manufacturer_blok'] || !filter['manufacturer_blok'].list_options)
	{
		return;
	}
	var selected = [];
	for(var k = 0; k < filter['manufacturer_blok'].list_options.length; k++)
	{
		if(filter['manufacturer_blok'].list_options[k].value === true)
		{
			selected.push(arr_manufacturers[filter['manufacturer_blok'].list_options[k].id]);
		}
	}
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		if(selected.length > 0)
		{
			filter['manufacturer_blok'].manufacturer_in_filter = selected;
		}
		else if(filter['manufacturer_blok'].list_options.length === 0 && typeof arr_manufacturers !== 'undefined')
		{
			filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers.slice(0);
		}
		else
		{
			filter['manufacturer_blok'].manufacturer_in_filter = selected;
		}
	}
	else if(selected.length > 0)
	{
		filter['manufacturer_blok'].manufacturer_in_filter = selected;
	}
	else
	{
		filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers.slice(0);
	}
}
function epcSameArticle(left, right)
{
	var normalize = function(value) {
		return String(value || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	};
	var leftNorm = normalize(left);
	var rightNorm = normalize(right);
	if(leftNorm === rightNorm)
	{
		return true;
	}
	// CHPU URLs use stripped article; price rows may carry suffixes (e.g. 1310154101STD).
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && leftNorm !== '' && rightNorm !== '')
	{
		return leftNorm.indexOf(rightNorm) === 0 || rightNorm.indexOf(leftNorm) === 0;
	}
	return false;
}
function epcSameManufacturer(left, right)
{
	if(right == null)
	{
		return true;
	}
	// Exact match, or CP synonym group (AISIN ≡ AISINC).
	var leftNorm = String(left || '').trim().toUpperCase();
	var rightNorm = String(right || '').trim().toUpperCase();
	if(leftNorm === '' || rightNorm === '')
	{
		return false;
	}
	if(leftNorm === rightNorm)
	{
		return true;
	}
	if(typeof epcCrossBrandsEquivalent === 'function')
	{
		return epcCrossBrandsEquivalent(leftNorm, rightNorm);
	}
	var canonicalMap = window.epcManufacturerCanonicalMap || {};
	var leftCanon = String(canonicalMap[leftNorm] || leftNorm).toUpperCase();
	var rightCanon = String(canonicalMap[rightNorm] || rightNorm).toUpperCase();
	return leftCanon === rightCanon;
}

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
	innerSort();//Внутренняя сортировка (Наличие-Срок-Цена) / внешняя вход в внутреннею
	if(typeof epcChpuSafeResultReview === 'function')
	{
		epcChpuSafeResultReview();
	}
	else
	{
		resultReview();//Обновляем отображние результата
	}
}




// ------------------------------------------------------------------------------------------------------------------------------




//Метод соединения полученного от сервера результата с общим объектом описания найденных товаров
function bindBunchResult(answer)
{
	//Полученный результат распределяем по структуре результата
	var result_length = answer.Products.length;
    for(var i=0; i < result_length; i++)
    {
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		{
			// Keep warehouse label, but group synonym brands under the page brand when equivalent.
			var rawMfr = String(answer.Products[i].manufacturer || '').trim();
			answer.Products[i].manufacturer_warehouse = rawMfr;
			var pageMfr = '';
			if(typeof search_object !== 'undefined' && search_object)
			{
				pageMfr = String(search_object.requested_manufacturer || SelectedManufacturer || '').trim();
			}
			if(pageMfr && typeof epcSameManufacturer === 'function' && epcSameManufacturer(rawMfr, pageMfr))
			{
				answer.Products[i].manufacturer = pageMfr;
			}
			else
			{
				answer.Products[i].manufacturer = rawMfr;
			}
			if(!answer.Products[i].storage_caption)
			{
				var sid = parseInt(answer.Products[i].storage_id, 10);
				if(typeof all_storages_info !== 'undefined' && all_storages_info[sid])
				{
					answer.Products[i].storage_caption = String(all_storages_info[sid].name || '');
				}
				else if(typeof all_storages !== 'undefined' && all_storages[sid])
				{
					answer.Products[i].storage_caption = String(all_storages[sid] || '');
				}
			}
		}
		if(epcChpuProductExists(answer.Products[i].manufacturer, answer.Products[i].article, answer.Products[i].storage_id))
		{
			continue;
		}
        // Преобразование типов
		answer.Products[i]["exist"] = answer.Products[i]["exist"] * 1;
		answer.Products[i]["time_to_exe"] = answer.Products[i]["time_to_exe"] * 1;
		answer.Products[i]["price"] = answer.Products[i]["price"] * 1;
		
		var manufacturer 			= String(answer.Products[i]["manufacturer"]);
		manufacturer = epcDecodeHtmlEntities(manufacturer);
		
        var article 				= String(answer.Products[i]["article"]);
        var name 					= String(answer.Products[i]["name"]);
        var article_show 			= String(answer.Products[i]["article_show"]);
        var exist 					= answer.Products[i]["exist"];
        var time_to_exe 			= answer.Products[i]["time_to_exe"];
		var price 					= answer.Products[i]["price"];
		var storage 				= answer.Products[i]["storage_id"] * 1;
		var storage_color 			= answer.Products[i]["color"];
		
		var search_name 			= answer.Products[i]["search_name"] * 1;// Флаг товара найденного по наименованию
		
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
			//Если такого производителя еще не было - создаем для него ячейку
			if(Products.Spare_Box.Products.Manufacturers[manufacturer] == undefined)
			{
				Products.Spare_Box.Products.Manufacturers[manufacturer] = new Object;
			}
			//Если такого артикула еще не было у данного производителя
			if(Products.Spare_Box.Products.Manufacturers[manufacturer][article] == undefined)
			{
				Products.Spare_Box.Products.Manufacturers[manufacturer][article] = new Array();//Ячейка для объектов товаров
				
				//Создаем новый тип товара и добавляем его в список типов
				var ProductType = new Object;
				ProductType.manufacturer = manufacturer;
				ProductType.article = article;
				ProductType.name = name;
				ProductType.article_show = article_show;
				ProductType.exist = exist;
				ProductType.time_to_exe = time_to_exe;
				ProductType.price = price;
				ProductType.storage = storage;
				
				Products.Spare_Box.ProductsTypes.push(ProductType);
			}
			
			//Добавляем сам объект товара
			Products.Spare_Box.Products.Manufacturers[manufacturer][article].push(answer.Products[i]);
			
			//Для учетного объекта - указываем, что товар находится в списке аналогов
			AID_Object.isRequired 				= false;
			AID_Object.isSearchName 			= false;						
			AID_Object.isQuickAnalogs 			= false;
			AID_Object.isAnalogs 				= false;
			AID_Object.isPossibleReplacement 	= false;
			AID_Object.isSpare_Box	 			= true;
		}
		else
		{
			// Если товар найден по наименованию
			if(search_name === 1){
				
				//Если такого производителя еще не было - создаем для него ячейку
				if(Products.SearchName.Products.Manufacturers[manufacturer] == undefined)
				{
					Products.SearchName.Products.Manufacturers[manufacturer] = new Object;
				}
				//Если такого артикула еще не было у данного производителя
				if(Products.SearchName.Products.Manufacturers[manufacturer][article] == undefined)
				{
					Products.SearchName.Products.Manufacturers[manufacturer][article] = new Array();//Ячейка для объектов товаров
					
					//Создаем новый тип товара и добавляем его в список типов
					var ProductType = new Object;
					ProductType.manufacturer = manufacturer;
					ProductType.article = article;
					ProductType.name = name;
					ProductType.article_show = article_show;
					ProductType.exist = exist;
					ProductType.time_to_exe = time_to_exe;
					ProductType.price = price;
					ProductType.storage = storage;
					
					Products.SearchName.ProductsTypes.push(ProductType);
				}
				
				//Добавляем сам объект товара
				Products.SearchName.Products.Manufacturers[manufacturer][article].push(answer.Products[i]);
				
				//Для учетного объекта - указываем, что товар находится в списке аналогов
				AID_Object.isRequired 				= false;
				AID_Object.isSearchName 			= true;
				AID_Object.isQuickAnalogs 			= false;
				AID_Object.isAnalogs 				= false;
				AID_Object.isPossibleReplacement 	= false;
				AID_Object.isSpare_Box			 	= false;
				
				
			}else{
				
				
				//Продукт считает Запрошенным, если совпал Артикул
				var requestedManufacturer = SelectedManufacturer;
				if(requestedManufacturer == null && search_object.requested_manufacturer)
				{
					requestedManufacturer = search_object.requested_manufacturer;
				}
				var chpuArticleMatch = (typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && epcSameArticle(article, search_object.article));
				if( chpuArticleMatch || (epcSameArticle(article, search_object.article) && epcSameManufacturer(manufacturer, requestedManufacturer)) )
				{
					//Если такого производителя еще не было - создаем для него ячейку
					if(Products.Required.Products.Manufacturers[manufacturer] == undefined)
					{
						Products.Required.Products.Manufacturers[manufacturer] = new Object;
					}
					if(Products.Required.Products.Manufacturers[manufacturer][article] == undefined)
					{
						Products.Required.Products.Manufacturers[manufacturer][article] = new Array();//Ячейка для самих объектов товаров
						
						//Создаем новый тип товара и добавляем его в список типов
						var ProductType = new Object;
						ProductType.manufacturer = manufacturer;
						ProductType.article = article;
						ProductType.name = name;
						ProductType.article_show = article_show;
						ProductType.exist = exist;
						ProductType.time_to_exe = time_to_exe;
						ProductType.price = price;
						ProductType.storage = storage;
						
						Products.Required.ProductsTypes.push(ProductType);
					}
					
					//Добавляем сам объект товара в ячейку
					Products.Required.Products.Manufacturers[manufacturer][article].push(answer.Products[i]);
					
					//Для учетного объекта - указываем, что товар находится в списке запрошенных
					AID_Object.isRequired 				= true;//Флаг - Запрошенный
					AID_Object.isSearchName 			= false;
					AID_Object.isQuickAnalogs 			= false;
					AID_Object.isAnalogs 				= false;
					AID_Object.isPossibleReplacement 	= false;
					AID_Object.isSpare_Box			 	= false;
					
				}
				else//Товар распределяем в Аналоги
				{
					if(epcSameArticle(article, search_object.article)){
						// Возможные замены
						
						//Если такого производителя еще не было - создаем для него ячейку
							if(Products.PossibleReplacement.Products.Manufacturers[manufacturer] == undefined)
							{
								Products.PossibleReplacement.Products.Manufacturers[manufacturer] = new Object;
							}
							//Если такого артикула еще не было у данного производителя
							if(Products.PossibleReplacement.Products.Manufacturers[manufacturer][article] == undefined)
							{
								Products.PossibleReplacement.Products.Manufacturers[manufacturer][article] = new Array();//Ячейка для объектов товаров
								
								//Создаем новый тип товара и добавляем его в список типов
								var ProductType = new Object;
								ProductType.manufacturer = manufacturer;
								ProductType.article = article;
								ProductType.name = name;
								ProductType.article_show = article_show;
								ProductType.exist = exist;
								ProductType.time_to_exe = time_to_exe;
								ProductType.price = price;
								ProductType.storage = storage;
								
								Products.PossibleReplacement.ProductsTypes.push(ProductType);
							}
							
							//Добавляем сам объект товара
							Products.PossibleReplacement.Products.Manufacturers[manufacturer][article].push(answer.Products[i]);
							
							//Для учетного объекта - указываем, что товар находится в списке аналогов
							AID_Object.isRequired 				= false;
							AID_Object.isSearchName 			= false;
							AID_Object.isQuickAnalogs 			= false;
							AID_Object.isAnalogs 				= false;
							AID_Object.isPossibleReplacement 	= true;
							AID_Object.isSpare_Box	 			= false;
							
					}else{
						// Аналоги делем на 2 категории:
						// - Те что со сроком 0-2 дня
						// - Остальные
						
						if(time_to_exe <= group_day){
							// быстрые аналоги
							//Если такого производителя еще не было - создаем для него ячейку
							if(Products.Quick_Analogs.Products.Manufacturers[manufacturer] == undefined)
							{
								Products.Quick_Analogs.Products.Manufacturers[manufacturer] = new Object;
							}
							//Если такого артикула еще не было у данного производителя
							if(Products.Quick_Analogs.Products.Manufacturers[manufacturer][article] == undefined)
							{
								Products.Quick_Analogs.Products.Manufacturers[manufacturer][article] = new Array();//Ячейка для объектов товаров
								
								//Создаем новый тип товара и добавляем его в список типов
								var ProductType = new Object;
								ProductType.manufacturer = manufacturer;
								ProductType.article = article;
								ProductType.name = name;
								ProductType.article_show = article_show;
								ProductType.exist = exist;
								ProductType.time_to_exe = time_to_exe;
								ProductType.price = price;
								ProductType.storage = storage;
								
								Products.Quick_Analogs.ProductsTypes.push(ProductType);
							}
							
							//Добавляем сам объект товара
							Products.Quick_Analogs.Products.Manufacturers[manufacturer][article].push(answer.Products[i]);
							
							//Для учетного объекта - указываем, что товар находится в списке аналогов
							AID_Object.isRequired 				= false;
							AID_Object.isSearchName 			= false;
							AID_Object.isQuickAnalogs 			= true;
							AID_Object.isAnalogs 				= false;
							AID_Object.isPossibleReplacement 	= false;
							AID_Object.isSpare_Box	 			= false;
							
						}else{
							
							// Остальные аналоги
							//Если такого производителя еще не было - создаем для него ячейку
							if(Products.Analogs.Products.Manufacturers[manufacturer] == undefined)
							{
								Products.Analogs.Products.Manufacturers[manufacturer] = new Object;
							}
							//Если такого артикула еще не было у данного производителя
							if(Products.Analogs.Products.Manufacturers[manufacturer][article] == undefined)
							{
								Products.Analogs.Products.Manufacturers[manufacturer][article] = new Array();//Ячейка для объектов товаров
								
								//Создаем новый тип товара и добавляем его в список типов
								var ProductType = new Object;
								ProductType.manufacturer = manufacturer;
								ProductType.article = article;
								ProductType.name = name;
								ProductType.article_show = article_show;
								ProductType.exist = exist;
								ProductType.time_to_exe = time_to_exe;
								ProductType.price = price;
								ProductType.storage = storage;
								
								Products.Analogs.ProductsTypes.push(ProductType);
							}
							
							//Добавляем сам объект товара
							Products.Analogs.Products.Manufacturers[manufacturer][article].push(answer.Products[i]);
							
							//Для учетного объекта - указываем, что товар находится в списке аналогов
							AID_Object.isRequired 				= false;
							AID_Object.isSearchName 			= false;						
							AID_Object.isQuickAnalogs 			= false;
							AID_Object.isAnalogs 				= true;
							AID_Object.isPossibleReplacement 	= false;
							AID_Object.isSpare_Box	 			= false;
						}
					}
				}
				
			}
		}
		
		
        //Добавляем учетный объект в список учетных объектов
        Products.All.push(AID_Object);
        
    }//for(i)
		
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




// ------------------------------------------------------------------------------------------------------------------------------




//Отображение/Переотображение результата запроса
function resultReview()
{
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi())
	{
		if(typeof epcSyncCrossReferenceGlobals === 'function')
		{
			epcSyncCrossReferenceGlobals();
		}
		if(typeof epcCrossExpandReferenceGraph === 'function')
		{
			epcCrossExpandReferenceGraph();
		}
		if(typeof epcCrossTryBindPendingStockForNewReferences === 'function')
		{
			epcCrossTryBindPendingStockForNewReferences();
		}
		if(typeof epcSyncChpuFilterManufacturers === 'function' && !window.epcChpuManufacturersFilterSynced)
		{
			epcSyncChpuFilterManufacturers();
			window.epcChpuManufacturersFilterSynced = true;
		}
	}
	//Общее содержимое
    var products_html = "";
	var epcChpuCrossStockRenderedKeys = {};
    
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
	if(flag_one == true){
		headlines.price.caption += " <img src=\"/content/files/images/asc.png\" />";
		headlines.price.subclass += " sorted";
	}else{
		//Ставим обозначение текущей внутренней сортировки
		if(
			outerSortState.field == innerSortState.field
		){
			headlines[innerSortState.field].caption += " <img src=\"/content/files/images/"+innerSortState.asc_desc+".png\" />";
			headlines[innerSortState.field].subclass += " sorted";
		}
		
		//Ставим обозначение текущей внешней сортировки, т.е. по полям Производитель, Артикул, Наименование
		if( (outerSortState.field != 'exist' && 
			outerSortState.field != 'time_to_exe' && 
			outerSortState.field != 'price') || outerSortState.field != innerSortState.field
		){
			headlines[outerSortState.field].caption += " <img src=\"/content/files/images/"+outerSortState.asc_desc+".png\" />";
			headlines[outerSortState.field].subclass += " sorted";
		}
	}
    
	
    var head_block = '<tr><th class="th_photo" title="Photo"></th><th class="th_manufacturer'+headlines.manufacturer.subclass+'" onclick="outerSortChange(\'manufacturer\');">'+headlines.manufacturer.caption+'</th><th class="th_article'+headlines.article.subclass+'" onclick="outerSortChange(\'article\');">'+headlines.article.caption+'</th><th class="th_name'+headlines.name.subclass+'" onclick="outerSortChange(\'name\');">'+headlines.name.caption+'</th><th class="th_exist'+headlines.exist.subclass+'" onclick="innerSortChange(\'exist\');">'+headlines.exist.caption+'</th><th class="th_time_to_exe'+headlines.time_to_exe.subclass+'" onclick="innerSortChange(\'time_to_exe\');">'+headlines.time_to_exe.caption+'</th><th class="th_info"><?php echo translate_str_by_id(4325); ?></th><th class="th_price'+headlines.price.subclass+'" onclick="innerSortChange(\'price\');">'+headlines.price.caption+'</th><th class="th_add_to_cart">Actions</th><th class="th_color"></th></tr>';
	window.epcLastProductsHeadBlock = head_block;
    
    
    //HTML для запрошенного артикула
    var required_block = "";
	var required_split = epcPartTypeCreateSplit();
	//HTML для найденных по наименованию
    var SearchName_block = "";
	//HTML для быстрых аналогов
	var quick_analogs_block = "";
	var quick_analogs_split = epcPartTypeCreateSplit();
    //HTML для аналогов
    var analogs_block = "";
	var analogs_split = epcPartTypeCreateSplit();
	//HTML для блока Возможные замены
	var PossibleReplacement_block = "";
	//HTML для дополнительного блока
	var Spare_Box_block = "";
	
	
	// Найденные бренды после фильтрации = сбрасываем список перед фильтрацией
	arr_manufacturers_posle_filter =  new Array();
	// Найденные склады после фильтрации
	arr_storages_posle_filter =  new Array();
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi())
	{
		if(window.epcChpuSkipFilterPreserveOnce)
		{
			window.epcChpuSkipFilterPreserveOnce = false;
		}
		else if(typeof epcBuildChpuFilterUiPreserve === 'function')
		{
			epcBuildChpuFilterUiPreserve();
		}
	}
	
	
	// Массив всех найденных позиций после фильтрации. Нужен для формирования фильтра
	var ALL_ProductsObjects = new Array;
    

	// Если текущий выбранный фильтр производители или склады то сбрасываем фильтры диапазонов на предыдущие значения, что бы можно было отменить фильтр убрав checkbox
	if((typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing) && (this_filter == 'manufacturer_blok' || this_filter == 'storages_blok' || this_filter == 'sam_price_time_blok')){
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
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi())
	{
		epcApplyChpuActiveFilterState();
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
	
	
	
	
	
	
	
	
	// Работаем с Запрошенным
	var requiredGroups = epcChpuGroupProductBlocks(Products.Required.ProductsTypes, Products.Required.Products.Manufacturers);
    for(var i=0; i < requiredGroups.length; i++)
    {
		var ProductsObjects = requiredGroups[i].products.slice();
		var RequiredProductType = requiredGroups[i].productType;

		
		// Фильтрация позиций
		ProductsObjects = epcApplyResultFilters(ProductsObjects);
		ProductsObjects = epcDedupeChpuProductsByBrandArticle(ProductsObjects);
		// Если установлены флаги самых быстрых и дешевых позиций в группе
		if(sam_price_time != ''){ProductsObjects = sam_price_time_fanc(ProductsObjects);}
	
		
		if(ProductsObjects.length > 0){
			// Общий массив позиций, по нему формируются новые значения фильтра
			ALL_ProductsObjects.push(ProductsObjects);
			for(var rp = 0; rp < ProductsObjects.length; rp++)
			{
				if(arr_manufacturers_posle_filter.indexOf(ProductsObjects[rp].manufacturer) === -1)
				{
					arr_manufacturers_posle_filter.push(ProductsObjects[rp].manufacturer);
				}
			}
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Required_count++;
			if(!epc_show_all_result_rows && (cnt_on_page + start_page_Required) <= Products_Required_Show_count ){continue;}
			Products_Required_Show_count++;
			
			// Формируем html позиций блока
			for(var p=0; p < ProductsObjects.length; p++){
				epcPartTypeAppendRow(required_split, getProductRecordHTML(ProductsObjects[p], p, ProductsObjects.length, RequiredProductType, 'required'), ProductsObjects[p].manufacturer, ProductsObjects[p].price);
			}
		}
    }// END Работаем с Запрошенным
    
	
	
	
    
	
	
	
	// Работаем с найденными по НАИМЕНОВАНИЮ
    for(var i=0; i < Products.SearchName.ProductsTypes.length; i++)
    {
		var Manufacturer = Products.SearchName.ProductsTypes[i].manufacturer;
        var Article = Products.SearchName.ProductsTypes[i].article;
        //Массив объектов товаров:
        var ProductsObjects = Products.SearchName.Products.Manufacturers[Manufacturer][Article];
		
		
		// Фильтрация позиций
		ProductsObjects = epcApplyResultFilters(ProductsObjects);
		// Если установлены флаги самых быстрых и дешевых позиций в группе
		if(sam_price_time != ''){ProductsObjects = sam_price_time_fanc(ProductsObjects);}
		
		
		if(ProductsObjects.length > 0){
			// Общий массив позиций, по нему формируются новые значения фильтра
			ALL_ProductsObjects.push(ProductsObjects);
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_SearchName_count++;
			if(!epc_show_all_result_rows && (cnt_on_page + start_page_SearchName) <= Products_SearchName_Show_count ){continue;}
			Products_SearchName_Show_count++;
			
			// Формируем html позиций блока
			for(var p=0; p < ProductsObjects.length; p++){
				SearchName_block += getProductRecordHTML(ProductsObjects[p], p, ProductsObjects.length, Products.SearchName.ProductsTypes[i], 'SearchName');
			}
		}
    }// END
	
	
	
	
	
	
	
	
	// Работаем с Быстрыми Аналогами
	var quickAnalogGroups = epcChpuGroupProductBlocks(Products.Quick_Analogs.ProductsTypes, Products.Quick_Analogs.Products.Manufacturers);
	for(var i=0; i < quickAnalogGroups.length; i++)
    {
		var ProductsObjects = quickAnalogGroups[i].products.slice();
		var QuickAnalogProductType = quickAnalogGroups[i].productType;
		
		// Фильтрация позиций
		ProductsObjects = epcApplyResultFilters(ProductsObjects);
		ProductsObjects = epcDedupeChpuProductsByBrandArticle(ProductsObjects);
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		{
			ProductsObjects = epcFilterChpuCrossStockNotYetRendered(ProductsObjects, epcChpuCrossStockRenderedKeys);
		}
		// Если установлены флаги самых быстрых и дешевых позиций в группе
		if(sam_price_time != ''){ProductsObjects = sam_price_time_fanc(ProductsObjects);}
		
		
		if(ProductsObjects.length > 0){
			// Общий массив позиций, по нему формируются новые значения фильтра
			ALL_ProductsObjects.push(ProductsObjects);
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Quick_Analogs_count++;
			if(!epc_show_all_result_rows && (cnt_on_page + start_page_Quick_Analogs) <= Products_Quick_Analogs_Show_count ){continue;}
			Products_Quick_Analogs_Show_count++;
			
			// Формируем html позиций блока
			for(var p=0; p < ProductsObjects.length; p++){
				epcPartTypeAppendRow(quick_analogs_split, getProductRecordHTML(ProductsObjects[p], p, ProductsObjects.length, QuickAnalogProductType, 'quick_analogs'), ProductsObjects[p].manufacturer, ProductsObjects[p].price);
			}
		}
    }// END Работаем с Быстрыми Аналогами
	
	
	
	
	
	
	
	
   // Работаем с Аналогами
	var analogGroups = epcChpuGroupProductBlocks(Products.Analogs.ProductsTypes, Products.Analogs.Products.Manufacturers);
    for(var i=0; i < analogGroups.length; i++)
    {
		var ProductsObjects = analogGroups[i].products.slice();
		var AnalogProductType = analogGroups[i].productType;
		
		// Фильтрация позиций
		ProductsObjects = epcApplyResultFilters(ProductsObjects);
		ProductsObjects = epcDedupeChpuProductsByBrandArticle(ProductsObjects);
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		{
			ProductsObjects = epcFilterChpuCrossStockNotYetRendered(ProductsObjects, epcChpuCrossStockRenderedKeys);
		}
		// Если установлены флаги самых быстрых и дешевых позиций в группе
		if(sam_price_time != ''){ProductsObjects = sam_price_time_fanc(ProductsObjects);}
		
		
		if(ProductsObjects.length > 0){
			// Общий массив позиций, по нему формируются новые значения фильтра
			ALL_ProductsObjects.push(ProductsObjects);
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Analogs_count++;
			if(!epc_show_all_result_rows && (cnt_on_page + start_page_Analogs) <= Products_Analogs_Show_count ){continue;}
			Products_Analogs_Show_count++;
			
			// Формируем html позиций блока
			for(var p=0; p < ProductsObjects.length; p++){
				epcPartTypeAppendRow(analogs_split, getProductRecordHTML(ProductsObjects[p], p, ProductsObjects.length, AnalogProductType, 'analogs'), ProductsObjects[p].manufacturer, ProductsObjects[p].price);
			}
		}
    }// END Работаем с Аналогами
	
	
	
	
	
	
	
	
	// Работаем с PossibleReplacement
    for(var i=0; i < Products.PossibleReplacement.ProductsTypes.length; i++)
    {
		var Manufacturer = Products.PossibleReplacement.ProductsTypes[i].manufacturer;
        var Article = Products.PossibleReplacement.ProductsTypes[i].article;
		//Массив объектов товаров:
        var ProductsObjects = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article];

		
		// Фильтрация позиций
		ProductsObjects = epcApplyResultFilters(ProductsObjects);
		// Если установлены флаги самых быстрых и дешевых позиций в группе
		if(sam_price_time != ''){ProductsObjects = sam_price_time_fanc(ProductsObjects);}
	
		
		if(ProductsObjects.length > 0){
			// Общий массив позиций, по нему формируются новые значения фильтра
			ALL_ProductsObjects.push(ProductsObjects);
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_PossibleReplacement_count++;
			if(!epc_show_all_result_rows && (cnt_on_page + start_page_PossibleReplacement) <= Products_PossibleReplacement_Show_count ){continue;}
			Products_PossibleReplacement_Show_count++;
			
			// Формируем html позиций блока
			for(var p=0; p < ProductsObjects.length; p++){
				PossibleReplacement_block += getProductRecordHTML(ProductsObjects[p], p, ProductsObjects.length, Products.PossibleReplacement.ProductsTypes[i], 'PossibleReplacement');
			}
		}
    }// END Работаем с PossibleReplacement
	
	
	
	
	
	
	
	
	// Работаем с Spare_Box
    for(var i=0; i < Products.Spare_Box.ProductsTypes.length; i++)
    {
		var Manufacturer = Products.Spare_Box.ProductsTypes[i].manufacturer;
        var Article = Products.Spare_Box.ProductsTypes[i].article;
		//Массив объектов товаров:
        var ProductsObjects = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article];

		
		// Фильтрация позиций
		ProductsObjects = epcApplyResultFilters(ProductsObjects);
		// Если установлены флаги самых быстрых и дешевых позиций в группе
		if(sam_price_time != ''){ProductsObjects = sam_price_time_fanc(ProductsObjects);}
	
		
		if(ProductsObjects.length > 0){
			// Общий массив позиций, по нему формируются новые значения фильтра
			ALL_ProductsObjects.push(ProductsObjects);
			
			// Если количество отображенных позиций больше или равно количеству которое мы можем отобразить то выходим из цикла что бы не показывать оставшиеся позиции
			Products_Spare_Box_count++;
			if(!epc_show_all_result_rows && (cnt_on_page + start_page_Spare_Box) <= Products_Spare_Box_Show_count ){continue;}
			Products_Spare_Box_Show_count++;
			
			// Формируем html позиций блока
			for(var p=0; p < ProductsObjects.length; p++){
				Spare_Box_block += getProductRecordHTML(ProductsObjects[p], p, ProductsObjects.length, Products.Spare_Box.ProductsTypes[i], 'Spare_Box');
			}
		}
    }// END Работаем с Spare_Box
	
	
	
	
	
	
	
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
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && ALL_ProductsObjects.length > 0)
	{
		arr_manufacturers_posle_filter = [];
		for(var ap = 0; ap < ALL_ProductsObjects.length; ap++)
		{
			var filteredBucket = ALL_ProductsObjects[ap];
			for(var bp = 0; bp < filteredBucket.length; bp++)
			{
				var filteredManufacturer = filteredBucket[bp].manufacturer;
				if(arr_manufacturers_posle_filter.indexOf(filteredManufacturer) === -1)
				{
					arr_manufacturers_posle_filter.push(filteredManufacturer);
				}
			}
        }
	}
	
	
	
	
	
	
	// Формируем HTML проценки
	if(epcPartTypeHasRows(required_split))
	{
		required_block = epcPartTypeComposeRows(required_split);
	}
	if(epcPartTypeHasRows(quick_analogs_split))
	{
		quick_analogs_block = epcPartTypeComposeRows(quick_analogs_split);
	}
	if(epcPartTypeHasRows(analogs_split))
	{
		analogs_block = epcPartTypeComposeRows(analogs_split);
	}
	
	if(required_block != ''){
		var urlBrandNotice = '';
		var urlBrand = (search_object.requested_manufacturer || SelectedManufacturer || '').toString().trim().toUpperCase();
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && urlBrand !== '' && Products_Required_count > 0)
		{
			var hasUrlBrandStock = false;
			for(var ri = 0; ri < Products.Required.ProductsTypes.length; ri++)
			{
				if(epcSameManufacturer(Products.Required.ProductsTypes[ri].manufacturer, urlBrand))
				{
					hasUrlBrandStock = true;
					break;
				}
			}
			if(!hasUrlBrandStock)
			{
				urlBrandNotice = '<tr><td colspan="10" style="padding:12px 10px; background:#fff8e6; border-bottom:1px solid #f0e4b5;"><div class="epc-brand-notice"><div class="epc-brand-notice__text"><i class="fa fa-info-circle"></i> No stock for <strong>' + urlBrand + '</strong> on this article. Showing available brands from UAE price lists.</div><button type="button" class="btn btn-sm btn-primary epc-btn-quote" onclick="epcAddManualToQuoteFromSearch();">Add to Quote</button></div></td></tr>';
			}
		}
		required_block = '<tr><td colspan="10" class="products_table_block_caption"><?php echo translate_str_by_id(4326); ?></td></tr>' + urlBrandNotice + required_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Required\');" id="next_page_Required"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}else{
		required_block = epcRequiredNotFoundCaptionHTML();
	}
	
	if(Products_Required_count > 0)
	{
		// CHPU: keep cross-reference stock visible together with the requested part number.
		if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
		{
			quick_analogs_block = "";
			analogs_block = "";
			PossibleReplacement_block = "";
			SearchName_block = "";
			Spare_Box_block = "";
		}
	}
	else
	{
		var hasAnyStockRows = (Products_Quick_Analogs_count + Products_Analogs_count + Products_PossibleReplacement_count + Products_SearchName_count + Products_Spare_Box_count) > 0;
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		{
			if(!hasAnyStockRows)
			{
				required_block = epcRequiredNotFoundCaptionHTML();
			}
		}
		else
		{
			var epc_cross_fallback_html = epcCrossFallbackHTML();
			if(
				epc_cross_fallback_html != '' &&
				!hasAnyStockRows
			)
			{
				required_block = '';
				quick_analogs_block = '';
				analogs_block = '';
				PossibleReplacement_block = epc_cross_fallback_html;
			}
		}
	}
	
	var epc_cross_not_in_stock_block = '';
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		var epc_cross_in_stock_split = epcPartTypeMergeSplits(quick_analogs_split, analogs_split);
		var epc_cross_in_stock_rows = epcPartTypeComposeRows(epc_cross_in_stock_split);
		if(epc_cross_in_stock_rows !== '')
		{
			epc_cross_in_stock_rows += '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Quick_Analogs\');" id="next_page_Quick_Analogs"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
			epc_cross_in_stock_rows += '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Analogs\');" id="next_page_Analogs"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
			quick_analogs_block = epcCrossInStockCaptionHTML('Cross references in stock (available in UAE warehouses)') + epc_cross_in_stock_rows;
			analogs_block = '';
		}
		else
		{
			quick_analogs_block = '';
			analogs_block = '';
		}
		epc_cross_not_in_stock_block = epcCrossRefsNotInStockRowsHTML();
	}
	else
	{
		if(quick_analogs_block != "")
		{
			quick_analogs_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="10" class="products_table_block_caption"><?php echo translate_str_by_id(4328); ?></td></tr>' + quick_analogs_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Quick_Analogs\');" id="next_page_Quick_Analogs"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
		}
	if(analogs_block != ''){
			analogs_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="10" class="products_table_block_caption"><?php echo translate_str_by_id(4329); ?></td></tr>' + analogs_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Analogs\');" id="next_page_Analogs"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
		}
	}
	
	if(SearchName_block != ''){
		SearchName_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="10" class="products_table_block_caption"><?php echo translate_str_by_id(4330); ?></td></tr>' + SearchName_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'SearchName\');" id="next_page_SearchName"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}
	
	if(PossibleReplacement_block != ''){
		if(PossibleReplacement_block.indexOf('epc-cross-fallback') === -1)
		{
			PossibleReplacement_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="10" class="products_table_block_caption"><?php echo translate_str_by_id(4331); ?></td></tr>' + PossibleReplacement_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'PossibleReplacement\');" id="next_page_PossibleReplacement"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
		}
	}
	
	if(Spare_Box_block != ''){
		Spare_Box_block = '<tr><td style="padding-top: 70px; border-top: 0;" colspan="10" class="products_table_block_caption"><?php echo translate_str_by_id(4660); ?></td></tr>' + Spare_Box_block + '<tr><td style="text-align:center; padding:0;" class="next_page_box" colspan="10"><a style="margin-top:15px; display:none;" class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="next_page(\'Spare_Box\');" id="next_page_Spare_Box"><i class="fa fa-arrow-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4127); ?></a></td></tr>';
	}
	
	// Оюъединяем блоки позиций
	products_html = required_block + PossibleReplacement_block + quick_analogs_block + analogs_block + SearchName_block + Spare_Box_block;
	if(typeof epc_cross_not_in_stock_block !== 'undefined' && epc_cross_not_in_stock_block !== '')
	{
		products_html += epc_cross_not_in_stock_block;
	}
	
	if(products_html != ''){
		
		let for_percentage = '';
		<?php
		if((int)$group_info['for_percentage'] === 1){
		?>
		let show_data_checked = '';
		if(flag_show_data){
			show_data_checked = 'checked';
		}
		for_percentage += '<div class="div_show_data_checked" style="text-align: right; position: relative;"><input '+show_data_checked+' style="margin: 0; padding: 0; margin-right: 0px; height: 20px; width: 20px; position: absolute; top: 1px; cursor: pointer; right: 0px;" type="checkbox" id="show_data" onChange="show_data();"/></div><div class="show_data_class" style="text-align: right; padding-right: 25px;"><select id="select_the_groups_margin" onChange="changing_the_groups_margin();" title="Pricing profile">';
			<?php
			// Pricing profiles only (Retail/Wholesale/CIS/GCC/…) — not ERP department roles.
			$epc_pricing_profiles = function_exists('epc_storefront_pricing_profile_groups')
				? epc_storefront_pricing_profile_groups($db_link)
				: array();
			$epc_selected_margin_id = isset($this_group_id_margin) ? (int)$this_group_id_margin : (int)$group_id;
			foreach($epc_pricing_profiles as $group_record){
				$selected = ((int)$group_record['id'] === $epc_selected_margin_id) ? 'selected=\"selected\"' : '';
				$option_label = translate_str_by_id($group_record['value']);
				$option_label = str_replace(array("\\", "'"), array("\\\\", "\\'"), $option_label);
				echo "for_percentage += '<option ".$selected." value=\"".(int)$group_record['id']."\">".$option_label."</option>';";
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
	if(this_filter != '' && products_html == '' && epcPartSearchShouldShowUnavailableNotice()){
		products_html = epcPartSearchNotAvailableHTML();
	}
	if(products_html === '' && !epcPartSearchHasAnyStockRows() && epcPartSearchShouldShowUnavailableNotice())
	{
		products_html = epcPartSearchNotAvailableHTML();
	}
	else if(epcPartSearchShouldShowUnavailableNotice() && !epcPartSearchHasAnyStockRows() && products_html !== '' && products_html.indexOf('epc-part-search-unavailable') === -1 && products_html.indexOf('all_table_products') === -1)
	{
		var unavailableNotice = epcPartSearchNotAvailableHTML();
		if(unavailableNotice !== '')
		{
			products_html = unavailableNotice + products_html;
		}
	}
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi() && typeof epcChpuBuildDirectStockTableHtml === 'function')
	{
		var hasTableShell = products_html.indexOf('all_table_products') !== -1;
		var hasStockMarkers = products_html.indexOf('td_exist') !== -1
			|| products_html.indexOf('epc-btn-cart') !== -1
			|| products_html.indexOf('epc-product-actions') !== -1
			|| products_html.indexOf('epc-ssr-warehouse') !== -1;
		var needsDirectStockTable = Products.All && Products.All.length > 0 && (!hasTableShell || !hasStockMarkers);
		if(needsDirectStockTable)
		{
			var directStockHtml = epcChpuBuildDirectStockTableHtml();
			if(directStockHtml !== '')
			{
				products_html = directStockHtml;
			}
		}
	}
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi())
	{
		window.epcChpuSkipFilterDomRead = false;
		window.epcChpuSkipResultFiltersOnce = false;
		window.epcChpuSkipFilterPreserveOnce = false;
		if(typeof epcChpuUpdateFilterRangesFromProducts === 'function')
		{
			epcChpuUpdateFilterRangesFromProducts();
		}
	}
    
	// Отображаем проценку
    document.getElementById("products_area").innerHTML = products_html;
	if(typeof epcBindSearchRowPhotoLoaders === 'function')
	{
		epcBindSearchRowPhotoLoaders(document.getElementById('products_area'));
	}
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi() && products_html.indexOf('Cross references — not found') === -1)
	{
		if(typeof epcChpuAppendCrossNotInStockToTable === 'function')
		{
			epcChpuAppendCrossNotInStockToTable();
		}
	}
	
	// Отображаем фильтр
	showPropertiesWidgets();
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi() && typeof epcApplyChpuActiveFilterState === 'function')
	{
		epcApplyChpuActiveFilterState();
	}
	
	// Работаем с кнопками "показать еще" вконце блоков
	var next_page_Required = document.getElementById('next_page_Required');
	var next_page_SearchName = document.getElementById('next_page_SearchName');
	var next_page_Quick_Analogs = document.getElementById('next_page_Quick_Analogs');
	var next_page_Analogs = document.getElementById('next_page_Analogs');
	var next_page_PossibleReplacement = document.getElementById('next_page_PossibleReplacement');
	var next_page_Spare_Box = document.getElementById('next_page_Spare_Box');
	
	if(epc_show_all_result_rows){
		['next_page_Required','next_page_SearchName','next_page_Quick_Analogs','next_page_Analogs','next_page_PossibleReplacement','next_page_Spare_Box'].forEach(function(nextId){
			var nextBtn = document.getElementById(nextId);
			if(nextBtn){
				nextBtn.style.display = 'none';
			}
		});
	}else{
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
	}
	
	
	
	show_pictures_products();// Отображаем миниатюры товаров
	epcExpandAllResultRows();

	<?php
	if((int)$group_info['for_percentage'] === 1){
	?>
	document.getElementById("select_the_groups_margin").value = this_group_id_margin;
	show_data();
	<?php
	}
	?>
}




// ------------------------------------------------------------------------------------------------------------------------------




// Единая функция формирования HTML-кода для одной записи товара.
function epcProductWarehouseCaption(Product)
{
	var label = (Product && Product.storage_caption) ? String(Product.storage_caption) : '';
	if(label === '' && typeof all_storages_info !== 'undefined' && Product && all_storages_info[Product.storage_id])
	{
		var info = all_storages_info[Product.storage_id] || {};
		label = String(info.name || info.short_name || info.full_name || '');
	}
	if(label === '' && typeof all_storages !== 'undefined' && Product && all_storages[Product.storage_id])
	{
		label = String(all_storages[Product.storage_id] || '');
	}
	return label;
}
function getProductRecordHTML(Product, index, quantity, ProductType, blok)
{
    var manufacturer = "", article_show = "", name = "";
	var isChpuWarehouseSubRow = (index > 0 && quantity > 1);
	var rowGroupKey = epcChpuBrandArticleGroupKey(ProductType.manufacturer || Product.manufacturer, ProductType.article || Product.article || Product.article_show) + "_" + blok;

	var time_to_exe = Product.time_to_exe;
	if(Product.time_to_exe != Product.time_to_exe_guaranteed)
	{
		time_to_exe = Product.time_to_exe + "-" + Product.time_to_exe_guaranteed +" <?php echo translate_str_by_id(4097); ?>.";
	}else{
		if(time_to_exe == 0)
		{
			time_to_exe = "<?php echo translate_str_by_id(4197); ?>";
			var warehouseTermLabel = epcProductWarehouseCaption(Product);
			if(warehouseTermLabel !== '')
			{
				time_to_exe += ' · ' + warehouseTermLabel;
			}
		}else{
			time_to_exe = time_to_exe + " <?php echo translate_str_by_id(4097); ?>.";
		}
	}
	time_to_exe = epcStorefrontSensitiveCellHTML(time_to_exe);
    var color = Product.color;
    
	
	
	
	//Строка для показа цены (base shop currency in data-epc-base-price; display converts only)
	var priceValue = (typeof epcFormatMoney === 'function') ? epcFormatMoney(Product.price) : digit(Product.price);
	var price = '<span class="epc-price-value" data-epc-base-price="' + String(Number(Product.price) || 0) + '">' + priceValue + '</span>';
	price = epcStorefrontPriceCellHTML(price, Product);
	
	
	
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
	
    info += "<a title=\"Фотографии товара\" href=\"https://www.google.ru/search?q="+encodeURIComponent(ProductType.manufacturer)+"+"+ProductType.article+"&newwindow=1&biw=1366&bih=667&tbm=isch&tbo=u&source=univ&sa=X&ved=0CC8QsARqFQoTCMDCoO70jMkCFQGFLAodrT0GFw\" target=\"_blank\"><span><i style=\"font-size: .8em;\" class=\"fa fa-camera\"></i></span></a>";
	
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
        info += "<a title=\"<?php echo translate_str_by_id(4334); ?>\" href=\"<?php echo $multilang_params['lang_href']; ?>"+Product.url+"\" target=\"_blank\"><i class=\"fa fa-file-image-o\"></i></a>";
    }
	
	info = '<span class="info_box">'+ info +'</span>';
	
	<?php
	if((int)$group_info['for_percentage'] === 1){
	?>
	info += "<div class='show_data_class' style=\"margin: 3px 0px; white-space: nowrap; max-width: 80px; font-size: 11px; position: relative;\" title=\""+ Product.storage_caption +"\"><span style=\"overflow: hidden; text-overflow: ellipsis;\" class=\"info_box\">"+ Product.storage_caption +"</span></div>";
	<?php
	}
	?>
	// Always show warehouse name on storefront results (not only CHPU pages)
	var epcWarehouseLabel = epcProductWarehouseCaption(Product);
	if(epcWarehouseLabel !== '' && !isChpuWarehouseSubRow)
	{
		info += "<div class='show_data_class epc-warehouse-label' style=\"margin: 3px 0px; white-space: nowrap; max-width: 110px; font-size: 11px; font-weight: 600;\" title=\"Warehouse\"><span class=\"info_box\">"+ epcWarehouseLabel +"</span></div>";
	}
    
	info = '<div style="margin: 3px 0px;">'+ info +'</div>';
	info = epcStorefrontSensitiveCellHTML(info);
	
	
	
    //Формирование колонки "Наличие"
    //Объект с описанием наличия - для отображения в информационном окне
    var supply_info_json = "{&quot;exist&quot;:"+Product.exist+",&quot;time_to_exe&quot;:"+Product.time_to_exe+",&quot;time_to_exe_guaranteed&quot;:"+Product.time_to_exe_guaranteed+",&quot;probability&quot;:"+Product.probability+"}";
	if(typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible)
	{
		supply_info_json = "{&quot;exist&quot;:0,&quot;time_to_exe&quot;:0,&quot;time_to_exe_guaranteed&quot;:0,&quot;probability&quot;:0}";
	}
    //Колонка
    var exist = "<span onclick=\"openInfoWindow(null, null, 1, '"+supply_info_json+"');\">" + Product.exist + "<img src=\"/lib/TreelaxCharts/sectors.php?number=2&value0="+Product.probability+"&value1="+(100-Product.probability)+"&start_angle=30&size=50&inside_size=1&slope=1.1\" /></span>";
	exist = epcStorefrontSensitiveCellHTML(exist);
    
	
    
	
    // Сворачиваем строки, если их больше чем указано в настройках для блока
	var rowClassExtra = isChpuWarehouseSubRow ? ' epc-warehouse-subrow' : '';
	var subRowClass = rowClassExtra.trim();
	var storageInfo = (typeof all_storages_info !== 'undefined' && all_storages_info && all_storages_info[Product.storage_id])
		? all_storages_info[Product.storage_id]
		: null;
	var useStorageBg = !!(storageInfo && parseInt(storageInfo.bg_line_color, 10) === 1);
	if(useStorageBg){
		var start_wrap_div = "<tr class=\""+subRowClass+"\" style='background:"+color+";'>";
	}else{
		var start_wrap_div = "<tr class=\""+subRowClass+"\">";
	}
    var end_wrap_div = "</tr>";
    var show_hide_button = "";//Кнопка "Показать/Скрыть"
	var tmp_cnt_to_hide = cnt_to_hide;
    if(index >= tmp_cnt_to_hide)
    {
        //В зависимости от текущего состояния - задаем значение атрибута style:
        var row_style = "";
        if(wrap_states[wrap_blocks_assoc[rowGroupKey]] == false)
        {
            row_style = "display:none;";
        }
		
		if(useStorageBg){
			start_wrap_div = "<tr style=\""+row_style+" background:"+color+";\" class=\"hide_row hide_row_"+wrap_blocks_assoc[rowGroupKey]+(subRowClass ? ' '+subRowClass : '')+"\">";
		}else{
			start_wrap_div = "<tr style=\""+row_style+"\" class=\"hide_row hide_row_"+wrap_blocks_assoc[rowGroupKey]+(subRowClass ? ' '+subRowClass : '')+"\">";
		}
    }else{
		if(index > 0){
			if(useStorageBg){
				start_wrap_div = "<tr style='background:"+color+";' class=\"hide_row"+(subRowClass ? ' '+subRowClass : '')+"\">";
			}else{
				start_wrap_div = "<tr class=\"hide_row"+(subRowClass ? ' '+subRowClass : '')+"\">";
			}
		}
	}
   
   
    if(index == 0 && quantity > tmp_cnt_to_hide)//Если это первый элемент, но не единственный - приделываем кнопку Показать/Скрыть. Работаем с индексным списком для таких блоков
    {
        //Добавляем ID
        if(wrap_blocks_assoc[rowGroupKey] == undefined)
        {
            wrap_blocks_index[wrap_blocks_index.length] = rowGroupKey;
            wrap_blocks_assoc[rowGroupKey] = wrap_blocks_index.length-1;
            wrap_states[wrap_blocks_index.length-1] = true;// Expanded by default — all warehouse rows visible
        }
        
        var show_hide_text = "<?php echo translate_str_by_id(4127); ?>";
        if(wrap_states[wrap_blocks_assoc[rowGroupKey]] == true)
        {
            show_hide_text = "<?php echo translate_str_by_id(4131); ?>";
        }
        
        show_hide_button = "<div class=\"show_hide_button\" onclick=\"show_hide_block("+wrap_blocks_assoc[rowGroupKey]+", false);\"><span style=\"line-height:1.4em;\" id=\"show_hide_button_"+wrap_blocks_assoc[rowGroupKey]+"\">"+show_hide_text+"</span></div>";
    }
    
    
	
	
    //Колонки Производитель, Артикул, Наименование
	var rowBrand = ProductType.manufacturer || Product.manufacturer || '';
	var rowArticle = ProductType.article_show || ProductType.article || Product.article_show || Product.article || '';
    if(index == 0)
    {
		manufacturer = '<span title="'+ rowBrand +'">'+ rowBrand +'</span>';
        article_show = "<a title='<?php echo translate_str_by_id(4335); ?>: "+ rowArticle +"' class=\"bread_crumbs_a\" style=\"text-decoration:underline; color:#000; font-weight:700;\" href=\""+ (typeof epcChpuBrandArticleUrl === 'function' ? epcCrossEsc(epcChpuBrandArticleUrl(rowBrand, Product.article || ProductType.article)) : ('<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article=' + encodeURIComponent(Product.article || ProductType.article))) +"\">"+ rowArticle +"</a>";
        name = "<span title=\""+Product.name+"\">"+ProductType.name+"</span>";
    }
	else if(isChpuWarehouseSubRow)
	{
		var subBrand = Product.manufacturer || rowBrand || '';
		var subArticle = Product.article_show || Product.article || rowArticle || '';
		var subName = Product.name || ProductType.name || '';
		var subWarehouse = epcProductWarehouseCaption(Product);
		manufacturer = '<span title="'+ subBrand +'">'+ subBrand +'</span>';
		if(subWarehouse !== '')
		{
			manufacturer += '<div class="epc-warehouse-subrow__label" title="Warehouse offer"><i class="fa fa-level-up fa-rotate-90" aria-hidden="true"></i> '+ subWarehouse +'</div>';
		}
		article_show = "<a title='<?php echo translate_str_by_id(4335); ?>: "+ subArticle +"' class=\"bread_crumbs_a\" style=\"text-decoration:underline; color:#000; font-weight:700;\" href=\""+ (typeof epcChpuBrandArticleUrl === 'function' ? epcCrossEsc(epcChpuBrandArticleUrl(subBrand, Product.article || subArticle)) : ('<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article=' + encodeURIComponent(Product.article || subArticle))) +"\">"+ subArticle +"</a>";
		name = "<span title=\""+subName+"\">"+subName+"</span>";
	}
    
	
	
	
	// Кнопки увеличения количества товара добавляемого в корзину //////////////////////////////////////////////////////////////////////////////////////////////
	var p_min_order = Product.min_order * 1;
	var p_exist = Product.exist * 1;
	if(isNaN(p_exist) || p_exist < 0){ p_exist = 0; }
	
	if(p_min_order == 0){
		p_min_order = 1;
	}
	// Pass real stock qty (0 = out of stock → Quote only, no Cart).
	cart_html = epcProductActionsHTML(
		Product.aid,
		p_exist,
		p_min_order,
		p_exist > 0 ? 'cart_only' : 'quote_only',
		Product.manufacturer || (typeof ProductType !== 'undefined' ? ProductType.manufacturer : '') || '',
		Product.article || Product.article_show || (typeof ProductType !== 'undefined' ? (ProductType.article || ProductType.article_show) : '') || ''
	);
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
	
	if(!isChpuWarehouseSubRow)
	{
		name = "<span title=\""+Product.name+"\">"+Product.name+"</span>";
	}
	
	// <!-------------------------------------------- End Картинки в проценке ---------------------------------------->
	
	// Товары Б\У - Добавляем картинку товара, если она есть
	if(!isChpuWarehouseSubRow && typeof Product.json_params == "string"){
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
	
	
    var rowArticleRaw = Product.article || ProductType.article || '';
	var photoCell = epcSearchRowPhotoCellHTML(rowBrand, rowArticleRaw, index);
    return start_wrap_div + photoCell + '<td class="td_manufacturer">'+ manufacturer + show_hide_button +'</td><td class="td_article">'+ article_show +'</td><td class="td_name">'+ name +'</td><td class="td_exist">'+ exist +'</td><td class="td_time_to_exe"><span onclick="openInfoWindow(null, null, 1, \''+ supply_info_json +'\');">'+ time_to_exe +'</span></td><td class="td_info">'+ info +'</td><td class="td_price">'+ price +'</td><td class="td_add_to_cart">'+ cart_html +'</td>'+ '<td class="td_color" style="background:'+color+'"></td>' + end_wrap_div;
}




// ------------------------------------------------------------------------------------------------------------------------------




//Индексный и ассоциативный массивы для блоков скрытия "Остальных товаров"
var wrap_blocks_assoc = new Array();//Производитель+Артикул_+blok указывает на индекс
var wrap_blocks_index = new Array();//Индекс указывает на Производитель+Артикул_+blok
var wrap_states = new Array();//Индекс указывает на флаг Открыт(true)/Закрыт(false)
function epcExpandAllResultRows()
{
	var i;
	for(i = 0; i < wrap_blocks_index.length; i++)
	{
		if(wrap_states[i] === false && typeof show_hide_block === 'function')
		{
			show_hide_block(i, true);
		}
	}
}
//Скрываем / Открываем блок. immediately - флаг. Скрыть сразу, т.е. без анимации
function show_hide_block(id, immediately)
{
    var show_hide_button = document.getElementById("show_hide_button_"+id);
    
	if(show_hide_button == undefined){
		return;
	}
	
    //Обращаем состояние блока
    if(wrap_states[id] == false)
    {
        //Открываем
        wrap_states[id] = true;
        if(immediately)
        {
			$(".hide_row_"+id).css('display', 'table-row');
        }
        else
        {
            $(".hide_row_"+id).show("slow");
        }
        show_hide_button.innerHTML = "<?php echo translate_str_by_id(4131); ?>";
    }
    else//Скрываем
    {
        wrap_states[id] = false;
        if(immediately)
        {
            $(".hide_row_"+id).css('display', 'none');
        }
        else
        {
            $(".hide_row_"+id).hide(300);
        }
        show_hide_button.innerHTML = "<?php echo translate_str_by_id(4127); ?>";
    }
}




// ------------------------------------------------------------------------------------------------------------------------------




//ВНУТРЕННЯЯ СОРТИРОВКА
var innerSortState = new Object;//Объект описания внутренней сортировки
innerSortState.field = 'price';
innerSortState.asc_desc = 'asc';
max_time_to_exe = <?=(int)$DP_Config->max_time_to_exe?>;
function innerSort()
{
    //Сортируем каждый набор видов товаров по полю (Запрошенный)
    for(var i=0; i < Products.Required.ProductsTypes.length; i++)
    {
        var Manufacturer = Products.Required.ProductsTypes[i].manufacturer;
        var Article = Products.Required.ProductsTypes[i].article;
        // Сортируем группу позиций
        Products.Required.Products.Manufacturers[Manufacturer][Article].sort(compareNumbers);
		
		// Для того что бы показать товары с доставкой 0-2 дня - вверху списка, после сортировки позиций по цене сформируем новый массив продуктов куда в начало запишем позиции с быстрой доставкой а затем остальные, таким образом они так же останутся отсортированными по цене
		if(flag_time_head && flag_one && innerSortState.field !== 'price'){
			// новый массив с запрошенным артикулом
			var array_products = new Array();
			
			//Массив объектов товаров:
			var ProductsObjects = Products.Required.Products.Manufacturers[Manufacturer][Article];
			
			// Сначала добавляем в массив товары с доставкой 0-2 дн.
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe <= max_time_to_exe && ProductsObjects[p].time_to_exe_guaranteed <= max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Теперь добавляем в массив все остальные товары
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe > max_time_to_exe || ProductsObjects[p].time_to_exe_guaranteed > max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Сохраняем
			Products.Required.Products.Manufacturers[Manufacturer][Article] = array_products;
		}
		
		// После того как отсортировали позиции внутри группы, нужно отсортировать сами группы
		// Записываем текущие значения свойств первой позиции в группе что затем произвести внешнею сортировку
		var exist = Products.Required.Products.Manufacturers[Manufacturer][Article][0].exist * 1;
		var time_to_exe = Products.Required.Products.Manufacturers[Manufacturer][Article][0].time_to_exe * 1;
		var price = Products.Required.Products.Manufacturers[Manufacturer][Article][0].price * 1;
		
		Products.Required.ProductsTypes[i].exist = exist;
		Products.Required.ProductsTypes[i].time_to_exe = time_to_exe;
		Products.Required.ProductsTypes[i].price = price;
    }
	
	
	
	////////////////////////////////////////////////////////////////////////////
	
	
	
	//Сортируем каждый набор видов товаров в группе Найденных по наименованию
    for(var i=0; i < Products.SearchName.ProductsTypes.length; i++)
    {
        var Manufacturer = Products.SearchName.ProductsTypes[i].manufacturer;
        var Article = Products.SearchName.ProductsTypes[i].article;
        // Сортируем группу позиций
        Products.SearchName.Products.Manufacturers[Manufacturer][Article].sort(compareNumbers);
		
		// После того как отсортировали позиции внутри группы, нужно отсортировать сами группы
		// Записываем текущие значения свойств первой позиции в группе что затем произвести внешнею сортировку
		var exist = Products.SearchName.Products.Manufacturers[Manufacturer][Article][0].exist * 1;
		var time_to_exe = Products.SearchName.Products.Manufacturers[Manufacturer][Article][0].time_to_exe * 1;
		var price = Products.SearchName.Products.Manufacturers[Manufacturer][Article][0].price * 1;
		
		Products.SearchName.ProductsTypes[i].exist = exist;
		Products.SearchName.ProductsTypes[i].time_to_exe = time_to_exe;
		Products.SearchName.ProductsTypes[i].price = price;
    }
	
	
	
	////////////////////////////////////////////////////////////////////////////
	
	
	
	//Сортируем каждый набор видов товаров по полю (Быстрые Аналоги)
    for(var i=0; i < Products.Quick_Analogs.ProductsTypes.length; i++)
    {
        var Manufacturer = Products.Quick_Analogs.ProductsTypes[i].manufacturer;
        var Article = Products.Quick_Analogs.ProductsTypes[i].article;
        Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article].sort(compareNumbers);
		
		// Для того что бы показать товары с доставкой 0-2 дня - вверху списка, после сортировки позиций по цене сформируем новый массив продуктов куда в начало запишем позиции с быстрой доставкой а затем остальные, таким образом они так же останутся отсортированными по цене
		if(flag_time_head && flag_one && innerSortState.field !== 'price'){
			// новый массив с запрошенным артикулом
			var array_products = new Array();
			
			//Массив объектов товаров:
			var ProductsObjects = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article];
			
			// Сначала добавляем в массив товары с доставкой 0-2 дн.
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe <= max_time_to_exe && ProductsObjects[p].time_to_exe_guaranteed <= max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Теперь добавляем в массив все остальные товары
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe > max_time_to_exe || ProductsObjects[p].time_to_exe_guaranteed > max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Сохраняем
			Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article] = array_products;
		}
		
		// После того как отсортировали позиции внутри группы, нужно отсортировать сами группы
		// Записываем текущие значения свойств первой позиции в группе что затем произвести внешнею сортировку
		var exist = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article][0].exist * 1;
		var time_to_exe = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article][0].time_to_exe * 1;
		var price = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article][0].price * 1;
		
		Products.Quick_Analogs.ProductsTypes[i].exist = exist;
		Products.Quick_Analogs.ProductsTypes[i].time_to_exe = time_to_exe;
		Products.Quick_Analogs.ProductsTypes[i].price = price;
    }
    
	
	
	////////////////////////////////////////////////////////////////////////////
	
	
	
    //Сортируем каждый набор видов товаров по полю (Аналоги)
	for(var i=0; i < Products.Analogs.ProductsTypes.length; i++)
    {
        var Manufacturer = Products.Analogs.ProductsTypes[i].manufacturer;
        var Article = Products.Analogs.ProductsTypes[i].article;
        Products.Analogs.Products.Manufacturers[Manufacturer][Article].sort(compareNumbers);
		
		// Для того что бы показать товары с доставкой 0-2 дня - вверху списка, после сортировки позиций по цене сформируем новый массив продуктов куда в начало запишем позиции с быстрой доставкой а затем остальные, таким образом они так же останутся отсортированными по цене
		if(flag_time_head && flag_one && innerSortState.field !== 'price'){
			// новый массив с запрошенным артикулом
			var array_products = new Array();
			
			//Массив объектов товаров:
			var ProductsObjects = Products.Analogs.Products.Manufacturers[Manufacturer][Article];
			
			// Сначала добавляем в массив товары с доставкой 0-2 дн.
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe <= max_time_to_exe && ProductsObjects[p].time_to_exe_guaranteed <= max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Теперь добавляем в массив все остальные товары
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe > max_time_to_exe || ProductsObjects[p].time_to_exe_guaranteed > max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Сохраняем
			Products.Analogs.Products.Manufacturers[Manufacturer][Article] = array_products;
		}
		
		// После того как отсортировали позиции внутри группы, нужно отсортировать сами группы
		// Записываем текущие значения свойств первой позиции в группе что затем произвести внешнею сортировку
		var exist = Products.Analogs.Products.Manufacturers[Manufacturer][Article][0].exist * 1;
		var time_to_exe = Products.Analogs.Products.Manufacturers[Manufacturer][Article][0].time_to_exe * 1;
		var price = Products.Analogs.Products.Manufacturers[Manufacturer][Article][0].price * 1;
		
		Products.Analogs.ProductsTypes[i].exist = exist;
		Products.Analogs.ProductsTypes[i].time_to_exe = time_to_exe;
		Products.Analogs.ProductsTypes[i].price = price;
    }
	
	
	
	////////////////////////////////////////////////////////////////////////////
	
	
	
	//Сортируем каждый набор видов товаров по полю (PossibleReplacement)
    for(var i=0; i < Products.PossibleReplacement.ProductsTypes.length; i++)
    {
        var Manufacturer = Products.PossibleReplacement.ProductsTypes[i].manufacturer;
        var Article = Products.PossibleReplacement.ProductsTypes[i].article;
        // Сортируем группу позиций
        Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article].sort(compareNumbers);
		
		// Для того что бы показать товары с доставкой 0-2 дня - вверху списка, после сортировки позиций по цене сформируем новый массив продуктов куда в начало запишем позиции с быстрой доставкой а затем остальные, таким образом они так же останутся отсортированными по цене
		if(flag_time_head && flag_one && innerSortState.field !== 'price'){
			// новый массив с запрошенным артикулом
			var array_products = new Array();
			
			//Массив объектов товаров:
			var ProductsObjects = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article];
			
			// Сначала добавляем в массив товары с доставкой 0-2 дн.
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe <= max_time_to_exe && ProductsObjects[p].time_to_exe_guaranteed <= max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Теперь добавляем в массив все остальные товары
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe > max_time_to_exe || ProductsObjects[p].time_to_exe_guaranteed > max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Сохраняем
			Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article] = array_products;
		}
		
		// После того как отсортировали позиции внутри группы, нужно отсортировать сами группы
		// Записываем текущие значения свойств первой позиции в группе что затем произвести внешнею сортировку
		var exist = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article][0].exist * 1;
		var time_to_exe = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article][0].time_to_exe * 1;
		var price = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article][0].price * 1;
		
		Products.PossibleReplacement.ProductsTypes[i].exist = exist;
		Products.PossibleReplacement.ProductsTypes[i].time_to_exe = time_to_exe;
		Products.PossibleReplacement.ProductsTypes[i].price = price;
    }
	
	
	
	////////////////////////////////////////////////////////////////////////////
	
	
	
	//Сортируем каждый набор видов товаров по полю (Spare_Box)
    for(var i=0; i < Products.Spare_Box.ProductsTypes.length; i++)
    {
        var Manufacturer = Products.Spare_Box.ProductsTypes[i].manufacturer;
        var Article = Products.Spare_Box.ProductsTypes[i].article;
        // Сортируем группу позиций
        Products.Spare_Box.Products.Manufacturers[Manufacturer][Article].sort(compareNumbers);
		

		// Для того что бы показать товары с доставкой 0-2 дня - вверху списка, после сортировки позиций по цене сформируем новый массив продуктов куда в начало запишем позиции с быстрой доставкой а затем остальные, таким образом они так же останутся отсортированными по цене
		if(flag_time_head && flag_one && innerSortState.field !== 'price'){
			// новый массив с запрошенным артикулом
			var array_products = new Array();
			
			//Массив объектов товаров:
			var ProductsObjects = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article];
			
			// Сначала добавляем в массив товары с доставкой 0-2 дн.
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe <= max_time_to_exe && ProductsObjects[p].time_to_exe_guaranteed <= max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Теперь добавляем в массив все остальные товары
			for(var p=0; p < ProductsObjects.length; p++)
			{
				if(ProductsObjects[p].time_to_exe > max_time_to_exe || ProductsObjects[p].time_to_exe_guaranteed > max_time_to_exe)
				{
					array_products.push(ProductsObjects[p]);
				}
			}
			
			// Сохраняем
			Products.Spare_Box.Products.Manufacturers[Manufacturer][Article] = array_products;
		}
		
		// После того как отсортировали позиции внутри группы, нужно отсортировать сами группы
		// Записываем текущие значения свойств первой позиции в группе что затем произвести внешнею сортировку
		var exist = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article][0].exist * 1;
		var time_to_exe = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article][0].time_to_exe * 1;
		var price = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article][0].price * 1;
		
		Products.Spare_Box.ProductsTypes[i].exist = exist;
		Products.Spare_Box.ProductsTypes[i].time_to_exe = time_to_exe;
		Products.Spare_Box.ProductsTypes[i].price = price;
    }
	
	
	
	////////////////////////////////////////////////////////////////////////////
	
	
	
	// Производим внешнюю сортировку с учетом внутренней что бы отсортировать группы
	if(flag_one == true){
		outerSortState.field = 'price';
		outerSortState.asc_desc = 'asc';
		innerSortState.field = 'price';
		innerSortState.asc_desc = 'asc';
	}else{
		outerSortState.field = innerSortState.field;
	}
	outerSortState.asc_desc = innerSortState.asc_desc;

	outerSort();
}




// ------------------------------------------------------------------------------------------------------------------------------




// Сортировка числительных значений внутри группы
function compareNumbers(x, y)
{
    if(innerSortState.asc_desc == "asc")
    {
        if(parseFloat(x[innerSortState.field]) > parseFloat(y[innerSortState.field]))
        {
            return 1;
        }
        else if(parseFloat(x[innerSortState.field]) < parseFloat(y[innerSortState.field]))
        {
            return -1;
        }
        else
        {
            // При равных значениях сортируем либо по цене либо по сроку
			if(innerSortState.field == 'price'){
				if(parseFloat(x['time_to_exe']) > parseFloat(y['time_to_exe']))
				{
					return 1;
				}
				else if(parseFloat(x['time_to_exe']) < parseFloat(y['time_to_exe']))
				{
					return -1;
				}else{
					return 0;
				}
			}else{
				if(parseFloat(x['price']) > parseFloat(y['price']))
				{
					return 1;
				}
				else if(parseFloat(x['price']) < parseFloat(y['price']))
				{
					return -1;
				}else{
					return 0;
				}
			}
        }
    }
    else
    {
        if(parseFloat(x[innerSortState.field]) < parseFloat(y[innerSortState.field]))
        {
            return 1;
        }
        else if(parseFloat(x[innerSortState.field]) > parseFloat(y[innerSortState.field]))
        {
            return -1;
        }
        else
        {
            // При равных значениях сортируем либо по цене либо по сроку
			if(innerSortState.field == 'price'){
					
				if(parseFloat(x['time_to_exe']) > parseFloat(y['time_to_exe']))
				{
					return 1;
				}
				else if(parseFloat(x['time_to_exe']) < parseFloat(y['time_to_exe']))
				{
					return -1;
				}
				else
				{
					return 0;
				}
				
			}else{
				
				if(parseFloat(x['price']) > parseFloat(y['price']))
				{
					return 1;
				}
				else if(parseFloat(x['price']) < parseFloat(y['price']))
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
}




// ------------------------------------------------------------------------------------------------------------------------------




// Сортировка числительных значений между группами (для внешней сортировки)
function compareNumbers_2(x, y)
{
	if(outerSortState.asc_desc == "asc")
    {
        if(parseFloat(x[outerSortState.field]) > parseFloat(y[outerSortState.field]))
        {
            return 1;
        }
        else if(parseFloat(x[outerSortState.field]) < parseFloat(y[outerSortState.field]))
        {
            return -1;
        }
        else
        {
            // При равных значениях сортируем либо по цене либо по сроку
			if(outerSortState.field == 'price'){
				if(parseFloat(x['time_to_exe']) > parseFloat(y['time_to_exe']))
				{
					return 1;
				}
				else if(parseFloat(x['time_to_exe']) < parseFloat(y['time_to_exe']))
				{
					return -1;
				}else{
					return 0;
				}
			}else{
				if(parseFloat(x['price']) > parseFloat(y['price']))
				{
					return 1;
				}
				else if(parseFloat(x['price']) < parseFloat(y['price']))
				{
					return -1;
				}else{
					return 0;
				}
			}
        }
    }
    else
    {
        if(parseFloat(x[outerSortState.field]) < parseFloat(y[outerSortState.field]))
        {
            return 1;
        }
        else if(parseFloat(x[outerSortState.field]) > parseFloat(y[outerSortState.field]))
        {
            return -1;
        }
        else
        {
            // При равных значениях сортируем либо по цене либо по сроку
			if(outerSortState.field == 'price'){
				
				if(parseFloat(x['time_to_exe']) > parseFloat(y['time_to_exe']))
				{
					return 1;
				}
				else if(parseFloat(x['time_to_exe']) < parseFloat(y['time_to_exe']))
				{
					return -1;
				}
				else
				{
					return 0;
				}
				
			}else{
				if(parseFloat(x['price']) > parseFloat(y['price']))
				{
					return 1;
				}
				else if(parseFloat(x['price']) < parseFloat(y['price']))
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
}




// ------------------------------------------------------------------------------------------------------------------------------




//Смена состояния внутренней сортировки
function innerSortChange(field)
{
	if(flag_one == true && field == 'price'){
		flag_one = false;
		innerSortState.asc_desc = "asc";
		innerSortState.field = field;
	}else{
		//Если тоже поле - меняем только направление
		if(innerSortState.field == field)
		{
			if(innerSortState.asc_desc == "asc")
			{
				innerSortState.asc_desc = "desc";
			}
			else
			{
				innerSortState.asc_desc = "asc";
			}
		}
		else//Если поле другое - ставим это поле и направление asc
		{
			flag_one = false;
			innerSortState.asc_desc = "asc";
			innerSortState.field = field;
		}
    }
   
    //Производим саму сортировку
    innerSort()
	
    //Обновляем отображние результата
	if(this_filter != ''){
		productsCountRequest(this_filter.replace('_blok', ''));
	}else{
		resultReview();
	}
}




// ------------------------------------------------------------------------------------------------------------------------------




//ВНЕШНЯЯ СОРТИРОВКА
var outerSortState = new Object;//Объект описания внешней сортировки
function outerSort()
{
	if(outerSortState.field == "exist" || outerSortState.field == "time_to_exe" || outerSortState.field == "price"){
		Products.Required.ProductsTypes.sort(compareNumbers_2);
		Products.SearchName.ProductsTypes.sort(compareNumbers_2);
		Products.Quick_Analogs.ProductsTypes.sort(compareNumbers_2);
		Products.Analogs.ProductsTypes.sort(compareNumbers_2);
		Products.PossibleReplacement.ProductsTypes.sort(compareNumbers_2);
		Products.Spare_Box.ProductsTypes.sort(compareNumbers_2);
	}else{
		Products.Required.ProductsTypes.sort(compareStrings);
		Products.SearchName.ProductsTypes.sort(compareStrings);
		Products.Quick_Analogs.ProductsTypes.sort(compareStrings);
		Products.Analogs.ProductsTypes.sort(compareStrings);
		Products.PossibleReplacement.ProductsTypes.sort(compareStrings);
		Products.Spare_Box.ProductsTypes.sort(compareStrings);
	}
	
	/*
	// При первоначальной загрузке аналоги сортируем по бренду
	if(flag_one == true){
		outerSortState.field = 'manufacturer';
		outerSortState.asc_desc = 'asc';
		Products.Analogs.ProductsTypes.sort(compareStrings);
	}
	if(flag_one == true){
		outerSortState.field = 'price';
		outerSortState.asc_desc = 'asc';
		Products.Quick_Analogs.ProductsTypes.sort(compareNumbers_2);
	}
	*/
}




// ------------------------------------------------------------------------------------------------------------------------------




//Функция сравнения строковых значений
function compareStrings(x, y)
{
    if(outerSortState.asc_desc == "asc")
    {
        if(String(x[outerSortState.field]) > String(y[outerSortState.field]))
        {
            return 1;
        }
        else if(String(x[outerSortState.field]) < String(y[outerSortState.field]))
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
        if(String(x[outerSortState.field]) < String(y[outerSortState.field]))
        {
            return 1;
        }
        else if(String(x[outerSortState.field]) > String(y[outerSortState.field]))
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }
}




// ------------------------------------------------------------------------------------------------------------------------------




//Смена ВНЕШНЕЙ сортировки
function outerSortChange(field)
{
    if(flag_one == true){
		flag_one = false;
		outerSortState.asc_desc = "asc";
		outerSortState.field = field;
	}else{
		//Если тоже поле - меняем только направление
		if(outerSortState.field == field)
		{
			if(outerSortState.asc_desc == "asc")
			{
				outerSortState.asc_desc = "desc";
			}
			else
			{
				outerSortState.asc_desc = "asc";
			}
		}
		else//Если поле другое - ставим это поле и направление asc
		{
			outerSortState.field = field;
			outerSortState.asc_desc = "asc";
		}
    }
	
    //Производим саму сортировку
    outerSort();
	
    //Обновляем отображние результата
	if(this_filter != ''){
		productsCountRequest(this_filter.replace('_blok', ''));
	}else{
		resultReview();
	}
}




// ------------------------------------------------------------------------------------------------------------------------------




//Добавление в корзину
function addToCart(aid)
{
	if(typeof epcStorefrontRequireLoginForCommerce === 'function' && !epcStorefrontRequireLoginForCommerce())
	{
		return;
	}
    //1. По списку учетных объектов определяем, в где находится объект товара (Запрошенные/Аналоги)
    var AID_Object = Products.All[aid];
	if(!AID_Object)
	{
		alert("Unable to add this item to cart. Please refresh the page and try again.");
		return;
	}
    
    //2. Получаем сам объект товара
    var Product = new Object;
    if(AID_Object.isRequired == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.Required.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Required.ProductsTypes[i].manufacturer;
            var Article = Products.Required.ProductsTypes[i].article;
        
            //Массив объектов товаров:
            var ProductsObjects = Products.Required.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isSearchName == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.SearchName.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.SearchName.ProductsTypes[i].manufacturer;
            var Article = Products.SearchName.ProductsTypes[i].article;
        
            //Массив объектов товаров:
            var ProductsObjects = Products.SearchName.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isAnalogs == true)
    {
        //Ищем объект товара в списке аналогов
        for(var i=0; i < Products.Analogs.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Analogs.ProductsTypes[i].manufacturer;
            var Article = Products.Analogs.ProductsTypes[i].article;
            
            //Массив объектов товаров:
            var ProductsObjects = Products.Analogs.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isQuickAnalogs == true)
	{
		//Ищем объект товара в списке быстрых аналогов
        for(var i=0; i < Products.Quick_Analogs.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Quick_Analogs.ProductsTypes[i].manufacturer;
            var Article = Products.Quick_Analogs.ProductsTypes[i].article;
            
            //Массив объектов товаров:
            var ProductsObjects = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
	}else if(AID_Object.isPossibleReplacement == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.PossibleReplacement.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.PossibleReplacement.ProductsTypes[i].manufacturer;
            var Article = Products.PossibleReplacement.ProductsTypes[i].article;
        
            //Массив объектов товаров:
            var ProductsObjects = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isSpare_Box == true)
    {
        //Ищем объект товара в списке запрошенных
        for(var i=0; i < Products.Spare_Box.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Spare_Box.ProductsTypes[i].manufacturer;
            var Article = Products.Spare_Box.ProductsTypes[i].article;
        
            //Массив объектов товаров:
            var ProductsObjects = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
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
    
	log('<?php echo translate_str_by_id(4661); ?>:');
	log(Product);
	
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
                if(answer.code == "auth")
                {
					if(answer.login_url){ window.location.href = answer.login_url; }
					else if(typeof epcStorefrontRequireLoginForCommerce === 'function'){ epcStorefrontRequireLoginForCommerce(); }
					return;
                }
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
	if(typeof epcStorefrontRequireLoginForCommerce === 'function' && !epcStorefrontRequireLoginForCommerce())
	{
		return;
	}
    var AID_Object = Products.All[aid];
    var Product = new Object;
    if(AID_Object.isRequired == true)
    {
        for(var i=0; i < Products.Required.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Required.ProductsTypes[i].manufacturer;
            var Article = Products.Required.ProductsTypes[i].article;
            var ProductsObjects = Products.Required.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isSearchName == true)
    {
        for(var i=0; i < Products.SearchName.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.SearchName.ProductsTypes[i].manufacturer;
            var Article = Products.SearchName.ProductsTypes[i].article;
            var ProductsObjects = Products.SearchName.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isAnalogs == true)
    {
        for(var i=0; i < Products.Analogs.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Analogs.ProductsTypes[i].manufacturer;
            var Article = Products.Analogs.ProductsTypes[i].article;
            var ProductsObjects = Products.Analogs.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isQuickAnalogs == true)
	{
        for(var i=0; i < Products.Quick_Analogs.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Quick_Analogs.ProductsTypes[i].manufacturer;
            var Article = Products.Quick_Analogs.ProductsTypes[i].article;
            var ProductsObjects = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
	}else if(AID_Object.isPossibleReplacement == true)
    {
        for(var i=0; i < Products.PossibleReplacement.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.PossibleReplacement.ProductsTypes[i].manufacturer;
            var Article = Products.PossibleReplacement.ProductsTypes[i].article;
            var ProductsObjects = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
            }
        }
    }else if(AID_Object.isSpare_Box == true)
    {
        for(var i=0; i < Products.Spare_Box.ProductsTypes.length; i++)
        {
            var Manufacturer = Products.Spare_Box.ProductsTypes[i].manufacturer;
            var Article = Products.Spare_Box.ProductsTypes[i].article;
            var ProductsObjects = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article];
            for(var p=0; p < ProductsObjects.length; p++)
            {
                if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
                {
					Product = Object.assign({}, ProductsObjects[p]);
                    break;
                }
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
					if(answer.login_url){ window.location.href = answer.login_url; }
					else if(typeof epcStorefrontRequireLoginForCommerce === 'function'){ epcStorefrontRequireLoginForCommerce(); }
					else { alert(answer.message || "Please sign in to use quotes."); }
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