<?php
//Скрипт для работы с мультиязычностью

$created_strings_count = 0;//Глобальный счетчик созданных строк за текущий вызов скрипта

// -------------------------------------------------------------------------------------------

//Функция получения следующего str_key. Используется везде, где добавляются новые строки.
function get_next_str_key()
{
	global $created_strings_count;
	global $DP_Config;
	
	$created_strings_count++;
	
	return time() . "_" . $created_strings_count . "_" . md5($DP_Config->domain_path);
}

// -------------------------------------------------------------------------------------------

//Функция возвращает перевод строки по ее ключу
function translate_str_by_key($str_key, $lang_code=null)
{
	//Для пустого ключа - пустая строка
	if( ! $str_key || empty($str_key) )
	{
		return '';
	}
	
	//Глобальные
	global $db_link;//Подключение к БД
	global $DP_Config;//Конфигурация
	
	//Если передан язык, используем его. Если не передан, то, берем рабочий язык.
	if( !$lang_code )
	{
		$lang_code = get_work_lang();
	}
	
	
	//Определяем, является ли данная строка текстом ошибки (такие строки выводим ВСЕГДА только на английском языке. Поле same у таких строк игнорируется)
	$is_error_query = $db_link->prepare("SELECT `is_error` FROM `lang_text_strings` WHERE `str_key` = ?;");
	$is_error_query->execute( array($str_key) );
	$is_error = $is_error_query->fetchColumn();
	if( $is_error == 1 )
	{
		$lang_code = 'en';
	}
	
	
	//Далее получаем перевод строки для данного языка
	$str = null;
	if( $is_error == 1 )
	{
		//Для строк, которые являются текстами ошибок
		$translation_query = $db_link->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?;');
		$translation_query->execute( array($str_key, $lang_code) );
		$str = $translation_query->fetchColumn();
	}
	else
	{
		//Для обычных строк
		$translation_query = $db_link->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = (SELECT IFNULL(`same`, ?) AS `same` FROM `lang_text_strings` WHERE `str_key` = ? );');
		$translation_query->execute( array($str_key, $lang_code, $str_key) );
		$str = $translation_query->fetchColumn();
	}
	
	
	//Если перевод не найден, заменяем заглушкой
	if( $str === false || $str === null )
	{
		if( isset($DP_Config->multilang_highlight_empty_strings) && $DP_Config->multilang_highlight_empty_strings == true )
		{
			$str = '==Empty string==';
		}
	}
	
	
	//Если эта строка является текстом ошибки, то, добавляем обозначение:
	if( $is_error == 1 )
	{
		$str = "ERROR STR_KEY: ".$str_key.". ".$str;
	}
	
	
	//Если включена отладка мультиязычности
	if( isset($DP_Config->multilang_debug) && $DP_Config->multilang_debug == true )
	{
		$str = $str." (".$str_key.")";
	}
	
	
	return $str;
}

// -------------------------------------------------------------------------------------------

//Функция-заглушка. Нужна здесь для обратной совместимости с теми скриптами, которые изначально были написаны под поиск перевода по ID строк. Затем, была внедрена обновленная схема данных, обеспечивающая полностью уникальные в рамках ВСЕХ сайтов ключи, и теперь для новых скриптов используется новая функция translate_str_by_key(). А, эта функция нужна здесь, чтобы старые скрипты продолжили работу без переделки (в тех скриптах ID строк равны их ключам - поэтому работает).
function translate_str_by_id($str_key, $lang_code=null)
{
	return translate_str_by_key($str_key, $lang_code);
}

// -------------------------------------------------------------------------------------------

//Универсальная функция сохранения пользовательского контента с учетом мультиязычности (тексты страниц, товары и т.д.). Возвращает 0 в случае ошибки, возвращает str_key в случае успеха (str_key может отличаться от переданного в аргументах). Аргумент $let_edit_not_custom - флаг "разрешить редактирование исходных (некастомных) строк" - может потребоваться для некоторых функций, например, для редактирования уведомлений
function save_custom_translation($str_key, $value, $lang_code=null, $let_edit_not_custom=false)
{
	//Глобальные
	global $db_link;//Подключение к БД
	
	
	//Если передан язык, используем его. Если не передан, то, берем рабочий язык.
	if( !$lang_code )
	{
		$lang_code = get_work_lang();
	}
	
	
	/*
	Принцип такой
	
	Здесь может быть два действия:
	- сохранить перевод на указанный язык (ВСЕГДА выполняется)
	- создать строку ПЕРЕД сохранением перевода (выполняется, если есть хотя бы одно из условий: str_key равен 0; строка не найдена в БД; строка не является кастомной (не кастомные строки - это те, которые существуют в CMS в исходном виде) )
	
	TODO. Возможно, если потребуется на практике, то, можно будет добавить настройку в конфиг сайта - так, чтобы не учитывать флаг is_custom, т.е. чтобы новая строка (кастомная) не создавалась бы при условии, что str_key уже есть в БД. Т.е. тогда пользователь, редактируя пользовательский контент, будет менять в т.ч. и исходные строки CMS. А кастомные будут создаваться только при условии, что идет создание какого-то нового контента, например, новая страница.
	*/
	
	/*
	Добавление строки - это один запрос. Перевод - то же просто. Т.е. использовать одни и те эе ajax-скрипты от редактора нет особой необходимости.
	
	Если идет копирование существующей некастомной строки, то, получается, инициатор должен об этом узнать и перезаписать у себя str_key, указывающий на новую строку. Тогда, возврат 0 означает, что отработало с ошибкой. Возврат не 0 означает, что сохранили успешно и значение возврата означает key сохраненной строки. А инициатор тогда уже сможет перезаписать этот key на своей стороне.
	
	//Тогда на стороне инициатора будет что-то типа:
	//$value = save_custom_translation( $value , $value);
	//На примере config_edit.php получается, что существующее в файле значение прогоняется через функцию. //Строка в БД по первому аргументу не будет найдена, и строка создастся, $value будет переводом. Вернется key строки, который запишестся в конфиг.
	
	----
	На клиентской стороне:
	- инпут со строкой, как в обычной версии CMS (<field_name>). Здесь содержится значение строки (т.е. перевод на определенный язык)
	- скрытый инпут с str_key (имя по шаблону <field_name>_lang_str_key, или его старый вариант - <field_name>_lang_str_id )
	Если строка новая, то, <field_name>_lang_str_key содержит 0. Если строка не новая, то, содержит key строки.
	Тогда в эту функцию передается значение из <field_name>_lang_str_key и отдельно из <field_name>
	---- Тогда алгоритм этой функции работает, как надо (создание новой, копирования некастомной и т.д.).
	Остается такой вопрос: если строка новая (при создании контента), либо, строка создается копированием некастомной, то, иницатор на своей стороне должен получить key созданной строки и при этом понять, была ли ошибка. Тогда, возврат key строки в случае успеха, 0 в случае ошибки.
	Тогда на стороне инициатора будет что-то типа:
	str_key = save_custom_translation( str_key (из скрытого поля) , $value);
	Далее - либо запись str_key в нужное место, либо, выдать ошибку, если возвращенный str_key == 0
	
	*/
	
	
	//Формируем информативное описание строки (создаваемой или копируемой)
	global $DP_Content;
	$str_description = "";
	switch( $DP_Content->url )
	{
		//1
		case 'control/config':
			$str_description = 'CONFIG EDITING';
			break;
		//2
		case 'control/notifications_settings/notification':
			$str_description = 'NOTIFICATION EDITING';
			break;
		//3
		case 'shop/geo/nodes':
			$str_description = "GEO TREE EDITING";
			break;
		//4
		case 'shop/logistics/offices/office':
			if( isset($_GET['office_id']) )
			{
				$str_description = 'OFFICE EDITING';
			}
			else
			{
				$str_description = 'OFFICE CREATING';
			}
			break;
		//5
		case 'shop/orders/statuses':
			$str_description = 'STATUSES EDITING';
			break;
		//6
		case 'shop/finance/operations_editor/operation':
			if( isset( $_GET['operation_id'] ) )
			{
				$str_description = 'FINANCE OPERATION EDITING';
			}
			else
			{
				$str_description = 'FINANCE OPERATION CREATING';
			}
			break;
		//7
		case 'shop/logistics/sposoby-polucheniya/sposob-polucheniya':
			$str_description = 'DELIVERY METHOD EDITING';
			break;
		//8
		case 'shop/taby-poiska/tab-poiska':
			$str_description = 'TAB EDITING';
			break;
		/*//9
		case '':
			$str_description = '';
			break;*/
		//10
		case 'shop/catalogue/catalogue_editor':
			$str_description = 'CATEGORIES TREE EDITING';
			break;
		//11
		case 'shop/catalogue/products/product':
			if( isset($_GET['product_id']) )
			{
				$str_description = 'PRODUCT EDITING';
			}
			else
			{
				$str_description = 'PRODUCT CREATING';
			}
			break;
		//12
		case 'shop/catalogue/line_lists/line_list':
			if( isset($_GET['id']) )
			{
				$str_description = 'LINE LIST EDITING';
			}
			else
			{
				$str_description = 'LINE LIST CREATING';
			}
			break;
		//13.1
		case 'shop/catalogue/tree_lists/tree_list':
			if( isset($_GET['id']) )
			{
				$str_description = 'TREE LIST BY TREE EDITING';
			}
			else
			{
				$str_description = 'TREE LIST BY TREE CREATING';
			}
			break;
		//13.2
		case 'shop/catalogue/tree_lists/redaktor-po-vetvyam':
			if( isset($_GET['tree_list_id']) )
			{
				$str_description = 'TREE LIST BY BRANCH EDITING';
			}
			else
			{
				$str_description = 'TREE LIST BY BRANCH CREATING';
			}
			break;
		//14
		case 'shop/catalogue/specialnye-poiski/specialnyj-poisk':
			if( isset($_GET['special_search_id']) )
			{
				$str_description = 'SPECIAL SEARCH EDITING';
			}
			else
			{
				$str_description = 'SPECIAL SEARCH CREATING';
			}
			break;
		//15
		case 'shop/catalogue/tovary-na-glavnoj':
			$str_description = 'MAIN PAGE PRODUCTS EDITING';
			break;
		//16
		case 'content/content_tree':
			$str_description = 'CONTENT TREE EDITING';
			break;
		//17
		case 'content/edit_content':
			$str_description = 'CONTENT EDITING';
			break;
		//18
		case 'content/content_manager/content':
			$str_description = 'NO TREE EDITOR CONTENT EDITING';
			break;
		//19
		case 'content/dopolnitelnye-teksty/dopolnitelnyj-tekst':
			if( isset($_GET['url']) )
			{
				$str_description = 'TEXT FOR URL EDITING';
			}
			else
			{
				$str_description = 'TEXT FOR URL CREATING';
			}
			break;
		//20
		case 'menu/menu_edit':
			if( isset($_GET['menu_id']) )
			{
				$str_description = 'MENU EDITING';
			}
			else
			{
				$str_description = 'MENU CREATING';
			}
			break;
		//21
		case 'users/usergroups':
			$str_description = 'USER GROUPS EDITING';
			break;
		//22
		case 'users/registracionnye-varianty':
			$str_description = 'REG VARIANTS EDITING';
			break;
		//23
		case 'users/polya-registracii':
			$str_description = 'REG FIELDS EDITING';
			break;
		//24
		case 'modules/module':
			if( isset($_GET['module_id']) )
			{
				$str_description = 'MODULE EDITING';
			}
			else
			{
				$str_description = 'MODULE CREATING';
			}
			break;
		//25
		case 'plugins/plugin':
			$str_description = 'PLUGIN EDITING';
			break;
		//26
		case 'requests/polya-vin-zaprosa':
			$str_description = 'VIN FROM FIELDS EDITING';
			break;
		//27
		case 'shop/cash/operations_editor':
			$str_description = 'CASH OPERATIONS TYPES EDITING';
			break;
		default:
			$str_description = "OTHER";
	}
	//Для функций, которые работают через AJAX и где нет $DP_Content
	// - //9 Редактирование причин и статусов возвратов
	if( basename($_SERVER['SCRIPT_FILENAME']) == 'ajax_statuses.php' )
	{
		$str_description = "RETURN STATUSES CREATING EDITING";
	}
	if( basename($_SERVER['SCRIPT_FILENAME']) == 'ajax_reasons.php' )
	{
		$str_description = "RETURN REASONS CREATING EDITING";
	}
	//Испорт каталога из XML
	if( basename($_SERVER['SCRIPT_FILENAME']) == 'ajax_xml_reader.php' )
	{
		$str_description = "CATALOG XML IMPORTING";
	}
	//Импорт CSV в каталог
	if( basename($_SERVER['SCRIPT_FILENAME']) == 'ajax_handle_file.php' )
	{
		$str_description = "CATALOG CSV IMPORTING";
	}
	
	
	$work_str_key = 0;//KEY рабочей строки (на ней сохраняем перевод и ее возвращаем в случае успеха)
	$need_to_create = false;//Флаг - нужно создать полностью новую строку.
	$translation_to_update = false;//Флаг "Обновить перевод" (если 1). Если 0, то, добавить перевод.
	
	//Если изначально $str_key равен 0, значит идет создание контента
	if( $str_key == 0 )
	{
		//Однозначно создается полностью новая строка
		$need_to_create = true;
	}
	else
	{
		//Получаем строку из БД
		$get_str_query = $db_link->prepare("SELECT *, (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` AND `lang_code` = ? ) AS `is_such_translation` FROM `lang_text_strings` WHERE `str_key` = ?;");
		$get_str_query->execute( array($lang_code, $str_key) );
		$str_record = $get_str_query->fetch();
		
		//Если не нашли такую строку в БД, значит ее нет. Скорее всего, такая ситуация на практике не возникнет, но, если возникла, то, возможно идет какая-то обратная совсместимость, например, если в качестве str_id передано исходное значение строки, которое таким образом приведет к созданию новой строки и сохранению исходного значения в виде перевода
		if( !$str_record )
		{
			//Однозначно создается полностью новая строка
			$need_to_create = true;
		}
		else if( $str_record['is_custom'] == 0 && !$let_edit_not_custom )
		{
			//Строка есть в БД, но, она не кастомная, а исходная, и, нельзя редактировать исходные строки. Исходные строки не меняем в процессе редактирования контента. Всместо них создаем кастомные строки.
			
			//Создается новая (кастомная) строка путем копирования исходной (не кастомной). Принцип такой: описание - техническое, same - null, is_error - false вне зависимости от значений в исходной строке (логически это правильно), is_custom выставляем в 1 (это будет кастомная строка)
			$work_str_key = get_next_str_key();
			if( ! $db_link->prepare("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `is_custom`, `str_key`) VALUES (?,?,?,?,?);")->execute( array($str_description.' CUSTOM COPIED', null, 0, 1, $work_str_key ) ) )
			{
				return false;
			}
			else
			{
				//Получаем все переводы исходной строки
				$get_translations_query = $db_link->prepare("SELECT * FROM `lang_text_strings_translation` WHERE `str_key` = ?;");
				$get_translations_query->execute( array( $str_key ) );
				while( $translation = $get_translations_query->fetch() )
				{
					//Если перевод на нужный язык есть, то далее его нужно будет обновить, а не добавить
					if( $translation['lang_code'] == $lang_code )
					{
						$translation_to_update = true;
					}
					
					//Добавляем перевод в БД, указывая key новой строки
					if( !$db_link->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?,?,?);")->execute( array($work_str_key, $translation['lang_code'], $translation['value']) ) )
					{
						return false;
					}
				}
			}
		}
		else if( $str_record['is_custom'] == 1 || ($str_record['is_custom'] == 0 && $let_edit_not_custom) )
		{
			//Строка кастомная, либо она не кастомная, но, можно редактировать не кастомную строку. Создавать строку не нужно. Ниже нужно будет только сохранить перевод.
			$work_str_key = $str_key;
			
			//Необходимо определить, есть ли уже для данной строки перевод на данный язык.
			if( $str_record['is_such_translation'] == 1 )
			{
				$translation_to_update = true;
			}
		}
		else
		{
			return false;
		}
	}
	
	//Если нужно создать полностью новую строку. Создаваемая строка будет точно кастомной.
	if( $need_to_create )
	{
		$work_str_key = get_next_str_key();
		
		if( ! $db_link->prepare("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `is_custom`, `str_key`) VALUES (?,?,?,?,?);")->execute( array($str_description.' CUSTOM CREATED', null, 0, 1, $work_str_key) ) )
		{
			return false;
		}
	}
	
	
	//Рабочая строка есть. Осталось только сохранить ее перевод на указанный язык.
	$binding_values = array($value, $work_str_key, $lang_code);
	$SQL_save_translation = "UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?;";
	if( !$translation_to_update )
	{
		$SQL_save_translation = "INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?);";
	}
	
	if( $db_link->prepare($SQL_save_translation)->execute( $binding_values ) )
	{
		return $work_str_key;
	}
	else
	{
		return false;
	}
}

// -------------------------------------------------------------------------------------------

//Функция получения рабочего языка
function get_work_lang()
{
	//Глобальные
	global $DP_Lang;//Язык из ядра
	
	if( $DP_Lang )
	{
		$lang = $DP_Lang;
	}
	else
	{
		//Здесь определяем, откуда идет запрос. Получается, что этот скрипт подключен к скрипту, выполняемому через AJAX и нужно определить, откуда идет запрос. Если из фроттенда, то, берем язык из первого алиаса, если из бэкенда, то, берем язык из куки. Все это делается в функции multilang_init()
		$multilang_params = multilang_init();
		
		$lang = $multilang_params['lang'];
	}
	
	return $lang;
}
// -------------------------------------------------------------------------------------------
//Вспомогательные функции для получения адреса страницы и т.д.


if( ! function_exists('getPageUrl_2') )
{
	//Получение строки URL страницы
	function getPageUrl_2()
	{
		$pageURL = 'http';
		if( isset($_SERVER["HTTPS"]) )
		{
			if ($_SERVER["HTTPS"] == "on") 
			{
				$pageURL .= "s";
			}
		}
		$pageURL .= "://";
		
		
		//Более правильный вариант
		$forwarded_port = isset($_SERVER['HTTP_X_FORWARDED_PORT']) ? $_SERVER['HTTP_X_FORWARDED_PORT'] : null;
		$pageURL = ((!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] != 'off') || (isset($_SERVER['SERVER_PORT']) && $_SERVER['SERVER_PORT'] == 443) || $forwarded_port == 443) ? "https://" : "http://";
		
		
		
		$server_port = isset($_SERVER["SERVER_PORT"]) ? $_SERVER["SERVER_PORT"] : 80;
		if ($server_port != "80" && $server_port != "443") 
		{
			$pageURL .= (isset($_SERVER["SERVER_NAME"]) ? $_SERVER["SERVER_NAME"] : "localhost") . ":". $server_port . (isset($_SERVER["REQUEST_URI"]) ? $_SERVER["REQUEST_URI"] : "/");
		} 
		else 
		{
			$pageURL .= (isset($_SERVER["SERVER_NAME"]) ? $_SERVER["SERVER_NAME"] : "localhost") . (isset($_SERVER["REQUEST_URI"]) ? $_SERVER["REQUEST_URI"] : "/");
		}
		return $pageURL;
	}//~function getPageUrl_2()
}
if( ! function_exists('getPageRoute') )
{
	//Определие маршрута страницы
	function getPageRoute()
	{
		//Получаем строку маршрута:
		$page_path = parse_url(getPageUrl_2(), PHP_URL_PATH);
		
		//Убираем index.php/, если есть
		$page_path = str_replace("index.php/", "", $page_path);
		
		//Если первый знак - "/", убираем:
		if($page_path[0] == "/")
		{
			$page_path = substr_replace($page_path, "", 0, 1);
		}
		//Если последний знак - "/", убираем:
		if( strlen($page_path) > 0 )
		{
			if($page_path[strlen($page_path)-1] == "/")
			{
				$page_path = substr_replace($page_path, "", strlen($page_path)-1, 1);
			}
		}
		
		
		return $page_path;
	}//~function getPageRoute()
}

// -------------------------------------------------------------------------------------------

//Вспомогательная функция - заменяет <lang> на текущий язык. Используется, к примеру, для тех ссылок, которые пользователь задает в ПУ и которые могут быть любого типа - абсолютные, относительные, внешние. Можно, например, в настройках шаблона Nero указать ссылки для баннеров в формате /<lang>/shop/orders и такие ссылки при выводе нужно прогонять через эту функцию - чтобы 
function get_real_link($link)
{
	global $multilang_params;
	
	return str_replace('<lang>', $multilang_params['lang'], $link);
}
// -------------------------------------------------------------------------------------------

/*
Функция получения URL страницы, в котором первый подраздел URL (который содержит язык) заменен на тег <lang>
ВНИМАНИЕ! Эта функция должна вызываться ТОЛЬКО при выполнении сразу всех условий:
- режим фронтенд
- мультиязычность включена
- скрипт точно определил, что на первом уровне URL СУЩЕСТВУЮЩИЙ ЯЗЫК
В противном случае - функция будет работать не корректно.
*/
function get_page_url_with_lang_tag()
{
	global $DP_Config;//Конфигурация CMS
	
	//Первый алиас - это код языка.
	$page_route = explode('/', getPageRoute() );
	$lang_url = $page_route[0];//Это код языка, взятый из URL
	
	$pageUrl = getPageUrl_2();
	
	//На случай, если в URL присутствует порт 443
	$pageUrl = str_replace(':443', '', $pageUrl);
	//var_dump($pageUrl);
	//var_dump($_SERVER);
	return str_replace( $DP_Config->domain_path.$lang_url , $DP_Config->domain_path.'<lang>' , $pageUrl );
}


// -------------------------------------------------------------------------------------------
//Реализация блок-схемы мультиязычности. Эта функция вызывается в самом начале для того, чтобы инициализировать параметры мультиязычности, которые затем уже используются ядром или другим скриптом.
function multilang_init()
{
	//Глобальные
	global $db_link;//Подключение к БД
	global $DP_Config;//Конфигурация CMS
	global $isFrontMode;//Режим Фронтенд/Бэкенд (для контекста ядра)
	global $DP_Core;//Переменная, объявленная в ядре (по ее наличию определяем, что работаем в ядре)
	
	// Avoid re-running after HTML started (get_work_lang() calls this when $DP_Lang is unset); prevents setcookie() after output.
	static $multilang_cache = null;
	if ($multilang_cache !== null) {
		return $multilang_cache;
	}
	
	$multilang_params = array();//Результатом этой функции будет ассоциативный массив, содержащий параметры мультиязычности
	
	
	//Инициализация из POST (пока оставляем проверку secret_succession)
	if( isset( $_POST['multilang_params'] ) && isset( $_POST['check'] ) && $_POST['check'] == $DP_Config->secret_succession )
	{
		$multilang_params_POST = json_decode( $_POST['multilang_params'] , true );
		if( $multilang_params_POST != null )
		{
			$multilang_params = $multilang_params_POST;
			$multilang_cache = $multilang_params;
			return $multilang_params;
		}
	}
	
	
	/*
	$multilang_params['lang'] - рабочий язык (на котором выводятся текстовые строки)
	$multilang_params['multilang'] - Флаг "Мультиязычность включена"
	
	//Для режима фронтенда при включенной мультиязычности
	$multilang_params['is_just_domain'] - флаг "Обращение к чистому домену"
	$multilang_params['is_lang_url_exists'] - флаг "в URL указан код доступного языка"
	
	//Для фронтенда - алиас языка для ссылок. Этот параметр НУЖНО подставлять хардкодом ВЕЗДЕ к ссылкам ВО ФРОНТЕНДЕ.
	Например, "<сюда добавляем это поле>/path1/path2"
	Тогда получится, если мультиязычность отключена - этот параметр пустой и не влияет на ссылки. Если мультиязычность включена, то, к ссылкам добавляется алиас языка
	$multilang_params['lang_href'] - формат '/lang_code'
	$multilang_params['lang_href_no_slash'] - формат 'lang_code'
	$multilang_params['lang_href_slash_after'] - формат 'lang_code/'
	
	//Для фронтенда, для модуля переключения языка - заготовка ссылки, на которую нужно перейти при переключении языка. По сути, это URL страницы, в котором подраздел языка заменен на <lang> - так, чтобы его потом проще было заменить на код языка в JavaScript без регулярных выражений и прочих обработок:
	$multilang_params['page_url_with_lang_tag'] - URL страницы с тегом <lang>
	*/
	
	if( (bool)$DP_Config->multilang == true )
	{
		//Мультиязычность is ON
		
		$is_front_mode = 1;//Сюда записываем режим, в котором работаем. 1 - по-умолчанию
		
		//Сначала нужно определить режим Фронтенд/Бэкенд. Если этот файл подключен к ядру, то, режим берем из ядра. Если файл подключен к AJAX-скрипту, то, нужно определить, откуда прищел запрос.
		if( ! isset($DP_Core) )
		{
			//AJAX-скрипт. Определяем, откуда пришел запрос. Если первый алиас равен backend_dir - это бэкенд.
			
			//Если запрос с этого же домена (как и должно быть)
			if( parse_url($DP_Config->domain_path, PHP_URL_HOST) == parse_url($_SERVER['HTTP_REFERER'], PHP_URL_HOST) )
			{
				//Берем path (т.е. подразделы, указанные после домена)
				$path = parse_url($_SERVER['HTTP_REFERER'], PHP_URL_PATH);
				$path = explode('/', $path);
				$path = $path[1];//0 - пустая строка
				
				//Если первый подраздел равен имени бэкенда, значит работаем в бэкенде
				if( $path == $DP_Config->backend_dir )
				{
					$is_front_mode = 0;
				}
			}
			//else - запрос пришел неизвестно откуда - считаем, что работаем во фронтенде.
			// Backend AJAX without Referer: URL path is /cp/...
			if( $is_front_mode == 1 && isset($_SERVER['REQUEST_URI']) )
			{
				$req_path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
				if( $req_path !== null && $req_path !== false && preg_match('#^/' . preg_quote($DP_Config->backend_dir, '#') . '(/|$)#u', $req_path) )
				{
					$is_front_mode = 0;
				}
			}
		}
		else
		{
			//Работа в контексте ядра. Режим уже указан в $isFrontMode
			$is_front_mode = $isFrontMode;
		}
		
		//Далее, в зависимости от режима
		if($is_front_mode)
		{
			//Режим Фронтенд. Для этого режима могут быть варианты.
			/*
			1. Если пользователь зашел на страницу
			1.1. Зашел на чистый домен (тогда язык определяется куки, либо, берется по-умолчанию, если куки не выставлено)
			1.2. Зашел в подраздел сайта. Тогда язык берется из первого алиаса URL.
			
			2. Если запрос к AJAX-скрипту. Язык берем из куки. Подразумевается, что пользователь открыл страницу. Она на нужном языке. При этом, язык записался в куки.
			*/
			
			//Определяем, был ли переход на чистый домен или на подраздел сайта.
			if( getPageRoute() == '' || ! isset($DP_Core) )
			{
				//Переход на чистый домен, либо обращение к AJAX-скрипту
				
				//Маршрут пустой и при этом есть $DP_Core - значит был переход на чистый домен. Выставляем параметр.
				if( isset($DP_Core) )
				{
					$multilang_params['is_just_domain'] = true;
					
					//Для модуля переключения языка - параметр page_url_with_lang_tag полностью равен URL страницы. Это может быть чистый домен, либо, чистый домен с GET-параметрами. Т.е. при переключении языка заменять в URL нечего.
					$multilang_params['page_url_with_lang_tag'] = getPageUrl_2();
				}
				
				
				//Получаем настройку языка из куки.
				$lang = null;
				if( isset($_COOKIE['lang']) )
				{
					$lang = $_COOKIE['lang'];
				}
				
				//Проверяем корректность настройки языка в куки (язык должен быть включен)
				$lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?;');
				$lang_query->execute( array(1, $lang) );
				
				if( $lang_query->fetchColumn() )
				{
					$multilang_params['lang'] = $lang;//Есть рабочий язык
				}
				else
				{
					//Либо куки не было установлено, либо установлено с некорректным языком. Тогда, получаем язык по-умолчанию.
					$default_lang_query = $db_link->prepare('SELECT * FROM `lang_languages` WHERE `active` = ? AND `is_default` = ?;');
					$default_lang_query->execute( array(1,1) );
					$default_lang_record = $default_lang_query->fetch();
					
					$multilang_params['lang'] = $default_lang_record['lang_code'];//Есть рабочий язык
					setcookie("lang", $multilang_params['lang'], time()+9999999, "/", '',false);//Куки языка клиентской части
				}
			}
			else
			{
				//Переход на подраздел
				
				$multilang_params['is_just_domain'] = false;//Выставляем параметр - переход не на чистый домен.
				
				//Первый алиас - это код языка.
				$page_route = explode('/', getPageRoute() );
				$lang_url = $page_route[0];//Это код языка, взятый из URL
				
				
				//Проверяем корректность настройки языка в URL (язык должен быть включен)
				$lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?;');
				$lang_query->execute( array(1, $lang_url) );
				
				if( $lang_query->fetchColumn() )
				{
					$multilang_params['lang'] = strtolower($lang_url);//Есть рабочий язык
					
					$multilang_params['is_lang_url_exists'] = true;//Выставляем параметр "Язык в URL доступен" - Да
					
					//Выставим куки. Это нужно, если, к примеру, пользователь руками ввел адрес в строку браузера - чтобы эти куки могли читать AJAX-скрипты для вывода соответствующего языка.
					setcookie("lang", $multilang_params['lang'], time()+9999999, "/", '',false);//Куки языка для клиентской части
					
					
					//Для модуля выбора языка - URL страницы, в котором первый подраздел (с языком) заменен на тег <lang>
					$multilang_params['page_url_with_lang_tag'] = get_page_url_with_lang_tag();
				}
				else
				{
					//В URL указан не существующий язык сайта. Это будет означать, что запрошенной страницы не существует. При этом, 404 нужно показать на языке по-умолчанию, либо на языке, который установлен в куки при условии, что он доступен.
					
					$multilang_params['is_lang_url_exists'] = false;//Выставляем параметр "Язык в URL доступен" - Нет
					
					//Получаем настройку языка из куки.
					$lang = null;
					if( isset($_COOKIE['lang']) )
					{
						$lang = $_COOKIE['lang'];
					}

					//Проверяем корректность настройки языка в куки (язык должен быть включен)
					$lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?;');
					$lang_query->execute( array(1, $lang) );
					if( $lang_query->fetchColumn() )
					{
						//Есть рабочий язык в куки - его и применяем
						$multilang_params['lang'] = strtolower($lang);
					}
					else
					{
						//Либо куки не было установлено, либо установлено с некорректным языком
						//Получаем язык по-умолчанию.
						$default_lang_query = $db_link->prepare('SELECT * FROM `lang_languages` WHERE `active` = ? AND `is_default` = ?;');
						$default_lang_query->execute( array(1,1) );
						$default_lang_record = $default_lang_query->fetch();
						
						$multilang_params['lang'] = $default_lang_record['lang_code'];//Есть рабочий язык
						setcookie("lang", $multilang_params['lang'], time()+9999999, "/", '',false);//Куки языка для клиентской части
					}
					
					//Для модуля переключения языка указываем полный URL страницы без тега <lang>. Означает, что при выборе языка страница так и останется 404, т.к. URL никак не поменяется. Логика такая: пользователь сейчас на несуществующей странице, т.к. на первом подразделе URL - не существующий язык. Возможно и далее - тоже несуществующие разделы по страницам, категориям, товарам и т.д., поэтому нет смысла заменять первый параметр URL на существующий язык сайта. Т.е. при переключении языка произойдет переход на эту же несуществующую страницу.
					$multilang_params['page_url_with_lang_tag'] = getPageUrl_2();
					
					
					//TODO
					//Возможно, после некоторого тестирования, следует потом передалать этот блок: прочитать куки lang и если такой язык есть, то, страницу 404 показать на таком языке, вместо языка по-умолчанию.
				}
			}	
		}
		else
		{
			//Режим Бэкенд: lang_cp cookie, или принудительный язык из config (backend_ui_lang), или язык по умолчанию
			$forced_backend_lang = '';
			if( isset($DP_Config->backend_ui_lang) && $DP_Config->backend_ui_lang !== null && $DP_Config->backend_ui_lang !== '' )
			{
				$forced_backend_lang = trim( (string) $DP_Config->backend_ui_lang );
			}
			if( $forced_backend_lang !== '' )
			{
				$lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?;');
				$lang_query->execute( array(1, $forced_backend_lang) );
				if( $lang_query->fetchColumn() )
				{
					$multilang_params['lang'] = $forced_backend_lang;
					setcookie("lang_cp", $multilang_params['lang'], time()+9999999, "/", '',false);
				}
				else
				{
					$forced_backend_lang = '';
				}
			}
			if( $forced_backend_lang === '' )
			{
				$lang_cp = null;
				if( isset($_COOKIE['lang_cp']) )
				{
					$lang_cp = $_COOKIE['lang_cp'];
				}
				
				$lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?;');
				$lang_query->execute( array(1, $lang_cp) );
				
				if( $lang_query->fetchColumn() )
				{
					$multilang_params['lang'] = $lang_cp;
				}
				else
				{
					$default_lang_query = $db_link->prepare('SELECT * FROM `lang_languages` WHERE `active` = ? AND `is_default` = ?;');
					$default_lang_query->execute( array(1,1) );
					$default_lang_record = $default_lang_query->fetch();
					
					$multilang_params['lang'] = $default_lang_record['lang_code'];
					setcookie("lang_cp", $multilang_params['lang'], time()+9999999, "/", '',false);
				}
			}
		}
		
		$multilang_params['multilang'] = true;//Мультиязычность включена
		$multilang_params['lang_href'] = '/'.$multilang_params['lang'];//Для подстановки к ссылкам
		$multilang_params['lang_href_no_slash'] = $multilang_params['lang'];//Для подстановки к ссылкам
		$multilang_params['lang_href_slash_after'] = $multilang_params['lang'].'/';//Для подстановки к ссылкам
	}
	else
	{
		//Мультиязычность is OFF
		$multilang_params['lang_href'] = '';//Алиас языка пустой
		$multilang_params['lang_href_no_slash'] = '';//Алиас языка пустой
		$multilang_params['lang_href_slash_after'] = '';//Алиас языка пустой
		
		//Получаем язык по-умолчанию
		$lang_query = $db_link->prepare('SELECT * FROM `lang_languages` WHERE `active` = ? AND `is_default` = ?;');
		$lang_query->execute( array(1,1) );
		$lang_record = $lang_query->fetch();
		
		//Таким образом, мультиязычность отключена. Есть единственный язык по-умолчанию. URL во фронтенде строятся без алиаса языка.
		$multilang_params['multilang'] = false;//Мультиязычность отключена
		$multilang_params['lang'] = $lang_record['lang_code'];//Рабочий язык
	}
	
	// Принудительный язык витрины (как backend_ui_lang для ПУ): только для фронта, см. config frontend_ui_lang
	if( isset($DP_Config->frontend_ui_lang) && $DP_Config->frontend_ui_lang !== null && $DP_Config->frontend_ui_lang !== '' )
	{
		$forced_front = trim( (string) $DP_Config->frontend_ui_lang );
		$lang_query = $db_link->prepare('SELECT COUNT(*) FROM `lang_languages` WHERE `active` = ? AND `lang_code` = ?;');
		$lang_query->execute( array(1, $forced_front) );
		if( $lang_query->fetchColumn() )
		{
			global $isFrontMode;
			$is_front_mode_here = 1;
			if( ! isset($DP_Core) )
			{
				if( isset($_SERVER['HTTP_REFERER']) && parse_url($DP_Config->domain_path, PHP_URL_HOST) == parse_url($_SERVER['HTTP_REFERER'], PHP_URL_HOST) )
				{
					$path = parse_url($_SERVER['HTTP_REFERER'], PHP_URL_PATH);
					$path = explode('/', $path);
					$path = isset($path[1]) ? $path[1] : '';
					if( $path == $DP_Config->backend_dir )
					{
						$is_front_mode_here = 0;
					}
				}
				if( $is_front_mode_here == 1 && isset($_SERVER['REQUEST_URI']) )
				{
					$req_path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
					if( $req_path !== null && $req_path !== false && preg_match('#^/' . preg_quote($DP_Config->backend_dir, '#') . '(/|$)#u', $req_path) )
					{
						$is_front_mode_here = 0;
					}
				}
			}
			else
			{
				$is_front_mode_here = $isFrontMode;
			}
			if( $is_front_mode_here )
			{
				$multilang_params['lang'] = $forced_front;
				setcookie("lang", $forced_front, time()+9999999, "/", '',false);
				if( !empty($multilang_params['multilang']) )
				{
					$multilang_params['lang_href'] = '/'.$forced_front;
					$multilang_params['lang_href_no_slash'] = $forced_front;
					$multilang_params['lang_href_slash_after'] = $forced_front.'/';
				}
			}
		}
	}
	
	$multilang_cache = $multilang_params;
	return $multilang_params;
}

// -------------------------------------------------------------------------------------------
?>