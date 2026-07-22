<?php
defined('_ASTEXE_') or die('No access');
//Скрипт для корневого раздела "Гараж"


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();//ID пользователя


$transmission = array("akpp"=>translate_str_by_id(4052), "mkpp"=>translate_str_by_id(4053), "robot"=>translate_str_by_id(4054));


//Получаем id древовидного списка с наименованием Автомобили
$tree_list_cars_id = 0;

$tree_list_cars_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_tree_lists` WHERE `caption` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN (:caption_ru, :caption_en) ) ;');
$tree_list_cars_query->bindValue(':caption_ru', 'Автомобили');
$tree_list_cars_query->bindValue(':caption_en', 'Cars');
$tree_list_cars_query->execute();
if( $tree_list_cars_query->fetchColumn() > 0 )
{
	$tree_list_cars_query = $db_link->prepare('SELECT `id` FROM `shop_tree_lists` WHERE `caption` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN (:caption_ru, :caption_en) );');
	$tree_list_cars_query->bindValue(':caption_ru', 'Автомобили');
	$tree_list_cars_query->bindValue(':caption_en', 'Cars');
	$tree_list_cars_query->execute();
	
	$tree_list_cars_record = $tree_list_cars_query->fetch();
	$tree_list_cars_id = $tree_list_cars_record["id"];
}


if( $user_id > 0)
{
	if( ! empty( $_POST["action"] ) )
	{
		// -------------------------------------------------------------------------------
		//Защита от CSRF-атак
		require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
		// -------------------------------------------------------------------------------
		
		if( $_POST["action"] == "delete_car" )
		{
			$car_id = (int)$_POST["car_id"];
			
			$delete_query = $db_link->prepare('DELETE FROM `shop_docpart_garage` WHERE `id` = :car_id AND `user_id` = :user_id;');
			$delete_query->bindValue(':car_id', $car_id);
			$delete_query->bindValue(':user_id', $user_id);
			
			
			if( ! $delete_query->execute() )
			{
				$error_message = urlencode(translate_str_by_id(4260));
				?>
				<script>
					location="<?php echo $multilang_params['lang_href']; ?>/garazh?error_message=<?php echo $error_message; ?>";
				</script>
				<?php
				exit;
			}
			else
			{
				$success_message = urlencode(translate_str_by_id(4261));
				?>
				<script>
					location="<?php echo $multilang_params['lang_href']; ?>/garazh?success_message=<?php echo $success_message; ?>";
				</script>
				<?php
				exit;
			}
		}
	}
	else//Действий нет - выводим страницу
	{
		//Для работы с пользователем
		require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
		$user_session = DP_User::getUserSession();
		$epc_g_lang = isset($multilang_params['lang_href']) ? rtrim((string)$multilang_params['lang_href'], '/') : '/en';
		?>
		<style>
		.epc-garazh-gms{margin:0 0 18px;padding:18px 20px;border-radius:16px;color:#f8fafc;background:linear-gradient(125deg,#0b1220,#164e63 55%,#0e7490);box-shadow:0 14px 32px rgba(11,18,32,.18)}
		.epc-garazh-gms h2{margin:0 0 6px;color:#fff;font-size:22px;font-weight:800}
		.epc-garazh-gms p{margin:0 0 12px;opacity:.92;max-width:60ch}
		.epc-garazh-gms a{display:inline-flex;align-items:center;gap:6px;margin:0 8px 6px 0;padding:8px 12px;border-radius:999px;background:rgba(255,255,255,.16);color:#fff!important;font-weight:700;text-decoration:none!important;border:1px solid rgba(255,255,255,.22);font-size:12px}
		.epc-garazh-gms a:hover{background:rgba(255,255,255,.28)}
		</style>
		<div class="epc-garazh-gms col-md-12">
			<h2><i class="fa fa-wrench"></i> My Garage · Service ready</h2>
			<p>Keep your vehicles here, then book workshop service or track an open job. Workshop staff use Garage Manager for the full repair workflow.</p>
			<a href="<?php echo htmlspecialchars($epc_g_lang.'/auto-workshop#book', ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-calendar"></i> Book service</a>
			<a href="<?php echo htmlspecialchars($epc_g_lang.'/auto-workshop#track', ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-search"></i> Track job</a>
			<a href="<?php echo htmlspecialchars($epc_g_lang.'/garage/login', ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-sign-in"></i> Garage Manager login</a>
		</div>
		<div class="col-md-6" style="padding-bottom:20px;">
			<a class="btn btn-ar btn-primary" href="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil"><i class="fa fa-car"></i> <i class="fa fa-plus"></i><?php echo translate_str_by_id(4262); ?></a>
			<a href="<?php echo $multilang_params['lang_href']; ?>/garazh/bloknot?garage=0" class="btn btn-ar btn-primary"><i class="fa fa-pencil-square-o"></i> <?php echo translate_str_by_id(2100); ?></a>
		</div>
		
		<div class="col-md-6" style="padding-bottom:20px;">
			<form role="form">
				<div class="input-group">
					<input value="" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(5611); ?>" id="garage_search_input" />
					<span class="input-group-btn">
						<a class="btn btn-ar btn-default" onClick="document.getElementById('garage_search_input').value=''; $('.car_div').css('display', 'block');"><i style="margin-right: 0px; color: #555;" class="fa fa-times" aria-hidden="true"></i></a>
					</span>
				</div>
			</form>
			<script>
				//Функция поиска автомобиля по гаражу
				$('#garage_search_input').keyup(function(){
					//Страка поиска
					let str = document.getElementById("garage_search_input").value;
					
					if(str.length >= 2){
						//Объект для запроса
						var request_object = new Object;
						request_object.action = 'search';
						request_object.search_str = str;
						
						//Отправляем запрос
						jQuery.ajax({
							type: "POST",
							async: true,
							url: "/content/shop/docpart/garage/ajax_operations_cars.php",
							dataType: "json",//Тип возвращаемого значения
							data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
							success: function(answer)
							{
								//console.log(answer);
								if(answer.status == true){
									$('.car_div').css('display', 'none');
									if(answer.list){
										if(answer.list.length > 0){
											for(let i=0; i < answer.list.length; i++)
											{
												$('#car_' + answer.list[i]).css('display', 'block');
											}
										}else{
											//Нет совпадений
										}
									}else{
										$('.car_div').css('display', 'block');
									}
								}else{
									$('.car_div').css('display', 'block');
								}
							},
							error: function (e, ajaxOptions, thrownError){
								$('.car_div').css('display', 'block');
							}
						});
					}else{
						$('.car_div').css('display', 'block');
					}
				});
				
				//Выбор основного автомобиля
				function active_car(el, car_id)
				{
					//Объект для запроса
					var request_object = new Object;
					request_object.action = 'active_car';
					request_object.user_id = '<?=$user_id;?>';
					request_object.car_id = car_id;
					
					//Отправляем запрос
					jQuery.ajax({
						type: "POST",
						async: true,
						url: "/content/shop/docpart/garage/ajax_operations_cars.php",
						dataType: "json",//Тип возвращаемого значения
						data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
						success: function(answer)
						{
							if(answer.status == false){
								alert('<?php echo translate_str_by_id(2122); ?>: '+answer.message);
							}else{
								if($(el).hasClass('btn-success')){
									$('.active_car').removeClass('btn-success');
								}else{
									$('.active_car').removeClass('btn-success');
									$(el).addClass('btn-success');
								}
							}
						}
					});
				}
			</script>
		</div>
		
		
		
		
		
		<div class="col-md-12">
			
			<form name="delete_car_form" method="POST">
				<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
				<input type="hidden" name="action" value="delete_car" />
				<input type="hidden" name="car_id" id="car_id_to_delete" value="" />
			</form>
			<script>
			//Удаление автомобиля
			function delete_car(car_id)
			{
				if( !confirm("<?php echo translate_str_by_id(4263); ?>") )
				{
					return;
				}
				
				document.getElementById("car_id_to_delete").value = car_id;
				document.forms["delete_car_form"].submit();
			}
			</script>
			
			
			
			<?php
			//Технические данные для работы с заказами
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

			//Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
			$WHERE_statuses_not_count = "";
			$WHERE_statuses_not_count_without_and = "";
			for($i=0; $i<count($orders_items_statuses_not_count); $i++)
			{
				$WHERE_statuses_not_count .= " AND `status` != ".(int)$orders_items_statuses_not_count[$i];
				
				if($i > 0)$WHERE_statuses_not_count_without_and .= " AND ";
				$WHERE_statuses_not_count_without_and .= " `status` != ".(int)$orders_items_statuses_not_count[$i];
			}
			
			//Для подсчета суммы оплаты по заказу
			$INCOME_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = `shop_orders`.`id` AND `order_id` > 0), 0)";
			$ISSUE_SQL = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = `shop_orders`.`id` AND `order_id` > 0), 0)";
			
			
			
			// -----------------
			
			
			
			$cars_query = $db_link->prepare('SELECT * FROM `shop_docpart_garage` WHERE `user_id` = :user_id ORDER BY `active` DESC, `caption` ASC;');
			$cars_query->bindValue(':user_id', $user_id);
			$cars_query->execute();
			while( $car = $cars_query->fetch() )
			{
				//Считаем статистику по автомобилю
					$SQL_SELECT_TOTAL_INDICATORS = "SELECT 
					COUNT(*) AS `orders_count`, 
					SUM( CAST( (SELECT SUM(`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2) ) ) AS `items_sum_total`, 
					SUM( CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` $WHERE_statuses_not_count) AS DECIMAL(20,2) ) ) AS `price_sum_total`, 
					CAST( ($ISSUE_SQL - $INCOME_SQL) AS DECIMAL(20,2) ) AS `paid_sum` 
					FROM `shop_orders` WHERE `id` IN(SELECT `order_id` FROM `shop_docpart_garage_orders` WHERE `garage_id` = ?) GROUP BY `id`;";
					
					$total_indicators_query = $db_link->prepare($SQL_SELECT_TOTAL_INDICATORS);
					$total_indicators_query->execute(array($car["id"]));
					$total_indicators_cars = $total_indicators_query->fetch();
				?>
				<div id="car_<?php echo $car["id"]; ?>" class="panel panel-default car_div">
					<div class="panel-heading">
					<?php 
							if($car["active"] == 1)
							{
							?>
								<a class="btn btn-ar btn-default btn-success pull-right active_car" onclick="active_car(this, <?php echo $car['id']; ?>);"><i class="fa fa-check-square-o" aria-hidden="true"></i><?php echo translate_str_by_id(4993); ?></a>
							<?php
							}else{
							?>
							<a class="btn btn-ar btn-default pull-right active_car" onclick="active_car(this, <?php echo $car['id']; ?>);"><i class="fa fa-check-square-o" aria-hidden="true"></i><?php echo translate_str_by_id(4993); ?></a>
							<?php
							}
						?>
						<h3 class="panel-title" style="font-weight:400;" ><?php echo $car["caption"]; ?></h3>
					</div>
					<div class="panel-body">
						
						<div class="row">
							<div class="col-md-8">
								<div>
									<b><?php echo $car["marka"] .' '. $car["model"]; ?></b>
								</div>
								
								<small><?php echo ($car["vin"]!=="") ? $car["vin"]:$car["frame"]; ?></small>
								
								<table class="table">
									<tr>
										<td><small><?php echo translate_str_by_id(2047); ?>:</small> <?php echo $car["body_type"]; ?></td>
										<td><small><?php echo translate_str_by_id(5575); ?>:</small> <?php echo $car["engine_value"]; ?></td>
									</tr>
									<tr>
										<td><small><?php echo translate_str_by_id(4240); ?>:</small> <?php echo ($car["fuel_type"]=="gas") ? translate_str_by_id(4264) : translate_str_by_id(4265); ?></td>
										<td><small><?php echo translate_str_by_id(4246); ?>:</small> <?php echo $transmission[$car["transmission"]]; ?></td>
									</tr>
									<tr>
										<td><small><?php echo translate_str_by_id(4247); ?>:</small> <?php echo ($car["wheel"]=="left") ? translate_str_by_id(4248) : translate_str_by_id(4249); ?></td>
										<td><small><?php echo translate_str_by_id(3650); ?>:</small> <?php echo $car["color"]; ?></td>
									</tr>
									<tr>
										<td><small><?php echo translate_str_by_id(4266); ?>:</small> <?php echo $car["country"]; ?></td>
										<td><small><?php echo translate_str_by_id(4044); ?>:</small> <?php echo $car["year"]; ?></td>
									</tr>
									<tr>
										<td colspan="2"><small><?php echo translate_str_by_id(4251); ?>:</small> <?php echo $car["note"]; ?></td>
									</tr>
								</table>
							</div>
							
							<div class="col-md-4 text-center">
								<span class="block_image">
									
									<?php
									$img_src = "";
									$car_id = $car["id"];
									if( $car_id > 0 )
									{
										if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/garage/".$car_id.".jpg") )
										{
											$img_src = "/content/files/images/garage/".$car_id.".jpg?refresh=".time();
										}
										else if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/images/garage/".$car_id.".png") )
										{
											$img_src = "/content/files/images/garage/".$car_id.".png?refresh=".time();
										}
									}
									?>
								
									<img src="<?php echo $img_src; ?>" onerror="this.src='/content/files/images/no_image.png'">
								</span>
								
								<div class="text-left" style="margin-top:10px;">
									<small>
										<table class="table">
											<tr>
												<td><?php echo translate_str_by_id(766); ?>:</td>
												<td><?php echo (int) $total_indicators_cars['orders_count']; ?></td>
												<td><?php echo translate_str_by_id(3251); ?>:</td>
												<td><?php echo number_format($total_indicators_cars['price_sum_total'], 2, ',', ' '); ?></td>
											</tr>
											<tr>
												<td><?php echo translate_str_by_id(4379); ?>:</td>
												<td><?php echo number_format($total_indicators_cars['paid_sum'], 2, ',', ' '); ?></td>
												<td><?php echo translate_str_by_id(5563); ?>:</td>
												<td><?php echo number_format($total_indicators_cars['price_sum_total'] - $total_indicators_cars['paid_sum'], 2, ',', ' '); ?></td>
											</tr>
										</table>
									</small>
								</div>
								
							</div>
						</div>
						
						
						
						<div class="row">
							<div class="col-md-12" style="padding:10px;">
							
								<a class="btn btn-ar btn-primary" href="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>"><i class="fa fa-car"></i> <i class="fa fa-pencil"></i> <?php echo translate_str_by_id(2270); ?></a>
								<a class="btn btn-ar btn-danger" href="javascript:void(0);" onclick="delete_car(<?php echo $car_id; ?>);"><i class="fa fa-car"></i> <i class="fa fa-trash"></i><?php echo translate_str_by_id(2224); ?></a>
								
								<hr>
								
								
								
								<a href="<?php echo $multilang_params['lang_href']; ?>/garazh/bloknot?garage=<?=$car["id"];?>" class="btn btn-ar btn-default btn_margin"><i class="fa fa-pencil-square-o"></i> <?php echo translate_str_by_id(4270); ?></a>
								
								<a href="<?php echo $multilang_params['lang_href']; ?>/shop/orders?garage=<?=$car["id"];?>" class="btn btn-ar btn-default btn_margin"><i class="fa fa-shopping-bag"></i> <?php echo translate_str_by_id(766); ?></a>
								
								
								
								<a class="btn btn-ar btn-default btn_margin" href="javascript:void(0);" onclick="request_to_seller(<?php echo $car["id"]; ?>);"><i class="fa fa-rocket"></i> <?php echo translate_str_by_id(4115); ?></a>
								<script>
								//Переход на запрос продавцу
								function request_to_seller(car_id)
								{
									//Куки ставим на минуту
									var date = new Date(new Date().getTime() + 60 * 1000);
									document.cookie = "seller_request="+car_id+"; path=/; expires=" + date.toUTCString();
									
									location = "<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu";
								}
								</script>
								
								
								
								<?php
								//Проверим подключен ли к сайту Специальный поиск, если нет то кнопку показывать нет смысла
								$special_searches_query = $db_link->prepare('SELECT `id` FROM `shop_special_searches` WHERE `active` = 1 LIMIT 1;');
								$special_searches_query->execute();
								$special_searches = $special_searches_query->fetch();
								if($special_searches['id'] > 0){
								?>
									<script>
									<?php
									$max_value = 0;
									$car_tree_list_json = json_decode($car["car_tree_list_json"], true);
									
									//Находим id последнего узла в древовидном списке
									foreach($car_tree_list_json AS $key => $value)
									{
										if($value != 0)
										{
											$max_value = $value;
										}
									}
									?>
									//Переход на поиск в собственном каталоге товаров
									function search_in_own_catalogue_<?php echo $car["id"]; ?>()
									{
										console.log(<?php echo $max_value; ?>);
										
										document.cookie = "sp_tl_<?php echo $tree_list_cars_id; ?>=<?php echo $max_value; ?>; path=/;";
										
										location="<?php echo $multilang_params['lang_href']; ?>/shop/search_products?search_type=garage";
									}
									</script>
									<a class="btn btn-ar btn-default btn_margin" href="javascript:void(0);" onclick="search_in_own_catalogue_<?php echo $car["id"]; ?>();"><i class="fa fa-cubes"></i> <?php echo translate_str_by_id(4269); ?></a>
								<?php
								}
								?>
								
								
								
								<br/>
								
								
								
								<?php
								//Здесь выводим функции для поиска товаров по каталогам
								
								$vin_frame = '';
								if(!empty($car["vin"])){
									$vin_frame = $car["vin"];
								}
								if(!empty($car["frame"])){
									$vin_frame = $car["frame"];
								}
								if(!empty($vin_frame)){
									?>
									
									<a class="btn btn-ar btn-default btn_margin" href="<?php echo $multilang_params['lang_href']; ?>/originalnye-katalogi?vin=<?php echo $vin_frame; ?>&VinAction=Search&language=ru"><i class="fa fa-search"></i> <?php echo translate_str_by_id(1731); ?></a>
									
									<?php
								}
								
								
								
								// -------------------------------------------------------------------------------------------
								
								//1. Каталог ТО
								$to_link = "";
								$to_json = json_decode($car["to_json"], true);
								if( $to_json["to_mark"] > 0 )
								{
									$to_link = "/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya/vybor-modeli?car_id=".$to_json["to_mark"]."&car_name=".urlencode(strtoupper($car["mark"]));
									
									
									if($to_json["to_model"] > 0)
									{
										$car_name = urlencode(strtoupper($car["mark"]));
										$model_caption = urlencode($car["model"]);
										$img = "";
										
										//Получаем список моделей выбранной марки через веб-сервис каталога, чтобы получить необходимые строки
										$curl = curl_init();
										curl_setopt($curl, CURLOPT_URL, "http://ucats.ru/ucats/to/get_car_models.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&car_id=".$to_json["to_mark"]);
										curl_setopt($curl, CURLOPT_HEADER, 0);
										curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
										curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
										curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
										$curl_result = curl_exec($curl);
										curl_close($curl);
										$curl_result = json_decode($curl_result, true);
										
										if($curl_result["status"] == "ok")
										{
											for($i=0; $i < count($curl_result["list"]); $i++)
											{
												if($curl_result["list"][$i]["id"] == $to_json["to_model"])
												{
													$model_caption = urlencode($curl_result["list"][$i]["title"]." ".$curl_result["list"][$i]["content"]);
													$img = urlencode($curl_result["list"][$i]["img"]);
													break;
												}
											}
										}
										
										$to_link = "/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya/vybor-modeli/vybor-komplektacii?car_id=".$to_json["to_mark"]."&model_id_to=".$to_json["to_model"]."&model_caption=$model_caption&car_name=$car_name&img=$img";
										
										
										if($to_json["to_model_types"] > 0)
										{
											$type_id = $to_json["to_model_types"];
											$type_caption = "";
											
											//Получаем список моделей выбранной марки через веб-сервис каталога
											$curl = curl_init();
											curl_setopt($curl, CURLOPT_URL, "http://ucats.ru/ucats/to/get_types.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&model_id=".$to_json["to_model"]);
											curl_setopt($curl, CURLOPT_HEADER, 0);
											curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
											curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
											curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
											$curl_result = curl_exec($curl);
											curl_close($curl);
											$curl_result = json_decode($curl_result, true);

											if($curl_result["status"] == "ok")
											{
												for($i=0; $i < count($curl_result["list"]); $i++)
												{
													if($curl_result["list"][$i]["id"] == $type_id)
													{
														$engine = $curl_result["list"][$i];
														$engine_name = $engine["name"]." ".$engine["engine_model"];
														$engine_horse = $engine["engine_horse"]." л.с.";
														$engine_fuel = $engine["engine"];
														$engine_type_year = $engine["type_year"];
														
														$type_caption = urlencode($engine_name." ".$engine_horse." ".$engine_fuel." ".$engine_type_year);
														break;
													}
												}
											}
											
											
											
											$to_link = "/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya/vybor-modeli/vybor-komplektacii/spisok-zapchastej?car_id=".$to_json["to_mark"]."&model_id_to=".$to_json["to_model"]."&model_caption=$model_caption&car_name=$car_name&type_id=$type_id&type_caption=$type_caption&img=$img";
										}
									}
									
									
									?>
									
									<a class="btn btn-ar btn-default btn_margin" href="<?php echo $to_link; ?>"><i class="fa fa-wrench"></i> <?php echo translate_str_by_id(885); ?></a>
									
									<?php
									
								}
								
								
								
								// -------------------------------------------------------------------------------------------
								
								//2. Поиск по каталогу AutoXP
								$parts_catalogues_query = $db_link->prepare('SELECT * FROM `shop_docpart_search_tabs` WHERE `name` = :name;');
								$parts_catalogues_query->bindValue(':name', 'parts_catalogues');
								$parts_catalogues_query->execute();
								$parts_catalogues_record = $parts_catalogues_query->fetch();
								$parts_catalogues_parameters = json_decode($parts_catalogues_record["parameters_values"], true);
								
								if( (int)$parts_catalogues_parameters["autoxp_id"] > 0 )
								{
									$autoxp_id = (int)$parts_catalogues_parameters["autoxp_id"];
									
									//Если админ указал такую марку для показа
									if( array_search($car["mark_id"], $parts_catalogues_parameters["autoxp_show_cars"]) !== 0 )
									{
										//Получаем ссылку для AutoXP
										$href_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars_catalogue_links` WHERE `catalogue_id` = 1 AND `car_id` = :car_id;');
										$href_query->bindValue(':car_id', $car["mark_id"]);
										$href_query->execute();
										$href_record = $href_query->fetch();
										
										?>
										
										<a class="btn btn-ar btn-default btn_margin" href="javascript:void(0);" onclick="autoxp_redirect('<?php echo $href_record["href"].$autoxp_id; ?>');"><i class="fa fa-wrench"></i> <?php echo translate_str_by_id(4201); ?> AutoXP</a>
										
										<script>
										//Переход на autoxp
										function autoxp_redirect(dir)
										{
											//Сама проверка
											jQuery.ajax({
												type: "GET",
												async: false, //Запрос синхронный
												url: "/autoxp_clicks_control.php"+"?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
												dataType: "json",//Тип возвращаемого значения
												success: function(answer)
												{
													if(answer == 0)
													{
														alert("<?php echo translate_str_by_id(4183); ?>");
														location.reload();
													}
													else
													{
														location = dir;
													}
												}
											});
										}
										</script>
										<?php
									}
								}
								
								
								
								
								// -------------------------------------------------------------------------------------------
								
								//3. Поиск по каталогу ilcats
								if( (int)$parts_catalogues_parameters["ilcats_clid"] > 0 )
								{
									$ilcats_clid = (int)$parts_catalogues_parameters["ilcats_clid"];
									
									//Если такая марка указана админом, как показывемая
									if( (int)$parts_catalogues_parameters["ilcats_car_".$car["mark_id"]] != 0 )
									{
										//Получаем ссылку для neoriginal
										$href_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars_catalogue_links` WHERE `catalogue_id` = :catalogue_id AND `car_id` = :car_id;');
										$href_query->bindValue(':catalogue_id', 2);
										$href_query->bindValue(':car_id', $car["mark_id"]);
										$href_query->execute();
										$href_record = $href_query->fetch();
										
										$href_record = $href_record["href"];
										
										$href_record = str_replace("<clid>", $ilcats_clid, $href_record);
										$href_record = str_replace("<pid>", $parts_catalogues_parameters["ilcats_car_".$car["mark_id"]], $href_record);
										
										?>
										
										<a class="btn btn-ar btn-default btn_margin" href="<?php echo $href_record; ?>"><i class="fa fa-wrench"></i> <?php echo translate_str_by_id(4201); ?> neoriginal.ru</a>
										
										<?php
									}
								}
								
								
								
								// -------------------------------------------------------------------------------------------
								
								//4. Поиск по каталогу catalogs-parts.com
								if( $parts_catalogues_parameters["catalogs_parts_com_id"] != "" )
								{
									//Если такая марка указана админом, как показывемая
									if( array_search($car["mark_id"], $parts_catalogues_parameters["catalogs_parts_com_show_cars"]) !== 0 )
									{
										//Получаем ссылку для AutoXP
										$href_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars_catalogue_links` WHERE `catalogue_id` = :catalogue_id AND `car_id` = :car_id;');
										$href_query->bindValue(':catalogue_id', 3);
										$href_query->bindValue(':car_id', $car["mark_id"]);
										$href_query->execute();
										$href_record = $href_query->fetch();
										
										$href_record = $href_record["href"];
										$href_record = str_replace("client:;", "client:".$parts_catalogues_parameters["catalogs_parts_com_id"].";", $href_record);
										
										?>
										
										<a class="btn btn-ar btn-default btn_margin" href="<?php echo $href_record; ?>"><i class="fa fa-wrench"></i> <?php echo translate_str_by_id(4201); ?> catalogs-parts.com</a>
										
										<?php
									}
									
									//Для поиска по VIN
									if( true )
									{
										//Получаем ссылку для AutoXP
										$href_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars_catalogue_links` WHERE `catalogue_id` = :catalogue_id AND `car_id` = :car_id;');
										$href_query->bindValue(':catalogue_id', 3);
										$href_query->bindValue(':car_id', $car["mark_id"]);
										$href_query->execute();
										$href_record = $href_query->fetch();
										
										$href_record = $href_record["href"];
										$car_subdomain = explode(".", $href_record);
										$car_subdomain = explode("//", $car_subdomain[0]);
										$car_subdomain = $car_subdomain[1];

										switch($car_subdomain){
											case 'kia' :
												$catalog = 'catalog:eur;';
											break;
											default : $catalog = ''; break;
										}

										$href = "http://$car_subdomain.catalogs-parts.com/#{client:".$parts_catalogues_parameters["catalogs_parts_com_id"].";page:vin;lang:ru;".$catalog."vin:".$car["vin"]."}";

										
										?>
										
										<a class="btn btn-ar btn-default btn_margin" href="<?php echo $href; ?>"><i class="fa fa-wrench"></i> VIN catalogs-parts.com</a>
										
										<?php
									}
									
									
									//Для aftermarket
									?>
									
									<a class="btn btn-ar btn-default btn_margin" href="https://aftermarket.catalogs-parts.com/#{client:<?php echo $parts_catalogues_parameters["catalogs_parts_com_id"]; ?>;page:models;lang:ru;catalog:pc}"><i class="fa fa-wrench"></i> Aftermarket</a>
									
									<?php
								}
								
								
								
								// -------------------------------------------------------------------------------------------
								
								//UCatalog
								$UCatalog_json = json_decode($car["UCatalog_json"], true);
								if(!empty($UCatalog_json)){
								?>
									
									<a class="btn btn-ar btn-default btn_margin" href="<?php echo $multilang_params['lang_href']; ?>/?UCatalog_get_garage=<?php echo $car["id"]; ?>"><i class="fa fa-wrench"></i> UCatalog</a>
									
								<?php
								}
								
								
								
								// -------------------------------------------------------------------------------------------
								
								?>
								
							</div>
						</div>
						
						
						
					</div>
				</div>
				<?php
			}
			?>
		</div>
		<?php
	}
}
else
{
	?>
	<div class="col-md-12">
		<p><?php echo translate_str_by_id(4271); ?></p>
		<div class="panel panel-primary">
		<?php
		//Единый механизм формы авторизации
		$login_form_postfix = "garage";
		require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
		?>
		</div>
	</div>
	<?php
}
?>





<style>
	.btn_margin
	{
		margin-bottom:5px;
		margin-right: 2px;
	}
	.car_div
	{
		margin-bottom:100px;
	}
<?php
if( strtolower($DP_Template->name) == "limo" )
{
	?>
	.btn-default
	{
		color:#FFF!important;
	}
	<?php
}
?>
</style>