<?php
// Список запчастей определенного конечного узла дерева
defined('_UCatalog_') or die('No access');

// Получаем данные от API
$postdata = array();
$postdata['method'] = 'get_parts';
$postdata['type'] = $request_object['type'];
$postdata['mark_id'] = $request_object['mark_id'];
$postdata['model_id'] = $request_object['model_id'];
$postdata['modification_id'] = $request_object['modification_id'];
$postdata['parent'] = $request_object['parent'];
$postdata['section_id'] = $request_object['section_id'];

$curl_result = u_curl($postdata);

$result = json_decode($curl_result, true);

if($result['status'] === true){
	$data_array = $result['parts'];
}

// Формируем HTML
if(!empty($data_array)){
	ob_start();
	?>
	
	<table class="table table_parts">
	<tr>
		<th><?php echo translate_str_by_id(2070); ?></th>
		<th><?php echo translate_str_by_id(2071); ?></th>
		<th><?php echo translate_str_by_id(2102); ?></th>
		<th></th>
	</tr>
	<?php
	foreach($data_array as $item){
	?>
	<tr>
		<td><?php echo $item['manufacturer']; ?></td>
		<td><?php echo $item['article_show']; ?></td>
		<td><?php echo $item['name']; ?></td>
		<td class="text-right" style="white-space: nowrap;">
		<span onClick="show_modal_product_info('<?php echo $item['manufacturer']; ?>', '<?php echo $item['article']; ?>', '<?php echo $item['name']; ?>');" class="btn_primary"><i class="fa fa-info-circle" aria-hidden="true"></i></span>
		<span onClick="UCatalog_show_modal_add_notepad('<?php echo $item['manufacturer']; ?>', '<?php echo $item['article']; ?>', '<?php echo $item['name']; ?>');" class="btn_primary"><i class="fa fa-car" aria-hidden="true"></i></span>
		<?php
		if( $DP_Config->chpu_search_config["chpu_search_on"] == true )
		{
			echo '<span onClick="window.open(\''. $DP_Config->domain_path .$multilang_params['lang_href_slash_after'].'parts/'. htmlentities(mb_strtoupper(trim($item['manufacturer']), "UTF-8"), ENT_QUOTES, "UTF-8") .'/'. mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $item['article']), "UTF-8") .'\', \'_blank\');" class="btn_primary">'.translate_str_by_id(2072).'</span>';
		}else{
			echo '<span onClick="window.open(\''. $DP_Config->domain_path .$multilang_params['lang_href_slash_after'].'shop/part_search?brend='. htmlentities(mb_strtoupper(trim($item['manufacturer']), "UTF-8"), ENT_QUOTES, "UTF-8") .'&article='. mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $item['article']), "UTF-8") .'\', \'_blank\');" class="btn_primary">'.translate_str_by_id(2072).'</span>';
		}
		?>
		</td>
	</tr>
	<?php
	}
	?>
	</table>
	
	<?php
	$html = ob_get_contents();
	ob_end_clean();
	
	$answer["status"] = true;
	$answer["html"] = $html;
	$answer["tag"] = 'UCatalog_parts';
}
?>