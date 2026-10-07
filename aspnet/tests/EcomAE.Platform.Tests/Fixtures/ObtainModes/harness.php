<?php
// Renders the PHP content/shop/obtaining_modes includes against a throwaway database for the golden HTML.
// One include per process (they require_once show_office_info.php).
// Usage: php harness.php <repo_root> <dsn> <user> <password> <out_dir> <case>
declare(strict_types=1);

[$self, $root, $dsn, $dbUser, $dbPassword, $out, $case] = $argv;
$_SERVER['DOCUMENT_ROOT'] = $root;
define('_ASTEXE_', 1);

final class HarnessConfig
{
	public string $domain_path = 'https://www.epartscart.com/';
	public string $backend_dir = 'cp';
}

$DP_Config = new HarnessConfig();
$GLOBALS['DP_Config'] = $DP_Config;
$DP_Lang = 'en';
$multilang_params = array('lang' => 'en', 'lang_href' => '/en');
$db_link = new PDO($dsn, $dbUser, $dbPassword, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db_link->query('SET NAMES utf8;');
require $root . '/lang/dp_lang.php';

$cases = array(
	'office_details' => array('get_in_office/show_details.php', array('mode' => 1, 'office_id' => 1)),
	'office_missing' => array('get_in_office/show_details.php', array('mode' => 1, 'office_id' => 99)),
	'office_manager' => array('get_in_office/manager_interface.php', array('mode' => 1, 'office_id' => '1')),
	'carriers_details' => array('epc_carriers/show_details.php', array('mode' => 2, 'carrier' => 'aramex', 'service' => 'PPX', 'city' => 'Dubai & Co', 'country' => 'AE', 'address' => '<b>Warehouse</b> 7', 'phone' => '+971 50', 'weight_kg' => 2.5)),
	'carriers_details_price' => array('epc_carriers/show_details.php', array('mode' => 2, 'carrier' => 'zzz', 'service' => 'S1', 'delivery_price' => '12.5', 'rate' => 99, 'country' => 'SA', 'weight_kg' => '3')),
	'carriers_details_rate' => array('epc_carriers/show_details.php', array('mode' => 2, 'carrier' => 'DHL', 'service' => 'EXPRESS', 'rate' => 1234.567, 'weight_kg' => 1.0)),
	'carriers_details_demo' => array('epc_carriers/show_details.php', array('mode' => 2, 'country' => 'sa')),
	'carriers_manager_form' => array('epc_carriers/manager_interface.php', array('mode' => 2, 'carrier' => 'fedex', 'city' => 'Sharjah', 'country' => 'AE', 'address' => 'A "1"', 'phone' => '06', 'weight_kg' => 4)),
	'carriers_manager_shipments' => array('epc_carriers/manager_interface.php', array('mode' => 2, 'carrier' => 'nope')),
);
[$file, $how_get_json] = $cases[$case];
$obtain_mode = array('caption' => $how_get_json['mode'] === 1 ? 'om_office' : 'om_carriers');
$obtain_caption = $obtain_mode['caption'];
$order_record = array('id' => $case === 'carriers_manager_shipments' ? 41 : 40);

ob_start();
require $root . '/content/shop/obtaining_modes/' . $file;
file_put_contents($out . '/' . $case . '.html', ob_get_clean());
