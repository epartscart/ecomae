<?php
/**
 * Скрипт модуля Хлебные крошки
*/
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_breadcrumb.php';

//Названия узловов для страниц ошибок 403 и 404 (ВНИМАНИЕ! ЖЕЛАТЕЛЬНО ЭТИ ЗНАЧЕНИЯ ПРИВЕСТИ В СООТВЕТСТВИЕ С ПЛАГИНОМ НАСТРОЙКИ СТРАНИЦ 403 и 404):
$step_caption_403 = translate_str_by_id(3991);
$step_caption_404 = translate_str_by_id(3);

$pageIs404 = isset($DP_Content->service_data['error_page']) && (int) $DP_Content->service_data['error_page'] === 404;


//Разбиваем путь на алиасы
$nodes = explode("/", $url_route);

//HTML хлебных крошок
$path_steps = "<li><a href=\"/".$DP_Config->backend_dir."\">".translate_str_by_id(3992)."</a></li>";

//URL для текущего узла
$node_url = "";


//Есть ли путь или только главная страница
$is_path = false;

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
    $node_url = $node_url.$nodes[$i];
    
    
    $step_url = '/'.$DP_Config->backend_dir.'/'.$node_url;//url страницы берем из браузера
    $isLastNode = ($i === count($nodes) - 1);
    $step_caption = epc_cp_breadcrumb_caption_for_node($db_link, $node_url, $step_caption_404, $isLastNode, $pageIs404);
    
    
    //ПРОВЕРЯЕМ В ОСНОВНЫХ МАТЕРИАЛАХ CMS
	$node_query = $db_link->prepare("SELECT `url`, `value`, `id` FROM `content` WHERE `url` = ? AND `is_frontend`=0;");
	$node_query->execute( array($node_url) );
    $node_record = $node_query->fetch();
	if( $node_record != false )
    {
        $step_caption = translate_str_by_id($node_record["value"]);
        $step_url = '/'.$DP_Config->backend_dir.'/'.$node_record["url"];
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
		$user_groups = (isset($user_profile['groups']) && is_array($user_profile['groups'])) ? $user_profile['groups'] : array();
		for($a=0; $a < count($user_groups); $a++)
		{
			if(array_search((int)$user_groups[$a], $allowed_groups) !== false)
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
    }

    
    if( $i != count($nodes) -1 )
    {
        $step_caption = "<li><a href=\"$step_url\">".$step_caption."</a></li>";
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
	<ol class="hbreadcrumb breadcrumb">
		<?php echo $path_steps; ?>
	</ol>
	<?php
}
?>
