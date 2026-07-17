<?php
/**
 * CP page ACL helpers.
 *
 * is_anable() is called once per left-menu item. Without request-scoped caches
 * that becomes hundreds of content/content_access queries per CP page load.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Normalize a CP menu/control item URL to the content.url form (no backend prefix, no query).
 */
function epc_cp_acl_content_url(array $item): string
{
	global $DP_Config;
	$url = (string) ($item['url'] ?? '');
	$url = explode('?', $url)[0];
	$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
	$url = str_replace('/' . $backend . '/', '', $url);
	$url = ltrim($url, '/');
	return $url;
}

/**
 * Preload content id + access groups for a list of menu/control items (batched).
 *
 * @param array<int,array<string,mixed>> $items
 */
function epc_cp_acl_preload(array $items): void
{
	global $db_link;
	if (!($db_link instanceof PDO) || $items === array()) {
		return;
	}
	if (!isset($GLOBALS['epc_cp_acl_content_id_by_url']) || !is_array($GLOBALS['epc_cp_acl_content_id_by_url'])) {
		$GLOBALS['epc_cp_acl_content_id_by_url'] = array();
	}
	if (!isset($GLOBALS['epc_cp_acl_groups_by_content']) || !is_array($GLOBALS['epc_cp_acl_groups_by_content'])) {
		$GLOBALS['epc_cp_acl_groups_by_content'] = array();
	}

	$urls = array();
	foreach ($items as $item) {
		if (!is_array($item)) {
			continue;
		}
		$url = epc_cp_acl_content_url($item);
		if ($url === '' || array_key_exists($url, $GLOBALS['epc_cp_acl_content_id_by_url'])) {
			continue;
		}
		$urls[$url] = true;
	}
	$urlList = array_keys($urls);
	if ($urlList === array()) {
		return;
	}

	// Chunk IN() lists to stay under packet/placeholder limits.
	foreach (array_chunk($urlList, 200) as $chunk) {
		$placeholders = implode(',', array_fill(0, count($chunk), '?'));
		$st = $db_link->prepare("SELECT `id`, `url` FROM `content` WHERE `url` IN ($placeholders)");
		$st->execute($chunk);
		$contentIds = array();
		while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
			$url = (string) $row['url'];
			$id = (int) $row['id'];
			$GLOBALS['epc_cp_acl_content_id_by_url'][$url] = $id;
			$contentIds[] = $id;
			if (!isset($GLOBALS['epc_cp_acl_groups_by_content'][$id])) {
				$GLOBALS['epc_cp_acl_groups_by_content'][$id] = array();
			}
		}
		foreach ($chunk as $url) {
			if (!array_key_exists($url, $GLOBALS['epc_cp_acl_content_id_by_url'])) {
				$GLOBALS['epc_cp_acl_content_id_by_url'][$url] = 0;
			}
		}
		if ($contentIds === array()) {
			continue;
		}
		$ph2 = implode(',', array_fill(0, count($contentIds), '?'));
		$st2 = $db_link->prepare("SELECT `content_id`, `group_id` FROM `content_access` WHERE `content_id` IN ($ph2)");
		$st2->execute($contentIds);
		while ($row = $st2->fetch(PDO::FETCH_ASSOC)) {
			$cid = (int) $row['content_id'];
			$GLOBALS['epc_cp_acl_groups_by_content'][$cid][] = (int) $row['group_id'];
		}
	}
}

/**
 * Expand allowed groups with nested children (request-cached per seed set).
 *
 * @param array<int,int> $explicitGroups
 * @return array<int,int>
 */
function epc_cp_acl_expand_groups(array $explicitGroups): array
{
	$explicitGroups = array_values(array_unique(array_map('intval', $explicitGroups)));
	sort($explicitGroups);
	$cacheKey = implode(',', $explicitGroups);
	if (!isset($GLOBALS['epc_cp_acl_expanded_groups']) || !is_array($GLOBALS['epc_cp_acl_expanded_groups'])) {
		$GLOBALS['epc_cp_acl_expanded_groups'] = array();
	}
	if (array_key_exists($cacheKey, $GLOBALS['epc_cp_acl_expanded_groups'])) {
		return $GLOBALS['epc_cp_acl_expanded_groups'][$cacheKey];
	}

	global $inserted_groups;
	global $allowed_groups;
	$inserted_groups = array();
	$allowed_groups = $explicitGroups;
	if (function_exists('getAllowedGroups')) {
		for ($i = 0; $i < count($allowed_groups); $i++) {
			getAllowedGroups($allowed_groups[$i]);
		}
		$allowed_groups = array_merge($allowed_groups, $inserted_groups);
	}
	$expanded = array_values(array_unique(array_map('intval', $allowed_groups)));
	$GLOBALS['epc_cp_acl_expanded_groups'][$cacheKey] = $expanded;
	return $expanded;
}

//Определение функции проверки доступа к странице
function is_anable($item = null)
{
	if (!$item || !is_array($item)) {
		return false;
	}

	global $db_link;
	global $DP_Config;

	$url = epc_cp_acl_content_url($item);
	if ($url === '') {
		return false;
	}

	if (!isset($GLOBALS['epc_cp_acl_result_by_url']) || !is_array($GLOBALS['epc_cp_acl_result_by_url'])) {
		$GLOBALS['epc_cp_acl_result_by_url'] = array();
	}
	if (array_key_exists($url, $GLOBALS['epc_cp_acl_result_by_url'])) {
		return $GLOBALS['epc_cp_acl_result_by_url'][$url];
	}

	// Content id (preloaded map or single lookup)
	$contentId = null;
	if (isset($GLOBALS['epc_cp_acl_content_id_by_url']) && array_key_exists($url, $GLOBALS['epc_cp_acl_content_id_by_url'])) {
		$contentId = (int) $GLOBALS['epc_cp_acl_content_id_by_url'][$url];
	} else {
		$content_query = $db_link->prepare('SELECT `id` FROM `content` WHERE `url` = ? LIMIT 1;');
		$content_query->execute(array($url));
		$row = $content_query->fetch(PDO::FETCH_ASSOC);
		$contentId = $row ? (int) $row['id'] : 0;
		if (!isset($GLOBALS['epc_cp_acl_content_id_by_url']) || !is_array($GLOBALS['epc_cp_acl_content_id_by_url'])) {
			$GLOBALS['epc_cp_acl_content_id_by_url'] = array();
		}
		$GLOBALS['epc_cp_acl_content_id_by_url'][$url] = $contentId;
	}
	if ($contentId <= 0) {
		$GLOBALS['epc_cp_acl_result_by_url'][$url] = false;
		return false;
	}

	//ПРОФИЛЬ АДМИНИСТРАТОРА:
	$user_profile = DP_User::getAdminProfile();
	if (!is_array($user_profile)) {
		$user_profile = array('groups' => array());
	}
	if (!isset($user_profile['groups']) || !is_array($user_profile['groups'])) {
		$user_profile['groups'] = array();
	}

	//СПИСОК ДОПУЩЕННЫХ ГРУПП
	if (isset($GLOBALS['epc_cp_acl_groups_by_content'][$contentId]) && is_array($GLOBALS['epc_cp_acl_groups_by_content'][$contentId])) {
		$explicit_groups = $GLOBALS['epc_cp_acl_groups_by_content'][$contentId];
	} else {
		$explicit_groups = array();
		$content_access_query = $db_link->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?;');
		$content_access_query->execute(array($contentId));
		while ($content_access_record = $content_access_query->fetch(PDO::FETCH_ASSOC)) {
			$explicit_groups[] = (int) $content_access_record['group_id'];
		}
		if (!isset($GLOBALS['epc_cp_acl_groups_by_content']) || !is_array($GLOBALS['epc_cp_acl_groups_by_content'])) {
			$GLOBALS['epc_cp_acl_groups_by_content'] = array();
		}
		$GLOBALS['epc_cp_acl_groups_by_content'][$contentId] = $explicit_groups;
	}

	$allowed_groups_expanded = epc_cp_acl_expand_groups($explicit_groups);

	//ПРОВЕРКА ДОСТУПА К МАТЕРИАЛУ
	$access_allowed = false;
	if (count($allowed_groups_expanded) === 0) {
		$access_allowed = !empty($user_profile['groups']);
	} else {
		$userGroups = array_map('intval', $user_profile['groups']);
		foreach ($userGroups as $gid) {
			if (in_array($gid, $allowed_groups_expanded, true)) {
				$access_allowed = true;
				break;
			}
		}
	}

	$GLOBALS['epc_cp_acl_result_by_url'][$url] = (bool) $access_allowed;
	return (bool) $access_allowed;
}
