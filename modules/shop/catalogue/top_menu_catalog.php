<?php
defined('_ASTEXE_') or die('No access');


if (!isset($DP_Config) || !($DP_Config instanceof DP_Config)) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
	$DP_Config = new DP_Config();
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
	if (function_exists('epc_portal_apply_config')) {
		epc_portal_apply_config($DP_Config);
	}
}

//Подключение к БД
if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
	} catch (PDOException $e) {
		exit('No DB connect');
	}
}
$db_link->query("SET NAMES utf8;");

// Дерево только отображаемых для клиента категорий
$where_published_flag = true;

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix

$catalogue_tree = is_string($catalogue_tree_dump_JSON)
	? json_decode($catalogue_tree_dump_JSON, true)
	: null;
if (!is_array($catalogue_tree)) {
	$catalogue_tree = array();
}

if (!empty($catalogue_tree)) {
	try {
		echo '
	<ul class="nav navbar-nav nav_cat">
		<li>
			<a href="javascript:void(0);" class="dropdown-cat-btn dropdown-toggle" data-toggle="dropdown">'.translate_str_by_id(4201).'<span class="hidden-sm"> '.translate_str_by_id(4769).'</span></a>
			<ul class="dropdown-menu keep_open dropdown-menu-left fadeIn">
			'. getHtmlOfTopMenuCatalogue($catalogue_tree) .'
			</ul>
		</li>
	</ul>';
	} catch (Throwable $e) {
		// Do not break storefront header if catalogue menu fails.
	}
}

function getHtmlOfTopMenuCatalogue($catalogue_tree, $level = 0)
{
	global $DP_Template;
	global $multilang_params;

	if (!is_array($catalogue_tree) || $level > 12) {
		return '';
	}

	$level++;
    $html = "";
	if( ! isset($DP_Template->data_value->cnt_category_after_hidden) ){
		$cnt_category_after_hidden = 15;
	}else{
		$cnt_category_after_hidden = (int) $DP_Template->data_value->cnt_category_after_hidden;
	}
    
	
    //Цикл формирования пунктов данного уровня
	$cnt_level = count($catalogue_tree);
	for ($i = 0; $i < $cnt_level; $i++) {
		if (!isset($catalogue_tree[$i]) || !is_array($catalogue_tree[$i])) {
			continue;
		}
		$category = $catalogue_tree[$i];
		$category_child_count = (int) ($category['$count'] ?? $category['count'] ?? 0);
	
		if( ($level === 1) && ($i === $cnt_category_after_hidden) && ($cnt_level > $cnt_category_after_hidden) ){
			$html .= '
			<li class="dropdown-submenu">
				<div id="top-menu-catalogue-accordion">
					<div id="top-menu-catalogue-collapseTwo" class="panel-collapse collapse">
						<ul class="open_t dropdown-submenu">';
		}

        //1. Получаем атрибут href:
        $href = $multilang_params['lang_href']."/".$category["url"];
        
        $class_li = '';
		if ($category_child_count > 0) {
			$class_li = ' class="dropdown-submenu"';
		}
        $html .= "<li".$class_li.">";//Начало пункта li

		$class_a = '';
		if ($category_child_count > 0) {
			 $class_a = ' class="has_children"';
		}
        $html .= '<a'.$class_a.' href="'.$href.'">'.trim($category["value"])."</a>";
        
        //Если пункт содержит вложенные пункты
        if ($category_child_count > 0)
        {
			if(($level % 2) !== 0 && $level > 2){
				$html .= '<ul class="dropdown-menu dropdown-menu-left">';
			}else{
				$html .= '<ul class="dropdown-menu">';
			}
			$child_tree = (isset($category['data']) && is_array($category['data'])) ? $category['data'] : array();
            $html .= getHtmlOfTopMenuCatalogue($child_tree, $level);
            $html .= "</ul>";
        }

        $html .= "</li>";
		
		if( ($level === 1) && ($i >= $cnt_category_after_hidden) && ($cnt_level === ($i+1)) ){
			$html .= '
						</ul>
					</div>
					<div class="catalogue-collapse-link-box">
						<a data-toggle="collapse" data-parent="#top-menu-catalogue-accordion" href="#top-menu-catalogue-collapseTwo" class="collapsed">
							'.translate_str_by_id(4770).'
						</a>
					</div>
				</div>
			</li>';
		}
    }

    return $html;
}
?>