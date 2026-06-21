<?php
/**
 * Страничный скрипт для нстройки курсов валют
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( !empty($_POST["save_action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	if( $_POST["save_action"] == "general" )
	{
		$s_page = (int)$_POST["s_page"];
		
		$no_error = true;//Флаг - сохранение выполнено без ошибок
	
		//Получаем все валюты, кроме главной (т.е. курс главной валюты мы не настраиваем, он равен 1)
		$currencies_query = $db_link->prepare("SELECT * FROM `shop_currencies`");
		$currencies_query->execute();
		while( $currency = $currencies_query->fetch() )
		{
			//Этой валюты не было на странице
			if( empty($_POST["rate_".$currency["iso_code"]]) )
			{
				continue;
			}
			
			//Сохраняем курсы всех валют
			if( $db_link->prepare("UPDATE `shop_currencies` SET `rate` = ? WHERE `iso_code` = ?;")->execute( array($_POST["rate_".$currency["iso_code"]], $currency["iso_code"]) ) != true )
			{
				$no_error = false;
			}
		}
		
		
		if($no_error)
		{
			$success_message = translate_str_by_id(3276);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?success_message=<?php echo $success_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$error_message = translate_str_by_id(3277);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?error_message=<?php echo $error_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
	}
	else if( $_POST["save_action"] == "available_currencies" )
	{
		$currencies = json_decode($_POST["currencies_list"], true);
		$available = (int)$_POST["available"];
		$s_page = (int)$_POST["s_page"];
		
		if( array_search($DP_Config->shop_currency, $currencies) !== false )
		{
			$error_message = translate_str_by_id(3278);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
		
		
		$currencies_for_sql = "";
		$binding_values = array();
		$currencies_list = json_decode($_POST["currencies_list"], true);
		for( $i=0; $i<count($currencies_list); $i++)
		{
			if( $i > 0 )
			{
				$currencies_for_sql = $currencies_for_sql.",";
			}
			$currencies_for_sql = $currencies_for_sql."?";
			array_push($binding_values, $currencies_list[$i]);
		}
		
		
		array_unshift($binding_values, $available);
		

		if( $db_link->prepare("UPDATE `shop_currencies` SET `available` = ? WHERE `iso_code` IN (".$currencies_for_sql.");")->execute($binding_values) != true )
		{
			$error_message = translate_str_by_id(3279);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?error_message=<?php echo $error_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$success_message = translate_str_by_id(2157);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?success_message=<?php echo $success_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
	}
}
else
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	?>
	
	<?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="document.forms['rates_save_form'].submit();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3280); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="set_available_currencies(true, false);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/on.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2644); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="set_available_currencies(false, false);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3281); ?></div>
				</a>
				
				
				
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3282); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<form method="POST" name="rates_save_form">
						<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
						<input type="hidden" value="general" name="save_action" />
						<input type="hidden" value="<?php echo (int)$_GET["s_page"]; ?>" name="s_page" />
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead> 
								<tr> 
									<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
									<th>ID</th>
									<th><?php echo translate_str_by_id(2533); ?> ISO</th>
									<th><?php echo translate_str_by_id(2277); ?> ISO</th>
									<th><?php echo translate_str_by_id(3283); ?></th>
									<th><?php echo translate_str_by_id(3284); ?></th>
									<th><?php echo translate_str_by_id(3285); ?></th>
								</tr>
							</thead>
							<tbody>
							<?php
							
							//Массивы для JS с id элементов и с чекбоксами элементов
							$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
							$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
							

							$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `shop_currencies` ORDER BY `order`;");
							$elements_query->execute();
							
							$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
							$elements_count_rows_query->execute();
							$elements_count_rows = $elements_count_rows_query->fetchColumn();
							
							//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
							//---------------------------------------------------------------------------------------------->
							//Определяем количество страниц для вывода:
							$p = $DP_Config->list_page_limit;//Штук на страницу
							$count_pages = (int)($elements_count_rows / $p);//Количество страниц
							if($elements_count_rows%$p)//Если остались еще элементы
							{
								$count_pages++;
							}
							//Определяем, с какой страницы начать вывод:
							$s_page = 0;
							if(!empty($_GET['s_page']))
							{
								$s_page = $_GET['s_page'];
							}
							//----------------------------------------------------------------------------------------------|
							
							
							for($i=0, $d=0; $i<$elements_count_rows && $d<$p; $i++, $d++)
							{
								$element_record = $elements_query->fetch();
								
								//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
								if($i < $s_page*$p)
								{
									$d--;
									continue;
								}
								
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["iso_code"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["iso_code"].";\n";//Добавляем элемент для JS
								
							?>
								<tr>
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["iso_code"]; ?>');" id="checked_<?php echo $element_record["iso_code"]; ?>" name="checked_<?php echo $element_record["iso_code"]; ?>"/></td>
									<td><?php echo $element_record["id"]; ?></td>
									<td><?php echo $element_record["iso_code"]; ?></td>
									<td><?php echo $element_record["iso_name"]; ?></td>
									<td>
										<input class="form-control" type="text" name="rate_<?php echo $element_record["iso_code"]; ?>" value="<?php echo $element_record["rate"]; ?>" style="width:100px;" />
									</td>
									<td>
										<?php
										if($DP_Config->shop_currency == $element_record["iso_code"])
										{
											?>
											<img title="<?php echo translate_str_by_id(3286); ?>" src="/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/star.png" style="width:18px;border-radius:50%;" />
											<?php
										}
										?>
									</td>
									
									<td>
									<?php 
										if($element_record["available"] == 1) 
										{
											?>
											<img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/on.png" onclick="set_available_currencies(false, [<?php echo $element_record["iso_code"]; ?>] );" style="cursor:pointer;" />
											<?php
										}
										else
										{
											?>
											<img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/off.png" onclick="set_available_currencies(true, [<?php echo $element_record["iso_code"]; ?>] );" style="cursor:pointer;" />
											<?php
										}
									?>
								</td>
									
								</tr>
							<?php
							}//for
							?>
							</tbody>
						</table>
					</form>
				</div>
				
				
				<?php
				//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				if( $count_pages > 1 )
				{
					?>
					<div class="row">
						<div class="col-lg-12 text-center">
							<div class="dataTables_paginate paging_simple_numbers">
								<ul class="pagination">
								<?php
								for($i=0; $i < $count_pages; $i++)
								{
									//Класс первой страницы
									$previous = "";
									if($i == 0) $previous = "previous";
									
									//Класс последней страницы
									$next = "";
									if($i == $count_pages-1) $next = "next";
									
									if($i == $s_page)//Текущая страница
									{
										?>
										<li class="paginate_button active <?php echo $previous; ?> <?php echo $next; ?>"><a href="javascript:void(0);"><?php echo $i; ?></a></li>
										<?php
									}
									else
									{
										?>
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/finance/nastrojka-kursov-valyut?s_page=$i"; ?>"><?php echo $i; ?></a></li>
										<?php
									}
								}
								?>
								</ul>
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
	
    
	
	
	
	
	
	
	
	
	
	
	
	<form name="available_currency_form" method="POST">
		<input type="hidden" name="save_action" value="available_currencies" />
		<input type="hidden" id="currencies_list" name="currencies_list" value="" />
		<input type="hidden" id="available" name="available" value="" />
		<input type="hidden" name="s_page" value="<?php echo (int)$_GET["s_page"]; ?>" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	// ---------------------------------------------------------
	function set_available_currencies(available, currencies)
	{
		if(available == true)
		{
			document.getElementById("available").value = "1";
		}
		else
		{
			document.getElementById("available").value = "0";
		}
		
		
		if(currencies == false)
		{
			currencies = getCheckedElements();
			
			if(currencies.length == 0)
			{
				alert("<?php echo translate_str_by_id(3287); ?>");
				return;
			}
		}
		
		
		
		document.getElementById("currencies_list").value = JSON.stringify(currencies);
		
		
		document.forms["available_currency_form"].submit();
	}
	// ---------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	

	
	
	<script>
    <?php
    echo $for_js;//Выводим массив с чекбоксами для элементов
    ?>
    //Обработка переключения Выделить все/Снять все
    function on_check_uncheck_all()
    {
        var state = document.getElementById("check_uncheck_all").checked;
        
        for(var i=0; i<elements_array.length;i++)
        {
            document.getElementById(elements_array[i]).checked = state;
        }
    }//~function on_check_uncheck_all()
    
    
    
    //Обработка переключения одного чекбокса
    function on_one_check_changed(id)
    {
        //Если хотя бы один чекбокс снят - снимаем общий чекбокс
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == false)
            {
                document.getElementById("check_uncheck_all").checked = false;
                break;
            }
        }
    }//~function on_one_check_changed(id)
    
    
    
    //Получение массива id отмеченых элементов
    function getCheckedElements()
    {
        var checked_ids = new Array();
        //По массиву чекбоксов
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == true)
            {
                checked_ids.push(elements_id_array[i]);
            }
        }
        
        return checked_ids;
    }
    </script>
	
	<?php
}
?>