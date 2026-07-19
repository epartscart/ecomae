<?php
/**
 * Страница управления складами
 * 
 * Данная страница предназначена для создания/редактирования/удаления складов
*/
defined('_ASTEXE_') or die('No access');

$product_types = array(1 => 'Catalog', 2 => 'Price list');
?>

<?php
if( ! empty($_POST["action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    if($_POST["action"] == "delete_storages")
    {
        $storages = json_decode($_POST["storages"]);
        
        //Делаем строку с перечислением через запятую складов
        $storages_str = "";
        $binding_values = array();
		for($i=0; $i < count($storages); $i++)
        {
            if($i > 0) $storages_str .= ",";
            $storages_str .= "?";
			
			array_push($binding_values, $storages[$i]);
        }
        
        //Алгоритм удаления складов:
        //1. Удалить учетную запись склада (shop_storages)
        $delete_record_result = true;//Результат
        if( $db_link->prepare("DELETE FROM `shop_storages` WHERE `id` IN ($storages_str);")->execute($binding_values) != true)
        {
            $delete_record_result = false;//Результат
        }
        
        //2. Удаление привязок склада к магазинам (offices_storages_map)
        $delete_offices_storages_map_result = true;//Результат
		if( $db_link->prepare("DELETE FROM `shop_offices_storages_map` WHERE `storage_id` IN ($storages_str);")->execute( $binding_values ) != true)
        {
            $delete_offices_storages_map_result = false;//Результат
        }
        

        
        if($delete_record_result && $delete_offices_storages_map_result )
        {
            $success_message = translate_str_by_id(3466);
			epc_cp_redirect('/shop/logistics/storages?success_message=' . rawurlencode($success_message));
        }
        else
        {
            $error_message = translate_str_by_id(2912).": <br>";
            if(!$delete_record_result)
            {
                $error_message .= translate_str_by_id(3467)."<br>";
            }
            if(!$delete_offices_storages_map_result)
            {
                $error_message .= translate_str_by_id(3468)."<br>";
            }
			epc_cp_redirect('/shop/logistics/storages?error_message=' . rawurlencode(strip_tags(str_replace('<br>', "\n", $error_message))));
        }
        
        
    }
}
else//Действий нет - выводим страницу
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
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/storages/storage">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
				</a>
				
				<?php
				//Ссылка на страницу "Свой IP"
				print_backend_button( array("url"=>"/content/usefull/ip.php", "background_color"=>"#ffc500", "fontawesome_class"=>"fas fa-network-wired", "caption"=>translate_str_by_id(795), "target"=>"_blank") );
				?>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/shop/logistics/storages/groups">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/modules.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3469); ?></div>
				</a>
				
				
				<a class="panel_a" onClick="deleteStorages();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>


				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
    
    
    <!-- Блок удаления складов -->
    <form name="delete_storages_form" method="POST">
        <input type="hidden" name="action" value="delete_storages" />
        <input type="hidden" name="storages" id="storages_to_delete" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <script>
        //Удаление складов
        function deleteStorages()
        {
            var storages = getCheckedElements();
            
            if(storages.length == 0)
            {
                alert("<?php echo translate_str_by_id(3470); ?>");
                return;
            }
            if(!confirm("<?php echo translate_str_by_id(3471); ?>"))
            {
                return;
            }
            
            
            
            
            document.getElementById("storages_to_delete").value = JSON.stringify(storages);
            document.forms["delete_storages_form"].submit();
        }
    </script>
    
    
    
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3472); ?>
			</div>
			<div class="panel-body">
				<div class="epc-cp-table-filter-bar" style="display:flex;flex-wrap:wrap;gap:10px;align-items:center;margin:0 0 12px;">
					<label for="epc_storages_filter" style="margin:0;font-weight:700;font-size:12px;color:#64748b;">Find warehouse</label>
					<input type="search" id="epc_storages_filter" class="form-control input-sm" style="max-width:320px;" placeholder="Name, code, price list, ID…" autocomplete="off" />
					<span class="text-muted small">Showing <strong id="epc_storages_filter_count">—</strong> warehouses</span>
				</div>
				<div class="table-responsive">
					<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped" id="epc_storages_table">
						<thead> 
							<tr> 
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
								<th>ID</th>
								<th></th>
								<th><?php echo translate_str_by_id(2277);//Название ?></th>
								<th><?php echo translate_str_by_id(3473);//Псевдоним ?></th>
								<th><?php echo translate_str_by_id(4639);//Прайс-лист ?></th>
								<th><?php echo translate_str_by_id(3474);//Тип интерфейса ?></th>
								<th></th>
								<th><?php echo translate_str_by_id(3475);//Тип товара ?></th>
							</tr>
						</thead>
						<tbody>
						<?php
						
						//Массивы для JS с id элементов и с чекбоксами элементов
						$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
						$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
						
						
						$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS *, (SELECT `name` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) AS `interface_type_name`, (SELECT `product_type` FROM `shop_storages_interfaces_types` WHERE `id` = `shop_storages`.`interface_type`) AS `product_type` FROM `shop_storages`;");
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
						
						
						//----------------------------------------------------------------------------------------------
						//Список прайс-листов
						$all_prices_list = array();
						$elements_prices_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices`;");
						$elements_prices_query->execute();
						while($element_prices_record = $elements_prices_query->fetch()){
							$all_prices_list[$element_prices_record['id']] = $element_prices_record;
						}
						//----------------------------------------------------------------------------------------------
						
						
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
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["id"].";\n";//Добавляем элемент для JS
							
							$a_item = "<a href=\"".$DP_Config->domain_path.$DP_Config->backend_dir."/shop/logistics/storages/storage?id=".$element_record["id"]." \">";
							
							$prices_list_name = '';
							if($element_record["interface_type"] == 2){
								$connection_options = json_decode($element_record["connection_options"], true);
								if(isset($all_prices_list[$connection_options['price_id']])){
									$prices_list_name = $connection_options['price_id'] .' - '. $all_prices_list[$connection_options['price_id']]['name'];
								}
							}
							$epc_filter_hay = strtolower(trim(
								(string) $element_record['id'] . ' ' .
								(string) $element_record['name'] . ' ' .
								(string) $element_record['short_name'] . ' ' .
								(string) $prices_list_name . ' ' .
								(string) ($element_record['interface_type_name'] ?? '')
							));
						?>
							<tr data-epc-filter="<?php echo htmlspecialchars($epc_filter_hay, ENT_QUOTES, 'UTF-8'); ?>">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>"/></td>
								<td><?php echo $a_item.$element_record["id"]; ?></a></td>
								<td><?php echo ($element_record['hidden'])?'<i class="fa fa-ban" aria-hidden="true" title="<?php echo translate_str_by_id(4640); ?>"></i>':''; ?></td>
								<td><?php echo $a_item.$element_record["name"]; ?></a></td>
								<td><?php echo $a_item.$element_record["short_name"]; ?></a></td>
								<td><?php echo $a_item.$prices_list_name; ?></a></td>
								<td><?php echo $a_item.$element_record["interface_type_name"]; ?></a></td>
								<td style="width: 40px; text-align: center;">
									<?php 
									$storage_image = '';
									switch($element_record["interface_type"]){
										case 1: $storage_image = 'package.png'; break;
										case 2: $storage_image = 'xls.png'; break;
										case 6: break;
										default:  $storage_image = 'api.png'; 
									}
									?>
									<img src="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/content/shop/logistics/images/<?=$storage_image;?>" alt="" style="max-height: 32px; width: auto;">
								</td>
								<td>
								<?php
								if( isset($product_types[$element_record["product_type"]]) )
								{
									echo $a_item.$product_types[$element_record["product_type"]];
									?>
									</a>
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
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/logistics/storages?s_page=$i"; ?>"><?php echo $i; ?></a></li>
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
	
	
	
    
    
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
                    <a class="showhide"><i class="fa fa-chevron-up"></i></a>
                </div>
				<?php echo translate_str_by_id(2396); ?>
			</div>
			<div class="panel-body">
				
				<?php echo translate_str_by_id(4641); ?>
				
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

	(function () {
		function bindStoragesFilter() {
			if (typeof window.epcCpBindTableFilter === 'function') {
				window.epcCpBindTableFilter({
					inputId: 'epc_storages_filter',
					tableId: 'epc_storages_table',
					countId: 'epc_storages_filter_count'
				});
			}
		}
		if (document.readyState === 'loading') {
			document.addEventListener('DOMContentLoaded', bindStoragesFilter);
		} else {
			bindStoragesFilter();
		}
	})();
    </script>
    
    <?php
}//~else - вывод страницы
?>