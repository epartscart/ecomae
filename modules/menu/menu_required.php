<?php
/**
 * Скрипт для функции - подключается через require_once
*/
defined('_ASTEXE_') or die('No access');

/**
 * Resolve a menu item's path fragment for hide-list matching (front menu cleanup).
 */
function epc_menu_item_path_key(array $item): string
{
	global $db_link;

	$href = '';
	$mode = (string) ($item['link_mode'] ?? '');
	if ($mode === 'url') {
		$href = (string) ($item['href'] ?? '');
	} elseif ($mode === 'content' && !empty($item['content_id']) && isset($db_link) && $db_link instanceof PDO) {
		try {
			$stmt = $db_link->prepare('SELECT `url` FROM `content` WHERE `id` = :id LIMIT 1');
			$stmt->bindValue(':id', (int) $item['content_id'], PDO::PARAM_INT);
			$stmt->execute();
			$row = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				$href = '/' . ltrim((string) $row['url'], '/');
			}
		} catch (Throwable $e) {
			$href = '';
		}
	}

	$href = strtolower(trim($href));
	$href = preg_replace('~^https?://[^/]+~', '', $href);
	$href = preg_replace('~^/(en|me|ru)/~', '/', $href);
	return trim($href, '/');
}

/**
 * Hide Cart / My orders / Balance / Information / Contacts from the storefront top menu.
 *
 * @param list<array<string,mixed>> $items
 * @return list<array<string,mixed>>
 */
function epc_menu_filter_storefront_hidden(array $items): array
{
	$pathHide = array(
		'shop/cart',
		'shop/orders',
		'shop/balans',
		'kontakty',
		'contacts',
		'payment',
		'delivery',
		'ob-oplate',
		'o-dostavke',
	);
	$captionHide = array(
		'cart',
		'my orders',
		'my order',
		'balance',
		'information',
		'contacts',
		'корзина',
		'мои заказы',
		'баланс',
		'информация',
		'контакты',
		'about payment',
		'about delivery',
		'об оплате',
		'о доставке',
	);

	$out = array();
	foreach ($items as $item) {
		if (!is_array($item)) {
			continue;
		}

		$path = epc_menu_item_path_key($item);
		$captionRaw = (string) ($item['a_innerhtml'] ?? '');
		$caption = function_exists('translate_str_by_id') ? (string) translate_str_by_id($captionRaw) : $captionRaw;
		$caption = mb_strtolower(trim(html_entity_decode(strip_tags($caption), ENT_QUOTES, 'UTF-8')));

		$hide = false;
		if ($caption !== '' && in_array($caption, $captionHide, true)) {
			$hide = true;
		}
		if (!$hide && $path !== '') {
			foreach ($pathHide as $needle) {
				if ($path === $needle || strpos($path, $needle . '/') === 0 || substr($path, -strlen($needle)) === $needle) {
					// Keep Product / Selection catalogs etc.; only exact path matches for payment/delivery as leaf URLs.
					if (in_array($needle, array('payment', 'delivery'), true)) {
						if ($path === $needle || preg_match('~(^|/)' . preg_quote($needle, '~') . '(/|$)~', $path)) {
							$hide = true;
							break;
						}
					} else {
						$hide = true;
						break;
					}
				}
			}
		}

		// Information dropdown: parent with payment/delivery children (or empty/# href + Information caption).
		if (!$hide && !empty($item['$count']) && !empty($item['data']) && is_array($item['data'])) {
			$childPaths = array();
			foreach ($item['data'] as $child) {
				if (is_array($child)) {
					$childPaths[] = epc_menu_item_path_key($child);
				}
			}
			$hasPayment = false;
			$hasDelivery = false;
			foreach ($childPaths as $cp) {
				if ($cp === 'payment' || $cp === 'ob-oplate' || strpos($cp, 'payment') !== false || strpos($cp, 'ob-oplate') !== false) {
					$hasPayment = true;
				}
				if ($cp === 'delivery' || $cp === 'o-dostavke' || strpos($cp, 'delivery') !== false || strpos($cp, 'o-dostavke') !== false) {
					$hasDelivery = true;
				}
			}
			if ($hasPayment || $hasDelivery) {
				$hide = true;
			}
		}

		if ($hide) {
			continue;
		}

		if (!empty($item['data']) && is_array($item['data'])) {
			$item['data'] = epc_menu_filter_storefront_hidden($item['data']);
			$item['$count'] = count($item['data']);
		}

		$out[] = $item;
	}

	return $out;
}


//Рекурсивный метод получения HTML-кода пунктов меню
/*
Метод принимает массив пунктов меню одного уровня.
Сам уровень - это по сути список <ul></ul>, а все его пункты - это содержимое внутри списка, т.е. <li></li>
*/
function getHtmlOfMenu($menu_php, $level = null)
{
    global $isFrontMode;//Режим работы
    global $db_link;
    global $DP_Config;
    global $DP_Content;//Объект материала от ядра
    global $multilang_params;//Параметры мультиязычности
	
    $level++; 
    $html = "";//Начинаем формировать строку с html списка пунктов данного уровня
    
    //Цикл формирования пунктов данного уровня
    for($i=0; $i < count($menu_php); $i++)
    {
        //ИТЕРАЦИЯ ЦИКЛА - ФОРМИРОВАНИЕ ПУНКТА, Т.Е. <li>
        
        //1. Получаем атрибут href:
        $href = "";
        if($menu_php[$i]["link_mode"] == "url")//Содержимое задано напрямую
        {
            $href = $menu_php[$i]["href"];
			if( $isFrontMode && !empty($multilang_params['multilang']) )
			{
				if( $href === '/' || $href === '' )
				{
					$href = rtrim($multilang_params['lang_href'], '/') . '/';
				}
				else if( isset($href[0]) && $href[0] === '/' && strpos($href, $multilang_params['lang_href']) !== 0 )
				{
					$href = $multilang_params['lang_href'] . $href;
				}
			}
        }
        else if($menu_php[$i]["link_mode"] == "content")//Содержимое необходимо получить из таблицы content (т.е. это ссылка на материал)
        {
			$stmt = $db_link->prepare('SELECT * FROM `content` WHERE `id` = :id;');
			$stmt->bindValue(':id', $menu_php[$i]["content_id"]);
			$stmt->execute();
			$content_record = $stmt->fetch(PDO::FETCH_ASSOC);
			
            if($content_record != false)
            {
                $href = "/".$content_record["url"];
                if(!$isFrontMode)//Если работаем в режиме бэкэнда - в ссылки добавляем каталог бэкэнда
                {
                    $navPrefix = function_exists('epc_cp_nav_url_prefix') ? epc_cp_nav_url_prefix() : ('/' . $DP_Config->backend_dir);
                    $href = $navPrefix . $href;
                }
				//Если это фронтенд и включена мультиязычность, необходимо добавить к ссылке код языка
				else if( $multilang_params['multilang'] == true )
				{
					$href = $multilang_params['lang_href'] . $href;
				}
				
            }
            else//Материал отсутствует - возможно он был удален
            {
                $href = "";
            }
        }
        
        //2. Добавляем CLASS ACTIVE для li
        //Адрес ссылки
        $href_value = str_replace(array("index.php", $DP_Config->domain_path, $DP_Config->backend_dir), "", $href);//Подгоняем ссылку под маршрут
        if( strlen($href_value) > 1 )
		{
			if($href_value[0] == "/"  )//Если опять в начале оказался знак "/" - убираем
			{
				$href_value = substr_replace($href_value, "", 0, 1);
				if($href_value[0] == "/")//Если опять в начале оказался знак "/" - убираем
				{
					$href_value = substr_replace($href_value, "", 0, 1);
				}
			}
		}
        if($href_value == $DP_Content->url || ($href_value == "/" && $DP_Content->main_flag))
        {
            if($menu_php[$i]["class_li"] != "") $menu_php[$i]["class_li"] .= " ";
            $menu_php[$i]["class_li"] .= "active";
        }
        
        if($menu_php[$i]['$count'] > 0)
        {
			$href = 'javascript:void(0);" data-toggle="dropdown" aria-expanded="false';
			$menu_php[$i]["class_a"] .= ' dropdown-toggle';
			$menu_php[$i]["class_li"] .= ' dropdown';
			$menu_php[$i]['class_ul'] .= ' dropdown-menu animated-2x animated fadeIn';
			
			if($level > 1){
				$menu_php[$i]["class_li"] .= ' dropdown-submenu';
			}
		}
        
        //3. Атрибуты li
        $class_li = "";
        if($menu_php[$i]["class_li"] != "")
        {
            $class_li = " class=\"".$menu_php[$i]["class_li"]."\"";
        }
        
        $id_li = "";
        if($menu_php[$i]["id_li"] != "")
        {
            $id_li = " id=\"".$menu_php[$i]["id_li"]."\"";
        }
        $html .= "<li".$class_li.$id_li.">";//Начало пункта li
        
        //4. Формируем ссылку (тег a)
        $class_a = "";
        $id_a = "";
        $a_innerhtml = "";
        $target = "";
        $onclick = "";
        
        //target
        if($menu_php[$i]["target"] != "")
        {
            $target = " target= \"".$menu_php[$i]["target"]."\"";
        }
        //onclick
        if($menu_php[$i]["onclick"] != "")
        {
            $onclick = " onclick= \"".$menu_php[$i]["onclick"]."\"";
        }
        //class_a
        if($menu_php[$i]["class_a"] != "")
        {
            $class_a = " class= \"".$menu_php[$i]["class_a"]."\"";
        }
        //id_a
        if($menu_php[$i]["id_a"] != "")
        {
            $id_a = " id= \"".$menu_php[$i]["id_a"]."\"";
        }
        
        $html .= "<a href=\"$href\"".$class_a.$id_a.$target.$onclick.">".translate_str_by_id($menu_php[$i]["a_innerhtml"])."</a>";
        
        //Если пункт содержит вложенные пункты
        if($menu_php[$i]['$count'] > 0)
        {
            $class_ul = "";
            if($menu_php[$i]['class_ul'] != "")
            {
                $class_ul = " class=\"".$menu_php[$i]['class_ul']."\"";
            }
            
            $id_ul = "";
            if($menu_php[$i]['id_ul'] != "")
            {
                $id_ul = " id=\"".$menu_php[$i]['id_ul']."\"";
            }
            
            $html .= "<ul".$class_ul.$id_ul.">";
            $html .= getHtmlOfMenu($menu_php[$i]["data"], $level);
            $html .= "</ul>";
        }
        
        $html .= "</li>";
    }//~for($i)
    
    return $html;
}//~function getHtmlOfMenu($menu_php)
?>