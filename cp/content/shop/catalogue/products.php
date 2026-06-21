<?php
/**
 * Страница управления товарами
 * 
 * Варианты действий:
 * - обращение без параметров - отображение категорий корня каталога
 * - обращение с параметром category_id - отображение подкатегорий или товаров(если категория конечна)
 * - обращение с параметром action=delete - удаление отмеченных товаров
 * 
 * 
 * Функции на панели управления:
 * - добавить товар (если в данный момент открыта конечная категория);
 * - удалить отмеченные (если в данный момент открыта конечная категория);
 * - редактировать товар (если в данный момент открыта конечная категория);
 * 
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( isset($_POST["action"]) )//Есть действия
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
    if($_POST["action"] == "delete_products")
    {
		try
		{
			//Старт транзакции
			if( ! $db_link->beginTransaction()  )
			{
				throw new Exception(translate_str_by_id(2132));
			}
			
			$category_id = (int) $_POST["category_id"];
			$products_to_delete = json_decode($_POST["products_list"], true);
			//Подключаем модульный скрипт для удаления продуктов (работает в контексте транзакции)
			require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/catalogue/delete_products_sub.php");
			
		}
		catch (Exception $e)
		{
			//Откатываем все изменения
			$db_link->rollBack();
            ?>
            <script>
                location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products?category_id=<?php echo $category_id; ?>&error_message=<?php echo urlencode($e->getMessage()); ?>";
            </script>
            <?php
            exit;
		}
		
		//Дошли до сюда, значит выполнено ОК
		$db_link->commit();//Коммитим все изменения и закрываем транзакцию
		
		?>
		<script>
			location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/catalogue/products?category_id=<?php echo $category_id; ?>&success_message=<?php echo urlencode(translate_str_by_id(2989)); ?>";
		</script>
		<?php
		exit;
    }
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    $is_products_mode = true;//Флаг - страница работает в режиме отображения товаров
    $category_block_type = 2;//Тип блоков категорий - для редактирования справочников товаров (используется в /content/shop/catalogue/printCategories.php)
	$product_block_type = 2;//Параметр для скрипта /content/shop/catalogue/printProducts.php - знать, как выводить товары
    
    //ID категории для отображения
    if(!empty($_GET["category_id"]))
    {
        $category_id = (int) $_GET["category_id"];
    }
    else
    {
        $category_id = 0;
    }

    $epc_apai_cp_filter = null;
    if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_cp_catalogue_filter.php')) {
        require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_cp_catalogue_filter.php';
        $epc_apai_cp_filter = epc_apai_cp_catalogue_filter_ctx($db_link, $category_id);
    }
    
    
    if($category_id > 0)
    {
        //Есть параметр category_id - нужно понять, является ли он конечным (count = 0)
        $category_record_query = $db_link->prepare("SELECT * FROM `shop_catalogue_categories` WHERE `id` = ?;");
		$category_record_query->execute( array($category_id) );
        $category_record = $category_record_query->fetch();
        
        if($category_record["count"] == 0)//Подкатегорий нет - значит отображаем товары
        {
            $is_products_mode = true;
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
    
    

    //Решаем, что выводить:
    if (!empty($epc_apai_cp_filter['enabled'])) {
        epc_apai_cp_catalogue_filter_render($epc_apai_cp_filter, (string) $DP_Config->backend_dir);
    }
    if($is_products_mode == false)//Подкатегории
    {
	?>
		<div class="<?php echo !empty($epc_apai_cp_filter['enabled']) ? '' : 'col-lg-12'; ?>">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2990); ?>
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
	
	if($is_products_mode == true || $category_id == 0)//Подкатегории
    {
    ?>
        
        <?php
            require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
        ?>
            
        <div class="<?php echo !empty($epc_apai_cp_filter['enabled']) ? '' : 'col-lg-12'; ?>">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2113); ?>
				</div>
				<div class="panel-body">
					
					<?php
					// Ссылка на корневую страницу
					print_backend_button( array("url"=>"/".$DP_Config->backend_dir."/shop/catalogue/products", "background_color"=>"#27ae60", "fontawesome_class"=>"fas fa-boxes", "caption"=>translate_str_by_id(2764)) );
					?>
					
					<?php
					if($is_products_mode == true)//Конечная категория
					{
					?>
					<a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/catalogue/products/product?category_id=<?php echo $category_id; ?>">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_add.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2267); ?></div>
					</a>
					<?php
					}
					?>
					
					<a class="panel_a" onClick="checkAll(true);" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/checkbox.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2293); ?></div>
					</a>
					
					<a class="panel_a" onClick="checkAll(false);" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/selection_off.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2294); ?></div>
					</a>
					
					<a class="panel_a" onClick="deleteProducts();" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
					</a>

					<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
					</a>
					
					<?php
					if($is_products_mode == true)//Конечная категория
					{
					?>
					<a class="panel_a" style="float:right;" onClick="deleteProductsСategory();" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/content_delete.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2991); ?></div>
					</a>
					
					<a class="panel_a right-sidebar-toggle" style="float:right;" id="sidebar" href="javascript:void(0);">
						<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/filter.png') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo translate_str_by_id(2992); ?></div>
					</a>
					<?php
					}
					?>
					
				</div>
			</div>
		</div>
		
		
		

        
        
        
        
        
        <!-- Форма удаления товаров из справочника -->
        <form method="POST" name="delete_products_form">
            <input type="hidden" name="action" value="delete_products" />
            <input type="hidden" name="products_list" id="products_list_to_delete" value="" />
            <input type="hidden" name="category_id" value="<?php echo $category_id; ?>" />
			<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
        </form>
        <script>
        //Функция удаления товаров
        function deleteProducts()
        {
            var checked_products = getCheckedProducts();
            if(checked_products.length == 0)
            {
                alert("<?php echo translate_str_by_id(2993); ?>");
                return;
            }
            
            if(!confirm("<?php echo translate_str_by_id(2994); ?>"))
            {
                return;
            }
            
            document.getElementById("products_list_to_delete").value = JSON.stringify(checked_products);
            
            document.forms["delete_products_form"].submit();
        }
		//Функция удаления всех товаров конкретной категории
        function deleteProductsСategory()
        {
            if(!confirm("<?php echo translate_str_by_id(2995); ?>"))
            {
                return;
            }
            document.forms["delete_products_form"].submit();
        }
        </script>
        
        
        
        
        
        
        
        
        
        
        <!-- БЛОК ДЛЯ РАБОТЫ С ТОВАРАМИ (ВЫДЕЛЕНИЕ, СНЯТИЕ И Т.Д.) -->
        <script>
            // -----------------------------------------------------------------------------------------------------------
            //Получение отмеченных продуктов (список ID)
            function getCheckedProducts()
            {
                var products_checkboxes = document.getElementsByClassName("product_checkbox");
                
                var products_checked = new Array();
                
                for(var i=0; i < products_checkboxes.length; i++)
                {
                    if(products_checkboxes[i].checked == true)
                    {
                        products_checked.push(products_checkboxes[i].getAttribute("product_id"));
                    }
                }
                
                return products_checked;
            }
            // -----------------------------------------------------------------------------------------------------------
            //Отметить все (true) / Снять все (false)
            function checkAll(check)
            {
                var products_checkboxes = document.getElementsByClassName("product_checkbox");
                
                for(var i=0; i < products_checkboxes.length; i++)
                {
                    products_checkboxes[i].checked = check;
                }
            }
            // -----------------------------------------------------------------------------------------------------------
        </script>
        
        
        
        
        
        
        
        
        
        
        
        
        <?php
        //Общий скрипт вывода товаров в основную область страницы
        require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printProducts.php");
        ?>
		
		
<?php
	}
    if (!empty($epc_apai_cp_filter['enabled'])) {
        epc_apai_cp_catalogue_filter_close();
    }
}//~else//Действий нет - выводим страницу
?>