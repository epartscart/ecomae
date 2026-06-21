<?php
defined('_ASTEXE_') or die('No access');
//Скрипт вывода специальных поисков для покупателя
?>


<?php
//Получаем список специальных поисков
$sp_query = $db_link->prepare('SELECT COUNT(*) FROM `shop_special_searches` WHERE `active` = 1 ORDER BY `order`;');
$sp_query->execute();
$sp_count_rows = $sp_query->fetchColumn();
if( $sp_count_rows > 0 )
{
	?>
	<div class="col-xs-12 col-sm-12 col-md-12 col-lg-12">
	<h2 class="section-title"><?php echo translate_str_by_id(4146); ?></h2>
	<?php
}


$sp_query = $db_link->prepare('SELECT * FROM `shop_special_searches` WHERE `active` = 1 ORDER BY `order`;');
$sp_query->execute();
?>
<ul class="cat_blocks">
<?php
while( $sp = $sp_query->fetch() )
{
	/*
	Страрый вариант, до ЧПУ
	?>
	<li>
    	<a href="/shop/search_products?search_type=<?php echo $sp["alias"]; ?>">
    		<span class="block_image">
				<img src="/content/files/images/catalogue_images/<?php echo $sp["img"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
			</span>
    		<span class="block_caption"><?php echo $sp["caption"]; ?></span>
    	</a>
    </li>
	<?php
	*/
	?>
	<li>
    	<a href="<?php echo $multilang_params['lang_href']; ?>/<?php echo $sp["alias"]; ?>">
    		<span class="block_image">
				<img src="/content/files/images/catalogue_images/<?php echo $sp["img"]; ?>" onerror="this.src='/content/files/images/no_image.png'" />
			</span>
    		<span class="block_caption"><?php echo translate_str_by_id($sp["caption"]); ?></span>
    	</a>
    </li>
	<?php
}
?>
</ul>


<?php
if( $sp_count_rows > 0 )
{
	?>
	</div>
	<?php
}
?>