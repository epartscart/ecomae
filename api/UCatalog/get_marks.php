<?php
// Mарки транспортных средств
defined('_UCatalog_') or die('No access');

if(${'type_show_'.$request_object['type']}){
	// Получаем данные из сохраненного лога
	if(file_exists($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/tmp_".$request_object['type'].".json")){
		$json = file_get_contents($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/tmp_".$request_object['type'].".json");
		$tmp_log = json_decode($json, true);
		
		if( !empty($tmp_log['timestamp']) && !empty($tmp_log['data_array']) ){
			if( (time() - $tmp_log['timestamp']) < (3600 * 24) ){
				$data_array = $tmp_log['data_array'];
			}
		}
	}

	// Получаем данные от API
	if(empty($data_array)){
		$postdata = array();
		$postdata['method'] = 'get_marks';
		$postdata['type'] = $request_object['type'];
		
		$curl_result = u_curl($postdata);
		
		$result = json_decode($curl_result, true);
		
		if($result['status'] === true){
			$data_array = $result['marks'];
			
			$f = fopen($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/tmp_".$request_object['type'].".json", "w");
			fwrite($f, json_encode(array('timestamp'=>time(), 'date'=>date('d.m.Y H:i'), 'data_array'=>$data_array)));
		}
	}
}

// Формируем HTML
if(!empty($data_array)){
	ob_start();
	?>
	
	<div class="row">
		
		<?php
		$popular_flag = false;
		$letters = array();
		foreach($data_array as $marka)
		{
			$marka["caption"] = trim($marka["caption"]);
			if( in_array($marka["caption"], ${'filter_'.$request_object['type']}) ){
				continue;
			}
			$letter = (string) mb_substr($marka["caption"],0,1,'UTF-8');
			if(!in_array($letter, $letters)){
				$letters[] = $letter;
			}
			if( in_array($marka["caption"], ${'popular_'.$request_object['type']}) ){
				$popular_flag = true;
			}
		}
		
		if(!empty($letters)){
		?>
		<div class="col-md-12">
			<ul class="UCatalog_nav_letters">
				<?php
				if($popular_flag){
					?>
					<li id="UCatalog_nav_letter_popular" class="active" onClick="UCatalog_letter('popular');"><?php echo translate_str_by_id(2093); ?></li>
					<li id="UCatalog_nav_letter_all" onClick="UCatalog_letter('all');"><?php echo translate_str_by_id(2094); ?></li>
					<?php
				}else{
					?>
					<li id="UCatalog_nav_letter_all" class="active" onClick="UCatalog_letter('all');"><?php echo translate_str_by_id(2094); ?></li>
					<?php
				}
				?>
				
				<?php
				foreach($letters as $letter)
				{
				?>
				<li id="UCatalog_nav_letter_<?php echo $letter; ?>" onClick="UCatalog_letter('<?php echo $letter; ?>');"><?php echo $letter; ?></li>
				<?php
				}
				?>
			</ul>
		</div>
		<?php
		}
		
		foreach($data_array as $marka)
		{
			$marka["caption"] = trim($marka["caption"]);
			if( in_array($marka["caption"], ${'filter_'.$request_object['type']}) ){
				continue;
			}
			$letter = (string) mb_substr($marka["caption"],0,1,'UTF-8');
			$class = 'letter_'.$letter;
			if($popular_flag){
				if( in_array($marka["caption"], ${'popular_'.$request_object['type']}) ){
					$class .= ' popular';
				}else{
					$class .= ' hidden';
				}
			}
			?>
			<div class="col-xs-6 col-sm-6 col-md-3 UCatalog_tab_element <?php echo $class; ?>">
				<div onClick="UCatalog_loading('get_models', '<?php echo $marka["caption"]; ?>', '<?php echo $request_object['type']; ?>', '<?php echo $marka["id"]; ?>');"><div><?php echo $marka["caption"]; ?></div></div>
			</div>
			<?php
		}
		?>
	</div>
	
	<?php
	$html = ob_get_contents();
	ob_end_clean();
	
	$answer["status"] = true;
	$answer["html"] = $html;
	$answer["tag"] = 'UCatalog_tab_content';
}else{
	if(isset($result['message'])){
		$answer["message"] = '<h5>UCatalog, '.translate_str_by_id(2095).': '.$result['message'].'</h5>';
	}else{
		$answer["message"] = '<h5>'.translate_str_by_id(2096).'</h5>';
	}
}
?>