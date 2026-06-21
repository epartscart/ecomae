<?php
function epc_currency_supported_defaults()
{
	return array(
		'784' => array('iso_name'=>'AED','caption_short'=>'AED','sign'=>'AED','rate'=>1,'order'=>1),
		'840' => array('iso_name'=>'USD','caption_short'=>'USD','sign'=>'$','rate'=>3.6725,'order'=>2),
		'978' => array('iso_name'=>'EUR','caption_short'=>'EUR','sign'=>'€','rate'=>4.0,'order'=>3),
		'586' => array('iso_name'=>'PKR','caption_short'=>'PKR','sign'=>'Rs','rate'=>0.0131,'order'=>4),
		'643' => array('iso_name'=>'RUB','caption_short'=>'RUB','sign'=>'₽','rate'=>0.040,'order'=>5),
		'682' => array('iso_name'=>'SAR','caption_short'=>'SAR','sign'=>'SAR','rate'=>0.979,'order'=>6),
		'414' => array('iso_name'=>'KWD','caption_short'=>'KWD','sign'=>'KWD','rate'=>12.0,'order'=>7),
		'512' => array('iso_name'=>'OMR','caption_short'=>'OMR','sign'=>'OMR','rate'=>9.54,'order'=>8),
		'634' => array('iso_name'=>'QAR','caption_short'=>'QAR','sign'=>'QAR','rate'=>1.01,'order'=>9),
		'48' => array('iso_name'=>'BHD','caption_short'=>'BHD','sign'=>'BHD','rate'=>9.74,'order'=>10)
	);
}

function epc_currency_country_map()
{
	return array(
		'AE'=>'784','PK'=>'586','US'=>'840','GB'=>'840',
		'DE'=>'978','FR'=>'978','IT'=>'978','ES'=>'978','NL'=>'978','BE'=>'978','AT'=>'978','IE'=>'978','PT'=>'978','FI'=>'978','GR'=>'978',
		'RU'=>'643','SA'=>'682','KW'=>'414','OM'=>'512','QA'=>'634','BH'=>'48'
	);
}

function epc_currency_ensure_supported($db_link)
{
	foreach(epc_currency_supported_defaults() as $iso_code => $row)
	{
		try
		{
			$exists = $db_link->prepare("SELECT COUNT(*) FROM `shop_currencies` WHERE `iso_code` = ?;");
			$exists->execute(array($iso_code));
			if((int)$exists->fetchColumn() > 0)
			{
				$stmt = $db_link->prepare("UPDATE `shop_currencies` SET `available` = 1, `caption_short` = ?, `sign` = ? WHERE `iso_code` = ?;");
				$stmt->execute(array($row['caption_short'], $row['sign'], $iso_code));
			}
			else
			{
				$stmt = $db_link->prepare("INSERT INTO `shop_currencies` (`iso_code`, `iso_name`, `caption_short`, `sign`, `rate`, `available`, `order`) VALUES (?, ?, ?, ?, ?, 1, ?);");
				$stmt->execute(array($iso_code, $row['iso_name'], $row['caption_short'], $row['sign'], $row['rate'], $row['order']));
			}
		}
		catch(Exception $e)
		{
		}
	}
}

function epc_currency_records($db_link, $DP_Config)
{
	epc_currency_ensure_supported($db_link);
	$records = array();
	try
	{
		$supported = array_keys(epc_currency_supported_defaults());
		$placeholders = implode(',', array_fill(0, count($supported), '?'));
		$query = $db_link->prepare("SELECT `iso_code`, `iso_name`, `caption_short`, `sign`, `rate`, `available` FROM `shop_currencies` WHERE `iso_code` IN (".$placeholders.") ORDER BY `order`, `iso_name`;");
		$query->execute($supported);
		while($row = $query->fetch(PDO::FETCH_ASSOC))
		{
			$rate = (float)$row['rate'];
			if($rate <= 0) { $rate = 1; }
			$records[(string)$row['iso_code']] = array(
				'iso_code'=>(string)$row['iso_code'],
				'iso_name'=>(string)$row['iso_name'],
				'caption_short'=>(string)$row['caption_short'],
				'sign'=>(string)$row['sign'],
				'rate'=>$rate
			);
		}
	}
	catch(Exception $e)
	{
	}
	if(empty($records[(string)$DP_Config->shop_currency]))
	{
		$defaults = epc_currency_supported_defaults();
		$records[(string)$DP_Config->shop_currency] = isset($defaults[(string)$DP_Config->shop_currency]) ? $defaults[(string)$DP_Config->shop_currency] : array('iso_code'=>(string)$DP_Config->shop_currency,'iso_name'=>'AED','caption_short'=>'AED','sign'=>'AED','rate'=>1);
	}
	return $records;
}

function epc_currency_selected_iso($records, $DP_Config, $db_link = null)
{
	$user_id = 0;
	if (class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
		$user_id = (int)DP_User::getUserId();
	}
	if ($user_id > 0) {
		if (!function_exists('epc_trade_user_currency_iso')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_customer_trade.php';
		}
		if ($db_link === null && isset($GLOBALS['db_link']) && $GLOBALS['db_link']) {
			$db_link = $GLOBALS['db_link'];
		}
		if ($db_link) {
			$fixed = epc_trade_user_currency_iso($db_link, $user_id);
			if ($fixed !== '' && isset($records[$fixed])) {
				return $fixed;
			}
		}
	}

	$shop_iso = (string)$DP_Config->shop_currency;
	$selected = isset($_COOKIE['epc_currency']) ? preg_replace('/[^0-9]/', '', (string)$_COOKIE['epc_currency']) : '';
	if($selected !== '' && isset($records[$selected])) { return $selected; }
	$country = isset($_COOKIE['epc_country']) ? strtoupper(preg_replace('/[^A-Z]/', '', (string)$_COOKIE['epc_country'])) : '';
	$map = epc_currency_country_map();
	if($country !== '' && isset($map[$country]) && isset($records[$map[$country]])) { return $map[$country]; }
	return $shop_iso;
}

function epc_currency_format_amount($amount, $records, $selected_iso, $mode = 'sign_before')
{
	$selected_iso = isset($records[$selected_iso]) ? $selected_iso : key($records);
	$record = $records[$selected_iso];
	$rate = !empty($record['rate']) ? (float)$record['rate'] : 1;
	$value = ((float)$amount) / $rate;
	$number = number_format($value, 2, '.', ' ');
	$indicator = ($mode == 'short_name_after') ? $record['caption_short'] : $record['sign'];
	if($mode == 'no') { return $number; }
	if($mode == 'sign_after' || $mode == 'short_name_after') { return $number.' '.$indicator; }
	return $indicator.' '.$number;
}

function epc_currency_js_config($records, $selected_iso, $base_iso, $mode)
{
	return array(
		'base'=>(string)$base_iso,
		'selected'=>(string)$selected_iso,
		'mode'=>$mode,
		'currencies'=>$records,
		'countryMap'=>epc_currency_country_map()
	);
}
?>
