<?php
/*
Скрипт демонстрационной страницы каталога ТО
Выбор модели автомобиля
*/
defined('_ASTEXE_') or die('No access');

$car_name = htmlentities($_GET["car_name"], ENT_QUOTES, "UTF-8");
$car_id = (int)$_GET["car_id"];
?>

<style>
.like_search_tab_car_letter
{
	display: inline-block!important;
    width: 25px!important;
    font-weight: bold!important;
    font-size: 16px!important;
}
.like_search_tab_car_caption
{
	display: inline-block!important;
}
</style>

<?php
//Получаем список моделей выбранной марки через веб-сервис каталога
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, $DP_Config->ucats_url."ucats/to/get_car_models.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&car_id=".$car_id);
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
	
	if(in_array($car_name, $ucats_filter_brends) || in_array(mb_strtoupper(trim($car_name), "UTF-8"), $ucats_filter_brends))
	{
		echo translate_str_by_id(3);
	}else{
		//Всего будет $cols_count колонок.:
		$cols_count = 3;
		$cars_for_col = (int)(count($curl_result["list"])/$cols_count) + 1;//Количество автомобилей в одной колонке
		$col_counter = 0;//Выведено элементов в текущей колонке
		$cars_counter = 0;//Выведено элементов всего - на все колонки
		$cars_count_total = NULL;
		for($i=0; $i < count($curl_result["list"]); $i++)
		{
			if($col_counter == 0)
			{
				?>
				<ul class="search_tab_car_ul">
				<?php
			}
			
			
			$car = $curl_result["list"][$i];
			$model_id_to = $car["id"];
			
			
			if( ! isset($car["img"]) )
			{
				$car["img"] = NULL;
			}
			
			if(in_array($car["title"]." ".$car["content"], $ucats_filter_brends)){
				continue;
			}
			?>
			<li>
				<div class="like_search_tab_car_caption">
					<a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya/vybor-modeli/vybor-komplektacii?car_id=<?php echo $car_id; ?>&model_id_to=<?php echo $model_id_to; ?>&model_caption=<?php echo urlencode($car["title"]." ".$car["content"]); ?>&car_name=<?php echo urlencode($car_name); ?>&img=<?php echo urlencode($car["img"]); ?>">
						<?php echo $car["title"]." ".$car["content"]; ?>
					</a>
				</div>
			</li>
			<?php
			
			//Автомобиль выведен - инкрементируем счетчики
			$col_counter++;
			$cars_counter++;
			
			
			//Если в данной колонке выведены все автомобили ИЛИ если выведены вообще все автомобили
			if($col_counter == $cars_for_col || $cars_counter == $cars_count_total)
			{
				$col_counter = 0;//Сбрасываем счетчик выведеных автомобилей в колонке
				?>
				</ul>
				<?php
			}
			
		}
	}
}
else
{
	var_dump($curl_result);
}
?>