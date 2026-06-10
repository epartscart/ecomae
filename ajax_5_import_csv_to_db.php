<?php
/**
 * Очередной шаг общего алгоритма загрузки прайс-листа "Загрузка файлов csv во таблицу в Базе данных"
 * 
 * Перед началом данного этапа во временном каталоге остаются только файлы формата csv(txt)
*/
header('Content-Type: application/json;charset=utf-8;');
set_time_limit(600);

function prepareString($string)
{
	$sweep=array("/", "#", "\r\n", "\r", "\n", "\t", "'", '"', "\\");
	$string = str_replace($sweep,"", $string);
	
	return $string;
}





//Конфигурация Treelax
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;



//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
    $answer["result"] = 0;
    $answer["message"] = "No DB connect";
    exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");

require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_price_upload_history.php");
$epc_uploaded_by = 0;

// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


if($DP_Config->tech_key !== $_GET['key'])
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------

	//Для работы с пользователями
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
	//Проверяем доступ в панель управления
	if( ! DP_User::isAdmin())
	{
		$answer = array();
		$answer["status"] = false;
		$answer["message"] = 'Forbidden';
		exit(json_encode($answer));
	}
	$epc_admin_session = DP_User::getAdminSession();
	$epc_uploaded_by = (int)($epc_admin_session['user_id'] ?? 0);
}




//Временный каталог
$work_dir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload;


//Получаем конфигурацию прайс-листа
$price_id = $_GET["price_id"];

$price_configuration_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices` WHERE `id` = ?;");
$price_configuration_query->execute( array($price_id) );
$price_configuration = $price_configuration_query->fetch();
$strings_to_left = $price_configuration["strings_to_left"];//Сколько строк пропустить
$csv_delimiter = $price_configuration["separator"];
if($csv_delimiter == '\t' || $csv_delimiter == '\\t')
{
	$csv_delimiter = "\t";
}
if($csv_delimiter == '')
{
	$csv_delimiter = ';';
}
//Составляем список задействованных колонок:
$operational_cols = array();
$all_cols_types_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices_cols_types`");
$all_cols_types_query->execute();
while($col_type = $all_cols_types_query->fetch() )
{
    //Составляем ассоциативый массив: "тип колонки" => "номер колонки в файле". Если такой колонки нет в файле, то значение равно 0
    $operational_cols[$col_type["name"]] = $price_configuration[$col_type["name"]."_col"];
}



//Инициатор обращения (js или cron)
$initiator = $_GET["initiator"];

//Определяем, нужно ли предварительной очищать таблицу
if($initiator == "js")//Если обращение из html страницы - флаг полностью обновить указан в аргументе
{
    $clean_before = false;
    if($_GET["clean_before"] != NULL)$clean_before = true;
}
else//обращение через cron - флаг "полностью обновить" указан в настройках конфигурации
{
    $clean_before = $price_configuration["clean_before"];
}

$next_id_query = $db_link->query("SELECT COALESCE(MAX(`id`), 0) FROM `shop_docpart_prices_data`;");
$next_id = (int)$next_id_query->fetchColumn();

//Предварительно очищаем таблицу назначения
if($clean_before)
{
    if( $db_link->prepare("DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = ?;")->execute( array($price_id) ) != true)
    {
        $answer = array();
        $answer["result"] = 0;
        $answer["message"] = translate_str_by_id(3687);
        exit(json_encode($answer));
    }
}

$SQL_sub = "";//Подстрока для SQL - cхема расположния колонок в файле

$epc_total_imported = 0;
$epc_archived_file = '';
$epc_archived_name = '';
$epc_archived_size = 0;

$dh = opendir($work_dir);//Открываем временный каталог
//Пробегаем по содержимому временного каталога
$first_file = true;//Флаг - работаем с первым файлом
while (false !== ($obj = readdir($dh)))
{
	if($obj=='.' || $obj=='..' || $obj=="index.html" ) 
	{
		continue;
	}
	else
	{
		//Открываем исходный файл
		$file = fopen($work_dir."/".$obj, "r");
		
		//Пропускаем требуемое количество строк
		for($i=0; $i < $strings_to_left; $i++)
		{
			fgetcsv($file, 0, $csv_delimiter);
		}
		
		//На время загрузки отключаем индексы
		if( $db_link->prepare("ALTER TABLE `shop_docpart_prices_data` DISABLE KEYS;")->execute() != true)
		{
			$answer = array();
			$answer["result"] = 0;
			$answer["message"] = translate_str_by_id(3111);
			closedir($dh);//Закрываем каталог
			exit(json_encode($answer));
		}
		
		//Готовим запрос
		$SQL = "INSERT INTO `shop_docpart_prices_data` (`id`,`price_id`,`manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`) VALUES ";
		$SQL_VALUES = '';
		$binding_values = array();
		
		$max = 10000;
		$n = 0;
		
		if ($epc_archived_file === '') {
			$epc_archived_name = $obj;
			$epc_archived_file = epc_price_history_archive_file($work_dir."/".$obj, (int)$price_id, $obj);
			if (is_file($work_dir."/".$obj)) {
				$epc_archived_size = (int)filesize($work_dir."/".$obj);
			}
		}

		//Читаем файл построчно.
		while (($current_record = fgetcsv($file, 0, $csv_delimiter)) !== false)
		{	
			if( !is_array($current_record) || (count($current_record) === 1 && trim((string)$current_record[0]) === '') )
			{
				continue;
			}
			$n++;
			$epc_total_imported++;
			if($SQL_VALUES != ''){
				$SQL_VALUES .= ', ';
			}
			$SQL_VALUES .= '(?,?,?,?,?,?,?,?,?,?,?)';
			$next_id++;
			
			//Получаем данные из строки
			$manufacturer = "";
			if( $operational_cols["manufacturer"] > 0 )
			{
				$manufacturer = prepareString($current_record[$operational_cols["manufacturer"]-1]);
				$manufacturer = trim($manufacturer);
			}
			$article = "";
			$article_show = "";
			if( $operational_cols["article"] > 0 )
			{
				$article = $current_record[$operational_cols["article"]-1];
				$article_show = prepareString($current_record[$operational_cols["article"]-1]);
				
				$sweep = array(" ", "-", "_", "`", "/", "'", '"', "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t");
				$article = str_replace($sweep, "", $article);
				$article = strtoupper($article);
			}
			$name = "";
			if( $operational_cols["name"] > 0 )
			{
				$name = prepareString($current_record[$operational_cols["name"]-1]);
				$name = trim($name);
			}
			$exist = 0;
			if( $operational_cols["exist"] > 0 )
			{
				$exist = (int) trim($current_record[$operational_cols["exist"]-1]);
			}
			$price = 0;
			if( $operational_cols["price"] > 0 )
			{
				$price = $current_record[$operational_cols["price"]-1];
				$price = str_replace(' ', '', $price);
				$price = str_replace(',', '.', $price);
				$price = (float) $price;
			}
			$time_to_exe = 0;
			if( $operational_cols["time_to_exe"] > 0 )
			{
				$time_to_exe = (int)$current_record[$operational_cols["time_to_exe"]-1];
				$time_to_exe = trim($time_to_exe);
			}
			$storage = "";
			if( $operational_cols["storage"] > 0 )
			{
				$storage = prepareString($current_record[$operational_cols["storage"]-1]);
				$storage = trim($storage);
			}
			$min_order = 0;
			if( $operational_cols["min_order"] > 0 )
			{
				$min_order = (int)$current_record[$operational_cols["min_order"]-1];
				$min_order = trim($min_order);
			}
			
			//Формируем строку
			$binding_values[] = $next_id;
			$binding_values[] = $price_id;
			$binding_values[] = $manufacturer;
			$binding_values[] = $article;
			$binding_values[] = $article_show;
			$binding_values[] = $name;
			$binding_values[] = $exist;
			$binding_values[] = $price;
			$binding_values[] = $time_to_exe;
			$binding_values[] = $storage;
			$binding_values[] = $min_order;
			
			if($n == $max){
				
				$INSERT_GENERAL_QUERY = $db_link->prepare($SQL.$SQL_VALUES);
				
				if( $INSERT_GENERAL_QUERY->execute($binding_values) != true)
				{
					$answer = array();
					$answer["result"] = 0;
					$answer["message"] = "SQL ".translate_str_by_id(2095)." (INSERT)";
					closedir($dh);//Закрываем каталог
					exit(json_encode($answer));
				}
				
				$n = 0;
				$SQL_VALUES = '';
				$binding_values = array();
			}
		}
		
		if($n > 0){
			
			$INSERT_GENERAL_QUERY = $db_link->prepare($SQL.$SQL_VALUES);
			
			if( $INSERT_GENERAL_QUERY->execute($binding_values) != true)
			{
				$answer = array();
				$answer["result"] = 0;
				$answer["message"] = translate_str_by_id(2524);
				closedir($dh);//Закрываем каталог
				exit(json_encode($answer));
			}
			
			$n = 0;
			$SQL_VALUES = '';
			$binding_values = array();
		}
		
		//Снова включаем индексы
		if( $db_link->prepare("ALTER TABLE `shop_docpart_prices_data` ENABLE KEYS;")->execute() != true)
		{
			$answer = array();
			$answer["result"] = 0;
			$answer["message"] = translate_str_by_id(3111);
			closedir($dh);//Закрываем каталог
			exit(json_encode($answer));
		}
		
		fclose($file);//Закрываем файл
		
	    //Удаляем файл csv
	    unlink($work_dir."/".$obj);//Удаляем файл
	}//else 1
}//~while 1
closedir($dh);//Закрываем каталог

$epc_rows_in_db = epc_price_history_count_items($db_link, (int)$price_id);
epc_price_history_save($db_link, array(
	'price_id' => (int)$price_id,
	'price_name' => (string)$price_configuration['name'],
	'upload_source' => 'cp_wizard',
	'original_filename' => $epc_archived_name,
	'stored_relpath' => $epc_archived_file,
	'file_size' => $epc_archived_size,
	'rows_imported' => $epc_total_imported,
	'rows_in_db' => $epc_rows_in_db,
	'status' => $epc_total_imported > 0 ? 'ok' : 'failed',
	'error_text' => $epc_total_imported > 0 ? '' : 'No rows imported',
	'uploaded_by' => $epc_uploaded_by,
));

$answer = array();
$answer["result"] = 1;
$answer["records_handled"] = $epc_total_imported;
$answer["records_in_db"] = $epc_rows_in_db;
exit(json_encode($answer));
?>