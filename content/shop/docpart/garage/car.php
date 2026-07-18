<?php
defined('_ASTEXE_') or die('No access');
//Скрипт для создания и редактирования одного автомобиля


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();//ID пользователя



if( !empty($_POST["save_action"]) && $user_id > 0 )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Получаем данные:
	$car_id = (int)$_POST["car_id"];
	$caption = htmlentities(trim($_POST["caption"]));
	$mark_id = (int)$_POST["mark_id"];
	$marka = htmlentities(mb_strtoupper(trim($_POST["marka"]), "UTF-8"));
	$model = htmlentities(mb_strtoupper(trim($_POST["model"]), "UTF-8"));
	$year = (int)$_POST["year"];
	$body_type = htmlentities($_POST["body_type"]);
	$engine_value = htmlentities($_POST["engine_value"]);
	$fuel_type = htmlentities($_POST["fuel_type"]);
	$vin = htmlentities(mb_strtoupper(trim($_POST["vin"]), "UTF-8"));
	$frame = htmlentities(mb_strtoupper(trim($_POST["frame"]), "UTF-8"));
	$color = htmlentities($_POST["color"]);
	$country = htmlentities($_POST["country"]);
	$wheel = htmlentities($_POST["wheel"]);
	$transmission = htmlentities($_POST["transmission"]);
	$note = htmlentities($_POST["note"]);
	
	
	
	//Формируем JSON-запись привязки к каталогу ТО
	$to_json = array();
	$to_json["to_mark"] = (int)$_POST["to_mark"];
	$to_json["to_model"] = (int)$_POST["to_model"];
	$to_json["to_model_types"] = (int)$_POST["to_model_types"];
	$to_json = json_encode($to_json);
	
	
	//Формируем JSON-запись привязки к древовидному списку автомобилей
	$car_tree_list_json = array();
	$car_tree_list_level = 1;
	while( !empty( $_POST["car_tree_list_level_".$car_tree_list_level] ) )
	{
		$car_tree_list_json["level_" .$car_tree_list_level] = (int)$_POST["car_tree_list_level_".$car_tree_list_level];
		$car_tree_list_level++;
	}
	$car_tree_list_json = json_encode($car_tree_list_json);
	
	
	
	if( $car_id == 0 )
	{
		//Идет создание нового автомобиля
		$SQL_INSERT = 'INSERT INTO `shop_docpart_garage` (`caption`,`mark_id`,`marka`,`model`,`year`,`body_type`,`engine_value`,`fuel_type`,`vin`,`frame`,`color`,`country`,`wheel`,`transmission`,`note`,`to_json`,`car_tree_list_json`,`user_id`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);';
		
		$binding_values = array($caption, $mark_id, $marka, $model, $year, $body_type, $engine_value, $fuel_type, $vin, $frame, $color, $country, $wheel, $transmission, $note, $to_json, $car_tree_list_json, $user_id);
		$insert_query = $db_link->prepare($SQL_INSERT);
		
		
		//1. Добавляем учетную запись автомобиля и получаем id
		if( ! $insert_query->execute($binding_values) )
		{
			//Выдаем ошибку создания нового автомобиля
			$error_message = translate_str_by_id(4226);
			?>
			<script>
				location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$car_id = $db_link->lastInsertId();
			
			
			//2. Сохраняем изображение под именем <id>.<расширение>
			$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/garage/";
			$FILE_POST = $_FILES["car_img"];
			if( $FILE_POST["size"] > 0 )
			{
				//Получаем файл:
				$fileName = $FILE_POST["name"];
				
				//Получаем расширение файла
				$file_extension = explode(".", $fileName);
				
				//Недопустимы двойные расширения и файлы без расширений
				if( count($file_extension) > 2 || count($file_extension) == 1 )
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4227));
					$warning_message = urlencode(translate_str_by_id(4228));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>&warning_message=<?php echo $warning_message; ?>";
					</script>
					<?php
					exit;
				}
				
				$file_extension = strtolower($file_extension[count($file_extension)-1]);
				//Имя файла будет вида <id элемента>.$file_extension
				$saved_file_name = $car_id.".".$file_extension;
				
				if($file_extension !== "jpg" && $file_extension !== "png")
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4227));
					$warning_message = urlencode(translate_str_by_id(4229));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>&warning_message=<?php echo $warning_message; ?>";
					</script>
					<?php
					exit;
				}
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if (copy($FILE_POST['tmp_name'], $uploadfile))
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4230));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>";
					</script>
					<?php
					exit;
				} 
				else 
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4227));
					$warning_message = urlencode(translate_str_by_id(4231));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>&warning_message=<?php echo $warning_message; ?>";
					</script>
					<?php
					exit;
				}
			}
			else
			{
				//Выдаем ошибку создания нового автомобиля
				$success_message = urlencode(translate_str_by_id(4230));
				?>
				<script>
					location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>";
				</script>
				<?php
				exit;
			}
		}
	}
	else
	{
		//Идет редактирование существующего автомобиля
		//1. Обновляем учетную запись автомобиля
		$binding_values = array($caption, $mark_id, $marka, $model, $year, $body_type, $engine_value, $fuel_type, $vin, $frame, $color, $country, $wheel, $transmission, $note, $to_json, $car_tree_list_json, $car_id, $user_id);
		$update_query = $db_link->prepare('UPDATE `shop_docpart_garage` SET `caption` = ?,`mark_id` = ?,`marka` = ?,`model` = ?,`year` = ?,`body_type` = ?,`engine_value` = ?,`fuel_type` = ?,`vin` = ?,`frame` = ?,`color` = ?,`country` = ?,`wheel` = ?,`transmission` = ?,`note` = ?,`to_json` = ?,`car_tree_list_json` = ? WHERE `id` = ? AND `user_id` = ?;');
		
		
		if( $update_query->execute($binding_values) == true )
		{
			//2. Обновляем изображение
			$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/garage/";
			$FILE_POST = $_FILES["car_img"];
			if( $FILE_POST["size"] > 0 )
			{
				//Получаем файл:
				$fileName = $FILE_POST["name"];
				
				//Получаем расширение файла
				$file_extension = explode(".", $fileName);
				
				//Недопустимы двойные расширения и файлы без расширений
				if( count($file_extension) > 2 || count($file_extension) == 1 )
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4232));
					$warning_message = urlencode(translate_str_by_id(4228));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>&warning_message=<?php echo $warning_message; ?>";
					</script>
					<?php
					exit;
				}
				
				$file_extension = strtolower($file_extension[count($file_extension)-1]);
				//Имя файла будет вида <id элемента>.$file_extension
				$saved_file_name = $car_id.".".$file_extension;
				
				if($file_extension !== "jpg" && $file_extension !== "png")
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4232));
					$warning_message = urlencode(translate_str_by_id(4229));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>&warning_message=<?php echo $warning_message; ?>";
					</script>
					<?php
					exit;
				}
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if (copy($FILE_POST['tmp_name'], $uploadfile))
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4233));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>";
					</script>
					<?php
					exit;
				} 
				else 
				{
					//Выдаем ошибку создания нового автомобиля
					$success_message = urlencode(translate_str_by_id(4232));
					$warning_message = urlencode(translate_str_by_id(4231));
					?>
					<script>
						location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>&warning_message=<?php echo $warning_message; ?>";
					</script>
					<?php
					exit;
				}
			}
			else
			{
				//Выдаем ошибку создания нового автомобиля
				$success_message = urlencode(translate_str_by_id(4233));
				?>
				<script>
					location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&success_message=<?php echo $success_message; ?>";
				</script>
				<?php
				exit;
			}
		}
		else
		{
			//Выдаем ошибку редактирования автомобиля
			$error_message = translate_str_by_id(4234);
			?>
			<script>
				location="<?php echo $multilang_params['lang_href']; ?>/garazh/avtomobil?car_id=<?php echo $car_id; ?>&error_message=<?php echo $error_message; ?>";
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
	
	if( $user_id == 0 )
	{
		?>
		
		<p><?php echo translate_str_by_id(4235); ?></p>
    
	
		<div class="panel panel-primary">
		<?php
		//Единый механизм формы авторизации
		$login_form_postfix = "my_orders";
		require($_SERVER["DOCUMENT_ROOT"]."/modules/login/login_form_general.php");
		?>
		</div>
		
		<?php
	}
	else
	{
		//Исходные данные:
		$car_id = 0;
		$caption = "";
		$mark_id = 0;
		$marka = "";
		$model = "";
		$year = "";
		$body_type = "";
		$engine_value = "";
		$fuel_type = "gas";
		$vin = "";
		$frame = "";
		$color = "";
		$country = "";
		$wheel = "left";
		$transmission = "mkpp";
		$note = "";
		
		$to_json = array();
		$car_tree_list_json = array();
		
		if( !empty( $_GET["car_id"] ) )
		{
			$car_id = (int)$_GET["car_id"];
			
			
			$car_query = $db_link->prepare('SELECT * FROM `shop_docpart_garage` WHERE `id` = :car_id AND `user_id` = :user_id;');
			$car_query->bindValue(':car_id', $car_id);
			$car_query->bindValue(':user_id', $user_id);
			$car_query->execute();
			$car_record = $car_query->fetch();
			
			
			$caption = $car_record["caption"];
			$mark_id = $car_record["mark_id"];
			$marka = $car_record["marka"];
			$model = $car_record["model"];
			$year = $car_record["year"];
			$body_type = $car_record["body_type"];
			$engine_value = $car_record["engine_value"];
			$fuel_type = $car_record["fuel_type"];
			$vin = $car_record["vin"];
			$frame = $car_record["frame"];
			$color = $car_record["color"];
			$country = $car_record["country"];
			$wheel = $car_record["wheel"];
			$transmission = $car_record["transmission"];
			$note = $car_record["note"];
			
			
			$to_json = json_decode($car_record["to_json"], true);
			$car_tree_list_json = json_decode($car_record["car_tree_list_json"], true);
		}
		
		?>
		<form name="save_form" method="POST" enctype="multipart/form-data">
			
			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
			<input type="hidden" name="save_action" value="save_action" />
			<input type="hidden" name="car_id" value="<?php echo $car_id; ?>" />
		
			<div class="panel panel-primary">
				<div class="panel-heading"><?php echo translate_str_by_id(4236); ?></div>
				<div class="panel-body">
					  <div class="form-group col-md-6">
						<label for="exampleInputEmail1"><?php echo translate_str_by_id(4237); ?></label>
						<input value="<?php echo $caption; ?>" name="caption" id="caption" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4238); ?>">
					  </div>
					  

					  <div class="form-group col-md-6 hidden">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(2085); ?></label>

						
						<select name="mark_id" class="form-control" >
							<option value="0"><?php echo translate_str_by_id(4239); ?></option>
							<?php
							$shop_docpart_cars_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars` ORDER BY `caption`;');
							$shop_docpart_cars_query->execute();
							while( $shop_docpart_cars_record = $shop_docpart_cars_query->fetch() )
							{
								$selected = "";
								if( $mark_id == $shop_docpart_cars_record["id"] )
								{
									$selected = " selected=\"selected\" ";
								}
								
								?>
								<option value="<?php echo $shop_docpart_cars_record["id"]; ?>" <?php echo $selected; ?>><?php echo strtoupper(translate_str_by_id($shop_docpart_cars_record["caption"])); ?></option>
								<?php
							}
							?>
						</select>
					  </div>
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(2085); ?></label>
						<input value="<?php echo $marka; ?>" name="marka" id="marka" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2318); ?>, BMW">
					  </div>
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(2086); ?></label>
						<input value="<?php echo $model; ?>" name="model" id="model" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2318); ?>, 5 Series">
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo mb_convert_case(translate_str_by_id(4060), MB_CASE_TITLE); ?></label>
						<input value="<?php echo $year; ?>" name="year" id="year" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2318); ?>, 2010">
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo mb_convert_case(translate_str_by_id(4047), MB_CASE_TITLE); ?></label>
						<input value="<?php echo $body_type; ?>" name="body_type" id="body_type" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2318); ?>, <?php echo translate_str_by_id(4062); ?>">
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo mb_convert_case(translate_str_by_id(4045), MB_CASE_TITLE); ?></label>
						<input value="<?php echo $engine_value; ?>" name="engine_value" id="engine_value" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(2318); ?>, 1.6">
					  </div>
					  
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(4240); ?></label>
						<select name="fuel_type" id="fuel_type" class="form-control">
							
							<?php
							$gas_selected = "";
							$disel_selected = "";
							if( $fuel_type === "gas" )
							{
								$gas_selected = " selected=\"selected\"";
								$disel_selected = "";
							}
							else
							{
								$gas_selected = "";
								$disel_selected = " selected=\"selected\"";
							}
							?>
							
							<option value="gas" <?php echo $gas_selected; ?>><?php echo translate_str_by_id(4241); ?></option>
							<option value="disel"  <?php echo $disel_selected; ?>><?php echo translate_str_by_id(4242); ?></option>
						  </select>
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1">VIN</label>
						<input value="<?php echo $vin; ?>" name="vin" id="vin" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4243); ?>">
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1">FRAME</label>
						<input value="<?php echo $frame; ?>" name="frame" id="frame" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4244); ?>">
					  </div>
					  
					  
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo mb_convert_case(translate_str_by_id(3650), MB_CASE_TITLE); ?></label>
						<input value="<?php echo $color; ?>" name="color" id="color" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4245); ?>">
					  </div>
					  
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo mb_convert_case(translate_str_by_id(4266), MB_CASE_TITLE); ?></label>
						<input value="<?php echo $country; ?>" name="country" id="country" type="text" class="form-control" placeholder="<?php echo translate_str_by_id(4246); ?>">
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(4247); ?></label>
						<select name="wheel" id="wheel" class="form-control">
							
							<?php
							$left_selected = "";
							$right_selected = "";
							if( $wheel === "left" )
							{
								$left_selected = " selected=\"selected\"";
								$right_selected = "";
							}
							else
							{
								$left_selected = "";
								$right_selected = " selected=\"selected\"";
							}
							?>
							
							<option value="left" <?php echo $left_selected; ?>><?php echo translate_str_by_id(4248); ?></option>
							<option value="right" <?php echo $right_selected; ?>><?php echo translate_str_by_id(4249); ?></option>
						  </select>
					  </div>
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(4250); ?></label>
						<select name="transmission" id="transmission" class="form-control">
							
							<?php
							$akpp_selected = "";
							$mkpp_selected = "";
							$robot_selected = "";
							if( $transmission === "akpp" )
							{
								$akpp_selected = " selected=\"selected\"";
								$mkpp_selected = "";
								$robot_selected = "";
							}
							else if($transmission === "mkpp")
							{
								$akpp_selected = "";
								$mkpp_selected = " selected=\"selected\"";
								$robot_selected = "";
							}
							else
							{
								$akpp_selected = "";
								$mkpp_selected = "";
								$robot_selected = " selected=\"selected\"";
							}
							?>
						
						
							<option value="akpp" <?php echo $akpp_selected; ?>><?php echo translate_str_by_id(4052); ?></option>
							<option value="mkpp" <?php echo $mkpp_selected; ?>><?php echo translate_str_by_id(4053); ?></option>
							<option value="robot" <?php echo $robot_selected; ?>><?php echo translate_str_by_id(4054); ?></option>
						  </select>
					  </div>
					  
					  
					  
					  
					  <div class="form-group col-md-6">
						<label for="exampleInputPassword1"><?php echo translate_str_by_id(4251); ?></label>
						<textarea value="<?php echo $value; ?>" name="note" id="note" class="form-control" rows="5"><?php echo $note; ?></textarea>
					  </div>
					  

					  <div class="form-group col-md-12 text-center">
						
						<input name="car_img" type="file" id="car_img" style="display:none;" onchange="onFileChanged();" />

						<label for="" class="col-lg-12 control-label"><?php echo translate_str_by_id(4252); ?></label>
						
						
						<?php
						$img_src = "";
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
						
						<img onerror = "this.src = '<?php echo "/content/files/images/no_image.png"; ?>'" src="<?php echo $img_src; ?>" style="max-width:96px; max-height:96px; cursor:pointer;" id="image_preshow" onclick="document.getElementById('car_img').click();" />
						
						<script>
						//Обработка изменения файла для выбранного элемента
						function onFileChanged()
						{
							var input_file = document.getElementById("car_img");//input для файла изображения
							var file = input_file.files[0];//Получаем выбранный файл
							
							if(file == undefined)
							{
								return;
							}
							
							//Запрещаем загружать файлы больше 50 Кб
							if(file.size > 51200)
							{
								/*
								input_file.value = null;
								alert("Размер файла превышает 50 Кб");
								return;
								*/
							}
							
							//Проверяем тип файла
							if(file.type != "image/jpeg" && file.type != "image/jpg" && file.type != "image/png" && file.type != "image/gif")
							{
								input_file.value = null;
								alert("<?php echo translate_str_by_id(2444); ?>");
								return;
							}
							
							
							//Создаем url файла для его отображения
							document.getElementById("image_preshow").setAttribute("src", URL.createObjectURL(file));
						}
						</script>
						
						
					  </div>
				</div>
			</div>





			<div class="panel panel-primary">
				<div class="panel-heading"><?php echo translate_str_by_id(4253); ?></div>
				<div class="panel-body">
				
				
					<div class="form-group col-md-4">
						<label for="exampleInputEmail1"><?php echo translate_str_by_id(2085); ?></label>
						<select name="to_mark" class="form-control" id="to_mark" onchange="on_to_mark_selected();">
							<option value="0"><?php echo translate_str_by_id(4239); ?></option>
							<?php
							//Делаем запрос в веб-сервис Ucats
							$curl = curl_init();
							curl_setopt($curl, CURLOPT_URL, "http://ucats.ru/ucats/to/get_cars.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password);
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
									?>
									<option value="<?php echo $curl_result["list"][$i]["id"]; ?>"><?php echo $curl_result["list"][$i]["name"]; ?></option>
									<?php
								}
							}
							?>
						</select>
						<script>
						function on_to_mark_selected()
						{
							console.log(document.getElementById("to_mark").value);
							
							
							//Сбрасываем селектор моделей перез новым запросом
							document.getElementById("to_model").innerHTML = "<option value=\"0\"><?php echo translate_str_by_id(4239); ?></option>";
							
							//Сбрасываем селектор комплектаций перез новым запросом
							document.getElementById("to_model_types").innerHTML = "<option value=\"0\"><?php echo translate_str_by_id(4239); ?></option>";
							
							
							jQuery.ajax({
								type: "POST",
								async: true, //Запрос синхронный
								url: "<?php echo $DP_Config->domain_path;?>content/shop/docpart/garage/to_link/ajax_get_mark_models.php",
								dataType: "json",//Тип возвращаемого значения
								data: "to_mark="+document.getElementById("to_mark").value+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
								success: function(answer)
								{
									console.log(answer);
									if(answer.status == true)
									{
										var models_list = answer.list;
										var select_models_html = "<option value=\"0\"><?php echo translate_str_by_id(4239); ?></option>";
										for(var i=0; i < models_list.length; i++)
										{
											select_models_html += "<option value=\""+models_list[i].id+"\">"+models_list[i].name+" "+models_list[i].content+"</option>";
										}
										document.getElementById("to_model").innerHTML = select_models_html;
										
										
										<?php
										//Если стараница работает в режиме редактирования автомобиля - вызов функции выбора модели каталога ТО
										if( $car_id > 0 )
										{
											?>
											set_to_model_after_start();
											<?php
										}
										?>
										
									}
									else
									{
										alert("<?php echo translate_str_by_id(4254); ?>");
									}
								}
							}); 
							
							
						}
						</script>
					</div>
					
					
					<div class="form-group col-md-4">
						<label for="exampleInputEmail1"><?php echo translate_str_by_id(2086); ?></label>
						<select name="to_model" class="form-control" id="to_model" onchange="on_to_model_selected();">
							<option><?php echo translate_str_by_id(4239); ?></option>
						</select>
						<script>
						function on_to_model_selected()
						{
							console.log(document.getElementById("to_model").value);

							//Сбрасываем селектор комплектаций перез новым запросом
							document.getElementById("to_model_types").innerHTML = "<option value=\"0\"><?php echo translate_str_by_id(4239); ?></option>";
							
							
							jQuery.ajax({
								type: "POST",
								async: true, //Запрос синхронный
								url: "<?php echo $DP_Config->domain_path;?>content/shop/docpart/garage/to_link/ajax_get_models_types.php",
								dataType: "json",//Тип возвращаемого значения
								data: "to_model="+document.getElementById("to_model").value+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
								success: function(answer)
								{
									console.log(answer);
									if(answer.status == true)
									{
										var models_list = answer.list;
										var select_models_html = "<option value=\"0\"><?php echo translate_str_by_id(4239); ?></option>";
										for(var i=0; i < models_list.length; i++)
										{
											select_models_html += "<option value=\""+models_list[i].id+"\">"+models_list[i].name+", "+models_list[i].engine + ", " + models_list[i].engine_model +", "+models_list[i].engine_horse+"<?php echo translate_str_by_id(4255); ?>, "+models_list[i].type_year+"</option>";
										}
										document.getElementById("to_model_types").innerHTML = select_models_html;
										
										
										<?php
										//Если стараница работает в режиме редактирования автомобиля - вызов функции выбора комплектации каталога ТО
										if( $car_id > 0 )
										{
											?>
											set_to_model_types_after_start();
											<?php
										}
										?>
									}
									else
									{
										alert("<?php echo translate_str_by_id(4254); ?>");
									}
								}
							}); 
							
							
						}
						</script>
					</div>
					
					
					<div class="form-group col-md-4">
						<label for="exampleInputEmail1"><?php echo translate_str_by_id(4256); ?></label>
						<select name="to_model_types" class="form-control" id="to_model_types">
							<option><?php echo translate_str_by_id(4239); ?></option>
						</select>
					</div>
					
					
				</div>
			</div>


			
			<?php
			//Проверим подключен ли к сайту Специальный поиск, если нет то настройку показывать нет смысла
			$special_searches_query = $db_link->prepare('SELECT `id` FROM `shop_special_searches` WHERE `active` = 1 LIMIT 1;');
			$special_searches_query->execute();
			$special_searches = $special_searches_query->fetch();
			if($special_searches['id'] > 0)
			{
			?>
			<div class="panel panel-primary">
				<div class="panel-heading"><?php echo translate_str_by_id(4257); ?></div>
				<div class="panel-body">
					
					<div class="form-group col-md-12">
						<?php echo translate_str_by_id(4258); ?>
					</div>
					
					
					<div class="form-group col-md-12" id="tree_list_div">
					
						
						<select name="car_tree_list_level_1" class="form-control" id="car_tree_list_level_1" onchange="onTreeListSelectChange(1);">
							<option value="0"><?php echo translate_str_by_id(4239); ?></option>
							<?php
							$tree_list_id = 0;
							//Получаем список узлов первого уровня
							$car_tree_list_level1_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = (SELECT `id` FROM `shop_tree_lists` WHERE `caption` IN ( SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN (?,?) ) ) AND `level` = 1 ORDER BY `value` ASC;');
							//$car_tree_list_level1_query->bindValue(':caption', 'Автомобили');
							$car_tree_list_level1_query->execute( array('Автомобили', 'Cars') );
							while( $car_tree_list_level1_record = $car_tree_list_level1_query->fetch() )
							{
								$tree_list_id = $car_tree_list_level1_record["tree_list_id"];
								?>
								<option id="tree_option_<?php echo $car_tree_list_level1_record["id"]; ?>" value="<?php echo $car_tree_list_level1_record["id"]; ?>" webix_kids="<?php echo $car_tree_list_level1_record["count"]; ?>"><?php echo translate_str_by_id($car_tree_list_level1_record["value"]); ?></option>
								<?php
							}
							?>
						</select>
						
						
					</div>
					
					
					<div class="text-center" style="display:none;" id="tree_list_loading_gif"><img style="margin-top:4px;" src="/content/files/images/ajax-loader-transparent.gif" /></div>
					
					
				</div>
			</div>
			<script>
			//Обработка изменения селектора свойства типа "Древовидный список"
			function onTreeListSelectChange(level)
			{
				//1. Определяем значение выбранного элемента
				var select_value = document.getElementById("car_tree_list_level_"+level).value;
				
				//1.1. Для записи текущего значения данного свойства
				var current_level = parseInt(level);
				var current_value = select_value;
				
				
				
				//2. Удаляем все селекты после данного
				var next_level = parseInt(level) + 1;
				while(true)
				{
					var next_select = document.getElementById("car_tree_list_level_"+next_level);
					
					if( next_select != null )
					{
						document.getElementById("tree_list_div").removeChild(next_select);
						next_level++;
					}
					else
					{
						break;
					}
				}
				
				//3. Проверяем, выставлено ли значение "Все"
				if(select_value == 0)//Если выбрано значение "Все"
				{
					//Если это значение на селекте не первого уровня
					if( current_level > 1 )
					{
						//То, ставим текущее значение - значение предыдущего селекта
						current_level = current_level - 1;
						current_value = document.getElementById("car_tree_list_level_"+current_level).value;
					}
				}
				

				//6. Выбрано определенное значение (отличное от значения "Все") - асинхронно подгружаем следующий селект
				if(select_value != 0)
				{
					if( parseInt( document.getElementById("tree_option_"+select_value).getAttribute("webix_kids") ) == 0 )
					{
						//Этот селект последний в ветви
						return;
					}
					
					
					//int_1 - property_id
					//int_2 - level+1
					//int_3 - property_index
					
					//Индикация загрузки следующего списка - ON
					document.getElementById("tree_list_loading_gif").setAttribute("style", "display:block;");
					
					jQuery.ajax({
						type: "GET",
						url:'/content/shop/catalogue/tree_lists/ajax/ajax_get_brunch_items.php?tree_list_id=<?php echo $tree_list_id; ?>&parent_id='+select_value+'&int_1=0&int_2='+(parseInt(level)+1)+'&int_3=0'+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
						async: true,
						dataType:"json",
						success: function(data)
						{
							//console.log(data);
							
							//Индикация загрузки следующего списка - OFF
							document.getElementById("tree_list_loading_gif").setAttribute("style", "display:none;");
							
							
							if(data.data.length > 0)
							{
								//Селект
								var select = document.createElement('select');
								select.setAttribute("style", "margin-top:5px;");
								select.setAttribute("class", "form-control");
								select.setAttribute("id", "car_tree_list_level_"+data.int_2);
								select.setAttribute("name", "car_tree_list_level_"+data.int_2);
								select.setAttribute("onchange", "onTreeListSelectChange("+data.int_2+");");
								//Заполняем селект элементами
								var html = "<option value=\"0\">Все</option>";
								for(var i=0; i < data.data.length; i++)
								{
									html += "<option value=\""+data.data[i].id+"\" id=\"tree_option_"+data.data[i].id+"\" webix_kids=\""+data.data[i].webix_kids+"\">"+data.data[i].value+"</option>"
								}
								select.innerHTML = html;
								document.getElementById("tree_list_div").appendChild(select);
								
								
								<?php
								//Если страница в режиме редактирования - выставляем селекты при загрузке страницы
								if( $car_id > 0 )
								{
									?>
									for(var i=0; i < start_tree_list_set_map.length; i++)
									{
										//console.log("РАБОТАЕМ. Уровень: " + data.int_2);
										
										
										//Уже такой выставляли. Значит пользователь уже меняет значение в процессе работы со страницей, т.е. это идет уже не начальная инициализация селектов
										if( start_tree_list_set_map[i].set == true )
										{
											continue;
										}
										
										
										if( parseInt(start_tree_list_set_map[i].level) ==  parseInt(data.int_2) )
										{
											start_tree_list_set_map[i].set = true;
											
											
											document.getElementById("car_tree_list_level_" + data.int_2).value = parseInt(car_tree_list_json["level_" + data.int_2]);
											onTreeListSelectChange(data.int_2);
										}
									}
									<?php
								}
								?>
							}
						}
					});
				}	
			}
			</script>
			<?php
			}
			?>
			
		</form>
		
		
		
		
		
		<?php
		//Блок для инициализации селектов каталога ТО и древовидного списка автомобилей
		if( $car_id > 0 )
		{
			?>
			<script>
			var to_json = JSON.parse('<?php echo json_encode($to_json); ?>');
			var car_tree_list_json = JSON.parse('<?php echo json_encode($car_tree_list_json); ?>');
			
			
			// -------------------------------------------------------------------------------------
			//Каталог ТО
			if( parseInt(to_json.to_mark) != 0 )
			{
				document.getElementById("to_mark").value = parseInt(to_json.to_mark);
				on_to_mark_selected();
			}
			
			//Функция выбора модели из каталога ТО после загрузки страницы
			var to_model_after_start_already = false;
			function set_to_model_after_start()
			{
				if( to_model_after_start_already )
				{
					return;
				}
				to_model_after_start_already = true;
				
				
				if( parseInt(to_json.to_model) != 0 )
				{
					document.getElementById("to_model").value = parseInt(to_json.to_model);
					on_to_model_selected();
				}
			}
			
			//Функция выбора комплектации из каталога ТО после загрузки страницы
			var to_model_types_after_start_already = false;
			function set_to_model_types_after_start()
			{
				if( to_model_types_after_start_already )
				{
					return;
				}
				to_model_types_after_start_already = true;
				
				
				if( parseInt(to_json.to_model_types) != 0 )
				{
					document.getElementById("to_model_types").value = parseInt(to_json.to_model_types);
				}
			}
			
			// -------------------------------------------------------------------------------------
			//Массив для учета выставления селектов древовидных списков при загрузке страницы в режиме редактирования
			var start_tree_list_set_map = new Array();
			for(var i=1; i <= <?php echo count($car_tree_list_json, true); ?>; i++)
			{
				var ob = new Object;
				ob.level = i;//Уровень
				ob.set = false;//Еще не выставили
				
				start_tree_list_set_map.push(ob);
			}
			
			//Древовидный список
			if( parseInt(car_tree_list_json.level_1) > 0 )
			{
				document.getElementById("car_tree_list_level_1").value = parseInt(car_tree_list_json.level_1);
				start_tree_list_set_map[0].set = true;//Считаем, что первый уровень выставили
				onTreeListSelectChange(1);
			}
			// -------------------------------------------------------------------------------------
			</script>
			<?php
		}
		?>
		
		
		
		
		
		
		
		<script>
		//Функция сохранения
		function save_action()
		{
			//Проверка корректности заполнения полей:
			var year = parseInt(document.getElementById("year").value);
			/*
			if( year > <?php echo date("Y", time()); ?> || year < 1900 )
			{
				alert("Введите реальный год выпуска в формате YYYY");
				return;
			}
			*/
			
			if(document.getElementById("caption").value == ''){
				alert("<?php echo translate_str_by_id(5610); ?>");
				return;
			}
			
			document.forms["save_form"].submit();
		}
		</script>
		<div class="row">
		<div class="col-lg-12">
			<a class="btn btn-ar btn-primary" href="javascript:void(0);" onclick="save_action();"><i class="fa fa-save"></i> <?php echo translate_str_by_id(2114); ?></a>
		</div>
		</div>
	<?php
	}
}
?>
