<?php
//Генерирет хеш
function genHmacStr($array, $secret_key) 
{
	// @array - массив POST-данных
	// @secret_key - Ключ

	$str = ""; //строка данных для HMAC
	
	foreach($array as $key=>$value) 
	{
		if( $key == "DESC" || $key == "P_SIGN" ) //пропускаем не нужные параметры
		{
			continue;
		}
		
		$length_value = iconv_strlen($value); //Получаем длину параметра
		
		if($value == "")
		{
			$value = "-";
			$length_value = "";
		}
		
		$str .= $length_value.$value; //Добавляем очередную длину и значение параметра к строке
	}
	
	$strHMAC = hash_hmac("sha256", $str, pack("H*", $secret_key)); //Получаем HASH
	
	return  $strHMAC;
}
//------------------------------------------------------------------------------------------------//

?>