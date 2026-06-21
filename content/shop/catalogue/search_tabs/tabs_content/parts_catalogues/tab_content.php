<?php
//Скрипт для вывода содержимого таба "Поиск запчастей по марке" (вывод подключаемых каталогов AutoXP, catalogs-parts, ilcats и т.д.)
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();

/*
Пользовательский интерфейс:
1. Сначала выводятся марки

2. Пользователь выбирает марку

3. 
- Если в марке два и более каталогов - показываются ссылки на каталоги и кнопка "Вернуться к выбору марки"
- Если в марке один каталог - идет сразу переход на каталог
- Если в марке нет каталогов - она не показывается

*/

//Алгоритм работы:


//1. Получаем админские настройки таба
$parts_catalogues_tab_query = $db_link->prepare('SELECT * FROM `shop_docpart_search_tabs` WHERE `name` = :name;');
$parts_catalogues_tab_query->bindValue(':name', 'parts_catalogues');
$parts_catalogues_tab_query->execute();
$parts_catalogues_tab_record = $parts_catalogues_tab_query->fetch();
$parts_catalogues_tab_parameters_values = json_decode($parts_catalogues_tab_record["parameters_values"], true);



//Некоторые поля должны быть массивами. Если в админке для них не указаны значения - значит здесь нужно присвоить таким поля значение в виде пустого массива
if( ! isset($parts_catalogues_tab_parameters_values["autoxp_show_cars"]) )
{
	$parts_catalogues_tab_parameters_values["autoxp_show_cars"] = array();
}
if( ! isset($parts_catalogues_tab_parameters_values["catalogs_parts_com_show_cars"]) )
{
	$parts_catalogues_tab_parameters_values["catalogs_parts_com_show_cars"] = array();
}



//2. Получаем список каталогов из таблицы shop_docpart_cars_catalogues
$catalogues = array();
$catalogues_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars_catalogues`;');
$catalogues_query->execute();
while( $catalogue = $catalogues_query->fetch() )
{
	if( ! isset($parts_catalogues_tab_parameters_values[$catalogue["assoc_name"]."_show"]) )
	{
		continue;
	}
	
	if($parts_catalogues_tab_parameters_values[$catalogue["assoc_name"]."_show"] == "on")
	{
		array_push($catalogues, (int)$catalogue["id"]);
	}
}
$catalogues_for_sql = json_encode($catalogues);
$catalogues_for_sql = str_replace("[","(",$catalogues_for_sql);
$catalogues_for_sql = str_replace("]",")",$catalogues_for_sql);



if( count($catalogues) > 0 )
{
	//2. Получаем ссылки на каталоги по маркам автомобилей через таблицу shop_docpart_cars_catalogue_links.
	/*
	Создаем объект описания вида:
	Массив, ключем которого является ID марки.
	Полем массива является объект, в котором содержится:
	- название марки
	- перечень доступных каталоговё
	- перечень ссылок по доступным каталогам
	*/
	$cars_objects = array();


	$cars_links_query = $db_link->prepare('SELECT *, (SELECT `caption` FROM `shop_docpart_cars` WHERE `id` = `shop_docpart_cars_catalogue_links`.`car_id`) AS `car_caption`, (SELECT `assoc_name` FROM `shop_docpart_cars_catalogues` WHERE `id` = `shop_docpart_cars_catalogue_links`.`catalogue_id`) AS `catalogue_assoc_name` FROM `shop_docpart_cars_catalogue_links` WHERE `catalogue_id` IN '.$catalogues_for_sql.' ORDER BY `car_caption`;');

	$cars_links_query->execute();
	while( $car_link = $cars_links_query->fetch() )
	{
		if( $cars_objects[$car_link["car_id"]] == null )
		{
			$cars_objects[$car_link["car_id"]] = array();
			
			$cars_objects[$car_link["car_id"]]["car_caption"] = strtoupper($car_link["car_caption"]);
			
			//Массив для объектов Каталогов для данной марки
			$cars_objects[$car_link["car_id"]]["catalogues"] = array();
		}
		
		
		//Объект каталога
		$catalogue = array();
		$catalogue["name"] = $car_link["catalogue_assoc_name"];
		$catalogue["caption"] = $parts_catalogues_tab_parameters_values[$car_link["catalogue_assoc_name"]."_caption"];
		$catalogue["order"] = $parts_catalogues_tab_parameters_values[$car_link["catalogue_assoc_name"]."_order"];
		
		
		//ЗДЕСЬ ИДЕТ СПЕЦИАЛЬНЫЙ АЛГОРИТМ - ДЛЯ КАЖДОГО КАТАЛОГА СВОЙ. В этом алгоритме определяем:
		// - включен ли данный автомобиль в админских настройках, а также настраиваем ссылку - также в зависимсти от админских настроек
		if( $car_link["catalogue_assoc_name"] == "autoxp")
		{
			//Определяем, включена ли данная марка для каталога
			if( array_search($car_link["car_id"], $parts_catalogues_tab_parameters_values["autoxp_show_cars"]) === false )
			{
				continue;
			}
			//Работаем здесь - значит есть такая марка. Указываем ссылку
			$catalogue["href"] = $car_link["href"].$parts_catalogues_tab_parameters_values["autoxp_id"];
		}
		else if( $car_link["catalogue_assoc_name"] == "ilcats" )
		{
			//Определяем, включена ли данная марка для каталога
			if( $parts_catalogues_tab_parameters_values["ilcats_car_".$car_link["car_id"]] == "" )
			{
				continue;
			}
			//Работаем здесь - значит есть такая марка. Указываем ссылку
			$catalogue["href"] = str_replace("<pid>", $parts_catalogues_tab_parameters_values["ilcats_car_".$car_link["car_id"]], $car_link["href"]);//Меняем PID
			
			$catalogue["href"] = str_replace("<clid>", $parts_catalogues_tab_parameters_values["ilcats_clid"],$catalogue["href"]);//Меняем clid
			
		}
		else if( $car_link["catalogue_assoc_name"] == "catalogs_parts_com" )
		{
			//Определяем, включена ли данная марка для каталога
			if( array_search($car_link["car_id"], $parts_catalogues_tab_parameters_values["catalogs_parts_com_show_cars"]) === false )
			{
				continue;
			}
			//Работаем здесь - значит есть такая марка. Указываем ссылку
			$catalogue["href"] = str_replace("client:;","client:".$parts_catalogues_tab_parameters_values["catalogs_parts_com_id"].";",$car_link["href"]);
		}
		
		
		//Добавляем объект каталога к списку
		array_push($cars_objects[$car_link["car_id"]]["catalogues"], $catalogue);
		
		
		
		
		//Сортируем список каталогов по полю order
		$orders = array();
		foreach ($cars_objects[$car_link["car_id"]]["catalogues"] as $key => $cat)
		{
			$orders[$key] = (int)$cat['order'];
		}
		array_multisort($orders, SORT_ASC, $cars_objects[$car_link["car_id"]]["catalogues"]);
	}



	//Отфильтруем марки, в которых нет ссылок
	$cars_objects_filtered = array();
	foreach( $cars_objects AS $car_id => $car_object)
	{
		if( count($car_object["catalogues"]) == 0)
		{
			continue;
		}
		
		$car_object["car_id"] = $car_id;
		$cars_objects_filtered[$car_id] = $car_object;
	}
	$cars_objects = $cars_objects_filtered;




	$cars_by_key = array();//Массив в виде: "Буква"=>"Массив марок"
	$cars_count_total = count($cars_objects);//Общее количество марок
	foreach( $cars_objects AS $car_id => $car_object)
	{
		//Получаем первую букву:
		$letter = mb_substr(translate_str_by_key($car_object["car_caption"]), 0, 1, "UTF-8");
		
		if( $cars_by_key[$letter] == null )
		{
			$cars_by_key[$letter] = array();
		}
		
		
		array_push($cars_by_key[$letter], $car_object);
	}




	?>
	<div id="parts_catalogues_tab_work_area">
	<div class="search_tab_clar">
		<?php echo translate_str_by_id(4179); ?>:
	</div>
	<?php

	//Выводим автомобили
	//Всего будет 5 колонок.:
	$cars_for_col = (int)($cars_count_total/5) + 1;//Количество автомобилей в одной колонке
	$col_counter = 0;//Выведено марок в текущей колонке
	$cars_counter = 0;//Выведено марок всего - на все колонки
	$shown_letters = array();//Массив показанных букв
	foreach($cars_by_key AS $letter => $cars)
	{
		for($i=0; $i < count($cars); $i++)
		{
			if($col_counter == 0)
			{
				?>
				<ul class="search_tab_car_ul">
				<?php
			}
			
			?>
			<li>
				<div class="search_tab_car_letter">
				<?php
				//Показываем букву, если еще такую не показывали
				if( array_search($letter, $shown_letters) === false )
				{
					echo strtoupper($letter);
					array_push($shown_letters, $letter);
				}
				?>
				</div>
				<div class="search_tab_car_caption">
					<?php
					//Здесь может быть два варианта. Если ссылка единственная - сразу переход на нее. Если ссылок больше одной - то при нажатии открываем выбор каталога
					if( count($cars[$i]["catalogues"]) > 1)
					{
						?>
						<a rel="nofollow" href="javascript:void(0);" onclick="parts_catalogues_tab_show_catalogues(<?php echo $cars[$i]["car_id"]; ?>);"><?php echo $cars[$i]["car_caption"]; ?></a>
						<?php
					}
					else//Ссылка - единственная
					{
						if( $cars[$i]["catalogues"][0]["name"] == "autoxp" )
						{
							?>
							<a href="javascript:void(0);" onclick="autoxp_redirect('<?php echo $cars[$i]["catalogues"][0]["href"]; ?>');"><?php echo translate_str_by_key($cars[$i]["car_caption"]); ?></a>
							<?php
						}
						else
						{
							?>
							<a rel="nofollow" href="<?php echo $cars[$i]["catalogues"][0]["href"]; ?>" target="_blank"><?php echo strtoupper(translate_str_by_key($cars[$i]["car_caption"])); ?></a>
							<?php
						}
					}
					?>
				</div>
			</li>
			<?php
			
			
			
			
			//Автомобиль выведен - инкрементируем счетчики
			$col_counter++;
			$cars_counter++;
			
			
			
			//Если в данной колонке выведены все автомобили ИЛИ если выведены вообще все автомобили
			if($col_counter == $cars_for_col || $cars_counter == $cars_count_total)
			{
				$col_counter = 0;//Сбрасываем счетчик выведеных автомобилей в колонке
				
				?>
				</ul>
				<?php
			}
		}
	}
	?>
	</div>
	<script>
	var cars_objects = JSON.parse('<?php echo json_encode($cars_objects); ?>');
	var cars_html = "";//Переменная для хранения HTML марок автомобилей
	// ----------------------------------------------------------------------
	//Функция отображения каталогов выбранной марки
	function parts_catalogues_tab_show_catalogues(car_id)
	{
		//Сначала сохраняем HTML панели марок, чтобы потом можно было вернуться
		cars_html = document.getElementById("parts_catalogues_tab_work_area").innerHTML;
		
		
		//Формируем HTML для выбора каталогов нужной марки
		var catalogues_html = "<div class=\"search_tab_clar\"><?php echo translate_str_by_id(4180); ?> "+cars_objects[String(car_id)]["car_caption"]+" <?php echo translate_str_by_id(4181); ?>:</div>";
		
		
		//По массиву каталогов:
		var catalogues = cars_objects[String(car_id)]["catalogues"];
		for( var i = 0; i < catalogues.length ; i++)
		{
			catalogues_html += "<div class=\"search_tab_car_catalogue\">";
			if(catalogues[i]["name"] == "autoxp")
			{
				catalogues_html += "<a href=\"javascript:void(0);\" onclick=\"autoxp_redirect('"+catalogues[i]["href"]+"');\"><i class=\"fa fa-check\"></i> "+catalogues[i]["caption"]+"</a>";
			}
			else
			{
				catalogues_html += "<a rel=\"nofollow\" href=\""+catalogues[i]["href"]+"\" target=\"_blank\"><i class=\"fa fa-check\"></i> "+catalogues[i]["caption"]+"</a>";
			}
			catalogues_html += "</div>";
		}
		
		
		
		catalogues_html += "<a class=\"search_tab_car_catalogue_back\" href=\"javascript:void(0);\" onclick=\"parts_catalogues_tab_show_cars();\"><i class=\"fa fa-arrow-left\"></i> <?php echo translate_str_by_id(4182); ?></a>";
		
		
		
		
		//Показываем HTML выбора каталогов
		document.getElementById("parts_catalogues_tab_work_area").innerHTML = catalogues_html;
	}
	// ----------------------------------------------------------------------
	//Функция отображения марок (т.е. возврат ко всем маркам)
	function parts_catalogues_tab_show_cars()
	{
		document.getElementById("parts_catalogues_tab_work_area").innerHTML = cars_html;
	}
	// ----------------------------------------------------------------------
	</script>
	
	
	
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
else
{
	?>
	<div class="search_tab_clar">
		<?php echo translate_str_by_id(4184); ?>
	</div>
	<?php
}
?>