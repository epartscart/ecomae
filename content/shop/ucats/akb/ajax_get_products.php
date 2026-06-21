<?php
//Серверный скрипт для получения списка товаров
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;//Конфигурация CMS

$products_query = $_POST["products_query"];


//Делаем запрос в веб-сервис Ucats
$curl = curl_init();
curl_setopt($curl, CURLOPT_URL, $DP_Config->ucats_url."ucats/akb/get_products.php");
curl_setopt($curl, CURLOPT_HEADER, 0);
curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
curl_setopt($curl, CURLOPT_POST, true);
curl_setopt($curl, CURLOPT_POSTFIELDS, "login=".$DP_Config->ucats_login."&password=".$DP_Config->ucats_password."&products_query=".urlencode($products_query));
curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
curl_setopt($curl, CURLOPT_FOLLOWLOCATION, true);
$curl_result = curl_exec($curl);
curl_close($curl);
$curl_result = json_decode($curl_result, true);

if(is_array($curl_result["products"])){
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
	if(!empty($ucats_filter_brends)){
		$arr = array();
		foreach($curl_result["products"] as $item){
			if(!in_array($item['manufacturer'], $ucats_filter_brends)){
				$arr[] = $item;
			}
		}
		$curl_result["products"] = $arr;
	}
}

exit(json_encode($curl_result["products"]));
?>