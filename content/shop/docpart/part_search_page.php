<?php
//Скрипт страницы отображения результатов поиска товаров по артикулу
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';


// Legacy /shop/part_search?article=… redirects are handled in dp_core.php (302 to brand picker or single-brand CHPU).
// CHPU pages (/parts/brands/ARTICLE etc.) use service_data, not $_GET — never hard-exit those.
if( $DP_Config->chpu_search_config["chpu_search_on"] == true )
{
	if( isset($_GET["article"]) && empty($DP_Content->service_data['article_search_chpu']) )
	{
		$epcWhBrand = isset($_GET['brend']) ? trim((string) $_GET['brend']) : '';
		$epcWhArticle = trim((string) $_GET['article']);
		if ($epcWhBrand !== '' && $epcWhArticle !== ''
			&& function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname()) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/epc_spare_parts_warehouse.php';
			global $db_link;
			if ($db_link instanceof PDO) {
				$whHit = epc_spare_parts_warehouse_search($epcWhBrand, $epcWhArticle, $db_link, $DP_Config);
				if (!empty($whHit['ok']) && !empty($whHit['redirect_url'])) {
					header('Location: ' . (string) $whHit['redirect_url'], true, 302);
					exit;
				}
				if (!empty($whHit['ok']) && !empty($whHit['product_url'])) {
					header('Location: ' . (string) $whHit['product_url'], true, 302);
					exit;
				}
			}
		}
		if ($epcWhBrand === '') {
			exit;
		}
	}
}



// ------------------------------------------------------------------------------------------------
//Получаем артикул:
if( isset($DP_Content->service_data["article"]) )
{
	$article_input = $DP_Content->service_data["article"];
}
else
{
	$article_input = $_GET["article"];//Для сохранения совместимости со старым вариантом
}


//Если был передан производитель черех $_GET (только для старого варианта) - кодируем его. Если это ЧПУ-проценка (новый вариант), то, $_GET["brend"] ниже переинициализируется из переменной $manufacturer, которая уже закодирована
if( isset($_GET["brend"]) )
{
	$_GET["brend"] = htmlentities($_GET["brend"]);
}


//Тип поиска
$search_type = "no_chpu";//По умолчанию тип поиск - без ЧПУ, т.е. старый вариант
$manufacturer = '';
$use_selected_manufacturer = false;
if( isset($DP_Content->service_data["search_type"]) )
{
	//ЧПУ-поиск. Могут быть варианты: all_brands_by_article и prices_by_article_and_manufacturer
	$search_type = $DP_Content->service_data["search_type"];
}
//Производитель (при ЧПУ-поиске, если это второй шаг)
if( $search_type == 'prices_by_article_and_manufacturer' )
{
	//Производитель из URL
	$manufacturer = $DP_Content->service_data["manufacturer"];
	
	$use_selected_manufacturer = false;//Флаг - используем выбранного производителя из опций пользователя. В противном случае в переменную $_GET["brend"] подставляем производителя из URL - тогда алгоритм будет выполняться по старому варианту с аргументом $_GET["brend"], т.е. получение списка производителей и затем автоматический выбор.
	
	//Если опций пользователя нет в ЧПУ-втором шаге, это значит, что был переход на эту страницу по прямой ссылке.
	
	/*
	Далее - необходимо получить опции пользователя с выбором производителя.
	Если опции есть, то, делать запрос производителей уже не надо. Делаем сразу опрос поставщиков по ценам.
	Если опций нет, то дальнейшее выполнение скрипта полностью аналогично старому варианту с переданным аргументом $_GET["brend"], т.е. получение списка производителей и автоматический выбор одного из них, если такой есть в списке.*/
	$selected_manufacturer = DP_User::get_user_option_by_key("selected_manufacturer");
	if(!$selected_manufacturer)
	{
		$_GET["brend"] = $manufacturer;
	}
	else
	{
		//Есть какие-то опции - нужно проверить, соответствуют ли они производителю из URL.
		$selected_manufacturer = json_decode($selected_manufacturer, true);
		
		//Если 10 минут еще не истекли
		if( ( time() - (int)$selected_manufacturer["time"] ) < 600 )
		{
			//Если производитель в опциях соответствует тому, что указан в URL
			if( strtolower($selected_manufacturer["SelectedManufacturer"]) == strtolower(html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8')) )
			{
				$use_selected_manufacturer = true;//Используем производителя из опций пользователя, т.е. опрос поставщиков для получения списка производителей уже делать НЕ НАДО (все необходимые JavaScript-переменные будут инициализированы из опций пользователя)
				
				//И, удаляем опцию пользователя (на тот случай, если он в адресной строке заменит только артикул, а производителя оставит - тогда опция уже будет некорректна). Да и БД не будет переполняться
				DP_User::delete_user_option("selected_manufacturer");
			}
			else
			{
				$_GET["brend"] = $manufacturer;
			}
		}
		else
		{
			$_GET["brend"] = $manufacturer;
		}
	}
	// Brand in CHPU URL: always query all UAE price warehouses (article-only SQL), not one supplier poll.
	$epc_chpu_direct_pricing = !empty($manufacturer);
}
else
{
	$epc_chpu_direct_pricing = false;
}
// Article-only CHPU: show brand picker first (brands/ARTICLE or legacy MANUFACTURER/ARTICLE step-1 URL).
$epc_brand_picker_mode = ($search_type === 'all_brands_by_article')
	|| ($search_type === 'prices_by_article_and_manufacturer' && empty($manufacturer));

// /parts/{BRAND} browse page — content is part_search_manufacturer_browse.php only (no async manufacturer poll).
if ($search_type === 'manufacturer_browse') {
	return;
}

// ------------------------------------------------------------------------------------------------








//Запрашиваемый артикул
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_article_match.php');
$docpart_cross_interchange_path = $_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_cross_interchange.php';
if(is_file($docpart_cross_interchange_path))
{
	require_once($docpart_cross_interchange_path);
}
$sweep=array(" ", "-", "_", "`", "/", "'", '"', "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t");
$article = str_replace($sweep,"", $article_input);
$article = strtoupper($article);

$epc_cross_fallback_rows = array();
$article_norm_for_cross = docpart_normalize_article_for_price($article_input);
$epc_use_local_crosses = (isset($DP_Config->local_crosses) && !empty($DP_Config->local_crosses));
if($article_norm_for_cross !== '' && $epc_use_local_crosses)
{
	try
	{
		// Keep SSR/TTFB tiny: deep cross expansion runs in JS ajax_epc_cross_search.
		// Under load, skip SSR analogs entirely so CP pages (e.g. /cp/shop/prices/multivendor) do not 524.
		$epc_chpu_cross_rounds = 1;
		$epc_chpu_cross_limit = 40;
		$epc_skip_ssr_cross = false;
		if (function_exists('sys_getloadavg')) {
			$__epc_cross_load = @sys_getloadavg();
			if (is_array($__epc_cross_load) && isset($__epc_cross_load[0])) {
				$__epc_load1 = (float) $__epc_cross_load[0];
				if ($__epc_load1 >= 4.0) {
					$epc_skip_ssr_cross = true;
				} elseif ($__epc_load1 >= 2.5) {
					$epc_chpu_cross_rounds = 1;
					$epc_chpu_cross_limit = 20;
				}
			}
		}
		$cross_partners = $epc_skip_ssr_cross
			? array()
			: docpart_load_interchange_partners($db_link, $article_norm_for_cross, $epc_chpu_cross_rounds, $epc_chpu_cross_limit);
		$cross_seen = array();
		$cross_anchor_brand = !empty($manufacturer) ? trim(html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8')) : '';
		$cross_anchor_article = trim($article_input);
		foreach($cross_partners as $partner)
		{
			if($partner['article'] === '')
			{
				continue;
			}
			$key = strtoupper($partner['brand'].'|'.$partner['article_norm']);
			if(isset($cross_seen[$key]))
			{
				continue;
			}
			$cross_seen[$key] = true;
			$epc_cross_fallback_rows[] = array(
				'brand' => $partner['brand'],
				'article' => $partner['article'],
				'cross_brand' => $cross_anchor_brand,
				'cross_article' => $cross_anchor_article,
			);
		}
	}
	catch(Exception $e)
	{
		$epc_cross_fallback_rows = array();
	}
}

// Genuine (OE) vs aftermarket — UMAPI Epart Catalog: passenger, commercial, motorbike
require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_genuine_manufacturers.php');
$epc_genuine_catalog_url = (isset($multilang_params['lang_href']) ? $multilang_params['lang_href'] : '') . '/umapi_catalog';
$epc_genuine_part_type_index = epc_genuine_build_frontend_index($db_link, isset($DP_Config) ? $DP_Config : null, $epc_genuine_catalog_url);
$GLOBALS['epc_genuine_part_type_index'] = $epc_genuine_part_type_index;

// Поиск по наименованию в каталоге и прайс листах
$name_search_enabled = true;// Настройка поиска: включен / выключен

if($name_search_enabled)
{
	$searsch_str = trim(strip_tags($article_input));
}

// ПИШЕМ СТАТИСТИКУ ЗАПРОСОВ
//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();//ID пользователя

//Запись статистики перенесена в скрипт ajax_asynchron.php
//$insert_query = $db_link->prepare('INSERT INTO `shop_stat_article_queries` (`article`, `ip`, `user_id`, `time`) VALUES (?, ?, ?, ?);');
//$insert_query->execute( array(htmlentities($article), $_SERVER["REMOTE_ADDR"], $user_id, time()) );



/* ********************************* */
/*									 */
/*  НАСТРОЙКА ФИЛЬТРА И ОТОБРАЖЕНИЯ  */
/*									 */
/* ********************************* */

/* Ограничение количества отображаемых позиций (10, 20, 50) */
if( ! isset($_COOKIE["cnt_on_page_settings"]) )
{
	$_COOKIE["cnt_on_page_settings"] = 0;
}
$cnt_on_page_settings = (int)$_COOKIE["cnt_on_page_settings"];
if(empty($cnt_on_page_settings))
{
	$cnt_on_page_settings = 10;// Начальное значение 
}

// Начальное положение фильтра (1 - развернут, 0 - свернут)
$initial_position_filter = (int)$DP_Config->show_filter;

// Отображать строку поиска (1 - да, 0 - нет)
$initial_position_search = (int)$DP_Config->show_search_string;

// CHPU part pages (/parts/BRAND/ARTICLE): show filter, but never the redundant
// in-page "Search by part number" panel (header search is enough).
if (!empty($epc_chpu_direct_pricing)) {
	$initial_position_filter = 1;
	$initial_position_search = 0;
}

/* ********************************* */



//Получаем данные по валюте отображения
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_currency.php");
$epc_part_currency_records = epc_currency_records($db_link, $DP_Config);
$epc_part_selected_currency_iso = epc_currency_selected_iso($epc_part_currency_records, $DP_Config);
$currency_record = $epc_part_currency_records[$epc_part_selected_currency_iso];
$currency_sign = $currency_record["sign"];
$seo_currency_code = 'USD';
if ((string)$epc_part_selected_currency_iso === '784') {
	$seo_currency_code = 'AED';
} else if (!empty($currency_record["caption_short"])) {
	$seo_currency_code = strtoupper(preg_replace('/[^A-Z]/', '', (string)$currency_record["caption_short"]));
	if ($seo_currency_code === '') {
		$seo_currency_code = 'USD';
	}
}
//Строка для обозначения валюты
if($DP_Config->currency_show_mode == "no"){$currency_indicator = "";}
else if($DP_Config->currency_show_mode == "sign_before" || $DP_Config->currency_show_mode == "sign_after"){$currency_indicator = $currency_sign;}else{$currency_indicator = $currency_record["caption_short"];}

$epc_chpu_anchor_has_stock = false;

if ($search_type == 'prices_by_article_and_manufacturer' && !empty($manufacturer)) {
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';
	$seo_manufacturer = html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
	$seo_article_expr = docpart_sql_article_normalized_expr('`article`');
	$seo_priceClause = function_exists('epc_seo_stock_requires_price') && epc_seo_stock_requires_price($db_link)
		? ' AND IFNULL(`price`, 0) > 0' : '';
	$seo_product_query = $db_link->prepare(
		"SELECT `manufacturer`, `article`, `article_show`, `name`, `exist`, `price`
		FROM `shop_docpart_prices_data`
		WHERE " . $seo_article_expr . " = ? AND UPPER(TRIM(`manufacturer`)) = UPPER(?)
		AND IFNULL(`exist`, 0) > 0" . $seo_priceClause . "
		ORDER BY `exist` DESC" . ($seo_priceClause !== '' ? ', `price` ASC' : '') . "
		LIMIT 1"
	);
	$seo_product_query->execute(array($article, $seo_manufacturer));
	$seo_product = $seo_product_query->fetch(PDO::FETCH_ASSOC);
	if ($seo_product && !empty($epc_chpu_direct_pricing)) {
		$epc_chpu_anchor_has_stock = ((float)$seo_product['exist'] > 0);
	}
	if ($seo_product) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
		$seo_include_price = epc_seo_schema_include_price($db_link);
		$seo_schema = epc_seo_build_product_schema_array(
			$seo_product,
			$DP_Config,
			$multilang_params['lang_href'],
			$seo_include_price,
			$seo_currency_code,
			isset($epc_cross_fallback_rows) && is_array($epc_cross_fallback_rows) ? $epc_cross_fallback_rows : array()
		);
		?>
<script type="application/ld+json"><?php echo json_encode($seo_schema, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE); ?></script>
		<?php
		// Crawlable cross / OE numbers (not JS-only) so Google can index related part numbers.
		if (!empty($epc_cross_fallback_rows) && is_array($epc_cross_fallback_rows)) {
			$seoCrossBrand = strtoupper(trim((string) ($seo_product['manufacturer'] ?? $seo_manufacturer)));
			$seoCrossArt = trim((string) ($seo_product['article_show'] ?? $seo_product['article'] ?? $article));
			$seoLangHref = isset($multilang_params['lang_href']) ? (string) $multilang_params['lang_href'] : '/en';
			?>
<nav class="epc-seo-cross-refs" aria-label="Cross references and OE numbers" style="max-width:1100px;margin:16px auto;padding:14px 18px;border:1px solid #e2e8f0;border-radius:10px;background:#f8fafc">
	<h2 style="margin:0 0 8px;font-size:18px;color:#0f172a"><?php echo htmlspecialchars($seoCrossBrand . ' ' . $seoCrossArt, ENT_QUOTES, 'UTF-8'); ?> — cross references &amp; OE numbers</h2>
	<p style="margin:0 0 10px;font-size:13px;color:#475569">Part number / article <strong><?php echo htmlspecialchars($seoCrossArt, ENT_QUOTES, 'UTF-8'); ?></strong> interchanges with:</p>
	<ul style="margin:0;padding-left:18px;columns:2;column-gap:24px;font-size:13px;line-height:1.55">
			<?php
			$seoCrossShown = 0;
			foreach ($epc_cross_fallback_rows as $seoCr) {
				if ($seoCrossShown >= 40) {
					break;
				}
				$crBrand = trim((string) ($seoCr['brand'] ?? ''));
				$crArt = trim((string) ($seoCr['article'] ?? ''));
				if ($crArt === '') {
					continue;
				}
				$crUrl = function_exists('epc_chpu_build_part_url')
					? epc_chpu_build_part_url($DP_Config, $seoLangHref, $crBrand, $crArt)
					: '';
				$label = trim($crBrand . ' ' . $crArt);
				$seoCrossShown++;
				if ($crUrl !== '') {
					echo '<li><a href="' . htmlspecialchars($crUrl, ENT_QUOTES, 'UTF-8') . '">'
						. htmlspecialchars($label, ENT_QUOTES, 'UTF-8')
						. '</a> <span style="color:#64748b">(part number '
						. htmlspecialchars($crArt, ENT_QUOTES, 'UTF-8') . ')</span></li>';
				} else {
					echo '<li>' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8')
						. ' <span style="color:#64748b">(part number '
						. htmlspecialchars($crArt, ENT_QUOTES, 'UTF-8') . ')</span></li>';
				}
			}
			?>
	</ul>
</nav>
			<?php
		}
	}
}

if (!empty($epc_chpu_direct_pricing) && !$epc_chpu_anchor_has_stock && !empty($manufacturer) && $article !== '') {
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
	$epc_anchor_mfr = html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
	// Prefer indexed article_search to avoid REPLACE() CPU spikes on CHPU stock checks.
	$epc_anchor_art_expr = (function_exists('docpart_price_data_ensure_article_search_column')
		&& docpart_price_data_ensure_article_search_column($db_link))
		? '`article_search`'
		: docpart_sql_article_normalized_expr('`article`');
	$epc_anchor_price_clause = '';
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';
		if (function_exists('epc_seo_stock_requires_price') && epc_seo_stock_requires_price($db_link)) {
			$epc_anchor_price_clause = ' AND IFNULL(`price`, 0) > 0';
		}
	}
	try {
		@$db_link->exec('SET SESSION max_statement_time = 2');
		$epc_anchor_stock_query = $db_link->prepare(
			"SELECT `exist` FROM `shop_docpart_prices_data`
			WHERE " . $epc_anchor_art_expr . " = ? AND UPPER(TRIM(`manufacturer`)) = UPPER(?)
			AND IFNULL(`exist`, 0) > 0" . $epc_anchor_price_clause . "
			LIMIT 1"
		);
		$epc_anchor_stock_query->execute(array($article, $epc_anchor_mfr));
		$epc_anchor_stock_row = $epc_anchor_stock_query->fetch(PDO::FETCH_ASSOC);
		if ($epc_anchor_stock_row) {
			$epc_chpu_anchor_has_stock = true;
		}
	} catch (Exception $e) {
		// Keep false.
	}
}
if ((!empty($epc_chpu_direct_pricing) || !empty($epc_brand_picker_mode)) && !$epc_chpu_anchor_has_stock && $article !== '') {
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
	$epc_anchor_art_expr = (function_exists('docpart_price_data_ensure_article_search_column')
		&& docpart_price_data_ensure_article_search_column($db_link))
		? '`article_search`'
		: docpart_sql_article_normalized_expr('`article`');
	$epc_any_price_clause = '';
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';
		if (function_exists('epc_seo_stock_requires_price') && epc_seo_stock_requires_price($db_link)) {
			$epc_any_price_clause = ' AND IFNULL(`price`, 0) > 0';
		}
	}
	try {
		@$db_link->exec('SET SESSION max_statement_time = 2');
		$epc_anchor_any_stock_query = $db_link->prepare(
			"SELECT `exist` FROM `shop_docpart_prices_data`
			WHERE " . $epc_anchor_art_expr . " = ?
			AND IFNULL(`exist`, 0) > 0" . $epc_any_price_clause . "
			LIMIT 1"
		);
		$epc_anchor_any_stock_query->execute(array($article));
		if ($epc_anchor_any_stock_query->fetch(PDO::FETCH_ASSOC)) {
			$epc_chpu_anchor_has_stock = true;
		}
	} catch (Exception $e) {
		// Keep false.
	}
}



//Техническая информация в проценке
$userProfile = DP_User::getUserProfile();//Профиль пользователя
$group_id = (int) $userProfile["groups"][0];
$this_group_id_margin = $group_id;
$group_query = $db_link->prepare('SELECT `for_percentage` FROM `groups` WHERE `id` = ?;');
$group_query->execute(array($group_id));
$group_info = $group_query->fetch();
if((int)$group_info['for_percentage'] === 1){
	$flag_show_data = true;
	if( isset($_COOKIE["flag_show_data"]) )
	{
		if($_COOKIE["flag_show_data"] === 'true'){
			$flag_show_data = 'true';
		}else{
			$flag_show_data = 'false';
		}
	}
	if( isset($_COOKIE["this_group_id_margin"]) )
	{
		$group_query = $db_link->prepare('SELECT * FROM `groups`;');
		$group_query->execute();
		while($group_record = $group_query->fetch()){
			if($_COOKIE["this_group_id_margin"] == $group_record['id'])
			{
				$this_group_id_margin = (int) $_COOKIE["this_group_id_margin"];
				break;
			}
		}
	}
?>
	<script>
	var this_group_id_margin = <?php echo (int) $this_group_id_margin;?>;
	var flag_show_data = <?=$flag_show_data;?>;
	function show_data(){
		flag_show_data = document.getElementById('show_data').checked;
		if(flag_show_data){
			$('.show_data_class').css('display', 'block');
			$('.div_show_data_checked').css('height', '0px');
		}else{
			$('.show_data_class').css('display', 'none');
			$('.div_show_data_checked').css('height', '23px');
		}
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "flag_show_data="+flag_show_data+"; path=/; expires=" + date.toUTCString();
	}
	// Функция смены наценки группы
	function changing_the_groups_margin(){
		let group_id = document.getElementById("select_the_groups_margin").value;
		this_group_id_margin = group_id;
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "this_group_id_margin="+this_group_id_margin+"; path=/; expires=" + date.toUTCString();
		
		onGetStoragesData();
	}
	//Пересчитать наценки
	function recalculate_groups_margin(){
		if(Products.Required.ProductsTypes){
			for(var i=0; i < Products.Required.ProductsTypes.length; i++)
			{
				var Manufacturer = Products.Required.ProductsTypes[i].manufacturer;
				var Article = Products.Required.ProductsTypes[i].article;
			
				//Массив объектов товаров:
				var ProductsObjects = Products.Required.Products.Manufacturers[Manufacturer][Article];
				for(var p=0; p < ProductsObjects.length; p++)
				{
					ProductsObjects[p].price = ProductsObjects[p].groups_price[this_group_id_margin];
					ProductsObjects[p].markup = ProductsObjects[p].groups_markup[this_group_id_margin];
					ProductsObjects[p].check_hash = ProductsObjects[p].groups_check_hash[this_group_id_margin];
				}
			}
			
			for(var i=0; i < Products.SearchName.ProductsTypes.length; i++)
			{
				var Manufacturer = Products.SearchName.ProductsTypes[i].manufacturer;
				var Article = Products.SearchName.ProductsTypes[i].article;
			
				//Массив объектов товаров:
				var ProductsObjects = Products.SearchName.Products.Manufacturers[Manufacturer][Article];
				for(var p=0; p < ProductsObjects.length; p++)
				{
					ProductsObjects[p].price = ProductsObjects[p].groups_price[this_group_id_margin];
					ProductsObjects[p].markup = ProductsObjects[p].groups_markup[this_group_id_margin];
					ProductsObjects[p].check_hash = ProductsObjects[p].groups_check_hash[this_group_id_margin];
				}
			}
			
			for(var i=0; i < Products.Quick_Analogs.ProductsTypes.length; i++)
			{
				var Manufacturer = Products.Quick_Analogs.ProductsTypes[i].manufacturer;
				var Article = Products.Quick_Analogs.ProductsTypes[i].article;
			
				//Массив объектов товаров:
				var ProductsObjects = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article];
				for(var p=0; p < ProductsObjects.length; p++)
				{
					ProductsObjects[p].price = ProductsObjects[p].groups_price[this_group_id_margin];
					ProductsObjects[p].markup = ProductsObjects[p].groups_markup[this_group_id_margin];
					ProductsObjects[p].check_hash = ProductsObjects[p].groups_check_hash[this_group_id_margin];
				}
			}
			
			for(var i=0; i < Products.Analogs.ProductsTypes.length; i++)
			{
				var Manufacturer = Products.Analogs.ProductsTypes[i].manufacturer;
				var Article = Products.Analogs.ProductsTypes[i].article;
			
				//Массив объектов товаров:
				var ProductsObjects = Products.Analogs.Products.Manufacturers[Manufacturer][Article];
				for(var p=0; p < ProductsObjects.length; p++)
				{
					ProductsObjects[p].price = ProductsObjects[p].groups_price[this_group_id_margin];
					ProductsObjects[p].markup = ProductsObjects[p].groups_markup[this_group_id_margin];
					ProductsObjects[p].check_hash = ProductsObjects[p].groups_check_hash[this_group_id_margin];
				}
			}
			
			for(var i=0; i < Products.PossibleReplacement.ProductsTypes.length; i++)
			{
				var Manufacturer = Products.PossibleReplacement.ProductsTypes[i].manufacturer;
				var Article = Products.PossibleReplacement.ProductsTypes[i].article;
			
				//Массив объектов товаров:
				var ProductsObjects = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article];
				for(var p=0; p < ProductsObjects.length; p++)
				{
					ProductsObjects[p].price = ProductsObjects[p].groups_price[this_group_id_margin];
					ProductsObjects[p].markup = ProductsObjects[p].groups_markup[this_group_id_margin];
					ProductsObjects[p].check_hash = ProductsObjects[p].groups_check_hash[this_group_id_margin];
				}
			}
			
			for(var i=0; i < Products.Spare_Box.ProductsTypes.length; i++)
			{
				var Manufacturer = Products.Spare_Box.ProductsTypes[i].manufacturer;
				var Article = Products.Spare_Box.ProductsTypes[i].article;
			
				//Массив объектов товаров:
				var ProductsObjects = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article];
				for(var p=0; p < ProductsObjects.length; p++)
				{
					ProductsObjects[p].price = ProductsObjects[p].groups_price[this_group_id_margin];
					ProductsObjects[p].markup = ProductsObjects[p].groups_markup[this_group_id_margin];
					ProductsObjects[p].check_hash = ProductsObjects[p].groups_check_hash[this_group_id_margin];
				}
			}
			
		}else{
			
			for(var i=0; i < Products.Required.length; i++)
			{
				Products.Required[i].price = Products.Required[i].groups_price[this_group_id_margin];
				Products.Required[i].markup = Products.Required[i].groups_markup[this_group_id_margin];
				Products.Required[i].check_hash = Products.Required[i].groups_check_hash[this_group_id_margin];
			}
			
			for(var i=0; i < Products.SearchName.length; i++)
			{
				Products.SearchName[i].price = Products.SearchName[i].groups_price[this_group_id_margin];
				Products.SearchName[i].markup = Products.SearchName[i].groups_markup[this_group_id_margin];
				Products.SearchName[i].check_hash = Products.SearchName[i].groups_check_hash[this_group_id_margin];
			}
			
			for(var i=0; i < Products.Quick_Analogs.length; i++)
			{
				Products.Quick_Analogs[i].price = Products.Quick_Analogs[i].groups_price[this_group_id_margin];
				Products.Quick_Analogs[i].markup = Products.Quick_Analogs[i].groups_markup[this_group_id_margin];
				Products.Quick_Analogs[i].check_hash = Products.Quick_Analogs[i].groups_check_hash[this_group_id_margin];
			}
			
			for(var i=0; i < Products.Analogs.length; i++)
			{
				Products.Analogs[i].price = Products.Analogs[i].groups_price[this_group_id_margin];
				Products.Analogs[i].markup = Products.Analogs[i].groups_markup[this_group_id_margin];
				Products.Analogs[i].check_hash = Products.Analogs[i].groups_check_hash[this_group_id_margin];
			}
			
			for(var i=0; i < Products.PossibleReplacement.length; i++)
			{
				Products.PossibleReplacement[i].price = Products.PossibleReplacement[i].groups_price[this_group_id_margin];
				Products.PossibleReplacement[i].markup = Products.PossibleReplacement[i].groups_markup[this_group_id_margin];
				Products.PossibleReplacement[i].check_hash = Products.PossibleReplacement[i].groups_check_hash[this_group_id_margin];
			}
			
			for(var i=0; i < Products.Spare_Box.length; i++)
			{
				Products.Spare_Box[i].price = Products.Spare_Box[i].groups_price[this_group_id_margin];
				Products.Spare_Box[i].markup = Products.Spare_Box[i].groups_markup[this_group_id_margin];
				Products.Spare_Box[i].check_hash = Products.Spare_Box[i].groups_check_hash[this_group_id_margin];
			}
			
		}
	}
	</script>
<?php
}
?>



<?php
// Фильтрация проценки
$sql = "SELECT * FROM `shop_docpart_filter`;";
$query = $db_link->prepare($sql);
$query->execute();

$brends_filtr = array();
while($rov = $query->fetch())
{
	if(empty($brends_filtr[$rov['manufacturer']])){
		$brends_filtr[$rov['manufacturer']] = array();
	}
	
	if($rov['article'] === ''){
		$brends_filtr[$rov['manufacturer']]['-'] = array('list_storages'=>json_decode($rov['list_storages'], true));
	}
}
?>
<script>
// список брендов для фильтрации таблицы брендов
var brends_filtr = JSON.parse('<?=json_encode($brends_filtr);?>');
</script>




<?php 
if((int)$DP_Config->is_async_search == 1)
{ 
?>
<!-------------------------------------------- Назад к списку производителей -------------------------------------------->
<div class="col-lg-12" id="back_to_brands_box" style="display:none;">
	<span style="display: inline-block; margin-bottom: 10px; cursor: pointer; width: 262.5px" onClick="back_to_brands();"><i class="fa fa-arrow-left" id="back_to_brands_box_icon" aria-hidden="true"></i> <?php echo translate_str_by_id(5612); ?></span>
	<script>
	function back_to_brands(){
		
		startBeforeRequestTime = 0;
		
		// Флаг будет сообщать о том что произошла первоначальная загрузка страницы
		flag_first_loading = true;

		this_filter = '';// Какой именно фильтр выбран

		sam_price = 0;// Самые дешевые
		sam_time = 0;// Самые быстрые поставки
		sam_price_time = '';//Какая из кнопок была выбрана первой sam_price или sam_time

		// Бренды
		arr_manufacturers =  new Array();
		// Найденные бренды после фильтрации
		arr_manufacturers_posle_filter =  new Array();

		// Склады
		arr_storages = new Array();
		arr_storages_posle_filter =  new Array();
		// Цвета складов
		arr_storages_color =  new Array();

		// Свойства фильтра
		filter =  new Array();

		list_brend_show = false;// Флаг - был ли открыт список производителей перед обновлением фильтра
		list_storages_show = false;// Флаг - был ли открыт список складов перед обновлением фильтра

		// Цена
		filter['price_blok'] = new Object;
		filter['price_blok'].show = 1;// включен или нет
		filter['price_blok'].caption = '<?php echo translate_str_by_id(4303); ?>';
		filter['price_blok'].property_type_id = 2;
		filter['price_blok'].property_id = 'price';
		filter['price_blok'].min_value = undefined;
		filter['price_blok'].max_value = undefined;

		// Срок
		filter['time_to_exe_blok'] = new Object;
		filter['time_to_exe_blok'].show = 1;
		filter['time_to_exe_blok'].caption = '<?php echo translate_str_by_id(3433); ?>';
		filter['time_to_exe_blok'].property_type_id = 2;
		filter['time_to_exe_blok'].property_id = 'time_to_exe';
		filter['time_to_exe_blok'].min_value = undefined;
		filter['time_to_exe_blok'].max_value = undefined;

		// Наличие
		filter['exist_blok'] = new Object;
		filter['exist_blok'].show = 1;
		filter['exist_blok'].caption = '<?php echo translate_str_by_id(4304); ?>';
		filter['exist_blok'].property_type_id = 2;
		filter['exist_blok'].property_id = 'exist';
		filter['exist_blok'].min_value = undefined;
		filter['exist_blok'].max_value = undefined;

		// Бренды
		filter['manufacturer_blok'] = new Object;
		filter['manufacturer_blok'].show = 1;
		filter['manufacturer_blok'].caption = '<?php echo translate_str_by_id(2070); ?>';
		filter['manufacturer_blok'].property_id = 'manufacturer';
		filter['manufacturer_blok'].property_type_id = 5;
		filter['manufacturer_blok'].list_type = 1;
		filter['manufacturer_blok'].list_options = new Array;
		filter['manufacturer_blok'].manufacturer_in_filter = new Array;

		// Склады
		filter['storages_blok'] = new Object;
		filter['storages_blok'].show = 1;
		filter['storages_blok'].caption = '<?php echo translate_str_by_id(4305); ?>';
		filter['storages_blok'].property_id = 'storages';
		filter['storages_blok'].property_type_id = 5;
		filter['storages_blok'].list_type = 1;
		filter['storages_blok'].list_options = new Array;
		filter['storages_blok'].storages_in_filter = new Array;

		start_page_Required = 0;// Количество отображенных строк (групп) позиций Запрошенный артикул
		start_page_SearchName = 0;// Количество отображенных строк (групп) позиций Найденных по наименованию
		start_page_Quick_Analogs = 0;// Количество отображенных строк (групп) позиций Быстрые аналоги
		start_page_Analogs = 0;// Количество отображенных строк (групп) позиций Аналоги
		start_page_PossibleReplacement = 0;// Количество отображенных строк (групп) позиций PossibleReplacement
		start_page_Spare_Box = 0;// Количество отображенных строк (групп) позиций Spare_Box

		cnt_on_page = <?php echo $cnt_on_page_settings; ?>;//Сколько прибавлять позиций по кнопке "Паказать еще"

		show_all_position_flag = false;		
		
		if(Products.Required.Products){
			
			Products = new Object;
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
			//Этот список предназначен для получения объекта товара по его AID:
			Products.All = new Array();//Список объектов
			
			//Индексный и ассоциативный массивы для блоков скрытия "Остальных товаров"
			wrap_blocks_assoc = new Array();//Производитель+Артикул_+blok указывает на индекс
			wrap_blocks_index = new Array();//Индекс указывает на Производитель+Артикул_+blok
			wrap_states = new Array();//Индекс указывает на флаг Открыт(true)/Закрыт(false)

		}else{
			Products = new Object;
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
			//Этот список предназначен для получения объекта товара по его AID:
			Products.All = new Array();//Список объектов 
		}

		Products_All_Asked = false;
		
		document.getElementById('filter_position').innerHTML = '';
		if(this_position_filter == 1){
			show_filter_clicked();
		}
		
		SelectedManufacturer = null;//Выбранный производитель
		manufacturersReview();
		
		document.getElementById("back_to_brands_box").style.display = 'none';
		
		if(document.getElementById("card_positions")){
			document.getElementById("card_positions").innerHTML = '';
		}
	}
	</script>
</div>
<!-------------------------------------------- End Назад к списку производителей ---------------------------------------->









<?php
}
//Формирум объект описания точек выдачи и складов
// require_once may already be satisfied from another scope (e.g. a module), leaving
// $customer_offices unset/null here — count(null) fatals on PHP 8+ and kills /parts/brands/*.
$epc_customer_offices_path = $_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php";
require_once $epc_customer_offices_path;
if (!isset($customer_offices) || !is_array($customer_offices)) {
	include $epc_customer_offices_path;
}
if (!isset($customer_offices) || !is_array($customer_offices)) {
	$customer_offices = array();
}

//var_dump($customer_offices);

$office_storage_bunches = array();//Список всех связок всех офисов обслуживания со своими складами. По этому списку будет осуществляться опрос складов

$office_storage_bunches_prices = array();//Такой же точно массив, только для складов типа Docpart-Price - для возможности из одновременного запроса

//Для каждого магазина получить список складов (не Treelax складов) и опросить каждый склад
for($i=0; $i < count($customer_offices); $i++)
{
    $offices_storages_map[$customer_offices[$i]] = array();//ID точки обслуживания => список складов
    
    //Получаем список складов для данной точки обслуживания у которых product_type = 2 (т.е. автозапчасти)    
	$storages_query = $db_link->prepare('SELECT DISTINCT(storage_id) AS storage_id, (SELECT `handler_folder` FROM `shop_storages_interfaces_types` WHERE `id` = (SELECT `interface_type` FROM `shop_storages` WHERE `id` = `shop_offices_storages_map`.`storage_id`) ) AS `handler_folder` FROM shop_offices_storages_map WHERE office_id = ? AND storage_id IN (SELECT id FROM shop_storages WHERE interface_type > 1 AND `hidden` = 0);');
	$storages_query->execute( array($customer_offices[$i]) );
    while( $storage = $storages_query->fetch() )
    {
		//Определяем версию протокола (1 шаг/2 шага)
		$protocol_version = 1;//По умолчанию
		//Если в папке обработчика присутствует скрипт get_manufacturers.php, значит версия протокола - 2 шаговый
		if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/suppliers_handlers/".$storage["handler_folder"]."/get_manufacturers.php") )
		{
			$protocol_version = 2;
		}
		
		if( !isset($storage["storage_id"]) )
		{
			$storage["storage_id"] = null;
		}
		
		
		//Добавляем связку только, если склад не прайсовый
		if($storage["handler_folder"] != "prices")
		{
			
			// Определим склад каталога товаров
			$treelax_catalogue = false;
			if($storage["handler_folder"] === 'treelax_catalogue'){
				$treelax_catalogue = true;
			}
			
			//API-поставщиков добавляем в основной список
			array_push($office_storage_bunches, array("office_id"=>(int)$customer_offices[$i], "storage_id"=>(int)$storage["storage_id"], "sent" => 0, "protocol_version"=>$protocol_version, "manufacturers_sent" => 0, "treelax_catalogue" => $treelax_catalogue));
		}
		else
		{
			//Прайсовых поставщиков добавляем во вспомогательный список
			array_push($office_storage_bunches_prices, array("office_id"=>(int)$customer_offices[$i], "storage_id"=>(int)$storage["storage_id"], "sent" => 0, "protocol_version"=>$protocol_version, "manufacturers_sent" => 0));
		}
    }
	
	//После наполнения списка связок, вспомогательный список для прайсовых поствщиков добавляем первым элементом в основной список - для того, чтобы сначала опросить прайс-листы
	/*
	Версия протокола - ставим 3
	Добавляем еще один параметр office_storage_bunches - используется на сервере для понимания, какие связки складов и магазинов опросить
	*/
	if( count($office_storage_bunches_prices) > 0 )
	{
		array_unshift($office_storage_bunches, array("office_id"=>0, "storage_id"=>0, "sent" => 0, "protocol_version"=>3, "manufacturers_sent" => 0, "office_storage_bunches"=>$office_storage_bunches_prices) );
		
		//Обнуляем массив для следующей итерации офисов
		$office_storage_bunches_prices = array();
	}
	
}

$epc_default_office_id = !empty($customer_offices) ? (int)$customer_offices[0] : 1;

// Drop storefront-disabled warehouses before JS polls them
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_storage_flags.php';
	if (function_exists('epc_ssf_filter_office_storage_bunches')) {
		$office_storage_bunches = epc_ssf_filter_office_storage_bunches($db_link, $office_storage_bunches);
	}
}

// When offices have no mapped price warehouses, still add a protocol-3 bunch for all active price lists.
$epc_has_price_bunch = false;
foreach ($office_storage_bunches as $epc_bunch_check) {
	if (isset($epc_bunch_check['protocol_version']) && (int)$epc_bunch_check['protocol_version'] === 3) {
		$epc_has_price_bunch = true;
		break;
	}
}
if (!$epc_has_price_bunch) {
	$epc_fallback_price_bunches = array();
	try {
		$epc_fallback_storages_query = $db_link->prepare(
			'SELECT `id` FROM `shop_storages`
			WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)
			AND `hidden` = 0
			ORDER BY `id`;'
		);
		$epc_fallback_storages_query->execute(array('prices'));
		while ($epc_fallback_storage = $epc_fallback_storages_query->fetch(PDO::FETCH_ASSOC)) {
			$epc_fallback_price_bunches[] = array(
				'office_id' => $epc_default_office_id,
				'storage_id' => (int)$epc_fallback_storage['id'],
				'sent' => 0,
				'protocol_version' => 1,
				'manufacturers_sent' => 0,
			);
		}
	} catch (Exception $e) {
		$epc_fallback_price_bunches = array();
	}
	if (!empty($epc_fallback_price_bunches)) {
		array_unshift(
			$office_storage_bunches,
			array(
				'office_id' => 0,
				'storage_id' => 0,
				'sent' => 0,
				'protocol_version' => 3,
				'manufacturers_sent' => 0,
				'office_storage_bunches' => $epc_fallback_price_bunches,
			)
		);
	}
}

if((int)$DP_Config->is_async_search == 1)
{
	// Добавляем в конец массива сервер кроссов для асинхронного запроса
	array_push($office_storage_bunches, array('protocol_version' => 'server'));	
}

// Include every active price-list warehouse in part search (UAE lists may not be mapped to the current office).
for ($epc_bunch_index = 0; $epc_bunch_index < count($office_storage_bunches); $epc_bunch_index++)
{
	if ((int)$office_storage_bunches[$epc_bunch_index]['protocol_version'] !== 3 || empty($office_storage_bunches[$epc_bunch_index]['office_storage_bunches']))
	{
		continue;
	}
	$epc_existing_storage_ids = array();
	foreach ($office_storage_bunches[$epc_bunch_index]['office_storage_bunches'] as $epc_price_bunch)
	{
		$epc_existing_storage_ids[(int)$epc_price_bunch['storage_id']] = true;
	}
	try
	{
		$epc_all_price_storages_query = $db_link->prepare(
			'SELECT `id` FROM `shop_storages`
			WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)
			AND `hidden` = 0
			ORDER BY `id`;'
		);
		$epc_all_price_storages_query->execute(array('prices'));
		while ($epc_price_storage = $epc_all_price_storages_query->fetch(PDO::FETCH_ASSOC))
		{
			$epc_storage_id = (int)$epc_price_storage['id'];
			if (isset($epc_existing_storage_ids[$epc_storage_id]))
			{
				continue;
			}
			array_push(
				$office_storage_bunches[$epc_bunch_index]['office_storage_bunches'],
				array(
					'office_id' => $epc_default_office_id,
					'storage_id' => $epc_storage_id,
					'sent' => 0,
					'protocol_version' => 1,
					'manufacturers_sent' => 0,
				)
			);
			$epc_existing_storage_ids[$epc_storage_id] = true;
		}
	}
	catch (Exception $e)
	{
		// Keep office-mapped storages only.
	}
	break;
}

// CHPU brand+article and /parts/brands/ARTICLE: pre-load price-list stock server-side (article-only SQL).
$epc_initial_price_bunch = null;
$epc_article_only_price_bunch = (!empty($epc_chpu_direct_pricing) || !empty($epc_brand_picker_mode)) && $article !== '';
$epc_host_load1 = null;
if (function_exists('sys_getloadavg')) {
	$epc_load_tmp = @sys_getloadavg();
	if (is_array($epc_load_tmp) && isset($epc_load_tmp[0])) {
		$epc_host_load1 = (float) $epc_load_tmp[0];
	}
}
if ($epc_article_only_price_bunch) {
	try {
		// Only skip SSR warehouse SQL under extreme load. Skipping too early leaves
		// guests with an empty #products_area ("Goods not found") when ajax is slow.
		if ($epc_host_load1 !== null && $epc_host_load1 >= 22.0) {
			throw new RuntimeException('load_shed_ssr_bunch');
		}
		$epc_ss_price_bunches = array();
		foreach ($office_storage_bunches as $epc_ss_bunch) {
			if ((int) $epc_ss_bunch['protocol_version'] === 3 && !empty($epc_ss_bunch['office_storage_bunches'])) {
				$epc_ss_price_bunches = $epc_ss_bunch['office_storage_bunches'];
				break;
			}
		}
		if (!empty($epc_ss_price_bunches)) {
			if (!class_exists('prices_enclosure', false)) {
				define('DOCPART_PRICES_ENCLOSURE_LIBRARY', true);
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/suppliers_handlers/prices/common_interface.php';
			}
			$epc_ss_storage_options = array(
				'user_id' => $user_id,
				'group_id' => $group_id,
				'office_storage_bunches' => $epc_ss_price_bunches,
				'analogs' => array(),
			);
			// Article-only SQL across mapped UAE price warehouses (ignore URL brand for stock lookup).
			$epc_ss_prices = new prices_enclosure($article, array(), $epc_ss_storage_options, $article);
			$epc_initial_price_bunch = json_decode(json_encode($epc_ss_prices), true);
			if (empty($epc_initial_price_bunch['Products'])) {
				$epc_initial_price_bunch = null;
			} else {
				$epc_initial_price_bunch['result'] = 1;
				$epc_initial_price_bunch['storage_id'] = 0;
			}
		}
	} catch (Throwable $e) {
		$epc_initial_price_bunch = null;
	}
	if (!empty($epc_initial_price_bunch) && !empty($epc_initial_price_bunch['Products'])) {
		$epc_chpu_anchor_has_stock = true;
	}
}

/**
 * Server-render warehouse stock rows into #products_area so stock is visible
 * before JS runs (and remains if ajax_asynchron / cross-search 524 clears paint).
 */
function epc_chpu_ssr_warehouse_table_html(array $products, $currency_indicator = ''): string
{
	if ($products === array()) {
		return '';
	}
	if (!function_exists('epc_storefront_prices_visible_for_user')) {
		$helpers = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
		if (is_file($helpers)) {
			require_once $helpers;
		}
	}
	$pricesVisible = function_exists('epc_storefront_prices_visible_for_user')
		? epc_storefront_prices_visible_for_user(isset($GLOBALS['user_id']) ? (int) $GLOBALS['user_id'] : null)
		: true;
	$priceCta = (!$pricesVisible && function_exists('epc_storefront_prices_login_cta_html'))
		? epc_storefront_prices_login_cta_html()
		: '';
	$currencyLabel = trim((string) $currency_indicator);
	$priceHeader = $currencyLabel !== '' ? ('Price, ' . htmlspecialchars($currencyLabel, ENT_QUOTES, 'UTF-8')) : 'Price';
	$oeRows = '';
	$amRows = '';
	$genuineBrands = array();
	if (!empty($GLOBALS['epc_genuine_part_type_index']['brands']) && is_array($GLOBALS['epc_genuine_part_type_index']['brands'])) {
		$genuineBrands = $GLOBALS['epc_genuine_part_type_index']['brands'];
	}
	foreach ($products as $product) {
		if (!is_array($product)) {
			continue;
		}
		$exist = (float) ($product['exist'] ?? 0);
		if ($exist <= 0) {
			continue;
		}
		$brand = trim((string) ($product['manufacturer'] ?? ''));
		$articleShow = trim((string) ($product['article_show'] ?? $product['article'] ?? ''));
		$name = trim((string) ($product['name'] ?? ''));
		$warehouse = trim((string) ($product['storage_caption'] ?? ''));
		$priceRaw = (float) ($product['price'] ?? 0);
		$term = 'In warehouse';
		if ($warehouse !== '') {
			$term .= ' · ' . $warehouse;
		}
		if ($pricesVisible) {
			$priceHtml = '<span class="epc-price-value">' . htmlspecialchars(number_format($priceRaw, 2, '.', ''), ENT_QUOTES, 'UTF-8') . '</span>';
		} else {
			$priceHtml = $priceCta !== '' ? $priceCta : '&mdash;';
		}
		$infoHtml = '';
		if ($warehouse !== '') {
			$infoHtml = '<div class="show_data_class epc-warehouse-label" style="margin:3px 0;white-space:nowrap;max-width:110px;font-size:11px;font-weight:600" title="Warehouse"><span class="info_box">'
				. htmlspecialchars($warehouse, ENT_QUOTES, 'UTF-8')
				. '</span></div>';
		}
		$brandKey = function_exists('docpart_synonym_normalize_brand')
			? docpart_synonym_normalize_brand($brand)
			: strtoupper(preg_replace('/\s+/', '', $brand));
		$isOe = ($brandKey !== '' && !empty($genuineBrands[$brandKey]));
		$rowClass = $isOe ? 'epc-part-type-row--genuine' : 'epc-part-type-row--aftermarket';
		$rowHtml = '<tr class="' . $rowClass . ' epc-ssr-warehouse-row">'
			. '<td class="td_photo"></td>'
			. '<td class="td_manufacturer"><span>' . htmlspecialchars($brand, ENT_QUOTES, 'UTF-8') . '</span></td>'
			. '<td class="td_article"><strong>' . htmlspecialchars($articleShow, ENT_QUOTES, 'UTF-8') . '</strong></td>'
			. '<td class="td_name"><span title="' . htmlspecialchars($name, ENT_QUOTES, 'UTF-8') . '">' . htmlspecialchars($name, ENT_QUOTES, 'UTF-8') . '</span></td>'
			. '<td class="td_exist">' . htmlspecialchars((string) (int) $exist, ENT_QUOTES, 'UTF-8') . '</td>'
			. '<td class="td_time_to_exe"><span class="epc-warehouse-term">' . htmlspecialchars($term, ENT_QUOTES, 'UTF-8') . '</span></td>'
			. '<td class="td_info">' . $infoHtml . '</td>'
			. '<td class="td_price product_price">' . $priceHtml . '</td>'
			. '<td class="td_add_to_cart"><span class="epc-ssr-actions-pending" style="font-size:11px;color:#64748b">Loading actions…</span></td>'
			. '<td class="td_color"></td>'
			. '</tr>';
		if ($isOe) {
			$oeRows .= $rowHtml;
		} else {
			$amRows .= $rowHtml;
		}
	}
	if ($oeRows === '' && $amRows === '') {
		return '';
	}
	$oeCount = $oeRows === '' ? 0 : substr_count($oeRows, '<tr');
	$amCount = $amRows === '' ? 0 : substr_count($amRows, '<tr');
	$count = $oeCount + $amCount;
	$body = '';
	if ($oeCount > 0) {
		$body .= '<tr class="epc-part-type-caption epc-part-type-caption--genuine"><td colspan="10"><span class="epc-part-type-pill">Genuine (OE) (' . (int) $oeCount . ')</span></td></tr>' . $oeRows;
	}
	if ($amCount > 0) {
		$body .= '<tr class="epc-part-type-caption epc-part-type-caption--aftermarket"><td colspan="10"><span class="epc-part-type-pill">Aftermarket (' . (int) $amCount . ')</span></td></tr>' . $amRows;
	}
	$banner = '<div class="epc-ssr-warehouse-banner" style="margin:0 0 10px;padding:10px 12px;border:1px solid #bbf7d0;border-radius:8px;background:#f0fdf4;color:#14532d;font-size:13px;text-align:left">'
		. '<strong>UAE warehouse stock</strong> — ' . (int) $count . ' offer'
		. ((int) $count === 1 ? '' : 's')
		. ' ready below. Cross references may list additional numbers that are not in stock.'
		. '</div>';
	return $banner
		. '<table id="all_table_products" class="epc-ssr-warehouse-table">'
		. '<thead><tr>'
		. '<th class="th_photo"></th>'
		. '<th class="th_manufacturer">Manufacturer</th>'
		. '<th class="th_article">Article</th>'
		. '<th class="th_name">Name</th>'
		. '<th class="th_exist">Availability</th>'
		. '<th class="th_time_to_exe">Term</th>'
		. '<th class="th_info">Info</th>'
		. '<th class="th_price">' . $priceHeader . '</th>'
		. '<th class="th_add_to_cart">Actions</th>'
		. '<th class="th_color"></th>'
		. '</tr></thead><tbody>'
		. $body
		. '</tbody></table>';
}

// Bootstrap manufacturer rows for CHPU URLs (e.g. /parts/AISIN/DT068) so price-list search can run.
$epc_chpu_manufacturer_bootstrap = array();
if (!empty($epc_chpu_direct_pricing) && !empty($manufacturer)) {
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
	$epc_mfr_show = mb_strtoupper(html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8'), 'UTF-8');
	$epc_mfr_names = array($epc_mfr_show);
	try {
		$synonym_query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = (SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1);');
		$synonym_query->execute(array($epc_mfr_show));
		$synonym_record = $synonym_query->fetch(PDO::FETCH_ASSOC);
		if ($synonym_record && !empty($synonym_record['name'])) {
			$epc_mfr_names[] = mb_strtoupper(trim($synonym_record['name']), 'UTF-8');
		}
		$synonyms_query = $db_link->prepare('SELECT `synonym` FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = (SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1);');
		$synonyms_query->execute(array($epc_mfr_show));
		while ($synonym_row = $synonyms_query->fetch(PDO::FETCH_ASSOC)) {
			if (!empty($synonym_row['synonym'])) {
				$epc_mfr_names[] = mb_strtoupper(trim($synonym_row['synonym']), 'UTF-8');
			}
		}
	} catch (Exception $e) {
		// Continue with URL manufacturer only.
	}
	$epc_mfr_names = array_values(array_unique($epc_mfr_names));
	$epc_price_storage_map = array();
	try {
		$price_storages_query = $db_link->prepare(
			'SELECT `id`, `short_name`, `connection_options`
			FROM `shop_storages`
			WHERE `interface_type` IN (SELECT `id` FROM `shop_storages_interfaces_types` WHERE `handler_folder` = ?)
			AND `hidden` = 0;'
		);
		$price_storages_query->execute(array('prices'));
		while ($price_storage = $price_storages_query->fetch(PDO::FETCH_ASSOC)) {
			$connection_options = json_decode($price_storage['connection_options'], true);
			if (!empty($connection_options['price_id'])) {
				$epc_price_storage_map[(int)$connection_options['price_id']] = array(
					'storage_id' => (int)$price_storage['id'],
					'warehouse' => $price_storage['short_name'],
				);
			}
		}
	} catch (Exception $e) {
		$epc_price_storage_map = array();
	}
	if (!empty($epc_price_storage_map)) {
		$art_expr = docpart_sql_article_normalized_expr('`article`');
		$price_ids = array_keys($epc_price_storage_map);
		$price_placeholders = implode(',', array_fill(0, count($price_ids), '?'));
		$mfr_placeholders = implode(',', array_fill(0, count($epc_mfr_names), '?'));
		$binding_values = array($article);
		$binding_values = array_merge($binding_values, $epc_mfr_names, $price_ids);
		try {
			$bootstrap_query = $db_link->prepare(
				"SELECT DISTINCT `manufacturer`, `price_id`
				FROM `shop_docpart_prices_data`
				WHERE " . $art_expr . " = ?
				AND UPPER(TRIM(`manufacturer`)) IN (" . $mfr_placeholders . ")
				AND `price_id` IN (" . $price_placeholders . ")"
			);
			$bootstrap_query->execute($binding_values);
			$epc_seen_bootstrap = array();
			while ($bootstrap_row = $bootstrap_query->fetch(PDO::FETCH_ASSOC)) {
				$price_id = (int)$bootstrap_row['price_id'];
				if (empty($epc_price_storage_map[$price_id])) {
					continue;
				}
				$storage_id = (int)$epc_price_storage_map[$price_id]['storage_id'];
				$hash = md5(mb_strtoupper(trim($bootstrap_row['manufacturer']), 'UTF-8') . '|' . $storage_id);
				if (isset($epc_seen_bootstrap[$hash])) {
					continue;
				}
				$epc_seen_bootstrap[$hash] = true;
				$office_id = $epc_default_office_id;
				foreach ($customer_offices as $customer_office_id) {
					$office_map_query = $db_link->prepare('SELECT `office_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1;');
					$office_map_query->execute(array((int)$customer_office_id, $storage_id));
					if ($office_map_query->fetch()) {
						$office_id = (int)$customer_office_id;
						break;
					}
				}
				$epc_boot_mfr = mb_strtoupper(trim((string)$bootstrap_row['manufacturer']), 'UTF-8');
				$epc_chpu_manufacturer_bootstrap[] = array(
					'manufacturer' => $bootstrap_row['manufacturer'],
					'manufacturer_id' => 0,
					// Keep the warehouse brand as stored (AISINC ≠ AISIN).
					'manufacturer_show' => $epc_boot_mfr !== '' ? $epc_boot_mfr : $epc_mfr_show,
					'name' => '',
					'storage_id' => $storage_id,
					'office_id' => $office_id,
					'synonyms_single_query' => true,
					'params' => array('type' => 'prices'),
					'valid' => true,
				);
			}
		} catch (Exception $e) {
			$epc_chpu_manufacturer_bootstrap = array();
		}
	}
	// Always register every active price warehouse for CHPU pages (not only DB synonym hits).
	if (!empty($epc_chpu_direct_pricing) && !empty($epc_price_storage_map)) {
		$epc_seen_bootstrap_ids = array();
		foreach ($epc_chpu_manufacturer_bootstrap as $epc_boot_row) {
			$epc_seen_bootstrap_ids[(int)$epc_boot_row['storage_id']] = true;
		}
		foreach ($epc_price_storage_map as $price_id => $price_storage_info) {
			$storage_id = (int)$price_storage_info['storage_id'];
			if (isset($epc_seen_bootstrap_ids[$storage_id])) {
				continue;
			}
			$office_id = $epc_default_office_id;
			foreach ($customer_offices as $customer_office_id) {
				$office_map_query = $db_link->prepare('SELECT `office_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1;');
				$office_map_query->execute(array((int)$customer_office_id, $storage_id));
				if ($office_map_query->fetch()) {
					$office_id = (int)$customer_office_id;
					break;
				}
			}
			$epc_chpu_manufacturer_bootstrap[] = array(
				'manufacturer' => $epc_mfr_show,
				'manufacturer_id' => 0,
				'manufacturer_show' => $epc_mfr_show,
				'name' => '',
				'storage_id' => $storage_id,
				'office_id' => $office_id,
				'synonyms_single_query' => true,
				'params' => array('type' => 'prices'),
				'valid' => true,
			);
			$epc_seen_bootstrap_ids[$storage_id] = true;
		}
	}
	if (empty($epc_chpu_manufacturer_bootstrap) && !empty($epc_price_storage_map)) {
		foreach ($epc_price_storage_map as $price_id => $price_storage_info) {
			$office_id = $epc_default_office_id;
			foreach ($customer_offices as $customer_office_id) {
				$office_map_query = $db_link->prepare('SELECT `office_id` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1;');
				$office_map_query->execute(array((int)$customer_office_id, (int)$price_storage_info['storage_id']));
				if ($office_map_query->fetch()) {
					$office_id = (int)$customer_office_id;
					break;
				}
			}
			$epc_chpu_manufacturer_bootstrap[] = array(
				'manufacturer' => $epc_mfr_show,
				'manufacturer_id' => 0,
				'manufacturer_show' => $epc_mfr_show,
				'name' => '',
				'storage_id' => (int)$price_storage_info['storage_id'],
				'office_id' => $office_id,
				'synonyms_single_query' => true,
				'params' => array('type' => 'prices'),
				'valid' => true,
			);
		}
	}
}

?>
<script>
// Функция вывода логов в консоле
function log(text){
	<?php
	if( DP_User::isAdmin() )
	{
	?>
	var log_enabled = 1;// Включены ли логи
	if(log_enabled){
		console.log(text);
	}
	<?php
	}
	?>
}

var office_storage_bunches = JSON.parse('<?php echo json_encode($office_storage_bunches); ?>');
var epcChpuManufacturerBootstrap = <?php echo json_encode(!empty($epc_chpu_manufacturer_bootstrap) ? $epc_chpu_manufacturer_bootstrap : array(), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
log('<?php echo translate_str_by_id(4283); ?>:');
log(office_storage_bunches);
log('');
</script>




<?php
/* НАСТРОЙКИ РАБОТЫ АСИНХРОННОГО ОПРОСА ПОСТАВЩИКОВ */

// Получаем список групп складов
$storages_groups = array();

if(!empty($office_storage_bunches)){
	
	// Склады первой группы - то что в базе сайта: прайс листы, каталог товаров
	$storages_arr = array();
	foreach($office_storage_bunches as $item_bunches){
		// Если прайс или treelax_catalogue
		if($item_bunches['protocol_version'] == 3 || ( isset($item_bunches['treelax_catalogue']) && $item_bunches['treelax_catalogue'] == true) ){
			$storages_arr[] = $item_bunches;
		}
	}
	if(!empty($storages_arr)){
		$storages_groups[] = array('storages' => $storages_arr, 'sent' => 0, 'manufacturers_sent' => 0);
	}
	
	
	
	// Склады пользовательских групп
	$query = $db_link->prepare('SELECT * FROM `shop_storages_groups` ORDER BY `order`;');
	$query->execute();
	while($record = $query->fetch()){
		$storages = explode(',', $record['storages']);
		$storages_arr = array();
		if(!empty($storages)){
			foreach($storages as $storage_id){
				$storage_id = (int) trim($storage_id);
				foreach($office_storage_bunches as $item_bunches){
					// Если id склада есть в группе
					if( isset($item_bunches['storage_id']) && $item_bunches['storage_id'] == $storage_id){
						$storages_arr[] = $item_bunches;
						break 1;// Выходим на один уровень
					}
				}
			}
		}
		if(!empty($storages_arr)){
			$storages_groups[] = array('storages' => $storages_arr, 'sent' => 0, 'manufacturers_sent' => 0);
		}
	}

	
	
	// Заполняем последнею группу, в которой будут оставшиеся склады
	$storages_arr = array();
	foreach($office_storage_bunches as $item_bunches){
		if( ( isset($item_bunches['storage_id']) && $item_bunches['storage_id'] == 0 ) || ( isset($item_bunches['treelax_catalogue']) && $item_bunches['treelax_catalogue'] == true) ){
			continue;// Пропускаем потому что эти склады находятся в 0 группе
		}
		
		if( !isset($item_bunches['storage_id']) )
		{
			$item_bunches['storage_id'] = null;
		}
		
		$storage_id = $item_bunches['storage_id'];
		$flag_none_group = true;
		
		// Ищем id склада в группах
		foreach($storages_groups as $item_group){
			if(!empty($item_group['storages'])){
				foreach($item_group['storages'] as $this_bunches){
					if( isset($this_bunches['storage_id']) && $this_bunches['storage_id'] == $storage_id){
						$flag_none_group = false;// Склад уже добавлен в группу
						break 2;// Выходим из обоих циклов
					}
				}
			}
		}
		
		// Если id склада нет в группах
		if($flag_none_group == true){
			$storages_arr[] = $item_bunches;
		}
	}
	if(!empty($storages_arr)){
		// Если вне групп слишком много складов то разабьем их на несколько групп
		$cnt = count($storages_arr);
		$cnt_group = 15;// Максимальное количество складов в группе
		if($cnt > $cnt_group){
			$n = 0;
			$k = 0;
			$storages_arr_tmp = array();
			for($i = 0; $i < $cnt; $i++){
				$storages_arr_tmp[$k][] = $storages_arr[$i];
				$n++;
				if($n == $cnt_group){
					$n = 0;
					$k++;
				}
			}
			if(!empty($storages_arr_tmp)){
				foreach($storages_arr_tmp as $storages_arr){
					$storages_groups[] = array('storages' => $storages_arr, 'sent' => 0, 'manufacturers_sent' => 0);
				}
			}
		}else{
			$storages_groups[] = array('storages' => $storages_arr, 'sent' => 0, 'manufacturers_sent' => 0);
		}
	}
}

if((int)$DP_Config->is_async_search == 1)
{
	//Получаем географический узел покупателя
	$geo_id = NULL;
	if (isset($_COOKIE["my_city"])) {
		$geo_id = $_COOKIE["my_city"];
	}
	//Куки не были еще выставлены - выводим для самого первого гео-узла, чтобы хоть что-то показать
	if ($geo_id == NULL) {
		$min_geo_id_query = $db_link->prepare('SELECT MIN(`id`) AS `id` FROM `shop_geo`;');
		$min_geo_id_query->execute();
		$min_geo_id_record = $min_geo_id_query->fetch();
		$geo_id = $min_geo_id_record["id"];
	}

	// Формируем запросы на получение списка производителей
	$postdata_manufacturers = array();
	if (!empty($office_storage_bunches)) {
		foreach ($office_storage_bunches as $item) {
			if ($item['protocol_version'] === 2) {
				
				if( !isset($item['storage_id']) )
				{
					$item['storage_id'] = null;
				}
				
				// API - поставщик
				$postdata_manufacturers[] = array('url' => $DP_Config->domain_path . 'content/shop/docpart/ajax_getManufacturersList.php',  'query' => 'is_async_search=1&geo_id=' . $geo_id . '&office_id=' . $item['office_id'] . '&storage_id=' . $item['storage_id'] . '&query=' . urlencode(json_encode(array('article' => $article))));
			} else if ($item['protocol_version'] === 3) {
				// Прайс листы
				$postdata_manufacturers[] = array('url' => $DP_Config->domain_path . 'content/shop/docpart/ajax_getManufacturersListFromPrices.php', 'query' => 'is_async_search=1&group_id=' . (int)$group_id . '&office_storage_bunches=' . urlencode(json_encode($item['office_storage_bunches'])) . '&query=' . urlencode(json_encode(array('article' => $article))));
			} else if ($item['protocol_version'] === 'server') {
				// Сервер кроссов
				$postdata_manufacturers[] = array('url' => $DP_Config->domain_path . 'content/shop/docpart/ajax_getManufacturersListFromCrossServer.php', 'query' => 'is_async_search=1&query=' . urlencode(json_encode(array('article' => $article))));
			}
		}
	}
}

?>
<script>
// Список групп складов
var storages_groups = JSON.parse('<?=json_encode($storages_groups);?>');


<?php
if((int)$DP_Config->is_async_search == 1)
{
?>
var postdata_storages = JSON.parse('<?= json_encode($postdata_manufacturers); ?>');
<?php 
}
?>

log('<?php echo translate_str_by_id(4284); ?>:');

log(storages_groups);
log('');

// ----------------------------------------------------------------------------------------------------------------------------------------------------
//ОБЪЕКТ ДЛЯ ХРАНЕНИЯ СПИСКА ВОЗМОЖНЫХ ВАРИАНТОВ ТОВАРОВ
var ProductsManufacturers = new Array();//Все варианты всех поставщиков
var ProductsManufacturers_Shown = new Object();//Флаги отображенных
var ProductsManufacturers_Shown_Count = 0;//Количество отображенных производителей в таблице

var ProductsManufacturers_All_Asked = false;//Флаг - обозначает, что все поставщики с типом протокола 2 опрошены

function epcSelectedManufacturerRows()
{
	if(SelectedManufacturer == null)
	{
		return [];
	}
	var rows = ProductsManufacturers_Shown[SelectedManufacturer];
	return (rows && rows.length) ? rows : [];
}
function epcChpuFillPriceManufacturersForBunch(search_object_clone, nested_bunches)
{
	if(!search_object_clone || !nested_bunches || !nested_bunches.length)
	{
		return;
	}
	// Article-only price pages: query all UAE price warehouses (ignore brand filter in SQL).
	if(typeof epcUsesArticleOnlyStockUi === 'function' && epcUsesArticleOnlyStockUi())
	{
		search_object_clone.manufacturers = [];
		return;
	}
	if(!Array.isArray(search_object_clone.manufacturers))
	{
		search_object_clone.manufacturers = [];
	}
	if(typeof epcPushPriceManufacturersForBunch === 'function' && SelectedManufacturer != null)
	{
		epcPushPriceManufacturersForBunch(search_object_clone, nested_bunches);
	}
	if(search_object_clone.manufacturers.length > 0)
	{
		return;
	}
	var pushed_storage = {};
	var bootstrap = (typeof epcChpuManufacturerBootstrap !== 'undefined' && epcChpuManufacturerBootstrap.length)
		? epcChpuManufacturerBootstrap
		: [];
	for(var b = 0; b < bootstrap.length; b++)
	{
		var boot_row = bootstrap[b];
		var storage_id = parseInt(boot_row.storage_id, 10);
		if(storage_id < 1 || pushed_storage[storage_id])
		{
			continue;
		}
		for(var n = 0; n < nested_bunches.length; n++)
		{
			if(parseInt(nested_bunches[n].storage_id, 10) === storage_id)
			{
				search_object_clone.manufacturers.push(boot_row);
				pushed_storage[storage_id] = true;
				break;
			}
		}
	}
	if(search_object_clone.manufacturers.length > 0)
	{
		return;
	}
	var brand = (search_object.requested_manufacturer || SelectedManufacturer || '').toString().trim();
	if(brand === '')
	{
		return;
	}
	for(var s = 0; s < nested_bunches.length; s++)
	{
		var bunch_storage_id = parseInt(nested_bunches[s].storage_id, 10);
		if(bunch_storage_id < 1 || pushed_storage[bunch_storage_id])
		{
			continue;
		}
		search_object_clone.manufacturers.push({
			manufacturer: brand,
			manufacturer_id: 0,
			manufacturer_show: brand,
			name: '',
			storage_id: bunch_storage_id,
			office_id: parseInt(nested_bunches[s].office_id, 10) || 1,
			synonyms_single_query: true,
			params: {type: 'prices'},
			valid: true
		});
		pushed_storage[bunch_storage_id] = true;
	}
}
function epcFetchJsonWithTimeout(url, options, timeoutMs, fallbackValue)
{
	var ms = parseInt(timeoutMs, 10) || 45000;
	var timed = new Promise(function(resolve) {
		window.setTimeout(function() {
			resolve(fallbackValue);
		}, ms);
	});
	var fetched = fetch(url, options).then(function(response) {
		if(!response.ok)
		{
			return fallbackValue;
		}
		return response.json();
	}).catch(function() {
		return fallbackValue;
	});
	return Promise.race([fetched, timed]);
}
function epcPushPriceManufacturersForBunch(search_object_clone, nested_bunches)
{
	var rows = epcSelectedManufacturerRows();
	if(SelectedManufacturer == null || !nested_bunches || !nested_bunches.length)
	{
		return;
	}
	var pushed_storage = {};
	var template_row = rows.length > 0 ? rows[0] : null;
	for(var m = 0; m < rows.length; m++)
	{
		for(var n = 0; n < nested_bunches.length; n++)
		{
			var storage_id = parseInt(nested_bunches[n].storage_id, 10);
			if(storage_id < 1 || pushed_storage[storage_id])
			{
				continue;
			}
			if(parseInt(rows[m].storage_id, 10) === storage_id)
			{
				if(rows[m].params && rows[m].params.type === 'prices')
				{
					search_object_clone.manufacturers.push(rows[m]);
					pushed_storage[storage_id] = true;
				}
			}
		}
	}
	// CHPU URLs: ensure every price warehouse is queried (bootstrap may only list one storage).
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		for(var s = 0; s < nested_bunches.length; s++)
		{
			var bunch_storage_id = parseInt(nested_bunches[s].storage_id, 10);
			if(bunch_storage_id < 1 || pushed_storage[bunch_storage_id])
			{
				continue;
			}
			search_object_clone.manufacturers.push({
				manufacturer: template_row ? template_row.manufacturer : (search_object.requested_manufacturer || SelectedManufacturer),
				manufacturer_id: 0,
				manufacturer_show: SelectedManufacturer,
				name: template_row ? template_row.name : '',
				storage_id: bunch_storage_id,
				office_id: parseInt(nested_bunches[s].office_id, 10) || 1,
				synonyms_single_query: true,
				params: {type: 'prices'},
				valid: true
			});
			pushed_storage[bunch_storage_id] = true;
		}
	}
	if(search_object_clone.manufacturers.length < 1 && rows.length > 0)
	{
		search_object_clone.manufacturers.push(rows[0]);
	}
}
function epcClearCrossStockFromResults()
{
	var productsArea = document.getElementById('products_area');
	if(!productsArea)
	{
		return;
	}
	var crossBlock = productsArea.querySelector('.epc-cross-stock-results');
	if(crossBlock && crossBlock.parentNode)
	{
		crossBlock.parentNode.removeChild(crossBlock);
	}
}
function epcEnsureChpuFilterVisible()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	// In Docpart UI, 1 = filter expanded/visible, 0 = collapsed ("Expand filter").
	this_position_filter = 1;
	this_position_search = 1;
	var filter_div = document.getElementById('filter_div');
	var filter_position = document.getElementById('filter_position');
	var filter_div_style_body = document.getElementById('filter_div_style_body');
	var filter_div_a_text = document.getElementById('filter_div_a_text');
	if(filter_div)
	{
		filter_div.classList.remove('hidden');
		filter_div.classList.remove('col-md-12');
		filter_div.classList.add('col-md-3');
	}
	if(filter_position)
	{
		filter_position.style.display = 'block';
	}
	if(filter_div_style_body)
	{
		filter_div_style_body.style.display = 'block';
	}
	if(filter_div_a_text)
	{
		filter_div_a_text.innerHTML = '<i class="fa fa-arrow-circle-up" aria-hidden="true"></i> <?php echo translate_str_by_id(4300); ?>';
	}
	var footerFilter = document.getElementById('footer-filter');
	var footerReset = document.getElementById('footer_filter_reset');
	if(footerFilter)
	{
		footerFilter.style.display = 'block';
	}
	if(footerReset)
	{
		footerReset.style.display = 'block';
	}
	if(typeof epcSyncPartSearchLayout === 'function')
	{
		epcSyncPartSearchLayout();
	}
	this_position_filter = 1;
	this_position_search = 1;
}
function epcCollectManufacturersFromLoadedProducts()
{
	var seen = {};
	var list = [];
	function addManufacturer(name)
	{
		var normalized = String(name || '').trim();
		if(normalized === '' || seen[normalized])
		{
			return;
		}
		seen[normalized] = true;
		list.push(normalized);
	}
	function scanBucket(bucket)
	{
		if(!bucket || !bucket.Products || !bucket.Products.Manufacturers)
		{
			return;
		}
		for(var manufacturer in bucket.Products.Manufacturers)
		{
			addManufacturer(manufacturer);
		}
	}
	if(typeof Products === 'undefined' || !Products)
	{
		return list;
	}
	scanBucket(Products.Required);
	scanBucket(Products.Analogs);
	scanBucket(Products.Quick_Analogs);
	scanBucket(Products.SearchName);
	scanBucket(Products.PossibleReplacement);
	scanBucket(Products.Spare_Box);
	return list;
}
function epcSyncChpuFilterManufacturers()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	var manufacturers = epcCollectManufacturersFromLoadedProducts();
	if(!manufacturers.length)
	{
		return;
	}
	var prevChecked = {};
	var activeManufacturers = (window.epcChpuActiveFilterState && window.epcChpuActiveFilterState.manufacturers)
		? window.epcChpuActiveFilterState.manufacturers.slice(0)
		: null;
	if(filter['manufacturer_blok'].list_options && filter['manufacturer_blok'].list_options.length)
	{
		for(var p = 0; p < filter['manufacturer_blok'].list_options.length; p++)
		{
			var prevOpt = filter['manufacturer_blok'].list_options[p];
			if(prevOpt && prevOpt.text && prevOpt.value === true)
			{
				prevChecked[prevOpt.text] = true;
			}
		}
	}
	var hadPriorOptions = (Object.keys(prevChecked).length > 0) || (activeManufacturers && activeManufacturers.length > 0);
	arr_manufacturers = manufacturers;
	arr_manufacturers.sort(sortFunction);
	filter['manufacturer_blok'].list_options = new Array();
	for(var i = 0; i < arr_manufacturers.length; i++)
	{
		var manufacturerChecked = true;
		if(activeManufacturers)
		{
			manufacturerChecked = (activeManufacturers.indexOf(arr_manufacturers[i]) !== -1);
		}
		else if(hadPriorOptions)
		{
			manufacturerChecked = (prevChecked[arr_manufacturers[i]] === true);
		}
		filter['manufacturer_blok'].list_options[i] = new Object();
		filter['manufacturer_blok'].list_options[i].id = i;
		filter['manufacturer_blok'].list_options[i].value = manufacturerChecked;
		filter['manufacturer_blok'].list_options[i].text = arr_manufacturers[i];
		filter['manufacturer_blok'].list_options[i].search = arr_manufacturers[i];
		filter['manufacturer_blok'].list_options[i].match_count = 1;
	}
	if(typeof epcSyncChpuManufacturerFilterFromCheckboxes === 'function')
	{
		epcSyncChpuManufacturerFilterFromCheckboxes();
	}
	else
	{
		filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers.slice(0);
	}
}
var epcBrandPickerWarehouseDone = false;
var epcBrandPickerCatalogDone = false;
function epcChpuBrandPickerArticle()
{
	return (typeof search_object !== 'undefined' && search_object && search_object.article)
		? String(search_object.article)
		: '<?php echo $article; ?>';
}
function epcChpuNavigateToBrandArticle(manufacturer_show)
{
	var manufacturer_alias = manufacturer_show;
	if(manufacturer_alias == null || manufacturer_alias === '')
	{
		manufacturer_alias = '<?php echo $DP_Config->chpu_search_config["level_2"]["mode_2"]["url"]; ?>';
	}
	manufacturer_alias = String(manufacturer_alias).split('/').join('<?php echo $DP_Config->chpu_search_config["slash_code"]; ?>');
	location = '<?php echo $multilang_params['lang_href']; ?>/<?php echo $DP_Config->chpu_search_config["level_1"]["url"]; ?>/' + encodeURI(manufacturer_alias) + '/<?php echo $article; ?>';
}
function epcMergeCatalogBrandsIntoPicker(catalogManufacturers)
{
	if(!catalogManufacturers || !catalogManufacturers.length)
	{
		return;
	}
	var existing = {};
	for(var i = 0; i < ProductsManufacturers.length; i++)
	{
		if(ProductsManufacturers[i] && ProductsManufacturers[i].manufacturer_show)
		{
			existing[ProductsManufacturers[i].manufacturer_show] = true;
		}
	}
	var merged = [];
	for(var c = 0; c < catalogManufacturers.length; c++)
	{
		var catalogRow = catalogManufacturers[c];
		if(!catalogRow || !catalogRow.manufacturer_show || existing[catalogRow.manufacturer_show])
		{
			continue;
		}
		merged.push(catalogRow);
	}
	if(merged.length)
	{
		addManufacturersToList(merged);
	}
}
function epcBrandPickerMergeBrandsFromStockProducts(products)
{
	if(!products || !products.length)
	{
		return;
	}
	var brands = [];
	var seen = {};
	for(var i = 0; i < products.length; i++)
	{
		var p = products[i] || {};
		var brand = String(p.manufacturer || p.manufacturer_show || p.brand || '').trim();
		if(!brand)
		{
			continue;
		}
		var key = brand.toUpperCase();
		if(seen[key])
		{
			continue;
		}
		seen[key] = true;
		brands.push({
			manufacturer: brand,
			manufacturer_show: brand,
			name: String(p.name || ''),
			have_price: 1
		});
	}
	if(brands.length)
	{
		epcMergeCatalogBrandsIntoPicker(brands);
	}
}
function epcBrandPickerFetchStockPreview()
{
	if(typeof epc_brand_picker_mode === 'undefined' || !epc_brand_picker_mode)
	{
		return Promise.resolve(false);
	}
	if(typeof epc_initial_price_bunch !== 'undefined' && epc_initial_price_bunch && epc_initial_price_bunch.Products && epc_initial_price_bunch.Products.length)
	{
		return Promise.resolve(true);
	}
	if(typeof office_storage_bunches === 'undefined' || !office_storage_bunches || !office_storage_bunches.length)
	{
		return Promise.resolve(false);
	}
	var nested = null;
	for(var i = 0; i < office_storage_bunches.length; i++)
	{
		if(parseInt(office_storage_bunches[i].protocol_version, 10) === 3 && office_storage_bunches[i].office_storage_bunches && office_storage_bunches[i].office_storage_bunches.length)
		{
			nested = office_storage_bunches[i].office_storage_bunches;
			break;
		}
	}
	if(!nested)
	{
		return Promise.resolve(false);
	}
	var article = epcChpuBrandPickerArticle();
	if(!article)
	{
		return Promise.resolve(false);
	}
	var search_object_clone = {
		article: article,
		manufacturers: [],
		analogs: [],
		office_storage_bunches: nested
	};
	var priceFetchUrl = '<?= $DP_Config->domain_path . 'content/shop/docpart/ajax_getProductsOfBunch.php' ?>';
	var query = 'geo_id=<?= $geo_id ?>&async=1&tech_key=<?= urlencode($DP_Config->tech_key) ?>&user_id=<?= $user_id ?>&group_id=<?= $group_id ?>&office_id=0&storage_id=0&query=' + encodeURIComponent(JSON.stringify(search_object_clone));
	return epcFetchJsonWithTimeout(
		priceFetchUrl,
		{
			method: 'POST',
			headers: {'Content-Type': 'application/x-www-form-urlencoded;charset=utf-8;'},
			body: query,
			credentials: 'same-origin'
		},
		45000,
		null
	).then(function(chunk) {
		if(!chunk || parseInt(chunk.result, 10) !== 1 || !chunk.Products || !chunk.Products.length)
		{
			return false;
		}
		epc_initial_price_bunch = {
			result: 1,
			storage_id: 0,
			Products: chunk.Products
		};
		epc_brand_picker_stock_preview = true;
		epcBrandPickerMergeBrandsFromStockProducts(chunk.Products);
		if(typeof epcApplyInitialPriceBunch === 'function')
		{
			epcApplyInitialPriceBunch();
		}
		return true;
	}).catch(function(err) {
		console.error('epcBrandPickerFetchStockPreview', err);
		return false;
	});
}
function epcFetchCatalogArticleBrands()
{
	if(typeof epc_brand_picker_mode === 'undefined' || !epc_brand_picker_mode)
	{
		epcBrandPickerCatalogDone = true;
		return Promise.resolve();
	}
	var article = epcChpuBrandPickerArticle();
	if(!article)
	{
		epcBrandPickerCatalogDone = true;
		return Promise.resolve();
	}
	return fetch('/content/shop/docpart/ajax_epc_article_brands.php?article=' + encodeURIComponent(article), {cache: 'no-store'})
		.then(function(response) {
			if(!response.ok)
			{
				throw new Error('catalog brands request failed');
			}
			return response.json();
		})
		.then(function(data) {
			if(data && data.manufacturers && data.manufacturers.length)
			{
				epcMergeCatalogBrandsIntoPicker(data.manufacturers);
			}
		})
		.catch(function(err) {
			console.error('epcFetchCatalogArticleBrands', err);
		})
		.finally(function() {
			epcBrandPickerCatalogDone = true;
			epcTryFinalizeBrandPicker();
		});
}
function epcTryFinalizeBrandPicker()
{
	if(typeof epc_brand_picker_mode === 'undefined' || !epc_brand_picker_mode)
	{
		return;
	}
	if(!epcBrandPickerWarehouseDone || !epcBrandPickerCatalogDone)
	{
		return;
	}
	epcFinalizeManufacturersPicker();
}
function epcFinalizeManufacturersPicker()
{
	if(typeof epc_brand_picker_mode === 'undefined' || !epc_brand_picker_mode)
	{
		return;
	}
	if(typeof epc_brand_picker_stock_preview !== 'undefined' && epc_brand_picker_stock_preview)
	{
		var processingPreview = document.getElementById('processing_indicator');
		if(processingPreview)
		{
			processingPreview.innerHTML = '<br/>';
		}
		return;
	}
	manufacturersReview();
	if(ProductsManufacturers_Shown_Count == 0 && ProductsManufacturers.length == 0)
	{
		onManufacturerSelected(null);
		return;
	}
	if(ProductsManufacturers_Shown_Count == 1)
	{
		epcChpuNavigateToBrandArticle(ProductsManufacturers[0].manufacturer_show);
		return;
	}
	if(ProductsManufacturers_Shown_Count > 1)
	{
		var processing = document.getElementById('processing_indicator');
		if(processing)
		{
			processing.innerHTML = '<br/>';
		}
		var productsArea = document.getElementById('products_area');
		if(productsArea)
		{
			productsArea.scrollIntoView({behavior: 'smooth', block: 'start'});
		}
	}
}
function epcMountChpuCrossActions()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	if(typeof epc_brand_picker_mode !== 'undefined' && epc_brand_picker_mode)
	{
		return;
	}
	var bar = document.getElementById('epc-chpu-actions-bar');
	var tools = document.querySelector('.epc-parts-result-tools');
	var crossBtn = document.getElementById('epc-cross-search-btn');
	var fitmentBtn = document.getElementById('epc-fitment-check-btn');
	if(bar && crossBtn && crossBtn.parentNode !== bar)
	{
		bar.appendChild(crossBtn);
	}
	if(bar && fitmentBtn && fitmentBtn.parentNode !== bar)
	{
		bar.insertBefore(fitmentBtn, crossBtn && crossBtn.parentNode === bar ? crossBtn : null);
	}
	if(bar && tools)
	{
		var leftover = tools.querySelectorAll('a,button');
		for(var t = 0; t < leftover.length; t++)
		{
			if(leftover[t].parentNode === tools)
			{
				bar.appendChild(leftover[t]);
			}
		}
		tools.style.display = 'none';
	}
}
function epcChpuInitFiltersAfterLoad()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	filter['storages_blok'].show = 0;
	if(typeof epcSyncChpuManufacturerFilterFromCheckboxes === 'function')
	{
		epcSyncChpuManufacturerFilterFromCheckboxes();
	}
	else if(typeof arr_manufacturers !== 'undefined' && arr_manufacturers.length)
	{
		filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers.slice(0);
	}
	if(typeof arr_storages !== 'undefined' && arr_storages.length)
	{
		filter['storages_blok'].storages_in_filter = arr_storages.slice(0);
	}
	if(typeof initProperiesValues === 'function' && !window.epcChpuSkipFilterDomRead)
	{
		try
		{
			initProperiesValues();
		}
		catch(sliderErr) {}
	}
	var rangeBlocks = ['price_blok', 'time_to_exe_blok', 'exist_blok'];
	for(var r = 0; r < rangeBlocks.length; r++)
	{
		var rangeBlock = filter[rangeBlocks[r]];
		if(!rangeBlock || rangeBlock.min_need === undefined || rangeBlock.max_need === undefined)
		{
			continue;
		}
		if(rangeBlock.old_min_need === undefined)
		{
			rangeBlock.old_min_need = rangeBlock.min_need;
		}
		if(rangeBlock.old_max_need === undefined)
		{
			rangeBlock.old_max_need = rangeBlock.max_need;
		}
	}
}
function epcRemoveWarehouseSidebarBlock()
{
	var prime = document.getElementById('epc_prime_storages_blok');
	if(prime && prime.parentNode)
	{
		prime.parentNode.removeChild(prime);
	}
	var filterPosition = document.getElementById('filter_position');
	if(!filterPosition)
	{
		return;
	}
	var storageBlocks = filterPosition.querySelectorAll('[data-epc-filter="storages"]');
	for(var i = 0; i < storageBlocks.length; i++)
	{
		var node = storageBlocks[i];
		if(node.parentNode)
		{
			node.parentNode.removeChild(node);
		}
	}
	var properties = filterPosition.querySelectorAll('.one_property');
	for(var j = 0; j < properties.length; j++)
	{
		if(properties[j].innerHTML.indexOf('Warehouse checkboxes become active after stock loads') !== -1 && properties[j].parentNode)
		{
			properties[j].parentNode.removeChild(properties[j]);
		}
	}
}
var epcCrossFetchPromise = null;
var epcCrossFetchArticleKey = '';
if(typeof window.epcCrossAllReferences === 'undefined') { window.epcCrossAllReferences = []; }
if(typeof window.epcCrossReferencesTotal === 'undefined') { window.epcCrossReferencesTotal = 0; }
if(typeof window.epcCrossStockKeyMap === 'undefined') { window.epcCrossStockKeyMap = {}; }
if(typeof window.epcManufacturerSynonymMap === 'undefined') { window.epcManufacturerSynonymMap = {}; }
if(typeof window.epcManufacturerCanonicalMap === 'undefined') { window.epcManufacturerCanonicalMap = {}; }
function epcCrossFallbackSeedRows()
{
	if(typeof epcCrossFallbackRows === 'undefined' || !epcCrossFallbackRows || !epcCrossFallbackRows.length)
	{
		return [];
	}
	return epcCrossFallbackRows.map(function(row) {
		return {
			brand: row.brand || '',
			article: row.article || '',
			article_norm: row.article || '',
			source: 'cp_fallback'
		};
	});
}
function epcApplyCrossFallbackState(reasonText)
{
	var fallbackRows = epcCrossFallbackSeedRows();
	if(!fallbackRows.length)
	{
		return false;
	}
	window.epcCrossAllReferences = fallbackRows.slice(0);
	if(typeof epcCrossMergeFallbackIntoReferences === 'function')
	{
		epcCrossMergeFallbackIntoReferences();
	}
	if(typeof epcSyncCrossReferenceGlobals === 'function')
	{
		epcSyncCrossReferenceGlobals();
	}
	var refLoaded = fallbackRows.length;
	window.epcCrossReferencesLoaded = refLoaded;
	window.epcCrossReferencesTotal = refLoaded;
	epcCrossReferencesLoaded = refLoaded;
	epcCrossReferencesTotal = refLoaded;
	var count = document.getElementById('epc-cross-search-count');
	var crossBtn = document.getElementById('epc-cross-search-btn');
	if(count)
	{
		count.textContent = refLoaded + ' references' + (reasonText ? ' (saved)' : '');
	}
	if(crossBtn)
	{
		crossBtn.disabled = false;
		crossBtn.setAttribute('title', reasonText || ('Open ' + refLoaded + ' cross references from saved crosses'));
	}
	return true;
}
function epcFetchCrossData(article)
{
	var articleValue = String(article || (typeof search_object !== 'undefined' ? search_object.article : '') || '').trim();
	if(!articleValue)
	{
		return Promise.resolve({status: true, references: [], stock: [], total: 0});
	}
	var fetchKey = articleValue + '|' + ((typeof search_object !== 'undefined' && search_object && search_object.requested_manufacturer) ? search_object.requested_manufacturer : '');
	if(epcCrossFetchPromise && epcCrossFetchArticleKey === fetchKey)
	{
		return epcCrossFetchPromise;
	}
	epcCrossFetchArticleKey = fetchKey;
	epcCrossFetchPromise = null;
	var crossUrl = '/content/shop/docpart/ajax_epc_cross_search.php?article=' + encodeURIComponent(articleValue);
	if(typeof search_object !== 'undefined' && search_object)
	{
		var crossBrand = (search_object.requested_manufacturer || search_object.manufacturer || '').toString().trim();
		if(!crossBrand && typeof SelectedManufacturer !== 'undefined' && SelectedManufacturer)
		{
			crossBrand = String(SelectedManufacturer).trim();
		}
		if(crossBrand !== '')
		{
			crossUrl += '&brand=' + encodeURIComponent(crossBrand);
		}
	}
	epcCrossFetchPromise = epcFetchJsonWithTimeout(crossUrl, {credentials: 'same-origin'}, 60000, {status: false, references: [], stock: [], total: 0})
		.then(function(data) {
			data = data || {};
			window.epcCrossAllReferences = (data.references && data.references.length) ? data.references.slice(0) : [];
			if(typeof epcCrossRebuildBrandByArticleMap === 'function')
			{
				epcCrossRebuildBrandByArticleMap();
			}
			var refLoaded = data.reference_count ? parseInt(data.reference_count, 10) : window.epcCrossAllReferences.length;
			var refTotal = data.total ? parseInt(data.total, 10) : refLoaded;
			if((!data.status || refLoaded <= 0) && epcApplyCrossFallbackState('Cross search is using saved CP crosses while live lookup catches up.'))
			{
				data.references = window.epcCrossAllReferences.slice(0);
				data.total = window.epcCrossReferencesTotal;
				data.reference_count = window.epcCrossReferencesLoaded;
				refLoaded = data.reference_count;
				refTotal = data.total;
			}
			window.epcCrossReferencesLoaded = refLoaded;
			window.epcCrossReferencesTotal = refTotal;
			epcCrossReferencesLoaded = refLoaded;
			epcCrossReferencesTotal = refTotal;
			window.epcPendingCrossStock = (data.stock && data.stock.length) ? data.stock.slice(0) : [];
			window.epcLastCrossStock = window.epcPendingCrossStock.slice(0);
			window.epcManufacturerSynonymMap = (data.manufacturer_synonyms && typeof data.manufacturer_synonyms === 'object') ? data.manufacturer_synonyms : {};
			window.epcManufacturerCanonicalMap = (data.manufacturer_canonical && typeof data.manufacturer_canonical === 'object') ? data.manufacturer_canonical : {};
			if(typeof epcPendingCrossStock !== 'undefined')
			{
				epcPendingCrossStock = window.epcPendingCrossStock;
			}
			if(typeof epcSyncCrossReferenceGlobals === 'function')
			{
				epcSyncCrossReferenceGlobals();
			}
			window.epcCrossStockKeyMap = {};
			epcCrossStockKeyMap = window.epcCrossStockKeyMap;
			if(data.stock && data.stock.length && typeof epcMarkCrossStockKeys === 'function')
			{
				epcMarkCrossStockKeys(data.stock);
			}
			if(typeof epcRebuildCrossInStockMaps === 'function')
			{
				epcRebuildCrossInStockMaps();
			}
			else if(typeof epcCrossAllReferences !== 'undefined')
			{
				epcCrossAllReferences = window.epcCrossAllReferences;
				epcCrossReferencesTotal = window.epcCrossReferencesTotal;
			}
			var count = document.getElementById('epc-cross-search-count');
			var crossBtn = document.getElementById('epc-cross-search-btn');
			if(count)
			{
				var stockCount = (data.stock && data.stock.length) ? data.stock.length : 0;
				if(typeof epcFormatCrossCountLabel === 'function')
				{
					count.textContent = epcFormatCrossCountLabel(refLoaded, refTotal, stockCount);
				}
				else
				{
					count.textContent = refLoaded + ' crosses' + (stockCount > 0 ? (' · ' + stockCount + ' in stock') : '');
				}
			}
			if(crossBtn)
			{
				crossBtn.disabled = !(refLoaded || refTotal);
				if(refTotal > refLoaded && refLoaded > 0)
				{
					crossBtn.setAttribute('title', 'Showing ' + refLoaded + ' of ' + refTotal.toLocaleString() + ' cross references on this page (full catalog is larger). Click to browse or download.');
				}
				else
				{
					crossBtn.setAttribute('title', (refTotal || refLoaded) ? ('Open ' + (refTotal || refLoaded) + ' cross references') : 'No cross references found');
				}
			}
			if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && window.epcLastCrossStock && window.epcLastCrossStock.length && typeof epcChpuEnsureCrossStockTableVisible === 'function')
			{
				var area = document.getElementById('products_area');
				if(area)
				{
					if(area.innerHTML.indexOf('all_table_products') === -1)
					{
						epcChpuEnsureCrossStockTableVisible(window.epcLastCrossStock, false);
					}
					else if(typeof epcChpuAppendCrossNotInStockToTable === 'function')
					{
						epcChpuSyncCrossDataBeforeRender(window.epcLastCrossStock);
						epcChpuAppendCrossNotInStockToTable();
					}
				}
			}
			return data;
		})
		.catch(function(err) {
			console.error('epcFetchCrossData', err);
			window.epcPendingCrossStock = [];
			window.epcManufacturerSynonymMap = {};
			epcPendingCrossStock = [];
			if(epcApplyCrossFallbackState('Cross search is using saved CP crosses while live lookup is unavailable.'))
			{
				return {
					status: true,
					references: window.epcCrossAllReferences.slice(0),
					stock: [],
					total: window.epcCrossReferencesTotal,
					reference_count: window.epcCrossReferencesLoaded
				};
			}
			window.epcCrossAllReferences = [];
			epcCrossAllReferences = [];
			var count = document.getElementById('epc-cross-search-count');
			var crossBtn = document.getElementById('epc-cross-search-btn');
			if(count) { count.textContent = 'Cross search unavailable'; }
			if(crossBtn) {
				crossBtn.disabled = true;
				crossBtn.setAttribute('title', 'Cross search is temporarily unavailable');
			}
			return {status: false, references: [], stock: [], total: 0};
		});
	return epcCrossFetchPromise;
}
function epcChpuShowProcessingIndicator(messageHtml)
{
	var processing = document.getElementById('processing_indicator');
	if(!processing)
	{
		return;
	}
	processing.style.display = '';
	if(messageHtml)
	{
		processing.innerHTML = messageHtml;
	}
}
function epcChpuHideProcessingIndicator()
{
	var processing = document.getElementById('processing_indicator');
	if(processing)
	{
		processing.innerHTML = '';
		processing.style.display = 'none';
	}
}
function epcChpuAreaHasStockPaint(area)
{
	if(!area || !area.innerHTML)
	{
		return false;
	}
	if(area.querySelector && area.querySelector('.epc-ssr-warehouse-table, .epc-ssr-warehouse-row, .epc-product-actions, .epc-btn-cart, .epc-btn-quote'))
	{
		return true;
	}
	var html = area.innerHTML;
	return html.indexOf('all_table_products') !== -1
		&& (html.indexOf('epc-ssr-warehouse') !== -1
			|| html.indexOf('epc-product-actions') !== -1
			|| html.indexOf('epc-btn-cart') !== -1
			|| html.indexOf('td_exist') !== -1);
}
function epcChpuApplyCombinedResults(priceData)
{
	var processing = document.getElementById('processing_indicator');
	var productsArea = document.getElementById('products_area');
	var hadTableRows = false;
	try
	{
		if(typeof bindBunchResult !== 'function' || typeof resultReview !== 'function')
		{
			return false;
		}
		if((!priceData || parseInt(priceData.result, 10) !== 1 || !priceData.Products || !priceData.Products.length)
			&& typeof epc_initial_price_bunch !== 'undefined'
			&& epc_initial_price_bunch
			&& epc_initial_price_bunch.Products
			&& epc_initial_price_bunch.Products.length)
		{
			var initialArticle = String(epc_initial_price_bunch.Products[0].article || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
			var currentArticle = (typeof search_object !== 'undefined' && search_object && search_object.article)
				? String(search_object.article).replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase()
				: '';
			if(initialArticle !== '' && initialArticle === currentArticle)
			{
				priceData = epc_initial_price_bunch;
			}
		}
		if(priceData && parseInt(priceData.result, 10) === 1 && priceData.Products && priceData.Products.length)
		{
			bindBunchResult(priceData);
			hadTableRows = true;
		}
		var crossStock = [];
		if(typeof epcPendingCrossStock !== 'undefined' && epcPendingCrossStock && epcPendingCrossStock.length)
		{
			crossStock = epcPendingCrossStock.slice(0);
		}
		else if(window.epcPendingCrossStock && window.epcPendingCrossStock.length)
		{
			crossStock = window.epcPendingCrossStock.slice(0);
		}
		else if(window.epcLastCrossStock && window.epcLastCrossStock.length)
		{
			crossStock = window.epcLastCrossStock.slice(0);
		}
		if(crossStock.length)
		{
			window.epcLastCrossStock = crossStock.slice(0);
		}
		if(crossStock.length && typeof epcBindCrossStockProducts === 'function')
		{
			epcBindCrossStockProducts(crossStock);
			hadTableRows = true;
		}
		if(crossStock.length && typeof epcMarkCrossStockKeys === 'function')
		{
			epcMarkCrossStockKeys(crossStock);
		}
		if(typeof epcRebuildCrossInStockMaps === 'function')
		{
			epcRebuildCrossInStockMaps();
		}
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
		var shouldRenderTable = hadTableRows || (typeof epcChpuHasRenderableRows === 'function' && epcChpuHasRenderableRows());
		if(shouldRenderTable)
		{
			var reviewed = false;
			var hadTableBeforeReview = (productsArea && productsArea.innerHTML.indexOf('all_table_products') !== -1);
			if(typeof epcChpuSafeResultReview === 'function')
			{
				reviewed = epcChpuSafeResultReview();
			}
			else
			{
				try
				{
					resultReview();
					reviewed = true;
				}
				catch(reviewErr)
				{
					console.error('epcChpuApplyCombinedResults resultReview', reviewErr);
				}
			}
			if(!reviewed && typeof epcChpuEnsureCrossStockTableVisible === 'function')
			{
				if(!epcChpuEnsureCrossStockTableVisible(crossStock, false) && productsArea && typeof epcPartSearchNotAvailableHTML === 'function' && typeof epcPartSearchShouldShowUnavailableNotice === 'function' && epcPartSearchShouldShowUnavailableNotice())
				{
					productsArea.innerHTML = epcPartSearchNotAvailableHTML();
				}
			}
			else if(!reviewed && productsArea && typeof epcPartSearchNotAvailableHTML === 'function' && typeof epcPartSearchShouldShowUnavailableNotice === 'function' && epcPartSearchShouldShowUnavailableNotice())
			{
				productsArea.innerHTML = epcPartSearchNotAvailableHTML();
			}
			if(productsArea && !hadTableBeforeReview && productsArea.innerHTML.indexOf('all_table_products') === -1 && crossStock.length && typeof epcChpuEnsureCrossStockTableVisible === 'function')
			{
				epcChpuEnsureCrossStockTableVisible(crossStock, false);
			}
			// resultReview can filter everything away; restore warehouse/actions paint.
			if(productsArea && !epcChpuAreaHasStockPaint(productsArea))
			{
				if(crossStock.length && typeof epcChpuEnsureCrossStockTableVisible === 'function' && epcChpuEnsureCrossStockTableVisible(crossStock, false))
				{
					// restored from cross stock
				}
				else if(typeof epcApplyInitialPriceBunch === 'function')
				{
					epcApplyInitialPriceBunch();
				}
				else if(typeof epcChpuBuildDirectStockTableHtml === 'function')
				{
					var rebuildHtml = epcChpuBuildDirectStockTableHtml();
					if(rebuildHtml)
					{
						productsArea.innerHTML = rebuildHtml;
					}
				}
			}
		}
		else if(productsArea)
		{
			if(typeof epcChpuEnsureCrossStockTableVisible === 'function' && epcChpuEnsureCrossStockTableVisible(crossStock, false))
			{
				// Cross stock table rendered without standard resultReview rows.
			}
			else if(epcChpuAreaHasStockPaint(productsArea))
			{
				// Keep SSR / prior warehouse paint; do not replace with "Goods not found".
				epcChpuHideProcessingIndicator();
			}
			else if(typeof epcApplyInitialPriceBunch === 'function' && epcApplyInitialPriceBunch())
			{
				epcChpuHideProcessingIndicator();
			}
			else if(typeof epcPartSearchShouldShowUnavailableNotice === 'function' && !epcPartSearchShouldShowUnavailableNotice())
			{
				epcChpuHideProcessingIndicator();
			}
			else
			{
			productsArea.innerHTML = (typeof epcPartSearchNotAvailableHTML === 'function')
				? epcPartSearchNotAvailableHTML()
				: "<p><?php echo translate_str_by_id(4078); ?></p>";
			if(typeof epcCrossRefsNotInStockHTML === 'function')
			{
				var missingHtml = epcCrossRefsNotInStockHTML();
				if(missingHtml !== '')
				{
					productsArea.innerHTML += missingHtml;
				}
			}
			else if(typeof epcCrossRefsNotInStockRowsHTML === 'function')
			{
				var missingRows = epcCrossRefsNotInStockRowsHTML();
				if(missingRows !== '')
				{
					productsArea.innerHTML += '<table id="all_table_products"><tbody>' + missingRows + '</tbody></table>';
				}
			}
			if(typeof showPropertiesWidgets === 'function')
			{
				try
				{
					showPropertiesWidgets();
				}
				catch(filterErr)
				{
					console.error('epcChpuApplyCombinedResults showPropertiesWidgets', filterErr);
				}
			}
			}
		}
		if(productsArea && productsArea.innerHTML.indexOf('all_table_products') === -1 && crossStock.length && typeof epcChpuEnsureCrossStockTableVisible === 'function')
		{
			epcChpuEnsureCrossStockTableVisible(crossStock, false);
		}
		else if(productsArea && productsArea.innerHTML.indexOf('all_table_products') !== -1 && typeof epcChpuAppendCrossNotInStockToTable === 'function')
		{
			epcChpuSyncCrossDataBeforeRender(crossStock);
			epcChpuAppendCrossNotInStockToTable();
		}
		if(crossStock.length)
		{
			epcPendingCrossStock = null;
			window.epcPendingCrossStock = null;
		}
		if(typeof epcEnsureChpuFilterVisible === 'function')
		{
			epcEnsureChpuFilterVisible();
		}
		if(typeof epcSyncPartSearchLayout === 'function')
		{
			epcSyncPartSearchLayout();
		}
		if(typeof epcMountChpuCrossActions === 'function')
		{
			epcMountChpuCrossActions();
		}
		if(typeof epcRemoveWarehouseSidebarBlock === 'function')
		{
			epcRemoveWarehouseSidebarBlock();
		}
		return hadTableRows;
	}
	catch(err)
	{
		console.error('epcChpuApplyCombinedResults', err);
		if(productsArea && epcChpuAreaHasStockPaint(productsArea))
		{
			epcChpuHideProcessingIndicator();
			return true;
		}
		if(productsArea && typeof epcApplyInitialPriceBunch === 'function' && epcApplyInitialPriceBunch())
		{
			epcChpuHideProcessingIndicator();
			return true;
		}
		if(productsArea && (!productsArea.innerHTML || productsArea.innerHTML.replace(/\s/g, '') === ''))
		{
			productsArea.innerHTML = (typeof epcPartSearchNotAvailableHTML === 'function')
				? epcPartSearchNotAvailableHTML()
				: "<p><?php echo translate_str_by_id(4078); ?></p>";
		}
		if(processing && productsArea && !epcChpuAreaHasStockPaint(productsArea))
		{
			epcChpuShowProcessingIndicator("<p><?php echo translate_str_by_id(4078); ?></p>");
		}
		return false;
	}
	finally
	{
		var area = document.getElementById('products_area');
		var hasRenderedTable = area && area.innerHTML && area.innerHTML.indexOf('all_table_products') !== -1;
		var hasBoundProducts = (typeof Products !== 'undefined' && Products.All && Products.All.length > 0);
		if(hasRenderedTable)
		{
			epcChpuHideProcessingIndicator();
		}
		else if(hasBoundProducts && typeof epcChpuSafeResultReview === 'function')
		{
			try
			{
				epcChpuSafeResultReview();
			}
			catch(reviewErr)
			{
				console.error('epcChpuApplyCombinedResults retry review', reviewErr);
			}
			if(area && area.innerHTML && area.innerHTML.indexOf('all_table_products') !== -1)
			{
				epcChpuHideProcessingIndicator();
			}
			else
			{
				epcChpuHideProcessingIndicator();
			}
		}
		else if(processing)
		{
			epcChpuHideProcessingIndicator();
		}
		Products_All_Asked = true;
		epcChpuMainSearchPending = false;
	}
}
function epcFinishChpuPriceLoad(data)
{
	return epcChpuApplyCombinedResults(data);
}
function epcRunChpuPriceSearch()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	window.epcChpuCombinedApplyDone = false;
	epcClearCrossStockFromResults();
	if(typeof epcResetChpuFilterState === 'function')
	{
		epcResetChpuFilterState();
	}
	Products_All_Asked = false;
	var productsAreaNow = document.getElementById('products_area');
	var alreadyHasStock = typeof epcChpuAreaHasStockPaint === 'function' && epcChpuAreaHasStockPaint(productsAreaNow);
	if(!alreadyHasStock && typeof epcApplyInitialPriceBunch === 'function' && epcApplyInitialPriceBunch())
	{
		alreadyHasStock = true;
		productsAreaNow = document.getElementById('products_area');
	}
	// Keep warehouse rows visible; only show the warehouse spinner when area is empty.
	if(!alreadyHasStock)
	{
		epcChpuShowProcessingIndicator("<p><?php echo translate_str_by_id(4294); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>");
	}
	else
	{
		epcChpuHideProcessingIndicator();
	}
	var priceFetchPromise = null;
	var priceFetchUrl = '<?= $DP_Config->domain_path . 'content/shop/docpart/ajax_getProductsOfBunch.php' ?>';
	var priceFetchOpts = {
		method: 'POST',
		headers: {'Content-Type': 'application/x-www-form-urlencoded;charset=utf-8;'},
		credentials: 'same-origin'
	};
	for(var i = 0; i < office_storage_bunches.length; i++)
	{
		if(parseInt(office_storage_bunches[i].protocol_version, 10) !== 3)
		{
			continue;
		}
		var nested_bunches = office_storage_bunches[i].office_storage_bunches || [];
		if(!nested_bunches.length)
		{
			break;
		}
		var search_object_clone = {};
		for(var key in search_object)
		{
			search_object_clone[key] = search_object[key];
		}
		search_object_clone.office_storage_bunches = nested_bunches;
		search_object_clone.analogs = [];
		search_object_clone.manufacturers = [];
		if(typeof epcChpuFillPriceManufacturersForBunch === 'function')
		{
			epcChpuFillPriceManufacturersForBunch(search_object_clone, nested_bunches);
		}
		var query = 'geo_id=<?= $geo_id ?>&async=1&tech_key=<?= urlencode($DP_Config->tech_key) ?>&user_id=<?= $user_id ?>&group_id=<?= $group_id ?>&office_id=0&storage_id=0&query=' + encodeURIComponent(JSON.stringify(search_object_clone));
		priceFetchPromise = epcFetchJsonWithTimeout(
			priceFetchUrl,
			{
				method: priceFetchOpts.method,
				headers: priceFetchOpts.headers,
				body: query,
				credentials: priceFetchOpts.credentials
			},
			45000,
			null
		).then(function(chunk) {
			if(!chunk || parseInt(chunk.result, 10) !== 1 || !chunk.Products || !chunk.Products.length)
			{
				return null;
			}
			return chunk;
		});
		break;
	}
	if(!priceFetchPromise)
	{
		priceFetchPromise = Promise.resolve(null);
	}
	var crossFetchPromise = (typeof epcFetchCrossData === 'function')
		? epcFetchCrossData(search_object.article)
		: Promise.resolve({references: [], stock: []});
	Promise.all([priceFetchPromise, crossFetchPromise])
		.then(function(results) {
			var crossData = results[1];
			if(crossData && crossData.stock && crossData.stock.length)
			{
				window.epcPendingCrossStock = crossData.stock.slice(0);
				window.epcLastCrossStock = window.epcPendingCrossStock.slice(0);
				epcPendingCrossStock = window.epcPendingCrossStock;
			}
			window.epcChpuCombinedApplyDone = true;
			epcChpuApplyCombinedResults(results[0]);
		})
		.catch(function(err) {
			console.error('epcRunChpuPriceSearch', err);
			window.epcChpuCombinedApplyDone = true;
			// Prefer restoring server warehouse stock over an empty/unavailable paint after 524/timeouts.
			epcChpuApplyCombinedResults(null);
		});
}
function epcFetchChpuPriceBunchDirect()
{
	epcRunChpuPriceSearch();
}
function epcUsesArticleOnlyStockUi()
{
	return (typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
		|| (typeof epc_brand_picker_stock_preview !== 'undefined' && epc_brand_picker_stock_preview);
}
function epcChpuTableNeedsActionHydration(area)
{
	if(!area || !area.innerHTML)
	{
		return false;
	}
	// SSR warehouse seed has stock rows but empty Actions (no cart/quote/WhatsApp/fitment).
	if(area.querySelector && area.querySelector('.epc-ssr-warehouse-table, .epc-ssr-warehouse-row, .epc-ssr-warehouse-banner'))
	{
		return true;
	}
	if(area.innerHTML.indexOf('all_table_products') === -1)
	{
		return false;
	}
	return area.innerHTML.indexOf('epc-product-actions') === -1
		&& area.innerHTML.indexOf('epc-btn-cart') === -1
		&& area.innerHTML.indexOf('epc-btn-quote') === -1;
}
function epcApplyInitialPriceBunch()
{
	if(typeof epc_initial_price_bunch === 'undefined' || !epc_initial_price_bunch || !epc_initial_price_bunch.Products || !epc_initial_price_bunch.Products.length)
	{
		return false;
	}
	if(typeof epc_brand_picker_mode !== 'undefined' && epc_brand_picker_mode)
	{
		epc_brand_picker_stock_preview = true;
	}
	var productsArea = document.getElementById('products_area');
	if(productsArea)
	{
		var crossBlock = productsArea.querySelector('.epc-cross-stock-results');
		if(crossBlock && crossBlock.parentNode)
		{
			crossBlock.parentNode.removeChild(crossBlock);
		}
	}
	// Fresh bind so SSR seed is replaced by full search-result rows with actions.
	if(typeof epcResetChpuProductsState === 'function' && (!Products || !Products.All || !Products.All.length || (productsArea && epcChpuTableNeedsActionHydration(productsArea))))
	{
		epcResetChpuProductsState();
	}
	bindBunchResult(epc_initial_price_bunch);
	Products_All_Asked = true;
	var processing = document.getElementById('processing_indicator');
	try
	{
		if(typeof onGetStoragesData === 'function')
		{
			onGetStoragesData();
		}
		else if(typeof epcChpuSafeResultReview === 'function')
		{
			epcChpuSafeResultReview();
		}
		else if(typeof resultReview === 'function')
		{
			resultReview();
		}
		if(typeof showPropertiesWidgets === 'function')
		{
			showPropertiesWidgets();
		}
	}
	catch(renderErr)
	{
		console.error('epcApplyInitialPriceBunch', renderErr);
	}
	// If resultReview failed/filtered everything, or left SSR without actions, rebuild from bound Products.
	if(productsArea && (productsArea.innerHTML.indexOf('all_table_products') === -1 || epcChpuTableNeedsActionHydration(productsArea)))
	{
		try
		{
			if(typeof epcChpuBuildDirectStockTableHtml === 'function')
			{
				var fallbackHtml = epcChpuBuildDirectStockTableHtml();
				if(fallbackHtml)
				{
					productsArea.innerHTML = fallbackHtml;
				}
			}
		}
		catch(fallbackErr)
		{
			console.error('epcApplyInitialPriceBunch fallback', fallbackErr);
		}
	}
	if(processing)
	{
		processing.innerHTML = '';
		processing.style.display = 'none';
	}
	epcClearManufacturersPollTimeout();
	return true;
}
var epc_chpu_direct_pricing = <?php echo !empty($epc_chpu_direct_pricing) ? 'true' : 'false'; ?>;
var epc_brand_picker_mode = <?php echo !empty($epc_brand_picker_mode) ? 'true' : 'false'; ?>;
var epc_brand_picker_stock_preview = <?php echo (!empty($epc_brand_picker_mode) && !empty($epc_initial_price_bunch['Products'])) ? 'true' : 'false'; ?>;
var epc_chpu_search_on = <?php echo !empty($DP_Config->chpu_search_config['chpu_search_on']) ? 'true' : 'false'; ?>;
var epc_chpu_lang_href = <?php echo json_encode($multilang_params['lang_href'], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE); ?>;
var epc_chpu_parts_url = <?php echo json_encode($DP_Config->chpu_search_config['level_1']['url'], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE); ?>;
var epc_chpu_brands_url = <?php echo json_encode($DP_Config->chpu_search_config['level_2']['mode_1']['url'], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE); ?>;
var epc_chpu_slash_code = <?php echo json_encode($DP_Config->chpu_search_config['slash_code'], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE); ?>;
function epcChpuNormalizeArticleForUrl(article)
{
	return String(article || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
}
function epcChpuBrandArticleUrl(brand, article)
{
	var articleNorm = epcChpuNormalizeArticleForUrl(article);
	var brandName = String(brand || '').trim();
	if(!articleNorm)
	{
		return epc_chpu_lang_href + '/shop/part_search';
	}
	if(typeof epc_chpu_search_on !== 'undefined' && epc_chpu_search_on)
	{
		if(!brandName)
		{
			return epc_chpu_lang_href + '/' + epc_chpu_parts_url + '/' + epc_chpu_brands_url + '/' + encodeURIComponent(articleNorm);
		}
		var manufacturerAlias = brandName.split('/').join(epc_chpu_slash_code);
		return epc_chpu_lang_href + '/' + epc_chpu_parts_url + '/' + encodeURIComponent(manufacturerAlias) + '/' + encodeURIComponent(articleNorm);
	}
	var legacyUrl = epc_chpu_lang_href + '/shop/part_search?article=' + encodeURIComponent(articleNorm);
	if(brandName)
	{
		legacyUrl += '&brend=' + encodeURIComponent(brandName);
	}
	return legacyUrl;
}
var epcChpuMainSearchPending = false;
var epcPendingCrossStock = null;
function epcMarkChpuMainSearchPending()
{
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		epcChpuMainSearchPending = true;
	}
}
function epcMarkChpuMainSearchComplete()
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	epcChpuMainSearchPending = false;
}
function epcResetChpuSupplierPolling()
{
	Products_All_Asked = false;
	if(typeof office_storage_bunches !== 'undefined')
	{
		for(var i = 0; i < office_storage_bunches.length; i++)
		{
			office_storage_bunches[i].sent = 0;
		}
	}
	if(typeof storages_groups !== 'undefined')
	{
		for(var g = 0; g < storages_groups.length; g++)
		{
			storages_groups[g].sent = 0;
			storages_groups[g].manufacturers_sent = 0;
		}
	}
}
function epcResetChpuFilterState()
{
	if(typeof filter === 'undefined')
	{
		return;
	}
	var rangeBlocks = ['price_blok', 'time_to_exe_blok', 'exist_blok'];
	for(var i = 0; i < rangeBlocks.length; i++)
	{
		if(!filter[rangeBlocks[i]])
		{
			continue;
		}
		filter[rangeBlocks[i]].min_value = undefined;
		filter[rangeBlocks[i]].max_value = undefined;
		filter[rangeBlocks[i]].min_need = undefined;
		filter[rangeBlocks[i]].max_need = undefined;
		filter[rangeBlocks[i]].old_min_need = undefined;
		filter[rangeBlocks[i]].old_max_need = undefined;
	}
	if(filter['manufacturer_blok'])
	{
		filter['manufacturer_blok'].list_options = new Array();
		filter['manufacturer_blok'].manufacturer_in_filter = new Array();
	}
	this_filter = '';
}
function epcResetChpuProductsState()
{
	Products = new Object;
	Products.Required = new Object;
	Products.Required.Products = new Object;
	Products.Required.Products.Manufacturers = new Object;
	Products.Required.ProductsTypes = new Array();
	Products.SearchName = new Object;
	Products.SearchName.Products = new Object;
	Products.SearchName.Products.Manufacturers = new Object;
	Products.SearchName.ProductsTypes = new Array();
	Products.Quick_Analogs = new Object;
	Products.Quick_Analogs.Products = new Object;
	Products.Quick_Analogs.Products.Manufacturers = new Object;
	Products.Quick_Analogs.ProductsTypes = new Array();
	Products.Analogs = new Object;
	Products.Analogs.Products = new Object;
	Products.Analogs.Products.Manufacturers = new Object;
	Products.Analogs.ProductsTypes = new Array();
	Products.PossibleReplacement = new Object;
	Products.PossibleReplacement.Products = new Object;
	Products.PossibleReplacement.Products.Manufacturers = new Object;
	Products.PossibleReplacement.ProductsTypes = new Array();
	Products.Spare_Box = new Object;
	Products.Spare_Box.Products = new Object;
	Products.Spare_Box.Products.Manufacturers = new Object;
	Products.Spare_Box.ProductsTypes = new Array();
	Products.All = new Array();
	arr_manufacturers = new Array();
	arr_storages = new Array();
	arr_storages_color = new Object();
}
function epcChpuStartFullPriceSearch(manufacturer_show)
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return;
	}
	epcCrossFetchPromise = null;
	window.epcCrossAllReferences = [];
	window.epcCrossReferencesTotal = 0;
	window.epcPendingCrossStock = null;
	window.epcCrossStockKeyMap = {};
	window.epcManufacturerSynonymMap = {};
	window.epcManufacturerCanonicalMap = {};
	window.epcChpuActiveFilterState = null;
	window.epcChpuManufacturersFilterSynced = false;
	if(typeof epcSyncCrossReferenceGlobals === 'function')
	{
		epcSyncCrossReferenceGlobals();
	}
	epcMarkChpuMainSearchPending();
	epcResetChpuSupplierPolling();
	epcResetChpuProductsState();
	if(typeof epcResetChpuFilterState === 'function')
	{
		epcResetChpuFilterState();
	}
	epcClearCrossStockFromResults();
	var productsArea = document.getElementById('products_area');
	var initialArticleKeep = '';
	if(typeof epc_initial_price_bunch !== 'undefined' && epc_initial_price_bunch && epc_initial_price_bunch.Products && epc_initial_price_bunch.Products.length)
	{
		initialArticleKeep = String(epc_initial_price_bunch.Products[0].article || '').replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
	}
	var currentArticleKeep = (typeof search_object !== 'undefined' && search_object && search_object.article)
		? String(search_object.article).replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase()
		: '';
	var keepInitialWarehousePaint = (
		initialArticleKeep !== ''
		&& initialArticleKeep === currentArticleKeep
		&& typeof epcChpuAreaHasStockPaint === 'function'
		&& epcChpuAreaHasStockPaint(productsArea)
	);
	// Never blank a successful warehouse paint while the slow supplier poll runs —
	// a 524/empty ajax response used to leave guests with an empty results area.
	if(productsArea && !keepInitialWarehousePaint)
	{
		productsArea.innerHTML = '';
	}
	if(!keepInitialWarehousePaint)
	{
		epcChpuShowProcessingIndicator("<p><?php echo translate_str_by_id(4294); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>");
	}
	// Brand+article CHPU: one protocol-3 query across all UAE price warehouses (article-only SQL).
	// Do not call onManufacturerSelected() — that runs getStoragesDataAsync (1/1 loader) and filters by brand in SQL.
	epcRunChpuPriceSearch();
}
function epcFinishStoragesSearchUi()
{
	var processing = document.getElementById('processing_indicator');
	if(Products.All.length == 0)
	{
		if(processing)
		{
			processing.innerHTML = "<p><?php echo translate_str_by_id(4078); ?></p>";
		}
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && typeof epcRunChpuPriceSearch === 'function' && !epcChpuMainSearchPending)
		{
			epcRunChpuPriceSearch();
			return;
		}
		if(typeof epcFetchChpuPriceBunchDirect === 'function')
		{
			epcFetchChpuPriceBunchDirect();
			return;
		}
	}
	else if(processing)
	{
		processing.innerHTML = "";
	}
	if(typeof epcMarkChpuMainSearchComplete === 'function')
	{
		epcMarkChpuMainSearchComplete();
	}
	if(typeof epcEnsureChpuFilterVisible === 'function')
	{
		epcEnsureChpuFilterVisible();
	}
	if(typeof showPropertiesWidgets === 'function')
	{
		showPropertiesWidgets();
	}
}
function epcChpuInlineSearchSubmit(event)
{
	if(typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)
	{
		return true;
	}
	if(event && event.preventDefault)
	{
		event.preventDefault();
	}
	var form = (event && event.target && event.target.tagName === 'FORM') ? event.target : (event && event.target ? event.target.closest('form') : null);
	if(form)
	{
		var articleInput = form.querySelector('[name="article"]');
		if(articleInput && articleInput.value)
		{
			search_object.article = String(articleInput.value).replace(/[\s\-_`\/'"\\.,#]/g, '').toUpperCase();
		}
	}
	epcChpuStartFullPriceSearch(SelectedManufacturer);
	return false;
}
var epc_initial_price_bunch = <?php
if (!empty($epc_initial_price_bunch) && !empty($epc_initial_price_bunch['Products'])) {
	$epc_products_for_js = array();
	foreach ($epc_initial_price_bunch['Products'] as $epc_product) {
		$epc_products_for_js[] = $epc_product;
	}
	echo json_encode(
		array(
			'result' => (int)$epc_initial_price_bunch['result'],
			'storage_id' => (int)$epc_initial_price_bunch['storage_id'],
			'Products' => $epc_products_for_js,
		),
		JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES
	);
} else {
	echo 'null';
}
?>;
function epcBootstrapManufacturersForChpu(entries)
{
	if(!entries || !entries.length)
	{
		return;
	}
	for(var i = 0; i < entries.length; i++)
	{
		addManufacturersToList([entries[i]]);
	}
	manufacturersReview();
}

<?php
//Если это ЧПУ-второй шаг, и в опциях пользователя есть соответствующий выбранный производитель
if( isset($use_selected_manufacturer) )
{
	if( $use_selected_manufacturer )
	{
		?>
		ProductsManufacturers = <?php echo json_encode($selected_manufacturer["ProductsManufacturers"]); ?>;
		ProductsManufacturers_Shown = <?php echo json_encode($selected_manufacturer["ProductsManufacturers_Shown"]); ?>;
		ProductsManufacturers_Shown_Count = <?php echo $selected_manufacturer["ProductsManufacturers_Shown_Count"]; ?>;
		ProductsManufacturers_All_Asked = true;
		<?php
	}
}
?>

// ----------------------------------------------------------------------------------------------------------------------------------------------------
//Теперь необходимо предоставить покупателю список производителей, у которых встречается данный артикул
var manufacturersListFromPrices = false;//Флаг - Список производителей запросили из прайсов
var has_crosses_query = <?php echo (($DP_Config->ucats_crosses == 0 && $DP_Config->local_crosses == 0) || $DP_Config->list_brends_crosses == 0)?'true':'false'; ?>;//Флаг - Список производителей запросили с сервера кроссов

function getManufacturersList()
{
	var request_storages = new Array();// Список складов для запроса к скрипту ajax_asynchron.php
	
	if(ProductsManufacturers_All_Asked == false){
		
		// Если не все склады опрошены
		
		// Цикл по группам складов
		for(var g=0; g < storages_groups.length; g++)
		{
			if(storages_groups[g]['manufacturers_sent'] == 1)
			{
				continue;// Опрошенная группа - пропускаем
			}
				
			// Цикл по списку складов в группе
			for(var i=0; i < storages_groups[g]['storages'].length; i++)
			{
				//Поставщика с версией протокола 1 - пропускаем
				if(storages_groups[g]['storages'][i].protocol_version === 1)
				{
					continue;
				}
				
				// Добавляем склад в запрос
				request_storages.push(storages_groups[g]['storages'][i]);
			}
			
			// Если работаем с последней группой складов то в нее нужно добавить опрос сервера кроссов
			if(g == (storages_groups.length -1)){
				if( !has_crosses_query )
				{
					has_crosses_query = true;//Опросили.
					var server = new Object;
					server.protocol_version = 'server';
					request_storages.push(server);
				}
				
				// Флаг - все склады опрошены
				ProductsManufacturers_All_Asked = true;
			}
			
			storages_groups[g]['manufacturers_sent'] = 1;// Отмечаем что группа опрошена и выходим из цикла
			break;
		}
		
		var beforeRequestTime =  new Date().getTime();// Начальное время перед запросом

		// Если есть данные для запроса
		if(request_storages.length > 0){
			
			var request_object = new Object;
				request_object.action = 'get_manufacturers';
				request_object.article = '<?=$article;?>';
				request_object.storages = request_storages;
			
			log('<?php echo translate_str_by_id(4285); ?> '+ g +':');
			log(request_object);
			
			jQuery.ajax({
				type: "POST",
				async: true, //Запрос асинхронный
				url: "/content/shop/docpart/ajax_asynchron.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
				success: function(answer)
				{
					log('<?php echo translate_str_by_id(4286); ?> '+ g +':');
					log(answer);
					
					var afterReuestTime = new Date().getTime();
					var issue = (afterReuestTime - beforeRequestTime)/1000;
					
					log("<?php echo translate_str_by_id(4287); ?> - " + issue + "c");
					log('');
					
					if(answer.result == 1)//Запрос выполнен успешно
					{
						if(answer.data.length > 0){
							for(var i = 0; i < answer.data.length; i++){
								if(answer.data[i] != null){
									addManufacturersToList(answer.data[i].ProductsManufacturers);//Добавляем результат в общий объект
								}
							}
						}
						manufacturersReview();//Переотображаем таблицу производителей
					}
					
					getManufacturersList();//Делаем следующий запрос
					return;
				},
				error: function (e, ajaxOptions, thrownError){
					log('<?php echo translate_str_by_id(4286); ?> '+ g +':');
					log('<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError);
					log('');
					log('');
					
					getManufacturersList();//Делаем следующий запрос
					return;
				}
			});
		}else{
			// В группе для опроса были только склады с 1 типом интерфейса поэтому объект пуст и нужно перейти к опросу следующей группы
			getManufacturersList();//Делаем следующий запрос
			return;
		}
		
	}else{
		
		// Все склады опрошены
		
		
		
		// brend **********************************************************
		<?php
		// Если передан бренд то выбираем его автоматически
		if( ! empty($_GET["brend"]) ){
			
			$brend = urldecode($_GET["brend"]);
			$brend = str_replace('"',"'",$brend);
			$brend = trim($brend);
			$brend = mb_strtoupper($brend, "UTF-8");
			
			echo 'var synonym_brend = "";';// Переменная в которой будет наименование бренда как оно должно отображаться на сайте из таблицы синонимов
			
			// Находим если есть правильное наименование бренда из таблицы синонимов по переданному в запросе наименованию
			$synonym_query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = (SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1);');
			$synonym_query->execute( array( html_entity_decode($brend) ) );
			$synonym_record = $synonym_query->fetch();
			if( $synonym_record != false )
			{
				$synonym_record["name"] = str_replace('"',"'",$synonym_record["name"]);
				echo 'synonym_brend = "'. strtoupper($synonym_record["name"]) .'";';
			}
			
		?>
		var brend =  $('<textarea />').html('<?=$brend;?>').text();
		var ManufacturerSelected_tmp = null;
		for(var i=0; i < ProductsManufacturers.length; i++)
		{
			if(ProductsManufacturers[i].manufacturer_show == brend || ProductsManufacturers[i].manufacturer_show == synonym_brend){
				ManufacturerSelected_tmp = ProductsManufacturers[i].manufacturer_show;
				break;
			}else if(ProductsManufacturers[i].manufacturer == brend){
				ManufacturerSelected_tmp = ProductsManufacturers[i].manufacturer_show;
				break;
			}
		}
		if(ManufacturerSelected_tmp != null){
			onManufacturerSelected(ManufacturerSelected_tmp);
		}else{
			document.getElementById("processing_indicator").innerHTML = "<br/><p style='background: #f9f9f9; border: 1px solid #ddd; border-radius: 5px; padding: 5px; color: #000;'><i class='fa fa-info-circle'></i> <?php echo translate_str_by_id(4288); ?></p><br/>";
			
			if(ProductsManufacturers.length == 0)
			{
				onManufacturerSelected(null);//Указываем его в качестве выбранного
			}else{
				manufacturersReview();
				document.getElementById('table-manufacturers').style.display = 'block';
			}
		}
		
		return;
		<?php
		}
		?>
		//*****************************************************************
		
		
		
		epcBrandPickerWarehouseDone = true;
		epcTryFinalizeBrandPicker();
	}
}

function epcFinishManufacturersAsyncRequest(request_storages, beforeRequestTimeall)
{
	if (request_storages > 0) {
		return;
	}
	epcClearManufacturersPollTimeout();
	ProductsManufacturers_All_Asked = true;
	var afterReuestTimeall = new Date().getTime();
	var issue = (afterReuestTimeall - beforeRequestTimeall)/1000;
	log("<?php echo translate_str_by_id(5617); ?> - " + issue + "sec");
	getManufacturersListAsync();
}

var epcManufacturersPollTimeoutId = null;
function epcClearManufacturersPollTimeout()
{
	if(epcManufacturersPollTimeoutId)
	{
		clearTimeout(epcManufacturersPollTimeoutId);
		epcManufacturersPollTimeoutId = null;
	}
}
function epcStartManufacturersPollTimeout()
{
	if(epcManufacturersPollTimeoutId)
	{
		return;
	}
	epcManufacturersPollTimeoutId = setTimeout(function(){
		epcManufacturersPollTimeoutId = null;
		if(ProductsManufacturers_All_Asked)
		{
			return;
		}
		ProductsManufacturers_All_Asked = true;
		if(typeof epcApplyInitialPriceBunch === 'function' && epcApplyInitialPriceBunch())
		{
			if(typeof epc_brand_picker_mode !== 'undefined' && epc_brand_picker_mode)
			{
				epcBrandPickerWarehouseDone = true;
				epcTryFinalizeBrandPicker();
			}
			return;
		}
		var processingTimeout = document.getElementById('processing_indicator');
		if(processingTimeout)
		{
			processingTimeout.innerHTML = '';
		}
		epcBrandPickerWarehouseDone = true;
		if(typeof epcTryFinalizeBrandPicker === 'function')
		{
			epcTryFinalizeBrandPicker();
		}
	}, 3000);
}

function getManufacturersListAsync()
{
	if(typeof epc_brand_picker_stock_preview !== 'undefined' && epc_brand_picker_stock_preview)
	{
		ProductsManufacturers_All_Asked = true;
		epcBrandPickerWarehouseDone = true;
		epcTryFinalizeBrandPicker();
		return;
	}
	if(ProductsManufacturers_All_Asked == false){
		epcStartManufacturersPollTimeout();
		var beforeRequestTimeall =  new Date().getTime();// Начальное время перед запросом
		var request_storages = postdata_storages.length;
		var manufacturers_total = postdata_storages.length;
		
		// Цикл по всем складам
		for(var g=0; g < postdata_storages.length; g++)
		{

			var beforeRequestTime =  new Date().getTime();// Начальное время перед запросом
			fetch(postdata_storages[g]['url'], {
			method: 'POST',
			headers: {
				'Content-Type': 'application/x-www-form-urlencoded;charset=utf-8;'
			},
			body: postdata_storages[g]['query']
			})
			.then((response) => {
				
				return response.json();
			})
			.then((data) => {
				request_storages--;
				document.getElementById("processing_indicator").innerHTML = "<p><?php echo translate_str_by_id(5613); ?> "+(manufacturers_total-request_storages)+" / "+manufacturers_total+"</p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>";
				var products_manufacturers = (data && data.ProductsManufacturers) ? data.ProductsManufacturers : [];
				if (products_manufacturers.length > 0)
				{ 
					log('<?php echo translate_str_by_id(5614); ?> '+ data.storage +':');
					log(data);
					
					var afterReuestTime = new Date().getTime();
					var issue = (afterReuestTime - beforeRequestTime)/1000;
					
					log("<?php echo translate_str_by_id(4287); ?> - " + issue + "c");
					log('');
					
					if(data.status == true || data.result == 1 || products_manufacturers.length > 0)//Запрос выполнен успешно
					{
						if(products_manufacturers.length > 0){
							addManufacturersToList(products_manufacturers);//Добавляем результат в общий объект
						}
						if(typeof epc_brand_picker_mode === 'undefined' || !epc_brand_picker_mode)
						{
						manufacturersReview();//Переотображаем таблицу производителей
					}
				}
				}
				else if (products_manufacturers.length == 0){
					log("<?php echo translate_str_by_id(5615); ?> "+ (data && data.storage ? data.storage : '') +':');
					log(data);
				}
				else {
					log("<?php echo translate_str_by_id(5616); ?> "+ (data && data.storage ? data.storage : '') +':');
					log(data);
				}

				epcFinishManufacturersAsyncRequest(request_storages, beforeRequestTimeall);
				return;
			})
			.catch((e) => {
				request_storages--;
				console.log('<?php echo translate_str_by_id(2095); ?>: ' + e.message);
				console.log(e.response);
				epcFinishManufacturersAsyncRequest(request_storages, beforeRequestTimeall);
				return;
			});
		}
	}else{
		// Все склады опрошены
		
		// brend **********************************************************
		<?php
		// Если передан бренд то выбираем его автоматически
		if (!empty($_GET["brend"])) {
			
			$brend = urldecode($_GET["brend"]);
			$brend = str_replace('"', "'", $brend);
			$brend = trim($brend);
			$brend = mb_strtoupper($brend, "UTF-8");

			echo 'var synonym_brend = "";'; // Переменная в которой будет наименование бренда как оно должно отображаться на сайте из таблицы синонимов
		
			// Находим если есть правильное наименование бренда из таблицы синонимов по переданному в запросе наименованию
			$synonym_query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = (SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1);');
			$synonym_query->execute(array(html_entity_decode($brend)));
			$synonym_record = $synonym_query->fetch();
			if ($synonym_record != false) {
				$synonym_record["name"] = str_replace('"', "'", $synonym_record["name"]);
				echo 'synonym_brend = "' . strtoupper($synonym_record["name"]) . '";';
			}

			?>
			var brend =  $('<textarea />').html('<?= $brend; ?>').text();
			var ManufacturerSelected_tmp = null;
			for(var i=0; i < ProductsManufacturers.length; i++)
			{
				if(ProductsManufacturers[i].manufacturer_show == brend || ProductsManufacturers[i].manufacturer_show == synonym_brend){
					ManufacturerSelected_tmp = ProductsManufacturers[i].manufacturer_show;
					break;
				}else if(ProductsManufacturers[i].manufacturer == brend){
					ManufacturerSelected_tmp = ProductsManufacturers[i].manufacturer_show;
					break;
				}
			}
			if(ManufacturerSelected_tmp != null){
				onManufacturerSelected(ManufacturerSelected_tmp);
			}else{
				document.getElementById("processing_indicator").innerHTML = "<br/><p style='background: #f9f9f9; border: 1px solid #ddd; border-radius: 5px; padding: 5px; color: #000;'><i class='fa fa-info-circle'></i> <?php echo translate_str_by_id(4288); ?></p><br/>";
			
				if(ProductsManufacturers.length == 0)
				{
					onManufacturerSelected(null);//Указываем его в качестве выбранного
				}else{
					manufacturersReview();
					document.getElementById('table-manufacturers').style.display = 'block';
				}
			}
		
			return;
			<?php
		}
		?>
		//*****************************************************************
		
		epcBrandPickerWarehouseDone = true;
		epcTryFinalizeBrandPicker();
	}
}
// ----------------------------------------------------------------------------------------------------------------------------------------------------
//Добавления списка производителей после опроса каждого поставщика
/*
function addManufacturersToList(products_manufacturers)
{
	for(var i=0; i < products_manufacturers.length; i++)
	{
		if(products_manufacturers[i]['manufacturer'] != null){
			ProductsManufacturers.push(products_manufacturers[i]);
		}
	}
}
*/
function addManufacturersToList(products_manufacturers)
{
	for(var i=0; i < products_manufacturers.length; i++)
	{
		if(products_manufacturers[i]['manufacturer'] != null){
			
			log(products_manufacturers[i]['manufacturer_show']);
			log('brends_filtr:');
			log(brends_filtr);
			
			if(brends_filtr[products_manufacturers[i]['manufacturer_show']] !== undefined){
				var brends_filtr_this = brends_filtr[products_manufacturers[i]['manufacturer_show']];
				
				log('brends_filtr_this:');
				log(brends_filtr_this);
				
				if(brends_filtr_this['-'] !== undefined){
					// Значит стоит ограничение на бренд без учета артикула
					var this_list_storages = brends_filtr_this['-']['list_storages'];
					// Проверяем бренд с учетом артикула
					if(this_list_storages === null || this_list_storages.indexOf(products_manufacturers[i]['storage_id']*1) !== -1 || products_manufacturers[i]['storage_id'] == 0){
						continue;
					}
				}
				
				if(brends_filtr_this['<?=$article;?>'] !== undefined){
					// Значит стоит ограничение на бренд и артикул
					var this_list_storages = brends_filtr_this['<?=$article;?>']['list_storages'];
					// Проверяем бренд с учетом артикула
					if(this_list_storages === null || this_list_storages.indexOf(products_manufacturers[i]['storage_id']*1) !== -1 || products_manufacturers[i]['storage_id'] == 0){
						continue;
					}
				}
			}
			
			if(brends_filtr['<?=$article;?>'] !== undefined){
				continue;
			}
			
			ProductsManufacturers.push(products_manufacturers[i]);
		}
	}
}
// ----------------------------------------------------------------------------------------------------------------------------------------------------
//Переотображение таблицы производителей после опроса каждого поставщика
function manufacturersReview()
{
	if(ProductsManufacturers.length == 0)
	{
		return;
	}
	
	ProductsManufacturers.sort(sortFunctionProductsManufacturers);// Сортировка
	
	ProductsManufacturers_Shown = new Object();//Сбрасываем массив отображенных
	ProductsManufacturers_Shown_Count = 0;//Количество показанных производителей
	var not_name_text = '<?php echo translate_str_by_id(4289); ?>';
	
	
	
	// brend **********************************************************
	<?php
	// Если передан бренд то скрываем таблицу брендов от пользователя
	if( ! empty($_GET["brend"]) ){
	?>
	var html = "<div id=\"table-manufacturers\" style=\"display:none;\" class=\"table-responsive\"><table cellpadding=\"1\" cellspacing=\"1\" class=\"table table-condensed table-striped\">";
	<?php
	}else{
	?>
	var html = "<div id=\"table-manufacturers\" class=\"table-responsive\"><table cellpadding=\"1\" cellspacing=\"1\" class=\"table table-condensed table-striped epc-brand-picker-table\">";
	<?php
	}
	?>
	//*****************************************************************
	
	
	
	html += "<thead><tr> <th><?php echo translate_str_by_id(4276); ?></th> <th><?php echo translate_str_by_id(2071); ?></th> <th><?php echo translate_str_by_id(2102); ?></th> <th></th> </tr></thead><tbody>";
	
	for(var i=0; i < ProductsManufacturers.length; i++)
	{
		//Если это первый такой производитель - создаем для него массив всех объектов
		if( ProductsManufacturers_Shown[ProductsManufacturers[i].manufacturer_show] == undefined )
		{
			ProductsManufacturers_Shown[ProductsManufacturers[i].manufacturer_show] = new Array();
			
			// Если у первого элемента не было указано наименование, возьмем его из последующих
			if(ProductsManufacturers[i].name == null || ProductsManufacturers[i].name == '' || ProductsManufacturers[i].name == false || ProductsManufacturers[i].name == not_name_text || ProductsManufacturers[i].name == '<?php echo translate_str_by_id(4290); ?>')
			{
				for(var j=0; j < ProductsManufacturers.length; j++)
				{
					if(ProductsManufacturers[i].manufacturer_show === ProductsManufacturers[j].manufacturer_show){
						if(ProductsManufacturers[j].name !== null && ProductsManufacturers[j].name !== '' && ProductsManufacturers[j].name !== false && ProductsManufacturers[j].name !== not_name_text && ProductsManufacturers[j].name !== '<?php echo translate_str_by_id(4290); ?>')
						{
							ProductsManufacturers[i].name = ProductsManufacturers[j].name;
							break;
						}
					}
				}
			}
		}
		
		//Добавляем объект (будет потом использоваться при запросах поставщикам)
		//ProductsManufacturers_Shown[ProductsManufacturers[i].manufacturer_show][ProductsManufacturers[i].storage_id] = ProductsManufacturers[i];
		ProductsManufacturers_Shown[ProductsManufacturers[i].manufacturer_show].push(ProductsManufacturers[i]);
		//Если такой уже один отобразили - пропускаем
		if( ProductsManufacturers_Shown[ProductsManufacturers[i].manufacturer_show].length > 1 )
		{
			continue;
		}
		
		var a_tag = "<a href=\"javascript:void(0);\" onclick=\"onManufacturerSelected('"+ProductsManufacturers[i].manufacturer_show.replace(/'/g,"\\'")+"'); \" >";
		
		var button_tag = "<button onclick=\"onManufacturerSelected('"+ProductsManufacturers[i].manufacturer_show.replace(/'/g,"\\'")+"'); \" type=\"button\" class=\"btn btn-ar btn-primary\">";

		if(ProductsManufacturers[i].name == null || ProductsManufacturers[i].name == '' || ProductsManufacturers[i].name == false)
		{
			ProductsManufacturers[i].name = not_name_text;
		}
		

		html += "<tr> <td>" + a_tag + ProductsManufacturers[i].manufacturer_show+"</a></td> <td>" + a_tag + "<?php echo $article; ?></a></td> <td>" + a_tag + ProductsManufacturers[i].name+"</a></td> <td class='text-right'>"+button_tag+"<?php echo translate_str_by_id(4291); ?></button></td> </tr>";

		ProductsManufacturers_Shown_Count ++;//Количество показанных производителей
	}

	
	html += "</tbody></table></div>";
	
	
	// brend **********************************************************
	<?php
	if( ! empty($_GET["brend"]) ){
	?>
	//Таблицу производителей показываем если в ней есть бренды
	if( ProductsManufacturers_Shown_Count > 0 )
	{
		document.getElementById("products_area").innerHTML = html;
	}
	<?php
	}else{
	?>
	//Таблицу производителей показываем только если в ней больше 1 производителя
	if( ProductsManufacturers_Shown_Count > 1 )
	{
		document.getElementById("products_area").innerHTML = html;
	}
	<?php
	}
	?>
	//*****************************************************************
}
// ----------------------------------------------------------------------------------------------------------------------------------------------------
//Обработка выбора производителя
var SelectedManufacturer = null;//Выбранный производитель
function onManufacturerSelected(manufacturer_show)
{	
	<?php
	/*
	Здесь в зависимости от $search_type
	*/
	//Старый вариант. И, такой же точно - ЧПУ-второй шаг. Со старым вариантом - понятно. С ЧПУ-второй шаг: если производитель записан в опции пользователя, то все JavaScript-переменные будут взяты из опций пользователя и при загрузке страницы - сразу пойдет вызов данной функции (onManufacturerSelected(manufacturer_show)). Если опций пользователя нет, то при ЧПУ-втором шаге будет сначала опрос поставщиков для получения списка производителей и затем автоматический выбор одного из них, т.е. также - вызов этой функции.
	if( $search_type == "no_chpu" || ($search_type == "prices_by_article_and_manufacturer" && $use_selected_manufacturer && empty($epc_chpu_direct_pricing)) )
	{
		?>
		if( ! ProductsManufacturers_All_Asked )
		{
			alert("<?php echo translate_str_by_id(4292); ?>");
			return;
		}
		
		
		SelectedManufacturer = manufacturer_show;
		
		var productsArea = document.getElementById("products_area");
		if(productsArea)
		{
			var crossBlock = productsArea.querySelector('.epc-cross-stock-results');
			if(crossBlock && crossBlock.parentNode)
			{
				crossBlock.parentNode.removeChild(crossBlock);
			}
			productsArea.innerHTML = "";
		}
		getAnalogsList();//После выбора производителя - Делаем запрос аналогов от сервера кроссов
		<?php
	}
	else if( $search_type == "prices_by_article_and_manufacturer" && !empty($epc_chpu_direct_pricing) )
	{
		?>
		ProductsManufacturers_All_Asked = true;
		SelectedManufacturer = manufacturer_show;
		if(typeof epcChpuStartFullPriceSearch === 'function')
		{
			epcChpuStartFullPriceSearch(manufacturer_show);
		}
		<?php
	}
	//ЧПУ - первый шаг. Записываем выбранного производителя в опции пользователя - По прямому выбору пользователя, либо автоматически, если производитель всего один. После того, как производитель записан в опции - следует переход на ЧПУ-второй шаг.
	else if($search_type == "all_brands_by_article" || ($search_type == "prices_by_article_and_manufacturer" && !$use_selected_manufacturer && empty($epc_chpu_direct_pricing)) )
	{
		?>
		if( ! ProductsManufacturers_All_Asked )
		{
			alert("<?php echo translate_str_by_id(4292); ?>");
			return;
		}
		
		SelectedManufacturer = manufacturer_show;
		epcChpuNavigateToBrandArticle(manufacturer_show);
		
		<?php
	}
	?>
}
// ----------------------------------------------------------------------------------------------------------------------------------------------------
//Шаг - подбор аналогов по артикулу или по артикулу-производителю
function getAnalogsList()
{
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		if(typeof epcChpuStartFullPriceSearch === 'function')
		{
			epcChpuStartFullPriceSearch(SelectedManufacturer);
		}
		return;
	}
	// Добавляем в запрос все синонимы выбранного производителя от каждого склада
	search_object.manufacturers = new Array();
	
	if(SelectedManufacturer != null){
		var selected_manufacturer_rows = ProductsManufacturers_Shown[SelectedManufacturer];
		if(selected_manufacturer_rows && selected_manufacturer_rows.length){
			for(var m = 0; m < selected_manufacturer_rows.length; m++)
		{
			// Отфильтровываем строки из прайс листов, так как запрос аналогов нужно делать по производителю который был передан складом
				if(selected_manufacturer_rows[m].params !== null){
					if(selected_manufacturer_rows[m].params.type === 'prices'){
					continue;
				}
			}
				search_object.manufacturers.push(selected_manufacturer_rows[m]);//Добавляем сюда строку именно выбранную
			}
		}
	}
	
	document.getElementById("processing_indicator").innerHTML = "<p><?php echo translate_str_by_id(4293); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>";
	
    jQuery.ajax({
        type: "POST",
        async: true, //Запрос асинхронный
        url: "/content/shop/docpart/ajax_getAnalogsList.php",
        dataType: "json",//Тип возвращаемого значения
        data: "search_object="+encodeURIComponent(JSON.stringify(search_object)),
        success: function(answer)
		{
            log('<?php echo translate_str_by_id(4295); ?>:');
            log(answer);
            log('');
			if(answer.result == 1)//Запрос выполнен успешно
            {
                search_object.analogs = answer.analogs;
            }
            
			document.getElementById("processing_indicator").innerHTML = "<p><?php echo translate_str_by_id(4294); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>";
			
			log('-----------------------------------------------------');
			log('');
			
			startBeforeRequestTime = new Date().getTime();// Время начало опроса складов
			
			<?php 
			if((int)$DP_Config->is_async_search == 1)
			{
			?>
			getStoragesDataAsync();//Запрос данных о наличии товаров на складах
			<?php
			}
			else
			{
			?>
			getStoragesData();//Запрос данных о наличии товаров на складах
			<?php
			}
			?>
        },
		error: function (e, ajaxOptions, thrownError){
			log('<?php echo translate_str_by_id(4295); ?>:');
			log('<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError);
			log('');
			
			document.getElementById("processing_indicator").innerHTML = "<p><?php echo translate_str_by_id(4294); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>";
			
			log('-----------------------------------------------------');
			log('');
			
			startBeforeRequestTime = new Date().getTime();// Время начало опроса складов
			
			<?php 
			if((int)$DP_Config->is_async_search == 1)
			{
			?>
			getStoragesDataAsync();//Запрос данных о наличии товаров на складах
			<?php
			}
			else
			{
			?>
			getStoragesData();//Запрос данных о наличии товаров на складах
			<?php
			}
			?>
		}
    });
}
// ----------------------------------------------------------------------------------------------------------------------------------------------------
var startBeforeRequestTime = 0;// Время начало опроса складов
//Запрос данных о наличии товаров на складах
function getStoragesData()
{
	var request_storages = new Array();// Список складов для запроса к скрипту ajax_asynchron.php
	
	for(var g=0; g < storages_groups.length; g++)
	{
		if(storages_groups[g]['sent'] == 1){
			continue;// Пропускаем опрошенную группу
		}
		
		// Делаем группу опрошенной
		storages_groups[g]['sent'] = 1;
		
		// Клонируем объект search_object потому что он будет у каждого склада свой
		var search_object_clone = new Object;
		for (var key in search_object) {
		  search_object_clone[key] = search_object[key];
		}
		
		for(var i=0; i < storages_groups[g]['storages'].length; i++)
		{
			search_object_clone.office_storage_bunches = new Array();
			search_object_clone.manufacturers = new Array();
			
			//Если это поставщик с версией протокола 2 - создаем для него список нужных производителей
			if( storages_groups[g]['storages'][i].protocol_version == 2 )
			{
				if( ProductsManufacturers.length == 0 )
				{
					// Если запрос производителя не дал результата то пропускаем склад если он api поставщик
					// В каталоге нужно сделать поиск так как запрос может быть по наименованию и могут найтись аналоги
					if(storages_groups[g]['storages'][i].treelax_catalogue === false){
						continue;
					}
				}
				
				if(ProductsManufacturers.length > 0){
					if(SelectedManufacturer != null){
						var epc_selected_manufacturer_rows = epcSelectedManufacturerRows();
						for(var m = 0; m < epc_selected_manufacturer_rows.length; m++)
						{
							if( parseInt(epc_selected_manufacturer_rows[m].storage_id) == parseInt(storages_groups[g]['storages'][i].storage_id) &&
							parseInt(epc_selected_manufacturer_rows[m].office_id) == parseInt(storages_groups[g]['storages'][i].office_id))
							{
								search_object_clone.manufacturers.push(epc_selected_manufacturer_rows[m]);//Добавляем сюда строку именно из API поставщика
							}
						}
					}
				}
				
				//Если список нужных производителей пуст - значит у данного поставщика нет таких производителей - пропускаем, чтобы не тратить время
				if(search_object_clone.manufacturers.length == 0)
				{
					if(storages_groups[g]['storages'][i].treelax_catalogue === false){
						continue;
					}else{
						var epc_selected_manufacturer_rows = epcSelectedManufacturerRows();
						if(SelectedManufacturer != null && epc_selected_manufacturer_rows.length > 0){
							search_object_clone.manufacturers.push(epc_selected_manufacturer_rows[0]);
						}
					}
				}
			}
			else if(storages_groups[g]['storages'][i].protocol_version == 3)//Для прайс-листов
			{
				if(SelectedManufacturer != null){
					epcPushPriceManufacturersForBunch(search_object_clone, storages_groups[g]['storages'][i].office_storage_bunches);
				}
				
				//В объект запроса добавляем связки складов и магазинов для прайс-листов
				search_object_clone.office_storage_bunches = storages_groups[g]['storages'][i].office_storage_bunches;
			}
			
			// Убираем из запроса аналоги для складов которым они не нужных
			if(storages_groups[g]['storages'][i].protocol_version != 3 && storages_groups[g]['storages'][i].treelax_catalogue === false){
				search_object_clone.analogs = new Array();
			}
			
			// Добавляем к складу список его производителей
			storages_groups[g]['storages'][i]['search_object'] = new Object;
			for (var key in search_object_clone) {
			  storages_groups[g]['storages'][i]['search_object'][key] = search_object_clone[key];
			}
			
			// Добавляем склад в запрос
			request_storages.push(storages_groups[g]['storages'][i]);
		}
		
		// Делаем запрос
		
		// Если есть данные для запроса
		if(request_storages.length > 0){
			
			var request_object = new Object;
				request_object.action = 'get_articles';
				request_object.article = '<?=$article;?>';
				request_object.storages = request_storages;
			
			
			log('Запрос артикула - группа '+ g +':');
			log(request_object);
			
			var beforeRequestTime =  new Date().getTime();
			
			jQuery.ajax({
				type: "POST",
				async: true, //Запрос асинхронный
				url: "/content/shop/docpart/ajax_asynchron.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
				success: function(answer)
				{
					log('<?php echo translate_str_by_id(4286); ?> '+ g +':');
					log(answer);

					var afterReuestTime = new Date().getTime();
					var issue = (afterReuestTime - beforeRequestTime)/1000;

					log("<?php echo translate_str_by_id(4287); ?> - " + issue + "<?php echo translate_str_by_id(4296); ?>");
					log('');
					
					if(answer.result == 1)//Запрос выполнен успешно
					{
						if(answer.data){
							if(answer.data.length > 0){
								var fflag = false;
								for(var i = 0; i < answer.data.length; i++){
									if(answer.data[i] != null){
										if(answer.data[i].Products.length > 0){
											fflag = true;
											bindBunchResult(answer.data[i]);//Добавляем полученный ответ к общему объекту описания
										}
									}
								}
								if(fflag == true){
									// Переотображаем проценку
									onGetStoragesData();
								}
							}
						}
					}
					
					getStoragesData();//Делаем следующий запрос
					return;
				},
				error: function (e, ajaxOptions, thrownError){
					log('<?php echo translate_str_by_id(4286); ?> '+ g +':');
					log('<?php echo translate_str_by_id(2122); ?>: '+ e.status +' - '+ thrownError);
					log('');
					log('');
					
					getStoragesData();//Делаем следующий запрос
					return;
				}
			});
		}else{
			// В группе для опроса не было складов - все склады были типа 2 и для них не было производителей, поэтому их все пропустили
			log('<?php echo translate_str_by_id(4297); ?> '+ g +':');
			log(request_storages);
			log('<?php echo translate_str_by_id(4286); ?> '+ g +':');
			log('<?php echo translate_str_by_id(4298); ?>');
			log('');
			log('');
			getStoragesData();//Делаем следующий запрос
			return;
		}
		
		// Выходим из функции
		return;
	}

	// Цикл полностью выполнился - значит неопрошенных связок не осталось - обозначаем, что все данные загружены
	if(Products.All.length == 0)
	{
		document.getElementById("processing_indicator").innerHTML = "<p><?php echo translate_str_by_id(4078); ?></p>";
	}
	else
	{
		// Все данные загружены - Просто убираем индикатор загрузки
		document.getElementById("processing_indicator").innerHTML = "";
	}
	
	var afterReuestTime = new Date().getTime();
	var issue = (afterReuestTime - startBeforeRequestTime)/1000;
	log('-----------------------------------------------------');
	log('');
	log("<?php echo translate_str_by_id(4299); ?> - " + issue + "<?php echo translate_str_by_id(4296); ?>");
	log('');
}

var Products_All_Asked = false; // Флаг окончания опроса всех поставщиков
//Запрос данных о наличии товаров на складах
function getStoragesDataAsync()
{
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		if(typeof epcRunChpuPriceSearch === 'function' && !epcChpuMainSearchPending)
		{
			epcRunChpuPriceSearch();
		}
		return;
	}
	var request_storages = new Array();// Список складов для запроса

	if (Products_All_Asked == false )
	{
		for(var i=0; i < office_storage_bunches.length; i++)
		{	
			
			// Клонируем объект search_object потому что он будет у каждого склада свой
			var search_object_clone = new Object;
			for (var key in search_object) {
				search_object_clone[key] = search_object[key];
			}
			search_object_clone.office_storage_bunches = new Array();
			search_object_clone.manufacturers = new Array();
			
			//Если это поставщик с версией протокола 2 - создаем для него список нужных производителей
			if( office_storage_bunches[i].protocol_version == 2 )
			{
				if( ProductsManufacturers.length == 0 )
				{
					// Если запрос производителя не дал результата то пропускаем склад если он api поставщик
					// В каталоге нужно сделать поиск так как запрос может быть по наименованию и могут найтись аналоги
					if(office_storage_bunches[i].treelax_catalogue === false){
						continue;
					}
				}
				if(ProductsManufacturers.length > 0){
					if(SelectedManufacturer != null){
						var epc_selected_manufacturer_rows = epcSelectedManufacturerRows();
						for(var m = 0; m < epc_selected_manufacturer_rows.length; m++)
						{
							if( parseInt(epc_selected_manufacturer_rows[m].storage_id) == parseInt(office_storage_bunches[i].storage_id) &&
							parseInt(epc_selected_manufacturer_rows[m].office_id) == parseInt(office_storage_bunches[i].office_id))
							{
								search_object_clone.manufacturers.push(epc_selected_manufacturer_rows[m]);//Добавляем сюда строку именно из API поставщика
							}
						}
					}
				}
				
				//Если список нужных производителей пуст - значит у данного поставщика нет таких производителей - пропускаем, чтобы не тратить время
				if(search_object_clone.manufacturers.length == 0)
				{
					if(office_storage_bunches[i].treelax_catalogue === false){
						continue;
					}else{
						var epc_selected_manufacturer_rows = epcSelectedManufacturerRows();
						if(SelectedManufacturer != null && epc_selected_manufacturer_rows.length > 0){
							search_object_clone.manufacturers.push(epc_selected_manufacturer_rows[0]);
						}
					}
				}
			}
			else if(office_storage_bunches[i].protocol_version == 3)//Для прайс-листов
			{
				if(SelectedManufacturer != null){
					epcPushPriceManufacturersForBunch(search_object_clone, office_storage_bunches[i].office_storage_bunches);
				}
				
				//В объект запроса добавляем связки складов и магазинов для прайс-листов
				search_object_clone.office_storage_bunches = office_storage_bunches[i].office_storage_bunches;
			}
			else if(office_storage_bunches[i].protocol_version == 'server') continue;
			
			
			// Убираем из запроса аналоги для складов которым они не нужных
			if(office_storage_bunches[i].protocol_version != 3 && office_storage_bunches[i].treelax_catalogue === false){
				search_object_clone.analogs = new Array();
			}
			
			// Добавляем к складу список его производителей
			office_storage_bunches[i]['search_object'] = new Object;
			for (var key in search_object_clone) {
				office_storage_bunches[i]['search_object'][key] = search_object_clone[key];
			}

			// Формируем параметры запроса склада
			office_storage_bunches[i]['query'] = 'geo_id=<?= $geo_id ?>&async=1&tech_key=<?= urlencode($DP_Config->tech_key) ?>&user_id=<?= $user_id ?>&group_id=<?= $group_id ?>&office_id=' + office_storage_bunches[i]['office_id'] +'&storage_id=' + office_storage_bunches[i]['storage_id'] + '&query=' + encodeURIComponent(JSON.stringify(office_storage_bunches[i]['search_object']));
			
			// Добавляем склад в запрос
			request_storages.push(office_storage_bunches[i]);

			// Количество складов для опроса
			request_storages_count = request_storages.length;

			if (request_storages.length > 0){

				var beforeRequestTime =  new Date().getTime();// Начальное время перед запросом

				fetch('<?= $DP_Config->domain_path . 'content/shop/docpart/ajax_getProductsOfBunch.php' ?>', {
				method: 'POST',
				headers: {
					'Content-Type': 'application/x-www-form-urlencoded;charset=utf-8;'
				},
				body: office_storage_bunches[i]['query']
				})
				.then((response) => {
					return response.json();
				})
				.then((data) => {
					document.getElementById("back_to_brands_box").style.display = 'block';
					request_storages_count--;
					document.getElementById("processing_indicator").innerHTML = "<p><?php echo translate_str_by_id(5613); ?> "+(request_storages.length-request_storages_count)+" из "+request_storages.length+"</p><img src=\"/content/files/images/ajax-loader-transparent.gif\" /><br><br>";
					log('Результат cклада '+ data.storage_id+':');
					log(data);

					var afterReuestTime = new Date().getTime();
					var issue = (afterReuestTime - beforeRequestTime)/1000;

					log("<?php echo translate_str_by_id(4287); ?> - " + issue + "sec");
					log('');
					
					if(data.result == 1 && data.Products && data.Products.length > 0)//Запрос выполнен успешно
					{
							bindBunchResult(data);//Добавляем полученный ответ к общему объекту описания
							onGetStoragesData();
					}
						
					if (request_storages_count == 0){
						document.getElementById("back_to_brands_box_icon").style.color = '<?=$DP_Template->data_value->main_color?>';
						Products_All_Asked = true;
						epcFinishStoragesSearchUi();
					}

					return;
				})
				.catch((e) => {
					request_storages_count--;
					console.log('<?php echo translate_str_by_id(2122); ?>: ' + e.message);
					console.log(e.response);
					if (request_storages_count == 0){
						document.getElementById("back_to_brands_box_icon").style.color = '<?=$DP_Template->data_value->main_color?>';
						Products_All_Asked = true;
						epcFinishStoragesSearchUi();
					}
					return;
				});
			}
		}
		if (request_storages.length == 0)
		{
			Products_All_Asked = true;
			getStoragesDataAsync();
		}
	}
	else{
		epcFinishStoragesSearchUi();
		var afterReuestTime = new Date().getTime();
		var issue = (afterReuestTime - startBeforeRequestTime)/1000;
		log('-----------------------------------------------------');
		log('');
		log("<?php echo translate_str_by_id(4299); ?> - " + issue + "sec");
		log('');
	}
}
// ---------------------------------------------------------------------------------------------------------------------
</script>


















<?php
$epc_result_brand = '';
if($search_type == 'prices_by_article_and_manufacturer' && !empty($manufacturer)){
	$epc_result_brand = html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
}else if(!empty($_GET["brend"])){
	$epc_result_brand = html_entity_decode($_GET["brend"], ENT_QUOTES | ENT_XML1, 'UTF-8');
}
$epc_result_article = !empty($article_input) ? $article_input : $article;
$epc_result_brand_display = $epc_result_brand !== '' ? $epc_result_brand : translate_str_by_id(2070);
$epc_universal_mode = isset($_GET['universal']) && (string)$_GET['universal'] === '1';
?>
<?php
/* Search-result hero ("Searching for") + related-parts strip removed from UI —
   header search + results table are enough. Fitment/cross helpers stay available
   via compact toolbar controls below when needed. */
?>
<?php if (empty($epc_brand_picker_mode)) { ?>
<div class="epc-parts-result-tools" style="margin:8px 0 14px;display:flex;flex-wrap:wrap;gap:8px;align-items:center;">
	<button type="button" class="btn btn-default btn-sm epc-fitment-check-btn" id="epc-fitment-check-btn" data-article="<?php echo htmlspecialchars($epc_result_article, ENT_QUOTES, 'UTF-8'); ?>"<?php if ($epc_result_brand !== '') { ?> data-brand="<?php echo htmlspecialchars($epc_result_brand, ENT_QUOTES, 'UTF-8'); ?>"<?php } ?>>
		<i class="fa fa-car" aria-hidden="true"></i> Fitment check
	</button>
	<button type="button" class="btn btn-default btn-sm epc-cross-search-btn" id="epc-cross-search-btn" data-article="<?php echo htmlspecialchars($epc_result_article, ENT_QUOTES, 'UTF-8'); ?>" title="Open all cross references">
		<i class="fa fa-random" aria-hidden="true"></i>
		<span id="epc-cross-search-count"><?php echo count($epc_cross_fallback_rows); ?> references</span>
	</button>
</div>
<div class="epc-fitment-panel" id="epc-fitment-panel" aria-live="polite">
	<div class="epc-fitment-panel__head">
		<div>
			<div class="epc-fitment-panel__title">Part Fitment</div>
			<div class="epc-fitment-panel__hint">Choose the matching brand/number — photo, specifications and vehicle fitment load automatically.</div>
		</div>
		<button type="button" class="epc-fitment-panel__close" id="epc-fitment-close" aria-label="Close fitment check">&times;</button>
	</div>
	<div class="epc-fitment-panel__body">
		<div id="epc-fitment-brands" class="epc-fitment-message">Click Fitment check to load matching brands from eparts catalog.</div>
		<div class="epc-fitment-type-tabs" id="epc-fitment-types" style="display:none;">
			<button type="button" data-section="PC" class="active">Passenger</button>
			<button type="button" data-section="CV">Commercial</button>
			<button type="button" data-section="Motorcycle">Motorbike</button>
			<button type="button" data-section="ALL">All vehicles</button>
		</div>
		<div class="epc-fitment-widget-shell" id="epc-fitment-widget-shell" style="display:none;">
			<div id="epc-fitment-part" class="epc-fitment-part" style="display:none;" aria-live="polite"></div>
			<div id="applicability_widget" class="epc-fitment-message">Select a brand/part box to load fitment.</div>
		</div>
	</div>
</div>
<?php } ?>
<script>
(function(){
	var button = document.getElementById('epc-fitment-check-btn');
	var panel = document.getElementById('epc-fitment-panel');
	var close = document.getElementById('epc-fitment-close');
	var brandsBox = document.getElementById('epc-fitment-brands');
	var typesBox = document.getElementById('epc-fitment-types');
	var widgetShell = document.getElementById('epc-fitment-widget-shell');
	var selectedArticle = '';
	var selectedBrand = '';
	var selectedSection = 'PC';
	var selectedFitment = null;
	var selectedPartDetail = null;
	var brandsLoaded = false;
	var brandsLoadedArticle = '';
	var pendingPreferredBrand = '';
	var partBox = document.getElementById('epc-fitment-part');
	if(!panel || !brandsBox) { return; }
	function esc(value) {
		return String(value == null ? '' : value).replace(/[&<>"']/g, function(ch) {
			return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[ch];
		});
	}
	function compact(value) {
		return String(value || '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
	}
	function defaultArticle() {
		if(selectedArticle) { return selectedArticle; }
		if(button) { return button.getAttribute('data-article') || ''; }
		return '';
	}
	function fitmentArticle(row) {
		return row.DISPLAY_NR || row.SEARCH_NUMBER || row.ARTICLE || defaultArticle();
	}
	function fitmentDisplay(row) {
		return row.DISPLAY_NR || row.SEARCH_NUMBER || defaultArticle();
	}
	function brandsEquivalent(left, right) {
		return compact(left) !== '' && compact(left) === compact(right);
	}
	function setMessage(message) {
		brandsBox.className = 'epc-fitment-message';
		brandsBox.innerHTML = message;
	}
	function api(action, params) {
		var query = Object.keys(params || {}).map(function(key) {
			return encodeURIComponent(key) + '=' + encodeURIComponent(params[key]);
		}).join('&');
		var url = '/api/umapi_proxy.php?action=' + encodeURIComponent(action) + (query ? '&' + query : '') + '&language=en&vehicle_type=PC';
		return fetch(url, {cache: 'no-store', credentials: 'same-origin'})
			.then(function(response) {
				return response.json().catch(function() { return {}; }).then(function(data) {
					if(response.ok) { return data; }
					if(data && (Array.isArray(data.data) || data.PC || data.CV || data.Motorcycle)) { return data; }
					var err = new Error((data && data.message) ? String(data.message) : ('HTTP ' + response.status));
					err.status = response.status;
					err.data = data;
					return Promise.reject(err);
				});
			});
	}
	function fitmentErrorMessage(err, fallback) {
		if(err && err.data && err.data.message) { return String(err.data.message); }
		if(err && err.message) { return String(err.message); }
		return fallback || 'Fitment lookup is temporarily unavailable.';
	}
	function loadEpartscrossFitmentFallback(article, widget) {
		if(!widget || !article) { return; }
		widget.className = 'epc-fitment-message';
		widget.innerHTML = '<div class="epc-fitment-message">Loading vehicle applicability from cross-reference catalog...</div>';
		var oldScript = document.getElementById('epc-fitment-epartscross-script');
		if(oldScript && oldScript.parentNode) { oldScript.parentNode.removeChild(oldScript); }
		var script = document.createElement('script');
		script.id = 'epc-fitment-epartscross-script';
		script.type = 'text/javascript';
		script.async = true;
		script.onerror = function() {
			widget.innerHTML = '<div class="epc-fitment-message">Vehicle fitment is temporarily unavailable. Update the Epart catalog API key in Control Panel or try again later.</div>';
		};
		var lang = (document.documentElement.getAttribute('lang') || 'en').toLowerCase();
		if(lang !== 'ru') { lang = 'en'; }
		script.src = '/api/epartscross_fitment.js.php?n=' + encodeURIComponent(article) + '&lang=' + encodeURIComponent(lang) + '&_=' + Date.now();
		document.body.appendChild(script);
	}
	function rowsFromPayload(data) {
		return Array.isArray(data) ? data : (data && Array.isArray(data.data) ? data.data : []);
	}
	function yearRange(row) {
		var from = row.CI_FROM || '';
		var to = row.CI_TO || '';
		if(from && to) { return from + ' - ' + to; }
		if(from) { return from + ' - now'; }
		return to || '';
	}
	function fitmentPowerText(row) {
		var kw = row.POWER_KW || row.POWER_KW_START || '';
		var ps = row.POWER_PS || row.POWER_PS_START || '';
		if(kw && ps) { return kw + ' kW / ' + ps + ' PS'; }
		if(kw) { return String(kw) + ' kW'; }
		if(ps) { return String(ps) + ' PS'; }
		return '';
	}
	function fitmentEngineText(row) {
		return [row.CAPACITY_TECH || row.CAPACITY_LT || '', row.FUEL_TYPE || '', row.BODY_TYPE || row.PLATFORM_TYPE || ''].filter(Boolean).join(' / ');
	}
	function fitmentModificationText(row) {
		return row.PASSENGER_CAR || row.COMMERCIAL_VEHICLE || row.MOTORBIKE || '';
	}
	function csvValue(value) {
		var text = String(value == null ? '' : value);
		if(/[",\r\n]/.test(text)) {
			return '"' + text.replace(/"/g, '""') + '"';
		}
		return text;
	}
	function downloadFitmentExcel(rows) {
		if(!rows || !rows.length) { return; }
		var sectionLabel = selectedSection === 'ALL' ? 'All vehicles' : selectedSection;
		var sheetRows = [
			['Part brand', selectedBrand || ''],
			['Part number', selectedArticle || defaultArticle() || ''],
			['Vehicle type', sectionLabel],
			['Exported', new Date().toISOString().slice(0, 19).replace('T', ' ')],
			[],
			['Make', 'Model', 'Modification', 'Year', 'Power', 'Engine / fuel']
		];
		rows.forEach(function(row) {
			sheetRows.push([
				row.MANUFACTURER || '',
				row.MODEL_SERIES || '',
				fitmentModificationText(row),
				yearRange(row),
				fitmentPowerText(row),
				fitmentEngineText(row)
			]);
		});
		var csv = sheetRows.map(function(line) {
			return line.map(csvValue).join(',');
		}).join('\r\n');
		var blob = new Blob(['\ufeff' + csv], {type: 'text/csv;charset=utf-8;'});
		var link = document.createElement('a');
		var safeBrand = String(selectedBrand || 'brand').replace(/[^\w\-]+/g, '_');
		var safeArticle = String(selectedArticle || defaultArticle() || 'part').replace(/[^\w\-]+/g, '_');
		link.href = URL.createObjectURL(blob);
		link.download = safeBrand + '-' + safeArticle + '-fitment-' + String(selectedSection || 'PC').toLowerCase() + '.csv';
		document.body.appendChild(link);
		link.click();
		window.setTimeout(function() {
			URL.revokeObjectURL(link.href);
			if(link.parentNode) { link.parentNode.removeChild(link); }
		}, 100);
	}
	function criteriaList(detail) {
		var rows = [];
		['CRITERIAS', 'LA_CRITERIAS'].forEach(function(key) {
			var list = detail && detail[key];
			if(Array.isArray(list)) {
				list.forEach(function(item) { rows.push(item); });
			}
		});
		return rows;
	}
	function criteriaFind(detail, patterns) {
		var list = criteriaList(detail);
		for(var i = 0; i < list.length; i++) {
			var label = String(list[i].CRI_DES || list[i].CRI_SHORT_DES || '').toLowerCase();
			for(var j = 0; j < patterns.length; j++) {
				if(label.indexOf(patterns[j]) !== -1) {
					var value = list[i].VALUE || list[i].DES || '';
					if(value !== '' && value !== null) {
						return String(value) + (list[i].CRI_UNIT_DES ? ' ' + list[i].CRI_UNIT_DES : '');
					}
				}
			}
		}
		return '';
	}
	function articleImageUrl(detail) {
		if(!detail || !detail.MEDIA_FILE || !detail.SUP_ID) { return ''; }
		return 'https://image.umapi.ru/IMAGE/' + encodeURIComponent(detail.SUP_ID) + '/' + encodeURIComponent(detail.MEDIA_FILE);
	}
	var brandImageCache = {};
	function brandImageKey(brand, article) {
		return compact(brand) + '|' + compact(article);
	}
	function ensureImageLightbox() {
		if(document.getElementById('epc-image-lightbox')) { return; }
		var el = document.createElement('div');
		el.id = 'epc-image-lightbox';
		el.className = 'epc-image-lightbox';
		el.innerHTML = '<div class="epc-image-lightbox__backdrop"></div><div class="epc-image-lightbox__panel"><button type="button" class="epc-image-lightbox__close" aria-label="Close photo">&times;</button><img src="" alt=""></div>';
		document.body.appendChild(el);
		el.querySelector('.epc-image-lightbox__backdrop').onclick = function() { epcCloseImageLightbox(); };
		el.querySelector('.epc-image-lightbox__close').onclick = function() { epcCloseImageLightbox(); };
	}
	function epcCloseImageLightbox() {
		var el = document.getElementById('epc-image-lightbox');
		if(el) { el.classList.remove('active'); }
	}
	function epcOpenImageLightbox(url, alt) {
		if(!url) { return; }
		ensureImageLightbox();
		var el = document.getElementById('epc-image-lightbox');
		var img = el.querySelector('img');
		img.src = url;
		img.alt = alt || '';
		el.classList.add('active');
	}
	window.epcOpenImageLightbox = epcOpenImageLightbox;
	function bindPartImageClick(container, url, alt) {
		if(!container || !url) { return; }
		var media = container.querySelector('.epc-fitment-part-card__media');
		if(!media) { return; }
		media.classList.add('epc-fitment-part-card__media--clickable');
		media.setAttribute('role', 'button');
		media.setAttribute('tabindex', '0');
		media.setAttribute('aria-label', 'View larger photo');
		media.onclick = function(event) {
			event.preventDefault();
			event.stopPropagation();
			epcOpenImageLightbox(url, alt);
		};
		media.onkeydown = function(event) {
			if(event.key === 'Enter' || event.key === ' ') {
				event.preventDefault();
				epcOpenImageLightbox(url, alt);
			}
		};
	}
	function brandCardThumbHtml(brand, article, existingUrl) {
		if(existingUrl) {
			return '<span class="epc-fitment-brand-card__thumb epc-fitment-brand-card__thumb--loaded" data-epc-photo-url="' + esc(existingUrl) + '"><img src="' + esc(existingUrl) + '" alt="" loading="lazy"></span>';
		}
		return '<span class="epc-fitment-brand-card__thumb epc-fitment-brand-card__thumb--empty"><i class="fa fa-image"></i></span>';
	}
	function updateBrandCardThumb(key, url, brand, article) {
		if(!brandsBox || !key || !url) { return; }
		var card = brandsBox.querySelector('[data-fitment-key="' + key + '"]');
		if(!card) { return; }
		var b = brand || card.getAttribute('data-fitment-brand') || '';
		var a = article || card.getAttribute('data-fitment-article') || '';
		var thumb = card.querySelector('.epc-fitment-brand-card__thumb');
		if(!thumb) { return; }
		var span = document.createElement('span');
		span.className = 'epc-fitment-brand-card__thumb epc-fitment-brand-card__thumb--loaded';
		span.innerHTML = '<img src="' + esc(url) + '" alt="" loading="lazy">';
		span.style.cursor = 'zoom-in';
		span.onclick = function(e) {
			e.preventDefault();
			e.stopPropagation();
			epcOpenImageLightbox(url, b + ' ' + a);
		};
		if(thumb.parentNode) {
			thumb.parentNode.replaceChild(span, thumb);
		}
	}
	function fetchBrandImage(brand, article) {
		var key = brandImageKey(brand, article);
		if(Object.prototype.hasOwnProperty.call(brandImageCache, key)) {
			var cached = brandImageCache[key];
			return cached && typeof cached.then === 'function' ? cached : Promise.resolve(cached);
		}
		brandImageCache[key] = api('analogs', {article: article, brand: brand, limit: 12, offset: 0, source: 'part_search'})
			.then(function(data) {
				var rows = rowsFromPayload(data);
				var target = rows.filter(function(row) {
					return compact(row.BRAND || row.SUP_BRAND) === compact(brand) && compact(row.ARTICLE_NR || row.ARTICLE || row.ART_ARTICLE_NR) === compact(article);
				})[0] || rows.filter(function(row) {
					return compact(row.BRAND || row.SUP_BRAND) === compact(brand);
				})[0] || rows[0];
				if(!target || !target.ART_ID) { return ''; }
				return api('article', {id: target.ART_ID, source: 'part_search'}).then(articleImageUrl);
			})
			.then(function(url) {
				brandImageCache[key] = url || '';
				return url || '';
			})
			.catch(function() {
				brandImageCache[key] = '';
				return '';
			});
		return brandImageCache[key];
	}
	window.epcFetchBrandPartImage = fetchBrandImage;
	function detailFacts(detail) {
		var facts = [];
		if(detail.PACK_UNIT) { facts.push({label: 'Pack unit', value: detail.PACK_UNIT}); }
		if(detail.QUANTITY_PER_UNIT) { facts.push({label: 'Qty / unit', value: detail.QUANTITY_PER_UNIT}); }
		if(detail.MATERIAL_MARK) { facts.push({label: 'Material', value: detail.MATERIAL_MARK}); }
		if(detail.STATUS_DES) { facts.push({label: 'Status', value: detail.STATUS_DES}); }
		if(Array.isArray(detail.EAN_CODES) && detail.EAN_CODES.length) {
			facts.push({label: 'EAN', value: detail.EAN_CODES.map(function(code) {
				return typeof code === 'string' ? code : (code.EAN || code.CODE || '');
			}).filter(Boolean).join(', ')});
		}
		if(Array.isArray(detail.INFO)) {
			detail.INFO.forEach(function(item, index) {
				var text = item.TEXT || item.DES || '';
				if(text) { facts.push({label: 'Info ' + (index + 1), value: text}); }
			});
		}
		return facts;
	}
	function renderPartDetail(detail, brand, article) {
		if(!partBox) { return; }
		selectedPartDetail = detail || null;
		if(!detail) {
			partBox.style.display = 'none';
			partBox.innerHTML = '';
			return;
		}
		var name = detail.COMPLETE_DES || detail.DES || detail.ART_PRODUCT_NAME || 'Part';
		var img = articleImageUrl(detail);
		var weight = criteriaFind(detail, ['weight', 'net weight', 'gross weight', 'weight [']);
		var country = criteriaFind(detail, ['country', 'country of origin', 'origin']) || detail.COUNTRY || detail.COUNTRY_OF_ORIGIN || '';
		var specs = criteriaList(detail).filter(function(item) {
			var label = String(item.CRI_DES || item.CRI_SHORT_DES || '').toLowerCase();
			return label.indexOf('weight') === -1 && label.indexOf('country') === -1 && label.indexOf('origin') === -1;
		});
		var facts = detailFacts(detail);
		var specHtml = specs.slice(0, 8).map(function(item) {
			var label = item.CRI_SHORT_DES || item.CRI_DES || 'Spec';
			var value = item.VALUE || item.DES || '';
			var unit = item.CRI_UNIT_DES || '';
			return '<span class="epc-fitment-spec-chip" title="' + esc(label) + '"><b>' + esc(label) + '</b> ' + esc(value + (unit ? ' ' + unit : '')) + '</span>';
		}).join('');
		var factsHtml = facts.slice(0, 4).map(function(item) {
			return '<span class="epc-fitment-spec-chip epc-fitment-spec-chip--muted"><b>' + esc(item.label) + '</b> ' + esc(item.value) + '</span>';
		}).join('');
		partBox.className = 'epc-fitment-part';
		partBox.style.display = 'block';
		partBox.innerHTML =
			'<div class="epc-fitment-part-card">' +
				'<div class="epc-fitment-part-card__media">' +
					(img ? '<img src="' + esc(img) + '" alt="" loading="lazy" onerror="this.style.display=\'none\';this.nextElementSibling.style.display=\'flex\';">' : '') +
					'<div class="epc-fitment-part-card__placeholder"' + (img ? ' style="display:none;"' : '') + '><i class="fa fa-image"></i></div>' +
				'</div>' +
				'<div class="epc-fitment-part-card__main">' +
					'<p class="epc-fitment-part-card__brand">' + esc(brand) + ' · <span>' + esc(article) + '</span></p>' +
					'<h4 class="epc-fitment-part-card__name">' + esc(name) + '</h4>' +
					'<dl class="epc-fitment-part-card__facts">' +
						'<div><dt>Weight</dt><dd>' + esc(weight || '—') + '</dd></div>' +
						'<div><dt>Country</dt><dd>' + esc(country || '—') + '</dd></div>' +
					'</dl>' +
				'</div>' +
				'<div class="epc-fitment-part-card__specs">' +
					'<div class="epc-fitment-part-card__specs-title">Specifications &amp; details</div>' +
					'<div class="epc-fitment-part-card__chips">' + (specHtml || '') + (factsHtml || '') +
						(!specHtml && !factsHtml ? '<span class="epc-fitment-spec-chip epc-fitment-spec-chip--muted">No extra specifications available for this part.</span>' : '') +
					'</div>' +
				'</div>' +
			'</div>';
		bindPartImageClick(partBox, img, brand + ' ' + article);
		if(img) {
			brandImageCache[brandImageKey(brand, article)] = img;
			updateBrandCardThumb(brandImageKey(brand, article), img, brand, article);
		}
	}
	function sectionRows(fitment, section) {
		if(!fitment) { return []; }
		if(section === 'ALL') {
			return [].concat(fitment.PC || [], fitment.CV || [], fitment.Motorcycle || []);
		}
		return fitment[section] || [];
	}
	function renderFitment(fitment) {
		var widget = document.getElementById('applicability_widget');
		if(!widget) { return; }
		selectedFitment = fitment || {};
		var rows = sectionRows(selectedFitment, selectedSection);
		var total = (selectedFitment.PC || []).length + (selectedFitment.CV || []).length + (selectedFitment.Motorcycle || []).length;
		if(!total) {
			widget.className = 'epc-fitment-message';
			widget.innerHTML = '<div class="epc-fitment-message">No vehicle fitment was found in Epart catalog for this part.</div>';
			return;
		}
		if(!rows.length) {
			widget.className = 'epc-fitment-message';
			widget.innerHTML = '<div class="epc-fitment-message">No rows in this vehicle type. Choose another tab or All vehicles.</div>';
			return;
		}
		var html = '<table class="table table-condensed table-striped epc-umapi-table"><thead><tr><th>Make</th><th>Model</th><th>Modification</th><th>Year</th><th>Power</th><th>Engine / fuel</th></tr></thead><tbody>';
		rows.forEach(function(row) {
			html += '<tr><td>' + esc(row.MANUFACTURER || '') + '</td><td>' + esc(row.MODEL_SERIES || '') + '</td><td>' + esc(fitmentModificationText(row)) + '</td><td>' + esc(yearRange(row)) + '</td><td>' + esc(fitmentPowerText(row)) + '</td><td>' + esc(fitmentEngineText(row)) + '</td></tr>';
		});
		html += '</tbody></table>';
		var sectionLabel = selectedSection === 'ALL' ? 'All vehicles' : selectedSection;
		widget.className = 'epc-fitment-widget-table-host';
		widget.innerHTML = '<div class="epc-fitment-results-toolbar">'
			+ '<span class="epc-fitment-results-toolbar__count"><strong>' + rows.length + '</strong> vehicle' + (rows.length === 1 ? '' : 's') + ' <span class="epc-fitment-results-toolbar__meta">(' + esc(sectionLabel) + ')</span></span>'
			+ '<button type="button" class="btn btn-xs btn-default epc-fitment-download-btn" id="epc-fitment-download-btn" title="Download fitment list for Excel">'
			+ '<i class="fa fa-file-excel-o" aria-hidden="true"></i> Download Excel</button>'
			+ '</div>'
			+ '<div class="epc-fitment-table-scroll">' + html + '</div>';
		var downloadBtn = document.getElementById('epc-fitment-download-btn');
		if(downloadBtn) {
			downloadBtn.onclick = function() { downloadFitmentExcel(rows); };
		}
	}
	function resolveAndLoadFitment(article, brand) {
		var widget = document.getElementById('applicability_widget');
		if(!widget || !article || !brand) { return; }
		widgetShell.style.display = 'block';
		if(typesBox) { typesBox.style.display = 'flex'; }
		widget.innerHTML = '<div class="epc-fitment-message">Looking up part details in Epart catalog for ' + esc(brand) + ' ' + esc(article) + '...</div>';
		api('analogs', {article: article, brand: brand, limit: 30, offset: 0, source: 'fitment'})
			.then(function(data) {
				var rows = rowsFromPayload(data);
				var target = rows.filter(function(row) {
					return compact(row.BRAND || row.SUP_BRAND) === compact(brand) && compact(row.ARTICLE_NR || row.ARTICLE || row.ART_ARTICLE_NR) === compact(article);
				})[0] || rows.filter(function(row) {
					return compact(row.BRAND || row.SUP_BRAND) === compact(brand);
				})[0] || rows[0];
				if(!target || !target.ART_ID) {
					throw new Error('Article ID not found');
				}
				var artId = target.ART_ID;
				var displayBrand = target.BRAND || brand;
				var displayArticle = target.ARTICLE_NR || target.ART_ARTICLE_NR || article;
				widget.innerHTML = '<div class="epc-fitment-message">Loading vehicle fitment list...</div>';
				if(partBox) {
					partBox.style.display = 'block';
					partBox.innerHTML = '<div class="epc-fitment-message">Loading part photo, specifications, weight and country...</div>';
				}
				return Promise.all([
					api('article', {id: artId, source: 'fitment'}),
					api('article_links', {id: artId, source: 'fitment'})
				]).then(function(results) {
					renderPartDetail(results[0], displayBrand, displayArticle);
					return results[1];
				});
			})
			.then(renderFitment)
			.catch(function(err) {
				renderPartDetail(null, selectedBrand, selectedArticle);
				loadEpartscrossFitmentFallback(article, widget);
			});
	}
	function renderBrands(rows, preferredBrand) {
		if(!rows.length) {
			setMessage('No matching brand was found in Epart catalog for this part number.');
			return;
		}
		var preferred = preferredBrand || pendingPreferredBrand || '';
		pendingPreferredBrand = '';
		brandsBox.className = 'epc-fitment-brand-grid';
		brandsBox.innerHTML = rows.map(function(row, index) {
			var brand = row.BRAND || row.SUP_BRAND || row.MANUFACTURER || 'Brand';
			var number = fitmentDisplay(row);
			var title = row.TITLE || row.DES || 'Click to view fitment';
			var isActive = preferred ? brandsEquivalent(brand, preferred) : (index === 0);
			var key = brandImageKey(brand, fitmentArticle(row));
			var cachedImg = brandImageCache[key];
			var thumbUrl = typeof cachedImg === 'string' && cachedImg ? cachedImg : '';
			return '<button type="button" class="epc-fitment-brand-card' + (isActive ? ' active' : '') + '" data-fitment-key="' + esc(key) + '" data-fitment-brand="' + esc(brand) + '" data-fitment-article="' + esc(fitmentArticle(row)) + '">' +
				brandCardThumbHtml(brand, fitmentArticle(row), thumbUrl) +
				'<span class="epc-fitment-brand-card__text"><strong>' + esc(brand) + '</strong><span>' + esc(number) + '</span><small>' + esc(title) + '</small></span></button>';
		}).join('');
		Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function(card) {
			card.onclick = function() {
				Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function(item) { item.classList.remove('active'); });
				card.classList.add('active');
				selectedArticle = card.getAttribute('data-fitment-article') || selectedArticle;
				selectedBrand = card.getAttribute('data-fitment-brand') || selectedBrand;
				resolveAndLoadFitment(selectedArticle, selectedBrand);
			};
		});
		var target = null;
		if(preferred) {
			Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function(card) {
				if(!target && brandsEquivalent(card.getAttribute('data-fitment-brand') || '', preferred)) {
					target = card;
				}
			});
		}
		if(!target) {
			target = brandsBox.querySelector('.epc-fitment-brand-card');
		}
		if(target) {
			Array.prototype.forEach.call(brandsBox.querySelectorAll('.epc-fitment-brand-card'), function(item) { item.classList.remove('active'); });
			target.classList.add('active');
			selectedArticle = target.getAttribute('data-fitment-article') || selectedArticle;
			selectedBrand = target.getAttribute('data-fitment-brand') || selectedBrand;
			resolveAndLoadFitment(selectedArticle, selectedBrand);
		}
	}
	function resetFitmentWidget() {
		selectedFitment = null;
		selectedPartDetail = null;
		if(widgetShell) { widgetShell.style.display = 'none'; }
		if(typesBox) { typesBox.style.display = 'none'; }
		var widget = document.getElementById('applicability_widget');
		if(widget) {
			widget.innerHTML = '<div class="epc-fitment-message">Select a brand/part box to load fitment.</div>';
		}
		if(partBox) {
			partBox.style.display = 'none';
			partBox.innerHTML = '';
		}
	}
	function loadBrandsForArticle(article, preferredBrand) {
		article = String(article || '').trim();
		if(article === '') { return; }
		if(brandsLoadedArticle !== article) {
			brandsLoaded = false;
			resetFitmentWidget();
		}
		brandsLoaded = true;
		brandsLoadedArticle = article;
		selectedArticle = article;
		pendingPreferredBrand = String(preferredBrand || '').trim();
		if(button) { button.setAttribute('data-article', article); }
		setMessage('Loading matching brand and part number boxes from eparts catalog...');
		widgetShell.style.display = 'none';
		if(typesBox) { typesBox.style.display = 'none'; }
		api('brands', {article: article, source: 'fitment'})
			.then(function(data) {
				var rows = rowsFromPayload(data);
				if(data && data.offline_message && rows.length) {
					brandsBox.setAttribute('data-epc-fitment-offline', '1');
				} else if(brandsBox) {
					brandsBox.removeAttribute('data-epc-fitment-offline');
				}
				renderBrands(rows, pendingPreferredBrand);
			})
			.catch(function(err) {
				brandsLoaded = false;
				brandsLoadedArticle = '';
				setMessage(fitmentErrorMessage(err, 'Fitment brand lookup is temporarily unavailable.'));
			});
	}
	function ensureFitmentPanelPortal() {
		if(panel.parentNode !== document.body) {
			document.body.appendChild(panel);
		}
	}
	function resetFitmentPanelStyles() {
		panel.classList.remove('epc-fitment-panel--anchored', 'epc-fitment-panel--centered');
		panel.style.top = '';
		panel.style.left = '';
		panel.style.right = '';
		panel.style.bottom = '';
		panel.style.width = '';
		panel.style.height = '';
		panel.style.maxHeight = '';
		panel.style.transform = '';
	}
	function positionFitmentPanel(anchorEl) {
		resetFitmentPanelStyles();
		if(!anchorEl || typeof anchorEl.getBoundingClientRect !== 'function') {
			panel.classList.add('epc-fitment-panel--centered');
			return;
		}
		panel.classList.add('epc-fitment-panel--anchored');
		var rect = anchorEl.getBoundingClientRect();
		var panelW = Math.min(920, Math.max(320, window.innerWidth - 24));
		var panelH = Math.min(Math.max(360, window.innerHeight * 0.72), window.innerHeight - 24);
		var gap = 10;
		var top = rect.bottom + gap;
		if(top + panelH > window.innerHeight - 12) {
			top = Math.max(12, rect.top - panelH - gap);
		}
		if(top < 12) {
			top = 12;
			panelH = Math.min(panelH, window.innerHeight - top - 12);
		}
		var left = rect.left + (rect.width / 2) - (panelW / 2);
		left = Math.max(12, Math.min(left, window.innerWidth - panelW - 12));
		panel.style.width = panelW + 'px';
		panel.style.height = panelH + 'px';
		panel.style.maxHeight = panelH + 'px';
		panel.style.top = top + 'px';
		panel.style.left = left + 'px';
		window.setTimeout(function() {
			try {
				anchorEl.scrollIntoView({block: 'nearest', inline: 'nearest', behavior: 'smooth'});
			} catch(scrollErr) {}
		}, 40);
	}
	function openFitmentPanel(article, preferredBrand, anchorEl) {
		ensureFitmentPanelPortal();
		positionFitmentPanel(anchorEl);
		panel.classList.add('active');
		document.body.style.overflow = 'hidden';
		loadBrandsForArticle(article, preferredBrand);
	}
	window.epcOpenFitmentCheck = openFitmentPanel;
	if(typesBox) {
		Array.prototype.forEach.call(typesBox.querySelectorAll('button[data-section]'), function(typeButton) {
			typeButton.onclick = function() {
				Array.prototype.forEach.call(typesBox.querySelectorAll('button[data-section]'), function(item) { item.classList.remove('active'); });
				typeButton.classList.add('active');
				selectedSection = typeButton.getAttribute('data-section') || 'PC';
				renderFitment(selectedFitment);
			};
		});
	}
	if(button) {
		button.onclick = function() {
			if(panel.classList.contains('active')) {
				panel.classList.remove('active');
				document.body.style.overflow = '';
			} else {
				openFitmentPanel(
					button.getAttribute('data-article') || '',
					button.getAttribute('data-brand') || '',
					null
				);
			}
		};
	}
	if(close) {
		close.onclick = function() {
			panel.classList.remove('active');
			document.body.style.overflow = '';
			resetFitmentPanelStyles();
		};
	}
	document.addEventListener('keydown', function(event) {
		if(event.key === 'Escape' && panel.classList.contains('active')) {
			panel.classList.remove('active');
			document.body.style.overflow = '';
			resetFitmentPanelStyles();
		}
		if(event.key === 'Escape') {
			epcCloseImageLightbox();
		}
	});
})();
(function(){
	var crossBtn = document.getElementById('epc-cross-search-btn');
	if(!crossBtn || crossBtn.getAttribute('data-loaded') === '1') { return; }
	crossBtn.setAttribute('data-loaded', '1');
	var article = crossBtn.getAttribute('data-article') || '';
	var count = document.getElementById('epc-cross-search-count');
	var crossRefs = [];
	var crossStock = [];
	var crossTotal = 0;
	if(typeof epcCrossFallbackRows !== 'undefined' && epcCrossFallbackRows.length)
	{
		crossRefs = epcCrossFallbackSeedRows();
		crossTotal = crossRefs.length;
		crossBtn.disabled = false;
	}
	function esc(value) {
		return String(value == null ? '' : value).replace(/[&<>"']/g, function(ch) {
			return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[ch];
		});
	}
	function searchUrl(row) {
		if(typeof epcChpuBrandArticleUrl === 'function')
		{
			return epcChpuBrandArticleUrl(row.brand || '', row.article || '');
		}
		var url = '<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article=' + encodeURIComponent(row.article || '');
		if(row.brand) { url += '&brend=' + encodeURIComponent(row.brand); }
		return url;
	}
	function stockSearchUrl(item) {
		if(typeof epcChpuBrandArticleUrl === 'function')
		{
			return epcChpuBrandArticleUrl(item.brand || '', item.article || '');
		}
		return '<?php echo $multilang_params['lang_href']; ?>/shop/part_search?article=' + encodeURIComponent(item.article || '') + (item.brand ? '&brend=' + encodeURIComponent(item.brand) : '');
	}
	function csvValue(value) {
		value = String(value == null ? '' : value);
		return '"' + value.replace(/"/g, '""') + '"';
	}
	function downloadCsv(filename, rows) {
		var csv = rows.map(function(row) { return row.map(csvValue).join(','); }).join('\r\n');
		var blob = new Blob(['\ufeff' + csv], {type: 'text/csv;charset=utf-8;'});
		var link = document.createElement('a');
		link.href = URL.createObjectURL(blob);
		link.download = filename;
		document.body.appendChild(link);
		link.click();
		window.setTimeout(function() {
			URL.revokeObjectURL(link.href);
			link.parentNode.removeChild(link);
		}, 100);
	}
	function downloadReferenceCsv(refs) {
		var rows = [['Brand', 'Article', 'Search URL']];
		refs.forEach(function(row) {
			rows.push([row.brand || '', row.article || '', location.origin + searchUrl(row)]);
		});
		downloadCsv('cross-references-' + article + '.csv', rows);
	}
	function downloadStockCsv(stock) {
		var rows = [['Brand', 'Article', 'Name', 'Price', 'Currency', 'Quantity', 'Search URL']];
		stock.forEach(function(item) {
			rows.push([item.brand || '', item.article || '', item.name || '', item.price || '', item.currency || '', item.qty || '', location.origin + stockSearchUrl(item)]);
		});
		downloadCsv('cross-stock-' + article + '.csv', rows);
	}
	function crossRefInStock(row) {
		if(typeof epcCrossRefIsInStock === 'function')
		{
			return epcCrossRefIsInStock(row.brand, row.article_norm || row.article);
		}
		return false;
	}
	function openCrossModal(refs, stock, total) {
		var existing = document.getElementById('epc-cross-modal');
		if(existing && existing.parentNode) {
			existing.parentNode.removeChild(existing);
		}
		var loaded = refs.length;
		var catalogTotal = parseInt(total, 10) || loaded;
		var modal = document.createElement('div');
		modal.id = 'epc-cross-modal';
		modal.className = 'epc-cross-modal';
		modal.innerHTML = '<div class="epc-cross-modal__dialog" role="dialog" aria-modal="true">' +
			'<div class="epc-cross-modal__head"><div><strong>Cross references for ' + esc(article) + '</strong><span></span></div><button type="button" class="epc-cross-modal__close" aria-label="Close">&times;</button></div>' +
			'<div class="epc-cross-modal__tools"><input type="search" class="form-control" id="epc-cross-modal-filter" placeholder="Filter brand or article"><button type="button" class="btn btn-primary" id="epc-cross-download-refs">Download references CSV</button><button type="button" class="btn btn-success" id="epc-cross-download-stock">Download stock CSV</button></div>' +
			'<div class="epc-cross-modal__body"><div class="epc-cross-modal__table"><table class="table table-striped table-condensed"><thead><tr><th>#</th><th>Brand</th><th>Article</th><th>Availability</th><th class="text-right">Action</th></tr></thead><tbody id="epc-cross-modal-rows"></tbody></table></div>' +
			'<div class="epc-cross-modal__stock"><strong>In stock on UAE price lists</strong><div id="epc-cross-modal-stock"></div></div></div>' +
			'</div>';
		document.body.appendChild(modal);
		var rowsBox = modal.querySelector('#epc-cross-modal-rows');
		var stockRows = modal.querySelector('#epc-cross-modal-stock');
		var filter = modal.querySelector('#epc-cross-modal-filter');
		var headNote = modal.querySelector('.epc-cross-modal__head span');
		function availabilityCell(row) {
			if(typeof epcCrossAvailabilityBadgeHTML === 'function')
			{
				return epcCrossAvailabilityBadgeHTML(crossRefInStock(row), '');
			}
			return crossRefInStock(row) ? '<span class="epc-avail-badge epc-avail-badge--yes">In stock</span>' : '<span class="epc-avail-badge epc-avail-badge--no">Not in stock</span>';
		}
		function renderRows() {
			var term = (filter.value || '').toLowerCase();
			var shown = 0;
			rowsBox.innerHTML = refs.map(function(row, index) {
				var text = String((row.brand || '') + ' ' + (row.article || '')).toLowerCase();
				if(term && text.indexOf(term) === -1) { return ''; }
				shown++;
				return '<tr><td>' + (index + 1) + '</td><td><strong>' + esc(row.brand || '') + '</strong></td><td>' + esc(row.article || '') + '</td><td>' + availabilityCell(row) + '</td><td class="text-right"><a class="btn btn-xs btn-primary" href="' + esc(searchUrl(row)) + '">Search availability & price</a></td></tr>';
			}).join('') || '<tr><td colspan="5" class="text-center">No matching cross reference found.</td></tr>';
			if(catalogTotal > loaded)
			{
				headNote.textContent = shown + ' shown on this page (' + loaded + ' loaded of ' + catalogTotal.toLocaleString() + ' in catalog)';
			}
			else
			{
				headNote.textContent = shown + ' of ' + catalogTotal + ' references shown';
			}
		}
		stockRows.innerHTML = stock.length ? stock.map(function(item) {
			var priceText = (typeof epcFormatMoney === 'function') ? epcFormatMoney(item.price || 0) : (esc(item.price || '') + ' ' + esc(item.currency || ''));
			return '<div class="epc-cross-modal__stock-row"><span><strong>' + esc(item.brand || '') + ' ' + esc(item.article || '') + '</strong><small>' + esc(item.name || '') + '</small></span><b>' + priceText + '</b><em>Qty: ' + esc(item.qty || '') + '</em><a class="btn btn-xs btn-success" href="' + esc(stockSearchUrl(item)) + '">Open price/cart</a></div>';
		}).join('') : '<div class="epc-cross-modal__empty">No cross stock match in loaded price lists.</div>';
		renderRows();
		filter.addEventListener('input', renderRows);
		modal.querySelector('.epc-cross-modal__close').onclick = function() { modal.parentNode.removeChild(modal); };
		modal.addEventListener('click', function(event) { if(event.target === modal) { modal.parentNode.removeChild(modal); } });
		modal.querySelector('#epc-cross-download-refs').onclick = function() { downloadReferenceCsv(refs); };
		modal.querySelector('#epc-cross-download-stock').onclick = function() { downloadStockCsv(stock); };
		filter.focus();
	}
	function epcTryRenderPendingCrossStock() {
		if(!epcPendingCrossStock || !epcPendingCrossStock.length) { return; }
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing) {
			if(typeof epcTryMergePendingCrossStock === 'function') {
				epcTryMergePendingCrossStock();
			}
			return;
		}
		if(epcChpuMainSearchPending) { return; }
		renderStockResultsArea(epcPendingCrossStock);
	}
	function renderStockResultsArea(stock) {
		var productsArea = document.getElementById('products_area');
		if(!productsArea || !stock.length) { return; }
		if(typeof epc_brand_picker_mode !== 'undefined' && epc_brand_picker_mode) { return; }
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing) { return; }
		if(epcChpuMainSearchPending) { return; }
		var mainTable = productsArea.querySelector ? productsArea.querySelector('#all_table_products') : null;
		if(mainTable && !(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)) {
			return;
		}
		if(typeof Products_All_Asked !== 'undefined' && !Products_All_Asked) {
			return;
		}
		if(productsArea.querySelector) {
			var oldBlock = productsArea.querySelector('.epc-cross-stock-results');
			if(oldBlock && oldBlock.parentNode) {
				oldBlock.parentNode.removeChild(oldBlock);
			}
		}
		var html = '<div class="epc-cross-stock-results"><div class="epc-cross-stock-results__head"><strong>Cross reference stock found</strong><span>These items match cross numbers for ' + esc(article) + ' and are available in your loaded stock. Full warehouse filters and cart actions appear in the main results table when the supplier search completes.</span></div>';
		html += '<div class="table-responsive"><table class="table table-striped table-condensed"><thead><tr><th>Brand</th><th>Part number</th><th>Name</th><th>Warehouse</th><th class="text-right">Price</th><th class="text-right">Qty</th><th class="text-right">Action</th></tr></thead><tbody>';
		html += stock.map(function(item){
			var priceText = (typeof epcFormatMoney === 'function') ? epcFormatMoney(item.price || 0) : (esc(item.price || '') + ' ' + esc(item.currency || ''));
			return '<tr><td><strong>' + esc(item.brand || '') + '</strong></td><td>' + esc(item.article || '') + '</td><td>' + esc(item.name || '') + '</td><td>' + esc(item.warehouse || '') + '</td><td class="text-right"><strong>' + priceText + '</strong></td><td class="text-right">' + esc(item.qty || '') + '</td><td class="text-right"><a class="btn btn-xs btn-primary" href="' + esc(stockSearchUrl(item)) + '">Search in full table</a></td></tr>';
		}).join('');
		html += '</tbody></table></div></div>';
		if(mainTable) {
			var existingCross = productsArea.querySelector('.epc-cross-stock-results');
			if(existingCross && existingCross.parentNode) {
				existingCross.parentNode.removeChild(existingCross);
			}
			mainTable.insertAdjacentHTML('afterend', html);
		} else if(productsArea.innerHTML.replace(/\s/g, '') === '') {
			if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing) {
				return;
			}
			productsArea.innerHTML = html;
		} else {
			productsArea.insertAdjacentHTML('beforeend', html);
		}
		var processing = document.getElementById('processing_indicator');
		if(processing && productsArea.innerHTML.indexOf('all_table_products') === -1) {
			if(!(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && epcChpuMainSearchPending)) {
				processing.innerHTML = '';
			}
		}
	}
	function scheduleStockResultsArea(stock) {
		epcPendingCrossStock = stock;
		epcTryRenderPendingCrossStock();
	}
	function openLoadedCrossModal() {
		openCrossModal(crossRefs, crossStock, crossTotal || crossRefs.length);
	}
	crossBtn.onclick = openLoadedCrossModal;
	if(typeof epcFetchCrossData === 'function')
	{
		epcFetchCrossData(article).then(function(data){
			crossRefs = data && data.references ? data.references : [];
			crossStock = data && data.stock ? data.stock : [];
			crossTotal = data && data.total ? parseInt(data.total, 10) : crossRefs.length;
			if(count && typeof epcFormatCrossCountLabel === 'function')
			{
				var loaded = data.reference_count ? parseInt(data.reference_count, 10) : crossRefs.length;
				var stockN = crossStock.length;
				count.textContent = epcFormatCrossCountLabel(loaded, crossTotal, stockN);
			}
		});
	}
})();
</script>
<?php
// Поиск отображается только в мобильной версии, нужен что бы отобразить поиск выше фильтра.
if($initial_position_search == 1 && empty($epc_brand_picker_mode) && empty($epc_chpu_direct_pricing)){
	$value_for_input_search = str_replace('"','',$value_for_input_search);
?>
<div class="hidden-md hidden-lg col-md-12 search_limo">
	<div class="panel panel-primary">
		<div class="panel-heading"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(4176); ?></div>
		<div style="position:relative;" class="panel-body">
			<form role="form" action="<?php echo $multilang_params['lang_href']; ?>/shop/part_search" method="GET">
				<div class="input-group">
					<input value="<?php echo $value_for_input_search; ?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4176); ?>" name="article" />
					<span class="input-group-btn">
						<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
					</span>
				</div>
			</form>
		</div>
	</div>
</div>
<?php
}
?>




<!----- ФИЛЬТР ------>
<?php
// Стили блока фильтра и отображения проценки
if($initial_position_filter == 1){
	$filter_div_a_text = '<i class="fa fa-arrow-circle-up" aria-hidden="true"></i> '.translate_str_by_id(4300);
	$initial_position_filter = 0;// Нужно для того что бы при первом отображении правильно сработало
}else{
	$filter_div_a_text = '<i class="fa fa-arrow-circle-down" aria-hidden="true"></i> '.translate_str_by_id(4301);
	$initial_position_filter = 1;// Нужно для того что бы при первом отображении правильно сработало
}

// Стили блоков при отображении или скрытии поиска
if($initial_position_search == 1){
	// Отображается поиск
	$filter_div_style_body = '';
	$filter_div_class = 'col-md-3';
}else{
	// скрыт поиск
	$filter_div_class = 'hidden';
	$filter_div_style_body = ' display:none;';
}
$procenka_div_class = 'col-md-12';
$epc_part_search_main_class = 'col-md-12';
if ($filter_div_class === 'col-md-3' && (int)$initial_position_filter === 0)
{
	$epc_part_search_main_class = 'col-md-9';
}
if (!empty($epc_chpu_direct_pricing)) {
	$filter_div_a_text = '<i class="fa fa-arrow-circle-up" aria-hidden="true"></i> '.translate_str_by_id(4300);
	$initial_position_filter = 1;
	$filter_div_class = 'col-md-3';
	$filter_div_style_body = '';
	$epc_part_search_main_class = 'col-md-9';
}
if (!empty($epc_brand_picker_mode)) {
	$initial_position_search = 0;
	$filter_div_class = 'hidden';
	$filter_div_style_body = ' display:none;';
	$epc_part_search_main_class = 'col-md-12';
	$procenka_div_class = 'col-md-12 epc-brand-picker-procenka-hidden';
}
?>
<script>
// Свернут или развернут фильтр
var this_position_filter = <?=$initial_position_filter;?>;

// Отображается ли поиск
var this_position_search = <?=$initial_position_search;?>;

// Разворачиваем либо сворачиваем фильтр в зависимости от текущего положения.
function show_filter_clicked(){
	
	var filter_div = document.getElementById('filter_div');
	var filter_position = document.getElementById('filter_position');
	var filter_div_a_text = document.getElementById('filter_div_a_text');
	var filter_div_style_body = document.getElementById('filter_div_style_body');
	
	var procenka_div = document.getElementById('procenka_div');
	
	filter_div.classList.remove('hidden');
	
	if(this_position_filter == 1){
		// Скрываем фильтр
		filter_position.style.display = 'none';
		this_position_filter = 0;
		document.getElementById('footer-filter').style.display = 'none';
		document.getElementById('footer_filter_reset').style.display = 'none';
		
		if(this_position_search == 0){
			// Поиск скрыт
			filter_div.classList.remove('col-md-3');
			filter_div.classList.add('col-md-12');
			
			filter_div_style_body.style.display = 'none';
		}
		
		var search_main = document.querySelector('.epc-part-search-main');
		if(search_main){
			search_main.classList.remove('col-md-9');
			search_main.classList.add('col-md-12');
		}
		
		
		filter_div_a_text.innerHTML = '<i class="fa fa-arrow-circle-down" aria-hidden="true"></i> <?php echo translate_str_by_id(4301); ?>';
	}else{
		// отображаем фильтр
		filter_position.style.display = 'block';
		this_position_filter = 1;
		document.getElementById('footer-filter').style.display = 'block';
		document.getElementById('footer_filter_reset').style.display = 'block';
		
		if(this_position_search == 0){
			// Поиск скрыт
			filter_div.classList.remove('col-md-12');
			filter_div.classList.add('col-md-3');
			
			filter_div_style_body.style.display = 'block';
		}
		
		var search_main = document.querySelector('.epc-part-search-main');
		if(search_main){
			search_main.classList.remove('col-md-12');
			search_main.classList.add('col-md-9');
		}
		
		filter_div_style_body = '';
		filter_div_a_text.innerHTML = '<i class="fa fa-arrow-circle-up" aria-hidden="true"></i> <?php echo translate_str_by_id(4300); ?>';
	}
	epcSyncPartSearchLayout();
}
function epcSyncPartSearchLayout()
{
	var filter_div = document.getElementById('filter_div');
	var procenka_div = document.getElementById('procenka_div');
	var search_main = document.querySelector('.epc-part-search-main');
	if(!filter_div || !procenka_div)
	{
		return;
	}
	filter_div.classList.remove('hidden');
	if(this_position_search == 1)
	{
		filter_div.classList.remove('col-md-12');
		filter_div.classList.add('col-md-3');
		if(search_main){
			if(this_position_filter == 1){
				search_main.classList.remove('col-md-12');
				search_main.classList.add('col-md-9');
			}else{
				search_main.classList.remove('col-md-9');
				search_main.classList.add('col-md-12');
			}
		}
	}
}
function epcPrimeWarehouseFilter()
{
	// Disabled: placeholder "Warehouse of shop" block confused users on CHPU/UAE search.
	return;
}
</script>
<!----- Блока фильтра ------>
<div class="row epc-part-search-layout<?php echo !empty($epc_chpu_direct_pricing) ? ' epc-chpu-direct-part-search' : ''; ?><?php echo !empty($epc_brand_picker_mode) ? ' epc-brand-picker-mode' : ''; ?>">
<div id="filter_div" class="<?=$filter_div_class;?>">
	<div class="panel panel-primary">
		<div class="panel-heading" style="position:relative;">
			<a id="filter_div_a_text" href="javascript:void(0);" onclick="show_filter_clicked();"><?=$filter_div_a_text;?></a>
		</div>
		<div id="filter_div_style_body" style="position:relative;<?=$filter_div_style_body;?>" class="panel-body">
			
			
			<?php
				//Определяем формат вывода таблицы
				if($DP_Config->products_table_mode == 0)//На выбор покупателя
				{
					//Покупатель ранее ставил куки
					if( !empty($_COOKIE["products_table_mode"]) )
					{
						$table_mode = $_COOKIE["products_table_mode"];
					}
					else//Покупатель еще не ставил куки - отображаем по умолчанию
					{
						$table_mode = 1;
					}
					//Отображаем возможность для настройки покупателем
					$products_table_mode_query = $db_link->prepare('SELECT `options` FROM `config_items` WHERE `name` = ?;');
					$products_table_mode_query->execute( array('products_table_mode') );
					$products_table_mode_record = $products_table_mode_query->fetch();
					$modes = json_decode($products_table_mode_record["options"], true);
					?>
						<div class="input-group" style="width:100%;">
							<select id="products_table_mode_select" onchange="on_products_table_mode_selected();" class="form-control" style="max-height:34px; width:100%;">
							<?php
							for($i=0; $i < count($modes); $i++)
							{
								if($modes[$i]["value"] == 0)continue;//Этот пункт "На выбор покупателя" - пропускаем
								
								?>
								<option value="<?php echo $modes[$i]["value"]; ?>"><?php echo translate_str_by_id($modes[$i]["caption"]); ?></option>
								<?php
							}
							?>
							</select>
						</div>
						<script>
						//Ставим текущий способ отображения:
						document.getElementById("products_table_mode_select").value = '<?php echo $table_mode; ?>';
						
						//Обрабобка селектора
						function on_products_table_mode_selected()
						{
							//Выбанный способ отображения
							var products_table_mode = document.getElementById("products_table_mode_select").value;
							
							//Устанавливаем cookie (на полгода)
							var date = new Date(new Date().getTime() + 15552000 * 1000);
							document.cookie = "products_table_mode="+products_table_mode+"; path=/; expires=" + date.toUTCString();
							
							//Обновляем страницу
							location.reload();
						}
						</script>
					<?php
				}
				else//Указано менеджером
				{
					$table_mode = $DP_Config->products_table_mode;
					
					// Если отображается поиск выравниваем блоки
					if($initial_position_search == 1){
						//Отображаем возможность для настройки покупателем
						$products_table_mode_query = $db_link->prepare('SELECT `options` FROM `config_items` WHERE `name` = ?;');
						$products_table_mode_query->execute( array('products_table_mode') );
						$products_table_mode_record = $products_table_mode_query->fetch();
						$modes = json_decode($products_table_mode_record["options"], true);
						// Для того что бы блок был такой же по высоте что и поиска
					?>
						<div class="input-group" style="width:100%;">
							<?php
								for($i=0; $i < count($modes); $i++)
								{
									if($modes[$i]["value"] != $table_mode)continue;
									?>
									<input disabled style="width:100%; background:#fff;" title="<?php echo translate_str_by_id(4302); ?>" value="<?php echo $modes[$i]["caption"]; ?>" type="text" class="form-control"/>
									<?php
								}
							?>
						</div>
					<?php
					}
				}
			?>
			
			<style>hr{margin:15px 0px;}</style>
			<div id="filter_position"></div>
		</div>
		<div id="footer_filter_reset"></div>
		<div id="footer-filter"></div>
	</div>
</div>

<div class="epc-part-search-main <?php echo $epc_part_search_main_class; ?>">
<?php if (!empty($epc_brand_picker_mode)) { ?>
<div class="epc-brand-picker-top">
	<div class="epc-brand-picker-top__head">
		<span class="epc-brand-picker-top__eyebrow"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(4176); ?></span>
		<h2 class="epc-brand-picker-top__title"><?php echo htmlspecialchars($epc_result_article, ENT_QUOTES, 'UTF-8'); ?></h2>
		<p class="epc-brand-picker-top__hint">Several manufacturers use this part number. Choose a brand, then open warehouse prices and stock.</p>
	</div>
	<div id="work_area" class="epc-brand-picker-work-area" align="center">
		<?php
		$epc_picker_ssr_html = '';
		if (!empty($epc_initial_price_bunch['Products']) && is_array($epc_initial_price_bunch['Products'])) {
			$epc_picker_ssr_html = epc_chpu_ssr_warehouse_table_html(
				$epc_initial_price_bunch['Products'],
				isset($currency_indicator) ? $currency_indicator : ''
			);
		}
		?>
		<div id="processing_indicator"<?php if ($epc_picker_ssr_html !== '') { ?> style="display:none"<?php } ?>>
			<?php if ($epc_picker_ssr_html === '') { ?>
			<p><?php echo translate_str_by_id(4314); ?>...</p><img src="/content/files/images/ajax-loader-transparent.gif" alt="" />
			<?php } ?>
		</div>
		<div id="products_area" class="epc-part-search-results" role="region" aria-label="Part search results"><?php
			echo $epc_picker_ssr_html;
		?></div>
	</div>
</div>
<?php } ?>
<?php
if($initial_position_search == 1 && empty($epc_brand_picker_mode) && empty($epc_chpu_direct_pricing)){
	$value_for_input_search = str_replace('"','',$value_for_input_search);
?>
<div class="hidden-xs hidden-sm search_limo" style="margin-bottom:12px;">
	<div class="panel panel-primary">
		<div class="panel-heading"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(4176); ?></div>
		<div style="position:relative;" class="panel-body">
			<form role="form" action="<?php echo $multilang_params['lang_href']; ?>/shop/part_search" method="GET">
				<div class="input-group">
					<input value="<?php echo $value_for_input_search; ?>" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4176); ?>" name="article" />
					<span class="input-group-btn">
						<button class="btn btn-ar btn-primary" type="submit"><?php echo translate_str_by_id(2763); ?></button>
					</span>
				</div>
			</form>
		</div>
	</div>
</div>
<?php
}
?>


<!----- Открытие блока проценки ------>
<div id="procenka_div" class="<?php echo $procenka_div_class; ?>">












<?php
/* ************************************* */
/* ********    ФИЛЬТР ПОЗИЦИЙ    ******* */
/* ************************************* */

// Warehouse labels for filters / result chips (prefer short_name, fall back to name)
$storages_query = $db_link->prepare('SELECT `id`, `interface_type`, `name`, `short_name`, `bg_line_color` FROM `shop_storages`;');
$storages_query->execute();
$all_storages = array();
$bg_line_color_Docpart_Treelax = 0;
while( $storage = $storages_query->fetch() )
{
	$whLabel = trim((string) ($storage['short_name'] ?? ''));
	if ($whLabel === '') {
		$whLabel = trim((string) ($storage['name'] ?? ''));
	}
	$all_storages[$storage['id']] = $whLabel;
	if($storage['interface_type'] == 6){
		$bg_line_color_Docpart_Treelax = $storage['bg_line_color'];
	}
}
// Выбираем дополнительную информацию по складам
$storages_query = $db_link->prepare('SELECT * FROM `shop_storages`;');
$storages_query->execute();
$all_storages_info = array();
while( $storage = $storages_query->fetch() )
{
	if($storage['interface_type'] == 1){
		$storage['bg_line_color'] = $bg_line_color_Docpart_Treelax;
	}
	$whLabel = trim((string) ($storage['short_name'] ?? ''));
	if ($whLabel === '') {
		$whLabel = trim((string) ($storage['name'] ?? ''));
	}
	$all_storages_info[$storage['id']] = array(
		'name' => $whLabel,
		'short_name' => (string) ($storage['short_name'] ?? ''),
		'full_name' => (string) ($storage['name'] ?? ''),
		'bg_line_color' => $storage['bg_line_color'],
	);
}
?>
<script>





// Флаг будет сообщать о том что произошла первоначальная загрузка страницы
var flag_first_loading = true;

// Все склады
var all_storages = JSON.parse('<?php echo json_encode($all_storages); ?>');
var all_storages_info = JSON.parse('<?php echo json_encode($all_storages_info); ?>');
document.addEventListener('DOMContentLoaded', function(){
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing){
		if(typeof epcEnsureChpuFilterVisible === 'function'){
			epcEnsureChpuFilterVisible();
		}
		if(typeof search_object !== 'undefined' && search_object && search_object.article && typeof epcFetchCrossData === 'function'){
			epcFetchCrossData(search_object.article);
		}
		var searchLayout = document.querySelector('.epc-part-search-layout');
		if(searchLayout && typeof searchLayout.scrollIntoView === 'function'){
			searchLayout.scrollIntoView({behavior: 'smooth', block: 'start'});
		}
	}
	if(typeof epcSyncPartSearchLayout === 'function'){
		epcSyncPartSearchLayout();
	}
	if(typeof epcRemoveWarehouseSidebarBlock === 'function'){
		epcRemoveWarehouseSidebarBlock();
	}
});

var this_filter = '';// Какой именно фильтр выбран

var sam_price = 0;// Самые дешевые
var sam_time = 0;// Самые быстрые поставки
var sam_price_time = '';//Какая из кнопок была выбрана первой sam_price или sam_time

// Бренды
var arr_manufacturers =  new Array();
// Найденные бренды после фильтрации
var arr_manufacturers_posle_filter =  new Array();

// Склады
var arr_storages = new Array();
var arr_storages_posle_filter =  new Array();
// Цвета складов
var arr_storages_color =  new Array();

// Свойства фильтра
var filter =  new Array();

var list_brend_show = false;// Флаг - был ли открыт список производителей перед обновлением фильтра
var list_storages_show = false;// Флаг - был ли открыт список складов перед обновлением фильтра


// Цена
filter['price_blok'] = new Object;
filter['price_blok'].show = 1;// включен или нет
filter['price_blok'].caption = '<?php echo translate_str_by_id(4303); ?>';
filter['price_blok'].property_type_id = 2;
filter['price_blok'].property_id = 'price';
filter['price_blok'].min_value = undefined;
filter['price_blok'].max_value = undefined;

// Срок
filter['time_to_exe_blok'] = new Object;
filter['time_to_exe_blok'].show = 1;
filter['time_to_exe_blok'].caption = '<?php echo translate_str_by_id(3433); ?>';
filter['time_to_exe_blok'].property_type_id = 2;
filter['time_to_exe_blok'].property_id = 'time_to_exe';
filter['time_to_exe_blok'].min_value = undefined;
filter['time_to_exe_blok'].max_value = undefined;

// Наличие
filter['exist_blok'] = new Object;
filter['exist_blok'].show = 1;
filter['exist_blok'].caption = '<?php echo translate_str_by_id(4304); ?>';
filter['exist_blok'].property_type_id = 2;
filter['exist_blok'].property_id = 'exist';
filter['exist_blok'].min_value = undefined;
filter['exist_blok'].max_value = undefined;

// Бренды
filter['manufacturer_blok'] = new Object;
filter['manufacturer_blok'].show = 1;
filter['manufacturer_blok'].caption = '<?php echo translate_str_by_id(2070); ?>';
filter['manufacturer_blok'].property_id = 'manufacturer';
filter['manufacturer_blok'].property_type_id = 5;
filter['manufacturer_blok'].list_type = 1;
filter['manufacturer_blok'].list_options = new Array;
filter['manufacturer_blok'].manufacturer_in_filter = new Array;

// Склады
filter['storages_blok'] = new Object;
filter['storages_blok'].show = <?php echo !empty($epc_chpu_direct_pricing) ? '0' : '1'; ?>;
filter['storages_blok'].caption = '<?php echo translate_str_by_id(4305); ?>';
filter['storages_blok'].property_id = 'storages';
filter['storages_blok'].property_type_id = 5;
filter['storages_blok'].list_type = 1;
filter['storages_blok'].list_options = new Array;
filter['storages_blok'].storages_in_filter = new Array;




/*
var flag_search = new Array();
	flag_search.push('Искомый артикул');
	flag_search.push('Аналоги');
*/
	
	
	
	
var start_page_Required = 0;// Количество отображенных строк (групп) позиций Запрошенный артикул
var start_page_SearchName = 0;// Количество отображенных строк (групп) позиций Найденных по наименованию
var start_page_Quick_Analogs = 0;// Количество отображенных строк (групп) позиций Быстрые аналоги
var start_page_Analogs = 0;// Количество отображенных строк (групп) позиций Аналоги
var start_page_PossibleReplacement = 0;// Количество отображенных строк (групп) позиций PossibleReplacement
var start_page_Spare_Box = 0;// Количество отображенных строк (групп) позиций Spare_Box

var cnt_on_page = <?php echo $cnt_on_page_settings; ?>;//Сколько прибавлять позиций по кнопке "Паказать еще"
//Установка cookie для ограничения количества отображаемых элементов
function set_cnt_on_page_settings(cnt)
{
    //Устанавливаем cookie (на полгода)
    var date = new Date(new Date().getTime() + 15552000 * 1000);
    document.cookie = "cnt_on_page_settings="+cnt+"; path=/; expires=" + date.toUTCString();
	
	cnt_on_page = cnt;
	start_page_Required = 0;
	start_page_SearchName = 0;
	start_page_Quick_Analogs = 0;
	start_page_Analogs = 0;
	start_page_PossibleReplacement = 0;
	start_page_Spare_Box = 0;
	resultReview();
}
// Отображение следующей страницы с позиций
function next_page(btn){
	// Увеличиваем счетчик возможного количества отображенных позиций
	switch(btn){
		case 'Required' :
			start_page_Required += cnt_on_page;
		break;
		case 'SearchName' :
			start_page_SearchName += cnt_on_page;
		break;
		case 'Quick_Analogs' :
			start_page_Quick_Analogs += cnt_on_page;
		break;
		case 'Analogs' :
			start_page_Analogs += cnt_on_page;
		case 'PossibleReplacement' :
			start_page_PossibleReplacement += cnt_on_page;
		break;
		case 'Spare_Box' :
			start_page_Spare_Box += cnt_on_page;
		break;
	}
	resultReview();
}	
// Функция сортировки
function sortFunction(a, b){
  if(a < b)return -1
  if(a > b)return 1
  return 0
}
// Функция сортировки списка Производителей
function sortFunctionProductsManufacturers(a, b){
  if(a.manufacturer_show < b.manufacturer_show)return -1
  if(a.manufacturer_show > b.manufacturer_show)return 1
  return 0
}
var show_all_position_flag = false;
// Функция раскрытия всех групп
function show_all_position(){
	for(var i = 0; i < wrap_blocks_index.length; i++){
		show_hide_block(i, true);
	}
	show_all_position_flag = !show_all_position_flag;
}
	
//Показать виджеты свойств
function showPropertiesWidgets()
{
	<?php
	
	$show_all_group_html = '';
	if($table_mode != 2){
		$show_all_group_html = '
		<tr> 
			<td>
				<input id="show_all_position" type="checkbox" style="width:20px; height:20px;" onclick="show_all_position()"/>
			</td>
			<td style="padding-left:5px; padding-top:2px;">
				<label for="show_all_position">'.translate_str_by_id(4306).'</label>
			</td>
		</tr>';
	}
	
	
	$btn_html = '
	
	<div style="margin:15px 0px 0px 0px;">
		<table style="width:100%; text-align:center;">
		<tr>
			<td>
				<a title="'.translate_str_by_id(4307).'" style="background:none; color:#999; border-color:#ccc;" class="btn btn-sm btn-danger" href="javascript:void(0);" onclick="set_cnt_on_page_settings(10);">10</a>
			</td>
			<td>
				<a title="'.translate_str_by_id(4307).'" style="background:none; color:#999; border-color:#ccc;" class="btn btn-sm btn-danger" href="javascript:void(0);" onclick="set_cnt_on_page_settings(20);">20</a>
			</td>
			<td>
				<a title="'.translate_str_by_id(4307).'" style="background:none; color:#999; border-color:#ccc;" class="btn btn-sm btn-danger" href="javascript:void(0);" onclick="set_cnt_on_page_settings(50);">50</a>
			</td>
		</tr>
		</table>
	</div>
	
	<hr/>
	
	<div id="reset_box"></div>
	
	<div>
		<table>
			<tr>
				<td>
					<input style="width:20px; height:20px;" id="min_price_in_group" type="checkbox" onclick="in_check();"/>
				</td>
				<td style="padding-left:5px;">
					<label for="min_price_in_group">'.translate_str_by_id(4308).'</label>
				</td>
			</tr>
			<tr>
				<td>
					<input style="width:20px; height:20px;" id="min_time_in_group" type="checkbox" onclick="in_check();"/>
				</td>
				<td style="padding-left:5px;">
					<label for="min_time_in_group">'.translate_str_by_id(4309).'</label>
				</td>
			</tr>
			'. $show_all_group_html .'
		</table>
	</div>
	
	<hr/>
	
	';
	
	$btn_html = str_replace("\n",'',$btn_html);
	$btn_html = str_replace("\r",'',$btn_html);
	$btn_html = str_replace("\t",'',$btn_html);
	?>
	
	var filter_html = '';
	
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		if(typeof epcApplyChpuActiveFilterState === 'function')
		{
			epcApplyChpuActiveFilterState();
		}
		if(window.epcChpuFilterUiPreserve)
		{
			var preserveBeforeBuild = window.epcChpuFilterUiPreserve;
			for(var preserveRangeKey in preserveBeforeBuild.ranges)
			{
				if(preserveBeforeBuild.ranges.hasOwnProperty(preserveRangeKey) && filter[preserveRangeKey])
				{
					var preservedRange = preserveBeforeBuild.ranges[preserveRangeKey];
					filter[preserveRangeKey].min_need = (preservedRange.min_need !== undefined) ? preservedRange.min_need : preservedRange.min;
					filter[preserveRangeKey].max_need = (preservedRange.max_need !== undefined) ? preservedRange.max_need : preservedRange.max;
				}
			}
			if(filter['manufacturer_blok'] && filter['manufacturer_blok'].list_options)
			{
				for(var preserveManufacturerIndex = 0; preserveManufacturerIndex < filter['manufacturer_blok'].list_options.length; preserveManufacturerIndex++)
				{
					var preserveManufacturerOption = filter['manufacturer_blok'].list_options[preserveManufacturerIndex];
					if(preserveManufacturerOption && preserveManufacturerOption.text && preserveBeforeBuild.manufacturers.hasOwnProperty(preserveManufacturerOption.text))
					{
						preserveManufacturerOption.value = preserveBeforeBuild.manufacturers[preserveManufacturerOption.text];
					}
				}
			}
		}
	}
	
	for (filter_block in filter){

		filter_block = filter[filter_block];

		var property_id = filter_block.property_id;
		var property_type_id = filter_block.property_type_id;
		if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && property_id === 'storages'){
			continue;
		}
		if ((property_id == "manufacturer" && <?php echo (int)$DP_Config->view_manufacturer_filter?> != 1) || 
		(property_id == "storages" && <?php echo (int)$DP_Config->view_storage_filter?> != 1) 
		|| (property_type_id == 2 && <?php echo (int)$DP_Config->show_range_blocks?> != 1) ) {
			continue;
			//filter_block.show = 0;
		}

		if(filter_block.show !== 1){
			filter_html += '<div class="one_property" style="display:none;"';
		}else{
			filter_html += '<div class="one_property"';
		}
		if(property_id === 'storages')
		{
			filter_html += ' data-epc-filter="storages"';
		}
		filter_html += '>';
		filter_html += '<strong>'+ filter_block.caption +'</strong><br/>';
        
		switch(property_type_id)
        {
            case 1:
            case 2:
			
                filter_html += '<div class="slider_ranges">';
                    filter_html += '<input type="text" onkeyup="return proverka_numeric(this);" onchange="onchange_range_min(\''+ property_id +'\');" id="range_min_'+ property_id +'"  />';
                    filter_html += ' — ';
                    filter_html += '<input type="text" onkeyup="return proverka_numeric(this);" onchange="onchange_range_max(\''+ property_id +'\');" id="range_max_'+ property_id +'"  />';
                    filter_html += '<div class="productsCountPopup" id="productsCountPopup_' +property_id +'"></div>';
                filter_html += '</div>';
                
                filter_html += '<div class="slider_container">';
                    filter_html += '<div id="slider-range_'+ property_id +'">';
                    filter_html += '</div>';
                filter_html += '</div>';
				
                break;
            case 5:
                var printed = 0;//Считаем количество выведенных опций данного списка
				var start_hide = 0;//Флаг "Начали скрывать остальные опции"
                filter_html += "<div class=\"list_div\">";
                //Выводим все пункты списка
                for(var l=0; l < filter_block.list_options.length; l++)
                {
					var in_disabled = '';
					var in_disabled_style = '';
					if(property_id == 'manufacturer'){
						if(arr_manufacturers_posle_filter.length > 0){
							if(arr_manufacturers_posle_filter.indexOf(filter_block.list_options[l].search) === -1){
								in_disabled = ' disabled ';
								in_disabled_style = 'color:#ccc; ';
							}
						}
					}
				
					if(property_id == 'storages'){
						if(arr_storages_posle_filter.length > 0){
							if(arr_storages_posle_filter.indexOf(filter_block.list_options[l].search) === -1){
								in_disabled = ' disabled ';
								in_disabled_style = 'color:#ccc; ';
							}
						}
					}
					
					
					
					//Скрываем те опции, в которых отсутствуют товары
                    var display_none = "";
                    if(filter_block.list_options[l].match_count === 0)
                    {
                        display_none = " display:none;";
                    }
                    else//Считаем количество выведеных опций
                    {
                        printed++;//Эта опция будет выведена
                    }
                    
                    var option_html = "";//HTML для данной опции
                    option_html += "<div style=\""+in_disabled_style+display_none+"\">";
					
					
					if(filter_block.list_options[l].value){
						in_checked = 'checked';
						in_disabled = '';
					}else{
						in_checked = '';
					}
					
					
					
					
					option_html += "<input "+ in_checked + in_disabled +" type=\"checkbox\" id=\"list_"+property_id+"_"+filter_block.list_options[l].id+"\" class=\"css-checkbox\" onchange=\"setProductsCountPopupId('productsCountPopup_"+property_id+"_"+filter_block.list_options[l].id+"'); productsCountRequest('"+property_id+"');\" />";
                    
                    option_html += "<label style=\""+in_disabled_style+"\" for=\"list_"+property_id+"_"+filter_block.list_options[l].id+"\" class=\"css-label\">"+filter_block.list_options[l].text+"</label>";
                    option_html += "<div class=\"productsCountPopup\" id=\"productsCountPopup_"+property_id+"_"+filter_block.list_options[l].id+"\"></div>";
                    option_html += "</div>";
                    
                    
                    
                    if(printed == 6 && start_hide == 0)//До этого было выведено 5. Эта шестая - начинаем скрывать
                    {
                        filter_html += "<div state=\"hidden\" style=\"display:none\" id=\"other_list_options_"+property_id+"\">";
						start_hide = 1;//Флаг - начали скрывать остальные опции
                    }
                    
                    filter_html += option_html;
                    
                    //Если выведенных опций списка больше 5 и это последняя опция - выводим закрывающий div
                    if(l == filter_block.list_options.length -1 && printed > 5)
                    {
                        filter_html += "</div>";
                    }
                }//for(l)
                if(printed > 5)//Если количество элементов в списке больше 5, то выводим кнопку для открытия/закрытия списка
                {
                    
                    filter_html += "<div class=\"show_hidden_div\" style=\"text-align:center\">";
                        filter_html += "<a class=\"show_hidden_a\" id=\"show_hidden_a_"+property_id+"\" href=\"javascript:void(0);\" onclick=\"other_list_options_handle('"+property_id+"');\"><?php echo translate_str_by_id(4130); ?></a>";
                    filter_html += "</div>";
                    
                    //$javascript_for_print_after .= "\nother_list_options_handle($property_id);\n";//Делаем вызов функции для скрытия блока
                }
                
                filter_html += "</div>";
                break;
        }
		
		
		filter_html += "</div>";//Добавляем HTML в блок свойств

		//if(filter_block.show == 1)
		if(property_id != 'storages')
        {
            filter_html += "<hr/>";
        }
		
		 filter_html += '</div>';
		
	}
	
	document.getElementById("filter_position").innerHTML = '<?=$btn_html;?>' + filter_html;
	if(typeof epcRemoveWarehouseSidebarBlock === 'function'){
		epcRemoveWarehouseSidebarBlock();
	}
	
	

	
	if(this_filter != ''){
		reset_html = "<div style=\"text-align: center; padding: 10px 0px 20px;\"><a class=\"btn btn-ar btn-primary\" style=\"cursor:pointer;\" onclick=\"reset_filter();\"><?php echo translate_str_by_id(4310); ?></a></div>";
		reset_html_2 = '<hr/>' + reset_html;
		if(sam_price_time != ''){
			if(sam_price == 1){
				document.getElementById('min_price_in_group').checked = true;
			}
			if(sam_time == 1){
				document.getElementById('min_time_in_group').checked = true;
			}
		}
		
	}else{
		reset_html = '';
		reset_html_2 = '';
	}
	
	document.getElementById('reset_box').innerHTML = reset_html;
	document.getElementById('footer_filter_reset').innerHTML = reset_html_2;
	document.getElementById("footer-filter").innerHTML = '<div class="panel-heading" style="position:relative;"><a href="javascript:void(0);" onclick="show_filter_clicked();"><i class="fa fa-arrow-circle-up" aria-hidden="true"></i> <?php echo translate_str_by_id(4300); ?></a></div>';
	
    //Инициализировать слайдеры для типов int и float
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && window.epcChpuFilterUiPreserve)
	{
		var preserve = window.epcChpuFilterUiPreserve;
		for(var preserveKey in preserve.ranges)
		{
			if(preserve.ranges.hasOwnProperty(preserveKey) && filter[preserveKey])
			{
				var preservedRangeValues = preserve.ranges[preserveKey];
				filter[preserveKey].min_need = (preservedRangeValues.min_need !== undefined) ? preservedRangeValues.min_need : preservedRangeValues.min;
				filter[preserveKey].max_need = (preservedRangeValues.max_need !== undefined) ? preservedRangeValues.max_need : preservedRangeValues.max;
			}
		}
		if(filter['manufacturer_blok'] && filter['manufacturer_blok'].list_options)
		{
			for(var li = 0; li < filter['manufacturer_blok'].list_options.length; li++)
			{
				var listOpt = filter['manufacturer_blok'].list_options[li];
				if(listOpt && listOpt.text && preserve.manufacturers.hasOwnProperty(listOpt.text))
				{
					listOpt.value = preserve.manufacturers[listOpt.text];
				}
			}
		}
	}
	for (filter_block in filter){
		

		filter_block = filter[filter_block];
		
		if(filter_block.property_type_id === 1 || filter_block.property_type_id === 2){
			sliderIntFloatInit(filter_block);
		}
    }
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing && typeof epcSyncChpuManufacturerFilterFromListOptions === 'function')
	{
		epcSyncChpuManufacturerFilterFromListOptions();
    }
    
	// если первоначальная загрузка страницы
	if(flag_first_loading){
		<?php if (!empty($epc_chpu_direct_pricing)) { ?>
		if(typeof epcEnsureChpuFilterVisible === 'function'){
			epcEnsureChpuFilterVisible();
		}
		<?php } else { ?>
		show_filter_clicked();
		<?php } ?>
		flag_first_loading = false;
	}
	
	// Раскрываем список брендов если он был раскрыт перед обновлением
	if(list_brend_show){
		other_list_options_handle('manufacturer');
	}
	// Раскрываем список складов если он был раскрыт перед обновлением
	if(list_storages_show){
		other_list_options_handle('storages');
	}
	
	if(show_all_position_flag){
		document.getElementById('show_all_position').checked = true;
	}
	
	// Делаем фон диапазона в текущий цвет сайта
	if($('#slider-range_price').children(".ui-slider-range") != undefined){
		$('#slider-range_price').children(".ui-slider-range").addClass('btn-ar btn-primary');
		$('#slider-range_time_to_exe').children(".ui-slider-range").addClass('btn-ar btn-primary');
		$('#slider-range_exist').children(".ui-slider-range").addClass('btn-ar btn-primary');
	}
}//~function showPropertiesWidgets()

// Функция определяет выбраны ли фильтры по самой низкой цене и сроку
function in_check(){
	
	if(document.getElementById("min_price_in_group").checked){
		sam_price = 1;
		if(sam_price_time == ''){
			sam_price_time = 'sam_price';
		}
	}else{
		sam_price = 0;
		if(sam_price_time == 'sam_price'){
			sam_price_time = '';
		}
	}
	
	if(document.getElementById("min_time_in_group").checked){
		sam_time = 1;
		if(sam_price_time == ''){
			sam_price_time = 'sam_time';
		}
	}else{
		sam_time = 0;
		if(sam_price_time == 'sam_time'){
			sam_price_time = '';
			if(sam_price == 1){
				sam_price_time = 'sam_price';
			}
		}
	}
	
	productsCountRequest('sam_price_time');
}

//Функция инициализации слайдера
function sliderIntFloatInit(property)
{
	if(property.min_value === undefined || property.max_value === undefined)
	{
		return;
	}
    var this_znachenie_min = Math.floor(property.min_value);
	var this_znachenie_max = Math.ceil(property.max_value);
	if(isNaN(this_znachenie_min) || isNaN(this_znachenie_max))
	{
		return;
	}

	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		if(property.min_need !== undefined && property.max_need !== undefined)
		{
			this_znachenie_min = Math.floor(property.min_need);
			this_znachenie_max = Math.ceil(property.max_need);
		}
	}
	else if(this_filter == property.property_id + '_blok'){
		
		this_znachenie_min = property.min_need;
		this_znachenie_max = property.max_need;

		filter[this_filter].old_min_need = this_znachenie_min;
		filter[this_filter].old_max_need = this_znachenie_max;
		
	}
	
		//Создаем слайдер
		jQuery( "#slider-range_"+property.property_id ).slider({
			range: true,
			min: Math.floor(property.min_value),
			max: Math.ceil(property.max_value),
			values: [this_znachenie_min, this_znachenie_max],
			slide: function( event, ui ) {//Событие - передвижение
				$( "#range_min_"+property.property_id ).val( ui.values[ 0 ]);
				$( "#range_max_"+property.property_id ).val( ui.values[ 1 ] );
			},
			stop: function(){//Событие - отпустили слайдер
				
				productsCountRequest(property.property_id);//Запрос количества товаров
			}
		});
		
		//Выставляем текущие крайние значение в поля ввода
		$( "#range_min_"+property.property_id ).val( jQuery( "#slider-range_"+property.property_id ).slider( "values", 0 ) );
		$( "#range_max_"+property.property_id ).val( jQuery( "#slider-range_"+property.property_id ).slider( "values", 1 ) );
}

// Изменяем слайдер при редактировании инпутов - min
function onchange_range_min(property){
	
	property = filter[property+'_blok'];

	var value1=jQuery("#range_min_"+property.property_id).val();
	var value2=jQuery("#range_max_"+property.property_id).val();
	
	if(parseInt(value1) > parseInt(value2)){
		value1 = value2;
		jQuery("#range_min_"+property.property_id).val(value1);
	}
	jQuery("#slider-range_"+property.property_id).slider("values",0,value1);
	productsCountRequest(property.property_id);
}
// Изменяем слайдер при редактировании инпутов - max
function onchange_range_max(property){
	
	property = filter[property+'_blok'];

	var value1=jQuery("#range_min_"+property.property_id).val();
	var value2=jQuery("#range_max_"+property.property_id).val();
	
	if(parseInt(value2) < parseInt(value1)){
		value2 = value1;
		jQuery("#range_max_"+property.property_id).val(value2);
	}
	jQuery("#slider-range_"+property.property_id).slider("values",1,value2);
	productsCountRequest(property.property_id);
}
// Проверка инпутов слайдера на ввод числа
function proverka_numeric(input){
	input.value = input.value.replace(/[^\d,]/g, '');
}

//Функция предназначена для скрытия/открытия опций списка, если их больше ограниченного числа
function other_list_options_handle(property_id)
{
	//Реверсируем значение атрибута class
    var other_list_options_div = document.getElementById("other_list_options_"+property_id);
	
    if(other_list_options_div.getAttribute("state") == "hidden")
    {
        // Открыли список
		if(property_id == 'manufacturer'){ list_brend_show = true; }
		if(property_id == 'storages'){ list_storages_show = true; }
		
		other_list_options_div.setAttribute("state", "shown");
        jQuery('#other_list_options_'+property_id).fadeIn(200, 'swing', function(){});
        document.getElementById("show_hidden_a_"+property_id).innerHTML = "<?php echo translate_str_by_id(4131); ?>";
    }
    else
    {
		// Скрыли список
		if(property_id == 'manufacturer'){ list_brend_show = false; }
		if(property_id == 'storages'){ list_storages_show = false; }

		other_list_options_div.setAttribute("state", "hidden");
        jQuery('#other_list_options_'+property_id).fadeOut(200, 'swing', function(){});
        document.getElementById("show_hidden_a_"+property_id).innerHTML = "<?php echo translate_str_by_id(4130); ?>";
    }
}

function setProductsCountPopupId(next_id)
{
    //alert(next_id);
	// Функция используется в разных участках кода. Лучше ее не убирать.
}

//Инициализация значений свойств
function initProperiesValues()
{
    
	for (i in filter){

		switch( parseInt(filter[i].property_type_id) )
        {
            case 1:
            case 2:
				var sliderNode = jQuery( "#slider-range_"+filter[i].property_id );
				if(sliderNode.length && typeof sliderNode.slider === 'function')
				{
					try
					{
						filter[i].min_need = sliderNode.slider( "values", 0 );
						filter[i].max_need = sliderNode.slider( "values", 1 );
					}
					catch(e) {}
				}
                break;
            case 4:
                filter[i].true_checked = document.getElementById("checkbox_true_"+filter[i].property_id).checked;
                filter[i].false_checked = document.getElementById("checkbox_false_"+filter[i].property_id).checked;
                break;
            case 5:
                
				
				
				for(var o=0; o < filter[i].list_options.length; o++)
                {
					filter[i].list_options[o].value = document.getElementById("list_"+filter[i].property_id+"_"+filter[i].list_options[o].id).checked;
                }
				
                break;
        }
		
	}
}

//Запрос количества продуктов, соответствующих указанным требованиям
function productsCountRequest(id)
{
	initProperiesValues();//Инициализируем список свойств выставленными значениями
	
	// Определяем выбранные фильтры
	
	// Бренды
	var manufacturer_in_filter = new Array();
	for(var k = 0; k < filter['manufacturer_blok'].list_options.length; k++){
		if(filter['manufacturer_blok'].list_options[k].value === true){
			manufacturer_in_filter.push(arr_manufacturers[filter['manufacturer_blok'].list_options[k].id]);
		}
	}
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		// Always respect checkbox state; do not reset to all brands when changing price/qty sliders.
		filter['manufacturer_blok'].manufacturer_in_filter = manufacturer_in_filter.slice(0);
		if(typeof epcStoreChpuActiveFilterState === 'function')
		{
			epcStoreChpuActiveFilterState();
		}
	}
	else if(manufacturer_in_filter.length > 0){
		filter['manufacturer_blok'].manufacturer_in_filter = manufacturer_in_filter;
	}else{
		filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers;
	}
	
	
	
	// Склады
	var storages_in_filter = new Array();
	for(var k = 0; k < filter['storages_blok'].list_options.length; k++){
		if(filter['storages_blok'].list_options[k].value === true){
			storages_in_filter.push(arr_storages[filter['storages_blok'].list_options[k].id]);
		}
	}
	if(storages_in_filter.length > 0){
		filter['storages_blok'].storages_in_filter = storages_in_filter;
	}else{
		filter['storages_blok'].storages_in_filter = arr_storages;
	}
	
	
	
	
	this_filter = id + '_blok';
	
	// Сбрасываем количество отображаемых элементов после применения фильтра
	start_page_Required = 0;
	start_page_SearchName = 0;
	start_page_Quick_Analogs = 0;
	start_page_Analogs = 0;
	start_page_PossibleReplacement = 0;
	start_page_Spare_Box = 0;
	
	// Переотображаем проценку
	resultReview();
}

// Функция сбрасывает фильтр и обновляет таблицу проценки
function reset_filter(){
	
	// Сбрасываем текущие значения
		// Цена
			filter['price_blok'].min_value = undefined;
			filter['price_blok'].max_value = undefined;
			filter['price_blok'].old_min_need = undefined;
			filter['price_blok'].old_max_need = undefined;
		
		// Срок
			filter['time_to_exe_blok'].min_value = undefined;
			filter['time_to_exe_blok'].max_value = undefined;
			filter['time_to_exe_blok'].old_min_need = undefined;
			filter['time_to_exe_blok'].old_max_need = undefined;
		
		// Наличие
			filter['exist_blok'].min_value = undefined;
			filter['exist_blok'].max_value = undefined;
			filter['exist_blok'].old_min_need = undefined;
			filter['exist_blok'].old_max_need = undefined;
			
		filter['manufacturer_blok'].manufacturer_in_filter = arr_manufacturers;
		filter['storages_blok'].storages_in_filter = arr_storages;
			
		for(var o=0; o < filter['manufacturer_blok'].list_options.length; o++)
		{
			filter['manufacturer_blok'].list_options[o].value = false;
		}
		for(var o=0; o < filter['storages_blok'].list_options.length; o++)
		{
			filter['storages_blok'].list_options[o].value = false;
		}
	
	sam_price = 0;	
	sam_time = 0;	
	sam_price_time = '';	
	// Сбрасываем количество отображаемых элементов после применения фильтра
	start_page_Required = 0;
	start_page_SearchName = 0;
	start_page_Quick_Analogs = 0;
	start_page_Analogs = 0;
	start_page_PossibleReplacement = 0;
	start_page_Spare_Box = 0;
	this_filter = '';
	if(typeof epc_chpu_direct_pricing !== 'undefined' && epc_chpu_direct_pricing)
	{
		window.epcChpuActiveFilterState = null;
		window.epcChpuFilterUiPreserve = null;
		window.epcChpuManufacturersFilterSynced = false;
	}
	// Сворачиваем группы
	if(show_all_position_flag){
		show_all_position();
	}
	
	resultReview();
}


// Функция производит фильтрацию позиций на основе значений фильтра
function filtering_items(ProductsObjects){
	
	var tmp_arr = new Array();
	for(var p=0; p < ProductsObjects.length; p++){
		
		
		// Range filters (price / delivery / availability)
		if(this_filter != 'sam_price_time_blok')
		{
			if(typeof epcProductPassesRangeFilters === 'function')
			{
				if(!epcProductPassesRangeFilters(ProductsObjects[p]))
				{
					continue;
				}
			}
			else if(
			(this_filter != 'sam_price_time_blok') &&
				filter[this_filter] &&
			(
			filter[this_filter].min_need < filter[this_filter].old_min_need || 
			filter[this_filter].max_need > filter[this_filter].old_max_need 
			)
		){
			switch(this_filter){
				case 'price_blok' :
					if(
						(ProductsObjects[p].price*1 < filter['price_blok'].min_need) ||
						(ProductsObjects[p].price*1 > filter['price_blok'].max_need)
					){continue;}
				break;
				case 'time_to_exe_blok' :
					if(
						(ProductsObjects[p].time_to_exe < filter['time_to_exe_blok'].min_need) ||
						(ProductsObjects[p].time_to_exe > filter['time_to_exe_blok'].max_need)
					){continue;}
				break;
				case 'exist_blok' :
					if(
						(ProductsObjects[p].exist < filter['exist_blok'].min_need) ||
						(ProductsObjects[p].exist > filter['exist_blok'].max_need)
					){continue;}
				break;
			}
			}
		}
		
		// Найденные бренды после фильтрации
		if(arr_manufacturers_posle_filter.indexOf(ProductsObjects[p].manufacturer) === -1){
			arr_manufacturers_posle_filter.push(ProductsObjects[p].manufacturer);
		}
		
		
		
		// Найденные склады после фильтрации
		if(arr_storages_posle_filter.indexOf(String(ProductsObjects[p].storage_id)) === -1){
			arr_storages_posle_filter.push(String(ProductsObjects[p].storage_id));
		}
		
		// Фильтруем по бренду (with manufacturer_synonyms when available)
		if(typeof epcProductMatchesManufacturerFilter === 'function')
		{
			if(filter['manufacturer_blok'].manufacturer_in_filter.length > 0 &&
				!epcProductMatchesManufacturerFilter(ProductsObjects[p].manufacturer, filter['manufacturer_blok'].manufacturer_in_filter))
			{
				continue;
			}
		}
		else if(filter['manufacturer_blok'].manufacturer_in_filter.indexOf(ProductsObjects[p].manufacturer) === -1)
		{
			continue;
		}
		
		// Фильтруем по складу (skip when filter not initialized yet)
		if(filter['storages_blok'].storages_in_filter.length > 0 &&
			filter['storages_blok'].storages_in_filter.indexOf(ProductsObjects[p].storage_id*1) === -1){
			continue;
		}
		
		// Еспи позиция прошла фильтр
		tmp_arr.push(ProductsObjects[p]);
	}
	// Новый массив позиций после фильтрации
	ProductsObjects = tmp_arr;
	
	return ProductsObjects;
}

// Функция определяем самые быстрые и дешевые позиции в объекте
function sam_price_time_fanc(ProductsObjects){
	
	var min_price_in_group = undefined;
	var min_time_in_group  = undefined;
	
	if(sam_price_time == 'sam_price'){
		
		// Находим минимальную цену в группе
		for(var p=0; p < ProductsObjects.length; p++){
			if(min_price_in_group == undefined){
				min_price_in_group = ProductsObjects[p].price;
			}else{
				if(min_price_in_group > ProductsObjects[p].price){
					min_price_in_group = ProductsObjects[p].price;
				}
			}
		}
		
		// Фильтруем по минимальной цене в группе
		var tmp_arr = new Array();
		for(var p=0; p < ProductsObjects.length; p++){
			if(min_price_in_group < ProductsObjects[p].price){
				continue;
			}
			tmp_arr.push(ProductsObjects[p]);
		}
		ProductsObjects = tmp_arr;
		
		// Если так же выбран фильтр по минимальному сроку
		if(sam_time == 1){
			
			// Находим минимальный срок доставки в группе
			for(var p=0; p < ProductsObjects.length; p++){
				if(min_time_in_group == undefined){
					min_time_in_group = ProductsObjects[p].time_to_exe;
				}else{
					if(min_time_in_group > ProductsObjects[p].time_to_exe){
						min_time_in_group = ProductsObjects[p].time_to_exe;
					}
				}
			}
			
			// Фильтруем по минимальному сроку в группе
			var tmp_arr = new Array();
			for(var p=0; p < ProductsObjects.length; p++){
				if(min_time_in_group < ProductsObjects[p].time_to_exe){
					continue;
				}
				tmp_arr.push(ProductsObjects[p]);
			}
			ProductsObjects = tmp_arr;
		}
	}
	
	if(sam_price_time == 'sam_time'){
		
		// Находим минимальный срок доставки в группе
		for(var p=0; p < ProductsObjects.length; p++){
			if(min_time_in_group == undefined){
				min_time_in_group = ProductsObjects[p].time_to_exe;
			}else{
				if(min_time_in_group > ProductsObjects[p].time_to_exe){
					min_time_in_group = ProductsObjects[p].time_to_exe;
				}
			}
		}
		
		// Фильтруем по минимальному сроку в группе
		var tmp_arr = new Array();
		for(var p=0; p < ProductsObjects.length; p++){
			if(min_time_in_group < ProductsObjects[p].time_to_exe){
				continue;
			}
			tmp_arr.push(ProductsObjects[p]);
		}
		ProductsObjects = tmp_arr;
		
		// Если так же выбран фильтр по минимальной цене
		if(sam_price == 1){
			
			// Находим минимальную цену в группе
			for(var p=0; p < ProductsObjects.length; p++){
				if(min_price_in_group == undefined){
					min_price_in_group = ProductsObjects[p].price;
				}else{
					if(min_price_in_group > ProductsObjects[p].price){
						min_price_in_group = ProductsObjects[p].price;
					}
				}
			}
			
			// Фильтруем по минимальной цене в группе
			var tmp_arr = new Array();
			for(var p=0; p < ProductsObjects.length; p++){
				if(min_price_in_group < ProductsObjects[p].price){
					continue;
				}
				tmp_arr.push(ProductsObjects[p]);
			}
			ProductsObjects = tmp_arr;
		}
	}
	
	return ProductsObjects;
}





// Функция отделяет тысячные знаки пробелом. Используется для отображения цены
function digit(str){
    var parts = (str + '').split('.'),
        main = parts[0],
        len = main.length,
        output = '',
        i = len - 1;
    
    while(i >= 0) {
        output = main.charAt(i) + output;
        if ((len - i) % 3 === 0 && i > 0) {
            output = ' ' + output;
        }
        --i;
    }

    if (parts.length > 1) {
        output += '.' + parts[1];
    }
    return output;
}






//Функция добавления требуемого количества
function plusCountNeed(product_record_id, count, min_count)
{
	if(min_count === undefined){
		min_count = 1;
	}
	
	//Текущее количество
	var current_count_need = parseInt(document.getElementById("count_need_"+product_record_id).value) + parseInt(min_count);
	
	//Если максимальное количество на складе 0 то поставим 1
	if(count < 1){
		count = 1;
	}
	
	//Если не привышено максимальное значение то увеличиваем
	if(current_count_need <= count){
		document.getElementById("count_need_"+product_record_id).value = current_count_need;
	}else{
		alert("<?php echo translate_str_by_id(4311); ?>");
	}
}
	
//Функция вычитания требуемого количества
function minusCountNeed(product_record_id, count, min_count)
{
	if(min_count === undefined){
		min_count = 1;
	}
	
	//Текущее количество
	var current_count_need = parseInt(document.getElementById("count_need_"+product_record_id).value) - parseInt(min_count);
	
	//Если максимальное количество на складе 0 то поставим 1
	if(count < 1){
		count = 1;
	}
	
	//Если не привышено максимальное значение то увеличиваем
	if(current_count_need >= parseInt(min_count)){
		document.getElementById("count_need_"+product_record_id).value = current_count_need;
	}else{
		alert("<?php echo translate_str_by_id(4312); ?>");
	}
}
	
//Функция изменения количества при ручном вводе в поле
function onKeyUpCountNeed(product_record_id, count, count_min)
{
	if(count_min === undefined){
		count_min = 1;
	}
	
	//Текущее количество
	var current_count_need = parseInt(document.getElementById("count_need_"+product_record_id).value);
	
	//Если введено допустимое значение
	if((current_count_need <= count && current_count_need >= count_min) && ((getDecimal((current_count_need / count_min))*1) == 0))
	{
		
	}
	else//Просто исправляем обратно
	{
		alert("<?php echo translate_str_by_id(4313); ?>");
		document.getElementById("count_need_"+product_record_id).value = count_min;
	}
}

// Возвращает дробную часть
function getDecimal(num) {
	var str = "" + num;
	var zeroPos = str.indexOf(".");
	if (zeroPos == -1) return 0;
	str = str.slice(zeroPos);
	return +str;
}

// На устройствах с небольшим разрешением экрана автоматически сворачиваем фильтр при первой загрузке
if(screen.width < 991 && (typeof epc_chpu_direct_pricing === 'undefined' || !epc_chpu_direct_pricing)){
	show_filter_clicked();
}




/* ************************************* */
/* *************    END    ************* */
/* ************************************* */
</script>



















<?php
//В зависимости от режима - отображаем результат соответствующим скриптом
echo epc_wa_styles();
echo epc_wa_frontend_script($DP_Config);
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/part_search_page_".$table_mode.".php");
?>

<?php if (!empty($epc_chpu_direct_pricing)) { ?>
<div class="epc-chpu-actions-bar" id="epc-chpu-actions-bar" aria-label="Part page tools"></div>
<?php } ?>
<?php if (empty($epc_brand_picker_mode)) { ?>
<div id="work_area" align="center">
	<?php
	//В зависимости от режима работы скрипта - выбираем исходном сообщение
	if( $search_type == "no_chpu" || $search_type == "all_brands_by_article" )
	{
		$start_message = translate_str_by_id(4314)."...";
	}
	else if( $search_type == "prices_by_article_and_manufacturer" )
	{
		if( $use_selected_manufacturer || !empty($epc_chpu_direct_pricing) )
		{
			$start_message = translate_str_by_id(4294)."...";
		}
		else
		{
			$start_message = translate_str_by_id(4314)."...";
		}
	}
	?>


    <?php
	$epc_ssr_warehouse_html = '';
	if (!empty($epc_initial_price_bunch['Products']) && is_array($epc_initial_price_bunch['Products'])) {
		$epc_ssr_warehouse_html = epc_chpu_ssr_warehouse_table_html(
			$epc_initial_price_bunch['Products'],
			isset($currency_indicator) ? $currency_indicator : ''
		);
	}
	?>
    <div id="processing_indicator"<?php if ($epc_ssr_warehouse_html !== '') { ?> style="display:none"<?php } ?>>
        <?php if ($epc_ssr_warehouse_html === '') { ?>
        <p><?php echo $start_message; ?></p><img src="/content/files/images/ajax-loader-transparent.gif" />
        <?php } ?>
    </div>

    
    <div id="products_area" class="epc-part-search-results" role="region" aria-label="Part search results">
    <?php echo $epc_ssr_warehouse_html; ?>
    </div>
</div>
<?php } ?>
</div><!-- /procenka_div -->
<?php
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';
	if (function_exists('epc_seo_regional_footer_html')) {
		echo epc_seo_regional_footer_html($DP_Config);
	}
}
?>
</div><!-- /epc-part-search-main -->
</div><!-- /epc-part-search-layout -->



















<script>
function epcRunPartSearchBootstrapWhenReady(fn)
{
	if(typeof fn !== 'function')
	{
		return;
	}
	var run = function(){
		try
		{
			fn();
		}
		catch(err)
		{
			console.error('epcRunPartSearchBootstrap', err);
		}
	};
	if(typeof jQuery !== 'undefined')
	{
		jQuery(run);
		return;
	}
	var tries = 0;
	(function wait(){
		if(typeof jQuery !== 'undefined')
		{
			jQuery(run);
			return;
		}
		if(++tries > 120)
		{
			run();
			return;
		}
		window.setTimeout(wait, 50);
	})();
}
<?php
//ПРИ ЗАГРУЗКЕ СТРАНИЦЫ ПЕРЕДАЕМ ПЕРВУЮ КОМАНДУ
//Для старого варианта. И, такой же точно - ЧПУ-первый шаг
if( $search_type == "no_chpu" || !empty($epc_brand_picker_mode) )
{
	if( !empty($epc_brand_picker_mode) )
	{
	?>
	epcRunPartSearchBootstrapWhenReady(function(){
	epcBrandPickerWarehouseDone = false;
	epcBrandPickerCatalogDone = false;
	function epcBrandPickerAfterStockReady(hasStock)
	{
		if(hasStock)
		{
			epcBrandPickerWarehouseDone = true;
			epcBrandPickerCatalogDone = true;
			epcTryFinalizeBrandPicker();
			epcFetchCatalogArticleBrands();
			return;
		}
		epcFetchCatalogArticleBrands();
		<?php
		if((int)$DP_Config->is_async_search == 1)
		{
		?>
		getManufacturersListAsync();
		<?php
		}
		else
		{
		?>
		getManufacturersList();
		<?php
		}
		?>
	}
	if(typeof epcApplyInitialPriceBunch === 'function' && epcApplyInitialPriceBunch())
	{
		epcBrandPickerAfterStockReady(true);
	}
	else if(typeof epcBrandPickerFetchStockPreview === 'function')
	{
		epcBrandPickerFetchStockPreview().then(function(ok) {
			epcBrandPickerAfterStockReady(!!ok);
		});
	}
	else
	{
		epcBrandPickerAfterStockReady(false);
	}
	});
	<?php
	}
	else
	{
	?>
	epcRunPartSearchBootstrapWhenReady(function(){
	<?php
	//Первая команда: запрос списка производителей от поставщиков
	if((int)$DP_Config->is_async_search == 1)
	{
	?>
	getManufacturersListAsync();
	<?php
	}
	else
	{
	?>
	getManufacturersList();
	<?php
	}
	?>
	});
	<?php
	}
}
//ЧПУ-второй шаг
else if( $search_type == "prices_by_article_and_manufacturer" )
{
	if( !empty($epc_chpu_direct_pricing) )
	{
		$epc_direct_manufacturer = html_entity_decode($manufacturer, ENT_QUOTES | ENT_XML1, 'UTF-8');
		?>
		epcRunPartSearchBootstrapWhenReady(function(){
		ProductsManufacturers_All_Asked = true;
		SelectedManufacturer = '<?php echo str_replace(array("\\", "'"), array("\\\\", "\\'"), $epc_direct_manufacturer); ?>';
		if(typeof epcEnsureChpuFilterVisible === 'function'){
			epcEnsureChpuFilterVisible();
		}
		if(typeof epcMountChpuCrossActions === 'function'){
			epcMountChpuCrossActions();
		}
		function epcChpuEnsureStockTableVisible()
		{
			var area = document.getElementById('products_area');
			var needsHydrate = (typeof epcChpuTableNeedsActionHydration === 'function')
				? epcChpuTableNeedsActionHydration(area)
				: false;
			if(needsHydrate && typeof epcApplyInitialPriceBunch === 'function')
			{
				// Replace SSR seed with full search-result rows (qty + Add to Cart + Quote + WhatsApp + Fitment).
				epcApplyInitialPriceBunch();
				area = document.getElementById('products_area');
				needsHydrate = (typeof epcChpuTableNeedsActionHydration === 'function')
					? epcChpuTableNeedsActionHydration(area)
					: false;
			}
			var hasTable = area && area.innerHTML && area.innerHTML.indexOf('all_table_products') !== -1;
			var hasActions = area && (
				area.innerHTML.indexOf('epc-product-actions') !== -1
				|| area.innerHTML.indexOf('epc-btn-cart') !== -1
				|| area.innerHTML.indexOf('epc-btn-quote') !== -1
			);
			if(hasTable && hasActions && !needsHydrate)
			{
				if(typeof epcChpuHideProcessingIndicator === 'function')
				{
					epcChpuHideProcessingIndicator();
				}
				return true;
			}
			if(!hasTable && typeof epcApplyInitialPriceBunch === 'function' && epcApplyInitialPriceBunch())
			{
				area = document.getElementById('products_area');
				hasActions = area && (
					area.innerHTML.indexOf('epc-product-actions') !== -1
					|| area.innerHTML.indexOf('epc-btn-cart') !== -1
					|| area.innerHTML.indexOf('epc-btn-quote') !== -1
				);
				if(hasActions)
				{
					if(typeof epcChpuHideProcessingIndicator === 'function')
					{
						epcChpuHideProcessingIndicator();
					}
					return true;
				}
			}
			return false;
		}
		if(epcChpuEnsureStockTableVisible()){
			if(typeof epcFetchCrossData === 'function'){
				epcFetchCrossData(search_object.article);
			}
		}else if(typeof epcChpuStartFullPriceSearch === 'function'){
			epcChpuStartFullPriceSearch('<?php echo str_replace(array("\\", "'"), array("\\\\", "\\'"), $epc_direct_manufacturer); ?>');
		}
		// Retry paint — empty white area under Fitment/Crosses is usually a late/failed first review.
		var epcChpuPaintTries = 0;
		var epcChpuPaintTimer = setInterval(function(){
			epcChpuPaintTries++;
			if(epcChpuEnsureStockTableVisible() || epcChpuPaintTries >= 12)
			{
				clearInterval(epcChpuPaintTimer);
				if(typeof epcFetchCrossData === 'function' && epcChpuPaintTries > 1)
				{
					epcFetchCrossData(search_object.article);
				}
			}
		}, 500);
		});
		<?php
	}
	else//Начинаем с начала - с запроса производителей по артикулу, т.к. в опциях пользователя не найден выбранный производитель. При этом, после получения списка прозводителей - он будет выбран автоматически, т.к. он содержится в URL.
	{
		?>
		epcRunPartSearchBootstrapWhenReady(function(){
		<?php
		//Первая команда: запрос списка производителей от поставщиков
		if((int)$DP_Config->is_async_search == 1)
		{
		?>
		getManufacturersListAsync();
		<?php
		}
		else
		{
		?>
		getManufacturersList();
		<?php
		}
		?>
		});
		<?php
	}
}
?>
</script>




<!-------------------------------------------- Start Работа с окнами -------------------------------------------->
<div id="info_windows_area" style="display:none;">
    <div id="dialog">
    </div>
</div>
<script>
//Функция открытия окна
function openInfoWindow(title, text, type, info_object_json)
{
    var window_text = text;
    var window_title = title;
    
    
    //Для специальных окон - в зависимости от типа - формируем окно
    if(type != undefined)
    {
        var info_object = JSON.parse(info_object_json);
        
        log(info_object);
        
        switch(type)
        {
            case 1:
                window_title = "<?php echo translate_str_by_id(4315); ?>";
                window_text = "<div align=\"center\"><p style=\"color:#AAA\"><?php echo translate_str_by_id(4316); ?> "+info_object.probability+"%</p>";
                window_text += "<img src=\"/lib/TreelaxCharts/sectors.php?number=2&value0="+info_object.probability+"&value1="+(100-info_object.probability)+"&start_angle=20&size=400&inside_size=5&slope=1.1&square=0\" /></div>";
                window_text += "<div><?php echo translate_str_by_id(4317); ?>: "+info_object.time_to_exe+" <?php echo translate_str_by_id(4097); ?>. <?php echo translate_str_by_id(4318); ?>: "+info_object.time_to_exe_guaranteed+" <?php echo translate_str_by_id(4097); ?>.</div>";
                window_text += "<div><?php echo translate_str_by_id(4319); ?>: "+info_object.exist+" <?php echo translate_str_by_id(4095); ?>.</div>";
                break;
        }
    }



    //Инициализируем div диалога:
    var dialog = document.getElementById("dialog");
    dialog.innerHTML = window_text;
    $( "#dialog" ).dialog({
        title:window_title,
    });
}

</script>
<!-------------------------------------------- End Работа с окнами -------------------------------------------->




<!----- Закрытие блока проценки ------>
</div>




<?php
if($user_id > 0){
?>
<!---------------------------------------------- ГАРАЖ ---------------------------------------------->
<style>
body {
   padding: 0 !important;
}
#my_modal_box_for_garage .modal {
	z-index:99999999;
}
#my_modal_box_for_garage .modal-header {
  text-align: center;
  font-size: 14px;
  background: #fff;
  color:#000;
  border-bottom: 1px solid #999;
}
#my_modal_box_for_garage .close{
	color:#000;
}
#my_modal_box_for_garage .modal-footer {
	border-top: 1px solid #999;
	text-align: center;
}
</style>
<div id="my_modal_box_for_garage">
  <div class="modal fade" id="modal_garage" role="dialog">
    <div class="modal-dialog">
      <div class="modal-content">
        <div class="modal-header" style="padding:10px 15px;">
          <button type="button" class="close" data-dismiss="modal">&times;</button>
          <b><?php echo translate_str_by_id(4320); ?></b>
        </div>
        <div class="modal-body" style="color:#000; padding:40px 50px;">
			<div id="add_bloknot_content">
				<?php
				$query = $db_link->prepare('SELECT *, (SELECT `caption` FROM `shop_docpart_cars` WHERE `id` = `shop_docpart_garage`.`mark_id`) AS `mark` FROM `shop_docpart_garage` WHERE `user_id` = ?;');
				$query->execute( array($user_id) );
				echo '<select id="garage_auto" class="form-control">';
				echo '<option value="0">'.translate_str_by_id(2100).'</option>';
				while($car = $query->fetch())
				{
					echo '<option value="'.$car['id'].'">'. $car["mark"].' '.$car["model"].' '.$car["year"].' '.translate_str_by_id(4321).' - '.$car["caption"] .'</option>';
				}
				echo '</select>';
				?>
			</div>
			<div id="add_bloknot_msg"></div>
        </div>
        <div id="add_bloknot_btn" class="modal-footer">
			<a style="margin-bottom: 5px;" class="btn btn-ar btn-primary" onclick="add_bloknot();"><?php echo translate_str_by_id(2101); ?></a>
        </div>
      </div>
    </div>
  </div>
</div>
<script>
var id_in_bloknot = -1;// id позиции которую будем добавлять в блокнот
// Функция отображения блока добавления позиции в блокнот
function show_add_bloknot(id){
	id_in_bloknot = id;
	$("#modal_garage").modal();
}
// Функция добавления позиции в блокнот гаража
function add_bloknot(){
	if(id_in_bloknot >= 0){
		var n = document.getElementById("garage_auto").options.selectedIndex;
		var val = document.getElementById("garage_auto").options[n].value;
		var aid = id_in_bloknot;
		
		//1. По списку учетных объектов определяем, в где находится объект товара (Запрошенные/Аналоги)
		var AID_Object = Products.All[aid];
		
		////////////////////////////////////////////////////
		<?php
		if($table_mode == 1){
		?>
		
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
		
		<?php
		}else{
		?>
		
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
		
		<?php
		}
		?>
		/////////////////////////////////////////////////
		
		jQuery.ajax({
			type: "POST",
			async: false, //Запрос синхронный
			url: "/content/shop/docpart/garage/ajax_add_to_notepad.php",
			dataType: "json",//Тип возвращаемого значения
			data: "garage="+val+"&product="+encodeURIComponent(JSON.stringify(Product))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(answer)
			{
				var icon = '<i style="font-size: 30px; color: green;" class="fa fa-check"></i> ';
				if(answer.status != true){
					icon = '<i style="font-size: 30px; color: red;" class="fa fa-times"></i> ';
				}
				
				document.getElementById('add_bloknot_content').style.display = "none";
				document.getElementById('add_bloknot_btn').style.display = "none";
				document.getElementById('add_bloknot_msg').innerHTML = icon + answer.message;
				
				setTimeout(function(){
					$("#modal_garage").modal('hide');
					
				}, 1200);
				
				setTimeout(function(){
					document.getElementById('add_bloknot_content').style.display = "block";
					document.getElementById('add_bloknot_btn').style.display = "block";
					document.getElementById('add_bloknot_msg').innerHTML = '';
				}, 1500);
			},
			error: function (e, ajaxOptions, thrownError){
				alert('<?php echo translate_str_by_id(2122); ?>');
			}
		});
	}
}
</script>
<!-------------------------------------------- End ГАРАЖ -------------------------------------------->
<?php
}
?>





<!--------------------------------------- Картинки в проценке --------------------------------------->
<style>
body {
   padding: 0 !important;
}
.td_manufacturer {
	position:relative;
}
@media screen and (min-width: 768px){
	.show_picture + .show_hide_button {
		position: absolute;
		left: 55px;
		bottom: 0;
	}
}
@media screen and (max-width: 767px){
	#all_table_products .td_exist {
		width: auto;
	}
	#all_table_products .td_manufacturer {
		text-align: left;
		min-width: 90px;
	}
	.show_hide_button {
		position: static;
	}
}
#all_table_products .hide_row .td_name {
    border-top: 1px solid #ddd;
}
#all_table_products tr.epc-warehouse-subrow td {
    border-top: 1px dashed #d9e2ef;
}
#all_table_products tr.epc-warehouse-subrow .td_manufacturer {
	padding-left: 0;
}
#all_table_products tr.epc-warehouse-subrow .td_article,
#all_table_products tr.epc-warehouse-subrow .td_name {
	color: #94a3b8;
}
<?php
// Стили для шаблона LIMO
if($DP_Template->id == 61){
?>
.td_exist img{
	max-width:initial;
}
#modal_products_info {color:#222;}
#modal_products_info .btn {
    margin-bottom: 2px;
    font-size: 14px;
    padding-left: 11px;
    padding-right: 11px;
    margin-right: 0;
}
#modal_products_info .btn-default {
    border: 1px solid;
}
#modal_products_info .modal-content {
    padding: 0;
}
<?php
}
?>
</style>

<link href="/lib/Lightbox/css/lightbox.css" rel="stylesheet" type="text/css" />
<script type="text/javascript" src="/lib/Lightbox/js/lightbox.js"></script>
<div class="modal" id="modal_products_info">
	<div class="modal-dialog" style="width: auto; max-width: 800px;">
		<div class="modal-content" style="border-radius: 10px;">
			<div id="modal_products_info_body" class="modal-body" style="padding: 0;"></div>
		</div>
	</div>
</div>

<script>
// Для корректной одновременной работы нескольких модальных окон
epcRunPartSearchBootstrapWhenReady(function(){
$(document).on('hidden.bs.modal', '.modal', function () {
    $('.modal:visible').length && $(document.body).addClass('modal-open');
});
});

// Список товаров для которых ранее была запрошена информация
if(typeof list_products_info === 'undefined')
{
	var list_products_info = new Array;
}

// Функция отображения миниатюр товаров
function show_pictures_products(){
	setTimeout(get_info, 100);
}

// Функция запроса информации о товаре
function get_info(){
	for(let key in list_products_info){
		if(list_products_info[key].ajax === false){
			list_products_info[key].ajax = true;
			
			let request_object = new Object;
				request_object.action = 'all';
				request_object.key = key;
				request_object.manufacturer = list_products_info[key].manufacturer;
				request_object.article = list_products_info[key].article;
			
			jQuery.ajax({
				type: "POST",
				async: true,
				url: "/content/shop/docpart/ajax_get_info.php",
				dataType: "json",
				data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
				success: function(answer)
				{
					if(answer.result == 1){
						list_products_info[answer.key].json = true;
						$("#work_area").append('<div style="display:none; max-height: 100px; overflow: auto; text-align: left; margin-bottom: 20px; border: 1px solid #999; padding: 10px;" id="product_info_'+answer.key+'">'+answer.json+'</div>');
						show_images_products(answer.json, answer.key);
					}
					get_info();
				},
				error: function (e, ajaxOptions, thrownError){
					get_info();
				}
			});
				
			return;
		}else{
			if(list_products_info[key].json === true){
				let json = $("#product_info_"+key).html();
				show_images_products(json, key);
			}
		}
	}
	log('<?php echo translate_str_by_id(4322); ?>:');
	log(list_products_info);
}

// Функция отображения картинки товара
function show_images_products(json, key){
	let info = JSON.parse(json);
	if(info.images != undefined && info.images.length > 0){
		if($(".product_img_"+ key).length){
			//$(".product_img_"+ key +" span").css('background-image','url(/info_images/'+info.images[0]+'.jpg)');
			$(".product_img_"+ key +" span").css('background-image','url(/content/shop/docpart/ajax_get_info.php?image_path='+encodeURIComponent(info.images[0]));
			$(".product_img_"+ key).css('display','inline-block');
		}
	}
}

// Функция переключения таба с информацией о товаре
function show_product_info_tab(id){
	$(".product_info_tab").css('display','none');
	$("#product_info_tab_"+id).css('display','block');
}

// Функция отображения модального окна с информацией о товаре
function show_modal_product_info(key, aid){
	let product = get_product_object(aid);
	product.currency_indicator = '<?=$currency_indicator;?>';
	
	if(document.getElementById("count_need_"+aid)){
		product['count_need'] = parseInt(document.getElementById("count_need_"+aid).value);
	}else{
		product['count_need'] = product['min_order'];
	}
	
	if(list_products_info[key].json === true){
		document.getElementById("modal_products_info_body").innerHTML = '';
		
		let request_object = new Object;
			request_object.action = 'html';
			request_object.json = $("#product_info_"+ key).html();
			request_object.product = product;
		
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/content/shop/docpart/ajax_get_info.php",
			dataType: "text",
			data: "request_object="+encodeURIComponent(JSON.stringify(request_object)),
			success: function(answer)
			{
				document.getElementById("modal_products_info_body").innerHTML = answer;
			}
		});
		
		$("#modal_products_info").modal();
	}
}

// Функция получения объекта товара
function get_product_object(aid){
	let AID_Object = Products.All[aid];
	let Product = new Object;
	<?php
	if($table_mode == 1){
	?>
	if(AID_Object.isRequired == true)
	{
		for(let i=0; i < Products.Required.ProductsTypes.length; i++)
		{
			let Manufacturer = Products.Required.ProductsTypes[i].manufacturer;
			let Article = Products.Required.ProductsTypes[i].article;
			let ProductsObjects = Products.Required.Products.Manufacturers[Manufacturer][Article];
			for(let p=0; p < ProductsObjects.length; p++)
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
		for(let i=0; i < Products.SearchName.ProductsTypes.length; i++)
		{
			let Manufacturer = Products.SearchName.ProductsTypes[i].manufacturer;
			let Article = Products.SearchName.ProductsTypes[i].article;
			let ProductsObjects = Products.SearchName.Products.Manufacturers[Manufacturer][Article];
			for(let p=0; p < ProductsObjects.length; p++)
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
		for(let i=0; i < Products.Analogs.ProductsTypes.length; i++)
		{
			let Manufacturer = Products.Analogs.ProductsTypes[i].manufacturer;
			let Article = Products.Analogs.ProductsTypes[i].article;
			let ProductsObjects = Products.Analogs.Products.Manufacturers[Manufacturer][Article];
			for(let p=0; p < ProductsObjects.length; p++)
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
		for(let i=0; i < Products.Quick_Analogs.ProductsTypes.length; i++)
		{
			let Manufacturer = Products.Quick_Analogs.ProductsTypes[i].manufacturer;
			let Article = Products.Quick_Analogs.ProductsTypes[i].article;
			let ProductsObjects = Products.Quick_Analogs.Products.Manufacturers[Manufacturer][Article];
			for(let p=0; p < ProductsObjects.length; p++)
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
		for(let i=0; i < Products.PossibleReplacement.ProductsTypes.length; i++)
		{
			let Manufacturer = Products.PossibleReplacement.ProductsTypes[i].manufacturer;
			let Article = Products.PossibleReplacement.ProductsTypes[i].article;
			let ProductsObjects = Products.PossibleReplacement.Products.Manufacturers[Manufacturer][Article];
			for(let p=0; p < ProductsObjects.length; p++)
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
		for(let i=0; i < Products.Spare_Box.ProductsTypes.length; i++)
		{
			let Manufacturer = Products.Spare_Box.ProductsTypes[i].manufacturer;
			let Article = Products.Spare_Box.ProductsTypes[i].article;
			let ProductsObjects = Products.Spare_Box.Products.Manufacturers[Manufacturer][Article];
			for(let p=0; p < ProductsObjects.length; p++)
			{
				if(parseInt(ProductsObjects[p].aid) == parseInt(aid))
				{
					Product = Object.assign({}, ProductsObjects[p]);
					break;
				}
			}
		}
	}
	<?php
	}else{
	?>
	if(AID_Object.isRequired == true)
	{
		//Ищем объект товара в списке запрошенных
		for(let i=0; i < Products.Required.length; i++)
		{
			if( parseInt(Products.Required[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Required[i]);
				break;
			}
		}
	}else if(AID_Object.isSearchName == true)
	{
		for(let i=0; i < Products.SearchName.length; i++)
		{
			if( parseInt(Products.SearchName[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.SearchName[i]);
				break;
			}
		}
	}else if(AID_Object.isQuickAnalogs == true)
	{
		for(let i=0; i < Products.Quick_Analogs.length; i++)
		{
			if( parseInt(Products.Quick_Analogs[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Quick_Analogs[i]);
				break;
			}
		}
	}else if (AID_Object.isAnalogs == true)
	{
		for(let i=0; i < Products.Analogs.length; i++)
		{
			if( parseInt(Products.Analogs[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Analogs[i]);
				break;
			}
		}
	}else if (AID_Object.isPossibleReplacement == true)
	{
		for(let i=0; i < Products.PossibleReplacement.length; i++)
		{
			if( parseInt(Products.PossibleReplacement[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.PossibleReplacement[i]);
				break;
			}
		}
	}else if (AID_Object.isSpare_Box == true)
	{
		for(let i=0; i < Products.Spare_Box.length; i++)
		{
			if( parseInt(Products.Spare_Box[i].aid) == parseInt(aid))
			{
				Product = Object.assign({}, Products.Spare_Box[i]);
				break;
			}
		}
	}
	<?php
	}
	?>
	return Product;
}

<?php
if($user_id == 0){
?>
function show_add_bloknot(id){
	alert('<?php echo translate_str_by_id(4323); ?>');
}
<?php
}
?>
</script>

<!----------------------------------- End Картинки в проценке ------------------------------->


