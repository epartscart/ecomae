<?php
/**
 * CP breadcrumb helpers — resolve trail segments without "404 Not found" noise.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Human-readable label from a URL path segment (shop/procurement → Procurement).
 */
function epc_cp_breadcrumb_humanize_segment(string $nodeUrl): string
{
	$part = (string) basename(str_replace('\\', '/', $nodeUrl));
	$part = preg_replace('/[^a-z0-9_-]+/i', ' ', $part);
	$part = trim((string) preg_replace('/\s+/', ' ', $part));
	if ($part === '') {
		return '';
	}
	return ucwords(str_replace(array('-', '_'), ' ', strtolower($part)));
}

/**
 * Resolve breadcrumb caption for one backend URL node (no leading /cp/).
 */
function epc_cp_breadcrumb_caption_for_node(PDO $dbLink, string $nodeUrl, string $fallback404, bool $isLast, bool $pageIs404): string
{
	global $DP_Config;

	$nodeUrl = trim(str_replace('\\', '/', $nodeUrl), '/');
	if ($nodeUrl === '') {
		return '';
	}

	$nodeQuery = $dbLink->prepare('SELECT `url`, `value`, `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$nodeQuery->execute(array($nodeUrl));
	$nodeRecord = $nodeQuery->fetch(PDO::FETCH_ASSOC);
	if ($nodeRecord !== false) {
		return translate_str_by_id($nodeRecord['value']);
	}

	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	$itemQuery = $dbLink->prepare('SELECT `caption` FROM `control_items` WHERE `url` = ? OR `url` LIKE ? OR `url` LIKE ? LIMIT 1');
	$itemQuery->execute(array(
		'/<backend>/' . $nodeUrl,
		'%/' . $nodeUrl,
		'%/' . $nodeUrl . '?%',
	));
	$captionKey = (string) $itemQuery->fetchColumn();
	if ($captionKey !== '') {
		$label = translate_str_by_id($captionKey);
		if ($label !== '' && stripos($label, '404') === false) {
			return $label;
		}
	}

	if ($isLast && $pageIs404) {
		return $fallback404;
	}

	$human = epc_cp_breadcrumb_humanize_segment($nodeUrl);
	if ($human !== '') {
		return $human;
	}

	return $isLast && $pageIs404 ? $fallback404 : epc_cp_breadcrumb_humanize_segment($nodeUrl);
}

/**
 * Register a folder-style CP content node so breadcrumbs never 404 on intermediate paths.
 *
 * @return int content id
 */
function epc_cp_breadcrumb_ensure_folder_content(PDO $pdo, string $parentUrl, string $url, string $alias, string $valueKey, string $title, int $order = 50): int
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new Exception('Parent content not found: ' . $parentUrl);
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$now = time();

	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')
		->execute(array($valueKey, $title));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
		->execute(array($valueKey, 'en', $title));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')
		->execute(array($valueKey, 'ru', $title));

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = \'\', `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($title, $parentId, $level, $alias, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', \'\', ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array($url, $level, $alias, $valueKey, $parentId, $title, $title, $now, $now, $order));
		$contentId = (int) $pdo->lastInsertId();
	}

	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = (int) $st->fetchColumn();
	$groups = array($root > 0 ? $root : 1);
	$collect = function ($pid) use ($pdo, &$collect, &$groups) {
		$ch = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($pid));
		while ($r = $ch->fetch(PDO::FETCH_ASSOC)) {
			$groups[] = (int) $r['id'];
			$collect((int) $r['id']);
		}
	};
	if ($root > 0) {
		$collect($root);
	}
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach (array_unique($groups) as $gid) {
		$ins->execute(array($contentId, (int) $gid));
	}
	return $contentId;
}

/**
 * Ensure intermediate folder nodes exist for mainstream CP modules (fixes breadcrumb 404).
 *
 * @return array<string,int>
 */
function epc_cp_breadcrumb_repair_intermediate_folders(PDO $pdo): array
{
	$folders = array(
		array('shop', 'shop/procurement', 'procurement_folder', 'epc_cp_group_procurement', 'Procurement', 87),
		array('shop', 'shop/marketing', 'marketing_folder', 'epc_cp_group_marketing', 'Marketing', 89),
		array('shop', 'shop/payments', 'payments_folder', 'epc_cp_group_payments', 'Payment gateways', 88),
		array('shop', 'shop/channels', 'channels_folder', 'epc_cp_group_channels', 'Channels', 84),
	);
	$out = array();
	foreach ($folders as $row) {
		list($parentUrl, $url, $alias, $valueKey, $title, $order) = $row;
		try {
			$out[$url] = epc_cp_breadcrumb_ensure_folder_content($pdo, $parentUrl, $url, $alias, $valueKey, $title, (int) $order);
		} catch (Exception $e) {
			$out[$url] = 0;
		}
	}
	return $out;
}
