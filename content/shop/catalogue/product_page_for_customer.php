<?php
/**
 * Скрипт страницы продукта
*/
defined('_ASTEXE_') or die('No access');
?>

<div class="product_page">

<?php
//ДЛЯ РАБОТЫ С ПОЛЬЗОВАТЕЛЕМ
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_pricing.php");
$userProfile = DP_User::getUserProfile();
$group_id = $userProfile["groups"][0];//Берем первую группу пользователя
$user_id = DP_User::getUserId();
if (function_exists('epc_pricing_resolve_customer_group_id')) {
	$group_id = epc_pricing_resolve_customer_group_id($db_link, (int) $user_id, (int) $group_id);
}

//Указатель валюты
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/general/get_currency_indicator.php");

//Получить список магазинов покупателя
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");

//Техническая информация по интернте-магазину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/orders_background.php");

//Выводим страницу товара:
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProduct_Info.php");


//Подключаем скрипт с общей функцией вывода блока товара ( printProductBlock(product) )
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/helper.php");
//ТИП БЛОКА (1,2,3,4,5)
$product_block_type = 5;//ТИП ДЛЯ БЛОКОВ ТОВАРОВ НА ГЛАВНОЙ, СОПУТСТВУЮЩИХ ТОВАРОВ
//СТИЛЬ БЛОКА ДЛЯ ТОВАРА
$main_class_of_block = "product_div_tile col-xs-12 col-sm-4 col-md-3 col-lg-1-5";//5 колонок


//Подстрока для умножение цены на курс валюты склада
$SQL_currency_rate = "(SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`) )";
?>


<div class="row"></div>
<div class="col-lg-12">
	<h2 class="section-title"><?php echo translate_str_by_id(4164); ?></h2>
	<div class="product_suggestions_box">
		<div class="product_suggestions product_suggestions_header">
			<div class="price">
				<?php echo translate_str_by_id(2751); ?>
			</div>
			<div class="exist">
				<?php echo translate_str_by_id(1717); ?>
			</div>
			<div class="reserved">
				<?php echo translate_str_by_id(4098); ?>
			</div>
			<div class="exist_details">
				<?php echo translate_str_by_id(3550); ?>
			</div>
		</div>

		<?php
		$time_now = time();
		//Для каждого магазина получить список складов и опросить каждый склад
		for($i=0; $i < count($customer_offices); $i++)
		{
			$office_id = $customer_offices[$i];
			?>
			<div class="product_office">
				<?php
				$office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
				$office_query->execute(array($office_id));
				$office_info = $office_query->fetch(PDO::FETCH_ASSOC);
				?>
				<span><?php echo trim(translate_str_by_id($office_info['caption'])) .'<div><small>'. trim(translate_str_by_id($office_info['city'])) .', '. trim(translate_str_by_id($office_info['address'])) .'</small></div>'; ?></span>
			</div>
			<?php
			$flag_offers = false;
			
			//Получаем id товаров по цене с данного склада
			$product_query = $db_link->prepare('SELECT *, CAST(`price`*'.$SQL_currency_rate.' AS decimal(10,2)) AS `price`, (SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = `shop_storages_data`.`storage_id` LIMIT 1) AS `additional_time` FROM `shop_storages_data` WHERE `product_id` = ? AND (`price`>0 AND (`exist`>0 OR `reserved`>0)) AND `storage_id` IN(SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ?) ORDER BY `price`, `exist`;');
			$product_query->execute( array($office_id, $product_id, $office_id) );
			while($product = $product_query->fetch())
			{
				$price = $product["price"];
				$storage_id = $product["storage_id"];
				$additional_time = $product["additional_time"];
				$storage_record_id = $product["id"];
				$exist = $product["exist"];
				$main_action_html = "";
				$div_id = $office_id."_".$storage_id."_".$storage_record_id;
				$flag_offers = true;
				
				
				//Получаем наценку:
				$purchase_price = (float) $price;
				$markup_query = $db_link->prepare('SELECT `markup`/100 as `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= ? AND `max_point` > ?;');
				$markup_query_args = array($office_id, $storage_id, $group_id, $price, $price);
				$markup_query->execute($markup_query_args);
				$markup_record = $markup_query->fetch();
				$price = $price + $price*$markup_record["markup"];//Накидываем наценку
				// CP price-management stack (guest 40% / retail profile / B2B profile)
				if (function_exists('epc_pricing_apply_sell_from_purchase')) {
					$epc_brand = (string) ($product['manufacturer'] ?? '');
					$epc_article = (string) ($product['article'] ?? $product['article_show'] ?? '');
					$epc_sell = epc_pricing_apply_sell_from_purchase(
						$db_link,
						(int) $group_id,
						$epc_brand,
						$purchase_price,
						$epc_article,
						(int) $storage_id
					);
					if (!empty($epc_sell['visible']) && (float) $epc_sell['price'] > 0) {
						$price = (float) $epc_sell['price'];
					}
				}
				
				
				//ОБРАБАТЫВАЕМ СРОК ПОСТАВКИ
				if(time() < $product["arrival_time"]){
					$time_to_exe = (int)((($product["arrival_time"] + ($additional_time * 3600)) - time()) / 86400);
				}else{
					if($product["time_to_exe"] > 0){
						$time_to_exe = $product["time_to_exe"] + ((int)($additional_time / 24));
					}else{
						$time_to_exe = ((int)($additional_time / 24));
					}
				}
				$product["time_to_exe"] = $time_to_exe;
				
				
				//ОБРАБАТЫВАЕМ ОКРУГЛЕНИЕ ЦЕН
				if($DP_Config->price_rounding == '1')//Без копеечной части
				{
					if( $price != (int)$price )
					{
						$price = (int)$price + 1;
					}
					else
					{
						$price = (int)$price;
					}
				}
				else if($DP_Config->price_rounding == '2')//До 5 руб
				{
					$price = (integer)$price;
					$price_str = (string)$price;
					$price_str_last_char = (integer)$price_str[strlen($price_str)-1];
					if($price_str_last_char > 0 && $price_str_last_char < 5)
					{
						$price = $price + (5 - $price_str_last_char);
					}
					else if($price_str_last_char > 5 && $price_str_last_char <= 9)
					{
						$price = $price + (10 - $price_str_last_char);
					}
				}
				else if($DP_Config->price_rounding == '3')//До 10 руб
				{
					$price = (integer)$price;
					$price_str = (string)$price;
					$price_str_last_char = (integer)$price_str[strlen($price_str)-1];
					if($price_str_last_char != 0)
					{
						$price = $price + (10 - $price_str_last_char);
					}
				}
				
				
				//Указываем проверочный хеш для предотвращения подмены данных злоумышленниками через Javascript
				$check_hash = md5($product_id.$office_id.$storage_id.$storage_record_id.$price.$DP_Config->tech_key);
				?>
				<div
					id = "<?php echo $div_id; ?>"
					product_id = "<?php echo $product_id; ?>"
					office_id = "<?php echo $office_id; ?>"
					storage_id = "<?php echo $storage_id; ?>"
					storage_record_id = "<?php echo $storage_record_id; ?>"
					price = "<?php echo $price; ?>"
					time_to_exe = "<?php echo $time_to_exe; ?>"
					exist = "<?php echo $exist; ?>"
					check_hash = "<?php echo $check_hash; ?>"
				></div>
				
				
				<div class="product_suggestions">
					<div class="price">
						<?php echo $price; ?>
					</div>
					<div class="exist">
						<?php echo $product["exist"]; ?>
					</div>
					<div class="reserved">
						<?php echo $product["reserved"]; ?>
					</div>
					<div class="exist_details">
						<?php
						if($product["time_to_exe"] == 0 && $exist > 0)
						{
							?>
							<?php echo translate_str_by_id(4094); ?>
							<?php
							$main_action_html = '<div class="btn-ar btn-primary cart_btn_purchase_action">
								<table><tr><td><div class="product_div_count_need"><a class="count_need_minus" href="javascript:void(0);" onclick="minusCountNeed(\''.$div_id.'\', '.$exist.', '.$min_order.');">-</a><input class="count_need_input count_need_'.$div_id.'" type="text" value="'.$min_order.'" onchange="onKeyUpCountNeed(\''.$div_id.'\', '.$exist.', '.$min_order.');"/><a class="count_need_plus" href="javascript:void(0);" onclick="plusCountNeed(\''.$div_id.'\', '.$exist.', '.$min_order.');">+</a></div></td><td><a href="javascript:void(0);" onclick="purchase_action(\''.$div_id.'\');">'.translate_str_by_id(4096).'</a></td></tr></table>
							</div>';
							
						}
						else if($exist > 0)
						{
							?>
							<?php echo $product["time_to_exe"]; ?> дн.
							<?php
							$main_action_html = '<div class="btn-ar btn-primary cart_btn_purchase_action">
								<table><tr><td><div class="product_div_count_need"><a class="count_need_minus" href="javascript:void(0);" onclick="minusCountNeed(\''.$div_id.'\', '.$exist.', '.$min_order.');">-</a><input class="count_need_input count_need_'.$div_id.'" type="text" value="'.$min_order.'" onchange="onKeyUpCountNeed(\''.$div_id.'\', '.$exist.', '.$min_order.');"/><a class="count_need_plus" href="javascript:void(0);" onclick="plusCountNeed(\''.$div_id.'\', '.$exist.', '.$min_order.');">+</a></div></td><td><a href="javascript:void(0);" onclick="purchase_action(\''.$div_id.'\');">'.translate_str_by_id(4096).'</a></td></tr></table>
							</div>';
						}
						else
						{
							?>
							<?php echo translate_str_by_id(4098); ?>
							<?php
							$main_action_html = "";
						}
						?>
					</div>
					<div class="purchase">
						<?php echo $main_action_html; ?>
					</div>
				</div>
				<?php
			}
			
			if($flag_offers === false){
				?>
				<div class="product_suggestions">
					<div style="padding-left:20px;">
						<?php echo translate_str_by_id(4165); ?>
					</div>
					<div class="purchase">
						<div class="btn-ar btn-primary cart_btn_purchase_action">
							<table><tr><td><div class="product_div_count_need"></div></td><td><a href="<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu" target="_blank"><?php echo translate_str_by_id(4115); ?></a></td></tr></table>
						</div>
					</div>
				</div>
				<?php
			}
		}
		?>

	</div>
</div>






<div class="col-md-12">
	<h2 class="section-title"><?php echo translate_str_by_id(2069); ?></h2>
	<!-- Nav tabs -->
	<ul class="nav nav-tabs">
		<li class="active"><a href="#tab_product_1" data-toggle="tab"><?php echo translate_str_by_id(3164); ?></a></li>
		<li><a href="#tab_product_2" data-toggle="tab"><?php echo translate_str_by_id(2073); ?></a></li>
		<li><a href="#tab_product_3" data-toggle="tab"><?php echo translate_str_by_id(4148); ?></a></li>
	</ul>
	 
	<!-- Tab panes -->
	<div class="tab-content navbar-inverse">
		<div class="tab-pane active" id="tab_product_1">
			<?php
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/product_specifications.php");
			// Rich SKU photos + multi-type specification sheets (CP-managed).
			$epcSkuMediaStorefront = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/epc_sku_media_storefront.php';
			if (is_file($epcSkuMediaStorefront) && isset($db_link) && $db_link instanceof PDO) {
				require_once $epcSkuMediaStorefront;
				$epcSkuBrand = '';
				$epcSkuArticle = '';
				if (!empty($product_record) && is_array($product_record)) {
					$epcSkuBrand = (string) ($product_record['manufacturer'] ?? $product_record['brand'] ?? '');
					$epcSkuArticle = (string) ($product_record['article'] ?? '');
				}
				if (function_exists('epc_sku_media_render_storefront')) {
					epc_sku_media_render_storefront($db_link, array(
						'product_id' => (int) ($product_id ?? 0),
						'brand' => $epcSkuBrand,
						'article' => $epcSkuArticle,
						'show_photos' => true,
						'show_specs' => true,
					));
				}
			}
			?>
		</div>
		
		<div class="tab-pane" id="tab_product_2">
			<?php
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/product_description.php");
			?>
		</div>
		
		<div class="tab-pane" id="tab_product_3">
			<?php
			require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/product_evaluations.php");
			?>
		</div>
	</div>
</div>




<?php
// -----------------------------------------------------------------------------------
// START ВАРИАНТЫ ИСПОЛНЕНИЯ ТОВАРА

//Для отсутствующих значений:
$for_null_of_lists = translate_str_by_id(4166);

//Получаем перечень свойств данной категории
$properties_list = array();
$category_properties_query = $db_link->prepare('SELECT *, (SELECT `type` FROM `shop_line_lists` WHERE `id` = `shop_categories_properties_map`.`list_id`) AS `list_type` FROM `shop_categories_properties_map` WHERE `category_id` = ?;');
$category_properties_query->execute( array($category_id) );
while( $property = $category_properties_query->fetch() )
{
	array_push($properties_list, array("id"=>$property["id"], "type_id"=>$property["property_type_id"], "list_id"=>$property["list_id"], "list_type"=>$property["list_type"])  );
}

$head_line_shown = false;//Флаг - заголовок ВСЕГО БЛОКА показан

//Блок с выводом вариантов исполнения товара
$options_properties_query = $db_link->prepare('SELECT * FROM `shop_categories_properties_map` WHERE `category_id` = ? AND `is_option`=1;');
$options_properties_query->execute( array($category_id) );
while( $option_property = $options_properties_query->fetch() )
{
	//Название свойства
	$option_property_caption = $option_property["value"];
	
	$head_line_property_shown = false;//Флаг - заголовок СВОЙСТВА показан
	
	//Формируем подстроку с условие: нужны товары этой же категории, у которых равны все свойства кроме данного
	$SQL_PROPERTIES_CONDITIONS = "";
	$binding_args_params = array();
	for($i=0; $i < count($properties_list); $i++)
	{
		$property_type_id = (int)$properties_list[$i]["type_id"];
		$property_id = (int)$properties_list[$i]["id"];
		
		//Знак равенства
		$equal = " = ";
		if( $property_id == $option_property["id"] )
		{
			$equal = " != ";
		}
		
		switch( $property_type_id )
		{
			case 1:
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT `value` FROM shop_properties_values_int WHERE product_id = shop_catalogue_products.id AND `property_id` = ?), 0) '.$equal.' IFNULL((SELECT `value` FROM shop_properties_values_int WHERE product_id = ? AND `property_id` = ?),0)';
				
				array_push($binding_args_params, $property_id);
				array_push($binding_args_params, $product_id);
				array_push($binding_args_params, $property_id);
				
				break;
			case 2:
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT `value` FROM shop_properties_values_float WHERE product_id = shop_catalogue_products.id AND `property_id` = ?),0) '.$equal.' IFNULL((SELECT `value` FROM shop_properties_values_float WHERE product_id = ? AND `property_id` = ?),0)';
				
				array_push($binding_args_params, $property_id);
				array_push($binding_args_params, $product_id);
				array_push($binding_args_params, $property_id);
				
				break;
			case 3:
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT `value` FROM shop_properties_values_text WHERE product_id = shop_catalogue_products.id AND `property_id` = ?),\'\') '.$equal.' IFNULL((SELECT `value` FROM shop_properties_values_text WHERE product_id = ? AND `property_id` = ?),\'\')';
				
				
				array_push($binding_args_params, $property_id);
				array_push($binding_args_params, $product_id);
				array_push($binding_args_params, $property_id);
				
				break;
			case 4:
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT `value` FROM shop_properties_values_bool WHERE product_id = shop_catalogue_products.id AND `property_id` = ?),0) '.$equal.' IFNULL((SELECT `value` FROM shop_properties_values_bool WHERE product_id = ? AND `property_id` = ?),0)';
				
				array_push($binding_args_params, $property_id);
				array_push($binding_args_params, $product_id);
				array_push($binding_args_params, $property_id);
				
				break;
			case 5:
				if((int)$properties_list[$i]["list_type"] == 1)
				{
					//Для списка с единичным выбором
					$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT `value` FROM shop_properties_values_list WHERE product_id = shop_catalogue_products.id AND `property_id` = ?), ?) '.$equal.' IFNULL((SELECT `value` FROM shop_properties_values_list WHERE product_id = ? AND `property_id` = ?), ?)';
					
					array_push($binding_args_params, $property_id);
					array_push($binding_args_params, $for_null_of_lists);
					array_push($binding_args_params, $product_id);
					array_push($binding_args_params, $property_id);
					array_push($binding_args_params, $for_null_of_lists);
				}
				else
				{
					//Для списка с множественным выбором
					$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT group_concat(`value`) FROM shop_properties_values_list WHERE product_id = shop_catalogue_products.id AND `property_id` = ?), ?) '.$equal.' IFNULL((SELECT group_concat(`value`) FROM shop_properties_values_list WHERE product_id = ? AND `property_id` = ?),?)';
					
					array_push($binding_args_params, $property_id);
					array_push($binding_args_params, $for_null_of_lists);
					array_push($binding_args_params, $product_id);
					array_push($binding_args_params, $property_id);
					array_push($binding_args_params, $for_null_of_lists);
				}
				break;
			case 6:
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND IFNULL((SELECT group_concat(`value`) FROM shop_properties_values_tree_list WHERE product_id = shop_catalogue_products.id AND `property_id` = ?), ?) '.$equal.' IFNULL((SELECT group_concat(`value`) FROM shop_properties_values_tree_list WHERE product_id = ? AND `property_id` = ?), ?)';
				
				array_push($binding_args_params, $property_id);
				array_push($binding_args_params, $for_null_of_lists);
				array_push($binding_args_params, $product_id);
				array_push($binding_args_params, $property_id);
				array_push($binding_args_params, $for_null_of_lists);
				
				break;
		}
	}
	
	//Подстрока для получения значения свойства (варианта исполнения), которое отличается от данного товара
	$SQL_different_option_value = "";
	$binding_args_different_option_value = array();
	switch($option_property["property_type_id"])
	{
		case 1:
			$SQL_different_option_value = 'SELECT IFNULL(`value`, ?) FROM `shop_properties_values_int` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ?';
			break;
		case 2:
			$SQL_different_option_value = 'SELECT IFNULL(`value`, ?) FROM `shop_properties_values_float` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ?';
			break;
		case 3:
			$SQL_different_option_value = 'SELECT IFNULL(`value`, ?) FROM `shop_properties_values_text` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ?';
			break;
		case 4:
			$SQL_different_option_value = 'SELECT IFNULL(`value`, ?) FROM `shop_properties_values_bool` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ?';
			break;
		case 5:
			$SQL_different_option_value = 'SELECT IFNULL(group_concat(`value`), ?) FROM `shop_line_lists_items` WHERE `id` IN (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ?)';
			break;
		case 6:
			$SQL_different_option_value = 'SELECT IFNULL(group_concat(`value`), ?) FROM `shop_tree_lists_items` WHERE `id` IN (SELECT `value` FROM `shop_properties_values_tree_list` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ?)';
			break;
	}
	array_push($binding_args_different_option_value, $for_null_of_lists);
	array_push($binding_args_different_option_value, $option_property["id"]);
	
	$SQL_options_products = "SELECT 
				* 
			FROM 
				(SELECT
					shop_catalogue_products.id AS id,
					shop_catalogue_products.caption AS caption,
					shop_catalogue_products.alias AS alias,
					shop_catalogue_categories.url AS category_url,
					(".$SQL_different_option_value.") AS `different_option_value`
				FROM
					shop_catalogue_products
				LEFT OUTER JOIN shop_catalogue_categories ON shop_catalogue_products.category_id = shop_catalogue_categories.id
				WHERE
					shop_catalogue_products.category_id = ? ".$SQL_PROPERTIES_CONDITIONS.") AS `all` ";
	$sql_args_array = array();
	array_push($sql_args_array, $category_id);
	
	$sql_args_array = array_merge($binding_args_different_option_value, $sql_args_array);
	$sql_args_array = array_merge($sql_args_array, $binding_args_params);
	
	//Получаем все товары, у которых совпали с данным товаром все свойства, кроме этого, т.е. получаем "варианты исполнения" данного товара
	$options_products_query = $db_link->prepare($SQL_options_products);
	$options_products_query->execute($sql_args_array);
	while( $option_product = $options_products_query->fetch() )
	{
		//Здесь выводим ссылки на страницы других товаров
		if( ! $head_line_shown  )
		{
			?>
			<div class="container"><div class="row">
			<div class="col-xs-12 col-sm-12 col-md-12 col-lg-12">
				<h2 class="section-title"><?php echo translate_str_by_id(4167); ?></h2>
			<?php
			$head_line_shown = true;
		}
		
		if( ! $head_line_property_shown  )
		{
			?>
			<p class="product-option-message"><?php echo translate_str_by_id(4168); ?> <b>"<?php echo $option_property_caption; ?>"</b>:</p>
			<?php
			$head_line_property_shown = true;
		}
		
		$product_url = $option_product["alias"];
		if( $DP_Config->product_url != "alias" )
		{
			$product_url = $option_product["id"];
		}
		?>
		<a class="product-option-variant" href="<?php echo $multilang_params['lang_href']; ?>/<?php echo $option_product["category_url"]; ?>/<?php echo $product_url; ?>" title="<?php echo translate_str_by_id(4168); ?> <?php echo $option_property_caption; ?>: <?php echo $option_product["different_option_value"]; ?>"><?php echo $option_product["different_option_value"]; ?></a>
		<?php
	}
}

if( $head_line_shown  )
{
	?>
	</div></div></div>
	<?php
}

// END ВАРИАНТЫ ИСПОЛНЕНИЯ ТОВАРА
// -----------------------------------------------------------------------------------
?>
















<?php
//----------------------------------------------------------------------------------------------------------------
//START БЛОК СОПУТСТВУЮЩИХ ТОВАРОВ

// Подстрока для запроса сопутствующих товаров
$products_ids_str = "SELECT `product_id_related` FROM `shop_related_products` WHERE `product_id` = $product_id";

//Подключение построение запроса
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");

//Подключаем скрипт генерации объектов товаров единого формата ( $products_objects )
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/generate_products_objects_by_sql.php");

if( count($products_objects) != 0)//Если объекты товаров
{
	?>
	<div class="col-xs-12 col-sm-12 col-md-12 col-lg-12">
		<h2 class="section-title"><?php echo translate_str_by_id(781); ?></h2>
		<?php
		foreach( $products_objects AS $id => $product )
		{
			printProductBlock($product);
		}
		?>
	</div>
	<?php
}

//EDN БЛОК СОПУТСТВУЮЩИХ ТОВАРОВ
//----------------------------------------------------------------------------------------------------------------
?>















<?php
//----------------------------------------------------------------------------------------------------------------
//START БЛОК ПОХОЖИХ ТОВАРОВ

$block_of_similar_products = true;// Флаг символизирующий открытие блока похожих товаров, для того что бы в подзапросе использовать другую переменную с условиями

$propucts_request["products_sort_mode"]["field"] = 'random';//Сортировка
$product_from = 0;//С какого продукта начать
$product_max_count = 5;//До какого продукта показывать (НЕ включительно)

//ФОРМИРУЕМ СТРОКИ УСЛОВИЙ ДЛЯ СВОЙСТВ
$SQL_PROPERTIES_CONDITIONS = "";
$sql_args_array = array();

// Только товары той же категории
if(!empty($category_id)){
	if($SQL_PROPERTIES_CONDITIONS != ""){
		$SQL_PROPERTIES_CONDITIONS .= " AND ";
	}
	$SQL_PROPERTIES_CONDITIONS .= "(`shop_catalogue_products`.`category_id` = ?)";
	array_push($sql_args_array, $category_id);
}

// Cкрываем не опубликованные товары
if(1)
{
	if($SQL_PROPERTIES_CONDITIONS != ""){
		$SQL_PROPERTIES_CONDITIONS .= " AND ";
	}
	$SQL_PROPERTIES_CONDITIONS .= "(`shop_catalogue_products`.`published_flag` = ?)";
	array_push($sql_args_array, 1);
}

// Текущий товар не выбираем
if(!empty($product_id)){
	if($SQL_PROPERTIES_CONDITIONS != ""){
		$SQL_PROPERTIES_CONDITIONS .= " AND ";
	}
	$SQL_PROPERTIES_CONDITIONS .= "(`shop_catalogue_products`.`id` != ?)";
	array_push($sql_args_array, $product_id);
}

// Свойства данного товара которые используются для сравнения похожих товаров
$for_similar_properties_query = $db_link->prepare('SELECT * FROM shop_categories_properties_map WHERE category_id = ? AND for_similar = 1;');
$for_similar_properties_query->execute( array($category_id) );
while( $for_similar_property = $for_similar_properties_query->fetch() )
{
	$property_type_id = $for_similar_property["property_type_id"];
	$property_id = $for_similar_property["id"];
	
	switch($property_type_id)
	{
		case 1:
		case 2:
		case 3:
			if( $property_type_id == 1 )$type_postfix = "int";
			if( $property_type_id == 2 )$type_postfix = "float";
			if( $property_type_id == 3 )$type_postfix = "text";
			
			$property_value_query = $db_link->prepare('SELECT `value` FROM `shop_properties_values_'.$type_postfix.'` WHERE `product_id` = :product_id AND `property_id` = :property_id;');
			$property_value_query->bindValue(':product_id', $product_id);
			$property_value_query->bindValue(':property_id', $property_id);
			$property_value_query->execute();
			$property_value_record = $property_value_query->fetch();
			if( $property_value_record != false )
			{
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND (SELECT `value` FROM shop_properties_values_'.$type_postfix.' WHERE product_id = shop_catalogue_products.id AND `property_id` = ?) = ?';
				
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $property_value_record["value"]);
			}
			break;
		case 4:
			//Получаем значение свойства
			$property_value_query = $db_link->prepare('SELECT `value` FROM `shop_properties_values_bool` WHERE `property_id` = :property_id AND `product_id` = :product_id;');
			$property_value_query->bindValue(':property_id', $property_id);
			$property_value_query->bindValue(':product_id', $product_id);
			$property_value_query->execute();
			$property_value_record = $property_value_query->fetch();
			if( $property_value_record != false )
			{
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND (SELECT `value` FROM `shop_properties_values_bool` WHERE `product_id` = shop_catalogue_products.id AND `property_id` = ?) = ?';
				
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $property_value_record["value"]);
			}
			break;
		case 5:
			$list_type = $for_similar_property["list_type"];//Тип списка
			$list_id = $for_similar_property["list_id"];//ID списка
			
			if($list_type == 1)
			{
				$OR_AND = "OR";
			}
			else if($list_type == 2)
			{
				$OR_AND = "AND";
			}
			
			$SQL_VALUES_COND = "";//Подстрока с условиями для поля value
			
			$list_options_query = $db_link->prepare('SELECT `value` FROM `shop_properties_values_list` WHERE `property_id` = :property_id AND `product_id` = :product_id;');
			$list_options_query->bindValue(':property_id', $property_id);
			$list_options_query->bindValue(':product_id', $product_id);
			$list_options_query->execute();
			while( $list_option = $list_options_query->fetch() )
			{
				if($SQL_VALUES_COND != "")$SQL_VALUES_COND .= ' '.$OR_AND.' ';
				$SQL_VALUES_COND = $SQL_VALUES_COND . ' (SELECT `value` FROM `shop_properties_values_list` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ? AND `value` = ?) ';
				
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $list_option["value"]);
			}
			if($SQL_VALUES_COND != "")//Если строка заполнена, то по меньше мере одно значение отмечено, значит добавляем строку
			{
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND ('.$SQL_VALUES_COND.')';
			}
			break;//case 5
		case 6:
			$list_id = $for_similar_property["list_id"];//ID списка
			
			$SQL_VALUES_COND = "";//Подстрока с условиями для поля value
			
			$list_options_query = $db_link->prepare('SELECT `value` FROM `shop_properties_values_tree_list` WHERE `property_id` = :property_id AND `product_id` = :product_id;');
			$list_options_query->bindValue(':property_id', $property_id);
			$list_options_query->bindValue(':product_id', $product_id);
			$list_options_query->execute();
			while( $list_option = $list_options_query->fetch() )
			{
				if($SQL_VALUES_COND != "")$SQL_VALUES_COND .= ' OR ';
				$SQL_VALUES_COND = $SQL_VALUES_COND . ' (SELECT `value` FROM `shop_properties_values_tree_list` WHERE `product_id` = `shop_catalogue_products`.`id` AND `property_id` = ? AND `value` = ?) ';
				
				array_push($sql_args_array, $property_id);
				array_push($sql_args_array, $list_option["value"]);
			}
			if($SQL_VALUES_COND != "")//Если строка заполнена, то по меньше мере одно значение отмечено, значит добавляем строку
			{
				$SQL_PROPERTIES_CONDITIONS = $SQL_PROPERTIES_CONDITIONS . ' AND ('.$SQL_VALUES_COND.')';
			}
			break;
	}//switch($property_type_id)
}

// Завершение формирования строки
if($SQL_PROPERTIES_CONDITIONS != ""){
	$SQL_PROPERTIES_CONDITIONS = "WHERE " . $SQL_PROPERTIES_CONDITIONS;
}

//Подключение построение запроса
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");

$sql_args_array_of_similar_products = $sql_args_array;//Вынесем переменные запроса в новую переменную, потому что оснавная будет изменена

//Дорабатываем запрос для выборки лимита
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");

//Получаем список ID товаров в случайном порядке
$products_id_random = array();
$stmt = $db_link->prepare($SQL_limit);
$stmt->execute($sql_args_array_of_similar_products);
while( $product_record = $stmt->fetch() )
{
	$products_id_random[] = $product_record['id'];
}
$products_ids_str = implode(',', $products_id_random);

//Повторно выбираем товары но уже только те которые ранее получили с учетом products_ids_str
$propucts_request["products_sort_mode"]["field"] = 'price';//Сортировка
$sql_args_array = array();
$block_of_similar_products = false;// Флаг символизирующий закрытие блока похожих товаров

require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_all.php");
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/query_builder/query_products_show.php");

//Подключаем скрипт генерации объектов товаров единого формата ( $products_objects )
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/generate_products_objects_by_sql.php");

if( count($products_objects) != 0)//Если объекты товаров
{
	?>
	<div class="col-xs-12 col-sm-12 col-md-12 col-lg-12">
		<h2 class="section-title"><?php echo translate_str_by_id(4169); ?></h2>
		<?php
		foreach( $products_objects AS $id => $product )
		{
			printProductBlock($product);
		}
		?>
	</div>
	<?php
}

//EDN БЛОК ПОХОЖИХ ТОВАРОВ
//----------------------------------------------------------------------------------------------------------------
?>































<?php
//Функция добавления в корзину
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/common_add_to_basket.php");
?>

</div><!-- <div class="product_page"> -->