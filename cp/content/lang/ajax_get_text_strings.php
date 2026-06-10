<?php
//Серверный скрипт получения текстовых строк

// -------------------------------------------------------------------------------

//Конфигурация CMS
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

// -------------------------------------------------------------------------------
//Описание таблиц и колонок с мультиязыным контентом
require_once($_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/lang/lang_tabs_cols.php');
// -------------------------------------------------------------------------------

//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = "Forbidden";
	exit(json_encode($answer));//Вообще не является администратором бэкенда
}


// ------------------------------
//Проверка привелегий (пользователь должен иметь доступ к следующим страницам)
$pages_to_check = array();
$pages_to_check[] = array('url'=>'lang/editor', 'is_frontend' => 0);//Редактор переводов текстовых строк
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");
// ------------------------------


// -------------------------------------------------------------------------------

/*
Параметры для запроса:
- количество (LIMIT 100)
- начать с (т.е. порядковый номер строки из выборки по условиям)
- исключения строк (ID строк, которые были добавлены пользователем и уже отображаются на странице)
*/

// -------------------------------------------------------------------------------
//Получаем параметры запроса
$items_filter = json_decode($_POST['items_filter'], true);
$items_sort = json_decode($_POST['items_sort'], true);
$limit_from = $_POST['limit_from'];
$limit_count = $_POST['limit_count'];
$items_new = json_decode($_POST['items_new'], true);

// -------------------------------------------------------------------------------

//Получем массив со всеми языками платформы
$languages = array();
$languages_query = $db_link->prepare("SELECT * FROM `lang_languages`;");
$languages_query->execute();
while( $language = $languages_query->fetch() )
{
	$languages[] = $language['lang_code'];
}


// -------------------------------------------------------------------------------
//Формируем SQL-запрос. Кроме id и description из основной таблицы, получаем еще перечень языков, для которых нет переводом и для которых есть переводы.

$SQL = "SELECT * ";

//Для каждого языка в платформе добавляем колонку has_<язык> (т.е. имеет ли строка перевод для данного языка)
for( $i=0 ; $i < count($languages); $i++ )
{
	//$languages[$i] - считаем безопасным, т.к. берется из таблицы lang_languages, которая никак пользователями не редактируется
	
	$SQL .= ", (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `lang_code` = '".$languages[$i]."' AND `str_key` = `lang_text_strings`.`str_key` ) AS `has_".$languages[$i]."` ";
}

//Также получаем перевод строки на текущий язык сайта
$SQL .= ", (SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = `lang_text_strings`.`str_key` ) AS `current_lang_translation` ";

//Для режима "Две колонки"
if( $_POST['left_lang'] != '' && $_POST['right_lang'] != '' )
{
	$SQL .= ", (SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = `lang_text_strings`.`str_key` ) AS `left_lang_translation` ";
	$SQL .= ", (SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = `lang_text_strings`.`str_key` ) AS `right_lang_translation` ";
}

$SQL .= " FROM `lang_text_strings` ";

// -------------

//WHERE ПО ФИЛЬТРАМ
$SQL_WHERE = "";
$binding_values = array();
$binding_values[] = get_work_lang();//Первый параметр - текущий язык сайта

//Для режима "Две колонки"
if( $_POST['left_lang'] != '' && $_POST['right_lang'] != '' )
{
	$binding_values[] = $_POST['left_lang'];
	$binding_values[] = $_POST['right_lang'];
}


//STR_KEY
if( isset( $items_filter['str_key'] ) && $items_filter['str_key'] != null && $items_filter['str_key'] != '' )
{
	if( $items_filter['str_key_like'] == 1 )
	{
		$SQL_WHERE .= " `str_key` LIKE ? ";
		$binding_values[] = '%'.$items_filter['str_key'].'%';
	}
	else
	{
		$SQL_WHERE .= " `str_key` = ? ";
		$binding_values[] = $items_filter['str_key'];
	}
}
//DESCRIPTION
if( isset( $items_filter['description'] ) && $items_filter['description'] != null && $items_filter['description'] != '' )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	if( $items_filter['description_like'] == 1 )
	{
		$SQL_WHERE .= " `description` LIKE ? ";
		$binding_values[] = "%".$items_filter['description']."%";
	}
	else
	{
		$SQL_WHERE .= " `description` = ? ";
		$binding_values[] = $items_filter['description'];
	}
}
//ПЕРЕВОД
if( isset( $items_filter['translation'] ) && $items_filter['translation'] != null && $items_filter['translation'] != '' )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	
	if( $items_filter['translation_like'] == 1 )
	{
		$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `value` LIKE ? AND `str_key` = `lang_text_strings`.`str_key` ) > ? ";
		$binding_values[] = "%".$items_filter['translation']."%";
		$binding_values[] = 0;
	}
	else
	{
		$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `value` = ? AND `str_key` = `lang_text_strings`.`str_key` ) > ? ";
		$binding_values[] = $items_filter['translation'];
		$binding_values[] = 0;
	}
}
//ПО НАЛИЧИЮ ПЕРЕВОДОВ
if( isset( $items_filter['translation_progress'] ) && ( $items_filter['translation_progress'] != null && $items_filter['translation_progress'] != 0 ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	
	//Интересуют - Переведены на 100%
	if( $items_filter['translation_progress'] == 1 )
	{
		//Количество переводов равно количеству языков
		$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` ) = (SELECT COUNT(*) FROM `lang_languages`) ";
	}
	//Интересуют - Переведены частично
	else if( $items_filter['translation_progress'] == 2 )
	{
		//Количество переводов меньше количества языков, но, не равно 0
		$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` ) < (SELECT COUNT(*) FROM `lang_languages`) AND (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` ) != 0 ";
	}
	//Интересуют - вообще не переведены
	else if( $items_filter['translation_progress'] == 3 )
	{
		//Количество переводом равно 0
		$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` ) = 0 ";
	}
	//Интересуют - Не переведены, либо, переведены частично
	else if( $items_filter['translation_progress'] == 4 )
	{
		//Количество переводов меньше количества языков
		$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` ) < (SELECT COUNT(*) FROM `lang_languages`) ";
	}
}
//НЕТ ПЕРЕВОДОВ В
if( isset( $items_filter['no_translation_in'] ) && ( $items_filter['no_translation_in'] != null && $items_filter['no_translation_in'] != '0' ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	//Где нет перевода этой строки на указанный язык
	$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` AND `lang_code` = ? ) = 0 ";
	$binding_values[] = $items_filter['no_translation_in'];
}
//ЕСТЬ ПЕРЕВОДЫ
if( isset( $items_filter['has_translation_in'] ) && ( $items_filter['has_translation_in'] != null && $items_filter['has_translation_in'] != '0' ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	//Где есть перевод этой строки на указанный язык
	$SQL_WHERE .= " (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` AND `lang_code` = ? ) = 1 ";
	$binding_values[] = $items_filter['has_translation_in'];
}

//ОДИНАКОВЫЙ ПЕРЕВОД НА ВСЕ ЯЗЫКИ
if( isset( $items_filter['same'] ) && ( $items_filter['same'] != null && $items_filter['same'] != '0' ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	if( $items_filter['same'] == 1 )
	{
		//Все строки, где одинаковый перевод
		$SQL_WHERE .= " `same` IS NOT NULL ";
		//$binding_values[] = NULL;
	}
	else if( $items_filter['same'] == 2 )
	{
		//Все строки, где отдельный перевод на каждый язык
		$SQL_WHERE .= " `same` IS NULL ";
		//$binding_values[] = NULL;
	}
	else
	{
		//Строки с одинаковым переводом, который берется с определенного языка
		$SQL_WHERE .= " `same` = ? ";
		$binding_values[] = $items_filter['same'];
	}
}

//Тип строки "Системная"/"Кастомная"
if( isset( $items_filter['is_custom'] ) && ( $items_filter['is_custom'] == '0' || $items_filter['is_custom'] == '1' ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	$SQL_WHERE .= " `is_custom` = ? ";
	$binding_values[] = $items_filter['is_custom'];
}

//ИСПОЛЬЗОВАНИЕ СТРОКИ - Используется/Не используется/Использование не искалось
if( isset( $items_filter['used_found'] ) && ( $items_filter['used_found'] == '0' || $items_filter['used_found'] == '1' || $items_filter['used_found'] == '2' ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	$SQL_WHERE .= " `used_found` = ? ";
	$binding_values[] = $items_filter['used_found'];
}


//ЯВЛЯЕТСЯ ТЕКСТОМ ОШИБКИ
if( isset( $items_filter['is_error'] ) && ( $items_filter['is_error'] != null && $items_filter['is_error'] != '0' ) )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	if( $items_filter['is_error'] == 2 )
	{
		$items_filter['is_error'] = 0;//2 в фильтре соответстует 0 в таблице
	}
	
	$SQL_WHERE .= " `is_error` = ? ";
	$binding_values[] = $items_filter['is_error'];
}

//ПО ИСПОЛЬЗОВАНИЮ СТРОК В ОПРЕДЕЛЕННЫХ ТАБЛИЦАХ И В ОПРЕДЕЛЕННЫХ КОЛОНКАХ
if( isset( $items_filter['table'] ) && $items_filter['table'] != null && $items_filter['table'] != '0' )
{
	//Зашли сюда, значит выставлен селектор таблиц. Проверяем наличие этой таблицы в $lang_tabs_cols (для исключения SQL-инъекций)
	if( !isset( $lang_tabs_cols[$items_filter['table']] ) || !is_array( $lang_tabs_cols[$items_filter['table']] ) )
	{
		exit;
	}
	
	//Указаная таблица есть в массиве.
	
	//Если пользователь дополнительно указан определенную колонку, то, искать строку будем только в этой колонке. Если пользователь не указал колонку, то, будем искать строку во всех колонках данной таблицы, указанных в $lang_tabs_cols[$items_filter['table']]
	
	$tab_cols = array();
	if( isset( $items_filter['column'] ) && $items_filter['column'] != null && $items_filter['column'] != '0' )
	{
		//Пользователь указал колонку в селекторе колонок. Проверяем имя колонки, чтобы исключить SQL-инъекции
		if( !isset( $lang_tabs_cols[$items_filter['table']][ $items_filter['column'] ] ) || ( $lang_tabs_cols[$items_filter['table']][ $items_filter['column'] ] != 'text' && $lang_tabs_cols[$items_filter['table']][ $items_filter['column'] ] != 'json' ) )
		{
			exit;
		}
		
		//Такая колонка есть в массиве
		
		//Добавляем колонку в массив для формирования условия в SQL-запросе
		$tab_cols = array( $items_filter['column'] => $lang_tabs_cols[$items_filter['table']][ $items_filter['column'] ] );
	}
	else
	{
		//Поиск строки будет по всем колонкам, указанным для данной таблицы
		$tab_cols = $lang_tabs_cols[$items_filter['table']];
	}
	
	
	//Формируем условия в SQL-запрос
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	$SQL_WHERE .= " ( ";
	$more_one = 0;
	foreach( $tab_cols AS $col => $type )
	{
		if( $more_one == 1 )
		{
			$SQL_WHERE .= " OR ";
		}
		
		if( $type == 'text' )
		{
			$SQL_WHERE .= " (SELECT COUNT(*) FROM `".$items_filter['table']."` WHERE `".$col."` = `lang_text_strings`.`str_key` ) > 0 ";
		}
		else
		{
			$SQL_WHERE .= " (SELECT COUNT(*) FROM `".$items_filter['table']."` WHERE `".$col."` LIKE CONCAT('%\"',`lang_text_strings`.`str_key`,'\"%') ) > 0 ";
		}
		
		$more_one = 1;
	}
	$SQL_WHERE .= " ) ";
}




//ИСКЛЮЧЕНИЯ НОВЫХ СТРОК, КОТОРЫЕ УЖЕ ОТОБРАЖЕНЫ ПОЛЬЗОВАТЕЛЮ
if( isset( $items_new ) && $items_new != null && is_array($items_new) && count($items_new)>0 )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	$SQL_WHERE .= " `str_key` NOT IN (";
	for( $i=0 ; $i < count($items_new) ; $i++ )
	{
		if( $i > 0 )
		{
			$SQL_WHERE .= ", ";
		}
		
		$SQL_WHERE .= "?";
		
		$binding_values[] = $items_new[$i];
	}
	$SQL_WHERE .= ") ";
}


//Для ограниченного режима работы редактора не показываем кастомные строки
if( $DP_Config->multilang_editor_restricted_mode )
{
	if( $SQL_WHERE != "" )
	{
		$SQL_WHERE .= " AND ";
	}
	
	$SQL_WHERE .= " `is_custom` = 0 ";
}


if( $SQL_WHERE != "" )
{
	$SQL_WHERE = " WHERE ".$SQL_WHERE;
}
// -------------

//Проверяем параметры для LIMIT (должны быть целыми числами, не отрицательными. from может быть 0. count не может быть 0)
if( 
	$limit_from != (int)$limit_from || 
	$limit_count != (int)$limit_count || 
	$limit_from < 0 || 
	( $limit_count <= 0 || $limit_count > 5000 )
)
{
	exit;
}
$limit_from = (int)$limit_from;
$limit_count = (int)$limit_count;

// -------------

//Проверяем папаметры сортировки (поле только из списка допустимых. Направление - одно из двух)
if( 
	array_search( $items_sort['field'], array('str_key', 'description', 'current_lang_translation') ) === false ||
	array_search( $items_sort['asc_desc'], array('asc', 'desc') ) === false
 )
{
	exit;
}

// -------------


$SQL .= $SQL_WHERE." ORDER BY `".$items_sort['field']."` ".$items_sort['asc_desc']." LIMIT ".$limit_from.", ".$limit_count.";";

// -------------------------------------------------------------------------------
//Выполняем запрос

$items = array();

$strings_query = $db_link->prepare($SQL);
$strings_query->execute( $binding_values );
while( $item = $strings_query->fetch() )
{
	$item['description'] = htmlentities($item['description']);
	$item['current_lang_translation'] = htmlentities($item['current_lang_translation']);
	
	
	$items[] = $item;
}




$answer = array();
$answer["SQL"] = $SQL;
$answer["debug"] = json_encode($items_filter);
$answer["status"] = true;
$answer["message"] = "OK";
$answer["items"] = $items;
exit(json_encode($answer));
?>