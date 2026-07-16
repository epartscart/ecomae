<?php
/**
 * Lightweight guide snapshot (PHP 5.6+). Does not load docpart_price_upload_history.php.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

function epc_guide_snapshot(PDO $db, $config)
{
	$byLoadMode = array();
	$priceLists = array();
	$q = $db->query(
		'SELECT p.`id`, p.`name`, p.`load_mode`, p.`last_updated`,
		 (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `records_count`
		 FROM `shop_docpart_prices` p ORDER BY p.`name`'
	);
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$lm = (int)$row['load_mode'];
		if (!isset($byLoadMode[$lm])) {
			$byLoadMode[$lm] = array('count' => 0, 'records' => 0, 'lists' => array());
		}
		$byLoadMode[$lm]['count']++;
		$byLoadMode[$lm]['records'] += (int)$row['records_count'];
		$byLoadMode[$lm]['lists'][] = array(
			'id' => (int)$row['id'],
			'name' => (string)$row['name'],
			'last_updated' => (string)$row['last_updated'],
			'records_count' => (int)$row['records_count'],
		);
		$priceLists[] = $row;
	}

	$historyBySource = array();
	try {
		$h = $db->query(
			'SELECT `upload_source`, COUNT(*) AS `cnt`, MAX(`created_at`) AS `last_at`
			 FROM `epc_price_upload_history` GROUP BY `upload_source`'
		);
		while ($row = $h->fetch(PDO::FETCH_ASSOC)) {
			$historyBySource[(string)$row['upload_source']] = array(
				'uploads' => (int)$row['cnt'],
				'last_at' => (string)$row['last_at'],
			);
		}
	} catch (Exception $e) {
		$historyBySource = array();
	}

	$cronTasks = 0;
	$cronPriceLinks = 0;
	try {
		$cronTasks = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab`')->fetchColumn();
		$cronPriceLinks = (int)$db->query('SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices`')->fetchColumn();
	} catch (Exception $e) {
		$cronTasks = -1;
	}

	$pendingTasks = 0;
	try {
		$pendingTasks = (int)$db->query(
			"SELECT COUNT(*) FROM `shop_docpart_pyprices_tasks`
			 WHERE `status` IS NULL OR `status` = '' OR `status` NOT IN ('done','completed','error','failed')"
		)->fetchColumn();
	} catch (Exception $e) {
		$pendingTasks = -1;
	}

	return array(
		'generated_at' => date('Y-m-d H:i:s'),
		'load_modes' => array(1 => 'Manual', 2 => 'FTP', 3 => 'E-mail', 4 => 'URL'),
		'by_load_mode' => $byLoadMode,
		'price_lists_total' => count($priceLists),
		'history_by_source' => $historyBySource,
		'cron_tasks' => $cronTasks,
		'cron_price_links' => $cronPriceLinks,
		'pyprices_pending_tasks' => $pendingTasks,
		'channels' => epc_guide_channel_definitions($config),
	);
}

function epc_guide_channel_definitions($config)
{
	$backend = isset($config['backend_dir']) ? $config['backend_dir'] : 'cp';
	$domain = isset($config['domain_path']) ? $config['domain_path'] : '';
	$techKey = isset($config['tech_key']) ? $config['tech_key'] : '';

	return array(
		array('title' => 'CP upload wizard', 'formats' => 'CSV, TXT, ZIP, Excel', 'test' => 'Green Upload button on price row'),
		array('title' => 'Pyprices — file from PC', 'formats' => 'CSV, XLSX per pyprices', 'test' => 'File input on manager row'),
		array('title' => 'FTP', 'formats' => 'File on FTP (file_name_substring)', 'test' => 'Manual FTP icon'),
		array('title' => 'E-mail', 'formats' => 'IMAP attachment per list rules', 'test' => 'Manual E-mail icon; one list per file name'),
		array('title' => 'URL / link', 'formats' => 'Direct file URL', 'test' => 'Manual link icon'),
		array('title' => 'Cron schedule', 'formats' => 'FTP/email/URL per schedule', 'test' => 'wget cron every minute'),
		array('title' => 'Deploy API', 'formats' => 'epc-upload-uae-prices.php', 'test' => 'POST price_file + tech_key'),
		array(
			'title' => 'Commerce data (sales / purchase / inventory)',
			'formats' => 'Excel/CSV → *-S / *.P / *-L warehouse lists',
			'test' => 'CP /shop/prices/commerce or POST /epc-upload-commerce-prices.php',
		),
	);
}
