<?php
/*
Плагин для замены мета-данных страницы
*/

defined('_ASTEXE_') or die('No access');

//$DP_Config->show_site_name = 0;
//$DP_Content->title_tag = "Проверка";

//Получаем правило формирования метаданных для данной страницы
$stmt = $db_link->prepare('SELECT * FROM `metadata_handler_rules` WHERE `content_id` = :content_id;');
$stmt->bindValue(':content_id', $DP_Content->id);
$stmt->execute();

$rule = $stmt->fetch(PDO::FETCH_ASSOC);

if($rule != false)
{
	//Правило для тега title
	if($rule["title_rule"] != "")
	{
		$title_rule = json_decode($rule["title_rule"], true);
		
		//В зависимости от типа правила - выбираем способ получения тега
		switch($title_rule["type"])
		{
			//Наименование берется от url
			case "url":
				$url = $title_rule["value"];//Адрес
				//Аргументы
				for($a=0; $a < count($title_rule["args"]); $a++)
				{
					switch($title_rule["args"][$a]["type"])
					{
						//Получаем из конфигурационного файла
						case "config":
							$url = str_replace("%".$a, $DP_Config->{$title_rule["args"][$a]["value"]}, $url);
							break;
						//Получаем из аргументов сктраницы
						case "get":
							$url = str_replace("%".$a, $_GET[$title_rule["args"][$a]["value"]],$url);
							break;
						//Записано явно
						case "text":
							$url = str_replace("%".$a, $title_rule["args"][$a]["value"], $url);
							break;
					}
				}
				
				
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, $url);
				curl_setopt($curl, CURLOPT_HEADER, 0);
				curl_setopt($curl, CURLOPT_RETURNTRANSFER, 1);
				curl_setopt($curl, CURLOPT_SSL_VERIFYHOST, 0);
				curl_setopt($curl, CURLOPT_SSL_VERIFYPEER, 0);
				$curl_result = curl_exec($curl);
				curl_close($curl);
				$DP_Content->title_tag = $curl_result;
				break;
			case "complex":
				$DP_Content->title_tag = translate_str_by_id($title_rule["value"]);
				for($a=0; $a < count($title_rule["args"]); $a++)
				{
					switch($title_rule["args"][$a]["type"])
					{
						case "get":
							$DP_Content->title_tag = str_replace("%".$a, $_GET[$title_rule["args"][$a]["value"].""], $DP_Content->title_tag);
							break;
					}
				}
				break;
		}//~switch($title_rule["type"])
	}//~if($rule["title_rule"] != "")
	if($rule["description_rule"] != "")
	{
		$description_rule = json_decode($rule["description_rule"], true);
		//В зависимости от типа правила - выбираем способ получения тега
		switch($description_rule["type"])
		{
			case "like_title":
				$DP_Content->description_tag = str_replace("\"", "\\\"", $DP_Content->title_tag);
				break;
		}
	}
}//~if(mysqli_num_rows($rule_query) == 1)
//ДАЛЕЕ ПОЛУЧАЕМ мета-данные для URL (если он прописан)
$stmt = $db_link->prepare('SELECT * FROM `text_for_url` WHERE `url` = :url;');
$stmt->bindValue(':url', getPageUrl());
$stmt->execute();
$url_record = $stmt->fetch(PDO::FETCH_ASSOC);
if( $url_record != false )
{	
	if( $url_record["title_tag"] != "" )
	{
		$DP_Content->title_tag = $url_record["title_tag"];
	}
	if( $url_record["description_tag"] != "" )
	{
		$DP_Content->description_tag = $url_record["description_tag"];
	}
	if( $url_record["keywords_tag"] != "" )
	{
		$DP_Content->keywords_tag = $url_record["keywords_tag"];
	}
}
?>