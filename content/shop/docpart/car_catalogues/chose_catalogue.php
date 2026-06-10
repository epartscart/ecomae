<?php
/**
 * Страничный скрипт для выбора каталога для выбранной марки
*/
define("_ASTEXE_", 1);

if( empty($_GET["car_id"]) )
{
	?>
	<script>
		location = "<?php echo $multilang_params['lang_href']; ?>/shop/avtomobilnye-katalogi";
	</script>
	<?php
	exit();
}

$car_id = $_GET["car_id"];

$car_query = $db_link->prepare('SELECT * FROM `shop_docpart_cars` WHERE `id` = :car_id;');
$car_query->bindValue(':car_id', $car_id);
$car_query->execute();
$car = $car_query->fetch();
?>



<?php
//Запрашиваем существующие каталоги для этой марки:
$SQL_SELECT_CAR_CATALOGUES = "SELECT
`shop_docpart_cars_catalogues`.`caption` AS `caption`,
`shop_docpart_cars_catalogues`.`image` AS `image`,
`shop_docpart_cars_catalogues`.`assoc_name` AS `assoc_name`,
`shop_docpart_cars_catalogues`.`include_on` AS `include_on`,
`shop_docpart_cars_catalogues`.`show_on` AS `show_on`,
`shop_docpart_cars_catalogues`.`options_json` AS `options_json_catalogue`,
`shop_docpart_cars_catalogue_links`.`value_int` AS `value_int_link`,
`shop_docpart_cars_catalogue_links`.`href` AS `href`,
`shop_docpart_cars_catalogues`.`id` AS `id`
FROM
`shop_docpart_cars_catalogue_links`
INNER JOIN `shop_docpart_cars_catalogues` ON `shop_docpart_cars_catalogues`.`id` = `shop_docpart_cars_catalogue_links`.`catalogue_id`
WHERE
`shop_docpart_cars_catalogue_links`.`car_id` = $car_id AND `show_on` = 1;";



$catalogue_query = $db_link->prepare('SELECT
`shop_docpart_cars_catalogues`.`caption` AS `caption`,
`shop_docpart_cars_catalogues`.`image` AS `image`,
`shop_docpart_cars_catalogues`.`assoc_name` AS `assoc_name`,
`shop_docpart_cars_catalogues`.`include_on` AS `include_on`,
`shop_docpart_cars_catalogues`.`show_on` AS `show_on`,
`shop_docpart_cars_catalogues`.`options_json` AS `options_json_catalogue`,
`shop_docpart_cars_catalogue_links`.`value_int` AS `value_int_link`,
`shop_docpart_cars_catalogue_links`.`href` AS `href`,
`shop_docpart_cars_catalogues`.`id` AS `id`
FROM
`shop_docpart_cars_catalogue_links`
INNER JOIN `shop_docpart_cars_catalogues` ON `shop_docpart_cars_catalogues`.`id` = `shop_docpart_cars_catalogue_links`.`catalogue_id`
WHERE
`shop_docpart_cars_catalogue_links`.`car_id` = :car_id AND `show_on` = 1;');
$catalogue_query->bindValue(':car_id', $car_id);
$catalogue_query->execute();
$catalogue = $catalogue_query->fetch();

if( $catalogue == false )
{
    ?>
    <?php echo translate_str_by_id(4202); ?>
    <?php
}
else
{
	$catalogue_query->execute();//Начать зановос с нулевой строки
    ?>
    <p><?php echo translate_str_by_id(4203); ?>:</p>
	<ul class="cat_blocks">
    <?php
    while($catalogue = $catalogue_query->fetch())
    {
        $image = $catalogue["image"];
        $caption = $catalogue["caption"];
        $assoc_name = $catalogue["assoc_name"];
        $include_on = $catalogue["include_on"];
        $show_on = $catalogue["show_on"];
        
        if($show_on == false)continue;
        
        $options_catalogue = json_decode($catalogue["options_json_catalogue"], true);//Опции каталога
        $value_int_link = $catalogue["value_int_link"];//Целочисленная опция ссылки данного каталога для данного автомобиля
        
        if($include_on == 1)
        {
            //Формируем ссылку
            switch($assoc_name)
            {
                case "autoxp":
                    //Проверяем, не превышен ли лимит
					$autoxp_limit_query = $db_link->prepare('SELECT `clicks_count` FROM `shop_docpart_autoxp_clicks` WHERE `month` = :month AND `year` = :year;');
					$autoxp_limit_query->bindValue(':month', date("n", time()));
					$autoxp_limit_query->bindValue(':year', date("Y", time()));
					$autoxp_limit_query->execute();
					$autoxp_limit_record = $autoxp_limit_query->fetch();
                    if($autoxp_limit_record == false)//Запросов еще не было - разрешаем
                    {
                        $href = "javascript:void(0);";
                        $onclick = "autoxp_redirect('".$catalogue["href"].$options_catalogue["client_id"]."');";
                    }
                    else
                    {
						$autoxp_limit_query->execute();
						
                        $autoxp_limit_record = $autoxp_limit_query->fetch();
                        $autoxp_limit = $autoxp_limit_record["clicks_count"];
                        //Запросов меньше 2000 - разрешаем
                        if($autoxp_limit < 2000)
                        {
                            $href = "javascript:void(0);";
                            $onclick = "autoxp_redirect('".$catalogue["href"].$options_catalogue["client_id"]."');";
                        }
                        else
                        {
                            $href = "javascript:void(0);";
                            $onclick = "alert('".translate_str_by_id(4204)."');";
                        }
                    }
                    break;
                case "ilcats":
                    $href = $catalogue["href"].$value_int_link;
                    $onclick = "";
                    break;
				case "catalogs_parts_com":
					$href = str_replace("client:", "client:".$options_catalogue["client"], $catalogue["href"]);
					$onclick = "";
					break;
				case "docpart_to":
					$href = $multilang_params['lang_href'].$catalogue["href"]."&car_id=$car_id";
					$onclick = "";
					break;
            }
        }
        else
        {
            $href = "javascript:void(0);";
            $onclick = "alert('".translate_str_by_id(4205)."');";
        }
        
        ?>
        
        
        <li>
        	<a href="<?php echo $href; ?>" onclick="<?php echo $onclick; ?>">
				<span class="block_image">
					<img src="/content/files/images/catalogue_logos/<?php echo $image; ?>" onerror="this.src='/content/files/images/no_image.png'" />
				</span>
        		<span class="block_caption"><?php echo translate_str_by_id($caption); ?></span>
        	</a>
        </li>
        
        <script>
        //Переход на autoxp
        function autoxp_redirect(dir)
        {
            //Сама проверка
        	jQuery.ajax({
                type: "GET",
                async: false, //Запрос синхронный
                url: "<?php echo $DP_Config->domain_path; ?>autoxp_clicks_control.php",
                dataType: "json",//Тип возвращаемого значения
                success: function(answer)
                {
                    if(answer == 0)
                    {
                        alert("<?php echo translate_str_by_id(4183); ?>");
                        location.reload();
                    }
                    else
                    {
                        location = dir;
                    }
                }
        	});
        }
        </script>
        
        
        
        <?php
    }
	?>
	</ul>
	<?php
}
?>