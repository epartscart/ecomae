<?php
/*Страничный скрипт для отображения логов отладки API поставщиков*/
defined('_ASTEXE_') or die('No access');



//ID склада, для которого нужно показать лог
$storage_id = (int)$_GET["storage_id"];




if( $storage_id > 0 && (int)$DP_Config->suppliers_api_debug == 1 )
{
	if( file_exists($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$storage_id.".php") )
	{
		require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/tmp/suppliers_api_log/".$storage_id.".php");
	}
	else
	{
		echo translate_str_by_id(3348);
	}
}
else
{
	echo translate_str_by_id(3349);
}

?>