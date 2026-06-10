<?php
// Дерево узлов, определенной модификации транспортного средства
defined('_UCatalog_') or die('No access');

// Получаем данные от API
$postdata = array();
$postdata['method'] = 'get_tree';
$postdata['type'] = $request_object['type'];
$postdata['mark_id'] = $request_object['mark_id'];
$postdata['model_id'] = $request_object['model_id'];
$postdata['modification_id'] = $request_object['modification_id'];
$postdata['parent'] = $request_object['parent_id'];

$curl_result = u_curl($postdata);

$result = json_decode($curl_result, true);

if($result['status'] === true){
	$data_array = $result['nodes'];
}

// Формируем HTML
if(!empty($data_array)){
	ob_start();
	?>
	
	<?php
	if($request_object["parent_id"] === '0'){
		echo '<div class="row"><div class="col-md-5"><ul class="UCatalog_tree">';
	}
	foreach($data_array as $num => $item){
		if($request_object["parent_chain"] !== ''){
			$parent_chain = $request_object["parent_chain"].'_'.($num+1);
		}else{
			$parent_chain = ($num+1);
		}
		if($request_object["level"] == 1){
			$request_object["parent_num"] = $num;
		}
		echo '<li>';
			if($item['is_folder'] == true){
			?>
				<div id="UCatalog_tree_drop_<?php echo $parent_chain; ?>" onClick="UCatalog_loading('get_tree', '<?php echo $request_object["caption"]; ?>', '<?php echo $request_object['type']; ?>', '<?php echo $request_object["mark_id"]; ?>', '<?php echo $request_object["model_id"]; ?>', '<?php echo $request_object["modification_id"]; ?>', '<?php echo $item["id"]; ?>', '<?php echo $parent_chain; ?>', ''); UCatalog_tree_drop('<?php echo $parent_chain; ?>');" class="UCatalog_tree_drop">+</div>
				
				<div id="UCatalog_tree_caption_<?php echo $parent_chain; ?>" class="UCatalog_tree_caption" onClick="UCatalog_loading('get_tree', '<?php echo $request_object["caption"]; ?>', '<?php echo $request_object['type']; ?>', '<?php echo $request_object["mark_id"]; ?>', '<?php echo $request_object["model_id"]; ?>', '<?php echo $request_object["modification_id"]; ?>', '<?php echo $item["id"]; ?>', '<?php echo $parent_chain; ?>'); UCatalog_tree_drop('<?php echo $parent_chain; ?>');"><?php echo $item['caption']; ?></div>
				
				<ul id="UCatalog_tree_<?php echo $parent_chain; ?>"></ul>
			<?php
			}else{
			?>
				<div id="UCatalog_tree_caption_<?php echo $parent_chain; ?>" class="UCatalog_tree_caption" onClick="UCatalog_loading('get_parts', '<?php echo $request_object["caption"]; ?>', '<?php echo $request_object['type']; ?>', '<?php echo $request_object["mark_id"]; ?>', '<?php echo $request_object["model_id"]; ?>', '<?php echo $request_object["modification_id"]; ?>', '<?php echo $item["id"]; ?>', '<?php echo $parent_chain; ?>', '<?php echo $item["id"]; ?>'); UCatalog_tree_caption_bg('<?php echo $parent_chain; ?>');"><?php echo $item['caption']; ?></div>
			<?php
			}
		echo '</li>';
		
	}
	if($request_object["parent_id"] === '0'){
		echo '</ul></div><div id="UCatalog_parts" class="col-md-7"></div></div>';
		?>
		<div class="modal" id="UCatalog_modal_products_info">
			<div class="modal-dialog" style="width: auto; max-width: 800px;">
				<div class="modal-content" style="border-radius: 10px;">
					<div id="UCatalog_modal_products_info_body" class="modal-body" style="padding: 0;"></div>
				</div>
			</div>
		</div>
		<div class="modal" id="UCatalog_modal_garage">
			<div class="modal-dialog" style="width: auto; max-width: 600px;">
				<div class="modal-content" style="border-radius: 10px;">
					<div id="UCatalog_modal_garage_body" class="modal-body" style="padding: 0;"></div>
				</div>
			</div>
		</div>
		<?php
	}
	?>
	
	<?php
	$html = ob_get_contents();
	ob_end_clean();
	
	$answer["status"] = true;
	$answer["html"] = $html;
	
	if($request_object['parent_id'] !== '0'){
		$answer["tag"] = 'UCatalog_tree_'.$request_object['parent_chain'];
	}
}
?>