<?php
/*
Скрипт страницы сравнения товаров
*/
defined('_ASTEXE_') or die('No access');


//Получить список магазинов покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");


//Техническая информация по интернет-магазину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");


//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$userProfile = DP_User::getUserProfile();
$group_id = $userProfile["groups"][0];//Берем первую группу пользователя


//Функция добавления в корзину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/common_add_to_basket.php");


//Подключаем скрипт с общей функцией вывода блока товара ( printProductBlock(product) )
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/helper.php");
//ТИП БЛОКА (1,2,3,4,5,6)
$product_block_type = 7;//ТИП ДЛЯ БЛОКОВ ТОВАРОВ В ЗАКЛАДКАХ


$main_class_of_block = "product_div_tile col-xs-12 col-sm-12 col-md-12 col-lg-12 product_div_tile-fixed_width";



//Получаем закладки
$compare = NULL;
if( isset($_COOKIE["compare"]) )
{
	$compare = $_COOKIE["compare"];
}
if($compare == NULL || $compare == "[]")
{
	?>
	<p><?php echo translate_str_by_id(4081); ?></p>
	<p><?php echo translate_str_by_id(4082); ?></p>
	<?php
}
else
{
	$compare = json_decode($compare, true);
	for( $c=0; $c < count($compare) ; $c++ )
	{
		$compare[$c] = (int)$compare[$c];
	}
	$compare = json_encode($compare);
	
	
	$compare = str_replace( array("[", "]"), "", $compare);
	
	
	//Получаем список категорий товаров, которые добавлены в сравнения. И указываем текущую категорию
	$category_id = (int)$_GET["category_id"];
	$categories = array();
	$stmt = $db_link->prepare('SELECT DISTINCT(`id`) AS `id`, `value` FROM `shop_catalogue_categories` WHERE `id` IN (SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` IN ('.$compare.') );');
	$stmt->execute();
	while( $category = $stmt->fetch(PDO::FETCH_ASSOC) )
	{
		$current = false;
		if($category_id == 0 && count($categories) == 0)//Если категрия не указана в $_GET - ставим первую категорию
		{
			$category_id = $category["id"];
			$current = true;
		}
		else if($category_id == $category["id"])//Если категория указана - ставим ее
		{
			$current = true;
		}
		
		array_push($categories, array("id"=>$category["id"], "value"=>translate_str_by_id($category["value"]), "current"=>$current) );
	}
	
	
	// Подстрока для запроса товаров
	$products_ids_str = $compare;
	
	//Подключение построение запроса
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");
	
	//Подключаем скрипт генерации объектов товаров единого формата ( $products_objects )
	require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/generate_products_objects_by_sql.php");
	?>
	
	
	<script>
	var categories_products = new Object;//Карта товаров по категориям
	<?php
	foreach( $products_objects AS $id => $product )
	{
		?>
		if(categories_products[<?php echo $product["category_id"]; ?>] == undefined)
		{
			categories_products[<?php echo $product["category_id"]; ?>] = new Array();
		}
		categories_products[<?php echo $product["category_id"]; ?>].push(<?php echo $product["id"]; ?>);
		<?php
	}
	?>
	
	var category_properties = new Array();
	</script>
	
	
	<?php
	$is_description = false;//Флаг - есть ли текстовое описание у товаров
	
	//Выводим HTML для блоков товаров
	foreach( $products_objects AS $id => $product )
	{
		if($product["description"] != "")
		{
			$is_description = true;
		}
		?>
		<div class="hidden" id="product_block_<?php echo $id; ?>">
			
			<?php
			printProductBlock($product);
			?>
			
		</div>
		<?php
	}
	?>
	
	
	
	<div class="col-lg-12" id="work_area">
	</div>
		

	<?php
	//Получаем список свойств категории
	$stmt = $db_link->prepare('SELECT * FROM `shop_categories_properties_map` WHERE `category_id` = :category_id ORDER BY `order` ASC;');
	$stmt->bindValue(':category_id', $category_id);
	$stmt->execute();
	while( $category_property = $stmt->fetch(PDO::FETCH_ASSOC) )
	{
		$property_type_id = $category_property["property_type_id"];
		$property_id = $category_property["id"];
		$caption = $category_property["value"];
		$list_id = $category_property["list_id"];//Используется только для списков
		
		?>
		<script>
			category_properties[category_properties.length] = new Object;
			category_properties[category_properties.length - 1].property_id = <?php echo $property_id; ?>;
			category_properties[category_properties.length - 1].property_type_id = <?php echo $property_type_id; ?>;
			category_properties[category_properties.length - 1].caption = "<?php echo translate_str_by_id($caption); ?>";
			category_properties[category_properties.length - 1].list_id = <?php echo $list_id; ?>;
		</script>
		<?php
		
		
		//Далее в зависимости от типа свойства - получаем его значения для товаров
		switch($property_type_id)
		{
			case 1:
			case 2:
			case 3:
				if($property_type_id == 1)
				{
					$table_postfix = "int";
					?>
					<script>
						category_properties[category_properties.length - 1].data_type = "number";
					</script>
					<?php
				}
				else if($property_type_id == 2)
				{
					$table_postfix = "float";
					?>
					<script>
						category_properties[category_properties.length - 1].data_type = "number";
					</script>
					<?php
				}
				else if($property_type_id == 3)
				{
					$table_postfix = "text";
					?>
					<script>
						category_properties[category_properties.length - 1].data_type = "text";
					</script>
					<?php
				}
				
				
				foreach( $products_objects AS $id => $product )
				{
					$value_query = $db_link->prepare('SELECT `value` FROM `shop_properties_values_'.$table_postfix.'` WHERE `property_id` = :property_id AND `product_id` = :product_id;');
					$value_query->bindValue(':property_id', $property_id);
					$value_query->bindValue(':product_id', $id);
					$value_query->execute();
					$value_record = $value_query->fetch(PDO::FETCH_ASSOC);
					$value = "";
					if( $value_record != false )
					{
						$value = $value_record["value"];
					}
					
					
					if( $property_type_id == 3 )
					{
						$value = translate_str_by_id($value);
					}
					
					
					$products_objects[$id]["property_id_$property_id"] = $value;
				}
				break;
			case 4:
				foreach( $products_objects AS $id => $product )
				{
					$value_query = $db_link->prepare('SELECT `value` FROM `shop_properties_values_bool` WHERE `property_id` = :property_id AND `product_id` = :product_id;');
					$value_query->bindValue(':property_id', $property_id);
					$value_query->bindValue(':product_id', $id);
					$value_query->execute();
					$value_record = $value_query->fetch(PDO::FETCH_ASSOC);
					
					$value = "";
					if( $value_record != false )
					{
						$value = $value_record["value"];
					}
					if($value == 1)
					{
						$value = translate_str_by_id(2456);
					}
					else
					{
						$value = translate_str_by_id(2457);
					}
					
					$products_objects[$id]["property_id_".$property_id] = $value;
					
					?>
					<script>
						category_properties[category_properties.length - 1].data_type = "text";
					</script>
					<?php
				}
				break;
			case 5:
				//Получаем тип списка:
				$value_query = $db_link->prepare('SELECT `type`, `data_type` FROM `shop_line_lists` WHERE `id` = :id;');
				$value_query->bindValue(':id', $list_id);
				$value_query->execute();
				$list_info_record = $value_query->fetch(PDO::FETCH_ASSOC);
				$list_type = $list_info_record["type"];
				$data_type = $list_info_record["data_type"];
				?>
				<script>
					category_properties[category_properties.length - 1].list_type = <?php echo $list_type; ?>;
					category_properties[category_properties.length - 1].data_type = '<?php echo $data_type; ?>';
					if(category_properties[category_properties.length - 1].list_type == 2)
					{
						category_properties[category_properties.length - 1].list_items = new Array;
					}
				</script>
				<?php
				if($list_type == 1)//Для единичного списка
				{
					
					foreach( $products_objects AS $id => $product )
					{
						$value_query = $db_link->prepare('SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `property_id` = :property_id AND `product_id` = :product_id);');
						$value_query->bindValue(':property_id', $property_id);
						$value_query->bindValue(':product_id', $id);
						$value_query->execute();
						$value_record = $value_query->fetch(PDO::FETCH_ASSOC);
						$value = "";
						if( $value_record != false )
						{
							$value = $value_record["value"];
						}
						
						$products_objects[$id]["property_id_$property_id"] = translate_str_by_id($value);
					}
				}
				else//Для множественного списка
				{
					//Получаем элементы списка
					$list_items = array();
					$list_items_query = $db_link->prepare('SELECT `id`, `value` FROM `shop_line_lists_items` WHERE `line_list_id` = :line_list_id ORDER BY `order`;');
					$list_items_query->bindValue(':line_list_id', $list_id);
					$list_items_query->execute();
					while( $list_item = $list_items_query->fetch(PDO::FETCH_ASSOC) )
					{
						array_push($list_items, array("id"=>$list_item["id"], "value"=>$list_item["value"]) );
						?>
						<script>
							category_properties[category_properties.length - 1].list_items.push({"id":<?php echo $list_item["id"]; ?>, "value":"<?php echo translate_str_by_id($list_item["value"]); ?>"});
						</script>
						<?php
					}
					
					foreach($list_items AS $item_index => $item_object )
					{
						foreach( $products_objects AS $id => $product )
						{
							$value_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_properties_values_list` WHERE `property_id` = :property_id AND `product_id`=:product_id AND `value`=:value;');
							$value_query->bindValue(':property_id', $property_id);
							$value_query->bindValue(':product_id', $id);
							$value_query->bindValue(':value', $list_items[$item_index]["id"]);
							$value_query->execute();
							
							
							if( $value_query->fetchColumn() == 1 )
							{
								$value = translate_str_by_id(2456);
							}
							else
							{
								$value = translate_str_by_id(2457);
							}
							
							$products_objects[$id]["property_id_".$property_id."_".$list_items[$item_index]["id"]] = $value;
						}
					}
				}
				break;
		}
	}//while - по всем свойствам
	?>
	
	
	
	<script>
	// ----------------------------------------------------------
	//Обработка переключения селектора категорий
	function onCategoryChange()
	{
		location="<?php echo $multilang_params['lang_href']; ?>/shop/sravneniya?category_id="+document.getElementById("category_select").value;
	}
	// ----------------------------------------------------------
	//Удаление выбранной категории
	function removeCurrentGroup()
	{
		//Товары в куки
		var compare = getCookie('compare');
		compare = JSON.parse(compare);
		
		//Товары на удаление
		var compare_to_del = categories_products[document.getElementById("category_select").value];
		
		//Удаляем товары из массива. Цикл в обратную сторону - чтобы после удаления индексы не сдвигались
		for(var i= compare.length -1; i >= 0; i--)
		{
			if( compare_to_del.indexOf(compare[i]) >= 0 )
			{
				compare.splice(i,1);
			}
		}
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "compare="+JSON.stringify(compare)+"; path=/; expires=" + date.toUTCString();
		
		//Перезагружаем страницу уже без параметра категории, т.к. текущая категория удаляется
		location="<?php echo $multilang_params['lang_href']; ?>/shop/sravneniya";
	}
	// --------------------------------------------------------------------------------------
	//Функция переотображения всего массива products_objects
	function allReview()
	{
		document.getElementById("work_area").innerHTML = "";//Сбрасываем отображение
		
		
		fields_actions = "<option value=\"cancel\"></option><option value=\"asc\"><?php echo translate_str_by_id(4084); ?></option><option value=\"desc\"><?php echo translate_str_by_id(4083); ?></option>";
		
		
		var html = "<table class=\"table table-nonfluid\">";
			html += "<thead>";
				html += "<tr>";
					html += "<th><div class=\"<?php echo $main_class_of_block; ?>\"><div class=\"form-group\"><label for=\"category_select\" class=\"col-lg-12 control-label\"><?php echo translate_str_by_id(2990); ?></label><div class=\"col-lg-12\"><select class=\"form-control\" onchange=\"onCategoryChange();\" id=\"category_select\">";
					<?php
					for($i=0; $i < count($categories); $i++)
					{
						$selected = "";
						if( $categories[$i]["current"] )
						{
							$selected = " selected = 'selected' ";
						}
						?>
						html += "<option <?php echo $selected; ?> value=\"<?php echo $categories[$i]["id"]; ?>\"><?php echo $categories[$i]["value"]; ?></option>";
						<?php
					}
					?>
					html += "</select></div></div><div class=\"text-right\"><a href=\"javascript:void(0);\" onclick=\"removeCurrentGroup();\" title=\"<?php echo translate_str_by_id(4085); ?>\"><i class=\"fa fa-remove\"></i><span> <?php echo translate_str_by_id(4086); ?></span></a></div></div></th>";
					
					//Выводим блоки товаров
					if( products_objects.length > 0 )
					{
						for(var i=0; i < products_objects.length; i++)
						{
							html += "<th>";
							
							html += products_objects[i].product_block_HTML;
							
							html += "</th>";
						}
					}
				html += "</tr>";
			html += "</thead>";
			
			//Текстовое описание - пока не выводим:
			<?php
			if($is_description && false)
			{
				?>
				if( products_objects.length > 0 )
				{
					html += "<tbody";
						html += "<tr>";
							html += "<td><?php echo translate_str_by_id(2073); ?></td>";
							
							for(var i=0; i < products_objects.length; i++)
							{
								html += "<td>"+products_objects[i].description+"</td>";
							}
							
						html += "</tr>";
					html += "</tbody";
				}
				<?php
			}
			?>
			
			
			

			//Свойства
			if( products_objects.length > 0 )
			{
				for(var i=0; i < category_properties.length; i++)
				{
					switch( category_properties[i].property_type_id )
					{
						case 1:
						case 2:
						case 3:
						case 4:
							html += "<tbody>";
								html += "<tr>";
									html += "<td id=\"property_id_"+category_properties[i].property_id+"\"><select onClosed=\"alert('ok');\" id=\"select_property_id_"+category_properties[i].property_id+"\" onchange=\"field_action('property_id_"+category_properties[i].property_id+"', this, '"+category_properties[i].data_type+"');\" style=\"width:20px;\">"+fields_actions+"</select> "+category_properties[i].caption+"</td>"
									
									for(var p=0; p < products_objects.length; p++)
									{
										html += "<td>"+products_objects[p]["property_id_"+category_properties[i].property_id]+"</td>";
									}
									
								html += "</tr>";
							html += "</tbody>";
							break;
						case 5:
							if( parseInt(category_properties[i].list_type) == 1)//Единичный выбор
							{
								html += "<tbody>";
									html += "<tr>";
										html += "<td id=\"property_id_"+category_properties[i].property_id+"\"><select id=\"select_property_id_"+category_properties[i].property_id+"\" onchange=\"field_action('property_id_"+category_properties[i].property_id+"', this, '"+category_properties[i].data_type+"');\" style=\"width:20px;\">"+fields_actions+"</select> "+category_properties[i].caption+"</td>"
									
									for(var p=0; p < products_objects.length; p++)
									{
										html += "<td>"+products_objects[p]["property_id_"+category_properties[i].property_id]+"</td>";
									}
									
								html += "</tr>";
							html += "</tbody>";
							}
							else//Множественный выбор
							{
								html += "<tbody>";
									html += "<tr>";
										html += "<td colspan=\""+ (products_objects.length + 1) +"\"><h4>" + category_properties[i].caption + "</h4></td>";
									html += "</tr>";
									
									for(var j=0; j < category_properties[i].list_items.length; j++)
									{
										html += "<tr>";
											html += "<td id=\"property_id_"+category_properties[i].property_id+"_"+category_properties[i].list_items[j].id+"\"><select id=\"select_property_id_"+category_properties[i].property_id+"_"+category_properties[i].list_items[j].id+"\" onchange=\"field_action('property_id_"+category_properties[i].property_id+"_"+category_properties[i].list_items[j].id+"', this, '"+category_properties[i].data_type+"');\" style=\"width:20px;\">"+fields_actions+"</select> "+category_properties[i].list_items[j].value+"</td>";
											
											for(var p=0; p < products_objects.length; p++)
											{
												html += "<td>"+products_objects[p]["property_id_"+category_properties[i].property_id+"_"+category_properties[i].list_items[j].id]+"</td>";
											}
											
										html += "</tr>";
									}
									
								html += "</tbody>";
							}
							break;
					}
				}
			}
		html += "</table>";
		
		
		document.getElementById("work_area").innerHTML = html;
		
		
		//Выставляем обозначение сортировки
		if(sorter.field != "no" && products_objects.length > 0 )
		{
			document.getElementById(sorter.field).innerHTML += " <i class=\"fa fa-sort-amount-"+sorter.asc_desc+" fa-rotate-270\"></i>";
			
			document.getElementById("select_"+sorter.field).value = sorter.asc_desc;
		}
		
		if(products_objects.length == 0)
		{
			document.getElementById("work_area").innerHTML += "<?php echo translate_str_by_id(4087); ?>";
		}
	}
	// --------------------------------------------------------------------------------------
	var products_objects = new Array();
	<?php
	//Делаем вывод для JS для возможности управления отображением на стороне клиента
	foreach( $products_objects AS $id => $product )
	{
		?>
		products_objects.push(<?php echo json_encode($product); ?>);
		products_objects[products_objects.length - 1].product_block_HTML = document.getElementById("product_block_"+<?php echo $id; ?>).innerHTML;
		<?php
	}
	?>
	console.log(products_objects);
	// --------------------------------------------------------------------------------------
	//Сортировка массива товаров по указанному полю
	var sorter = new Object;
	sorter.field = "no";
	sorter.asc_desc = "asc";
	// --------------------------------------------------------------------------------------
	//Действие со свойством: сортировка, удаление и т.д.
	function field_action(field, select, data_type)
	{
		if(select.value == "cancel")
		{
			return;
		}
		else if( select.value == "asc" || select.value == "desc" )
		{
			//console.log("Сортируем по полю: " + field + ". Направление " + select.value+". Тип данных: " + data_type);
		
			//Объект сортировки
			sorter.field = field;
			sorter.asc_desc = select.value;
			
			//В зависимости от типа данных поля
			if(data_type == "text")
			{
				products_objects.sort(compareStrings);
			}
			else
			{
				products_objects.sort(compareNumbers);
			}
			
			//Переотображаем
			allReview();
		}
	}
	// --------------------------------------------------------------------------------------
	//Функция сравнения строковых значений
	function compareStrings(x, y)
	{
		if(sorter.asc_desc == "asc")
		{
			if(String(x[sorter.field]) > String(y[sorter.field]))
			{
				return 1;
			}
			else if(String(x[sorter.field]) < String(y[sorter.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
		else
		{
			if(String(x[sorter.field]) < String(y[sorter.field]))
			{
				return 1;
			}
			else if(String(x[sorter.field]) > String(y[sorter.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
	}
	// --------------------------------------------------------------------------------------
	//Функция сравнения числовых значений
	function compareNumbers(x, y)
	{
		if(sorter.asc_desc == "asc")
		{
			if(parseFloat(x[sorter.field]) > parseFloat(y[sorter.field]))
			{
				return 1;
			}
			else if(parseFloat(x[sorter.field]) < parseFloat(y[sorter.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
		else
		{
			if(parseFloat(x[sorter.field]) < parseFloat(y[sorter.field]))
			{
				return 1;
			}
			else if(parseFloat(x[sorter.field]) > parseFloat(y[sorter.field]))
			{
				return -1;
			}
			else
			{
				return 0;
			}
		}
	}
	// --------------------------------------------------------------------------------------
	
	allReview();//После загрузки страницы переотображаем
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	<?php
	//Если количество товаров превышает 4 - делаем горизотальную полосу прокрутки
	if( count($products_objects) > 4)
	{
		?>
		<script>
			document.getElementsByTagName("body")[0].setAttribute("style", "overflow-x: scroll;");
			document.getElementsByTagName("html")[0].setAttribute("style", "overflow-x: scroll;");
		</script>
		<?php
	}
}
?>