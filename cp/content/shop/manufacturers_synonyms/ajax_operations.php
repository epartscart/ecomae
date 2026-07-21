<?php
/**
 * CP AJAX — manufacturer synonyms CRUD.
 * Used by /cp/shop/manufacturers_synonyms (was missing → 404, page blank).
 */
ini_set('display_errors', '0');
header('Content-Type: application/json; charset=utf-8');
if (ob_get_level()) {
	ob_end_clean();
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

try {
	$dbHost = trim((string) $DP_Config->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$db_link = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$db_link->query('SET NAMES utf8mb4');
} catch (Throwable $e) {
	exit(json_encode(array('status' => false, 'message' => 'No DB connect', 'manufacturers' => array(), 'synonyms' => array())));
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	exit(json_encode(array('status' => false, 'message' => 'forbidden', 'manufacturers' => array(), 'synonyms' => array())));
}

// CSRF (shared CP guard)
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';

// Note: do not hard-fail on content_access group flags here.
// The CP route already gates the page; isAdmin()+CSRF is enough for this AJAX.
// (Strict content_access checks caused blank lists for admins who can open the page.)

/**
 * Ensure synonym tables exist (tenant DBs may lack them).
 */
function epc_mfr_syn_ensure_schema(PDO $db): void
{
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `shop_docpart_manufacturers` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`name` VARCHAR(255) NOT NULL DEFAULT '',
			PRIMARY KEY (`id`),
			UNIQUE KEY `uq_name` (`name`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
	);
	$db->exec(
		"CREATE TABLE IF NOT EXISTS `shop_docpart_manufacturers_synonyms` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
			`manufacturer_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`synonym` VARCHAR(255) NOT NULL DEFAULT '',
			PRIMARY KEY (`id`),
			KEY `idx_mfr` (`manufacturer_id`),
			KEY `idx_synonym` (`synonym`(191))
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
	);
}

function epc_mfr_syn_clean_name($name): string
{
	$name = urldecode((string) $name);
	$name = html_entity_decode($name, ENT_QUOTES | ENT_HTML5, 'UTF-8');
	$name = trim(str_replace(array("\0", "\r", "\n", "\t"), '', $name));
	// Keep brand punctuation used in real catalogues; strip control chars only.
	$name = preg_replace('/[\x00-\x1F\x7F]/u', '', $name);
	if (function_exists('mb_substr')) {
		$name = mb_substr($name, 0, 255, 'UTF-8');
	} else {
		$name = substr($name, 0, 255);
	}
	return trim((string) $name);
}

try {
	epc_mfr_syn_ensure_schema($db_link);
} catch (Throwable $e) {
	exit(json_encode(array(
		'status' => false,
		'message' => 'Schema error: ' . $e->getMessage(),
		'manufacturers' => array(),
		'synonyms' => array(),
	)));
}

$raw = (string) ($_POST['request_object'] ?? $_GET['request_object'] ?? '');
$request_object = json_decode($raw, true);
if (!is_array($request_object)) {
	// Some clients send already-decoded nested encoding
	$request_object = json_decode(urldecode($raw), true);
}
if (!is_array($request_object)) {
	exit(json_encode(array('status' => false, 'message' => 'bad_request', 'manufacturers' => array(), 'synonyms' => array())));
}

$action = (string) ($request_object['action'] ?? '');
$answer = array('status' => false, 'manufacturers' => array(), 'synonyms' => array());

try {
	switch ($action) {
		case 'get_manufacturers':
			$list = array();
			$st = $db_link->query('SELECT `id`, `name` FROM `shop_docpart_manufacturers` ORDER BY `name` ASC');
			while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
				$list[] = array(
					'id' => (int) $r['id'],
					'name' => (string) $r['name'],
				);
			}
			$answer['status'] = true;
			$answer['manufacturers'] = $list;
			break;

		case 'get_synonyms':
			$mfrId = (int) ($request_object['id'] ?? 0);
			$list = array();
			if ($mfrId > 0) {
				$st = $db_link->prepare(
					'SELECT `id`, `synonym`, `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms`
					 WHERE `manufacturer_id` = ? ORDER BY `synonym` ASC'
				);
				$st->execute(array($mfrId));
				while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
					$list[] = array(
						'id' => (int) $r['id'],
						'synonym' => (string) $r['synonym'],
						'manufacturer_id' => (int) $r['manufacturer_id'],
					);
				}
			}
			$answer['status'] = true;
			$answer['synonyms'] = $list;
			break;

		case 'add_manufacturer':
			$name = epc_mfr_syn_clean_name($request_object['name'] ?? '');
			if ($name === '') {
				$answer['message'] = 'empty_name';
				break;
			}
			$chk = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1');
			$chk->execute(array($name));
			if ($chk->fetchColumn()) {
				$answer['message'] = 'duplicate';
				break;
			}
			$ins = $db_link->prepare('INSERT INTO `shop_docpart_manufacturers` (`name`) VALUES (?)');
			$answer['status'] = $ins->execute(array($name));
			$answer['id'] = (int) $db_link->lastInsertId();
			break;

		case 'save_manufacturer':
			$id = (int) ($request_object['id'] ?? 0);
			$name = epc_mfr_syn_clean_name($request_object['name'] ?? '');
			if ($id <= 0 || $name === '') {
				$answer['message'] = 'bad_input';
				break;
			}
			$chk = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? AND `id` <> ? LIMIT 1');
			$chk->execute(array($name, $id));
			if ($chk->fetchColumn()) {
				$answer['message'] = 'duplicate';
				break;
			}
			$upd = $db_link->prepare('UPDATE `shop_docpart_manufacturers` SET `name` = ? WHERE `id` = ?');
			$answer['status'] = $upd->execute(array($name, $id));
			break;

		case 'del_manufacturer':
			$id = (int) ($request_object['id'] ?? 0);
			if ($id <= 0) {
				$answer['message'] = 'bad_id';
				break;
			}
			$db_link->prepare('DELETE FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?')->execute(array($id));
			$del = $db_link->prepare('DELETE FROM `shop_docpart_manufacturers` WHERE `id` = ?');
			$answer['status'] = $del->execute(array($id));
			break;

		case 'add_synonym':
			$mfrId = (int) ($request_object['id'] ?? 0);
			$name = epc_mfr_syn_clean_name($request_object['name'] ?? '');
			if ($mfrId <= 0 || $name === '') {
				$answer['message'] = 'bad_input';
				break;
			}
			$existsMfr = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers` WHERE `id` = ? LIMIT 1');
			$existsMfr->execute(array($mfrId));
			if (!$existsMfr->fetchColumn()) {
				$answer['message'] = 'manufacturer_missing';
				break;
			}
			// Synonym must not collide with another manufacturer name or synonym
			$chkName = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1');
			$chkName->execute(array($name));
			if ($chkName->fetchColumn()) {
				$answer['message'] = 'duplicate_manufacturer_name';
				break;
			}
			$chkSyn = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1');
			$chkSyn->execute(array($name));
			if ($chkSyn->fetchColumn()) {
				$answer['message'] = 'duplicate_synonym';
				break;
			}
			$ins = $db_link->prepare(
				'INSERT INTO `shop_docpart_manufacturers_synonyms` (`manufacturer_id`, `synonym`) VALUES (?, ?)'
			);
			$answer['status'] = $ins->execute(array($mfrId, $name));
			$answer['id'] = (int) $db_link->lastInsertId();
			break;

		case 'save_synonym':
			$id = (int) ($request_object['id'] ?? 0);
			$name = epc_mfr_syn_clean_name($request_object['name'] ?? '');
			if ($id <= 0 || $name === '') {
				$answer['message'] = 'bad_input';
				break;
			}
			$chkName = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1');
			$chkName->execute(array($name));
			if ($chkName->fetchColumn()) {
				$answer['message'] = 'duplicate_manufacturer_name';
				break;
			}
			$chkSyn = $db_link->prepare('SELECT `id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? AND `id` <> ? LIMIT 1');
			$chkSyn->execute(array($name, $id));
			if ($chkSyn->fetchColumn()) {
				$answer['message'] = 'duplicate_synonym';
				break;
			}
			$upd = $db_link->prepare('UPDATE `shop_docpart_manufacturers_synonyms` SET `synonym` = ? WHERE `id` = ?');
			$answer['status'] = $upd->execute(array($name, $id));
			break;

		case 'del_synonym':
			$id = (int) ($request_object['id'] ?? 0);
			if ($id <= 0) {
				$answer['message'] = 'bad_id';
				break;
			}
			$del = $db_link->prepare('DELETE FROM `shop_docpart_manufacturers_synonyms` WHERE `id` = ?');
			$answer['status'] = $del->execute(array($id));
			break;

		default:
			$answer['message'] = 'unknown_action';
			break;
	}
} catch (Throwable $e) {
	$answer['status'] = false;
	$answer['message'] = 'query_failed';
	$answer['error'] = $e->getMessage();
}

exit(json_encode($answer));
