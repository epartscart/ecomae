<?php
//Скрипт для вывода содержимого таба "Поиск по артикулу"
defined('_ASTEXE_') or die('No access');
?>
<div class="search_tab_clar"><?php echo translate_str_by_id(4172); ?>:</div>


<?php
//1. Получаем админские настройки таба
$aftermarket_tab_query = $db_link->prepare('SELECT * FROM `shop_docpart_search_tabs` WHERE `name` = :name;');
$aftermarket_tab_query->bindValue(':name', 'aftermarket');
$aftermarket_tab_query->execute();
$aftermarket_tab_record = $aftermarket_tab_query->fetch(PDO::FETCH_ASSOC);
$aftermarket_tab_parameters_values = json_decode($aftermarket_tab_record["parameters_values"], true);


$aftermarket_catalogs_parts_html = "";
if( isset($aftermarket_tab_parameters_values["aftermarket_catalogs_parts_com_show"]) )
{
	if( $aftermarket_tab_parameters_values["aftermarket_catalogs_parts_com_show"] === "on" )
	{
		$aftermarket_catalogs_parts_html = "<div class=\"search_tab_car_catalogue\"><a href=\"https://aftermarket.catalogs-parts.com/#{client:".$aftermarket_tab_parameters_values["aftermarket_catalogs_parts_com_id"].";page:models;lang:ru;catalog:pc}\" target=\"_blank\"><i class=\"fa fa-check\"></i> ".$aftermarket_tab_parameters_values["aftermarket_catalogs_parts_com_caption"]."</a></div>";
	}
}




$aftermarket_ilcats_html = "";
if( isset($aftermarket_tab_parameters_values["aftermarket_ilcats_show"]) )
{
	if( $aftermarket_tab_parameters_values["aftermarket_ilcats_show"] === "on" )
	{
		$aftermarket_ilcats_html = "<div class=\"search_tab_car_catalogue\"><a href=\"http://aftermarket.autocats.ru.com/pid/".$aftermarket_tab_parameters_values["aftermarket_ilcats_pid"]."/clid/".$aftermarket_tab_parameters_values["aftermarket_ilcats_clid"]."\" target=\"_blank\"><i class=\"fa fa-check\"></i> ".$aftermarket_tab_parameters_values["aftermarket_ilcats_caption"]."</a></div>";
	}
}



if( $aftermarket_tab_parameters_values["aftermarket_catalogs_parts_com_order"] <= $aftermarket_tab_parameters_values["aftermarket_ilcats_order"] )
{
	$aftermarket_html = $aftermarket_catalogs_parts_html.$aftermarket_ilcats_html;
}
else
{
	$aftermarket_html = $aftermarket_ilcats_html.$aftermarket_catalogs_parts_html;
}


if( $aftermarket_html == "")
{
	$aftermarket_html = "<p class=\"search_tab_car_catalogue_back\" style=\"text-decoration:none;\"><i class=\"fa fa-wrench\"></i> ".translate_str_by_id(4173)." \"".translate_str_by_id(790)."\"</p>";
}
echo $aftermarket_html;
?>




<p class="search_tab_car_catalogue_back" style="text-decoration:none;"><i class="fa fa-info-circle"></i> <?php echo translate_str_by_id(4174); ?></p>