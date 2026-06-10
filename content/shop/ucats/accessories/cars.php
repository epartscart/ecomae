<?php
/*
Скрипт демонстрационной страницы каталога ТО
Выбор марки автомобиля
*/
defined('_ASTEXE_') or die('No access');
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
//Делаем запрос в веб-сервис Ucats
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, $DP_Config->ucats_url."ucats/accessories/get_cars.php?login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password);
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
	
	$arr = array();
	if(is_array($curl_result["list"])){
		foreach($curl_result["list"] as $item){
			if(!in_array($item['name'], $ucats_filter_brends) && !in_array(mb_strtoupper(trim($item['name']), "UTF-8"), $ucats_filter_brends))
			{
				$arr[] = $item;
			}
		}
	}
	$curl_result["list"] = $arr;
	
	$cars_by_key = array();//Массив в виде: "Буква"=>"Массив марок"
	$cars_count_total = count($curl_result["list"]);//Общее количество марок
	for($i=0; $i < count($curl_result["list"]); $i++)
	{
		//Получаем первую букву:
		$letter = mb_substr($curl_result["list"][$i]["name"], 0, 1, "UTF-8");
		
		if( ! isset($cars_by_key[$letter]) )
		{
			$cars_by_key[$letter] = array();
		}
		
		
		array_push($cars_by_key[$letter], $curl_result["list"][$i]);
	}
	
	
	
	//Выводим автомобили
	//Всего будет $cols_count колонок.:
	$cols_count = 5;
	$cars_for_col = (int)($cars_count_total/$cols_count) + 1;//Количество автомобилей в одной колонке
	$col_counter = 0;//Выведено марок в текущей колонке
	$cars_counter = 0;//Выведено марок всего - на все колонки
	$shown_letters = array();//Массив показанных букв
	foreach($cars_by_key AS $letter => $cars)
	{
		for($i=0; $i < count($cars); $i++)
		{
			if($col_counter == 0)
			{
				?>
				<ul class="search_tab_car_ul">
				<?php
			}
			
			?>
			<li>
				<div class="like_search_tab_car_letter">
				<?php
				//Показываем букву, если еще такую не показывали
				if( array_search($letter, $shown_letters) === false )
				{
					echo $letter;
					array_push($shown_letters, $letter);
				}
				?>
				</div>
				<div class="like_search_tab_car_caption">
					<a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/avtoaksessuary/vybor-modeli?car_name=<?php echo urlencode($cars[$i]["name"]); ?>">
						<?php echo strtoupper($cars[$i]["name"]); ?>
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