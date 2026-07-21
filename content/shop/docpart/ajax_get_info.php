<?php
// .htaccess - для имитации картинки
//RewriteRule ^info_images(.*) /content/shop/docpart/ajax_get_info.php?action=img [L]

ini_set('display_errors', '0');

// Конфигурация сайта
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;


// Проверка доступа
if(strpos($_SERVER['HTTP_REFERER'], $DP_Config->domain_path) !== 0){
	exit('Forbidden 403');
}


//Настройка включения/отключения сервиса Ucats part_info
if( $DP_Config->ucats_part_info == false )
{
	exit( json_encode( array('result'=>0) ) );
}



// Получаем параметры запроса
if(!empty($_POST['request_object'])){
	$request_object = json_decode($_POST['request_object'], true);
	$action = $request_object['action'];// Тип запроса
	$key = $request_object['key'];// Тип запроса
	$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $request_object['article']), "UTF-8");
	$manufacturer = mb_strtoupper(trim($request_object['manufacturer']), "UTF-8");
}


// Когда идет запрос картинки
if(!empty($_GET['image_path'])){
	$action = 'img';
}


// Формируем массив ответа
$answer = array();
$answer["result"] = 0;
$answer["key"] = $key;


switch($action){
	case 'all':
		
		header('Content-Type: application/json;charset=utf-8;');
		
		// Соединение с БД
		try
		{
			$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
		}
		catch (PDOException $e) 
		{
			$answer["message"] = "No DB connect";
			exit( json_encode($answer) );
		}
		$db_link->query("SET NAMES utf8;");

		if(!empty($article) && !empty($manufacturer) && !empty($DP_Config->ucats_login) && !empty($DP_Config->ucats_password)){
			
			// Формируем список производителей с учетом синонимов
			$manufacturers_array = get_synonyms($manufacturer);
			
			// Аргументы запроса
			$postdata = http_build_query(
				array(
					'login' => $DP_Config->ucats_login,						// Логин пользователя ucats
					'password' => $DP_Config->ucats_password,				// Пароль пользователя ucats
					'article' => $article,									// Артикул
					'manufacturer' => json_encode($manufacturers_array)		// Производитель
				)
			);
			
			$curl = curl_init();
			curl_setopt($curl, CURLOPT_URL, $DP_Config->api_ucats_url."part_info/");
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 20); 
			curl_setopt($curl, CURLOPT_TIMEOUT, 20);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			$curl_result = curl_exec($curl);
			curl_close($curl);
			
			$result_object = json_decode($curl_result, true);
			
			if($result_object['status'] == true){
				$answer["result"] = 1;
				$answer["json"] = $curl_result;
				
			}
		}
		
	break;
	case 'img':
	
	//$image_path = str_replace(array("/info_images/",".jpg"),"",$_SERVER['REQUEST_URI']);
	$image_path = $_GET['image_path'];
	
	// Аргументы запроса
	$postdata = http_build_query(
		array(
			'login' => $DP_Config->ucats_login,
			'password' => $DP_Config->ucats_password,
			'image_path' => $image_path							
		)
	);
	
	$curl = curl_init();
	curl_setopt($curl, CURLOPT_URL, $DP_Config->api_ucats_url."part_info/get_image.php");
	curl_setopt($curl, CURLOPT_HEADER, true);
	curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
	curl_setopt($curl, CURLOPT_POST, true);
	curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 20); 
	curl_setopt($curl, CURLOPT_TIMEOUT, 20);
	curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
	curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
	$curl_result = curl_exec($curl);
	$ch_info = curl_getinfo($curl);
	
	if($ch_info['http_code'] == 200)
	{
		$header = substr($curl_result, 0, $ch_info['header_size']);
		$curl_result = substr($curl_result, $ch_info['header_size']);

		header('Content-Type: '.$ch_info['content_type']);
		exit($curl_result);
	}
	
	curl_close($curl);
	
	break;
	case 'html':
		
		//Нужно для мультиязычности
		if( !isset($db_link) )
		{
			// Соединение с БД
			try
			{
				$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
			}
			catch (PDOException $e) 
			{
				$answer["message"] = "No DB connect";
				exit( json_encode($answer) );
			}
			$db_link->query("SET NAMES utf8;");
		}
		// -------------------------------------------------------------------------------
		//Подключение мультиязычности
		require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
		$multilang_params = multilang_init();
		// -------------------------------------------------------------------------------
		
		
		$json = json_decode($request_object['json'], true);
		$product = $request_object['product'];
		$manufacturer = $product['manufacturer'];
		$article = $product['article'];
		$name = $product['name'];
		
		if($product['min_order'] <= 0){
			$product['min_order'] = 1;
		}
		if($product['exist'] <= 0){
			$product['exist'] = 1;
		}
		
		$time_to_exe = $product['time_to_exe'];
		$time_to_exe_guaranteed = $product['time_to_exe_guaranteed'];
		if($time_to_exe != $time_to_exe_guaranteed)
		{
			$time_to_exe = $time_to_exe ."-". $time_to_exe_guaranteed ." ".translate_str_by_id(4097).".";
		}else{
			if($time_to_exe == 0)
			{
				$time_to_exe = translate_str_by_id(4197);
			}else{
				$time_to_exe = $time_to_exe ." ".translate_str_by_id(4097).".";
			}
		}
		
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';
		$epc_guest_commerce_blocked = epc_storefront_guest_commerce_blocked();
		$epc_sensitive_mask = function_exists('epc_storefront_sensitive_mask')
			? epc_storefront_sensitive_mask()
			: '**';

		// Цена
		if ($epc_guest_commerce_blocked) {
			$price = htmlspecialchars($epc_sensitive_mask, ENT_QUOTES, 'UTF-8');
			$time_to_exe = $epc_sensitive_mask;
			$exist_display = $epc_sensitive_mask;
		} else {
			$price = number_format($product['price'], 2, '.', ' ');

			//Индикатор валюты
			if($DP_Config->currency_show_mode == "sign_before")
			{
				$price = $product['currency_indicator'] .' '. $price;
			}
			else if($DP_Config->currency_show_mode == "sign_after" || $DP_Config->currency_show_mode == "short_name_after")
			{
				$price = $price .' '. $product['currency_indicator'];
			}
			$exist_display = $product['exist'] . ' ' . translate_str_by_id(4095) . '.';
		}
		?>
		<div class="row" style="margin: 0;">
			<div class="col-lg-12" style="border-bottom: 1px solid #ddd; padding: 5px 15px; position: relative; font-weight: bold;">
				<i onClick="$('#modal_products_info').modal('hide');" style="position: absolute; top: 8px; right: 8px; font-size: 18px; cursor: pointer;" class="fa fa-times-circle-o" aria-hidden="true"></i>
				<span><?php echo translate_str_by_id(2069); ?></span>
			</div>
		</div>
		<div style="padding: 14px 15px 15px; border-bottom: 1px solid #ddd;">
			<div class="row">
				<div style="background: #f8f8f8; border: 1px solid #ddd; padding: 5px 15px 3px 15px; margin: 0; margin-bottom: 15px;" class="col-lg-12"><p style="font-size: 14px; font-weight: 500; padding: 0; margin: 0;"><?=$name;?></p></div>
				<div class="col-sm-4 col-md-6 col-lg-6" style="text-align: center;">
					<?php
					$images = array();
					if(is_array($json['images']) && !empty($json['images'])){
						foreach($json['images'] as $item){
							$item = urlencode($item);
							//$images[] = '/info_images/'.$item.'.jpg';
							$images[] = '/content/shop/docpart/ajax_get_info.php?image_path='.$item;
						}
					}
					if(empty($images)){
						$images[] = '/content/files/images/no_image.png';
					}
					?>
					<a style="display: block; border: 1px solid #ddd; border-radius: 5px; padding:15px;" href="<?=$images[0];?>" rel="lightbox-product">
						<div style="height: 200px; background-image: url(<?=$images[0];?>); background-size: contain; background-repeat: no-repeat; background-position: center center;"></div>
					</a>
					<?php
					$cnt = count($images);
					if($cnt > 1){
					?>
					<div style="text-align:left;">
						<?php
						for($i = 1; $i < $cnt; $i++){
						?>
						<a href="<?=$images[$i];?>" rel="lightbox-product" style="line-height: 0; border: 1px solid #ddd; padding: 5px; margin-right:1px; margin-top:5px; border-radius: 5px; display: inline-block;" ><span style="width: 40px; height: 30px; background: url(<?=$images[$i];?>); display: inline-block; background-size: cover; background-position: center center;"></span></a>
						<?php
						}
						?>
					</div>
					<?php
					}
					?>
				</div>
				
				<div class="col-xs-12 hidden-sm hidden-md hidden-lg" style="margin-bottom:20px;"></div>
				
				<div class="col-sm-8 col-md-6 col-lg-6">
					<table class="table" style="position: relative; top: -1px; margin-bottom: 0;">
						<tr>
							<td style="padding-top: 0; vertical-align: top; margin-top: 0; border: 0;"><?php echo translate_str_by_id(3114); ?>:</td>
							<td style="padding-top: 0; vertical-align: top; margin-top: 0; border: 0; font-size:12px; text-align:right; padding-right:20px;"><?=$manufacturer;?></td>
						</tr>
						<tr>
							<td><?php echo translate_str_by_id(2071); ?>:</td>
							<td style="font-size:12px; text-align:right; padding-right:20px;"><?=$article;?></td>
						</tr>
						<tr>
							<td><?php echo translate_str_by_id(3433); ?>:</td>
							<td style="font-size:12px; text-align:right; padding-right:20px;"><?=htmlspecialchars((string)$time_to_exe, ENT_QUOTES, 'UTF-8');?></td>
						</tr>
						<tr>
							<td><?php echo translate_str_by_id(4094); ?>:</td>
							<td style="font-size:12px; text-align:right; padding-right:20px;"><?=htmlspecialchars((string)$exist_display, ENT_QUOTES, 'UTF-8');?></td>
						</tr>
					</table>
					
					<div style="text-align:right; margin-top: 10px;">
						<?php if ($epc_guest_commerce_blocked) { ?>
						<div><?=$price;?></div>
						<div style="margin-top:10px;"><?php echo epc_storefront_commerce_login_cta_html(isset($multilang_params) && is_array($multilang_params) ? $multilang_params : null); ?></div>
						<?php } else { ?>
						<div style="display:inline-block;">
							<table>
								<tr>
									<td>
										<span style="margin-right: 5px; font-size: 18px; font-weight: 600; color: #555; white-space: nowrap;"><?=$price;?></span>
									</td>
									<td>
										<a style="width: 50px; height: 25px; display: inline-block; text-align: center; border: 1px solid #ddd; border-right: 0; border-radius: 4px 0px 0px 4px;" onclick="minusCountNeed(<?=$product['aid'];?>, <?=$product['exist'];?>, <?=$product['min_order'];?>); $('#product_info_count_need').val($('#count_need_<?=$product['aid'];?>').val());" class="count_need_minus" href="javascript:void(0);"><i class="fa fa-minus"></i></a>
									</td>
									<td>
										<input style="text-align: center; width: 58px; font-size: 14px; height: 25px; margin: 0px; vertical-align: middle; border: 1px solid #ddd; box-shadow: none;" type="text" value="<?=$product['count_need'];?>" onChange="$('#count_need_<?=$product['aid'];?>').val($('#product_info_count_need').val()); onKeyUpCountNeed(<?=$product['aid'];?>, <?=$product['exist'];?>, <?=$product['min_order'];?>);" id="product_info_count_need" />
									</td>
									<td>
										<a style="width: 50px; height: 25px; display: inline-block; text-align: center; border: 1px solid #ddd; border-left: 0; border-radius: 0px 4px 4px 0px;" onclick="plusCountNeed(<?=$product['aid'];?>, <?=$product['exist'];?>, <?=$product['min_order'];?>); $('#product_info_count_need').val($('#count_need_<?=$product['aid'];?>').val());" class="count_need_plus" href="javascript:void(0);"><i class="fa fa-plus"></i></a>
									</td>
								</tr>
							</table>
						</div>
						<div>
							<a style="border-radius: 4px; background:#fff;" href="javascript:void(0);" class="btn btn-ar btn-default" onclick="show_add_bloknot(<?=$product['aid'];?>);"><?php echo translate_str_by_id(4198); ?></a>
							
							<a style="border-radius: 4px;" href="javascript:void(0);" class="btn btn-ar btn-primary" onclick="addToCart(<?=$product['aid'];?>);"><?php echo translate_str_by_id(4199); ?></a>
						</div>
						<?php } ?>
					</div>
					
				</div>
			</div>
		</div>
		
		<div style="background:#f5f5f5; padding: 5px 15px; border-bottom: 1px solid #ddd; margin-bottom:0px; white-space: nowrap; overflow: hidden; overflow-x: auto;">
			
			<?php
			if(is_array($json['product_status']) && !empty($json['product_status'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; margin-right:4px;" href="javascript:void(0);" onclick="show_product_info_tab(1);"><?php echo translate_str_by_id(2073); ?></a>
			<?php
			}
			?>
			
			<?php
			if(is_array($json['properties']) && !empty($json['properties'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; margin-right:4px;" href="javascript:void(0);" onclick="show_product_info_tab(2);"><?php echo translate_str_by_id(2074); ?></a>
			<?php
			}
			?>
			
			<?php
			if(is_array($json['original_parts']) && !empty($json['original_parts'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; margin-right:4px;" href="javascript:void(0);" onclick="show_product_info_tab(3);"><?php echo translate_str_by_id(2075); ?></a>
			<?php
			}
			?>
			
			<?php
			if(is_array($json['applicability']) && !empty($json['applicability'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; margin-right:4px;" href="javascript:void(0);" onclick="show_product_info_tab(4);"><?php echo translate_str_by_id(2076); ?></a>
			<?php
			}
			?>
			
			<?php
			if(is_array($json['replacements']) && !empty($json['replacements'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; margin-right:4px;" href="javascript:void(0);" onclick="show_product_info_tab(5);"><?php echo translate_str_by_id(2077); ?></a>
			<?php
			}
			?>
			
			<?php
			if(is_array($json['crosses']) && !empty($json['crosses'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px; margin-right:4px;" href="javascript:void(0);" onclick="show_product_info_tab(6);"><?php echo translate_str_by_id(2078); ?></a>
			<?php
			}
			?>
			
			<?php
			if(is_array($json['parts']) && !empty($json['parts'])){
			?>
			<a style="text-decoration: none; font-size: 12px; background:#fff; color: #444; border: 1px solid #ddd; padding: 4px 10px; border-radius: 4px;" href="javascript:void(0);" onclick="show_product_info_tab(7);"><?php echo translate_str_by_id(2079); ?></a>
			<?php
			}
			?>
			
		</div>
		
		<?php
		if(is_array($json['product_status']) && !empty($json['product_status'])){
			?>
			<div id="product_info_tab_1" class="product_info_tab" style="padding: 15px;">
				<b><?php echo translate_str_by_id(2080); ?></b>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<tbody>
						<?php
						foreach($json['product_status'] as $item){
							?>
							<tr>
								<td><?=$item['description'];?></td>
							</tr>
							<tr>
								<td><strong><?php echo translate_str_by_id(2081); ?>: </strong><?=$item['status_value'];?></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		
		<?php
		if(is_array($json['properties']) && !empty($json['properties'])){
			?>
			<div id="product_info_tab_2" class="product_info_tab" style="display:none; padding: 15px;">
				<b><?php echo translate_str_by_id(2074); ?></b>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<tbody>
						<?php
						foreach($json['properties'] as $item){
							if(empty($item['title'])){
								$item['title'] = translate_str_by_id(2082);
							}
							?>
							<tr>
								<td><?=$item['title'];?></td>
								<td><?=$item['value'];?></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		
		<?php
		if(is_array($json['original_parts']) && !empty($json['original_parts'])){
			?>
			<div id="product_info_tab_3" class="product_info_tab" style="display:none; padding: 15px;">
				<b><?php echo translate_str_by_id(2083); ?></b>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<thead>
							<tr>
								<th><?php echo translate_str_by_id(2070); ?></th>
								<th><?php echo translate_str_by_id(2071); ?></th>
								<th></th>
							</tr>
						</thead>
						<tbody>
						<?php
						foreach($json['original_parts'] as $item){
							?>
							<tr>
								<td><?=$item['manufacturer'];?></td>
								<td><?=$item['article'];?></td>
								<td style="text-align:right;"><a target="_blank" style="color:#222; white-space: nowrap;" href="<?php echo $multilang_params['lang_href']; ?>/parts/<?=$item['manufacturer'];?>/<?=$item['article'];?>"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(2763); ?></a></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		<?php
		if(is_array($json['applicability']) && !empty($json['applicability'])){
			?>
			<div id="product_info_tab_4" class="product_info_tab" style="display:none; padding: 15px;">
				<b><?php echo translate_str_by_id(2084); ?></b>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<thead>
							<tr>
								<th><?php echo translate_str_by_id(2085); ?></th>
								<th><?php echo translate_str_by_id(2086); ?></th>
								<th><?php echo translate_str_by_id(2087); ?></th>
								<th style="text-align:right;"><?php echo translate_str_by_id(2073); ?></th>
							</tr>
						</thead>
						<tbody>
						<?php
						foreach($json['applicability'] as $item){
							?>
							<tr>
								<td><?=$item['mark'];?></td>
								<td><?=$item['model'];?></td>
								<td><?=$item['years'];?></td>
								<td style="text-align:right;"><?=$item['description'];?></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		<?php
		if(is_array($json['replacements']) && !empty($json['replacements'])){
			?>
			<div id="product_info_tab_5" class="product_info_tab" style="display:none; padding: 15px;">
				<b><?php echo translate_str_by_id(2088); ?></b><br/>
				<small><?php echo translate_str_by_id(2089); ?></small>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<thead>
							<tr>
								<th><?php echo translate_str_by_id(2070); ?></th>
								<th><?php echo translate_str_by_id(2071); ?></th>
								<th></th>
							</tr>
						</thead>
						<tbody>
						<?php
						foreach($json['replacements'] as $item){
							?>
							<tr>
								<td><?=$item['manufacturer'];?></td>
								<td><?=$item['article'];?></td>
								<td style="text-align:right;"><a target="_blank" style="color:#222; white-space: nowrap;" href="<?php echo $multilang_params['lang_href']; ?>/parts/<?=$item['manufacturer'];?>/<?=$item['article'];?>"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(2763); ?></a></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		<?php
		if(is_array($json['crosses']) && !empty($json['crosses'])){
			?>
			<div id="product_info_tab_6" class="product_info_tab" style="display:none; padding: 15px;">
				<b><?php echo translate_str_by_id(2090); ?></b>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<thead>
							<tr>
								<th><?php echo translate_str_by_id(2070); ?></th>
								<th><?php echo translate_str_by_id(2071); ?></th>
								<th></th>
							</tr>
						</thead>
						<tbody>
						<?php
						foreach($json['crosses'] as $item){
							?>
							<tr>
								<td><?=$item['manufacturer'];?></td>
								<td><?=$item['article'];?></td>
								<td style="text-align:right;"><a target="_blank" style="color:#222; white-space: nowrap;" href="<?php echo $multilang_params['lang_href']; ?>/parts/<?=$item['manufacturer'];?>/<?=$item['article'];?>"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(2763); ?></a></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		<?php
		if(is_array($json['parts']) && !empty($json['parts'])){
			?>
			<div id="product_info_tab_7" class="product_info_tab" style="display:none; padding: 15px;">
				<b><?php echo translate_str_by_id(2091); ?></b>
				<div style="overflow: hidden; overflow-x: auto;">
					<table class="table" style="font-size:12px;">
						<thead>
							<tr>
								<th><?php echo translate_str_by_id(2070); ?></th>
								<th><?php echo translate_str_by_id(2071); ?></th>
								<th><?php echo translate_str_by_id(2092); ?></th>
								<th></th>
							</tr>
						</thead>
						<tbody>
						<?php
						foreach($json['parts'] as $item){
							?>
							<tr>
								<td><?=$item['manufacturer'];?></td>
								<td><?=$item['article'];?></td>
								<td><?=$item['count'];?></td>
								<td style="text-align:right;"><a target="_blank" style="color:#222; white-space: nowrap;" href="<?php echo $multilang_params['lang_href']; ?>/parts/<?=$item['manufacturer'];?>/<?=$item['article'];?>"><i class="fa fa-search" aria-hidden="true"></i> <?php echo translate_str_by_id(2763); ?></a></td>
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
			<?php
		}
		?>
		
		
		<?php
		
		exit;
		
	break;
}



// Функция получения синонимов производителя
function get_synonyms($manufacturer){
	
	global $db_link;
	
	$manufacturer_list = array();
	
	if(!empty($manufacturer))
	{
		array_push($manufacturer_list, $manufacturer);
		
		$shop_docpart_manufacturer_id = false;
		$synonym_query = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ?;');
		$synonym_query->execute(array($manufacturer));
		$synonym_record = $synonym_query->fetch();
		
		if( $synonym_record != false )
		{
			$shop_docpart_manufacturer_id = $synonym_record["id"];
		}
		else
		{
			$synonym_query = $db_link->prepare('SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ?;');
			$synonym_query->execute(array($manufacturer));
			$synonym_record = $synonym_query->fetch();
			if( $synonym_record != false )
			{
				$shop_docpart_manufacturer_id = $synonym_record["manufacturer_id"];
			}
		}
		
		if(!empty($shop_docpart_manufacturer_id))
		{
			$synonym_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?;');
			$synonym_query->execute(array($shop_docpart_manufacturer_id));
			if( $synonym_query->fetchColumn() > 0 )
			{
				$query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = ?;');
				$query->execute(array($shop_docpart_manufacturer_id));
				$record = $query->fetch();
				if($record["name"] !== $manufacturer)
				{
					array_push($manufacturer_list, $record["name"]);
				}
				
				$synonym_query = $db_link->prepare('SELECT `synonym` FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?;');
				$synonym_query->execute(array($shop_docpart_manufacturer_id));
				
				while($synonym_record = $synonym_query->fetch() )
				{
					if($synonym_record["synonym"] !== $manufacturer && mb_detect_encoding($synonym_record["synonym"]) != "UTF-8")
					{
						array_push($manufacturer_list, $synonym_record["synonym"]);
					}
				}
			}
		}
	}
	
	return($manufacturer_list);
}


exit(json_encode($answer));
?>