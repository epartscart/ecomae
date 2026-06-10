<?php
/*
Скрипт страницы с отображением подключенных устройств
*/
defined('_ASTEXE_') or die('No access');

if( isset( $_POST["action"] ) )
{
	
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
				Действия
			</div>
			<div class="panel-body">
				
				<?php
				//Страница Чеки
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/onlajn-kassy/checks", "background_color"=>"#63ce1c", "fontawesome_class"=>"fas fa-receipt", "caption"=>"Чеки") );
				
				//Корневой раздел по онлайн-кассам
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/onlajn-kassy", "background_color"=>"#3498db", "fontawesome_class"=>"fas fa-receipt", "caption"=>"Корневой раздел по чекам") );
				?>
				
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption">Выход</div>
				</a>
			</div>
		</div>
	</div>
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Подключенные устройства
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<?php
					//Получаем список кассовых аппаратов
					$kkt_devices_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS *, (SELECT `description` FROM `shop_kkt_interfaces_types` WHERE `handler` = `shop_kkt_devices`.`handler` LIMIT 1) AS `interface_description` FROM `shop_kkt_devices`");
					$kkt_devices_query->execute();
					
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					//Массивы для JS с id элементов и с чекбоксами элементов
					$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
					$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
					?>
					
					<table id="" class="footable table table-hover toggle-arrow " data-sort="false" data-page-size="<?php echo $elements_count_rows; ?>">
						<thead>
							<th style="width:30px;"><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /></th>
							<th style="width:30px;">ID</th>
							<th>Наименование</th>
							<th>Интерфейс реальной ККТ</th>
							<th>Действия</th>
						</thead>
						<tbody>
				
						<?php
						while( $item = $kkt_devices_query->fetch() )
						{
							//Для Javascript
							$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$item["id"]."\";\n";//Добавляем элемент для JS
							$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$item["id"].";\n";//Добавляем элемент для JS
							?>
							<tr class="" id="">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $item["id"]; ?>');" id="checked_<?php echo $item["id"]; ?>" name="checked_<?php echo $item["id"]; ?>" /></td>
								
								<td><?php echo $item["id"]; ?></td>
								
								<td><?php echo translate_str_by_id($item["name"]); ?></td>
								
								<td>
									<?php
									if( $item["interface_description"] == "" )
									{
										?>
										Реальная касса не подключена. Чеки с сайта не отправляются
										<?php
									}
									else
									{
										echo $item["interface_description"];
									}
									?>
								</td>
								
								<td>
									<button class="btn btn-info " type="button" onclick="shop_kkt_device_info(<?php echo $item["id"]; ?>);"><i class="fa fa-info"></i> Информация</button>
								</td>
								
							</tr>
							<?php
						}
						?>
						</tbody>
					</table>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<script>
	//Показываем подробную информацию по кассе
	function shop_kkt_device_info(kkt_device_id)
	{
		document.getElementById("kkt_info_div").innerHTML = "<div class=\"text-center\">Пожалуйста, подождите, идет запрос информации...<br><img src=\"/content/files/images/ajax-loader-transparent.gif\" /></div>";
		
		
		jQuery('#modal_kkt_device_info').modal();//ОТКРЫВАЕМ ОКНО
		
		
		jQuery.ajax({
			type: "POST",
			async: true,
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/kkt/ajax_get_kkt_device_info.php",
			dataType: "text",//Тип возвращаемого значения
			data: "kkt_device_id="+kkt_device_id+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			success: function(kkt_device_info){
				document.getElementById("kkt_info_div").innerHTML = kkt_device_info;
			}
		});
		
	}
	</script>
	<div class="text-center m-b-md">
		<div class="modal fade" id="modal_kkt_device_info" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title">Подробная информация по ККТ</h4>
					</div>
					<div class="modal-body">
						<div class="row" id="kkt_info_div">
							
						</div>
					</div>
					<div class="modal-footer">
						<button type="button" class="btn btn-default" data-dismiss="modal">OK</button>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<script>
    // ----------------------------------------------------------------------------------------
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
    // ----------------------------------------------------------------------------------------
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
    // ----------------------------------------------------------------------------------------
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
    // ----------------------------------------------------------------------------------------
    </script>
	
	<?php
}
?>