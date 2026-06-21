<?php
$catalog_options = array("apiClientId"=>15652, "apiKey"=>"2fc9e2385b7e5270ade11c165ca84c8b", "apiDomain"=>"incar62.ru");

define ("apiClientId", $catalog_options["apiClientId"]);
define ("apiKey", $catalog_options["apiKey"]);
define ("apiDomain" , $catalog_options["apiDomain"]);
define ("apiStaticContentHost","//static.ilcats.ru");
define ("apiImagesHost","//images.ilcats.ru");
define ("apiVersion","2.0");
define ("apiArticlePartLink","http://".$catalog_options["apiDomain"]."/".$multilang_params['lang_href_no_slash']."/shop/part_search?article=<%API_URL_PART_NUMBER%>");
define ("apiArticlePartLinkTarget", 1);
define ("apiPartWBrandLink", 'http://'.$catalog_options["apiDomain"]."/".$multilang_params['lang_href_no_slash'].'/shop/part_search?article=<%API_URL_PART_NUMBER%>');
define ("apiPartWBrandLinkTarget", 1);
define ("apiPartUsageTarget", 0);
define ("apiPartInfo",$partInfoValue);


define ("apiClientIpAddress",$_SERVER["REMOTE_ADDR"]);

define ("apiHttpCatalogsPath",'originalnye-katalogi');

$apiActiveLanguages=[
	'ru'
];
?>