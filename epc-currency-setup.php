<?php
declare(strict_types=1);
if(($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit('Forbidden');
}
header('Content-Type: text/plain; charset=utf-8');
require __DIR__ . '/config.php';
$cfg = new DP_Config();
$pdo = new PDO('mysql:host='.$cfg->host.';dbname='.$cfg->db, $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
$pdo->query("SET NAMES utf8;");

$currencies = array(
	'784' => array('AED','AED','AED',1,1),
	'840' => array('USD','USD','$',3.6725,2),
	'978' => array('EUR','EUR','€',4.0,3),
	'586' => array('PKR','PKR','Rs',0.0131,4),
	'643' => array('RUB','RUB','₽',0.040,5),
	'682' => array('SAR','SAR','SAR',0.979,6),
	'414' => array('KWD','KWD','KWD',12.0,7),
	'512' => array('OMR','OMR','OMR',9.54,8),
	'634' => array('QAR','QAR','QAR',1.01,9),
	'48' => array('BHD','BHD','BHD',9.74,10)
);
foreach($currencies as $iso => $row) {
	$exists = $pdo->prepare("SELECT COUNT(*) FROM `shop_currencies` WHERE `iso_code` = ?;");
	$exists->execute(array($iso));
	if((int)$exists->fetchColumn() > 0) {
		$stmt = $pdo->prepare("UPDATE `shop_currencies` SET `iso_name` = ?, `caption_short` = ?, `sign` = ?, `rate` = ?, `available` = 1, `order` = ? WHERE `iso_code` = ?;");
		$stmt->execute(array($row[0], $row[1], $row[2], $row[3], $row[4], $iso));
	} else {
		$stmt = $pdo->prepare("INSERT INTO `shop_currencies` (`iso_code`, `iso_name`, `caption_short`, `sign`, `rate`, `available`, `order`) VALUES (?, ?, ?, ?, ?, 1, ?);");
		$stmt->execute(array($iso, $row[0], $row[1], $row[2], $row[3], $row[4]));
	}
}

$configPath = __DIR__ . '/config.php';
$config = file_get_contents($configPath);
$config = preg_replace('/public\s+\$shop_currency\s*=\s*[\'"][^\'"]*[\'"]\s*;/', "public \$shop_currency = '784';", $config);
file_put_contents($configPath, $config);
@unlink(__FILE__);
echo "Currency setup complete. Base shop currency is AED (784). Display currencies enabled: AED, USD, EUR, PKR, RUB, SAR, KWD, OMR, QAR, BHD.\n";
?>
