<?php
// Renders PHP content/shop/payments/epc_payment_method_picker.php against a throwaway database for the golden HTML.
// Usage: php harness.php <repo_root> <dsn> <user> <password> <out_dir>
declare(strict_types=1);

[$self, $root, $dsn, $dbUser, $dbPassword, $out] = $argv;
$_SERVER['DOCUMENT_ROOT'] = $root;

final class HarnessConfig
{
	public string $domain_path = 'https://www.epartscart.com/';
}

$DP_Config = new HarnessConfig();
$DP_Lang = 'en';
$db_link = new PDO($dsn, $dbUser, $dbPassword, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db_link->query('SET NAMES utf8;');
require $root . '/lang/dp_lang.php';

ob_start();
require $root . '/content/shop/payments/epc_payment_method_picker.php';
file_put_contents($out . '/picker_full.html', ob_get_clean());

$db_link->exec('UPDATE `shop_payment_systems` SET `anable` = 0');
ob_start();
require $root . '/content/shop/payments/epc_payment_method_picker.php';
file_put_contents($out . '/picker_empty.html', ob_get_clean());
