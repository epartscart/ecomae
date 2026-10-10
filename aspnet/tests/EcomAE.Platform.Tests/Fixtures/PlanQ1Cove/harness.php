<?php
// PHP 8.3 goldens for plan Q1-cove (social publish). HTTP stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function cove_freeze($value)
{
	if (is_string($value)) {
		$value = preg_replace('/ecomae_cpw_[a-f0-9]+/', 'TENANT_DB', $value);
		return $value;
	}
	if (is_array($value)) {
		$out = array();
		foreach ($value as $k => $v) {
			$out[$k] = cove_freeze($v);
		}
		return $out;
	}
	return $value;
}

function cove_http_route($method, $url, $body)
{
	$method = strtoupper((string) $method);
	if (strpos($url, 'graph.facebook.com') !== false && $method === 'GET' && strpos($url, '/me?') !== false) {
		return array(200, json_encode(array('id' => 'me1', 'name' => 'Me Page')));
	}
	if (strpos($url, 'graph.facebook.com') !== false && $method === 'GET' && strpos($url, 'fields=id,name') !== false) {
		return array(200, json_encode(array('id' => 'pg1', 'name' => 'Shop Page')));
	}
	if (strpos($url, 'graph.facebook.com') !== false && $method === 'GET' && strpos($url, 'fields=id,username') !== false) {
		return array(200, json_encode(array('id' => 'ig1', 'username' => 'parts.ae')));
	}
	if (strpos($url, 'graph.facebook.com') !== false && $method === 'GET' && strpos($url, 'fields=status_code') !== false) {
		return array(200, json_encode(array('status_code' => 'FINISHED')));
	}
	if (strpos($url, '/photos') !== false) {
		return array(200, json_encode(array('id' => 'fb-photo-1')));
	}
	if (strpos($url, '/videos') !== false) {
		return array(200, json_encode(array('id' => 'fb-vid-1')));
	}
	if (strpos($url, '/feed') !== false) {
		return array(200, json_encode(array('id' => 'fb-feed-1')));
	}
	if (strpos($url, '/media_publish') !== false) {
		return array(200, json_encode(array('id' => 'ig-media-1')));
	}
	if (strpos($url, '/media') !== false) {
		return array(200, json_encode(array('id' => 'ig-box-1')));
	}
	if (strpos($url, 'creator_info/query') !== false) {
		return array(200, json_encode(array('data' => array(
			'creator_username' => 'ttuser',
			'privacy_level_options' => array('SELF_ONLY', 'PUBLIC_TO_EVERYONE'),
		))));
	}
	if (strpos($url, 'video/init') !== false) {
		return array(200, json_encode(array('data' => array('publish_id' => 'tt-pub-1'))));
	}
	if (strpos($url, 'status/fetch') !== false) {
		return array(200, json_encode(array('data' => array('status' => 'PUBLISH_COMPLETE'))));
	}
	if (strpos($url, 'fail.example') !== false) {
		return array(400, json_encode(array('error' => array('message' => 'bad token'))));
	}
	if (strpos($url, 'errno.example') !== false) {
		return array(0, false, 'Failed to connect');
	}
	return array(404, json_encode(array('message' => 'no route')));
}

function cove_run_pure()
{
	$GLOBALS['DP_Config'] = (object) array();
	$def = epc_social_graph_version();
	$GLOBALS['DP_Config']->epc_meta_graph_version = ' v22.0-beta! ';
	$custom = epc_social_graph_version();
	$GLOBALS['DP_Config']->epc_meta_graph_version = '!!!';
	$fallback = epc_social_graph_version();
	$caps = array(
		epc_social_compose_caption('Hello', ''),
		epc_social_compose_caption('', '#parts'),
		epc_social_compose_caption('Hello', '#parts'),
		epc_social_compose_caption('  Hi  ', '  #x  '),
	);
	$urls = array(
		epc_social_is_video_url('https://cdn.example/a.MP4?x=1'),
		epc_social_is_video_url('https://cdn.example/a.jpg'),
		epc_social_is_image_url('https://cdn.example/a.PNG'),
		epc_social_is_image_url('https://cdn.example/a.mp4'),
		epc_social_is_video_url('not a url'),
	);
	$ok = epc_social_http_json('GET', 'https://graph.facebook.com/v21.0/me?fields=id,name&access_token=t');
	$bad = epc_social_http_json('GET', 'https://fail.example/x');
	$net = epc_social_http_json('GET', 'https://errno.example/x');
	$form = epc_social_http_json('POST', 'https://graph.facebook.com/v21.0/pg/feed', array(), array('message' => 'Hi & you', 'access_token' => 't'), 45, array('form' => true));
	$emptyArr = epc_social_http_json('POST', 'https://open.tiktokapis.com/v2/post/publish/creator_info/query/', array('Authorization: Bearer t'), array());
	return cove_freeze(array($def, $custom, $fallback, $caps, $urls, $ok, $bad, $net, $form, $emptyArr, $GLOBALS['COVE_LAST']));
}

function cove_seed_account(PDO $pdo, $siteKey, $platform, array $data)
{
	epc_social_ensure_schema($pdo);
	return epc_social_save_account($pdo, $siteKey, array_merge(array('platform' => $platform), $data));
}

function cove_acct_snap(PDO $pdo, $siteKey, $platform)
{
	$st = $pdo->prepare('SELECT `status`,`last_test_ok`,`last_test_at` FROM `epc_social_accounts` WHERE `site_key`=? AND `platform`=?');
	$st->execute(array($siteKey, $platform));
	$row = $st->fetch(PDO::FETCH_ASSOC) ?: array();
	unset($row['last_test_at']);
	return $row;
}

function cove_run_accounts(PDO $pdo)
{
	$GLOBALS['DP_Config'] = (object) array();
	epc_social_ensure_schema($pdo);
	$miss = epc_social_account_credentials($pdo, 'alpha', 'facebook');
	$metaMiss = epc_social_account_public_meta($pdo, 'alpha', 'facebook');
	cove_seed_account($pdo, 'alpha', 'facebook', array('access_token' => 'tok', 'page_id' => 'pg1', 'username' => 'page'));
	cove_seed_account($pdo, 'alpha', 'instagram', array('access_token' => 'igtok', 'page_id' => 'ig1', 'username' => 'ig'));
	cove_seed_account($pdo, 'alpha', 'tiktok', array('access_token' => 'tttok', 'username' => 'tt'));
	cove_seed_account($pdo, 'alpha', 'linkedin', array('access_token' => 'li', 'username' => 'li'));
	cove_seed_account($pdo, 'beta', 'facebook', array('access_token' => 'other', 'page_id' => 'pgX'));
	$cred = epc_social_account_credentials($pdo, 'alpha', 'Facebook!');
	unset($cred['access_token'], $cred['api_key'], $cred['api_secret']);
	$meta = epc_social_account_public_meta($pdo, 'alpha', 'facebook');
	$cross = epc_social_account_credentials($pdo, 'alpha', 'facebook');
	$other = epc_social_account_public_meta($pdo, 'beta', 'facebook');
	$fb = epc_social_test_account_live($pdo, 'alpha', 'facebook');
	$ig = epc_social_test_account_live($pdo, 'alpha', 'instagram');
	$tt = epc_social_test_account_live($pdo, 'alpha', 'tiktok');
	$li = epc_social_test_account_live($pdo, 'alpha', 'linkedin');
	$unk = epc_social_test_account_live($pdo, 'alpha', 'nope');
	$none = epc_social_test_account_live($pdo, 'gamma', 'facebook');
	$noTok = epc_social_meta_test_facebook(array('access_token' => '', 'page_id' => 'x'));
	return cove_freeze(array(
		$miss, $metaMiss, $cred, $meta, $other['has_token'],
		$fb, $ig, $tt, $li, $unk, $none, $noTok,
		cove_acct_snap($pdo, 'alpha', 'facebook'),
		cove_acct_snap($pdo, 'alpha', 'linkedin'),
	));
}

function cove_run_publish(PDO $pdo)
{
	$GLOBALS['DP_Config'] = (object) array();
	cove_seed_account($pdo, 'alpha', 'facebook', array('access_token' => 'tok', 'page_id' => 'pg1'));
	cove_seed_account($pdo, 'alpha', 'instagram', array('access_token' => 'igtok', 'ig_user_id' => 'ig1'));
	cove_seed_account($pdo, 'alpha', 'tiktok', array('access_token' => 'tttok', 'privacy_level' => 'SELF_ONLY'));
	epc_social_save_draft($pdo, 'alpha', array('platform' => 'facebook', 'title' => 'F1', 'caption' => 'Hi', 'hashtags' => '#ae', 'media_url' => 'https://cdn.example/a.jpg'));
	epc_social_save_draft($pdo, 'alpha', array('platform' => 'facebook', 'title' => 'F2', 'caption' => 'Vid', 'media_url' => 'https://cdn.example/a.mp4'));
	epc_social_save_draft($pdo, 'alpha', array('platform' => 'facebook', 'title' => 'F3', 'caption' => 'Text only'));
	epc_social_save_draft($pdo, 'alpha', array('platform' => 'instagram', 'title' => 'I1', 'caption' => 'IG', 'media_url' => 'https://cdn.example/a.jpg'));
	epc_social_save_draft($pdo, 'alpha', array('platform' => 'tiktok', 'title' => 'T1', 'caption' => 'TT', 'media_url' => 'https://cdn.example/a.mp4'));
	epc_social_save_draft($pdo, 'alpha', array('platform' => 'linkedin', 'title' => 'L1', 'caption' => 'LI'));
	$ids = $pdo->query('SELECT `id`,`title` FROM `epc_social_post_drafts` ORDER BY `id`')->fetchAll(PDO::FETCH_KEY_PAIR);
	$photo = epc_social_publish_draft($pdo, 'alpha', (int) array_search('F1', $ids, true));
	$video = epc_social_publish_draft($pdo, 'alpha', (int) array_search('F2', $ids, true));
	$feed = epc_social_publish_draft($pdo, 'alpha', (int) array_search('F3', $ids, true));
	$ig = epc_social_publish_draft($pdo, 'alpha', (int) array_search('I1', $ids, true));
	$tt = epc_social_publish_draft($pdo, 'alpha', (int) array_search('T1', $ids, true));
	$li = epc_social_publish_draft($pdo, 'alpha', (int) array_search('L1', $ids, true));
	$miss = epc_social_publish_draft($pdo, 'alpha', 99);
	$cross = epc_social_publish_draft($pdo, 'beta', (int) array_search('F1', $ids, true));
	$needPage = epc_social_meta_publish_facebook($pdo, 'beta', 'x', '');
	$needIg = epc_social_meta_publish_instagram($pdo, 'alpha', 'x', '');
	$needTt = epc_social_tiktok_publish_video($pdo, 'alpha', 'x', 'https://cdn.example/a.jpg');
	$rows = $pdo->query('SELECT `title`,`status`,`external_post_id`,`last_error` FROM `epc_social_post_drafts` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	return cove_freeze(array($photo, $video, $feed, $ig, $tt, $li, $miss, $cross, $needPage, $needIg, $needTt, $rows));
}

function cove_run_now(PDO $pdo)
{
	$GLOBALS['DP_Config'] = (object) array();
	cove_seed_account($pdo, 'alpha', 'facebook', array('access_token' => 'tok', 'page_id' => 'pg1'));
	$bad = epc_social_publish_now($pdo, 'alpha', array('platform' => '', 'caption' => 'x'));
	$empty = epc_social_publish_now($pdo, 'alpha', array('platform' => 'facebook'));
	$ok = epc_social_publish_now($pdo, 'alpha', array(
		'platform' => 'facebook',
		'title' => '',
		'caption' => 'Now post',
		'media_url' => 'https://cdn.example/a.jpg',
	));
	$rows = $pdo->query('SELECT `title`,`status`,`caption` FROM `epc_social_post_drafts` ORDER BY `id`')->fetchAll(PDO::FETCH_ASSOC);
	return cove_freeze(array($bad, $empty, $ok, $rows));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$cleanup = function () use ($admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
	};
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!defined('CURLOPT_RETURNTRANSFER')) { define('CURLOPT_RETURNTRANSFER', 19913); }
	if (!defined('CURLOPT_CUSTOMREQUEST')) { define('CURLOPT_CUSTOMREQUEST', 10036); }
	if (!defined('CURLOPT_HTTPHEADER')) { define('CURLOPT_HTTPHEADER', 10023); }
	if (!defined('CURLOPT_POSTFIELDS')) { define('CURLOPT_POSTFIELDS', 10015); }
	if (!defined('CURLOPT_TIMEOUT')) { define('CURLOPT_TIMEOUT', 13); }
	if (!defined('CURLOPT_SSL_VERIFYPEER')) { define('CURLOPT_SSL_VERIFYPEER', 64); }
	if (!defined('CURLOPT_FOLLOWLOCATION')) { define('CURLOPT_FOLLOWLOCATION', 52); }
	if (!defined('CURLINFO_HTTP_CODE')) { define('CURLINFO_HTTP_CODE', 2097154); }
	if (!function_exists('curl_init')) {
		function curl_init($url = null)
		{
			$GLOBALS['COVE_CURL'] = array('url' => $url, 'opts' => array(), 'err' => '', 'errno' => 0, 'http' => 0);
			return 'cove-curl';
		}
		function curl_setopt_array($ch, $opts)
		{
			$GLOBALS['COVE_CURL']['opts'] = $opts + ($GLOBALS['COVE_CURL']['opts'] ?? array());
			return true;
		}
		function curl_exec($ch)
		{
			$url = (string) ($GLOBALS['COVE_CURL']['url'] ?? '');
			$opts = $GLOBALS['COVE_CURL']['opts'] ?? array();
			$method = (string) ($opts[10036] ?? $opts['CURLOPT_CUSTOMREQUEST'] ?? 'GET');
			$body = $opts[10015] ?? $opts['CURLOPT_POSTFIELDS'] ?? null;
			$GLOBALS['COVE_LAST'] = array('method' => $method, 'url' => $url, 'body' => $body);
			$hit = cove_http_route($method, $url, $body);
			if (isset($hit[2])) {
				$GLOBALS['COVE_CURL']['errno'] = 7;
				$GLOBALS['COVE_CURL']['err'] = $hit[2];
				$GLOBALS['COVE_CURL']['http'] = 0;
				return false;
			}
			$GLOBALS['COVE_CURL']['http'] = (int) $hit[0];
			return $hit[1];
		}
		function curl_errno($ch)
		{
			return (int) ($GLOBALS['COVE_CURL']['errno'] ?? 0);
		}
		function curl_error($ch)
		{
			return (string) ($GLOBALS['COVE_CURL']['err'] ?? '');
		}
		function curl_getinfo($ch, $opt = 0)
		{
			if ((int) $opt === 2097154) {
				return (int) ($GLOBALS['COVE_CURL']['http'] ?? 0);
			}
			return $GLOBALS['COVE_CURL']['http'] ?? 0;
		}
		function curl_close($ch)
		{
		}
	}
	$stub = sys_get_temp_dir() . '/cove_doc_' . substr(md5(uniqid('', true)), 0, 8);
	mkdir($stub . '/content/general_pages', 0777, true);
	file_put_contents($stub . '/content/general_pages/epc_portal.php', "<?php\n");
	file_put_contents($stub . '/content/general_pages/epc_integrations_helpers.php', "<?php\n");
	$_SERVER['DOCUMENT_ROOT'] = $stub;
	require $root . '/content/social_media/epc_social_publish.php';
	$_GET = array();
	$GLOBALS['db_link'] = $pdo;
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1cove_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1cove_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
