<?
function generateArticleUrl2 ($Num){
	$Num = preg_replace("/[&nbsp;\W]/", "", $Num);
	if (($_GET['clid'] and $_GET['clid']!=1) or $_GET['pid'] or $_GET['shopid']){
		$IdUppend='';
		if (!empty($_GET['clid'])) $IdUppend.="&clid={$_GET['clid']}";
		if (!empty($_GET['pid'])) $IdUppend.="&pid={$_GET['pid']}";
		if (!empty($_GET['shopid'])) $IdUppend.="&shopid={$_GET['shopid']}";
		$Link="/redirect.php?brand={$_GET['brand']}&num={$Num}{$IdUppend}";
	} else $Link=str_replace(['<%API_URL_BRAND_NAME%>', '<%API_URL_PART_NUMBER%>'], [$_GET['brand'], $Num], apiArticlePartLink);
	if (!defined('apiArticlePartLinkTarget') or apiArticlePartLinkTarget==1) $Target = '_blank'; else $Target='';
	return "<a href='{$Link}' target='{$Target}'>{$Num}</a>";
}
function generateBrandUrl($Array){
	$Num=preg_replace("/[&nbsp;\W]/", "", $Array['number']);
	$Brand=preg_replace("/[\s\W]/", "", $Array['brand']);
	$PartBrand=preg_replace("/[\s\W]/", "", $Array['partbrand']);
	if (($_GET['clid'] and $_GET['clid']!=1) or $_GET['pid'] or $_GET['shopid']){
		$Link="/redirect.php?brand={$Brand}&num={$Num}&partbrand={$PartBrand}".($_GET['clid'] ? "&clid={$_GET['clid']}" : "").($_GET['pid'] ? "&pid={$_GET['pid']}" : "").($_GET['shopid'] ? "&shopid={$_GET['shopid']}" : "");
	} else $Link=str_replace(['<%API_URL_BRAND_NAME%>', '<%API_URL_PART_NUMBER%>'], [$PartBrand, $Num],apiPartWBrandLink);
	if (!defined('apiPartWBrandLinkTarget') or apiPartWBrandLinkTarget == 1) $Target = '_blank'; else $Target='';
	return "<a href='{$Link}' target='{$Target}'>{$Num}</a>";
}
function generateLink2 ($LinkArr, $fullLink=true, $IgnoreVin=false){
	if (!empty($_GET['clid'])) $LinkArr["params"]['clid']=$_GET['clid'];
	if (!empty($_GET['pid'])) $LinkArr["params"]['pid']=$_GET['pid'];
	if (!empty($_GET['shopid'])) $LinkArr["params"]['shopid']=$_GET['shopid'];

	if ($LinkArr['RobotsRedirect']){
		$LinkArr["params"]['clid']=$LinkArr["params"]['pid']=$LinkArr["params"]['shopid']=$LinkArr["params"]['rewrite']='';
	}

	if ($_GET['CSSManager']) $LinkArr["params"]['CSSManager']=$_GET['CSSManager'];
	if ($_GET['cssdomain']) $LinkArr["params"]['cssdomain']=$_GET['cssdomain'];
	if ($_GET['vin']) {if ($_GET['CatDefaultPage'] and !$IgnoreVin) {unset($LinkArr["params"]['vin']);}}
	if (empty($LinkArr["params"]["language"]) && isset($_GET["language"])) $LinkArr["params"]["language"] = $_GET["language"];
	global $IlcatsInjections; if ($IlcatsInjections) {$IlcatsInjection='generateLink2'; include('IlcatsInjections.php');}
	if (in_array($LinkArr["params"]['brand'], ['cataloglist', 'cataloglistTest'])) unset($LinkArr["params"]['brand']);
	if ($LanguageLink=$LinkArr["params"]['LanguageLink']==1) {unset($LinkArr["params"]['LanguageLink'], $LinkArr["params"]['partInfo']);}
	if ($LinkArr["params"]) $Params=http_build_query($LinkArr["params"]);
	if ($Params) $Params="?" . $Params;

	if ($LinkArr["catRootUrl"]){
		unset($brand);
		if ($_GET["language"]!='ru') $Params='?language='.$_GET["language"];
	}
	if (($brand=='/' or $brand=='/cataloglist' or $brand=='/cataloglistTest') and ($LanguageLink or $LinkArr["params"]['VinAction'])) $brand='';

	$Link=$brand.'/'.$Params;
	if ($LinkArr['urlAnchor']) $Link.='#'.$LinkArr['urlAnchor'];
	if (defined('apiHttpCatalogsPath') and apiHttpCatalogsPath) $Link='/'.apiHttpCatalogsPath.$Link;

	if ($fullLink) {
		if ((!defined('apiPartUsageTarget') or apiPartUsageTarget == 1) and $get['function']=='getPartUsage') $Target = '_blank'; else $Target='';
		$Link="<a href='{$Link}' title='{$Title}' target='{$Target}'>{$LinkArr['linkText']}</a>";
	}
	return $Link;
}

function getApiData($params, $apiKey = apiKey, $apiDomain = apiDomain, $cliId = apiClientId,  $apiVersion = apiVersion){
	//Show($params);
	$st="?clientId=$cliId&apiKey=$apiKey&apiVersion=$apiVersion&domain=$apiDomain"."&partnerClientIp=".apiClientIpAddress;
	if ($params['function']=='getParts' and apiPartInfo>0) $params["partInfo"]=apiPartInfo;
	foreach ($params as $key=>$val)
		$st.="&$key=".$val;
	$url="http://api.ilcats.ru/".$st;
	global $IlcatsInjections; if ($IlcatsInjections) {$IlcatsInjection='getApiData'; include('IlcatsInjections.php');}
	//Show($url);

	$st=file_get_contents($url);
	//Show($st);
	$data=json_decode($st,true);
	return $data;
}
function ImplodeIfArray($Array, $Glue='', $ReturnScalar=true){return is_array($Array) ? implode($Glue, $Array) : ($ReturnScalar ? $Array : "");}

function Show($V){
	echo "<pre>";
	print_r($V);
	echo "</pre>";
}

function SendMail($To, $Subject, $Template, $Body, $From='', $AttachmentFileName='', $AttachmentPath='', $AttachmentNewFileName='')
{
	global $Site, $Locale;

	/*
		$m->ReplyTo("{$Site['Name']};{$Site['MainMail']}");
		$m->Cc($Site['MainMail']);
		$m->Priority(4) ;	// установка приоритета
		$m->log_on(true); // включаем лог, чтобы посмотреть служебную информацию
		echo "Письмо отправлено, вот исходный текст письма:<br><pre>", $m->Get(), "</pre>";
	*/
	require_once($_SERVER['DOCUMENT_ROOT']."/API.v2/PHP/LibMail/libmail.php");


	if ($To and $Body)
	{
		$Mail=new Mail('', '', true);
		if (!$From) {$From="errorreport@neoriginal.ru";}
		$Mail->From($From);
		$Mail->To($To);
		if (!$Subject) {$Subject='Сообщение об ошибке на сайте';}
		$Mail->Subject($Subject);
		$Mail->Body($Body, "html");
		$Mail->Bcc('k92393@ya.ru');
		if ($Attachment) {$Mail->Attach( $_SERVER['DOCUMENT_ROOT'].$AttachmentPath.$AttachmentFileName, $AttachmentNewFileName, "", "attachment");}
		$Mail->smtp_on("ssl://smtp.yandex.ru","errorreport@neoriginal.ru","1ErrorRep", 465, 10); // используя эу команду отправка пойдет через smtp
		$Mail->Send();
	}
}

function ShowApiAnswer($data, $debughash){
	if ($debughash){
		//Show(getApiData(['function'=>'checkDebugHash', 'debughash'=>$debughash]));
		if (getApiData(['function'=>'checkDebugHash', 'debughash'=>$debughash])['result'])
			if ($data) Show($data);
			else 'Wrong server answer';
	}

}


?>