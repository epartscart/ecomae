<?php
/**
 * Скрипт для вывода блока категорий в основную область страницы.
 * 
 * В зависимости от типа параметра $category_block_type страница может выводить блоки категорий для следующих целей
 * 1 - Отображение для покупателей
 * 2 - Отображения для администратора каталога (при редактировании справочников товаров)
 * 3 - Отображение для кладовщика - для управления наличием товара
*/
defined('_ASTEXE_') or die('No access');

$binding_values = array();
$sub_sql_published_flag = '';
if($category_block_type == 1)
{
	$sub_sql_published_flag = ' AND `published_flag` = ? ';
	array_push($binding_values, 1);
}
$SQL_get_categories = 'SELECT * FROM `shop_catalogue_categories` WHERE `parent` = ? '.$sub_sql_published_flag.' ORDER BY `order`;';
array_unshift($binding_values, $category_id);
$subcategories_query = $db_link->prepare($SQL_get_categories);
$subcategories_query->execute($binding_values);

if($category_block_type == 1){
	
	?>
	<div class="row" style="padding: 0px 4px; margin-top:-9px;">
	<?php
	$epc_el_cat_rendered = 0;
	while( $subcategory = $subcategories_query->fetch() )
	{
		if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php';
			if ($db_link instanceof PDO && function_exists('epc_epartscart_storefront_active') && epc_epartscart_storefront_active($db_link)) {
				if (function_exists('epc_epartscart_is_apai_alias') && epc_epartscart_is_apai_alias((string) ($subcategory['alias'] ?? ''))) {
					continue;
				}
			}
		}
		$epc_el_cat_rendered++;
		$color_class = "navbar-inverse";
		$href = "";
		switch($category_block_type)
		{
			case 1:
				$href = $multilang_params['lang_href']."/".$subcategory["url"];
				break;
			case 2:
				$href = "/".$DP_Config->backend_dir."/shop/catalogue/products?category_id=".$subcategory["id"];
				break;
			case 3:
				$href = "/".$DP_Config->backend_dir."/shop/logistics/stock?category_id=".$subcategory["id"];
				break;
		}
		
		//Если переход идет от специального поиска
		$sp = "";
		if( isset($_GET["sp"]) )
		{
			$sp = "?sp=yes";
		}
		
		$img = '/content/files/images/no_image.png';
		if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_epartscart_storefront.php';
			if ($db_link instanceof PDO && function_exists('epc_epartscart_use_neutral_product_image')
				&& epc_epartscart_use_neutral_product_image($db_link) && function_exists('epc_epartscart_catalog_placeholder_url')) {
				$img = epc_epartscart_catalog_placeholder_url($db_link);
			} elseif (function_exists('epc_storefront_catalog_placeholder_for_hint')) {
				$img = epc_storefront_catalog_placeholder_for_hint((string) ($subcategory["url"] ?? '') . ' ' . (string) ($_SERVER['REQUEST_URI'] ?? ''));
			}
		}
		$background_size = 'background-size: contain;';
		if(!empty($subcategory["image"]) && file_exists($_SERVER["DOCUMENT_ROOT"].'/content/files/images/catalogue_images/'.$subcategory["image"])){
			$img = '/content/files/images/catalogue_images/'.$subcategory["image"];
			//$background_size = '';// Что бы не масштабировать изображение в пределах блока, выглядит четче, но нужно картинки згружать нужных размеров.
		}
		
		$newcatblock_class = 'col-xs-6 col-sm-4 col-md-4 col-lg-4';
		if($DP_Content->main_flag)
		{
			$newcatblock_class = 'col-xs-6 col-sm-4 col-md-4 col-lg-3';
		}
		?>
		
		<div class="<?=$newcatblock_class;?> new-cat-block">
			<a href="<?php echo $href.$sp; ?>" class="ucats-h-1 new-cat-block-catalog">
				<div class="new-cat-block-catalog-img" style="background:url('<?=$img;?>') no-repeat; background-position: center; <?=$background_size;?>"></div>
				<div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id($subcategory["value"]); ?></div>
			</a>
		</div>

		<?php
	}
	if ($epc_el_cat_rendered === 0 && function_exists('epc_electronicae_storefront_active') && epc_electronicae_storefront_active()) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_electronicae_storefront.php';
		echo epc_electronicae_render_empty_category();
	}
	?>
	</div>
	<?php
	
}else{
	
	?>
	<ul class="cat_blocks">
	<?php
	while( $subcategory = $subcategories_query->fetch() )
	{
		$href = "";
		switch($category_block_type)
		{
			case 1:
				$href = "/".$subcategory["url"];
				break;
			case 2:
				$href = "/".$DP_Config->backend_dir."/shop/catalogue/products?category_id=".$subcategory["id"];
				break;
			case 3:
				$href = "/".$DP_Config->backend_dir."/shop/logistics/stock?category_id=".$subcategory["id"];
				break;
		}
		
		//Если переход идет от специального поиска
		$sp = "";
		if( isset($_GET["sp"]) )
		{
			$sp = "?sp=yes";
		}
		?>
		
		<li>
			<a href="<?php echo $href.$sp; ?>">
				<span class="block_image">
					<img src="/content/files/images/catalogue_images/<?php echo $subcategory["image"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
				</span>
				<span class="block_caption"><?php echo translate_str_by_id($subcategory["value"]); ?></span>
			</a>
		</li>

		<?php
	}
	?>
	</ul>
<?php
}
?>