<?php
/**
Скрипт для обеспечения вывода меню каталога товаров в панели управления
*/
defined('_ASTEXE_') or die('No access');

//Массив с перечнем маршрутов страниц, которые указывают на режим работы модуля:
//2 - Вывод категорий каталога для администратора каталога
//3 - Вывод категорий каталога для кладовщика
$module_modes_map = array();
//Для администратора каталога
$module_modes_map["shop/catalogue/products"] = 2;//Страница для добавления товаров в категорию
$module_modes_map["shop/catalogue/products/product"] = 2;//Страница создания/редактирования товара
//Для кладовщика
$module_modes_map["shop/logistics/stock"] = 3;//Страница для выбора категории в Управлении наличием
$module_modes_map["shop/logistics/stock/product"] = 3;//Страница для выбора категории в Управлении наличием



//ОПРЕДЕЛЕНИЯ PHP
//Рекурсивная функция для вывода категорий на одном уровне одной ветви
function printCatalogueBranch($data)
{
    global $DP_Config;
    global $DP_Content;
    global $module_modes_map;
    
    $href_type = $module_modes_map[(string)$DP_Content->url];//Вариант вывода ссылок на категории
	
	?>
	<ul class="nav nav-second-level">
	<?php
	
    for($i=0; $i < count($data); $i++)
    {
		//Пропускаем категории, снятые с публикации (только для покупателей)
		if($data[$i]['published_flag'] == 0 && $href_type == 1)
		{
			continue;
		}
		

        $href = "javascript:void(0);";
        switch($href_type)
        {
            case 2:
                $href = "/".$DP_Config->backend_dir."/shop/catalogue/products?category_id=".$data[$i]["id"];
                break;
            case 3:
                $href = "/".$DP_Config->backend_dir."/shop/logistics/stock?category_id=".$data[$i]["id"];
                break;
            default:
                $href = "javascript:void(0);";
                break;
        }
        
		
		
		
		?>
		<li>
		<?php
		if($data[$i]['$count']>0)
		{
			$href = "javascript:void(0);";
			?>
			<a href="<?php echo $href; ?>"><span class="nav-label"><?php echo $data[$i]["value"]; ?></span> <span class="fa arrow"></span></a>
			<?php
		}
		else
		{
			?>
			<a href="<?php echo $href; ?>"><?php echo $data[$i]["value"]; ?></a>
			<?php
		}
        ?>
		
			
			<?php
			if($data[$i]['$count']>0)
			{
				printCatalogueBranch($data[$i]["data"]);
			}
			?>
        </li>
        <?php
    }
    ?>
    </ul>
    <?php
}
?>
