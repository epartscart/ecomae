<?php
/**
 * Скрипт для вывода информации по офису обслуживания
*/
defined('_ASTEXE_') or die('No access');


$office_query = $db_link->prepare('SELECT * FROM `shop_offices` WHERE `id` = ?;');
$office_query->execute( array($office_to_show) );
$office = $office_query->fetch();
?>

<table class="table">
	<tr>
		<th><?php echo translate_str_by_id(4418); ?></th>
	</tr>
	<tr>
		<td>
			<span><?php echo translate_str_by_id(3376); ?>: <?php echo translate_str_by_id($office["city"]).", ".translate_str_by_id($office["address"]); ?></span> <a title="<?php echo translate_str_by_id(4445); ?>" style="cursor:pointer;" data-toggle="collapse" onClick="ymaps_init();" data-target="#collapse_office_map_container" aria-expanded="false" aria-controls="collapse_office_map_container"><i class="fa fa-map-o" aria-hidden="true"></i></a>
			<div class="collapse" id="collapse_office_map_container">
				<br/>
				<div style="width: 100%;" id="map" class="office_map_container"></div>
			</div>
		</td>
	</tr>
	<tr>
		<td><?php echo translate_str_by_id(4446); ?>: <?php echo str_replace(array("\n"), "<br>",translate_str_by_id($office["timetable"])); ?></td>
	</tr>
	<tr>
		<td><?php echo translate_str_by_id(1312); ?>: <?php echo $office["phone"]; ?></td>
	</tr>
</table>



<!-- START БЛОК ДЛЯ ВЫВОДА СХЕМ РАСПОЛОЖЕНИЯ ОФИСОВ -->
<?php
$yandex_lang = 'ru-RU';
if( $multilang_params['lang'] != 'ru' )
{
	$yandex_lang = 'en_US';
}
?>
<script src="https://api-maps.yandex.ru/2.0-stable/?load=package.standard&lang=<?php echo $yandex_lang; ?>" type="text/javascript"></script>
<script type="text/javascript">
	var myMap, myPlacemark;
	var ymaps_init_flag = 0;
	// --------------------------------------------------------------------------------------
	function map_init()
	{
		myMap = new ymaps.Map ("map", {
			center: [<?php echo $office["coordinates"]; ?>],
			zoom: 16
		}); 
		
		
		myPlacemark = new ymaps.Placemark([<?php echo $office["coordinates"]; ?>], {balloonContent: "<?php echo translate_str_by_id($office["caption"]); ?>"}, {
        			iconImageHref: "/content/files/images/maps-marker.png",
        			iconImageSize: [27, 44],
        			iconImageOffset: [-13, -44]});
		
		myMap.geoObjects.add(myPlacemark);
		
		
		myMap.controls.add(new ymaps.control.MapTools());
		myMap.controls.add('typeSelector');
		myMap.controls.add('zoomControl');
	}
	// --------------------------------------------------------------------------------------
	function ymaps_init(){
		if(ymaps_init_flag == 0){
			ymaps.ready(map_init);
			ymaps_init_flag = 1;
		}
	}
</script>
<!-- END БЛОК ДЛЯ ВЫВОДА СХЕМ РАСПОЛОЖЕНИЯ ОФИСОВ -->