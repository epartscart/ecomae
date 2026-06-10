<?php
// Типы транспортных средств
defined('_UCatalog_') or die('No access');

// Получаем данные из сохраненного лога
if(file_exists($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/tmp_types.json")){
	$json = file_get_contents($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/tmp_types.json");
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
	$postdata['method'] = 'get_types';
	
	$curl_result = u_curl($postdata);
	
	$result = json_decode($curl_result, true);
	
	if($result['status'] === true){
		$data_array = $result['types'];
		
		$f = fopen($_SERVER["DOCUMENT_ROOT"]."/api/UCatalog/tmp_types.json", "w");
		fwrite($f, json_encode(array('timestamp'=>time(), 'date'=>date('d.m.Y H:i'), 'data_array'=>$data_array)));
	}
}

// Формируем HTML
if(!empty($data_array)){
	ob_start();
	?>
	<div class="row">
		<div class="col-md-12">
			
			<div class="UCatalog_nav_box">
				<ul class="UCatalog_nav_tabs">
				<?php
				foreach($data_array as $type_tab)
				{
					// Проверяем включено ли отображение раздела
					$param_name = 'type_show_'.$type_tab["name"];
					if($$param_name != true){
						continue;
					}
					if(empty($request_object['type'])){
						$request_object['type'] = $type_tab['name'];
						$request_object['caption'] = $type_tab["caption"];
					}
					$active = "";
					if($request_object['type'] === $type_tab['name'])
					{
						$active = "active";
					}
					?>
					<li id="UCatalog_nav_tab_<?php echo $type_tab["name"]; ?>" class="<?php echo $active; ?>" onClick="UCatalog_loading('get_marks', '<?php echo $type_tab["caption"]; ?>', '<?php echo $type_tab["name"]; ?>');"><?php echo $type_tab["caption"]; ?></li>
					<?php
				}
				?>
				</ul>
				
				<?php
				//Подключение к БД
				try
				{
					$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
				}
				catch (PDOException $e) 
				{
					exit("No DB connect");
				}
				$db_link->query("SET NAMES utf8;");

				//Для работы с пользователем
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
				$user_id = DP_User::getUserId();
				
				if($user_id > 0){
				$query = $db_link->prepare("SELECT `id`, `caption`, `UCatalog_json` FROM `shop_docpart_garage` WHERE `UCatalog_json` != '' AND `user_id` = ?;");
				$query->execute(array($user_id));
				$html = '';
				while($record = $query->fetch())
				{
					$html .= '<div onClick="UCatalog_get_garage('.$record['id'].')">'.$record['caption'].'</div>';
				}
				
				if($html != ''){
				?>
				
					<span onClick="UCatalog_show_garage_list();" class="btn_primary UCatalog_garage_btn"><i class="fa fa-car" aria-hidden="true"></i></span>
					<div>
					<?php echo $html;?>
					</div>
				
				<?php
				}
				}
				?>
				
			</div>
			
			<div id="UCatalog_tab_content">

			</div>
		
		</div>
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