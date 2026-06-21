<?php
/**
 * Страничный скрипт - управление запросами клиентов
*/
defined('_ASTEXE_') or die('No access');

require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий


// формируем пагинацию
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link)
{

	// print_r($all);
	// echo "</br>";
	// print_r($lim);
	// echo "</br>";
	// print_r($prev);
	// echo "</br>";
	// print_r($curr_link);
	// echo "</br>";

    $html = '';
	// осуществляем проверку, чтобы выводимые первая и последняя страницы
    // не вышли за границы нумерации
    $first = $curr_link - $prev;
    if ($first < 1) $first = 0;
		$last = $curr_link + $prev;
		
		$count_pages = (int)($all / $lim);//Количество страниц
		if($all%$lim)//Если остались еще элементы
		{
			$count_pages++;
		}

		$count_pages = $count_pages - 1;

    if ($last > $count_pages) $last = $count_pages;
 
    // начало вывода нумерации
    // выводим первую страницу
    $y = 0;
    if ($first > 0) $html .= "<li class='paginate_button'><a onclick='goToPage({$y})'>0</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li class='paginate_button'><a onclick='goToPage({$y})'>...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li class='paginate_button'><a onclick='goToPage({$y})'>$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= "<li class='paginate_button ".$curr_css."'><a>". $i ."</a></li>";
        } else {
            $alink = "<li class='paginate_button'><a onclick='goToPage(";
            if($i != 0) $alink .= "{$i}";
            $alink .= ")'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < $count_pages && $count_pages - $last > 2) $html .=  "<li class='paginate_button'><a onclick='goToPage({$y})'>...</a></li>";
    // выводим последнюю страницу
    $e = $count_pages;
    if ($last < $count_pages) $html .=  "<li class='paginate_button'><a onclick='goToPage({$e})'>$e</a></li>";
	
	return $html;
}

?>





<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2113); ?>
		</div>
		<div class="panel-body">
			
			<?php
			print_backend_button(array('background_color'=>'#26c5df', 'fontawesome_class'=>'fas fa-address-card', 'caption'=>translate_str_by_id(5222), 'url'=>$DP_Config->domain_path.$DP_Config->backend_dir.'/requests/polya-vin-zaprosa'));
			?>
			
		</div>
	</div>
</div>




<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<div class="panel-tools">
				<a class="showhide"><i class="fa fa-chevron-up"></i></a>
			</div>
			<?php echo translate_str_by_id(5223); ?>
		</div>
		<div class="panel-body">
			<?php
			$viewed = -1;
			$customer_id = '';

			//Получаем текущие значения фильтра:
			$vin_filter = NULL;
			if(isset($_COOKIE["vin_filter"])){
				$vin_filter = $_COOKIE["vin_filter"];
			}
			if($vin_filter != NULL)
			{
				$vin_filter = json_decode($vin_filter, true);
				$viewed = $vin_filter["viewed"];
				$customer_id = $vin_filter["customer_id"];
			}
			?>
			
			
			<div class="col-lg-4">
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3581); ?>
					</label>
					<div class="col-lg-6">
						<select id="viewed" class="form-control" <?=($viewed != -1)?'style="background:#b9fcab;"':'';?>>
							<option value="-1"><?php echo translate_str_by_id(2094); ?></option>
							<option value="1"><?php echo translate_str_by_id(3581); ?></option>
							<option value="0"><?php echo translate_str_by_id(3582); ?></option>
						</select>
						<script>
							document.getElementById("viewed").value = <?php echo $viewed; ?>;
						</script>
					</div>
				</div>
			</div>
			
			<div class="col-lg-4">
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(3818); ?>
					</label>
					<div class="col-lg-6">
						<input <?=($customer_id !== '')?'style="background:#b9fcab;"':'';?> type="text" id="customer_id" value="<?php echo $customer_id; ?>" class="form-control" />
					</div>
				</div>
			</div>
			
		</div>
		<div class="panel-footer">
			<button class="btn btn-success" type="button" onclick="filterUsers();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
			<button class="btn btn-primary" type="button" onclick="unsetFilterUsers();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2233); ?></button>
		</div>
	</div>
</div>

<script>
// ------------------------------------------------------------------------------------------------
//Устновка cookie в соответствии с фильтром
function filterUsers()
{
	var vin_filter = new Object;
	
	vin_filter.viewed = document.getElementById("viewed").value;
	vin_filter.customer_id = document.getElementById("customer_id").value;
	
	//Устанавливаем cookie (на полгода)
	var date = new Date(new Date().getTime() + 15552000 * 1000);
	document.cookie = "vin_filter="+JSON.stringify(vin_filter)+"; path=/; expires=" + date.toUTCString();
	
	//Обновляем страницу
	goToPage(0);
}
// ------------------------------------------------------------------------------------------------
//Снять все фильтры
function unsetFilterUsers()
{
	var vin_filter = new Object;
	
	
	vin_filter.viewed = -1;
	vin_filter.customer_id = '';

	//Устанавливаем cookie (на полгода)
	var date = new Date(new Date().getTime() + 15552000 * 1000);
	document.cookie = "vin_filter="+JSON.stringify(vin_filter)+"; path=/; expires=" + date.toUTCString();
	
	//Обновляем страницу
	goToPage(0);
}
// ------------------------------------------------------------------------------------------------
</script>



















<?php
//Выводим таблицу
?>
<script>
// ------------------------------------------------------------------------------------------------
//Возвращает cookie с именем name, если есть, если нет, то undefined
function getCookie(name) 
{
	var matches = document.cookie.match(new RegExp(
		"(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
	));
	return matches ? decodeURIComponent(matches[1]) : undefined;
}

//Переход на другую страницу заказа
function goToPage(need_page)
{
	//Устанавливаем cookie (на полгода)
	var date = new Date(new Date().getTime() + 15552000 * 1000);
	document.cookie = "vin_need_page="+need_page+"; path=/; expires=" + date.toUTCString();
	
	//Обновляем страницу
	location='/<?php echo $DP_Config->backend_dir; ?>/requests';
}
// ------------------------------------------------------------------------------------------------
</script>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(5224); ?>
		</div>
		<div class="panel-body">
			<div class="table-responsive">
				<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
					<thead> 
						<tr> 
							<th>
								<input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/>
							</th>
							<th>ID</th>
							<th><?php echo translate_str_by_id(3250); ?></th>
							<th><?php echo translate_str_by_id(3818); ?></th>
						</tr>
					</thead>
					<tbody>
					<?php
					
					//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
					//---------------------------------------------------------------------------------------------->
					
					//Определяем, с какой страницы начать вывод:
					$s_page = 0;
					if( isset($_COOKIE['vin_need_page']) )
					{
						$s_page = (int) $_COOKIE['vin_need_page'];
					}

					//Определяем сколько пропустить записей для выборки
					$p = $DP_Config->list_page_limit;//Штук на страницу
					$start_elements_of_page = abs($s_page * $p);
					
					//----------------------------------------------------------------------------------------------|
					
					
					
					//Массивы для JS с id групп и с чекбоксами групп
					$for_js = "var vin_array = new Array();\n";//Выведем массив для JS с чекбоксами
					$for_js = $for_js."var vin_id_array = new Array();\n";//Выведем массив для JS с ID
					


					//Подстрока с условиями фильтрования пользователей
					$WHERE_CONDITIONS = "";

					//По куки фильтра:
					$vin_filter = NULL;
					if(isset($_COOKIE["vin_filter"])){
						$vin_filter = $_COOKIE["vin_filter"];
					}
					if($vin_filter != NULL)
					{
						$vin_filter = json_decode($vin_filter, true);
						
						// 1.
						if($vin_filter["viewed"] != -1 )
						{
							if($WHERE_CONDITIONS != "")
							{
								$WHERE_CONDITIONS .= " AND ";
							}
							$WHERE_CONDITIONS .= " `viewed` = ".$vin_filter["viewed"];
						}
						
						if($vin_filter["customer_id"] !== '' )
						{
							if($WHERE_CONDITIONS != "")
							{
								$WHERE_CONDITIONS .= " AND ";
							}
							$WHERE_CONDITIONS .= " `user_id` = ".(int)$vin_filter["customer_id"];
						}
						
						if($WHERE_CONDITIONS != "")
						{
							$WHERE_CONDITIONS = " WHERE ".$WHERE_CONDITIONS;
						}
					}//~if
					
					
					
					//Получаем список зарегистрированных пользователей
					$vin_list_SQL = "SELECT SQL_CALC_FOUND_ROWS *
						FROM
					`users_vin`
					
					".$WHERE_CONDITIONS." ORDER BY `viewed` ASC, `id` DESC LIMIT $start_elements_of_page, $p";
					
					
					//var_dump($vin_list_SQL);
					

					$vin_list_query = $db_link->prepare($vin_list_SQL);
					$vin_list_query->execute();
					
					//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
					//---------------------------------------------------------------------------------------------->
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					//Определяем количество страниц для вывода:
					$count_pages = (int)($elements_count_rows / $p);//Количество страниц
					if($elements_count_rows%$p)//Если остались еще элементы
					{
						$count_pages++;
					}
					
					//----------------------------------------------------------------------------------------------|
					
					while($vin_list_array = $vin_list_query->fetch())
					{
						
						
						$viewed_class = "";
						$viewed_flag = $vin_list_array["viewed"];
						if( $viewed_flag == 0)
						{
							$viewed_class = " not_viewed";
						}
						
						
						
						$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/requests/request?vin_id=".$vin_list_array["id"]."\">";
						?>
						<tr class="<?php echo $viewed_class; ?>">
							<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $vin_list_array["id"];?>');" id="checked_<?php echo $vin_list_array["id"];?>" name="checked_<?php echo $vin_list_array["id"];?>"/></td>
							
							<td><?php echo $a_item.$vin_list_array["id"];?></a></td>
							<td><?php echo $a_item.date("d-m-Y H:i",$vin_list_array["time"]);?></a></td>
							<td><?php echo $a_item.$vin_list_array["user_id"];?></a></td>
							
							<?php
							$for_js = $for_js."vin_array[vin_array.length] = \"checked_".$vin_list_array["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."vin_id_array[vin_id_array.length] = ".$vin_list_array["id"].";\n";//Добавляем элемент для JS
							?>
						</tr>
						<?php
					}//while
					?>
					</tbody>
				</table>
			</div>
			
			
			
			<?php
			//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
			if( $count_pages > 1 )
			{
				// формируем пагинацию
				$pagination = pagination($elements_count_rows, $p, 3, $s_page, 'paginate_button active', '');
				if($pagination != '<a class="paginate_button active">1</a>'){
					$pagination = '<div class="pagination">'.$pagination.'</div>';
				}else{
					$pagination = '';
				}
				?>
				<div class="row">
					<div class="col-lg-12 text-center">
						<div class="dataTables_paginate paging_simple_numbers">
							<?php echo $pagination; ?>
						</div>
					</div>
				</div>
			<?php
			}
			//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
			?>
			
			
			
		</div>
	</div>
</div>




















<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(3587); ?>
		</div>
		<div class="panel-body">
		
			<div class="form-group">
				<label for="" class="col-lg-2 control-label">
					<?php echo translate_str_by_id(3589); ?>
				</label>
				<div class="col-lg-8">
					<select id="setUsersVinViewed" class="form-control">
						<option value="1"><?php echo translate_str_by_id(3581); ?></option>
						<option value="0"><?php echo translate_str_by_id(3582); ?></option>
					</select>
				</div>
				<div class="col-lg-2">
					<button class="btn w-xs btn-success" onclick="setUsersVinViewed();"><?php echo translate_str_by_id(3588); ?></button>
				</div>
			</div>
			
		</div>
	</div>
</div>

<script>
<?php
echo $for_js;//Выводим массив с чекбоксами для элементов
?>
//Обработка переключения Выделить все/Снять все
function on_check_uncheck_all()
{
	var state = document.getElementById("check_uncheck_all").checked;
	
	for(var i=0; i<vin_array.length;i++)
	{
		document.getElementById(vin_array[i]).checked = state;
	}
}//~function on_check_uncheck_all()



//Обработка переключения одного чекбокса
function on_one_check_changed(id)
{
	//Если хотя бы одна группа снята - снимаем общий чекбокс
	for(var i=0; i<vin_array.length;i++)
	{
		if(document.getElementById(vin_array[i]).checked == false)
		{
			document.getElementById("check_uncheck_all").checked = false;
			break;
		}
	}
}//~function on_one_check_changed(id)



//Установить статус просмотра
function setUsersVinViewed()
{
	//Составляем список отмеченных:
	var vin_list = "";
	for(var i=0; i < vin_array.length; i++)
	{
		if(document.getElementById(vin_array[i]).checked == true)
		{
			if(vin_list.length != 0) vin_list += ",";//Если уже есть отмеченные пользователи
			vin_list += vin_id_array[i];
		}
	}
	if(vin_list.length == 0)
	{
		alert("Не указаны запросы");
		return;
	}
	
	vin_list = "[" + vin_list + "]";//Преобразуем в массив JSON
	
	//Объект запроса
	var request_object = new Object;
	request_object.vins = vin_list;
	request_object.viewed_flag = document.getElementById("setUsersVinViewed").value;

	jQuery.ajax({
		type: "POST",
		async: true, //Запрос асинхронный
		url: "/<?php echo $DP_Config->backend_dir; ?>/content/requests/ajax_set_users_vin_viewed.php",
		dataType: "json",//Тип возвращаемого значения
		data: "request_object="+JSON.stringify(request_object)+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
		success: function(answer)
		{
			if(answer.status == true)
			{
				//Обновляем страницу
				location='/<?php echo $DP_Config->backend_dir; ?>/requests';
			}
			else
			{
				console.log(answer);
				alert("<?php echo translate_str_by_id(3599); ?>");
			}
		}
	});
}
</script>