<?php
/*
Скрипт для генерации карты сайта в виде html-страницы
*/
defined('_ASTEXE_') or die('No access');
?>


<style>
/* all levels */
.sitemap a
{
	color: #000;
	text-decoration: none;
}

/* first level */
.sitemap
{
	margin: 2em 0;
	list-style-type: none;
	background: url(dots1.png) repeat-y 0 0;
	padding: 0;
}

.sitemap li { display: inline; }

.sitemap li a
{
	display: block;
	padding: 0 0 0 15px;
	margin: 0;
	line-height: 24px;
	background: url(bullet1.png) no-repeat 0 0;
}

.sitemap li a.open { background: url(bullet1-open.png) no-repeat 0 0; }

/* second level */
.sitemap ul
{
	margin: 0;
	padding: 0;
	background: url(dots2.png) repeat-y 30px 0;
}

.sitemap li li a
{
	padding: 0 0 0 45px;
	background: url(bullet2.png) no-repeat 0 0;
}

.sitemap li li a.open { background: url(bullet2-open.png) no-repeat 0 0; }

/* third level */
.sitemap ul ul
{
	padding: 0;
	background: url(dots3.png) repeat-y 60px 0;
}

.sitemap li li li a
{
	padding: 0 0 0 75px;
	background: url(bullet3.png) no-repeat 0 0;
}

.sitemap li li li a.open { background: url(bullet3-open.png) no-repeat 0 0; }

/* fourth level */
.sitemap ul ul ul
{
	padding: 0;
	background: url(dots4.png) repeat-y 90px 0;
}

.sitemap li li li li a
{
	padding: 0 0 0 105px;
	background: url(bullet4.png) no-repeat 0 0;
}

.sitemap li li li li a.open { background: url(bullet4-open.png) no-repeat 0 0; }
</style>



<?php
// --------------------------------- Start PHP - метод ---------------------------------
function addContentToDump($content, $candidate_data, $candidate_id)
{
    //Знаем уровень level
    if($content["parent"] == $candidate_id)
    {
        array_push($candidate_data, $content);
        return $candidate_data;//Возвращаем массив с добавленной катерией
    }
    else
    {
        for($i=0; $i < count($candidate_data); $i++)
        {
            if($candidate_data[$i]["count"] == 0)
            {
                continue;
            }
            $current_count = count($candidate_data[$i]["data"]);//Сколько элементов в массиве до рекурсивного вызова
            $candidate_data[$i]["data"] = addContentToDump($content, $candidate_data[$i]["data"], $candidate_data[$i]["id"]);
        }
        return $candidate_data;
    }
}
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - метод ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~





function printSitemap($sitemap, $is_first)
{
	if($is_first)$ul_class = " class=\"sitemap\"";
	else $ul_class = "";
	?><ul<?php echo $ul_class; ?>><?php
	
	for($i=0; $i < count($sitemap["data"]); $i++)
	{
		?><li><a href="<?php echo $multilang_params['lang_href']; ?>/<?php echo $sitemap["data"][$i]["url"]; ?>"><?php echo $sitemap["data"][$i]["value"]; ?></a><?php
		if(count($sitemap["data"][$i]["data"]) > 0)
		{
			printSitemap($sitemap["data"][$i]);
		}
		?></li><?php
	}
	
	?></ul><?php
}





//ФОРМИРУЕМ sitemap для материалов
$root_content = array();
$root_content["data"] = array();

$stmt = $db_link->prepare('SELECT * FROM `content` WHERE `is_frontend`=:is_frontend ORDER BY `level`;');
$stmt->bindValue(':is_frontend', 1);
$stmt->execute();

while( $content_record = $stmt->fetch(PDO::FETCH_ASSOC) )
{
	//Пропускаем: Главную, раздел Пользователи, раздел Магазин
	
	if($content_record["main_flag"] == 1 || $content_record["url"]=="users" || $content_record["url"]=="shop")
	{
		continue;
	}
	
	//- создаем объект материала
	$current_content = array();
	$current_content["id"] = (integer)$content_record["id"];
	$current_content["count"] = (integer)$content_record['count'];
	$current_content["url"] = $content_record["url"];
	$current_content["level"] = (integer)$content_record['level'];
	$current_content["alias"] = $content_record["alias"];
	$current_content["value"] = $content_record["value"];
	$current_content["parent"] = (integer)$content_record['parent'];
	$current_content["data"] = array();
	
	//Добавляем объект категории в объект иерархии
	$root_content["data"] = addContentToDump($current_content, $root_content["data"], 0);
}//~for($i)
	




//Добавляем сюда страницы каталога
$root_category = array();
$root_category["data"] = array();

$stmt = $db_link->prepare('SELECT * FROM `shop_catalogue_categories` ORDER BY `level`;');
$stmt->execute();
while( $category = $stmt->fetch(PDO::FETCH_ASSOC) )
{
	$current_category = array();
	$current_category["id"] = (integer)$category["id"];
	$current_category["count"] = (integer)$category['count'];
	$current_category["url"] = $category["url"];
	$current_category["level"] = (integer)$category['level'];
	$current_category["alias"] = $category["alias"];
	$current_category["value"] = $category["value"];
	$current_category["parent"] = (integer)$category['parent'];
	$current_category["data"] = array();
	
	if($current_category["count"] == 0)//Конечная категория - заполняем товарами
	{
		$product_stmt = $db_link->prepare('SELECT * FROM `shop_catalogue_products` WHERE `category_id` = :category_id;');
		$product_stmt->bindValue(':category_id', $current_category["id"]);
		$product_stmt->execute();
		while( $product = $product_stmt->fetch(PDO::FETCH_ASSOC) )
		{
			$current_product = array();
			$current_product["id"] = (integer)$product["id"];
			$current_product["count"] = 0;
			$current_product["url"] = $current_category["url"]."/".$product["alias"];
			$current_product["level"] = 0;
			$current_product["alias"] = $product["alias"];
			$current_product["value"] = $product["caption"];
			$current_product["parent"] = (integer)$current_category["id"];
			$current_product["data"] = array();
			
			array_push($current_category["data"], $current_product);
		}
	}
	
	//Добавляем объект категории в объект иерархии
	$root_category["data"] = addContentToDump($current_category, $root_category["data"], 0);
}

//var_dump($root_category);

$root_content["data"] = array_merge($root_content["data"], $root_category["data"]);

printSitemap($root_content, true);
?>