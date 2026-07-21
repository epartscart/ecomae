<?php
/**
 * Страница одного товара:
 * - создание;
 * - редактирование
*/
defined('_ASTEXE_') or die('No access');

//require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/tree_lists/dp_tree_list_item.php");//Определение класса элемента древовидного списка


//Мультиязычность. Массив с отдельными полями, которые содержать переводимые значения. Кроме свойств, текстового описани и стикеров.
$translated_items = array('caption', 'title_tag', 'description_tag', 'keywords_tag');


// ---------------------------------------------------------------------------------------------------
//Функция сортировки линейных списков по возрастанию для чисел
function sort_line_list_asc_number($f1,$f2)
{

    if($f1["value"] < $f2["value"]) return -1;
    elseif($f1["value"] > $f2["value"]) return 1;
    else return 0;
}
// ---------------------------------------------------------------------------------------------------
//Функция сортировки линейных списков по возрастанию для строк
function sort_line_list_asc_text($f1,$f2)
{
    return strcasecmp(mb_strtoupper($f1["value"], "UTF-8"), mb_strtoupper($f2["value"], "UTF-8"));
}
// ---------------------------------------------------------------------------------------------------
//Функция сортировки линейных списков по убыванию для чисел
function sort_line_list_desc_number($f1,$f2)
{
    if($f1["value"] > $f2["value"]) return -1;
    elseif($f1["value"] < $f2["value"]) return 1;
    else return 0;
}
// ---------------------------------------------------------------------------------------------------
//Функция сортировки линейных списков по убыванию для строк
function sort_line_list_desc_text($f1,$f2)
{
    return (strcasecmp(mb_strtoupper($f1["value"], "UTF-8"), mb_strtoupper($f2["value"], "UTF-8")))*(-1);
}
// ---------------------------------------------------------------------------------------------------
?>

<?php
/**
 * Общие определения
*/
//Постфиксы таблиц значений свойств - зависят от типа свойства
$property_types_tables = array("1"=>"int", "2"=>"float", "3"=>"text", "4"=>"bool", "5"=>"list", "6"=>"tree_list");
?>


<?php
if(!empty($_POST["save_action"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	/*
	echo '<pre>';
    var_dump($_POST);
	echo '</pre>';
    exit;
	*/
	
    /*
	$properties_objects = json_decode($_POST["properties_objects"], true);
	var_dump($properties_objects);
	
	for($p=0; $p < count($properties_objects); $p++)
	{
		echo ."\n";
		echo strtolower($properties_objects[$p]["caption"])."\n";
	}
	
    exit;
	*/
    
    if($_POST["save_action"] == "create")//СОЗДАНИЕ НОВОГО ТОВАРА
    {
        //var_dump($_POST);
        //exit;
        
        
        //1.0. Данные:
        $category_id = $_POST["category_id"];
        //$caption = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["caption"])));
        $alias = trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["alias"]));
        //$title_tag = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["title_tag"])));
        //$description_tag = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["description_tag"])));
        //$keywords_tag = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["keywords_tag"])));
        $robots_tag = htmlentities($_POST["robots_tag"]);
		$published_flag = $_POST["published_flag"];
        
		
		//Обработка мультиязычности (кастомный алгоритм по полям из массива)
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			//$value = save_custom_translation($_POST[$item["name"]."_lang_str_id"], $value);
			
			${$translated_items[$i]} = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST[$translated_items[$i]])));
			
			${$translated_items[$i]} = save_custom_translation( $_POST[ $translated_items[$i]."_lang_str_id" ] , ${$translated_items[$i]});
		}
		
		
		
		$product_stickers = json_decode($_POST["product_stickers"], true);
		
        //1.1. Создаем запись в таблице shop_catalogue_products
		if( $db_link->prepare("INSERT INTO `shop_catalogue_products` (`category_id`, `caption`, `alias`, `title_tag`, `description_tag`, `keywords_tag`, `robots_tag`, `published_flag`) VALUES (?,?,?,?,?,?,?,?);")->execute( array($category_id, $caption, $alias, $title_tag, $description_tag, $keywords_tag, $robots_tag, $published_flag) ) != true)
        {
            $error_message = translate_str_by_id(2940);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products/product?category_id=<?php echo $category_id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        //1.1.1 Получаем ID добавленного товара:
		$product_id = $db_link->lastInsertId();
        
        

        //1.2. Создаем записи в таблице shop_products_properties_values (т.е. значения свойств товара)
        //СВОЙСТВА ЗАПИСЫВАЕМ НА ОСНОВЕ СПИСКА СВОЙСТВ ОТ КЛИЕНТА
        $insert_property_value_result = true;//Накопительный результат добавления значений свойств
        $create_line_list_new_item_result = true;//Накопительный результат создания новых элементов линейных списков
        $properties_objects = json_decode($_POST["properties_objects"], true);
        $SUB_SQL_FIELDS = "(`product_id`, `property_id`, `category_id`, `value`)";//Набор полей таблиц - у всех типов свойств - набор одинаковый
        for($p=0; $p < count($properties_objects); $p++)
        {
            $property_id = $properties_objects[$p]["property_id"];
            $property_type_id = $properties_objects[$p]["property_type_id"];//Тип свойства
            
            
            $execute_sql = true;//Флаг - выполнить SQL-запрос на добавление. Если в свойстве типа список не будет отмеченных опций - SQL-запрос не выполняется
            $binding_values = array();
			switch($property_type_id)
            {
                case 1:
                    $table_postfix = "int";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
                    break;
                case 2:
                    $table_postfix = "float";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
                    break;
                case 3:
                    $table_postfix = "text";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					//Значения свойства Артикул очищаем от лишних знаков. Код строки мультиязычности может отличаться (caption свойства может быть кастомным). Поэтому сравниваем по значению строки с исходной строкой id 1713
					if( mb_strtolower($properties_objects[$p]["caption"], 'UTF-8') == mb_strtolower( translate_str_by_id(1713) , 'UTF-8') )
					{
						$properties_objects[$p]["value"] = preg_replace("/[^A-Za-z0-9А-Яа-яёЁ]/ui", '', $properties_objects[$p]["value"]);
						$properties_objects[$p]["value"] = strtoupper($properties_objects[$p]["value"]);
					}
					
					
					//Обработка мультиязычности (кастомный алгоритм)
					$properties_objects[$p]["value"] = save_custom_translation( $properties_objects[$p]["value_lang_str_id"], htmlentities($properties_objects[$p]["value"]) );
					
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"] );
                    break;
                case 4:
                    $table_postfix = "bool";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
                    break;
                case 5:
                    $table_postfix = "list";
                    $checked_options_array = $properties_objects[$p]["value"];//Массив с выбранными свойствами
                    
                    
                    // ----------------- START - СОЗДАНИЕ НОВОГО ЭЛЕМЕНТА ЛИНЕЙНОГО СПИСКА ------->
                    /**
                     * Единичный список:
                     * - если есть ручной ввод - добавляем значение в shop_line_list и этоже значение у товара
                     * Множественный список:
                     * - если есть ручной ввод - добавляем значения в shop_line_list и эти же значения присваиваем товару в добавление к тем, которые были отмечены (это отличие от единичного списка)
                    */
                    //Если было введено значение вручную:
                    if($properties_objects[$p]["manual_input"] != "")
                    {
                        //0. Сначала определяем Специфику списка (list_type) - Единичный / Множественный
                        if((integer)$properties_objects[$p]["list_type"] == (integer)1)
                        {
                            $manual_input = array($properties_objects[$p]["manual_input"]);//Элемент единственный - подгоняем под массив с единственным элементом
                            
                            //Это только для единичного списка. Т.к. для множественного отмеченные значения - всеравно остаются в добавление к введенным вручную
                            $checked_options_array = array();//Сбрасываем массив значений, который был получен от $_POST запроса. Там было одно значение (т.е. список с ед.выбором). Теперь мы его заменим тем, что было введено через ручной ввод
                        }
                        else if((integer)$properties_objects[$p]["list_type"] == (integer)2)//Для множественного списка - значения можно записывать через ;
                        {
                            $manual_input = $properties_objects[$p]["manual_input"];
                            $manual_input = explode(";", $manual_input);//Получаем массив элементов
                        }
                        

                        //1. Получаем описание линейного списка
                        $SQL_SELECT_LINE_LIST = "SELECT
                            `shop_line_lists`.`id` AS `id`,
                            `shop_line_lists`.`caption` AS `caption`,
                            `shop_line_lists`.`type` AS `type`,
                            `shop_line_lists`.`data_type` AS `data_type`,
                            `shop_line_lists`.`auto_sort` AS `auto_sort`
                            FROM
                            `shop_categories_properties_map`
                            INNER JOIN `shop_line_lists` ON `shop_categories_properties_map`.`list_id` = `shop_line_lists`.`id`
                            WHERE
                            `shop_categories_properties_map`.`id` = ?;";
                        $line_list_query = $db_link->prepare($SQL_SELECT_LINE_LIST);
						$line_list_query->execute( array($property_id) );
                        $line_list_record = $line_list_query->fetch();
                        $line_list_ID = $line_list_record["id"];//ID линейного списка
                        $line_list_AUTO_SORT = $line_list_record["auto_sort"];//Настройка автосортировки
                        $line_list_DATA_TYPE = $line_list_record["data_type"];//Тип данных списка
						
						//Получаем элементы списка
						$line_list_STRUCTURE_PHP = array();
						$list_items_query = $db_link->prepare("SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `order`;");
						$list_items_query->execute( array($line_list_ID) );
						while( $item = $list_items_query->fetch() )
						{
							array_push($line_list_STRUCTURE_PHP, array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"]) ) );
						}
						
                        //$line_list_STRUCTURE_JSON = $line_list_record["structure"];//JSON описание списка
                        //$line_list_STRUCTURE_PHP = json_decode($line_list_STRUCTURE_JSON, true);
                        
                        
                        //Всем значениям, введенным вручную
                        for($m = 0; $m < count($manual_input); $m++)
                        {
                            //2. Проверяем наличие такого же свойства в этом списке (пользователь мог ввести вручную существующее значение)
                            $is_item_exist = false;//Флаг - такого свойства еще не было в линейном списке
                            for($li = 0; $li < count($line_list_STRUCTURE_PHP); $li++)
                            {
                                //2a Если такое значение в списке уже есть - просто читаем его id
                                if( $line_list_STRUCTURE_PHP[$li]["value"] == $manual_input[$m] )
                                {
                                    $is_item_exist = true;//Такое свойство было уже
                                    array_push($checked_options_array, (integer)$line_list_STRUCTURE_PHP[$li]["id"]);//В массив значений
                                    break;
                                }
                            }
                            //2b Если такого значения в списке еще нет - вносим его. Его id будет - следующий
                            if(!$is_item_exist)
                            {
                                //Новый элемент в линейный список
                                array_push($line_list_STRUCTURE_PHP, array('value'=>htmlentities($manual_input[$m]), 'is_new'=>true));
                                
                                //Сортируем список с уже добавленным элементом
                                if($line_list_AUTO_SORT != "no")
                                {
                                    usort($line_list_STRUCTURE_PHP, "sort_line_list_".$line_list_AUTO_SORT."_".$line_list_DATA_TYPE);
                                }
                                
								//ОБНОВЛЯЕМ ТАБЛИЦУ С ЭЛЕМЕНТАМИ СПИСКА
								for($l_u=0; $l_u < count($line_list_STRUCTURE_PHP); $l_u++)
								{
									$order = $l_u+1;
									
									//Новые элементы: добавляем
									if( !empty($line_list_STRUCTURE_PHP[$l_u]["is_new"]) )
									{
										//Мультиязычность. Кастомный алгоритм
										$line_list_STRUCTURE_PHP[$l_u]["value"] = save_custom_translation(0, $line_list_STRUCTURE_PHP[$l_u]["value"]);
										
										
										
										if( ! $db_link->prepare("INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES (?, ?, ?);")->execute( array($line_list_ID, $line_list_STRUCTURE_PHP[$l_u]["value"], $order) ) )
										{
											$create_line_list_new_item_result = false;
										}
										else//Элемент добавлен в линейный список. Теперь добавляем его в значение свойста у самого товара
										{
											array_push($checked_options_array, $db_link->lastInsertId() );//В массив значений
										}
									}
									else//Старые элементы: обновляем order
									{
										if( ! $db_link->prepare("UPDATE `shop_line_lists_items` SET `order` = ? WHERE `id` = ?;")->execute( array($order, $line_list_STRUCTURE_PHP[$l_u]["id"]) ) )
										{
											$create_line_list_new_item_result = false;
										}
									}
								}
                            }
                        }
                    }//~if(!empty($_POST["manual_input_$property_id"]))
                    // ----------------- END - СОЗДАНИЕ НОВОГО ЭЛЕМЕНТА ЛИНЕЙНОГО СПИСКА -------|
                    
                    
                    
                    
                    
                    if(count($checked_options_array) > 0)
                    {
                        $SUB_SQL_VALUES = "";
                        for($o=0; $o < count($checked_options_array); $o++)
                        {
                            if($o > 0) $SUB_SQL_VALUES .= ", ";
                            $SUB_SQL_VALUES .= "(?, ?, ?, ?)";
							
							array_push($binding_values, $product_id);
							array_push($binding_values, $property_id);
							array_push($binding_values, $category_id);
							array_push($binding_values, $checked_options_array[$o]);
                        }
                    }
                    else//Свойства не отмечены - запрос не выполняем
                    {
                        $execute_sql = false;
                    }
                    break;
				case 6:
                    $table_postfix = "tree_list";
                    $checked_options_array = $properties_objects[$p]["value"];//Массив с выбранными свойствами

                    if(count($checked_options_array) > 0)
                    {
                        $SUB_SQL_VALUES = "";
                        for($o=0; $o < count($checked_options_array); $o++)
                        {
                            if($o > 0) $SUB_SQL_VALUES .= ", ";
                            $SUB_SQL_VALUES .= "(?, ?, ?, ?)";
							
							array_push($binding_values, $product_id);
							array_push($binding_values, $property_id);
							array_push($binding_values, $category_id);
							array_push($binding_values, $checked_options_array[$o]);
                        }
                    }
                    else//Свойства не отмечены - запрос не выполняем
                    {
                        $execute_sql = false;
                    }
                    break;
            }//switch
            if($execute_sql)
            {
                $SQL_INSERT_PROPERTY_VALUE = "INSERT INTO `shop_properties_values_$table_postfix` $SUB_SQL_FIELDS VALUES $SUB_SQL_VALUES;";
            
                //Добавляем свойство
                if( $db_link->prepare($SQL_INSERT_PROPERTY_VALUE)->execute($binding_values) != true)
                {
                    $insert_property_value_result = false;
                }
            }
        }//for($p) - по объектам описаний свойств от клиента
        
        
        
        
        //1.3. Добавляем изображения товара в таблицу shop_products_images
        $unique_index = time();//Для добавления уникального индекса в имя файла
        $files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/products_images/";
        $images_save_result = true;//Накопительный результат сохранения изображений
        $images_list = json_decode($_POST["images_list"], true);//Список объектов описания изображений
        for($i=0; $i < count($images_list); $i++)
        {
			//Если изображение от шаблона, то просто добавляем учетную запись на уже существующий файл
			if($images_list[$i]["image_of_template"] == 1)
			{
				if( $db_link->prepare("INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?, ?);")->execute( array($product_id, $images_list[$i]["name"]) ) != true )
				{
					$images_save_result = false;
				}
			}
			else//Новое изображение - загружается через форму
			{
				$FILE_POST = $_FILES["image_".$images_list[$i]["client_id"]];//Форма с файлом
				
				if( $FILE_POST["size"] > 0 )
				{
					//Получаем файл:
					$fileName = $FILE_POST["name"];
					
					//Получаем расширение файла
					$file_extension = explode(".", $fileName);
					$file_extension = $file_extension[count($file_extension)-1];
					
					//Проверка расширения
					$file_extension = strtolower($file_extension);
					if( array_search( $file_extension, array('png', 'jpg', 'jpeg', 'gif') ) === false )
					{
						exit;
					}
					
					
					//Проверка типа файла:
					$allowedTypes = array(IMAGETYPE_PNG, IMAGETYPE_JPEG, IMAGETYPE_GIF);
					$detectedType = exif_imagetype($FILE_POST['tmp_name']);
					if( !in_array($detectedType, $allowedTypes) )
					{
						exit;
					}
					
					
					//Имя файла будет вида <id категории>.$file_extension
					$saved_file_name = $alias."_".$unique_index.".".$file_extension;
					
					$uploadfile = $files_upload_dir.$saved_file_name;
					
					if (copy($FILE_POST['tmp_name'], $uploadfile))
					{
						if( $db_link->prepare("INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?,?);")->execute( array($product_id, $saved_file_name) ) != true)
						{
							$images_save_result = false;
						}
					}
					else
					{
						$images_save_result = false;
					}
					$unique_index++;
				}
			}
        }
        
        
        //1.4 Добавляем текстовое описание товара
        $product_text_save_result = true;
        $product_text = $_POST["product_text"];
		
		//Не допускаем вставку php-кода при текстовом типе контента
		if( true )
		{
			do
			{
				$product_text = str_replace('<?', '[CODE]', $product_text);
			}while( strpos($product_text, '<?') !== false );
			
			
			do
			{
				$product_text = str_replace('?>', '[/CODE]', $product_text);
			}while( strpos($product_text, '?>') !== false );
		}
		
		//Мультиязычность. Кастомный алгоритм
		$product_text = save_custom_translation($_POST["product_text_lang_str_id"], $product_text);
		
        if( $db_link->prepare("INSERT INTO `shop_products_text` (`product_id`, `content`) VALUES (?,?);")->execute( array($product_id, $product_text) ) != true)
        {
            $product_text_save_result = false;
        }
		
		
		
		
		//1.5 ОБРАБОТКА СТИКЕРОВ
		$product_stickers_save_result = true;
		//Добавление стикеров
		for($i = 0; $i < count($product_stickers); $i++)
		{
			$value = htmlentities($product_stickers[$i]["value"]);
			$color_text = htmlentities($product_stickers[$i]["color_text"]);
			$color_background = htmlentities($product_stickers[$i]["color_background"]);
			$href = htmlentities($product_stickers[$i]["href"]);
			$class_css = htmlentities($product_stickers[$i]["class_css"]);
			$description = htmlentities($product_stickers[$i]["description"]);
			$order = $i + 1;
			
			//Мультиязычность. Кастомный алгоритм
			$value = save_custom_translation(0, $value);
			$description = save_custom_translation(0, $description);

			if( $db_link->prepare("INSERT INTO `shop_products_stickers` (`product_id`, `value`, `color_text`, `color_background`, `href`, `class_css`, `description`, `order`) VALUES (?,?,?,?,?,?,?,?);")->execute( array($product_id, $value, $color_text, $color_background, $href, $class_css, $description, $order) ) != true)
			{
				$product_stickers_save_result = false;
			}
		}
		
		
		
		
		//1.6 ОБРАБОТКА СОПУТСТВУЮЩИХ ТОВАРОВ
		if( $db_link->prepare("DELETE FROM `shop_related_products` WHERE `product_id` = ?;")->execute(array($product_id)) != true )
		{
			$product_related_preclean = false;
		}else{
			$product_related = json_decode($_POST["product_related"], true);
			$product_related_save_result = true;
			for($i = 0; $i < count($product_related); $i++)
			{
				$product_id_related = $product_related[$i]["product_id"];
				if($product_id == $product_id_related){
					continue;
				}
				$order = $i + 1;
				
				if( $db_link->prepare("INSERT INTO `shop_related_products` (`product_id`, `product_id_related`, `order`) VALUES (?,?,?);")->execute( array($product_id, $product_id_related, $order) ) != true)
				{
					$product_related_save_result = false;
				}
			}
		}
		
        
        
        //ПРОВЕРКА РЕЗУЛЬТАТА
        if($insert_property_value_result && $images_save_result && $product_text_save_result && $create_line_list_new_item_result && $product_stickers_save_result)
        {
            $success_message = translate_str_by_id(2941);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products/product?product_id=<?php echo $product_id; ?>&category_id=<?php echo $category_id; ?>&success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
            $error_message = translate_str_by_id(2912).": <br>";
            if(!$insert_property_value_result)
            {
                $error_message .= translate_str_by_id(2942)."<br>";
            }
            if(!$images_save_result)
            {
                $error_message .= translate_str_by_id(2943)."<br>";
            }
            if(!$product_text_save_result)
            {
                $error_message .= translate_str_by_id(2944)."<br>";
            }
            if(!$create_line_list_new_item_result)
            {
                $error_message .= translate_str_by_id(2945)."<br>";
            }
			if(!$product_stickers_save_result)
            {
                $error_message .= translate_str_by_id(2946)."<br>";
            }
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products/product?product_id=<?php echo $product_id; ?>&category_id=<?php echo $category_id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        
    }//if("CREATE")
    else if($_POST["save_action"] == "edit")//РЕДАКТИРОВАНИЕ НОВОГО ТОВАРА
    {
        //2.0. Данные
        $category_id = $_POST["category_id"];
        $product_id = $_POST["product_id"];
        //$caption = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["caption"])));
        $alias = trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["alias"]));
        //$title_tag = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["title_tag"])));
        //$description_tag = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["description_tag"])));
        //$keywords_tag = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST["keywords_tag"])));
        $robots_tag = htmlentities($_POST["robots_tag"]);
        $published_flag = $_POST["published_flag"];
        
		$product_stickers = json_decode($_POST["product_stickers"], true);
        
		
		
		//Обработка мультиязычности (кастомный алгоритм по полям из массива)
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			//$value = save_custom_translation($_POST[$item["name"]."_lang_str_id"], $value);
			
			${$translated_items[$i]} = htmlentities(trim(str_replace(array("'", '"', '`', "\n", "\r", "\t"), '', $_POST[$translated_items[$i]])));
			
			${$translated_items[$i]} = save_custom_translation( $_POST[ $translated_items[$i]."_lang_str_id" ] , ${$translated_items[$i]});
		}
		
		
		
        //2.1.Обновляем данные в таблице shop_catalogue_products
        if( $db_link->prepare("UPDATE `shop_catalogue_products` SET `caption` = ?, `alias` = ?, `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?, `robots_tag` = ?, `published_flag` = ? WHERE `id` = ?;")->execute( array($caption, $alias, $title_tag, $description_tag, $keywords_tag, $robots_tag, $published_flag, $product_id) ) != true)
        {
            $error_message = translate_str_by_id(2947);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products/product?product_id=<?php echo $product_id; ?>&category_id=<?php echo $category_id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
        
        
        
        //2.2. НОВЫЙ ВАРИАНТ Обновляем свойства товара (таблица shop_products_properties_values)
        //2.2.1 Предварительно удаляем записи всех свойств данного товара:
        $delete_property_value_result = true;//Накопительный результат удаления значений свойств
        $create_line_list_new_item_result = true;//Накопительный результат создания новых элементов линейных списков
		if( $db_link->prepare("DELETE FROM `shop_properties_values_int` WHERE `product_id` = ?;")->execute( array($product_id) ) != true )$delete_property_value_result = false;
        if( $db_link->prepare("DELETE FROM `shop_properties_values_float` WHERE `product_id` = ?;")->execute( array($product_id) ) != true )$delete_property_value_result = false;
        if( $db_link->prepare("DELETE FROM `shop_properties_values_text` WHERE `product_id` = ?;")->execute( array($product_id) ) != true )$delete_property_value_result = false;
        if( $db_link->prepare("DELETE FROM `shop_properties_values_bool` WHERE `product_id` = ?;")->execute( array($product_id) ) != true )$delete_property_value_result = false;
        if( $db_link->prepare("DELETE FROM `shop_properties_values_list` WHERE `product_id` = ?;")->execute( array($product_id) ) != true )$delete_property_value_result = false;
		if( $db_link->prepare("DELETE FROM `shop_properties_values_tree_list` WHERE `product_id` = ?;")->execute( array($product_id) ) != true )$delete_property_value_result = false;
        //2.2.2 Создаем записи в таблице shop_products_properties_values (т.е. значения свойств товара) - АНАЛОГИЧНО 1.2.
        //СВОЙСТВА ЗАПИСЫВАЕМ НА ОСНОВЕ СПИСКА СВОЙСТВ ОТ КЛИЕНТА
        $insert_property_value_result = true;//Накопительный результат добавления значений свойств
        $properties_objects = json_decode($_POST["properties_objects"], true);
        $SUB_SQL_FIELDS = "(`product_id`, `property_id`, `category_id`, `value`)";//Набор полей таблиц - у всех типов свойств - набор одинаковый
        for($p=0; $p < count($properties_objects); $p++)
        {
            $property_id = $properties_objects[$p]["property_id"];
            $property_type_id = $properties_objects[$p]["property_type_id"];//Тип свойства
            
            $execute_sql = true;//Флаг - выполнить SQL-запрос на добавление. Если в свойстве типа список не будет отмеченных опций - SQL-запрос не выполняется
            $binding_values = array();
			switch($property_type_id)
            {
                case 1:
                    $table_postfix = "int";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
					
                    break;
                case 2:
                    $table_postfix = "float";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
                    break;
                case 3:
                    $table_postfix = "text";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					//Значения свойства Артикул очищаем от лишних знаков. Код строки мультиязычности может отличаться (caption свойства может быть кастомным). Поэтому сравниваем по значению строки с исходной строкой id 1713
					if( mb_strtolower($properties_objects[$p]["caption"], 'UTF-8') == mb_strtolower( translate_str_by_id(1713) , 'UTF-8') )
					{
						$properties_objects[$p]["value"] = preg_replace("/[^A-Za-z0-9А-Яа-яёЁ]/ui", '', $properties_objects[$p]["value"]);
						$properties_objects[$p]["value"] = strtoupper($properties_objects[$p]["value"]);
					}
					
					//Обработка мультиязычности (кастомный алгоритм)
					//Обработка мультиязычности (кастомный алгоритм)
					$properties_objects[$p]["value"] = save_custom_translation( $properties_objects[$p]["value_lang_str_id"], htmlentities($properties_objects[$p]["value"]) );
					
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
                    break;
                case 4:
                    $table_postfix = "bool";
                    $SUB_SQL_VALUES = "(?, ?, ?, ?)";
					
					array_push($binding_values, $product_id);
					array_push($binding_values, $property_id);
					array_push($binding_values, $category_id);
					array_push($binding_values, $properties_objects[$p]["value"]);
                    break;
                case 5:
                    $table_postfix = "list";
                    $checked_options_array = $properties_objects[$p]["value"];//Массив с выбранными свойствами
                    
                    
                    // ----------------- START - СОЗДАНИЕ НОВОГО ЭЛЕМЕНТА ЛИНЕЙНОГО СПИСКА ------->
                    /**
                     * Единичный список:
                     * - если есть ручной ввод - добавляем значение в shop_line_list и этоже значение у товара
                     * Множественный список:
                     * - если есть ручной ввод - добавляем значения в shop_line_list и эти же значения присваиваем товару в добавление к тем, которые были отмечены (это отличие от единичного списка)
                    */
                    //Если было введено значение вручную:
                    if($properties_objects[$p]["manual_input"] != "")
                    {
                        //0. Сначала определяем Специфику списка (list_type) - Единичный / Множественный
                        if((integer)$properties_objects[$p]["list_type"] == (integer)1)
                        {
                            $manual_input = array($properties_objects[$p]["manual_input"]);//Элемент единственный - подгоняем под массив с единственным элементом
                            
                            //Это только для единичного списка. Т.к. для множественного отмеченные значения - всеравно остаются в добавление к введенным вручную
                            $checked_options_array = array();//Сбрасываем массив значений, который был получен от $_POST запроса. Там было одно значение (т.е. список с ед.выбором). Теперь мы его заменим тем, что было введено через ручной ввод
                        }
                        else if((integer)$properties_objects[$p]["list_type"] == (integer)2)//Для множественного списка - значения можно записывать через ;
                        {
                            $manual_input = $properties_objects[$p]["manual_input"];
                            $manual_input = explode(";", $manual_input);//Получаем массив элементов
                        }
                        

                        //1. Получаем описание линейного списка
                        $SQL_SELECT_LINE_LIST = "SELECT
                            `shop_line_lists`.`id` AS `id`,
                            `shop_line_lists`.`caption` AS `caption`,
                            `shop_line_lists`.`type` AS `type`,
                            `shop_line_lists`.`data_type` AS `data_type`,
                            `shop_line_lists`.`auto_sort` AS `auto_sort`
                            FROM
                            `shop_categories_properties_map`
                            INNER JOIN `shop_line_lists` ON `shop_categories_properties_map`.`list_id` = `shop_line_lists`.`id`
                            WHERE
                            `shop_categories_properties_map`.`id` = ?;";
                        $line_list_query = $db_link->prepare($SQL_SELECT_LINE_LIST);
						$line_list_query->execute( array($property_id) );
                        $line_list_record = $line_list_query->fetch();
                        $line_list_ID = $line_list_record["id"];//ID линейного списка
                        $line_list_AUTO_SORT = $line_list_record["auto_sort"];//Настройка автосортировки
                        $line_list_DATA_TYPE = $line_list_record["data_type"];//Тип данных списка
						
						
						//Получаем элементы списка
						$line_list_STRUCTURE_PHP = array();
						$list_items_query = $db_link->prepare("SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `order`;");
						$list_items_query->execute( array($line_list_ID) );
						while( $item = $list_items_query->fetch() )
						{
							array_push($line_list_STRUCTURE_PHP, array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"]) ) );
						}
						
                        //$line_list_STRUCTURE_JSON = $line_list_record["structure"];//JSON описание списка
						//$line_list_STRUCTURE_PHP = json_decode($line_list_STRUCTURE_JSON, true);
                        
                        
                        //Всем значениям, введенным вручную
                        for($m = 0; $m < count($manual_input); $m++)
                        {
                            //2. Проверяем наличие такого же свойства в этом списке (пользователь мог ввести вручную существующее значение)
                            $is_item_exist = false;//Флаг - такого свойства еще не было в линейном списке
                            for($li = 0; $li < count($line_list_STRUCTURE_PHP); $li++)
                            {
                                //2a Если такое значение в списке уже есть - просто читаем его id
                                if($line_list_STRUCTURE_PHP[$li]["value"] == $manual_input[$m])
                                {
                                    $is_item_exist = true;//Такое свойство было уже
                                    array_push($checked_options_array, (integer)$line_list_STRUCTURE_PHP[$li]["id"]);//В массив значений
                                    break;
                                }
                            }
                            //2b Если такого значения в списке еще нет - вносим его. Его id будет - следующий
                            if(!$is_item_exist)
                            {
                                //Новый элемент в линейный список
                                array_push($line_list_STRUCTURE_PHP, array('value'=>htmlentities($manual_input[$m]), 'is_new'=>true) );
                                
                                //Сортируем список с уже добавленным элементом
                                if($line_list_AUTO_SORT != "no")
                                {
                                    usort($line_list_STRUCTURE_PHP, "sort_line_list_".$line_list_AUTO_SORT."_".$line_list_DATA_TYPE);
                                }
                                
								
								//ОБНОВЛЯЕМ ТАБЛИЦУ С ЭЛЕМЕНТАМИ СПИСКА
								for($l_u=0; $l_u < count($line_list_STRUCTURE_PHP); $l_u++)
								{
									$order = $l_u+1;
									
									//Новые элементы: добавляем
									if( !empty($line_list_STRUCTURE_PHP[$l_u]["is_new"]) )
									{
										//Мультиязычность. Кастомный алгоритм
										$line_list_STRUCTURE_PHP[$l_u]["value"] = save_custom_translation(0, $line_list_STRUCTURE_PHP[$l_u]["value"]);
										
										
										if( ! $db_link->prepare("INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES (?, ?, ?);")->execute( array($line_list_ID, $line_list_STRUCTURE_PHP[$l_u]["value"], $order) ) )
										{
											$create_line_list_new_item_result = false;
										}
										else//Элемент добавлен в линейный список. Теперь добавляем его в значение свойста у самого товара
										{
											array_push($checked_options_array, $db_link->lastInsertId() );//В массив значений
										}
									}
									else//Старые элементы: обновляем order
									{
										if( ! $db_link->prepare("UPDATE `shop_line_lists_items` SET `order` = ? WHERE `id` = ?;")->execute( array($order, $line_list_STRUCTURE_PHP[$l_u]["id"]) ) )
										{
											$create_line_list_new_item_result = false;
										}
									}
								}
                            }
                        }
                    }//~if(!empty($_POST["manual_input_$property_id"]))
                    // ----------------- END - СОЗДАНИЕ НОВОГО ЭЛЕМЕНТА ЛИНЕЙНОГО СПИСКА -------|
                    
                    
                    
                    if(count($checked_options_array) > 0)
                    {
                        $SUB_SQL_VALUES = "";
                        for($o=0; $o < count($checked_options_array); $o++)
                        {
                            if($o > 0) $SUB_SQL_VALUES .= ", ";
                            $SUB_SQL_VALUES .= "(?,?,?,?)";
							
							
							array_push($binding_values, $product_id);
							array_push($binding_values, $property_id);
							array_push($binding_values, $category_id);
							array_push($binding_values, $checked_options_array[$o]);
                        }
                    }
                    else
                    {
                        $execute_sql = false;
                    }
                    break;
				case 6:
                    $table_postfix = "tree_list";
                    $checked_options_array = $properties_objects[$p]["value"];//Массив с выбранными свойствами
                    if(count($checked_options_array) > 0)
                    {
                        $SUB_SQL_VALUES = "";
                        for($o=0; $o < count($checked_options_array); $o++)
                        {
                            if($o > 0) $SUB_SQL_VALUES .= ", ";
                            $SUB_SQL_VALUES .= "(?,?,?,?)";
							
							array_push($binding_values, $product_id);
							array_push($binding_values, $property_id);
							array_push($binding_values, $category_id);
							array_push($binding_values, $checked_options_array[$o]);
                        }
                    }
                    else
                    {
                        $execute_sql = false;
                    }
                    break;
            }//switch
            if($execute_sql)
            {
                $SQL_INSERT_PROPERTY_VALUE = "INSERT INTO `shop_properties_values_$table_postfix` $SUB_SQL_FIELDS VALUES $SUB_SQL_VALUES;";

                //Добавляем свойство
                if( $db_link->prepare($SQL_INSERT_PROPERTY_VALUE)->execute($binding_values) != true)
                {
                    $insert_property_value_result = false;
                }
            }
        }//for($p) - по объектам описания свойств
        
        
        
        
        //2.3. Обработка изображений
        $images_save_result = true;//Накопительный результат сохранения изображений
        $images_delete_result = true;//Накопительный результат удаления изображений
        $images_list = json_decode($_POST["images_list"], true);//Список объектов описания изображений от клиента
        //2.3.1 Удаляем с сервера изображения, которые были удалены при редактировании товара
        $images_to_delete = array();//Список ID изображений, которые нужно удалить
        
		$server_images_list_query = $db_link->prepare("SELECT `id`, `file_name` FROM `shop_products_images` WHERE `product_id` = ?;");
		$server_images_list_query->execute( array($product_id) );
        while( $server_image = $server_images_list_query->fetch() )
        {
            $image_exist = false;//Флаг - данное изображение есть в списке от клиента (т.е. при редактировании оно осталось)
            
            //Ищем данное изображение в списке изображений от клиента
            for($c=0; $c < count($images_list); $c++)
            {
                if($images_list[$c]["server_id"] == $server_image["id"] )
                {
                    $image_exist = true;
                    break;//for($c)
                }
            }
            
            //Если изображение было удалено при редактировании - добавляем его ID в список на удаление и удаляем сам файл
            if( ! $image_exist )
            {
				//Вносим ID учетной записи изображения в список на удаление
                array_push($images_to_delete, $server_image["id"]);
				
				//Теперь удаляем сам файл, если он больше не используется в других учетных записях
				$check_image_use_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_products_images` WHERE `file_name` = ? AND `product_id` != ?;");
				$check_image_use_query->execute( array($server_image["file_name"], $product_id) );
				if( $check_image_use_query->fetchColumn() == 0 )
				{
					//Удаляем файл изображения
					if( ! unlink($_SERVER["DOCUMENT_ROOT"]."/content/files/images/products_images/".$server_image["file_name"]))
					{
						$images_delete_result = false;
					}
				}
            }
        }
        //Удаляем учетные записи изображений
        if(count($images_to_delete) > 0)
        {
			$binding_values = array();
            $SQL_DELETE_IMAGES = "DELETE FROM `shop_products_images` WHERE ";
            for($i=0; $i < count($images_to_delete); $i++)
            {
                if($i > 0)$SQL_DELETE_IMAGES .= " OR";
                $SQL_DELETE_IMAGES .= " `id` = ?";
				
				array_push($binding_values, $images_to_delete[$i]);
            }
            $SQL_DELETE_IMAGES .= ";";
            if( $db_link->prepare($SQL_DELETE_IMAGES)->execute($binding_values) != true)
            {
                $images_delete_result = false;
            }
        }
        //2.3.2 Добавляем на сервер новые изображения
        $unique_index = time();//Для добавления уникального индекса в имя файла
        $files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/products_images/";
        for($i=0; $i < count($images_list); $i++)
        {
			if($images_list[$i]["image_of_template"] == 1)//Если изображение взято от шаблона
			{
				if( $db_link->prepare("INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?, ?);")->execute( array($product_id, $images_list[$i]["name"]) ) != true )
				{
					$images_save_result = false;
				}
			}
			else//Изображения не взято от шаблона (т.е. новое или уже существующее)
			{
				//Если задан server_id - изображение уже есть на сервере
				if($images_list[$i]["server_id"] > 0)
				{
					continue;
				}
				
				$FILE_POST = $_FILES["image_".$images_list[$i]["client_id"]];//Форма с файлом
				
				if( $FILE_POST["size"] > 0 )
				{
					//Получаем файл:
					$fileName = $FILE_POST["name"];
					
					//Получаем расширение файла
					$file_extension = explode(".", $fileName);
					$file_extension = $file_extension[count($file_extension)-1];
					
					
					//Проверка расширения
					$file_extension = strtolower($file_extension);
					if( array_search( $file_extension, array('png', 'jpg', 'jpeg', 'gif') ) === false )
					{
						exit;
					}
					
					//Проверка типа файла:
					$allowedTypes = array(IMAGETYPE_PNG, IMAGETYPE_JPEG, IMAGETYPE_GIF);
					$detectedType = exif_imagetype($FILE_POST['tmp_name']);
					if( !in_array($detectedType, $allowedTypes) )
					{
						exit;
					}
					
					
					//Имя файла будет вида <id категории>.$file_extension
					$saved_file_name = $alias."_".$unique_index.".".$file_extension;
					
					$uploadfile = $files_upload_dir.$saved_file_name;
					
					if (copy($FILE_POST['tmp_name'], $uploadfile))
					{
						if( $db_link->prepare("INSERT INTO `shop_products_images` (`product_id`, `file_name`) VALUES (?, ?);")->execute( array($product_id, $saved_file_name) ) != true)
						{
							$images_save_result = false;
						}
					}
					else
					{
						$images_save_result = false;
					}
					$unique_index++;
				}
			}
        }
        
        
        
        //2.4 ОБНОВЛЕНИЕ ТЕКСТОВОГО ОПИСАНИЯ
        $product_text_save_result = true;
        $product_text = $_POST["product_text"];
		
		//Не допускаем вставку php-кода при текстовом типе контента
		if( true )
		{
			do
			{
				$product_text = str_replace('<?', '[CODE]', $product_text);
			}while( strpos($product_text, '<?') !== false );
			
			
			do
			{
				$product_text = str_replace('?>', '[/CODE]', $product_text);
			}while( strpos($product_text, '?>') !== false );
		}
		
		//Мультиязычность. Кастомный алгоритм
		$product_text = save_custom_translation($_POST["product_text_lang_str_id"], $product_text);
		
        //Проверяем наличие текстового описания
        $check_text_exist = $db_link->prepare("SELECT COUNT(*) FROM `shop_products_text` WHERE `product_id` = ?;");
		$check_text_exist->execute( array($product_id) );
        if( $check_text_exist->fetchColumn() == 1)
        {
			if( $db_link->prepare("UPDATE `shop_products_text` SET `content` = ? WHERE `product_id` = ?;")->execute( array($product_text, $product_id) ) != true)
			{
				$product_text_save_result = false;
			}
        }
        else
        {
			if( $db_link->prepare("INSERT INTO `shop_products_text` (`product_id`, `content`) VALUES (?, ?);")->execute( array($product_id, $product_text) ) != true)
			{
				$product_text_save_result = false;
			}
        }
        
		
		
		
		//2.5 ОБРАБОТКА СТИКЕРОВ
		$product_stickers_save_result = true;
		$product_stickers_delete_result = true;
		//Удаление стикеров, которые были удалены при редактировании
		$stickers_not_delete_list = "";
		$binding_values = array();
		array_push($binding_values, $product_id);
		for($i = 0; $i < count($product_stickers); $i++)
		{
			if($i > 0)
			{
				$stickers_not_delete_list .= ", ";
			}
			$stickers_not_delete_list .= "?";
			
			array_push($binding_values, $product_stickers[$i]["id"]);
		}
		if($stickers_not_delete_list != "")//т.е. есть хотя бы один стикер. А если его нет, то значит список пустой и нужно из БД удалить все стикеры этого товара
		{
			$stickers_not_delete_list = " AND `id` NOT IN ($stickers_not_delete_list)";
		}
		$SQL_STICKERS_DELETE = "DELETE FROM `shop_products_stickers` WHERE `product_id`=?".$stickers_not_delete_list.";";
		if( $db_link->prepare($SQL_STICKERS_DELETE)->execute($binding_values) != true)
        {
            $product_stickers_delete_result = false;
        }
		//Добавление/редактирование стикеров
		for($i = 0; $i < count($product_stickers); $i++)
		{
			$value = htmlentities($product_stickers[$i]["value"]);
			$color_text = htmlentities($product_stickers[$i]["color_text"]);
			$color_background = htmlentities($product_stickers[$i]["color_background"]);
			$href = htmlentities($product_stickers[$i]["href"]);
			$class_css = htmlentities($product_stickers[$i]["class_css"]);
			$description = htmlentities($product_stickers[$i]["description"]);
			$order = $i + 1;
			
			if($product_stickers[$i]["is_new"] == true)
			{
				//Мультиязычность. Кастомный алгоритм
				$value = save_custom_translation(0, $value);
				$description = save_custom_translation(0, $description);
				
				
				if( $db_link->prepare("INSERT INTO `shop_products_stickers` (`product_id`, `value`, `color_text`, `color_background`, `href`, `class_css`, `description`, `order`) VALUES (?,?,?,?,?,?,?,?);")->execute( array($product_id, $value, $color_text, $color_background, $href, $class_css, $description, $order) ) != true)
				{
					$product_stickers_save_result = false;
				}
			}
			else
			{
				$id = $product_stickers[$i]["id"];
				
				//Мультиязычность. Кастомный алгоритм
				$value = save_custom_translation($product_stickers[$i]["value_lang_str_id"], $value);
				$description = save_custom_translation($product_stickers[$i]["description_lang_str_id"], $description);
				
				if( $db_link->prepare("UPDATE `shop_products_stickers` SET `value` = ?, `color_text`=?, `color_background`=?, `href`=?, `class_css`=?, `description`=?, `order`=? WHERE `id` = ?;")->execute( array($value, $color_text, $color_background, $href, $class_css, $description, $order, $id) ) != true)
				{
					$product_stickers_save_result = false;
				}
			}
		}

        
		
		
		//1.6 ОБРАБОТКА СОПУТСТВУЮЩИХ ТОВАРОВ
		$product_related_preclean = true;
		if( $db_link->prepare("DELETE FROM `shop_related_products` WHERE `product_id` = ?;")->execute(array($product_id)) != true )
		{
			$product_related_preclean = false;
		}else{
			$product_related = json_decode($_POST["product_related"], true);
			$product_related_save_result = true;
			for($i = 0; $i < count($product_related); $i++)
			{
				$product_id_related = $product_related[$i]["product_id"];
				if($product_id == $product_id_related){
					continue;
				}
				$order = $i + 1;
				
				if( $db_link->prepare("INSERT INTO `shop_related_products` (`product_id`, `product_id_related`, `order`) VALUES (?,?,?);")->execute( array($product_id, $product_id_related, $order) ) != true)
				{
					$product_related_save_result = false;
				}
			}
		}
		
		
		
        
        //ПРОВЕРКА РЕЗУЛЬТАТА
        if($delete_property_value_result && $insert_property_value_result && $images_save_result && $images_delete_result && $product_text_save_result && $create_line_list_new_item_result && $product_stickers_save_result && $product_stickers_delete_result)
        {
            $success_message = translate_str_by_id(2948);
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products/product?product_id=<?php echo $product_id; ?>&category_id=<?php echo $category_id; ?>&success_message=<?php echo $success_message; ?>";
            </script>
            <?php
            exit;
        }
        else
        {
            $error_message = translate_str_by_id(2912).": <br>";
            if(!$delete_property_value_result)
            {
                $error_message .= translate_str_by_id(2949)."<br>";
            }
            if(!$insert_property_value_result)
            {
                $error_message .= translate_str_by_id(2950)."<br>";
            }
            if(!$images_save_result)
            {
                $error_message .= translate_str_by_id(2951)."<br>";
            }
            if(!$images_delete_result)
            {
                $error_message .= translate_str_by_id(2952)."<br>";
            }
            if(!$product_text_save_result)
            {
                $error_message .= translate_str_by_id(2953)."<br>";
            }
            if(!$create_line_list_new_item_result)
            {
                $error_message .= translate_str_by_id(2954)."<br>";
            }
			if(!$product_stickers_save_result)
            {
                $error_message .= translate_str_by_id(2955)."<br>";
            }
			if(!$product_stickers_delete_result)
            {
                $error_message .= translate_str_by_id(2956)."<br>";
            }
			if(!$product_related_preclean)
            {
                $error_message .= translate_str_by_id(2957)."<br>";
            }
			if(!$product_related_save_result)
            {
                $error_message .= translate_str_by_id(2958)."<br>";
            }
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products/product?product_id=<?php echo $product_id; ?>&category_id=<?php echo $category_id; ?>&error_message=<?php echo $error_message; ?>";
            </script>
            <?php
            exit;
        }
    }//else (EDIT)
}//if(action)
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
    ?>
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
    <?php
    //Исходные данные
    $page_name = translate_str_by_id(2959);
    $category_id = $_GET["category_id"];
    $product_id = 0;
    $action_type = "create";
    //$caption = "";
    $alias = "";
    //$title_tag = "";
    //$description_tag = "";
    //$keywords_tag = "";
    $robots_tag = "";
    $properties_values = array();//Массив для текущих значений свойств
    $product_text = "";//Текстовое описание товара
    $product_text_lang_str_id = 0;//Текстовое описание товара (Мультиязычность)
    $published_flag = 1;
	$product_template = 0;//ID товара, который нужно использовать в качестве шаблона при инициализации полей
	
	$product_stickers = array();//Массив для стикеров
	
	
	//Мультиязычность
	for( $i=0 ; $i < count($translated_items) ; $i++ )
	{
		${$translated_items[$i]} = "";
		${$translated_items[$i]."_lang_str_id"} = 0;
	}
	
	
	
	//Если идет редактирование товара
	if( !empty($_GET["product_id"]) )
	{
		$page_name = translate_str_by_id(2960);
        $product_id = $_GET["product_id"];
        $action_type = "edit";
		
		$product_template = $product_id;
	}
	
	if( !empty($_GET["template_id"]) )
	{
		$product_template = $_GET["template_id"];
	}
	
    //Если есть ID продукта для исходной инициализации полей (т.е. шаблон)
    if( $product_template > 0 )
    {
        //Получаем текущие данные товара:
        $product_query = $db_link->prepare("SELECT * FROM `shop_catalogue_products` WHERE `id` = ?;");
		$product_query->execute( array($product_template) );
        $product_record = $product_query->fetch();
        
        //Указываем общие параметры товара
        //$caption = $product_record["caption"];
        $alias = $product_record["alias"];
        //$title_tag = $product_record["title_tag"];
        //$description_tag = $product_record["description_tag"];
        //$keywords_tag = $product_record["keywords_tag"];
        $robots_tag = $product_record["robots_tag"];
        $published_flag = $product_record["published_flag"];
        
		
		
		//Мультиязычность
		for( $i=0 ; $i < count($translated_items) ; $i++ )
		{
			${$translated_items[$i]."_lang_str_id"} = $product_record[$translated_items[$i]];
			${$translated_items[$i]} = translate_str_by_id($product_record[$translated_items[$i]]);
		}
		
		
		
        //Получаем текстовое описани товара
		$product_text_query = $db_link->prepare("SELECT `content` FROM `shop_products_text` WHERE `product_id` = ?;");
		$product_text_query->execute( array($product_template) );
		$product_text_record = $product_text_query->fetch();
        if( $product_text_record != false )
        {
            $product_text_lang_str_id = $product_text_record["content"];
            $product_text = translate_str_by_id($product_text_record["content"]);
        }
		
		//Получаем стикеры
		$product_stickers_query = $db_link->prepare("SELECT * FROM `shop_products_stickers` WHERE `product_id` = ? ORDER BY `order`;");
		$product_stickers_query->execute( array($product_template) );
		while($product_sticker = $product_stickers_query->fetch() )
		{
			array_push($product_stickers, array("id"=>$product_sticker["id"], "is_new"=>false, "value_lang_str_id"=>$product_sticker["value"], "value"=>translate_str_by_id($product_sticker["value"]), "color_text"=>$product_sticker["color_text"], "color_background"=>$product_sticker["color_background"], "href"=>$product_sticker["href"], "class_css"=>$product_sticker["class_css"], "description_lang_str_id"=>$product_sticker["description"], "description"=>translate_str_by_id($product_sticker["description"]), '$level'=>1, '$parent'=>0, '$count'=>0) );
		}
		
        
        //Получаем текущие настройки товара НОВЫЙ ВАРИАНТ
        //Получаем список свойств категории
		$category_properties_query = $db_link->prepare("SELECT * FROM `shop_categories_properties_map` WHERE `category_id` = ?;");
		$category_properties_query->execute( array($category_id) );
        while( $category_property = $category_properties_query->fetch() )
        {   
            //Формируем ассоциативный массив для данного свойства
            $property_record = array();
            $property_record["property_type_id"] = $category_property["property_type_id"];//Тип свойства
            $property_record["property_id"] = $category_property["id"];//ID свойства
            $property_id = $category_property["id"];//ID свойства - Для строки SQL
            
            $table_postfix = $property_types_tables[(string)$property_record["property_type_id"]];//Постфикс таблицы
            
            
            //Получаем значение данного свойства для товара:
			$property_value_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS `id`, `value` FROM `shop_properties_values_$table_postfix` WHERE `product_id` = ? AND `property_id` = ?;");
			$property_value_query->execute( array($product_template, $property_id) );
            
			$property_value_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
			$property_value_rows_query->execute();
			$property_value_rows = $property_value_rows_query->fetchColumn();
			
			if( $property_value_rows > 0)
            {
                //Задаем значение
                switch($property_record["property_type_id"])
                {
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        $property_value_record = $property_value_query->fetch();
                        $property_record["value"] = $property_value_record["value"];
                        break;
                    case 5:
                        //Свойство "Линейный список" - значений может быть несколько
                        $property_record["value"] = array();
                        for($v=0; $v < $property_value_rows; $v++)
                        {
                            $property_value_record = $property_value_query->fetch();
                            array_push($property_record["value"], (integer)$property_value_record["value"]);
                        }
                        break;
					case 6:
                        //Свойство "Древовидный список" - значений может быть несколько
                        $property_record["value"] = array();
                        for($v=0; $v < $property_value_rows; $v++)
                        {
                            $property_value_record = $property_value_query->fetch();
                            array_push($property_record["value"], (integer)$property_value_record["value"]);
                        }
                        break;
                }
                
                array_push($properties_values, $property_record);
            }//~if() - есть значение свойства
        }//for($i) - по всем свойствам категории
    }//if() - если был переход для редактирования товара
    ?>
    
    <script>
    //Выводим массив ранее выбранных опций списков в javascript
    var lists_options_server_id_maps = JSON.parse('<?php echo json_encode($lists_options_server_id_maps); ?>');
    </script>
    
    
    <!--Форма для отправки-->
    <form name="form_to_save" method="post" style="display:none" enctype="multipart/form-data">
        <input name="save_action" id="save_action" type="text" value="<?php echo $action_type; ?>" style="display:none"/>
        
        
        <!-- Общие настройки товара -->
        <input type="text" name="product_id" id="product_id" value="<?php echo $product_id; ?>" />
        <input type="text" name="category_id" id="category_id" value="<?php echo $category_id; ?>" />
        <input type="text" name="caption" id="caption" value="" />
        <input type="text" name="alias" id="alias" value="" />
        <input type="text" name="title_tag" id="title_tag" value="" />
        <input type="text" name="description_tag" id="description_tag" value="" />
        <input type="text" name="keywords_tag" id="keywords_tag" value="" />
        <input type="text" name="robots_tag" id="robots_tag" value="" />
		<input type="text" name="published_flag" id="published_flag" value="" />
        
		
		<!-- Мультиязычность - для общих настроек товара -->
		<?php
		for( $i=0 ; $i < count($translated_items) ; $i++ )
		{
			?>
			<input type="hidden" name="<?php echo $translated_items[$i]; ?>_lang_str_id" value="<?php echo ${$translated_items[$i]."_lang_str_id"}; ?>" />
			<?php
		}
		?>
		
        <!-- Свойства товара -->
        <input type="text" name="properties_objects" id="properties_objects" value="" />
        
        <!-- Текстовое описание товара -->
        <input type="text" name="product_text" id="product_text" value="" />
        <input type="hidden" name="product_text_lang_str_id" id="product_text_lang_str_id" value="<?php echo $product_text_lang_str_id; ?>" />
        
        <!-- Изображения загружаются с помощью input[type="file"] -->
        <div id="img_box">
        </div>
        <input type="text" name="images_list" id="images_list" value="" />
        
		<!-- Стикеры товара -->
        <input type="text" name="product_stickers" id="product_stickers" value="" />
		
		<!-- Сопутствующие товары -->
        <input type="text" name="product_related" id="product_related" value="" />
		
        <!-- Значения линейных список, введенные вручную -->
        
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
    </form>
    <!--Форма для отправки-->
    
    
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				
				<?php
				// Ссылка на корневую страницу
				print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/catalogue/products?category_id=$category_id", "background_color"=>"#3498db", "fontawesome_class"=>"fas fa-chevron-left", "caption"=>translate_str_by_id(2961)) );
				?>
				
				<a class="panel_a" onClick="save_action();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				<?php
				//Если идет редактирование товара, то выводим кнопку для создания товара на его основе
				if($product_id > 0)
				{
					?>
					<a class="panel_a" onClick="location='/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/products/product?category_id=<?php echo $category_id; ?>&template_id=<?php echo $product_id; ?>';" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/copy.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2962); ?></div>
					</a>
					
					
					
					
					<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/logistics/stock/product?product_id=<?php echo $product_id; ?>" target="_blank">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/cargo_control.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2963); ?></div>
					</a>
					
					
					<?php
				}
				?>

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
    

    
    
	<!-- Start ШАБЛОН ТОВАРА -->
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2637); ?>
			</div>
			<div class="panel-body">
				
				<div class="form-group">
					<label for="" class="col-lg-3 control-label">
						<?php echo translate_str_by_id(2964); ?>
					</label>
					<div class="col-lg-9">
						<select id="product_template_selector" onchange="init_by_template();" class="form-control">
							<option value="0"><?php echo translate_str_by_id(2823); ?></option>
							<?php
							$products_same_category_query = $db_link->prepare("SELECT `id`, `caption` FROM `shop_catalogue_products` WHERE `category_id` = ? AND `id` != ? ORDER BY `id` DESC LIMIT 100;");
							$products_same_category_query->execute( array($category_id, $product_id) );
							while( $sample = $products_same_category_query->fetch() )
							{
								?>
								<option value="<?php echo $sample["id"]; ?>"><?php echo $sample["caption"]." (ID ".$sample["id"].")"; ?></option>
								<?php
							}
							?>
						</select>
					</div>
				</div>
			</div>
		</div>
	</div>
	<script>
	//Функция инициализации полей на основе шаблона
	function init_by_template()
	{
		var template_id = document.getElementById("product_template_selector").value;
		if( parseInt(template_id) == 0)
		{
			return;
		}
		
		//Подтверждение перехода. Спрашиваем, если страница в режиме редактирования или заполнены какие-либо поля
		if( <?php echo $product_id; ?> > 0 || 
		document.getElementById("caption_input").value != "" || 
		document.getElementById("alias_input").value != "" || 
		document.getElementById("title_tag_input").value != "" || 
		document.getElementById("description_tag_input").value != "" || 
		document.getElementById("keywords_tag_input").value != "" || 
		document.getElementById("robots_tag_input").value != "" || 
		document.getElementById("robots_tag_input").value != "" || 
		tinymce.activeEditor.getContent() != "" || 
		images_list.length > 0)
		{
			if( ! confirm("<?php echo translate_str_by_id(2965); ?>") )
			{
				return;
			}
		}

		location = "/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/products/product?category_id=<?php echo $category_id; ?>&template_id="+template_id+"<?php if($product_id != 0) echo "&product_id=".$product_id; ?>";
	}
	</script>
    <!-- End ШАБЛОН ТОВАРА -->
    
    
    
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2966); ?>
			</div>
			<div class="panel-body">
				<div class="row ">
					<div class="col-lg-12">
					<div class="col-lg-12">
						<div class="form-group">
							<table style="width:100%;">
								<tr>
									<td><input style="margin:0; margin-right: 10px; margin-top: 3px; cursor:pointer; width: 25px; height: 25px;" type="checkbox" id="avto_meta" /></td>
									<td style="width:100%;">
										<label for="avto_meta" style="margin:0; cursor:pointer;">
											<?php echo translate_str_by_id(2967); ?>
											<br/><small><?php echo translate_str_by_id(2968); ?></small>
										</label>
									</td>
								</tr>
							</table>
						</div>
					</div>
					</div>
				</div>
				<div class="row ">
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2277); ?>*
							</label>
							<div class="col-lg-6">
								<textarea class="form-control" onkeyup="initFieldsByCaption();" id="caption_input"><?php echo $caption; ?></textarea>
							</div>
						</div>
					</div>
					
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								URL*
							</label>
							<div class="col-lg-6">
								<textarea class="form-control" id="alias_input"><?php echo $alias; ?></textarea>
							</div>
						</div>
					</div>
					
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2167); ?>
							</label>
							<div class="col-lg-6">
								<textarea class="form-control" id="title_tag_input"><?php echo $title_tag; ?></textarea>
							</div>
						</div>
					</div>
					
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2237); ?>
							</label>
							<div class="col-lg-6">
								<textarea class="form-control" id="description_tag_input"><?php echo $description_tag; ?></textarea>
							</div>
						</div>
					</div>
					
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2281); ?>
							</label>
							<div class="col-lg-6">
								<textarea class="form-control" id="keywords_tag_input"><?php echo $keywords_tag; ?></textarea>
							</div>
						</div>
					</div>
					
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2282); ?>
							</label>
							<div class="col-lg-6">
								<textarea class="form-control" id="robots_tag_input"><?php echo $robots_tag; ?></textarea>
							</div>
						</div>
					</div>
					
					
					<?php
					$published_flag_checked = "";
					if($published_flag == 1)
					{
						$published_flag_checked = " checked=\"checked\"";
					}
					?>
					<div class="col-lg-4">
						<div class="form-group">
							<label for="" class="col-lg-6 control-label">
								<?php echo translate_str_by_id(2800); ?>
							</label>
							<div class="col-lg-6">
								<input type="checkbox" id="published_flag_checkbox"<?php echo $published_flag_checked; ?> />
							</div>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
    <!-- Блок автоматической подстановки значений по имени -->
    <script>
    function initFieldsByCaption()
    {
        if(document.getElementById("avto_meta").checked){
			var caption_value = document.getElementById("caption_input").value;
			
			//Инициализируем URL (alias)
			var alias = iso_9_translit(caption_value,  5);//5 - русский текст
			alias = alias.replace(/\s/g, '-');
			alias = alias.toLowerCase();
			alias = alias.replace(/[^\d\sA-Z\-_]/gi, '-');//Убираем все символы кроме букв, цифр, тире и нинего подчеркивания
			document.getElementById("alias_input").value = alias;
			
			//Инициализируем Title
			document.getElementById("title_tag_input").value = caption_value;
			
			//Иницализируем Мета-description
			document.getElementById("description_tag_input").value = caption_value;
			
			//Иницализируем Мета-keywords
			document.getElementById("keywords_tag_input").value = caption_value;
		}
    }
    </script>
    
    
    
    
    
    <script>
    var properties_objects = new Array();//Массив с объектами описания свойств
    </script>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2969); ?>
			</div>
			<div class="panel-body">
				<?php
				$category_properties_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `shop_categories_properties_map` WHERE `category_id` = ? ORDER BY `order` ASC;");
				$category_properties_query->execute( array($category_id) );
				
				$category_properties_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
				$category_properties_rows_query->execute();
				$category_properties_rows = $category_properties_rows_query->fetchColumn();
				
				for($i_p=0; $i_p < $category_properties_rows; $i_p++)
				{
					$property_record = $category_properties_query->fetch();
					
					if( $i_p > 0)
					{
						?>
						<div class="hr-line-dashed col-lg-12"></div>
						<?php
					}
					?>
					
					<div class="col-lg-12">
					
						<label for="" class="col-lg-4 control-label"><?php echo translate_str_by_id($property_record["value"]); ?></label>
					
						<?php
						$list_type_for_js = "";
						$manual_input_value = "";//Виджет для ручного ввода значений
						switch($property_record["property_type_id"])
						{
							case 1:
							case 2:
								$widget = "<input type=\"number\" name=\"property_".$property_record["id"]."\" id=\"property_".$property_record["id"]."\" value=\"\" class=\"form-control\" />";
								break;
							case 3:
								$widget = "<input type=\"text\" name=\"property_".$property_record["id"]."\" id=\"property_".$property_record["id"]."\" value=\"\" class=\"form-control\" /> <input type=\"hidden\" name=\"property_".$property_record["id"]."_lang_str_id\" id=\"property_".$property_record["id"]."_lang_str_id\" value=\"0\" class=\"form-control\" />";
								break;
							case 4:
								$widget = "<input type=\"checkbox\" name=\"property_".$property_record["id"]."\" id=\"property_".$property_record["id"]."\" />";
								break;
							case 5://СПИСОК ЗНАЧЕНИЙ
								//Получаем учетную запись списка
								$list_query = $db_link->prepare("SELECT * FROM `shop_line_lists` WHERE `id` = ?;");
								$list_query->execute( array($property_record["list_id"]) );
								$list_record = $list_query->fetch();
								$multiple = "class=\"form-control\"";//По умолчанию - просто ставим класс form-control
								$multiple_script = "";
								if($list_record["type"] == 2)//Список со множественным выбором
								{
									$multiple = "multiple=\"multiple\"";
									$multiple_script = "<script>\n$('#property_".$property_record["id"]."').multipleSelect({placeholder: \"Нажмите для выбора...\", width:\"100%\"});\n</script>";
									$list_type_for_js = "properties_objects[properties_objects.length-1].list_type = 2;";//Для объекта javascript
								}
								else//ДЛЯ СПИСКА С ЕДИНСТВЕННЫМ ВЫБОРОМ
								{
									$list_type_for_js = "properties_objects[properties_objects.length-1].list_type = 1;";//Для объекта javascript
								}
								//Виджет для ручного ввода для списков
								$manual_input_value = "<input class=\"form-control\" placeholder=\"".translate_str_by_id(2970)."...\" type=\"text\" name=\"manual_input_".$property_record["id"]."\" id=\"manual_input_".$property_record["id"]."\" />";
								
								
								//Получаем элементы списка
								$list_items = array();//Массив с элементами списка
								$list_items_query = $db_link->prepare("SELECT * FROM `shop_line_lists_items` WHERE `line_list_id` = ? ORDER BY `order`;");
								$list_items_query->execute( array($property_record["list_id"]) );
								while( $item = $list_items_query->fetch() )
								{
									array_push($list_items, array("id"=>$item["id"], "value"=>translate_str_by_id($item["value"])) );
								}
								

								$widget = "<select $multiple name=\"property_".$property_record["id"]."\" id=\"property_".$property_record["id"]."\">";
								for($o=0; $o < count($list_items); $o++)
								{
									$widget .= "<option value=\"".$list_items[$o]["id"]."\">".$list_items[$o]["value"]."</option>";
								}
								
								$widget .= "</select>".$multiple_script;
								break;
							case 6://ДРЕВОВИДНЫЙ СПИСОК
								$list_query = $db_link->prepare("SELECT * FROM `shop_tree_lists` WHERE `id` = ?;");
								$list_query->execute( array($property_record["list_id"]) );
								$list_record = $list_query->fetch();

								//Получаем текущие элементы списка
								//$needed_tree_list_id = $property_record["list_id"];//Указываем ID древовидного списка, который требуется получить
								//require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/tree_lists/get_tree_list_items.php");//Получение объекта иерархии указанного древовидного списка
								//$list_items = $tree_list_dump_JSON;

								$widget = "<div style=\"height:250px;\" name=\"property_".$property_record["id"]."\" id=\"property_".$property_record["id"]."\"></div>";
								
								$widget .= "<script>";
								$widget .= "							
								tree_list_".$property_record["id"]." = new webix.ui({
									view:\"tree\",
									
									template:\"{common.icon()} {common.checkbox()} {common.folder()} #value# (#id#)\",
									
									editable:false,
									container:\"property_".$property_record["id"]."\",
									select:true,
									drag:false,
									editor:\"text\",
									//url: \"/content/shop/catalogue/tree_lists/ajax/ajax_async_tree_loader.php\",
									on:{
										onAfterOpen:function(id)
										{
										},
										onDataRequest: function (id)
										{
											var promise = webix.ajax().sync().get(\"".$DP_Config->domain_path."/content/shop/catalogue/tree_lists/ajax/ajax_async_tree_loader.php?csrf_guard_key=".$user_session["csrf_guard_key"]."&parent_id=\" + id+\"&tree_list_id=".$property_record["list_id"]."\");
											
											this.parse(
												promise.responseText
											);
											
											// cancelling default behaviour
											return false;
										}
										,
										onAfterLoad:function()
										{
											if (typeof (tree_list_init_".$property_record["id"].") === \"function\") {
												//Проверка пройдена
												//Вызываем функцию...
												tree_list_init_".$property_record["id"]."();
											}
										}
									  },
									
								});
								webix.event(window, \"resize\", function(){ tree_list_".$property_record["id"].".adjust(); });
								
								tree_list_".$property_record["id"].".attachEvent(\"onItemCheck\", function(id)
								{
									onTreeListItemCheck(".$property_record["id"].", id);
								});
								
								";
								
								$widget .= "tree_list_".$property_record["id"].".load(\"/content/shop/catalogue/tree_lists/ajax/ajax_async_tree_loader.php?csrf_guard_key=".$user_session["csrf_guard_key"]."&parent_id=0&tree_list_id=".$property_record["list_id"]."\");";
								
								//$widget .= "tree_list_".$property_record["id"].".parse($list_items);";
								//$widget .= "tree_list_".$property_record["id"].".openAll();";
								
								
								//Добавляем функцию для обработки выставления галочек
								$widget .= "//Массив с текущими значениями списка:
								var property_".$property_record["id"]."_values = new Array();
								//Функция выставления галочек для списка
								function tree_list_init_".$property_record["id"]."()
								{
									//Отмечаем свойства в дереве
									auto_check = true;//Блокируем обработку выставления галочек
									for(var trl=0; trl < property_".$property_record["id"]."_values.length; trl++)
									{	
										if(tree_list_".$property_record["id"].".getItem(property_".$property_record["id"]."_values[trl]) != undefined)
										{
											tree_list_".$property_record["id"].".checkItem(property_".$property_record["id"]."_values[trl]);
										}
									}
									auto_check = false;
								}";
								
								
								
								
								$widget .= "</script>";
								
								break;
						}//switch
						?>
						<div class="col-lg-8">
							<?php
							if($manual_input_value != "")//Есть поле для ввода ручного значения - для списков
							{
								?>
								<div class="row">
								<div class="col-lg-6">
									<?php echo $widget; ?>
								</div>
								
								<div class="col-lg-6">
									<?php echo $manual_input_value; ?>
								</div>
								</div>
								<?php
							}
							else//Просто выводим виджет для настройки свойства
							{
								echo $widget;
							}
							?>
						</div>
					</div>

					<script>
						//Добавляем объект в список свойств на javascript
						properties_objects[properties_objects.length] = new Object;
						properties_objects[properties_objects.length-1].property_id = <?php echo $property_record["id"]; ?>;
						properties_objects[properties_objects.length-1].property_type_id = <?php echo $property_record["property_type_id"]; ?>;
						properties_objects[properties_objects.length-1].caption = '<?php echo translate_str_by_id($property_record["value"]); ?>';
						<?php echo $list_type_for_js; ?>
					</script>
					<?php
				}//for(i) - по свойствам категории
				?>
			</div>
		</div>
	</div>
	
	
	
	<script>
	// ---------------------------------------------------------------------------------------------
	//ОБРАБОТКА ДЛЯ СВОЙСТВ ТИПА "ДРЕВОВИДНЫЙ СПИСОК"
	var auto_check = false;//Для предотвращения обработки программного выставления чекбоксов (семафор)
	function onTreeListItemCheck(property_id, item_id)
	{
		if(auto_check)
		{
			return;
		}
		
		//Получаем состояние отмеченного элемента:
		var is_checked = eval("tree_list_"+property_id).isChecked(item_id);
		//console.log("Свойство: " + property_id + ", элемент: " + item_id + ", состояние: " + is_checked );
		
		auto_check = true;//Начинаем обработку чекбоксов
		
		console.log(eval("property_"+property_id+"_values"));
		
		//Далее логика
		if( is_checked )
		{
			//Добавляем отмеченный элемент в массив отмеченных
			if(eval("property_"+property_id+"_values").indexOf(parseInt(item_id)) == -1)
			{
				eval("property_"+property_id+"_values").push(parseInt(item_id));
			}
			
			
			//Выставляются все элементы, вложенные в него (рекурсивно, т.е. до упора), а также, отмечаются все элементы, находящиеся выше него по цепочке - до самого верхнего
			//Обработка вложенных элементов
			var childItems = getChildItems(property_id, item_id);
			for(var i=0; i < childItems.length; i++)
			{
				eval("tree_list_"+property_id).checkItem( childItems[i] );
				
				//Добавляем отмеченный элемент в массив отмеченных
				if(eval("property_"+property_id+"_values").indexOf(parseInt(childItems[i])) == -1)
				{
					eval("property_"+property_id+"_values").push(parseInt(childItems[i]));
				}
			}
			
			//Обработка элементов родительской ветви
			var parent_brunch = getUpperBrunch(property_id, item_id);
			for(var i=0; i < parent_brunch.length; i++)
			{
				eval("tree_list_"+property_id).checkItem( parent_brunch[i] );
				
				//Добавляем отмеченный элемент в массив отмеченных
				if(eval("property_"+property_id+"_values").indexOf(parseInt(parent_brunch[i])) == -1)
				{
					eval("property_"+property_id+"_values").push(parseInt(parent_brunch[i]));
				}
			}
		}
		else
		{
			//Удаляем снятый элемент из массива отмеченных
			if(eval("property_"+property_id+"_values").indexOf(parseInt(item_id)) != -1)
			{
				eval("property_"+property_id+"_values").splice(eval("property_"+property_id+"_values").indexOf(parseInt(item_id)), 1);
			}
			
			
			if( eval("tree_list_"+property_id).getItem(item_id).$count != eval("tree_list_"+property_id).getItem(item_id).webix_kids && eval("tree_list_"+property_id).getItem(item_id).webix_kids != undefined )
			{
				webix.message("В узле " + eval("tree_list_"+property_id).getItem(item_id).value + " ("+item_id+") не загружены вложенные узлы. Раскройте узел для управления вложенными узлами");
			}

			
			//Снимаются все элементы, вложенные в него (рекурсивно, т.е. до упора). При этом, элементы, находящиеся выше, остаются отмеченными
			//Обработка вложенных элементов
			var childItems = getChildItems(property_id, item_id);
			for(var i=0; i < childItems.length; i++)
			{
				eval("tree_list_"+property_id).uncheckItem( childItems[i] );
				
				//Удаляем снятый элемент из массива отмеченных
				if(eval("property_"+property_id+"_values").indexOf(parseInt(childItems[i])) != -1)
				{
					eval("property_"+property_id+"_values").splice(eval("property_"+property_id+"_values").indexOf(parseInt(childItems[i])), 1);
				}
				
				if( eval("tree_list_"+property_id).getItem(childItems[i]).$count != eval("tree_list_"+property_id).getItem(childItems[i]).webix_kids && eval("tree_list_"+property_id).getItem(childItems[i]).webix_kids != undefined )
				{
					webix.message("В узле " + eval("tree_list_"+property_id).getItem(childItems[i]).value + " ("+childItems[i]+") не загружены вложенные узлы. Раскройте узел для управления вложенными узлами");
				}
			}
		}
		auto_check = false;//Прекращаем обработку чекбоксов
	}
	// ---------------------------------------------------------------------------------------------
	//Рекурсивная функция получения всех вложенных элементов указанного узла дерева
	function getChildItems(property_id, item_id)
	{
		var childItems = new Array();//Массив вложенных элеметов

		var first = true;
		var nextItem = undefined;
		
		while(true)
		{
			if(first)
			{
				nextItem = eval("tree_list_"+property_id).getFirstChildId( item_id );//Первый вложенный элемент
				
				first = false;
			}
			else
			{
				nextItem = eval("tree_list_"+property_id).getNextSiblingId( nextItem );//Следующий вложенный элемент
			}
			
			
			if( nextItem == null ){break;}
			childItems.push(nextItem);//Добавляем первый вложенный элемент в массив
			
			
			if( eval("tree_list_"+property_id).getFirstChildId( nextItem ) != null )
			{	
				childItems = childItems.concat(getChildItems(property_id, nextItem));
			}
		}
		
		return childItems;
	}
	// ---------------------------------------------------------------------------------------------
	//Рекурсивная функция получения всей родительской ветви к верху дерева
	function getUpperBrunch(property_id, item_id)
	{
		var parent_brunch = new Array();//Массив ветви
		
		var parent_id = eval("tree_list_"+property_id).getParentId(item_id);//ID родительского узла
		
		//console.log(parent_id);
		
		if(parent_id != 0)
		{
			parent_brunch.push(parent_id);
			
			parent_brunch = parent_brunch.concat(getUpperBrunch(property_id, parent_id));
		}
		
		return parent_brunch;
	}
	// ---------------------------------------------------------------------------------------------
	</script>
	
	
	
	

    
    
    
    
    <!-- Start ТЕСТОВОЕ ОПИСАНИЕ ТОВАРА -->
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2971); ?>
			</div>
			<div class="panel-body">
				<textarea class="tinymce_editor" id="tinymce_editor"></textarea>
				<script>
					tinymce.init({
						selector: "textarea.tinymce_editor",
						plugins: [
							"advlist autolink lists link image charmap print preview anchor",
							"searchreplace visualblocks code fullscreen",
							"insertdatetime media table contextmenu paste textcolor"
						],
						toolbar: [ 
								"newdocument | bold italic underline strikethrough | alignleft aligncenter alignright alignjustify | styleselect | formatselect | fontselect | fontsizeselect | ", 
								"cut copy paste | bullist numlist | outdent indent | blockquote | undo redo | removeformat subscript superscript | link image | forecolor backcolor",
						]
					});
				</script>
			</div>
		</div>
	</div>
    <!-- End ТЕСТОВОЕ ОПИСАНИЕ ТОВАРА -->
    
    
    
    
    
    
    <!-- ИЗОБРАЖЕНИЯ ТОВАРА -->
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2972); ?>
			</div>
			<div class="panel-body" id="mini_images_div">
			</div>
			<div class="panel-footer">
				<button onclick="openImageDialog();" class="btn btn-success " type="button"><i class="fa fa-plus"></i> <span class="bold"><?php echo translate_str_by_id(2267); ?></span></button>
			</div>
		</div>
	</div>
	<?php
	// Deep-link into unlimited SKU photos + multi-type specs manager.
	$epcSkuMediaUrl = '/' . trim((string) ($DP_Config->backend_dir ?? 'cp'), '/') . '/shop/catalogue/sku_media';
	if (!empty($product_id)) {
		$epcSkuMediaUrl .= '?product_id=' . (int) $product_id;
	}
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				SKU photos &amp; specification sheets
			</div>
			<div class="panel-body">
				<p class="text-muted" style="margin-bottom:10px;">
					Add unlimited product photos and multiple specification types (Technical, Dimensions, Packaging, Custom…) with unlimited rows.
					Customers see a gallery + formatted specification sheets on the product page.
				</p>
				<a class="btn btn-primary" href="<?php echo htmlspecialchars($epcSkuMediaUrl, ENT_QUOTES, 'UTF-8'); ?>">
					<i class="fa fa-picture-o"></i> Manage photos &amp; specifications
				</a>
			</div>
		</div>
	</div>
    <script>
        //РАБОТА С ИЗОБРАЖЕНИЯМИ
        var images_count = 0;//Счетчик для изображений - используется исключительно для работы с изображениями на уровне браузера. Т.е. это ID изображения в сессии браузера
        var images_list = new Array();//Список изображений (информационных объектов)
        var current_image = "";//Глобальная переменная для информационного объекта
        // ----------------------------------------------------------------------------------
        //Функция получения Набора для нового текущего файла
        function getNewCurrentImageObject()
        {
            images_count++;//ID изображения в текущей сессии
            current_image = new Object;//Информационный объект для текущего изображения
            current_image.server_id = 0;//ID изображения на сервере. 0 говорит о том, что изображение еще не было загружено на сервер
            current_image.client_id = images_count;//ID изображения в текущей сессии
            //Добавляем текущую форму для изображения:
            var input_file = document.createElement("input");
            input_file.setAttribute("type","file");
            input_file.setAttribute("name","image_"+current_image.client_id);
            input_file.setAttribute("id","image_"+current_image.client_id);
            input_file.setAttribute("accept","image/jpeg,image/jpg,image/png,image/gif");
            input_file.setAttribute("onchange","onFileChanged();");
            document.getElementById('img_box').appendChild(input_file);
        }
        // ----------------------------------------------------------------------------------
        //Открыть диалог выбора файла для input[type=file] текущего изображений
        function openImageDialog()
        {
            document.getElementById("image_"+current_image.client_id).click();
        }
        // ----------------------------------------------------------------------------------
        //Обработчик при выборе изображения
        function onFileChanged()
        {
            var input_file = document.getElementById("image_"+current_image.client_id);//input для файла изображения
            var file = input_file.files[0];//Получаем выбранный файл
            if(file == undefined) return;
            
            //Проверяем тип файла
            if(file.type != "image/jpeg" && file.type != "image/jpg" && file.type != "image/png" && file.type != "image/gif")
            {
                input_file.value = null;
                alert("<?php echo translate_str_by_id(2444); ?>");
                return;
            }
            
            //Создаем локальный URL для индикации изображения:
            current_image.url = URL.createObjectURL(file);
            
            //Получаем имя файла
            current_image.name = file.name;
            current_image.name_short = file.name;
            if(current_image.name_short.length > 9)
            {
                current_image.name_short = current_image.name_short.substring(0, 9) + "...";
            }
            
            //Копируем информационный объект текущего изображения в список изображений
            images_list.push(current_image);
            
            //Новый набор для следубщего изображения
            getNewCurrentImageObject();
            
            //Отображение изображений
            showImages();
        }
        // ----------------------------------------------------------------------------------
        //Функция для отображения миниатюр
        function showImages()
        {
            var images_set = "";
            for(var i=0; i < images_list.length; i++)
            {
                images_set += "<div style=\"display:inline-block;margin-left:5px;\">";
                images_set += "<div align=\"left\"><a class=\"delete_a\" href=\"javascript:void(0);\" onclick=\"deleteImage("+images_list[i].client_id+");\"><span>x</span></a></div>";
                images_set += "<img src=\""+images_list[i].url+"\" style=\"max-width:100px; max-height:100px;\"/>";
                images_set += "<div align=\"center\">"+images_list[i].name_short+"</div>";
                images_set += "</div>";
            }
            document.getElementById("mini_images_div").innerHTML = images_set;
        }
        // ----------------------------------------------------------------------------------
        //Удаление изображений
        function deleteImage(img_client_id)
        {
            //Находим объект описания в списке
            for(var i=0; i < images_list.length; i++)
            {
                if(images_list[i].client_id == img_client_id)
                {
                    //Удаляем input[type=file]. Если server_id > 0, то input отсутствует, т.к. это изображение уже на сервере
                    if(images_list[i].server_id == 0)
                    {
                        var input_file = document.getElementById("image_"+img_client_id);
                        input_file.parentNode.removeChild(input_file);
                    }
                    
                    //Удаляем объект описания из списка
                    images_list.splice(i, 1);
                    break;
                }
            }
            
            showImages();//Перерисовываем индикацию изображений
        }
    </script>
    
	
	
	
	
	
	<!-- СТИКЕРЫ ТОВАРА -->
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<div class="panel-tools">
					<a class="showhide"><i class="fa fa-chevron-up"></i></a>
				</div>
				<?php echo translate_str_by_id(2973); ?>
			</div>
			<div class="panel-body">
				<div id="container_A" style="height:200px;">
				</div>
			</div>
			<div class="panel-footer">
				<button onclick="add_new_item_tree_stickers();" class="btn btn-success " type="button"><i class="fa fa-plus"></i> <span class="bold"><?php echo translate_str_by_id(2267); ?></span></button>
				
				<button onclick="delete_selected_item_tree_stickers();" class="btn btn-danger" type="button"><i class="fa fa-trash-o"></i> <span class="bold"><?php echo translate_str_by_id(2224); ?></span></button>
			</div>
		</div>
	</div>
	<script>
	/*
	Объект стикера состоит:
	- id (Индекс MySQL)
	- order (Порядок отображения)
	
	- value (Наименование)
	- color_background (Цвет фона)
	- color_text (Цвет текста)
	- href (Адрес ссылки, куда ведет)
	- class_css (Дополнительный css класс)
	
	- description (Тектовое описание при наведении)
	*/
	var stickers = new Array();
	
    /*ДЕРЕВО*/
    //Для редактируемости дерева
    webix.protoUI({
        name:"edittree"
    }, webix.EditAbility, webix.ui.tree);
    //Формирование дерева
    tree_stickers = new webix.ui({
        editable:false,//Не редактируемое
        container:"container_A",//id блока div для дерева
        view:"tree",
    	select:true,//можно выделять элементы
    	drag:true,//можно переносить
    });
    /*~ДЕРЕВО*/
	webix.event(window, "resize", function(){ tree_stickers.adjust(); });
    //-----------------------------------------------------
    webix.protoUI({
        name:"editlist" // or "edittree", "dataview-edit" in case you work with them
    }, webix.EditAbility, webix.ui.list);
    //-----------------------------------------------------
    //Событие при выборе элемента дерева
    tree_stickers.attachEvent("onAfterSelect", function(id)
    {
    	onSelected_tree_stickers();
    });
    //-----------------------------------------------------
    //Обработка выбора элемента
    function onSelected_tree_stickers()
    {
    }//function onSelected_tree_stickers()
    //-----------------------------------------------------
    //Событие при успешном редактировании элемента дерева
    tree_stickers.attachEvent("onValidationSuccess", function(){
        onSelected_tree_stickers();
    });
    //-----------------------------------------------------
    tree_stickers.attachEvent("onAfterEditStop", function(state, editor, ignoreUpdate){
        onSelected_tree_stickers();
    });
    //-----------------------------------------------------
	//Обработчик После перетаскивания узлов дерева
	tree_stickers.attachEvent("onAfterDrop",function(){
	    onSelected_tree_stickers();
	});
	//-----------------------------------------------------
	//Обработчик После двойного щелчка
	tree_stickers.attachEvent("onItemDblClick", function(id, e, node){
		openStickerWindow();//Открываем окно редактирования стикера
	});
    //-----------------------------------------------------
    //Добавить новый элемент в дерево
    function add_new_item_tree_stickers()
    {
    	//Добавляем элемент в выделенный узел
    	var parentId= tree_stickers.getSelectedId();//Выделеный узел
    	var newItemId = tree_stickers.add( {value:"<?php echo translate_str_by_id(2974); ?>", is_new:true, color_background:"#00FF00", color_text:"#FFFFFF", description:"", href:"", class_css:""}, tree_stickers.count(), 0);//Добавляем новый узел и запоминаем его ID
    	
    	onSelected_tree_stickers();//Обработка текущего выделения
    }
    //-----------------------------------------------------
    //Удаление выделеного элемента
    function delete_selected_item_tree_stickers()
    {
    	var nodeId = tree_stickers.getSelectedId();
		
		if(nodeId == 0)
		{
			alert("<?php echo translate_str_by_id(2975); ?>");
			return;
		}
		
    	tree_stickers.remove(nodeId);
    	onSelected_tree_stickers();
    }
    //-----------------------------------------------------
    //Снятие выделения с дерева
    function unselect_tree()
    {
    	tree_stickers.unselect();
    	onSelected_tree_stickers();
    }
    //-----------------------------------------------------
    </script>
	<!-- Модальное окно РЕДАКТИРОВАНИЕ СТИКЕРА -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalWindow_sticker" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2976); ?></h4>
					</div>
					<div class="modal-body">
						<div class="row">
							<div class="form-group">
								<label for="" class="col-lg-6 control-label">
									<?php echo translate_str_by_id(2277); ?>
								</label>
								<div class="col-lg-6">
									<input onKeyUp="sticker_changed_set();" type="text" class="form-control" id="sticker_value_input" />
								</div>
							</div>
							<div class="hr-line-dashed col-lg-12"></div>
							<div class="form-group">
								<label for="" class="col-lg-6 control-label">
									<?php echo translate_str_by_id(2977); ?>
								</label>
								<div class="col-lg-6">
									<input onChange="sticker_changed_set();" type="color" class="form-control" id="sticker_color_text_input" />
								</div>
							</div>
							<div class="hr-line-dashed col-lg-12"></div>
							<div class="form-group">
								<label for="" class="col-lg-6 control-label">
									<?php echo translate_str_by_id(2978); ?>
								</label>
								<div class="col-lg-6">
									<input onChange="sticker_changed_set();" type="color" class="form-control" id="sticker_color_background_input" />
								</div>
							</div>
							<div class="hr-line-dashed col-lg-12"></div>
							<div class="form-group">
								<label for="" class="col-lg-6 control-label">
									<?php echo translate_str_by_id(2979); ?>
								</label>
								<div class="col-lg-6">
									<input onKeyUp="sticker_changed_set();" type="text" class="form-control" id="sticker_href_input" />
								</div>
							</div>
							<div class="hr-line-dashed col-lg-12"></div>
							<div class="form-group">
								<label for="" class="col-lg-6 control-label">
									<?php echo translate_str_by_id(2980); ?>
								</label>
								<div class="col-lg-6">
									<input onKeyUp="sticker_changed_set();" type="text" class="form-control" id="sticker_class_css_input" />
								</div>
							</div>
							<div class="hr-line-dashed col-lg-12"></div>
							<div class="form-group">
								<label for="" class="col-lg-6 control-label">
									<?php echo translate_str_by_id(2981); ?>
								</label>
								<div class="col-lg-6">
									<textarea onKeyUp="sticker_changed_set();" class="form-control" id="sticker_description_input"></textarea>
								</div>
							</div>
						</div>
					</div>
					<div class="modal-footer">
						
						<button onclick="sticker_save(false);" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2189); ?></span></button>
						
						<button onclick="sticker_save(true);" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2816); ?></span></button>
					
					
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<script>
	var sticker_changed = false;//Флаг - В окне редактирования стикера были изменения
	// -----------------------------------------------------------------------------------
	//Функция открытия окна редактирования стикера
	function openStickerWindow()
	{
		sticker_changed = false;//Ставим флаг, что изменений не было
		
		//Выделенный стикер
		var nodeId = tree_stickers.getSelectedId();
		var node = tree_stickers.getItem(nodeId);
		
		//Инициализируем поля редактирования стикерова
		document.getElementById("sticker_value_input").value = node.value;
		document.getElementById("sticker_color_text_input").value = node.color_text;
		document.getElementById("sticker_color_background_input").value = node.color_background;
		document.getElementById("sticker_href_input").value = node.href;
		document.getElementById("sticker_class_css_input").value = node.class_css;
		document.getElementById("sticker_description_input").value = node.description;
		
		$('#modalWindow_sticker').modal();//Открыть окно
	}
	// -----------------------------------------------------------------------------------
	//Функция выставления флага, что были изменения при редактировании стикера
	function sticker_changed_set()
	{
		sticker_changed = true;
	}
	// -----------------------------------------------------------------------------------
	//Функция сохранения измений стикера
	function sticker_save(close_after)
	{
		//Выделенный стикер
		var nodeId = tree_stickers.getSelectedId();
		var node = tree_stickers.getItem(nodeId);
		
		//Сохраняем значения в объект дерева
		node.value = document.getElementById("sticker_value_input").value;
		node.color_text = document.getElementById("sticker_color_text_input").value;
		node.color_background = document.getElementById("sticker_color_background_input").value;
		node.href = document.getElementById("sticker_href_input").value;
		node.class_css = document.getElementById("sticker_class_css_input").value;
		node.description = document.getElementById("sticker_description_input").value;
		
		//Ставим флаг - нет изменений
		sticker_changed = false;
		
		//Скрываем окно
		if(close_after)
		{
			$('#modalWindow_sticker').modal('hide');//Скрыть окно
		}
		
		//Обновляем отображение дерева
		tree_stickers.refresh();
	}
	// -----------------------------------------------------------------------------------
	//Событие при закрытии окна
	$('#modalWindow_sticker').on('hide.bs.modal',function(){
		
		//Если есть не сохраненные изменения
		if(sticker_changed)
		{
			if( confirm("<?php echo translate_str_by_id(2982); ?>") )
			{
				return true;//Закрыть окно
			}
			else
			{
				return false;//Прервать закрытие окна
			}
		}
		else
		{
			return true;//Закрыть окно
		}
	});
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
    
    <script>
    //Функция сохранения товара
    var url_unique = false;//Переменная используется для проверки уникальности URL
    var product_id = <?php echo $product_id; ?>;
    var category_id = <?php echo $category_id; ?>;
    function save_action()
    {
        //1. Проверки корректности
        //1.1 Заполнение названия
        if(document.getElementById("caption_input").value == "")
        {
            alert("<?php echo translate_str_by_id(2983); ?>");
            return;
        }
        //1.2 Заполнение url
        var alias = document.getElementById("alias_input").value.toLowerCase();
        if(alias == "")
        {
            alert("<?php echo translate_str_by_id(2984); ?>");
            return;
        }
        //1.3 Проверка корректности alias
        var regex = new RegExp("[a-z0-9_-]{1,}");//Регулярное выражение для поля
		//Далее ищем подстроку по регулярному выражению
		var match = regex.exec(String(alias));
		if(match == null)
		{
			alert("<?php echo translate_str_by_id(2985); ?>");
			return;
		}
		else//Найдена подходящая подстрока, но, могут быть также лишние знаки - нужно проверить
		{
		    var match_value = String(match[0]);//Подходящая подстрока
			if(match_value != alias)
			{
				alert("<?php echo translate_str_by_id(2986); ?>");
				return;
			}
		}
		//1.4 Проверка уникальности url (должен быть уникальным в пределах одной категории)
    	jQuery.ajax({
    	   type: "POST",
    	   async: false, //Запрос синхронный
    	   url: "<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/content/shop/catalogue/ajax_check_product_alias.php",
    	   dataType: "json",//Тип возвращаемого значения
    	   data: "alias="+alias+"&product_id="+product_id+"&category_id="+category_id+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
    	   success: function(is_url_unique){
    			   url_unique = is_url_unique;
    	   }
    	 }); 
    	if(url_unique == false)
    	{
    		alert("<?php echo translate_str_by_id(2987); ?>");
    		return;
    	}
    	else
    	{
    		//alert("URL принят");
    	}
        
        
        
        //2. ЗАПОЛНЯЕМ ДАННЫЕ В ФОРМУ
        //2.1 Общие данные
        document.getElementById("caption").value = document.getElementById("caption_input").value;
        document.getElementById("alias").value = alias;
        document.getElementById("title_tag").value = document.getElementById("title_tag_input").value;
        document.getElementById("description_tag").value = document.getElementById("description_tag_input").value;
        document.getElementById("keywords_tag").value = document.getElementById("keywords_tag_input").value;
        document.getElementById("robots_tag").value = document.getElementById("robots_tag_input").value;
        
		if(document.getElementById("published_flag_checkbox").checked == true)
		{
			document.getElementById("published_flag").value = 1;
		}
		else
		{
			document.getElementById("published_flag").value = 0;
		}
		
		
        //2.2 Свойства товара
        //Инициализируем значения свойств в объектах описания свойств
        for(var i=0; i < properties_objects.length; i++)
        {
            switch(properties_objects[i].property_type_id)
            {
                case 1:
                case 2:
					properties_objects[i].value = document.getElementById("property_"+properties_objects[i].property_id).value;
					break;
                case 3:
                    properties_objects[i].value = document.getElementById("property_"+properties_objects[i].property_id).value;
                    properties_objects[i].value_lang_str_id = document.getElementById("property_"+properties_objects[i].property_id+"_lang_str_id").value;
                    break;
                case 4:
                    if(document.getElementById("property_"+properties_objects[i].property_id).checked)
                    {
                        properties_objects[i].value = 1;
                    }
                    else
                    {
                        properties_objects[i].value = 0;
                    }
                    break;
                case 5:
                    properties_objects[i].value = new Array();//Для свойств типа "Линейный список" - значения храним в виде массивов
                    
                    if(properties_objects[i].list_type == 1)//Для единичного типа - просто добавляем значение в массив
                    {
                        properties_objects[i].value.push(document.getElementById("property_"+properties_objects[i].property_id).value);
                    }
                    else if(properties_objects[i].list_type == 2)//Для множественного типа - копируем массив отмеченных элементов
                    {
                        properties_objects[i].value = [].concat($("#property_"+properties_objects[i].property_id).multipleSelect('getSelects'));
                    }
                    
                    properties_objects[i].manual_input = document.getElementById("manual_input_"+properties_objects[i].property_id).value;//Записываем значение, введнное вручную
                    break;
				case 6:
					properties_objects[i].value = new Array();//Для свойств типа "Древовидный список" - значения храним в виде массивов
					//properties_objects[i].value = eval("tree_list_"+properties_objects[i].property_id).getChecked();
					
					properties_objects[i].value = eval("property_"+properties_objects[i].property_id+"_values");
					break;
            }//switch - по типам свойств
        }//for(i) - по свойствам
        document.getElementById("properties_objects").value = JSON.stringify(properties_objects);
        
        //2.3 Изображения (форма уже содержит элементы input[type=file]), а сюда мы указываем список объектов описания изображений
        document.getElementById("images_list").value = JSON.stringify(images_list);
        
        
        //2.4 Текстовое описание товара
        var product_text = tinymce.activeEditor.getContent();//Получаем содержимое из текстового редактора
        document.getElementById("product_text").value = product_text;
        
		
		//2.5 Стикеры товара
		var product_stickers_JSON = tree_stickers.serialize();//Получаем JSON-представление дерева
		product_stickers_TEXT = JSON.stringify(product_stickers_JSON);
		document.getElementById("product_stickers").value = product_stickers_TEXT;
		
		
		//2.6 Сопутствующие товары
		var product_related_JSON = related_products_list.serialize();//Получаем JSON-представление дерева
		product_related_TEXT = JSON.stringify(product_related_JSON);
		document.getElementById("product_related").value = product_related_TEXT;
		
        //alert("ok");
        //return;
        
        document.forms["form_to_save"].submit();
    }
    </script>
    
    
    
    <script>
    <?php
    //ИНИЦИАЛИЗАЦИЯ ТЕКУЩИХ ДАННЫХ ТОВАРА (СВОЙСТВА И ИЗОБРАЖЕНИЯ)
    if($product_template != 0)
    {
        //ИНИЦИАЛИЗАЦИЯ СВОЙСТВ (ЕСЛИ ИДЕТ РЕДАКТИРОВАНИЕ)
        for($i = 0; $i < count($properties_values); $i++)
        {
            $type = $properties_values[$i]["property_type_id"];//ID типа свойства
            $property_id = $properties_values[$i]["property_id"];//ID свойства (т.е. id из таблицы shop_categories_properties_map)
            $value = $properties_values[$i]["value"];
            
            switch($type)
            {
                case 1:
                case 2:
                    ?>
                    document.getElementById("property_"+<?php echo $property_id; ?>).value = <?php echo $value; ?>;
                    <?php
                    break;
                case 3://Строка - в кавычках
                    ?>
                    document.getElementById("property_"+<?php echo $property_id; ?>).value = "<?php echo translate_str_by_id($value); ?>";
                    document.getElementById("property_"+<?php echo $property_id; ?>+"_lang_str_id").value = "<?php echo $value; ?>";
                    <?php
                    break;
                case 4:
                    ?>
                    document.getElementById("property_"+<?php echo $property_id; ?>).checked = <?php echo $value; ?>;
                    <?php
                    break;
                case 5:
                    ?>
                    //Массив с текущими значениями списка:
                    var property_<?php echo $property_id; ?>_values = JSON.parse('<?php echo json_encode($value); ?>');
                    
                    //В зависимости от типа списка (единичный/множественный):
                    if(document.getElementById("property_"+<?php echo $property_id; ?>).getAttribute("multiple") == "multiple")//Множественный
                    {
                        $('#property_<?php echo $property_id; ?>').multipleSelect('setSelects', property_<?php echo $property_id; ?>_values);
                    }
                    else//Единичный
                    {
                        document.getElementById("property_"+<?php echo $property_id; ?>).value = property_<?php echo $property_id; ?>_values[0];
                    }
                    <?php
                    break;
				case 6:
                    ?>
					//Массив с текущими значениями списка:
					property_<?php echo $property_id; ?>_values = JSON.parse('<?php echo json_encode($value); ?>');
					console.log(property_<?php echo $property_id; ?>_values);
                    <?php
                    break;
            }
        }//for($i)[PHP]
        // -----------------------------------------------------------------------------
        
        //ИНИЦИАЛИЗАЦИЯ ИЗОБРАЖЕНИЙ (ЕСЛИ ИДЕТ РЕДАКТИРОВАНИЕ)
        $images_id_query = $db_link->prepare("SELECT `id`, `file_name` FROM `shop_products_images` WHERE `product_id` = ?;");
		$images_id_query->execute( array($product_template) );
        while( $image_id_record = $images_id_query->fetch() )
        {
            ?>
            images_count++;//ID изображения в текущей сессии
            images_list[images_list.length] = new Object;//Объект описания изображения
			
			<?php
			//Если изображение от шаблона, то ставим соответствующий флаг - чтобы сервер знал, как обработать
			if($product_template != $product_id)
			{
				?>
				images_list[images_list.length-1].image_of_template = 1;
				<?php
			}
			?>
			
            images_list[images_list.length-1].server_id = <?php echo $image_id_record["id"]; ?>;//ID изображения на сервере
            images_list[images_list.length-1].client_id = images_count;//ID изображения в текущей сессии
            images_list[images_list.length-1].name = "<?php echo $image_id_record["file_name"]; ?>";
            images_list[images_list.length-1].name_short = images_list[images_list.length-1].name;
            if(images_list[images_list.length-1].name_short.length > 9)
            {
                images_list[images_list.length-1].name_short = images_list[images_list.length-1].name_short.substring(0, 9) + "...";
            }
            images_list[images_list.length-1].url = "<?php echo $DP_Config->domain_path; ?>/content/files/images/products_images/" + images_list[images_list.length-1].name;
            <?php
        }
        
        
        // ----------------------------------------------------------------------------
        //ИНИЦИАЛИЗАЦИЯ ТЕКСТОВОГО ОПИСАНИЯ
        ?>
        //Заполняем текущее содержимое:
        var current_content = '<?php echo addcslashes(str_replace(array("\n","\r"), '', $product_text), "'"); ?>';
        document.getElementById("tinymce_editor").value = current_content;
        <?php
		
		// ----------------------------------------------------------------------------
        //ИНИЦИАЛИЗАЦИЯ СТИКЕРОВ
		$product_stickers = json_encode($product_stickers);
		?>
		var product_stickers = <?php echo $product_stickers; ?>;
	    tree_stickers.parse(product_stickers);
	    tree_stickers.openAll();
		<?php
    }
    ?>
    
	//console.log(images_list);
	
    getNewCurrentImageObject();//СОЗДАЕМ НАБОР ДЛЯ ТЕКУЩЕГО ИЗОБРАЖЕНИЯ
    showImages();//Показываем изображения
    </script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
<!-- СОПУТСТВУЮЩИЕ ТОВАРЫ -->	
<!-- ------------------------------------------------------------------------------------------------------------------------------------------- -->
	
	<?php
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
	?>
	
	<div class="col-lg-12" id="related_products_div_col">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(781); ?>
			</div>
			<div class="panel-body">
				<div id="container_C" style="height:200px;">
				</div>
			</div>
			<div class="panel-footer">
				<button onclick="editRelatedProducts();" class="btn btn-success " type="button"><i class="fa fa-plus"></i> <span class="bold"><?php echo translate_str_by_id(2267); ?></span></button>
				<button onclick="deleteRelatedProducts();" class="btn btn-danger" type="button"><i class="fa fa-trash-o"></i> <span class="bold"><?php echo translate_str_by_id(2224); ?></span></button>
			</div>
		</div>
	</div>
	
	<style>
	#products_area .main_action_div,
	#products_area .product_div_marks
	{
		display:none;
	}
	#products_area .product_checkbox
	{
		cursor: pointer;
		width: 20px;
		height: 20px;
		margin: 0;
	}
	#products_area .product_div_tile {
		height: 310px;
		overflow: hidden;
	}
	#products_area .showAnother_tile {
		height: 310px;
	}
	#products_area .product_div_list_photo > .product_div_name, 
	#products_area .product_div_list > .product_div_name
	{
		width: auto;
		right: 0;
	}
	#products_area .product_div_tile > .product_div_name {
		white-space: normal;
	}
	#products_area .product_div_image_wrap a,
	#products_area .product_div_name a
	{
	  pointer-events: none;
	  cursor: default;
	}
	</style>
	
	<!-- Модальное окно "Выбор сопутствующих товаро" -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalWindow_relatedProducts" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2988); ?></h4>
					</div>
					<div class="modal-body">
						<div class="row">
							<div id="container_B" style="height:350px;">
							</div>
							
							<button onclick="catalogue_tree_related.openAll();" class="btn btn-primary2 " type="button"><i class="fa fa-folder-open"></i> <span class="bold"><?php echo translate_str_by_id(2937); ?></span></button>
						
							<button onclick="catalogue_tree_related.closeAll();" class="btn btn-primary " type="button"><i class="fa fa-folder"></i> <span class="bold"><?php echo translate_str_by_id(2938); ?></span></button>
							
							<button onclick="unselect_tree_related();" class="btn btn-primary " type="button"><span class="bold"><?php echo translate_str_by_id(2268); ?></span></button>
						</div>
						<br/>
						<div class="row">
							<div style="height:650px;">
								<div class="row">
								
								 <!-- БЛОК ДЛЯ РАБОТЫ С ТОВАРАМИ (ВЫДЕЛЕНИЕ, СНЯТИЕ И Т.Д.) -->
								<script>
									// -----------------------------------------------------------------------------------------------------------
									//Получение отмеченных продуктов (список ID)
									function getCheckedProducts()
									{
										var products_checkboxes = document.getElementsByClassName("product_checkbox");
										
										var products_checked = new Array();
										
										for(var i=0; i < products_checkboxes.length; i++)
										{
											if(products_checkboxes[i].checked == true)
											{
												let product = new Object;
												product.product_id = products_checkboxes[i].getAttribute("product_id");
												product.value = products_checkboxes[i].getAttribute("product_caption");
												products_checked.push(product);
											}
										}
										
										return products_checked;
									}
									// -----------------------------------------------------------------------------------------------------------
									//Отметить все (true) / Снять все (false)
									function checkAll(check)
									{
										var products_checkboxes = document.getElementsByClassName("product_checkbox");
										
										for(var i=0; i < products_checkboxes.length; i++)
										{
											products_checkboxes[i].checked = check;
										}
									}
									// -----------------------------------------------------------------------------------------------------------
								</script>
								
								<?php
								$is_products_mode = true;//Флаг - страница работает в режиме отображения товаров
								$category_block_type = 2;//Тип блоков категорий - для редактирования справочников товаров (используется в /content/shop/catalogue/printCategories.php)
								$product_block_type = 2;//Параметр для скрипта /content/shop/catalogue/printProducts.php - знать, как выводить товары
    
								//ID категории для отображения
								if(!empty($_GET["category_id"]))
								{
									$category_id = $_GET["category_id"];
								}
								else
								{
									$category_id = 0;
								}
    
								//Общий скрипт вывода товаров в основную область страницы
								require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProducts.php");
								?>
								
								</div>
								
								<div class="hidden" id="side_properties_widgets_div"></div>
							
							</div>
						</div>
					</div>
					<div class="modal-footer">
						<button onclick="checkAll(true);" class="btn btn-primary2 " type="button"><i class="fa fa-check-square"></i> <span class="bold"><?php echo translate_str_by_id(2293); ?></span></button>
						<button onclick="checkAll(false);" class="btn btn-primary " type="button"><i class="fa fa-square-o"></i> <span class="bold"><?php echo translate_str_by_id(2294); ?></span></button>
						<button onclick="applyRelatedProducts();" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2189); ?></span></button>
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2190); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	<script>
	var related_products_map = new Array();
	
	<?php
	//ИНИЦИАЛИЗАЦИЯ ИЗ БАЗЫ ДАННЫХ ПРИ ОТКРЫТИИ СТРАНИЦЫ
	$related_products_map = array();
	$main_product_ids_query = $db_link->prepare("SELECT `product_id_related`, (SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = `shop_related_products`.`product_id_related`) AS 'caption' FROM `shop_related_products` WHERE `product_id` = ? ORDER BY `order`;");
	$main_product_ids_query->execute(array($product_id));
	while( $product_id_record = $main_product_ids_query->fetch() )
	{
		$related_products_map[] = array('product_id'=>$product_id_record['product_id_related'], 'value'=>$product_id_record['caption']);
	}
	if(!empty($related_products_map)){
	?>
	related_products_map = JSON.parse('<?php echo json_encode($related_products_map); ?>');
	<?php
	}
	?>
	
	//-----------------------------------------------------
	//Кнопка "Редактировать список сопутствующих товаров"
	var catalogue_tree_related = "";
	function editRelatedProducts()
	{
		//Сбрасываем старое дерево
		catalogue_tree_related = "";
		document.getElementById("container_B").innerHTML = "";
		
		//Формирование дерева каталога
		catalogue_tree_related = new webix.ui({
			
			//Шаблон элемента дерева
			template:function(obj, common)//Шаблон узла дерева
        	{
                var folder = common.folder(obj, common);
        	    var value_text = "<span>" + obj.value + "</span>";//Вывод текста
				var checkbox = "";
				
        	    //Чекбоксы только для товаров
				if(obj.is_product == true)
                {
                    checkbox = common.checkbox(obj, common);
                }
				
                return common.icon(obj, common) + checkbox + folder + value_text;
        	},//~template
			
			editable:false,//редактируемое
			container:"container_B",//id блока div для дерева
			view:"tree",
			select:true,//можно выделять элементы
			drag:false,//можно переносить
		});
		
		webix.event(window, "resize", function(){ catalogue_tree_related.adjust(); });
		
		var catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
		catalogue_tree_related.parse(catalogue);
		
		// ----------------------------------------------------------------------------------------------------
		//Событие перед выбором элемента дерева
		catalogue_tree_related.attachEvent("onBeforeSelect", function(category_id)
		{
			ref_getProductsHTML(category_id);
		});
	
		//После отображения окна - подгоняем дерево под размер
		$('#modalWindow_relatedProducts').on('shown.bs.modal',function(){
			catalogue_tree_related.adjust();
		});
		
		checkAll(false);// Снять ранее выделенные элементы
		
		$('#modalWindow_relatedProducts').modal();//Открыть окно
	}
	//-----------------------------------------------------
	//Кнопка "Применить" в окне выбора товара
	function applyRelatedProducts()
	{
		var checked_products = getCheckedProducts();
		for(var i=0; i < checked_products.length; i++)
		{
			let flag = true;
			for(var j=0; j < related_products_map.length; j++)
			{
				if(related_products_map[j].product_id === checked_products[i].product_id){
					flag = false;
					break;
				}
			}
			if(flag === true){
				related_products_map.push(checked_products[i]);
			}
		}
		
		//Переотображаем список сопутствующих товаров
		onSelected();
		
		//Скрыть окно выбора сопутствующих товаров товаров
		$('#modalWindow_relatedProducts').modal('hide');
		
	}
	//-----------------------------------------------------
	//Снятие выделения с дерева
    function unselect_tree_related()
    {
    	catalogue_tree_related.unselect();
    	ref_getProductsHTML(0);
    }
	function ref_getProductsHTML(category_id){
		document.getElementById("products_area").innerHTML = "<div class=\"text-center\" id=\"start_loading_div\"><p><?php echo translate_str_by_id(2939); ?></p><img src=\"/content/files/images/ajax-loader-transparent.gif\" class=\"loading_img\" /></div>";
		
		propucts_request.category_id = category_id;
		propucts_request.needPagesCount = 1;//Нужна одна страница
        propucts_request.startFrom = 0;
        propucts_request.innerHTML_mode = "refresh";//Способ работы с innerHTML блока товаров (add/refresh)
		
		productsCountRequest();
		getProductsHTML();
		
	}
	//-----------------------------------------------------
	//Обработка выбора элемента в дереве основных товаров
    function onSelected()
    {
		//1 ИНИЦИАЛИЗАЦИЯ
		//Есть выделенные узлы основных товаров - инициализируем список их сопутствующих товаров
		related_products_list = "";//Сбрасываем переменную списка
		document.getElementById("container_C").innerHTML = "";//Сбрасываем линейный список сопутсвующих товаров
		
		//2 ГОТОВИМ СПИСОК СОПУТСТВУЮЩИХ ТОВАРОВ ДЛЯ ВЫДЕЛЕННЫХ ОСНОВНЫХ
		var related_products_actual = new Array();//Массив, которым нужно инициализировать список сопутсвующих товаров. Берется на основе выделенных основных товаров
		
		//Берем список сопутствующих товаров от первого элемента. Если функция выполняется здесь, значит, у остальных списки такие же
		related_products_actual = related_products_map;
		
		//3 СПИСОК СОПУТСТВУЮЩИХ
		//Инициализируем линейный список сопутсвующих товаров
		related_products_list = new webix.ui({
			editable:false,//редактируемое
			container:"container_C",//id блока div для дерева
			view:"tree",
			select:true,//можно выделять элементы
			drag:true,//можно переносить
		});
		related_products_list.parse(related_products_actual);//Показываем список сопутствующих товаров
		webix.event(window, "resize", function(){ related_products_list.adjust(); });
		
		//4 ОТОБРАЖЕНИЕ
		//Показать контейнер
		related_products_list.adjust();
    }//function onSelected()
	//-----------------------------------------------------
	function deleteRelatedProducts(){
		let product = related_products_list.getSelectedItem();
		if(product){
			var products_checked = new Array();
			
			for(var i=0; i < related_products_map.length; i++)
			{
				if(related_products_map[i].product_id !== product.product_id)
				{
					products_checked.push(related_products_map[i]);
				}
			}
			
			related_products_map = products_checked;
			
			onSelected();
		}
	}
	
	onSelected();
	</script>
	
<!-- ------------------------------------------------------------------------------------------------------------------------------------------- -->	
<!-- СОПУТСТВУЮЩИЕ ТОВАРЫ END -->



    <?php
}
?>