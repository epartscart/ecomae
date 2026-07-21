<?php
/**
 * Страница редатора каталога
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/dp_category_record.php");//Определение класса записи категории
require_once($_SERVER["DOCUMENT_ROOT"]."/content/general_pages/epc_cp_page_frame.php");

/**
 * Safe UI string for catalogue editor — never echo PHP null / literal "null".
 */
function epc_cat_ed_t($str_id, $fallback = '')
{
	$t = translate_str_by_id($str_id);
	if ($t === null || $t === false) {
		$t = '';
	}
	$t = trim((string) $t);
	if ($t === '' || strcasecmp($t, 'null') === 0) {
		return $fallback !== '' ? $fallback : '';
	}
	return $t;
}

/**
 * Coerce hierarchy field to a safe string before htmlentities / DB write.
 */
function epc_cat_ed_str($value)
{
	if ($value === null || $value === false) {
		return '';
	}
	$s = trim((string) $value);
	return (strcasecmp($s, 'null') === 0) ? '' : $s;
}

// --------------------------------- Start PHP - метод ---------------------------------
//Рекурсивная функция для перевода иерархического массива (JSON перечня категорий) в линейный массив (просто набор объектов категорий)
function getLinearListOfCategories($hierarchy_array)
{
    $linear_array = array();//Линейный массив
    
    for($i=0; $i<count($hierarchy_array); $i++)
    {
        //Генерируем объект записи материала и заносим его в линейный массив
        $current_category = new DP_CatalogueCategory;
        $current_category->id = $hierarchy_array[$i]["id"];
        $current_category->alias = epc_cat_ed_str($hierarchy_array[$i]["alias"] ?? '');
        $current_category->url = epc_cat_ed_str($hierarchy_array[$i]["url"] ?? '');
        $current_category->count = $hierarchy_array[$i]['$count'];
        $current_category->level = $hierarchy_array[$i]['$level'];
		$current_category->parent = $hierarchy_array[$i]['$parent'];
        $current_category->robots_tag = htmlentities(epc_cat_ed_str($hierarchy_array[$i]['robots_tag'] ?? ''), ENT_QUOTES, "UTF-8", false);
        $current_category->import_format = epc_cat_ed_str($hierarchy_array[$i]['import_format'] ?? '');
        $current_category->export_format = epc_cat_ed_str($hierarchy_array[$i]['export_format'] ?? '');
        $current_category->properties = $hierarchy_array[$i]['properties'];
		$current_category->published_flag = $hierarchy_array[$i]['published_flag'];
		$current_category->image = epc_cat_ed_str($hierarchy_array[$i]['image'] ?? '');
		$current_category->img_blob = $hierarchy_array[$i]['img_blob'] ?? '';
		$current_category->img_blob_name = $hierarchy_array[$i]['img_blob_name'] ?? '';
		$current_category->by_template = $hierarchy_array[$i]['by_template'] ?? 0;
        
		
		//Мультиязычность. Получаем поля для переводимых строк
		for( $f=0 ; $f < count( $current_category->translated_items ) ; $f++ )
		{
			$fieldName = $current_category->translated_items[$f];
			$fieldVal = isset($hierarchy_array[$i][$fieldName]) ? epc_cat_ed_str($hierarchy_array[$i][$fieldName]) : '';
			if ($fieldName === 'value' && $fieldVal === '') {
				$fieldVal = 'Category #' . (int) $current_category->id;
			}
			$current_category->{$fieldName} = htmlentities($fieldVal, ENT_QUOTES, "UTF-8", false);
			
			//ID строки
			$current_category->{$fieldName."_lang_str_id"} = $hierarchy_array[$i][$fieldName."_lang_str_id"] ?? 0;
		}
		
		
        array_push($linear_array, $current_category);
        
        //Рекурсивный вызов для вложенного уровня
        if($hierarchy_array[$i]['$count'] > 0)
        {
            $data_linear_array = getLinearListOfCategories($hierarchy_array[$i]["data"]);
            //Добавляем массив вложенного уровня к текущему
            for($j=0; $j<count($data_linear_array); $j++)
            {
                array_push($linear_array, $data_linear_array[$j]);
            }//for(j)
        }
    }//for(i)
    
    return $linear_array;
}//~function getLinearListOfCategories($hierarchy_array)
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~


if(!empty($_POST["save_tree"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	try
	{
		//Старт транзакции
		if( ! $db_link->beginTransaction()  )
		{
			throw new Exception(translate_str_by_id(2132));
		}
		
		//Генерируем линейный массив на основе полученого иерархического
		$php_dump = json_decode($_POST["tree_json"], true);
		$linear_array = array();//Линейный массив материалов
		$linear_array = getLinearListOfCategories($php_dump);//Генерируем линейный массив категорий
		
		//Создаем объект, чтобы обращаться к массиву с именами переводимых строк
		$category_class = new DP_CatalogueCategory;
		
		
		//По всем элементам линейного массива: Созданние и Обновление
		for($i=0; $i<count($linear_array); $i++)
		{
			$is_category_new = true;
			
			$order = $i+1;//Порядок отображения категории
			
			
			//Мультиязычность
			//Обработка мультиязычности
			//Вызов функции сохранения строки в виде перевода на текущий язык панели управления (кастомный алгоритм). В ответ вернется ID этой строки, который и нужно будет сохранить
			for( $f=0 ; $f < count( $category_class->translated_items ) ; $f++ )
			{
				$linear_array[$i]->{$category_class->translated_items[$f]} = save_custom_translation($linear_array[$i]->{$category_class->translated_items[$f]."_lang_str_id"}, $linear_array[$i]->{$category_class->translated_items[$f]});
				
				if( $linear_array[$i]->{$category_class->translated_items[$f]} == 0 )
				{
					throw new Exception('Error saving custom translation');
				}
			}

			
			//Проверяем существование записи категории:
			$check_category_exist_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `id`=?;");
			$check_category_exist_query->execute( array($linear_array[$i]->id) );
			if($check_category_exist_query->fetchColumn() == 1)
			{
				$is_category_new = false;//Категория уже существовала ранее
				//Запись существует - ее нужно обновить

				if( ! $db_link->prepare("UPDATE `shop_catalogue_categories` SET `alias`=?, `url`=?, `count`=?, `level`=?, `value`=?, `parent`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `robots_tag`=?, `import_format`=?, `export_format`=?, `order` = ?, `published_flag` = ?, `image` = ? WHERE `id`=?;")->execute( array($linear_array[$i]->alias, $linear_array[$i]->url, $linear_array[$i]->count, $linear_array[$i]->level, $linear_array[$i]->value, $linear_array[$i]->parent, $linear_array[$i]->title_tag, $linear_array[$i]->description_tag, $linear_array[$i]->keywords_tag, $linear_array[$i]->robots_tag, $linear_array[$i]->import_format, $linear_array[$i]->export_format, $order, $linear_array[$i]->published_flag, $linear_array[$i]->image, $linear_array[$i]->id) ) )
				{
					throw new Exception(translate_str_by_id(2765));
				}
			}
			else
			{
				//Запись не существует - ее нужно создать
				if( ! $db_link->prepare("INSERT INTO `shop_catalogue_categories` (`id`, `alias`, `url`,`count`, `level`, `value`, `parent`, `title_tag`, `description_tag`, `keywords_tag`, `robots_tag`, `import_format`, `export_format`, `order`, `published_flag`, `image`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);")->execute( array($linear_array[$i]->id, $linear_array[$i]->alias, $linear_array[$i]->url, $linear_array[$i]->count, $linear_array[$i]->level, $linear_array[$i]->value, $linear_array[$i]->parent, $linear_array[$i]->title_tag, $linear_array[$i]->description_tag, $linear_array[$i]->keywords_tag, $linear_array[$i]->robots_tag, $linear_array[$i]->import_format, $linear_array[$i]->export_format, $order, $linear_array[$i]->published_flag, $linear_array[$i]->image) ) )
				{
					throw new Exception(translate_str_by_id(2766));
				}
			}
			
			
			//НОВЫЙ ПОДЭТАП - РАБОТА СО СВОЙСТВАМИ КАТЕГОРИЙ
			$properties = $linear_array[$i]->properties;
			//Удаляем свойства, которые были удалены при редактировании категории - только если категория НЕ новая
			if($is_category_new == false)
			{
				//Получаем все свойства категории, какие у нас вообще только есть
				
				$all_category_properties_query = $db_link->prepare("SELECT * FROM `shop_categories_properties_map` WHERE `category_id` = ?;");
				$all_category_properties_query->execute( array($linear_array[$i]->id) );
				
				while( $current_property = $all_category_properties_query->fetch() )
				{
					$do_not_delete = false;//Флаг - не нужно удалять
					for($p = 0; $p < count($properties); $p++)
					{
						if($current_property["id"] == $properties[$p]["id"])
						{
							$do_not_delete = true;
							break;//for($p)
						}
					}
					
					if($do_not_delete == false)
					{
						if( ! $db_link->prepare("DELETE FROM `shop_categories_properties_map` WHERE `id` = ?;")->execute( array($current_property["id"]) ) )
						{
							throw new Exception(translate_str_by_id(2767));
						}
					}
				}//for($ep)
			}//if($is_category_new == false)
			//Создаем или обновляем свойства
			for($p = 0; $p < count($properties); $p++)
			{
				$property_order = $p + 1;//Порядковый номер свойства - для нужно расположения при отображении
				
				$SQL = "";
				
				
				//Мультиязычность для имен свойств (кастомный алгоритм)
				$property_lang_str_id = 0;//Это, если свойство новое
				if( $properties[$p]["just_created"] != true )
				{
					$property_lang_str_id = $properties[$p]["value_lang_str_id"];//Это, если свойство старое
				}
				$properties[$p]["value"] = save_custom_translation($property_lang_str_id, htmlentities($properties[$p]["value"], ENT_QUOTES, "UTF-8", false));
				if( $properties[$p]["value"] == 0 )
				{
					throw new Exception('Error saving translation of propery name - custom function');
				}
				
				//Если свойство имеет флаг just_created = 1, значит - это новое свойство - INSERT
				if($properties[$p]["just_created"] == true)
				{
					$SQL = "INSERT INTO `shop_categories_properties_map` (`category_id`, `property_type_id`, `value`, `list_id`, `order`, `for_similar`, `is_option`) VALUES (?, ?, ?, ?, ?, ?, ?);";
					
					$binding_values = array($properties[$p]["category_id"], $properties[$p]["property_type_id"], $properties[$p]["value"], $properties[$p]["list_id"], $property_order, $properties[$p]["for_similar"], $properties[$p]["is_option"]);
				}
				else//Свойтво уже было - то у него поле id равно id из таблицы свойств - UPDATE
				{
					$SQL = "UPDATE `shop_categories_properties_map` SET `value` = ?, `list_id` = ?, `order` = ?, `for_similar` = ?, `is_option` = ?  WHERE `id` = ?;";
					
					$binding_values = array($properties[$p]["value"], $properties[$p]["list_id"], $property_order, $properties[$p]["for_similar"], $properties[$p]["is_option"], $properties[$p]["id"]);
				}
				
				
				if( ! $db_link->prepare($SQL)->execute( $binding_values ) )
				{
					throw new Exception(translate_str_by_id(2768));
				}
				//echo "Выполнено: ".$SQL."<br>";
			}
		}//for($i) По всем элементам линейного массива:
		
		
		//По всем записям базы данных для удаления записей, которые были удалены при редактировании
		$deleted_categories_list = array();//Массив с ID удаляемыйх категорий
		$deleted_categories_images = array();//Список имен файлов изображений удаленных категорий
		$all_categories_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories`;");
		$all_categories_query->execute();
		while( $category_record = $all_categories_query->fetch() )
		{
			$such_category_exist = false;
			for($j=0; $j < count($linear_array); $j++)
			{
				if($category_record["id"] == $linear_array[$j]->id)
				{
					$such_category_exist = true;
					break;
				}
			}
			
			//Если такой категории нет в сохраняемом перечне, значит при редактировании она была удалена - удаляем ее из БД, а также удаляем ее пиктограмму
			if(!$such_category_exist)
			{
				array_push($deleted_categories_list, $category_record["id"]);//Добавляем ID в список
				if( ! $db_link->prepare("DELETE FROM `shop_catalogue_categories` WHERE `id` = ?;")->execute( array($category_record["id"]) ) )
				{
					throw new Exception(translate_str_by_id(2769));
				}
				
				//Добавляем имя файла изображения этой категории в список файлов на удаление
				if( array_search($category_record["image"], $deleted_categories_images) === false && $category_record["image"] != NULL && $category_record["image"] != "" )
				{
					$deleted_categories_images[] = $category_record["image"];
				}
			}
		}
		//Удаляем свойства удаленных категорий
		$SQL_DELETE_PROPERTIES = "DELETE FROM `shop_categories_properties_map` WHERE";
		$binding_values = array();
		for($i=0; $i < count($deleted_categories_list); $i++)
		{
			if($i > 0)
			{
				$SQL_DELETE_PROPERTIES .= " OR";
			}
			$SQL_DELETE_PROPERTIES .= " category_id = ?";
			
			
			array_push($binding_values, $deleted_categories_list[$i]);
		}
		if(count($deleted_categories_list) > 0)
		{
			if( ! $db_link->prepare($SQL_DELETE_PROPERTIES)->execute( $binding_values ) )
			{
				throw new Exception(translate_str_by_id(2770));
			}
		}
		//УДАЛЯЕМ ПРОДУКТЫ УДАЛЕННЫХ КАТЕГОРИЙ
		if(count($deleted_categories_list) > 0)
		{
			//Получаем список продуктов каждой категории
			$products_to_delete = array();
			$SQL_SELECT_PRODUCTS_TO_DELETE = "SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` IN (";
			$binding_values = array();
			for($i=0; $i < count($deleted_categories_list); $i++)
			{
				if($i > 0)
				{
					$SQL_SELECT_PRODUCTS_TO_DELETE .= ",";
				}
				$SQL_SELECT_PRODUCTS_TO_DELETE .= "?";
				
				array_push($binding_values, $deleted_categories_list[$i]);
			}
			$SQL_SELECT_PRODUCTS_TO_DELETE .= ");";
			
			$products_to_delete_query = $db_link->prepare($SQL_SELECT_PRODUCTS_TO_DELETE);
			$products_to_delete_query->execute($binding_values);
			while($product = $products_to_delete_query->fetch() )
			{
				array_push($products_to_delete, $product["id"]);
			}
			
			if(count($products_to_delete) > 0)
			{
				//Подключаем модульный скрипт для удаления продуктов (он работает в контексте транзакции)
				require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/catalogue/delete_products_sub.php");
			}
		}// ~ удаление товаров удаленных категорий
		

		
		//СОХРАНЕНИЕ ИЗОБРАЖЕНИЙ
		$warning_message = '';//Если загрузка одного или нескольких файлов будет с ошибкой, то, запись в БД продолжаем. Но, пользователю нужно показать warning
		$warning_message_for_blob = '';
		$files_upload_dir = $_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/";
		for($i=0; $i < count($linear_array); $i++)
		{
			//Если категория создается на основе шаблона, то, проверяем наличие изображения в поле blob
			if( isset( $linear_array[$i]->by_template ) )
			{
				if( $linear_array[$i]->by_template > 0 )
				{
					if( !empty( $linear_array[$i]->img_blob ) )
					{
						//Получаем расширение файла
						$file_extension = explode(".", $linear_array[$i]->img_blob_name);
						$file_extension = $file_extension[count($file_extension)-1];
						
						
						//Проверка расширения
						$file_extension = strtolower($file_extension);
						if( array_search( $file_extension, array('png', 'jpg', 'jpeg', 'gif') ) === false )
						{
							throw new Exception(translate_str_by_id(2771));
						}
						
						
						
						//Имя файла будет вида <id категории>.$file_extension
						$saved_file_name = $linear_array[$i]->id.".".$file_extension;
						
						//Прежде, чем сохранять файл с таким именем, нужно проверить, чтобы в других категориях это имя файла не использовалось (иначе при замене файла у них файл тоже заменится)
						$file_used_by_other = false;//Флаг "Файл используется в других категориях"
						$file_pref = 1;//Префикс для уникальности имени файла
						do
						{
							$check_file_use_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `image` = ? AND `id` != ?;');
							$check_file_use_query->execute( array( $saved_file_name, $linear_array[$i]->id) );
							if( $check_file_use_query->fetchColumn() > 0 )
							{
								$file_used_by_other = true;//Имя файла используется другими категориями, заменять файл нельзя
								
								//Имя файла будет вида <id категории>_<префикс>.$file_extension
								$saved_file_name = $linear_array[$i]->id."_".$file_pref.".".$file_extension;
								
								$file_pref++;
							}
							else
							{
								$file_used_by_other = false;//Имя уникально
							}
							
						}while( $file_used_by_other );
						//В итоге - полный путь к файлу
						$uploadfile = $files_upload_dir.$saved_file_name;
						
						//Создаем файл из blob шаблона категории
						$ifp = fopen( $uploadfile, 'wb' );//На запись в бинарном виде
						if( $ifp )
						{
							fwrite( $ifp, base64_decode( $linear_array[$i]->img_blob ) );
							fclose( $ifp ); 
						}
						else
						{
							$warning_message_for_blob = translate_str_by_id(2772).'. ';
						}
						
						if( ! $db_link->prepare("UPDATE `shop_catalogue_categories` SET `image` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $linear_array[$i]->id) ) )
						{
							throw new Exception(translate_str_by_id(2773));
						}
					}
				}
			}
			
			
			
			
			//Если для данной категории загружается файл через инпут.
			$FILE_POST = $_FILES["img_".$linear_array[$i]->id];
			
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
					throw new Exception(epc_cat_ed_t(2774, 'Invalid image file type'));
				}
				
				
				//Имя файла будет вида <id категории>.$file_extension
				$saved_file_name = $linear_array[$i]->id.".".$file_extension;
				
				
				//Прежде, чем сохранять файл с таким именем, нужно проверить, чтобы в других категориях это имя файла не использовалось (иначе при замене файла у них файл тоже заменится)
				$file_used_by_other = false;//Флаг "Файл используется в других категориях"
				$file_pref = 1;//Префикс для уникальности имени файла
				do
				{
					$check_file_use_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `image` = ? AND `id` != ?;');
					$check_file_use_query->execute( array( $saved_file_name, $linear_array[$i]->id) );
					if( $check_file_use_query->fetchColumn() > 0 )
					{
						$file_used_by_other = true;//Имя файла используется другими категориями, заменять файл нельзя
						
						//Имя файла будет вида <id категории>_<префикс>.$file_extension
						$saved_file_name = $linear_array[$i]->id."_".$file_pref.".".$file_extension;
						
						$file_pref++;
					}
					else
					{
						$file_used_by_other = false;//Имя уникально
					}
					
				}while( $file_used_by_other );
				
				
				$uploadfile = $files_upload_dir.$saved_file_name;
				
				if ( ! copy($FILE_POST['tmp_name'], $uploadfile) )
				{
					//throw new Exception("Ошибка загрузки файла для пиктограммы категории");
					$warning_message = translate_str_by_id(2775).'. ';
				}
				else if( ! $db_link->prepare("UPDATE `shop_catalogue_categories` SET `image` = ? WHERE `id` = ?;")->execute( array($saved_file_name, $linear_array[$i]->id) ) )
				{
					throw new Exception(translate_str_by_id(2776));
				}
			}
		}
		$warning_message = $warning_message.$warning_message_for_blob;
		
		
		//Новый блок для работы с изображениями
		//Удаляем изображения удаленных категорий
		for( $i=0 ; $i < count($deleted_categories_images) ; $i++)
		{
			//Перед тем, как удалить файл, проверяем, используется ли он в другой категории
			$check_file_use_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `image` = ?;');
			$check_file_use_query->execute( array( $deleted_categories_images[$i] ) );
			if( $check_file_use_query->fetchColumn() == 0 )
			{
				//Данный файл не используется в других категориях - удаляем его
				if( file_exists( $_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/".$deleted_categories_images[$i] ) )
				{
					if( ! unlink($_SERVER["DOCUMENT_ROOT"]."/content/files/images/catalogue_images/".$deleted_categories_images[$i]))
					{
						//throw new Exception("Ошибка удаления файла для пиктограммы категории");
					}
				}
			}
		}
		
	}
	catch (Exception $e)
	{
		//Откатываем все изменения
		$db_link->rollBack();
		
		?>
        <script>
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/catalogue_editor?error_message=<?php echo urlencode($e->getMessage().". ".translate_str_by_id(2142)."."); ?>";
        </script>
        <?php
        exit;
	}

	//Дошли до сюда, значит выполнено ОК
	$db_link->commit();//Коммитим все изменения и закрываем транзакцию
	
	?>
	<script>
		location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/catalogue_editor?success_message=<?php echo urlencode(translate_str_by_id(2777)); ?>&warning_message=<?php echo urlencode($warning_message); ?>";
	</script>
	<?php
	exit;
}
else//Если действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/get_catalogue_tree.php");//Получение объекта иерархии существующих категорий для вывода в дерево-webix
    
	/*
	//Получаем ID следующей категории товара
	$next_id_query = $db_link->prepare("SHOW TABLE STATUS LIKE 'shop_catalogue_categories'");
	$next_id_query->execute();
	$next_id_record = $next_id_query->fetch();
	if( $next_id_record == false )
	{
		exit("SQL error: next_id_query");
	}
    $next_id = $next_id_record["Auto_increment"];//ID следующей добавляемой категории товара
	*/
	//Определяем следующий ID ($next_id)
	$table_name = "shop_catalogue_categories";
	$col_name = "id";//Имя колонки, в которой содержится id записей (обычно имя равно id)
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/lib/docpart/get_next_id.php");

	$backend_dir = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	if ($backend_dir === '') {
		$backend_dir = 'cp';
	}
	$css_path = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/epc_catalogue_editor.css';
	$css_ver = is_file($css_path) ? (string) filemtime($css_path) : '1';
	if (function_exists('epc_cp_register_page_assets')) {
		epc_cp_register_page_assets(array('/content/shop/catalogue/epc_catalogue_editor.css?v=' . rawurlencode($css_ver)));
	}
	if (function_exists('epc_cp_page_frame_open')) {
		epc_cp_page_frame_open(array(
			'class' => 'epc-cat-editor-frame',
			'hero' => array(
				'badge' => 'Catalogue',
				'title' => epc_cat_ed_t(2113, 'Catalogue editor'),
				'sub' => 'Build and maintain your product category tree — names, SEO, images, and properties.',
				'actions' => array(
					array(
						'url' => '/' . $backend_dir . '/shop/catalogue/products',
						'label' => 'Products',
						'icon' => 'fa-th-large',
						'primary' => true,
					),
					array(
						'url' => '/' . $backend_dir . '/shop/catalogue/sku_media',
						'label' => 'SKU media',
						'icon' => 'fa-camera',
					),
				),
			),
		));
	}

	$lbl_add = htmlspecialchars(epc_cat_ed_t(2267, 'Add category'), ENT_QUOTES, 'UTF-8');
	$lbl_delete = htmlspecialchars(epc_cat_ed_t(2224, 'Delete'), ENT_QUOTES, 'UTF-8');
	$lbl_unselect = htmlspecialchars(epc_cat_ed_t(2268, 'Clear selection'), ENT_QUOTES, 'UTF-8');
	$lbl_props = htmlspecialchars(epc_cat_ed_t(2778, 'Properties'), ENT_QUOTES, 'UTF-8');
	$lbl_save = htmlspecialchars(epc_cat_ed_t(2114, 'Save'), ENT_QUOTES, 'UTF-8');
	$lbl_home = htmlspecialchars(epc_cat_ed_t(2116, 'Control panel'), ENT_QUOTES, 'UTF-8');
	$lbl_tree = htmlspecialchars(epc_cat_ed_t(2784, 'Category tree'), ENT_QUOTES, 'UTF-8');
	$lbl_params = htmlspecialchars(epc_cat_ed_t(2799, 'Category parameters'), ENT_QUOTES, 'UTF-8');
	$lbl_base = htmlspecialchars(epc_cat_ed_t(2779, 'Add base properties for new categories'), ENT_QUOTES, 'UTF-8');
	$lbl_templates = htmlspecialchars(epc_cat_ed_t(2707, 'Templates'), ENT_QUOTES, 'UTF-8');
	$lbl_create_tpl = htmlspecialchars(epc_cat_ed_t(2785, 'Save as template'), ENT_QUOTES, 'UTF-8');
	$hint_base = htmlspecialchars(
		epc_cat_ed_t(2778, 'Properties')
		. '<ul><li><b>' . epc_cat_ed_t(2071, 'Manufacturer') . '</b> (' . epc_cat_ed_t(2781, 'type') . ' &quot;' . epc_cat_ed_t(2006, 'Text') . '&quot;)</li>'
		. '<li><b>' . epc_cat_ed_t(2070, 'Article') . '</b> (' . epc_cat_ed_t(2781, 'type') . ' &quot;' . epc_cat_ed_t(2782, 'List') . '&quot;)</li></ul>'
		. epc_cat_ed_t(2783, 'Base properties are added automatically when you create a category.'),
		ENT_QUOTES,
		'UTF-8'
	);
	$hint_tpl = htmlspecialchars(
		'<b>' . epc_cat_ed_t(2786, 'Category templates') . '</b><br><br>'
		. epc_cat_ed_t(2787, 'Save a branch as a reusable template, then paste it into another place in the tree.') . '<br><br>'
		. '<b>' . epc_cat_ed_t(2788, 'Create') . '</b>, ' . epc_cat_ed_t(2789, 'select a category and click') . ' &quot;' . epc_cat_ed_t(2790, 'Save as template') . '&quot;. '
		. epc_cat_ed_t(2791, 'The selected branch becomes a template.') . '<br><br>'
		. '<b>' . epc_cat_ed_t(2792, 'Apply') . '</b> ' . epc_cat_ed_t(2793, 'open') . ' &quot;' . epc_cat_ed_t(2707, 'Templates') . '&quot; '
		. epc_cat_ed_t(2794, 'and use') . ' &quot;' . epc_cat_ed_t(2795, 'Copy') . '&quot;. '
		. epc_cat_ed_t(2796, 'Then paste with') . ' &quot;' . epc_cat_ed_t(2797, 'Paste') . '&quot; (' . epc_cat_ed_t(2798, 'tree footer') . ').',
		ENT_QUOTES,
		'UTF-8'
	);

	$base_properties_checked = ' checked="checked" ';
	if (isset($_COOKIE['base_properties']) && (int) $_COOKIE['base_properties'] != 1) {
		$base_properties_checked = '';
	}
    ?>
    
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>

	<div class="epc-cat-editor">
		<div class="epc-cat-editor__toolbar" role="toolbar" aria-label="Catalogue actions">
			<button type="button" class="epc-ce-btn" onclick="add_new_item();"><i class="fa fa-plus"></i> <?php echo $lbl_add; ?></button>
			<button type="button" class="epc-ce-btn epc-ce-btn--danger" onclick="delete_selected_item();"><i class="fa fa-trash"></i> <?php echo $lbl_delete; ?></button>
			<button type="button" class="epc-ce-btn" onclick="unselect_tree();"><i class="fa fa-times"></i> <?php echo $lbl_unselect; ?></button>
			<button type="button" class="epc-ce-btn" onclick="open_properties_window();"><i class="fa fa-list"></i> <?php echo $lbl_props; ?></button>
			<button type="button" class="epc-ce-btn epc-ce-btn--primary" onclick="save_tree();"><i class="fa fa-save"></i> <?php echo $lbl_save; ?></button>
			<span class="epc-cat-editor__toolbar-spacer"></span>
			<a class="epc-ce-btn" href="/<?php echo htmlspecialchars($backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/catalogue/products"><i class="fa fa-th-large"></i> Products</a>
			<a class="epc-ce-btn" href="/<?php echo htmlspecialchars($backend_dir, ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-home"></i> <?php echo $lbl_home; ?></a>
		</div>

		<div class="epc-cat-editor__options">
			<script>
			function add_base_properties()
			{
				return document.getElementById("base_properties").checked === true;
			}
			function base_properties_changed()
			{
				var base_properties = add_base_properties() ? "1" : "0";
				var date = new Date(new Date().getTime() + 15552000 * 1000);
				document.cookie = "base_properties="+base_properties+"; path=/; expires=" + date.toUTCString();
			}
			</script>
			<input onchange="base_properties_changed();" type="checkbox" value="base_properties" id="base_properties" <?php echo $base_properties_checked; ?> />
			<label for="base_properties"><?php echo $lbl_base; ?></label>
			<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo $hint_base; ?>');"><i class="fa fa-info"></i></button>
		</div>

		<div class="epc-cat-editor__workspace">
			<div class="epc-cat-editor__pane">
				<div class="epc-cat-editor__pane-h">
					<div>
						<h3><?php echo $lbl_tree; ?></h3>
						<span>Drag to reorder · double-click to rename</span>
					</div>
				</div>
				<div class="epc-cat-editor__search">
					<i class="fa fa-search" aria-hidden="true"></i>
					<input type="search" id="epc_cat_tree_filter" placeholder="Filter categories…" autocomplete="off" />
				</div>
				<div class="epc-cat-editor__tree-wrap">
					<div id="container_A"></div>
				</div>
				<div class="epc-cat-editor__pane-f">
					<div id="tree_footer_buttons"></div>
					<div id="copy_cut_buffer_div" class="text-right"></div>
				</div>
				<div class="epc-cat-editor__pane-f">
					<a class="btn btn-primary btn-sm" href="javascript:void(0);" onclick="open_templates_window();"><i class="far fa-clone"></i> <span class="bold"><?php echo $lbl_templates; ?></span></a>
					<a class="btn btn-info btn-sm create-template-button" href="javascript:void(0);" onclick="create_template();"><i class="fas fa-arrow-left"></i> <span class="bold"><?php echo $lbl_create_tpl; ?></span></a>
					<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo $hint_tpl; ?>');"><i class="fa fa-info"></i></button>
				</div>
			</div>

			<div class="epc-cat-editor__pane" id="content_info_div_col">
				<div class="epc-cat-editor__pane-h">
					<div>
						<h3><?php echo $lbl_params; ?></h3>
						<span>Select a category to edit details</span>
					</div>
				</div>
				<div class="epc-cat-editor__detail-body" id="content_info_div">
					<div class="epc-ce-empty">Select a category in the tree to view and edit its parameters.</div>
				</div>
			</div>
		</div>
	</div>

    <!--Форма для отправки-->
    <form name="form_to_save" method="post" enctype="multipart/form-data" aria-hidden="true">
        <input name="save_tree" id="save_tree" type="hidden" value="ok"/>
        <input name="tree_json" id="tree_json" type="hidden" value=""/>
		<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string) $user_session["csrf_guard_key"], ENT_QUOTES, 'UTF-8'); ?>" />
        <div id="img_box"></div>
    </form>
    <!--Форма для отправки-->

	<script src="/<?php echo $DP_Config->backend_dir; ?>/content/shop/catalogue/copy_paste_categories.js.php"></script>

    
    <script type="text/javascript" charset="utf-8">
    var next_id = <?php echo (int) $next_id; ?>;//id следующей категории
    var epc_cat_no_image = <?php echo json_encode('/content/files/images/no_image.png', JSON_UNESCAPED_SLASHES); ?>;
    var epc_cat_image_base = <?php echo json_encode(rtrim((string) $DP_Config->domain_path, '/') . '/content/files/images/catalogue_images/', JSON_UNESCAPED_SLASHES); ?>;

    function epcCatStr(v, fallback)
    {
    	if (v === null || v === undefined || v === false || v === 'null') {
    		return (fallback === undefined || fallback === null) ? '' : String(fallback);
    	}
    	var s = String(v);
    	if (s.toLowerCase() === 'null') {
    		return (fallback === undefined || fallback === null) ? '' : String(fallback);
    	}
    	return s;
    }
    function epcCatEscHtml(v)
    {
    	return epcCatStr(v).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    }
    function epcCatEscAttr(v)
    {
    	return epcCatEscHtml(v).replace(/'/g,'&#39;');
    }
    function epcCatDisplayName(obj)
    {
    	var name = epcCatStr(obj && obj.value, '');
    	if (name === '') {
    		name = 'Category #' + epcCatStr(obj && obj.id, '?');
    	}
    	return name;
    }
    function epcSanitizeCatalogueTree(nodes)
    {
    	if (!nodes || !nodes.length) {
    		return;
    	}
    	for (var i = 0; i < nodes.length; i++) {
    		var n = nodes[i];
    		var keys = ['value','alias','url','title_tag','description_tag','keywords_tag','robots_tag','import_format','export_format','image','image_url'];
    		for (var k = 0; k < keys.length; k++) {
    			var key = keys[k];
    			if (n[key] === null || n[key] === undefined || n[key] === false || n[key] === 'null') {
    				n[key] = '';
    			} else {
    				n[key] = String(n[key]);
    				if (n[key].toLowerCase() === 'null') {
    					n[key] = '';
    				}
    			}
    		}
    		if (n.value === '') {
    			n.value = 'Category #' + n.id;
    		}
    		if (n.properties && n.properties.length) {
    			for (var p = 0; p < n.properties.length; p++) {
    				if (n.properties[p].value === null || n.properties[p].value === undefined || n.properties[p].value === 'null') {
    					n.properties[p].value = '';
    				}
    				n.properties[p].value = String(n.properties[p].value || '');
    				if (n.properties[p].value === '' || n.properties[p].value.toLowerCase() === 'null') {
    					n.properties[p].value = 'Property';
    				}
    			}
    		}
    		if (n.data && n.data.length) {
    			epcSanitizeCatalogueTree(n.data);
    		}
    	}
    }

    /*ДЕРЕВО*/
    //Для редактируемости дерева
    webix.protoUI({
        name:"edittree"
    }, webix.EditAbility, webix.ui.tree);
    //Формирование дерева
    tree = new webix.ui({
		
		//Шаблон элемента дерева
    	template:function(obj, common)//Шаблон узла дерева
        	{
                var folder = common.folder(obj, common);
        	    var icon = "";
        	    var label = epcCatEscHtml(epcCatDisplayName(obj));
        	    var value_text = "<span>" + label + "</span>";
				
        	    //Индикация материала, снятого с публикации
        	    var icon_system = "";
				if(obj.published_flag == false)
                {
                    icon_system += "<img src='/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/lock.png' class='col_img' style='float:right; margin:0px 4px 8px 4px;'>";
                    value_text = "<span style=\"color:#AAA\">" + label + "</span>";
                }
				
				
				//Индикация элемента, помеченного на вырезание
				if( typeof obj.to_cut != 'undefined' )
				{
					if( obj.to_cut == true )
					{
						folder = "<i class=\"fas fa-cut\" style='margin:0px 8px 8px 4px;color:#CCC;'></i>";
						value_text = "<span style=\"color:#CCC;\">" + label + "</span>";
					}
				}
				
				
                return common.icon(obj, common) + icon + folder + icon_system + value_text;
        	},//~template
		
		
		
		
        editable:true,//редактируемое
        editValue:"value",
    	editaction:"dblclick",//редактирование по двойному нажатию
        container:"container_A",//id блока div для дерева
        view:"edittree",
    	select:true,//можно выделять элементы
    	drag:true,//можно переносить
    	editor:"text",//тип редактирование - текстовый
    	filterMode:{ showSubItems:true }
    });
	webix.event(window, "resize", function(){ tree.adjust(); });
	(function(){
		var filterInput = document.getElementById('epc_cat_tree_filter');
		if (!filterInput) { return; }
		var filterTimer = null;
		filterInput.addEventListener('input', function(){
			var q = this.value;
			clearTimeout(filterTimer);
			filterTimer = setTimeout(function(){
				if (!q) {
					tree.filter('#value#', '');
					tree.openAll();
					return;
				}
				tree.filter('#value#', q);
			}, 160);
		});
	})();
    /*~ДЕРЕВО*/
    //-----------------------------------------------------
    webix.protoUI({
        name:"editlist" // or "edittree", "dataview-edit" in case you work with them
    }, webix.EditAbility, webix.ui.list);
    //-----------------------------------------------------
    //Событие при выборе элемента дерева
    tree.attachEvent("onAfterSelect", function(id)
    {
    	onSelected();
    });
    //Обработка выбора элемента
    function onSelected()
    {
		//Кнопки Копировать и Вырезать не активны
		activate_copy_cut_buttons(false);
		
		var detailCol = document.getElementById("content_info_div_col");
		var detailBox = document.getElementById("content_info_div");
		
        //Если категории не созданы
    	if(tree.count() == 0)
    	{
    	    detailBox.innerHTML = "<div class=\"epc-ce-empty\">No categories yet. Click <b>Add category</b> to create the first one.</div>";
			if (detailCol) { detailCol.style.display = ""; }
    	    return;
    	}
    	
    	//Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	if(!node_id)
    	{
    	    detailBox.innerHTML = "<div class=\"epc-ce-empty\">Select a category in the tree to view and edit its parameters.</div>";
			if (detailCol) { detailCol.style.display = ""; }
    	    return;
    	}
    	
		//Кнопки Копировать и Вырезать активны
		activate_copy_cut_buttons(true);
		if (detailCol) { detailCol.style.display = ""; }
		
    	var node = tree.getItem(node_id);
    	// Normalize nullish fields on the live node object
    	node.value = epcCatDisplayName(node);
    	node.alias = epcCatStr(node.alias);
    	node.title_tag = epcCatStr(node.title_tag);
    	node.description_tag = epcCatStr(node.description_tag);
    	node.keywords_tag = epcCatStr(node.keywords_tag);
    	node.robots_tag = epcCatStr(node.robots_tag);
    	node.image = epcCatStr(node.image);
    	node.image_url = epcCatStr(node.image_url, epc_cat_no_image);
    	if (!node.image_url) { node.image_url = epc_cat_no_image; }
    	
    	var checked = (node.published_flag == 1) ? " checked=\"checked\" " : "";
    	var parameters_table_html = "";
		parameters_table_html += "<div class=\"epc-ce-section\"><div class=\"epc-ce-section-title\">General</div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\">ID</label><div class=\"col-lg-6\">"+epcCatEscHtml(node.id)+"</div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2277, 'Name'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><strong>"+epcCatEscHtml(node.value)+"</strong> <span class=\"text-muted\">(double-click in tree)</span></div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2800, 'Published'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><input onchange=\"dynamicApplyingCheck('published_flag');\" type=\"checkbox\" id=\"published_flag\" "+checked+" class=\"form-control\"/></div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2166, 'Alias'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('alias');\" id=\"alias\" value=\""+epcCatEscAttr(node.alias)+"\" class=\"form-control\" /></div></div>";
		parameters_table_html += "</div>";

		parameters_table_html += "<div class=\"epc-ce-section\"><div class=\"epc-ce-section-title\">SEO</div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2167, 'Title tag'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><input type=\"text\" onKeyUp=\"dynamicApplying('title_tag');\" id=\"title_tag\" value=\""+epcCatEscAttr(node.title_tag)+"\" class=\"form-control\"/></div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2280, 'Description'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><textarea class=\"form-control\" onKeyUp=\"dynamicApplying('description_tag');\" id=\"description_tag\">"+epcCatEscHtml(node.description_tag)+"</textarea></div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2281, 'Keywords'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><textarea class=\"form-control\" onKeyUp=\"dynamicApplying('keywords_tag');\" id=\"keywords_tag\">"+epcCatEscHtml(node.keywords_tag)+"</textarea></div></div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2282, 'Robots'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><input type=\"text\" id=\"robots_tag\" onKeyUp=\"dynamicApplying('robots_tag');\" value=\""+epcCatEscAttr(node.robots_tag)+"\" class=\"form-control\"/></div></div>";
		parameters_table_html += "</div>";

		parameters_table_html += "<div class=\"epc-ce-section\"><div class=\"epc-ce-section-title\">Image</div>";
		parameters_table_html += "<div class=\"form-group\"><label class=\"col-lg-6 control-label\"><?php echo htmlspecialchars(epc_cat_ed_t(2801, 'Category image'), ENT_QUOTES, 'UTF-8'); ?></label><div class=\"col-lg-6\"><button class=\"btn btn-success\" type=\"button\" onclick=\"document.getElementById('img_"+node.id+"').click();\"><i class=\"fa fa-file\"></i> <span class=\"bold\"><?php echo htmlspecialchars(epc_cat_ed_t(2802, 'Choose image'), ENT_QUOTES, 'UTF-8'); ?></span></button></div></div>";
		parameters_table_html += "<div class=\"col-lg-12 text-center\" id=\"image_div\"></div>";
		parameters_table_html += "</div>";

    	detailBox.innerHTML = parameters_table_html;
    	
    	document.getElementById("image_div").innerHTML = "<img onerror=\"this.src=epc_cat_no_image\" src=\""+epcCatEscAttr(node.image_url)+"\" alt=\"\" />";
    }//function onSelected()
    //-----------------------------------------------------
	//Функция динамическиго применния значений
	function dynamicApplying(attribute)
	{
	    var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
    	
    	var str_value = document.getElementById(attribute).value;
    	
    	var str_handled = str_value.replace(/"/g, "&quot;");
    	
    	node[attribute] = str_handled;
	}
	//-----------------------------------------------------
	//Функция динамического применения значений чекбоксов
	function dynamicApplyingCheck(attribute)
	{
		var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
		
		if(document.getElementById(attribute).checked == true)
		{
			node[attribute] = 1;
		}
		else
		{
			node[attribute] = 0;
		}
		
		tree.refresh();
	}
    //-----------------------------------------------------
    //Обработка изменения файла для выбранной категории
    function onFileChanged()
    {
        //Выделенный узел
    	var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);

        var input_file = document.getElementById("img_"+node_id);//input для файла изображения
        var file = input_file.files[0];//Получаем выбранный файл
        
        if(file == undefined)
        {
            return;
        }
        
        //Запрещаем загружать файлы больше 50 Кб
        if(file.size > 512000)
        {
            input_file.value = null;
            alert("<?php echo translate_str_by_id(2803); ?>");
            return;
        }
        
        //Проверяем тип файла
        if(file.type != "image/jpeg" && file.type != "image/jpg" && file.type != "image/png" && file.type != "image/gif")
        {
            input_file.value = null;
            alert("<?php echo translate_str_by_id(2804); ?>");
            return;
        }
        
        
        //Создаем url файла для его отображения
        node.image_url = URL.createObjectURL(file);
    
        onSelected();
    }
    //-----------------------------------------------------
    //Событие при успешном редактировании элемента дерева
    tree.attachEvent("onValidationSuccess", function(){
        onSelected();
    });
    //-----------------------------------------------------
    tree.attachEvent("onAfterEditStop", function(state, editor, ignoreUpdate){
        //Задаем поле Alias - как транслитерация поля value;
        var node_id = tree.getSelectedId();//ID выделенного узла
    	node = tree.getItem(node_id);//Выделенный узел
        node.alias = iso_9_translit(node.value,  5);//5 - русский текст
        node.alias = node.alias.replace(/\s/g, '-');
        node.alias = node.alias.toLowerCase();
		node.alias = node.alias.replace(/[^\d\sA-Z\-_]/gi, '');//Убираем все символы кроме букв, цифр, тире и нинего подчеркивания
		
        onSelected();
    });
    //-----------------------------------------------------
	//Обработчик После перетаскивания узлов дерева
	tree.attachEvent("onAfterDrop",function(){
	    onSelected();
	});
    //-----------------------------------------------------
    //Добавить новый элемент в дерево
    function add_new_item()
    {
    	//Добавляем элемент в выделенный узел
    	var parentId= tree.getSelectedId();//Выделеный узел
    	var newItemId = 0;
		
		//Проверка, чтобы вставка была не в вырезаемый узел
		if( parentId > 0 )
		{
			var parentItem = tree.getItem(parentId);
			if( typeof parentItem.to_cut != 'undefined' )
			{
				if( parentItem.to_cut )
				{
					alert('<?php echo translate_str_by_id(2805); ?>');
					return;
				}
			}
		}
		
		
		<?php
		$category_class = new DP_CatalogueCategory();
		$new_node_fields_js = '';
		for ($i = 0; $i < count($category_class->translated_items); $i++) {
			$field = $category_class->translated_items[$i];
			$fieldVal = epc_cat_ed_str($category_class->{$field});
			if ($field === 'value' && $fieldVal === '') {
				$fieldVal = 'New category';
			}
			$new_node_fields_js .= $field . ':' . json_encode($fieldVal, JSON_UNESCAPED_UNICODE) . ', ';
			$new_node_fields_js .= $field . '_lang_str_id:' . (int) $category_class->{$field . '_lang_str_id'} . ', ';
		}
		$prop_mfr = json_encode(epc_cat_ed_t(2071, 'Manufacturer'), JSON_UNESCAPED_UNICODE);
		$prop_art = json_encode(epc_cat_ed_t(2070, 'Article'), JSON_UNESCAPED_UNICODE);
		?>
		if( add_base_properties() )
		{
			newItemId = tree.add( {<?php echo $new_node_fields_js; ?>id:next_id, alias:"", url:"", robots_tag:"", import_format:"", export_format:"", image_url:epc_cat_no_image, published_flag:1, properties:[{value:<?php echo $prop_mfr; ?>, category_id:next_id, property_type_id:3, just_created:1, list_id:0, for_similar:0, is_option:0},{value:<?php echo $prop_art; ?>, category_id:next_id, property_type_id:5, just_created:1, list_id:10, for_similar:0, is_option:0}], image:''}, 0, parentId);
		}
		else
		{
			newItemId = tree.add( {<?php echo $new_node_fields_js; ?>id:next_id, alias:"", url:"", robots_tag:"", import_format:"", export_format:"", image_url:epc_cat_no_image, published_flag:1, image:'', properties:[]}, 0, parentId);
		}
    	
    	//Добавляем поле для изображения в форму:
    	var input_file = document.createElement("input");
        input_file.setAttribute("type","file");
        input_file.setAttribute("name","img_"+next_id);
        input_file.setAttribute("id","img_"+next_id);
        input_file.setAttribute("accept","image/jpeg,image/jpg,image/png,image/gif");
        input_file.setAttribute("onchange","onFileChanged();");
        document.getElementById('img_box').appendChild(input_file);
    	
    	onSelected();//Обработка текущего выделения
    	next_id++;//Следующий ID материала
    	tree.open(parentId);//Раскрываем родительский узел
    	
    	/*
    	Принцип работы с изображениями.
    	Категория содержит поле image_url, которое используется исключительно для отображения пиктограммы
    	Кроме этого, в форме сохранения есть блок с элементами input[type=file], которые используются для сохранения изображений на сервере.
    	
    	Если при сохранении, для категории не задано значение в input, то для этой категории измений изображений не происходит.
    	Если значение задано, то происходит сохранение изображения на сервере
    	
    	*/
    }
    //-----------------------------------------------------
    //Удаление выделеного элемента
    function delete_selected_item()
    {
    	var nodeId = tree.getSelectedId();
    	tree.remove(nodeId);
    	onSelected();
    }
    //-----------------------------------------------------
    //Снятие выделения с дерева
    function unselect_tree()
    {
    	tree.unselect();
    	onSelected();
    }
    //-----------------------------------------------------
    //Сохранение перечня категорий
    function save_tree()
    {
        var tree_In_JSON = tree.serialize();//Получаем JSON-представление дерева
        
        //Проверяем отсутствие совпадений атрибутов alias в каждой ветви на одном уровне
        if( ! detectDuplicatedAlias(tree_In_JSON) )
        {
            webix.alert({
                title: "<?php echo translate_str_by_id(2122); ?>",
                text: "<?php echo translate_str_by_id(2807); ?>",
                type:"confirm-error"
            });
            return false;
        }
        
        
        //Проверка пустых значений атрибутов alias
        if( ! detectEmptyAlias(tree_In_JSON))//Передаем JSON представление дерева в рекурсивный метод проверки атрибутов
        {
            webix.alert({
                title: "<?php echo translate_str_by_id(2122); ?>",
                text: "<?php echo translate_str_by_id(2808); ?>",
                type:"confirm-error"
            });
            return false;
        }
    
        
        //webix.message("Ok")
        //return;
        
    
    	//Получаем строку JSON:
    	var tree_json_to_save = tree.serialize();
    	tree_dump = JSON.stringify(tree_json_to_save);
    	
    	//Задаем значение поля в форме:
    	var tree_json_input = document.getElementById("tree_json");
    	tree_json_input.value = tree_dump;
    	
    	document.forms["form_to_save"].submit();//Отправляем
    }
    //-----------------------------------------------------
    //Рекурсивный метод проверки повторяющихся значений атрибута alias
    function detectDuplicatedAlias(level_array)
    {
        for(var i=0; i<level_array.length; i++)
        {
            if(level_array[i]["alias"] == "") continue;//Пустой не интересует - он будет выявлен далее
            
            var node = tree.getItem(level_array[i]["id"]);//Получаем объект узла дерева
            if(isAliasRepeated(node.$parent, node.alias, node.id))
            {
                return false;//Если метод вернул false - дальше проверять нет смысла - выходим
            }
            
            //Рекурсивный вызов для вложенного уровня
            if(level_array[i]['$count'] > 0)
            {
                if(detectDuplicatedAlias(level_array[i]["data"]) == false)
                {
                    return false;//Если метод вернул false - дальше проверять нет смысла - выходим
                }
            }
        }
        
        return true;
    }
    //-----------------------------------------------------
    //Метод проверки существования повторяющихся значений атрибута alias на одном уровне
    //parent_id - родитель уровня; alias - проверяемое значение атрибута; except_node_id - узел, который не должен участвовать в проверке (например, когда сравнивается его собственное значение)
    function isAliasRepeated(parent, alias, except_node_id)
    {
        if(alias == "") return false;//Пустые значения вообще не проверяем
        
        if(parent == 0)//Работаем с узлами верхнего уровня
        {
            var first_id_same_level = tree.getFirstChildId(0);//Получаем Id самого первого узла дерева - он в любом случае на верхнем уровне
            var current_id = 0;//ID текущего проверяемого узла
            while(true)
            {
                //Сначала опрелеляем id текущего проверяемого узла
                if(current_id == 0)//Т.е. первая итерация цикла
                {
                    current_id = first_id_same_level;//Первый узел на уровне (в данном случае - первый узел дерева)
                }
                else
                {
                    current_id = tree.getNextSiblingId(current_id);//Получаем id следующего узла
                    if(current_id == null || current_id == false)//Больше узлов нет
                    {
                        break;
                    }
                }
                if(except_node_id == current_id)//Сам узел - пропускаем
                {
                    continue;
                }
                if(tree.getItem(current_id).$parent != 0)//Это может быть вложенный элемент (т.е. его вернул метод getNextSiblingId()). Он не должен проходить эту проверку, т.к. мы проверяем в данном случае только узлы верхнего уровня
                {
                    continue;
                }
                //Проверяемый узел подлежит проверке значения:
                var current_checked_node = tree.getItem(current_id);//Проверяемый узел
                if(current_checked_node.alias == alias)
                {
                    return true;//АТРИБУТ alias ПОВТОРЯЕТСЯ
                }
            }//~while(true)
        }//~if()
        else//Работаем с вложеженными узлами одного уровня одной ветви
        {
            var node_parent = tree.getItem(parent);//Родительский узел
            var first_id_same_level = tree.getFirstChildId(parent);//Получаем id первого узла на этом уровне в этой ветви
            var current_id = 0;//ID текущего проверяемого узла
            for(var i=0; i<node_parent.$count; i++)
            {
                //Сначала опрелеляем id текущего проверяемого узла
                if(i==0)
                {
                    current_id = first_id_same_level;//Первый узел на уровне
                }
                else
                {
                    current_id = tree.getNextSiblingId(current_id);//Получаем id следующего узла
                }
                if(except_node_id == current_id)//Проверяемый узел
                {
                    continue;
                }
                var current_checked_node = tree.getItem(current_id);//Проверяемый узел
                if(current_checked_node.alias == alias)
                {
                    return true;//АТРИБУТ alias ПОВТОРЯЕТСЯ
                }
            }//~for
        }
        
        return false;//Повторений атрибута не найдено
    }//~function isAliasRepeated(parent, alias, except_node_id = 0)
    //-----------------------------------------------------
    //Рекурсивный метод проверки на 
    function detectEmptyAlias(level_array)
    {
        for(var i=0; i<level_array.length; i++)
        {
            //Проверяем атрибуты данного узла
            //Если здесь выявлено не полное заполнение, то сразу return false;
            //1. Проверяем Alias
            if(level_array[i]["alias"] == "") return false;
            
            //Здесь можно поставить полный URL для данной категории (узла), т.к. она сама и элементы из е ветви всех уровней выше нее прошли проверку
            var node = tree.getItem(level_array[i]["id"]);//Получаем объект узла дерева
            if(node.$level == 1)//Для верхних элементов, их полные url равны их алиасам
            {
                node.url = node.alias;
            }
            else//Для вложенных элементов, их url равны <url родителя>+"/"+<свой алиас>
            {
                node.url = tree.getItem(node.$parent).url + "/" + node.alias;
            }
            
            
            //Рекурсивный вызов для вложенного уровня
            if(level_array[i]['$count'] > 0)
            {
                if(detectEmptyAlias(level_array[i]["data"]) == false)
                {
                    return false;//Если метод вернул false - дальше проверять нет смысла - выходим
                }
            }
        }
        
        return true;
    }//~function detectEmptyAlias(level_array)
    //-----------------------------------------------------
    //Рекурсивный метод инициализации полей image_url для каждой категории после загрузки страницы
    function img_box_start_init(level_array)
    {
        for(var i=0; i<level_array.length; i++)
        {
            //Добавляем input - он пустой в любом случае, даже если изображение было добавлено при последнем редактировании
            var input_file = document.createElement("input");
            input_file.setAttribute("type","file");
            input_file.setAttribute("name","img_"+level_array[i]["id"]);
            input_file.setAttribute("id","img_"+level_array[i]["id"]);
            input_file.setAttribute("accept","image/jpeg,image/jpg,image/png,image/gif");
            input_file.setAttribute("onchange","onFileChanged();");
            input_file.setAttribute("tabindex","-1");
            input_file.setAttribute("aria-hidden","true");
            document.getElementById('img_box').appendChild(input_file);

            var imageName = epcCatStr(level_array[i]["image"]);
            if (imageName) {
            	level_array[i]["image_url"] = epc_cat_image_base + imageName;
            } else {
            	level_array[i]["image_url"] = epc_cat_no_image;
            }
            
            //Рекурсивный вызов для вложенного уровня
            if(level_array[i]['$count'] > 0)
            {
                img_box_start_init(level_array[i]["data"]);
            }
        }
    }//~function img_box_start_init(level_array)
    //-----------------------------------------------------
    
    //Инициализация редактора дерева материалов после загруки страницы
    function catalogue_start_init()
    {
    	var saved_catalogue = <?php echo $catalogue_tree_dump_JSON; ?>;
    	if (!saved_catalogue || typeof saved_catalogue !== 'object') {
    		saved_catalogue = [];
    	}
    	epcSanitizeCatalogueTree(saved_catalogue);
    	img_box_start_init(saved_catalogue);//Инициализируем изображения для категорий
	    tree.parse(saved_catalogue);
	    tree.openAll();
	    tree.adjust();
    }
    catalogue_start_init();
    onSelected();//Обработка текущего выделения
    </script>
    
    
    
    
    
    
	
	
	
	
	<!-- START НОВОЕ МОДАЛЬНОЕ ОКНО -->
	<!-- START НОВОЕ МОДАЛЬНОЕ ОКНО -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalPropertiesOfCategory" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2809); ?></h4>
					</div>
					<div class="modal-body">
						
						<div class="row">
							<div class="col-lg-12">
								<div class="hpanel">
									<div class="panel-heading hbuilt">
										<?php echo translate_str_by_id(2810); ?>
									</div>
									<div class="panel-body text-center float-e-margins">
									
										<button type="button" class="btn w-xs btn-primary2" onclick="add_int_property();"><?php echo translate_str_by_id(2811); ?></button>
									
										<button type="button" class="btn w-xs btn-info" onclick="add_float_property();"><?php echo translate_str_by_id(2004); ?></button>
										
										<button type="button" class="btn w-xs btn-success" onclick="add_text_property();"><?php echo translate_str_by_id(2119); ?></button>
										
										<button type="button" class="btn w-xs btn-warning" onclick="add_bool_property();"><?php echo translate_str_by_id(2812); ?></button>
										
										<button type="button" class="btn w-xs btn-danger" onclick="add_list_property();"><?php echo translate_str_by_id(2782); ?></button>
									
										<button type="button" class="btn w-xs btn-primary" onclick="add_tree_list_property();"><?php echo translate_str_by_id(2813); ?></button>
									
									</div>

								</div>
							</div>
						</div>
						
						<div class="row">
							<div class="col-lg-6">
								<div class="hpanel">
									<div class="panel-heading hbuilt">
										<?php echo translate_str_by_id(2814); ?>
									</div>
									<div class="panel-body">
										<div id="container_B" style="height:150px;"></div>
									</div>
								</div>
							</div>
							
							
							<div class="col-lg-6" id="list_selector_div_container">
								<div class="hpanel">
									<div class="panel-heading hbuilt">
										<?php echo translate_str_by_id(2815); ?>
									</div>
									<div class="panel-body">
										<div id="list_selector_div">
										</div>
									</div>
								</div>
							</div>
						</div>
						
					</div>
					<div class="modal-footer">
						<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2447); ?></button>
						<button type="button" class="btn btn-success" onclick="apply_properties(0);"><?php echo translate_str_by_id(2189); ?></button>
						<button type="button" class="btn btn-success" onclick="apply_properties(1);"><?php echo translate_str_by_id(2816); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<!-- END НОВОЕ МОДАЛЬНОЕ ОКНО -->
	<!-- END НОВОЕ МОДАЛЬНОЕ ОКНО -->
	
	
	

    <script>
		var is_changes = false;//Флаг - при работе со свойствами катагории - есть несохраненные изменения
		
		//Событие при закрытии окна
		$('#modalPropertiesOfCategory').on('hide.bs.modal',function(){
			if(is_changes)
			{
				if( confirm("<?php echo translate_str_by_id(2817); ?>")  )
				{
					return true;
				}
				else
				{
					return false;
				}
			}
			
			return true;
		});
		
		//После отображения окна - подгоняем дерево под размер
		$('#modalPropertiesOfCategory').on('shown.bs.modal',function(){
			properties_tree.adjust();
		});
        // ----------------------------------------------------------------
        var properties_tree = "";//Переменная для дерева свойств
        // ----------------------------------------------------------------
        //Открыть модальное окно для настроек свойств категории
        function open_properties_window()
        {
			is_changes = false;//Окно только открываем - изменений свойств еще нет
			
            //Выделенный узел
        	var node_id = tree.getSelectedId();//ID выделенного узла
        	if(node_id == 0)
        	{
        	    alert("<?php echo translate_str_by_id(2818); ?>");
        	    return;
        	}
        	
        	var node = "";//Ссылка на объект узла
        	//Выделенный узел
        	node = tree.getItem(node_id);
        	
            if(node.$count > 0)
            {
                alert("<?php echo translate_str_by_id(2819); ?>");
        	    return;
            }
            
            //Предварительно очищаем окно
            var container_B = document.getElementById("container_B");
            container_B.innerHTML = "";
            
            $('#modalPropertiesOfCategory').modal();//ОТКРЫВАЕМ ОКНО
            
        	/**Инициализируем дерево со свойствами категории*/
        	//Для редактируемости дерева
            webix.protoUI({
                name:"edittree"
            }, webix.EditAbility, webix.ui.tree);
            //Формирование дерева
            properties_tree = new webix.ui({
				
				//Шаблон элемента дерева
				template:function(obj, common)//Шаблон узла дерева
					{
						
						var folder = common.folder(obj, common);
						var icon = "";
						var value_text = "<span>" + obj.value + "</span>";//Вывод текста
						
						
						//В зависимости от типа свойства - обозначаем
						switch( parseInt(obj.property_type_id) )
						{
							case 1:
								value_text = "<span>" + obj.value + " <b>(<?php echo translate_str_by_id(2811); ?>)</b></span>";//Вывод текста
								break;
							case 2:
								value_text = "<span>" + obj.value + " <b>(<?php echo translate_str_by_id(2004); ?>)</b></span>";//Вывод текста
								break;
							case 3:
								value_text = "<span>" + obj.value + " <b>(<?php echo translate_str_by_id(2119); ?>)</b></span>";//Вывод текста
								break;
							case 4:
								value_text = "<span>" + obj.value + " <b>(<?php echo translate_str_by_id(2812); ?>)</b></span>";//Вывод текста
								break;
							case 5:
								value_text = "<span>" + obj.value + " <b>(<?php echo translate_str_by_id(2782); ?>)</b></span>";//Вывод текста
								break;
							case 6:
								value_text = "<span>" + obj.value + " <b>(<?php echo translate_str_by_id(2813); ?>)</b></span>";//Вывод текста
								break;
						}
						
						return common.icon(obj, common) + icon + folder + value_text;
						
					},//~template
				
				
				
                editable:true,//редактируемое
                editValue:"value",
            	editaction:"dblclick",//редактирование по двойному нажатию
                container:"container_B",//id блока div для дерева
                view:"edittree",
            	select:true,//можно выделять элементы
            	drag:true,//можно переносить
            	editor:"text",//тип редактирование - текстовый
            });
			webix.event(window, "resize", function(){ properties_tree.adjust(); });
            /*~ДЕРЕВО*/
            //-----------------------------------------------------
            webix.protoUI({
                name:"editlist" // or "edittree", "dataview-edit" in case you work with them
            }, webix.EditAbility, webix.ui.list);

    	    properties_tree.parse(node.properties);
    	    properties_tree.openAll();
    	    //-----------------------------------------------------
            //Событие при выборе элемента дерева
            properties_tree.attachEvent("onAfterSelect", function(id)
            {
            	onSelected_properties_tree();
            });
            onSelected_properties_tree();//Обрабатываем текущее выделение (его отсутствие)
        }
        // ----------------------------------------------------------------
        //Обработка выделения свойства
        function onSelected_properties_tree()
        {
            var list_selector_div = document.getElementById("list_selector_div");//Селектор
            
            var property_node_id = properties_tree.getSelectedId();//ID выделенного узла
            if(property_node_id == 0)
            {
                list_selector_div.innerHTML = "";
				document.getElementById("list_selector_div_container").setAttribute("style", "display:none;");
				return;
            }
            
			document.getElementById("list_selector_div_container").setAttribute("style", "display:block;");
			
            var property_node = "";//Ссылка на объект узла
        	//Выделенный узел
        	property_node = properties_tree.getItem(property_node_id);
            
			
			//Обозначение свойства справа (название и тип)
			var property_info_text = "";
			
			property_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2277); ?></label><div class=\"col-lg-6\">"+property_node.value+"</div></div>";
			
			property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			var for_similar_current_state = "";
			if(property_node.for_similar == 1)
			{
				for_similar_current_state = "checked";
			}
			property_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2820); ?></label><div class=\"col-lg-6\"><input onchange=\"setForSimilar();\" type=\"checkbox\" class=\"form-control\" id=\"for_similar_input\" "+for_similar_current_state+" /></div></div>";
			
			
			
			
			property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			var is_option_current_state = "";
			if(property_node.is_option == 1)
			{
				is_option_current_state = "checked";
			}
			property_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2821); ?></label><div class=\"col-lg-6\"><input onchange=\"setIsOption();\" type=\"checkbox\" class=\"form-control\" id=\"is_option_input\" "+is_option_current_state+" /></div></div>";
			
			
			
			
			property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
			
			property_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2238); ?></label><div class=\"col-lg-6\">";
			
			switch( parseInt(property_node.property_type_id) )
			{
				case 1:
					property_info_text += "<?php echo translate_str_by_id(2811); ?>";
					break;
				case 2:
					property_info_text += "<?php echo translate_str_by_id(2004); ?>";
					break;
				case 3:
					property_info_text += "<?php echo translate_str_by_id(2822); ?>";
					break;
				case 4:
					property_info_text += "<?php echo translate_str_by_id(2812); ?>";
					break;
				case 5:
					property_info_text += "<?php echo translate_str_by_id(2782); ?>";
					break;
				case 6:
					property_info_text += "<?php echo translate_str_by_id(2813); ?>";
					break;
			}
			property_info_text += "</div></div>";
			

            //Если это свойство - линейный список
            if(property_node.property_type_id == 5)
            {
				property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
				
				property_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2782); ?></label><div class=\"col-lg-6\">";
				
                property_info_text += "<select class=\"form-control\" id=\"list_selector\" onchange=\"setListId();\">";
                property_info_text += "<option value=\"0\"><?php echo translate_str_by_id(2823); ?></option>";
                <?php
				$line_lists_query = $db_link->prepare("SELECT * FROM `shop_line_lists`");
				$line_lists_query->execute();
                while( $line_list_record = $line_lists_query->fetch() )
                {
                    ?>
                    property_info_text += "<option value=\"<?php echo $line_list_record["id"]; ?>\"><?php echo str_replace('"', '\"',translate_str_by_id($line_list_record["caption"])); ?></option>";
                    <?php
                }
                ?>
                property_info_text += "</select></div></div>";
				
				//Кнопка Удалить свойство
				property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
				property_info_text += "<div class=\"col-lg-12 text-center\"><button type=\"button\" class=\"btn w-xs btn-danger2\" onclick=\"delete_property();\"><?php echo translate_str_by_id(2824); ?></button></div>";
				
				
                list_selector_div.innerHTML = property_info_text;//Показали заполненный селектор
                
                //Выбираем текущее значение:
                document.getElementById("list_selector").value = property_node.list_id;
            }
			else if(property_node.property_type_id == 6)//Если это древовидный список
            {
				property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
				
				property_info_text += "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2813); ?></label><div class=\"col-lg-6\">";
				
                property_info_text += "<select class=\"form-control\" id=\"list_selector\" onchange=\"setListId();\">";
                property_info_text += "<option value=\"0\"><?php echo translate_str_by_id(2823); ?></option>";
                <?php
				$tree_lists_query = $db_link->prepare("SELECT * FROM `shop_tree_lists`");
				$tree_lists_query->execute();
                while( $tree_list_record = $tree_lists_query->fetch() )
                {
                    ?>
                    property_info_text += "<option value=\"<?php echo $tree_list_record["id"]; ?>\"><?php echo str_replace('"', '\"',translate_str_by_id($tree_list_record["caption"])); ?></option>";
                    <?php
                }
                ?>
                property_info_text += "</select></div></div>";
				
				//Кнопка Удалить свойство
				property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
				property_info_text += "<div class=\"col-lg-12 text-center\"><button type=\"button\" class=\"btn w-xs btn-danger2\" onclick=\"delete_property();\"><?php echo translate_str_by_id(2824); ?></button></div>";
				
				
                list_selector_div.innerHTML = property_info_text;//Показали заполненный селектор
                
                //Выбираем текущее значение:
                document.getElementById("list_selector").value = property_node.list_id;
            }
            else
            {
				//Кнопка Удалить свойство
				property_info_text += "<div class=\"hr-line-dashed col-lg-12\"></div>";//РАЗДЕЛИТЕЛЬ-----
				property_info_text += "<div class=\"col-lg-12 text-center\"><button type=\"button\" class=\"btn w-xs btn-danger2\" onclick=\"delete_property();\"><?php echo translate_str_by_id(2824); ?></button></div>";
				
                list_selector_div.innerHTML = property_info_text;
            }
        }
        // ----------------------------------------------------------------
        //Функция добавления свойства "Целое число"
        function add_int_property()
        {
            var node_id = tree.getSelectedId();//ID выделенного узла (ID категории)
        	properties_tree.add( {value:"<?php echo translate_str_by_id(2811); ?>", category_id:node_id, property_type_id:1, just_created:1, list_id:0, for_similar:0, is_option:0}, properties_tree.count(), 0);//Добавляем новый узел и запоминаем его ID
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
        //Функция добавления свойства "Число с точкой"
        function add_float_property()
        {
            var node_id = tree.getSelectedId();//ID выделенного узла (ID категории)
        	properties_tree.add( {value:"<?php echo translate_str_by_id(2004); ?>", category_id:node_id, property_type_id:2, just_created:1, list_id:0, for_similar:0, is_option:0}, properties_tree.count(), 0);//Добавляем новый узел и запоминаем его ID
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
        //Функция добавления свойства "Строка"
        function add_text_property()
        {
            var node_id = tree.getSelectedId();//ID выделенного узла (ID категории)
        	properties_tree.add( {value:"<?php echo translate_str_by_id(2006); ?>", category_id:node_id, property_type_id:3, just_created:1, list_id:0, for_similar:0, is_option:0}, properties_tree.count(), 0);//Добавляем новый узел и запоминаем его ID
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
        //Функция добавления свойства "Флаг"
        function add_bool_property()
        {
            var node_id = tree.getSelectedId();//ID выделенного узла (ID категории)
        	properties_tree.add( {value:"<?php echo translate_str_by_id(2008); ?>", category_id:node_id, property_type_id:4, just_created:1, list_id:0, for_similar:0, is_option:0}, properties_tree.count(), 0);//Добавляем новый узел и запоминаем его ID
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
        //Функция добавления свойства "Линейный список"
        function add_list_property()
        {
            var node_id = tree.getSelectedId();//ID выделенного узла (ID категории)
        	properties_tree.add( {value:"<?php echo translate_str_by_id(2782); ?>", category_id:node_id, property_type_id:5, just_created:1, list_id:0, for_similar:0, is_option:0}, properties_tree.count(), 0);//Добавляем новый узел и запоминаем его ID
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
		// ----------------------------------------------------------------
        //Функция добавления свойства "Древовидный список"
        function add_tree_list_property()
        {
            var node_id = tree.getSelectedId();//ID выделенного узла (ID категории)
        	properties_tree.add( {value:"<?php echo translate_str_by_id(2813); ?>", category_id:node_id, property_type_id:6, just_created:1, list_id:0, for_similar:0, is_option:0}, properties_tree.count(), 0);//Добавляем новый узел и запоминаем его ID
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
        //Удаление свойства
        function delete_property()
        {
            var property_nodeId = properties_tree.getSelectedId();
        	properties_tree.remove(property_nodeId);
        	onSelected_properties_tree();
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
        //Запомнить list_id при переключении селектора
        function setListId()
        {
            var property_node_id = properties_tree.getSelectedId();//ID выделенного узла (ID категории)
            
            var property_node = "";//Ссылка на объект узла
        	//Выделенный узел
        	property_node = properties_tree.getItem(property_node_id);
        	
        	property_node.list_id = document.getElementById("list_selector").value;
			
			is_changes = true;//Ставим флаг - Есть изменения
        }
        // ----------------------------------------------------------------
		//Запомнить настройку for_similar для свойства (Учитывать для похожих товаров)
		function setForSimilar()
		{
			var property_node_id = properties_tree.getSelectedId();//ID выделенного узла (ID категории)
			
			var property_node = "";//Ссылка на объект узла
        	//Выделенный узел
        	property_node = properties_tree.getItem(property_node_id);
			
			if(document.getElementById("for_similar_input").checked == true)
			{
				property_node.for_similar = 1;
			}
			else
			{
				property_node.for_similar = 0;
			}
			
			
			is_changes = true;//Ставим флаг - Есть изменения
		}
		// ----------------------------------------------------------------
		//Запомнить настройку is_option для свойства (Флаг - является вариантом исполнения)
		function setIsOption()
		{
			var property_node_id = properties_tree.getSelectedId();//ID выделенного узла (ID категории)
			
			var property_node = "";//Ссылка на объект узла
        	//Выделенный узел
        	property_node = properties_tree.getItem(property_node_id);
			
			if(document.getElementById("is_option_input").checked == true)
			{
				property_node.is_option = 1;
			}
			else
			{
				property_node.is_option = 0;
			}
			
			
			is_changes = true;//Ставим флаг - Есть изменения
		}
		// ----------------------------------------------------------------
        //Применить настройки свойств для категории
        function apply_properties(close_after)
        {
            //Выделенный узел
        	var node_id = tree.getSelectedId();//ID выделенного узла
        	var node = "";//Ссылка на объект узла
        	//Выделенный узел
        	node = tree.getItem(node_id);
        	
        	//Дамп дерева свойств для категории:
        	var properties_json = properties_tree.serialize();//Получаем JSON-представление дерева
        	
			if(properties_json.length > 0){
				for (let property in properties_json) {
					if((properties_json[property]['property_type_id'] == '5' || properties_json[property]['property_type_id'] == '6') && properties_json[property]['list_id'] == '0'){
						alert('<?php echo translate_str_by_id(2825); ?> '+properties_json[property]['value']+' <?php echo translate_str_by_id(2826); ?>.');
						return false;
					}
				}
			}
			
        	node.properties = properties_json;//Сохраняем дерево свойств в объект узла категории
        	
			is_changes = false;//Ставим флаг - Нет изменений, т.к. мы только что их сохранили
			
			if(close_after)
			{
				$('#modalPropertiesOfCategory').modal('hide');
			}
        }
        // ----------------------------------------------------------------
    </script>
    <!--Start Модальное окно: Настройка свойств категории -->
    
	
	
	
	
	
	
	
	
	
	
	
	
	
	<!-- START МОДАЛЬНОЕ ОКНО - Шаблоны категорий -->
	<div class="text-center m-b-md">
		<div class="modal fade" id="modalCategoriesTemplates" tabindex="-1" role="dialog"  aria-hidden="true">
			<div class="modal-dialog modal-lg">
				<div class="modal-content">
					<div class="color-line"></div>
					<div class="modal-header">
						<h4 class="modal-title"><?php echo translate_str_by_id(2827); ?></h4>
					</div>
					<div class="modal-body" id="modalCategoriesTemplates_workArea">
						
						<?php echo translate_str_by_id(2828); ?>
						
					</div>
					<div class="modal-footer">
						<button id="templates_window_close_button" type="button" class="btn btn-primary" data-dismiss="modal"><i class="fas fa-times"></i> <?php echo translate_str_by_id(2447); ?></button>
						<button type="button" id="delete_template_button" class="btn btn-danger" onclick="delete_category_template();"><i class="far fa-trash-alt"></i> <?php echo translate_str_by_id(2224); ?></button>
						<button type="button" id="copy_template_button" class="btn btn-success" onclick="template_to_buffer();"><i class="far fa-copy"></i> <?php echo translate_str_by_id(2795); ?></button>
					</div>
				</div>
			</div>
		</div>
	</div>
	<!-- Здесь хранится пустой файловый инпут шаблона, добавляемого в буфер для копирования -->
	<div style="display:none;" id="template_input_container">
	</div>
	<script>
	var backend_dir = '<?php echo $DP_Config->backend_dir; ?>';
	var csrf_guard_key = '<?php echo $user_session["csrf_guard_key"]; ?>';
	</script>
	<script src="/<?php echo $DP_Config->backend_dir; ?>/content/shop/catalogue/categories_templates/categories_templates.js.php"></script>
	<!-- END МОДАЛЬНОЕ ОКНО - Шаблоны категорий -->

    <?php
	if (function_exists('epc_cp_page_frame_close')) {
		epc_cp_page_frame_close();
	}
}//~else//Если действий нет - выводим страницу
?>