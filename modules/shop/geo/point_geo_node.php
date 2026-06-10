<?php
/**
 * Модуль выбора своего географического узла
*/
/*
Логика работы:
- если гео-узел - единственный, то модуль скрывается и автоматически ставит этот населенный пункт
- если населенных пунктов несколько,
	- если свой гео-узел еще не выбран - выдается окно для выбора
	- если узел выбран, то просто показывается модуль
*/
defined('_ASTEXE_') or die('No access');

/** Flat shop_geo list — avoids broken recursive JSON tree (PHP 8 count / encode failures). */
function epc_geo_render_flat_list(PDO $db_link)
{
	$limit = 800;
	$sql = 'SELECT `id`, `value`, `level` FROM `shop_geo` ORDER BY `level`, `order` LIMIT ' . (int) $limit;
	$stmt = $db_link->prepare($sql);
	if (!$stmt || !$stmt->execute()) {
		echo '<div class="geo_default_level">Location list unavailable</div>';
		return;
	}
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		try {
			$level = (int) ($row['level'] ?? 0);
			if ($level === 1) {
				$geo_node_class = 'geo_top_level';
			} elseif ($level === 2) {
				$geo_node_class = 'geo_second_level';
			} else {
				$geo_node_class = 'geo_default_level';
			}
			$raw = isset($row['value']) ? (string) $row['value'] : '';
			$label = $raw;
			if ($raw !== '' && function_exists('translate_str_by_id')) {
				$label = (string) translate_str_by_id($raw);
			}
			$label = htmlspecialchars($label, ENT_QUOTES, 'UTF-8');
			if ($geo_node_class === 'geo_default_level') {
				echo '<div class="' . $geo_node_class . '" onclick="set_my_city(' . (int) $row['id'] . ');" style="cursor:pointer">' . $label . '</div>';
			} else {
				echo '<div class="' . $geo_node_class . '">' . $label . '</div>';
			}
		} catch (Throwable $e) {
			continue;
		}
	}
}

if( isset($_COOKIE["my_city"]) )
{
	$stmt = $db_link->prepare('SELECT * FROM `shop_geo` WHERE `id` = :id;');
	$stmt->bindValue(':id', $_COOKIE["my_city"]);
	$stmt->execute();
	$node = $stmt->fetch();
	if(empty($node)){
		$_COOKIE["my_city"] = null;
	}
}

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>


<script>
//Устновка своего города
function set_my_city(id)
{
    //Увеличиваем наличие на сервере и только после этого отображаем
    jQuery.ajax({
        type: "POST",
        async: false, //Запрос синхронный
        url: "/modules/shop/geo/ajax_set_my_city.php",
        dataType: "json",//Тип возвращаемого значения
        data: "geo_id="+id+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
        success: function(answer)
        {
            if(answer == 1)
            {
                location.reload();
            }
        }
    });
}
</script>



<?php
//Если географический узел не единственный - выводим модуль выбора города
$stmt = $db_link->prepare('SELECT COUNT(*) FROM `shop_geo`;');
$stmt->execute();
if( $stmt->fetchColumn() > 1 )
{
	//Гео-узлов больше одного. Получаем первый гео-узел
	$stmt = $db_link->prepare('SELECT `id`, `value` FROM `shop_geo`;');
	$stmt->execute();
	$first_node = $stmt->fetch();
	
    //Выставляем текущий город
    $city_name = translate_str_by_id($first_node["value"]);//Первый узел из списка
	?>
	
    <!-- Start Модальное окно -->
        <div style="display:none" id="modal_content_div">
        	<div class="popup_content">
        		<a href="javascript:void(0);" class="popup_window_close" style="text-decoration:none;"><span style="position: relative; top: -5px; left: -3px;">X</span></a>
				<font style="font-weight:bold;"><?php echo translate_str_by_id(4771); ?></font>
        		<div class="geo_list">
        			<?php
        			try {
        				epc_geo_render_flat_list($db_link);
        			} catch (Throwable $e) {
        				// Keep storefront rendering if geo picker fails.
        			}
        			?>
        		</div>
        	</div>
        </div>
        <script>
        	//Создание модального окна
        	var div_modal = document.createElement('div');//Объект DIV
        	div_modal.setAttribute('class', 'popup_window');//Класс в соответствии со стилем
        	div_modal.innerHTML = document.getElementById("modal_content_div").innerHTML;//Содержимое окна берем из образца
        	document.body.insertBefore(div_modal, document.body.firstChild);//Добавляем окно в самое начало BODY. Т.о. окно будет выше всех
        </script>
        <script>
        	modal_geo_list = $('.popup_window');
        	modal_geo_list.click(function(event) {
        		e = event || window.event
        		if (e.target == this) {
        			$(modal_geo_list).css('display', 'none')
        		}
        	});
        	$('.popup_window_close').click(function() {
        		modal_geo_list.css('display', 'none');
        	});
        	
        	// ----------------------------------------------------------------
        	function openPopupWindow_CityList()
        	{
        		modal_geo_list.css('display', 'block');
        	}
        	// ----------------------------------------------------------------
        </script>
    <!-- End Модальное окно -->
	<?php
    //Если есть установленные куки
    if( isset($_COOKIE["my_city"]) )
    {
		$stmt = $db_link->prepare('SELECT `value` FROM `shop_geo` WHERE `id` = :id;');
		$stmt->bindValue(':id', $_COOKIE["my_city"]);
		$stmt->execute();
		$city_name_record = $stmt->fetch(PDO::FETCH_ASSOC);
        if( $city_name_record != false )
        {
            $city_name = translate_str_by_id($city_name_record["value"]);
			
			?>
			<div class="customer_city" id="customer_city" onclick="openPopupWindow_CityList();">
				<i style="font-size: 1.2em;" class="fa fa-map-marker" aria-hidden="true"></i> <span><?php echo $city_name; ?></span>
			</div>
			<?php
        }
    }
    else//куки не установлены - выдаем окно выбора города
    {
        ?>
        <script>
			openPopupWindow_CityList();
        </script>
        <?php
    }
}//~if(mysqli_num_rows($geo_count) > 1)
else//Если единственный географический узел - ставим его в куки
{
	if (!isset($_COOKIE['my_city']) || $_COOKIE['my_city'] === '' || $_COOKIE['my_city'] === null) {
		$single_stmt = $db_link->prepare('SELECT `id` FROM `shop_geo` ORDER BY `level`, `order` LIMIT 1;');
		$single_stmt->execute();
		$single_node = $single_stmt->fetch(PDO::FETCH_ASSOC);
		if (!empty($single_node['id'])) {
		?>
		<script>
			set_my_city(<?php echo (int) $single_node['id']; ?>);
		</script>
		<?php
		}
	}
}



// *****************************************************************************************************
//Рекурсивная функция для вывода географических узлов
function printGeoNodes($geo_tree_dump, $depth = 0)
{
    if (!is_array($geo_tree_dump) || $depth > 32) {
        return;
    }
    for ($i = 0, $n = count($geo_tree_dump); $i < $n; $i++)
    {
        if (!isset($geo_tree_dump[$i]) || !is_array($geo_tree_dump[$i])) {
            continue;
        }
        switch((integer)$geo_tree_dump[$i]['level'])
    	{
    		case 1:
    			$geo_node_class = "geo_top_level";
    			break;
    		case 2:
    			$geo_node_class = "geo_second_level";
    			break;
    		default:
    			$geo_node_class = "geo_default_level";
    			break;
    	}
    	
		if($geo_node_class == "geo_default_level"){
		?>
		<div class="<?php echo $geo_node_class; ?>" onclick="set_my_city(<?php echo $geo_tree_dump[$i]["id"]; ?>);" style="cursor:pointer"><?php echo $geo_tree_dump[$i]["value"]; ?></div>
		<?php
		}else{
		?>
		<div class="<?php echo $geo_node_class; ?>"><?php echo $geo_tree_dump[$i]["value"]; ?></div>
		<?php
		}
		
	    $child_nodes = (isset($geo_tree_dump[$i]['data']) && is_array($geo_tree_dump[$i]['data']))
	        ? $geo_tree_dump[$i]['data']
	        : array();
	    if (count($child_nodes) > 0) {
	        printGeoNodes($child_nodes, $depth + 1);
	    }
    }//~for($i=0; $i < count($geo_tree_dump); $i++)
}//~function printGeoNodes($geo_tree_dump)
?>