<?php
/**
 * Скрипт определения рекурсивной функции для вывода категорий на одном уровне одной ветви - в виде бокового меню
 * 
 * В отдельном скрипте - чтобы не было Dupliate Definition
*/
defined('_ASTEXE_') or die('No access');
?>

<?php

$ul_id = 0;//Инкремент для ID элементов ul

//Рекурсивная функция для вывода категорий на одном уровне одной ветви
function printCatalogueNode($data, $first_call)
{
    global $DP_Config;
    global $DP_Content;
    global $module_modes_map;
	global $multilang_params;
    
    global $ul_id;
	$ul_id++;
	
    //Если в перечне маршрутов страниц нет такого маршрута
    if( !isset($module_modes_map[(string)$DP_Content->url]) )
    {
        $href_type = 1;
    }
    else
    {
        $href_type = $module_modes_map[(string)$DP_Content->url];//Вариант вывода ссылок на категории
    }
	
	
	//Определяем класс ul
	if($first_call)
	{
		?>
		<ul id="ul_<?php echo $ul_id; ?>" class="sidebar-nav animated fadeIn">
		<?php
	}
	else
	{
		?>
		<ul id="ul_<?php echo $ul_id; ?>" class="menu-submenu list-unstyled collapse">
		<?php
	}

    for($i=0; $i < count($data); $i++)
    {
        $li_class = "";//Активный / Последний / С вложенными
        
		//Пропускаем категории, снятые с публикации (только для покупателей)
		if($data[$i]['published_flag'] == 0 && $href_type == 1)
		{
			continue;
		}
		
        if($data[$i]['$count']>0)
        {
            $li_class .= " has-sub";
        }
        if($i == count($data) - 1)
        {
            $li_class .= " last";
        }
        
        
        $href = "";
        switch($href_type)
        {
            case 1:
                $href = $multilang_params['lang_href']."/".$data[$i]["url"];
                break;
            case 2:
                $href = "/".$DP_Config->backend_dir."/shop/catalogue/products?category_id=".$data[$i]["id"];
                break;
            case 3:
                $href = "/".$DP_Config->backend_dir."/shop/logistics/stock?category_id=".$data[$i]["id"];
                break;
            default:
                $href = "#";
                break;
        }
        
        if($data[$i]['$count']>0)
        {
			$ul_id_1 = $ul_id + 1;
			?>
			<li class="<?php echo $li_class; ?>"><a data-toggle="collapse" class aria-expanded="false" href="#ul_<?php echo $ul_id_1; ?>"><span><?php echo $data[$i]["value"]; ?></span></a>
			<?php
            printCatalogueNode($data[$i]["data"], 0);
        }
		else
		{
			?>
			<li class="<?php echo $li_class; ?>"><a href="<?php echo $href; ?>"><span><?php echo $data[$i]["value"]; ?></span></a>
			<?php
		}
        ?>
        </li>
        <?php
    }
    ?>
    </ul>
    <?php
}

//Массив с перечнем маршрутов страниц, которые указывают на режим работы модуля:
//1 - Вывод категорий каталога для покупателей
//2 - Вывод категорий каталога для администратора каталога
//3 - Вывод категорий каталога для кладовщика
$module_modes_map = array();
//Для администратора каталога
$module_modes_map["shop/catalogue/products"] = 2;//Страница для добавления товаров в категорию
$module_modes_map["shop/catalogue/products/product"] = 2;//Страница создания/редактирования товара
//Для кладовщика
$module_modes_map["shop/logistics/stock"] = 3;//Страница для выбора категории в Управлении наличием
$module_modes_map["shop/logistics/stock/product"] = 3;//Страница для выбора категории в Управлении наличием
?>