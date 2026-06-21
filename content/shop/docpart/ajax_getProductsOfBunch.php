<?php
/**
 * Серверный скрипт для получения данных о товарах одной связки (офис-склад)
*/
header('Content-Type: application/json;charset=utf-8;');
ini_set('display_errors', 0);
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
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_storage_flags.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/epc_storefront_prices_helpers.php");


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");



class ProductsOfBunch//Класс ответа
{
    public $result;//Рузультат работы (1 - успешно, 0 - не успешно)
    public $time;//Время запроса
    public $storage_id;//storage_id
	public $message;//Сообщение
	public $Products = array();//Список товаров
    
    public $user_id = 0;//ID пользователя
    public $group_id = 0;//Группа пользователя
    
    public function __construct($query, $office_id, $storage_id, $db_link, $DP_Config)
    {
        $this->storage_id = $storage_id;//storage_id

		if ((int) $storage_id > 0 && epc_ssf_is_storage_disabled($db_link, (int) $storage_id)) {
			$this->result = 0;
			$this->message = 'storage_storefront_disabled';
			return;
		}
		
		//0. Получаем данные по пользователю
		if($_POST['async'] == 1 && $DP_Config->tech_key == $_POST['tech_key']){
			$this->user_id = $_POST['user_id'];
			$this->group_id = $_POST['group_id'];
		}else{
			$this->user_id = DP_User::getUserId();//ID пользователя
			$userProfile = DP_User::getUserProfile();//Профиль пользователя
			$this->group_id = $userProfile["groups"][0];//Первая группа пользователя. Если у пользователя несколько групп - работаем только с первой
        }
		
		
		
		// ------------------------------------------------------------------------
		
		// Техническая информация в проценке - формируем массив наценок под каждую группу - магазин - склад
		
		$markups_users_groups = array();
		
		$group_query = $db_link->prepare('SELECT * FROM `groups` WHERE `id` = ?;');
		$group_query->execute(array($this->group_id));
		$group_info = $group_query->fetch();
		
		if((int)$group_info['for_percentage'] === 1){
			
			$all_shop_offices = array();
			$all_shop_storages = array();
			$all_groups = array();
			
			// Получаем все id офисов
			$all_shop_offices_query = $db_link->prepare('SELECT `id` FROM `shop_offices`;');
			$all_shop_offices_query->execute();
			while($all_shop_offices_record = $all_shop_offices_query->fetch())
			{
				$all_shop_offices[] = $all_shop_offices_record['id'];
			}
			
			// Получаем все id складов
			$all_shop_storages_query = $db_link->prepare('SELECT `id` FROM `shop_storages`;');
			$all_shop_storages_query->execute();
			while($all_shop_storages_record = $all_shop_storages_query->fetch())
			{
				$all_shop_storages[] = $all_shop_storages_record['id'];
			}
			
			// Получаем все id групп
			$all_groups_query = $db_link->prepare('SELECT `id` FROM `groups`;');
			$all_groups_query->execute();
			while($all_groups_record = $all_groups_query->fetch())
			{
				$all_groups[] = $all_groups_record["id"];
			}
			
			// Получаем все наценки
			foreach($all_groups as $id_group){
				foreach($all_shop_offices as $id_office){
					foreach($all_shop_storages as $id_storage){
						$markups_query = $db_link->prepare('SELECT `min_point`, `max_point`, `markup`/100 AS `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? ORDER BY `min_point`;');
						$markups_query->execute( array($id_office, $id_storage, $id_group) );
						while( $markup = $markups_query->fetch() )
						{
							$markups_users_groups[$id_group][$id_office][$id_storage][] = $markup;
						}
					}
				}
			}
		}
		
		// ------------------------------------------------------------------------
		
		
		
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
			$storage_caption_for_manager_query->execute( array('%"'.$this->user_id.'"%', $storage_id, $office_id) );
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
			
			if( $storage_record == false || $storage_record == null )
			{
				$storage_record = array();
				$storage_record['handler_folder'] = null;
				$storage_record['connection_options'] = null;
				$storage_record['rate'] = null;
			}
			
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
				
				if( !isset($query["analogs"]) )
				{
					$query["analogs"] = null;
				}
				
				$storage_options["analogs"] = $query["analogs"];//Список аналогов
				
				//Получаем географический узел покупателя
				$geo_id = NULL;
				if( isset($_COOKIE["my_city"]) )
				{
					$geo_id = $_COOKIE["my_city"];
				}

				//Куки не были еще выставлены - выводим для самого первого гео-узла, чтобы хоть что-то показать
				if($geo_id == NULL)
				{
					$min_geo_id_query = $db_link->prepare('SELECT MIN(`id`) AS `id` FROM `shop_geo`;');
					$min_geo_id_query->execute();
					$min_geo_id_record = $min_geo_id_query->fetch();
					$geo_id = $min_geo_id_record["id"];
				}
				
				if($_POST['async'] == 1 && $DP_Config->tech_key == $_POST['tech_key']){
					$geo_id = $_POST['geo_id'];
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
			$price_bunches = (!empty($query["office_storage_bunches"]) && is_array($query["office_storage_bunches"]))
				? $query["office_storage_bunches"]
				: array();
			$storage_options["office_storage_bunches"] = epc_ssf_filter_office_storage_bunches($db_link, $price_bunches);
			$storage_options["analogs"] = (!empty($query["analogs"]) && is_array($query["analogs"])) ? $query["analogs"] : array();
			
			$handler_folder = "prices";
		}
		
		
		// ----------------------------------------------------------------------------------------------
        // ----------------------------------------------------------------------------------------------
		// ----------------------------------------------------------------------------------------------
		
		//Производитель - для прайс-листов
		$manufacturer = NULL;
		if( isset($query["manufacturer"]) )
		{
			$manufacturer = $query["manufacturer"];
		}
		
		
        //4. Обращаемся к поставщику
		$curl_result = null;
		if($handler_folder === 'prices')
		{
			if(!class_exists('prices_enclosure', false))
			{
				define('DOCPART_PRICES_ENCLOSURE_LIBRARY', true);
				require_once($_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/suppliers_handlers/prices/common_interface.php');
			}
			$manufacturers_for_prices = array();
			if(isset($query['manufacturers']) && is_array($query['manufacturers']))
			{
				$manufacturers_for_prices = $query['manufacturers'];
			}
			$searsch_str = !empty($query['searsch_str']) ? $query['searsch_str'] : $query['article'];
			$prices_ob = new prices_enclosure($query['article'], $manufacturers_for_prices, $storage_options, $searsch_str);
			$curl_result = json_decode(json_encode($prices_ob), true);
		}
		else
		{
			$postdata = http_build_query(
				array(
					'article' => $query["article"],
					'searsch_str' => $query["searsch_str"],
					'manufacturer' => $manufacturer,
					'manufacturers' => json_encode($query["manufacturers"]),
					'storage_options' => json_encode($storage_options)
				)
			);
			$curl = curl_init();
			$protocol_script = "common_interface.php";
			if( file_exists($_SERVER["DOCUMENT_ROOT"]."/content/shop/docpart/suppliers_handlers/".$handler_folder."/get_supplies.php") )
			{
				$protocol_script = "get_supplies.php";
			}
			curl_setopt($curl, CURLOPT_URL, $DP_Config->domain_path."content/shop/docpart/suppliers_handlers/".$handler_folder."/".$protocol_script);
			curl_setopt($curl, CURLOPT_RETURNTRANSFER, true);
			curl_setopt($curl, CURLOPT_POST, true);
			curl_setopt($curl, CURLOPT_CONNECTTIMEOUT, (int) $DP_Config->max_time_answer_storage);
			curl_setopt($curl, CURLOPT_TIMEOUT, (int) $DP_Config->max_time_answer_storage);
			curl_setopt($curl, CURLOPT_POSTFIELDS, $postdata);
			curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
			curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
			$curl_raw = curl_exec($curl);
			curl_close($curl);
			$curl_result = json_decode($curl_raw, true);
		}
        if( !is_array($curl_result) || !isset($curl_result["result"]) || (int)$curl_result["result"] !== 1 )
        {
            $this->result = 0;
            $this->message = "Storage handler error";
            return;
        }
		

        $this->Products = !empty($curl_result["Products"]) ? $curl_result["Products"] : array();
		
		
		
		// ----------------------------------------------------------------------------------------------
		
		//СИНОНИМЫ
		$synonyms = array();
		$synonym_query = $db_link->prepare("SELECT `synonym`, (SELECT `name` FROM `shop_docpart_manufacturers` WHERE `id` = `shop_docpart_manufacturers_synonyms`.`manufacturer_id`) AS 'name' FROM `shop_docpart_manufacturers_synonyms`;");
		$synonym_query->execute();
		while($synonym_record = $synonym_query->fetch()){
			$synonyms[mb_strtoupper(str_replace('"',"'",$synonym_record["synonym"]), 'UTF-8')] = htmlentities(mb_strtoupper(str_replace('"',"'",$synonym_record["name"]), 'UTF-8'));
		}
		
		// ----------------------------------------------------------------------------------------------
		//Цикл по всем товарам для дополнительной обработки
		if(true)
		{
			$product_dump_list = array();//Список дампов продуктов для проверки на уникальность
			$Products_local = array();//Отфильтрованный список товаров
			
			//Сам цикл
			for($i=0; $i < count($this->Products); $i++)
			{
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
					$this->Products[$i]["check_hash"] = md5($this->Products[$i]["manufacturer"].$this->Products[$i]["article"].$this->Products[$i]["article_show"].$this->Products[$i]["name"].$this->Products[$i]["exist"].$this->Products[$i]["price"].$this->Products[$i]["time_to_exe"].$this->Products[$i]["time_to_exe_guaranteed"].$this->Products[$i]["storage"].$this->Products[$i]["min_order"].$this->Products[$i]["probability"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["price_purchase"].$this->Products[$i]["markup"].$this->Products[$i]["json_params"].$this->Products[$i]["product_type"].$DP_Config->tech_key);
				}
				
				
				//СИНОНИМ
				$synonym = NULL;
				if( isset($synonyms[mb_strtoupper(str_replace('"',"'", html_entity_decode($this->Products[$i]["manufacturer"], ENT_QUOTES | ENT_XML1, 'UTF-8') ), 'UTF-8')]) )
				{
					$synonym = $synonyms[mb_strtoupper(str_replace('"',"'", html_entity_decode($this->Products[$i]["manufacturer"], ENT_QUOTES | ENT_XML1, 'UTF-8') ), 'UTF-8')];
				}
				if(!empty($synonym))
				{
					$this->Products[$i]["manufacturer_transferred"] = $this->Products[$i]["manufacturer"];
					$this->Products[$i]["manufacturer"] = $synonym;
				}
				
				
				//Определяем Б\У
				$by_flag = false;
				$t2_json_params_array = json_decode($this->Products[$i]["json_params"], true);
				if( !empty($t2_json_params_array) && ( isset($t2_json_params_array['used']) && (int) $t2_json_params_array['used'] === 1) ){
					$by_flag = true;
				}
				

				if($by_flag === true){
					array_push($Products_local, $this->Products[$i]);//Вносим продукт в фильтрованный список
				}else{
					//Дамп - для фильтра уникальности предложений
					$dump = md5($this->Products[$i]["article"].$this->Products[$i]["manufacturer"].$this->Products[$i]["price"].$this->Products[$i]["exist"].$this->Products[$i]["time_to_exe"].$this->Products[$i]["storage_id"]);

					//Добавляем только уникальные предложения
					if( array_search($dump, $product_dump_list) === false )
					{
						array_push($product_dump_list, $dump);//Вносим дамп в список уникальных дампов
						array_push($Products_local, $this->Products[$i]);//Вносим продукт в фильтрованный список
					}
				}
				
			}
			$this->Products = $Products_local;//Переинициализируем список товаров
		}
		
		// ----------------------------------------------------------------------------------------------
		// ----------------------------------------------------------------------------------------------
		
		// Цены остальных групп
		if((int)$group_info['for_percentage'] === 1){
			
			// Получаем все наименования складов
			$all_shop_storages = array();
			$all_shop_storages_query = $db_link->prepare('SELECT * FROM `shop_storages`;');
			$all_shop_storages_query->execute();
			while($all_shop_storages_record = $all_shop_storages_query->fetch())
			{
				$all_shop_storages[$all_shop_storages_record['id']] = $all_shop_storages_record;
			}
			
			for($i=0; $i < count($this->Products); $i++)
			{
				$this->Products[$i]["storage_caption"] = $all_shop_storages[$this->Products[$i]["storage_id"]]['name'];
				
				foreach($markups_users_groups as $group_id => $markups_aray)
				{
					$work_price = $this->Products[$i]["price_purchase"];//Берем закупочную цену
					
					foreach( $markups_aray[$this->Products[$i]["office_id"]][$this->Products[$i]["storage_id"]] AS $markup_range )
					{
						if( $work_price >= $markup_range["min_point"] && $work_price <= $markup_range["max_point"] )
						{
							//Добавили наценку
							$work_price = $work_price + $work_price*$markup_range["markup"];
							
							// ---------------
							
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
							
							// ---------------
							
							//Считаем хеш для защиты от подмены данных на уровне клиента
							if(!empty($this->Products[$i]["manufacturer_transferred"])){
								if($this->Products[$i]["product_type"] == 1)
								{
									$this->Products[$i]["groups_check_hash"][$group_id] = md5($this->Products[$i]["product_id"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["storage_record_id"].$work_price.$DP_Config->tech_key);
								}
								else
								{
									$this->Products[$i]["groups_check_hash"][$group_id] = md5($this->Products[$i]["manufacturer_transferred"].$this->Products[$i]["article"].$this->Products[$i]["article_show"].$this->Products[$i]["name"].$this->Products[$i]["exist"].$work_price.$this->Products[$i]["time_to_exe"].$this->Products[$i]["time_to_exe_guaranteed"].$this->Products[$i]["storage"].$this->Products[$i]["min_order"].$this->Products[$i]["probability"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["price_purchase"].((int)($markup_range["markup"]*100)).$this->Products[$i]["json_params"].$this->Products[$i]["product_type"].$DP_Config->tech_key);
								}
							}else{
								if($this->Products[$i]["product_type"] == 1)
								{
									$this->Products[$i]["groups_check_hash"][$group_id] = md5($this->Products[$i]["product_id"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["storage_record_id"].$work_price.$DP_Config->tech_key);
								}
								else
								{
									$this->Products[$i]["groups_check_hash"][$group_id] = md5($this->Products[$i]["manufacturer"].$this->Products[$i]["article"].$this->Products[$i]["article_show"].$this->Products[$i]["name"].$this->Products[$i]["exist"].$work_price.$this->Products[$i]["time_to_exe"].$this->Products[$i]["time_to_exe_guaranteed"].$this->Products[$i]["storage"].$this->Products[$i]["min_order"].$this->Products[$i]["probability"].$this->Products[$i]["office_id"].$this->Products[$i]["storage_id"].$this->Products[$i]["price_purchase"].((int)($markup_range["markup"]*100)).$this->Products[$i]["json_params"].$this->Products[$i]["product_type"].$DP_Config->tech_key);
								}
							}
							
							// ---------------
							
							$this->Products[$i]["groups_price"][$group_id] = $work_price;
							$this->Products[$i]["groups_markup"][$group_id] = (int)($markup_range["markup"]*100);
							break;
						}
					}
				}
			}
		}
		
		// ----------------------------------------------------------------------------------------------
		
		// Фильтрация проценки
		if(true)
		{
			$brends_filtr = array();
			$sql = "SELECT * FROM `shop_docpart_filter` WHERE `active` = 1;";
			$query = $db_link->prepare($sql);
			$query->execute();
			while($rov = $query->fetch())
			{
				$brends_filtr[] = $rov;//Список фильтров
			}
			
			if(!empty($brends_filtr) && !empty($this->Products))
			{
				$Products_local = array();//Отфильтрованный список товаров
				
				//Цикл по товарам
				for($i=0; $i < count($this->Products); $i++)
				{
					$validen = true;//Флаг, товар прошел фильтрацию
					
					//Фильтрация
					foreach($brends_filtr as $filtr)
					{
						if
						(
							( $validen === true ) && 
							( ($filtr['manufacturer'] === '') || ($filtr['manufacturer'] === $this->Products[$i]["manufacturer"]) ) && 
							( ($filtr['article'] === '') || ($filtr['article'] === $this->Products[$i]["article"]) ) && 
							( ($filtr['name'] === '') || (mb_strpos($this->Products[$i]["name"], $filtr['name'], 0, 'UTF-8') !== false) ) 
						)
						{
							$this_list_storages = json_decode($filtr['list_storages'], true);//Указанные склады
							if(empty($this_list_storages) || in_array((int)$this->Products[$i]["storage_id"], $this_list_storages)){
								
								// Если диапазоны не были заданы
								if( ($filtr['min_price'] == '' &&
									$filtr['max_price'] == '' &&
									$filtr['min_time']  == '' && 
									$filtr['max_time']  == '' ) || 
									($filtr['min_price'] == 0 &&
									$filtr['max_price'] == 0 &&
									$filtr['min_time']  == 0 && 
									$filtr['max_time']  == 0  )
								)
								{
									//Если вообще не какие уточнения не заданы
									if(
										($filtr['manufacturer'] === '') && 
										($filtr['article'] === '') && 
										($filtr['name'] === '') && 
										($filtr['list_storages'] === '')  
									)
									{
										// Пропускаем, иначе проценка полностью будет пустая
									}else{
										$validen = false;
									}
								}
								else
								{
									
									// Цена
									if($filtr['min_price'] > 0 || $filtr['max_price'] > 0){
										if(($this->Products[$i]["price"] >= $filtr['min_price']) && ($this->Products[$i]["price"] <= $filtr['max_price'])){
											// Попадает в диапазон, пропускаем
										}else{
											$validen = false;
										}
									}
									
									// Срок
									if($filtr['min_time'] > 0 || $filtr['max_time'] > 0){
										if(($this->Products[$i]["time_to_exe"] >= $filtr['min_time']) && ($this->Products[$i]["time_to_exe"] <= $filtr['max_time'])){
											// Попадает в диапазон, пропускаем
										}else{
											$validen = false;
										}
									}
									
								}
							}
						}
					}
					
					//Проверка
					if($validen === true)
					{
						array_push($Products_local, $this->Products[$i]);//Вносим продукт в фильтрованный список
					}
				}
				
				$this->Products = $Products_local;//Переинициализируем список товаров
			}
		}
		
		// ----------------------------------------------------------------------------------------------
		
		// ----------------------------------------------------------------------------------------------
		
        $this->result = 1;
    }//~__construct
}//~class ProductsOfBunch//Класс ответа

$query = json_decode($_POST["query"], true);

$time_start = microtime(true);
$ProductsOfBunch = new ProductsOfBunch($query, $_POST["office_id"], $_POST["storage_id"], $db_link, $DP_Config);
$time_end = microtime(true);
$ProductsOfBunch->time = number_format(($time_end - $time_start), 3, '.', '');

if (isset($DP_Config->is_async_search) && $DP_Config->is_async_search == 1)
{
	//Артикул
	$article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/", "", $query['article']), "UTF-8");
	$manufacturer = '';
	$name = '';

	//Производитель и  Наименование
	if(is_array($query['manufacturers'])){
		foreach($query['manufacturers'] as $manufacturer_object){
			$manufacturer_object = (array)$manufacturer_object;
			if(!empty($manufacturer_object['manufacturer_show'])){
				$manufacturer = htmlentities(mb_strtoupper(trim($manufacturer_object['manufacturer_show']), "UTF-8"), ENT_QUOTES, "UTF-8");
				$name = htmlentities(str_replace(array("\"", "\\", "'", "\n", "\r", "\t", "#", ">", "<"), "", $manufacturer_object['name']), ENT_QUOTES, "UTF-8");
				break ;
			}
		}
	}

	$user_id = DP_User::getUserId();

	//Если пользователь авторизован удаляем все записи запросов данного артикула пользователем за последние 24 часа, что бы не было дублей, для более корректной статистики
	if($user_id > 0){
		$where = '';
		$where = '`user_id` = ? AND `time` > ? AND `article` = ? AND `manufacturer` = ?';
		$binding_values = array();
		array_push($binding_values, $user_id);
		array_push($binding_values, (time() - 86400));
		array_push($binding_values, $article);
		array_push($binding_values, $manufacturer);
		
		$sql = "DELETE FROM `shop_stat_article_queries` WHERE ".$where;
		$query = $db_link->prepare($sql);
		$query->execute($binding_values);
	}

	//Пишем в таблицу статистики запрос данного артикула с текущей датой
	$where = '';
	$binding_values = array();
	array_push($binding_values, $article);
	array_push($binding_values, $manufacturer);
	array_push($binding_values, $name);
	array_push($binding_values, '');
	array_push($binding_values, $_SERVER['REMOTE_ADDR']);
	array_push($binding_values, $user_id);
	array_push($binding_values, time());

	$sql = "INSERT INTO `shop_stat_article_queries`(`id`, `article`, `manufacturer`, `name`, `search_string`, `ip`, `user_id`, `time`) VALUES (NULL,?,?,?,?,?,?,?)";
	$query = $db_link->prepare($sql);
	$query->execute($binding_values);
}

if (!epc_storefront_prices_visible_for_user((int) $ProductsOfBunch->user_id)) {
	if (!empty($ProductsOfBunch->Products) && is_array($ProductsOfBunch->Products)) {
		epc_storefront_prices_redact_products($ProductsOfBunch->Products);
	}
	$ProductsOfBunch->prices_visible = false;
} else {
	$ProductsOfBunch->prices_visible = true;
	$epc_vat_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
	if (is_readable($epc_vat_file) && !empty($ProductsOfBunch->Products) && is_array($ProductsOfBunch->Products)) {
		require_once $epc_vat_file;
		$epc_vat_ctx = epc_uae_customer_vat_resolve($db_link, (int) $ProductsOfBunch->user_id);
		$ProductsOfBunch->vat_type = $epc_vat_ctx['vat_type'];
		$ProductsOfBunch->vat_price_label = $epc_vat_ctx['price_label'];
		$ProductsOfBunch->vat_display_mode = $epc_vat_ctx['display_mode'];
		foreach ($ProductsOfBunch->Products as &$epc_vat_product) {
			if (is_array($epc_vat_product)) {
				epc_uae_customer_vat_apply_product_row($db_link, $epc_vat_product, (int) $ProductsOfBunch->user_id);
			}
		}
		unset($epc_vat_product);
	}
}

exit(json_encode($ProductsOfBunch));
?>