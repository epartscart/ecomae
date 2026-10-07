<?php
// Runs the PHP print_docs generators against a throwaway database to produce the golden HTML.
// Usage: php harness.php <repo_root> <dsn> <user> <password> <out_dir>
declare(strict_types=1);
date_default_timezone_set('UTC');

[$self, $root, $dsn, $dbUser, $dbPassword, $out] = $argv;
$_SERVER['DOCUMENT_ROOT'] = $root;

final class DP_User
{
	public static bool $admin = true;

	public static function getUserId(): int
	{
		return 0;
	}

	public static function getAdminId(): int
	{
		return 1;
	}

	public static function isAdmin(): bool
	{
		return self::$admin;
	}

	public static function isBackendGroup(): bool
	{
		return false;
	}
}

final class HarnessConfig
{
	public string $shop_currency = 'AED';
}

define('_ASTEXE_', 1);
define('_INTASK_', 1);

$db_link = new PDO($dsn, $dbUser, $dbPassword, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
$db_link->query('SET NAMES utf8;');
$DP_Config = new HarnessConfig();
$user_id = 0;

$steps = array(
	array('receipt_40', 'get_html_sales_receipt.php', 40),
	array('tax_40', 'get_html_uae_tax_invoice.php', 40),
	array('tax_40_saved', 'get_html_uae_tax_invoice.php', 40),
	array('tax_41', 'get_html_uae_tax_invoice.php', 41),
	array('receipt_41', 'get_html_sales_receipt.php', 41),
);
foreach ($steps as [$name, $file, $id]) {
	$order_id = $id;
	$epc_print_doc_title = 'Sales receipt';
	$HTML = '';
	require $root . '/content/shop/print_docs/service/' . $file;
	file_put_contents($out . '/' . $name . '.html', $HTML);
}
$docs = $db_link->query('SELECT `order_id`, `invoice_number` FROM `epc_einvoice_documents` ORDER BY `id`')->fetchAll(PDO::FETCH_NUM);
file_put_contents($out . '/einvoice_documents.txt', implode("\n", array_map(static fn ($r) => implode('|', $r), $docs)));
