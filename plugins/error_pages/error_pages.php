<?php
defined('_ASTEXE_') or die('No access');
//Плагин настройки страниц 404 и 403

if( isset( $DP_Content->service_data["error_page"] ) )
{
	$plugin_data_value = json_decode($plugin_record["data_value"], true);
	
	$DP_Content->value = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_value"]);
	$DP_Content->title_tag = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_title_tag"]);
	$DP_Content->description_tag = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_description_tag"]);
	$DP_Content->keywords_tag = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_keywords_tag"]);
	$DP_Content->author_tag = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_author_tag"]);
	$DP_Content->content_type = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_content_type"]);
	$DP_Content->content = translate_str_by_id($plugin_data_value[$DP_Content->service_data["error_page"]."_content"]);
}
?>