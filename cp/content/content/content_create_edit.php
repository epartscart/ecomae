<?php
//Страничный скрипт создания и редактирования одного материала - для схемы работы "No tree"
defined('_ASTEXE_') or die('No access');

//Режим редактирования Фронтэнд/Бэкэнд
$edit_mode = null;
if( isset($_COOKIE["edit_mode"]) )
{
	$edit_mode = $_COOKIE["edit_mode"];
}
switch($edit_mode)
{
    case "frontend":
        $is_frontend = 1;
        break;
    case "backend":
        $is_frontend = 0;
        break;
    default:
        $is_frontend = 1;
        break;
}


//Мультиязычность
$translated_items = array('value', 'description', 'content', 'title_tag', 'description_tag', 'keywords_tag', 'author_tag', );
/*
Особая обработка при сохранении:
content (другое имя целевой переменной - $content_content; без обработки через htmlentities; кастомный алгоритм применяется только для content_type == text)
*/
?>

<?php
//Рекурсивная функция обновления вложенных узлов
function handle_child_nodes($content_id)
{
	global $db_link;
	
	//Получаем нужные поля данного узла
	$content_data_query = $db_link->prepare("SELECT `url`, `level`, `count` FROM `content` WHERE `id` = ?;");
	if( ! $content_data_query->execute( array($content_id) ) )
	{
		return false;
	}
	$content_data_record = $content_data_query->fetch();
	if( $content_data_record == false )
	{
		return false;
	}
	
	
	$url = $content_data_record["url"];
	$level = $content_data_record["level"];
	$count = $content_data_record["count"];
	
	if( $count == 0 )
	{
		return true;
	}
	
	
	//Получаем список вложенных узлов
	$child_nodes_query = $db_link->prepare("SELECT `id`,`alias`,`count` FROM `content` WHERE `parent` = ?;");
	if( ! $child_nodes_query->execute( array($content_id) ) )
	{
		return false;
	}
	while( $child_node = $child_nodes_query->fetch() )
	{
		$child_alias = $child_node["alias"];
		$child_id = $child_node["id"];
		$child_count = $child_node["count"];
		
		//Сначала меняем level и url
		if( ! $db_link->prepare("UPDATE `content` SET `level` = ?+1, `url` = ? WHERE `id` = ?;")->execute( array($level, $url."/".$child_alias, $child_id  ) ) )
		{
			return false;
		}
		
		//Рекурсивный вызов для вложенных узлов
		if( $child_count > 0 )
		{
			//Если была ошибка хотя бы на одном узле - возращаем false
			if( ! handle_child_nodes($child_id) )
			{
				return false;
			}
		}
	}
	
	return true;
}



if( !empty($_POST["action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	$time = time();
	$content = json_decode($_POST["content_object"], true);
	
	
	//Обработка корректности json_decode($_POST["content_object"], true)
	$content_fields_names = array("content_id", "alias", "value", "parent", "description", "is_frontend", "content_type", "content", "title_tag", "description_tag", "keywords_tag", "author_tag", "main_flag", "css_js", "robots_tag", "published_flag", "groups_access", "check_hash");
	for( $i=0; $i < count($content_fields_names) ; $i++)
	{
		if( ! isset($content[$content_fields_names[$i]]) )
		{
			?>
			<script>
			alert('<?php echo translate_str_by_id(2125); ?>');
			
			
			location = "/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo urlencode(translate_str_by_id(2126)); ?>";
			</script>
			<?php
			exit;
		}
	}
	
	//Проверка хеша (content_id, is_frontend)
	if( md5( $content["content_id"].$content["is_frontend"].$DP_Config->secret_succession ) != $content["check_hash"] )
	{
		?>
		<script>
		alert('<?php echo translate_str_by_id(2127); ?>');
		
		
		location = "/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo urlencode(translate_str_by_id(2128)); ?>";
		</script>
		<?php
		exit;
	}
	
	
	
	//Защита от изменения режима редактирования (если, к примеру, пользователь переключил режим на другой странице, открытой параллельно)
	if( $is_frontend != $content["is_frontend"] )
	{
		$message_str_id = 2129;
		if( !$is_frontend )
		{
			$message_str_id = 2130;
		}
		
		?>
		<script>
		alert('<?php echo translate_str_by_id($message_str_id); ?>');
		
		
		location = "/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo urlencode(translate_str_by_id(2131)); ?>";
		</script>
		<?php
		exit;
	}
	
	
	if($content["content_id"] == 0)//Создание материала
	{
		//Формируем переменные для SQL-запроса INSERT
		$count = 0;
		$url = $content["alias"];//!!! ДАЛЕЕ ЗАВИСИТ ОТ PARENT
		$level = 1;//!!! ДАЛЕЕ ЗАВИСИТ ОТ PARENT
		$alias = $content["alias"];
		$value = htmlentities($content["value"], ENT_QUOTES, "UTF-8", false);
		$parent = $content["parent"];
		$description = $content["description"];
		$is_frontend = $content["is_frontend"];
		$content_type = $content["content_type"];
		$content_content = $content["content"];
		$title_tag = htmlentities($content["title_tag"], ENT_QUOTES, "UTF-8", false);
		$description_tag = htmlentities($content["description_tag"], ENT_QUOTES, "UTF-8", false);
		$keywords_tag = htmlentities($content["keywords_tag"], ENT_QUOTES, "UTF-8", false);
		$author_tag = htmlentities($content["author_tag"], ENT_QUOTES, "UTF-8", false);
		$main_flag = $content["main_flag"];
		$modules_array = "[]";
		$css_js = $content["css_js"];
		$robots_tag = htmlentities($content["robots_tag"], ENT_QUOTES, "UTF-8", false);
		$system_flag = 0;
		$published_flag = $content["published_flag"];
		$open = 0;
		$time_created = $time;
		//$time_edited = $time;
		$order = 1;
		
		
		if( $content_type != 'php' && $content_type != 'text' )
		{
			exit;
		}
		
		
		//Не допускаем вставку php-кода при текстовом типе контента
		if( strtolower($content_type) == 'text' )
		{
			do
			{
				$content_content = str_replace('<?', '[CODE]', $content_content);
			}while( strpos($content_content, '<?') !== false );
			
			
			do
			{
				$content_content = str_replace('?>', '[/CODE]', $content_content);
			}while( strpos($content_content, '?>') !== false );
		}
		
		
		
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception( translate_str_by_id(2132) );
			}
			
			if($parent > 0)//Создаваемый материал - вложен, значит нужно сформировать поля в зависимости от родительского узла
			{
				//Получаем данные родительского узла
				$parent_query = $db_link->prepare('SELECT `level`,`url`, `is_frontend` FROM `content` WHERE `id` = ?;');
				if( $parent_query->execute( array($parent) ) == false )
				{
					//SQL-ошибка получения данных родительского узла
					throw new Exception(translate_str_by_id(2133));
				}
				$parent_record = $parent_query->fetch();
				
				if( $parent_record == false )
				{
					//Ошибка определения данных родительского узла
					throw new Exception(translate_str_by_id(2134));
				}
				
				$parent_level = $parent_record["level"];
				$parent_url = $parent_record["url"];
				$parent_is_frontend = $parent_record["is_frontend"];
				
				if( $is_frontend != $parent_is_frontend )
				{
					throw new Exception(translate_str_by_id(2135));
				}
				
				
				//Изменяем данные материала для INSERT
				$level = $level + $parent_level;
				$url = $parent_url."/".$url;
			}
			
			
			
			//Если тип содержимого - php-скрипт
			if( strtolower($content_type) == 'php' )
			{
				//Получаем расширение файла
				$file_extension = explode(".", $content_content);
				$file_extension = $file_extension[count($file_extension)-1];
				
				//Проверка расширения
				$file_extension = strtolower($file_extension);
				if( $file_extension != 'php' )
				{
					throw new Exception(translate_str_by_id(2136));
				}
			}
			
			
			
			
			//Мультиязычность. Кастомный алгоритм. Сами переменные инициализированы выше, как в русской версии - без изменений
			for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
			{
				//Имя переменной, с которой сейчас работаем:
				$var_name = $translated_items[$i];
				//Отдельная обработка для поля content (другое имя переменной)
				if( $var_name == 'content' )
				{
					$var_name = 'content_content';
				}
				//Если тип материала - php, то, кастомный алгоритм не применяется (значение не мультиязычно)
				if( $var_name == 'content_content' && $content_type == 'php' )
				{
					continue;
				}
				
				
				${$var_name} = save_custom_translation( $content[$var_name."_lang_str_id"] , ${$var_name});
				if( ${$var_name} == 0 )
				{
					throw new Exception( 'Error executing custom algorithm' );
				}
			}
			
			
			
			
			//Добавляем сам материал
			$SQL_INSERT = "INSERT INTO `content` (`count`,`url`,`level`,`alias`,`value`,`parent`,`description`,`is_frontend`,`content_type`,`content`,`title_tag`,`description_tag`,`keywords_tag`,`author_tag`,`main_flag`,`modules_array`,`css_js`,`robots_tag`,`system_flag`,`published_flag`,`open`,`time_created`,`order`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?);";
			
			$binding_values = array($count, $url, $level, $alias, $value, $parent, $description, $is_frontend, $content_type, $content_content, $title_tag, $description_tag, $keywords_tag, $author_tag, $main_flag, $modules_array, $css_js, $robots_tag, $system_flag, $published_flag, $open, $time_created, $order);

			if( ! $db_link->prepare($SQL_INSERT)->execute($binding_values) )
			{
				throw new Exception(translate_str_by_id(2137));
			}
			//Материал добавлен - получаем его id
			$content_id = (int)$db_link->lastInsertId();
			
			if( $content_id == 0 )
			{
				throw new Exception(translate_str_by_id(2138));
			}
			
			//Создаваемый материал - вложен, значит нужно обработать родительский узел - добавить count
			if($parent > 0)
			{
				if( ! $db_link->prepare("UPDATE `content` SET `count` = `count`+1 WHERE `id` = ?;")->execute( array($parent) ) )
				{
					throw new Exception(translate_str_by_id(2139));
				}
			}
			
			//Ставим main_flag = 0 для другого материала, если этот указали главным
			if( $main_flag == 1 )
			{
				if( ! $db_link->prepare('UPDATE `content` SET `main_flag` = 0 WHERE `main_flag` = 1 AND `id` != ? AND `is_frontend` = ?;')->execute( array($content_id, $is_frontend) ) )
				{
					throw new Exception(translate_str_by_id(2140));
				}
			}
			
			//Добавляем права доступа
			$groups_access = json_decode($content["groups_access"], true);
			if( count($groups_access) > 0 )
			{
				$binding_values = array();
				
				$SQL_content_access = "INSERT INTO `content_access` (`content_id`, `group_id`) VALUES ";
				for($i=0; $i < count($groups_access); $i++)
				{
					if($i > 0)
					{
						$SQL_content_access .= ",";
					}
					$SQL_content_access .= " (?, ?) ";
					
					array_push($binding_values, $content_id);
					array_push($binding_values, $groups_access[$i]);
				}
				if( ! $db_link->prepare($SQL_content_access)->execute( $binding_values ) )
				{
					throw new Exception(translate_str_by_id(2141));
				}
			}
		}
		catch (Exception $e)
		{
			$db_link->rollBack();//Откатываем все изменения
			
			//Ошибка
			$error_message = urlencode($e->getMessage().". ".translate_str_by_id(2142));
			?>
			<script>
				location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager/content?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
		
		//Дошли сюда - значит все запросы выполнены без ошибок
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		//Выполнено успешно
		$success_message = urlencode(translate_str_by_id(2143));
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager/content?content_id=<?php echo $content_id; ?>&success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
	else//Редактирование материала
	{
		//Формируем переменные для SQL-запроса UPDATE
		$content_id = (int)$content["content_id"];
		//$count = 0;//НЕ МЕНЯЕТСЯ
		$url = $content["alias"];//!!! ДАЛЕЕ ЗАВИСИТ ОТ НОВОГО PARENT
		$level = 1;//!!! ДАЛЕЕ ЗАВИСИТ ОТ НОВОГО PARENT
		$alias = $content["alias"];
		$value = htmlentities($content["value"]);
		$parent = $content["parent"];
		$description = $content["description"];
		$is_frontend = $content["is_frontend"];//НЕ МЕНЯЕТСЯ
		$content_type = $content["content_type"];
		$content_content = $content["content"];
		$title_tag = htmlentities($content["title_tag"]);
		$description_tag = htmlentities($content["description_tag"]);
		$keywords_tag = htmlentities($content["keywords_tag"]);
		$author_tag = htmlentities($content["author_tag"]);
		$main_flag = $content["main_flag"];
		//$modules_array = "";//НЕ МЕНЯЕТСЯ
		$css_js = $content["css_js"];
		$robots_tag = htmlentities($content["robots_tag"]);
		//$system_flag = 0;//НЕ МЕНЯЕТСЯ
		$published_flag = $content["published_flag"];
		//$open = 0;//НЕ МЕНЯЕТСЯ
		//$time_created = $time;//НЕ МЕНЯЕТСЯ
		$time_edited = $time;
		//$order = 1;//НЕ МЕНЯЕТСЯ
		
		
		if( $content_type != 'php' && $content_type != 'text' )
		{
			exit;
		}
		
		
		//Не допускаем вставку php-кода при текстовом типе контента
		if( strtolower($content_type) == 'text' )
		{
			do
			{
				$content_content = str_replace('<?', '[CODE]', $content_content);
			}while( strpos($content_content, '<?') !== false );
			
			
			do
			{
				$content_content = str_replace('?>', '[/CODE]', $content_content);
			}while( strpos($content_content, '?>') !== false );
		}
		
		
		
		//Все действия с БД выполняем с помощью транзакции
		try
		{
			//Если тип содержимого - php-скрипт
			if( strtolower($content_type) == 'php' )
			{
				//Получаем расширение файла
				$file_extension = explode(".", $content_content);
				$file_extension = $file_extension[count($file_extension)-1];
				
				//Проверка расширения
				$file_extension = strtolower($file_extension);
				if( $file_extension != 'php' )
				{
					throw new Exception(translate_str_by_id(2136));
				}
			}
			
			
			
			if( ! $db_link->beginTransaction() )//Старт транзакции
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			//Получаем текущие данные узла
			$current_query = $db_link->prepare('SELECT `parent`, `main_flag`, `system_flag`, `is_frontend` FROM `content` WHERE `id` = ?;');
			if( ! $current_query->execute( array($content_id) ) )
			{
				throw new Exception(translate_str_by_id(2144));
			}
			$current_record = $current_query->fetch();
			
			if( $current_record == false )
			{
				throw new Exception(translate_str_by_id(2145));
			}
			
			$current_parent = $current_record["parent"];
			$current_main_flag = $current_record["main_flag"];
			$system_flag = $current_record["system_flag"];
			$current_is_frontend = $current_record["is_frontend"];
			
			if( $current_is_frontend != $is_frontend )
			{
				throw new Exception(translate_str_by_id(2146));
			}
			
			
			//Защита от изменения и удаления системных материалов
			if( $DP_Config->allow_edit_system_content != true )
			{
				if( $system_flag )
				{
					throw new Exception(translate_str_by_id(2147));
				}
			}
			
			
			//Получаем данные родительского узла, которые влияют на поля редактируемого материала
			if($parent > 0)
			{
				//Получаем данные родительского узла
				$parent_query = $db_link->prepare( 'SELECT `level`,`url`,`is_frontend` FROM `content` WHERE `id` = ?;' );
				if($parent_query->execute( array($parent) ) == false)
				{
					throw new Exception(translate_str_by_id(2148));
				}
				$parent_record = $parent_query->fetch();
				
				if( $parent_record == false )
				{
					throw new Exception(translate_str_by_id(2149));
				}
				
				$parent_level = $parent_record["level"];
				$parent_url = $parent_record["url"];
				$parent_is_frontend = $parent_record["is_frontend"];
				
				
				if( $is_frontend != $parent_is_frontend )
				{
					throw new Exception(translate_str_by_id(2150));
				}
				
				
				//Изменяем данные материала для UPDATE
				$level = $level + $parent_level;
				$url = $parent_url."/".$url;
			}
			

			//Мультиязычность. Кастомный алгоритм. Сами переменные инициализированы выше, как в русской версии - без изменений
			for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
			{
				//Имя переменной, с которой сейчас работаем:
				$var_name = $translated_items[$i];
				//Отдельная обработка для поля content (другое имя переменной)
				if( $var_name == 'content' )
				{
					$var_name = 'content_content';
				}
				//Если тип материала - php, то, кастомный алгоритм не применяется (значение не мультиязычно)
				if( $var_name == 'content_content' && $content_type == 'php' )
				{
					continue;
				}
				
				
				${$var_name} = save_custom_translation( $content[$var_name."_lang_str_id"], ${$var_name});
				if( ${$var_name} == 0 )
				{
					throw new Exception( 'Error executing custom algorithm' );
				}
			}
			
			//Обновляем данные материала
			if( ! $db_link->prepare("UPDATE `content` SET `url` = ?, `level` = ?, `alias` = ?, `value` = ?, `parent` = ?, `description` = ?, `content_type` = ?, `content` = ?, `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?, `author_tag` = ?, `main_flag` = ?, `css_js` = ?, `robots_tag` = ?, `published_flag` = ?, `time_edited` = ? WHERE `id` = ?;")->execute( array($url, $level, $alias, $value, $parent, $description, $content_type, $content_content, $title_tag, $description_tag, $keywords_tag, $author_tag, $main_flag, $css_js, $robots_tag, $published_flag, $time_edited, $content_id) ) )
			{
				throw new Exception(translate_str_by_id(2151));
			}
			
			
			//Обрабатываем поля count для родительских узлов
			if( $parent != $current_parent )//Был перенос
			{
				//Увеличивем count у нового parent
				if($parent > 0)
				{
					if( ! $db_link->prepare("UPDATE `content` SET `count` = `count`+1 WHERE `id` = ?;")->execute( array($parent) ) )
					{
						throw new Exception(translate_str_by_id(2152));
					}
				}
				
				//Уменьшаем count с старого родительского узла
				if( $current_parent > 0 )
				{
					if( ! $db_link->prepare("UPDATE `content` SET `count` = `count`-1 WHERE `id` = ?;")->execute( array($current_parent) ) )
					{
						throw new Exception(translate_str_by_id(2153));
					}
				}
			}
			
			
			//Если пользователь установил этот материал Главным. (снять его он не мог)
			if( $current_main_flag != $main_flag )
			{
				//Ставим main_flag = 0 для материала, который был главным до этого
				if( ! $db_link->prepare("UPDATE `content` SET `main_flag` = 0 WHERE `main_flag` = 1 AND `id` != ? AND `is_frontend` = ?;")->execute( array($content_id, $is_frontend) ) )
				{
					throw new Exception(translate_str_by_id(2154));
				}
			}
			
			
			//Обновляем права доступа
			$groups_access = json_decode($content["groups_access"], true);
			if( ! $db_link->prepare("DELETE FROM `content_access` WHERE `content_id` = ?;")->execute( array($content_id) ) )
			{
				throw new Exception(translate_str_by_id(2155));
			}
			if( count($groups_access) > 0 )
			{
				$binding_values = array();
				
				$SQL_content_access = "INSERT INTO `content_access` (`content_id`, `group_id`) VALUES ";
				for($i=0; $i < count($groups_access); $i++)
				{
					if($i > 0)
					{
						$SQL_content_access .= ",";
					}
					$SQL_content_access .= " (?, ?) ";
					
					array_push($binding_values, $content_id);
					array_push($binding_values, $groups_access[$i]);
					
				}
				if( ! $db_link->prepare($SQL_content_access)->execute( $binding_values ) )
				{
					throw new Exception(translate_str_by_id(2141));
				}
			}
			
			
			//Обработка вложенных узлов
			if( ! handle_child_nodes($content_id) )
			{
				throw new Exception(translate_str_by_id(2156));
			}
			
			
			//throw new Exception("Тестовое исключение");
		}
		catch (Exception $e)
		{
			$db_link->rollBack();//Откатываем все изменения и закрываем транзакцию
			//Ошибка получения данных родительского узла
			$error_message = urlencode($e->getMessage().". ".translate_str_by_id(2142));
			?>
			<script>
				location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager/content?content_id=<?php echo $content_id; ?>&error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
		
		
		//Дошли сюда - значит все запросы выполнены без ошибок
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		//Выполнено успешно
		$success_message = urlencode(translate_str_by_id(2157));
		?>
		<script>
			location="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager/content?content_id=<?php echo $content_id; ?>&success_message=<?php echo $success_message; ?>";
		</script>
		<?php
		exit;
	}
}
else//Действий нет - выводим страницу
{
	?>
	
	<?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
	
	
	<?php
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	$content_id = 0;
	$parent = 0;
	$value = "";
	$alias = "";
	$description = "";
	$content_type = "text";
	$content = "";
	$title_tag = "";
	$description_tag = "";
	$keywords_tag = "";
	$author_tag = "";
	$css_js = "";
	$robots_tag = "";
	$published_flag = 1;
	$main_flag = 0;
	$groups_access = array();
	$parent_value = translate_str_by_id(2162);
	
	
	
	
	//Мультиязычность
	for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
	{
		//Имя переменной, с которой сейчас работаем:
		$var_name = $translated_items[$i];
		
		${$var_name} = "";
		${$var_name."_lang_str_id"} = 0;
	}
	
	
	
	
	if( !empty( $_GET["content_id"] ) )
	{
		$content_id = (int)$_GET["content_id"];
		
		$content_query = $db_link->prepare("SELECT * FROM `content` WHERE `id` = ?;");
		$content_query->execute( array($content_id) );
		$content_record = $content_query->fetch();
		
		
		if( $content_record == false )
		{
			?>
			<script>
			alert('<?php echo translate_str_by_id(2158); ?>');
			
			location = "/<?php echo $DP_Config->backend_dir; ?>/content/content_manager";
			</script>
			<?php
			exit;
		}
		
		
		//Если пользователь руками передал сюда ID материала, относящегося к другому режиму редактирования - запрещаем его редактировать
		if( $is_frontend != $content_record["is_frontend"] )
		{
			
			$message_str_id = 2159;
			if( !$is_frontend )
			{
				$message_str_id = 2160;
			}
			
			?>
			<script>
			alert('<?php echo translate_str_by_id($message_str_id); ?>');
			
			
			location = "/<?php echo $DP_Config->backend_dir; ?>/content/content_manager?error_message=<?php echo urlencode(translate_str_by_id(2161)); ?>";
			</script>
			<?php
			exit;
		}
		
		
		$parent = $content_record["parent"];
		$value = $content_record["value"];
		$alias = $content_record["alias"];
		$description = $content_record["description"];
		$content_type = $content_record["content_type"];
		$content = $content_record["content"];
		$title_tag = $content_record["title_tag"];
		$description_tag = $content_record["description_tag"];
		$keywords_tag = $content_record["keywords_tag"];
		$author_tag = $content_record["author_tag"];
		$css_js = $content_record["css_js"];
		$robots_tag = $content_record["robots_tag"];
		$published_flag = $content_record["published_flag"];
		$main_flag = $content_record["main_flag"];
		
		
		
		//Мультиязычность
		for( $i = 0 ; $i < count( $translated_items ) ; $i++ )
		{
			//Имя переменной, с которой сейчас работаем:
			$var_name = $translated_items[$i];
			
			//Если текущий тип содержимого - php, то, $content не переводим
			if( $var_name == 'content' && $content_type == 'php' )
			{
				${$var_name."_lang_str_id"} = 0;//ID строки из мультиязычности
				
				continue;
			}
			
			${$var_name."_lang_str_id"} = ${$var_name};//ID строки из мультиязычности
			${$var_name} = translate_str_by_id(${$var_name});//Перевод строки на текущий язык ПУ
		}
		
		
		
		
		//Получаем права доступа к материалу
		$groups_access_query = $db_link->prepare("SELECT `group_id` FROM `content_access` WHERE `content_id` = ?;");
		$groups_access_query->execute( array($content_id) );
		while($groups_access_record = $groups_access_query->fetch() )
		{
			array_push($groups_access, (int)$groups_access_record["group_id"]);
		}
		
		//Получаем данные родительского узла
		$parent_value = translate_str_by_id(2162);
		if($parent > 0)
		{
			$parent_query = $db_link->prepare('SELECT `value` FROM `content` WHERE `id` = ?;');
			$parent_query->execute( array($parent) );
			$parent_record = $parent_query->fetch();
			$parent_value = translate_str_by_id($parent_record["value"]);
		}
	}
	?>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" onClick="save_action();" href="javascript:void(0);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2114); ?></div>
				</a>
				
				<?php
				if( $content_id > 0 )
				{
					?>
					<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager/content">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2163); ?></div>
					</a>
					<?php
				}
				?>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/content/content_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/documents.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2115); ?></div>
				</a>
				


				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2164); ?>
			</div>
			<div class="panel-body">
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						H1 *
					</label>
					<div class="col-lg-6">
						<input type="text" onKeyUp="on_h1_changed();" id="value_input" value="<?php echo $value; ?>" class="form-control"/>
						
						<script>
						//Обработка ввода H1 - инициализируем алиас на транслите
						function on_h1_changed()
						{
							if( document.getElementById("alias_autotranslit").checked )
							{
								var alias = "";
								alias = iso_9_translit(document.getElementById("value_input").value,  5);//5 - русский текст
								alias = alias.replace(/\s/g, '-');
								alias = alias.toLowerCase();
								alias = alias.replace(/[^\d\sA-Z\-_]/gi, '');//Убираем все символы кроме букв, цифр, тире и нинего подчеркивания
								
								document.getElementById("alias_input").value = alias;
							}
						}
						</script>
						
						
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2165); ?>
					</label>
					<div class="col-lg-6">
						<input type="checkbox" id="alias_autotranslit" value="alias_autotranslit" class="form-control" checked="checked" />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2166); ?> *
					</label>
					<div class="col-lg-6">
						<input type="text" id="alias_input" value="<?php echo $alias; ?>" class="form-control"/>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2167); ?> *
					</label>
					<div class="col-lg-6">
						<input type="text" id="title_tag_input" value="<?php echo $title_tag; ?>" class="form-control"/>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2168); ?>
					</label>
					<div class="col-lg-6">
						<textarea id="description_tag_input" class="form-control"/><?php echo $description_tag; ?></textarea>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2169); ?>
					</label>
					<div class="col-lg-6">
						<textarea id="keywords_tag_input" class="form-control"/><?php echo $keywords_tag; ?></textarea>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2170); ?>
					</label>
					<div class="col-lg-6">
						<input type="text" id="robots_tag_input" value="<?php echo $robots_tag; ?>" class="form-control"/>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2171); ?>
					</label>
					<div class="col-lg-6">
						<input type="text" id="author_tag_input" value="<?php echo $author_tag; ?>" class="form-control"/>
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	<div class="col-lg-6">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2172); ?>
			</div>
			<div class="panel-body">
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2173); ?>
					</label>
					<div class="col-lg-6">
						<?php
						if( $content_id == 0 )
						{
							?>
							<?php echo translate_str_by_id(2174); ?>
							<?php
						}
						else
						{
							echo $content_id;
						}
						?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2175); ?> *
					</label>
					<div class="col-lg-6">
						<textarea id="description_input" class="form-control"/><?php echo $description; ?></textarea>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2176); ?>
					</label>
					<div class="col-lg-6">
						<textarea id="css_js_input" class="form-control"/><?php echo $css_js; ?></textarea>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2118); ?>
					</label>
					<div class="col-lg-6">
						<select id="content_type_select" name="content_type_select" onchange="content_type_changed();" class="form-control">
    	                    <option value="text"><?php echo translate_str_by_id(2119); ?></option>
    	                    <option value="php"><?php echo translate_str_by_id(2120); ?></option>
    	                </select>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2177); ?>
					</label>
					<div class="col-lg-6">
						<?php
						$attribs = "";
						if( $main_flag == true )
						{
							$attribs = " checked=\"checked\" disabled=\"disabled\" ";
						}
						?>
						<input type="checkbox" class="form-control" id="main_flag_input" <?php echo $attribs; ?> />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2178); ?>
					</label>
					<div class="col-lg-6">
						<?php
						$attribs = "";
						if( $published_flag == true )
						{
							$attribs = " checked=\"checked\" ";
						}
						if( $main_flag == true )
						{
							$attribs .= " disabled=\"disabled\" ";
						}
						?>
						<input type="checkbox" class="form-control" id="published_flag_input" <?php echo $attribs; ?> />
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2179); ?>
					</label>
					<div class="col-lg-6">
						<select multiple="multiple" id="groups_selector">
						
						<?php
						//Получаем группы пользователей						
						//Получаем максимальный уровень вложенности групп
						$max_level_group_query = $db_link->prepare("SELECT MAX(`level`) AS `max_level` FROM `groups`;");
						$max_level_group_query->execute();
						$max_level_group_record = $max_level_group_query->fetch();
						$max_level_group = $max_level_group_record["max_level"];
						//Формируем SQL-запрос для получения записей в виде древовидной структуры (для групп)
						$SQL_GROUPS = "SELECT ";
						$SQL_GROUPS_fields = "";
						$SQL_GROUPS_joins = "";
						for($l=1; $l <= $max_level_group; $l++)
						{
							if( $l > 1 )
							{
								$SQL_GROUPS_fields = $SQL_GROUPS_fields.",";
								
								$l_last = $l -1;
								
								$SQL_GROUPS_joins = $SQL_GROUPS_joins." LEFT JOIN `groups` AS `t$l` ON `t$l`.`parent` = `t$l_last`.`id` ";
							}
							
							
							$SQL_GROUPS_fields = $SQL_GROUPS_fields."
							`t$l`.`id` AS `l".$l."_id`,
							`t$l`.`value` AS `l".$l."_value`,
							`t$l`.`level` AS `l".$l."_level`,
							`t$l`.`for_backend` AS `l".$l."_for_backend`";
						}
						//Собираем строку запроса
						$SQL_GROUPS = $SQL_GROUPS.$SQL_GROUPS_fields." FROM `groups` AS `t1` ".$SQL_GROUPS_joins." WHERE `t1`.`parent` =0;";
						
						
						
						
						$groups_query = $db_link->prepare($SQL_GROUPS);
						$groups_query->execute();
						
						$already_shown = array();//Фильтр - для уже показанных групп
						while( $group_record = $groups_query->fetch() )
						{
							$for_backend_group = false;//Флаг - группа для бэкенда. По-умолчанию, перед обработкой ветки - false
							
							//Заходим в ветку
							for($l=1; $l <= $max_level_group; $l++)
							{
								if( $group_record["l".$l."_for_backend"] == 1 )
								{
									$for_backend_group = true;//Начали выводить для бэкенда. Эта группа и все ее вложенные (до конца for) - для бэкенда
								}
								
								if($group_record["l".$l."_id"] == NULL)
								{
									break;//К следующей ветке
								}
								
								//Такой узел уже был показан выше
								if( array_search((int)$group_record["l".$l."_id"], $already_shown) === false )
								{
									array_push($already_shown, (int)$group_record["l".$l."_id"]);
								}
								else
								{
									continue;
								}
								
								
								//Добавляем обозначение вложенности
								$group_record["l".$l."_value"] = translate_str_by_id($group_record["l".$l."_value"]);
								for($lev=1; $lev < $group_record["l".$l."_level"]; $lev++)
								{
									$group_record["l".$l."_value"] = "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;".$group_record["l".$l."_value"];
								}
								
								
								//Если текущий режим - бэкенд, и в итерации - группа не для бэкнда
								if( $is_frontend == 0 && !$for_backend_group )
								{
									continue;
								}
								?>
								<option value="<?php echo $group_record["l".$l."_id"]; ?>"><?php echo $group_record["l".$l."_value"]; ?></option>
							<?php
							}
						}//for
						?>
						</select>
						
						
						<script>
							//Делаем из селектора виджет с чекбоками
							$('#groups_selector').multipleSelect({placeholder: "Нажмите для выбора...", width:"100%"});
							
							//Инициализируем выбранные значения
							$('#groups_selector').multipleSelect('setSelects', <?php echo json_encode($groups_access); ?>);
						</script>
						
						
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				
				
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2180); ?>
					</label>
					<div class="col-lg-6">
						<input type="hidden" id="parent_input" value="<?php echo $parent; ?>" />
						<button onClick="pointContentParent();" class="btn btn-success " type="button"><i class="fa fa-hand-pointer-o"></i> <span class="bold" id="parent_indicator"><?php echo $parent_value." (ID $parent) "; ?></span></button>
					</div>
				</div>
				<!-- Модальное окно "Выбор родительского узла" -->
				<div class="text-center m-b-md">
					<div class="modal fade" id="modalWindow_contentParent" tabindex="-1" role="dialog"  aria-hidden="true">
						<div class="modal-dialog modal-lg">
							<div class="modal-content">
								<div class="color-line"></div>
								<div class="modal-header">
									<h4 class="modal-title"><?php echo translate_str_by_id(2181); ?></h4>
								</div>
								<div class="modal-body">
									<div class="row" id="parent_content_tree">
									</div>
									<script>
									//Функция запроса материалов для выбора родительского
									var s_page = 0;
									function get_content_json_list()
									{
										document.getElementById("parent_content_tree").innerHTML = "<div class=\"text-center\"><?php echo translate_str_by_id(2182); ?></div> <div class=\"spinner\"> <div class=\"rect1\"></div> <div class=\"rect2\"></div> <div class=\"rect3\"></div> <div class=\"rect4\"></div> <div class=\"rect5\"></div> </div>";
										
										
										jQuery.ajax({
											type: "GET",
											async: true,
											url: "/<?php echo $DP_Config->backend_dir; ?>/content/content/ajax_get_content_json_list.php?code=<?php echo urlencode($DP_Config->secret_succession); ?>&content_id=<?php echo $content_id; ?>&is_frontend=<?php echo $is_frontend; ?>&s_page="+s_page+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
											dataType: "text",//Тип возвращаемого значения
											success: function(answer)
											{
												
												answer = JSON.parse(answer);
												console.log(answer);
												if(answer["status"] != true)
												{
													document.getElementById("parent_content_tree").innerHTML = "<?php echo translate_str_by_id(2183); ?>";
												}
												else
												{
													//Страницы здесь выводятся в иерархическом режиме - как в менеджере материалов
													var content = answer["content"];
													var max_level = answer["max_level"];
													var count_total_for_pagination = answer["count_total_for_pagination"];
													var count_total = answer["count_total"];
													var list_page_limit = answer["list_page_limit"];
													
													var content_html = "<table cellpadding=\"1\" cellspacing=\"1\" class=\"table table-condensed table-striped\"><thead><tr><th><?php echo translate_str_by_id(2235); ?></th><th></th><th>ID</th><th>H1</th></tr></thead><tbody>";
													
													
													
													//Выставление текущего
													var checked = "";
													if( parseInt(document.getElementById("parent_input").value) == 0 )
													{
														checked = " checked=\"checked\" ";
													}
													
													content_html += "<tr id=\"tr_0\" caption=\"<?php echo translate_str_by_id(2162); ?>\"><td>0</td><td><input type=\"radio\" name=\"parent_content_radio\" value=\"0\" "+checked+" id=\"parent_radio_0\" /></td><td><label for=\"parent_radio_0\">0</label></td><td><label for=\"parent_radio_0\"><?php echo translate_str_by_id(2162); ?></label></td></tr>";
													
													var already_shown = new Array();
													
													var strings_print_count = 1;
													
													for(var i=0; i < content.length; i++)
													{
														for(var l=1; l <= max_level; l++)
														{
															if(content[i]["l"+l+"_id"] == undefined)
															{
																break;//К следующей ветке
															}
															
															//Если это - этот же материал - переход к следующей ветке, т.к. нельзя вложить материал в самого себя и во вложенные материалы
															if(content[i]["l"+l+"_id"] == parseInt(<?php echo $content_id; ?>))
															{
																break;//К следующей ветке
															}
															
															//Такой узел уже был показан выше
															if( already_shown[content[i]["l"+l+"_id"]] == undefined )
															{
																already_shown[content[i]["l"+l+"_id"]] = content[i]["l"+l+"_id"];
															}
															else
															{
																continue;
															}
															
															var value_to_show = content[i]["l"+l+"_value"];
															
															//Добавляем отступ относительно корня дерева
															value_to_show = "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"+value_to_show;
															
															
															//Добавляем обозначение вложенности
															for(var lev=1; lev < content[i]["l"+l+"_level"]; lev++)
															{
																value_to_show = "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;"+value_to_show;
															}
															
															//Выставление текущего
															var checked = "";
															if( parseInt(document.getElementById("parent_input").value) == parseInt(content[i]["l"+l+"_id"]) )
															{
																checked = " checked=\"checked\" ";
															}
															
															content_html += "<tr id=\"tr_"+content[i]["l"+l+"_id"]+"\" caption=\""+content[i]["l"+l+"_value"]+"\"><td>"+strings_print_count+"</td><td><input type=\"radio\" name=\"parent_content_radio\" value=\""+content[i]["l"+l+"_id"]+"\" "+checked+" id=\"parent_radio_"+content[i]["l"+l+"_id"]+"\" /></td><td><label for=\"parent_radio_"+content[i]["l"+l+"_id"]+"\">"+content[i]["l"+l+"_id"]+"</label></td><td><label for=\"parent_radio_"+content[i]["l"+l+"_id"]+"\">"+value_to_show+"</label></td></tr>";
															strings_print_count++;
														}
													}
													
													content_html += "</tbody><tfoot><tr><td colspan=\"4\" style=\"text-align:center;\">";
													
													
													//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
													//Исходные данные для переключателя страниц:
													var current_page = parseInt(s_page);
													var rows_per_page = list_page_limit;
													var elements_count_rows = count_total_for_pagination;
													
													content_html += '<div class="btn-group">';//HTML переключателя страниц
													
													
													//КНОПКА "ВЛЕВО"
													var to_left_disabled = "";
													if( current_page == 0 )
													{
														to_left_disabled = "disabled";
													}
													
													content_html += '<a class="btn btn-default '+to_left_disabled+'" onclick="go_to_page(0);" href="javascript:void(0);">Первая</a>';
													content_html += '<a class="btn btn-default '+to_left_disabled+'" onclick="go_to_page('+ parseInt(current_page-1) +');" href="javascript:void(0);"><i class="fa fa-chevron-left"></i></a>';
													
													
													//Определяем количество страниц
													var pages_count = parseInt(elements_count_rows/rows_per_page);
													if( parseInt(elements_count_rows%rows_per_page) > 0 )
													{
														pages_count++;
													}
													
													
													//Выводим кнопки для конкретных страниц (с номерами)
													/*
													Количество страниц, теоретически, может быть очень большим. Чтобы не гонять цикл, пропускаем страницы до тех, которые нужно выводить
													*/
													var i_start = current_page - 2;
													if(i_start < 0)
													{
														i_start = 0;
													}
													
													for( var i = i_start; i < pages_count; i++)
													{
														//Две кнопки до текущей - показываем
														if( parseInt(current_page - i) > 2  )
														{
															continue;
														}
														
														
														//Две кнопки после текущей - показываем
														if( parseInt(i - current_page) > 2  )
														{
															break;
														}
														
														
														
														var active = "";
														if( parseInt(i) == parseInt(current_page) )
														{
															active = "active";
														}
														
														content_html += '<a href="javascript:void(0);" class="btn btn-default '+active+'" onclick="go_to_page('+parseInt(i)+');">'+ parseInt(i+1) +'</a>';
														
													}
													
													//КНОПКА "ВПРАВО"
													var to_right_disabled = "";
													if( parseInt(current_page+1) == parseInt(pages_count) )
													{
														to_right_disabled = "disabled";
													}
													
													content_html += '<a href="javascript:void(0);" class="btn btn-default '+to_right_disabled+'" onclick="go_to_page('+parseInt(current_page+1)+');"><i class="fa fa-chevron-right"></i></a>'
													content_html += '<a href="javascript:void(0);" class="btn btn-default '+to_right_disabled+'" onclick="go_to_page('+parseInt(pages_count-1)+');"><?php echo translate_str_by_id(2184); ?></a>';
													content_html += '</div><br>';
													
													content_html += '<div style="text-align:left;color:#000;"><?php echo translate_str_by_id(2185); ?> - <b>'+rows_per_page+'</b>. <?php echo translate_str_by_id(2186); ?><b>'+rows_per_page+'</b>.<br><?php echo translate_str_by_id(2187); ?>: <b>'+count_total+'</b><br><?php echo translate_str_by_id(2188); ?>: <b>'+pages_count+'</b></div>';
													
													content_html += '</td></tr></tfoot></table>';
													
													
													document.getElementById("parent_content_tree").innerHTML = content_html;
												}
											}
										});
									}
									</script>
								</div>
								<div class="modal-footer">
									<button onclick="applyContentParent();" class="btn btn-success " type="button"><i class="fa fa-check"></i> <span class="bold"><?php echo translate_str_by_id(2189); ?></span></button>
								
									<button type="button" class="btn btn-default" data-dismiss="modal"><?php echo translate_str_by_id(2190); ?></button>
								</div>
							</div>
						</div>
					</div>
				</div>
				<script>
				//-----------------------------------------------------
				//Кнопка "Указать родительский узел"
				function pointContentParent()
				{
					$('#modalWindow_contentParent').modal();//Открыть окно
					
					get_content_json_list();//Запросить материалы для выбора родительского
				}
				//-----------------------------------------------------
				//Кнопка "Применить" в окне выбора товара
				function applyContentParent()
				{
					var selected_parent = jQuery('input[name="parent_content_radio\"]:checked').val();
					
					if( selected_parent == undefined )
					{
						alert("<?php echo translate_str_by_id(2191); ?>");
						return;
					}
					
					
					//Устанавливаем индикацию и записываем в input
					document.getElementById("parent_input").value = selected_parent;
					document.getElementById("parent_indicator").innerHTML = document.getElementById("tr_"+selected_parent).getAttribute("caption")+" (ID "+selected_parent+")";
					
					
					//Скрыть окно выбора сопутствующих товаров товаров
					$('#modalWindow_contentParent').modal('hide');
				}
				//-----------------------------------------------------
				//Переход на другую страницу с выбором родительского материала
				function go_to_page(need_page)
				{
					s_page = need_page;
					
					get_content_json_list();//Запросить материалы для выбора родительского
				}
				//-----------------------------------------------------
				</script>
				
				
				
				
				
				
				
				
				
				
				
				
			</div>
		</div>
	</div>
	
	
	
	
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2192); ?>
			</div>
			<div class="panel-body">
				<div id="content_value_area"></div>
			</div>
		</div>
	</div>
	
	
	
	
	
	
	<!-- Для загрузки файлов через TinyMCE -->
	<iframe id="file_form_target" name="file_form_target" style="display:none"></iframe>
	<form id="file_form" action="/<?php echo $DP_Config->backend_dir; ?>/lib/tinymce/postAcceptor.php" target="file_form_target" method="post" enctype="multipart/form-data" style="width:0px;height:0;overflow:hidden">
		<input id="image_input" name="image" type="file" onchange="onFileSelected();">
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
    <script>
	//Обработка выбора файла текстовом редакторе
	function onFileSelected()
	{
		//Создаем данные для формы
		var formData = new FormData();
		formData.append('image', $('input[type=file]')[0].files[0]); 
		formData.append('csrf_guard_key', '<?php echo $user_session["csrf_guard_key"]; ?>');
		
		//Передаем форму с файлом на сервер
		$.ajax({
			url: '/<?php echo $DP_Config->backend_dir; ?>/lib/tinymce/postAcceptor.php',
			data: formData,
			dataType:"json",
			type: "POST",
			contentType: false,
			processData: false,
			success : function (answer){
				console.log("Ответ сервера: "+answer);
				
				if(answer.status == true)
				{
					//Указываем имя файл в окне его выбора от TinyMCE и закрываем окно
					top.$('.mce-btn.mce-open').parent().find('.mce-textbox').val(answer.url).closest('.mce-window').find('.mce-primary').click();
					
					//Очищаем input
					document.getElementById("image_input").value = '';
				}
				else
				{
					alert("<?php echo translate_str_by_id(2122); ?>: "+answer.message)
					
					//Очищаем input
					document.getElementById("image_input").value = '';
				}
			}
		})
	}
	</script>
	
	

    
   

    <script>
    //-----------------------------------------------------
    //Вспомогательные паременные для запоминания содержимого при переключении типа 
    var text_content = "";
    var php_content = "";
    var already_loaded = false;//Флаг - Страница полностью загружена. При загрузке страницы еще некоторые объекты не доступны и к ним нельзя обращаться.
    
    //Переключение типа содержимого
    function content_type_changed()
    {
        var content_type = document.getElementById("content_type_select").value;
        var content_value_area = document.getElementById("content_value_area");
        
        if(content_type == "php")
        {
            if(already_loaded)
            {
                text_content = tinymce.activeEditor.getContent();//Сначала запоминаем текстовое содержимое, чтобы не потерять
            }
			
			
			content_value_area.innerHTML = "<div class=\"form-group\"><label for=\"\" class=\"col-lg-6 control-label\"><?php echo translate_str_by_id(2124); ?></label><div class=\"col-lg-6\"><input type=\"text\" name=\"php_file_path\" id=\"php_file_path\" value=\"\" class=\"form-control\" /></div></div>";
			
			
            
            if(already_loaded)
            {
                document.getElementById("php_file_path").value = php_content;//Восстанавливаем запомненное содержимое (если оно было)
            }
        }
        else if(content_type == "text")
        {
            if(already_loaded)
            {
                php_content = document.getElementById("php_file_path").value;//Сначала запоминаем php-содержимое, чтобы не потерять
            }
            
            content_value_area.innerHTML = "<textarea style=\"min-height:400px\" class=\"tinymce_editor\" id=\"tinymce_editor\"></textarea>";
            tinymce.init({
                selector: "textarea.tinymce_editor",
                plugins: [
                    "advlist autolink lists link image charmap print preview anchor",
                    "searchreplace visualblocks code fullscreen",
                    "insertdatetime media table contextmenu paste textcolor"
                ],
				extended_valid_elements:"script[*]",
                toolbar: [ 
                        "newdocument | bold italic underline strikethrough | alignleft aligncenter alignright alignjustify | styleselect | formatselect | fontselect | fontsizeselect | ", 
                        "cut copy paste | bullist numlist | outdent indent | blockquote | undo redo | removeformat subscript superscript | link image | forecolor backcolor",
                ],
				file_browser_callback: function(field_name, url, type, win) {
					if(type=='image') $('#file_form input').click();
				}
            });
            
            if(already_loaded)
            {
                document.getElementById("tinymce_editor").value = tinymce.activeEditor.setContent(text_content);//Восстанавливаем запомненное содержимое (если оно было)
            }
        }
    }//~function content_type_changed()
    

    
    
    
    //-------------- ДЕЙСВТВИЯ ПОСЛЕ ЗАГРУЗКИ СТРАНИЦЫ -------------->
    //Тип содержимого при загрузке страницы:
    var current_content_type = "<?php echo $content_type; ?>";
    
    //Выставляем текущий вариант типа содержимого:
    content_type_select = document.getElementById("content_type_select");//Селектор типов содержимого
    for(var j=0; j<content_type_select.options.length; j++)
    {
        if(content_type_select.options[j].value == current_content_type)
        {
            content_type_select.options[j].selected = true;
            break;
        }
    }
    content_type_changed();//Обработка выбора типа содержимого
    
    
    
    //Заполняем текущее содержимое:
	<?php
	if($content_type == "text")
	{
		$content = addcslashes(str_replace(array("\n","\r"), '', $content), "'");
		$content = str_replace("/", "\/", $content);
	}
	else if($content_type == "php")
	{
		$content = $content;
	}
	?>
	var current_content = '<?php echo $content; ?>';
    if(current_content_type == "text")
    {
        //console.log(current_content);
        document.getElementById("tinymce_editor").value = current_content;
    }
    else if(current_content_type == "php")
    {
        document.getElementById("php_file_path").value = current_content;
    }
    
    already_loaded = true;//Страница загружена
    </script>
	
	
	
	
	<form method="POST" name="save_form" style="display:none;">
		<input type="hidden" name="action" value="save_action" />
		<input type="hidden" id="content_object" name="content_object" value="" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	var alias_unique = false;//Флаг - поле алиас уникально в пределах одного уровня одной ветви
	//Функция сохранения
	function save_action()
	{
		//Собираем объект
		var content_object = new Object;
		
		content_object.content_id = <?php echo $content_id; ?>;
		content_object.is_frontend = <?php echo $is_frontend; ?>;
		content_object.value = document.getElementById("value_input").value;
		content_object.alias = document.getElementById("alias_input").value;
		content_object.title_tag = document.getElementById("title_tag_input").value;
		content_object.description_tag = document.getElementById("description_tag_input").value;
		content_object.keywords_tag = document.getElementById("keywords_tag_input").value;
		content_object.robots_tag = document.getElementById("robots_tag_input").value;
		content_object.author_tag = document.getElementById("author_tag_input").value;
		content_object.description = document.getElementById("description_input").value;
		content_object.css_js = document.getElementById("css_js_input").value;
		content_object.content_type = document.getElementById("content_type_select").value;
		content_object.check_hash = '<?php echo md5($content_id.$is_frontend.$DP_Config->secret_succession); ?>';
		<?php
		//Мультиязычность. Заполняем ID строк
		for( $i = 0 ; $i < count($translated_items) ; $i++ )
		{
			$var_name = $translated_items[$i];
			
			//В исходной русской версии так получилось, что при сохранении имя для $content используется $content_content. Поэтому здесь дополнительно обрабатываем, чтобы при сохранеии корректно получить ID строки.
			if( $var_name == 'content' )
			{
				?>
				content_object.<?php echo $var_name; ?>_content_lang_str_id = '<?php echo ${$var_name."_lang_str_id"}; ?>';
				<?php
			}
			else
			{
				?>
				content_object.<?php echo $var_name; ?>_lang_str_id = '<?php echo ${$var_name."_lang_str_id"}; ?>';
				<?php
			}
		}
		?>
		
		
		if(document.getElementById("main_flag_input").checked)
		{
			content_object.main_flag = 1;
		}
		else
		{
			content_object.main_flag = 0;
		}
		
		if(document.getElementById("published_flag_input").checked)
		{
			content_object.published_flag = 1;
		}
		else
		{
			content_object.published_flag = 0;
		}
		
		
		
		var groups_access = [].concat( $("#groups_selector").multipleSelect('getSelects') );
		content_object.groups_access = JSON.stringify(groups_access);
		
		content_object.parent = parseInt(document.getElementById("parent_input").value);
		
		
		content_object.content = "";
		if(content_object.content_type=="text")
		{
			content_object.content = tinymce.activeEditor.getContent();
		}
		else
		{
			content_object.content = document.getElementById("php_file_path").value;
		}
		
		
		
		//console.log(content_object);
		
		
		//ПЕРЕД ОТПРАВКОЙ ФОРМЫ ПРОВЕРЯЕМ ВСЕ ПОЛЯ
		//H1 (value)
		if(content_object.value == "")
		{
			alert("<?php echo translate_str_by_id(2193); ?>");
			return;
		}
		//Алиас
		if(content_object.alias == "")
		{
			alert("<?php echo translate_str_by_id(2194); ?>");
			return;
		}
		//Алиас - на наличие недопустимых знаков (допустимы a-z_-)
		var regex = new RegExp("[a-z0-9\-_]{0,}");
		var match = regex.exec(String(content_object.alias));
		if(match == null)
		{
			alert("<?php echo translate_str_by_id(2195); ?>");
			return false;
		}
		else
		{
			var match_value = String(match[0]);//Подходящая подстрока
			if(match_value != content_object.alias)
			{
				alert("<?php echo translate_str_by_id(2196); ?>");
				return false;
			}
		}
		//Алиас - на уникальность в пределах одного уровня одной ветки
		alias_unique = false;
		jQuery.ajax({
			type: "GET",
			async: false, //Запрос синхронный
			url: "/<?php echo $DP_Config->backend_dir; ?>/content/content/ajax_check_alias.php?code=<?php echo urlencode($DP_Config->secret_succession); ?>&content_id=<?php echo $content_id; ?>&is_frontend=<?php echo $is_frontend; ?>&alias="+content_object.alias+"&parent="+content_object.parent+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
			dataType: "json",//Тип возвращаемого значения
			success: function(answer)
			{
				if(answer.status == true)
				{
					if(answer.message == "ok")
					{
						alias_unique = true;
					}
					else
					{
						alias_unique = false;
					}
				}
				else
				{
					alert("<?php echo translate_str_by_id(2197); ?>");
				}
			}
		});
    	if(alias_unique == false)
    	{
    		alert("<?php echo translate_str_by_id(2198); ?>");
    		return false;
    	}
		
		
		
		
		
		//Title
		if(content_object.title_tag == "")
		{
			alert("<?php echo translate_str_by_id(2199); ?>");
			return;
		}
		//Пояснение
		if(content_object.description == "")
		{
			alert("<?php echo translate_str_by_id(2200); ?>");
			return;
		}
		
		
		//Обработка главного
		if(content_object.main_flag == 1)
		{
			if(content_object.parent > 0)
			{
				alert("<?php echo translate_str_by_id(2201); ?>");
				return;
			}
			
			if(content_object.published_flag == 0)
			{
				alert("<?php echo translate_str_by_id(2202); ?>");
				return;
			}
		}
		
		
		//console.log(content_object);
		//alert("ok");
		//return;
		
		//Заполняем форму и отправляем
		document.getElementById("content_object").value = JSON.stringify(content_object);
		document.forms["save_form"].submit();
	}
	</script>
	
	
	
	<?php
}
?>