<?php
/*
Класс DocpartSuppliersAPI_Debug - для отладки API поставщиков

Описание работы:
Лог API поставщика предоставляет вывод результатов работы API при запросе одного артикула.
Файл лога создается во временной папке. Формат лога - php скрипт, в начале которого записывается php-код для защиты от просмотра в обход веб-интерфейса. Т.е. если напрямую обратиться к файлу лога через браузер - они ничего не покажет, поэтому в него можно выводить любую информацию.


Шаблон имени лога <id поставщика, т.е. id склада>.php


Одни поставщики работают по common_interface, а другие по get_manufacturers-get_suppliers.
При этом, каждый поставщик имеет свою особенность работы API. К примеру, для запроса списка производителей по артикулу у одного поставщика достаточно сделать только один запрос в API, а у другого нужно сделать целую последовательность запросов (например, получить список точек выдачи, получить список своих организаций и т.д.).

Чтобы с одной стороны обеспечить единый формат логов, а с другой учесть особенности каждого поставщика, принимается следующая архитектура работы логов.

У каждого поставщика в его протоколе API есть первый запрос и последний запрос.
А общее количество запросов может быть произвольным.

Абсолютно каждый запрос имеет следующую структуру данных для логов:
- текстовое описание запроса, например "Запрос списка производителей по артикулу" или "Запрос торговых организаций"
- текстовое описание параметров запроса (наиболее часто - это просто URL со всеми параметрами)
- текст ответа API без обработок, т.е. то, что возвращает API поставщика непосредственно
- обработанный ответ, т.е. ответ в виде объекта, который может быть более понятен человеку
Вспомогательные поля для лога по каждому запросу:
- Флаг "начать файл заново" (если да, то файл лога открывается для записи "w", если нет, то для "a")


Поэтому, логи целесообразно выводить в формате HTML - так, чтобы каждый запрос был выразительным и чтобы можно было легко прочитать его.


В каждом скрипте поставщика перед началом его работы создается объект DocpartSuppliersAPI_Debug, в котором изначально указываются следующие поля:
- storage_id (ID отлаживаемого поставщика, т.е. склада)
- имя скрипта, который его создал, например, armtek/get_manufacturers.php
- текстовое описание технологии или протокола по которому работает поставщик, например CURL-HTTP/HTTPS или SOAP.
Таким образом, объект лога будет знать, в какой файл лога писать, а имя скрипта, который его создал - тоже можно писать в лог для большей информативности, чтобы знать, в каком точно файле произошла ошибка.



Во всех методах класса есть проверка на активацию записи логов. Однако, при вызове методов логов все логируемые пемеменные должны передаваться в виде тестовых строк (даже если это объекты - их нужно преобразовывать в json перед тем, как передать в метод).
Поэтому РЕКОМЕНДУЕТСЯ вызовы методов обертывать в
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	
}
- чтобы не гонять впустую такие функции, как print_r, json_decode и т.д.


// -----------------------------------------------------------------------------------------

Общая последовательность использования:
- подключить данный класс
- создать объект (лучше всего создавать за пределами классов-обработчиков поставщиков)
- инициализировать параметры объекта
- создать файл лога
- далее можно логировать нужные данные, пользуясь методами, которые начинаются с log_ (в зависимости от вида логируемых данных).


При доработах этого класса - необходимо обеспечивать ПОЛНУЮ обратную совместимость.
ПОЭТОМУ, все методы, которые начинаются с log_ в качестве аргументов принимают только текстовые строки - каждый метод в разном количестве. Таким образом, все методы log_ в техническом плане - подобны, отличаются только символически - в зависимости от вида логируемых данных



// -----------------------------------------------------------------------------------------
ПРИМЕР ИСПОЛЬЗОВАНИЯ:

//ЛОГ - ПОДКЛЮЧЕНИЕ КЛАССА
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/DocpartSuppliersAPI_Debug.php");

(Подключается во всех файлах обработчиков, где нужно писать логи)

// ---------------

//Настройки подключения к складу
$storage_options = json_decode($_POST["storage_options"], true);

// ---------------

//ЛОГ - СОЗДАНИЕ ОБЪЕКТА
$DocpartSuppliersAPI_Debug = DocpartSuppliersAPI_Debug::getInstance();

(Создается во всех областях видимости переменных, где нужно писать лог. При этом - с помощью Singleton - объект создается единсвтвенным и поэтому, не нужно каждый раз вызывать init_object()
Создание объекта НЕ создает сам файл лога - для этого есть отдельный метод)

// ---------------

//ЛОГ - ИНИЦИАЛИЗАЦИЯ ПАРАМЕТРОВ ОБЪЕКТА
$DocpartSuppliersAPI_Debug->init_object( array("storage_id"=>$storage_options["storage_id"], "api_script_name"=>__FILE__, "api_type"=>"CURL-HTTP-JSON") );

(Этот метод вызывается единственный раз - вслед за первым созданием объекта лога)

// ---------------

//ЛОГ - СОЗДАНИЕ ФАЙЛА ЛОГА
$DocpartSuppliersAPI_Debug->start_log();

(вызывается в нужном месте - для каждого поставщика нужно выбирать отдельно)

// ---------------

//ЛОГ [API-запрос] (вся информация о запросе)
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_api_request("Получение списка брендов по артикулу ".$article, "http://portal.moskvorechie.ru/portal.api?l={$login}&p={$key}&act=brand_by_nr&nr={$article}&cs=utf8&name", $curl_result, print_r(json_decode($curl_result, true), true) );
}

// ---------------

//ЛОГ [ПЕРЕД API-запросом] (название запроса, запрос)
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_before_api_request("Получение списка товаров по артикулу ".$article, 'http://api-b2b.pwrs.ru/WCF/ClientService.svc?wsdl'."<br>Параметры: ".print_r($api_options, true) );
}

// ---------------

//ЛОГ [ПОСЛЕ API-запроса] (название запроса, ответ, обработанный ответ)
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_after_api_request("Получение списка товаров по артикулу ".$article, print_r( $suppliers_items , true ), print_r( $suppliers_items, true ) );
}

// ---------------

//ЛОГ - [СООБЩЕНИЕ] (простое сообщение в лог)
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_simple_message("Перед созданием SOAP-клиента");
}

// ---------------

//ЛОГ [РЕЗУЛЬТИРУЮЩИЙ ОБЪЕКТ - БРЭНДЫ]
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_supplier_handler_result("Список брендов", print_r($this->ProductsManufacturers, true) );
}

// ---

//ЛОГ [РЕЗУЛЬТИРУЮЩИЙ ОБЪЕКТ - ОСТАТКИ]
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_supplier_handler_result("Список остатков", print_r($this->Products, true) );
}

// ---------------

//ЛОГ - [ИСКЛЮЧЕНИЕ]
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_exception("Исключение", print_r($e, true) , $e->getMessage() );
}

// ---------------

//ЛОГ - [СООБЩЕНИЕ С ОШИБКОЙ]
if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
{
	$DocpartSuppliersAPI_Debug->log_error("Есть ошибка", print_r($error, true) );
}

// ---------------

Вывод ошибки для CURL:

if(curl_errno($ch))
{
	//ЛОГ - [СООБЩЕНИЕ С ОШИБКОЙ]
	if($DocpartSuppliersAPI_Debug->suppliers_api_debug)
	{
		$DocpartSuppliersAPI_Debug->log_error("CURL-ошибка", print_r(curl_error($ch), true) );
	}
}

// -----------------------------------------------------------------------------------------
ЗАГОТОВКА:

...

//ЛОГ - ПОДКЛЮЧЕНИЕ КЛАССА
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/DocpartSuppliersAPI_Debug.php");

...

//Настройки подключения к складу
$storage_options = json_decode($_POST["storage_options"], true);
//ЛОГ - СОЗДАНИЕ ОБЪЕКТА
$DocpartSuppliersAPI_Debug = DocpartSuppliersAPI_Debug::getInstance();
//ЛОГ - ИНИЦИАЛИЗАЦИЯ ПАРАМЕТРОВ ОБЪЕКТА
$DocpartSuppliersAPI_Debug->init_object( array("storage_id"=>$storage_options["storage_id"], "api_script_name"=>__FILE__, "api_type"=>"CURL-HTTP-JSON") );
//ЛОГ - СОЗДАНИЕ ФАЙЛА ЛОГА
$DocpartSuppliersAPI_Debug->start_log();

...

// -----------------------------------------------------------------------------------------
*/

// -----------------------------------------------------------------------------------------
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS
// -----------------------------------------------------------------------------------------
//Нужно для мультиязычности
if( !isset($db_link) )
{
	try
	{
		$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	}
	catch (PDOException $e) 
	{
		return;
	}
	$db_link->query("SET NAMES utf8;");
}
// -----------------------------------------------------------------------------------------
// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------




class DocpartSuppliersAPI_Debug
{
	//Поля
	public $api_type;//Текстовое описание технологии, по которой работает протокол, например CURL-HTTP/HTTPS
	public $storage_id;//ID отлаживаемого поставщика, т.е. склада
	public $supplier_caption;//Название поставщика (передается непосредственно скриптом поставщика)
	public $api_script_name;//Имя скрипта, который создал данный объект
	
	public $suppliers_api_debug;//Флаг 1 = записывать лог. Этот флаг желательно проверять везде, где нужно обратиться к методам этого класса - чтобы не вызывать впустую методы типа json_decode, print_r и т.д в скриптах common_interface, get_manufacturers, get_suppliers при преобразовании объектов в строки.
	
	
	// ------------------------------------------------------------------------
	
	public static $_instance;
	
	public static function getInstance()
    {
        if (self::$_instance != null) 
		{
			return self::$_instance;
		}
		
		self::$_instance = new self;
		
		return self::$_instance;
    }
	
	// ------------------------------------------------------------------------
	public function init_object($init_options)
	{
		global $db_link;
		
		//Инициализация объекта
		$this->api_type = $init_options["api_type"];
		$this->storage_id = (int)$init_options["storage_id"];
		$this->api_script_name = $init_options["api_script_name"];
		
		//Получем имя скрипта в виде <папка>/<имя файла>
		$this->api_script_name = explode("/", $this->api_script_name);
		if( count( $this->api_script_name ) == 1 )
		{
			$this->api_script_name = explode("\\", $this->api_script_name[0]);
		}
		$this->api_script_name = $this->api_script_name[ count($this->api_script_name) - 2 ]."/".$this->api_script_name[ count($this->api_script_name) - 1 ];
		
		
		//Конфиг CMS
		$DP_Config = new DP_Config;
		$this->suppliers_api_debug = (int)$DP_Config->suppliers_api_debug;
		
		
		
		//Получаем название склада (нужно соединение с БД, поэтому, чтобы не нагружать впустую - проверяем флаг)
		$this->supplier_caption = translate_str_by_id(4207);
		if( (int)$DP_Config->suppliers_api_debug == 1 )
		{
			$storage_caption_query = $db_link->prepare("SELECT `name` FROM `shop_storages` WHERE `id` = ?;");
			$storage_caption_query->execute( array($this->storage_id) );
			$storage_caption_record = $storage_caption_query->fetch();
			if( $storage_caption_record != false )
			{
				$this->supplier_caption = $storage_caption_record["name"];
			}
		}
	}
	// ------------------------------------------------------------------------
	//Конструктор
	private function __construct()
	{
		
	}
	// ------------------------------------------------------------------------
	//Метод записи запроса к API поставщика в лог
	/*
	$name - название запроса, например "Запрос списка производителей по артикулу" или "Запрос торговых организаций"
	$request - текст параметров запроса, обычно URL со всеми аргументами
	$answer - текст ответа API без обработки
	$answer_handled - текст ответа API после обработки, обычно объект в формате JSON
	*/
	public function log_api_request( $name, $request, $answer, $answer_handled )
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		
		$name = htmlentities($name);
		$answer = htmlentities($answer);
		$answer_handled = htmlentities($answer_handled);

		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#62cb31;color:#000;\">".translate_str_by_id(4208)." ".$this->api_script_name."): \"".$name."\" <div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#000;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4209).":</p><pre>".$request."</pre>");
		fwrite($log, "<p>".translate_str_by_id(4210).":</p><pre>".$answer."</pre>");
		fwrite($log, "<p>".translate_str_by_id(4211).":</p><pre>".$answer_handled."</pre>");

		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	/*
	Метод записи результирующего объекта в лог.
	
	//Если работает при $log_type = 2, то:
	$name - название результата запроса, например "Список производителей" или "Список товаров"
	$object_text - текст в формате JSON с результирующим объектом
	
	
	Файл лога открывается всегда в режиме "a", т.к. результирующий объект записывается обычно после запросов API
	*/
	public function log_supplier_handler_result($name, $object_text)
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		
		
		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог результата работы
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#4957b2;color:#FFF;\">".translate_str_by_id(4212)." (".$this->api_script_name."): \"".$name."\" <div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#FFF;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4213).":</p><pre>".$object_text."</pre>");

		
		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	//Метод логирования информации перед запросом (т.е. логируется сам запрос без ответа)
	public function log_before_api_request( $name, $request )
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		

		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#ecff47;color:#000;\">".translate_str_by_id(4214)." ".$this->api_script_name."): \"".$name."\" <div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#000;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4209).":</p><pre>".$request."</pre>");

		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	//Метод логирования информации после запроса (т.е. логируется ответ от запроса)
	public function log_after_api_request( $name, $answer, $answer_handled )
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		
		$name = htmlentities($name);
		$answer = htmlentities($answer);
		$answer_handled = htmlentities($answer_handled);

		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#ff47da;color:#FFF;\">".translate_str_by_id(4215)." ".$this->api_script_name."): \"".$name."\" <div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#FFF;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4210).":</p><pre>".$answer."</pre>");
		fwrite($log, "<p>".translate_str_by_id(4211).":</p><pre>".$answer_handled."</pre>");

		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	//Метод записи исключения - обычно, если запрос выполнен с исключением, то в штатном режиме не удастся его прологировать, поэтому - этот метод можно вызывать в catch(){}
	public function log_exception($name, $exception_object, $exception_message )
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		

		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#ff0000;color:#FFF;\">".translate_str_by_id(4216)." ".$this->api_script_name."): \"".$name."\" <div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#FFF;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4217).":</p><pre>".$exception_message."</pre>");
		fwrite($log, "<p>".translate_str_by_id(4218).":</p><pre>".$exception_object."</pre>");

		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	//Сообщение с ошибкой - выводится описание, что за ошибка и выводится строка, содержащая объект ошибки
	public function log_error($name, $error_object )
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		

		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#ff0000;color:#FFF;\">".translate_str_by_id(4219)." ".$this->api_script_name."): \"".$name."\" <div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#FFF;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4220).":</p><pre>".$error_object."</pre>");

		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	//Вывод простого сообщения в лог
	public function log_simple_message($message)
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		

		//Открываем файл в режиме "a" всегда
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "a" );
		
		
		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel panel-collapse\"><div class=\"panel-heading hbuilt\" style=\"background-color:#cecece;color:#000;\">".translate_str_by_id(4221)." ".$this->api_script_name.")<div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#000;\"></i></a></div></div><div class=\"panel-body\" style=\"display:none;\">");
		
		fwrite($log, "<p>".translate_str_by_id(4217).":</p><pre>".$message."</pre>");

		fwrite($log, "</div></div></div>");
		
		
		//Закрываем файл
		fclose($log);
	}
	// ------------------------------------------------------------------------
	//Метод начала нового лога, т.е. создаем файл с нуля
	public function start_log()
	{
		//Конфиг CMS
		$DP_Config = new DP_Config;
		
		//Если флаг не выставлен в true - логи не пишем
		if( (int)$DP_Config->suppliers_api_debug != 1 )
		{
			return;
		}
		
		
		//Открываем файл в режиме "w"
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$this->storage_id.".php", "w" );
		
		
		
		
		//Запрет просмотра вне веб-интерфейса
		fwrite($log, '<?php
defined(\'_ASTEXE_\') or die(\'No access\');
?>
');
			
			//Стиль для тега pre - чтобы отображать содержимое без прокрутки
		fwrite($log, "<style>
pre
{
	white-space: pre-wrap;
	white-space: -moz-pre-wrap;
	white-space: -pre-wrap;
	white-space: -o-pre-wrap;
	word-wrap: break-word;
	max-height: 600px;
}
</style>
");
			
		fwrite($log, "<div class=\"col-lg-12\"><div class=\"hpanel hgreen\"><div class=\"panel-heading hbuilt\">Информация о логе</div><div class=\"panel-body\">" );
		
		fwrite($log, date("<p>".translate_str_by_id(4222)." d.m.Y H:i:s</p>", time()) );
		fwrite($log, "<p>".translate_str_by_id(3606).": ".$this->supplier_caption."</p>" );
		fwrite($log, "<p>".translate_str_by_id(4223)." ".$this->api_script_name."</p>");
		fwrite($log, "<p>".translate_str_by_id(4224)." ".$this->api_type."</p>");
		
		fwrite($log, "</div></div></div>");
		
		fclose($log);
	}
	// ------------------------------------------------------------------------
}//~class DocpartSuppliersAPI_Debug
?>