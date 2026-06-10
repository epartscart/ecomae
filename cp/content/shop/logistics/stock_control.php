<?php
/**
 * Сктраничный скрипт для управлениям наличием товара на складах
 * 
 * Суть: на странице отображается каталог товаров на основе справочника.
 * Кладовщик выбирает конкретный товар из каталога. Затем открывается страница товара с виджетами для управления наличием товара на складах, которые может редактировать данный поставщик
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( ! empty($_POST["action"]) )//Есть действия
{
    
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    $is_products_mode = true;//Флаг - страница работает в режиме отображения товаров
    $category_block_type = 3;//Тип блоков категорий - для управления наличием (используется в /content/shop/catalogue/printCategories.php)
    $parent_url = '';
	
    //ID категории для отображения
    if(!empty($_GET["category_id"]))
    {
        $category_id = $_GET["category_id"];
    }
    else
    {
        $category_id = 0;
    }
    
    if($category_id > 0)
    {
        //Есть параметр category_id - нужно понять, является ли он конечным (count = 0)
        $category_record_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `id` = ?;");
		$category_record_query->execute( array($category_id) );
        $category_record = $category_record_query->fetch();
        
        if($category_record["parent"] > 0)//Подкатегорий нет - значит отображаем товары
        {
            $parent_url = '?category_id='.$category_record["parent"];
        }
		if($category_record["count"] == 0)//Подкатегорий нет - значит отображаем товары
        {
            $is_products_mode = true;
            $product_block_type = 3;//Параметр для скрипта /content/shop/catalogue/printProducts.php - знать, как выводить товары
        }
        else
        {
            $is_products_mode = false;
        }
    }
    else
    {
        $is_products_mode = false;//Будем выводить категории (причем корневые)
    }
    
	if(isset($_GET["category_id"])){
		?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2755); ?>
				</div>
				<div class="panel-body">
					<a style="float: left;" class="panel_a" href="/cp/shop/logistics/stock<?=$parent_url;?>">
						<div class="panel_a_img" style="background-color: #b9babb;width:96px;height:96px;display:table-cell;vertical-align:middle;"><i class="fas fa-chevron-left" style="color:#FFF;font-size:45px"></i></div>
						<div><?php echo translate_str_by_id(2961); ?></div>
					</a>
				</div>
			</div>
		</div>
		<?php
	}
	
    //Решаем, что выводить:
    if($is_products_mode == false)//Подкатегории
    {
		?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(3162); ?>
				</div>
				<div class="panel-body">
				<?php
				//Общий скрипт вывода категорий в основную область страницы
				require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printCategories.php");
				?>
				</div>
			</div>
		</div>
		<?php
    }
	
	// Подключаем скрипт таблицы товаров
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/catalogue/all_list_products.inc.php");
}
?>