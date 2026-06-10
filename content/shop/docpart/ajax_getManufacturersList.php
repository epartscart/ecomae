<?php
/**
 * Серверный скрипт для получения списка производителей по артикулу от одной связки офис-склад
*/
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация Treelax
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");


class ManufacturersList//Класс ответа
{
    public $status;//Рузультат работы (1 - успешно, 0 - не успешно)
    public $message;//Сообщение
	public $time;//Время запроса
	public $ProductsManufacturers = array();//Список объектов DocpartManufacturer
    public $storage;//ID склада для проценки
    public function __construct($query, $office_id, $storage_id, $geo_id, $db_link, $DP_Config) {
		
		$this->storage = $storage_id;

        //1. Получаем данные склада: настройки подключения, валюту и имя каталога, в котором находится скрипт-обработчик
        $SQL_storage_interface = "
		SELECT
			`shop_storages`.`connection_options` AS `connection_options`,
			`shop_storages`.`currency` AS `currency`,
			`shop_storages_interfaces_types`.`handler_folder` AS `handler_folder`
		FROM
			`shop_storages`
		INNER JOIN 
			`shop_storages_interfaces_types` ON `shop_storages`.`interface_type` = `shop_storages_interfaces_types`.`id`
		WHERE
		`shop_storages`.`id` = :storage_id;
		";
		
		$sth = $db_link->prepare($SQL_storage_interface);
		
		$sth->bindParam(':storage_id', $storage_id);
		$sth->execute();
		$storage_record = $sth->fetch();
		
		
        $handler_folder = $storage_record["handler_folder"];
        $storage_options = json_decode($storage_record["connection_options"], true);//Настройки для обработчика поставщика
        $storage_options["office_id"] = $office_id;
        $storage_options["storage_id"] = $storage_id;
		
		if($handler_folder === 'treelax_catalogue'){
			
			if(empty($geo_id)){
				//Получаем географический узел покупателя
				$geo_id = $_COOKIE["my_city"];

				//Куки не были еще выставлены - выводим для самого первого гео-узла, чтобы хоть что-то показать
				if($geo_id == NULL){
					
					$sth = $db_link->query("SELECT MIN(`id`) AS `id` FROM `shop_geo`");
					$sth->execute();
					$min_geo_id_record = $sth->fetch();
					
					$geo_id = $min_geo_id_record["id"];
					
				}
			}
			
			$storage_options["geo_id"] = $geo_id;
		}
		
		// ----------------------------------------------------------------------------------------------
        
		//2. Отправляем запрос в протокол поставщика
		$postdata = http_build_query(
			array(
				'article' => $query["article"],//Артикул
				'storage_options' => json_encode($storage_options)//Настройки подключения
			)
		);//Аргументы
		
		$curl = curl_init();
		curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/docpart/suppliers_handlers/".$handler_folder."/get_manufacturers.php");
		curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($curl, CURLOPT_HEADER, 0);
		curl_setopt($curl, CURLOPT_POST, true);
		curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, (int) $DP_Config->max_time_answer_storage); 
		curl_setopt($curl, CURLOPT_TIMEOUT, (int) $DP_Config->max_time_answer_storage);
		curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
		curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
		curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
		curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
		$exec = curl_exec($curl);
		curl_close($curl);
		
		$curl_result = json_decode($exec, true);
        
        if( !isset($curl_result["status"]) || (isset($curl_result["status"]) && $curl_result["status"] == false)) {
			
            $this->status = 0;
            $this->message = "Storage handler error (get manufacturers)";
            return;
			
        }
		
		//Фильтруем повторяющихся
		$local_manufacturers = array();
		
		$hashes = array();
		
		foreach ($curl_result["ProductsManufacturers"] as $c_m){
			if($c_m["valid"] === true){
				//Получаем id и название
				$c_m_id = $c_m["manufacturer_id"];
				$c_m_m = $c_m["manufacturer"];
				
				//Получаем хеш
				$hash = md5($c_m_id.$c_m_m);
				
				//Поиск хеша
				if ( ! isset($hashes[$hash]) ) {
					
					array_push($local_manufacturers, $c_m);
					
					$hashes[$hash] = true;
					
				}
			}
		}
		
		$this->ProductsManufacturers = $local_manufacturers;
		
		// ----------------------------------------------------------------------------------------------
		
        $this->status = true;
    }//~__construct
}//~class ManufacturersList//Класс ответа

$time_start = microtime(true);
$DP_Config = new DP_Config();//Конфигурация CMS

//Подключение к БД
$dsn = "mysql:dbname={$DP_Config->db};host={$DP_Config->host}";
$user = $DP_Config->user;
$password = $DP_Config->password;

try 
{
    $db_link = new PDO($dsn, $user, $password);
	$db_link->query("SET NAMES utf8;");
	
	$ManufacturersList = new ManufacturersList(
		json_decode($_POST["query"], true), 
		$_POST["office_id"], 
		$_POST["storage_id"], 
		$_POST["geo_id"], 
		$db_link, 
		$DP_Config
	);
}
catch (PDOException $e) 
{	
	exit();	
}

$time_end = microtime(true);
$ManufacturersList->time = number_format(($time_end - $time_start), 3, '.', '');

if (isset($_POST["is_async_search"]) && $_POST["is_async_search"] == 1)
{
	//СИНОНИМЫ
	$synonyms = array();
	$synonym_query = $db_link->prepare("SELECT `synonym`, (SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = `shop_docpart_manufacturers_synonyms`.`manufacturer_id`) AS 'name' FROM `shop_docpart_manufacturers_synonyms`;");
	$synonym_query->execute();
	while($synonym_record = $synonym_query->fetch()){
		$synonyms[mb_strtoupper(str_replace('"',"'",$synonym_record["synonym"]), 'UTF-8')] = mb_strtoupper(str_replace('"',"'",$synonym_record["name"]), 'UTF-8');
	}

	foreach($ManufacturersList->ProductsManufacturers as &$manufacturer)
	{
		$synonym = null;
		
		if( isset($synonyms[mb_strtoupper(str_replace('"',"'",$manufacturer["manufacturer"]), 'UTF-8')]) )
		{
			$synonym = $synonyms[mb_strtoupper(str_replace('"',"'",$manufacturer["manufacturer"]), 'UTF-8')];
		}
		else
		{
			$synonym = "";
		}
		if(!empty($synonym))
		{
			$manufacturer['manufacturer_show'] = mb_strtoupper($synonym, 'UTF-8');
		}
	}
}

exit(json_encode($ManufacturersList));
?>