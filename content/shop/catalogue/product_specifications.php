<?php
//Свойства
$properties = array();

//Получаем основные свойства товара по category_id
$properties_query = $db_link->prepare('SELECT * FROM `shop_categories_properties_map` WHERE `category_id` = :category_id ORDER BY `order` ASC;');
$properties_query->bindValue(':category_id', $category_id);
$properties_query->execute();
while($property_record = $properties_query->fetch())
{
	$property_caption = translate_str_by_id($property_record['value']);
	$property_value = '';
	
	$property_id = $property_record["id"];
	$list_id = $property_record["list_id"];//ID списка - если свойство списковое
	
	//Получаем значение данного свойства для товара:
	$table_postfix = $property_types_tables[(string)$property_record["property_type_id"]];//Постфикс таблицы
	$property_value_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_properties_values_'.$table_postfix.'` WHERE `product_id` = :product_id AND `property_id` = :property_id;');
	$property_value_query->bindValue(':product_id', $product_id);
	$property_value_query->bindValue(':property_id', $property_id);
	$property_value_query->execute();
	$property_value_query_count = $property_value_query->fetchColumn();

	if($property_value_query_count > 0)
	{
		$property_value_query = $db_link->prepare('SELECT `id`, `value` FROM `shop_properties_values_'.$table_postfix.'` WHERE `product_id` = :product_id AND `property_id` = :property_id;');
		$property_value_query->bindValue(':product_id', $product_id);
		$property_value_query->bindValue(':property_id', $property_id);
		$property_value_query->execute();
		
		//Задаем значение
		switch($property_record["property_type_id"])
		{
			case 1:
			case 2:
				$property_value_record = $property_value_query->fetch();
				$property_value = $property_value_record["value"];
				break;
			case 3:
				$property_value_record = $property_value_query->fetch();
				$property_value = translate_str_by_id($property_value_record["value"]);
				break;
			case 4:
				$property_value_record = $property_value_query->fetch();
				$property_value = $property_value_record["value"];
				if( (int)$property_value == 1 )
				{
					$property_value = 2456;//Да
				}
				else
				{
					$property_value = 2457;//Нет
				}
				$property_value = translate_str_by_id($property_value);
				break;
			case 5:
				//Свойство списковое - значений может быть несколько
				$list_property_items = array();
				while($property_value_record = $property_value_query->fetch())
				{
					array_push($list_property_items, (integer)$property_value_record["value"]);
				}
				//Теперь получаем названия значений свойств из линейных списков
				$line_list_items = array();
				
				$line_list_items_query = $db_link->prepare('SELECT `id`, `value` FROM `shop_line_lists_items` WHERE `line_list_id` = :line_list_id ORDER BY `order`;');
				$line_list_items_query->bindValue(':line_list_id', $list_id);
				$line_list_items_query->execute();
				while( $list_item = $line_list_items_query->fetch() )
				{
					array_push($line_list_items, array("id"=>$list_item["id"], "value"=>$list_item["value"]) );
				}
				$line_list_values_text = "";//Текстовая строка для вывода значений линейного списка
				for($L=0; $L < count($line_list_items); $L++)
				{
					if(array_search((integer)$line_list_items[$L]["id"], $list_property_items) !== false)
					{
						if($line_list_values_text != "") $line_list_values_text .= ", ";
						$line_list_values_text .= translate_str_by_id($line_list_items[$L]["value"]);
					}
				}
				$property_value = $line_list_values_text;
				break;
			case 6:
				//Свойство типа "Древовидный список" - значений может быть несколько
				$list_property_items = array();
				while($property_value_record = $property_value_query->fetch())
				{
					array_push($list_property_items, (int)$property_value_record["value"]);
				}
				//Переводим в строку:
				$list_property_items = json_encode($list_property_items);
				$list_property_items = str_replace( array("[", "]") , "", $list_property_items);
				
				//Теперь формируем строку для отображения значения данного свойства
				//Данное свойство отображаем в виде цепочек для каждой ветви
				$property_variants = array();//Массив для всех цепочек данного свойства
				
				$tree_items_query = $db_link->prepare('SELECT `id`, `value`, `level`, `count`, `parent` FROM `shop_tree_lists_items` WHERE `id` IN ('.$list_property_items.') ORDER BY `level` DESC, `order` ASC;');
				$tree_items_query->execute();
				while( $tree_item = $tree_items_query->fetch() )
				{
					$has_added = false;
					for( $pv = 0 ; $pv < count($property_variants); $pv++)
					{
						if($property_variants[$pv][ 0 ]["parent"] == $tree_item["id"])
						{
							array_unshift($property_variants[$pv], array("value"=>translate_str_by_id($tree_item["value"]), "level"=>$tree_item["level"], "count"=>$tree_item["count"], "parent"=>$tree_item["parent"], "id"=>$tree_item["id"]) );
							
							$has_added = true;
						}
					}
					if( ! $has_added )
					{
						array_unshift($property_variants, array());
						
						array_unshift($property_variants[ 0 ], array("value"=>translate_str_by_id($tree_item["value"]), "level"=>$tree_item["level"], "count"=>$tree_item["count"], "parent"=>$tree_item["parent"], "id"=>$tree_item["id"]) );
					}
				}
				$property_value = $property_variants;
				break;
		}
		
		if($property_value !== ''){
			$properties[] = array('caption' => $property_caption, 'value' => $property_value);
		}
	}//~if() - есть значение свойства
}

if(!empty($properties)){
	?>
	<table class="table">
		<tr>
			<th><?php echo translate_str_by_id(2102); ?></th>
			<th><?php echo translate_str_by_id(4170); ?></th>
		</tr>
	<?php
	foreach($properties as $item_property){
		?>
		<tr>
			<td><?php echo $item_property['caption'];?></td>
			<td>
			<?php
			if(is_array($item_property['value'])){
				$property_variants = $item_property['value'];
				echo '<div style="max-height: 500px; overflow: auto;">';
				for( $pv = 0 ; $pv < count($property_variants); $pv++)
				{
					if($pv > 0){echo "<br/>";}
					for( $pv_i = 0 ; $pv_i < count($property_variants[$pv]); $pv_i++)
					{
						if($pv_i > 0){echo " ";}
						if($pv_i == 0){echo "<b>";}
						if($pv_i == 2){echo '<br/><small style="position: relative; top: -5px;">';}
						
						echo html_entity_decode($property_variants[$pv][$pv_i]["value"]);
						
						if($pv_i == 0){echo "</b>";}
						if($pv_i == 2){echo "</small>";}
					}
				}
				echo "</div>";
			}else{
				echo $item_property['value'];
			}
			?>
			</td>
		</tr>
		<?php
	}
	?>
	</table>
	<?php
}else{
	echo translate_str_by_id(4171);
}
?>