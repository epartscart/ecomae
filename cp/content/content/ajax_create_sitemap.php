<?php
/**
 * Серверный скрипт для создания файла sitemap.xml
*/
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
	$answer["status"] = false;
	$answer["message"] = "No DB connect";
	exit( json_encode($answer) );
}
$db_link->query("SET NAMES utf8;");



//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

//Проверяем доступ в панель управления
if( ! DP_User::isAdmin())
{
	$answer = array();
	$answer["status"] = false;
	$answer["message"] = 'Forbidden';
	exit(json_encode($answer));
}



// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------



// Список выбранных материалов
$url_list = json_decode($_POST["url_list"], true);


// Максимальное число url в одном файле
$max_url = 30000;

// Максимальный вес файла в байтах (5 MB)
$max_size = 5242880;

// Количество созданных файлов sitemap
$cnt__sitemap = 0;


// Удаляем старые файлы
$i = 1;
while(file_exists($_SERVER["DOCUMENT_ROOT"]."/sitemap".$i.".xml")){
	unlink($_SERVER["DOCUMENT_ROOT"]."/sitemap".$i.".xml");
	$i++;
}


// Формируем файлы sitemap
$i = 1;
foreach($url_list as $url){
	
	$url = $url['url'];
	
	if($cnt__sitemap === 0){
		$cnt__sitemap++;
		// Создаем файл
		$file_path = $_SERVER["DOCUMENT_ROOT"]."/sitemap".$cnt__sitemap.".xml";
		$sitemap = fopen($file_path, "w");
		$i = 1;
		fwrite($sitemap, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
		fwrite($sitemap, "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
	}
	
	
	fwrite($sitemap, "<url>\n");
        fwrite($sitemap, "<loc>".$DP_Config->domain_path.$url."</loc>\n");
        fwrite($sitemap, "<changefreq>monthly</changefreq>\n");
        fwrite($sitemap, "<priority>1</priority>\n");
    fwrite($sitemap, "</url>\n");
	
	
	$i++;
	
	
	if( (filesize($_SERVER["DOCUMENT_ROOT"]."/sitemap".$cnt__sitemap.".xml") > $max_size) || $i > $max_url){
		fwrite($sitemap, "</urlset>");
		fclose($sitemap);
		$cnt__sitemap++;
		
		// Создаем файл
		$file_path = $_SERVER["DOCUMENT_ROOT"]."/sitemap".$cnt__sitemap.".xml";
		$sitemap = fopen($file_path, "w");
		$i = 1;
		fwrite($sitemap, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
		fwrite($sitemap, "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
	}
}





/////////////////////////////////////////////////////////////////////////////////////////////////





// Формируем массив url товаров каталога
$url_array = array();
$no_published_flag_categories = array();

$all_categories_query = $db_link->prepare("SELECT `id`, `url`, `parent`, `published_flag` FROM `shop_catalogue_categories` ORDER BY `level`, `order`");
$all_categories_query->execute();

while($category_record = $all_categories_query->fetch())
{
	
	if($category_record["published_flag"] == 0){
		$no_published_flag_categories[$category_record["id"]] = true;
		continue;
	}
	
	if($no_published_flag_categories[$category_record["parent"]] == true){
		$no_published_flag_categories[$category_record["id"]] = true;
		continue;
	}
	
	$category_url = $category_record["url"];
	$category_id  = (int) $category_record["id"];
	
	// Категория 
	$url_array[] = $category_url;
	
	// Продукты
	$products_query = $db_link->prepare("SELECT `id`, `alias`, `published_flag` FROM `shop_catalogue_products` WHERE `category_id` = $category_id;");
	$products_query->execute();
	
    while($product = $products_query->fetch())
    {
        
		if($product["published_flag"] == 0){
			continue;
		}
		
		//URL товара
        if($DP_Config->product_url == "id")
        {
            $product_url = $category_url."/".$product["id"];
        }
        else
        {
            $product_url = $category_url."/".$product["alias"];
        }
        
		$url_array[] = $product_url;
    }
}

// Формируем файлы sitemap
foreach($url_array as $url){
	
	if($cnt__sitemap === 0){
		$cnt__sitemap++;
		//Создаем файл
		$file_path = $_SERVER["DOCUMENT_ROOT"]."/sitemap".$cnt__sitemap.".xml";
		$sitemap = fopen($file_path, "w");
		$i = 1;
		fwrite($sitemap, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
		fwrite($sitemap, "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
	}
	
	
	fwrite($sitemap, "<url>\n");
        fwrite($sitemap, "<loc>".$DP_Config->domain_path.$url."</loc>\n");
        fwrite($sitemap, "<changefreq>monthly</changefreq>\n");
        fwrite($sitemap, "<priority>1</priority>\n");
    fwrite($sitemap, "</url>\n");
	
	
	$i++;
	
	
	if( (filesize($_SERVER["DOCUMENT_ROOT"]."/sitemap".$cnt__sitemap.".xml") > $max_size) || $i > $max_url){
		fwrite($sitemap, "</urlset>");
		fclose($sitemap);
		$cnt__sitemap++;
		
		// Создаем файл
		$file_path = $_SERVER["DOCUMENT_ROOT"]."/sitemap".$cnt__sitemap.".xml";
		$sitemap = fopen($file_path, "w");
		$i = 1;
		fwrite($sitemap, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
		fwrite($sitemap, "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
	}
}


// Закрываем последний файл
fwrite($sitemap, "</urlset>");
fclose($sitemap);


// Формируем индексный файл
$file_path = $_SERVER["DOCUMENT_ROOT"]."/sitemap.xml";
$sitemap = fopen($file_path, "w");
fwrite($sitemap, "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
fwrite($sitemap, "<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
for($i=$cnt__sitemap; $i > 0; $i--)
{
	fwrite($sitemap, "<sitemap>\n");
        fwrite($sitemap, "<loc>".$DP_Config->domain_path."sitemap".$i.".xml"."</loc>\n");
    fwrite($sitemap, "</sitemap>\n");
}
fwrite($sitemap, "</sitemapindex>");
fclose($sitemap);


echo "Ok";
?>