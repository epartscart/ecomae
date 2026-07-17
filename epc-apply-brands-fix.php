<?php
/**
 * Apply /parts/brands 500 fix via rename (root-owned part_search_page.php).
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$path = __DIR__ . '/content/shop/docpart/part_search_page.php';
$bak = $path . '.bak-brands-trace';
$src = file_get_contents(is_file($bak) ? $bak : $path);
if ($src === false) {
	exit("read fail\n");
}
// Strip any leftover trace instrumentation
if (strpos($src, 'EPC_BRANDS_TRACE') !== false && is_file($bak)) {
	$src = file_get_contents($bak);
}

$changes = 0;

$oldLegacy = <<<'OLD'
// Legacy /shop/part_search?article=… redirects are handled in dp_core.php (302 to brand picker or single-brand CHPU).
if( $DP_Config->chpu_search_config["chpu_search_on"] == true )
{
	if( isset($_GET["article"]) )
	{
OLD;
$newLegacy = <<<'NEW'
// Legacy /shop/part_search?article=… redirects are handled in dp_core.php (302 to brand picker or single-brand CHPU).
// CHPU pages (/parts/brands/ARTICLE etc.) use service_data, not $_GET — never hard-exit those.
if( $DP_Config->chpu_search_config["chpu_search_on"] == true )
{
	if( isset($_GET["article"]) && empty($DP_Content->service_data['article_search_chpu']) )
	{
NEW;
if (strpos($src, $oldLegacy) !== false) {
	$src = str_replace($oldLegacy, $newLegacy, $src, $n);
	$changes += $n;
	echo "legacy_gate ok\n";
} elseif (strpos($src, "empty(\$DP_Content->service_data['article_search_chpu'])") !== false) {
	echo "legacy_gate already\n";
} else {
	exit("legacy_gate marker missing\n");
}

$oldMfr = <<<'OLD'
//Тип поиска
$search_type = "no_chpu";//По умолчанию тип поиск - без ЧПУ, т.е. старый вариант
if( isset($DP_Content->service_data["search_type"]) )
{
OLD;
$newMfr = <<<'NEW'
//Тип поиска
$search_type = "no_chpu";//По умолчанию тип поиск - без ЧПУ, т.е. старый вариант
$manufacturer = '';
$use_selected_manufacturer = false;
if( isset($DP_Content->service_data["search_type"]) )
{
NEW;
if (strpos($src, $oldMfr) !== false) {
	$src = str_replace($oldMfr, $newMfr, $src, $n);
	$changes += $n;
	echo "manufacturer_init ok\n";
} elseif (strpos($src, "\$manufacturer = '';") !== false && strpos($src, '$use_selected_manufacturer = false;') !== false) {
	echo "manufacturer_init already\n";
} else {
	exit("manufacturer_init marker missing\n");
}

$oldOff = <<<'OLD'
//Формирум объект описания точек выдачи и складов
require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php");//Получили $customer_offices

//var_dump($customer_offices);

$office_storage_bunches = array();//Список всех связок всех офисов обслуживания со своими складами. По этому списку будет осуществляться опрос складов
OLD;
$newOff = <<<'NEW'
//Формирум объект описания точек выдачи и складов
// require_once may already be satisfied from another scope, leaving $customer_offices unset/null.
$epc_customer_offices_path = $_SERVER["DOCUMENT_ROOT"]."/content/shop/order_process/get_customer_offices.php";
require_once $epc_customer_offices_path;
if (!isset($customer_offices) || !is_array($customer_offices)) {
	include $epc_customer_offices_path;
}
if (!isset($customer_offices) || !is_array($customer_offices)) {
	$customer_offices = array();
}

//var_dump($customer_offices);

$office_storage_bunches = array();//Список всех связок всех офисов обслуживания со своими складами. По этому списку будет осуществляться опрос складов
NEW;
if (strpos($src, $oldOff) !== false) {
	$src = str_replace($oldOff, $newOff, $src, $n);
	$changes += $n;
	echo "offices_fix ok\n";
} elseif (strpos($src, '$epc_customer_offices_path') !== false) {
	echo "offices_fix already\n";
} else {
	exit("offices_fix marker missing\n");
}

$tmp = $path . '.fixnew';
if (file_put_contents($tmp, $src) === false) {
	exit("tmp write fail\n");
}
$lint = array();
$code = 0;
exec('php -l ' . escapeshellarg($tmp) . ' 2>&1', $lint, $code);
echo implode("\n", $lint) . "\n";
if ($code !== 0) {
	@unlink($tmp);
	exit("lint fail\n");
}
if (!@rename($tmp, $path)) {
	@unlink($tmp);
	exit("rename fail\n");
}
clearstatcache(true, $path);
if (function_exists('opcache_invalidate')) {
	opcache_invalidate($path, true);
}
if (function_exists('opcache_reset')) {
	@opcache_reset();
}
echo "deployed changes=$changes md5=" . md5_file($path) . "\n";
exit;
