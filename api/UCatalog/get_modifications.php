<?php
// Модификаций транспортных средств
defined('_UCatalog_') or die('No access');

// Получаем данные от API
$postdata = array();
$postdata['method'] = 'get_modifications';
$postdata['type'] = $request_object['type'];
$postdata['mark_id'] = $request_object['mark_id'];
$postdata['model_id'] = $request_object['model_id'];

$curl_result = u_curl($postdata);

$result = json_decode($curl_result, true);

if($result['status'] === true){
	$data_array = $result['modifications'];
}

// Формируем HTML
if(!empty($data_array)){
	ob_start();
	?>
	
	<div class="col-md-10" style="font-weight:500;">
		<div class="row">
		<div class="col-md-6"><?php echo translate_str_by_id(2086); ?></div>
		<?php
		foreach($data_array as $key => $item)
		{
			if(!empty($item['properties'])){
				$i = 0;
				foreach($item['properties'] as $property){
					echo '<div class="col-md-3">'.$property['property_caption'].'</div>';
					$i++;
					if($i > 1){
						break;
					}
				}
			}
			break;
		}
		?>
		</div>
	</div>
	<div class="row"></div>
	<div style="border-bottom: 1px solid #e4e4e4;"></div>
	
	<?php
	$n = 0;
	foreach($data_array as $key => $item)
	{
		$n++;
		?>
		<div class="col-md-12 UCatalog_modifications">
			<div class="row">
				<div class="col-md-10">
					<div class="row">
					<div class="col-md-6"><?php echo $item["caption"]; ?></div>
					<?php
					if(!empty($item['properties'])){
						$i = 0;
						foreach($item['properties'] as $property){
							echo '<div class="col-md-3">'.$property['property_value'].'</div>';
							$i++;
							if($i > 1){
								break;
							}
						}
					}
					?>
					</div>
				</div>
				<div class="col-md-2" style="height: 32px;">
				<div class="row"></div>
				<i onClick="UCatalog_modifications_show_hide_property('<?php echo $n; ?>')" class="fa fa-angle-double-down btn_property" aria-hidden="true"></i>
				<span onClick="UCatalog_loading('get_tree', '<?php echo $item["caption"]; ?>', '<?php echo $request_object['type']; ?>', '<?php echo $request_object["mark_id"]; ?>', '<?php echo $request_object["model_id"]; ?>', '<?php echo $item["id"]; ?>', '0', '', '');" class="btn_primary"><?php echo translate_str_by_id(2098); ?></span>
				<div class="row"></div>
				</div>
			</div>
		</div>
		
		<div id="UCatalog_modifications_property_<?php echo $n; ?>" class="col-md-12 UCatalog_modifications_property">
		<div class="row">
		<?php
		if(!empty($item['properties'])){
			$i = 0;
			foreach($item['properties'] as $property){
				echo '<div class="col-md-3"><b>'.$property['property_caption'].':</b><br/>'.$property['property_value'].'</div>';
				$i++;
				if($i == 4){
					$i = 0;
					echo '<div class="row"></div>';
				}
			}
		}
		?>
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
}
?>