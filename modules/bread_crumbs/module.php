<?php
/**
 * Скрипт модуля Хлебные крошки
*/
defined('_ASTEXE_') or die('No access');

require_once($_SERVER["DOCUMENT_ROOT"]."/modules/bread_crumbs/helper.php");


//Названия узловов для страниц ошибок 403 и 404 (ВНИМАНИЕ! ЖЕЛАТЕЛЬНО ЭТИ ЗНАЧЕНИЯ ПРИВЕСТИ В СООТВЕТСТВИЕ С ПЛАГИНОМ НАСТРОЙКИ СТРАНИЦ 403 и 404):
$step_caption_403 = translate_str_by_id(3991);
$step_caption_404 = translate_str_by_id(4755);


//Разбиваем путь на алиасы
$nodes = explode("/", $url_route);

//ЕСЛИ СТРАНИЦА ОТНОСИТСЯ К ЧПУ-проценке или специальным поискам
if( isset($DP_Content->service_data["article_search_chpu"]) || isset($DP_Content->service_data["sp"]) )
{
	$nodes = $DP_Content->service_data["bread_crumbs"];
}

//HTML хлебных крошок
$path_steps = "<li><a href=\"".$DP_Config->domain_path."\">".$DP_Config->site_name."</a></li>";

//Для режима мультиязычности добавляем код языка в URL
if( $multilang_params['multilang'] == true )
{
	$path_steps = "<li><a href=\"".$DP_Config->domain_path.$multilang_params['lang']."/\">".$DP_Config->site_name."</a></li>";
}


//URL для текущего узла
$node_url = "";

//Есть ли путь или только главная страница
$is_path = false;

//Флаг - предыдущий узел найден. Если нет, до, далее не ищем - они все будут 404
$last_found = true;

for($i=0; $i < count($nodes); $i++)
{
    //Обычно - если переход на главную
    if($nodes[$i] == "")continue;
    
    $is_path = true;//Не главная страница - значит будем выводить путь
   
    //Формируем URL для узла
    if($node_url != "")
    {
        $node_url = $node_url."/";
    }
	//Добавляем, если это не страница ЧПУ-проценки
	if( !isset($DP_Content->service_data["article_search_chpu"]) && !isset($DP_Content->service_data["sp"])  )
	{
		$node_url = $node_url.$nodes[$i];
	}

	
    //Заголовок и URL выводимого в хлебные крошки узла:
	$step_caption = $step_caption_404;//Название страницы по умолчанию
    $step_url = $node_url;//url страницы берем из браузера
    $flag_found = false;//Флаг (узел не найден)
	
	
	//Если предыдущий узел был найден
	if( $last_found )
	{
		//1. ПРОВЕРЯЕМ НАЛИЧИЕ УЗЛА В ОСНОВНЫХ МАТЕРИАЛАХ CMS
		$stmt = $db_link->prepare('SELECT `url`, `value`, `id` FROM `content` WHERE `url` = ? AND `is_frontend`= ?;');
		$stmt->execute( array($node_url, 1) );
		$node_record = $stmt->fetch();
		if( $node_record != false )
		{
			//Узел найден:
			$step_caption = translate_str_by_id($node_record["value"]);
			$step_url = $node_record["url"];
			$content_id = $node_record["id"];
			
			
			// ----------------- Контроль доступа к узлу ----- > START
			//(Контроль доступа осуществляем только для основных материалов (из таблицы content))
			/*Если к узлу нет доступа, то не показываем его название*/
			//СПИСОК ДОПУЩЕННЫХ ГРУПП
			$allowed_groups = array();//Список допущенных групп
			$stmt = $db_link->prepare('SELECT * FROM `content_access` WHERE `content_id` = ?;');
			$stmt->execute( array($content_id) );
			//Получаем список ЯВНО-допущенных
			while( $content_access_record = $stmt->fetch() )
			{
				$allowed_groups[] = (int)$content_access_record["group_id"];
			}
			//Теперь получаем все их вложенные группы
			$inserted_groups = array();//Массив для вложенных групп
			for($a=0; $a < count($allowed_groups); $a++)
			{
				getAllowedGroups($allowed_groups[$a]);//Функция из плагина контроля доступа содержимому
			}
			$allowed_groups = array_merge($allowed_groups, $inserted_groups);//Объединяем
			//ПРОВЕРКА ДОСТУПА К МАТЕРИАЛУ
			$access_allowed = false;//Флаг "Доступ разрешен"
			//Доступ разрешен, если хотя бы одна из групп пользователя имеет доступ или эта группа является вложенной к тем группам, которые имеют доступ
			//По всем группам пользователя ($user_profile["groups"] - тоже инициализирован в плагине контроля доступа):
			for($a=0; $a < count($user_profile["groups"]); $a++)
			{
				if(array_search((int)$user_profile["groups"][$a], $allowed_groups) !== false)
				{
					$access_allowed = true;
					break;
				}
			}
			//ДЕЙСТВИЯ С РЕЗУЛЬТАТОМ ПРОВЕРКИ ДОСТУПА К МАТЕРИАЛУ:
			if(!$access_allowed)
			{
				$step_caption = $step_caption_403;
			}
			// ----------------- Контроль доступа к узлу ----- ~ END
			
			$flag_found = true;//Флаг - узел есть в основных материалах
		}
		
		
		
		//ПРОВЕРЯЕМ В КАТЕГОРИЯХ ТОВАРОВ
		if( !$flag_found && !isset($DP_Content->service_data["article_search_chpu"]) && !isset($DP_Content->service_data["sp"]) )
		{
			$stmt = $db_link->prepare('SELECT `url`, `value` FROM `shop_catalogue_categories` WHERE `url` = ?;');
			$stmt->execute( array($node_url) );
			$node_record = $stmt->fetch();
			
			if( $node_record != false )
			{
				$step_caption = translate_str_by_id($node_record["value"]);
				$step_url = $node_record["url"];
				$flag_found = true;
			}
			else
			{
				if($DP_Config->product_url == 'id'){
					$stmt = $db_link->prepare('SELECT `caption` FROM `shop_catalogue_products` WHERE `id` = ?;');
				}else{
					$stmt = $db_link->prepare('SELECT `caption` FROM `shop_catalogue_products` WHERE `alias` = ?;');
				}
				$stmt->execute( array($nodes[$i]) );
				$node_record = $stmt->fetch();
				
				if($node_record != false)
				{
					$step_caption = translate_str_by_id($node_record["caption"]);
					$flag_found = true;
				}
			}
			//Если узел найден во встроенном каталоге товаров - доступ к нему не котролируем
		}
		
		
		//ЧПУ-проценка
		if( !$flag_found )
		{
			if( isset($DP_Content->service_data["article_search_chpu"]) || isset($DP_Content->service_data["sp"]) )
			{
				$step_caption = $nodes[$i]["caption"];
				$step_url = $nodes[$i]["url"];
				
				$flag_found = true;
			}
			
			//Если узел найден во ЧПУ-проценке - доступ к нему не котролируем
		}	
		
		
		
		//Получаем альтернативный узел:
		if( !$flag_found )
		{
			$alternative_version = get_alternative_bread_crumbs($url_route, $step_url);
			if($alternative_version != false)
			{
				if($alternative_version["step_caption"] != "")
				{
					//$step_caption = $alternative_version["step_caption"];
				}
				if($alternative_version["step_url"] != "")
				{
					$step_url = $alternative_version["step_url"];
				}
				$flag_found = true;
			}
		}
	}
    
	
	//Текущий узел не найден - все следующие искать не будем - они все будут 404
    if( ! $flag_found )
	{
		$last_found = false;
	}
	
	
	//Защита от XSS
	$step_caption = htmlentities($step_caption);
	
    if( $i != count($nodes) -1 )
    {
		//Защита от XSS
		$step_url = htmlentities($step_url);
		
		
		//Для режима мультиязычности добавляем код языка в URL
		if( $multilang_params['multilang'] == true )
		{
			$step_url = $multilang_params['lang'].'/'.$step_url;
		}
		
		
        $step_caption = "<li><a href=\"/$step_url\">".$step_caption."</a></li>";
    }
	else
	{
		$step_caption = "<li class=\"active\">".$step_caption."</li>";
	}
    
    $path_steps = $path_steps.$step_caption;
}//~for


if($is_path)
{
	?>
	<ol class="breadcrumb">
	<?php
    echo $path_steps;
	?>
	</ol>
	<?php
}
?>
