<?php
/**
 * Серверный скрипт для получения списка производителей по артикулу от сервера кроссов
*/
header('Content-Type: application/json;charset=utf-8;');
//Конфигурация Treelax
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS

//Класс для продукта
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/DocpartManufacturer.php");


class ManufacturersList//Класс ответа
{
    public $status;//Рузультат работы (1 - успешно, 0 - не успешно)
    public $message;//Сообщение
    public $time;//Время запроса
	public $ProductsManufacturers = array();//Список объектов DocpartManufacturer
    public $storage = "Сервер кроссов";
    
    public function __construct($query, $DP_Config)
    {
		$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $query["article"]), "UTF-8");
		
        if(!empty($article))
		{
			//Получаем список производителей с сервера Ucats
			if($DP_Config->ucats_crosses && $DP_Config->list_brends_crosses)
			{
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, "http://ucats.ru/ucats/crosses/get_parts_by_article.php?article=".$article."&login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password);
				curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
				curl_setopt($curl, CURLOPT_HEADER, 0);
				curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, 10); 
				curl_setopt($curl, CURLOPT_TIMEOUT, 10);
				curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
				curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
				curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
				$curl_result = curl_exec($curl);
				curl_close($curl);
				
				$curl_result = json_decode($curl_result, true);

				if( $curl_result["status"] == "ok" )
				{
					//Фильтруем повторяющихся
					$hashes = array();
					
					for($i=0; $i < count($curl_result["parts"]); $i++)
					{
						$DocpartManufacturer = new DocpartManufacturer($curl_result["parts"][$i]["manufacturer"],
							0,
							$curl_result["parts"][$i]["name"],
							0,
							0,
							true,
							array('type'=>'server')
						);
						
						if($DocpartManufacturer->valid === true){
							//Получаем хеш
							$hash = md5($DocpartManufacturer->manufacturer);
							
							//Поиск хеша
							if (!isset($hashes[$hash])){
								array_push($this->ProductsManufacturers, $DocpartManufacturer);
								$hashes[$hash] = true;
							}
						}
					}
				}
			}
			
			
			
			//Получаем список производителей из локальной таблицы кроссов
			if($DP_Config->local_crosses && $DP_Config->list_brends_crosses)
			{
				// --------------------------------------------------------------------------------------
				//Подключение к БД
				try
				{
					$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
				}
				catch (PDOException $e) 
				{
					return;
				}
				$db_link->query("SET NAMES utf8;");
				// --------------------------------------------------------------------------------------
				
				$analogs_query = $db_link->prepare('SELECT * FROM `shop_docpart_articles_analogs_list` WHERE `article` = ? OR `analog` = ?');
				$analogs_query->execute( array($article, $article) );
				while( $analog_record = $analogs_query->fetch() )
				{
					$manufacturer = '';
					
					if($article === $analog_record['article'])
					{
						$manufacturer = $analog_record["manufacturer_article"];
					}
					else if($article === $analog_record['analog'])
					{
						$manufacturer = $analog_record["manufacturer_analog"];
					}
					
					if($manufacturer != '')
					{
						$DocpartManufacturer = new DocpartManufacturer($manufacturer,
							0,
							'',
							0,
							0,
							true,
							array('type'=>'table')
						);
						
						if($DocpartManufacturer->valid === true){
							
							//Получаем хеш
							$hash = md5($DocpartManufacturer->manufacturer);
							
							//Поиск хеша
							if(!isset($hashes[$hash])){
								array_push($this->ProductsManufacturers, $DocpartManufacturer);
								$hashes[$hash] = true;
							}
							
						}
					}
				}
			}
		}
		
		// ----------------------------------------------------------------------------------------------
		
        $this->status = true;
    }//~__construct
}//~class ManufacturersList//Класс ответа


$time_start = microtime(true);
$ManufacturersList = new ManufacturersList(json_decode($_POST["query"], true), $DP_Config);
$time_end = microtime(true);
$ManufacturersList->time = number_format(($time_end - $time_start), 3, '.', '');

if (isset($_POST["is_async_search"]) && $_POST["is_async_search"] == 1)
{
	//Подключение к БД
	$dsn = "mysql:dbname={$DP_Config->db};host={$DP_Config->host}";
	$user = $DP_Config->user;
	$password = $DP_Config->password;

	try 
	{
		$db_link = new PDO($dsn, $user, $password);
		$db_link->query("SET NAMES utf8;");
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
			$manufacturer = (array)$manufacturer;

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
	catch (PDOException $e) 
	{	
		exit();	
	}
}

exit(json_encode($ManufacturersList));
?>