<?php

require_once($_SERVER["DOCUMENT_ROOT"]."/modules/debug/debug.class.php");
$debug = new Debug();

function generate_price($request_object){
	global $db_link;
	global $DP_Config;
	global $synonyms;
	global $debug;
	global $answer;

	$offices = (int)$request_object['offices'];
	$arr_storages = $request_object['arr_storages'];
	$arr_category = $request_object['arr_category'];

	$is_mailing = isset($request_object['is_mailing']) && !empty($request_object['is_mailing']) ? (int)$request_object['is_mailing'] : false;
	$columns_price = isset($request_object['columns_price']) && !empty($request_object['columns_price']) ? (int)$request_object['columns_price'] : array();

	if( isset($request_object['storages']) )
	{
		$storages = (int)$request_object['storages'];
	}
	else
	{
		$storages = 0;
	}

	$users_list = null;
	if( isset($request_object['users_list']) )
	{
		$users_list = $request_object['users_list'];
	}
	if( isset($request_object['emails_list']) )
	{
		$emails_list = explode(',', $request_object['emails_list']);
	}
	$group_id_my_list_emails = (int)$request_object['group_id_my_list_emails'];

	$min_price = 0;// Минимальная цена для выгрузки в прайс лист
	$max_price = 0;// Максимальня цена для выгрузки в прайс лист (0 - не используется)
	$min_time_to_exe = 0;
	$max_time_to_exe = 0;
	
	$groups = array();// список групп для наценки
	
	// В цикле проходим по списку выбранных пользователей что бы определить список групп от которых будем брать наценки
	if(is_array($users_list) && !empty($users_list))
	{
		$user_id_sql = '';
		foreach($users_list as $user)
		{
			if($user_id_sql != '')
			{
				$user_id_sql .= ', ';
			}
			$user_id_sql .= (int)$user;
		}
		$sql = "SELECT DISTINCT `group_id` FROM `users_groups_bind` WHERE `user_id` IN ($user_id_sql);";
		$query = $db_link->prepare($sql);
		$query->execute();
		//echo $sql;
		
		while( $rov = $query->fetch() )
		{
			$groups[] = $rov['group_id'];
		}
	}

	if(!empty($group_id_my_list_emails))
	{
		if(array_search($group_id_my_list_emails, $groups) === false)
		{
			$groups[] = $group_id_my_list_emails;
		}
	}

	if(!empty($groups))
	{
		// Формируем прайсы для каждой группы
		foreach($groups as $group)
		{

			// Порядок колонок в выгрузке
			$columns_name = array(
				'1' => 'manufacturer',
				'2' => 'article',
				'3' => 'name',
				'4' => 'exist',
				'5' => 'time_to_exe',
				'6' => 'price',
				'7' => 'min_order'
			);
			$arr_col_num = array();

			if(!empty($columns_price)) {
									
				$max_col = 0;
				
				if($is_mailing) {
					foreach($columns_price as $column_price) {
						if(empty($column_price['checked']) || $column_price['checked'] == false) continue;

						$max_col++;
						
						$column_price_number = $column_price['id'];
						$arr_col_num[$max_col] = $columns_name[$column_price_number];
					}
				} else {
					foreach($columns_price as $column_price_number) {
						$max_col++;
						
						$arr_col_num[$max_col] = $columns_name[$column_price_number];
					}
				}
				
				if(!empty($storage_columns)) {
					$number_of_columns = count($storage_columns);
					$max_col += $number_of_columns;

					foreach($storage_columns as $storage_column) {
						$arr_col_num[] = $storage_column['id'];
					}
				}

			} else {

				$manufacturer = 1;
				$article = 2;
				$name = 3;
				$exist = 4;
				$time_to_exe = 5;
				$price = 6;
				$min_order = 7;

				// Определяем максимальное число колонок в файле, так как формат выгрузки может быть настроен так что будут пустые колонки
				$max_col = $manufacturer;
				if($max_col < $article) $max_col = $article;
				if($max_col < $name) $max_col = $name;
				if($max_col < $exist) $max_col = $exist;
				if($max_col < $price) $max_col = $price;
				if($max_col < $time_to_exe) $max_col = $time_to_exe;
				if($max_col < $min_order) $max_col = $min_order;

				$arr_col_num = array(
					$manufacturer => "manufacturer",
					$article => "article",
					$name => "name",
					$exist => "exist",
					$price => "price",
					$time_to_exe => "time_to_exe",
					$min_order => "min_order"
				);

			}

			//Создаем директорию для хранения файлов прайс-листов
			if(!file_exists($_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/") )
			{
				mkdir($_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/");
			}
			
			$price_name = 'prices_'.$group.'.csv';
			
			if(isset($request_object['pattern_name'])) {
					$price_name = $request_object['pattern_name'].'.csv';
			}
			
			// Файл в который выгружаем данные
			$file = fopen($_SERVER["DOCUMENT_ROOT"].'/content/files/Documents/prices_tmp/'.$price_name, 'w');

			// Выводим содержимое файла
			$str = '';
			for($i=1; $i<=$max_col; $i++)
			{
				if(!empty($arr_col_num[$i]))
				{
					switch($arr_col_num[$i])
					{
						case 'manufacturer':
							$str .= translate_str_by_key('2070');
						break;
						case 'article':
							$str .= translate_str_by_key('2071')
						break;
						case 'name':
							$str .= translate_str_by_key('2102');
						break;
						case 'exist':
							$str .= translate_str_by_key('4324');
						break;
						case 'price':
							$str .= translate_str_by_key('2751');
						break;
						case 'time_to_exe':
							$str .= translate_str_by_key('3433');
						break;
						case 'min_order':
							$str .= translate_str_by_key('3661');
						break;
					}
					if($str != '' && $i < $max_col)$str .= ';';
				}
				else
				{
					$str .= ';';
				}
			}
			
			//Добавляем дополнительные колонки (url товара, картинка товара, описание товара)
			$str .= ';';
			$str .= ';';
			$str .= ';';
			$str .= ';';
			
			$str = iconv('UTF-8', 'windows-1251', $str);

			if(!empty($str))
			{
				// Записали заголовок
				$str .= "\r\n";
				fwrite($file, $str);
			}
				
			//----------------------------------------------------

			//ДАЛЕЕ РАБОТАЕМ ПО СКЛАДАМ С ТИПОМ DOCPART PRICE
			
			foreach( $arr_storages AS $storage_id )
			{
				//Отсеиваем склады с типом, не равным Docpart Price
				$storage_query = $db_link->prepare("SELECT * FROM `shop_storages` WHERE `id` = ?;");
				$storage_query->execute( array($storage_id) );
				$storage_record = $storage_query->fetch();
				if($storage_record["interface_type"] != 2)
				{
					continue;
				}
				
				$storage_connection_options = json_decode($storage_record["connection_options"], true);
				
				//price_id для данного склада
				$price_id = $storage_connection_options["price_id"];
				
				//Валюта склада
				$currency = $storage_record["currency"];
				
				//Курс валюты
				if( $currency == $DP_Config->shop_currency )
				{
					$currency_rate = 1;
				}
				else
				{
					$currency_rate_query = $db_link->prepare("SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = ?;");
					$currency_rate_query->execute( array($currency) );
					$currency_rate_record = $currency_rate_query->fetch();
					$currency_rate = $currency_rate_record["rate"];
				}
				
				
				//Получаем дополнительный срок доставки
				$additional_time_query = $db_link->prepare("SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1;");
				$additional_time_query->execute( array($offices, $storage_id) );
				$additional_time_record = $additional_time_query->fetch();
				$additional_time = (int)($additional_time_record["additional_time"]/24);
				
				
				
				//1. Есть $offices (номер магазина - может быть только один)
				//2. $storage_id - номер склада
				//3. Есть $group - номер группы покупателя
				
				//Можем получить: срок доставки, наценку, *валюту склада, *курс валюты к основной валюте сайта, *price_id для склада
				
				$SQL_products = "SELECT
					*,
					(SELECT `markup`/100 AS `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= `shop_docpart_prices_data`.`price` AND `max_point` > `shop_docpart_prices_data`.`price`) AS `markup`
				FROM
					`shop_docpart_prices_data`
				WHERE
					`price_id` = ?;";
				
				$products_query = $db_link->prepare($SQL_products);
				$products_query->execute( array($offices, $storage_id, $group, $price_id) );
				while( $item = $products_query->fetch() )
				{
					// >>>
					
					
					if(empty($item['price']))
					{
						continue;
					}

					$item['name'] = str_replace(array("&amp;","&frasl;","frasl;", "&", ";"), " ", $item['name']);
					$item['article'] = str_replace(array("&amp;","&frasl;","frasl;", "&", ";"), " ", $item['article']);
					$item['manufacturer'] = str_replace(array("&amp;","&frasl;","frasl;", "&", ";"), " ", $item['manufacturer']);


					$article_str = '';
					$manufacturer_str = '';
					$str = '';
					for($i=1; $i<=$max_col; $i++)
					{
						if(!empty($arr_col_num[$i]))
						{
							if($arr_col_num[$i] == 'price')
							{
								//Переводим в валюту сайта
								$item[$arr_col_num[$i]] = (float)($item[$arr_col_num[$i]] * $currency_rate);
								
								//Наценка
								$item[$arr_col_num[$i]] = $item[$arr_col_num[$i]] + ($item[$arr_col_num[$i]] * $item["markup"]);
								
								//Округление цены
								$work_price = $item[$arr_col_num[$i]];
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
								$item[$arr_col_num[$i]] = $work_price;

								$str .= (float)number_format($item[$arr_col_num[$i]],2,'.','');
							}
							else if($arr_col_num[$i] == 'time_to_exe')
							{
								if($item[$arr_col_num[$i]] == 0)
								{
									$str .= $additional_time;//'В наличии';
								}
								else
								{
									$str .= $item[$arr_col_num[$i]] + $additional_time;// . ' дн.';
								}
							}
							else
							{
								if($arr_col_num[$i] == 'name')
								{
									$str_name = str_replace(';',', ',$item[$arr_col_num[$i]]);
									$str_name = str_replace("\t",'',$str_name);
									$str_name = str_replace("\r",'',$str_name);
									$str_name = str_replace("\n",'',$str_name);
									$str .= trim($str_name);
								}
								else if($arr_col_num[$i] == 'article')
								{
									$article_str = $item[$arr_col_num[$i]];
									$article_str = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article_str), "UTF-8");
									$article_str .= "\t";//Добавляем табуляцию что бы убрать усечение нулей в excel
									$str .= $article_str;
								}
								else if($arr_col_num[$i] == 'manufacturer')
								{
									$manufacturer_str = $item[$arr_col_num[$i]];
									$manufacturer_str = htmlentities(mb_strtoupper(trim($manufacturer_str), "UTF-8"), ENT_QUOTES, "UTF-8");
									$str .= $manufacturer_str;
								}
								else if($arr_col_num[$i] == 'min_order')
								{
									if(empty($item[$arr_col_num[$i]]))
									{
										$item[$arr_col_num[$i]] = 1;
									}
									$str .= $item[$arr_col_num[$i]];
								}
								else
								{
									$str .= $item[$arr_col_num[$i]];
								}
							}
							
							if($i < $max_col)$str .= ';';
						}
						else
						{
							$str .= ';';
						}
					}
					
					//Добавляем дополнительные колонки (url товара, картинка товара, описание товара)
					$str .= ';';
					$str .= str_replace(' ', '_', str_replace("\t","",$article_str).'_'.$manufacturer_str).';';
					$str .= ';';
					$str .= ';';
					$str .= ';';
					
					$str = iconv('UTF-8', 'windows-1251', $str);
					
					if(!empty($str))
					{
						$str .= "\r\n";
						fwrite($file, $str);
					}
					
				}
			}

			if(!empty($arr_category) && is_array($arr_category))
			{					
				$category_id_sql = '';
				foreach($arr_category as $category)
				{
					if($category_id_sql != '')
					{
						$category_id_sql .= ', ';
					}
					$category_id_sql .= (int)$category;
				}
				
				
				
				foreach( $arr_storages AS $storage_id )
				{
					//Отсеиваем склады с типом, не равным Docpart Price
					$storage_query = $db_link->prepare("SELECT * FROM `shop_storages` WHERE `id` = ?;");
					$storage_query->execute( array($storage_id) );
					$storage_record = $storage_query->fetch();
					if($storage_record["interface_type"] != 1)
					{
						continue;
					}
					
					$storage_connection_options = json_decode($storage_record["connection_options"], true);
					
					//Валюта склада
					$currency = $storage_record["currency"];
					
					//Курс валюты
					if( $currency == $DP_Config->shop_currency )
					{
						$currency_rate = 1;
					}
					else
					{
						$currency_rate_query = $db_link->prepare("SELECT `rate` FROM `shop_currencies` WHERE `id` = ?;");
						$currency_rate_query->execute( array($currency) );
						$currency_rate_record = $currency_rate_query->fetch();
						$currency_rate = $currency_rate_record["rate"];
					}
					
					
					//Получаем дополнительный срок доставки
					$additional_time_query = $db_link->prepare("SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? LIMIT 1;");
					$additional_time_query->execute( array($offices, $storage_id) );
					$additional_time_record = $additional_time_query->fetch();
					$additional_time = (int)($additional_time_record["additional_time"]/24);
					
					
					
					$SQL_products = "SELECT 
							*,
							(SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = `shop_storages_data`.`category_id`) AS `category_url`,
							(SELECT `alias` FROM `shop_catalogue_products` WHERE `id` = `shop_storages_data`.`product_id`) AS `product_alias`,
							(SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = `shop_storages_data`.`product_id`) AS `name`,
							(SELECT `content` FROM `shop_products_text` WHERE `product_id` = `shop_storages_data`.`product_id` LIMIT 1) AS `content`,
							(SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = `shop_storages_data`.`product_id` LIMIT 1) AS `img`,
							(SELECT `value` FROM `shop_properties_values_text` WHERE `product_id` = `shop_storages_data`.`product_id` AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `value` LIKE 'Артикул' AND `property_type_id` = 3 AND `category_id` = `shop_storages_data`.`category_id` LIMIT 1) LIMIT 1) AS `article`,
							
							
							(SELECT `value` FROM `shop_line_lists_items` WHERE `id` = (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = `shop_storages_data`.`product_id` AND `property_id` = (SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = `shop_storages_data`.`category_id` AND `value` LIKE 'Производитель' AND `property_type_id` = 5 LIMIT 1) LIMIT 1) LIMIT 1) AS `manufacturer`,
							
							
							(SELECT `markup`/100 AS `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= `shop_storages_data`.`price` AND `max_point` > `shop_storages_data`.`price`) AS `markup`
							
						FROM 
							`shop_storages_data`
						WHERE 
							`storage_id` = ? AND `category_id` IN ($category_id_sql);";
							
						//echo $SQL_products;
						
					$products_query = $db_link->prepare($SQL_products);
					$products_query->execute( array($offices, $storage_id, $group, $storage_id) );
					while( $item = $products_query->fetch() )
					{
						// >>>
						
						$item['name'] = str_replace(array("&amp;","&frasl;","frasl;", "&", ";"), " ", translate_str_by_key($item['name']));
						$item['article'] = str_replace(array("&amp;","&frasl;","frasl;", "&", ";"), " ", translate_str_by_key($item['article']));
						$item['manufacturer'] = str_replace(array("&amp;","&frasl;","frasl;", "&", ";"), " ", translate_str_by_key($item['manufacturer']));

						$item['article'] = trim($item['article']);
						$item['manufacturer'] = trim($item['manufacturer']);
						
						//URL товара
						if($DP_Config->product_url == "id")
						{
							$item['url'] = $DP_Config->domain_path.$item['category_url']."/".$item["product_id"];
						}
						else
						{
							$item['url'] = $DP_Config->domain_path.$item['category_url']."/".$item["product_alias"];;
						}
						
						//Описание товара
						$item['content'] = str_replace(array("\n","\r","\t",";"),', ',translate_str_by_key($item['content']));
						
						//Картинка товара
						$item['img'] = trim($item['img']);
						if( ! strpos($item['img'], "/") && !empty($item['img']) )
						{
							$item['img'] = $DP_Config->domain_path."content/files/images/products_images/".$item['img'];
						}
						
						if(empty($item['price']))
						{
							continue;
						}
						if(empty($item['article']) && empty($item['name']))
						{
							continue;
						}
						
						$article_str = '';
						$manufacturer_str = '';
						$str = '';
						for($i=1; $i<=$max_col; $i++)
						{
							if(!empty($arr_col_num[$i]))
							{
								if($arr_col_num[$i] == 'price')
								{
									//Переводим в валюту сайта
									$item[$arr_col_num[$i]] = (float)($item[$arr_col_num[$i]] * $currency_rate);
									
									//Наценка
									$item[$arr_col_num[$i]] = $item[$arr_col_num[$i]] + ($item[$arr_col_num[$i]] * $item["markup"]);
									
									//Округление цены
									$work_price = $item[$arr_col_num[$i]];
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
									$item[$arr_col_num[$i]] = $work_price;
	
									$str .= (float)number_format($item[$arr_col_num[$i]],2,'.','');
								}
								else if($arr_col_num[$i] == 'time_to_exe')
								{										
									if( $item["arrival_time"] < time() )
									{
										$str .= ((int)$item["time_to_exe"] + $additional_time);//'В наличии';
									}
									else
									{
										$str .= (int)(($item["arrival_time"] - time() )/60/60/24) + $additional_time;// . ' дн.';
									}
								}
								else
								{
									if($arr_col_num[$i] == 'name')
									{
										$str_name = str_replace(';',' ',$item[$arr_col_num[$i]]);
										$str_name = str_replace("\r",'',$str_name);
										$str_name = str_replace("\n",'',$str_name);
										$str .= trim($str_name);
									}
									else if($arr_col_num[$i] == 'min_order')
									{
										if(empty($item[$arr_col_num[$i]]))
										{
											$item[$arr_col_num[$i]] = 1;
										}
										$str .= $item[$arr_col_num[$i]];
									}
									else if($arr_col_num[$i] == 'article')
									{
										$article_str = $item[$arr_col_num[$i]];
										$article_str = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article_str), "UTF-8");
										$article_str .= "\t";//Добавляем табуляцию что бы убрать усечение нулей в excel
										$str .= $article_str;
									}
									else if($arr_col_num[$i] == 'manufacturer')
									{
										$manufacturer_str = $item[$arr_col_num[$i]];
										$manufacturer_str = htmlentities(mb_strtoupper(trim($manufacturer_str), "UTF-8"), ENT_QUOTES, "UTF-8");
										$str .= $manufacturer_str;
									}
									else
									{
										$str .= $item[$arr_col_num[$i]];
									}
								}
								
								if($i < $max_col)$str .= ';';
							}
							else
							{
								$str .= ';';
							}
						}
						
						//Добавляем дополнительные колонки (url товара, картинка товара, описание товара)
						$str .= ';';
						$str .= str_replace(' ', '_', str_replace("\t","",$article_str).'_'.$manufacturer_str).';';
						$str .= trim($item['url']).';';
						$str .= trim($item['img']).';';
						$str .= trim($item['content']).';';
						
						$str = iconv('UTF-8', 'windows-1251', $str);
						
						if(!empty($str))
						{
							$str .= "\r\n";
							fwrite($file, $str);
						}
					}
				}
			}
		}

		//Если не было ошибок
		if( $is_mailing )
		{
			$debug->logger(translate_str_by_key('1711374149_1_5f735d1486aa51eb9a61df1cd635a0fb'));
			$send_result = true;
			//Почтовый обработчик
			//require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer_distribution.php");
			require_once($_SERVER["DOCUMENT_ROOT"]."/lib/DocpartMailer/docpart_mailer.php");
			$subject = translate_str_by_key($DP_Config->site_name)." ".translate_str_by_key('3660');
			$body = "<p>".translate_str_by_key('3660')." ".translate_str_by_key($DP_Config->site_name)." ".translate_str_by_key('1711373666_1_5f735d1486aa51eb9a61df1cd635a0fb')." ".date('d-m-Y', time())."</p>";
			//$body .= '<p>Отказаться от рассылке можно в <a href="http://yamato.kg/users/editform" target="_blank">личном кабинете</a></p>';
			$new_name_file = "prices_".date("d_m_Y", time()).".csv";//Имя файла, которое будет указано в письме
			if(isset($request_object['pattern_name'])) {
				$price_name = $request_object['pattern_name'].'.csv';
			}
			$users_list = $request_object['users_list'];
			$emails_list = explode(',', $request_object['emails_list']);
			$group_id_my_list_emails = (int)$request_object['group_id_my_list_emails'];
			$file = $_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/$price_name";
			$debug->logger(translate_str_by_key('1711374417_1_5f735d1486aa51eb9a61df1cd635a0fb').".", file_exists($file), true);
			$debug->logger(translate_str_by_key('1711374451_1_5f735d1486aa51eb9a61df1cd635a0fb').".", $users_list, true);
			$debug->logger(translate_str_by_key('1711374475_1_5f735d1486aa51eb9a61df1cd635a0fb').".", $emails_list, true);
			if(is_array($users_list) && !empty($users_list))
			{
				foreach($users_list as $user)
				{
					$sql = "SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? LIMIT 1;";
					$query = $db_link->prepare($sql);
					$query->execute( array($user) );
					$rov = $query->fetch();
					$group_id = $rov['group_id'];

					$sql = "SELECT `user_id`, `email` FROM `users` WHERE `user_id` = ?";
					$query = $db_link->prepare($sql);
					$query->execute( array($user) );
					while($rov = $query->fetch() )
					{
						$user_id = (int)$rov['user_id'];
						$email = trim($rov['email']);
						if(!empty($group_id) && !empty($email))
						{
							$price_name = "prices_$group_id.csv";
							if(isset($request_object['pattern_name'])) {
								$price_name = $request_object['pattern_name'].'.csv';
							}
							$file = $_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/$price_name";
							$debug->logger(translate_str_by_key('1711374417_1_5f735d1486aa51eb9a61df1cd635a0fb').".", file_exists($file), true);
							if(file_exists($file))
							{
								$docpartMailer = new DocpartMailer();//Объект обработчика
								$docpartMailer->Subject = $subject;//Тема письма
								$docpartMailer->Body = $body;//Текст письма
								$docpartMailer->CharSet="UTF-8";
								$docpartMailer->addAddress($email, $email);// Добавляем адрес в список получателей
								$docpartMailer->addAttachment($file, $price_name);// файл
								$docpartMailer->IsSMTP();
								$docpartMailer->IsHTML(true);
								if(!$docpartMailer->Send())
								{
									//Обработать ошибку отправки
									$send_result = false;
								}
							}
						}
					}
				}
			}
			if(!empty($emails_list))
			{
				foreach($emails_list as $email)
				{
					$email = trim($email);
					$group_id = $group_id_my_list_emails;
					if(!empty($group_id) && !empty($email))
					{
						$price_name = "prices_$group_id.csv";
						if(isset($request_object['pattern_name'])) {
							$price_name = $request_object['pattern_name'].'.csv';
						}
						$file = $_SERVER["DOCUMENT_ROOT"]."/content/files/Documents/prices_tmp/$price_name";
						if(file_exists($file))
						{
							$docpartMailer = new DocpartMailer();//Объект обработчика
							$docpartMailer->Subject = $subject;//Тема письма
							$docpartMailer->Body = $body;//Текст письма
							$docpartMailer->CharSet="UTF-8";
							$docpartMailer->addAddress($email, $email);// Добавляем адрес в список получателей
							$docpartMailer->addAttachment($file, $price_name);// файл
							$docpartMailer->IsSMTP();
							$docpartMailer->IsHTML(true);
							if(!$docpartMailer->Send())
							{
								//Обработать ошибку отправки
								$send_result = false;
							}
						}
					}
				}
			}
			if($send_result)
			{
				$debug->logger(translate_str_by_key('1711374559_1_5f735d1486aa51eb9a61df1cd635a0fb').".");
				$answer['status'] = true;
			}
			else
			{
				$debug->logger(translate_str_by_key('1711374618_1_5f735d1486aa51eb9a61df1cd635a0fb').".");
				$answer['message'] = translate_str_by_key('1711374672_1_5f735d1486aa51eb9a61df1cd635a0fb').'.';
			}
		}

		return true;
	}

	return false;
}



?>