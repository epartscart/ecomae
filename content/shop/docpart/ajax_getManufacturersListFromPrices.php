<?php
/**
 * Серверный скрипт для получения списка производителей по артикулу от прайс листов
*/
header('Content-Type: application/json;charset=utf-8;');

//Конфигурация Treelax
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS

//Класс бренда
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/DocpartManufacturer.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/docpart_article_match.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_storage_flags.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_pricing.php");

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

class prices_enclosure
{
	public $status;
	public $time;//Время запроса
	public $ProductsManufacturers = array();//Список брендов
    public $storage = "Прайс-листы";
	
	public function __construct($query, $office_storage_bunches, $DP_Config)
	{
		global  $db_link;

		/*****Учетные данные*****/
		$article = isset($query["article"]) ? $query["article"] : "";
		$group_id = isset($_POST["group_id"]) ? (int)$_POST["group_id"] : 0;
		$article_norm = docpart_normalize_article_for_price($article);
		/*****Учетные данные*****/

		// По каждой связке офис–склад отдельно: несколько складов могут ссылаться на один price_id
		// (разные поставщики с одним прайсом). Старый код держал один storage на price_id и схлопывал бренды по md5(manufacturer).
		$hashes = array();
		$art_expr = function_exists('docpart_sql_article_match_expr')
			? docpart_sql_article_match_expr($db_link, '`article`')
			: docpart_sql_article_normalized_expr('`article`');
		$article_candidates = docpart_collect_article_candidates(
			$db_link,
			$article_norm,
			!empty($DP_Config->local_crosses)
		);
		$price_ids_for_direct_check = array();
		foreach($office_storage_bunches as $office_storage_bunch)
		{
			$storage_check_query = $db_link->prepare('SELECT `connection_options` FROM `shop_storages` WHERE `id` = ?;');
			$storage_check_query->execute(array((int)$office_storage_bunch["storage_id"]));
			$storage_check_record = $storage_check_query->fetch();
			if($storage_check_record == false)
			{
				continue;
			}
			$connection_check_options = json_decode($storage_check_record["connection_options"], true);
			if(!empty($connection_check_options["price_id"]))
			{
				$price_ids_for_direct_check[] = (int)$connection_check_options["price_id"];
			}
		}
		$price_ids_for_direct_check = array_unique($price_ids_for_direct_check);
		$direct_stock_exists = false;
		if(!empty($price_ids_for_direct_check))
		{
			$price_placeholders = str_repeat('?,', count($price_ids_for_direct_check) - 1) . '?';
			$direct_check_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE " . $art_expr . " = ? AND `price_id` IN (" . $price_placeholders . ") LIMIT 1;");
			$direct_check_values = array_merge(array($article_norm), $price_ids_for_direct_check);
			$direct_check_query->execute($direct_check_values);
			$direct_stock_exists = ((int)$direct_check_query->fetchColumn() > 0);
		}
		$article_candidates_for_query = $direct_stock_exists ? array($article_norm) : $article_candidates;
		$article_placeholders = str_repeat('?,', count($article_candidates_for_query) - 1) . '?';
		$cnt = count($office_storage_bunches);
		for($i=0; $i < $cnt; $i++)
		{
			$storage_id = (int)$office_storage_bunches[$i]["storage_id"];
			$office_id = (int)$office_storage_bunches[$i]["office_id"];

			if (epc_ssf_is_storage_disabled($db_link, $storage_id)) {
				continue;
			}

			$storage_query = $db_link->prepare('SELECT `connection_options` FROM `shop_storages` WHERE `id` = ?;');
			$storage_query->execute( array($storage_id) );
			$storage_record = $storage_query->fetch();
			if( $storage_record == false )
			{
				continue;
			}
			$connection_options = json_decode($storage_record["connection_options"], true);
			if( empty($connection_options["price_id"]) )
			{
				continue;
			}
			$price_id = (int)$connection_options["price_id"];
			if (epc_ssf_storage_disabled_by_price($db_link, $price_id)) {
				continue;
			}

			$SQL = "SELECT * FROM `shop_docpart_prices_data` WHERE " . $art_expr . " IN (" . $article_placeholders . ") AND `price_id` = ?";
			$products_query = $db_link->prepare( $SQL );
			$binding_values = $article_candidates_for_query;
			$binding_values[] = $price_id;
			$products_query->execute( $binding_values );

			while($product = $products_query->fetch())
			{
				$epc_brand_rule = epc_pricing_get_brand_rule($db_link, $group_id, $product["manufacturer"]);
				if((int)$epc_brand_rule["visible"] === 0)
				{
					continue;
				}
				$DocpartManufacturer = new DocpartManufacturer(
					$product["manufacturer"],
					0,
					$product["name"],
					$office_id,
					$storage_id,
					true,
					array('type'=>'prices')
				);

				if($DocpartManufacturer->valid === true)
				{
					$hash = md5($DocpartManufacturer->manufacturer . '|' . $storage_id . '|' . $office_id);
					if (!isset($hashes[$hash]))
					{
						array_push($this->ProductsManufacturers, $DocpartManufacturer);
						$hashes[$hash] = true;
					}
				}
			}
		}
		
		
        $this->status = true;
	}//~function __construct($article)
};//~class prices_enclosure


$time_start = microtime(true);


$ManufacturersList = new prices_enclosure( 
	json_decode($_POST["query"], true), 
	json_decode($_POST["office_storage_bunches"], true ), 
	$DP_Config
);



$time_end = microtime(true);
$ManufacturersList->time = number_format(($time_end - $time_start), 3, '.', '');

if (isset($_POST["is_async_search"]) && $_POST["is_async_search"] == 1)
{
	try 
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