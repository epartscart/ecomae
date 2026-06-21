<?php
/**
 * Страничный скрипт для вывода логотипов автомобилей
*/
define("_ASTEXE_", 1);

?>

<?php
//Блок с общими каталогами - скрыт. Подключается по требованию клиента
if(false)
{
	?>
	<h1><?php echo translate_str_by_id(4200); ?></h1>
	<div style="display:table; margin-bottom:20px;">
		<a href="http://aftermarket.catalogs-parts.com/#{client:hfiecq;page:models;lang:ru;catalog:pc}" class="btn btn-large btn-success"><?php echo translate_str_by_id(4201); ?> AFTERMARKET catalogs-parts.com</a>
	</div>
	<?php
}
?>



<?php
$cars_query = $db_link->prepare('SELECT *, (SELECT DISTINCT(`car_id`) FROM `shop_docpart_cars_catalogue_links` WHERE `car_id` = `shop_docpart_cars`.id) AS `link_id` FROM `shop_docpart_cars`  ORDER BY `caption`;');
$cars_query->execute();
?>

<ul class="bs-glyphicons-list">
<?php
while($car = $cars_query->fetch())
{
    if($car["link_id"] == null)continue;
    ?>
	<li>
		<a href="<?php echo $multilang_params['lang_href']; ?>/shop/avtomobilnye-katalogi/vybor-kataloga?car_id=<?php echo $car["id"]; ?>">
			<img src="/content/files/images/car_logos/<?php echo $car["image"]; ?>" /><br>
			<span class="glyphicon-class"><?php echo translate_str_by_id($car["caption"]); ?></span>
		</a>
	</li>
    <?php
}
?>
</ul>