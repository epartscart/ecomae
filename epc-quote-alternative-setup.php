<?php
/**
 * Ensure shop_quote_items has alternative-offer columns.
 * Run: /epc-quote-alternative-setup.php?token=...
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
try {
	$db = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
		$DP_Config->user,
		$DP_Config->password
	);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$db->query('SET NAMES utf8');
} catch (Throwable $e) {
	exit("DB connect failed\n");
}

$cols = $db->query('SHOW COLUMNS FROM `shop_quote_items`')->fetchAll(PDO::FETCH_COLUMN);
$want = array(
	'offer_alternative' => "TINYINT(1) NOT NULL DEFAULT 0 COMMENT '1 = staff offered alternative part'",
	'alt_manufacturer' => "VARCHAR(128) DEFAULT NULL",
	'alt_article' => "VARCHAR(128) DEFAULT NULL",
	'alt_article_show' => "VARCHAR(128) DEFAULT NULL",
	'alt_name' => "VARCHAR(512) DEFAULT NULL",
	'alt_count_need' => "INT(11) DEFAULT NULL",
	'alt_quoted_price' => "DECIMAL(12,2) DEFAULT NULL",
	'alt_storage_id' => "INT(11) DEFAULT NULL COMMENT 'Supplier warehouse for order process'",
);

foreach ($want as $name => $ddl) {
	if (in_array($name, $cols, true)) {
		echo "ok exists {$name}\n";
		continue;
	}
	$db->exec("ALTER TABLE `shop_quote_items` ADD COLUMN `{$name}` {$ddl}");
	echo "added {$name}\n";
}

echo "Done.\n";
