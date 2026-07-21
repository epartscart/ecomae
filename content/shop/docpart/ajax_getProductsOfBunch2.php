<?php
/**
 * Серверный скрипт для получения данных о товарах одной связки (офис-склад)
*/
header('Content-Type: application/json;charset=utf-8;');
header('X-Robots-Tag: noindex, nofollow, noarchive');
header('Cache-Control: no-store');
//Конфигурация Treelax
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");


//Соединение с основной БД
$DP_Config = new DP_Config;//Конфигурация CMS
//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
	exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");


//Класс продукта
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/DocpartProduct.php");


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_anti_crawl.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_prices_helpers.php");

$epc_anti_crawl = epc_storefront_anti_crawl_enforce($DP_Config, array(
	'bucket' => 'getProductsOfBunch2',
	'guest_max' => 24,
	'user_max' => 90,
	'window' => 60,
));



class ProductsOfBunch//Класс ответа
{
    public $result;//Рузультат работы (1 - успешно, 0 - не успешно)
    public $message;//Сообщение
	public $Products = array();//Список товаров
    
    
    public $user_id = 0;//ID пользователя
    public $group_id = 0;//Группа пользователя
    
    public function __construct($article, $office_id, $storage_id, $db_link, $DP_Config, $user_id, $this_article)
    {
        //0. Получаем данные по пользователю
        $this->user_id = $user_id;//ID пользователя
        $userProfile = DP_User::getUserProfileById($user_id);//Профиль пользователя
        $this->group_id = $userProfile["groups"][0];//Первая группа пользователя. Если у пользователя несколько групп - работаем только с первой
        
		// ----------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------
		// ----------------------------------------------------------------------------------------------
		//Если $office_id и $storage_id равны 0, значит это единый запрос для прайс-листов - пропускаем все шаги и сразу вызываем обработчик
		if($office_id != 0)//Для API-поставщиков
		{
			//1. Формируем массив наценок
			$markups_2 = array();
			$markups_query = $db_link->prepare('SELECT `min_point`, `max_point`, `markup`/100 AS `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id`=? ORDER BY `min_point`;');
			$markups_query->execute( array($office_id, $storage_id, $this->group_id) );
			while( $markup = $markups_query->fetch() )
			{
				array_push($markups_2, $markup );
			}//for($i)
			
			
			// ------------------------------------------------------------------------
			
			//1. Формируем массив наценок следующим образом: Получаем диапазоны цен и формируем массив, ключами которого будут цены (целые числа). Для определения наценки - цена товара будет ключем к массиву, а значение массива - будет сама наценка.
			//Последний элемент массива $markups - это наценка на последний диапазон (до бесконечности), т.е. наценка на цену, которой нет в массиве - определяется последним элементом массива
			$markups = array();//Оставляем массив, чтобы сохранить обратную совместимость со старыми версиями протоколов поставщиков
			$markups[0]=0;
			/*
			$markups_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id`=? ORDER BY `min_point`;');
			$markups_query->execute( array($office_id, $storage_id, $this->group_id) );
			$price_ranges_count = $markups_query->fetchColumn();//Количество диапазонов цен
			
			$markups_query = $db_link->prepare('SELECT `min_point`, `max_point`, `markup`/100 AS `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id`=? ORDER BY `min_point`;');
			$markups_query->execute( array($office_id, $storage_id, $this->group_id) );
			$i=0;
			while( $markup = $markups_query->fetch() )
			{
				//Определяем минимальную точку
				if($i == 0)
				{
					$min_point = $markup["min_point"];//Если диапазон первый, то минимальная точка - 0 включительно
				}
				else
				{
					$min_point = $markup["min_point"]+1;//Если диапазон не первый - минимальная точка - на 1 больше, чем в БД
				}
				
				//Определяем максимальную точку
				if($i == $price_ranges_count - 1)//Если диапазон последний, максимальная точка - равна минимальной
				{
					$max_point = $min_point;
				}
				else//Если диапазон не последний - максимальная точка - как в БД
				{
					$max_point = $markup["max_point"];
				}
				
				for($p = $min_point; $p <= $max_point; $p++)
				{
					$markups[$p] = $markup["markup"];
				}
				
				$i++;
			}//for($i)
			*/
			// ----------------------------------------------------------------------------------------------
			
			//2. ДАННЫЕ ПО ОФИСУ И СКЛАДУ
			//2.1 Получаем дополнительный срок доставки от склада до офиса
			$additional_time_query = $db_link->prepare('SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?;');
			$additional_time_query->execute( array($office_id, $storage_id) );
			$additional_time_record = $additional_time_query->fetch();
			$additional_time = $additional_time_record["additional_time"];
			if($additional_time != 0)
			{
				$additional_time = (int)($additional_time/24);
			}
			
			// ----------------------------------------------------------------------------------------------
			
			//2.2 Получаем название офиса обслуживания
			$office_caption_query = $db_link->prepare('SELECT `caption` FROM `shop_offices` WHERE `id` = ?;');
			$office_caption_query->execute( array($office_id) );
			$office_caption_record = $office_caption_query->fetch();
			$office_caption = $office_caption_record["caption"];
			
			// ----------------------------------------------------------------------------------------------
			
			//2.3. Для менеджера получаем название склада. Т.е. название склада выводим только для менеджера данного офиса обслуживания
			$storage_caption_for_manager_query = $db_link->prepare('SELECT
				`shop_storages`.`name` AS `storage_caption`
				FROM
				`shop_offices`
				INNER JOIN `shop_offices_storages_map` ON `shop_offices`.`id` = `shop_offices_storages_map`.`office_id`
				INNER JOIN `shop_storages` ON `shop_storages`.`id` = `shop_offices_storages_map`.`storage_id`
				WHERE
				`shop_offices`.`users` LIKE ? AND
				`shop_storages`.`id` = ? AND
				`shop_offices`.`id` = ? LIMIT 1;');
			$storage_caption_for_manager_query->execute( array('%'.$this->user_id.'%', $storage_id, $office_id) );
			$storage_caption_record = $storage_caption_for_manager_query->fetch();
			
			if($storage_caption_record != false)
			{
				$storage_caption = $storage_caption_record["storage_caption"];
			}
			else
			{
				$storage_caption = "";//Запрос не от менеджера офиса
			}
			
			// ----------------------------------------------------------------------------------------------
			
			//3. Получаем данные склада: настройки подключения, валюту и имя каталога, в котором находится скрипт-обработчик
			$storage_query = $db_link->prepare('SELECT
				`shop_storages`.`connection_options` AS `connection_options`,
				`shop_storages`.`currency` AS `currency`,
				`shop_storages_interfaces_types`.`handler_folder` AS `handler_folder`,
				(SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = ?) ) AS `rate`
				FROM
				`shop_storages`
				INNER JOIN `shop_storages_interfaces_types` ON `shop_storages`.`interface_type` = `shop_storages_interfaces_types`.`id`
				WHERE
				`shop_storages`.`id` = ?;');
			$storage_query->execute( array($storage_id, $storage_id) );
			$storage_record = $storage_query->fetch();
			$handler_folder = $storage_record["handler_folder"];
			$storage_options = json_decode($storage_record["connection_options"], true);//Настройки для обработчика поставщика
			$storage_options["markups"] = $markups;//Добавляем сюда наценки
			$storage_options["office_id"] = $office_id;
			$storage_options["storage_id"] = $storage_id;
			$storage_options["additional_time"] = $additional_time;
			$storage_options["office_caption"] = $office_caption;
			$storage_options["storage_caption"] = $storage_caption;//Только для менеджера. Для остальных значение равно ""
			$storage_options["rate"] = $storage_record["rate"];
			
			if($handler_folder === 'treelax_catalogue'){
				$storage_options["analogs"] = $query["analogs"];//Список аналогов
				
				//Получаем географический узел покупателя
				$geo_id = $_COOKIE["my_city"];

				//Куки не были еще выставлены - выводим для самого первого гео-узла, чтобы хоть что-то показать
				if($geo_id == NULL)
				{
					$min_geo_id_query = $db_link->prepare('SELECT MIN(`id`) AS `id` FROM `shop_geo`;');
					$min_geo_id_query->execute();
					$min_geo_id_record = $min_geo_id_query->fetch();
					$geo_id = $min_geo_id_record["id"];
				}
				
				$storage_options["geo_id"] = $geo_id;
			}
			
			
			$storage_options["group_id"] = $this->group_id;
		}
		else//Для прайс-листов
		{
			$storage_options = array();
			
			$storage_options["user_id"] = $this->user_id;
			$storage_options["group_id"] = $this->group_id;
			$storage_options["office_storage_bunches"] = $query["office_storage_bunches"];//Связка магазинов со всеми складами типа прайс-лист
			$storage_options["analogs"] = $query["analogs"];//Список аналогов
			
			$handler_folder = "prices";
		}
		
		
		// ----------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------
		// ----------------------------------------------------------------------------------------------
		
		
        //4. Обращаемся к поставщику
        $postdata = http_build_query(
			array(
				'article' => $article,//Артикул
				'storage_options' => json_encode($storage_options)//Настройки подключения
			)
		);//Аргументы
		
		$curl = curl_init();
		//Определяем версию протокола
		$protocol_script = "common_interface.php";//Версия 1
		if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/suppliers_handlers/".$handler_folder."/get_supplies.php") )
		{
			//$protocol_script = "get_supplies.php";//Версия 2
		}
		curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/docpart/suppliers_handlers/".$handler_folder."/".$protocol_script);
		curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
		curl_setopt($curl, CURLOPT_POST, true);
		curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, (int) $DP_Config->max_time_answer_storage); 
		curl_setopt($curl, CURLOPT_TIMEOUT, (int) $DP_Config->max_time_answer_storage);
		curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
		curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
		curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
		$curl_result = curl_exec($curl);
		
		//var_dump($curl_result);
		
		curl_close($curl);
		
		$curl_result = json_decode($curl_result, true);
        if($curl_result["result"] == false)
        {
            $this->result = 0;
            $this->message = "Storage handler error";
            return;
        }
		

        $this->Products = $curl_result["Products"];
		
		
		
		// ----------------------------------------------------------------------------------------------
		//Цикл по всем товарам для дополнительной обработки
		if(true)
		{
			$product_dump_list = array();//Список дампов продуктов для проверки на уникальность
			$Products_local = array();//Отфильтрованный список товаров
			
			//Сам цикл
			for($i=0; $i < count($this->Products); $i++)
			{
				//Если в 1С был задан флаг "Без аналогов" - отфильтровываем аналоги
				if( $this_article == 1 )
				{
					if( $this->Products[$i]["article"] != $article )
					{
						continue;
					}
				}
				
				
				//НОВЫЙ ВАРИАНТ НАЦЕНКИ - КРОМЕ ПРАЙС-ЛИСТОВ И ТОВАРОВ СВОЕГО КАТАЛОГА
				if($office_id != 0 && $this->Products[$i]["product_type"] != 1 )
				{
					$work_price = $this->Products[$i]["price_purchase"];//Берем закупочную цену
					
					//Ищем наценку для этой цены и обрабатываем округление
					foreach( $markups_2 AS $markup_range )
					{
						if( $work_price >= $markup_range["min_point"] && $work_price <= $markup_range["max_point"] )
						{
							//Добавили наценку
							$work_price = $work_price + $work_price*$markup_range["markup"];
							
							$this->Products[$i]["price"] = $work_price;
							$this->Products[$i]["markup"] = (int)($markup_range["markup"]*100);
							break;
						}
					}
				}
				
				//Для дальнейшей обработки цены
				$work_price = $this->Products[$i]["price"];
				$work_price = number_format($work_price, 2, '.', '');//Округление по умолчанию
				
				//Округление цены
				if($DP_Config->price_rounding == '1')//Без копеечной части
				{
					if($work_price > (int)$work_price)
					{
						$work_price = (int)$work_price+1;
					}
					else
					{
						$work_price = (int)$work_price;
					}
				}
				else if($DP_Config->price_rounding == '2')//До 5 руб
				{
					$work_price = (integer)$work_price;
					$price_str = (string)$work_price;
					$price_str_last_char = (integer)$price_str[strlen($price_str)-1];
					if($price_str_last_char > 0 && $price_str_last_char < 5)
					{
						$work_price = $work_price + (5 - $price_str_last_char);
					}
					else if($price_str_last_char > 5 && $price_str_last_char <= 9)
					{
						$work_price = $work_price + (10 - $price_str_last_char);
					}
				}
				else if($DP_Config->price_rounding == '3')//До 10 руб
				{
					$work_price = (integer)$work_price;
					$price_str = (string)$work_price;
					$price_str_last_char = (integer)$price_str[strlen($price_str)-1];
					if($price_str_last_char != 0)
					{
						$work_price = $work_price + (10 - $price_str_last_char);
					}
				}
				
				//Строка была добавлена для создания фильтра проценки (для совместимости с JavaScript)
				$work_price = (float)$work_price;
				
				//Окончательная цена с наценкой
				$this->Products[$i]["price"] = $work_price;
				
				
				//Считаем хеш для защиты от подмены данных на уровне клиента
				if($this->Products[$i]["product_type"] == 1)
				{
					$this->Products[$i]["check_hash"] = md5($this->Products[$i]["product_id"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["storage_record_id"].$this->Products[$i]["price"].$DP_Config->tech_key);
				}
				else
				{
					$this->Products[$i]["check_hash"] = md5($this->Products[$i]["manufacturer"].$this->Products[$i]["article"].$this->Products[$i]["name"].$this->Products[$i]["exist"].$this->Products[$i]["price"].$this->Products[$i]["time_to_exe"].$this->Products[$i]["min_order"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["price_purchase"].$this->Products[$i]["markup"].$this->Products[$i]["product_type"].$DP_Config->tech_key);
				}
				
				
				//СИНОНИМ
				$synonym_query = $db_link->prepare('SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = (SELECT `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1);');
				$synonym_query->execute( array($this->Products[$i]["manufacturer"]) );
				$synonym_record = $synonym_query->fetch();
				if( $synonym_record != false )
				{
					$this->Products[$i]["manufacturer_transferred"] = $this->Products[$i]["manufacturer"];
					$synonym_record["name"] = str_replace('"',"'",$synonym_record["name"]);
					$this->Products[$i]["manufacturer"] = strtoupper($synonym_record["name"]);
				}
				
				
				//Дамп - для фильтра уникальности предложений
				$dump = md5($this->Products[$i]["article"].$this->Products[$i]["manufacturer"].$this->Products[$i]["price"].$this->Products[$i]["exist"].$this->Products[$i]["time_to_exe"]);
				
				//Добавляем только уникальные предложения
				if( array_search($dump, $product_dump_list) === false )
				{
					array_push($product_dump_list, $dump);//Вносим дамп в список уникальных дампов
					array_push($Products_local, $this->Products[$i]);//Вносим продукт в фильтрованный список
				}
			}
			$this->Products = $Products_local;//Переинициализируем список товаров
		}
		
		// ----------------------------------------------------------------------------------------------
        $this->result = 1;
    }//~__construct
}//~class ProductsOfBunch//Класс ответа




$article 		= $_GET["article"];
$office_id 		= (int)$_GET["office_id"];
$storage_id 	= (int)$_GET["storage_id"];
// Never trust GET user_id — session only (anti-crawl).
$user_id		= (int) $epc_anti_crawl['session_user_id'];
$this_article 	= (int)$_GET["this_article"];// Без аналогов (0 или 1)


$sweep=array(" ", "-", "_", "/", ".", ",", "#", "\r\n", "\r", "\n", "\t");
$article = str_replace($sweep, "", $article);
$article = strtoupper($article);

// http://shop.avtoross.com/content/shop/docpart/ajax_getProductsOfBunch2.php?article=0986487585&office_id=2&storage_id=18&user_id=65&this_article=0

$ProductsOfBunch = new ProductsOfBunch($article, $office_id, $storage_id, $db_link, $DP_Config, $user_id, $this_article);

if (!empty($ProductsOfBunch->Products) && is_array($ProductsOfBunch->Products)) {
	epc_storefront_fill_warehouse_captions($ProductsOfBunch->Products, $db_link);
}
if (empty($epc_anti_crawl['prices_visible']) && empty($epc_anti_crawl['tech_key'])) {
	if (!empty($ProductsOfBunch->Products) && is_array($ProductsOfBunch->Products)) {
		epc_storefront_prices_redact_products($ProductsOfBunch->Products);
	}
	$ProductsOfBunch->prices_visible = false;
} else {
	$ProductsOfBunch->prices_visible = true;
}

exit(json_encode($ProductsOfBunch));
?>