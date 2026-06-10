<?php
// Информация по отдельным товарам
defined('_UCatalog_') or die('No access');

// Получаем данные от API
$postdata = array();
$postdata['article'] = mb_strtoupper(preg_replace("/[^a-zA-Z0-9А-Яа-яёЁ]+/ui", "", $request_object['article']), "UTF-8");
$postdata['manufacturer'] = json_encode(array(mb_strtoupper(trim($request_object['manufacturer']), "UTF-8")));

$curl_result = u_curl($postdata, 'part_info');

$result = json_decode($curl_result, true);

if($result['status'] === true){
	$answer["status"] = true;
	$answer["json"] = $curl_result;
}

$answer["key"] = $request_object['key'];
?>