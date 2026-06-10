<?php
// Модели транспортных средств
defined('_UCatalog_') or die('No access');

// Получаем данные от API
$postdata = array();
$postdata['method'] = 'get_models';
$postdata['type'] = $request_object['type'];
$postdata['mark_id'] = $request_object['mark_id'];

$curl_result = u_curl($postdata);

$result = json_decode($curl_result, true);

if($result['status'] === true){
	$data_array = $result['models'];
}

// Формируем HTML
if(!empty($data_array)){
	ob_start();
	?>
	
	<div class="row">
		
		<?php
		$letters = array();
		$filters = array();
		foreach($data_array as $model)
		{
			$model["caption"] = trim($model["caption"]);
			
			$letter = (string) mb_substr($model["caption"],0,1,'UTF-8');
			if(!in_array($letter, $letters)){
				$letters[] = $letter;
			}
			
			$years = explode('-', trim($model["production_years"]));
			$year = explode('.', trim($years[0]));
			$year = (int) trim($year[1]);
			if($year < 2000){
				if(!in_array(1900, $filters)){
					$filters[] = 1900;
				}
			}
			if($year >= 2000 && $year < 2010){
				if(!in_array(2000, $filters)){
					$filters[] = 2000;
				}
			}
			if($year >= 2010 && $year < 2020){
				if(!in_array(2010, $filters)){
					$filters[] = 2010;
				}
			}
			if($year >= 2020 && $year < 2030){
				if(!in_array(2020, $filters)){
					$filters[] = 2020;
				}
			}
			if($year >= 2030){
				if(!in_array(2030, $filters)){
					$filters[] = 2030;
				}
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
		?>
		
		
		<?php
		if(!empty($filters)){
			sort($filters);
		?>
		<div class="col-md-12">
			<ul class="UCatalog_nav_filters">
				<li id="UCatalog_nav_filter_all" class="active" onClick="UCatalog_filter('all');">Все</li>
				<?php
				foreach($filters as $value)
				{
				?>
				<li id="UCatalog_nav_filter_<?php echo $value; ?>" onClick="UCatalog_filter('<?php echo $value; ?>');"><?php echo $value; ?></li>
				<?php
				}
				?>
			</ul>
		</div>
		<?php
		}
		?>
		
		
		<?php
		foreach($data_array as $model)
		{
			$model["caption"] = trim($model["caption"]);
			$letter = (string) mb_substr($model["caption"],0,1,'UTF-8');
			$class = 'letter_'.$letter;
			
			$years = explode('-', trim($model["production_years"]));
			$year = explode('.', trim($years[0]));
			$year = (int) trim($year[1]);
			if($year < 2000){
				$class .= ' filter_1900';
			}
			if($year >= 2000 && $year < 2010){
				$class .= ' filter_2000';
			}
			if($year >= 2010 && $year < 2020){
				$class .= ' filter_2010';
			}
			if($year >= 2020 && $year < 2030){
				$class .= ' filter_2020';
			}
			if($year >= 2030){
				$class .= ' filter_2030';
			}
			?>
			<div class="col-xs-12 col-sm-6 col-md-4 col-lg-3 UCatalog_tab_element <?php echo $class; ?>">
				<div onClick="UCatalog_loading('get_modifications', '<?php echo $model["caption"]; ?>', '<?php echo $request_object['type']; ?>', '<?php echo $request_object["mark_id"]; ?>', '<?php echo $model["id"]; ?>');"><div><?php echo $model["caption"]; ?></div><div><small><?php echo $model["production_years"]; ?></small></div></div>
			</div>
			<?php
		}
		?>
		<div id="UCatalog_tab_content_no_data" class="col-xs-12 hidden"><?php echo translate_str_by_id(2097); ?></div>
	</div>
	
	<?php
	$html = ob_get_contents();
	ob_end_clean();
	
	$answer["status"] = true;
	$answer["html"] = $html;
}else{
	if(isset($result['message'])){
		$answer["message"] = '<h5>UCatalog, '.translate_str_by_id(2095).': '.$result['message'].'</h5>';
	}else{
		$answer["message"] = '<h5>'.translate_str_by_id(2096).'</h5>';
	}
}
?>