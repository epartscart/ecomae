<?php
/**
 * Social Media Hub — live publish via Meta Graph (Facebook / Instagram)
 * and TikTok Content Posting API (Direct Post / PULL_FROM_URL).
 */
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_social_media_helpers.php';

function epc_social_graph_version(): string
{
	$v = '';
	if (isset($GLOBALS['DP_Config']) && is_object($GLOBALS['DP_Config'])) {
		$v = trim((string) ($GLOBALS['DP_Config']->epc_meta_graph_version ?? ''));
	}
	if ($v === '') {
		$v = 'v21.0';
	}
	return preg_replace('/[^a-zA-Z0-9.]/', '', $v) ?: 'v21.0';
}

/**
 * @param array|object|string|null $body
 * @param array{form?:bool} $options  form=true → application/x-www-form-urlencoded (Meta Graph photos/feed)
 * @return array{ok:bool,http:int,json:array,raw:string,error:string}
 */
function epc_social_http_json(string $method, string $url, array $headers = array(), $body = null, int $timeout = 45, array $options = array()): array
{
	$ch = curl_init($url);
	$hdr = array_merge(array('Accept: application/json'), $headers);
	$opts = array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_CUSTOMREQUEST => strtoupper($method),
		CURLOPT_HTTPHEADER => $hdr,
		CURLOPT_TIMEOUT => $timeout,
		CURLOPT_SSL_VERIFYPEER => true,
		CURLOPT_FOLLOWLOCATION => true,
	);
	if ($body !== null) {
		$asForm = !empty($options['form']);
		if ($asForm && is_array($body)) {
			$opts[CURLOPT_POSTFIELDS] = http_build_query($body);
			$hdr[] = 'Content-Type: application/x-www-form-urlencoded';
			$opts[CURLOPT_HTTPHEADER] = $hdr;
		} elseif (is_array($body) || is_object($body)) {
			$opts[CURLOPT_POSTFIELDS] = json_encode($body, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
			$hasCt = false;
			foreach ($hdr as $h) {
				if (stripos($h, 'Content-Type:') === 0) {
					$hasCt = true;
					break;
				}
			}
			if (!$hasCt) {
				$hdr[] = 'Content-Type: application/json';
			}
			$opts[CURLOPT_HTTPHEADER] = $hdr;
		} else {
			$opts[CURLOPT_POSTFIELDS] = (string) $body;
		}
	}
	curl_setopt_array($ch, $opts);
	$raw = (string) curl_exec($ch);
	$errno = curl_errno($ch);
	$err = curl_error($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	curl_close($ch);

	if ($errno) {
		return array('ok' => false, 'http' => $code, 'json' => array(), 'raw' => '', 'error' => $err !== '' ? $err : 'curl error');
	}
	$json = json_decode($raw, true);
	if (!is_array($json)) {
		$json = array();
	}
	$apiErr = '';
	if (!empty($json['error']['message'])) {
		$apiErr = (string) $json['error']['message'];
	} elseif (!empty($json['error']['error_description'])) {
		$apiErr = (string) $json['error']['error_description'];
	} elseif (!empty($json['message']) && $code >= 400) {
		$apiErr = (string) $json['message'];
	}
	$ok = ($code >= 200 && $code < 300 && $apiErr === '');
	return array(
		'ok' => $ok,
		'http' => $code,
		'json' => $json,
		'raw' => substr($raw, 0, 2000),
		'error' => $ok ? '' : ($apiErr !== '' ? $apiErr : ('HTTP ' . $code)),
	);
}

/**
 * @return array<string, string>
 */
function epc_social_account_credentials(PDO $pdo, string $siteKey, string $platform): array
{
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower($platform));
	$st = $pdo->prepare(
		'SELECT `username`, `encrypted_credentials` FROM `epc_social_accounts`
		 WHERE `site_key` = ? AND `platform` = ? LIMIT 1'
	);
	$st->execute(array($siteKey, $platform));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array();
	}
	$cred = json_decode(epc_social_decrypt((string) ($row['encrypted_credentials'] ?? ''), $siteKey), true);
	if (!is_array($cred)) {
		$cred = array();
	}
	$cred['username'] = (string) ($row['username'] ?? '');
	return array(
		'username' => (string) ($cred['username'] ?? ''),
		'access_token' => trim((string) ($cred['access_token'] ?? '')),
		'api_key' => trim((string) ($cred['api_key'] ?? '')),
		'api_secret' => trim((string) ($cred['api_secret'] ?? '')),
		'page_id' => trim((string) ($cred['page_id'] ?? '')),
		'ig_user_id' => trim((string) ($cred['ig_user_id'] ?? '')),
		'open_id' => trim((string) ($cred['open_id'] ?? '')),
		'privacy_level' => trim((string) ($cred['privacy_level'] ?? 'SELF_ONLY')),
	);
}

/**
 * Non-secret fields for form prefill (never returns tokens).
 *
 * @return array{page_id:string,ig_user_id:string,open_id:string,privacy_level:string,has_token:bool}
 */
function epc_social_account_public_meta(PDO $pdo, string $siteKey, string $platform): array
{
	$c = epc_social_account_credentials($pdo, $siteKey, $platform);
	return array(
		'page_id' => (string) ($c['page_id'] ?? ''),
		'ig_user_id' => (string) ($c['ig_user_id'] ?? ''),
		'open_id' => (string) ($c['open_id'] ?? ''),
		'privacy_level' => (string) ($c['privacy_level'] ?? 'SELF_ONLY'),
		'has_token' => trim((string) ($c['access_token'] ?? '')) !== '' || trim((string) ($c['api_key'] ?? '')) !== '',
	);
}

function epc_social_mark_account_test(PDO $pdo, string $siteKey, string $platform, bool $ok, string $message = ''): void
{
	$now = time();
	$meta = json_encode(array('last_test_message' => $message, 'at' => $now), JSON_UNESCAPED_UNICODE);
	$pdo->prepare(
		'UPDATE `epc_social_accounts`
		 SET `last_test_at` = ?, `last_test_ok` = ?, `status` = ?, `meta_json` = ?, `updated_at` = ?
		 WHERE `site_key` = ? AND `platform` = ?'
	)->execute(array($now, $ok ? 1 : 0, $ok ? 'verified' : 'error', $meta, $now, $siteKey, $platform));
}

function epc_social_compose_caption(string $caption, string $hashtags): string
{
	$caption = trim($caption);
	$hashtags = trim($hashtags);
	if ($hashtags === '') {
		return $caption;
	}
	if ($caption === '') {
		return $hashtags;
	}
	return $caption . "\n\n" . $hashtags;
}

function epc_social_is_video_url(string $url): bool
{
	$path = strtolower((string) parse_url($url, PHP_URL_PATH));
	foreach (array('.mp4', '.mov', '.m4v', '.webm') as $ext) {
		if (substr($path, -strlen($ext)) === $ext) {
			return true;
		}
	}
	return false;
}

function epc_social_is_image_url(string $url): bool
{
	$path = strtolower((string) parse_url($url, PHP_URL_PATH));
	foreach (array('.jpg', '.jpeg', '.png', '.gif', '.webp') as $ext) {
		if (substr($path, -strlen($ext)) === $ext) {
			return true;
		}
	}
	return false;
}

/**
 * Live credential test — Meta Graph or TikTok creator_info.
 *
 * @return array{ok:bool,message:string}
 */
function epc_social_test_account_live(PDO $pdo, string $siteKey, string $platform): array
{
	epc_social_ensure_schema($pdo);
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower($platform));
	$cred = epc_social_account_credentials($pdo, $siteKey, $platform);
	if ($cred === array() || ((string) ($cred['access_token'] ?? '') === '' && (string) ($cred['api_key'] ?? '') === '')) {
		epc_social_mark_account_test($pdo, $siteKey, $platform, false, 'No credentials');
		return array('ok' => false, 'message' => 'No account credentials for ' . $platform . '. Save an access token first.');
	}

	if ($platform === 'facebook') {
		$result = epc_social_meta_test_facebook($cred);
	} elseif ($platform === 'instagram') {
		$result = epc_social_meta_test_instagram($cred);
	} elseif ($platform === 'tiktok') {
		$result = epc_social_tiktok_test($cred);
	} elseif ($platform === 'linkedin' || $platform === 'x') {
		epc_social_mark_account_test($pdo, $siteKey, $platform, false, 'Publish not supported');
		return array(
			'ok' => false,
			'message' => ucfirst($platform) . ' live publish is not enabled yet. Vault storage works — use Copy → native app for now.',
		);
	} else {
		return array('ok' => false, 'message' => 'Unknown platform.');
	}

	epc_social_mark_account_test($pdo, $siteKey, $platform, !empty($result['ok']), (string) ($result['message'] ?? ''));
	return $result;
}

/**
 * @param array<string, string> $cred
 * @return array{ok:bool,message:string}
 */
function epc_social_meta_test_facebook(array $cred): array
{
	$token = (string) ($cred['access_token'] ?? '');
	$pageId = (string) ($cred['page_id'] ?? '');
	if ($token === '') {
		return array('ok' => false, 'message' => 'Facebook needs a Page access token.');
	}
	$v = epc_social_graph_version();
	if ($pageId !== '') {
		$url = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($pageId)
			. '?fields=id,name&access_token=' . rawurlencode($token);
	} else {
		$url = 'https://graph.facebook.com/' . rawurlencode($v) . '/me?fields=id,name&access_token=' . rawurlencode($token);
	}
	$res = epc_social_http_json('GET', $url);
	if (!$res['ok'] || empty($res['json']['id'])) {
		return array('ok' => false, 'message' => 'Facebook Graph test failed: ' . ($res['error'] !== '' ? $res['error'] : 'invalid token'));
	}
	$name = (string) ($res['json']['name'] ?? $res['json']['id']);
	$hint = $pageId === '' ? ' Tip: save Page / Business ID for publishing.' : '';
	return array('ok' => true, 'message' => 'Facebook Graph OK — connected as ' . $name . '.' . $hint);
}

/**
 * @param array<string, string> $cred
 * @return array{ok:bool,message:string}
 */
function epc_social_meta_test_instagram(array $cred): array
{
	$token = (string) ($cred['access_token'] ?? '');
	$igUserId = (string) ($cred['ig_user_id'] ?? '');
	if ($igUserId === '') {
		$igUserId = (string) ($cred['page_id'] ?? '');
	}
	if ($token === '' || $igUserId === '') {
		return array('ok' => false, 'message' => 'Instagram needs access token + Instagram Business user ID (Page / Business ID field).');
	}
	$v = epc_social_graph_version();
	$url = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($igUserId)
		. '?fields=id,username&access_token=' . rawurlencode($token);
	$res = epc_social_http_json('GET', $url);
	if (!$res['ok'] || empty($res['json']['id'])) {
		return array('ok' => false, 'message' => 'Instagram Graph test failed: ' . ($res['error'] !== '' ? $res['error'] : 'invalid token or IG user id'));
	}
	$uname = (string) ($res['json']['username'] ?? $res['json']['id']);
	return array('ok' => true, 'message' => 'Instagram Graph OK — @' . $uname . ' (Business/Creator).');
}

/**
 * @param array<string, string> $cred
 * @return array{ok:bool,message:string,privacy_options?:array}
 */
function epc_social_tiktok_test(array $cred): array
{
	$token = (string) ($cred['access_token'] ?? '');
	if ($token === '') {
		return array('ok' => false, 'message' => 'TikTok needs a user access token with video.publish scope.');
	}
	$url = 'https://open.tiktokapis.com/v2/post/publish/creator_info/query/';
	$res = epc_social_http_json('POST', $url, array('Authorization: Bearer ' . $token), array());
	if (!$res['ok']) {
		return array('ok' => false, 'message' => 'TikTok API test failed: ' . ($res['error'] !== '' ? $res['error'] : 'token rejected'));
	}
	$data = $res['json']['data'] ?? array();
	$name = (string) ($data['creator_username'] ?? $data['creator_nickname'] ?? 'creator');
	$opts = isset($data['privacy_level_options']) && is_array($data['privacy_level_options'])
		? $data['privacy_level_options'] : array();
	$msg = 'TikTok API OK — @' . $name . '.';
	if ($opts) {
		$msg .= ' Allowed privacy: ' . implode(', ', $opts) . '.';
	}
	$msg .= ' Unaudited apps can only post SELF_ONLY (private).';
	return array('ok' => true, 'message' => $msg, 'privacy_options' => $opts);
}

/**
 * @return array{ok:bool,message:string,external_post_id?:string,platform?:string}
 */
function epc_social_publish_draft(PDO $pdo, string $siteKey, int $draftId): array
{
	epc_social_ensure_schema($pdo);
	$st = $pdo->prepare(
		'SELECT * FROM `epc_social_post_drafts` WHERE `id` = ? AND `site_key` = ? LIMIT 1'
	);
	$st->execute(array($draftId, $siteKey));
	$draft = $st->fetch(PDO::FETCH_ASSOC);
	if (!$draft) {
		return array('ok' => false, 'message' => 'Draft not found.');
	}
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($draft['platform'] ?? '')));
	$caption = epc_social_compose_caption((string) ($draft['caption'] ?? ''), (string) ($draft['hashtags'] ?? ''));
	$mediaUrl = trim((string) ($draft['media_url'] ?? ''));

	$pdo->prepare(
		'UPDATE `epc_social_post_drafts` SET `status` = ?, `last_error` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
	)->execute(array('publishing', '', time(), $draftId, $siteKey));

	if ($platform === 'facebook') {
		$result = epc_social_meta_publish_facebook($pdo, $siteKey, $caption, $mediaUrl);
	} elseif ($platform === 'instagram') {
		$result = epc_social_meta_publish_instagram($pdo, $siteKey, $caption, $mediaUrl);
	} elseif ($platform === 'tiktok') {
		$result = epc_social_tiktok_publish_video($pdo, $siteKey, $caption, $mediaUrl);
	} else {
		$result = array('ok' => false, 'message' => 'Live publish supports Facebook, Instagram, and TikTok only.');
	}

	$now = time();
	if (!empty($result['ok'])) {
		$pdo->prepare(
			'UPDATE `epc_social_post_drafts`
			 SET `status` = ?, `external_post_id` = ?, `published_at` = ?, `last_error` = ?, `updated_at` = ?
			 WHERE `id` = ? AND `site_key` = ?'
		)->execute(array(
			'published',
			(string) ($result['external_post_id'] ?? ''),
			$now,
			'',
			$now,
			$draftId,
			$siteKey,
		));
		$result['platform'] = $platform;
		return $result;
	}

	$pdo->prepare(
		'UPDATE `epc_social_post_drafts` SET `status` = ?, `last_error` = ?, `updated_at` = ? WHERE `id` = ? AND `site_key` = ?'
	)->execute(array('error', (string) ($result['message'] ?? 'Publish failed'), $now, $draftId, $siteKey));
	return $result;
}

/**
 * Publish from pack/compose fields without a saved draft id.
 *
 * @return array{ok:bool,message:string,external_post_id?:string,draft_id?:int}
 */
function epc_social_publish_now(PDO $pdo, string $siteKey, array $data): array
{
	$platform = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($data['platform'] ?? '')));
	$title = trim((string) ($data['title'] ?? 'Published post'));
	$caption = trim((string) ($data['caption'] ?? ''));
	$hashtags = trim((string) ($data['hashtags'] ?? ''));
	$mediaUrl = trim((string) ($data['media_url'] ?? ''));
	if ($platform === '' || ($caption === '' && $mediaUrl === '')) {
		return array('ok' => false, 'message' => 'Platform plus caption or media URL is required.');
	}
	$save = epc_social_save_draft($pdo, $siteKey, array(
		'platform' => $platform,
		'title' => $title !== '' ? $title : ('Publish ' . $platform),
		'caption' => $caption,
		'hashtags' => $hashtags,
		'media_url' => $mediaUrl,
	));
	if (empty($save['ok'])) {
		return $save;
	}
	$st = $pdo->prepare(
		'SELECT `id` FROM `epc_social_post_drafts` WHERE `site_key` = ? ORDER BY `id` DESC LIMIT 1'
	);
	$st->execute(array($siteKey));
	$draftId = (int) $st->fetchColumn();
	if ($draftId <= 0) {
		return array('ok' => false, 'message' => 'Could not create draft for publish.');
	}
	$result = epc_social_publish_draft($pdo, $siteKey, $draftId);
	$result['draft_id'] = $draftId;
	return $result;
}

/**
 * @return array{ok:bool,message:string,external_post_id?:string}
 */
function epc_social_meta_publish_facebook(PDO $pdo, string $siteKey, string $caption, string $mediaUrl): array
{
	$cred = epc_social_account_credentials($pdo, $siteKey, 'facebook');
	$token = (string) ($cred['access_token'] ?? '');
	$pageId = (string) ($cred['page_id'] ?? '');
	if ($token === '' || $pageId === '') {
		return array('ok' => false, 'message' => 'Facebook publish needs Page access token + Page ID.');
	}
	$v = epc_social_graph_version();

	if ($mediaUrl !== '' && epc_social_is_video_url($mediaUrl)) {
		$url = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($pageId) . '/videos';
		$payload = array(
			'file_url' => $mediaUrl,
			'description' => $caption,
			'access_token' => $token,
		);
	} elseif ($mediaUrl !== '') {
		$url = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($pageId) . '/photos';
		$payload = array(
			'url' => $mediaUrl,
			'caption' => $caption,
			'access_token' => $token,
		);
	} else {
		$url = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($pageId) . '/feed';
		$payload = array(
			'message' => $caption,
			'access_token' => $token,
		);
	}

	$res = epc_social_http_json('POST', $url, array(), $payload, 60, array('form' => true));
	if (!$res['ok']) {
		return array('ok' => false, 'message' => 'Facebook publish failed: ' . ($res['error'] !== '' ? $res['error'] : 'API error'));
	}
	$postId = (string) ($res['json']['id'] ?? $res['json']['post_id'] ?? '');
	return array(
		'ok' => true,
		'message' => 'Published to Facebook Page.' . ($postId !== '' ? ' ID: ' . $postId : ''),
		'external_post_id' => $postId,
	);
}

/**
 * Instagram Content Publishing API — image or Reels video.
 *
 * @return array{ok:bool,message:string,external_post_id?:string}
 */
function epc_social_meta_publish_instagram(PDO $pdo, string $siteKey, string $caption, string $mediaUrl): array
{
	$cred = epc_social_account_credentials($pdo, $siteKey, 'instagram');
	$token = (string) ($cred['access_token'] ?? '');
	$igUserId = (string) ($cred['ig_user_id'] ?? '');
	if ($igUserId === '') {
		$igUserId = (string) ($cred['page_id'] ?? '');
	}
	if ($token === '' || $igUserId === '') {
		return array('ok' => false, 'message' => 'Instagram publish needs access token + Instagram Business user ID.');
	}
	if ($mediaUrl === '' || !preg_match('#^https?://#i', $mediaUrl)) {
		return array('ok' => false, 'message' => 'Instagram requires a public https media_url (image or .mp4 Reel).');
	}

	$v = epc_social_graph_version();
	$createUrl = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($igUserId) . '/media';
	$payload = array(
		'caption' => $caption,
		'access_token' => $token,
	);
	if (epc_social_is_video_url($mediaUrl)) {
		$payload['media_type'] = 'REELS';
		$payload['video_url'] = $mediaUrl;
		$payload['share_to_feed'] = true;
	} else {
		$payload['image_url'] = $mediaUrl;
	}

	$create = epc_social_http_json('POST', $createUrl, array(), $payload, 60, array('form' => true));
	if (!$create['ok'] || empty($create['json']['id'])) {
		return array('ok' => false, 'message' => 'Instagram media create failed: ' . ($create['error'] !== '' ? $create['error'] : 'no container id'));
	}
	$containerId = (string) $create['json']['id'];

	$statusUrl = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($containerId)
		. '?fields=status_code,status&access_token=' . rawurlencode($token);
	$ready = false;
	$lastStatus = '';
	for ($i = 0; $i < 24; $i++) {
		$poll = epc_social_http_json('GET', $statusUrl);
		$lastStatus = (string) ($poll['json']['status_code'] ?? $poll['json']['status'] ?? '');
		if (strtoupper($lastStatus) === 'FINISHED' || strtoupper($lastStatus) === 'PUBLISHED') {
			$ready = true;
			break;
		}
		if (strtoupper($lastStatus) === 'ERROR' || strtoupper($lastStatus) === 'EXPIRED') {
			return array('ok' => false, 'message' => 'Instagram container error: ' . $lastStatus);
		}
		// Images are often ready immediately; videos need processing time.
		usleep(epc_social_is_video_url($mediaUrl) ? 2500000 : 400000);
	}
	if (!$ready && epc_social_is_video_url($mediaUrl)) {
		return array('ok' => false, 'message' => 'Instagram video still processing (status: ' . ($lastStatus !== '' ? $lastStatus : 'unknown') . '). Try Publish again in a minute.');
	}

	$pubUrl = 'https://graph.facebook.com/' . rawurlencode($v) . '/' . rawurlencode($igUserId) . '/media_publish';
	$pub = epc_social_http_json('POST', $pubUrl, array(), array(
		'creation_id' => $containerId,
		'access_token' => $token,
	), 60, array('form' => true));
	if (!$pub['ok'] || empty($pub['json']['id'])) {
		return array('ok' => false, 'message' => 'Instagram media_publish failed: ' . ($pub['error'] !== '' ? $pub['error'] : 'no media id'));
	}
	$mediaId = (string) $pub['json']['id'];
	return array(
		'ok' => true,
		'message' => 'Published to Instagram.' . ($mediaId !== '' ? ' Media ID: ' . $mediaId : ''),
		'external_post_id' => $mediaId,
	);
}

/**
 * TikTok Direct Post via PULL_FROM_URL (requires verified domain + user consent).
 *
 * @return array{ok:bool,message:string,external_post_id?:string}
 */
function epc_social_tiktok_publish_video(PDO $pdo, string $siteKey, string $caption, string $mediaUrl): array
{
	$cred = epc_social_account_credentials($pdo, $siteKey, 'tiktok');
	$token = (string) ($cred['access_token'] ?? '');
	if ($token === '') {
		return array('ok' => false, 'message' => 'TikTok publish needs a user access token.');
	}
	if ($mediaUrl === '' || !preg_match('#^https?://#i', $mediaUrl) || !epc_social_is_video_url($mediaUrl)) {
		return array('ok' => false, 'message' => 'TikTok requires a public https video URL (.mp4/.mov). Verify the domain in TikTok Developer Portal.');
	}

	$privacy = strtoupper((string) ($cred['privacy_level'] ?? 'SELF_ONLY'));
	$allowedPrivacy = array('PUBLIC_TO_EVERYONE', 'MUTUAL_FOLLOW_FRIENDS', 'FOLLOWER_OF_CREATOR', 'SELF_ONLY');
	if (!in_array($privacy, $allowedPrivacy, true)) {
		$privacy = 'SELF_ONLY';
	}

	$initUrl = 'https://open.tiktokapis.com/v2/post/publish/video/init/';
	$payload = array(
		'post_info' => array(
			'title' => mb_substr($caption, 0, 2200),
			'privacy_level' => $privacy,
			'disable_duet' => false,
			'disable_comment' => false,
			'disable_stitch' => false,
			'video_cover_timestamp_ms' => 1000,
		),
		'source_info' => array(
			'source' => 'PULL_FROM_URL',
			'video_url' => $mediaUrl,
		),
	);

	$init = epc_social_http_json(
		'POST',
		$initUrl,
		array(
			'Authorization: Bearer ' . $token,
			'Content-Type: application/json; charset=UTF-8',
		),
		$payload,
		60
	);
	if (!$init['ok']) {
		$hint = ' Unaudited apps must use privacy SELF_ONLY and a private TikTok account.';
		return array('ok' => false, 'message' => 'TikTok init failed: ' . ($init['error'] !== '' ? $init['error'] : 'API error') . $hint);
	}
	$publishId = (string) ($init['json']['data']['publish_id'] ?? '');
	if ($publishId === '') {
		return array('ok' => false, 'message' => 'TikTok init returned no publish_id.');
	}

	$statusUrl = 'https://open.tiktokapis.com/v2/post/publish/status/fetch/';
	$last = '';
	for ($i = 0; $i < 20; $i++) {
		$poll = epc_social_http_json(
			'POST',
			$statusUrl,
			array(
				'Authorization: Bearer ' . $token,
				'Content-Type: application/json; charset=UTF-8',
			),
			array('publish_id' => $publishId)
		);
		$last = (string) ($poll['json']['data']['status'] ?? '');
		if (in_array($last, array('PUBLISH_COMPLETE', 'FAILED', 'SEND_TO_USER_INBOX'), true)) {
			break;
		}
		usleep(2000000);
	}

	if ($last === 'FAILED') {
		$failMsg = (string) ($poll['json']['data']['fail_reason'] ?? 'publish failed');
		return array('ok' => false, 'message' => 'TikTok publish failed: ' . $failMsg);
	}
	if ($last === 'SEND_TO_USER_INBOX') {
		return array(
			'ok' => true,
			'message' => 'TikTok sent video to creator inbox (inbox mode). Open TikTok app to finish posting.',
			'external_post_id' => $publishId,
		);
	}
	if ($last !== 'PUBLISH_COMPLETE' && $last !== '') {
		return array(
			'ok' => true,
			'message' => 'TikTok accepted publish (status: ' . $last . '). Check the TikTok app if not visible yet.',
			'external_post_id' => $publishId,
		);
	}

	return array(
		'ok' => true,
		'message' => 'Published to TikTok (' . $privacy . '). Publish ID: ' . $publishId,
		'external_post_id' => $publishId,
	);
}
