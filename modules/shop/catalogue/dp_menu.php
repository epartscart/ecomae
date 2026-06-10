<?php
defined('_ASTEXE_') or die('No access');


global $db_link, $DP_Config, $multilang_params;
if (!isset($DP_Config) || !($DP_Config instanceof DP_Config)) {
	require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
	$DP_Config = new DP_Config;//Конфигурация CMS
}
if (!function_exists('epc_portal_apply_config')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
}
epc_portal_apply_config($DP_Config);

//Подключение к БД (reuse storefront PDO when module runs inside dp_core)
if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
		$db_link->query("SET NAMES utf8;");
	} catch (PDOException $e) {
		echo '<!-- catalogue menu unavailable -->';
		return;
	}
}

// Дерево только отображаемых для клиента категорий
$where_published_flag = true;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
$catalogue_tree_skip_properties = true;
$epcMenuCacheKey = 'epc_cat_tree_json:v2:' . md5((string) ($DP_Config->db ?? '') . ':pub1');
$catalogue_tree_dump_JSON = epc_perf_cache_get($epcMenuCacheKey);
if (!is_string($catalogue_tree_dump_JSON) || $catalogue_tree_dump_JSON === '') {
	$catalogue_tree_dump_JSON = null;
}

if ($catalogue_tree_dump_JSON === null) {
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
	epc_perf_cache_set($epcMenuCacheKey, $catalogue_tree_dump_JSON, 900);
} else {
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");
}

$catalogue_tree = json_decode($catalogue_tree_dump_JSON, true);

if (function_exists('epc_electronicae_storefront_active') && epc_electronicae_storefront_active()) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';
	if (is_array($catalogue_tree)) {
		$catalogue_tree = epc_electronicae_filter_menu_tree($db_link, $catalogue_tree);
	}
}
if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php';
	if (is_array($catalogue_tree) && $db_link instanceof PDO && function_exists('epc_epartscart_filter_menu_tree')) {
		$catalogue_tree = epc_epartscart_filter_menu_tree($db_link, $catalogue_tree);
	}
}

$all_cnt_links = count($catalogue_tree);
$link_cnt_level_all = 0;

$tabs_header = '<div class="vertical-tab-list">
				<ul class="nav">';
getHtml_tabs_header($catalogue_tree);
$tabs_header .= '</ul>
			    </div>';

$tabs_content = '<div class="tab-content" style="position: relative; padding-top: 10px;">'.$tabs_content.'</div>';

echo $tabs_header;
echo $tabs_content;

function get_cnt_links($catalogue_tree)
{
	$cnt = 0;
	for($i = 0; $i < count($catalogue_tree); $i++){
		$cnt++;
		$category = $catalogue_tree[$i];
		$cnt += get_cnt_links($category["data"]);
	}
	return $cnt;
}

function getHtml_tabs_header($catalogue_tree)
{
	global $tabs_header;
	global $multilang_params;
	
    //Цикл формирования пунктов 1 уровня
	$cnt_level = count($catalogue_tree);
	for($i = 0; $i < $cnt_level; $i++){
		
		$category = $catalogue_tree[$i];
		
		if($category['published_flag'] == '0'){
			continue;
		}
		
		$value = trim($category["value"]);
		if ($value === '' || preg_match('/^\?+$/u', $value)) {
			continue;
		}
		
        $id = $category["id"];
        $href = $multilang_params['lang_href']."/".$category["url"];
		
		$image = '/content/files/images/no_image.png';
		if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php';
			if ($db_link instanceof PDO && function_exists('epc_epartscart_catalog_placeholder_url')) {
				$image = epc_epartscart_catalog_placeholder_url($db_link);
			}
		}
		if(!empty($category["image"])){
			$candidate = "/content/files/images/catalogue_images/".$category["image"];
			if(file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/".$category["image"])){
				$image = $candidate;
			}
		}
        
        
		
		
		if($i == 0){
			$class = 'class="active"';
		}else{
			$class = '';
		}
		
		if($category['$count'] > 0 )
        {
            $class_a = 'count_ch';
            $href = 'href="#category_'.$id.'" data-toggle="tab" data-hover="tab"';
        }else{
			$class_a = '';
			$href = 'href="'.$href.'"';
		}
		
        $tabs_header .= '<li '.$class.'><a class="'.$class_a.'" '.$href.'>
		<table>
			<tr>
				<td style="padding-right: 10px;"><img style="max-width: 30px; max-height: 30px; width: auto; height: auto;" src="'.$image.'"/></td>
				<td style="width: 100%;">'.$value.'</td>
			</tr>
		</table>
		</a></li>';
		
		//Формирование блока вложенных подкатегорий
		getHtml_tabs_content($category["data"], $i, $id, $image);
    }
}

function getHtml_tabs_content($catalogue_tree, $i, $id, $image)
{
	global $tabs_content;
	
	if($i == 0){
		$class = 'active';
	}else{
		$class = '';
	}
	
	$tabs_content .= '<div style="overflow: hidden;" class="tab-pane '.$class.'" id="category_'.$id.'">';
    $tabs_content .= getHtmlLink($catalogue_tree);
	$tabs_content .= '</div>';
}

function getHtmlLink($catalogue_tree, $level = 0)
{
	global $all_cnt_links, $link_cnt_level_all;
	global $multilang_params;
	
	$html = "";
	
	// Количество ссылок в текущем табе
	if($level === 0){
		$link_cnt_level_all = 0;
		$link_cnt_level = get_cnt_links($catalogue_tree);
		if($link_cnt_level < 40 && $link_cnt_level > 15){
			$show_cnt_links = (int) abs(ceil($link_cnt_level / 2));
		}else if($link_cnt_level > 40){
			$show_cnt_links = (int) abs(ceil($link_cnt_level / 3));
		}else{
			$show_cnt_links = $link_cnt_level;
		}
		
		$html .= '<div class="column_box_line">';
	}
	
	$level++;
    
    //Цикл формирования пунктов данного уровня
	$cnt_level = count($catalogue_tree);
	for($i = 0; $i < $cnt_level; $i++){
    
		$category = $catalogue_tree[$i];
		
		if( ($level === 1) ){
			$html .= '<div class="box_line">';
			$class = 'class="one_line"';
		}else if( ($level === 2) ){
			$class = 'class="two_line"';
		}else{
			$class = 'class="two_line" style="margin-left:'.(15*($level-1)).'px;"';
		}

        //1. Получаем атрибут href:
        $href = $multilang_params['lang_href']."/".$category["url"];
        
        $html .= '<a '.$class.' href="'.$href.'">'.trim($category["value"])."</a>";
        
        //Если пункт содержит вложенные пункты
        if($category['$count'] > 0 )
        {
            $html .= getHtmlLink($category["data"], $level);
        }
		
		if( ($level === 1) ){
			$html .= '</div>';
		}
		
		$link_cnt_level_all++;
		if($link_cnt_level_all >  $show_cnt_links){
			if( ($level === 1) ){
				$link_cnt_level_all = 0;
				$html .= '</div>';
				$html .= '<div class="column_box_line">';
			}
		}
    }
	
	if( ($level === 1) ){
		$html .= '</div>';
	}
	
    return $html;
}


?>
<script>
//После загрузки страницы
$(document).ready(function() {
	//Добавляем блок затемняющего фона
	$('header').after($('<div>', {class: 'fon-catalog'}));
	//Затемнение фона при раскрытии меню каталога
	$('.fon-catalog').on('click', function (e) {
	  showCatalogMenu();
	  $('#dp_menu').css('display', 'none');
	  $(this).css('display', 'none');
	  return false
	});
	//Отображение вложенных пунктов категории на которую наведен курсор
	$('[data-hover="tab"]').mouseenter(function (e) {
	  $(this).tab('show');
	});
});

//Раскрытие меню каталога
var dp_menu_Height = 0;
var scrollY = 0;

function showCatalogMenu(){
	
	let header_height = $('header').outerHeight();
	let alert_height = $('body .alert-info').outerHeight();
	let anchor_height = $('.sticky-anchor').outerHeight();
	if(anchor_height > 0){
		//Добавляем прокрутку выподающего меню если блок меню больше экрана
		$('#dp_menu').css('max-height', window.innerHeight-84);
	}else{
		//Добавляем прокрутку выподающего меню если блок меню больше экрана
		$('#dp_menu').css('max-height', window.innerHeight-30-header_height-alert_height+anchor_height);
	}
	
	if(document.getElementById('dp_menu')){
		if($('#dp_menu').css('display') == 'block'){
			  $('#dp_menu').css('display', 'none');
			  $('.fon-catalog').css('display', 'none');
			 
			  document.body.style.position = '';
			  document.body.style.top = "0px";
			  window.scrollTo(0, (parseInt(scrollY || '0') * 1));
			  $('header').css('padding-right', 0);
			  $('header .fixed').css('padding-right', 0);
			  
		  }else{
			  
			  scrollY = window.pageYOffset;
			  document.body.style.overflow = "hidden";

			  $('#dp_menu').css('display', 'block');
			  $('.fon-catalog').css('display', 'block');
			  dp_menu_Height = document.getElementById("dp_menu").scrollHeight - document.getElementById("dp_menu").clientHeight;
			  
			  let documentWidth = parseInt(document.documentElement.clientWidth);
			  let windowWidth = parseInt(window.innerWidth);
			  let scrollbarWidth = windowWidth - documentWidth;
			  $('header').css('padding-right', scrollbarWidth);
			  $('header .fixed').css('padding-right', scrollbarWidth);
			 
			  document.body.style.position = 'fixed';
			  document.body.style.top = "-"+scrollY+"px";
		  }
		return false;
	}
}

//Обработка прокрутки внутри меню, что бы сдвигать вниз при прокрутки блок вложенных подкатегорий
$(document).ready(function() {
	$('#dp_menu').css('overflow', 'auto');
	$('#dp_menu').on("load scroll resize", function(){
		
		let dp_menu_scrollTop = $('#dp_menu').scrollTop();
		if(dp_menu_scrollTop <= dp_menu_Height+10){
			$('#dp_menu .tab-content').css('padding-top', dp_menu_scrollTop+10);
		}
		
		//Добавляем прокрутку выподающего меню если блок меню больше экрана
		let header_height = $('header').outerHeight();
		let alert_height = $('body .alert-info').outerHeight();
		let anchor_height = $('.sticky-anchor').outerHeight();
		if(anchor_height > 0){
			$('#dp_menu').css('max-height', window.innerHeight-84);
		}else{
			$('#dp_menu').css('max-height', window.innerHeight-30-header_height-alert_height+anchor_height);
		}

	});
});
</script>