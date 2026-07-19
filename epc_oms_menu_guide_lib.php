<?php
/**
 * Shared OMS menu + daily guide DB apply helpers.
 * Used by epc-oms-menu-guide-setup.php and epc-cp-common-parity-setup.php.
 */
declare(strict_types=1);

function epc_oms_setup_pdo($cfg): PDO
{
	$host = trim((string) ($cfg->host ?? '127.0.0.1'));
	if ($host === 'localhost') {
		$host = '127.0.0.1';
	}
	return new PDO(
		'mysql:host=' . $host . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_oms_register_guide_route(PDO $pdo, string $backend): array
{
	$url = 'shop/orders/oms-guide';
	$php = '/' . $backend . '/content/shop/order_process/oms_daily_guide_page.php';
	$valueKey = 'epc_oms_guide_cp';
	$titleKey = 'OMS daily guide — step by step';
	$alias = 'oms-guide';
	$now = time();

	if (function_exists('epc_cp_mm_lang')) {
		epc_cp_mm_lang($pdo, $valueKey, 'OMS daily guide', 'OMS — ежедневный гид');
	}

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('shop/orders/orders'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new RuntimeException('Parent content missing: shop/orders/orders');
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$id = (int) $st->fetchColumn();
	$changed = false;

	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ?, `time_edited` = ?, `order` = ?
			 WHERE `id` = ?'
		)->execute(array($php, $titleKey, $valueKey, $parentId, $level, $alias, $now, 81, $id));
		$changed = true;
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$url,
			$level,
			$alias,
			$valueKey,
			$parentId,
			$titleKey,
			$php,
			$titleKey,
			$now,
			$now,
			81,
		));
		$id = (int) $pdo->lastInsertId();
		$changed = true;
	}

	if ($parentId > 0 && $id > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($id));
		$g = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$g->execute(array($parentId));
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		$added = 0;
		while ($row = $g->fetch(PDO::FETCH_ASSOC)) {
			$ins->execute(array($id, (int) $row['group_id']));
			$added++;
		}
		if ($added === 0) {
			$rootG = (int) $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1')->fetchColumn();
			$ins->execute(array($id, $rootG > 0 ? $rootG : 1));
		}
	}

	return array('content_id' => $id, 'url' => $url, 'php' => $php, 'changed' => $changed);
}
