<?php
/**
 * Определение класс товара, выдаваемого через поиск по артикулу
 * 
 * ДАННЫЙ ОБЪЕКТ может использоваться, как для product_type = 1, так и для product_type = 2
 * 
*/

require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/pricing/epc_pricing.php");

class DocpartProduct
{
    //ПОЛЯ ПРОДУКТА
    public $manufacturer;//Производитель
    public $article;//Чистый артикул - используется техникой
    public $article_show;//Артикул для отображения
    public $name;//Наименование
    public $exist;//Количество в наличии
    public $price;//Цена
    public $time_to_exe;//Срок доставки
    public $time_to_exe_guaranteed;//Срок доставки гарантированный
    public $storage;//Склад поставщика
    public $min_order;//Минимальный заказ
    public $probability;//Вероятность заказа
    public $office_id;//ID точки обслуживания
    public $storage_id;//ID склада
    public $office_caption;//Название точки обслуживания (`caption` из таблицы shop_offices)
    public $color;//Цвет
    
    //Инициализируется только для менеджеров
    public $storage_caption;//Название склада (`name` из таблицы shop_storages)
    
    //Здесь также указываем закупочную цену и наценку
    public $price_purchase;
    public $markup;
    
    
    //ДЛЯ ВОЗМОЖНОСТИ ВЫДАЧИ ТОВАРОВ КАТАЛОГА ЧЕРЕЗ СТРОКУ ПОИСКА ПО АРТИКУЛУ
    public $product_type;//Тип продукта (Treelax/Docpart)
    public $product_id;//ID продукта в каталоге
    public $storage_record_id;//ID записи поставки на складе
    public $product_url;//URL продукта
    
		//Новый параметр для записи некоторых технических даных, например полей для SAO. У каждого поставщика свой вариант
		public $json_params;
		
		public $valid;//Флаг корректности данных продукта
	    
		public $check_hash;//Хеш для предотвращения подмены данных злоумышленниками через JavaScript
		
		public $vat_price_label = '';
		public $vat_display_mode = '';
		
		public $search_name;//Флаг показывает что товар был найден по наименованию в каталоге или прайс листе
		
		//$rest_params - Это аргумент, который можно использовать для передачи любых параметров - чтобы не масштабировать конструктор
	
	
    public function __construct($manufacturer,
        $article,
        $name,
        $exist,
        $price,
        $time_to_exe,
        $time_to_exe_guaranteed,
        $storage,
        $min_order,
        $probability,
        $office_id,
        $storage_id,
        $office_caption,
        $color,
        $storage_caption,
        $price_purchase,
        $markup,
        $product_type,
        $product_id,
        $storage_record_id,
        $url,
				$json_params = '',
				$rest_params = NULL
    )
    {
		$DP_Config = new DP_Config;//Конфигурация CMS
		
		// Если товар найден по наименованию
		if( isset($rest_params['search_name']) )
		{
			if($rest_params['search_name'] === 1)
			{
				$this->search_name = 1;
			}
		}
		
		//Сразу переводим цены в валюту сайта
		if($rest_params == null)
		{
			$rest_params = array("rate"=>1);//Если данный параметр не передан - считаем курс валюты = 1
		}
		else
		{
			if( empty($rest_params['rate']) )
			{
				$rest_params['rate'] = 1;
			}
		}
		$price = $price * $rest_params["rate"];
		$price_purchase = $price_purchase * $rest_params["rate"];
		$epc_price_profile_visible = true;
		$epc_customer_group_id = 0;
		if( isset($rest_params["customer_group_id"]) )
		{
			$epc_customer_group_id = (int)$rest_params["customer_group_id"];
		}
		else if( isset($_REQUEST["group_id"]) )
		{
			$epc_customer_group_id = (int)$_REQUEST["group_id"];
		}
		if( $epc_customer_group_id > 0 )
		{
			try
			{
				global $db_link;
				static $epc_db = null;
				if( isset($db_link) && $db_link instanceof PDO )
				{
					$epc_db = $db_link;
				}
				else if( $epc_db === null )
				{
					$epc_db = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
					$epc_db->query("SET NAMES utf8;");
				}
				// Apply CP price-management stack from warehouse purchase (not pre-mapped sell).
				if (function_exists('epc_pricing_apply_sell_from_purchase')) {
					$epc_sell = epc_pricing_apply_sell_from_purchase($epc_db, $epc_customer_group_id, $manufacturer, $price_purchase, $article);
					$epc_price_profile_visible = !empty($epc_sell['visible']);
					$price = $epc_sell['price'];
					$markup = $epc_sell['markup_decimal'];
				} else {
					$epc_rule_result = epc_pricing_apply_brand_rule($epc_db, $epc_customer_group_id, $manufacturer, $price_purchase, 0.0, $article);
					$epc_price_profile_visible = $epc_rule_result["visible"];
					$price = $epc_rule_result["price"];
					$markup = $epc_rule_result["markup_decimal"];
				}
			}
			catch(Exception $e)
			{
			}
		}

		$epc_vat_label = '';
		$epc_vat_display_mode = '';
		try
		{
			$epc_vat_file = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_customer_vat.php';
			if( is_readable($epc_vat_file) && isset($epc_db) && $epc_db instanceof PDO )
			{
				require_once $epc_vat_file;
				$epc_vat_user_id = 0;
				if( class_exists('DP_User') )
				{
					$epc_vat_user_id = (int)DP_User::getUserId();
				}
				$epc_vat_applied = epc_uae_customer_vat_apply_display_price($epc_db, (float)$price, $epc_vat_user_id);
				$price = $epc_vat_applied['display_price'];
				$epc_vat_label = (string)($epc_vat_applied['price_label'] ?? '');
				$epc_vat_display_mode = (string)($epc_vat_applied['display_mode'] ?? '');
			}
		}
		catch(Exception $e)
		{
		}
		
		//Инициализация полей
        $this->manufacturer = htmlentities(mb_strtoupper(trim($manufacturer), "UTF-8"), ENT_QUOTES, "UTF-8");
        $this->article_show = htmlentities($article);
				$this->article = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $article), "UTF-8");
        $this->name = htmlentities(str_replace(array("\"", "\\", "'", "\n", "\r", "\t"), "", $name), ENT_QUOTES, "UTF-8");
        $this->exist = (int) str_replace(array(" ", "+", ">", "<", "ш", "т", "."), "", $exist);
        $this->price = number_format($price, 2, '.', '');
        $this->time_to_exe = (int)$time_to_exe;
        $this->time_to_exe_guaranteed = (int)$time_to_exe_guaranteed;
        $this->storage = htmlentities($storage);
        $this->min_order = (int)$min_order;
				if( $this->min_order == 0 )
				{
					$this->min_order = 1;
				}
        $this->probability = (int)$probability;
        $this->office_id = $office_id;
        $this->storage_id = $storage_id;
        $this->office_caption = htmlentities($office_caption);
        $this->color = $color;
        $this->storage_caption = htmlentities($storage_caption);
        $this->price_purchase = number_format($price_purchase, 2, '.', '');
        $this->markup = (int)($markup*100);
        $this->product_type = $product_type;
        $this->product_id = $product_id;
        $this->storage_record_id = $storage_record_id;
        $this->url = $url;
				$this->json_params = $json_params;
		$this->vat_price_label = $epc_vat_label;
		$this->vat_display_mode = $epc_vat_display_mode;
		
		
		//Проверяем корректность данных
		if($this->manufacturer == "" || 
		$this->article == "" || 
		$this->exist <= 0 || 
		$this->price <= 0 ||
		$epc_price_profile_visible === false)
		{
			$this->valid = false;
		}
		else
		{
			$this->valid = true;
		}
		
    }
}
?>