<?php
/*
Скрипт демонстрационной страницы каталога ТО
Список запчастей
*/
defined('_ASTEXE_') or die('No access');

//Получаем данные:
$car_name = htmlentities($_GET["car_name"], ENT_QUOTES, "UTF-8");
$model_caption = htmlentities($_GET["model_caption"], ENT_QUOTES, "UTF-8");
$car_id = (int)$_GET["car_id"];
if( isset($_GET["car_id_to"]) )
{
	$car_id_to = (int)$_GET["car_id_to"];
}

$ucats_filter_brends = array();
if( isset($DP_Config->ucats_filter_brends) && !empty($DP_Config->ucats_filter_brends) ){
$tmp_arr = explode(',', $DP_Config->ucats_filter_brends);
if(is_array($tmp_arr)){
	foreach($tmp_arr as $gtm){
		$gtm = trim($gtm);
		if( ! empty($gtm) ){
			$ucats_filter_brends[] = $gtm;
		}
	}
}
}

$model_id_to = (int)$_GET["model_id_to"];
$img = htmlentities($_GET["img"], ENT_QUOTES, "UTF-8");
$type_id = (int)$_GET["type_id"];
$type_caption = htmlentities($_GET["type_caption"], ENT_QUOTES, "UTF-8");

if(in_array($car_name, $ucats_filter_brends) || in_array(mb_strtoupper(trim($car_name), "UTF-8"), $ucats_filter_brends) || in_array($model_caption, $ucats_filter_brends))
{
	echo translate_str_by_id(3);
}else{
	?>
	<table class="table">
		<tr>
			<td>
				<div align="left" style="padding:5px;"><b><?php echo translate_str_by_id(2085); ?>:</b> <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya/vybor-modeli?car_id=<?php echo $car_id; ?>&car_name=<?php echo $car_name; ?>" class="bread_crumbs_a"><?php echo ucwords($car_name); ?></a></div>
				<div align="left" style="padding:5px;"><b><?php echo translate_str_by_id(2086); ?>:</b> <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya/vybor-modeli/vybor-komplektacii?car_id=<?php echo $car_id; ?>&car_name=<?php echo $car_name; ?>&model_id_to=<?php echo $model_id_to; ?>&model_caption=<?php echo $model_caption; ?>&img=<?php echo $img; ?>" class="bread_crumbs_a"><?php echo $model_caption; ?></a></div>
				<div align="left" style="padding:5px;"><b><?php echo translate_str_by_id(4256); ?>:</b> <?php echo $type_caption; ?></div>
			</td>
			<td>
				<img src="<?php echo $img; ?>" />
			</td>
		</tr>
	</table>
	<?php
	//Получаем список моделей выбранной марки через веб-сервис каталога
	$curl = curl_init();
	curl_setopt($curl, CURLOPT_URL, $DP_Config->ucats_url."ucats/to/get_parts.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&type_id=$type_id");
	curl_setopt($curl, CURLOPT_HEADER, 0);
	curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
	curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
	curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
	curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
	$curl_result = curl_exec($curl);
	curl_close($curl);
	$curl_result = json_decode($curl_result, true);

	if($curl_result["status"] == "ok")
	{
		//var_dump($curl_result);
		?>
		<table class="table">
			<tr>
				<th align="left"><?php echo translate_str_by_id(2073); ?></th>
				<th align="left">OEM</th>
				<th align="left"><?php echo translate_str_by_id(3571); ?></th>
				<th align="left"></th>
			</tr>
			
			<?php
			for($i=0; $i < count($curl_result["list"]); $i++)
			{
				$part = $curl_result["list"][$i];
				
				if($part["search"] == "")$part["search"] = $part["article"];
				if($part["search"] == "") $part["search"] == "NO";
				
				$tr_class = "even";
				if($i % 2 == 0)
				{
					$tr_class = "odd";
				}
				
				$href = $multilang_params['lang_href']."/shop/part_search?article=".$part["search"];
				$onclick = "";
				?>
				<tr class="<?php echo $tr_class; ?>">
					<td><a onclick="<?php echo $onclick; ?>" class="bread_crumbs_a" href="<?php echo $href; ?>"><?php echo $part["descr"]; ?></a></td>
					<td><a onclick="<?php echo $onclick; ?>" class="bread_crumbs_a" href="<?php echo $href; ?>"><?php echo $part["search"]; ?></a></td>
					<td><a onclick="<?php echo $onclick; ?>" class="bread_crumbs_a" href="<?php echo $href; ?>"><?php echo $part["comment"]; ?></a></td>
					<td><a onclick="<?php echo $onclick; ?>" class="bread_crumbs_a" href="<?php echo $href; ?>"><i class="fa fa-search"></i> <?php echo translate_str_by_id(2763); ?></a></td>
				</tr>
				<?php
			}
			?>
			
			
		</table>
		<?php
		
	}
	else
	{
		var_dump($curl_result);
	}
}
?>