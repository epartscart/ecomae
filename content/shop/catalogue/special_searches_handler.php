<?php
defined('_ASTEXE_') or die('No access');
//Скрипт обеспечивает работу специальных поисков

if( !isset($DP_Content->service_data) || !isset($DP_Content->service_data["sp"]) )
{
	exit;
}




if( $DP_Content->service_data["sp_step_type"] == 2 )
{
	//Здесь нужно вывести элементы древовидного списка
	$tree_list_items_query = $db_link->prepare("SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = ? AND `parent` = ? ORDER BY `order` ASC;");
	$tree_list_items_query->execute( array( $DP_Content->service_data["sp_step_tree_list_id"], $DP_Content->service_data["sp_step_tree_list_parent"] ) );
	?>
	<ul class="cat_blocks">
	<?php
	while( $tree_list_item = $tree_list_items_query->fetch() )
	{
		?>
		<li>
			<a href="<?php echo $multilang_params['lang_href']; ?>/<?php echo $url_route."/".$tree_list_item["alias"]; ?>">
				<span class="block_image">
					<img src="/content/files/images/tree_lists_images/<?php echo $tree_list_item["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
				</span>
				<span class="block_caption"><?php echo translate_str_by_id($tree_list_item["value"]); ?></span>
			</a>
		</li>
		<?php
	}
	?>
	</ul>
	<?php
}//Вывод элементов древовидного списка
else if( $DP_Content->service_data["sp_step_type"] == 1 )
{
	$categories = $DP_Content->service_data["sp_step_categories"];
	
	//Получаем строку с перечислением категорий через запятую
	$categories_str = "";
	for($i=0; $i < count($categories) ; $i++)
	{
		$categories[$i] = (int)$categories[$i];
		
		if($i > 0)
		{
			$categories_str .= ",";
		}
		$categories_str .= $categories[$i];
	}
	
	
	//Получаем категори
	$categories_query = $db_link->prepare('SELECT * FROM `shop_catalogue_categories` WHERE `id` IN ('.$categories_str.');');
	$categories_query->execute();
	?>
	<ul class="cat_blocks">
	<?php
	while( $category = $categories_query->fetch() )
	{
		?>
		<li>
			<a href="<?php echo $multilang_params['lang_href']; ?>/<?php echo $url_route."/".$category["alias"]; ?>">
				<span class="block_image">
					<img src="/content/files/images/catalogue_images/<?php echo $category["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
				</span>
				<span class="block_caption"><?php echo translate_str_by_id($category["value"]); ?></span>
			</a>
		</li>
		<?php
	}
	?>
	</ul>
	<?php
}
else if( $DP_Content->service_data["sp_step_type"] == 3 )
{
	//Выводим конечную категорию товаров (т.е. ее товары)
	
	//Показываем все категории с аргументом ?sp=yes
	$category_id = $DP_Content->service_data["sp_step_category_id"];
	
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/catalogue_for_customer.php");
}



/*
ДАЛЕЕ - старый вариант (по появления ЧПУ)
//Получаем исходные данные:

//Алиас поиска
$search_alias = $_GET["search_type"];

if( $search_alias !== "garage" )
{
	$search_query = $db_link->prepare('SELECT * FROM `shop_special_searches` WHERE `alias` = :alias;');
	$search_query->bindValue(':alias', $search_alias);
	$search_query->execute();
	$search_record = $search_query->fetch();
	$search_id = $search_record["id"];
	
	//Шаг поиска
	$step_alias = "";
	$step_record = NULL;
	if( isset($_GET["step"]) )
	{
		$step_alias = $_GET["step"];
		
		
		//Перечень шагов поиска
		$step_query = $db_link->prepare('SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = :search_id AND `alias` = :alias;');
		$step_query->bindValue(':search_id', $search_id);
		$step_query->bindValue(':alias', $step_alias);
		$step_query->execute();
		$step_record = $step_query->fetch();
		$step_record["objects"] = json_decode($step_record["objects"], true);
	}
	else//Получаем первый шаг поиска
	{
		$step_query = $db_link->prepare('SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = :search_id ORDER BY `order` LIMIT 1;');
		$step_query->bindValue(':search_id', $search_id);
		$step_query->execute();
		$step_record = $step_query->fetch();
		$step_record["objects"] = json_decode($step_record["objects"], true);
	}





	//ВЫВОД HTML
	if($step_alias == "")//Только зашли на поиск
	{
		//Показываем объекты первого уровня первого шага
		if( $step_record["type"] == 2 )//Тип шага - древовидный список
		{
			//ЗАПИСЬ КУКИ СП
			?>
			<script>
			document.cookie = "sp_alias=<?php echo $search_alias; ?>; path=/;";
			document.cookie = "sp_tl_<?php echo $step_record["objects"][0]; ?>=0; path=/;";
			</script>
			<?php
			
			//Получаем перечень элементов первого уровня древовидного списка
			$tree_list_items_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id AND `level` = 1;');
			$tree_list_items_query->bindValue(':tree_list_id', $step_record["objects"][0]);
			$tree_list_items_query->execute();
			?>
			<ul class="cat_blocks">
			<?php
			while( $tree_list_item = $tree_list_items_query->fetch() )
			{
				?>
				<li>
					<a href="/shop/search_products?search_type=<?php echo $search_alias; ?>&step=<?php echo $step_record["alias"]; ?>&step_item=<?php echo $tree_list_item["id"]; ?>">
						<span class="block_image">
							<img src="/content/files/images/tree_lists_images/<?php echo $tree_list_item["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
						</span>
						<span class="block_caption"><?php echo $tree_list_item["value"]; ?></span>
					</a>
				</li>
				<?php
			}
			?>
			</ul>
			<?php
		}
		else//Тип шага - "Категории товаров"
		{
			//Вывод категорий
			//...
			printSP_Categories();
		}
	}
	else//Был переход на какой-то элемент шага (или узел древовидного списка или категорию товаров)
	{
		//Определяем, что дальше
		if( $step_record["type"] == 2 )
		{
			//ЗАПИСЬ КУКИ СП
			?>
			<script>
			document.cookie = "sp_alias=<?php echo $search_alias; ?>; path=/;";
			document.cookie = "sp_tl_<?php echo $step_record["objects"][0]; ?>=<?php echo $_GET["step_item"]; ?>; path=/;";
			</script>
			<?php

			//Получаем перечень элементов первого уровня древовидного списка
			$tree_list_items_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id AND `parent` = :parent;');
			$tree_list_items_query->bindValue(':tree_list_id', $step_record["objects"][0]);
			$tree_list_items_query->bindValue(':parent', $_GET["step_item"]);
			$tree_list_items_query->execute();
			
			
			if( $tree_list_items_query->fetchColumn() > 0 )
			{
				$tree_list_items_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id AND `parent` = :parent;');
				$tree_list_items_query->bindValue(':tree_list_id', $step_record["objects"][0]);
				$tree_list_items_query->bindValue(':parent', $_GET["step_item"]);
				$tree_list_items_query->execute();
				
				?>
				<ul class="cat_blocks">
				<?php
				while( $tree_list_item = $tree_list_items_query->fetch() )
				{
					?>
					<li>
						<a href="/shop/search_products?search_type=<?php echo $search_alias; ?>&step=<?php echo $step_record["alias"]; ?>&step_item=<?php echo $tree_list_item["id"]; ?>">
							<span class="block_image">
								<img src="/content/files/images/tree_lists_images/<?php echo $tree_list_item["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
							</span>
							<span class="block_caption"><?php echo $tree_list_item["value"]; ?></span>
						</a>
					</li>
					<?php
				}
				?>
				</ul>
				<?php
			}
			else//Достигнут последний элемент шага - переход к следующему шагу
			{
				//Получааем следующий шаг
				$step_query = $db_link->prepare('SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = :search_id AND `order` > :order ORDER BY `order` LIMIT 1;');
				$step_query->bindValue(':search_id', $search_id);
				$step_query->bindValue(':order', $step_record["order"]);
				$step_query->execute();
				$step_record = $step_query->fetch();
				$step_record["objects"] = json_decode($step_record["objects"], true);
				
				if( $step_record["type"] == 2 )
				{
					//ЗАПИСЬ КУКИ СП
					?>
					<script>
					document.cookie = "sp_alias=<?php echo $search_alias; ?>; path=/;";
					document.cookie = "sp_tl_<?php echo $step_record["objects"][0]; ?>=0; path=/;";
					</script>
					<?php

					//Получаем перечень элементов первого уровня древовидного списка
					$tree_list_items_query = $db_link->prepare('SELECT * FROM `shop_tree_lists_items` WHERE `tree_list_id` = :tree_list_id AND `level` = 1;');
					$tree_list_items_query->bindValue(':tree_list_id', $step_record["objects"][0]);
					$tree_list_items_query->execute();
					?>
					<ul class="cat_blocks">
					<?php
					while( $tree_list_item = $tree_list_items_query->fetch() )
					{
						?>
						<li>
							<a href="/shop/search_products?search_type=<?php echo $search_alias; ?>&step=<?php echo $step_record["alias"]; ?>&step_item=<?php echo $tree_list_item["id"]; ?>">
								<span class="block_image">
									<img src="/content/files/images/tree_lists_images/<?php echo $tree_list_item["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
								</span>
								<span class="block_caption"><?php echo $tree_list_item["value"]; ?></span>
							</a>
						</li>
						<?php
					}
					?>
					</ul>
					<?php
				}
				else
				{
					//Вывод категорий
					//...
					printSP_Categories();
				}
				
			}
		}
		else
		{
			//Вывод категорий
			//...
			printSP_Categories();
		}
		
	}
}
else//Поиск из гаража
{
	//Показываем все категории с аргументом ?sp=yes
	$category_id = 0;
	$category_block_type = 1;
	$_GET["sp"] = "yes";
	
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/printCategories.php");
}









// -------------------------------------------------------------------------------
//Определение функций
//Функция вывода категорий товаров
function printSP_Categories()
{
	global $db_link, $search_id;
	
	//Получаем последний шаг СП (Это всегда - тип "Категории товаров")
	$last_step_query = $db_link->prepare('SELECT * FROM `shop_special_searches_steps` WHERE `search_id` = :search_id ORDER BY `order` DESC LIMIT 1;');
	$last_step_query->bindValue(':search_id', $search_id);
	$last_step_query->execute();
	$last_step = $last_step_query->fetch();
	
	$categories = json_decode($last_step["objects"], true);
	
	//Получаем строку с перечислением категорий через запятую
	$categories_str = "";
	for($i=0; $i < count($categories) ; $i++)
	{
		$categories[$i] = (int)$categories[$i];
		
		if($i > 0)
		{
			$categories_str .= ",";
		}
		$categories_str .= $categories[$i];
	}
	
	
	//Получаем категори
	$categories_query = $db_link->prepare('SELECT * FROM `shop_catalogue_categories` WHERE `id` IN ('.$categories_str.');');
	$categories_query->execute();
	?>
	<ul class="cat_blocks">
	<?php
	while( $category = $categories_query->fetch() )
	{
		?>
		<li>
			<a href="/<?php echo $category["url"]; ?>?sp=yes">
				<span class="block_image">
					<img src="/content/files/images/catalogue_images/<?php echo $category["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
				</span>
				<span class="block_caption"><?php echo $category["value"]; ?></span>
			</a>
		</li>
		<?php
	}
	?>
	</ul>
	<?php
}
*/
?>