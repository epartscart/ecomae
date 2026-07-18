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
		$disabledStorages = function_exists('epc_ssf_disabled_storage_ids') ? epc_ssf_disabled_storage_ids($db_link) : array();
		$disabledPrices = function_exists('epc_ssf_disabled_price_ids') ? epc_ssf_disabled_price_ids($db_link) : array();

		$storageIds = array();
		$bunchPairs = array();
		foreach ($office_storage_bunches as $office_storage_bunch) {
			$sid = (int) ($office_storage_bunch['storage_id'] ?? 0);
			$oid = (int) ($office_storage_bunch['office_id'] ?? 0);
			if ($sid < 1 || isset($disabledStorages[$sid])) {
				continue;
			}
			$storageIds[$sid] = true;
			$bunchPairs[] = array($oid, $sid);
		}

		$priceToPairs = array();
		$price_ids_for_direct_check = array();
		if ($storageIds) {
			$idList = array_keys($storageIds);
			$ph = implode(',', array_fill(0, count($idList), '?'));
			$storage_check_query = $db_link->prepare('SELECT `id`, `connection_options` FROM `shop_storages` WHERE `id` IN (' . $ph . ')');
			$storage_check_query->execute($idList);
			$storagePriceMap = array();
			while ($storage_check_record = $storage_check_query->fetch(PDO::FETCH_ASSOC)) {
				$connection_check_options = json_decode((string) ($storage_check_record['connection_options'] ?? ''), true);
				if (empty($connection_check_options['price_id'])) {
					continue;
				}
				$pid = (int) $connection_check_options['price_id'];
				if ($pid < 1 || isset($disabledPrices[$pid])) {
					continue;
				}
				$storagePriceMap[(int) $storage_check_record['id']] = $pid;
				$price_ids_for_direct_check[$pid] = true;
			}
			foreach ($bunchPairs as $pair) {
				$oid = $pair[0];
				$sid = $pair[1];
				if (!isset($storagePriceMap[$sid])) {
					continue;
				}
				$pid = $storagePriceMap[$sid];
				if (!isset($priceToPairs[$pid])) {
					$priceToPairs[$pid] = array();
				}
				$priceToPairs[$pid][] = array($oid, $sid);
			}
		}
		$price_ids_for_direct_check = array_keys($price_ids_for_direct_check);
		$direct_stock_exists = false;
		if (!empty($price_ids_for_direct_check)) {
			$price_placeholders = str_repeat('?,', count($price_ids_for_direct_check) - 1) . '?';
			$direct_check_query = $db_link->prepare(
				'SELECT 1 FROM `shop_docpart_prices_data` WHERE ' . $art_expr . ' = ? AND `price_id` IN (' . $price_placeholders . ') LIMIT 1'
			);
			$direct_check_values = array_merge(array($article_norm), $price_ids_for_direct_check);
			try {
				@$db_link->exec('SET SESSION max_statement_time = 2');
				@$db_link->exec('SET SESSION MAX_EXECUTION_TIME = 2000');
			} catch (Throwable $e) {
			}
			$direct_check_query->execute($direct_check_values);
			$direct_stock_exists = (bool) $direct_check_query->fetchColumn();
		}
		$article_candidates_for_query = $direct_stock_exists ? array($article_norm) : $article_candidates;
		if (empty($article_candidates_for_query) || empty($priceToPairs)) {
			$this->status = true;
			return;
		}
		$article_placeholders = str_repeat('?,', count($article_candidates_for_query) - 1) . '?';
		$priceIdList = array_keys($priceToPairs);
		$price_placeholders = implode(',', array_map('intval', $priceIdList));
		$SQL = 'SELECT `manufacturer`, `name`, `price_id` FROM `shop_docpart_prices_data`
			WHERE ' . $art_expr . ' IN (' . $article_placeholders . ')
			AND `price_id` IN (' . $price_placeholders . ')';
		$products_query = $db_link->prepare($SQL);
		$products_query->execute($article_candidates_for_query);
		while ($product = $products_query->fetch(PDO::FETCH_ASSOC)) {
			$price_id = (int) ($product['price_id'] ?? 0);
			if (!isset($priceToPairs[$price_id])) {
				continue;
			}
			$epc_brand_rule = epc_pricing_get_brand_rule($db_link, $group_id, $product['manufacturer']);
			if ((int) $epc_brand_rule['visible'] === 0) {
				continue;
			}
			foreach ($priceToPairs[$price_id] as $pair) {
				$office_id = $pair[0];
				$storage_id = $pair[1];
				$DocpartManufacturer = new DocpartManufacturer(
					$product['manufacturer'],
					0,
					$product['name'],
					$office_id,
					$storage_id,
					true,
					array('type' => 'prices')
				);
				if ($DocpartManufacturer->valid === true) {
					$hash = md5($DocpartManufacturer->manufacturer . '|' . $storage_id . '|' . $office_id);
					if (!isset($hashes[$hash])) {
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